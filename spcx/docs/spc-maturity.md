# SPC Maturity Scope — Bespoke Pipeline → Scientific-Computing Primitive

> **Status:** DRAFT v0.1 — initial scope capture
> **Date:** 2026-05-04
> **Purpose:** Define the renovation that reshapes SPC from its current bespoke-pipeline form into a scientific-computing primitive analogous to the matured `GaussianMixtureModel`. Independent axis from the state engine; can land in parallel.
> **Sibling document:** [state-engine-design.md](./state-engine-design.md) covers the persistence engine.

---

## 1. Why this scope exists

`GaussianMixtureModel` (in `src/gmm/`) has the shape of a scientific-computing primitive: stateful object, decoupled init / fit / inference, full inference surface (`Predict`, `PredictProba`, `Pdf`, `Mahal`, `Sample`), zero-allocation hot path. It is usable on its own without ever touching SPC.

SPC is still bespoke. `SpcBatch.Run` is a static entry point that bundles graph build + sweep + checkpoint dispatch. The polymorphic `SpcBatchRequest` (`SimHashes`/`Features`/`Documents`) embeds metric concerns in the request DTO. The runtime hot path conflates SW per-sweep "colors" with equilibrium cluster assignments. There is no separate model-object surface for inference after a fit.

This scope brings SPC up to GMM's level of structural maturity, fixes correctness issues that have surfaced during design review, and prepares the runtime for integration with the state engine.

## 2. Target API shape

A stateful `SuperParamagneticClustering` model class, analogous to `GaussianMixtureModel`. Sketch (names tentative):

```csharp
// Construction with explicit metric/proximity/kernel choices.
var spc = new SuperParamagneticClustering(
    temperatures: new[] { 0.01, 0.05, 0.1, ..., 5.0 },
    q: 20,
    metric: SpcMetric.Euclidean,
    proximity: SpcProximity.Knn,
    couplingKernel: CouplingKernel.Gaussian,
    k: 10);

// Two init paths, mirroring GMM's InitializeWithParameters vs RandomInitialize.
spc.InitializeFromFeatures(features);              // builds the graph internally
// OR
spc.InitializeWithGraph(precomputedCsrGraph);      // bring-your-own-graph

// Fit. Two modes: eager (no supervisor) vs. supervised (epoch rendezvous).
spc.Fit(steps: 400);
// OR
spc.Fit(steps: 400, supervisor: myTerminationOracle);

// Inference surface — all post-fit, all read-only.
double[][] equilibriumClusterCoMatrix = spc.GetCorrelationMatrix(T);
int[]      labels                     = spc.GetEquilibriumClusters(T, threshold: 0.5);
double     chi                        = spc.GetSusceptibility(T);                  // FK estimator
int[]      arrival                    = spc.GetPercolationArrival(T);
double[]   chiProfile                 = spc.GetSusceptibilityProfile();
double[]   criticalTs                 = spc.GetCriticalTemperatures(prominence: 0.1);
int[]      clusterCounts              = spc.GetClusterCounts();

// Resume / replay from persisted state (when state engine is wired).
spc.WarmStartFromCheckpoint(runReader);
```

Key shape contracts (mirror GMM):

- **Stateful** — holds `Temperatures`, `Q`, `Graph`, per-T simulation states, accumulators.
- **Init decoupled from fit** — graph build is one method; SW evolution is another.
- **Inference decoupled from fit** — derived signals are properties / methods on the model after `Fit` returns.
- **Metric is a property of the model**, not embedded in every fit call. Polymorphic `SpcBatchRequest` shape goes away.
- **`SpcBatch.Run`** becomes a thin convenience wrapper that constructs a model, calls `InitializeFromFeatures`, calls `Fit`, returns a structured result.

## 3. Per-temperature primitive

`PottsModel` is upgraded to be the per-temperature analog of `GaussianComponent`:

- Holds: spin state, RNG state, running accumulators, equilibration phase marker.
- `Step()` advances one SW sweep (atomic unit).
- Pre-allocated scratch (analog to GMM's `ScratchMean` / `ScratchCov`): union-find, label arrays, cluster map, bond-probability cache.
- Exposes accumulator state for read-only consumption: `BondClusterSizeMoments`, `ClusterCount`, `RunningSusceptibility`, optional `CooccurrenceAccumulator`.

The `SuperParamagneticClustering` outer model orchestrates an array of `PottsModel[]`, one per temperature, just as `GaussianMixtureModel` orchestrates `Components[K]`.

## 4. Three execution modes

Collapse to one mechanism with two knobs (epoch granularity, supervisor coupling):

| Mode | epoch_sweeps | Supervisor | Termination |
|---|---|---|---|
| **Eager** (default; classical online) | 1 | none — kernel reads its own running accumulators | sweep budget or in-loop convergence |
| **Supervised** | configurable | in-process callback or out-of-process directory observer | supervisor decides at rendezvous, signaled via `supervisor/termination-signal/v1` artifact |
| **Headless** | full run as one epoch | async / out-of-process post hoc | sweep budget; analysis post-mortem |

`Fit(steps)` is eager by default. `Fit(steps, supervisor)` runs supervised. Headless is supervised with the supervisor decoupled in time.

The supervisor's termination signal is an artifact (when state engine is wired) or a callback return value (when supervised in-process without engine). Both shapes coexist.

## 5. Observable cost discipline

The kernel always runs O(N)-per-sweep accumulators regardless of mode. They are essentially free given the simulation's existing per-sweep cost.

**Always on:**
- Bond-cluster size moments (the FK χ accumulator).
- Cluster-size histogram bins.
- Running cluster count.

**Configurable on (modest cost):**
- Equilibrium correlation matrix accumulator (sparse, optional, only on if `EquilibriumCorrelation = true` in run config; quadratic in N for large data).

**Always off in the kernel; supervisor or post-hoc only:**
- Fan-out KL.
- Cross-T peak detection.
- Anything requiring multiple temperatures' state simultaneously.

This is independent of supervision mode. Whether running values are *acted on* in real time is the supervisor's question; whether they are *computed* is the cost discipline's question.

## 6. Partial-temperature epoch contract

A partial-T epoch is a contiguous run of `epoch_sweeps` SW sweeps at one temperature, where `epoch_sweeps < total_sweeps_for_T`. Sweeps are atomic — `epoch_sweeps = 1` is the finest meaningful granularity.

### 6.1 Resumable state per (model, T)

Persistent across epoch boundaries:
- **Spin array** `int[N]` — the only spin state needed to resume.
- **Sweep counter within T** — cumulative completed sweeps.
- **RNG state** — see §8.
- **Running accumulators** — bond-cluster size moments, cluster-size histogram, equilibrium correlation accumulator (if enabled).
- **Equilibration phase marker** — `{burn-in, sampling}`. Affects whether sampled statistics are valid.

Ephemeral (recomputed each sweep, never persisted):
- Bond probability array (derived from `Graph.Weights` and `T`).
- Union-find scratch state.
- Cluster label / cluster map work arrays.

### 6.2 Per-epoch artifact bundle

Three artifacts per (T, epoch) in supervised / headless modes:

1. **Spin observation** (`spc/spin-observation/v3`) — anchor frame for first epoch of T, delta frame thereafter.
2. **Per-epoch summary** (`spc/temperature-summary/v1`) — small struct: `{temperature, epoch_index, cumulative_sweep_count, is_complete, equilibration_phase, cluster_count, running_chi, running_cluster_size_moments, current_spins_artifact_id}`.
3. **Optional bond-cluster size sample** (`spc/bond-cluster-sample/v1`) — raw `int[]` of cluster sizes from the last sweep of this epoch, for variance estimation and percolation visualization.

`is_complete = true` only on the final epoch of T. Partial epochs are `false`.

Running accumulators are *snapshotted* into the summary at each boundary, not reset. The accumulator state stays in memory across epoch boundaries (eager case) or is carried in the resumable state blob (resume after a process restart). Per-epoch χ is derivable post-hoc as a running-cumulative difference.

### 6.3 Resume semantics

On resume, the driver:

1. Walks the manifest to find the latest per-T summary.
2. If `is_complete`, skip this T entirely.
3. Otherwise:
   - Materialize spin state from `current_spins_artifact_id` (anchor + delta walk).
   - Restore running accumulators from the summary's snapshotted values.
   - Restore RNG state.
   - Calculate remaining sweeps as `total_sweeps - cumulative_sweep_count`.
   - Continue SW from the loaded spin state for the remaining sweeps, with epoch boundaries continuing to fire at `epoch_sweeps` intervals.

Matches the warm-start logic shape in [src/spc.batch.cs:200-251](../src/spc.batch.cs:200), but with running accumulators added to the resumable state.

## 7. Cross-temperature synchronization

Two reasonable scheduling models:

- **Independent** (current `Parallel.For`) — each T advances on its own pace; supervisor sees mixed-progress state at any instant. Faster, simpler.
- **Synchronized** (round-robin) — all Ts complete epoch K before any advances to K+1; supervisor sees coherent cross-T state. Slower (outer barrier), enables round-robin scheduling.

Configurable axis. Default independent; opt into synchronized when round-robin supervision is needed.

This is the gap-analysis #10 item — the persistence schema is ready for round-robin (each per-epoch summary already has `EpochCount`), but the runtime currently lacks the outer barrier. Synchronized mode adds it.

## 8. RNG state portability

`System.Random` doesn't expose its internal state. After a resume, fresh seeding produces a different stochastic trajectory than uninterrupted-run continuation — statistically equivalent (we sample from equilibrium either way), but bit-identical reproducibility breaks.

Fix: replace with a stateful RNG that supports `GetState()` / `SetState()`. Candidates:

- **xoshiro256++** — fast, statistically excellent, small state (4 × 64-bit). Standard in modern scientific computing.
- **PCG (PCG64)** — also good; comparable performance and quality.
- **Mersenne Twister** — historical default in MATLAB/NumPy, large state (~625 × 32-bit), slower than xoshiro/PCG, no real advantage today.

Probably xoshiro256++. Small isolated change; lives in the linalg / numerics utility area or as a dedicated `Rng` primitive.

This decision is recorded as open until the implementation lands; nothing in the engine or codec design depends on the specific choice.

## 9. Termination authority

| Mode | Authority | Mechanism |
|---|---|---|
| Eager | SW kernel | Sweep budget reached, or in-loop convergence test triggers |
| Supervised | Supervisor at epoch rendezvous | Returns termination signal, or publishes `supervisor/termination-signal/v1` artifact |
| Headless | SW kernel | Sweep budget reached; supervisor analysis is post-mortem |

In supervised mode, the SW driver polls for the termination signal at every epoch rendezvous before advancing. This is a cooperative-cancellation interface with the same shape as the existing `CancellationToken` plumbing, just driven by a policy function instead of an external event.

A termination signal is a configuration of `{action: stop|continue|advance-T|advance-handoff, reason, evaluatedAtEpoch}`. The model treats it as advisory and applies the action at the next epoch boundary.

## 10. Correctness items

Bundled into this scope rather than landed as one-offs.

### 10.1 FK susceptibility from bond-cluster sizes

[src/spc-thermo/chi.cs:41-59](../src/spc-thermo/chi.cs:41) currently buckets by spin label:

```csharp
counts[spin] = count + 1;          // bucket by color (Q=20)
sumSquares += count * count;
return sumSquares / spins.Length;  // χ from color buckets — biased
```

With Q=20 and many more bond-clusters in the disordered phase, multiple distinct bond-clusters share a color. By Cauchy-Schwarz, summing squares of color-bucket totals gives a positive bias relative to summing squares of true cluster sizes.

The canonical Fortuin-Kasteleyn estimator is:

```
χ(T) = (1/N) · Σ_c |c|²
```

where `c` ranges over actual bond-clusters from the SW union-find pass, averaged over equilibrium sweeps.

Fix:
- `FastUnionFind` exposes a non-allocating root-size enumeration: `WriteRootSizesTo(Span<int>) → int count`.
- `PottsModel` accumulates `Σ |c|²` per sweep using that enumeration.
- The accumulator is the source of `RunningSusceptibility` and the per-epoch `running_chi`.
- Old `SpcAnalysis.ComputeSusceptibility(int[] spins)` is removed. The artifact-based `AnalyzeCheckpointSusceptibility` is rewritten to read the accumulator-derived running value from per-epoch summaries (or, for offline-only, derived from `spc/bond-cluster-sample/v1` artifacts when the cheap accumulator wasn't enabled at run time).

### 10.2 Histogram and analysis cleanups

`SpcAnalysis.BuildHistograms` and other label-bucketed code in [src/spc.thermo.cs](../src/spc.thermo.cs) inherit the same conflation. Same shift to bond-cluster basis, or — where label-bucket histograms are actually what's wanted (visualization of color jitter) — rename to make the distinction explicit.

### 10.3 SW-color vs equilibrium-cluster semantics

`SpcBatchResult.FinalSpins[T]` is currently treated as "the clusters at T" by downstream code. It is actually an instantaneous snapshot of per-sweep colors and is only valid below T_c.

Fix:
- The model exposes both: `GetSpinSnapshot(T)` (the color view; honest about what it is) and `GetEquilibriumClusters(T, threshold)` (the connected components of the thresholded co-occurrence matrix; the proper cluster assignments).
- Codec inventory in the state engine doc reflects this split: `spc/spin-observation/v3` is the SW-color view; `thermo/equilibrium-clusters/v1` is the cluster-assignment view.
- Documentation strings on the SPC model surface call out which is which.

## 11. New computational primitives

Land alongside the API renovation:

- **Bond-cluster size accumulator** — running `Σ |c|²` and per-sweep cluster-size histogram, accumulated inside `PottsModel.Step()`.
- **Equilibrium correlation matrix accumulator** — sparse `<δ(sᵢ, sⱼ)>` per pair, averaged over sampling sweeps. Optional (configurable per run); cost is roughly `O(N · avg_neighbors)` per sweep using sparse representation.
- **Equilibrium-cluster derivation** — threshold the correlation matrix at `θ` (default 0.5), take connected components → `int[N]` cluster assignments. Cheap post-hoc operation.
- **Per-point percolation arrival index** — `int[N]` per T; "first sweep at which point i acquired its final-sweep label" (or stability-window or structural variant). Computed at T completion from the in-memory spin history; persisted via `spc/temperature-final-state/v1`.

## 12. Hot-path discipline

Mirroring the GMM zero-allocation pattern:

- No per-sweep heap allocations inside `PottsModel.Step()`.
- All scratch arrays pre-allocated at construction (union-find, labels, cluster map, bond probabilities).
- Accumulator state lives on the `PottsModel` instance, not in lambda closures.
- Parallel-temperature dispatch moves out of the runtime core (`SpcBatch.RunSimulationCore`) and into the driver layer. The `SuperParamagneticClustering.Fit` orchestrates the temperature loop; in supervised synchronous mode it runs an explicit barrier per epoch; in independent mode it dispatches each T's `PottsModel.Step()` calls via `Parallel.For` over Ts.

## 13. Drop polymorphic request DTO

Today `SpcBatchRequest` carries `SimHashes` / `Features` / `Documents` / `CovarianceInverse` polymorphically, and metric/proximity/kernel choices live on the request rather than on a model object. This is a smell: every `Fit` call carries data shape and metric choice together.

Fix:
- Metric, proximity, kernel become properties of `SuperParamagneticClustering`, set at construction.
- `Fit` takes `double[][] features` (or `ulong[]` for SimHash) — uniform shape per metric.
- The current `SpcBatchRequest` shape can survive as a *convenience wrapper* DTO that constructs a model and calls `Fit`, for callers who like the existing API surface, but it ceases to be the canonical entry.

This matches GMM's pattern: `Fit(double[][] data)` with model state already established.

## 14. Phasing

This scope is independent of the state engine. Suggested order:

1. **API + per-temperature primitive renovation** — `SuperParamagneticClustering` model class, `PottsModel` upgraded to per-T primitive, `SpcBatch.Run` becomes thin wrapper.
2. **Correctness fixes** — FK susceptibility, histogram cleanup, SW-color vs equilibrium-cluster semantics. Lands against existing checkpoint scaffolding (or with checkpointing temporarily disabled).
3. **New computational primitives** — bond-cluster accumulator, equilibrium correlation, percolation arrival.
4. **Three execution modes** — eager / supervised / headless plumbing. Termination signal interface.
5. **RNG state portability** — small isolated swap.
6. **Cross-T synchronization** — independent (default) and synchronized (round-robin) scheduling modes.
7. **State-engine integration** — once the engine substrate is in place ([state-engine-design.md](./state-engine-design.md) phases 1–4), the SPC codecs land and `SpcBatch.Run` migrates to checkpoint-as-handoff.

Steps 1–6 can land entirely against the existing scaffolding. Step 7 merges this work into the new engine.

## 15. Open questions

- **RNG choice** — xoshiro256++ vs PCG64. Probably xoshiro; not blocking.
- **Equilibrium correlation memory budget for large N** — sparse representation handles modest N well; for N > ~50,000 needs a streaming or chunked approach. Implementation detail of the correlation accumulator.
- **Equilibration detection** — automatic via per-sweep cluster-size variance, or user-declared burn-in budget? Probably both: automatic detection emits a flag in the per-epoch summary, but user-declared burn-in is the contractual default.
- **Equilibrium-cluster threshold** — fixed default (0.5) or per-run configurable? Probably configurable with a sensible default.
- **Sample-window definition** — within a T's sweep budget, where does burn-in end and sampling begin? Default: discard first 25%; configurable per run.
- **Round-robin scheduling internals** — single-thread iterating Ts vs. barrier with parallel-T inside each epoch. Latter parallelizes better; former is simpler and sufficient for small T-counts.

## 16. Relationship to current code

| Current | Becomes |
|---|---|
| [src/spc.batch.cs](../src/spc.batch.cs) `SpcBatch.Run` static entry | Thin convenience wrapper over `new SuperParamagneticClustering(...).Fit(features)` |
| [src/spc.batch.cs](../src/spc.batch.cs) `SpcBatchRequest` polymorphism | Metric/proximity/kernel become model properties; `features` is uniform per metric |
| [src/spc.potts.cs](../src/spc.potts.cs) `PottsModel.RunSimulation` | `PottsModel.Step()` (one sweep) + accumulator state on the instance |
| [src/spc.potts.cs](../src/spc.potts.cs) `FastUnionFind` | Adds non-allocating `WriteRootSizesTo(Span<int>)` |
| [src/spc-thermo/chi.cs](../src/spc-thermo/chi.cs) `ComputeSusceptibility(int[])` | Removed; FK accumulator on `PottsModel` provides the value |
| [src/spc.thermo.cs](../src/spc.thermo.cs) `BuildHistograms` (label-bucketed) | Reframed as cluster-size histograms, sourced from accumulator |
| `SpcBatchResult.FinalSpins[T]` | Stays for diagnostic access (color view); a separate `EquilibriumClusters[T]` exposes the cluster-assignment view |
| `Parallel.For` over Ts in `RunSimulationCore` | Moves to `SuperParamagneticClustering.Fit`; configurable independent vs. synchronized |
| `System.Random` in `PottsModel` | Replaced with stateful RNG (xoshiro256++ proposed) |

## 17. Non-goals

- Distributed simulation (cross-machine SW). Single-process, multi-threaded only.
- Variable-Q within a run. `Q` is fixed at construction.
- Online metric switching. Metric is fixed at construction.
- Auto-tuning of temperature schedule. User-specified ladder for v1; adaptive scheduling is a future concern.

---

> **Next steps:** validate framing, then execute Phase 1 of §14. The renovation lands against existing scaffolding; it does not depend on the state engine but converges with it at Phase 7.
