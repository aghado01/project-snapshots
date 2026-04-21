Now I have everything I need. Here is the README, written directly from the source:

---

```markdown
# spcx#

**Super-Paramagnetic Clustering in C#** — a .NET library implementing the Blatt–Wiseman–Domany (1996) SPC algorithm, extended with a broad metric and proximity menu, pluggable coupling kernels, a full KL-divergence analysis suite, and PowerShell-friendly batch/checkpoint DTOs.

The core idea: model a dataset as a Potts ferromagnet on a proximity graph, sweep temperature, and read off clusters at the phase transition where susceptibility peaks. No number-of-clusters input required. The right _T_ falls out of the physics.

---

## How It Works

### 1 — Distance field (11 metrics)

Each pair of data points is measured with a caller-chosen metric: `Hamming` (on 64-bit SimHash signatures), `Euclidean`, `Canberra`, `Manhattan`, `Minkowski` (configurable _p_), `Cosine`, `JensenShannon`, `Jaccard` (threshold-binarised), `Wasserstein` (1D Earth Mover's), `Mahalanobis` (caller-supplied Σ⁻¹), or `FisherRao` (geodesic on the statistical manifold).

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

`PottsModel` runs SW on the CSR graph at each temperature. Bond probability per edge is `1 − exp(−J/T)`. Vectorised bond-probability initialisation uses `System.Numerics.Tensors` (`TensorPrimitives.Multiply/Exp/Negate/Add`). Susceptibility is measured in the second half of each sweep (equilibrium only) as the variance of cluster sizes: `χ = ⟨Σ s²⟩ / N`.

The Potts state space is `Q = 20` (default). `FastUnionFind` with path compression and union-by-size handles cluster formation and spin-flip dispatch in the inner loop.

### 5 — Phase transition detection

`SpcAnalysis` operates post-hoc on `SpcBatchResult` output. Two complementary signals locate the critical temperature:

- **Susceptibility peak** (`SpcAnalysis.Susceptibility`) — standard χ(T) peak detection
- **KL divergence family** (`SpcAnalysis.KL`) — global KL between adjacent-temperature histograms, fan-out KL from cluster medoids, radial coherence, and Fisher information across the temperature sweep

`DetectPeaks` applies a prominence threshold (fraction of signal maximum) to find local maxima in either signal.

---

## Source Layout
```

src/
├── spc.batch.cs # SpcBatchRequest/Result DTOs, SpcBatch dispatcher, ValidateGraph, RunSimulationCore
├── spc.foundation.cs # Edge, CsrGraph (symmetric CSR builder), BoundedMinHeap, FastUnionFind
├── spc.potts.cs # PottsModel — SW simulation, SimulationResult
├── spc.analysis.cs # SpcAnalysis base: histograms, medoids, BFS shells, peak detection, purity scoring
├── spc.synthetic.cs # SyntheticData base: SyntheticDataset DTO, ToSpcBatchRequest adapter,
│ # sampling primitives (Box-Muller, Marsaglia-Tsang Gamma, Dirichlet),
│ # geometry primitives (sphere centroids, Gram-Schmidt, Haar rotation, covariance)
├── spc.checkpoint.cs # SpcCheckpoint DTO, disk flush/load logic, atomic .ckpt file protocol
│
├── analysis/
│ ├── analysis.chi.cs # Susceptibility peak detection
│ ├── analysis.kl.cs # KL divergence: global, fan-out, radial coherence, Fisher information
│ └── analysis.mhbs.cs # Multi-hop boundary sharpness analysis
│
├── deltas/
│ ├── SpcBatch.EstimatorMean.cs # Auto-delta: mean of 1-NN distances
│ └── SpcBatch.EstimatorMedian.cs # Auto-delta: median of 1-NN distances
│
├── hashish/ # Text fingerprinting (feed the Hamming metric path)
│ ├── simhash.cs # 64-bit SimHash — Compute(text) → ulong
│ ├── minhash.cs # MinHash with configurable band/row LSH
│ ├── ctph.cs # Context-Triggered Piecewise Hashing (ssdeep-style)
│ ├── tlsh.cs # TLSH locality-sensitive hash
│ └── bm25-stats.cs # BM25 term statistics (used upstream by SimHash pipeline)
│
├── kernels/
│ ├── SpcBatch.KernelGaussian.cs
│ ├── SpcBatch.KernelCauchy.cs
│ ├── SpcBatch.KernelLaplacian.cs
│ └── SpcBatch.KernelLinear.cs
│
├── metrics/ # 11 metric-specific graph builders (partial class SpcBatch)
│ ├── SpcBatch.Hamming.cs
│ ├── SpcBatch.Euclidean.cs
│ ├── SpcBatch.Canberra.cs
│ ├── SpcBatch.Manhattan.cs
│ ├── SpcBatch.Minkowski.cs
│ ├── SpcBatch.Cosine.cs
│ ├── SpcBatch.JensenShannon.cs
│ ├── SpcBatch.Jaccard.cs
│ ├── SpcBatch.Wasserstein.cs
│ ├── SpcBatch.Mahalanobis.cs
│ └── SpcBatch.FisherRao.cs
│
├── proximities/ # 4 graph construction rules (partial class SpcBatch)
│ ├── SpcBatch.Knn.cs
│ ├── SpcBatch.MutualKnn.cs
│ ├── SpcBatch.EpsilonBall.cs
│ └── SpcBatch.MstAugmented.cs
│
└── synthetic/ # 8 ground-truth dataset generators (partial class SyntheticData)
├── SyntheticData.BlattThreeCluster.cs # The canonical SPC benchmark from the original paper
├── SyntheticData.BlattHierarchy.cs # Multi-scale nested clusters (hierarchical ground truth)
├── SyntheticData.AnisotropicGaussian.cs # Correlated ellipsoidal clusters; exposes ClusterCovariances
├── SyntheticData.GaussianManifold.cs # Clusters on a curved manifold
├── SyntheticData.Simplex.cs # Dirichlet-distributed probability vectors
├── SyntheticData.SparseSupports.cs # Clusters with non-overlapping sparse feature support
├── SyntheticData.SpatialBlobs.cs # Simple isotropic blobs at varying densities
└── SyntheticData.TwoMoons.cs # Non-convex interleaved crescents

````

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

// result.Susceptibility[i]  — χ at Temperatures[i]
// result.FinalSpins[T]      — cluster labels at temperature T
// result.Graph              — CSR graph (shared read-only; required for analysis)
// result.Warnings           — connectivity diagnostics
````

All metric, proximity, kernel, and delta-estimator combinations share the same `SpcBatchRequest`/`SpcBatchResult` contract. `SpcBatch.Run` is the single entry point; internal dispatch is via partial-class metric and proximity builders.

---

## Checkpoint / Resume

Long temperature sweeps can be paused and resumed without re-running completed temperatures. Two modes:

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

// Resume — pass the stamp back:
request.CheckpointDirectory = @"C:\spc-runs";
request.ResumeDirectory     = stamp;
var resumed = SpcBatch.Run(request);
```

Each completed temperature is atomically flushed to a `spc_{stamp}_{T:X16}.ckpt` file before the in-memory write, so the on-disk record is always ahead. Cancellation (via `CancellationToken`) returns partial results for all temperatures that finished before cancellation was observed.

---

## Analysis

Post-hoc analysis works from `SpcBatchResult` without re-running the simulation:

```csharp
// Susceptibility peak → critical temperature
double[] chi = result.Susceptibility;
int[] peaks  = SpcAnalysis.DetectPeaks(chi, prominence: 0.1);
double Tc    = result.Temperatures[peaks];

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

The `hashish/` sub-library feeds the `Hamming` metric path. It provides:

- **SimHash** — 64-bit dimensionality-reducing signature; `SimHash.Compute(text) → ulong`; pass the resulting `ulong[]` as `SpcBatchRequest.SimHashes`
- **MinHash** — configurable LSH bands/rows for Jaccard similarity estimation
- **CTPH** — context-triggered piecewise hashing (ssdeep-style) for near-duplicate detection
- **TLSH** — locality-sensitive hash preserving edit-distance ordering
- **BM25** — term statistics used upstream in the SimHash pipeline

The metric kernel never parses text. All fingerprinting happens outside `SpcBatch`; the kernel receives `ulong[]` and computes popcount-based Hamming distances.

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

- **Partial-class architecture** — `SpcBatch` and `SyntheticData` are `static partial` classes. Each metric, proximity, kernel, and synthetic generator lives in its own file; adding a new metric means adding one `.cs` without touching the dispatcher (new `case` in the `switch`).
- **Metric agnosticism in analysis** — `SpcAnalysis` operates on `CsrGraph.Weights` (coupling values in [0, 1] post-kernel) and `FinalSpins` arrays. It has no knowledge of which metric produced them.
- **No runtime dependencies beyond .NET** — `System.Numerics.Tensors` is the only non-BCL dependency. The project is callable from PowerShell via `Add-Type` or standard .NET interop.

```

***

```
