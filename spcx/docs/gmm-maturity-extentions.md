# Hierarchical and Nested GMM Extensions

**Status:** Future research / v2+ — not in scope for the current `GaussianMixture` project.

This document records the design intent and literature anchors for hierarchical and nested mixture
model extensions so that the distinction between the current implementation and these richer model
families is explicit from the start.

---

## What the current implementation is

`GaussianMixture` is a **flat finite Gaussian mixture model** — K components in D dimensions, fit by
EM with a log-space E-step and Cholesky-backed covariance handling. The hierarchy visible in the
codebase (the `GmmTreeNode` / `ISpcShatterOracle` design intent) is an **engineered composition**:
it organises multiple flat GMM fits into a tree mirroring the SPC temperature hierarchy, but at
every node the model is still a standard finite mixture. There is no single global likelihood over a
tree-structured latent variable with shared parameters across levels.

The correct description is: _a hierarchical composition of flat GMMs guided by SPC_, not a
_hierarchical GMM model_.

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

## Relationship to the SPC handoff

The current design intent — SPC cores seed `InitializeWithParameters`, EM refines — is an
initialization strategy for the flat model and does not require any of the richer model families
above. It is compatible with all of them as a warm-start.

If the goal becomes "one Gaussian submodel per SPC cluster, with shared structure across the
temperature hierarchy," the right formulation is HMoG (family 2) or HDP (family 5), not a
hand-wired tree of flat GMM calls. The SPC partition ladder provides a natural prior on the
cluster structure that could inform either the gating network (HME) or the concentration parameter
(HDP).

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

Whichever fork is implemented, the recursive model must expose a `Flatten()` method that returns a flat `GaussianMixtureModel` with the same density. This keeps the recursive variant a *lens* on a flat model rather than a hard coupling, and means everything downstream that consumes a flat GMM (`Predict`, `Pdf`, `Sample`, `Mahal`) keeps working without recursion-aware overloads.

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

A flattened recursive mixture exposes K Gaussian components. The recursive tree split a *single* topological cluster (a curved manifold like a crescent) into multiple components to tile its shape. The flat model has no record of that grouping — it sees K independent density bumps in space. **Do not rely on the flattened GMM alone to remember that components 0..4 were all parts of the crescent.** That memory must live in a parallel artifact, not be smuggled into the flat model.

The recursive variant therefore exposes a wrapping object that holds the flat density model *and* the component-to-cluster map as siblings:

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

| Granularity | Method | What it answers | When to use it |
|---|---|---|---|
| **Component** | `DensityModel.Predict(x)` (or `ManifoldMixture.PredictComponent`) | Which fine-grained Gaussian piece best fits this point? | Density evaluation (`Pdf`, log-likelihood), Mahalanobis-to-assigned, conditional sampling, downstream models that consume per-component statistics, component-level diagnostics |
| **Cluster** | `ManifoldMixture.PredictCluster(x)` | Which logical cluster does this point belong to? | User-facing label output, evaluation against ground-truth cluster labels, anything where "the crescent" is a single answer |

The component granularity is *not* a debugging artifact — it is the right answer for every question that lives below the topology layer. The cluster granularity is *not* a coarsening of components — it is the only correct answer for the topology question, and it is wrong to derive it by anything other than the explicit `ComponentToClusterMap` remap.

This is also the prescription for `Flatten()`: it is **strictly** a density operation. It produces a `GaussianMixtureModel` that is mathematically equivalent in density to the recursive tree and carries no cluster identity. A caller that wants cluster identity must keep the `ManifoldMixture` wrapper, not just its `DensityModel`. If `Flatten()` ever returned a topology-aware object, every downstream consumer of `GaussianMixtureModel` would inherit topology-awareness it does not need — which is exactly what the wrapper-vs-subclass split is designed to prevent.

### Standalone recursive GMM — post-fit component aggregation

Everything above leaves `ComponentToClusterMap` to be populated externally — typically by an SPC topology oracle. That is the right path *when SPC is in the pipeline*, but a recursive GMM is also a standalone exotic-density modeller, and treating SPC as a prerequisite for cluster identity understates what GMM can do on its own. There is a substantial literature on recovering cluster identity directly from a fitted mixture, which the docs should not gloss over.

#### The components-vs-clusters mismatch

A flat full-covariance GMM is a universal approximator for densities on compact sets — given enough components, it can match arbitrary smooth densities to arbitrary accuracy. BIC-driven model selection (the local greedy scheme above, or a global sweep) optimises *density fidelity*: it chooses K to match the density well. The resulting K is typically larger than the number of perceptually or topologically distinct clusters, because non-convex regions need multiple components to tile their shape. This is not a defect — it is what enables the model to represent crescents, spirals, and product manifolds at all.

The consequence: **the "right K for density" and the "right number of clusters" are different questions**. BIC answers the first; component aggregation answers the second. A recursive GMM that only reports the flat density model has answered half the question.

#### The literature

Hennig (2010), *"Methods for merging Gaussian mixture components,"* Advances in Data Analysis and Classification 4(1), is the canonical survey. The methods it catalogues are all post-fit operations on a fitted `GaussianMixtureModel`:

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

| Path | Source of cluster identity | When it fits |
|---|---|---|
| **SPC topology oracle** | The graph prior — proximity rule, metric, coupling kernel under SPC's Potts dynamics. Identity propagates through the partition ladder and is recorded as the recursive tree builds | When SPC is in the pipeline; topology is dominated by graph-structural arguments (false bridges, manifold curvature) |
| **GMM post-fit merging** | The density model itself — modes, entropy, overlap, ridges of the fitted mixture | Standalone GMM use; topology should be derivable from density alone; or a cross-check on SPC's answer |

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

Output shapes diverge: Bayesian methods produce *posterior partition distributions*, not merge trees. The shared `Clustering` primitive's tier-3 slot holds these structures, with cutters (point-estimate summarisers) appropriate to their shape.

#### Topology-aware variants — alternative identity structures (tier 3, future reference)

Topology-aware methods derive cluster identity from the *density topology* — modes, ridges, level-set connectivity — of the fitted mixture. They produce structures that don't always reduce to a merge tree.

- **Persistent homology of the density (Chazal, Guibas, Oudot, Skraba 2013; Bauer, Lange, Wardetzky 2012).** Compute persistence diagrams of the mixture density's level-set filtration. Components are merged when their pairing has low persistence (noise); high-persistence pairs are genuine cluster boundaries. Output is a persistence diagram, with persistence-threshold cuts producing partitions.
- **Mode-tree clustering (Li, Ray, Lindsay 2007; Rinaldo & Wasserman 2010; Chen, Genovese, Wasserman 2017).** Find density modes via gradient ascent; components in the same basin-of-attraction merge. Produces a *mode tree*, structurally similar to a merge tree but indexed by density-level rather than agglomeration step. Rinaldo-Wasserman provides convergence guarantees and bias-variance theory.
- **Reeb graph clustering.** The Reeb graph of a density encodes level-set connectivity. Connected components of the Reeb graph at a given threshold define clusters. Less common in statistics than in computational topology / scientific visualization.
- **Ridge-based merging (Genovese, Perone-Pacifico, Verdinelli, Wasserman).** Density ridges form a 1-D skeleton of high-density structure; components on the same ridge segment merge. Powerful but ridge estimation is its own non-trivial primitive.

These produce structures distinct from merge trees: persistence diagrams, mode trees, Reeb graphs. Each has its own cutter (persistence threshold, mode basin, ridge segment); the shared `Clustering` primitive should host them as additional tier-3 typed structures alongside the Bayesian shapes above.

#### Strategy interface naming

`IComponentMergeStrategy` as drafted above is correct for merge-tree-shaped methods (Hennig-survey methods, frequentist hierarchical aggregation). For Bayesian and topology-aware methods that produce different output structures, a more general `IClusterIdentityStrategy` contract is appropriate — one whose return type is a typed identity structure (merge tree, persistence diagram, mode tree, posterior partition distribution) and whose downstream consumer applies the cutter appropriate to that shape. The naming is recorded as **open** until concrete tier-3 implementations land; for tier-2 (Hennig-survey methods only) the merge-strategy framing is sufficient.

#### Smooth boundary / region representations (tier 4, aspirational)

A different category from "produce cluster identity" — taking an existing clustering result and producing a *smooth surface representation* of cluster boundaries (or, equivalently, a smooth indicator function for each region). Recorded here for posterity; nothing in the v2 plan touches it.

The framing the user articulated: **reversible-jump MCMC over spline surfaces or cuts, like BARS but higher dimensional**. The relevant prior art:

- **BARS** (DiMatteo, Genovese, Kass 2001, *Biometrika*) — the canonical RJMCMC-over-knots univariate spline regression. Birth/death/move proposals on knots; the posterior averages over knot counts, locations, and coefficients. The starting point.
- **Bayesian MARS** (Denison, Mallick, Smith 1998, *JRSS B*) — direct higher-dimensional extension. RJMCMC over MARS basis functions (tensor products of univariate hinges). Operates in moderate D = 2–5. The most-cited "BARS-in-D-dimensions" reference.
- **BART — Bayesian Additive Regression Trees** (Chipman, George, McCulloch 2010). Not splines, but the most successful transdimensional Bayesian regression machinery in higher D. RJMCMC over an ensemble of CART trees; the ensemble averages to a smooth surface. Often outperforms tensor-spline methods for D > 4.
- **Treed Gaussian Processes** (Gramacy, Lee 2008) — RJMCMC trees with GP leaves. Hybrid: piecewise-stationary GP regression giving spline-like smoothness within partitions and adaptive piecewise structure across them.
- **Bayesian level-set inversion** (Iglesias, Lu, Stuart 2016 and follow-ups). The relevant strand specifically for *cuts* — treats a region's boundary as the zero level-set of a Bayesian function and samples the function. Mostly developed in inverse problems but the machinery transfers to cluster boundaries.
- **Penalized-spline density estimation** (Eilers-Marx P-splines, Bayesian variants) — alternative density representations to GMM. Finite-dim with sparse priors rather than transdimensional, but RJMCMC variants exist.

**Two distinct targets:**

- **Density surfaces.** Bayesian MARS / BART as alternatives to GMM for density estimation. Posterior over functions $f: \mathbb{R}^D \to \mathbb{R}$.
- **Cuts.** Smooth surface representations of cluster boundaries. The post-clustering equivalent of replacing the pixelated `int[N]` boundary (per-point hard labels) with a continuous level-set surface. As far as is known, *open territory* for SPC-derived clusters specifically; the closest published work is post-hoc level-set smoothing of clustering output rather than joint inference.

**Why this is "further out":**

- RJMCMC scales badly with dimension. Tensor-product spline anchors in D=4 with 10 anchors per axis = 10⁴ basis functions; birth/death/move proposals mix slowly. Bayesian MARS sidesteps this by restricting basis functions to interactions of small variable subsets.
- Jacobian determinants in dimension-changing moves get technical fast in D-dimensional spline parameterizations. Most practical higher-D RJMCMC restricts to special parameterizations or uses VB / pseudo-marginal HMC alternatives.
- The modern alternative for transdimensional inference is **variational Bayes with sparsity-inducing priors** (coefficients shrink to zero rather than appearing/disappearing). Less elegant but scales better.

**Where this would land architecturally:**

- A new **boundary / region representation** primitive, parallel to (not part of) the Clustering tiering. Inputs: a clustering result + the underlying data. Outputs: a Bayesian posterior over a boundary surface or region indicator.
- An **RJMCMC sampler** as shared infrastructure usable here *and* by the tier-3 Bayesian-GMM variants (Miller-Harrison MFM, Richardson-Green RJMCMC over K). Worth pulling out as its own primitive once at least two consumers want it.
- New typed structures (spline / level-set posteriors) alongside merge trees, persistence diagrams, and posterior partition distributions in the shared `Clustering` primitive's tier-4 slot.
- For SPC specifically, a `thermo/cluster-boundary/v1` codec carrying a Bayesian posterior over a smooth surface separating equilibrium clusters would be the natural extension once the underlying machinery exists. The current `thermo/equilibrium-clusters/v1` (per-point `int[N]`) leaves the boundary implicit in adjacency; this would make it explicit.

**What's genuinely novel** (i.e. not just citing existing work):

- BARS-like for **decision surfaces / cluster boundaries** is much less explored than BARS-like for regression.
- BARS-like on **manifolds** has very thin literature (Camarinha / Crouch / Machado on Riemannian splines; no standard RJMCMC-on-manifold-splines reference) — would be genuine research.
- Joint inference of clustering + smooth boundary (rather than two-stage cluster-then-smooth) is, as far as is known, open.

This belongs in the open horizon. Pulling it forward into the actual roadmap is contingent on a concrete consumer use case and on the tier-3 Bayesian-GMM variants having landed (which establishes the RJMCMC infrastructure).

### Recommended composition: local BIC during construction, global flatten at the end

Two ways to use BIC in a recursive build:

- **Local BIC (greedy)** — at each node, compare the BIC of the parent single Gaussian to the BIC of the proposed child mixture *on that node's data subset*. Split if children win. This matches the `ISplitCriterion` shape below and runs on shrinking N as the tree deepens.
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
- `SpcShatterCriterion` — defers to an `ISpcShatterOracle`, terminating when the SPC partition ladder says the local topology has stabilised.

This keeps the recursive driver a pure tree builder; the policy that drives splitting is swappable without touching the EM machinery or `GmmNode`. The interface intentionally does not take the parent or sibling state — split decisions are local by design (see the previous subsection).

### Connection to the partition-cutting layer

A sweep of recursive mixtures at varying max depths is an `IPartitionSequence<int>` indexed by depth, scored by total BIC. `ElbowCriterion` on that sequence selects an optimal tree depth — the same general cutter that selects K for a flat GMM and a critical temperature for SPC. The depth-1 case is identical to the current `GaussianMixtureModel`. See [project-primer.md](./project-primer.md) for the cross-package layer this hooks into.
