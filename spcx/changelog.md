# Changelog

## 2026-05-07 - VizCore: JSON Export Renderer, Naive Gaussian Overlay, CrescentEllipsoid v2 Fixes

### Added

- **`JsonExportRenderTarget`** (`src/viz-core/serializer.cs`, namespace `Viz.Renderers`): New `IRenderTarget` implementation that serialises a `ScenePackage` to a schema-versioned JSON snapshot (`schema_version: 1`). All layer arrays are flat row-major with explicit `*_shape` fields. Enums are serialised as strings via `JsonStringEnumConverter`. `[JsonIgnoreCondition.WhenWritingNull]` keeps absent layers out of the output. Constructor: `JsonExportRenderTarget(bool compact = false)` — selects between indented (dev/readable) and compact (automation/diffing) output. Both `JsonSerializerOptions` instances are static; no per-call allocation. Registered in `projects/VizCore/VizCore.csproj`.
- **Naive Gaussian overlay in `SyntheticDatasetAdapter`** (`src/viz-core/adapter.cs`): `BuildNaiveGaussianLayer` method added. For each `ArcGeometry` cluster that has a non-null `ClusterCovariances[i]` entry (populated by `CrescentEllipsoid.BuildCrescentApproxCovariance`), emits a `GaussianLayer` named `"Naive Gaussian (misleading)"` — the diagonal Gaussian a naive single-component fit would report for a crescent cluster. Intended to be rendered alongside the analytic ground-truth ellipsoid to visualise the misleading shape. Helpers added: `ComputeClusterMean` (iterates labels, averages matching feature rows), `ComputeClusterWeight` (fraction of points in cluster).

### Fixed

- **`CrescentEllipsoid` v2** (`src/synthetic/CrescentEllipsoid.cs`): Removed dead `scaleVec` block — the ternary `ellipsoidAxes[0] * gapScale == 0 ? ellipsoidAxes[0] : ellipsoidAxes[0]` always evaluated to the same value and the result was never passed to `BuildCovariance`.
- **`CrescentEllipsoid` v2 nullable annotations** (`src/synthetic/CrescentEllipsoid.cs`): `double[] ellipsoidAxes = null`, `double[] ellipsoidCenter = null`, and `double[] ellipsoidEulerXYZ = null` corrected to `double[]?` to satisfy NRT analysis; eliminated three nullable-related warnings.

### Notes

- The naive Gaussian layer is opt-in via `CrescentEllipsoid` — it requires `BuildCrescentApproxCovariance` to have been called on the generator and its result stored in `SyntheticDataset.ClusterCovariances[clusterIdx]`. The adapter produces the layer only when that slot is non-null.
- `JsonExportRenderTarget` is the intended handoff format between the offline C# pipeline and the browser client once a static artifact contract is defined (see `visualization-engine.md` §6.1 and `viz-core-architecture.md`).

---

## 2026-05-06 - VizCore: Layer Model, Scene Pipeline, and Three.js Renderer (Pass 1)

### Added

- **`INamedLayer` interface** (`src/viz-core/viz_core.cs`): Shared contract exposing `string Name { get; }` on all layer types. Enables type-safe generic filtering in `SceneBuilder.Filter<T where T : INamedLayer>` without `dynamic` dispatch. Applied to `LabelLayer`, `ScalarLayer`, `EdgeLayer`, `GaussianLayer`, and `SpineLayer`. `TemporalLabelSequence` is intentionally excluded — it is resolved once by direct name match rather than filtered into a collection.
- **`PointCloud`** (`src/viz-core/viz_core.cs`): Invariant N×D point set (row-major `ReadOnlyMemory<double>`) with optional human label. Constructor completed.
- **`LabelLayer`** (`src/viz-core/viz_core.cs`): Per-point integer label array with `LabelLayerKind` discriminator (`GroundTruth`, `SpinColor`, `EquilibriumCluster`, `GmmComponent`, `GmmCluster`, `Custom`). Constructor completed.
- **`ScalarLayer`** (`src/viz-core/viz_core.cs`): Per-point double annotation (coherence, Mahalanobis, percolation arrival, responsibility, log-likelihood). Constructor added.
- **`EdgeLayer`** (`src/viz-core/viz_core.cs`): Named sparse graph (src/dst/weight arrays) with optional `EdgeClusterSrc`/`EdgeClusterDst` GT annotations for false-bridge highlighting. Constructor added.
- **`GaussianLayer`** (`src/viz-core/viz_core.cs`): K×D means, K×D×D covariances, K weights, optional `ComponentToClusterMap` for topology-aware GMM. Constructor added.
- **`SpineLayer`** (`src/viz-core/viz_core.cs`): New layer type. Carries the clean generating curve/manifold (M×D `double[][]`) for a synthetic cluster as a named overlay, independent of the N-point cloud. Has `ClusterIdx`, `SpineLayerKind { Arc, Manifold }`, and optional `TangentBases`. Separate from `ScalarLayer` because it is a different-sized point set.
- **`TemporalLabelSequence`** (`src/viz-core/viz_core.cs`): Named ordered list of `LabelLayer` frames along a `TemporalAxis` (Temperature, Iteration, Depth, Custom). Constructor added.
- **`VizDataset`** (`src/viz-core/viz_core.cs`): Full seven-argument constructor added; `SpineLayers` property added as the sixth collection alongside labels, scalars, edges, Gaussians, and temporal sequences.
- **`SceneDescriptor`** (`src/viz-core/scene_renderer.cs`): Named rendering configuration — per-layer-type active-name lists (null = all), active temporal sequence name, frame index, and `SceneRenderHints`.
- **`SceneRenderHints`** (`src/viz-core/scene_renderer.cs`): Immutable render toggle flags: `ShowEdgeWeightsAsOpacity`, `ShowGaussianEllipsoids`, `ShowSpineOverlays`, `ShowTangentBases`, `OverlayComponentAndClusterColoring`, `AnnotateSpinColorVsEquilibrium`, `HighlightFalseBridges`.
- **`ScenePackage`** (`src/viz-core/scene_renderer.cs`): Backend-agnostic intermediate with resolved active layer lists and render hints. Full nine-argument constructor.
- **`SceneBuilder.Build`** (`src/viz-core/scene_renderer.cs`): Resolves a `SceneDescriptor` against a `VizDataset` — filters each layer collection by active-name set (or passes all through when null), resolves the active `TemporalLabelSequence` and frame index, returns a `ScenePackage`.
- **`SceneBuilder.Filter<T>`** (`src/viz-core/scene_renderer.cs`): Type-safe `where T : INamedLayer` generic; replaced the previous `dynamic` dispatch pattern.
- **`IRenderTarget`** (`src/viz-core/scene_renderer.cs`): `void Render(ScenePackage scene, Stream output)` — backend contract.
- **`ThreeJsHtmlRenderTarget`** (`src/viz-core/html_render_target.cs`): Self-contained single-file HTML renderer. Inlines point positions (Float32 downcast) and per-point colors as JSON. Uses Three.js r160 via an importmap + `<script type="module">` pattern (no bundler required, works in all modern browsers). Pass 1 implements: OrbitControls orbit/pan/zoom, auto-fit to bounding sphere, 12-color qualitative palette with deterministic overflow hash for K > 12, cluster legend with counts, `depthWrite: false` on point material for future overlay compatibility.
- **`IVizDatasetAdapter<TSource>`** (`src/viz-core/adapter.cs`): Generic adapter interface.
- **`SyntheticDatasetAdapter`** (`src/viz-core/adapter.cs`): Adapts a `SyntheticDataset` into a `VizDataset`. Produces: `PointCloud`, `LabelLayer[GroundTruth]`, `GaussianLayer[GT Ellipsoids]` from `EllipsoidGeometry` entries (analytic covariances, empirical weights), and `SpineLayer` entries from `ArcGeometry` and `ManifoldGeometry`. Does not produce `EdgeLayer` — metric and proximity rule choices belong to the diagnostic harness.
- **`VizCore` project** (`projects/VizCore/VizCore.csproj`): Class library compiling all four `src/viz-core/*.cs` files; references `SyntheticDatasets`.
- **`VizCoreSmoke` project** (`projects/VizCoreSmoke/VizCoreSmoke.csproj`, `Program.cs`): Executable smoke test wiring the full pipeline: `GenerateCrescentAndEllipsoid` → `SyntheticDatasetAdapter` → `SceneBuilder` → `ThreeJsHtmlRenderTarget` → `~/viz-smoke.html`.
- Both projects registered in `ps.core.pwshspc.sln`.

### Fixed

- **Three.js CDN**: Downgrade to r128 (which had UMD controls) was reverted. The correct fix is the importmap + ES module pattern targeting r160 — `OrbitControls` is now imported as a named ES module rather than accessed via `THREE.OrbitControls` on a global.
- **`SceneBuilder.Filter<T>`**: Replaced `(item as dynamic).Name` with `item.Name` after adding `INamedLayer`; eliminates a runtime binding failure under AOT-unfriendly configurations.
- **`VizDataset` constructor**: Was absent in initial draft; added. `SceneBuilder.Build` was calling `dataset.SpineLayers` which did not exist.

### Notes

- `TemporalLabelSequence` does not implement `INamedLayer` by design — it participates in a direct name-match lookup, not the typed filter contract.
- `GaussianLayer` for the crescent cluster is intentionally absent from `SyntheticDatasetAdapter` output — a single Gaussian cannot faithfully represent a crescent. Callers wanting a fitted ellipsoid for diagnostic comparison should add it separately.
- Pass 2 of the renderer (edge lines, Gaussian ellipsoid meshes, spine curve overlays, temporal scrubbing) is deferred pending a `ThreeJsHtmlRenderTarget` extension pass.

---

## 2026-05-03 - LinearAlgebra Primitive Extraction and GMM API Completion

### Added

- **`CholeskyDecomposition`** (`src/linalg/CholeskyDecomposition.cs`, namespace `LinearAlgebra`): Standalone reusable Cholesky primitive. Single allocation per component; `Decompose` recomputes L, L⁻¹, `LogDet`, and Σ⁻¹ in-place without heap allocation. Exposes `WriteInverseTo(target)` and `Sample(rng, mean)` (Box-Muller, uses L). Includes a 1e-12 diagonal floor to guard near-singular matrices.
- **`LinearAlgebra` project** (`projects/LinearAlgebra/LinearAlgebra.csproj`): Standalone project, no dependencies. Globs `src/linalg/*.cs`.
- **`GaussianMixture` project** (`projects/GaussianMixture/GaussianMixture.csproj`): Compiles `src/gmm/*.cs` under assembly `GaussianMixture`, namespace `StatisticalEstimators`. References `LinearAlgebra` and `DistanceMetrics`. Both projects added to `ps.core.pwshspc.sln` and nested under the `projects` solution folder.
- **`GaussianMixtureModel.RandomInitialize`** (`src/gmm/GaussianMixtureModel.cs`): Cold-start fallback called automatically from `FitCore` when `_isInitialized` is false. Partial Fisher-Yates picks K distinct rows as initial means; initial covariance is diagonal with Bessel-corrected per-dimension sample variance plus a 1e-6 floor; weights are uniform. Equivalent to MATLAB `fitgmdist(..., 'Start', 'randSample')`.
- **`GaussianMixtureModel.NumIterations`** (`src/gmm/GaussianMixtureModel.cs`): Tracks actual EM iterations performed per `Fit` call.
- **`GaussianMixtureModel.Pdf`** (`src/gmm/GaussianMixtureModel.cs`): Returns `double[]` of mixture density values Y[i] = Σ_k π_k · N(x_i | μ_k, Σ_k). Analogous to MATLAB `pdf(obj, X)`.
- **`GaussianMixtureModel.Mahal`** (`src/gmm/GaussianMixtureModel.cs`): Returns `double[n, K]` of squared Mahalanobis distances. Analogous to MATLAB `mahal(obj, X)`.
- **`GaussianMixtureModel.Sample`** (`src/gmm/GaussianMixtureModel.cs`): Draws n samples from the mixture. Multinomial component draw via weight CDF; each point drawn via `GaussianComponent.Sample`. Optional `componentIndices` out array. Analogous to MATLAB `random(obj, N)`.
- **`GaussianComponent.MahalanobisSquared`** (`src/gmm/GaussianComponent.cs`): Public surface for per-component D² = (x−μ)ᵀ Σ⁻¹ (x−μ); delegates to `Mahalanobis.DistanceSquared`.
- **`GaussianComponent.Sample`** (`src/gmm/GaussianComponent.cs`): Delegates to `CholeskyDecomposition.Sample`.

### Changed

- **`GaussianComponent` Cholesky ownership** (`src/gmm/GaussianComponent.cs`): Private `_choleskyL` / `_choleskyLInv` arrays removed. Component now holds a `CholeskyDecomposition _chol` instance. `UpdateCache` delegates decomposition, inverse, and `LogDet` to it. Docstring updated to remove stale Cholesky field reference.
- **`Mahalanobis.Distance` refactor** (`src/metrics/Mahalanobis.cs`): Inner kernel renamed from `MahalanobisCore` to `QuadraticFormCore`; returns the raw quadratic form (no sqrt). `Distance` wraps it with `Math.Sqrt`. New `DistanceSquared` public overload returns D² directly — avoids the sqrt when squared distance is sufficient (e.g. log-pdf evaluation, Mahal surface). Dispatch logic extracted to `DispatchQuadraticForm` for reuse by both public methods.

### Notes

- GMM project registration resolves the orphaned `src/gmm/` source files that were previously unlinked from any `.csproj`.
- `GaussianMixture.csproj` uses namespace `StatisticalEstimators` to preserve compatibility with existing call sites; assembly name is `GaussianMixture`.
- Deferred: diagonal covariance mode, `SharedCovariance`, configurable regularisation value, multiple replicates (`'Replicates'` style), AIC/BIC, CDF.

### Added

- **SPC core project** (`projects/SpcCore/SpcCore.csproj`): Compiles `src/spc.batch.cs`, `src/spc.checkpoint.cs`, `src/spc.graph.cs`, and `src/spc.potts.cs` as the runtime/checkpoint layer.
- **SPC thermo project** (`projects/SpcThermo/SpcThermo.csproj`): Compiles `src/spc.thermo.cs` plus `src/spc-thermo/*.cs` as the thermodynamic/information analysis layer over materialized SPC state.
- **SPC synthetic adapter project** (`projects/SpcSynthetic/SpcSynthetic.csproj`): Compiles `src/spc.synthetic.cs` separately from core, depending on both `SpcCore` and `SyntheticDatasets`.

### Changed

- **Solution membership** (`ps.core.pwshspc.sln`): Added `SpcCore`, `SpcThermo`, and `SpcSynthetic` so the runtime and analysis files are now build-validated by the solution.
- **Core/analysis boundary** (`src/spc.batch.cs`): `SpcCore` no longer calls `SpcAnalysis`; thermodynamic quantities are derived by `SpcThermo` consumers from `FinalSpins` or checkpoint history.

---

## 2026-05-01 - Thermodynamic Analysis Boundary Cleanup

### Changed

- **Potts simulation** (`src/spc.potts.cs`): Removed susceptibility accumulation from the Swendsen-Wang sweep loop. `PottsModel` now reports spin state plus lifecycle coordinates (`SweepCount`, `EpochCount`, `IsComplete`) and leaves thermodynamic interpretation to analysis code.
- **Chi analysis** (`src/spc-thermo/chi.cs`): Added susceptibility helpers over materialized spin states plus checkpoint-history analysis for supervisor-style monitoring of epoch frames.
- **Checkpoint/state layer** (`src/spc.checkpoint.cs`): Removed susceptibility fields from temperature checkpoint state. Checkpoints now persist execution state and artifact links; thermodynamic quantities are derived by analysis consumers.
- **SPC result shape** (`src/spc.batch.cs`): Removed `SpcBatchResult.Susceptibility`; callers should use `SpcThermo`/`SpcAnalysis` helpers to derive chi values from `FinalSpins` or checkpoint history.

---

## 2026-05-01 - SPC Core File-Boundary Cleanup

### Changed

- **SPC run contract and orchestration** (`src/spc.batch.cs`): Consolidated request/result/checkpoint DTOs, public `SpcBatch.Run`, and checkpoint-aware temperature sweep orchestration into one file.
- **SPC graph layer** (`src/spc.graph.cs`): Consolidated graph runtime topology (`Edge`, `CsrGraph`), metric/proximity/kernel graph initialization, and connectivity diagnostics into one file.
- **Potts/SW helper** (`src/spc.potts.cs`): Merged `FastUnionFind` into the Swendsen-Wang file while preserving path compression, union-by-size, `GetLabels()`, and `Reset()` reuse semantics.

---

## 2026-05-01 - Manifest-Backed SPC State Persistence

### Changed

- **Checkpoint/state layer** (`src/spc.checkpoint.cs`): Replaced the plain three-line temperature checkpoint writer with a manifest-backed state ledger. `CheckpointDirectory` is now a working root; each run writes under `{root}/{runStamp}/` with `spc_{runStamp}.manifest.json` at the run root and payload-type subdirectories for JSON summaries, spin observations, handoff artifacts, and future GMM artifacts. Legacy three-line `.ckpt` files remain readable on resume.
- **SPC batch wiring** (`src/spc.batch.cs`): Disk checkpointing now initializes a run manifest, records `StateManifestPath` on `SpcBatchResult`, and writes sweep count, final/current spins, and cluster count per completed temperature.
- **Epoch checkpointing** (`src/spc.batch.cs`, `src/spc.potts.cs`, `src/spc.checkpoint.cs`): Added optional `CheckpointPersistence.EpochSweepCount` support. Partial temperature epochs now write non-complete summaries and additive spin frames with `EpochCount` and `SweepCount`; resume warm-starts from incomplete temperature state and only skips temperatures marked complete.
- **State model** (`src/spc.checkpoint.cs`): Added typed artifact metadata for SPC, handoff, and GMM stages, including payload type, relative path, source/previous/base artifact links, container, compression, encoding, sequence number, incremental flag, byte length, and DTOs for handoff readiness snapshots and flat-array `GmmHandoffState` persistence.
- **Configurable persistence** (`src/spc.batch.cs`, `src/spc.checkpoint.cs`): Added `SpcBatchRequest.CheckpointPersistence` with greedy non-redundant defaults: JSON summaries plus compressed spin delta observations, with opt-ins for embedded summary spins, alternate compression, or snapshot-encoded spin observations.
- **Incremental replay** (`src/spc.checkpoint.cs`): Added compressed streaming read/write machinery for binary spin delta frames plus public read APIs for the manifest, observation artifact list, materialized spin history, and latest materialized spins.

### Notes

- The current terminal-temperature run path still behaves as pause/resume. The delta-frame schema is ready for the planned round-robin `RunEpochs` supervisor mode, where each epoch is one duty cycle of SPC work for a temperature and emits only the changed spin positions for that cycle.

---

## 2026-05-01 - Primitive Namespace Decoupling

### Changed

- **Metric primitives** (`src/metrics/`, `projects/DistanceMetrics/DistanceMetrics.csproj`): Renamed the standalone metric library from the SPC-branded project/namespace to `DistanceMetrics`.
- **Proximity graph primitives** (`src/graphs/`, `projects/ProximityGraphs/ProximityGraphs.csproj`): Extracted KNN, Mutual KNN, Epsilon-ball, and MST-augmented selection from `SpcBatch` partial methods into the standalone `ProximityGraphs` primitive library.
- **Coupling kernel primitives** (`src/kernels/`, `projects/CouplingKernels/CouplingKernels.csproj`): Extracted Gaussian, Cauchy, Laplacian, and Linear distance-to-coupling functions from `SpcBatch` into the standalone `CouplingKernels` primitive library.
- **Estimator primitives** (`src/estimators/`, `projects/StatisticalEstimators/StatisticalEstimators.csproj`): Renamed reusable delta/location estimators from the SPC-branded project/namespace to `StatisticalEstimators`.
- **Synthetic dataset primitives** (`src/synthetic/`, `projects/SyntheticDatasets/SyntheticDatasets.csproj`): Moved reusable labeled dataset DTOs, sampling helpers, geometry helpers, and generators into the standalone `SyntheticDatasets` primitive library.
- **SPC binding layer** (`src/spc.batch.cs`, `src/spc.graph.cs`): Kept SPC DTOs and dispatch in `SpcCore` while importing primitive namespaces at the binding points.
- **Project primer** (`.copilot/primer.md`, `README.md`): Codified the discipline that primitive namespaces are named by what they are, while application layers are named by what they do.

---

## 2026-05-01 - Static Metric Primitives and Graph Binding

### Changed

- **Metric architecture** (`src/metrics/`): Converted metric files from `SpcBatch` partial graph builders into standalone static pairwise-distance primitives such as `Euclidean.Distance`, `JensenShannon.Distance`, `Mahalanobis.Distance`, and `Poincare.Distance`.
- **SPC graph initialization** (`src/spc.graph.cs`, `src/spc.batch.cs`): Added a binding layer that selects the metric primitive, passes it to the chosen proximity rule, then converts neighbor distances through the selected coupling kernel.
- **Metric project** (`projects/SpcCore.Metrics/SpcCore.Metrics.csproj`, `ps.core.pwshspc.sln`): Added a standalone metric primitive library project and included it in the solution.
- **Documentation** (`README.md`, `.copilot/primer.md`, `TODO.md`): Codified the metric/proximity/coupling split and removed references to metrics as KNN builders.

---

## 2026-05-01 - Project Definition and Artifact Layout

### Changed

- **Project placement** (`projects/Hashish/Hashish.csproj`, `projects/SpcCore.Estimators/SpcCore.Estimators.csproj`): Moved SDK-style project files out of `src/` so source directories stay source-only and build configuration lives in a dedicated project-definition area.
- **Artifact placement** (`Directory.Build.props`, `.gitignore`): Redirected `bin/` and `obj/` output to root `artifacts/bin/` and `artifacts/obj/`, then ignored generated artifacts at the repo root.
- **Documentation** (`README.md`, `.copilot/primer.md`, `TODO.md`): Codified the convention that `src/` contains source files, `projects/` contains project definitions, and generated outputs belong under `artifacts/`.

---

## 2026-05-01 - .NET 10 Baseline and Estimator Primitive Library

### Changed

- **Build baseline** (`Directory.Build.props`, `projects/Hashish/Hashish.csproj`, `projects/SpcCore.Estimators/SpcCore.Estimators.csproj`): Set the repo-wide target framework to `net10.0` for .NET 10 LTS and moved shared SDK settings into `Directory.Build.props` so new projects do not silently drift back to older targets.
- **Hashish project scope** (`projects/Hashish/Hashish.csproj`): Disabled default compile item globbing and explicitly included only `src/hashish/*.cs` so the standalone Hashish library no longer attempts to compile the whole `src/` tree.
- **Estimator architecture** (`src/estimators/`): Reframed the folder as an application-agnostic estimator primitive layer rather than a delta-only partial-class directory. Delta estimators now live beside weighted location estimators and are intended to be dispatched by higher-level SPC/GMM/analysis layers.
- **Project documentation** (`README.md`, `.copilot/primer.md`, `TODO.md`): Updated platform language, build commands, source layout, and architectural guidance to reflect .NET 10 LTS, C#-first primitive libraries, and the expanded estimators folder.

### Added

- **`SpcCore.Estimators` project** (`projects/SpcCore.Estimators/SpcCore.Estimators.csproj`): Standalone estimator library for scalar delta summaries and indexed weighted location estimators.

---

## 2026-04-30 - Source Filename Cleanup

### Changed

- **Shortened source filenames** (`src/kernels/`, `src/graphs/`, `src/estimators/`, `src/synthetic/`, `src/spc-thermo/`): Removed the `SpcBatch.` prefix from graph / estimator partials, shortened kernel partials again from `Kernel*.cs` to bare metric-family names, removed the `SyntheticData.` prefix from synthetic generator partials, and shortened the thermodynamic analysis files to `chi.cs`, `kl.cs`, and `mhbs.cs` under `src/spc-thermo/`.
- **Documentation path sync** (`README.md`, `changelog.md`, `src/spc.thermo.cs`): Updated file inventory and path references to the shortened filenames and the current `graphs/`, `estimators/`, and `spc-thermo/` directory names.

---

## 2026-04-30 - Hashish Primitive Substrate

### Added

- **`TokenizerPreprocessing`** (`src/hashish/tokenizer.cs`): Shared Unicode normalization, case-folding, and word tokenization for future Hashish primitives.
- **`WordShingler`** (`src/hashish/shingler.cs`): Word-level n-gram shingles, including ordered output and deduplicated set output.
- **`JaccardContainment`** (`src/hashish/jaccard.cs`): Exact Jaccard similarity/distance, asymmetric containment, overlap coefficient, and word-shingle containment helper.
- **`Hashish.csproj`** (`src/hashish/Hashish.csproj`): Standalone SDK-style project for compiling the Hashish primitive library independently from the broader SPC reshaping work.
- **`MinHashLshIndex`** (`src/hashish/minhash.cs`): Band/row locality-sensitive candidate index now lives alongside `MinHash` in one coherent source/API surface.
- **`InverseDocumentFrequency` / `IdfModel`** (`src/hashish/idf.cs`): Reusable document-frequency and IDF statistics with smooth, Robertson-Sparck Jones, and plain formulas.
- **`BloomFilter`** (`src/hashish/bloom.cs`): Approximate membership filter with expected-item / false-positive-rate constructor.
- **`CountMin`** (`src/hashish/countmin.cs`): Streaming approximate frequency estimator with epsilon/delta constructor.
- **`HyperLogLog`** (`src/hashish/hyperloglog.cs`): Approximate distinct-count estimator with merge support.
- **`NormalizedCompressionDistance`** (`src/hashish/ncd.cs`): Standalone pairwise byte/text NCD primitive, independent from the SPC batch graph-builder NCD path.
- **`SeededHash`** (`src/hashish/seeded.cs`): Internal seeded FNV/mix helper shared by LSH and sketch structures.

### Updated (Documentation)

- **Hashish README section**: Expanded `hashish/` from text fingerprinting into the broader text/similarity/sketching/compression primitive layer.

### Validation

- **Standalone build**: `dotnet build src/hashish/Hashish.csproj` now succeeds cleanly and produces `Hashish.dll` as an independently compilable primitive library.

### Notes

- **MinHash XML docs**: Disambiguated the `Compute` cref in `minhash.cs` so the standalone build no longer emits the prior XML-doc warning.

---

## 2026-04-20 - HPC Phase B: Metric Vectorization + Code Cleanup

### Changed

- **`JensenShannonDistance`** (`JensenShannon.cs`): Fully vectorised with `TensorPrimitives` (`Add`, `Multiply`, `Divide`, `Max`, `Log`, `Sum`). Replaced scalar loop with branch guards (`if p[i] > 1e-15`) with element-wise `Max(ratio, 1e-15)` clamp before `Log` — mathematically equivalent, SIMD-compatible. Added three-tier scratch buffer strategy (stackalloc ≤64 / ThreadLocal ≤512 / ArrayPool > 512) to avoid O(N²) allocations during graph construction. Requires `using System.Buffers` and `using System.Numerics.Tensors`.
- **`FisherRaoDistance`** (`FisherRao.cs`): Fully vectorised with `TensorPrimitives` (`Multiply`, `Max`, `Sqrt`, `Sum`). Replaced scalar loop with branch guards (`if p[i] > 0 && q[i] > 0`) with element-wise `Max(buf, 0)` clamp before `Sqrt` — mathematically equivalent for proper probability inputs, SIMD-compatible. Added three-tier scratch buffer strategy (stackalloc ≤64 / ThreadLocal ≤512 / ArrayPool > 512). Requires `using System.Buffers` and `using System.Numerics.Tensors`.
- **`Wasserstein1D`** (`Wasserstein.cs`): Replaced O(N²) `(double[])a.Clone()` allocations during graph construction with ArrayPool rent/return pattern. Sort scratch is now acquired per-call from `ArrayPool<double>.Shared` and returned after `Array.Sort`. Reduces GC pressure on medium-large datasets. Added `using System.Buffers`.
- **`mhbs.cs`** (`thermo/`): Clarifying comment added explaining the file as a frozen Mahalanobis pillar in the thermodynamic analysis framework. Development paused pending SGLD/RunEpochs architectural decisions and broader analysis-layer shape finalization. Remains a stub (comment-only placeholder).

### Removed

- **`ComputePurity` from `SyntheticData`** (`spc.synthetic.cs`): Duplicate method removed. Canonical implementation retained in `SpcAnalysis.ComputePurity` (`spc.thermo.cs`). Scoring is an analysis concern, not a data-generation artifact. All call sites already use the `SpcAnalysis` version (no code migrations needed).

### Added

- **`ToSpcBatchRequest` extension method** (`SyntheticData`, `spc.synthetic.cs`): Bridge adapter from generated datasets to the `SpcBatchRequest` contract. Wires `SyntheticDataset.Features` through and accepts sweep configuration parameters (metric, K, temperatures, steps, proximity, kernel, estimator, etc.). Returns a ready-to-use request object. Mahalanobis covariance pooling deferred — callers must supply `CovarianceInverse` separately if needed. Avoids manual plumbing for test harnesses.

### Updated (Documentation)

- **Class docstring** (`SyntheticData`, `spc.synthetic.cs`): Added "Adapter" entry noting `ToSpcBatchRequest` as a shared primitive. Removed "Scoring — ComputePurity" from the shared-primitives list (moved to analysis layer).

### Invariants Preserved

- All public signatures unchanged (new `ToSpcBatchRequest` is an extension, additive only).
- `SpcAnalysis.ComputePurity` signature and semantics unchanged.
- Mahalanobis metric (pairwise distance form in `metrics/Mahalanobis.cs`) untouched.

### Notes

- **Pre-csproj, unverified APIs**: JSD and FisherRao vectorization assume `TensorPrimitives.Max<T>(ReadOnlySpan<T>, T, Span<T>)` (scalar overload) and `Multiply<T>(ReadOnlySpan<T>, T, Span<T>)` (scalar overload) exist in .NET 9+. Both should exist per platform docs; confirmation comes when csproj lands. If missing, fallback is manual per-element loops in those call sites.
- **Behavioral equivalence**: JSD and FisherRao branch-guard removal (`if p[i] > 1e-15` / `if q[i] > 0`) replaced with `Max(..., threshold)` clamps. Mathematically equivalent for valid probability inputs (non-negative, bounded); numerically safer against FP noise. Existing code relied on guard semantics; vectorised code relies on clamp semantics. Both produce the same output on well-formed data.

---

## 2026-04-17 - Batch A HPC: TensorPrimitives + Parallel Graph Construction

### Changed

- **`bondProb` init** (`spc.potts.cs`): Replaced scalar `Math.Exp` loop with vectorised `TensorPrimitives` sequence: `Multiply` (scale by -1/T) → `Exp` → `Negate` → `Add 1.0`. Hot path; called once per temperature per thread. Added `using System.Numerics.Tensors`.
- **`EuclideanDistance`** (`Euclidean.cs`): Replaced manual diff-squared loop with `TensorPrimitives.Distance<double>`.
- **`CosineDistance`** (`Cosine.cs`): Replaced three scalar dot/norm loops with `TensorPrimitives.CosineSimilarity<double>`. Zero-vector guard now checks `double.IsNaN(cosSim)` instead of `denom < 1e-15`.
- **`ManhattanDistance`** (`Manhattan.cs`): Replaced manual `Math.Abs` + sum loop with `TensorPrimitives.SumOfAbsoluteDifferences<double>`.
- **`MinkowskiDistance`** (`Minkowski.cs`): Added fast-path dispatch for `p=1` (`SumOfAbsoluteDifferences`) and `p=2` (`Distance`) before the scalar fallback. The `maxDiff` overflow guard is preserved in the scalar fallback for arbitrary `p`; the fast paths assume well-behaved input magnitudes.
- **`BuildMahalanobisGraph`** (`Mahalanobis.cs`): Replaced `new double[dim]` allocations on every pairwise call (O(N²) heap pressure) with a three-tier scratch buffer strategy: `stackalloc` for `dim ≤ 64`, `ThreadLocal<(double[], double[])>` for `dim ≤ 512`, `ArrayPool<double>` rent/return for `dim > 512`. Inner loop (scalar `invCov[i,j]` multiply) unchanged — TensorPrimitives vectorisation deferred to Batch B after `invCov` layout flatten. Added `_mahalScratch` ThreadLocal with remarks on write-before-read invariant and thread-count assumption.
- **`SelectNeighborsKnn`** (`Knn.cs`): Pass 1 (directed KNN heap construction) parallelised with `Parallel.For`. Each row owns its heap and writes only `directedNeighbors[i]` — no contention. Pass 2 (OR-symmetrize) remains sequential.
- **`SelectNeighborsMutualKnn`** (`MutualKnn.cs`): Pass 1 (directed KNN + HashSet construction) parallelised with `Parallel.For`. Same isolation property as KNN. Pass 2 (AND-filter) remains sequential.
- **`SelectNeighborsEpsilonBall`** (`EpsilonBall.cs`): Restructured from a single sequential upper-triangle pass with cross-row writes to a two-phase approach: Phase 1 is a `Parallel.For` upper-triangle scan where each row `i` writes only to `halfLists[i]`; Phase 2 is sequential O(E) symmetrization into the final `lists[]`.
- **`MstAugmented`** not parallelised — Kruskal's MST step is globally sequential; deferred to Batch B scope discussion.

### Not Changed (Batch A invariants)

- `invCov` layout (`double[,]`) — Batch B prerequisite for TensorPrimitives inner loop in Mahalanobis
- All public signatures and DTOs
- Simulation logic in `RunSimulationCore`, `PottsModel.RunSimulation`
- `CsrGraph`, `FastUnionFind`, checkpoint I/O

---

## 2026-04-17 - Checkpoint Filename Convention

> Superseded on 2026-05-01 by the manifest-backed run directory layout: `CheckpointDirectory` is now a working root, each run writes under `{root}/{runStamp}/`, and artifacts are grouped by payload type.

### Changed

- **`SpcBatch.Checkpoint.cs`**: Checkpoint filenames now use the format `spc_{runStamp}_{X16}.ckpt` instead of `{X16}.ckpt`. `runStamp` is a `yyyyMMdd_HHmmss` string stamped once per `Run()` call, scoping all checkpoint files for a run to a common prefix. Enables multiple runs to coexist in the same flat directory without collision.
- **`FlushTemperatureCheckpoint()`** (`SpcBatch.Checkpoint.cs`): Signature gains `string runStamp` parameter (inserted after `directory`). Key computed via string interpolation `$"spc_{runStamp}_{BitConverter.DoubleToInt64Bits(T):X16}"`.
- **`LoadCheckpointFromDirectory()`** (`SpcBatch.Checkpoint.cs`): Signature gains `string runStamp` parameter. Glob pattern changed from `*.ckpt` to `spc_{runStamp}_*.ckpt`. T identity is still parsed from `lines[0]` — the X16 segment is not extracted from the filename.
- **`SpcBatchResult.RunDirectory`** (`spc.batch.cs`): New property. Holds the `yyyyMMdd_HHmmss` stamp for the run's checkpoint files. Pass as `SpcBatchRequest.ResumeDirectory` (together with `CheckpointDirectory`) to resume. Null if `CheckpointDirectory` was not set.
- **`RunSimulationCore()`** (`spc.batch.cs`): Stamps once before `Parallel.For` (`DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")`), sets `result.RunDirectory`. Load path resolves `loadStamp` as `ResumeDirectory` when provided, otherwise the fresh `runStamp`. Both `LoadCheckpointFromDirectory` and `FlushTemperatureCheckpoint` receive the stamp.
- **`SpcBatchRequest` doc comments** (`spc.batch.cs`): `CheckpointDirectory` and `ResumeDirectory` comments updated at the time to reflect the former flat-directory design. This convention is superseded by the 2026-05-01 run-directory layout above.

### Invariants Preserved

- `.tmp` → `.ckpt` atomic rename is unchanged.
- File content format (3 lines: T, susceptibility, space-separated spins) is unchanged.
- In-memory `Checkpoint` still takes precedence over `ResumeDirectory`.

---

## 2026-04-17 - Hashish, Synthetic Data, and KNN Lies

### Added

- **`src/spc.synthetic.cs`** (root): `SyntheticDataset` DTO (`Features`, `Labels`, `ClusterCount`, `Parameters`, `LabelsByLevel`, `ClusterCovariances`). `ComputePurity()` scoring utility. Sampling primitives (`SampleStandardNormal`, `SampleGamma`, `SampleDirichlet`, `Normalize`) and geometry primitives (`PlaceCentroidsOnSphere`, `RandomRotationMatrix`, `GramSchmidtOrthonormalize`, `MultiplyMatrixVector`, `BuildCovariance`) as `internal static` — shared across all synthetic partials. `public static partial class SyntheticData` shell.
- **`src/synthetic/SpatialBlobs.cs`**: `GenerateSpatialBlobs` — isotropic Gaussian clusters with configurable separation and spread. Canonical smoke-test generator.
- **`src/synthetic/SparseSupports.cs`**: `GenerateSparseSupports` — high-dimensional sparse binary clusters with controlled support overlap. Good for Jaccard / Hamming metrics.
- **`src/synthetic/Simplex.cs`**: `GenerateSimplex` — Dirichlet-sampled probability vectors per cluster with optional disjoint support structure. Natural companion for JSD / Wasserstein.
- **`src/synthetic/AnisotropicGaussian.cs`**: `GenerateAnisotropicGaussian` — clusters with Haar-distributed rotation matrices and configurable anisotropy ratio. Tests Mahalanobis / Cosine sensitivity to covariance structure.
- **`src/synthetic/GaussianManifold.cs`**: `GenerateGaussianManifold` — clusters on a unit hypersphere surface (von Mises–Fisher style placement via Gram-Schmidt). Tests spherical geometry metrics.
- **`src/synthetic/BlattThreeCluster.cs`**: `GenerateBlattThreeCluster` — canonical three-cluster dataset from Blatt/Wiseman/Domany 1996 PRL. Reproducible benchmark for SPC phase transitions.
- **`src/synthetic/BlattHierarchy.cs`**: `GenerateBlattHierarchy` — hierarchical multi-scale cluster structure with `LabelsByLevel` output. Tests hierarchical phase transition resolution.
- **`src/synthetic/TwoMoons.cs`**: `GenerateTwoMoons` — two interleaved crescent manifolds with configurable noise. Tests non-convex cluster separation.
- **`src/hashish/` directory**: Standalone `Hashish` namespace housing five independent preprocessing utilities. Air-gapped from `SpcCore` — callers compose both, metrics never import Hashish.
  - `simhash.cs` (`SimHash`) — BM25-weighted 64-bit SimHash. `Compute(string)` returns `ulong`; `HammingDistance(ulong, ulong)` via `BitOperations.PopCount`.
  - `bm25-stats.cs` (`Bm25Stats`) — corpus-level BM25 IDF statistics.
  - `minhash.cs` (`MinHash`) — MinHash signatures for Jaccard estimation via shingling.
  - `tlsh.cs` (`TrendLocalitySensitiveHash`) — TLSH fuzzy digest; bucket histogram + quartile encoding.
  - `ctph.cs` (`ContextTriggeredPiecewiseHash`) — ssdeep-style CTPH via rolling FNV-1a; Levenshtein comparison.

### Changed

- **`SpcBatchRequest.SimHashes`** (`spc.batch.cs`): Renamed from `SimHashHex` (`string[]`) to `SimHashes` (`ulong[]`). The metric kernel never parses text or hex — signatures are produced upstream by `Hashish.SimHash.Compute` and handed over as raw `ulong`.
- **`Hamming.cs`**: Rewritten to consume `ulong[]` directly via `BitOperations.PopCount(hashes[i] ^ hashes[j])`. Hex parsing removed entirely from the hot path.
- **All 10 `Build<Metric>Graph` metric builders** (`src/metrics/`): Renamed from `BuildKnn<Metric>` — "KNN" was a misattribution. Each method dispatches through both `SpcProximity` axes (KNN, MutualKnn, EpsilonBall, MstAugmented) and never owned KNN specifically. Naming now reflects what the method actually does: build a proximity graph for a given metric.
- **`BuildGraphFromFeatures()`** (`spc.batch.cs`): Renamed from `BuildKnnFromFeatures`. Same misattribution — the shared helper dispatches on proximity, not KNN specifically.
- **`SpcBatch.Run()` doc comment** (`spc.batch.cs`): "metric-specific KNN builder" → "metric-specific graph builder".
- **All metric file doc comments** (`src/metrics/`): "<Metric> KNN builder" → "<Metric> proximity-graph builder"; "Builds KNN graph" → "Builds neighbor graph".
- **`spc.potts.cs` section header**: "Swendsen-Wang Monte Carlo on a KNN graph" → "Swendsen-Wang Monte Carlo on a proximity graph". The Potts model runs on any proximity graph — KNN, epsilon-ball, MST-augmented.
- **Hashish files** (`simhash.cs`, `bm25-stats.cs`, `minhash.cs`, `tlsh.cs`, `ctph.cs`): Added `namespace Hashish;` (file-scoped) to each. Previously lacked a namespace declaration.

### Removed

- **`src/spc.synthetic-DRAFT.cs`**: Deleted. ~780-line monolith replaced by `spc.synthetic.cs` root + eight `src/synthetic/` partials.
- **`src/spc.hashish.cs`**: Deleted. Placeholder for a partial-class dispatcher pattern that was explicitly rejected for Hashish — the toolbelt has heterogeneous signatures and is not a Strategy pattern. No dispatcher replacement.
- **`SimHash` hex path**: `Compute(string)` no longer returns a 16-char hex string. Returns `ulong`. `HammingDistance` no longer calls `Convert.ToUInt64`. All string hex round-trips removed.

## 2026-04-16 - Slash and Burn

### Added

- `src/spc.thermo.cs` with thermodynamic analyses, histograms, KL Divergence applications, placeholder for KL fan-out concept
- `metrics/FisherRao.cs` placeholder added to metrics inventory
- `metrics/SpcBatch.SyntheticData.cs` added with sealed classes for generating different kinds of synthetic data for testing bench work

### Removed

- `action-items.md` was becoming a problem of compounding intellectual debt and AI slop. Moved to discussion archives for reference only.

### Changed

- filenames may have changed in some places, need to validate that path pointers in the code are still valid

## 2026-04-15

### Added

- **`SpcProximity` enum** (`spc.batch.cs`): `Knn`, `MutualKnn`, `EpsilonBall`, `MstAugmented` — Axis 2 dispatch key for graph construction rules.
- **`Proximity` / `Epsilon` properties** (`SpcBatchRequest`): Expose proximity rule and epsilon-ball radius through the PS↔C# contract.
- **`proximities/` directory** with four partial class files:
  - `Knn.cs` — Standard KNN with OR-symmetrization. Produces undirected graph; some nodes may exceed K neighbors after symmetrization.
  - `MutualKnn.cs` — AND-rule: edge exists only if both nodes include each other in their K-nearest. Inherently symmetric, sparser than OR-KNN.
  - `EpsilonBall.cs` — Radius-based: edge exists if `d(i,j) < epsilon`. Inherently symmetric. Variable node degree.
  - `MstAugmented.cs` — Mutual KNN base augmented with Kruskal's MST bridging edges. Guarantees a connected graph; recommended for high-dimensional data or JSD metric.
- **`SelectNeighbors()`** (`spc.batch.cs`): Proximity dispatch method. Returns `(Neighbor[][], double[] nnDistances)` — the shared contract between all proximity rules and the coupling conversion step.
- **`ConvertToCoupling()`** (`spc.batch.cs`): Separated coupling conversion from neighbor selection. Consumes `Neighbor[][]` from any proximity rule; emits one `Edge` per canonical undirected pair.
- **`ValidateGraph()`** (`spc.batch.cs`): Post-construction connectivity check via union-find. Issues warnings for disconnected graphs, largest-component coverage below 90%, and isolated nodes.
- **`Warnings` property** (`SpcBatchResult`): Surfaces graph validation diagnostics to the PS orchestration layer. Non-blocking — simulation runs regardless.
- **`CsrGraph` struct** (`spc.foundation.cs`): Compressed Sparse Row graph representation with `Targets`, `Weights`, `RowPointers` flat arrays and `FromEdges(Edge[], int)` factory. Symmetric storage — each undirected edge stored in both directions. Replaces `Edge[]`/`EdgeCount` in PottsModel for cache-coherent SW bond formation.

### Changed

- **`changelog.md`**: Moved from `src/changelog.md` to project root `ps.core.pwshspc/changelog.md`.
- **`primer.md`** (`.copilot/primer.md`): Reconciled with `.copilot/brief` — absorbed all still-relevant context. Updated platform target from PS 7.5 to PS 7.6 / .NET 10. Updated "Where things are" table with current file inventory (`src/deltas/`, `src/proximities/`, all 10 metrics, `changelog.md` at root). Updated key conventions to reflect three dispatch axes (`SpcMetric`, `SpcProximity`, `DeltaEstimator`). Clarified checkpoint state as planned but not yet implemented.
- **`action-items.md`** (`src/action-items.md`): Full reconciliation pass against source code and changelog:
  - Four-Axis table: Axis 2 updated from `❌ Missing` / `SpcTopology` to `✅ Done` / `SpcProximity (4)`. Axis 3 updated from 3 estimators under `src/estimators/` to 2 under `src/deltas/`.
  - Key Correctness Findings: MAD → resolved (removed). KNN asymmetry → fixed (OR-symmetrization). MutualKNN anti-hub → implemented (`MstAugmented`). Connectivity check → implemented (`ValidateGraph`).
  - Design Decisions: D3 struck through (AddEdge deleted). D6 updated (landed as `SpcProximity`). D8 struck through (MAD deprecated/removed). D9 updated (rename moot — `KnnGraphBuilder` is dead code).
  - Phase 3: Estimator table updated (MAD removed, folder path corrected). Task 3.1 updated.
  - Phase 3B: All items 3B.1–3B.6 marked done with current naming. 3B.7 updated (rename moot, cleanup candidate).
  - Completed section: Added proximity axis, graph refactor, connectivity validation, KNN symmetrization, correctness fixes, dead code removal.
  - Future section restructured into Phase 6 (Hashing Internalization under `src/hashing/`) and Phase 7 (.NET 10 Hot-Path Optimizations). Former bullet-point items promoted to structured action items with dependencies. Cross-references `ps.core.mathdig/csharp/simhash.cs` as reference implementation for SimHash C# port, `FrozenDictionary`, `CollectionsMarshal`, and `stackalloc` patterns.
- **`BuildKnnFromFeatures()`** (`spc.batch.cs`): Refactored to dispatch through `SelectNeighbors` (proximity) then `ConvertToCoupling`. Accepts `proximity` and `epsilon` optional parameters.
- **All 10 metric `BuildKnn*` methods** (`src/metrics/`): Updated to pass `request.Proximity` and `request.Epsilon` through to `BuildKnnFromFeatures`.
- **`Hamming.cs`**: Rerouted from `KnnGraphBuilder.BuildFromHammingGaussian` to the shared `SelectNeighbors` + `ConvertToCoupling` path. Hamming now participates in proximity axis dispatch.
- **`RunSimulationCore()`** (`spc.batch.cs`): Now accepts a `List<string> warnings` parameter and attaches it to `SpcBatchResult.Warnings`.
- **`src/estimators/` → `src/deltas/`**: Renamed directory to reflect that these estimators serve the delta (bandwidth) axis specifically, not estimation in general. Partial class files moved accordingly.
- **`DeltaEstimator` documentation** (`spc.batch.cs`, `spc.foundation.cs`): Corrected misleading comment that framed `Mean` as a temporary default. `Mean` is the validated estimator from prior work; `Median` is theoretically more robust to outliers but has not been tested in this context.
- **`FastUnionFind`** (`spc.foundation.cs`): Added `Reset()` method — resets `parent[i]=i`, `size[i]=1` without reallocating arrays. Enables pre-allocation outside the SW step loop (HPC 1).
- **`PottsModel.RunSimulation`** (`spc.potts.cs`): Zero-alloc step loop. `FastUnionFind`, `labels`, `clusterMap`, and `sizes` pre-allocated once before the loop. `clusterMap` uses `int[]` with `-1` sentinel (was `Dictionary<int,int>`). `sizes` uses `int[]` with `Array.Clear` (was `Dictionary<int,int>`). Bond formation uses row-major CSR traversal with `j <= i` skip (was flat `Edge[]` iteration).
- **`RunSimulationCore`** (`spc.batch.cs`): Builds `CsrGraph` once from `Edge[]` via `CsrGraph.FromEdges`, shared across all temperature models. Temperature loop replaced with `Parallel.For` — each temperature creates its own `PottsModel` with isolated `Spins[]`, `bondProb[]`, and `Random Rng`. Results staged into `SimulationResult[]` during parallel phase, merged into `FinalSpins` Dictionary sequentially after. No `ThreadLocal<Random>` needed — per-model RNG ownership satisfies thread safety.

### Removed

- **`.copilot/brief`**: Removed. Content fully absorbed into `.copilot/primer.md` during reconciliation.
- **`DeltaEstimator.MAD`** (`spc.batch.cs`): Deprecated and removed from enum and dispatch. MAD measures dispersion of 1-NN distances (spread), not central tendency (typical scale) — a categorically different statistical question from what the delta estimator axis answers. Former partial class `SpcBatch.EstimatorMad.cs` removed from `src/estimators/` (now `src/deltas/`).
- **`KnnGraphBuilder`** (`spc.foundation.cs`): Class deleted. `BuildFromHamming`, `BuildFromHammingGaussian`, and `BuildFromDistanceMatrix` were superseded when `Hamming.cs` was rerouted through `SelectNeighbors` + `ConvertToCoupling`. `ParseHexHashes` (the sole live method) inlined directly into `BuildKnnHamming`. `Neighbor` and `BoundedMinHeap` retained — used by all four proximity builders. Section header updated to "Graph primitives — heap/neighbor types used by proximity builders".
- **`PottsModel.AddEdge`** (`spc.potts.cs`): Deleted. Method rebuilt the entire edge array on every call (O(N²) total). Only caller was `drafts/` — nothing in `src/` used it.
- **`PottsModel.LoadEdges`** (`spc.potts.cs`): Replaced by `LoadGraph(CsrGraph)` as part of CSR migration (HPC 3).
- **`PottsModel.PrecomputeBondProbabilities`** (`spc.potts.cs`): Deleted. Bond probability computation (`1 - exp(-J/T)`) is now a local allocation at the top of `RunSimulation`. Removes mandatory two-step call choreography and the stale `BondProb` field.

### Fixed

- **Wasserstein** (`Wasserstein.cs`): `Wasserstein1D` now sorts local copies of both input arrays before CDF integration. Previously silently produced wrong results on unsorted input.
- **Minkowski** (`Minkowski.cs`): `MinkowskiDistance` now factors out `max|diff|` before accumulating `|diff/max|^p`. Prevents `double` overflow for large `p` and non-trivial feature magnitudes.
- **Jaccard** (`Jaccard.cs`, `spc.batch.cs`): Binarization threshold was hardcoded at `0.5`. Added `SpcBatchRequest.JaccardThreshold` (default `0.5`) and wired it through as a closure parameter.

### Pending — HPC Upgrades DONE - Merge these items into added

All HPC items complete.

1. ~~**Pre-allocate `FastUnionFind` arrays** (`spc.potts.cs`):~~ **Done.** Added `FastUnionFind.Reset()` (`spc.foundation.cs`). `RunSimulation` allocates once before the step loop, calls `Reset()` per step. Eliminates one `FastUnionFind` + one `int[]` (`GetLabels`) allocation per step.
2. ~~**Replace hot-path `Dictionary<int,int>` with `int[]`** (`spc.potts.cs`):~~ **Done.** `clusterMap` → `int[N]` with `-1` sentinel, `sizes` → `int[N]` with `Array.Clear`, `labels` → pre-allocated `int[N]` filled inline via `uf.Find(i)`. Step loop is now zero-alloc.
3. ~~**CSR graph representation** (`spc.foundation.cs`):~~ **Done.** Added `CsrGraph` struct with `Targets`, `Weights`, `RowPointers` flat arrays and `FromEdges` factory. Each undirected edge stored in both directions (symmetric CSR). `PottsModel` now holds `CsrGraph` instead of `Edge[]`/`EdgeCount`. Bond formation uses row-major traversal with `j <= i` skip for canonical edge processing. CSR built once in `RunSimulationCore`, shared across all temperatures. `ValidateGraph` remains on `Edge[]` (runs once, not hot path).
4. ~~**`Parallel.For` temperature sweep** (`spc.batch.cs`):~~ **Done.** Temperature loop in `RunSimulationCore` replaced with `Parallel.For`. Results collected into `SimulationResult[]` (one slot per temperature, no contention), merged sequentially into `FinalSpins` Dictionary after all threads complete. `using System.Threading.Tasks` added.
5. ~~**Per-thread RNG**:~~ **Satisfied by existing architecture.** Each `PottsModel` already owns its own `private Random Rng`. `Parallel.For` creates one model per temperature — no shared RNG instances. No `ThreadLocal<Random>` needed.
