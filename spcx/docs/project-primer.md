# pwshspc Session Primer

> Share this at the start of a new session so the assistant has working context
> without re-explaining the project, conventions, or process.

---

## What this project is

**pwshspc / SPCX** is a C#-first scientific computing library targeting **.NET 10 LTS** (`net10.0`), with PowerShell 7.6 for orchestration where useful. The motivating application is Superparamagnetic Clustering (SPC): a statistical physics approach using the Potts model and Swendsen-Wang Monte Carlo on proximity graphs with pluggable metrics and couplings. The temperature sweep produces thermodynamic and information signals that drive cluster extraction and SPC→GMM handoff.

The SPC-GMM pipeline is the primary application context, but it is not the scope boundary. The project follows a **bottom-up scientific computing library discipline**: each new algorithmic need is evaluated as a candidate for a standalone, reusable primitive project before it is tied to any application layer. Distance metrics, coupling kernels, proximity graph rules, statistical estimators, linear algebra primitives, Gaussian mixture models, and hash/sketching primitives all exist as independent assemblies that happen to compose for SPC — but each can be used without it.

This recursive design process is intentional. When SPC or GMM needs something new, the question is always: _should this live in the application layer, or does it belong in a primitive that a future consumer could use independently?_ The answer shapes where code lands and how it is namespaced. The goal is a coherent library, not a collection of SPC-specific utilities.

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

## Where things are

| Path                              | What                                                                                       |
| --------------------------------- | ------------------------------------------------------------------------------------------ |
| `projects/`                       | SDK-style project definitions; build configuration lives outside `src/`                    |
| `projects/LinearAlgebra/`         | Standalone Cholesky decomposition primitive (no deps)                                      |
| `projects/GaussianMixture/`       | GMM: EM fitting, sampling, density, Mahal; depends on LinearAlgebra + DistanceMetrics      |
| `projects/SpcCore/`               | SPC run contract, graph binding, checkpointing, and Potts runtime project                  |
| `projects/SpcThermo/`             | Thermodynamic/information analysis project over materialized SPC state                     |
| `projects/SpcSynthetic/`          | Synthetic dataset to SPC request adapter project                                           |
| `projects/Hashish/`               | Standalone text/sketching/hash primitive library project                                   |
| `projects/DistanceMetrics/`       | Standalone metric primitive library project                                                |
| `projects/ProximityGraphs/`       | Standalone proximity graph primitive library project                                       |
| `projects/CouplingKernels/`       | Standalone distance-to-coupling kernel primitive library project                           |
| `projects/StatisticalEstimators/` | Standalone estimator primitive library project                                             |
| `projects/SyntheticDatasets/`     | Standalone synthetic dataset primitive library project                                     |
| `Directory.Build.props`           | Shared SDK settings; .NET 10 LTS target and root `artifacts/` output baseline              |
| `artifacts/bin/`                  | Build output for all projects; generated by `dotnet build`, not source                     |
| `artifacts/obj/`                  | Intermediate build objects; generated by `dotnet build`, not source                        |
| `src/linalg/`                     | `LinearAlgebra` source: `CholeskyDecomposition`                                            |
| `src/gmm/`                        | `GaussianMixture` source: `GaussianComponent`, `GaussianMixtureModel`, `ISpcShatterOracle` |
| `src/spc.batch.cs`                | DTOs, SpcCheckpoint carrier, public SpcBatch.Run orchestration                             |
| `src/spc.graph.cs`                | Edge/CsrGraph topology, graph initialization, connectivity diagnostics                     |
| `src/spc.potts.cs`                | PottsModel, SimulationResult struct, FastUnionFind, Swendsen-Wang loop                     |
| `src/spc.thermo.cs`               | SpcAnalysis base: histograms, medoids, BFS shells, peak detection, purity scoring          |
| `src/spc.synthetic.cs`            | SpcSynthetic adapter from `SyntheticDatasets` DTOs to `SpcBatchRequest`                    |
| `src/spc.checkpoint.cs`           | Manifest-backed SPC state/checkpoint persistence and handoff/GMM artifact DTOs             |
| `src/metrics/`                    | `DistanceMetrics` static pairwise metric primitives                                        |
| `src/graphs/`                     | `ProximityGraphs` rules: Knn, MutualKnn, EpsilonBall, MstAugmented                         |
| `src/kernels/`                    | `CouplingKernels` primitives: Gaussian, Cauchy, Laplacian, Linear                          |
| `src/estimators/`                 | `StatisticalEstimators` source files: delta + weighted location estimators                 |
| `src/hashish/`                    | Text, similarity, sketching, and compression primitive source files                        |
| `src/spc-thermo/`                 | Thermodynamic / information analysis partials                                              |
| `src/synthetic/`                  | `SyntheticDatasets` ground-truth dataset generators                                        |
| `.discussion/`                    | Thread archives and research notes; source material, not live code                         |
| `README.md`                       | Public-facing current project description and build commands                               |
| `changelog.md`                    | Historical record of code and architecture changes                                         |
| `TODO.md`                         | Informal backlog / scratch planning, not an authority                                      |

## Working style

**Convention/contract-based design with fail-fast.** Don't add verbose defensive checks where the system design guarantees correctness. Prefer typed contracts over stringly-typed flexibility.

Use the current source tree, `README.md`, `changelog.md`, and the user's latest request as live context. Treat `TODO.md` as informal backlog signal only, not as an authority.

**The user collects feedback from multiple assistants** (Gemini, Perplexity, Nemotron, GPT, Grok, etc.) and brings it in as `.discussion/` files. Treat these as information to consider, not directives. The user makes final calls on design; the assistant's job is to engage critically, not rubber-stamp.

**C#-first seam discipline:** standalone computational elements should be static classes or small typed primitives in C# projects. Namespace primitives by what they are (`DistanceMetrics`, `ProximityGraphs`, `CouplingKernels`, `StatisticalEstimators`, `SyntheticDatasets`, `Hashish`), and namespace application layers by what they do (`SpcCore`). Higher layers dispatch primitives for SPC, GMM handoff, analysis, or harness applications. PowerShell remains useful for orchestration/UX, but new compute should not be tied to PowerShell or `SpcBatch` when it can stand alone.

## Key conventions

- Gaussian coupling $J = \exp(-d^2 / 2\Delta^2)$ is the production physics model. Linear J is legacy/fast mode.
- `SpcBatchRequest`/`SpcBatchResult` are sealed classes (mutable DTOs). Internal returns like `SimulationResult` are readonly structs.
- Current SPC dispatch axes on `SpcBatchRequest`: `SpcMetric`, `SpcProximity`, `DeltaEstimator`, and `CouplingKernel`. Metric implementations under `src/metrics/`, graph selectors under `src/graphs/`, coupling kernels under `src/kernels/`, estimators under `src/estimators/`, synthetic generators under `src/synthetic/`, and Hashish primitives under `src/hashish/` should stay independent of `SpcBatch`; `src/spc.graph.cs` and the SPC dispatcher bind primitives to SPC graph initialization. `src/spc.synthetic.cs` is deliberately outside `SpcCore`, compiled by `SpcSynthetic`, and bridges `SyntheticDatasets` to `SpcBatchRequest` for harnesses.
- `SpcBatchResult.Warnings` surfaces graph validation diagnostics (disconnected components, isolated nodes, coverage) to the PS layer.
- `GaussianMixture` compiles `src/gmm/` under namespace `StatisticalEstimators` (assembly name `GaussianMixture`). Depends on `LinearAlgebra` and `DistanceMetrics`. Public surface: `Fit`, `Predict`, `PredictProba`, `Pdf`, `Mahal`, `Sample`, `NumIterations`, `IsConverged`, `FinalLogLikelihood`.
- `LinearAlgebra` compiles `src/linalg/` as a standalone primitive with no dependencies. Current content: `CholeskyDecomposition` — single-allocation Σ = LLᵀ decomposition with `WriteInverseTo`, `LogDet`, and `Sample`.
- `Mahalanobis.DistanceSquared` (`src/metrics/Mahalanobis.cs`) returns the raw quadratic form D² = (a−b)ᵀΣ⁻¹(a−b) without the sqrt; `Distance` wraps it. Prefer `DistanceSquared` when the sqrt is not needed (e.g. log-pdf, Mahal surface).
- SDK-style C# projects live under `projects/<ProjectName>/`, target `net10.0` through `Directory.Build.props`, and explicitly include source from `src/`. Keep `bin/` and `obj/` out of `src/`; generated output belongs under root `artifacts/`. Treat `Add-Type` assumptions as legacy transition context, not the architectural default.
- Checkpoints live in `src/spc.checkpoint.cs` and are now the run-state substrate, not only crash recovery. `SpcBatchRequest.CheckpointDirectory` is a working root; each run writes under `{root}/{yyyyMMdd_HHmmss}/` with payload-type folders (`temperature-checkpoints/`, `temperature-observations/`, `handoff-readiness/`, `gmm-handoff-state/`, etc.) plus `spc_{runStamp}.manifest.json` at the run root. Disk runs default to greedy non-redundant persistence: small JSON checkpoint summaries and compressed binary `.bin.br` spin delta-frame artifacts. `SpcBatchRequest.CheckpointPersistence` configures which artifacts are written; `EpochSweepCount` is run-start configuration and emits partial temperature checkpoints for round-robin/supervisor duty cycles. Partial temperature states use `IsComplete=false`, `CurrentSpinsArtifactId`, `EpochCount`, and `SweepCount`; only complete states populate the resume skip path. Checkpoints do not persist susceptibility or other thermodynamic analysis values. Replay ordered `TemperatureObservation` artifacts to materialize spins; supervisor-side thermodynamic checks live in `src/spc-thermo/chi.cs` via helpers such as `SpcAnalysis.AnalyzeCheckpointSusceptibility`. The manifest tracks relative path, source/previous/base artifact links, payload type, compression, encoding, sequence, byte length, and pipeline stage.

## What not to do

- Don't add features beyond what's in the current phase without discussing first.
- Don't suggest ValueTuples for PS-facing returns (bad interop: `.Item1`).
- Don't treat `.discussion/` thread suggestions as committed decisions — they're input, not plan.
- Don't introduce a new process ledger unless the user explicitly asks for one.
