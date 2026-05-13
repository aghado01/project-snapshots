# Numerical Primitives — Layered Design

> **Status:** DRAFT v0.2 — product-manifold framing corrected; three-axis dispatch added
> **Date:** 2026-05-07
> **Purpose:** Define the layered architecture for numerical primitives (linear algebra, manifolds, losses, optimisation, sampling, resampling) and the estimator façades that compose them. Captures the refactor boundary between today's monolithic `src/estimators/` folder and the target layered shape, the rationale for relocating application-specific helpers, and the design seams for the SPC→GMM handoff (robust location + scatter on a product manifold).
> **Sibling documents:** [project-primer.md](./project-primer.md) (overall design philosophy); [clustering-primitive.md](./clustering-primitive.md) (parallel tiered-primitive precedent); [spc-maturity.md](./spc-maturity.md) and [gmm-maturity-extentions.md](./gmm-maturity-extentions.md) (consumer-side maturity scopes).

---

## 1. Why this exists

This refactor exists to support the **Park & You (2026) geometric median on product manifolds** — the central use case driving the design. SPC's downstream consumers (notably the SPC→GMM handoff in [gmm-maturity-extentions.md](./gmm-maturity-extentions.md)) need a robust location estimator on product manifolds where heterogeneous geometric variables are jointly observed: spatial × spectral, mean × covariance (Bures-Wasserstein), feature × graph-position. The Park & You paper proves the foundational theory — existence and uniqueness on product Hadamard manifolds, 50% breakdown, Lipschitz stability — and supplies two algorithms (subgradient descent and product-aware Weiszfeld) that update components independently while incorporating the coupling structure of the joint objective:

```
F_median(p, q) = Σᵢ wᵢ √(d_M(p, xᵢ)² + d_N(q, yᵢ)²)
```

The non-separability of the L¹ objective — `√(d_M² + d_N²)` couples the two factors through the shared denominator — is precisely what existing single-manifold median methods cannot handle, and is the reason a refactor is required rather than a rebrand of existing code. From the paper (§2.2): _"the geometric median involves an ℓ¹-like objective that couples the components through a norm structure. This non-separability introduces both theoretical and computational challenges, **precluding the direct application of existing median methods designed for single manifolds.**"_

The current `src/estimators/` folder reflects this need imperfectly. Three paradigms coexist that share structural pattern but no implementation:

| Paradigm                           | Files                                                              | Coverage                                          |
| ---------------------------------- | ------------------------------------------------------------------ | ------------------------------------------------- |
| Scalar over distance samples       | `DeltaMuMean.cs`, `DeltaMuMedian.cs`, `SigmaEstimatorMad.cs`       | Special case for SPC bandwidth (relocates per §9) |
| Vector Euclidean with index/weight | `WeightedMean.cs`, `WeightedGeometricMedian.cs`, `MetricMedoid.cs` | Single-factor flat case                           |
| Riemannian product                 | `ManifoldMedian.cs` (Weiszfeld + subgradient on `M×N`)             | Park & You algorithm — current state of the art   |

**The third paradigm is the target.** The unification work is to recognise that the first two collapse to it: vector Euclidean is `IsFlat = true` on a single factor; the scalar SPC helper isn't an estimator at all (§9). The mathematical generality of the Park & You product-manifold formulation already subsumes both — what's missing is the trait-dispatched specialisation that lets the flat case avoid paying for log/exp it doesn't need, and the variadic generalisation that lets `k > 2` factor manifolds compose without rewriting the solver.

Two orthogonal dials describe the unified design:

- **Geometry dial** — flat (Euclidean) ⊂ single Riemannian factor ⊂ product Riemannian (`k` factors). Product manifolds with `k = 1` reduce to single-factor; with all-flat factors the product reduces to flat. Single dispatch, three regimes.
- **Loss dial** — L² (mean) ⊂ L¹ (median) ⊂ robust M-estimators (Huber, Tukey biweight). The L¹ case is the Park & You setting; L² and robust extensions plug into the same framework.

Both dials specialise via static-abstract trait dispatch (§3); the JIT erases dead branches at specialisation time. **The Park & You algorithm is the load-bearing path; flat and L² are JIT-eliminated specialisations of it, not separate algorithms.**

This document records the architectural shape; commits to the design boundary, not the implementation. None of the layout below is built yet.

## 2. Layered architecture

```
linalg/         ← matrix primitives (Cholesky today; future: SVD, QR, eigenvalue)
Manifolds/      ← geometric primitives (IManifold + concrete factor manifolds)
Losses/         ← robust statistical losses (IRobustLoss + L1, L2, Huber, Tukey)
Optimization/   ← solvers (IRLS, Subgradient, KarcherFlow fast path; future BFGS/CG/SGLD)
Sampling/       ← RNG primitives (index sampling — MATLAB randsample analog)
Resampling/     ← patterns (Bootstrap, Jackknife) calling user fns on resampled indices
Estimators/     ← statistical façades that compose the above
  Location/     ← continuous, ambient-space output (Mean, Median + optional Scatter)
  InSample/     ← discrete, returns data indices (Medoid)
  Scale/        ← dispersion (Mad scalar; Scatter multivariate; consistency factors)
  Diagnostics/  ← LocationEstimate, MedoidEstimate
```

Dependency arrows are acyclic and one-directional:

- `Estimators` → `{Optimization, Sampling, Resampling, Manifolds, Losses, linalg}`
- `Optimization` → `{Manifolds, Losses}`
- `Resampling` → `Sampling`
- `Manifolds`, `Losses`, `linalg`, `Sampling` — no internal dependencies

The split between `Optimization/` and `linalg/` is deliberate: linalg is for matrix decompositions (anyone may consume); Optimization is for iterative solvers parameterised by manifolds and losses (only Estimators-shaped consumers today, future SGLD outer-loop sampler later). Putting IRLS in linalg would invert the layering — linalg should be a foundation everything else depends on, not a consumer of higher-level abstractions.

## 3. Estimator unification — the IRLS axis

The unifying observation: **mean, median, and robust M-estimators are all minimisers of `Σ wᵢ ρ(d(p, xᵢ))` for different choices of `ρ`**, solved by iteratively reweighted least squares (IRLS) on a manifold.

| Today                       | ρ                    | IRLS form                                                                |
| --------------------------- | -------------------- | ------------------------------------------------------------------------ |
| Karcher / Fréchet mean      | `r²/2`               | one iteration with constant weights → closed form                        |
| Weiszfeld geometric median  | `r`                  | iterate with `wᵢ ← wᵢ / d`, plus singularity policy at coincident points |
| Robust M-estimator (future) | Huber, Tukey, Hampel | iterate with `wᵢ ← wᵢ · ψ(d) / d`                                        |

Translation table from current code to target:

- `LocationMean.WeightedEuclidean` → `IRLS + EuclideanVectorManifold + L2Loss` (closed-form path)
- `LocationGeometricMedian.WeightedEuclidean` → `IRLS + EuclideanVectorManifold + L1Loss`
- `ProductManifoldFrechetMean.Compute` → `IRLS + ProductManifold(...) + L2Loss`
- `ProductManifoldMedian.ComputeWeiszfeld` → `IRLS + ProductManifold(...) + L1Loss`
- `ProductManifoldMedian.ComputeSubgradient` → `Subgradient + ProductManifold(...) + L1Loss`

### Trait-dispatched specialisation

The unified solver dispatches via static-abstract interface members. `IRiemannianManifold<TPoint>` exposes `IsFlat` as a `static abstract bool`; `IRobustLoss` exposes `IsClosedForm` and `IsSingularAtZero` analogously. With `where TManifold : struct, IRiemannianManifold<TPoint>` and `where TLoss : struct, IRobustLoss`, the JIT specialises on each `(TManifold, TLoss)` pair and physically deletes branches whose static-abstract guard evaluates to a compile-time constant. The Euclidean binary contains no LogMap/ExpMap calls; the Poincaré binary contains no ambient-shortcut code. Zero runtime dispatch overhead, zero virtual calls.

### Product manifold via composed traits — the load-bearing path

`ProductManifold<TA, TB, ...>` is itself an `IRiemannianManifold<(TPointA, TPointB, ...)>` struct. The Park & You coupling structure is preserved automatically: `ProductManifold.Distance` returns the joint `√(Σⱼ d_j²)`, which feeds the shared `sumInverseDistances` denominator inside the unified Weiszfeld iteration. The per-factor `LogMap` writes into separate slices of a concatenated tangent buffer of size `Σⱼ dim_j`, and `ExpMap` reads back from those slices. The result is exactly the paper's "component-wise updates with coupled normalisation" — emergent from the trait composition, not specially handled.

Two side-effects fall out of the trait composition:

- **`ProductManifold.IsFlat = TA.IsFlat && TB.IsFlat && ...`** — a product of all-flat factors is flat, so the ambient shortcut applies automatically. A single non-flat factor forces the round-trip path. No special case at the solver level.
- **Scalar Euclidean is `EuclideanVectorManifold(dim = 1)`**; single-factor manifolds are `ProductManifold` with one element; today's hard-wired `M × N` is the two-factor case. The whole geometry dial collapses to one struct family.

### Hybrid Weiszfeld + subgradient is the default

Park & You §4.1.2 explicitly recommends the hybrid scheme, citing Beck & Sabach (2015): _"In practice, hybrid schemes that use Weiszfeld iterations when far from singularities and fall back to subgradient updates when near data points can be particularly effective."_ The existing `ManifoldMedian.cs` ships both `ComputeWeiszfeld` and `ComputeSubgradient` for exactly this reason. The unified solver should expose **hybrid as the default mode**:

- Smooth regime (`min_i d(p, xᵢ) > threshold`): Weiszfeld update.
- Near-singular regime (`min_i d(p, xᵢ) ≤ threshold`): subgradient step with decaying step size `η_k = η_0 / √(k+1)`.
- Exact coincidence (`min_i d(p, xᵢ) < ε`): optimality check; return early if `‖Σ_{j≠i} (w_j/d_j) log_p(x_j)‖ ≤ wᵢ`, otherwise zero-weight the singular point and continue.

Pure-Weiszfeld and pure-Subgradient modes remain accessible via `HybridMode = WeiszfeldOnly` (threshold = 0) and `HybridMode = SubgradientOnly` (threshold = ∞), for callers who want the unmodified behaviour of the original solvers.

### Three-axis dispatch

The unified solver dispatches on three orthogonal axes. They compose multiplicatively:

| Axis                                                  | Values                                                                                            | Stage                                 | Cost                                   |
| ----------------------------------------------------- | ------------------------------------------------------------------------------------------------- | ------------------------------------- | -------------------------------------- |
| **Geometry topology** (`TManifold.IsFlat`)            | Flat (ambient shortcut) <br> Curved (LogMap/ExpMap round-trip)                                    | Compile-time, JIT-erased              | Zero (dead-code elimination)           |
| **Algorithm regime** (hybrid mode)                    | Weiszfeld (smooth) <br> Subgradient (near singularity) <br> Optimality return (exact coincidence) | Runtime, per-iteration distance check | One branch per iteration after warm-up |
| **Singularity policy** (within Weiszfeld branch only) | `Regularise(ε)` — soft `1/max(d, ε)` <br> `OptimalityCheck` — return early at coincidence         | Runtime, configured per-call          | Negligible                             |

The 2×2 matrix of (geometry × algorithm) gives the four code paths the JIT-specialised solver actually emits:

|                                    | Flat (`IsFlat = true`, JIT-erased)                                       | Curved (`IsFlat = false`, JIT-erased)                                       |
| ---------------------------------- | ------------------------------------------------------------------------ | --------------------------------------------------------------------------- |
| **Weiszfeld** (smooth regime)      | Ambient weighted average: `p_new = Σᵢ ŵᵢ · xᵢ`                           | Tangent round-trip: `LogMap → AddScaled → ExpMap`                           |
| **Subgradient** (near singularity) | Ambient unit-vector descent: `p_new = p − η · Σᵢ wᵢ · (p − xᵢ)/‖p − xᵢ‖` | Tangent unit-vector descent: `LogMap → normalise → AddScaled → −η · ExpMap` |

All four cells exist; none subsume each other. Geometry is about _how_ to compute an update step; algorithm regime is about _which_ update to compute. They don't interact.

### What stays specialised

- **Medoid is genuinely a different algorithm class** (discrete argmin over a candidate set, no log/exp, no iteration). It lives in `Estimators/InSample/Medoid.cs`, parameterised on `IRobustLoss` so the implicit L¹ assumption in today's `MetricMedoid` becomes explicit and the L² discrete case becomes available without code duplication.
- **MAD is a scale estimator, not a location one.** It needs a location injected and returns a scale. Lives under `Estimators/Scale/`, sibling to `Scatter`. Conceptually pairs with location estimators rather than competes with them.
- **Scalar conveniences for SPC bandwidth selection** (`DeltaMean`, `DeltaMedian`) — see §9; these are not estimator primitives at all.

### Solver options

The current ad-hoc Weiszfeld implementations diverge on real algorithmic choices that must surface as options, not bake in silently:

| Option                                      | Values                                                                                                                                                                              | Where it shows up today                                                                                   |
| ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| **Hybrid mode**                             | `Hybrid` (default) — Weiszfeld with subgradient fallback near singularities <br> `WeiszfeldOnly` — pure Weiszfeld with singularity policy <br> `SubgradientOnly` — pure subgradient | `ProductManifoldMedian` ships both as separate methods; default behaviour today is implicit caller choice |
| **Singularity policy** (Weiszfeld branch)   | `Regularise(ε)` — replace `1/d` with `1/max(d, ε)` <br> `OptimalityCheck` — test the gradient norm at exact coincidence, return early if optimal                                    | `LocationGeometricMedian` uses Regularise; `ProductManifoldMedian.ComputeWeiszfeld` uses OptimalityCheck  |
| **Subgradient threshold**                   | `double` — distance to nearest data point below which to switch to subgradient under `Hybrid` mode                                                                                  | New; not exposed in current code                                                                          |
| **Step-size schedule** (subgradient branch) | `Constant(η)` <br> `Decaying(η₀, schedule = 1/√(k+1))` — paper's default                                                                                                            | `ProductManifoldMedian.ComputeSubgradient` hard-codes decaying                                            |
| **Convergence criterion**                   | `Absolute(τ)` — `shift < τ` <br> `RelativeToNorm(τ)` — `shift ≤ τ · (1 + ‖p‖)`                                                                                                      | `ProductManifoldMedian` is Absolute; `LocationGeometricMedian` is RelativeToNorm                          |

All policies are valid. Surface them as options; do not pick one and silently drop the others.

## 4. Naming ontology

Today's names tangle four orthogonal axes into ad-hoc compound terms:

| Axis           | Values                                               | Today's encoding                                                                              |
| -------------- | ---------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| Output domain  | continuous (ambient point) vs. discrete (data index) | "Medoid" implies discrete by convention                                                       |
| Loss function  | L¹ vs. L² vs. robust M                               | "Mean" / "Median" / "Geometric median" / "Frechet mean" — three vocabularies for two concepts |
| Geometry       | scalar / Euclidean vector / Riemannian               | "Delta" prefix, "Location" prefix, no prefix                                                  |
| Estimator role | location vs. scale vs. shape                         | "Location" prefix; "Sigma" / "Mad" for scale                                                  |

Target: let the **manifold parameter** carry the geometry, the **loss parameter** carry the loss, and the **type name** carry only the output-domain and role axes:

- `Location.Mean` — continuous L² minimiser. On Euclidean = arithmetic / weighted mean; on Riemannian = Fréchet/Karcher mean. The Fréchet/Karcher names live in docstrings only.
- `Location.Median` — continuous L¹ minimiser. On Euclidean = geometric median (Weiszfeld); on Riemannian = Riemannian Weiszfeld. Optionally also returns scatter — see §5.
- `InSample.Medoid` — discrete minimiser over a candidate set. Parameterised on `IRobustLoss`; defaults to L¹ (today's `MetricMedoid` behaviour).
- `Scale.Mad` — scalar Median Absolute Deviation. Takes location injected; returns scale. Existing API preserved.
- `Scale.Scatter` — multivariate analog of MAD. Tangent-space scatter computed at a robust location anchor.

The `Median ↔ Medoid` pair is the natural standard-literature mapping for the continuous-vs-discrete L¹ split (it is exactly the etymology — _medi-oid_ = "median-like, but in-sample"). The `Mean` continuous estimator has no widely-used discrete counterpart; if needed, `InSample.Medoid` accepts an `L2Loss` and produces it without new naming.

## 5. Joint location + scatter API

The robust scatter matrix is a **free byproduct of the converged Weiszfeld iteration**. The final iteration already has the inverse distances `1/d(xᵢ, μ)` and the tangent vectors `log_μ(xᵢ)`; the scatter is the inverse-distance-weighted outer product:

```
Σ_raw = (1 / Σ w̃ᵢ) Σᵢ w̃ᵢ (vᵢ vᵢᵀ),   w̃ᵢ = wᵢ / max(dᵢ, ε),  vᵢ = log_μ(xᵢ)
```

That promotes scatter from a separate routine to an optional output of the median solver. The API mirrors `Scale.Mad`'s raw vs. scaled split:

```csharp
// Today (Scale.Mad scalar):
double Mad.Compute(values, location, scratch);
double Mad.ComputeScaled(values, location, scratch, consistencyFactor);

// Target (Location.Median continuous + optional Scatter):
MedianResult<TPoint>        Median.Compute(manifold, data, weights, init, opts);
MedianScatterResult<TPoint> Median.ComputeWithScatter(manifold, data, weights, init,
                                                      Span<double> scatterDestination,
                                                      double consistencyFactor = 1.0,
                                                      opts);
```

Cost of `WithScatter` over base `Compute`: one extra `O(N · D²)` outer-product accumulation pass after convergence, plus the destination matrix. Zero new heap allocation if the destination is a caller-supplied span.

For the **product manifold** case, the scatter is over the _joint_ tangent space at the converged location. This is what the SPC→GMM handoff wants when its features are a product (e.g. spatial × spectral), since the GMM lives in the joint feature space too.

## 6. Consistency factor tiers

Calibrating raw scatter into an unbiased estimator of a target distribution's covariance requires a scalar consistency factor `c(D)`. Unlike scalar MAD's constant `1.4826`, the **multivariate factor is dimension-dependent**: in higher dimensions a standard Gaussian concentrates in a shell at radius `≈ √D` from the mean, and the inverse-distance weighting interacts with that radial distribution in a way that depends on `D`.

Calibration is a property of the **target distribution**, not the data. The natural tool is plain Monte Carlo (sample from the reference distribution, run the same scatter routine, take the ratio); MCMC would only be needed for distributions where direct sampling is unavailable, which doesn't apply to Gaussian or Laplace references.

| Tier                    | What ships                                                            | Use case                                                                                                                                     |
| ----------------------- | --------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| 1. Raw (`factor = 1.0`) | nothing extra                                                         | SPC→GMM handoff (intentional shrinkage prevents the E-step from swallowing boundary points; the GMM's M-step will inflate Σ as it converges) |
| 2. User-defined scalar  | one `double` parameter                                                | Caller has done their own calibration                                                                                                        |
| 3. User-defined rule    | `Func<int, double>` callback                                          | Caller wants dimension-aware lookup or a custom presets table                                                                                |
| 4. Built-in MC helper   | `Scatter.EstimateGaussianConsistency(int dim, int samples, int seed)` | Library computes the factor once at startup; result caches                                                                                   |
| 5. Tabulated presets    | Ship a table at common D values for `Gaussian`, `Laplace`             | After literature review (see references); avoids the MC cost on every startup                                                                |

Worth a literature pass before writing the MC helper: the **spatial sign covariance matrix** (Visuri & Koivunen; Croux & Haesbroeck; Tyler's M-estimator) has closely-related consistency results. The Weiszfeld scatter is not identical to the spatial-sign matrix — the former weights by `1/d`, the latter projects to the unit sphere — but the analytical machinery is the same family. There may be a closed-form or asymptotic expression that obviates the MC step.

Tiered usage discipline: **for the SPC→GMM handoff specifically, use raw scatter** (factor = 1.0). The intentional shrinkage is the right behaviour for the first E-step. General-purpose `FitRobustGaussian(data)` callers want tiers 4 or 5.

## 7. Optimization layer

Lives under `Optimization/` as a sibling to `linalg/`, not inside it. Hosts:

- `Irls.cs` — the unified solver. Generic over `(TManifold, TLoss, TPoint)` with static-abstract trait dispatch (§3). Implements the three-axis dispatch: geometry shortcut (compile-time), hybrid Weiszfeld+subgradient regime (runtime, default mode), singularity policy (configured). Closed-form short-circuit when `TLoss.IsClosedForm` (L² case = Karcher flow in one iteration). Subgradient is **co-housed inside Irls** rather than a separate solver, because the hybrid mode requires both update schemes in one loop. Pure-Weiszfeld and pure-Subgradient remain accessible via the hybrid mode option.
- Helper sub-primitives shared across solvers and configured via `IrlsOptions`: `StepSize.Constant`, `StepSize.Decaying(η₀)` (paper's `η₀/√(k+1)`), `Convergence.Absolute(τ)`, `Convergence.RelativeToNorm(τ)`, `SingularityPolicy.Regularise(ε)`, `SingularityPolicy.OptimalityCheck`, `HybridMode.{Hybrid, WeiszfeldOnly, SubgradientOnly}`, `SubgradientThreshold(d)`. These are the §3 knobs made first-class.
- (Future) `BFGS.cs`, `ConjugateGradient.cs`, `Sgld.cs` — placeholders for downstream needs. SGLD specifically anticipated in [src/clustering/spc/thermo/mhbs.cs](../src/clustering/spc/thermo/mhbs.cs)'s frozen design note as a potential outer-loop sampler over coupling/kernel scale; it belongs here, not in Estimators or in spc-thermo.

`KarcherFlow` as its own file vs. an IRLS short-circuit: defaulting to short-circuit. Keeps one entry point; the L² case is conceptually "IRLS that converges in one step." A standalone `KarcherFlow.cs` is defensible only if it accumulates documentation/specialisation that doesn't belong on the IRLS surface.

`Medoid` does **not** live here. It has no manifold/loss/iteration structure; it's a brute-force argmin over a candidate set parameterised on a distance callable. Its natural neighbours are PAM, CLARANS, k-medoids — clustering algorithms, not numerical optimisation. Stays in `Estimators/InSample/`.

## 8. Sampling and Resampling

MATLAB's reference design is a **three-tier separation** worth mirroring:

| MATLAB                             | Role                                    | What it knows about                                 |
| ---------------------------------- | --------------------------------------- | --------------------------------------------------- |
| `randsample(n, k, replace, w)`     | Index-only RNG primitive                | integers `1..N`, weights, replacement               |
| `datasample(x, k, dim, ...)`       | Data-slicing wrapper returning `[y, i]` | how to slice an array using the indices             |
| `bootstrp(B, fn, data)` / `bootci` | Resampling loop                         | how to call a user function `B` times and aggregate |
| `jackknife(fn, data)`              | Leave-one-out loop                      | companion to bootstrap; needed for BCa CIs          |

`datasample` is a thin layer on `randsample`. The crucial detail: `datasample` returns **both** `y` _and_ `i`, so callers can sample indices once and use them to slice multiple parallel arrays consistently. That is the pattern this project should adopt at the index level.

### C# adaptation

```
Sampling/
  IndexSampler.cs                        # randsample analog
                                         #   - with/without replacement
                                         #   - optional weights
                                         #   - takes Random or seed
                                         #   - writes to Span<int> destination (zero-alloc)
  WeightedSamplerWithoutReplacement.cs   # the wswor case is non-trivial; deserves its own file
                                         # (defer until a real consumer appears)
Resampling/
  Bootstrap.cs                           # bootstrp analog with indices-only callback
  Jackknife.cs                           # leave-one-out
  ConfidenceInterval.cs                  # Percentile, Basic, Normal, BCa over replicates
```

Two simplifications vs. MATLAB:

1. **Skip the data-slicing wrapper.** In MATLAB it exists to handle N-D arrays, dataset/table types, and column-vs-row ambiguity. In C# the data shape is explicit and the caller already has `Span<T>` and indexed access; just give them indices and let them slice themselves. This is also strictly faster — the bootstrap loop never has to materialise resampled arrays.

2. **Indices-only callback API.** The user function receives a `ReadOnlySpan<int>` of resampled indices and decides itself whether to copy:
   ```csharp
   public static T[] Bootstrap.Run<T>(
       int B,
       int sampleSize,
       int populationSize,
       Func<ReadOnlySpan<int>, T> statisticFn,
       Random rng,
       BootstrapOptions opts = default);
   ```
   For the geometric-median + scatter use case, `statisticFn` passes the indices straight into `Median.ComputeWithScatter(...)` without ever copying the points array.

### Why bootstrap belongs in this codebase

Once `Bootstrap.Run` exists, it pays for itself across the project:

- CI on the geometric median (cluster-centre uncertainty)
- CI on scatter eigenvalues (cluster-shape uncertainty)
- Bagged scatter (variance reduction by averaging Σ_raw across resamples)
- Stability diagnostics for SPC clusters under resampling
- Bootstrap hypothesis tests, e.g. for merge-temperature analysis in [src/clustering/spc/thermo/kl.cs](../src/clustering/spc/thermo/kl.cs)
- CI on the SPC delta-bandwidth (see §9)

### MATLAB primitives already on hand

The local MATLAB toolchest snapshot at `C:\Users\azrie\PDenv\UserGithub\project-snapshots\matlab-toolchest\` already contains `randsample.m` and `datasample.m` as direct references for the index-sampling layer. `bootstrp`, `bootci`, and `jackknife` from MathWorks' stats toolbox are not in this snapshot but are available locally for reference when the resampling layer is built.

## 9. Application-specific helpers move out

`DeltaMuMean.cs` and `DeltaMuMedian.cs` are not estimator primitives:

- They take no manifold, no loss, no weights — scalar summary statistics over a span.
- Their naming bakes in the application ("delta" = SPC coupling-kernel bandwidth).
- They are 5–10 lines each, dispatched via the `DeltaEstimator { Mean, Median }` enum in [src/clustering/spc/batch.cs](../src/clustering/spc/batch.cs), and consumed by exactly one call site (the auto-δ computation from 1-NN distances).

They move into the SPC graph initialisation as `internal` helpers. The `DeltaEstimator` enum, the auto-δ call site, and the helper functions then sit together as one cohesive unit; the kernel just receives `delta` as a parameter and stays unaware of how it was chosen. The shared `MedianOfSorted` micro-helper question dissolves — `Scale.Mad` keeps its own private 3-line version, SPC keeps its own.

The general principle: **anything whose naming bakes in an application-layer concept is a candidate for relocation to that layer.** Estimator primitives are, by definition, application-agnostic.

## 10. Tier philosophy

The unified estimator framework itself ships as one piece — there's no incremental version of the unification that's coherent. The **extensibility** is tier-shaped:

| Tier             | Status                                                                                                  | Trigger                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ---------------- | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1 (immediate)    | Scoped here                                                                                             | Park & You product-manifold Weiszfeld + subgradient with hybrid mode as default, preserving the coupling-via-shared-denominator structure of §3-§4 of the paper; static-abstract trait dispatch on geometry (`IsFlat`) and loss (`IsClosedForm`, `IsSingularAtZero`); L¹ and L² losses; `EuclideanVectorManifold` and variadic `ProductManifold` composed via traits; closed-form L² short-circuit; Medoid; Mad; raw Scatter (Weiszfeld-weighted, joint over product tangent space); Bootstrap with percentile CIs |
| 2 (next)         | Triggered by the SPC→GMM handoff and the robust-Gaussian fit use cases                                  | Huber and Tukey biweight losses; Gaussian/Laplace consistency factors via MC helper; Jackknife + BCa CIs                                                                                                                                                                                                                                                                                                                                                                                                           |
| 3 (open horizon) | Triggered by specific manifold consumers                                                                | Bures-Wasserstein manifold for GMM Σ-coupling (Park & You §5 example); Poincaré disk; Fisher-Rao simplex; Riemannian Mahalanobis distance composing the above                                                                                                                                                                                                                                                                                                                                                      |
| 4 (aspirational) | Triggered by SGLD landing per [src/clustering/spc/thermo/mhbs.cs](../src/clustering/spc/thermo/mhbs.cs) | SGLD outer-loop sampler in `Optimization/`; full robust covariance via MCD or Tyler's M on the tangent space; tabulated consistency-factor presets                                                                                                                                                                                                                                                                                                                                                                 |

Tiers 2–4 are additive against the tier-1 surface, not redesigns. Anything in tiers 2–4 that lacks a concrete consumer remains unbuilt; this document captures the seam, not a roadmap.

## 11. Open and resolved decisions

### Resolved (v0.2)

These were live in v0.1 and have been settled in v0.2:

- **R1. Trait dispatch via static abstract members.** The unified solver dispatches on `IRiemannianManifold<TPoint>.IsFlat`, `IRobustLoss.IsClosedForm`, `IRobustLoss.IsSingularAtZero` as static-abstract traits. Each concrete `(TManifold, TLoss)` pair JIT-specialises with dead branches eliminated. (See §3 trait-dispatched specialisation.)
- **R2. Scatter as output channel, not solver concern.** `Optimization.Irls.Solve` exposes converged IRLS weights via an optional `Span<double> finalIrlsWeights` parameter; `Estimators.Scale.Scatter.ComputeFromConvergedIrls` consumes them. The two compose in the `Estimators.Location.Median` façade as `Compute` and `ComputeWithScatter`. (See §5.)
- **R3. Product manifold via composed trait.** `ProductManifold<TA, TB, ...>.IsFlat => TA.IsFlat && TB.IsFlat && ...` propagates flatness through composition automatically. Variadic generalisation; the existing two-factor case is `ProductManifold` with two type parameters. (See §3 product manifold via composed traits.)
- **R4. Singularity policy on solver options, not on `IRobustLoss`.** The L¹ singularity is a property of the loss (encoded as `TLoss.IsSingularAtZero`), but the _handling_ (regularise vs. optimality-check) is a per-call solver choice. Settled.
- **R5. Hybrid Weiszfeld + subgradient is the default mode.** Per Park & You §4.1.2 / Beck & Sabach (2015). Pure-Weiszfeld and pure-Subgradient remain accessible. Settled. (See §3 hybrid Weiszfeld + subgradient.)
- **R6. Metrics vs. manifolds placement rule.** A distance function belongs in `manifolds/` — as a proper `struct` implementing `IRiemannianManifold` — if and only if a finite-dimensional `LogMap` and `ExpMap` exist for it. Functions without these (Euclidean, Manhattan, Wasserstein-1, Canberra, Jaccard, and similar extrinsic distances) stay in `metrics/` permanently. Under this rule, `Poincaré`, `FisherRaoHalfPlane`, and `FisherRaoSimplex` are candidates for promotion to `manifolds/` — all three have closed-form geodesic flows. `JensenShannon` and `Cosine` (angular / spherical) are geodesics in principle but their full manifold implementations (information-geometry simplex and S^{d-1}) are deferred to tier 3. `Wasserstein-1` is not a Riemannian geodesic; `Wasserstein-2` is, but only tractable as a struct for parametric families (Bures metric) — no such implementation exists here yet.

### Still open

These remain undecided and affect public API shape:

1. **Folder vs. flat for the `Location/InSample/Scale` split inside `Estimators/`.** Folders give cleaner namespaces and discoverability; flat (e.g. `LocationMean.cs`, `ScaleMad.cs`) is simpler. Default: folders.
2. **Joint scatter on product manifolds — one block matrix or per-factor matrices.** SPC→GMM probably wants joint; some downstream consumers may want factored. Default: joint, with per-factor as a future extension.
3. **Karcher fast path — IRLS short-circuit or sibling `KarcherFlow.cs`.** Default: short-circuit (`TLoss.IsClosedForm`); promote to sibling only if documentation accumulates.
4. **Subgradient threshold for hybrid mode.** What multiple of `ε` triggers the subgradient fallback? Default candidate: `10ε` or `√ε`. Pending benchmark on representative SPC clusters.
5. **Step-size schedule for the subgradient branch.** Paper uses `η_k = η₀ / √(k+1)` with caller-supplied `η₀`. Could also support `Constant(η)` for callers who prefer it. Default: paper's decaying schedule.
6. **Whether scalar SPC δ helpers need an explicit factory inside `Sampling/Resampling/`-style "rule" abstractions or stay as direct enum dispatch.** Default: stay as direct enum dispatch in SPC; no abstraction.
7. **Robust M-estimators (Huber, Tukey biweight) inside this refactor or as a follow-up.** Default: follow-up; tier-2 trigger.
8. **Where the dimension-dependent consistency factor comes from analytically.** Pending literature review (see references). Default: ship raw-only first, MC helper second, tabulated table third.

## 12. References and inputs

- **Geometric median on product manifolds (load-bearing)** — Park & You, "Geometric Medians on Product Manifolds," 2026. arXiv:2505.18844. Cleaned local copy at [.discussion/2505.18844v3.cleaned.md](../.discussion/2505.18844v3.cleaned.md) (or wherever the converted PDF lives); cited inline in [src/estimators/ManifoldMedian.cs](../src/estimators/ManifoldMedian.cs). Proves existence/uniqueness on Hadamard products, 50% breakdown, Lipschitz stability, and supplies the subgradient + product-aware Weiszfeld algorithms with hybrid recommendation in §4.1.2. **This paper is the design anchor; everything in §3 reduces to its formulation.**
- **Hybrid Weiszfeld+subgradient scheme** — Beck, A. & Sabach, S. (2015). "Weiszfeld's Method: Old and New Results." _Journal of Optimization Theory and Applications_ 164(1): 1–40. Referenced by Park & You §4.1.2 as the foundation for the hybrid mode adopted as default in §3.
- **Robust scatter on the tangent space** — Discussion thread at [.discussion/semi-sup-preview/gemini-manifold-medmad.md](../.discussion/semi-sup-preview/gemini-manifold-medmad.md) developing the inverse-distance-weighted scatter as the multivariate MAD analog and the SPC→GMM handoff use case. The Park & You §5 Bures-Wasserstein example is the natural manifold for the joint mean+covariance product.
- **Trait-dispatched manifold solver** — Discussion threads at [.discussion/gemini-geometric-median-general.md](../.discussion/gemini-geometric-median-general.md) (initial sketch — flawed, see thread for corrections) and [.discussion/gemini-manifold-median-v2.md](../.discussion/gemini-manifold-median-v2.md) (static-abstract `IsFlat` trait pattern adopted in §3). Note: both threads independently dropped the product-manifold structure and the singularity-handling correctness; v0.2 of this doc reinstates them as load-bearing.
- **Spatial sign covariance literature** — Visuri & Koivunen (2000); Croux & Haesbroeck (1999); Tyler's M-estimator (1987). Reference for the dimension-dependent consistency factor in §6.
- **MATLAB primitives** — `randsample.m` and `datasample.m` available in the local toolchest snapshot at `C:\Users\azrie\PDenv\UserGithub\project-snapshots\matlab-toolchest\`. `bootstrp`, `bootci`, `jackknife` available locally from the stats toolbox; not snapshotted.
- **MAD calibration constants** — `GaussianFactor = 1.4826`, `LaplaceFactor = 1.4427` are documented in [src/estimators/SigmaEstimatorMad.cs](../src/estimators/SigmaEstimatorMad.cs). The multivariate generalisation in §6 follows the same naming convention but with dimension-dependent functions in place of constants.
