# Clustering Primitive — Tiered Design

> **Status:** DRAFT v0.1 — initial scope capture
> **Date:** 2026-05-07
> **Purpose:** Define the shape of a shared `Clustering` primitive that hosts cross-application clustering machinery — partition-cutting, identity strategies, and (later) non-merge-tree identity structures and boundary representations. Neither SPC nor GMM should own this seam; both consume it.
> **Sibling documents:** [spc-maturity.md](./spc-maturity.md) §17.6 and [gmm-maturity-extentions.md](./gmm-maturity-extentions.md) reference this primitive from the SPC and GMM sides respectively.

---

## 1. Why this exists

SPC and GMM both produce indexed sequences of partitions (temperature-indexed for SPC, K-indexed or depth-indexed for GMM) and both face the same "select a cut" problem (susceptibility peak, BIC elbow, fixed-K, distance threshold). The shared shape is a candidate `Clustering` primitive that neither SPC nor GMM should own. None of it is committed to a project yet; the discipline is to recognize the seam before either application layer takes ownership of it.

This is a worked example of the bottom-up library discipline articulated in [project-primer.md](./project-primer.md): when SPC or GMM needs something new, the question is always _should this live in the application layer, or does it belong in a primitive that a future consumer could use independently?_ The answer here is "primitive."

## 2. The tiered scope

The primitive is intentionally **tiered**, scoped to grow as concrete needs surface rather than committing to an exhaustive surface up front.

### Tier 1 (immediate) — partition-cutting

The lowest-level shared layer. Both SPC and GMM produce sequences of partitions; both need to select one.

- `IPartitionSequence<TScore>` — temperature-indexed (SPC), K-indexed or depth-indexed (GMM).
- `IPartitionCriterion<TScore>` — strategy for selecting an index.
- `CutResult` — the resolved partition + its score + its criterion.
- `IClusterMembership` — out-of-sample prediction for a fitted partition.
- General cutters: `MaxK`, `Threshold`, `Elbow`.
- Merge-tree / linkage representations (MATLAB `Z`-style) as the most common sequence shape.

This is what SPC and GMM should depend on as soon as the maturity work in [spc-maturity.md](./spc-maturity.md) §17 and [gmm-maturity-extentions.md](./gmm-maturity-extentions.md) lands. Application-specific criteria (`SusceptibilityPeakCriterion`, `KLDivergenceCriterion` on the SPC side; `BICCriterion` on the GMM side) implement `IPartitionCriterion<double>` against `IPartitionSequence<double>` exposed by the application's tree / model-path output.

### Tier 2 (when the recursive GMM variant lands) — component-aggregation strategies

The natural expansion when the recursive GMM matures. The "component-vs-cluster" mismatch (see [gmm-maturity-extentions.md](./gmm-maturity-extentions.md) "Components are not clusters") creates a need for strategies that aggregate Gaussian components into topological clusters.

- `IClusterIdentityStrategy` (or similarly-named) — contract for producing a `ComponentToClusterMap` from a fitted `GaussianMixtureModel`.
- Concrete implementations from the Hennig 2010 survey: `EntropyMergeStrategy`, `ModalMergeStrategy`, `PairwiseOverlapStrategy`, `RidgeMergeStrategy`.
- The same `ManifoldMixture` slot is filled by either an SPC topology oracle or a GMM-internal merge strategy — the cutter is generic.

For tier-2 specifically, the merge-tree-shaped output makes `IComponentMergeStrategy` (returning a `LinkageTree`) the right contract; the broader `IClusterIdentityStrategy` framing only becomes load-bearing at tier 3.

### Tier 3 (future reference, captured but not built) — non-merge-tree identity structures

Methods that produce identity structures distinct from merge trees. Each requires its own typed structure and cutter; the design is recorded here so pulling any of these in is an additive change, not a redesign.

- **Persistence diagrams** — TDA-based clustering (Chazal, Guibas, Oudot, Skraba 2013; Bauer, Lange, Wardetzky 2012). Persistence-threshold cuts.
- **Bayesian posterior partition distributions** — DPMM, Mixtures of Finite Mixtures (Miller-Harrison 2014, 2018), RJMCMC over K (Richardson-Green 1997). Cutters are loss-based partition summarisers (Wade-Ghahramani 2018, minimum-Binder-loss / minimum-VI partition).
- **Mode-tree / Reeb-graph representations** — density-modal clustering (Li, Ray, Lindsay 2007; Rinaldo-Wasserman 2010; Chen, Genovese, Wasserman 2017). Mode-basin cuts.
- **Variational Bayesian GMM with sparsity priors** — components self-prune; not a merge-tree shape but produces the same downstream artifact (a partition).

The Bayesian and topology-aware variants are documented in detail in [gmm-maturity-extentions.md](./gmm-maturity-extentions.md). The shared primitive's tier-3 slot hosts the typed structures and the cutters; the GMM doc hosts the literature and the algorithmic detail.

### Tier 4 (aspirational, parallel track) — boundary / region representations

A **different category** from tiers 1–3. Tiers 1–3 produce cluster identity (which point belongs to which cluster). Tier 4 takes a clustering result and produces a **smooth-surface representation** of cluster boundaries or regions.

- **Bayesian MARS** (Denison, Mallick, Smith 1998) — RJMCMC over tensor-spline basis functions. Direct higher-D extension of BARS.
- **BART** (Chipman, George, McCulloch 2010) — RJMCMC over tree ensembles; the most successful transdimensional Bayesian regression in higher D.
- **Treed Gaussian Processes** (Gramacy, Lee 2008) — RJMCMC trees with GP leaves.
- **Bayesian level-set inversion** (Iglesias, Lu, Stuart 2016) — region boundary as the zero level-set of a Bayesian function.
- **Penalized-spline density estimation** (Eilers-Marx P-splines and Bayesian variants) — alternative density representations.

RJMCMC infrastructure is shared with tier 3 (Miller-Harrison MFM, Richardson-Green RJMCMC over K both rely on it); pulling the sampler out as its own primitive becomes worthwhile when at least two consumers want it.

For SPC specifically, a `thermo/cluster-boundary/v1` codec carrying a Bayesian posterior over a smooth surface separating equilibrium clusters would extend `thermo/equilibrium-clusters/v1` (per-point `int[N]`) by making the boundary explicit. See [gmm-maturity-extentions.md](./gmm-maturity-extentions.md) "Smooth boundary / region representations" for the longer treatment.

Recorded for posterity, not on any roadmap. Genuinely speculative.

## 3. Tier philosophy

| Tier | Status | Trigger |
|---|---|---|
| 1 | Immediate | SPC and GMM maturity scopes both depend on it |
| 2 | Next | Recursive GMM variant lands |
| 3 | Open horizon | Concrete consumer use case for a non-merge-tree identity structure |
| 4 | Aspirational | A consumer for smooth-boundary representations exists; tier-3 RJMCMC infrastructure is in place |

The discipline is to **capture the shape now** so that committing to any of tiers 2–4 later is additive rather than redesign. None of this commits implementation; what it commits is the design boundary — the seam between SPC, GMM, and a shared clustering layer is recognized before either application layer takes ownership of it.

## 4. Strategy interface naming

Recorded as **open** until concrete tier-3 implementations land.

- **Tier 2 only** (Hennig-survey methods, frequentist hierarchical aggregation) — `IComponentMergeStrategy` returning a `LinkageTree` is sufficient.
- **Tier 3 expansion** — `IClusterIdentityStrategy` with a typed return (merge tree, persistence diagram, mode tree, posterior partition distribution); downstream consumers apply the cutter appropriate to the shape.

For tier 4 the contract is genuinely different (input is a clustering result; output is a surface posterior), so a separate `IBoundaryRepresentationStrategy` or similarly-named primitive belongs alongside, not as a generalization of the identity-strategy interface.

## 5. Relationship to other docs

- [project-primer.md](./project-primer.md) — articulates the bottom-up library discipline that motivates this primitive.
- [spc-maturity.md](./spc-maturity.md) §17.6 — the SPC side; `SpcTreeMatrix : IPartitionSequence<double>`, SPC-specific criteria.
- [gmm-maturity-extentions.md](./gmm-maturity-extentions.md) — the GMM side; component-aggregation strategies, the recursive variant, and the longer treatment of tier-3 Bayesian / topology-aware variants and tier-4 boundary representations.
- [visualization-engine.md](./visualization-engine.md) — consumer of the primitive; clustering criteria show up as cut-controls in the dashboard.
- [state-engine-design.md](./state-engine-design.md) — codecs that persist the typed structures (merge trees, persistence diagrams, posterior partition distributions, boundary surfaces) when they exist.

## 6. Open questions

- **Project layout.** When the primitive becomes code, where does it live? Probably `projects/Clustering/` with `src/clustering/` source, paralleling the other primitives in [project-primer.md](./project-primer.md) "Where things are." Not committed until tier 1 is being implemented.
- **Strategy interface naming.** `IClusterIdentityStrategy` vs. `IClusterMergeStrategy` vs. retaining `IComponentMergeStrategy` as the only strategy — settled when tier-3 implementations exist.
- **Posterior partition distribution shape.** The right typed representation for a Bayesian posterior over partitions (sample-based vs. moment-summarised vs. closed-form) is open until a Bayesian variant is built.
- **Cross-tier compositions.** Some pipelines might want tier-1 + tier-3 (e.g. a merge tree cut, then refined via a Bayesian posterior). Composition rules are not specified; they fall out as concrete uses arise.

---

> **Next steps:** none. Tier 1 lands when [spc-maturity.md](./spc-maturity.md) §17 and [gmm-maturity-extentions.md](./gmm-maturity-extentions.md) materialise as code; this doc absorbs whatever shape those land with. Tiers 2–4 wait for triggers documented in §3.
