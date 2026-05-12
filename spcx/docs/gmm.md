# Gaussian Mixture Model

A flat K-component finite Gaussian mixture, fit by EM. Lives in
`projects/GaussianMixture/` (assembly `GaussianMixture.dll`, namespace
`StatisticalEstimators`).

This document is the user-facing reference for the module: how to fit,
infer, supervise, select K, and recover hierarchical cluster structure
from a flat fit.

## What it is, what it isn't

**Is:** standard finite GMM. K components in D dimensions, full
covariance, log-space E-step, Cholesky-backed inference. Comparable in
shape to MATLAB's `fitgmdist` or scikit-learn's `GaussianMixture`.

**Isn't:** Hierarchical Mixtures of Experts, Hierarchical Mixtures of
Gaussians, nested mixtures, Dirichlet Process mixtures. There is no
single global likelihood over a tree-structured latent variable.

**Hierarchy is layered on top, not built in.** The generative model at
each `GaussianMixtureModel` instance is flat. Hierarchical *clustering
output* is produced post-fit by `IComponentMergeStrategy`
implementations:

- `EntropyMergeStrategy` produces a full agglomerative dendrogram
  (Baudry et al., 2010).
- `ModalMergeStrategy` groups components by which local mode of the
  mixture density they ascend to.

Both produce an `int[]` component→cluster map, so cluster output is
algorithm-agnostic and directly comparable to HDBSCAN, k-means, or any
other algorithm publishing hard labels.

## Mental model

```
              EM fit                       merge strategy
   data  ───────────►  K components  ───────────────────►  M ≤ K clusters
  (N×D)                (μ_k, Σ_k, π_k)                     (int[N])
                       (responsibilities N×K)
```

A **component** is a single Gaussian with mean, covariance, and weight.
A **cluster** is a semantically coherent group of components — a
"compound" cluster (e.g. three intersecting ellipsoids merged into one
basin) is represented faithfully as a sub-mixture of its components.

## Quick start

```csharp
using StatisticalEstimators;

double[][] data = LoadFeatures();           // N points in D dimensions
int dimension = data[0].Length;

var gmm = new GaussianMixtureModel(k: 5, dimension: dimension);
gmm.Fit(data);                              // EM with random-sample init

int[]    labels = gmm.Predict(data);        // hard assignments
double[,] post  = gmm.PredictProba(data);   // N×K posteriors
double[]  pdf   = gmm.Pdf(data);            // density at each point
double[,] mahal = gmm.Mahal(data);          // squared Mahalanobis to each μ_k

double[][] synth = gmm.Sample(n: 1000);     // draw new points
```

## Fit modes

Four entry points. All four delegate to a single `FitCore`; the
difference is which extra signals are passed.

| Overload                                       | Use when                                |
| ---------------------------------------------- | --------------------------------------- |
| `Fit(data)`                                    | Unsupervised. The default.              |
| `Fit(data, hardLabels)`                        | Some points have certain labels (= 1.0).|
| `Fit(data, constraint)`                        | Soft external supervision.              |
| `Fit(data, hardLabels, constraint)`            | Both — pinned subset + soft prior.      |

`hardLabels` is `int[N]` where entry i is either a non-negative
component index (pinned) or −1 (unconstrained). `constraint` is any
`IResponsibilityConstraint`.

## Semi-supervised path

The semi-supervised mechanism is a **responsibility constraint** that
blends the supervisor's N×K confidence matrix `sᵢₖ` with the model's
E-step output:

```
r̂ᵢₖ ← (1 − λ) · r̂ᵢₖ  +  λ · sᵢₖ
```

`λ` typically anneals from a high value (supervisor dominates early) to
zero (free EM at convergence). The standard implementation is
`AnnealedSoftConstraint` with a linear schedule.

The module is **source-agnostic about who produces `sᵢₖ`**. Anything
that yields a row-stochastic N×K matrix is a valid supervisor:

- **Synthetic generators** — given known ground-truth labels, build
  `sᵢₖ` at controlled confidence to test how well the fit recovers
  the planted structure under varying signal strength. The primary
  validation path for this module.
- **A prior GMM pass** — `gmm.GetFinalResponsibilities()` is a valid
  `sᵢₖ` for a follow-up fit with stronger guidance.
- **External clustering** — SPC, label-propagation, k-means, or any
  other clustering algorithm that produces per-point per-class scores.

The constraint API has no dependency on any specific supervisor.

### Synthetic supervision example

```csharp
// Known labels from a synthetic generator.
int[] trueLabels = synthDataset.Labels;

// Build sᵢₖ at 80% confidence: labeled component gets 0.8, others split 0.2.
int n = trueLabels.Length, k = 5;
double[,] confidence = new double[n, k];
for (int i = 0; i < n; i++)
    for (int c = 0; c < k; c++)
        confidence[i, c] = (c == trueLabels[i]) ? 0.8 : 0.2 / (k - 1);

var constraint = new AnnealedSoftConstraint(
    confidenceMatrix: confidence,
    lambdaStart: 0.9,
    lambdaEnd: 0.0,
    lambdaHorizon: 20);   // decay over the first 20 iterations

gmm.Fit(data, constraint, maxIterations: 100);
```

### λ schedule

The decay horizon is decoupled from `maxIterations` so the supervisor's
authority fades on the caller's timeline, not the iteration cap. If
`lambdaHorizon` is left `null`, decay uses `maxIterations` and λ may
never reach `λ_end` if EM converges early.

## K-selection

Currently only BIC is implemented.

```csharp
var sweep = BicKSweep.Run(data, dimension, kMin: 1, kMax: 10, restarts: 3);
KSweepResult best = BicKSweep.BestByBic(sweep);
GaussianMixtureModel chosen = best.Model;
```

BIC tends to under-select K for non-Gaussian clusters. For algorithm-
agnostic K comparison against HDBSCAN or other clustering algorithms,
silhouette and Davies-Bouldin are the standard tools — both are
planned. AIC will land alongside.

## Merge strategies

When K is over-fit deliberately (more components than expected
clusters), a merge strategy contracts the K components down to a
cluster map.

### `EntropyMergeStrategy` — agglomerative dendrogram

Baudry et al. (2010). At each step, the pair of groups with the
smallest entropy increase from pooling is merged. Returns either a
final assignment (`Merge`) or the full sequence from K clusters down
to 1 (`MergeSequence`).

```csharp
var strategy = new EntropyMergeStrategy(targetClusters: 3);
int[] map = strategy.Merge(gmm.Components, gmm.GetFinalResponsibilities());

// Or for the full sequence:
MergeStep[] tree = EntropyMergeStrategy.MergeSequence(
    gmm.Components, gmm.GetFinalResponsibilities());
```

Operates on the responsibility matrix only — no re-fitting. Cost is
O(K²·N) total over the full sequence.

### `ModalMergeStrategy` — mode-tree clustering

Mode-based: from each component mean, gradient-ascend the mixture
log-density to the nearest local mode; group components by mode
identity. Captures the case where multiple components describe a
single compound density basin.

```csharp
var strategy = new ModalMergeStrategy();   // adaptive tolerance by default
int[] map = strategy.Merge(gmm.Components);
```

Does not consume the responsibility matrix — geometry only.

### When to use which

| Property                | Entropy                       | Modal                              |
| ----------------------- | ----------------------------- | ---------------------------------- |
| Needs responsibilities  | yes                           | no                                 |
| Produces dendrogram     | yes (full sequence)           | no (single level)                  |
| Cost                    | O(K²·N)                       | O(K · ascent_steps)                |
| Sensitive to            | classifier ambiguity          | density landscape                  |
| Compound clusters       | merges if responsibilities overlap | merges if modes coincide      |

## Inference surface

All methods are post-fit and stateless with respect to training data.

| Method               | Returns                                  |
| -------------------- | ---------------------------------------- |
| `Predict(X)`         | `int[N]` — argmax weighted log-pdf       |
| `PredictProba(X)`    | `double[N, K]` — posterior responsibilities |
| `Pdf(X)`             | `double[N]` — mixture density (may underflow) |
| `Mahal(X)`           | `double[N, K]` — squared Mahalanobis to each μ_k |
| `Sample(n)`          | `double[n][]` — draws from the mixture   |
| `GetFinalResponsibilities()` | N×K matrix from the last `Fit` call |
| `FinalLogLikelihood` | LL of training data under final params   |

## Initialization

Two paths.

**Random sample (default).** `RandomInitialize` picks K data points as
initial means, uses per-dimension sample variance as diagonal
covariance, and sets uniform weights. Pass an explicit `Random` for
determinism.

```csharp
gmm.RandomInitialize(data, new Random(seed: 42));
gmm.Fit(data);
```

**Warm start.** `InitializeWithParameters` accepts means, covariances,
and weights directly. Useful when seeds are produced upstream (a prior
GMM pass, an estimator output, an external clustering's centroids).

```csharp
gmm.InitializeWithParameters(means, covariances, weights);
gmm.Fit(data);
```

If `Fit` is called without prior initialization, random-sample init
runs automatically.

K-means++ initialization is planned but not yet implemented.

## Covariance regularization

The M-step adds a diagonal ridge to each covariance to prevent
singularity:

```csharp
var gmm = new GaussianMixtureModel(k: 5, dimension: D,
                                   covarianceRegularization: 1e-6);
```

The default (1e-6) is appropriate when features are roughly O(1) in
scale. For data on much larger or smaller scales — raw measurements,
heavily standardized features — adjust to match the natural variance
magnitude.

## Performance notes

- **E-step** is parallelized over data points via `Parallel.For`.
  Per-point `stackalloc` for the log-sum-exp scratch keeps the hot
  path allocation-free.
- **M-step** is single-threaded, accumulates into pre-allocated
  scratchpads on each `GaussianComponent`. No per-iteration heap
  traffic.
- **Cholesky factor** is preserved across iterations on each
  component, supporting both inference (Mahalanobis, log-pdf) and
  sampling without redundant decomposition.
- **`EntropyMergeStrategy`** maintains a ΔH cache and tracks alive
  primaries; merging is O(K²·N) total, not O(K³·N).
- **`ModeAscent`** has an allocation-free overload accepting a
  pre-allocated `ModeAscentScratch`; `ModalMergeStrategy.Merge` uses
  it.

## Status

| Capability                                | Status      |
| ----------------------------------------- | ----------- |
| EM with full covariance                   | done        |
| Soft responsibility constraints           | done        |
| Hard labels                               | done        |
| Combined hard + soft                      | done        |
| BIC K-sweep                               | done        |
| Entropy merging (Baudry)                  | done        |
| Modal merging                             | done        |
| Bhattacharyya pairwise distance           | done        |
| AIC / silhouette / Davies-Bouldin         | planned     |
| `Score(X)` — held-out mean log-likelihood | planned     |
| Confidence-matrix builder utility         | planned     |
| k-means++ initialization                  | planned     |
| LL trajectory diagnostic                  | planned     |

See also `docs/gmm-maturity-extentions.md` for the broader taxonomy
of mixture model families and the rationale for what is and isn't in
scope.
