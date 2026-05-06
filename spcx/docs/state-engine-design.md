# SPC State Engine — Design

> **Status:** DRAFT v0.2 — adds bundling principle, sidecar pattern, three-layer type model, supervisor architecture, observable cost discipline, concrete GMM codec inventory.
> **Date:** 2026-05-04
> **Supersedes:** the resume-only framing baked into `src/spc.checkpoint.cs`.
> **Purpose:** define the target shape of the state management / checkpoint engine that will support SPC pause/resume, offline replay, supervisor-driven cumulative analysis, and SPC → GMM handoff. This document is the canonical target; the gap between this document and current code is *debt*, not API.
> **Sibling document:** [spc-maturity.md](./spc-maturity.md) covers the parallel SPC bespoke→scientific-package renovation.

---

## Table of contents

- [Part I — Engine (general-purpose state management)](#part-i--engine-general-purpose-state-management)
- [Part II — Runtime (SPC + GMM application of the engine)](#part-ii--runtime-spc--gmm-application-of-the-engine)
- [Part III — Open questions, phasing, debt](#part-iii--open-questions-phasing-debt)
- [Part IV — Decisions log](#part-iv--decisions-log)

---

# Part I — Engine (general-purpose state management)

The engine is a primitives library for persisting progressive computational state. It knows nothing about SPC, GMM, or any specific data type. Domains plug in by registering codecs.

## 1. Purpose and invariants

The engine must support:

- Pause/resume of long-running progressive computations.
- Offline replay of any historical state during or after a run.
- Cumulative analysis by external observers, integrating over the run-so-far.
- Cross-run composition — downstream analyses may reference data from multiple runs.
- Distributed observation — multiple readers concurrently, possibly remote.
- Forward-compatible schemas so future workflows can read older runs.
- Generic key-value persistence as a substrate; specific domains are specializations on top.

Invariants:

1. **Append-only artifacts.** No artifact is ever rewritten. Each write produces a new chunk containing only the work done in that segment.
2. **Cumulative reconstruction.** Full state at any historical point can be reconstructed by reading chunks in order.
3. **Streaming + compressed IO.** Chunks are written compressed and read by streaming. Per-chunk IO cost is bounded by the delta, not the full state.
4. **Manifest authority.** A run's manifest is the single source of truth. Consumers do not directory-scan.
5. **Process decoupling.** Any consumer can read a run directory while the producer is running or after it ends, without attaching to the producer's process.
6. **Content-addressable.** Artifacts carry integrity metadata (content hash, sizes) so consumers can verify what they read matches what was written.
7. **Configurable per workflow.** What gets persisted is governed by the run's configuration and the codec registry.

Mutation is not a concern: under the run-directory pattern, artifacts are immutable once published. Updates happen via successor artifacts referencing predecessors.

## 2. Conceptual hierarchy

```
Run
└── Stage              a phase of the run
    └── Stream         a logical sequence of artifacts sharing semantics
        └── Epoch      a unit of progressive work within a stream
            └── Tile   sub-division within an epoch (often singleton)
                └── Artifact   a single chunk on disk
```

Each level has stable identity. An artifact's full address is `(run, stage, stream, epoch, tile, sequence)` plus a globally unique `ArtifactId`.

**Streams are the cumulative-analysis primitive.** Walking a stream's artifacts in order reconstructs that stream's full history; concatenation across streams composes views.

## 3. Run-level manifest

Lives at `{run-root}/run.manifest.json`. Written at run init, updated only on stage transitions and run completion. Configuration and identity sections are immutable after init.

Fields:

- **Identity** — run ID (timestamp + nonce or UUID), human-readable label, creation UTC.
- **Schema** — manifest schema version, codec registry version, header schema version.
- **Configuration** — full request DTO snapshot, canonicalized, plus a stable hash fingerprint.
- **Dataset fingerprint** — content hash of input features or external dataset reference.
- **Environment** — host, runtime version, library versions, machine identity.
- **Lineage** — parent run ID (for forks/resumes); child run pointers added as discovered.
- **Stage list** — declared stages with status (Pending, Active, Complete, Failed, Abandoned), entered/exited UTC, references to summary artifacts.
- **Tags** — arbitrary user-supplied K-V metadata; survives round-trip.

The run manifest is the entry point for any consumer: read it first, then consult the artifact log.

## 4. Artifact log

`{run-root}/artifacts.log`. Append-only operation log encoded as **length-prefixed newline-delimited JSON** (decided — see §23.1). Operations:

- `RegisterArtifact(metadata)` — a new artifact has been published.
- `MarkArtifactSuperseded(artifactId, successorId)` — used when a later anchor logically replaces an earlier delta chain (the chain remains until pruned).
- `RecordSupervisorFinding(targetArtifactIds, evidenceArtifactId)` — annotation pointing at existing artifacts.
- `Note(payload)` — free-form events for diagnostics.
- (Future) operations for distributed reconciliation, migration history.

Each entry: timestamp, monotonic sequence, operation type, payload, content hash. The length prefix on each line lets readers detect torn writes at the tail without parsing the full log.

A consumer rebuilds the artifact index by replaying the log. Optional derived snapshot indexes can be cached for fast access without changing the log's authority.

## 5. Per-artifact header

Every artifact begins with a self-describing header. A reader can validate and route an artifact without consulting the manifest or the log.

Fields:

- Magic bytes + header schema version.
- **Codec ID** — namespaced string, e.g. `spc/spin-observation/v3`, `gmm/responsibility/v1`, `thermo/chi-trace/v1`.
- **Encoding** — enum: `binary-custom`, `json`, `ndjson`, `lpac-pipe`, `dense-array` (NPY-shaped: dtype + shape + C/F order + raw bytes). Readers route on this; no sniffing.
- Run ID; lineage links: `BaseArtifactId` (anchor descent), `PreviousArtifactId` (immediate predecessor), `SourceArtifactId` (input that produced this), `PrimaryArtifactId` (sidecar pointer; see §7.2).
- Coordinates: stage, stream key, epoch, tile, sequence.
- Encoding flags: snapshot vs. delta; container; compression algorithm + level.
- **Type metadata**: storage dtype, declared logical type, optional compute dtype hint (see §9).
- Integrity: payload content hash + algorithm, uncompressed byte length, record count + change count (where applicable).
- Timestamps (created UTC).
- Reserved extension block for forward compatibility.

The header is versioned independently from the codec. Future header versions extend without invalidating older codecs.

## 6. Streams and cumulative reconstruction

A stream is a totally-ordered sequence of artifacts sharing a `(stage, stream-key)` identity. Two stream shapes:

- **Anchored delta chains** — for large progressive state (spins, responsibilities, parameter blocks). Anchors are full snapshots; deltas are additive. Replay seeks to the most recent anchor ≤ target epoch and walks deltas forward.
- **Pure append** — for evidence traces, log-likelihood histories, readiness snapshots, where each artifact is independent.

Stream operations exposed to consumers:

- **Walk** — yield artifacts in order. Foundation of cumulative analysis.
- **Materialize at epoch K** — combine artifacts up to epoch K to produce state.
- **Window** — trailing-N or sliding window over a stream for online analysis.
- **Concatenate** — logical view spanning multiple streams (e.g., all temperatures' summaries at the same epoch boundary).

Streams are the canonical seam for "cumulative analysis up to a point in time."

## 7. Bundling, sidecars, and per-point payloads

### 7.1 Bundling principle

A codec's payload type may bundle multiple per-point columns into a single tuple-row matrix. Bundle when **all three** are true:

- The columns are produced at the same time (same write event).
- They share a lifecycle (written together, retained together, deleted together).
- They are consumed together (a reader needing one usually needs the others).

Example bundles:
- Per-epoch SPC state at temperature T: `(spin_label, cluster_size_of_label, local_coupling_sum)` — all derived from the same SW sweep.
- Per-iteration GMM state per point: `(hard_label, log_responsibility_argmax, mahalanobis_to_assigned)` — all derived from the same E-step.
- Dataset features + per-feature flags: `(x[D], is_outlier_flag)` — set together at dataset generation.

Splitting these into separate sidecars creates manifest noise and three header overheads for no benefit.

### 7.2 Index-aligned sidecar artifacts

Sidecars are for data that **shares an index space** with a primary artifact but has a **different lifecycle, write cadence, or consumer set**. Pattern:

- A sidecar carries `PrimaryArtifactId` in its header pointing at the primary.
- `RecordCount` must match the primary's; the engine validates on load.
- Codec ID is distinct (e.g. `dataset/ground-truth-labels/v1` is a sidecar to `dataset/points/v1`).

Examples:
- Ground truth labels sidecar to the points artifact (different lifecycle: written at dataset generation, immutable).
- Fuzzy memberships sidecar to the points artifact (different shape: N × K vs. N × D; only some workflows need it).
- Predicted labels sidecar to the points artifact (different write cadence: produced at clustering completion).

Bundling collapses co-temporal columns into one artifact. Sidecars connect artifacts whose lifecycles differ but whose row indices align. Both patterns are first-class.

### 7.3 What does *not* go in either

Per-point trajectory indices — "for point i, the labels were [3, 3, 5, 5, 5, 1] across the recorded epochs" — are *derived* from a stream by transposition. They live under `derived/` if cached, recomputable from the primary stream. Do not denormalize into the primary stream.

Per-point timestamps that duplicate the artifact's `(epoch, sequence)` coordinates are a category error: epoch is implicit in which artifact you opened.

## 8. Epochs

An epoch is the minimal unit of work that produces a stream artifact. Granularity is configurable per stage:

- An SPC simulation epoch may be one full T-simulation, or N sweeps within a T-simulation.
- A GMM simulation epoch is one EM iteration.
- A thermodynamic-analysis epoch is one analysis pass over a window of input observations.

Within a stage, streams advance epochs independently. Stage-level synchronization (e.g., temperature completion barrier) is optional and explicit.

## 9. Tiles

A tile is a sub-division of an epoch's work that can be computed independently. Where work is divisible (graph construction per node, GMM E-step per data point, per-signal thermo analysis), tiles parallelize within an epoch and either merge into one artifact at write time or remain as addressable sub-artifacts.

Where work is not cleanly divisible (the SW inner sweep loop), tile is a singleton (`tile = 0`) and outer parallelism (across temperatures, across signals) is the only mechanism. The tile coordinate is always present in the address space; in the singleton case it carries no information.

Tile merge timing (write-time vs. read-time) is a per-codec choice.

## 10. Three-layer type model

Persistent artifacts decouple three distinct type concepts:

| Layer | What it describes | Where it lives |
|---|---|---|
| **Logical type** | The mathematical object — "a coupling weight is a non-negative real" | Codec definition |
| **Storage type** | The on-disk representation chosen at write time — `float32`, `float64`, `int8`, etc. | Per-artifact header |
| **Compute type** | The in-memory representation used by consumers during active computation | Selected at read time via `ReadContext` |

The codec declares the logical type and the *minimum* precision required for correctness. The `RunConfig` selects the storage type; the consumer selects the compute type. The codec handles casts internally.

This is what allows the engine to accommodate unforeseeable value ranges without committing to a dtype at design time. For numerical artifacts (CSR graph weights, spin observations, GMM parameters), the storage dtype is recorded as a fact in the header; consumers cast at materialization.

Defaults for SPC/GMM artifacts (proposed; codec authors can override):
- Spin labels: `int8` storage (Q ≤ 20 fits trivially), `int32` compute.
- Coupling weights: `float64` storage default; `float32` permissible when range probe (see §11.2) confirms safety.
- Responsibility matrix: `float32` storage, `float64` compute.
- Covariances, log-likelihoods, distances: `float64` throughout.

## 11. Codec registry

Codecs are external to the engine. The engine knows how to call them, not what they do.

### 11.1 Codec interface

```
interface IArtifactCodec<T> {
    string CodecId { get; }       // e.g. "spc/csr-graph/v1"
    int SchemaVersion { get; }
    Encoding Encoding { get; }    // routes header field

    // Dehydrate: in-memory T → bytes-on-stream.
    void Write(Stream s, T payload, WriteContext ctx);

    // Rehydrate full: stream → in-memory T (random access consumers).
    T Read(Stream s, ReadContext ctx);

    // Rehydrate streaming: stream → IEnumerable<TChunk> (streaming consumers).
    IEnumerable<TChunk> Stream(Stream s, ReadContext ctx);
}

interface IDeltaCodec<T> : IArtifactCodec<T> {
    T MergeDelta(T baseline, T delta);
    T ComputeDelta(T baseline, T current);
}
```

### 11.2 Contexts

`WriteContext` carries policy decisions resolved by the engine before the codec is invoked:
- Selected storage dtype.
- Compression algorithm + level.
- Anchor / delta encoding choice.
- Stream and epoch coordinates.

`ReadContext` carries consumer preferences:
- Compute dtype (defaults to header's storage dtype; consumer may upcast).
- Whether to materialize fully or stream.

The codec never makes policy decisions. The engine resolves policy from `RunConfig` and hands it down. This keeps codecs deterministic and testable without per-mode branching.

### 11.3 Optional ProbeRange diagnostic

For value-range-sensitive payloads (e.g., coupling weights with unknown dynamic range), a codec may emit a *companion stats artifact* before its primary write — `{codec}/range-stats/v1` — containing `{min, max, mean, p1, p99, recommended_storage_dtype}`. The graph stage can then write the primary CSR-graph artifact with `storage_dtype` set from the recommendation. This is a diagnostic codec pattern, not a method on the main codec interface — it stays surface-area-cheap.

### 11.4 Compression boundary

Compression algorithm and level live in the codec registry config (per codec), not in codec implementations. The engine wraps the output stream with the configured compression layer and hands the wrapped stream to the codec. Codecs own the on-the-wire bytes below the compression boundary; the engine owns the compression boundary itself. This eliminates duplicate compression-stream factories across codecs.

### 11.5 Codec-ID extensibility

- **Namespaced codec IDs** decouple artifact kinds from a closed enum. New codecs register without breaking old readers.
- **Old readers** encountering an unknown codec fall through to "unknown codec, skip with structured warning" rather than failing.
- **Versioning** is part of the ID (e.g. `/v3`); readers select a codec by ID match.

## 12. Compression and IO strategy

- **Per-codec compression policy.** No one-size-fits-all default.
- **Hot path** (live delta frames, summary records): light compression (Brotli quality 4–5, or Deflate). Latency-sensitive.
- **Archival** (anchors, run-completion summaries, evidence traces written at run end): Brotli quality 11, or recompression at run finalization.
- **Streaming IO end-to-end.** Codecs do not materialize full payloads in memory unless the codec semantics require it.
- **Compression algorithm + level configurable per codec, per mode.**

## 13. Engine API surface (sketch)

```
// Lifecycle
OpenRun(config) → Run
BeginStage(run, stageId) → StageContext
CompleteStage(stageContext)

// Single-shot artifacts
Publish<T>(stageContext, codec, payload, coordinates) → ArtifactId
Load<T>(run, artifactId) → T
Resolve(run, codecId, streamKey, epoch) → ArtifactId

// Streams
OpenStream<T>(stageContext, streamKey, codec) → StreamWriter<T>
Append<T>(streamWriter, payload, epoch, tile, isAnchor)
OpenStreamReader<T>(run, streamKey) → StreamReader<T>
Walk<T>(streamReader) → IEnumerable<StreamEntry<T>>
WalkChunks<T>(streamReader) → IAsyncEnumerable<TChunk>   // streaming rehydrate
MaterializeAt<T>(streamReader, epoch) → T

// Codec registry
Register(codec)
```

## 14. Cache and consistency semantics

The engine maintains an in-memory cache of recently-published artifacts as a read-side optimization.

- **Publish** always writes to disk first, then populates the cache. If disk write fails, the cache is not updated.
- **Load** checks the cache, falls back to disk read via codec.
- **Eviction policy** is open (see §23.1).
- **Single-process and cross-process consumers see the same API.** A consumer never branches on whether the artifact came from RAM or disk.

This makes the "every deliverable persisted" guarantee robust: bypassing disk in single-process mode would silently break the offline-inspection invariant.

## 15. Forward compatibility

- **Versioned schemas** at three levels: manifest, log, codec.
- **Namespaced codec IDs** support open extension.
- **Reserved extension blocks** in headers and manifest sections.
- **Self-describing artifacts** — a future reader can interrogate older runs.
- **Migration history** in the run manifest — every schema bump records the migration applied.

## 16. Cross-run analysis primitives

- **Run lineage** via parent/child pointers.
- **Dataset fingerprints** identify "same data, different config" runs.
- **Config fingerprints** identify "same config, different data" runs.
- **Run-collection layer** (future) walks a parent directory, builds an index, supports queries like "all runs of dataset X with K=10."
- **Composable views** (future) reference artifacts from multiple runs without copy.
- **Cross-run artifact references** are `(run-id, artifact-id)` tuples; the engine opens the referenced run on demand.

Run directories must be portable: relative paths everywhere, no absolute references in manifests or logs.

## 17. Distributed sync (long-horizon)

The design permits but does not implement distributed observation:

- Append-only operation log is naturally amenable to multi-reader replication.
- Content-addressable artifacts (hash-keyed) deduplicate across replicas.
- Idempotent registration — re-publishing the same artifact ID with matching content is a no-op.
- UTC timestamps + monotonic per-stream sequence numbers resolve ordering.
- Single-writer per run; multi-writer is a higher-layer concern building on these primitives.

## 18. Generic K-V framing

Underneath any domain specialization, the engine is a typed K-V store:

- **Key** = `(run, stage, stream, epoch, tile, sequence)` or the derived `ArtifactId`.
- **Value** = a chunk on disk identified by codec + payload.
- **Operations** = register, read, walk, materialize.

Domain specialization lives in the codec registry. The lower layers (manifest, log, header, file IO, compression) are codec-agnostic and can host arbitrary artifact types.

---

# Part II — Runtime (SPC + GMM application of the engine)

The runtime layer drives stages through the engine. It is built on top of the engine and the SPC/GMM codecs; the engine itself remains domain-agnostic.

## 19. Architectural layering

| Layer | Knowledge | Project location |
|---|---|---|
| **Engine** | General; runs, stages, streams, codecs, compression, IO. No SPC or GMM types. | `projects/StateEngine/` |
| **Codecs** | Domain. How to encode/decode specific payloads. | `spc/csr-graph/v1`, `spc/spin-observation/v3`, `thermo/chi-trace/v1`, `gmm/parameter-snapshot/v1`, etc. |
| **Runtime** | Workflow. Drives stages through the engine. Implements the SPC + GMM control flow. | `projects/SpcCore/`, `projects/GaussianMixture/`, future `projects/GmmCore/` |

Implications for project structure (post-engine):
- `projects/StateEngine/` — pure engine. No SPC or GMM types.
- `projects/SpcCodecs/` (or hosted in `SpcCore`) — SPC-specific codecs.
- `projects/SpcCore/` — runtime, references `StateEngine` + `SpcCodecs`.
- `projects/GmmCodecs/`, future `projects/GmmCore/` — same shape.
- `projects/SpcThermo/` — supervisor + analysis codecs registered against the engine.

## 20. Checkpoint-as-handoff

Stage boundaries are checkpoint boundaries. **Within a stage**, the runtime is free to use in-memory pipelines for intermediate computations. **At stage boundaries**, the runtime hands off via the engine: the producing stage publishes artifacts; the consuming stage reads them.

### 20.1 Concrete: graph initialization → SW hot path

Today (in-memory coupling, see [src/spc.batch.cs:149](../src/spc.batch.cs:149)):

```csharp
Edge[] edges = BuildGraphFromMetric(request, out int n);
return RunSimulationCore(edges, n, request, ...);
```

Under checkpoint-as-handoff:

```csharp
using var run = engine.OpenRun(config);

// Graph stage — builds CSR graph in memory, publishes one artifact, closes.
var graphArtifactId = GraphStage.Execute(run, request);

// SpcSim stage — takes graph artifact ID as input contract.
SpcSimStage.Execute(run, graphArtifactId, request);
```

In the single-process case the CSR graph is served from the engine's in-memory cache (no disk read). In a cross-process or replay case, it's read from disk transparently.

### 20.2 Why this matters

- **Every in-memory deliverable becomes persisted.** Offline inspection, debugging, and dev workflows fall out for free.
- **Stages become independently runnable.** Rerun `SpcSim` against an existing `Graph` artifact (different temperatures on the same graph). Fork a run from any stage.
- **Cross-run composition works.** A run can declare graph inputs from a prior run by artifact reference.
- **Distributed observation works.** Supervisor reading a remote run directory uses the same artifact-resolution API as the in-process runtime.
- **Tests get clean fixtures.** A test for `SpcSim` constructs a graph artifact and hands its ID to the stage.

### 20.3 What stays in memory

The handoff discipline applies at *stage boundaries*, not within stages. The SW sweep loop does not write per-sweep artifacts — it writes per-epoch artifacts where epoch granularity is configurable. The serialization budget is bounded by epoch granularity, not sweep granularity.

## 21. Supervisor execution and termination

Three supervision modes collapse to a single mechanism with two knobs (epoch granularity, supervisor coupling):

| Mode | epoch_sweeps | Supervisor | Termination authority |
|---|---|---|---|
| **Eager** (classical online) | 1 (per sweep) | none — kernel reads its own running accumulators | SW kernel: sweep budget or in-loop convergence test |
| **Supervised** (epoch-paced) | configurable | in-process or out-of-process at epoch rendezvous | Supervisor: writes termination signal as artifact |
| **Headless** (offline) | full run as one epoch | async / out-of-process post hoc | SW kernel: sweep budget; analysis post-mortem |

Termination signals are themselves artifacts (`supervisor/termination-signal/v1`), written to a known stream. The SW driver polls for them at every epoch rendezvous before advancing the next epoch. This means the supervisor can be in-process *or* out-of-process with the same contract: in-process publishes via the engine and gets cache-hit pickup; out-of-process publishes via disk write and is picked up by the next polling cycle.

## 22. Observable cost discipline

What gets accumulated *inside* the simulation kernel vs. derived *outside* is a cost question, not an architectural one. Rule of thumb:

- **O(N) per sweep observables belong inside the kernel.** Running mean, FK susceptibility, cluster-size histogram. These are essentially free; the simulation is already O(edges) per sweep, and a single pass over `int[N]` adds no asymptotic cost. Discarding them just to recompute from spin observations later wastes the work.
- **O(N²) or O(N·K) state belongs as supervisor passes.** Equilibrium correlation matrix `<δ(sᵢ, sⱼ)>`, fan-out KL, full responsibility matrix queries. Cramming these into the hot path bloats it for marginal benefit; deriving them from persisted observations is the right factoring.

This is an axis distinct from supervision mode. The kernel always runs the cheap accumulators regardless of whether anyone acts on them in real time. Whether running values are *acted on* is the supervisor's question (eager: kernel itself acts; supervised: external supervisor acts; headless: post-mortem analysis acts).

## 23. Canonical end-to-end lifecycle

A complete SPC + GMM run progresses through the following stages. Stages that do not run leave their directories absent. The lifecycle is the same skeleton for SPC-only, GMM-only, and combined runs.

### 23.1 `Init` — run initialization
- **Inputs:** request DTO, environment context, optional parent run reference.
- **Artifacts:** run manifest, opened artifact log.

### 23.2 `Graph` — proximity graph construction
- **Inputs:** dataset features (or precomputed distance field).
- **Operations:** pairwise distance, proximity rule, coupling kernel, connectivity diagnostics.
- **Artifacts:** CSR graph (singleton snapshot), graph diagnostics, optional coupling-weight range stats.

### 23.3 `SpcSim` — Swendsen-Wang temperature sweeps
- **Inputs:** CSR graph artifact ID, temperature list, sweep budget per T, epoch granularity.
- **Operations:** per temperature, SW sweeps grouped into epochs. Parallelism across temperatures by default; tiles within a temperature where divisible. See [spc-maturity.md](./spc-maturity.md) for the runtime renovation that makes the SW kernel publish proper bond-cluster statistics.
- **Artifacts** (per epoch, per T, per tile): spin observation (anchor or delta), per-epoch summary, optional bond-cluster size sample, per-T final-state bundle on completion.

### 23.4 `Thermo` — thermodynamic and information analysis
- **Inputs:** spin observation streams, bond-cluster statistics streams, per-epoch summaries.
- **Operations:** χ trends (FK estimator), K stability, fan-out KL, centroid drift, Fisher info, BFS shells, peak detection — full-history or windowed.
- **Artifacts:** evidence traces (one stream per signal), cross-temperature snapshots at completion of T blocks.

### 23.5 `Handoff` — readiness and handoff state
- **Inputs:** evidence traces.
- **Operations:** Phase 1 — manual review and selection. Phase 2 — automated readiness scoring.
- **Artifacts:** readiness snapshots (append-only), handoff state (selected initial conditions for GMM, with lineage links to source SPC artifacts).

### 23.6 `GmmInit` — GMM parameter lift
- **Inputs:** handoff state.
- **Operations:** translate SPC outputs into GMM means, covariances, weights via `GaussianMixtureModel.InitializeWithParameters`. May dispatch manifold-aware estimators (Karcher mean, geometric median) for warm-start.
- **Artifacts:** initial GMM parameter snapshot.

### 23.7 `GmmSim` — EM iterations
- **Inputs:** initial parameters, dataset.
- **Operations:** EM iterations as epochs. Tile-divisible at the data-point level for E-step responsibilities and M-step partial sums.
- **Artifacts** (per iteration, optionally per tile): parameter snapshot, responsibility frame, iteration summary.

### 23.8 `Done` — run completion
- Final state finalized, run manifest updated to `Complete`, optional run digest emitted.
- A run can complete at any earlier stage.

## 24. Per-stage codec inventory

GMM codec shapes are now concrete (the `GaussianMixtureModel` runtime is built; see `src/gmm/`). SPC codec shapes assume the SPC maturity renovation; see [spc-maturity.md](./spc-maturity.md).

| Stage | Codec ID | Shape / payload | Notes |
|---|---|---|---|
| Init | `run/manifest/v1` | JSON | Singleton at run root |
| Graph | `spc/csr-graph/v1` | binary, dense-array layout | Singleton; storage dtype set from coupling-stats |
| Graph | `spc/coupling-stats/v1` | small JSON | `{min, max, mean, p1, p99, recommended_dtype}` |
| Graph | `spc/graph-diagnostics/v1` | JSON | Connectivity warnings |
| SpcSim | `spc/spin-observation/v3` | binary delta-encoded `int8[N]` per epoch per T per tile | Hot path; anchored delta chain. **Note:** these are SW per-sweep colors, not equilibrium cluster assignments — see `spc/equilibrium-clusters/v1` for the latter |
| SpcSim | `spc/temperature-summary/v1` | JSON, small struct | `{cluster_count, sweep_count, equilibration_phase, running_chi, is_complete, current_spins_artifact_id}` per epoch per T |
| SpcSim | `spc/bond-cluster-sample/v1` | LPAC pipe-delimited, `int[]` | Optional; per-epoch sample of bond-cluster sizes for percolation visualization and FK variance estimation |
| SpcSim | `spc/temperature-final-state/v1` | binary, `(int label, int arrival_sweep, int cluster_size)[N]` per T | Bundled per-point payload at T completion; for replay/visualization. Header records settling criterion (`last-spin-flip` / `stability-window-K=5` / `structural`) |
| Thermo | `thermo/chi-trace/v1` | LPAC pipe-delimited, `(epoch, value)` per T | FK susceptibility derived from bond-cluster sizes |
| Thermo | `thermo/cluster-count-trace/v1` | LPAC pipe-delimited, `(epoch, K)` per T | K stability tracking |
| Thermo | `thermo/cooccurrence-matrix/v1` | binary, sparse symmetric `float[N, N]` per T | Equilibrium `<δ(sᵢ, sⱼ)>` averaged over sampling sweeps; source-of-truth for "the clusters at T" |
| Thermo | `thermo/equilibrium-clusters/v1` | binary, `int[N]` per T | Connected components of co-occurrence matrix above threshold; the "real" cluster assignments |
| Thermo | `thermo/cross-temperature-snapshot/v1` | JSON, structured | Peak indices, criticality scores |
| Handoff | `handoff/readiness/v1` | JSON, append | Per-evaluation snapshot |
| Handoff | `handoff/state/v1` | JSON | Singleton per handoff; selected initial conditions |
| Supervisor | `supervisor/termination-signal/v1` | JSON, small | Termination directive emitted by supervisor at epoch rendezvous |
| GmmInit | `gmm/initial-parameters/v1` | binary, K × `{Weight (float64), Mean[D] (float64), Covariance[D,D] (float64)}` | Singleton; do not persist `Σ⁻¹` or `LogNormalizationFactor` — rehydrate calls `UpdateCache()` |
| GmmSim | `gmm/parameter-snapshot/v1` | same as initial-parameters; anchor + delta candidate (delta = changed components) | Per EM iteration |
| GmmSim | `gmm/responsibility-frame/v1` | binary dense-array `float32[N, K]`, `float64` compute | Tile by row range (E-step is row-parallel by construction) |
| GmmSim | `gmm/iteration-summary/v1` | JSON, append | `{IterationIndex, FinalLogLikelihood, IsConverged, EffectiveCounts[K]}` |
| GmmSim | `gmm/sample-output/v1` | binary; `(samples[N,D], componentIndices[N])` | Synthetic mixture emission; pairs with dataset sidecar pattern |
| GmmSim | `gmm/locked-assignments/v1` | binary, `int[N]` | Sidecar to points artifact; semi-supervised input |
| Dataset | `dataset/points/v1` | binary dense-array `float64[N, D]` | Primary feature matrix |
| Dataset | `dataset/ground-truth-labels/v1` | binary `int32[N]` | Sidecar to points (synthetic data only) |
| Dataset | `dataset/ground-truth-hierarchy/v1` | binary `float[N-1, 3]` matching MATLAB `Z` layout | Sidecar; for Blatt hierarchy and similar |
| Dataset | `dataset/manifest/v1` | JSON | Per-dataset metadata, generation params |

## 25. Configuration and modes

`RunConfig` declares:

- Stages enabled.
- Per-stage epoch granularity.
- Per-stage tile policy (none, fixed count, divide-by-cores).
- Per-codec persistence policy (skip, write, encoding, compression algorithm + level).
- Anchor cadence per stream (every N epochs, every N change records, time-based).
- Retention policy (keep all, prune pre-anchor deltas after N epochs, archive policy at run completion).
- Supervisor execution model (none, in-process callback, observer process).
- Storage dtype overrides per codec.

Named modes resolve coherent presets. Examples (illustrative; final list TBD):

| Mode | Intent | Persistence shape |
|---|---|---|
| `ResumeOnly` | Cheap pause/resume | Latest pointer only; no replay |
| `Replay` | Offline scrub through history | Anchored delta streams + summaries; no supervisor |
| `Supervised` | Live supervisor watching | Replay + per-epoch evidence traces |
| `EvidenceAccumulation` | Manual GMM handoff workflow | Supervised + archival compression + extended retention |
| `ResearchTrace` | Maximum visibility | Everything, including tile-level intermediate artifacts |

## 26. On-disk layout (proposal)

```
{run-root}/{run-id}/
├── run.manifest.json
├── artifacts.log
├── stages/
│   ├── init/
│   ├── graph/
│   │   ├── csr-graph.bin.br
│   │   ├── coupling-stats.json
│   │   └── diagnostics.json
│   ├── spc-sim/
│   │   ├── observations/T{key}/ep{epoch:08}_t{tile:04}_s{seq:08}.bin.br
│   │   ├── summaries/T{key}/ep{epoch:08}.json
│   │   ├── bond-cluster-samples/T{key}/ep{epoch:08}.lpac.br
│   │   └── final-state/T{key}.bin.br
│   ├── thermo/
│   │   ├── traces/{signal}/T{key}.lpac.br
│   │   ├── cooccurrence/T{key}.bin.br
│   │   └── equilibrium-clusters/T{key}.bin.br
│   ├── handoff/
│   │   ├── readiness/
│   │   └── state/
│   ├── supervisor/
│   │   └── termination-signal-{seq}.json
│   ├── gmm-init/
│   ├── gmm-sim/
│   │   ├── parameters/iter{n:08}.bin.br
│   │   ├── responsibilities/iter{n:08}_t{tile:04}.bin.br
│   │   └── summaries/iter{n:08}.json
│   └── done/
└── derived/
```

The path layout is one valid encoding consistent with the artifact log. **The log is the authority; paths are convenience.**

---

# Part III — Open questions, phasing, debt

## 27. Open design questions

### 27.1 Engine internals
- **Anchor cadence policy** — time-based, epoch-count-based, change-volume-based, or hybrid?
- **Tile merge timing** — write-time vs. read-time, possibly per-codec choice.
- **Cross-run reference format** — opaque hash-based reference, structured URI, or both?
- **Manifest update concurrency** — single-writer per run is assumed. Do we need explicit locking, or rely on file-system atomicity of the log's append + the manifest's atomic-rename rewrite?
- **In-memory cache eviction policy** — LRU, size-bounded, weak references, codec-declared retention hints?
- **Run-level event API** — does the engine expose a structured event stream for in-process consumers, or do consumers tail the log?

**Resolved (no longer open):**
- *Operation log encoding* → length-prefixed ndjson (see decisions log §32).

### 27.2 Runtime / workflow
- **Stage failure semantics** — partial failure: artifacts logged as `Abandoned`, rolled back, or left for forensic inspection?
- **Stage dependency declaration** — should stages declare their input artifact requirements so the engine can validate before execution, or is that a runtime-layer concern?
- **Concurrent stages** — can `Thermo` run concurrently with `SpcSim`, reading observation artifacts as they're published? Implies a publish-event API on the writer side.
- **Cross-T epoch synchronization** — independent (current) vs. synchronized (round-robin); see [spc-maturity.md](./spc-maturity.md) §7.

### 27.3 SPC and GMM specifics
- **GMM E-step responsibility tile granularity** → per-row-range (E-step is row-parallel by construction). Settled.
- **GMM responsibility persistence shape** → full N×K via `gmm/responsibility-frame/v1`. Sufficient-statistics compression is a future-codec, not v1.
- **GMM label stability convention** — sort components by descending weight at end of `Fit` so cross-run label vectors are comparable without alignment. To be enforced inside `GaussianMixtureModel.Fit` itself.
- **GMM Mahalanobis matrix as artifact** — `gmm/mahalanobis-matrix/v1` is a candidate diagnostic codec; not v1 priority.
- **`IRiemannianManifold` extraction** — currently in `StatisticalEstimators`. Trigger for extracting to dedicated `Geometry` / `Manifolds` project is a non-product-manifold consumer (e.g., GMM Karcher warm-start dispatch).
- **Bond-cluster membership codec** — `spc/bond-cluster-membership/v1` deferred until percolation-rigorous analysis becomes a priority; reserved name documented.

## 28. Implementation phasing

Suggested order. Each phase produces something usable on its own and does not lock the schema in ways that block later phases.

1. **Engine substrate** — run manifest, artifact log, per-artifact header foundations. Hash, integrity, lineage primitives. Project skeleton for `StateEngine`.
2. **Codec registry + compression streams + streaming IO substrate.**
3. **First SPC codec** — `spc/csr-graph/v1`. Validates the engine end-to-end with one real artifact type.
4. **SpcSim codecs** — `spc/spin-observation/v3` (anchor + delta), `spc/temperature-summary/v1`. Replaces current `spc.checkpoint.cs` functionality cleanly.
5. **Stream replay primitives** — walk, materialize-at, window, concatenate. Lazy `IEnumerable` shape from the start.
6. **Runtime restructuring** — port `SpcBatch.Run` to the stage-driven model. `Graph` and `SpcSim` as explicit stages handing off via the engine. Coordinates with [spc-maturity.md](./spc-maturity.md) — which can land first, against the existing checkpoint scaffolding, then migrate to the engine.
7. **Thermo evidence streams + supervisor execution model.** Termination signal codec, polling cadence at SW driver.
8. **Handoff state + manual handoff workflow.** Manual workflow is the Phase 1 GMM target.
9. **GMM codecs and runtime** — engine integration of the existing `GaussianMixtureModel`.
10. **Tile semantics** — initially singleton, then real subdivision where workflows demand.
11. **Cross-run tooling** — run-collection index, dataset-fingerprint queries.
12. **Distributed observation primitives, migration tooling** — long-horizon work.

The SPC maturity work (FK susceptibility, bond-cluster accumulation, model API restructuring) can land in parallel with phases 1–3, against the existing checkpoint scaffolding. Phases 4 and 6 then merge it into the new engine substrate.

## 29. Architecture debt

Items in current code that are debt from the prior "resume only" framing. Listed here so they are not mistaken for design choices and so future work has a clear deletion list:

- `SpcCheckpoint`, `SpcRunStateManifest`, `SpcStateArtifact`, `SpcCheckpointPersistenceOptions`, `SpcTemperatureState`, `SpcTemperatureSpinFrame` in [src/spc.checkpoint.cs](../src/spc.checkpoint.cs) — superseded by engine primitives + SPC codecs.
- Temperature summary `.ckpt` files overwriting per epoch (loses per-epoch summary history).
- Manifest fully rewritten on every artifact insert (no `SpcRunStateWriter`).
- No artifact integrity metadata (content hash, uncompressed length, record count).
- No codec boundary; spin-frame IO inlined in `SpcBatch`.
- Two near-identical compression-stream factories.
- No anchor frames; replay walks the full delta chain.
- Persistence options expressed as a flag bag, not modes.
- `LoadCheckpoint` directory-scans `*.ckpt` rather than driving from the manifest.
- Brotli compression hardcoded to `SmallestSize` (quality 11) on every write.
- Susceptibility analysis materializes full spin history in memory.
- Schema fields `SpinCounts`, `CentroidSnapshot`, readiness slots reserved but never populated.
- `EpochSweepCount` is a sweep-count threshold, not an epoch primitive — misleadingly named.
- Runtime control flow couples in-memory between graph initialization and SW hot path; should be checkpoint-mediated.

## 30. Correctness bugs

Distinct category from architecture debt — these are wrong-formula or wrong-semantics issues to fix:

- **FK susceptibility computed from spin colors instead of bond-cluster sizes.** [spc-thermo/chi.cs:41-59](../src/spc-thermo/chi.cs:41) buckets by spin label, but with Q=20 and many more bond-clusters in the disordered phase, multiple distinct bond-clusters get conflated into one color bucket. Cauchy-Schwarz makes this a positive bias. The canonical FK estimator is `χ = (1/N) · Σ_c |c|²` over actual bond-clusters from the union-find pass, averaged over equilibrium sweeps. Fix bundled into the SPC maturity scope.
- **`BuildHistograms` and other spin-label-bucketed code** ([src/spc.thermo.cs:40](../src/spc.thermo.cs:40)) inherits the same conflation; needs the same shift to bond-cluster basis.
- **`SpcBatchResult.FinalSpins[T]`** is an instantaneous snapshot of per-sweep colors, not equilibrium cluster assignments. Currently treated as "the clusters at T" by downstream code; only valid below T_c. The proper "clusters at T" come from thresholding the equilibrium co-occurrence matrix and taking connected components — see `thermo/equilibrium-clusters/v1` in the codec inventory.
- **RNG state not resumable.** `System.Random` doesn't expose its state; resume creates a fresh RNG, so the stochastic trajectory diverges from uninterrupted-run reproducibility. Fix via swap to xoshiro/PCG with `GetState()`/`SetState()`. Bundled into SPC maturity scope.

No prior runs exist, so no historical results need annotation or invalidation — these are fix-before-first-run items.

## 31. Worth keeping during reimplementation

Pieces of the current implementation worth porting forward as references:

- The temperature-key-as-bit-string convention (`BitConverter.DoubleToInt64Bits` → hex) — portable across cultures, stable ordering.
- The atomic-rename file write pattern.
- The Brotli/GZip/Deflate compression-stream factory shape (extract into the engine compression boundary).
- The delta-encoding format for spin observations (changed indices + values) — port into the new spin observation codec; the on-wire format can stay close to current as a v3-or-v4 variant.
- The three-tier scratch buffer pattern in `Mahalanobis` (stackalloc / ThreadLocal / ArrayPool) — generalize as a small utility for other dimension-dispatched hot-path code.

---

# Part IV — Decisions log

Entries are dated and brief. Resolves prior open questions; do not modify after entry.

## 32.

### 2026-05-04
- **Operation log encoding:** length-prefixed newline-delimited JSON. Human-inspectable, append-friendly, parser-cheap. Covers manifest log only; payload artifacts use codec-specific encodings (binary, LPAC pipe-delimited, dense-array, JSON).
- **Three-layer type model:** logical / storage / compute split is canonical. Storage dtype recorded in artifact header as a fact; compute dtype selected at read time via `ReadContext`.
- **Encoding header field:** `Encoding` enum on per-artifact header — `binary-custom`, `json`, `ndjson`, `lpac-pipe`, `dense-array`. Readers route on this; no sniffing.
- **Compression boundary:** lives in engine, not in codec implementations. Codec writes/reads the bytes inside an already-wrapped compression stream; engine resolves algorithm + level from `RunConfig`.
- **GMM responsibility tile granularity:** per-row-range, matching the row-parallel E-step structure.
- **GMM derived state:** do not persist `Σ⁻¹` or `LogNormalizationFactor`; rehydrate calls `UpdateCache()`. Codec carries `Mean`, `Covariance`, `Weight` only.
- **Bundling principle:** co-temporal per-point columns bundle into one tuple-row codec. Sidecars are for index-aligned data with distinct lifecycles. Not "one signal per artifact."
- **`spc/spin-observation/v3` semantics:** explicitly "SW per-sweep colors," not equilibrium cluster assignments. The latter is `thermo/equilibrium-clusters/v1`, derived from `thermo/cooccurrence-matrix/v1`.
- **Three supervision modes (Eager / Supervised / Headless):** collapse to one mechanism with knobs (epoch granularity, supervisor coupling). Termination signal is itself an artifact.
- **Observable cost discipline:** O(N)-per-sweep observables run inside the kernel always; O(N²)-per-sweep observables are post-hoc supervisor passes. Independent of supervision mode.

---

> **Next steps:** validate framing, then execute Phase 1 of §28. The engine substrate must stand on its own without SPC dependencies before the first codec lands. SPC maturity (see [spc-maturity.md](./spc-maturity.md)) can land in parallel against the existing scaffolding.
