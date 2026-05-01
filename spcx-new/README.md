# spcx#

**Super-Paramagnetic Clustering in C#** — a .NET library implementing the Blatt–Wiseman–Domany (1996) SPC algorithm, extended with a broad metric and proximity menu, pluggable coupling kernels, a full KL-divergence analysis suite, and PowerShell-friendly batch/checkpoint DTOs.

**Platform baseline:** SPCX targets **.NET 10 LTS** (`net10.0`) and PowerShell 7.6. New C# projects should live under `projects/`, inherit common build settings from `Directory.Build.props`, and keep generated build output under `artifacts/` rather than `src/`.

The core idea: model a dataset as a Potts ferromagnet on a proximity graph, sweep temperature, and read off clusters at the phase transition where susceptibility peaks. No number-of-clusters input required. The right _T_ falls out of the physics.

---

## How It Works

### 1 — Distance field (12 metrics)

Each pair of data points is measured with a caller-chosen metric: `Hamming` (on 64-bit SimHash signatures), `Euclidean`, `Canberra`, `Manhattan`, `Minkowski` (configurable _p_), `Cosine`, `JensenShannon`, `Jaccard` (threshold-binarised), `Wasserstein` (1D Earth Mover's), `Mahalanobis` (caller-supplied Σ⁻¹), `FisherRao` (geodesic on the statistical manifold), or `Poincare` (hyperbolic unit-ball distance).

### 2 — Proximity graph (4 rules)

From the distance field, edges are selected by one of four rules:

- **KNN** — _K_ nearest neighbours, OR-symmetrised to produce an undirected graph
- **MutualKNN** — AND-rule; edge exists only if both nodes mutually name each other; inherently symmetric but risks connectivity loss in high dimensions
- **EpsilonBall** — all pairs within radius ε
- **MstAugmented** — starts from Mutual KNN, then runs Kruskal's on the full pairwise distance matrix and patches in MST bridging edges to guarantee connectivity; recommended for distributional metrics (Jensen-Shannon) and high-dimensional data

Post-construction, `ValidateGraph` runs union-find to count components and isolated nodes, surfacing warnings to the caller without blocking simulation.

### 3 — Coupling weights (4 kernels)

Each edge weight _J(d, δ)_ maps pairwise distance _d_ and bandwidth _δ_ to a coupling strength in [0, 1]:

- **Gaussian** — `exp(−d²/2δ²)` (canonical SPC formulation)
- **Cauchy** — `1/(1 + (d/δ)²)` (heavier tails)
- **Laplacian** — `exp(−d/δ)` (exponential decay)
- **Linear** — `max(0, 1 − d/δ)` (compact support)

If `Delta = 0`, bandwidth is auto-estimated from 1-NN distances using either `Mean` (validated) or `Median` (theoretically more robust to outliers; untested in this context — use with caution).

### 4 — Swendsen-Wang Monte Carlo

`PottsModel` runs SW on the CSR graph at each temperature. Bond probability per edge is `1 − exp(−J/T)`. Vectorised bond-probability initialisation uses `System.Numerics.Tensors` (`TensorPrimitives.Multiply/Exp/Negate/Add`). The simulation kernel owns spin evolution and epoch callbacks only; susceptibility is computed afterward by the chi analysis layer from materialized spin states or checkpoint history.

The Potts state space is `Q = 20` (default). `FastUnionFind` with path compression and union-by-size handles cluster formation and spin-flip dispatch in the inner loop.

### 5 — Phase transition detection

`SpcAnalysis` operates post-hoc on `SpcBatchResult` output. Two complementary signals locate the critical temperature:

- **Susceptibility peak** (`SpcAnalysis.AnalyzeSusceptibility`) — standard χ(T) peak detection
- **KL divergence family** (`SpcAnalysis.KL`) — global KL between adjacent-temperature histograms, fan-out KL from cluster medoids, radial coherence, and Fisher information across the temperature sweep

`DetectPeaks` applies a prominence threshold (fraction of signal maximum) to find local maxima in either signal.

---

## Source Layout

```

projects/
├── SpcCore/SpcCore.csproj # SPC run contract, graph binding, checkpointing, Potts runtime
├── SpcThermo/SpcThermo.csproj # SpcAnalysis thermodynamic/information analysis
├── SpcSynthetic/SpcSynthetic.csproj # Adapter from SyntheticDatasets DTOs to SpcBatchRequest
├── Hashish/Hashish.csproj # Standalone Hashish primitive library project
├── DistanceMetrics/DistanceMetrics.csproj # Standalone metric primitive library
├── ProximityGraphs/ProximityGraphs.csproj # Standalone proximity graph primitive library
├── CouplingKernels/CouplingKernels.csproj # Standalone distance-to-coupling primitive library
├── StatisticalEstimators/StatisticalEstimators.csproj # Standalone estimator primitive library
└── SyntheticDatasets/SyntheticDatasets.csproj # Standalone synthetic dataset primitive library

src/
├── spc.batch.cs # SpcBatchRequest/Result DTOs, SpcCheckpoint carrier, SpcBatch.Run orchestration
├── spc.graph.cs # Edge/CsrGraph runtime topology, graph initialization, connectivity diagnostics
├── spc.potts.cs # PottsModel, SimulationResult, FastUnionFind
├── spc.thermo.cs # SpcAnalysis base: histograms, medoids, BFS shells, peak detection, purity scoring
├── spc.synthetic.cs # SpcSynthetic adapter from SyntheticDatasets DTOs to SpcBatchRequest
├── spc.checkpoint.cs # SpcCheckpoint DTOs, manifest-backed state artifacts, atomic persistence
│
├── spc-thermo/
│ ├── chi.cs # Susceptibility peak detection
│ ├── kl.cs # KL divergence: global, fan-out, radial coherence, Fisher information
│ └── mhbs.cs # Multi-hop boundary sharpness analysis
│
├── estimators/
│ ├── DeltaMean.cs # Scalar distance/bandwidth mean estimator
│ ├── DeltaMedian.cs # Scalar distance/bandwidth median estimator
│ ├── LocationMean.cs # Weighted Euclidean Frechet mean for indexed observations
│ ├── LocationGeometricMedian.cs # Weighted Euclidean geometric median via Weiszfeld
│ ├── LocationMetricMedoid.cs # Weighted metric medoid over observed candidates
│ ├── LocationEstimate.cs # Shared location estimator diagnostics
│ └── LocationMedoidEstimate.cs # Metric-medoid result diagnostics
│
├── hashish/ # Text, similarity, sketching, and compression primitives
│ ├── simhash.cs # 64-bit SimHash — Compute(text) → ulong
│ ├── minhash.cs # MinHash signatures, Jaccard estimation, and band/row LSH index
│ ├── ctph.cs # Context-Triggered Piecewise Hashing (ssdeep-style)
│ ├── tlsh.cs # TLSH locality-sensitive hash
│ ├── bm25.cs # BM25 term statistics (used upstream by SimHash pipeline)
│ ├── idf.cs # Reusable document-frequency / IDF model
│ ├── tokenizer.cs # Unicode normalization and word tokenization
│ ├── shingler.cs # Word-level n-gram shingles
│ ├── jaccard.cs # Exact Jaccard, containment, and overlap primitives
│ ├── bloom.cs # Approximate membership filter
│ ├── countmin.cs # Streaming approximate frequency estimator
│ ├── hyperloglog.cs # Approximate cardinality estimator
│ └── ncd.cs # Standalone pairwise NCD primitive
│
├── kernels/ # CouplingKernels distance-to-coupling primitives
│ ├── Gaussian.cs
│ ├── Cauchy.cs
│ ├── Laplacian.cs
│ └── Linear.cs
│
├── metrics/ # Static pairwise metric primitives, independent of graph construction
│ ├── Hamming.cs
│ ├── Euclidean.cs
│ ├── Canberra.cs
│ ├── Manhattan.cs
│ ├── Minkowski.cs
│ ├── Cosine.cs
│ ├── JensenShannon.cs
│ ├── Jaccard.cs
│ ├── Ncd.cs
│ ├── Poincare.cs
│ ├── Wasserstein.cs
│ ├── Mahalanobis.cs
│ └── FisherRao.cs
│
├── graphs/ # ProximityGraphs primitive graph selection rules
│ ├── Knn.cs
│ ├── MutualKnn.cs
│ ├── EpsilonBall.cs
│ └── MstAugmented.cs
│
└── synthetic/ # SyntheticDatasets ground-truth generators and shared helpers
├── SyntheticData.cs # SyntheticDataset DTO, sampling primitives, geometry helpers
├── BlattThreeCluster.cs # The canonical SPC benchmark from the original paper
├── BlattHierarchy.cs # Multi-scale nested clusters (hierarchical ground truth)
├── AnisotropicGaussian.cs # Correlated ellipsoidal clusters; exposes ClusterCovariances
├── GaussianManifold.cs # Clusters on a curved manifold
├── HyperbolicBlobs.cs # Hyperbolic-space blobs for non-Euclidean stress tests
├── Simplex.cs # Dirichlet-distributed probability vectors
├── SparseSupports.cs # Clusters with non-overlapping sparse feature support
├── SpatialBlobs.cs # Simple isotropic blobs at varying densities
└── TwoMoons.cs # Non-convex interleaved crescents

```

---

## Build Baseline

The repository is C#-first and targets .NET 10 LTS. Current standalone projects:

```powershell
dotnet build .\projects\Hashish\Hashish.csproj
dotnet build .\projects\DistanceMetrics\DistanceMetrics.csproj
dotnet build .\projects\ProximityGraphs\ProximityGraphs.csproj
dotnet build .\projects\CouplingKernels\CouplingKernels.csproj
dotnet build .\projects\StatisticalEstimators\StatisticalEstimators.csproj
dotnet build .\projects\SyntheticDatasets\SyntheticDatasets.csproj
dotnet build .\projects\SpcCore\SpcCore.csproj
dotnet build .\projects\SpcThermo\SpcThermo.csproj
dotnet build .\projects\SpcSynthetic\SpcSynthetic.csproj
dotnet build .\ps.core.pwshspc.sln
```

`Directory.Build.props` is the authority for shared SDK settings, including `TargetFramework=net10.0`, `BaseOutputPath`, and `BaseIntermediateOutputPath`. Build outputs are written under `artifacts/bin/` and `artifacts/obj/`; `src/` should contain source files only.

Project files live under `projects/<ProjectName>/` and explicitly include their source files from `src/`. Primitive projects compile only their matching source folders; they are not root projects for every source file under `src/`.

---

## Estimator Primitives

The `estimators/` folder is compiled as `StatisticalEstimators`. It is no longer limited to the SPC delta axis; it is the application-agnostic statistical-estimation layer for reusable computational primitives:

- **Delta estimators** — scalar bandwidth summaries over distance samples (`DeltaMean`, `DeltaMedian`)
- **Location estimators** — weighted summaries over indexed observations (`LocationMean`, `LocationGeometricMedian`, `LocationMetricMedoid`)

These classes are standalone and do not depend on `SpcBatch`. SPC, GMM handoff, robust diagnostics, synthetic harnesses, and future geometry-aware modules should dispatch them from higher-level orchestration rather than baking estimator logic into domain-specific batch classes.

---

## Metric Primitives

The `metrics/` folder is compiled as `DistanceMetrics`, a static pairwise-distance library. Metric implementations such as `Euclidean.Distance`, `JensenShannon.Distance`, `Mahalanobis.Distance`, and `Poincare.Distance` do not build graphs and do not depend on `SpcBatch`.

The `graphs/` folder is compiled as `ProximityGraphs`, a primitive library for proximity selection over an index-level distance delegate. The `kernels/` folder is compiled as `CouplingKernels`, a primitive library that maps raw distances and bandwidths to Potts edge weights. SPC graph initialization is handled by `spc.graph.cs` and the SPC dispatcher: they bind a selected metric implementation to a selected proximity rule, then convert neighbor distances through the chosen coupling kernel. This keeps the axes separate:

- **Metric** — how two observations are measured
- **Proximity** — which measured pairs become graph edges
- **Coupling** — how raw distances become Potts edge weights

---

## Synthetic Dataset Primitives

The `synthetic/` folder is compiled as `SyntheticDatasets`. It owns labeled benchmark dataset DTOs, sampling helpers, geometry helpers, and generator partials without depending on `SpcBatch`.

SPC-specific conversion lives in `spc.synthetic.cs` and is compiled by `projects/SpcSynthetic/SpcSynthetic.csproj`. That adapter depends on both `SpcCore` and `SyntheticDatasets`; `SpcCore` itself does not depend on synthetic generators. Synthetic generators should stay reusable by analysis harnesses, GMM handoff experiments, metric validation, and future non-SPC consumers.

---

## Entry Point

```csharp
var request = new SpcBatchRequest
{
    Metric      = SpcMetric.Euclidean,
    Proximity   = SpcProximity.Knn,
    Features    = myPoints,          // double[][]
    K           = 10,
    Temperatures = new[] { 0.01, 0.05, 0.1, 0.5, 1.0, 2.0, 5.0 },
    Steps       = 400,
    // Delta = 0 → auto-estimated from 1-NN mean
};

SpcBatchResult result = SpcBatch.Run(request);

// result.FinalSpins[T]      — cluster labels at temperature T
// result.Graph              — CSR graph (shared read-only; required for analysis)
// result.Warnings           — connectivity diagnostics
```

All metric, proximity, kernel, and delta-estimator combinations share the same `SpcBatchRequest`/`SpcBatchResult` contract. `SpcBatch.Run` is the single core entry point; internal dispatch binds standalone primitive libraries to proximity selection and coupling kernels. Thermodynamic signals such as χ(T) are derived afterward from `SpcThermo` helpers over `FinalSpins` or checkpoint history.

---

## Checkpoint / Resume

Long temperature sweeps can be paused and resumed without re-running completed temperatures. Checkpoints now also form the run-state ledger that a future SPC→GMM supervisor can monitor without attaching to the simulation process. Two modes:

**In-memory** (pass between runs in the same process):

```csharp
SpcCheckpoint cp = result.ToCheckpoint();
// Later:
request.Checkpoint = cp;
var resumed = SpcBatch.Run(request);
```

**Disk** (survives process restarts and power loss):

```csharp
// First run — set a directory:
request.CheckpointDirectory = @"C:\spc-runs";
var result = SpcBatch.Run(request);
string stamp = result.RunDirectory;  // e.g. "20260420_014139"
string manifest = result.StateManifestPath;

// Resume — pass the stamp back:
request.CheckpointDirectory = @"C:\spc-runs";
request.ResumeDirectory     = stamp;
var resumed = SpcBatch.Run(request);
```

Each completed temperature is atomically flushed before the in-memory write, so the on-disk record is always ahead. `CheckpointDirectory` is a working root; each run writes to `CheckpointDirectory/{stamp}/`, where `{stamp}` is the returned `RunDirectory` value. A companion `CheckpointDirectory/{stamp}/spc_{stamp}.manifest.json` indexes completed SPC temperature artifacts and later handoff/GMM artifacts. Legacy flat three-line `.ckpt` files remain readable for resume.

The checkpoint layer separates control-plane metadata from large data payloads:

- `spc_{stamp}.manifest.json` stays plain JSON at the run root for cheap discovery.
- `temperature-checkpoints/spc_{stamp}_{T:X16}.ckpt` is a small JSON summary with lifecycle state, cluster count, sweep count, and references to payload artifacts.
- `temperature-observations/spc_{stamp}_temperature_{T:X16}_spins_{sequence}.bin.br` stores spin observations as compressed binary delta frames.
- Future payload types live in their own folders, such as `handoff-readiness/`, `gmm-handoff-state/`, `gmm-checkpoints/`, and `gmm-results/`.

Persistence is greedy by default but configurable through `SpcBatchRequest.CheckpointPersistence`. The default writes JSON summaries plus compressed delta spin observations, without embedding large spin arrays into the JSON summary. Callers can opt out of summaries or spin observations, choose compression, switch spin observations to snapshot encoding, deliberately embed spins in summaries for a specific downstream workflow, or set `EpochSweepCount` to emit partial temperature checkpoints every N sweeps.

The binary spin frames are additive and non-redundant. The first frame anchors a temperature by writing the spin count and changed index/value pairs from empty state; later delta frames write only positions whose spin label changed since the previous frame. Replaying all ordered `TemperatureObservation` artifacts for a temperature reconstructs its full checkpoint history; the `.ckpt` summary only points at the latest frame for fast resume. Consumers can use `LoadStateManifest`, `LoadTemperatureObservationArtifacts`, `LoadTemperatureSpinHistory`, and `LoadTemperatureSpins` depending on whether they need the ledger, raw artifact list, materialized per-frame history, or only a materialized state. This fits the planned epoch model, where an epoch is simply one duty cycle of SPC work for a temperature, whether that duty cycle is a full simulation or a smaller slice. Partial temperature summaries are written with `IsComplete=false`, `CurrentSpinsArtifactId`, `EpochCount`, and `SweepCount`; final summaries flip to `IsComplete=true` and add `FinalSpinsArtifactId`. On resume, incomplete temperatures warm-start from the latest current spins and continue the remaining sweeps, while complete temperatures are skipped. The manifest records each artifact's payload type, relative path, source artifact, previous artifact, base artifact, container (`Json` or `Binary`), compression (`None`, `Brotli`, `GZip`, or `Deflate`), encoding (`Snapshot` or `Delta`), sequence number, byte length, and pipeline stage.

The manifest/state layer is intentionally broader than crash recovery. It has typed artifact slots for `TemperatureCheckpoint`, `TemperatureObservation`, `HandoffReadiness`, `GmmHandoffState`, and future GMM checkpoints/results. Temperature checkpoints carry lifecycle coordinates (`EpochCount`, `SweepCount`, `IsComplete`) and spin artifact links so a future supervisor can poll a run directory, decide whether SPC should continue or halt for handoff, and materialize exactly the state it needs without attaching to the simulation process. Thermodynamic quantities are not persisted into checkpoint summaries; supervisor-side analysis should call `SpcAnalysis.AnalyzeCheckpointSusceptibility` or related `spc-thermo/chi.cs` helpers over the accumulated spin history. The schema still reserves richer fields for the upcoming round-robin `RunEpochs` mode (`SpinCounts`, centroid snapshots, readiness diagnostics).

Cancellation (via `CancellationToken`) returns partial results for all temperatures that finished before cancellation was observed.

---

## Analysis

Post-hoc analysis works from `SpcBatchResult` without re-running the simulation:

```csharp
// Susceptibility peak → critical temperature
double[] chi = SpcAnalysis.ComputeSusceptibilityProfile(result);
int[] peaks  = SpcAnalysis.DetectPeaks(chi, prominence: 0.1);
double Tc    = result.Temperatures[peaks[0]];

// Supervisor-style analysis over checkpointed epoch frames
var checkpointChi = SpcAnalysis.AnalyzeCheckpointSusceptibility(runRoot, runStamp);

// Cluster labels at critical temperature
int[] labels = result.FinalSpins[Tc];

// Cluster purity against ground truth (synthetic data)
double purity = SpcAnalysis.ComputePurity(labels, dataset.Labels);

// Medoids (highest within-cluster coupling weight)
int[] medoids = SpcAnalysis.FindMedoids(labels, result.Graph);

// BFS shell decomposition from a medoid
int[][] shells = SpcAnalysis.BfsShells(medoids, result.Graph, maxRadius: 5);

// Cluster count profile across temperature
int[] clusterCounts = SpcAnalysis.CountClusters(result);
```

`SpcAnalysis.KL` provides the full KL-divergence family for cases where the susceptibility peak is ambiguous: global KL between adjacent-T histograms, fan-out KL radiating from each medoid's BFS shells, radial coherence, and Fisher information.

---

## Synthetic Benchmarks

Eight generators in `SyntheticData` provide ground-truth datasets for characterising metric/proximity combinations. They do not prescribe configurations — each exposes a specific structural challenge (anisotropy, non-convexity, sparse support, simplex geometry, hierarchical scale) so experiment harnesses can sweep and measure directly.

```csharp
// Canonical SPC benchmark
var data = SyntheticData.GenerateBlattThreeCluster(seed: 42);

// Convert to request (metric/proximity choices are the harness's responsibility)
var req  = data.ToSpcBatchRequest(
    metric:      SpcMetric.Euclidean,
    k:           10,
    temperatures: Enumerable.Range(1, 50).Select(i => i * 0.1).ToArray(),
    steps:       300,
    proximity:   SpcProximity.MstAugmented
);
```

`BlattHierarchy` exposes `LabelsByLevel` for multi-resolution purity scoring. `AnisotropicGaussian` exposes `ClusterCovariances` for callers that want to pool and invert for a Mahalanobis run.

---

## Text Fingerprinting (Hashish)

The `hashish/` sub-library is the reusable text/similarity substrate. Its SimHash output feeds the `Hamming` metric path, but the primitives are intentionally usable outside SPC batch execution. It provides:

- **SimHash** — 64-bit dimensionality-reducing signature; `SimHash.Compute(text) → ulong`; pass the resulting `ulong[]` as `SpcBatchRequest.SimHashes`
- **MinHash** — configurable signatures for Jaccard similarity estimation, with `MinHashLshIndex` integrated into the same surface for band/row candidate retrieval
- **CTPH** — context-triggered piecewise hashing (ssdeep-style) for near-duplicate detection
- **TLSH** — locality-sensitive hash preserving edit-distance ordering
- **BM25** — term statistics used upstream in the SimHash pipeline
- **InverseDocumentFrequency** — reusable document-frequency and IDF statistics for vectorizers and scoring
- **TokenizerPreprocessing** — shared Unicode normalization, case-folding, and word tokenization
- **WordShingler** — word-level n-gram shingles for semantic overlap and deduplication
- **JaccardContainment** — exact Jaccard similarity, asymmetric containment, and overlap coefficient
- **BloomFilter / CountMin / HyperLogLog** — approximate membership, streaming frequency, and cardinality sketches
- **NormalizedCompressionDistance** — standalone pairwise byte/text NCD, separate from the SPC batch NCD graph-builder

The metric kernel never parses text. All fingerprinting happens outside `SpcBatch`; the kernel receives typed numeric payloads such as `ulong[]` and computes numeric distances.

---

## HPC Notes

The implementation prioritises cache coherence and allocation minimisation in the inner loop:

- **CSR layout** — `CsrGraph` stores neighbours and weights as two contiguous arrays (`Targets`, `Weights`) with `RowPointers` prefix-sum offsets. SW's inner loop accesses them sequentially per row, enabling hardware prefetch.
- **Pre-allocated temporaries** — `FastUnionFind`, label arrays, and cluster-size arrays are allocated once per temperature run, outside the step loop.
- **Vectorised bond probabilities** — `TensorPrimitives` computes `1 − exp(−J/T)` for the full edge array in a single SIMD pass before the step loop.
- **Parallel temperature sweep** — temperatures are independent; `Parallel.For` dispatches them concurrently, each owning its own `PottsModel` instance.
- **Bounded min-heap for KNN** — `BoundedMinHeap(k)` keeps the _K_ smallest distances per node in O(N log K) rather than O(N log N).

---

## Design Notes

- **Primitive/application split** — metrics, proximity graph rules, coupling kernels, estimators, synthetic datasets, and Hashish routines are named by what they are. `SpcCore` remains the SPC-facing runtime layer that binds those primitives into Potts graph initialization, checkpointing, and simulation. `SpcThermo` analyzes materialized SPC state, while `SpcSynthetic` adapts reusable synthetic datasets into SPC requests.
- **Metric agnosticism in analysis** — `SpcAnalysis` operates on `CsrGraph.Weights` (coupling values in [0, 1] post-kernel) and `FinalSpins` arrays. It has no knowledge of which metric produced them.
- **No runtime dependencies beyond .NET** — `System.Numerics.Tensors` is the only non-BCL dependency. The project is callable from PowerShell via `Add-Type` or standard .NET interop.

```

***

```
