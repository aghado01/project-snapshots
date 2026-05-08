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

### 16.1 Current checkpoint on-disk shape (debt)

Captured for orientation; the shape is debt and will be replaced when the state-engine substrate lands (see [state-engine-design.md](./state-engine-design.md) §29 for the canonical debt list and §26 for the target layout).

`SpcBatchRequest.CheckpointDirectory` is a working root. Each run writes under `{root}/{yyyyMMdd_HHmmss}/` with payload-type folders — `temperature-checkpoints/`, `temperature-observations/`, `handoff-readiness/`, `gmm-handoff-state/` — plus `spc_{runStamp}.manifest.json` at the run root. Disk runs default to greedy non-redundant persistence: small JSON checkpoint summaries and compressed binary `.bin.br` spin delta-frame artifacts. `SpcBatchRequest.CheckpointPersistence` configures which artifact kinds are written; `EpochSweepCount` is run-start configuration that emits partial-temperature checkpoints for round-robin / supervisor duty cycles.

Partial temperature states use `IsComplete=false`, `CurrentSpinsArtifactId`, `EpochCount`, and `SweepCount`; only complete states populate the resume-skip path (see §6.3). Checkpoints do not persist susceptibility or other thermodynamic analysis values; supervisor-side thermodynamic checks live in `src/spc-thermo/` (e.g. `SpcAnalysis.AnalyzeCheckpointSusceptibility` in `chi.cs`). The manifest tracks relative path, source / previous / base artifact links, payload type, compression, encoding, sequence, byte length, and pipeline stage.

Treat this as transitional: it is the as-built behaviour, not the design target. New code should align with the state-engine codec contract (§24 of state-engine-design.md) rather than extending the current implementation.

## 17. Analysis, tree, and handoff layer

The model surface in §2 returns thermodynamic signals (`GetSusceptibilityProfile`, `GetCriticalTemperatures`, `GetEquilibriumClusters(T)`) but stops short of the analysis-level primitives a caller actually wants. This section captures the shape that closes that gap. Names are tentative; the structural decisions are not.

### 17.1 `SpcTreeMatrix` — the temperature-indexed merge tree

A first-class merge-tree artifact analogous to MATLAB's `linkage` `Z` matrix and waveclus's `tree` matrix. Shape: `[num_temps × (4 + max_clusters)]` with metadata columns `(temp_index, temperature, susceptibility, K)` followed by per-cluster sizes.

Cheaply derivable from the equilibrium cooccurrence matrix (`thermo/cooccurrence-matrix/v1`) via single-linkage on `1 - cooccurrence`; merge heights correspond to the temperatures at which clusters become distinguishable. This is a supervisor-side artifact; the SW hot path does not need new instrumentation. Lands as the SPC-side `thermo/merge-tree/v1` codec (see [state-engine-design.md](./state-engine-design.md)) using the MATLAB `Z` layout so existing validation tooling works on both synthetic and real outputs.

The tree carries the structural information that the chi profile alone does not: a susceptibility peak says *that* a transition happened at T*, the tree says *which clusters merged*. Without it, critical temperatures are scalars; with it, they are labeled events in a queryable dendrogram.

### 17.2 `SpcClusterProfile` — per-cluster identity across temperatures

The SPC sweep produces a tree of clusters, but the current analysis methods return arrays indexed by temperature only. The natural consumer wants signals indexed by `(temperature, clusterId)` pairs, where `clusterId` is a stable identity that persists across temperature steps until the cluster merges into its parent.

```csharp
public sealed class SpcClusterProfile
{
    public string  ClusterId { get; }          // stable identity across T steps
    public double  BirthTemperature { get; }   // T where it first appears
    public double  MergeTemperature { get; }   // T where it merges (NaN if survives)
    public double  Lifetime { get; }
    public int[]   DataIndices { get; }        // points at birth T

    public double[] KLProfile { get; }         // per-T within lifetime
    public double[] CoherenceProfile { get; }
    public int[]    FanOutProfile { get; }

    public double  StabilityScore { get; }     // integrates KL + coherence over lifetime
    public bool    IsStableLeaf { get; }       // survives to lowest T without splitting
}
```

`StabilityScore` is the SPC analog of a recursive-mixture local BIC — a per-node quality signal asking "does this cluster have genuine internal structure?" For well-separated Gaussian data the susceptibility peak and a local-BIC split decision agree; for non-Gaussian topology they diverge, and that divergence is itself diagnostic of cluster shape.

### 17.3 `SpcAnalysisReport` — lazy analysis wrapper

`SpcAnalysis.AnalyzeGlobalKL`, `AnalyzeFanOut`, and `AnalyzeRadialCoherence` currently exist as independent static passes that each re-derive medoids and histograms. There is no "I've analyzed this result and here is the composite picture" object. `SpcAnalysisReport` wraps `SpcBatchResult` (or its successor on the matured model) with lazily-computed analysis views and exposes the most-asked question — "just give me cluster labels" — as a one-liner:

```csharp
public sealed class SpcAnalysisReport
{
    public SpcBatchResult Simulation { get; }
    public SpcTreeMatrix  Tree     => _tree   ??= SpcTreeMatrix.FromBatchResult(Simulation);
    public GlobalKLResult GlobalKL => _kl     ??= SpcAnalysis.AnalyzeGlobalKL(Simulation);
    public FanOutResult   FanOut   => _fanOut ??= SpcAnalysis.AnalyzeFanOut(Simulation);
    public IReadOnlyList<SpcClusterProfile> ClusterProfiles { get; }

    public double[] CriticalTemperatures => GlobalKL.CriticalTemperatures;
    public int[] LabelsAtTemperature(double T);
    public int[] LabelsAtCriticalTemperature(int idx = 0);
}
```

### 17.4 `SpcHandoff` — the SPC→GMM bridge DTO

`ISpcShatterOracle` is correctly flagged as a placeholder until both engines mature. The intermediate primitive is a concrete handoff DTO that draws its initial conditions from the stable leaf profiles in §17.2, making the cut criterion explicit rather than implicit in whichever temperature happens to be chosen:

```csharp
public sealed class SpcHandoff
{
    public int       K { get; }
    public double    Temperature { get; }
    public int[]     CoreAssignments { get; }
    public double[][]   InitialMeans { get; }
    public double[][,]  InitialCovariances { get; }
    public double[]     InitialWeights { get; }

    public static SpcHandoff FromReport(SpcAnalysisReport report, double[][] features);
}
```

The handoff carries topological identity *into* the GMM but the GMM is responsible for preserving it on the way back out. When a single SPC cluster is tiled by multiple Gaussian components (the recursive variant of [gmm-maturity-extentions.md](./gmm-maturity-extentions.md)), the GMM exposes a `ManifoldMixture` wrapper holding a `ComponentToClusterMap` that records which flat components belong to which `SpcClusterProfile.ClusterId`. The flat `GaussianMixtureModel` itself stays topology-blind; the wrapper closes the loop.

### 17.5 Robust leaf estimation

`WeightedGeometricMedian`, `ManifoldMedian`, and `SigmaEstimatorMad` in `src/estimators/` are the natural inputs for the GMM warm-start. The SW process puts both core points (high spin coherence, well inside the cluster) and boundary points (low coherence, ambiguous assignment) in the same leaf profile. Inverse-distance weighted scatter centered on the geometric median, with the inverse-distance weights modulated by `SpcClusterProfile.CoherenceProfile`, is the right combination — pure inverse distance upweights boundary points that happen to be spatially near the core, but coherence-weighted scatter encodes the thermodynamic stability signal directly.

```csharp
public sealed class RobustLeafEstimate
{
    public int       LeafIndex { get; }
    public double[]  Center { get; }            // geometric / manifold median
    public double[,] ScatterMatrix { get; }     // tangent-space, coherence × inverse-distance weighted
    public double    Weight { get; }            // normalized size × mean coherence
    public double    CoherenceMass { get; }
    public bool      IsDegenerate { get; }      // N_k < d+1 or singular scatter
}

public static class RobustLeafEstimator
{
    public static RobustLeafEstimate[] Estimate(
        double[][]               data,
        SpcClusterProfile[]      leafProfiles,
        IRobustLocationEstimator locationEstimator,
        IRobustScatterEstimator  scatterEstimator);
}
```

`IRobustLocationEstimator` / `IRobustScatterEstimator` strategies let the same factory serve flat Euclidean features, product manifolds, and (later) full Riemannian charts without changing the call site. `IsDegenerate` is a first-class condition that routes to a regularized fallback (diagonal or shared covariance), not a regularization patch on the scatter matrix.

### 17.6 Cross-package partition-cutting

`SpcTreeMatrix` is one instance of a more general shape — an indexed sequence of partitions with a scoring axis (temperature here, BIC/depth for GMM, merge distance for hierarchical linkage). The general shape — `IPartitionSequence<TScore>`, `IPartitionCriterion<TScore>`, `CutResult`, `IClusterMembership` — is intentionally not part of `SpcCore`: it belongs in a future `Clustering` primitive that both SPC and GMM consume (see [project-primer.md](./project-primer.md)). SPC contributes the domain-specific criteria (`SusceptibilityPeakCriterion`, `KLDivergenceCriterion`, `FanOutCriterion`); the general criteria (`MaxK`, `Threshold`, `Elbow`) live in the shared layer.

The handoff between the two layers is `SpcTreeMatrix : IPartitionSequence<double>` — SPC implements the shared interface, the cutter is generic.

## 18. SPC vs adjacent methods (positioning note)

A common reaction to SPC's "graph + clustering" framing is to identify it with spectral or diffusion-map clustering, which also build a graph and derive cluster structure from it. The methods are related at one limit but are not the same algorithm; recording the distinction so later readers don't conflate them.

**Spectral and diffusion methods** build a fixed graph and derive a *deterministic* embedding from spectral properties of an operator on it (Laplacian for spectral; Markov-chain transition matrix for diffusion maps). Topology enters via the operator's spectrum — small Laplacian eigenvalues encode connectivity, eigenvectors expose connected components. The probabilistic structure of diffusion maps (random walks at fixed time t) is real but bounded: there is no sampling procedure, no exploration of an energy landscape, and no temperature parameter that exposes scale. The embedding dimension k is a hyperparameter the user sets.

**SPC** is an energy-based generative model over partitions. The Potts Hamiltonian H = -Σ J_ij δ(s_i, s_j) defines a Boltzmann distribution P(s | T) ∝ exp(-H/T) over the entire space of partition assignments, parameterized by T. Swendsen-Wang Monte Carlo samples from this distribution. Five things follow that have no spectral analog:

1. **A full distribution over partitions, not a regularizer.** The partition function — what exact Bayesian inference on partitions would integrate over — is what SW samples from. The Laplacian quadratic form is a smoothness penalty, not a probability distribution.

2. **Multi-scale structure discovered, not assumed.** A spectral method gives one topology, fixed by k. SPC produces a *family* of partition states across T, and the dynamics expose physically meaningful scales as a critical-adjacent regime — not a single point T_c but a goldilocks zone of temperatures where partition structure is informative for the dataset under the chosen graph + metric + kernel. Susceptibility peaks (FK estimator) are the classical first-pass signal, but they are one of several thermodynamic signatures: fan-out KL across temperature steps, Mahalanobis-based regime characterizations, cluster-count plateaus, and other signals can co-locate or differ across this band. With expanded graph constructions, distance metrics, and coupling kernels the precise shape of this regime is data-dependent and not collapsible to one number. The analysis layer in `src/spc-thermo/` (currently `chi.cs`, `kl.cs`, `mhbs.cs`) is intentionally extensible to additional thermodynamic analyses as the operating space grows; the framing here treats susceptibility as the canonical entry point, not the totality of what's available.

3. **Stochastic dynamics that explore the configuration landscape.** Per-point coherence (how stably a label holds across sweeps) is a Monte-Carlo-derived measure of cluster-identity stability with no deterministic-eigendecomposition analog. This is what the GMM handoff downstream consumes — boundary points downweighted by `RobustLeafEstimator` (§17.5) are *thermodynamically* unstable, oscillating between basins, not just spatially ambiguous.

4. **Phase-transition semantics for identity.** Stable partitions in the goldilocks regime are metastable basins of a generative process — local minima of the system's effective potential — rather than connected components of a graph. The former is closer to a generative-model notion of "real cluster" than the latter, and the temperature scan exposes how that identity emerges (or fails to emerge) under the chosen graph prior.

5. **Fortuin-Kasteleyn duality.** SPC has a percolation interpretation in the random-cluster representation: cluster discovery is bond formation under a chemical potential. FK susceptibility χ = (1/N)Σ |c|² measures actual bond-cluster sizes from the percolation process. Spectral methods have no percolation dual.

There is a known limit-case connection: in the paramagnetic / disordered limit (T ≫ critical regime), the Potts model is dominated by quadratic fluctuations around the uniform distribution, and standard spectral clustering can be recovered as a high-T linearization. SPC at finite temperatures inside the critical-adjacent regime is doing strictly more than this linearization captures.

This is also why the SPC → GMM pipeline is its own thing, not "manifold-aware clustering then density fit." The signals SPC produces — coherence, critical-regime structure, merge events tied to physical transitions, multi-pillar thermodynamic analyses — have no clean spectral analog and they are exactly what the GMM handoff consumes (see §17.4 / §17.5).

## 19. Non-goals

- Distributed simulation (cross-machine SW). Single-process, multi-threaded only.
- Variable-Q within a run. `Q` is fixed at construction.
- Online metric switching. Metric is fixed at construction.
- Auto-tuning of temperature schedule. User-specified ladder for v1; adaptive scheduling is a future concern.

---

> **Next steps:** validate framing, then execute Phase 1 of §14. The renovation lands against existing scaffolding; it does not depend on the state engine but converges with it at Phase 7.
