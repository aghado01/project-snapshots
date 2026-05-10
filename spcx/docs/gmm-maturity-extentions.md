# Hierarchical and Nested GMM Extensions

**Status:** Future research / v2+ — not in scope for the current `GaussianMixture` project.

This document records the design intent and literature anchors for hierarchical and nested mixture
model extensions so that the distinction between the current implementation and these richer model
families is explicit from the start.

---

## What the current implementation is

`GaussianMixture` is a **flat finite Gaussian mixture model** — K components in D dimensions, fit by
EM with a log-space E-step and Cholesky-backed covariance handling. Richer within-seed fitting
strategies (tiling, within-cluster density approximation) compose multiple flat GMM fits into a
coordinated fitting workflow, but at every node the model is still a standard finite mixture.
There is no single global likelihood over a tree-structured latent variable with shared parameters
across levels.

The correct description is: _a coordinated composition of flat GMMs with optional SPC guidance_,
not a _hierarchical GMM model_.

---

## Taxonomy of richer model families

### 1. Hierarchical Mixtures of Experts (HME)

A gating-network tree where each leaf is an expert (can be Gaussian/regression), and soft gating
probabilities route inputs to leaves. The hierarchy is part of the generative story; the E-step and
M-step are derived for the full tree, not per-node independently.

**Key reference:** Jordan & Jacobs (1994), _Hierarchical Mixtures of Experts and the EM Algorithm_,
Neural Computation 6(2), 181–214.

### 2. Hierarchical Mixtures of Gaussians (HMoG / nested Gaussian clusters)

A latent-variable model pairing a dimensionality-reduction layer (PPCA, factor analysis) with a
mixture-of-Gaussians layer, optimised jointly. Each observed cluster lives in a subspace and shares
parameters with its parent node via a prior or a tied factor loading. The parameter-sharing
structure is part of the model, not a post-hoc wrapper.

**Key references:**

- Tipping & Bishop (1999), _Mixtures of Probabilistic Principal Component Analysers_, Statistics
  and Computing 9(1), 21–37.
- Ghahramani & Hinton (1996), _The EM Algorithm for Mixtures of Factor Analysers_, Technical
  Report CRG-TR-96-1, University of Toronto.

### 3. Nested / recursive mixture models

Each top-level cluster is itself a mixture of Gaussians, so the full model is a mixture of mixtures.
The generative story is explicitly recursive; the likelihood integrates over both the primary and
secondary component assignments. EM for this model requires a nested E-step.

**Key reference:** Li (2005), _Clustering Based on a Multilayer Mixture Model_, Journal of
Computational and Graphical Statistics 14(3), 547–568.

### 4. Dirichlet Process Mixture Models (non-parametric)

The number of components is not fixed; the DP prior allows K to grow with data. Inference is via
collapsed Gibbs sampling or variational Bayes. Naturally hierarchical through the stick-breaking
construction.

**Key references:**

- Ferguson (1973), _A Bayesian Analysis of Some Nonparametric Problems_, Annals of Statistics 1(2),
  209–230.
- Rasmussen (2000), _The Infinite Gaussian Mixture Model_, NIPS 12.

### 5. Hierarchical Dirichlet Process (HDP)

Groups of data share a common top-level DP; each group draws its own mixture from that shared base.
Natural for the SPC setting where groups correspond to temperature levels or experimental sessions.

**Key reference:** Teh, Jordan, Beal, Blei (2006), _Hierarchical Dirichlet Processes_, Journal of
the American Statistical Association 101(476), 1566–1581.

---

## Clustering algorithm landscape and positioning

The taxonomy above covers GMM-family extensions. This section places the full algorithm landscape —
density-based, graph-based, and evaluation frameworks — in relation to the arcs in this codebase
and answers the most common "why not X" questions.

### Density-based clustering: DBSCAN and HDBSCAN

**DBSCAN** (Ester et al., 1996 — _Density-Based Spatial Clustering of Applications with Noise_)
identifies clusters as contiguous dense regions separated by low-density space. A point is a _core
point_ if at least `minPts` neighbors fall within radius `ε`; clusters grow by reachability from
core points. Points unreachable from any core point are labeled noise. Output is `int[N]` with −1
for noise. K is not specified; it emerges from `(ε, minPts)`.

**HDBSCAN** (Campello, Moulavi, Sander, 2013; McInnes et al., 2017) extends DBSCAN to a
hierarchy by varying `ε` over a range and extracting the most persistent clusters from the
resulting condensed tree. It eliminates the brittle single-`ε` choice and handles clusters of
varying density. Output is `int[N]` plus an optional per-point membership strength `float[N]`.

#### Why not HDBSCAN?

HDBSCAN and GMM answer different questions and make different representational commitments. The
choice is not arbitrary:

| Property             | HDBSCAN                           | GMM (this codebase)                                  |
| -------------------- | --------------------------------- | ---------------------------------------------------- |
| Cluster shape        | Arbitrary (reachability-based)    | Ellipsoidal (Gaussian)                               |
| Density structure    | Non-parametric; no model          | Parametric; full (μ, Σ, π) per cluster               |
| Noise handling       | Explicit noise label (−1)         | No noise; every point assigned                       |
| Output contract      | Hard labels + membership strength | Hard labels + full `post[N,K]` responsibility matrix |
| K                    | Emergent from density persistence | Fixed (or BIC-selected)                              |
| Geometric artifacts  | None (no covariance)              | `mahalaD[N,K]`, Mahalanobis, density gradient        |
| Semi-supervised path | None standard                     | `IResponsibilityConstraint` + annealed λ             |
| SPC handoff          | No natural coupling               | Direct — SPC `sᵢₖ` → constrained E-step              |

**Where HDBSCAN wins:** irregular-shape clusters, datasets with genuine noise (not just boundary
ambiguity), and when no model-based downstream use (density evaluation, Mahalanobis distances,
Bayesian prediction) is needed. It is also faster for very large N because it avoids EM iteration.

**Where GMM wins:** when the output must support downstream geometric queries (Pdf, Mahal, credible
ellipsoids), when the cluster model will be used for classification of new points, when a
semi-supervised supervisor provides soft-label constraints, and when uncertainty quantification
over component membership is required.

**HDBSCAN as comparison baseline.** For the "why not HDBSCAN" question, the cleanest answer is:
run HDBSCAN and GMM on the same data, compare via an algorithm-agnostic criterion (Silhouette or
Davies-Bouldin — see K-selection section below), and report. This is the honest empirical answer
and sidesteps the "which is better" framing. Silhouette in particular works on any `int[N]` label
vector and requires no model parameters — it is the right tool for algorithm-agnostic comparison.

**References:**

- Ester, Kriegel, Sander, Xu (1996), _A Density-Based Algorithm for Discovering Clusters in Large
  Spatial Databases with Noise_, KDD-96.
- Campello, Moulavi, Sander (2013), _Density-Based Clustering Based on Hierarchical Density
  Estimates_, PAKDD.
- McInnes, Healy, Astels (2017), _hdbscan: Hierarchical density based clustering_, JOSS 2(11).

### K-selection and the cluster evaluation framework

K-selection is the problem of choosing the number of clusters K from data when K is unknown. It is
orthogonal to the choice of clustering algorithm — the same K-selection criterion can be evaluated
over kmeans, linkage, GMM, or any other backend.

MATLAB's `evalclusters` framework makes this separation explicit. The architecture is:

```
ClusterCriterion (abstract)
  ├─ KList: int[]            — candidate K values to evaluate
  ├─ CriterionValues: float[]— one score per K
  ├─ OptimalK: int           — the K selected by the criterion rule
  ├─ OptimalY: int[N]        — the label vector under OptimalK
  └─ ClusteringFunction      — provenance: which algorithm produced OptimalY
```

The backend is pluggable via a `(DATA, K) → int[N] | float[N,K]` function handle. Any clustering
algorithm that produces hard labels or a soft score matrix fits this contract. Evaluation is
incremental — `addK()` probes additional K values and updates the criterion curve without
refitting already-evaluated Ks.

**The four canonical criteria:**

| Criterion             | Measures                                                   | Direction               | Best K rule     | Requires                                   |
| --------------------- | ---------------------------------------------------------- | ----------------------- | --------------- | ------------------------------------------ |
| **Calinski-Harabasz** | Between-cluster scatter / within-cluster scatter (F-ratio) | Higher = better         | Global max      | Labels + data                              |
| **Silhouette**        | Per-point cohesion vs. separation: `(b−a)/max(a,b)`        | [-1,1]; higher = better | Global max      | Labels + data + distance metric            |
| **Davies-Bouldin**    | Mean max pairwise similarity: `(σⱼ+σₖ)/d(μⱼ,μₖ)`           | Lower = better          | Global min      | Labels + cluster centers                   |
| **Gap**               | `E[log W(K)] − log W(K)` vs. null reference                | Higher = better         | First local max | Labels + null distribution (B=100 samples) |

**Model-free vs. model-based:** Calinski-Harabasz, Silhouette, and Davies-Bouldin need only the
final `int[N]` label vector and the data — no model parameters. This makes them algorithm-agnostic
and usable as post-fit validators on any clustering output, including SPC and HDBSCAN. Gap
requires generating a null reference distribution; it is more expensive and couples weakly to the
backend. BIC (our current Arc 1 K-selection tool) is model-based — it requires the fitted GMM
log-likelihood and is specific to the GMM family.

**Distance metric coherence.** Silhouette and Davies-Bouldin both have a `Distance` parameter.
The distance metric used in evaluation must match the one used in clustering, or the criterion
value is incoherent. MATLAB enforces this via string aliases (sqEuclidean → Ward linkage). In our
design this is a caller obligation declared in the `KSweepResult` provenance tag.

#### K-selection in the arcs

| Arc                         | K source                          | Mechanism                        |
| --------------------------- | --------------------------------- | -------------------------------- |
| Arc 1 — Standalone GMM      | BIC sweep over `KList`            | Model-based; GMM-specific        |
| Arc 2 — Standalone SPC      | Susceptibility peak / χ criterion | Thermodynamic; no K-sweep needed |
| Arc 3 — Semi-supervised GMM | Provided by supervision contract  | K fixed by supervisor; no sweep  |

Calinski-Harabasz and Silhouette are the natural post-fit validators for Arc 1 results. They
require no refitting and give an algorithm-agnostic second opinion on whether the BIC-chosen K
produces a geometrically coherent partition.

#### The `IClusteringOracle` interface

The MATLAB function-handle backend `(DATA, K) → float[N,K]` reveals a clean interface boundary.
A backend that returns a soft score matrix rather than hard labels is a natural common ancestor for
two distinct consumers:

- **K-selection path:** convert soft scores to hard labels via argmax; evaluate criterion; sweep K.
- **Semi-supervised supervision path:** pass soft scores directly as `sᵢₖ` to
  `IResponsibilityConstraint`; K is fixed by the supervisor.

The interface contract is the same in both cases; how the caller consumes it differs. Naming this
`IClusteringOracle` and making it the declared input type for both paths — K-sweep evaluation and
Arc 3 supervision — creates a common seam for plugging in HDBSCAN membership strengths, SPC
co-occurrence matrices, or any other soft-assignment source.

#### DTO invariants for K-sweep results

From the MATLAB `ClusterCriterion` base class, the minimal K-sweep result DTO is:

```
InspectedK       int[1, numK]     — the K values evaluated (the index domain)
CriterionValues  float[1, numK]   — one score per K; parallel to InspectedK
CriterionDirection  enum          — Higher|Lower (which direction is better)
OptimalK         int              — the winning K
OptimalY         int[N]           — label vector under OptimalK
Provenance       string           — which algorithm + which criterion produced this
```

Three invariants worth enforcing on write:

1. **K-index pairing** — `CriterionValues[j]` corresponds to `InspectedK[j]`. A float array
   without its parallel K-index vector is semantically orphaned.
2. **Provenance** — `OptimalY` is meaningless without knowing which algorithm produced it and
   which metric was coherent with those labels. Required field, not optional metadata.
3. **Criterion direction** — you cannot compare CalinskiHarabasz values with DaviesBouldin values;
   one is "higher better," the other "lower better." The direction must be tagged so the
   `OptimalK` derivation rule is unambiguous.

#### Per-point Silhouette as a boundary detector

Silhouette's full output is `float[N]` per-point values, not just a scalar mean. A point with
silhouette near 0 or negative is geometrically ambiguous — equidistant between its own cluster and
its nearest neighbor. This is precisely the set of contested boundary points that the expansion
loop and resolution paths act on.

Post-fit per-point Silhouette is a model-free boundary diagnostic complementary to the
model-based posterior uncertainty in `post[N,K]`. They are not equivalent: Silhouette uses
inter-point distances; posterior uncertainty uses mixture density. Disagreements between the two
(high-uncertainty but clear Silhouette, or vice versa) are diagnostically interesting and worth
surfacing in the codec.

#### Davies-Bouldin as a post-fit separation diagnostic

DB computes `(σⱼ + σₖ) / d(μⱼ, μₖ)` per cluster pair — scatter over centroid distance. This is
the same ratio the Mahalanobis mutual penetration check measures during the expansion loop, in a
different parameterization:

- **Mahalanobis penetration** (during fitting) — covariance-adaptive, directional, used to stop
  the expansion loop.
- **Davies-Bouldin** (post-fit) — distance-normalized, cluster-symmetric, scalar per pair, used
  to audit the final partition quality.

The two are complementary. A high DB value for a cluster pair post-fit is a flag that the
expansion loop may have stopped too early for those two clusters — the same diagnostic question the
penetration check was asking in real time.

---

## Relationship to the SPC handoff

Three developmental arcs share this codebase and must not be conflated:

**Arc 1 — Standalone GMM.** No SPC in the pipeline. Initialization via `RandomInitialize` or
k-means++; cluster identity via `EntropyMergeStrategy` or `ModalMergeStrategy` post-fit. BIC
drives model selection. Fully self-contained.

**Arc 2 — Standalone SPC.** Full temperature sweep to susceptibility peak; thermodynamic analysis;
merge tree; cluster labels. No GMM involvement.

**Arc 3 — Semi-supervised GMM.** An external supervision agent provides two inputs that
constrain the GMM fit, then exits:

1. **Warm-start initialization** — the supervisor produces deliberately _conservative_ `(μₖ, Σₖ)`
   estimates per cluster that undersell cluster extent. The conservative bias is intentional: it
   produces a low-entropy initialization where early EM expansion is stable and directional. These
   estimates warm-start `InitializeWithParameters`.

2. **Constrained E-step** — the supervisor's per-point confidence matrix `sᵢₖ` enters the E-step
   as a soft-label clamp via `IResponsibilityConstraint`. The blending
   `rᵢₖ = (1-λ)r̂ᵢₖ + λ·sᵢₖ` with annealed λ prevents component drift across supervisor
   boundaries during fitting. This is active throughout every EM iteration until λ decays to zero.
   **Initialization alone does not keep components anchored** — the constrained E-step is
   load-bearing.

   **Pattern 3 exception:** the constrained E-step applies only to pattern 1 and pattern 2 seeds.
   Pattern 3 seeds (within-seed density approximation) bypass the global constrained EM pass
   entirely — their local EM runs to convergence, and they route to hierarchical inference. Their
   sub-components never enter the global pool; the `sᵢₖ` constraint has no sub-component
   granularity and cannot route a point to one of several overlapping sub-components within a seed.

   **λ annealing schedule (candidate):** the decay rate should track convergence speed, not wall
   iterations. A reasonable default is exponential decay tied to the EM log-likelihood delta — λ
   decreases quickly while LL is improving rapidly (supervisor still informative) and more slowly as
   EM plateaus. This avoids two failure modes: too-fast decay wastes the supervisor anchor before
   components stabilise near boundaries; too-slow decay prevents EM from closing to the
   data-determined boundary geometry. A linear decay tied to iteration count is simpler but ignores
   convergence rate; it is the fallback when LL-delta monitoring is not available.

The supervisor does **not** drive recursive splitting, does not directly determine
`ComponentToClusterMap`, and ceases to be load-bearing once λ has decayed. Cluster identity at
convergence is resolved by the same `IClusterIdentityStrategy` used in Arc 1. The
`ISpcShatterOracle` / `SpcShatterCriterion` framing predates this design and is superseded.

**Supervision contract (what GMM requires from any supervisor):** per-cluster warm-start `(μₖ, Σₖ)`
and optional core-point masks for `InitializeWithParameters`; the full N×K per-point confidence
matrix `sᵢₖ` for `IResponsibilityConstraint`; and a cluster count K. Additional metadata (e.g.
scatter-quality proxies such as $n_k$) is advisory — used in expansion loop diagnostics but not
part of the binding contract.

**SPC as the planned supervisor (roadmap).** SPC fulfils this contract by running a partial
temperature sweep, stopping early once core cluster estimates stabilize (see
[spc-maturity.md §17.7](./spc-maturity.md)). The `SpcHandoff` DTO carries: (i) for each cluster
$k$, the confidence-thresholded core points (those with $s_{ik} \geq \theta$) and their
`(μₖ, Σₖ)` from `RobustLeafEstimator`; (ii) the full N×K confidence matrix; (iii) the stopping
temperature and $n_k$ count. The $n_k$ count is a proxy for scatter estimate quality — small
$n_k$ means the initial ellipsoid shape may have high variance even if the center is approximately
right (see expansion loop note below). See [spc-maturity.md §17.4](./spc-maturity.md) for the
DTO definition and §17.7 for the partial-sweep stopping criterion.

If the goal becomes "one Gaussian submodel per SPC cluster, with shared structure across the
temperature hierarchy," the right formulation is HMoG (family 2) or HDP (family 5) — but that is
a separate research arc, not the semi-supervised GMM pipeline described above.

---

## Within-seed fitting strategies

When K seed clusters are known (from SPC or from a flat GMM pass), fitting is not a single
choice — there are three distinct patterns for how components are placed inside each seed's
territory. These patterns are **not recursive variants of each other**; they have different data
scopes, different local EM behaviors, and different resolution mechanisms.

### Pattern 1 — One component per seed

One ellipsoid per SPC core. Works when each cluster is approximately elliptical. Local EM runs
on confidence-thresholded core points to expand the ellipsoid outward from the conservative
initial estimate until it reaches cross-talk with a neighbor (see coordinated expansion below).
Resolution: modal closure or flatten + global constrained EM.

### Pattern 2 — Tiling (spatial splitting)

One SPC cluster requires multiple components to tile its spatial extent — the crescent, banana,
or arc case. Components are approximately **non-overlapping**: each tiles a segment of the
cluster's territory and has negligible density under its sibling components. BIC on spatial
subsets drives the decision on $n_k$; local EM bootstraps from spatially split subsets.
Resolution: flatten + global constrained EM (sub-components are cluster-separators within the
global scope; the contested zone with other seeds still needs global resolution).

### Pattern 3 — Within-seed density approximation

One SPC cluster has a genuinely multimodal or elongated density that no single Gaussian
represents well — the canonical example is two elongated ellipsoids crossed at 90° at the
same center. Multiple components are placed within the seed, but they **substantially overlap
at the center**: the assignment of a core point is probabilistic and density-driven, not
spatially separable. BIC on the full seed data (not spatial subsets) drives $n_k$; local EM
runs on the full seed data subset and runs to convergence — not just one step, not until
cross-talk, but until the within-seed local EM is done.

**Why local EM is legitimately complete for pattern 3:** the data scope for this seed's local
EM is "all points assigned to this seed." There is no cross-seed contamination problem: the
overlapping sub-components are entirely inside one seed's territory. The global step's job is
boundary resolution _between_ seeds, not anything internal to this seed's density.

Resolution: **hierarchical inference — not flattening.** The sub-components from pattern 3 are
never cluster-separators at the global level. Clustering is evaluated at the seed level;
within-seed sub-components answer the density question (`Pdf`, `Mahal`, `PredictComponent`)
but must not answer the clustering question (`PredictCluster`). `PredictCluster` routes through
`ComponentToClusterMap` which maps all $n_k$ sub-components of seed $k$ to the same cluster id,
correctly collapsing them.

### Coordinated local expansion loop (Patterns 1 and 2)

For patterns 1 and 2, the local expansion phase is **coordinated** — all K seeds advance
together one EM step at a time, with a global cross-talk check after each step:

```csharp
for (int step = 0; step < maxLocalSteps; step++)
{
    foreach (var seed in seeds)
        seed.LocalModel.Fit(data, hardLabels: seed.CoreMask, maxIterations: 1);

    if (DetectCrossTalk(seeds, data, epsilon))
        break;
}
```

#### Cross-talk detection metrics

The EM E-step handles boundary conditions implicitly: a peripheral point equidistant between two
seeds gets roughly equal responsibility from both — that shared responsibility _is_ the cross-talk
signal. No explicit proposal mechanism is needed; the density drives expansion and the
responsibilities report the boundary geometry at each step. The question is which summary statistic
to threshold. Three candidates from existing primitives:

**Mahalanobis mutual penetration** — for neighboring seeds $j$ and $k$, evaluate how deep seed
$j$'s mean has penetrated into seed $k$'s ellipsoid:
$$d_{jk} = \operatorname{Mahal}(\mu_j,\, \mu_k,\, \Sigma_k)$$
When this drops below $\sqrt{D}$ (the expected Mahalanobis distance of a point on the ellipsoid
surface), the seeds are encroaching. This metric is directional — $d_{jk} \neq d_{kj}$ — which is
useful since the asymmetry tells you which seed is the expander and which is the stationary one.
It is also inherently evaluated along the seed-center axis: the Mahalanobis distance from $\mu_j$
into $\Sigma_k$ measures exactly the penetration relevant to this pair.

**Weiszfeld scatter delta** — after each M-step, track $\|\Delta\Sigma_k\|_F$ (Frobenius norm of
the change in scatter). When this stabilises below $\varepsilon$ for seed $k$, that seed has
absorbed its natural territory and stopped growing — a per-seed self-report that requires no
neighbor enumeration. Cross-talk shows up as $\|\Delta\Sigma_k\|_F$ _not_ stabilising: contested
boundary points are being traded between seeds each step, keeping the scatter in flux. This is a
natural fit for stacks with Weiszfeld scatter already in the initialisation path.

**Responsibility overlap** — for each unlabeled point $x_i$, compute $\max_k(r_{ik})$. When a
meaningful fraction of points have $\max_k(r_{ik}) < \theta$ (no single seed dominates), you are
in contested territory. This is the cheapest check — responsibilities are already computed in the
E-step; no extra distance evaluation needed. Useful as a corroboration rather than a primary gate.

**Recommended two-condition stopping rule:**

- **Per-seed:** stop seed $k$'s contribution to the global loop when $\|\Delta\Sigma_k\|_F < \varepsilon$ — this seed has absorbed its natural territory.
- **Global:** stop the entire loop when $\operatorname{Mahal}(\mu_j, \mu_k, \Sigma_k) < \tau$ for any neighbor pair $(j, k)$ — a boundary has been reached.

The two conditions serve different purposes. A seed that stabilises early because its interior is
compact should still stop the global loop if a neighbor grows into it — Weiszfeld delta alone
misses that. Mahalanobis penetration catches the boundary event without requiring the expander to
have stabilised yet. Responsibility overlap is a cheap corroboration that doesn't require
enumerating neighbor pairs.

#### Modal interpretation of the expansion loop

The modal picture gives an alternative geometric reading of what the expansion loop is doing.
Each seed's local GMM has a mode — for a single Gaussian that is just $\mu_k$, but as the
ellipsoid expands the mode tracks the responsibility-weighted center of mass of whatever the seed
currently owns.

**Mode displacement as stopping condition.** While the ellipsoid is absorbing new points, the
mode is moving — each M-step shifts $\mu_k$ as more peripheral mass is incorporated. When modal
displacement drops below a threshold, the seed has settled into its natural territory. This is
equivalent to the $\|\Delta\Sigma_k\|_F$ condition but more interpretable: you are watching the
attractor converge rather than the covariance matrix.

**Cross-talk as modal basin conflict.** If gradient ascent is run from a contested boundary point,
it climbs toward one seed's mode or another depending on tiny perturbations — that instability is
exactly what cross-talk detection catches. The density ridgeline between modes is the geometric
object the expansion loop is trying not to cross.

The modal stopping condition for the expansion loop: **stop when no unlabeled peripheral point's
gradient ascent terminates at a different seed's mode than the nearest seed by Euclidean
distance.** This has no threshold to tune — the modes themselves define the boundary.

The gradient of the log mixture density is analytic and computable from existing primitives:
$$\nabla_x \log p(x) = \sum_k r_k(x) \cdot \Sigma_k^{-1}(\mu_k - x)$$
$\Sigma_k^{-1}$ is already available via Cholesky; $r_{ik}$ from the E-step. Gradient ascent from
a point typically converges in 5–20 steps on a smooth GMM landscape.

The modal approach is particularly relevant for pattern 2 tiling, where the outermost tile may
have a mode that is not well captured by the global seed mean $\mu_k$ — the Mahalanobis
penetration and modal basin checks converge to the same boundary in the purely Gaussian case, but
the modal version is more geometrically principled for non-elliptical tile shapes.

**Failure mode — noisy initial Σₖ and premature cross-talk detection.** If the SPC sweep stops
with small $n_k$, the scatter estimate from `ComputeWithScatter` has high variance. A noisy Σₖ
elongated toward a neighboring seed will artificially inflate the Mahalanobis mutual penetration
(or Bhattacharyya coefficient) between that pair — triggering a premature stop before the
ellipsoid has grown to realistic size in the perpendicular directions. This is a narrow failure
mode: it requires the noise to point specifically toward a neighbor, and the M-step has room to
self-correct as long as the stop is not triggered too early. The Hotelling T² stopping criterion
in SPC biases naturally against this — with small $n_k$, $T^2 = n_k \cdot \Delta\mu_k^T
\hat{\Sigma}_k^{-1} \Delta\mu_k$ is harder to pass, so the sweep runs longer and accumulates more
core points before handing off. Second-order concern; does not block the hybrid arc design.

#### Bayesian options for the expansion loop

Three Bayesian angles are available, at different cost points, and they compose cleanly with the
robust estimation stack (Weiszfeld scatter, geometric median).

**NIW prior on Σₖ (nearly free).** The Normal-Inverse-Wishart (NIW) is the conjugate prior for a
Gaussian. Adding it to the M-step replaces the sample scatter with the NIW posterior mean, which
automatically shrinks toward a prior shape (e.g. spherical) when $n_k$ is small and relaxes toward
the data geometry as evidence accumulates. This directly addresses the noisy-Σₖ failure mode
above: the prior regularises the scatter estimate during the early expansion steps when cores are
tight, and costs only 4 extra scalars per cluster ($\mu_0, \kappa_0, \nu_0, \Psi_0$). The
posterior credible region for $\mu_k$ also gives a Bayesian analog of the Hotelling T² stopping
condition — same structural test, Bayesian framing.

**Connection to Weiszfeld scatter (architectural note).** Weiszfeld scatter is a robust covariance
estimate via IRLS weights that down-weight outliers; the NIW posterior mean under a Gaussian
likelihood is the sample covariance, which is not robust. The coherent Bayesian robust analog is a
Student-t likelihood with a per-point latent scale variable $u_i$ — the t-EM of Peel & McLachlan
(2000). In t-EM the E-step computes $\mathbb{E}[u_i]$ alongside responsibilities; the M-step uses
$u_i$-weighted statistics. The key identity is:
$$w_i^{\text{Weiszfeld}} \approx \mathbb{E}[u_i]^{\text{t-EM}}$$
Both are robust M-estimators derived from different starting points (frequentist IRLS vs. Bayesian
latent-scale marginalisation). Weiszfeld initialization + t-EM expansion is therefore a coherent
pair from the same estimator family — not an accident. Plugging the t-EM $u_i$ weights into a NIW
update gives a **robust Bayesian scatter** without new machinery beyond what the existing stack
already computes.

**VBEM as a middle path (moderate cost, deferred).** Variational Bayes EM replaces point estimates
with variational distributions while keeping the EM structure intact. The E-step uses the expected
log-likelihood under the NIW posterior; the M-step updates NIW hyperparameters rather than point
estimates. VBEM updates are analytic (same cost as standard EM, just updating hyperparameters
instead of arrays) and deliver automatic regularisation plus free component pruning — components
with insufficient support shrink their weight to zero. The canonical reference is Bishop PRML
Ch. 10. This is not a v1 item — invoke when small-core scatter instability is observed in practice
or when uncertainty quantification at convergence is required.

**Full MCMC is not appropriate here** — sampling a posterior at every expansion step is expensive
and unnecessary. MCMC belongs to the tier-3 standalone Bayesian arc (posterior over partition,
RJMCMC over K), not the hybrid expansion loop.

| Approach              | What it gives                                                 | Cost                                           | When to use                                      |
| --------------------- | ------------------------------------------------------------- | ---------------------------------------------- | ------------------------------------------------ |
| NIW prior on Σₖ       | Regularised scatter for small $n_k$; Bayesian stopping signal | Minimal — 4 scalars/cluster                    | Always; nearly free                              |
| t-EM likelihood + NIW | Robust Bayesian scatter coherent with Weiszfeld init          | Low — per-point $u_i$ weight, already computed | Natural fit; add when robust expansion is needed |
| VBEM expansion loop   | Uncertainty throughout expansion; automatic pruning           | Moderate — replaces M-step arithmetic          | If small-core instability observed in practice   |
| Full MCMC             | Posterior over partition                                      | High                                           | Tier-3 standalone Bayesian arc only              |

The point of this loop is to let each seed expand its covariance outward from the conservative
initial estimate until it reaches contested territory **before** dropping into global EM. Seeds
that reach the boundary zone earlier pause; seeds with more interior room continue. When all
seeds have stopped, the peripheral contested zone is handed to the resolution stage.

It is pointless to initialize about conservative cores and then immediately drop into global EM
after a single local step. The value of the conservative initialization is specifically to
enable stable directional expansion during this loop — global EM inherits a near-realistic
initialization rather than a truncated one.

#### Primitive inventory and build-out plan

The primitives below are grouped by which arc and phase of GMM development needs them.
Arc 1 (standalone flat GMM) is the current codebase. Arc 3 Phase 1 is the semi-supervised GMM
arc with pattern 1 seeds only. Arc 3 Phase 2 adds patterns 2 and 3. The Bayesian column is
deferred and does not block either Arc 3 phase.

##### Arc 1 — Standalone flat GMM (current)

No new primitives required. The full Arc 1 fitting pipeline is satisfied by existing code:
`GaussianMixtureModel`, `GaussianComponent` (with `CovarianceInverse`, `EvaluateLogPdf`,
`Mean`, `Covariance`), `Mahalanobis.DistanceSquared`, `WeiszfeldScatter.Compute`,
`GetFinalResponsibilities`, and `Cholesky`. The density gradient formula is computable
on-demand from these without new files.

##### Arc 3 Phase 1 — Semi-supervised GMM, pattern 1 seeds

Blocking for the basic coordinated expansion loop with one component per seed, constrained
E-step, and modal/global EM resolution.

**Use existing code directly:**

| Purpose                                  | Existing primitive                                                       | Note                                                              |
| ---------------------------------------- | ------------------------------------------------------------------------ | ----------------------------------------------------------------- |
| Mahalanobis mutual penetration $d_{jk}$  | `Mahalanobis.DistanceSquared(μⱼ, μₖ, seedK.CovarianceInverse)`           | `CovarianceInverse` current after each M-step `UpdateCache`       |
| Responsibility overlap fraction          | `GaussianMixtureModel.GetFinalResponsibilities()` — row-max              | Already in E-step output; zero extra cost                         |
| Weiszfeld scatter per responsible subset | `WeiszfeldScatter.Compute(manifold, responsiblePoints, μₖ, ...)`         | IRLS weights ≡ t-EM $u_i$ — `outerWeights` param is the t-EM hook |
| Hard-label core clamping                 | `GaussianMixtureModel.Fit(data, hardLabels: coreMask, maxIterations: 1)` | Already exists; one-step truncation for coordinated loop          |

**Build (thin composition, no new geometric primitive):**

| What                                                   | Where                        | What it does                                                                                                                        |
| ------------------------------------------------------ | ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `MatrixNorms.FrobeniusDelta(double[,] a, double[,] b)` | `linalg/`                    | $\|\Delta\Sigma_k\|_F$ scatter-delta stopping signal; also needed for NIW Ψ update and convergence checks throughout                |
| Neighbor seed pair graph                               | `graphs/` thin static helper | K-NN over seed means; input to penetration check; reuse existing graph infra if present                                             |
| `DetectCrossTalk(seeds, data, epsilon)`                | Expansion loop driver method | Composes penetration + scatter delta + responsibility overlap → `bool` stop + diagnostic struct; not a standalone primitive         |
| Mode displacement inline tracking                      | Expansion loop driver        | Copy $\mu_k$ before M-step; compare after — `GaussianComponent.Mean` already available                                              |
| `IResponsibilityConstraint` implementation             | `gmm/`                       | Implements $r_{ik} = (1-\lambda)\hat{r}_{ik} + \lambda s_{ik}$ blending with annealed λ; interface slot exists, not yet implemented |

**New standalone primitive needed for Phase 1:**

| Primitive     | Where     | What it does                                                                                     |
| ------------- | --------- | ------------------------------------------------------------------------------------------------ |
| `MatrixNorms` | `linalg/` | `FrobeniusDelta(a, b)`, `FrobeniusNorm(a)` — the only genuinely new linalg file blocking Phase 1 |

##### Arc 3 Phase 2 — Semi-supervised GMM, patterns 2 and 3

Needed when tiling (pattern 2) or within-seed density approximation (pattern 3) seeds are
present. Blocks on Phase 1 complete.

**Use existing code directly:**

| Purpose                   | Existing primitive                                              | Note                                                                                                   |
| ------------------------- | --------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| Density gradient at $x$   | `GaussianComponent.CovarianceInverse`, `.Mean`, E-step $r_{ik}$ | $\nabla_x \log p(x) = \sum_k r_k(x) \cdot \Sigma_k^{-1}(\mu_k - x)$ — all terms on `GaussianComponent` |
| LogPdf at arbitrary point | `GaussianComponent.EvaluateLogPdf(x)`                           | Needed for modal ascent and basin check                                                                |
| BIC on spatial subsets    | `GaussianMixtureModel.Fit` + BIC from existing LL               | Pattern 2 $n_k$ selection                                                                              |

**New standalone primitives needed for Phase 2:**

| Primitive                                   | Where                     | What it does                                                                                                                                                                                                                                                                                     |
| ------------------------------------------- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ModeAscent`                                | `gmm/` or `optimization/` | `Ascend(double[] start, GaussianComponent[] components, int maxSteps, double tol) → double[]` — gradient ascent on mixture log-density; 5–10 lines using existing `CovarianceInverse` + E-step formula. `GetSeedBasin(double[] point, ...) → int` — which seed's mode does this point ascend to? |
| `BhattacharyyaCoefficient`                  | `metrics/` or `gmm/`      | Pattern 2 vs pattern 3 discriminant (post-local-convergence overlap test); computable from existing `Mahalanobis` and `Cholesky`                                                                                                                                                                 |
| `ManifoldMixture` + `ComponentToClusterMap` | `gmm/`                    | Already designed; needed for pattern 3 hierarchical inference and `PredictCluster` vs `PredictComponent` split                                                                                                                                                                                   |

##### Deferred — Bayesian options (no arc gate)

Add when small-core scatter instability is observed in practice or uncertainty quantification
is required. These do not block Arc 3 Phase 1 or Phase 2.

| Primitive               | Where                                    | What it does                                                                                                                                                                                                                 | Trigger                                      |
| ----------------------- | ---------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------- |
| `NiwPrior` struct       | `estimators/`                            | `{ Mu0, Kappa0, Nu0, Psi0 }` + `PosteriorMeanCovariance(sampleCov, effectiveN) → double[,]`. Shrinks toward spherical for small $n_k$; relaxes to data geometry as evidence grows. Drop-in scatter regularisation in M-step. | Small-core scatter instability observed      |
| t-EM via `outerWeights` | No new file — `WeiszfeldScatter.Compute` | t-EM $u_i$ weights plug into `outerWeights` param directly; no interface change needed. Coherent with Weiszfeld init — same estimator family.                                                                                | Robust scatter coherently integrated with EM |
| VBEM M-step variant     | `gmm/`                                   | Replace point-estimate M-step with NIW hyperparameter updates; E-step uses expected log-likelihood under posterior. Bishop PRML Ch. 10. Adds automatic component pruning.                                                    | Uncertainty quantification or pruning needed |

`NiwPrior` and `WeiszfeldScatter` are independent and composable: NIW regularises the initial
core scatter estimate; Weiszfeld down-weights outliers during expansion. Both can be active —
NIW as the prior, Weiszfeld as the robust likelihood — producing robust Bayesian scatter.

### Resolution paths

| Path                                | What it does                                                                                                                                                              | Patterns                        |
| ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------- |
| **Modal closure**                   | Freeze all parameters; one E-step over all N points; assign each point to the highest-density component. No M-step.                                                       | 1, 2 (with caveats — see below) |
| **Flatten + global constrained EM** | Pool all components from all seeds; run global EM with $s_{ik}$ soft-label constraint and annealed λ. Parameters update — boundary points influence final Σ.              | 1, 2                            |
| **Hierarchical inference**          | Keep the tree. Seed-level assignment by comparing expanded seed densities; within-seed assignment conditional on seed. Sub-components from different seeds never compete. | 3 only                          |

**BIC is not a resolution mechanism.** BIC was consumed upstream to decide $n_k$ per seed. At
the resolution stage the model specification is fixed; you are assigning boundary points, not
choosing between models. Invoking BIC here is a category error.

#### Modal closure for pattern 2: two contested zones, one easy

Pattern 2 (tiling) has two structurally different contested zones:

- **Intra-seed boundaries** (between sub-components of the same tiled seed): these points belong
  to the same cluster regardless of which sub-component wins — modal closure is correct and
  cheap; no global EM is needed for intra-seed assignment.

- **Inter-seed boundaries** (outermost tile of seed $k$ vs. neighbouring seed $j$): this is
  genuine cluster separation. Modal closure assigns correctly _only if_ the expansion loop ran
  far enough for the outermost tile's Σ to have absorbed the contested-zone geometry. If
  cross-talk was detected early — covariances still interior-biased — modal closure will leave
  Σ slightly wrong for boundary-adjacent points.

The consequence is downstream, not in the cluster assignment: Mahalanobis distances and
confidence ellipsoids at the inter-seed boundary are slightly off when using modal closure.
Whether that matters depends on what the caller does with `Pdf` and `Mahal` at boundary points.

| Resolution choice               | Cluster assignment                  | Boundary Σ accuracy                    | Cost                                    |
| ------------------------------- | ----------------------------------- | -------------------------------------- | --------------------------------------- |
| Modal closure                   | Correct if expansion loop was deep  | Interior-biased at inter-seed boundary | Cheapest — one E-step                   |
| Flatten + global constrained EM | Correct; updates with boundary data | Accurate                               | Full EM pass with soft-label constraint |

For most use cases, modal closure is the right default for pattern 2: the expansion loop runs
until cross-talk, which means the outermost tiles have already reached the contested region
before stopping. The boundary Σ error is small in practice, and global EM costs more. Promote
to flatten + global constrained EM when downstream inference (outlier scoring, manifold
distances) heavily uses boundary-region Mahalanobis values.

Pattern 3 requires hierarchical inference because flattening it would be wrong: pooling
pattern-3 sub-components into the global model elevates density-basis components to
cluster-separator status, causing them to incorrectly compete for boundary points from other
seeds during global EM.

### Distinguishing pattern 2 from pattern 3 in practice

The signal is **component overlap after local convergence**. Run local GMM with $n_k > 1$ on
the seed data; after convergence check the Bhattacharyya coefficient between the resulting
sub-components:

- **Low overlap** (coefficient below threshold) → non-overlapping tiling regime → pattern 2 →
  flatten + global EM is correct
- **High overlap** (substantial shared density mass) → density approximation regime → pattern 3
  → hierarchical inference is correct; spatial splitting would have been wrong

Computable from existing `Mahalanobis` primitives and the fitted `GaussianComponent` parameters
— no new estimators required.

---

## What "hierarchical GMM" means in this codebase vs. the literature

| Term              | In this codebase                                    | In the literature                                                  |
| ----------------- | --------------------------------------------------- | ------------------------------------------------------------------ |
| Hierarchical GMM  | Tree of flat GMM fits, one per SPC temperature node | HMoG / HME — hierarchy is in the generative model and EM equations |
| Nested clusters   | Subset-per-cluster flat GMM                         | Mixture-of-mixtures with a recursive likelihood                    |
| Shared parameters | Not implemented                                     | Tied factor loadings or shared covariance across levels (MPPCA)    |

The codebase usage is an engineering pattern, not a distinct statistical model class. Document it
as such when exposing the tree API.

---

## Deferred implementation notes

When implementing any of the above, start from the statistical model definition and derive the EM
updates from scratch rather than adapting the flat GMM code. The E-step and M-step change
substantially once hierarchy is in the generative story.

Minimum prerequisites before attempting:

- Stable flat GMM with full-covariance, Cholesky E-step, configurable regularisation ✓ (done)
- Robust initialization from SPC partition ladder (in progress)
- SPC standalone package with a well-defined partition ladder output (future)

---

## Design rules for the recursive variant when it lands

These rules apply when family 3 (nested / recursive mixture) is implemented. They are not commitments to any particular schedule, but they constrain the API shape so that early decisions do not paint later work into a corner. None of them require touching the current flat `GaussianMixture` code.

### Constrained vs. unconstrained weight factorization

A two-level recursive mixture is structurally a flat GMM with $K \cdot K_k$ components and a weight constraint $\pi_{kj} = \pi_k \cdot \pi_{k|j}$. Two implementation paths:

- **Constrained** — enforce the factorization throughout EM. Top-level partition has independent probabilistic meaning; BIC computable at both levels. Statistically principled, slower to converge, M-step requires the chain rule through the factorization.
- **Unconstrained** — initialise with structured parameters from a hierarchical scheme but run flat EM. The hierarchy is an initialisation strategy only; `Flatten()` is identity. This is what most practitioners actually use and what MATLAB's `fitgmdist` with a custom `Start` parameter delivers.

The model class should expose this as a construction-time toggle. The unconstrained variant is the right entry point for v2; the constrained variant follows when there is a concrete consumer that needs proper hierarchical semantics (e.g. local BIC for per-branch model selection).

### `Flatten()` is not optional

Whichever fork is implemented, the recursive model must expose a `Flatten()` method that returns a flat `GaussianMixtureModel` with the same density. This keeps the recursive variant a _lens_ on a flat model rather than a hard coupling, and means everything downstream that consumes a flat GMM (`Predict`, `Pdf`, `Sample`, `Mahal`) keeps working without recursion-aware overloads.

### Manifold awareness lives at the node, not the component

For manifold data, do **not** inject `IRiemannianManifold` into `GaussianComponent`. The component should remain flat-Euclidean and only ever see tangent vectors in a frame chosen by its enclosing node. The manifold geometry — `LogMap` / `ExpMap`, the choice of `ManifoldCenter` — belongs on the node:

```csharp
public sealed class GmmNode
{
    public GaussianMixtureModel LocalGmm { get; }   // flat-Euclidean in local tangent space
    public double[]             ManifoldCenter { get; }  // geometric / manifold median
    public IManifoldChart       LocalChart { get; }      // LogMap / ExpMap at ManifoldCenter

    public double[] ToLocal(double[] x)    => LocalChart.LogMap(ManifoldCenter, x);
    public double[] ToManifold(double[] v) => LocalChart.ExpMap(ManifoldCenter, v);
}
```

Each node recomputes its own tangent space at its own local center. A single global tangent space is only correct in the trivial depth-1 case; for deeper trees, child clusters concentrated far from the parent's center suffer increasing linearization error, and per-node recentering is the only clean fix. The flat case falls out as depth-1 with one tangent space — no special-casing.

This is a clean separation: differential geometry at the node boundary, linear algebra inside EM. `GaussianComponent` stays flat throughout, and EM convergence properties carry over from the flat case without modification.

### Components are not clusters — the topological mapping

A flattened recursive mixture exposes K Gaussian components. The recursive tree split a _single_ topological cluster (a curved manifold like a crescent) into multiple components to tile its shape. The flat model has no record of that grouping — it sees K independent density bumps in space. **Do not rely on the flattened GMM alone to remember that components 0..4 were all parts of the crescent.** That memory must live in a parallel artifact, not be smuggled into the flat model.

The recursive variant therefore exposes a wrapping object that holds the flat density model _and_ the component-to-cluster map as siblings:

```csharp
public sealed class ManifoldMixture
{
    public GaussianMixtureModel DensityModel { get; }   // flat, K components, topology-blind
    public int[] ComponentToClusterMap { get; }         // length K; component k → topological cluster id

    // Coarse user-facing label: which logical cluster does each point belong to?
    public int[] PredictCluster(double[][] data)
    {
        var components = DensityModel.Predict(data);
        var clusters = new int[data.Length];
        for (int i = 0; i < data.Length; i++)
            clusters[i] = ComponentToClusterMap[components[i]];
        return clusters;
    }

    // Fine-grained density label: which Gaussian piece fits this point best?
    public int[] PredictComponent(double[][] data) => DensityModel.Predict(data);
}
```

`DensityModel` is the unchanged flat GMM — it answers density, posterior, Mahalanobis, sampling, and component-level prediction directly. `ComponentToClusterMap` is topological memory: it preserves "components 0..7 all belong to the C-shape; components 8..11 belong to the ellipsoid" through `Flatten()` and beyond.

This is the GMM-side complement of `SpcClusterProfile.ClusterId` (see [spc-maturity.md](./spc-maturity.md) §17.2). The handoff from SPC to a recursive GMM passes cluster identity in via the per-cluster handoff inputs; the recursive driver records it on the way back out by populating `ComponentToClusterMap` as each child component is appended to the flat model. The flat `GaussianMixtureModel` itself remains topology-blind.

#### Two prediction granularities, both first-class

Inference against a `ManifoldMixture` is a deliberate choice between two granularities. Neither is a fallback for the other:

| Granularity   | Method                                                            | What it answers                                         | When to use it                                                                                                                                                                  |
| ------------- | ----------------------------------------------------------------- | ------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Component** | `DensityModel.Predict(x)` (or `ManifoldMixture.PredictComponent`) | Which fine-grained Gaussian piece best fits this point? | Density evaluation (`Pdf`, log-likelihood), Mahalanobis-to-assigned, conditional sampling, downstream models that consume per-component statistics, component-level diagnostics |
| **Cluster**   | `ManifoldMixture.PredictCluster(x)`                               | Which logical cluster does this point belong to?        | User-facing label output, evaluation against ground-truth cluster labels, anything where "the crescent" is a single answer                                                      |

The component granularity is _not_ a debugging artifact — it is the right answer for every question that lives below the topology layer. The cluster granularity is _not_ a coarsening of components — it is the only correct answer for the topology question, and it is wrong to derive it by anything other than the explicit `ComponentToClusterMap` remap.

This is also the prescription for `Flatten()`: it is **strictly** a density operation. It produces a `GaussianMixtureModel` that is mathematically equivalent in density to the recursive tree and carries no cluster identity. A caller that wants cluster identity must keep the `ManifoldMixture` wrapper, not just its `DensityModel`. If `Flatten()` ever returned a topology-aware object, every downstream consumer of `GaussianMixtureModel` would inherit topology-awareness it does not need — which is exactly what the wrapper-vs-subclass split is designed to prevent.

### Standalone recursive GMM — post-fit component aggregation

Everything above leaves `ComponentToClusterMap` to be populated externally — typically by an SPC topology oracle. That is the right path _when SPC is in the pipeline_, but a recursive GMM is also a standalone exotic-density modeller, and treating SPC as a prerequisite for cluster identity understates what GMM can do on its own. There is a substantial literature on recovering cluster identity directly from a fitted mixture, which the docs should not gloss over.

#### The components-vs-clusters mismatch

A flat full-covariance GMM is a universal approximator for densities on compact sets — given enough components, it can match arbitrary smooth densities to arbitrary accuracy. BIC-driven model selection (the local greedy scheme above, or a global sweep) optimises _density fidelity_: it chooses K to match the density well. The resulting K is typically larger than the number of perceptually or topologically distinct clusters, because non-convex regions need multiple components to tile their shape. This is not a defect — it is what enables the model to represent crescents, spirals, and product manifolds at all.

The consequence: **the "right K for density" and the "right number of clusters" are different questions**. BIC answers the first; component aggregation answers the second. A recursive GMM that only reports the flat density model has answered half the question.

#### The literature

Hennig (2010), _"Methods for merging Gaussian mixture components,"_ Advances in Data Analysis and Classification 4(1), is the canonical survey. The methods it catalogues are all post-fit operations on a fitted `GaussianMixtureModel`:

- **Modal merging** (Li, Ray, Lindsay 2007). Find the modes of the mixture density via gradient ascent. Components whose density basins lead to the same mode are merged. The merge tree is induced by the topology of the density function itself.
- **Entropy-based merging** (Baudry, Raftery, Celeux, Lo, Gottardo 2010). Greedily merge the pair of components whose merger reduces classification entropy least, until an entropy-criterion stops dropping. This directly addresses the BIC-overshoots-K phenomenon and is the most-cited GMM-side merging method.
- **Pairwise overlap / misclassification** (Hennig). Measure overlap as the probability of misclassification between two components; merge components above a threshold or in a hierarchical sweep.
- **Ridge-based** (Genovese, Perone-Pacifico, Verdinelli, Wasserman). Use density-ridge estimates to identify connected high-density structures; components whose means lie on the same ridge segment merge.

Each method produces a similarity (or merge-distance) between components. Wrapping that in a hierarchical agglomerative clustering pass produces a **component merge tree** — the GMM-side analog of `thermo/merge-tree/v1` on the SPC side.

#### The shape

A component merge tree is precisely an `IPartitionSequence<double>` (see [project-primer.md](./project-primer.md) and [spc-maturity.md](./spc-maturity.md) §17.6) — distance-indexed, with a partition at each merge level. The same `ElbowCriterion`, `MaxK`, and `Threshold` cutters that operate on SPC merge trees and on hierarchical-linkage trees apply here. Cutting the tree produces `ComponentToClusterMap` directly.

```csharp
public interface IComponentMergeStrategy
{
    // Build a merge tree over the components of a fitted GMM.
    LinkageTree BuildMergeTree(GaussianMixtureModel model, double[][] data);
}

// Concrete strategies follow the literature:
//   ModalMergeStrategy        — Li/Ray/Lindsay, density-mode basin merging
//   EntropyMergeStrategy      — Baudry et al., greedy entropy-reduction
//   PairwiseOverlapStrategy   — Hennig, misclassification-rate similarity
//   RidgeMergeStrategy        — Genovese et al. (deferred; non-trivial estimator)
```

The recursive driver — or any flat-fit consumer — composes a `ManifoldMixture` from the fitted `GaussianMixtureModel` plus a chosen merge strategy:

```csharp
GaussianMixtureModel flatModel = recursiveDriver.Fit(data).Flatten();
LinkageTree mergeTree = new EntropyMergeStrategy().BuildMergeTree(flatModel, data);
int[] componentToCluster = mergeTree.BestCut(new ElbowCriterion()).Labels;

var mixture = new ManifoldMixture(flatModel, componentToCluster);
```

#### Two symmetric paths to `ComponentToClusterMap`

The architecturally important point: **`ComponentToClusterMap` is the slot, and either path fills it the same way.**

| Path                     | Source of cluster identity                                                                                                                                                                                                                                                                   | When it fits                                                                                                                                                |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **SPC topology oracle**  | In the hybrid arc, the oracle provides `sᵢₖ` soft labels that constrain the E-step. `ComponentToClusterMap` is then populated by post-fit modal or entropy merging on the constrained-EM result — the oracle does not drive recursive splits or carry identity through the partition ladder. | When SPC is in the hybrid pipeline (Arc 3); oracle provides initialization parameters and E-step constraints; post-fit merging resolves components→clusters |
| **GMM post-fit merging** | The density model itself — modes, entropy, overlap, ridges of the fitted mixture                                                                                                                                                                                                             | Standalone GMM use; topology should be derivable from density alone; or a cross-check on SPC's answer                                                       |

Both paths produce a `ManifoldMixture` with the same shape. The flat `DensityModel` is identical in both cases. Downstream consumers of the `ManifoldMixture` cannot tell which path filled the map — and shouldn't need to. The two paths are complementary, not competing: a robust deployment can populate the map both ways and flag disagreements as a diagnostic, since SPC and density-merging looking at the same data and disagreeing on cluster count is itself a finding.

This also resolves a tension in the recursive driver: BIC-driven recursive splitting can stop at a depth that is correct for density but wrong for cluster count. A post-fit merge stage is the discipline that converts component count to cluster count without forcing the splitting criterion to do double duty.

#### Phasing

These methods are not v1 of the recursive variant. They land in this order:

1. **Recursive fit + `Flatten()`** — the model itself, density operation only.
2. **`ComponentToClusterMap` via SPC handoff** — the path documented above. Fills the slot for the SPC-driven case.
3. **`EntropyMergeStrategy`** as the first standalone path. Most-cited, BIC-pair-friendly, conceptually simple.
4. **`ModalMergeStrategy`** for cases where density topology is the right notion (vs. classification entropy).
5. **`PairwiseOverlapStrategy`** as an alternative; cheap to evaluate and useful as a cross-check.
6. **`RidgeMergeStrategy`** is a longer horizon; ridge estimation is its own non-trivial primitive.

The `LinkageTree` artifact and the `IComponentMergeStrategy` interface are part of the shared `Clustering` primitive (tier 2; see [project-primer.md](./project-primer.md)), not part of `GaussianMixture` itself — the merge methods take a fitted `GaussianMixtureModel` as input but their output is a generic merge tree, consumable by the same partition-cutting machinery used elsewhere.

#### Bayesian variants — same problem, different inference (tier 3, future reference)

The frequentist methods above operate on a fitted `GaussianMixtureModel` with point-estimate parameters. The Bayesian literature attacks the same problem from the inference side. None of these are v1; recorded for forward-looking design.

- **Mixtures of Finite Mixtures (Miller & Harrison 2014, 2018).** Direct argument that DP mixtures overestimate clusters because they conflate components with clusters. Provides a proper Bayesian model where K-for-clusters is the inferential target, with closed-form predictive structure. The most relevant Bayesian reference for the components-vs-clusters distinction.
- **RJMCMC over K (Richardson & Green 1997).** Reversible-jump MCMC produces a posterior over both K and the partition of points. The "merge" question becomes "what is the marginal posterior on the partition, integrating out K and component-mean configurations?" — bypasses the BIC-then-merge two-step entirely.
- **Variational Bayesian GMM with sparsity priors (Bishop PRML Ch. 10).** Components with weight → 0 prune themselves. Adjacent to merging but mechanistically different: prevents redundant components rather than merging them post-hoc.
- **Bayesian entropy / posterior-overlap merging.** Baudry et al.'s entropy method has a clean Bayesian upgrade — instead of EM plug-in responsibilities, use posterior predictive responsibilities from a Bayesian GMM. Merging decisions made on full posterior overlap rather than point estimates.
- **Wade & Ghahramani (2018), minimum-Binder-loss / minimum-VI partition.** Once you have a posterior over partitions, summarising it as a single point estimate is its own problem. Wade-Ghahramani provides loss-based partition summaries. Output: a single `int[N]` partition with optional credibility scores per pairwise co-clustering relation.

Output shapes diverge: Bayesian methods produce _posterior partition distributions_, not merge trees. The shared `Clustering` primitive's tier-3 slot holds these structures, with cutters (point-estimate summarisers) appropriate to their shape.

#### Topology-aware variants — alternative identity structures (tier 3, future reference)

Topology-aware methods derive cluster identity from the _density topology_ — modes, ridges, level-set connectivity — of the fitted mixture. They produce structures that don't always reduce to a merge tree.

- **Persistent homology of the density (Chazal, Guibas, Oudot, Skraba 2013; Bauer, Lange, Wardetzky 2012).** Compute persistence diagrams of the mixture density's level-set filtration. Components are merged when their pairing has low persistence (noise); high-persistence pairs are genuine cluster boundaries. Output is a persistence diagram, with persistence-threshold cuts producing partitions.
- **Mode-tree clustering (Li, Ray, Lindsay 2007; Rinaldo & Wasserman 2010; Chen, Genovese, Wasserman 2017).** Find density modes via gradient ascent; components in the same basin-of-attraction merge. Produces a _mode tree_, structurally similar to a merge tree but indexed by density-level rather than agglomeration step. Rinaldo-Wasserman provides convergence guarantees and bias-variance theory.
- **Reeb graph clustering.** The Reeb graph of a density encodes level-set connectivity. Connected components of the Reeb graph at a given threshold define clusters. Less common in statistics than in computational topology / scientific visualization.
- **Ridge-based merging (Genovese, Perone-Pacifico, Verdinelli, Wasserman).** Density ridges form a 1-D skeleton of high-density structure; components on the same ridge segment merge. Powerful but ridge estimation is its own non-trivial primitive.

These produce structures distinct from merge trees: persistence diagrams, mode trees, Reeb graphs. Each has its own cutter (persistence threshold, mode basin, ridge segment); the shared `Clustering` primitive should host them as additional tier-3 typed structures alongside the Bayesian shapes above.

#### Strategy interface naming

`IComponentMergeStrategy` as drafted above is correct for merge-tree-shaped methods (Hennig-survey methods, frequentist hierarchical aggregation). For Bayesian and topology-aware methods that produce different output structures, a more general `IClusterIdentityStrategy` contract is appropriate — one whose return type is a typed identity structure (merge tree, persistence diagram, mode tree, posterior partition distribution) and whose downstream consumer applies the cutter appropriate to that shape. The naming is recorded as **open** until concrete tier-3 implementations land; for tier-2 (Hennig-survey methods only) the merge-strategy framing is sufficient.

#### Smooth boundary / region representations (tier 4, aspirational)

A different category from "produce cluster identity" — taking an existing clustering result and producing a _smooth surface representation_ of cluster boundaries (or, equivalently, a smooth indicator function for each region). Recorded here for posterity; nothing in the v2 plan touches it.

The framing the user articulated: **reversible-jump MCMC over spline surfaces or cuts, like BARS but higher dimensional**. The relevant prior art:

- **BARS** (DiMatteo, Genovese, Kass 2001, _Biometrika_) — the canonical RJMCMC-over-knots univariate spline regression. Birth/death/move proposals on knots; the posterior averages over knot counts, locations, and coefficients. The starting point.
- **Bayesian MARS** (Denison, Mallick, Smith 1998, _JRSS B_) — direct higher-dimensional extension. RJMCMC over MARS basis functions (tensor products of univariate hinges). Operates in moderate D = 2–5. The most-cited "BARS-in-D-dimensions" reference.
- **BART — Bayesian Additive Regression Trees** (Chipman, George, McCulloch 2010). Not splines, but the most successful transdimensional Bayesian regression machinery in higher D. RJMCMC over an ensemble of CART trees; the ensemble averages to a smooth surface. Often outperforms tensor-spline methods for D > 4.
- **Treed Gaussian Processes** (Gramacy, Lee 2008) — RJMCMC trees with GP leaves. Hybrid: piecewise-stationary GP regression giving spline-like smoothness within partitions and adaptive piecewise structure across them.
- **Bayesian level-set inversion** (Iglesias, Lu, Stuart 2016 and follow-ups). The relevant strand specifically for _cuts_ — treats a region's boundary as the zero level-set of a Bayesian function and samples the function. Mostly developed in inverse problems but the machinery transfers to cluster boundaries.
- **Penalized-spline density estimation** (Eilers-Marx P-splines, Bayesian variants) — alternative density representations to GMM. Finite-dim with sparse priors rather than transdimensional, but RJMCMC variants exist.

**Two distinct targets:**

- **Density surfaces.** Bayesian MARS / BART as alternatives to GMM for density estimation. Posterior over functions $f: \mathbb{R}^D \to \mathbb{R}$.
- **Cuts.** Smooth surface representations of cluster boundaries. The post-clustering equivalent of replacing the pixelated `int[N]` boundary (per-point hard labels) with a continuous level-set surface. As far as is known, _open territory_ for SPC-derived clusters specifically; the closest published work is post-hoc level-set smoothing of clustering output rather than joint inference.

**Why this is "further out":**

- RJMCMC scales badly with dimension. Tensor-product spline anchors in D=4 with 10 anchors per axis = 10⁴ basis functions; birth/death/move proposals mix slowly. Bayesian MARS sidesteps this by restricting basis functions to interactions of small variable subsets.
- Jacobian determinants in dimension-changing moves get technical fast in D-dimensional spline parameterizations. Most practical higher-D RJMCMC restricts to special parameterizations or uses VB / pseudo-marginal HMC alternatives.
- The modern alternative for transdimensional inference is **variational Bayes with sparsity-inducing priors** (coefficients shrink to zero rather than appearing/disappearing). Less elegant but scales better.

**Where this would land architecturally:**

- A new **boundary / region representation** primitive, parallel to (not part of) the Clustering tiering. Inputs: a clustering result + the underlying data. Outputs: a Bayesian posterior over a boundary surface or region indicator.
- An **RJMCMC sampler** as shared infrastructure usable here _and_ by the tier-3 Bayesian-GMM variants (Miller-Harrison MFM, Richardson-Green RJMCMC over K). Worth pulling out as its own primitive once at least two consumers want it.
- New typed structures (spline / level-set posteriors) alongside merge trees, persistence diagrams, and posterior partition distributions in the shared `Clustering` primitive's tier-4 slot.
- For SPC specifically, a `thermo/cluster-boundary/v1` codec carrying a Bayesian posterior over a smooth surface separating equilibrium clusters would be the natural extension once the underlying machinery exists. The current `thermo/equilibrium-clusters/v1` (per-point `int[N]`) leaves the boundary implicit in adjacency; this would make it explicit.

**What's genuinely novel** (i.e. not just citing existing work):

- BARS-like for **decision surfaces / cluster boundaries** is much less explored than BARS-like for regression.
- BARS-like on **manifolds** has very thin literature (Camarinha / Crouch / Machado on Riemannian splines; no standard RJMCMC-on-manifold-splines reference) — would be genuine research.
- Joint inference of clustering + smooth boundary (rather than two-stage cluster-then-smooth) is, as far as is known, open.

This belongs in the open horizon. Pulling it forward into the actual roadmap is contingent on a concrete consumer use case and on the tier-3 Bayesian-GMM variants having landed (which establishes the RJMCMC infrastructure).

### Recommended composition: local BIC during construction, global flatten at the end

Two ways to use BIC in a recursive build:

- **Local BIC (greedy)** — at each node, compare the BIC of the parent single Gaussian to the BIC of the proposed child mixture _on that node's data subset_. Split if children win. This matches the `ISplitCriterion` shape below and runs on shrinking N as the tree deepens.
- **Global BIC (sweep)** — build to depth 1, flatten, score; build to depth 2, flatten, score; …; pick best depth. Statistically rigorous but pays full-EM cost at every depth.

The recommended composition is **local BIC for tree construction, unconstrained flatten as final output, optionally followed by one flat EM pass on the global dataset to settle component boundaries**. This preserves the divide-and-conquer cost benefit of the hierarchy, gives geometrically motivated initial parameters for the flat model, and outputs a standard `GaussianMixtureModel` that downstream consumers can use without any recursion-aware machinery. The hierarchy is, in this composition, a structured initializer for a flat model — which is exactly what the unconstrained path is designed for.

Global BIC is reserved for the rare case where there is no acceptable greedy proxy and the downstream consumer needs a statistically justified depth selection. It is not a default.

### `ISplitCriterion` shape

The recursive driver delegates the split decision to a strategy:

```csharp
public interface ISplitCriterion
{
    bool ShouldSplit(GaussianMixtureModel fittedModel, int dataCount, double finalLogLikelihood);
}
```

Concrete implementations:

- `BicSplitCriterion` — local BIC comparison as described above.
- `MdlSplitCriterion` — minimum description length variant.
- `SpcShatterCriterion` — **superseded by the constrained-EM design.** In the hybrid arc, recursive splitting (if any) is BIC-driven at initialization time; the oracle's role is soft-label constraints on the E-step (`IResponsibilityConstraint`), not split signals. Do not implement.

This keeps the recursive driver a pure tree builder; the policy that drives splitting is swappable without touching the EM machinery or `GmmNode`. The interface intentionally does not take the parent or sibling state — split decisions are local by design (see the previous subsection).

### Connection to the partition-cutting layer

A sweep of recursive mixtures at varying max depths is an `IPartitionSequence<int>` indexed by depth, scored by total BIC. `ElbowCriterion` on that sequence selects an optimal tree depth — the same general cutter that selects K for a flat GMM and a critical temperature for SPC. The depth-1 case is identical to the current `GaussianMixtureModel`. See [project-primer.md](./project-primer.md) for the cross-package layer this hooks into.
