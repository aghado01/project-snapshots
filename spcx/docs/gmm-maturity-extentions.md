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
