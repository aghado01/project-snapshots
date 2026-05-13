# pwshspc Session Primer

> Share this at the start of a new session so the assistant has working context
> without re-explaining the project, conventions, or process.

> **What this doc is.** Orientation, design philosophy, development approach, conventions, and pitfalls — the kind of meta-instruction that originally lived in `.github/instructions/`. It is **not** a design document, a roadmap, or a record of as-built behavior. It points at those when they exist.
>
> **Where to go for what this doc isn't:**
>
> - Design / scope for a specific subsystem → its dedicated doc in `docs/` (see _Doc map_ below).
> - Roadmap / phasing for a maturity scope → inside the relevant maturity doc.
> - As-built code state and current behavior → the source tree itself; any current behavior that conflicts with target design is captured as **debt** in the relevant maturity doc, not here.
> - Current project and source inventory → root `project.toml`; keep it updated as routine maintenance when projects or source roots move.
>
> When in doubt: this doc tells you _how to work_; the maturity / scope docs tell you _what to build_; the source tree tells you _what's there now_.

---

## Doc map

| Doc                                                        | Owns                                                                                                                                                                                                                                                                                                                                                                                                      |
| ---------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [project-primer.md](./project-primer.md) (this doc)        | Session orientation, design philosophy, working style, conventions, pitfalls                                                                                                                                                                                                                                                                                                                              |
| [spc-maturity.md](./spc-maturity.md)                       | SPC bespoke→primitive renovation: API shape, per-T primitive, execution modes, partial-T epoch contract, correctness items, analysis / tree / handoff layer                                                                                                                                                                                                                                               |
| [gmm-maturity-extentions.md](./gmm-maturity-extentions.md) | Hierarchical / nested / Bayesian / topology-aware GMM extensions; design rules for the recursive variant; aspirational boundary-representation track                                                                                                                                                                                                                                                      |
| [state-engine-design.md](./state-engine-design.md)         | General-purpose state / checkpoint engine: artifact log, codecs, streams, supervisor execution, codec inventory, on-disk layout, debt list                                                                                                                                                                                                                                                                |
| [clustering-primitive.md](./clustering-primitive.md)       | Shared cross-application clustering layer: tiered scope (partition-cutting → component aggregation → non-merge-tree identity → boundary representations)                                                                                                                                                                                                                                                  |
| [visualization-engine.md](./visualization-engine.md)       | Visualization layer that consumes engine artifacts: invariants, three-layer architecture, configuration matrix, pedagogical scenarios, phasing                                                                                                                                                                                                                                                            |
| [viz-core-architecture.md](./viz-core-architecture.md)     | As-built `VizCore` Passes 1–9 (layer model, scene pipeline, JSON render target, interactive VizApi server, schema-driven regen panel, edge/false-bridge rendering, spine/frame overlays, scalar heatmap, generator picker, GMM overlay + generalization, Wing-2 TDA flow, `GraphBuilder` single-graph wiring, `VizKernel` enum, kernel/bandwidth controls, `FisherRaoSimplex`/`FisherRaoHalfPlane` split) |

## What this project is

**pwshspc / SPCX** is a C#-first scientific computing library targeting **.NET 10 LTS** (`net10.0`), with PowerShell 7.6 for orchestration where useful. The motivating application is Superparamagnetic Clustering (SPC): a statistical physics approach using the Potts model and Swendsen-Wang Monte Carlo on proximity graphs with pluggable metrics and couplings. The temperature sweep produces thermodynamic and information signals that drive cluster extraction and SPC→GMM handoff.

The SPC-GMM pipeline is the primary application context, but it is not the scope boundary. The project follows a **bottom-up scientific computing library discipline**: each new algorithmic need is evaluated as a candidate for a standalone, reusable primitive project before it is tied to any application layer. Distance metrics, coupling kernels, proximity graph rules, statistical estimators, linear algebra primitives, Gaussian mixture models, and hash/sketching primitives all exist as independent assemblies that happen to compose for SPC — but each can be used without it.

This recursive design process is intentional. When SPC or GMM needs something new, the question is always: _should this live in the application layer, or does it belong in a primitive that a future consumer could use independently?_ The answer shapes where code lands and how it is namespaced. The goal is a coherent library, not a collection of SPC-specific utilities.

A worked example of this discipline: SPC and GMM both produce indexed sequences of partitions and both face the same "select a cut" problem. That shared shape is a candidate `Clustering` primitive that neither application should own — see [clustering-primitive.md](./clustering-primitive.md) for the tiered scope. A planned first-class consumer of these primitives is the visualization engine — see [visualization-engine.md](./visualization-engine.md). The primer's job is to flag that these unowned seams exist and point at the docs that own them; it does not duplicate their content.

> **Platform note:** PS 7.6 / .NET 10 LTS is the development basis. `Directory.Build.props` is the shared SDK authority and should keep new projects on `net10.0` with build output redirected to root `artifacts/`. Language and runtime features from this generation (e.g. span-oriented APIs, AVX-512 JIT auto-vectorization, `SearchValues<T>`, GC region improvements) should be considered when evaluating optimization opportunities; don't constrain solutions to older runtime capabilities.

## HPC programming

This project targets relentless performance. Modern .NET gives us the tools; using them correctly is a design discipline, not an afterthought. The following patterns recur throughout `src/` and should be the default for new compute code:

- **Zero allocation on hot paths.** Allocate once at construction or use caller-supplied scratch. Never allocate inside loops or per-point operations.
- **Three-tier scratch dispatch.** `stackalloc` for small N (≤ 64 or ≤ 512 depending on concern), `ThreadLocal<T>` for medium N on reused paths, `ArrayPool<T>.Shared` for large N. See `Mahalanobis.cs` and `ProductManifoldMedian` for examples.
- **`ReadOnlySpan<T>` / `Span<T>` over arrays** at primitive boundaries. Avoids copies and enables stack allocation at call sites.
- **`TensorPrimitives`** for vectorizable reductions (dot products, sums, norms). Prefer over manual loops where the operation maps cleanly.
- **`[MethodImpl(AggressiveInlining)]`** on inner-loop kernels where the JIT boundary cost matters (e.g. `QuadraticFormCore` in `Mahalanobis.cs`).
- **AVX-512 / SIMD auto-vectorization.** Keep inner loops free of branches, pointer aliasing, and non-sequential memory access to allow JIT auto-vectorization.
- **`readonly struct` for internal returns** (e.g. `SimulationResult`). Avoids heap allocation for short-lived value carriers.
- **Minimize indirection in tight loops.** Prefer concrete types over interfaces in hot paths; pass interface-typed manifolds at the outer call boundary, not inside per-point kernels.

**Parallel computing** is a standing question, not a blanket policy. The design question is always _is this worth parallelizing, and can it be posed correctly?_ Two cases are clear wins: (1) embarrassingly parallel work — per-point metric evaluation, per-edge coupling computation, per-component density evaluation — where threads share no state; and (2) structurally bounded parallel phases where a clear closure exists before the next stage begins. CSR graph initialization (pairwise metrics → edge list → CSR construction) is a concrete example of the second: the entire graph initialization can be parallelized, and the resulting graph is complete and immutable before the Swendsen-Wang hot path starts. SW itself achieves parallelism through a different mechanism — cluster proposal and bond activation can overlap across independent edges by careful state management, not by naive per-spin threading. Where parallelism is introduced it must have a defined boundary, thread-safe access to shared state (read-only after construction, or synchronized write paths), and explicit avoidance of over-subscription. `ThreadLocal<T>` scratch buffers are the preferred pattern for per-thread working memory; avoid shared mutable buffers.

## Project inventory

The current project and source inventory lives in the repo root at `project.toml`.

Treat `project.toml` as part of routine maintenance:

- update it when a project is added, removed, renamed, or re-scoped;
- update it when source files or source roots move between domains;
- update this primer only when the inventory contract or maintenance guidance changes.

High-level orientation only:

- `projects/` contains the SDK-style project definitions; `src/` contains the domain source roots.
- `src/clustering/spc/` is currently under renovation as part of the SPC maturity work. Primitives are being pulled into their domain projects and SPC is being rewritten as a consumer of those primitives plus its remaining Potts/runtime and thermo residuals.
- Because of that renovation, `src/clustering/spc/` is not the authority on target architecture. For intended SPC shape, prefer [spc-maturity.md](./spc-maturity.md) and [state-engine-design.md](./state-engine-design.md); for the current tree layout, prefer `project.toml`.

## Working style

**Convention/contract-based design with fail-fast.** Don't add verbose defensive checks where the system design guarantees correctness. Prefer typed contracts over stringly-typed flexibility.

Use the current source tree, `README.md`, `changelog.md`, and the user's latest request as live context. Treat `TODO.md` as informal backlog signal only, not as an authority.

**The user collects feedback from multiple assistants** (Gemini, Perplexity, Nemotron, GPT, Grok, etc.) and brings it in as `.discussion/` files. Treat these as information to consider, not directives. The user makes final calls on design; the assistant's job is to engage critically, not rubber-stamp.

**C#-first seam discipline:** standalone computational elements should be static classes or small typed primitives in C# projects. Namespace primitives by what they are (`DistanceMetrics`, `ProximityGraphs`, `CouplingKernels`, `StatisticalEstimators`, `SyntheticDatasets`, `Hashish`), and namespace application layers by what they do (`SpcCore`). Higher layers dispatch primitives for SPC, GMM handoff, analysis, or harness applications. PowerShell remains useful for orchestration/UX, but new compute should not be tied to PowerShell or `SpcBatch` when it can stand alone.

## Key conventions

- Gaussian coupling $J = \exp(-d^2 / 2\Delta^2)$ is the production physics model. Linear J is legacy/fast mode.
- `SpcBatchRequest`/`SpcBatchResult` are sealed classes (mutable DTOs). Internal returns like `SimulationResult` are readonly structs.
- Current SPC dispatch axes on `SpcBatchRequest`: `SpcMetric`, `SpcProximity`, `DeltaEstimator`, and `CouplingKernel`. Metric implementations under `src/metrics/`, graph topology selectors under `src/graphs/construction/`, coupling kernels under `src/graphs/coupling/`, TDA graph primitives under `src/graphs/tda/`, estimators under `src/estimators/`, synthetic generators under `src/synthetic/`, and Hashish primitives under `src/hashish/` should stay independent of `SpcBatch`; `src/clustering/spc/graph.cs` and the SPC dispatcher bind primitives to SPC graph initialization via `GraphBuilder.Build`. `src/clustering/spc/synthetic.cs` is deliberately outside `SpcCore`, compiled by `SpcSynthetic`, and bridges `SyntheticDatasets` to `SpcBatchRequest` for harnesses.
- `SpcBatchResult.Warnings` surfaces graph validation diagnostics (disconnected components, isolated nodes, coverage) to the PS layer.
- `GaussianMixture` compiles `src/clustering/gmm/` under namespace `StatisticalEstimators` (assembly name `GaussianMixture`). Depends on `LinearAlgebra` and `DistanceMetrics`. Public surface: `Fit`, `Predict`, `PredictProba`, `Pdf`, `Mahal`, `Sample`, `NumIterations`, `IsConverged`, `FinalLogLikelihood`.
- `LinearAlgebra` compiles `src/linalg/` as a standalone primitive with no dependencies. Current content includes `Cholesky` and `Frobenius` primitives.
- `Mahalanobis.DistanceSquared` (`src/metrics/Mahalanobis.cs`) returns the raw quadratic form D² = (a−b)ᵀΣ⁻¹(a−b) without the sqrt; `Distance` wraps it. Prefer `DistanceSquared` when the sqrt is not needed (e.g. log-pdf, Mahal surface).
- SDK-style C# projects live under `projects/<ProjectName>/`, target `net10.0` through `Directory.Build.props`, and explicitly include source from `src/`. Keep `bin/` and `obj/` out of `src/`; generated output belongs under root `artifacts/`. Treat `Add-Type` assumptions as legacy transition context, not the architectural default.
- Checkpoints / run state — current implementation in `src/clustering/spc/checkpoint.cs` is the run-state substrate (not just crash recovery), and is treated as transitional debt. Canonical engine target: [state-engine-design.md](./state-engine-design.md). SPC-specific renovation: [spc-maturity.md](./spc-maturity.md) (§16.1 documents the current on-disk shape; §29 of state-engine-design lists the debt items). New code should align with the codec contract in state-engine-design §24, not extend the current implementation.

## What not to do

- Don't add features beyond what's in the current phase without discussing first.
- Don't suggest ValueTuples for PS-facing returns (bad interop: `.Item1`).
- Don't treat `.discussion/` thread suggestions as committed decisions — they're input, not plan.
- Don't introduce a new process ledger unless the user explicitly asks for one.
