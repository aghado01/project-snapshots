## 2026-05-16 — Namespace and whitespace cleanup (post-viewer pass)

Written: 2026-05-16

### Fixed

- **`src/graphs/GraphSelection.cs`** — namespace corrected from `Graphs.Proximity` to `Graphs` so `NeighborSelection` resolves from the correct assembly root rather than the proximity sub-namespace.
- **`src/maths/geometry/RiemannianProductManifold.cs`** — file-header path comment updated from `Manifolds/ProductManifold.cs` to `Geometry/ProductManifold.cs` to reflect the current source layout.
- **`src/maths/optimization/irls-options.cs`** — whitespace alignment in `IrlsOptions.Default` property initializer trimmed to standard non-padded form; no behavioural change.

---

## 2026-05-16 — VizCore viewer line-field rendering pass and validation

Written: 2026-05-16

### Fixed

- **`src/viz-core/viewer.html`** — line-field rendering is now wired end-to-end in the embedded viewer. Added a `sel-linefield` dropdown beside the existing color selector, a `sceneLineFieldLayers` cache, and `_lineFieldMeshes` storage so `ScenePackage.line_field_layers` can be surfaced without reopening the old scalar/vector naming drift.
- **`src/viz-core/viewer.html`** — added `buildLineField(layer, cloudRadius, ptsSource)` as the concrete render path for `LineFieldLayer`: one centered segment per node at `p ± 0.05 * cloudRadius * d_hat`, `LineSegments` + `LineBasicMaterial({ vertexColors: true })`, zero-length collapse for degenerate directions, and no arrowheads so the viewer stays honest about the unoriented `d ≡ -d` contract.
- **`src/viz-core/viewer.html`** — line-field visibility now follows the same build-once doctrine as the other overlays. Layers are constructed once during initial load or JSON rehydrate, stored in `_lineFieldMeshes`, defaulted to hidden, and toggled only through `applyLineFieldLayer(name)` by flipping `.visible`.
- **`src/viz-core/viewer.html`** — node-signal recoloring now updates active line-field vertex colors in place by rewriting the existing color `BufferAttribute` and marking it dirty, so switching `NodeSignalLayer` stays interaction-hot instead of rebuilding geometry.
- **`src/viz-core/viewer.html`** — `rehydrateFromJson(...)` now tears down and rebuilds line-field meshes alongside the existing flow-field path, repopulates the dropdown from `newScene.line_field_layers`, and restores the previously selected layer name when it still exists in the regenerated scene.

### Validated

- **Owning viz build passed** — `projects/VizCore/VizCore.csproj` builds cleanly with the updated embedded viewer.
- **Nearest consumer builds passed** — `projects/VizApi/VizApi.csproj` and `projects/tests/VizCoreSmoke/VizCoreSmoke.csproj` both build cleanly against the current viewer/schema surface.
- **Draft contract host still runs cleanly** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` builds and its xUnit suite runs with the expected 7 skipped draft facts and 0 failures.

### Notes

- **Hot-path discipline held** — the user-toggle path does not allocate meshes, rebuild line geometry, or touch `scene.add(...)`; it only selects among prebuilt meshes and updates existing color buffers when the active node-signal layer changes.
- **Viewer contract remains honest** — `LineFieldLayer` is rendered as an unoriented line glyph surface, while `VectorFieldLayer` remains the oriented compatibility/render-ready path. `animate()` was left untouched.

## 2026-05-15 — Maths.LinAlg spectral sweep, project metadata sync, and validation refresh

### Fixed

- **`src/maths/linalg/MatrixOps.cs`** — the SIMD dot-product path now uses exact multiply-plus-add accumulation instead of `MultiplyAddEstimate`, so the orthonormalization / Ritz / residual path is no longer trading convergence quality for a speculative intrinsic shortcut.
- **`src/maths/linalg/LOBPCG.cs`** — repaired the recent bottom-spectrum solver back into an actual Ritz-based block iteration: explicit projection onto a small trial subspace, compact orthonormalization, null-mode deflation for Laplacians, precomputed inverse-square-root degrees for the normalized operator branch, and stable final eigenpair packaging from the current Ritz basis.
- **`src/maths/linalg/Spectral.cs`** — unified the shared `LaplacianType` / `EigenPair` surface with `LOBPCG` and kept `BuildCoherentField(...)` on the flat `[N x fieldDim]` row-major export path expected by the viewer-facing experiments.
- **`projects/Maths.LinAlg/Maths.LinAlg.csproj`** — confirmed the recent `Graphs.Primitives` project reference is present so the graph-aware spectral slice (`CsrGraph` consumers in `Spectral` / `LOBPCG`) builds from its actual dependency boundary.
- **`project.toml`** — refreshed the `Maths.LinAlg` inventory entry to describe the live surface (`MatrixOps`, `Spectral`, `LOBPCG`) and removed the obsolete `VizApi` failure note now that `projects/VizApi/VizApi.csproj` builds successfully again.

### Validated

- **Focused owning-project build passed** — `projects/Maths.LinAlg/Maths.LinAlg.csproj` builds cleanly after the spectral / iterative-eigensolver sweep.
- **Nearest consumer build passed** — `projects/VizApi/VizApi.csproj` builds cleanly on the current graph/geometry/viz stack, confirming the stale failure note in `project.toml` is no longer accurate.

### Notes

- **Current linalg shape** — `MatrixOps` is now the flat block-vector helper surface for the spectral path, while `Spectral.BuildCoherentField(...)` remains a viewer-oriented export that flattens the selected low-frequency modes into a row-major `[N x fieldDim]` buffer.
- **Scope of the LOBPCG repair** — this sweep restored the missing local Ritz step and cleaned the hot numerical path, but it did not add a dedicated numeric smoke harness yet. The next meaningful check would be a small path/cycle-graph spectral harness or a Crescent / Mobius comparison fixture.

## 2026-05-15 — Rename alignment, estimator/GMM rebind, and namespace-drift containment

### Fixed

- **`src/maths/linalg/ICA.cs`** — fixed the actual `double[][]` vs `double[,]` break in symmetric decorrelation by converting the jagged `WWt` scratch matrix to rectangular form before calling `Eigen.DecomposeSymmetric(...)`. `Maths.LinAlg` now builds on the real code path instead of relying on the earlier partial cleanup.
- **`projects/Graphs.Proximity/Graphs.Proximity.csproj`** — restored the graph-root compile surface (`GraphSelection.cs`, `GraphBuilder.cs`, `Bandwidth.cs`) so the assembly once again owns `NeighborSelection`, `GraphBuilder`, `ProximityRule`, `KernelType`, and bandwidth estimation instead of exposing only the proximity partials.
- **`src/graphs/proximity/*.cs`** — re-imported `Graphs` in the proximity partials so `NeighborSelection` and the graph-construction types resolve from the same assembly boundary they are compiled into.
- **`projects/Estimators/Estimators.csproj`** — estimator project identity is now `Estimators` end-to-end (`RootNamespace`, `AssemblyName`, recursive `src/estimators/**` compile glob). Added explicit `Maths.Geometry` and `Maths.Optimization` project references so the manifold estimator slice compiles from its real dependency surface.
- **`src/clustering/gmm/*.cs`** — the GMM runtime surface was rebound from `namespace StatisticalEstimators` to `namespace Estimators` so the clustering layer matches the current estimator project identity instead of dragging a stale namespace alias forward.
- **`src/viz-core/gmm_adapter.cs`** — updated to import `Estimators` so the visualisation adapter tracks the renamed GMM surface.
- **`projects/Clustering.GMM/Cluster.GMM.csproj`** — stale `Maths.LinAlg` project filename fixed (`Maths.LinAlg.csproj`) and explicit reference to `projects/Estimators/Estimators.csproj` added.
- **`projects/VizCore/VizCore.csproj`**, **`projects/TDA.Mapper/TDA.Mapper.csproj`**, **`projects/Clustering.SPC/Clustering.SPC.csproj`** — stale `Math.*` project paths corrected to the live `Maths.*` project filenames.
- **`projects/Maths.Optimization/Maths.Optimization.csproj`** — fixed the lingering stale reference to `..\Maths.Geometry\Math.Geometry.csproj` and added `System.Numerics.Tensors` so `Irls` compiles against the current `Maths.Geometry` and tensor surface.
- **`projects/tests/VizCoreSmoke/Program.cs`**, **`tests/tda/mapper/HyperbolicHierarchyTest.cs`**, **`src/tda/mapper/*.cs`**, and **`projects/VizApi/*`** — cleaned stale `ProximityGraphs`, `SyntheticDatasets`, old `MobiusPlacement` members, and old graph-root assumptions so downstream consumers match the current `Graphs.*`, `Synthetic`, and `Estimators` surfaces.
- **`project.toml`** — inventory now reflects the live repo state: `Maths.*` project paths, `Estimators` as the estimator assembly identity, updated `src/estimators` ownership notes, and an explicit `EmbeddingEtl` placeholder marked as not expected to build.
- **`ps.core.pwshspc.sln`** — solution entries now point at the real `projects\Maths.*\Maths.*.csproj` files and the estimator project is listed as `Estimators` instead of `StatisticalEstimators`.

### Written

| File                                      | Lines | What                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| ----------------------------------------- | ----: | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `src/graphs/primitives/CsrGraph.cs`       |   +91 | `InducedSubgraph(bool[], out int[] newToOld, out int[] oldToNew)` method on the struct                                                                                                                                                                                                                                                                                                                                                                          |
| `src/clustering/spc/AdaptiveScheduler.cs` |   332 | 3-stage adaptive scheduler against our `PottsModel` API. Coarse log-spaced probe -> dense band with `(T x Replicas x Rounds)` flat-task `Parallel.For` -> final `FkAndEdges` equilibrium. `chi_FK` as the susceptibility (reads `RunningSumSqClusterSizes` directly). Early-stop on coarse-stability >= 0.95. Deterministic per-task seeds derived from `HashCode.Combine(BaseSeed, T_quantized, round, replica)` when `BaseSeed` is set; OS-entropy otherwise. |
| `src/tda/mapper/Clusterers/MapperSpc.cs`  |   124 | `MapperSpcClusterer : IGraphClusterer`. Builds mask from preimage, calls `InducedSubgraph`, runs `AdaptiveScheduler`, applies `BlattCanonicalCut`, translates labels back to preimage-index order. Exposes `LastDiagnostics` for inspection.                                                                                                                                                                                                                    |

### Validated

- **Focused builds passed** — `projects/Maths.Optimization/Maths.Optimization.csproj`, `projects/Estimators/Estimators.csproj`, `projects/Clustering.GMM/Cluster.GMM.csproj`, `projects/VizCore/VizCore.csproj`, and `projects/Clustering.SPC/Clustering.SPC.csproj` all built successfully after the rename-alignment pass.
- **All `projects/tests` builds now pass** — `VizCoreSmoke`, `Spc.BlattSmoke`, `Spc.BlattAnalyze`, `Spc.HyperbolicSmoke`, and `TDA.Mapper.Tests` all build successfully after restoring the `Graphs.Proximity` assembly surface and cleaning the remaining stale imports.

### Lessons learned

- **Namespace drift is structural, not cosmetic** — when project names, assembly names, source namespaces, and consumer `using` directives stop moving together, the compiler reports downstream fallout (`type not found`, `member missing`, bad project refs) instead of the original identity split.
- **Inventory drift compounds code drift** — stale entries in `project.toml` and the solution file made the repo look partially renamed even after source files had moved. That delayed diagnosis because the metadata still described a world that no longer existed.

### Guidance

- **Rename atomically across all four identity layers** — namespace, assembly/root namespace, csproj path/file name, and solution/inventory metadata should change in one pass.
- **Treat partial types as a namespace hazard zone** — never move or rename one partial file in isolation; verify all siblings still declare the same namespace immediately after the change.
- **Validate one owning project and one consumer** — build only the library that owns the renamed surface and the nearest downstream consumer right after the edit. That catches both missing references and stale imports before the drift spreads.
- **Reserve `Maths.*` as the canonical prefix** — do not reintroduce `Math.*`; the collision risk with framework naming is real and it already produced avoidable ambiguity.

### Follow-up note

- **ETL transition note** — `projects/EmbeddingEtl` remains a non-building placeholder, but a live ETL-oriented source surface now exists under `src/ETL`. Any future VizApi/VizCore ETL-facing pointers should be reviewed against `src/ETL` first rather than assuming the old `EmbeddingEtl` project boundary still describes where those contracts and transforms live.

## 2026-05-15 — dev/tda: Mapper edge-smoothing utilities (B-spline and Laplacian)

### Added

- **`src_dev/tda/BSpline.cs`** — `BSpline` static class (no namespace; `src_dev/` experimental area). Implements the Cox–de Boor recursive algorithm: `EvaluateBasis(knots, i, p, t)` returns all non-zero basis function values at parameter `t`; `MakeClampedKnots(n, degree, interiorKnots)` builds the clamped knot vector for cubic B-splines. Intended as the foundation for fitting smooth curves through Mapper overlap-point sequences.
- **`src_dev/tda/LaplacianSmooth.cs`** — `LaplacianSmooth` free function (no class wrapper). Builds a sparse graph Laplacian `L = D − W` from CSR inputs (`rowPtr`, `colIdx`, `weights`), forms the SPD system `(I + λL)X = X_orig`, and solves per coordinate via a hand-rolled conjugate gradient (300 iterations, 1e-6 tolerance). Accepts an N×3 `Matrix<double>` and returns the smoothed layout.
- **`src_dev/tda/SmoothMapperEdges.cs`** — `SmoothMapperEdge` free function. Fits a regularized cubic B-spline through a sequence of 3D overlap points using chordal parameterization, quantile-placed interior knots, and a second-order smoothness penalty (smoothing weight `mu`). Returns dense evaluation points and the interpolated curve matrix. Wraps `BSpline.EvaluateBasis` and uses least-squares with a penalty term.
- **`src_dev/tda/laplacianSmoothLambda.cs`** — Usage snippet / lambda calibration driver. Computes an automatic `lambda` from median edge length (`0.1 / (medLen²)`), calls `LaplacianSmooth`, then iterates Mapper edges calling `SmoothMapperEdge` and `RenderSmoothTube`. Documents the intended pipeline: layout smoothing → per-edge B-spline fitting → tube rendering.

### Notes

- All four files live under `src_dev/tda/` — the experimental holding area for Mapper post-processing geometry work. They are not yet compiled into any project (`TDA.Mapper` or otherwise) and carry no namespace declaration.

## 2026-05-15 — GraphRepair namespace postmortem

### Postmortem

- **`src/graphs/GraphRepair.cs`** — the original failure was not a missing `Graphs.Primitives` project reference. The real issue was a partial-class namespace split: `GraphRepair.cs` declared `ProximityGraph` in `namespace Graphs` while the other proximity partials (`SelectKnn`, `SelectMutualKnn`, `SelectEpsilonBall`) live in `namespace Graphs.Proximity`.
- **Type split symptom** — that created two distinct types, `Graphs.ProximityGraph` and `Graphs.Proximity.ProximityGraph`, which led to misleading compiler errors that first surfaced as unresolved `UnionFind` and then, after namespace edits, as missing `SelectKnn`/`SelectMutualKnn`/`SelectEpsilonBall` members on `ProximityGraph`.
- **Takeaway** — for `partial` types, namespace identity is part of the type identity. When one file drifts namespaces, the compiler does not merge the declarations; it silently creates separate types and the resulting errors point at downstream missing members rather than the structural split itself.

## 2026-05-15 — Reference fixes: VizCore, Graphs.Proximity, ICA, Maths.Geometry

### Fixed

- **`VizCore.csproj`** — removed dead `LinearAlgebra\LinearAlgebra.csproj` and `GaussianMixture\GaussianMixture.csproj` references; added `Math.LinAlg\LinAlg.csproj`, `Clustering.GMM\Cluster.GMM.csproj`, and `StatisticalEstimators\StatisticalEstimators.csproj`.
- **`src/viz-core/adapter.cs`** — `using static SyntheticDatasets.SyntheticData` → `using static Synthetic.SyntheticData` (missed by the earlier bulk rename since it was a `using static` form).
- **`Maths.Geometry/Math.Geometry.csproj`** — added `<PackageReference Include="System.Numerics.Tensors" Version="10.0.0" />` (same fix as `Graphs.Distance`; required by `EuclideanVectorManifold`).
- **`Maths.LinAlg/ICA.cs`** — fixed pre-existing source bugs: added `OrthogonalRandomInit` (Box-Muller + Gram-Schmidt), replaced `double[,] sqrtInv` with `double[][]` in `SymmetricDecorrelation`, implemented `MatrixTranspose` and `MatrixMultiply` stubs.
- **`Graphs.Proximity.csproj`** — `<ProjectReference>` to `Graphs.Primitives` confirmed present (no change needed; `UnionFind` reachable).
- **`VizCoreSmoke.csproj`** — stale `ProximityGraphs\ProximityGraphs.csproj` → `Graphs.Proximity\Graphs.Proximity.csproj`.

## 2026-05-15 — Math→Maths namespace rename; src/math→src/maths

### Renamed (breaking)

- **`Math.*` → `Maths.*`** across all namespaces and assemblies — `Math.X` collides with the BCL `System.Math` alias in certain contexts; renamed to `Maths.X` globally.
  - `Math.LinAlg` → `Maths.LinAlg`
  - `Math.Geometry` → `Maths.Geometry`
  - `Math.Optimization` → `Maths.Optimization`
  - `Math.Rng` → `Maths.Rng`

### Changed

- **`src/math/` → `src/maths/`** — source folder renamed to match new namespace prefix.
- **23 source files updated** — `namespace Math.*` and `using Math.*` directives replaced throughout `src/maths/`, `src/estimators/`, `src/clustering/`, `src/tda/`, `src/viz-core/`.
- **4 csproj files updated** — `RootNamespace`, `AssemblyName`, and `<Compile>` glob paths (`src\math\` → `src\maths\`) in `Math.LinAlg/LinAlg.csproj`, `Math.Geometry/Math.Geometry.csproj`, `Math.Optimization/Math.Optimization.csproj`, `Math.Rng/Math.Rng.csproj`. Project folder names and SLN entries unchanged.
- **`project.toml`** — `assembly`, `sources`, `owner_projects`, and notes updated from `Math.*` → `Maths.*` and `src/math/` → `src/maths/`.

## 2026-05-15 — Math umbrella, distance taxonomy, smoke reorganization, Synthetic rename

### Renamed (breaking)

- **`SyntheticDatasets` → `Synthetic`** — namespace (`namespace Synthetic`), assembly, csproj, and all ProjectReferences updated. Callers: `VizCore`, `VizApi`, `Spc.BlattSmoke`, `Spc.HyperbolicSmoke`, `src/viz-core/adapter.cs`.
- **`Linalg` project** — path corrected to `projects/Math.LinAlg` (assembly was already `Math.LinAlg` from prior session; path entry in project.toml was stale).

### Added

- **`projects/Math.Geometry/Math.Geometry.csproj`** — new project; `namespace Math.Geometry`; covers `src/math/geometry/` (IRiemannianManifold, EuclideanVectorManifold, RiemannianProductManifold). No upstream project references.
- **`projects/Math.Optimization/Math.Optimization.csproj`** — new project; `namespace Math.Optimization`; covers `src/math/optimization/` (IRLS, IRobustLoss, loss options). References `Math.Geometry`.
- **`projects/Math.Rng/Math.Rng.csproj`** — new project; `namespace Math.Rng`; covers `src/math/rng/` (Xoshiro256++).
- **`projects/Graphs.Distance/Graphs.Distance.csproj`** — new project; covers `src/graphs/distance/**` with `Graphs.Distance.*` sub-namespaces organized by geometry family: `euclidean/`, `geodesic/`, `probability/`, `sets/`, `other/`.
- **`projects/tests/` subfolder** — smoke tests and test harnesses consolidated under `projects/tests/`: `VizCoreSmoke`, `Spc.BlattSmoke`, `Spc.HyperbolicSmoke`, `Spc.BlattAnalyze`, `TDA.Mapper` (formerly `TDA.Mapper.Tests`).

### Changed

- **`src/math/` umbrella** — `src/linalg`, `src/manifolds`, `src/losses`, `src/optimization` all relocated under `src/math/{linalg,geometry,optimization,rng}`. Corresponding `Math.*` namespaces applied to all source files.
- **`src/graphs/distance/`** — formerly `src/metrics/`. All 14 metric files assigned `Graphs.Distance.{Euclidean,Geodesic,Probability,Sets,Other}` namespaces.
- **`Spc.HyperbolicSmoke`** — removed stale single-file `<Compile>` hack for `Poincare.cs` (from deleted `src/manifolds/geodesics/`); replaced with `<ProjectReference>` to `Graphs.Distance`.
- **`VizCoreSmoke`** — fixed stale `ProximityGraphs\ProximityGraphs.csproj` reference → `Graphs.Proximity\Graphs.Proximity.csproj`.
- **`StatisticalEstimators.csproj`** — removed three stale compile globs (manifolds, losses, optimization); added `Math.Geometry` ProjectReference.
- **`Clustering.GMM`, `TDA.Mapper`** — updated `Linalg\Linalg.csproj` → `Math.LinAlg\LinAlg.csproj`.
- **`src/estimators/*.cs`** (5 files) — `using Manifolds;` → `using Math.Geometry;`.
- **`src/clustering/gmm/GaussianComponent.cs`** — `using Linalg;` → `using Math.LinAlg;`; `using DistanceMetrics;` → `using Graphs.Distance.Probability;`.
- **`projects/VizApi/DistanceFactory.cs`** — `using DistanceMetrics;` → `using Graphs.Distance.Euclidean; using Graphs.Distance.Geodesic;`.
- **`project.toml`** — all new/moved projects and source_roots updated; `src/manifolds`, `src/losses`, `src/optimization` source_roots marked `deprecated-moved`; smoke/test projects paths updated to `projects/tests/`.
- **`ps.core.pwshspc.sln`** — removed: `Linalg`, `DistanceMetrics`, old smoke paths. Added: `Math.LinAlg`, `Math.Geometry`, `Math.Optimization`, `Math.Rng`, `Graphs.Distance`, `projects/tests/*`.

## 2026-05-15 — SPC IO layer: SPCX checkpoint and SPCE edge-observable serializers

### Added

- **`src/clustering/spc/PottsModelStepResultIO.cs`** — `PottsModelStepResultIO` static class. Custom binary serializer for `PottsModelStepResult` using the SPCX v2 format. Persists spin configuration, cluster-size histogram, Xoshiro256++ RNG state, and running statistics (energy, magnetization). Self-describing with magic number and version field; `ReadFrom` validates magic and version before deserializing. Companion to the in-memory `GetCheckpoint/Restore` on `PottsModel` for durable checkpoint workflows.
- **`src/clustering/spc/PottsModelEdgeObservablesIO.cs`** — `PottsModelEdgeObservablesIO` static class. Binary serializer for `PottsModelEdgeObservables` using the SPCE format (sidecar to SPCX). Serializes the `BondFormedCount` and `SpinAgreementCount` per-edge accumulator arrays. Includes its own magic validation and version check. The two-file pair (SPCX + SPCE) is the complete durable checkpoint surface for a Potts model sweep.

### Notes (session recap)

✓ Xoshiro256PlusPlus with raw save/restore
✓ csproj wiring (Graphs.Primitives + Clustering.SPC, sln + toml updated)
✓ PottsModelStepResult DTO + GetCheckpoint/Restore on PottsModel
✓ TaskSpec + BuildTaskList + SpcScheduler.Run (flat-list dump scheduler)
✓ Fixture driver: Blatt Euclidean → CsrGraph → schedule → run
✓ Offline analysis (one-off script reading the checkpoint directory → χ(T))

## 2026-05-15 — Project restructure: TDA.Mapper promotion, namespace renames, synthetic/metrics taxonomy

### Renamed (breaking)

- **`CouplingKernels` → `Graphs.Coupling`** — namespace, assembly, and project renamed across all files (`src/graphs/coupling/*.cs`, `src/graphs/GraphBuilder.cs`).
- **`LinearAlgebra` → `Linalg`** — namespace, assembly, and project renamed.
- **`ProximityGraphs` → `Graphs.Proximity`** — namespace, assembly, and project renamed. Sources scoped to `src/graphs/*.cs` and `src/graphs/proximity/`; primitives and coupling subfolders now separately owned.
- **`GaussianMixture` → `Clustering.GMM`** — namespace, assembly, and project renamed.
- **`Graphs.TDA.Mapper` → `TDA.Mapper`** — namespace renamed across all mapper files (Mapper.cs, GraphMapper.cs, Cover/, Filters/, Clusterers/) and test files.

### Added

- **`projects/Clustering.Init/`** — new project covering `src/clustering/init/`; KMeans++ and related seeding strategies.
- **`projects/TDA.Mapper/`** — new project covering `src/tda/mapper/**`. References Graphs.Primitives, Graphs.Proximity, Linalg, Clustering.Init, Clustering.GMM.
- **`projects/TDA.Mapper.Tests/`** — new test project covering `tests/tda/mapper/`.
- **`src/tda/mapper/`** — promoted from `src_dev/graphs/tda/mapper/`. Superset of the old `src/graphs/tda/mapper/` with `IGraphFilter`, `IGraphClusterer` interfaces and graph `Build(CsrGraph,...)` overload.

### Changed

- **`ps.core.pwshspc.sln`** — removed ProximityGraphs, CouplingKernels, LinearAlgebra, GaussianMixture; added Graphs.Proximity, Graphs.Coupling, Linalg, Clustering.GMM, Clustering.Init, TDA.Mapper, TDA.Mapper.Tests.
- **`src/synthetic/` reorganized** — generators split into `euclidean/` (flat R^d: blobs, anisotropic Gaussians, sparse supports, Blatt hierarchies, TwoMoons, MobiusEllipsoid, CrescentEllipsoid) and `manifolds/` (curved/information-geometric: HyperbolicHierarchy, HyperbolicBlobs, GaussianManifold, Simplex). AnisotropicGaussian, SpatialBlobs, SparseSupports moved from root into `euclidean/`.
- **`projects/SyntheticDatasets/SyntheticDatasets.csproj`** — compile glob updated from `src/synthetic/*.cs` to `src/synthetic/**/*.cs` to cover subfolders.
- **`project.toml`** — all renamed/new projects updated; `src/metrics`, `src/manifolds`, `src/synthetic` source_root entries updated with scoping notes.

### Notes

- `src/metrics/` covers flat-space metrics only (Lp family, Mahalanobis, Jaccard, Hamming, NCD). Geodesic metrics for curved spaces live in `src/manifolds/geodesics/` (Poincare, FisherRaoHalfPlane, FisherRaoSimplex, Cosine, Wasserstein1).
- JensenShannon is a proper metric (sqrt form) and is a candidate for relocation from `src/metrics/` to `src/manifolds/geodesics/`.
- Hamming has no matching generator in `src/synthetic/`; candidates would need bit-packed categorical data.
- `src_dev/` and old `src/graphs/tda/` folders not yet cleaned up.

## 2026-05-14 — House cleaning part 1

### Changes

- Several breaking changes moving files around in `src/` to consolidate graph initialization code, codify contracts, streamline related ETL, synthetic data and graph initialization workflows, and fix various correctness problems and baroque code patterns. Separating manifold-related workflows with special adapter and contract needs from nominal euclidean workflows where less machinery is needed. Aiming for a coherent architecture without unnecessary complexity.
- moved items related to convergent graph construction inputs such as metric adapters,
- started grouping synthetic data generators into manifold and euclidean buckets

- Also added new `src_dev` bucket for WIP partial/broken code that isn't ready for building and for placeholders. `src_dev/` mirrors the organization of its sibling production roots intentionally, to convey likely destinations of WIP and placeholder items without implying normal `src/` ownership.

TODO: repair broken references across project where filepaths and filenames have suddenly changed. do not assume broken references in csproj files are vestigial. current breaks are part of the WIP cleanup progress.

## 2026-05-13 — Embedding ETL probe surface, graph-binding adapters, hyperbolic hierarchy

### Added

- **`projects/EmbeddingEtl/EmbeddingEtl.csproj` + `src/manifolds/transforms/PoincareTransforms.cs` — dedicated embedding ETL project and manifold transforms**: added a new ETL-oriented project boundary for manifold preparation work, separate from `StatisticalEstimators`. `PoincareTransforms` now exposes two explicit Euclidean-to-ball preparation paths: dataset-wide linear `ScaleToOpenBall` and tangent-space `ExpMapFromOrigin`, both with batch overloads for embedding matrices.
- **`src/manifolds/adapters/ProbeContracts.cs` + `src/manifolds/adapters/ManifoldAdapters.cs` + `src/manifolds/adapters/ContractValidator.cs` — manifold probe contract surface**: added a probe-specific adapter contract (`ProbeMetric`, `ProbeContract`, `IManifoldAdapter`) plus adapter lookup and graph-readiness validation. This gives the graph-binding layer a typed ETL handoff: raw ambient features -> adapted manifold features -> domain validation before distance dispatch.
- **`projects/VizApi/DistanceFactory.cs`** — graph-binding distance factory now lives on the consumer side rather than inside `EmbeddingEtl`. The ETL layer remains metric-free; VizApi owns the final metric-to-distance delegate binding over validated adapted features.
- **`src/synthetic/HyperbolicHierarchy.cs` + `projects/VizApi/Program.cs` + `src/viz-core/schema_catalog.cs` — `HyperbolicBlattHierarchy` integrated end-to-end**: added a new synthetic generator that produces recursively nested clusters directly inside the Poincare ball, exposes hierarchical GT labels through `LabelsByLevel`, is selectable in VizApi, and is registered in the schema catalog so the viewer generator picker surfaces it with its own parameter schema.

### Changed

- **`src/manifolds/adapters/identity.cs` + `src/manifolds/adapters/simplex.cs` + `src/manifolds/adapters/hyperbolic.cs` — adapters now enforce ETL buffer shape and manifold semantics**: all adapters now validate `raw.Length == n × d` before adaptation. The simplex adapter note was corrected to match softmax temperature semantics (higher `τ` flattens distributions), and the hyperbolic adapter now delegates to `PoincareTransforms.ExpMapFromOrigin` rather than duplicating its own tanh squash.
- **`projects/VizApi/Program.cs` — graph binding now flows through the probe contract pipeline**: the old hard-coded metric switch was replaced by `adapter -> contract -> validator -> distance factory -> GraphBuilder`. This centralises manifold preparation and domain checks instead of scattering special-case metric handling through the request path.
- **`projects/VizApi/Program.cs` — native-manifold generators now bypass redundant ETL adaptation**: `HyperbolicBlobs`, `HyperbolicBlattHierarchy`, `Simplex`, and `GaussianManifold` are treated as already native to their paired geodesic spaces when the requested metric matches. VizApi now uses `IdentityAdapter` for those cases rather than re-embedding already-manifold-valued fixtures into the same manifold a second time.
- **`src/viz-core/schema_catalog.cs` — graph metric schema now exposes `Poincare` directly**: the shared graph metric dropdown schema now includes `Poincare`, so hyperbolic-native generators can be explored from the UI without special-case front-end wiring.
- **`projects/VizApi/Program.cs` + `record RegenRequest`** — request surface extended for hyperbolic hierarchy control: added `HierarchyPoints`, `HierarchyDepth`, `BranchesPerNode`, `BasePointsPerLeaf`, and `RadiusDecay` so the new hierarchy generator can be driven through the same regen contract as the existing synthetic fixtures.

## 2026-05-12 — VizApi: graph-builder controls, paired metric fixtures, GMM/UI cleanup

### Added

- **`projects/VizApi/Program.cs` + `src/viz-core/schema_catalog.cs` — paired generator fixtures for metric storytelling**: `HyperbolicBlobs`, `Simplex`, and `GaussianManifold` are now first-class VizApi generators with schema entries, request fields, and build/adapt dispatch. This gives the viewer native fixtures for `Poincare`, `FisherRaoSimplex`, and `FisherRaoHalfPlane` instead of forcing those geodesics onto mismatched crescent/Mobius scenes.
- **`projects/VizApi/Program.cs` + `src/viz-core/viewer.html` — graph-builder control surface exposed end-to-end**: `EnsureConnected` (shown in the UI as **MST Repair**), `Kernel`, `Bandwidth`, `GmmMode`, and `Poincare` metric dispatch are now part of the regen contract and viewer controls. The right panel can now drive the full graph-builder story: metric -> topology rule -> bandwidth estimation -> coupling kernel.
- **`src/viz-core/scene_renderer.cs` + `src/viz-core/serializer.cs` — scene titles preserved in render payloads**: `SceneDescriptor.Title` is now carried through `ScenePackage` JSON so generator-specific headers such as `Hyperbolic Blobs`, `Simplex (probability vectors)`, and `Gaussian Manifold (mu, log sigma)` survive serialization and appear in the viewer.

### Changed

- **`src/viz-core/viewer.html` — GMM controls and visibility model redesigned**: the old single ellipsoid toggle was replaced by a mode dropdown (`Oracle` / `EM`) plus independent `Surface` and `Wireframe` toggles. Both default off, wireframe-only is now a valid state, and Gaussian colors are coordinated with the fitted cluster rather than falling back to muddy crimson/additive blends.
- **`projects/VizApi/Program.cs` + `src/viz-core/adapter.cs` — legacy K=1 Gaussian layers removed from the base synthetic adapter**: the adapter no longer emits always-on analytic/best-fit Gaussian overlays. VizApi now emits exactly one user-selected Gaussian layer per regen, keyed by `GmmMode`, with oracle components inheriting the source cluster index and EM components mapped back to dominant GT cluster labels for consistent coloring.
- **`src/viz-core/viewer.html` — graph diagnostics upgraded from a terse spec string to an explanatory stats strip**: the legend/header now reports metric, neighborhood rule, kernel, bandwidth, MST repair, edge count, component count, and coupling-weight range. `Scalar` was renamed to **Color by**, and the legend now includes a cluster palette key plus an edges key explaining cluster-colored wires and crimson false bridges.
- **`src/viz-core/viewer.html` — regen wiring and flow-field rendering repaired**: graph controls (`rule`, `k`, `metric`, `epsilon`, `kernel`, `bandwidth`, `seed`, `MST Repair`, `GmmMode`) now trigger regeneration; rehydrated local-flow meshes are re-added to the scene; flow glyphs can render as `Beam` or `Cone` with `Beam` as the default; scalar recoloring now propagates to the flow overlay; and scalar-layer selection is preserved across regens.
- **`projects/VizApi/Program.cs` + `src/viz-core/scene_renderer.cs` — flow-state propagation now reaches the scene package**: `ShowFlow` is no longer dropped on the floor. VizApi now emits scene hints with the requested vector-field visibility and filters active vector-field layers accordingly, so the serialized scene matches the current flow toggle state.
- **`src/viz-core/viewer.html` + `projects/VizApi/Program.cs` — edge diagnostics and guard rails tightened**: the graph marquee now reports false-bridge counts and surfaces invalid-edge/self-loop counts when present, edge mesh construction skips malformed/self-loop entries instead of drawing them, and VizApi asserts that CSR edge packing fills the exact expected edge count before serializing the layer.
- **`src/viz-core/serializer.cs` — Gaussian rendering now tolerates non-3D covariance matrices**: 2D fitted covariances are embedded into the top-left of a 3x3 render covariance with a small epsilon pad on the remaining diagonal, so 2D fixtures such as `GaussianManifold` and `HyperbolicBlobs` render as thin discs instead of throwing on a hard-coded 3x3 Cholesky read.
- **`projects/DistanceMetrics/DistanceMetrics.csproj` + `projects/VizApi/Program.cs` — geodesic metrics fully wired for the viewer**: `Poincare` is now compiled into `DistanceMetrics`, dispatched by VizApi, and selectable in the viewer alongside `FisherRaoSimplex` and `FisherRaoHalfPlane`.
- **`projects/ProximityGraphs/ProximityGraphs.csproj` + `src/graphs/GraphBuilder.cs` + `src/synthetic/MobiusEllipsoid.cs` — build hygiene fixes for the viz stack**: restored compile includes for proximity-selection / MST-repair sources, fixed the nullable scratch-buffer warning in `GraphBuilder`, and corrected the stale XML doc reference in `MobiusEllipsoid`.

### Removed

- **`src/viz-core/adapter.cs` — automatic legacy Gaussian overlays**: the unconditional `GT Ellipsoids (analytic)` and `Best-Fit Gaussian` layers are no longer injected into every adapted synthetic scene; Gaussian overlays are now an explicit VizApi/viewer concern.

## 2026-05-12 — SPC: legacy implementation archived to .depr, rewrite baseline cleared

### Changed

- **`project.toml`** — updated the SPC inventory to reflect the post-eviction state: `src/clustering/spc/` is now an intentionally empty rewrite surface, `SpcCore` / `SpcThermo` / `SpcSynthetic` are marked `rewrite-pending` with no active source files yet, `.depr/` is recorded as a top-level path, and `.depr/spc/` is now explicitly tracked as a deprecated reference-only source root.

### Removed

- **Active legacy SPC source ownership from `project.toml`** — the inventory no longer claims that `src/clustering/spc/batch.cs`, `checkpoint.cs`, `graph.cs`, `potts.cs`, `synthetic.cs`, or `src/clustering/spc/thermo/` are current active sources, because the old implementation has been moved wholesale to `.depr/spc/`.

## 2026-05-12 — Docs: root project inventory, SPC renovation note, stale-reference cleanup

### Added

- **`project.toml`** — new root-level project inventory file. Declares the current project list, source roots, maintenance contract, and authority boundaries. Records that `src/clustering/spc/` is under renovation as SPC maturity work extracts primitives into domain projects and rewrites SPC as a consumer of primitives plus Potts/runtime and thermo residuals.

### Changed

- **`docs/project-primer.md`** — removed the embedded path/project inventory table and replaced it with a pointer to `project.toml` as the current inventory authority. Added explicit routine-maintenance guidance for keeping the inventory file up to date and a note that the current `src/clustering/spc/` tree is not the authority on target SPC architecture during the renovation.
- **`docs/spc-maturity.md`** — added a current-tree renovation note and updated stale path references from pre-reorg locations (`src/gmm/`, `src/spc.batch.cs`, `src/spc-thermo/`, `src/spc.thermo.cs`) to the current `src/clustering/gmm/` and `src/clustering/spc/` layout.
- **`docs/state-engine-design.md`** — updated stale references to the current checkpoint, batch, GMM, and thermo source paths after the SPC/GMM reorganization.
- **`docs/numerics-primitives.md`** — updated references to SPC thermo and batch sources to the current `src/clustering/spc/` layout.
- **`docs/visualization-engine.md`** — replaced the stale `IDistanceMetric` registry wording with the current `VizMetric` + VizApi dispatch description, and updated the Wing-2 flow note to reflect the current single-graph `GraphBuilder.Build` / `CsrGraph` pipeline rather than the old second-pass neighbor-selection description.

## 2026-05-12 — Hashish: IMeasure, IDivergence dispatch surfaces

### Added

- **`src/hashish/measure.cs`** — `IMeasure<T>` plus zero-state readonly-struct adapters over existing symmetric primitives: `LevenshteinMeasure : IMeasure<string?>`, `CosineVectorMeasure : IMeasure<double[]>`, `JaccardMeasure<T> : IMeasure<IEnumerable<T>>`, and `DiceMeasure<T> : IMeasure<IEnumerable<T>>`. The intent is typed caller-side dispatch without string-keyed metric switches: distance/similarity pairs stay attached to the same domain surface while reusing the existing static implementations.
- **`src/hashish/divergence.cs`** — `IDivergence<T>` for asymmetric pairwise comparisons with `Forward(a, b)` and a default `Symmetric(a, b) = Forward(a, b) + Forward(b, a)`. `KlDivergence : IDivergence<ReadOnlyMemory<double>>` wraps `KLDivergence` using `ReadOnlyMemory<double>` rather than spans so arrays, pooled buffers, and slices can be passed through the dispatch surface without copying.

### Changed

- **`src/hashish/levenshtein.cs`** — replaced tuple assignment on `ReadOnlySpan<char>` with an explicit temporary-variable swap when placing the shorter input on the column axis. This preserves the two-row DP behavior but avoids the ref-struct tuple restriction that broke the `Hashish` project build.

## 2026-05-12 — Hashish: KLDivergence, Histogram

### Added

- **`src/hashish/kl.cs`** — `KLDivergence`. `Forward(ReadOnlySpan<double> p, ReadOnlySpan<double> q, double eps)`: KL(P‖Q) in nats; explicit length guard (span walkstep from `p.Length`); eps floor on q for near-zero values. `Symmetric`: Jeffreys divergence KL(P‖Q) + KL(Q‖P). Lifted and span-ified from `SpcCore.SpcAnalysis`; thermo-specific wrappers (`ComputeFisherInT`, sweep analysis) remain in thermo. Caller note in header: pre-smooth sparse text distributions via `Histogram.Normalize(alpha > 0)` rather than relying on eps.
- **`src/hashish/histogram.cs`** — `Histogram`. `Normalize(ReadOnlySpan<int> counts, Span<double> output, double alpha)`: counts → PMF with Lidstone add-α smoothing; formula `(count[i] + α) / (Σ counts + α·|support|)`; explicit length guard; degenerate all-zero + α=0 case returns zero vector. `BuildUnigram(ReadOnlySpan<string> tokens, FrozenDictionary<string,int> vocab, Span<double> output, double alpha)`: text-native path, tokens not in vocab silently skipped, `ArrayPool<int>` for count scratch. Both methods have allocating convenience overloads. `FrozenDictionary` contract matches `CooccurrenceModel.TokenIndex` and `TfIdfModel` vocab maps directly.

---

## 2026-05-12 — Hashish: Levenshtein, SorensenDice; metrics doc annotations

### Added

- **`src/hashish/levenshtein.cs`** — `Levenshtein`. `Distance(ReadOnlySpan<char>, ReadOnlySpan<char>)` / `Distance(string?, string?)`: two-row DP with common prefix/suffix trimming, `stackalloc` rows up to 256 columns, `ArrayPool<int>` beyond. Shorter string placed on column axis to minimize row width. `Similarity(...)`: normalized to [0,1] as `1 − distance / max(lenA, lenB)`; returns 1.0 for both-empty.
- **`src/hashish/jaccard.cs` — `DiceSimilarity` / `DiceDistance` / `WordShingleDiceSimilarity`**: Sørensen–Dice coefficient `2|A∩B| / (|A|+|B|)` added to `JaccardContainment`. Reuses existing private `IntersectionCount` and `ToSet`. Mirrors the Jaccard/Containment/OverlapCoefficient API shape including a `WordShingler`-backed convenience overload.

### Changed

- **`src/metrics/*.cs` — length-validation doc annotations**: All 12 vector metrics annotated with a one-line `/// <summary>` explaining their length-mismatch behavior. Three categories: natural array-indexing throw (Euclidean, Manhattan, Minkowski, Canberra, Jaccard, Wasserstein), unhelpful span-bounds throw (Cosine via `TensorPrimitives`, JensenShannon via `q.AsSpan`), and explicit guard with rationale (Poincaré, Mahalanobis, FisherRaoHalfPlane, FisherRaoSimplex).
- **`src/metrics/FisherRaoSimplex.cs`** — added `p.Length != q.Length` guard; `TensorPrimitives.Multiply` slices both spans from `p.Length` so a shorter `q` previously threw inside `TensorPrimitives` with no call-site context.

---

## 2026-05-12 — Validation guards: TF-IDF dense matrix, TfIdfSearch, MstAugmented doc fix

### Changed

- **`src/hashish/tfidf.cs` — `TransformAll` overloads**: Added `Array.MaxLength` guard before dense matrix allocation in both the `IReadOnlyList<string>` and `TokenizedCorpus` overloads. Throws `InvalidOperationException` with a message directing callers to `TransformSparse` when `n × Dimension > Array.MaxLength`.
- **`src/hashish/tfidf.search.cs` — `ScoreQuery` / `NearestDocuments`**: Added `denseRows.Length % dim != 0` guard before dimension inference in both methods. Throws `ArgumentException` with the mismatched lengths if the flat row buffer is not a clean multiple of `model.Dimension`.
- **`src/graphs/construction/MstAugmented.cs` — `EnsureConnected` doc comment**: Corrected algorithm description from "O(N² log N) via Kruskal's" to the actual Borůvka-phases implementation — each phase is one O(N²) sweep finding the cheapest outgoing edge per component; typical MutualKnn graphs finish in 1–2 phases; exits immediately if already connected.

---

## 2026-05-12 — Hashish: TF-IDF, Co-occurrence, CosineVectors, WelfordMahal

### Added

- **`src/hashish/tfidf.cs`** — Full TF-IDF pipeline. `TfIdfOptions`: `TfVariant` (`Sublinear` default, sklearn-equivalent `1 + log(tf)`; or `Raw`), L2-normalize flag, `MinDf`/`MaxDfRatio` vocabulary pruning, alphabetical sort for cross-run determinism. `TokenizedCorpus` caches tokenization so repeated `Transform` calls avoid re-tokenizing. `TfIdfModel`: `Fit(docs)` (span-based DF path) and `Fit(TokenizedCorpus)` (skips re-tokenization); `Transform(doc)` / `Transform(ReadOnlySpan<string>)` / `TransformSparse(doc)` / `TransformAll(docs|corpus)`. `TfIdf` orchestrator: `Tokenize`, `FitTransform` (one-shot flat `double[N*Dim]` dense rows). Verified: 5-doc corpus, all rows L2-normalize to 1.0, IDF numerics match direct `InverseDocumentFrequency.Compute`.
- **`src/hashish/tfidf.search.cs`** — `TfIdfSearch`. `ScoreQuery(model, rows, query)`: sparse-query × dense-rows (walks only query nnz). `NearestDocuments(model, rows, docIndex, topK)`: dense × dense. Both use a bounded min-heap for O(N log K) top-K retrieval.
- **`src/hashish/cooc.cs`** — `CooccurrenceModel` + `CooccurrenceBuilder`. Two-pass windowed co-occurrence: pass 1 tokenizes corpus and builds a frequency-ranked vocabulary (`minTokenFrequency`, `maxVocabSize` pruning); pass 2 accumulates symmetric counts into a flat `int[]` (row-major vocab×vocab). `FrozenDictionary` token→index map after build. `Count(int,int)` / `Count(string,string)` pair lookups; `Row(int)` / `Row(string)` zero-copy span accessors over the flat array. Symmetric increments: `Count(a,b) == Count(b,a)`.
- **`src/hashish/cooc_stats.cs`** — `CooccurrenceStats`. `Pmi` / `Ppmi` (Church & Hanks 1990, natural log); `PpmiVector(tokenIndex)` and `PpmiMatrix()` for distributional semantic vectors (suitable for cosine distance matrix or SPC input); `ConditionalProbability`; `ContextualEntropy` (Shannon H, bits) and `NormalizedContextualEntropy` (H / log₂ V, range [0,1]); `TopContextNeighbors` (PPMI-ranked, for model inspection). Index and string overloads throughout.
- **`src/hashish/cos.cs`** — `CosineVectors`. `ReadOnlySpan<double>` throughout; backed by `TensorPrimitives`. `Similarity` (clamps to [−1,1], returns 0 for zero vectors); `Distance` (arccos/π, [0,1]); `DistanceNormalized` (dot-product fast path — only valid after `NormalizeInPlace`, skips norm computation); `NormalizeInPlace(Span<double>)`; `BuildDistanceMatrix(double[][])` — normalizes copies into an `ArrayPool` buffer, builds upper triangle, returns flat n×n `double[]` suitable for SPC distance matrix input.
- **`src/estimators/WelfordMahal.cs`** — `OnlineMahalanobis` (namespace `Estimators.Online`). Diagonal Mahalanobis via Welford's algorithm: per-dimension running mean + M2, O(D) memory, numerically stable single-pass. Streaming/large-D alternative to full-covariance batch methods; does not capture inter-dimensional correlations.

### Changed

- **`src/hashish/idf.cs` — `InverseDocumentFrequency.Compute`** rewritten: `Regex.EnumerateMatches` replaces per-doc `Split`; `Dictionary<,>.GetAlternateLookup<ReadOnlySpan<char>>` with a `(LastSeenDoc, Count)` per-token entry replaces per-doc `HashSet`. One string allocation per unique corpus token, zero per-match `Match` objects, no per-doc `HashSet` allocation. Same public signature; same numerics.
- **`src/hashish/bm25.cs` — `Bm25Stats.Compute`**: now a 4-line shim delegating to `InverseDocumentFrequency.Compute`. Duplicate `WordRegex` definition and corpus walk removed.
- **`src/hashish/tokenizer.cs`** — `WordRegex` promoted to `internal` so `idf.cs`, `cooc.cs`, and future Hashish primitives share the single compiled `Regex` instance.
- **`projects/Hashish/Hashish.csproj`** — `System.Numerics.Tensors 10.0.0` package reference added (required by `cos.cs`; was missing).

---

## 2026-05-12 — GMM–Viz Integration, ClusterCovariances Removal

### Added

- **`src/viz-core/gmm_adapter.cs`** — `GmmVizAdapter` static class (`Viz.Adapters.Gmm`). Five methods that turn a fitted `GaussianMixtureModel` into viz layer types: `ToGaussianLayer` (flattens components, optional merge-strategy map), `ToComponentLabels` (hard assignments → `LabelLayerKind.GmmComponent`), `ToClusterLabels` (strategy-remapped assignments → `LabelLayerKind.GmmCluster`), `ToResponsibilityScalar` (column slice of cached responsibility matrix → `ScalarLayerKind.Responsibility`), `ToMahalanobisScalar` (per-point min-over-k √Mahal → `ScalarLayerKind.MahalanobisDistance`). Fills all latent slots pre-declared in `viz_core.cs`.
- **`projects/VizCore/VizCore.csproj`** — `ProjectReference` to `GaussianMixture`; `gmm_adapter.cs` added to Compile items.

### Changed

- **`src/viz-core/adapter.cs` — `BuildBestFitGaussianLayer`**: Replaced stored-covariance lookup with a real 1-component GMM fit (`RobustInitialize` + `Fit`) per non-convex cluster (ArcGeometry, MobiusTubeGeometry). No longer depends on `SyntheticDataset.ClusterCovariances`. `ComputeClusterMean` deleted (mean now comes from `GaussianComponent.Mean`).
- **`projects/VizApi/Program.cs`**: Oracle overlay relabeled `"GMM (Oracle) K={K}"` (was `"GMM K={K}"`) to distinguish it from the fitted model. Second overlay `"GMM (EM) K={K}"` added: fits a real `GaussianMixtureModel` on the point cloud at the same K and renders it via `GmmVizAdapter.ToGaussianLayer`. `using Viz.Adapters.Gmm` and `using StatisticalEstimators` added.

### Removed

- **`SyntheticDataset.ClusterCovariances`** (`src/synthetic/SyntheticData.cs`) — property deleted.
- **`BuildCrescentApproxCovariance`** (`src/synthetic/CrescentEllipsoid.cs`) — private helper deleted; crescent covariance is now computed on-demand by a 1-component GMM fit.
- **`ApproximateMobiusCovariance`** (`src/synthetic/MobiusEllipsoid.cs`) — private helper deleted; same reason.
- `ClusterCovariances` population removed from `AnisotropicGaussian`, `CrescentEllipsoid`, `MobiusEllipsoid` generators.

---

## 2026-05-11 — GMM Maturity: Merge Strategies, BIC Sweep, Constrained EM, Primitives

### Added

- **`src/clustering/gmm/IResponsibilityConstraint.cs`**: Interface `void Apply(double[,] responsibilities, int n, int k, int iteration, int maxIterations)`. Blending rule: r̂ᵢₖ ← (1−λ)·r̂ᵢₖ + λ·sᵢₖ, then row-renormalise. Degenerate hard-label case is λ=1 with one-hot sᵢₖ.
- **`src/clustering/gmm/AnnealedSoftConstraint.cs`**: `IResponsibilityConstraint` implementation. Linear λ schedule: λ(t) = λ_start + (λ_end − λ_start)·t/T. Constructor copies + optionally row-normalises the supervisor confidence matrix. Zero-sum rows (supervisor abstains) are skipped. Allocation-free `Apply`. Diagnostic `LambdaAt(int, int)` helper.
- **`src/clustering/gmm/IComponentMergeStrategy.cs`**: Interface `int[] Merge(GaussianComponent[] components, double[,]? responsibilities = null)`. Output contract: dense cluster indices in `[0, clusterCount)`, ordered by first appearance. Responsibilities parameter required by entropy strategies, ignored by geometry strategies.
- **`src/clustering/gmm/ModalMergeStrategy.cs`**: `IComponentMergeStrategy` implementation. Ascends from each component mean via `ModeAscent.Ascend`; components whose converged modes fall within tolerance are assigned the same cluster. Adaptive default tolerance: 5% of mean pairwise component-mean distance (data-scale independent). Three intersecting ellipsoids (one density basin) → all means converge to one mode → `map = [0,0,0]` (one cluster, three-component sub-mixture).
- **`src/clustering/gmm/EntropyMergeStrategy.cs`** + **`MergeStep` record**: Greedy entropy-reduction merging (Baudry et al. 2010). At each step the pair with minimum ΔH is merged into the pooled responsibility matrix. O(K²·N) per step; no re-fitting. `MergeSequence()` returns the full K→1 sequence as `MergeStep[]` (ClusterCount, ClassificationEntropy, ComponentToClusterMap per level). `Merge()` cuts at `targetClusters`.
- **`src/clustering/gmm/KSweepResult.cs`**: Record: K, BIC, LogLikelihood, NumIterations, IsConverged, Model.
- **`src/clustering/gmm/BicKSweep.cs`**: `BicKSweep.Run(data, dimension, kMin, kMax, ...)` — Forgy-initialised EM per K, multi-restart, BIC = −2·logL + p·ln(N), p = K·(D + D(D+1)/2 + 1) − 1. Returns `KSweepResult[]` sorted by K. `BestByBic()` convenience selector.
- **`src/linalg/MatrixNorms.cs`**: `FrobeniusNorm(double[,])`, `FrobeniusDelta(double[,], double[,])` (scatter-delta stopping signal for coordinated expansion), `CopyTo(double[,], double[,])` (M-step snapshot helper).
- **`src/clustering/gmm/ModeAscent.cs`**: `Ascend(start, components, maxSteps, tol) → double[]` — gradient ascent with backtracking line search (30 halvings), log-space responsibilities, zero-allocation inner loop. `GetBasin(point, components) → int` — component index of the density basin containing a point (pattern-2 tiling stopping test).
- **`src/clustering/gmm/BhattacharyyaCoefficient.cs`**: `Between(a, b) → double` (BC = exp(−D_B)), `Distance(a, b) → double` (D_B with Mahalanobis + log-det terms, derives ln|Σ| from cached `LogNormalizationFactor`). Raw-array overloads for merge strategy implementations.

### Changed

- **`src/clustering/gmm/GaussianMixtureModel.cs`**: New `Fit(data, IResponsibilityConstraint, ...)` overload. `FitCore` accepts `IResponsibilityConstraint? constraint = null`; calls `constraint?.Apply(_responsibilities, n, K, iter, maxIterations)` after E-step. Existing unconstrained and hard-label callers unchanged.
- **`src/synthetic/CrescentEllipsoid.cs`** + **`MobiusEllipsoid.cs`**: `EllipsoidShellMode` enum (`Solid`, `Gaussian`, `Hollow`, `Annular`). New parameter on `GenerateCrescentAndEllipsoid` / `GenerateMobiusAndEllipsoid`. Sampling loop replaces old cbrt-CDF with mode switch.
- **`src/viz-core/schema_catalog.cs`**: `ellipsoidShellMode` enum dropdown added to `CrescentAndEllipsoidSchema` and `MobiusAndEllipsoidSchema`.
- **`projects/VizApi/Program.cs`**: `RegenRequest.EllipsoidShellMode` field; both `BuildCrescentAdapted` and `BuildMobiusAdapted` parse and forward to generators.

---

## 2026-05-11 — VizApi GraphBuilder Wiring, VizKernel Enum, Borůvka MST Repair

### Added

- **`src/viz-core/viz_core.cs` — `VizKernel` enum**: New `VizKernel { Gaussian, Cauchy, Laplacian, Linear }` enum. Mirrors `ProximityGraphs.KernelType` without a compile-time dependency on the ProximityGraphs assembly. Keeps VizCore self-contained.
- **`src/viz-core/schema_catalog.cs` — `kernel`/`bandwidth` in `GraphSection`**: `kernel` enum control (`Gaussian`, `Cauchy`, `Laplacian`, `Linear`) and `bandwidth` float slider (0–10, step 0.05, description "0 = auto-estimate") added to the shared `GraphSection`, which appears in all generator schemas.
- **`projects/VizApi/Program.cs` — `Kernel`/`Bandwidth` in `RegenRequest`**: New record fields `string Kernel = "Gaussian"` and `double Bandwidth = 0.0` (auto-estimate). Both echoed into `mergedParams` for round-trip.

### Changed

- **`projects/VizApi/Program.cs` — Single `GraphBuilder.Build` call replaces dual `SelectNeighbors`**: `BuildPackage` now calls `GraphBuilder.Build(n, dist, rule, k, ε, kernel, bandwidth, ensureConnected)` once, producing a `CsrGraph`. Edge layer and `LocalTangent` (Wing-2) both consume the same `CsrGraph` — eliminates the second O(N²k) neighbor selection pass that previously ran for flow computation. `LocalTangent` adjacency extracted from `CsrGraph.RowPointers`/`Targets` directly.
- **`projects/VizApi/Program.cs` — `BuildEdgeLayerFromCsr` replaces `BuildEdgeLayer`**: New helper serializes a `CsrGraph` into an `EdgeLayer`. Edge weights are now **coupling strengths in (0,1]** (high weight = close/similar = bright) instead of raw distances. Iterates upper triangle only to emit each undirected edge once. Old `SelectNeighbors` static helper removed.
- **`projects/VizApi/Program.cs` — `MstAugmented` rule mapped to `ensureConnected` flag**: `req.NeighborRule == "MstAugmented"` sets `ensureConnected: true` with `ProximityRule.Knn` base, matching the `GraphBuilder` API design (MstAugmented is not a peer topology type).
- **`src/graphs/construction/MstAugmented.cs` — Borůvka connectivity repair replaces Kruskal**: `EnsureConnected` now uses Borůvka phases instead of all-pairs Kruskal sort. Each phase is one O(N²) sweep finding the cheapest outgoing edge per component; skips intra-component pairs before calling `pairDistance`. Exits immediately if already connected (zero distance calls). Eliminates the `List<(int,int,double)>` of N²/2 tuples (~300 MB at N=5000), `HashSet<long>` dedup set, and O(N² log N) sort. Typical MutualKnn graphs (1–3 disconnected components) complete in 1–2 phases. Produces identical bridging edges to Kruskal for distinct edge weights (all continuous distance functions).
- **`src/graphs/GraphSelection.cs` — `DisjointSet` removed**: The `internal sealed class DisjointSet` was a duplicate of `UnionFind` without `Reset()` or `GetLabels()`. Removed; all callers (`EnsureConnected`) use `ProximityGraphs.UnionFind` directly.

## 2026-05-11 — Source Reorganization, Graph Primitives Extraction, FisherRao Split

### Added

- **`src/graphs/Edge.cs`** — `public struct Edge { int Source; int Target; double J; }` in namespace `ProximityGraphs`. Extracted from SpcCore; now a standalone graph primitive with no SPC dependency.
- **`src/graphs/tda/CsrGraph.cs`** — `public struct CsrGraph` in namespace `ProximityGraphs`. Symmetric CSR adjacency: `Targets[]`, `Weights[]`, `RowPointers[]`, `NodeCount`. `static CsrGraph FromEdges(Edge[], int)` — two-pass symmetric build (degree count → prefix sum → cursor fill). Extracted from SpcCore.
- **`src/graphs/tda/UnionFind.cs`** — `public sealed class UnionFind` in namespace `ProximityGraphs`. Path-compressed union-by-size. Methods: `Find(int)`, `Union(int, int)`, `Reset()` (in-place, no realloc), `GetLabels()` (root per node). Replaces both `FastUnionFind` (spc.potts.cs) and `DisjointSet` (GraphSelection.cs) as the single union-find implementation.
- **`src/graphs/GraphBuilder.cs`** — New `public static class GraphBuilder` in namespace `ProximityGraphs`. Three-phase parallel graph construction pipeline, independent of SPC. `Build(n, dist, rule, k, ε, kernel, bandwidth, ensureConnected) → CsrGraph`: Phase 1 (parallel, via construction methods) → optional `EnsureConnected` → Phase 2 (sequential bandwidth estimation + kernel weighting → `CsrGraph.FromEdges`). `ToGraph(NeighborSelection, kernel, bandwidth) → CsrGraph`: Phase 2 only, for retrying different kernels on the same topology. `Validate(CsrGraph) → GraphDiagnostics`: component count, largest component coverage, isolated node count via `UnionFind`. `ProximityRule { Knn, MutualKnn, EpsilonBall }` enum (MstAugmented removed — handled via `ensureConnected: bool`). `KernelType { Gaussian, Cauchy, Laplacian, Linear }` enum. `GraphDiagnostics` struct.
- **`src/graphs/construction/MstAugmented.cs`** — `ProximityGraph.SelectMstAugmented` shim (calls `SelectMutualKnn` + `EnsureConnected`) and `ProximityGraph.EnsureConnected(NeighborSelection, int, Func<int,int,double>) → NeighborSelection`. Topology-agnostic MST bridging; original selection unmodified.
- **`src/metrics/FisherRaoHalfPlane.cs`** — New `public static class FisherRaoHalfPlane`. Computes the exact Fisher-Rao geodesic for univariate Gaussians parameterized as `(μ, log_σ)` on the Poincaré half-plane: $d = \sqrt{2} \cdot \text{arccosh}(1 + (\mu_1-\mu_2)^2 + 2(\sigma_1-\sigma_2)^2) / (2\sigma_1\sigma_2))$. Recovers σ via `Math.Exp(log_σ)`. Appropriate for `GaussianManifold` features; distinct from the Bhattacharyya (discrete simplex) form.
- **`src/clustering/hdbscan/placeholder.cs`** — Placeholder for future HDBSCAN implementation under the `clustering/` namespace.

### Changed

- **Source tree reorganization**:
  - `src/graphs/construction/` — `Knn.cs`, `MutualKnn.cs`, `EpsilonBall.cs` moved here (were at `src/graphs/`)
  - `src/graphs/coupling/` — `BandwidthEstimation.cs`, `Gaussian.cs`, `Cauchy.cs`, `Laplacian.cs`, `Linear.cs` moved here (were at `src/coupling/`)
  - `src/clustering/spc/` — `spc.batch.cs`, `spc.checkpoint.cs`, `spc.graph.cs`, `spc.potts.cs`, `spc.synthetic.cs`, `spc.thermo.cs` moved here (were at `src/`)
  - `src/clustering/spc/thermo/` — `chi.cs`, `kl.cs`, `mhbs.cs` moved here (were at `src/spc-thermo/`)
  - `src/clustering/gmm/` — `GaussianComponent.cs`, `GaussianMixtureModel.cs` moved here (were at `src/gmm/`)
  - All `.csproj` `<Compile Include>` paths updated to match new locations.
- **`src/metrics/FisherRao.cs` → `FisherRaoSimplex`**: Class renamed from `FisherRao` to `FisherRaoSimplex`. Computes Bhattacharyya angle — correct for discrete probability mass vectors. All callers updated. `SpcMetric.FisherRaoSimplex` and `VizMetric.FisherRaoSimplex` added; `FisherRaoHalfPlane` added as separate enum value.
- **`src/clustering/spc/spc.graph.cs`** — Stripped of `Edge`, `CsrGraph`, `SelectNeighbors`, `ConvertToCoupling`, `BuildGraphFromFeatures`. `BuildGraphFromMetric` now returns `CsrGraph` (was `Edge[]`). `BuildFeatureMetricGraph`, `BuildMahalanobisMetricGraph`, `BuildGraphFromSimHashes` all delegate to `GraphBuilder.Build(...)`. `SpcToProximityRule` maps `MstAugmented → MutualKnn`; `SpcToEnsureConnected` returns `true` for `MstAugmented`. `ValidateGraph` delegates to `GraphBuilder.Validate`.
- **`src/clustering/spc/spc.potts.cs`** — `FastUnionFind` class removed; replaced with `new UnionFind(N)` from `ProximityGraphs`.
- **`src/clustering/spc/spc.batch.cs`** — `RunSimulationCore` signature changed from `(Edge[] edges, ...)` to `(CsrGraph graph, ...)`. `result.Graph = graph` directly (no longer calls `CsrGraph.FromEdges`).
- **`projects/VizApi/Program.cs`** — `FisherRaoSimplex`/`FisherRaoHalfPlane` metric dispatch cases added; `RowOf(features, d, i)` helper added for metrics that require `double[]` row extraction. `using DistanceMetrics;` added. `DistanceMetrics` project reference added to `VizApi.csproj`.
- **`src/viz-core/schema_catalog.cs`** — `FisherRaoSimplex`/`FisherRaoHalfPlane` added to metric enum values in `GraphSection`.

## 2026-05-11 — Coupling Kernel Bandwidth Estimation, IRLS Weight Capture Fix

### Added

- **`src/coupling/BandwidthEstimation.cs`**: New `CouplingKernels.BandwidthEstimation` static class. Two-layer design: per-kernel routes (`ForGaussian`, `ForLaplacian`, `ForCauchy`, `ForLinear`) apply the natural estimator + consistency factor for each kernel; kernel-agnostic primitives (`Mean`, `Median`, `Mad`, `Max`) exposed for custom routing. Consistency factors: `GaussianFactor = 1.4826` (1/Φ⁻¹(0.75)), `LaplacianFactor = 1.4427` (1/ln(2)), `CauchyFactor = 1.0` (MAD of Cauchy(0,γ) = γ exactly). `ForLinear` uses max (compact support — δ should span the neighbourhood). `Mad` has two overloads: caller-supplied location and internally computed sample median. All sort-based methods take a caller-supplied scratch span. `DeltaMuMean`, `DeltaMuMedian`, and `SigmaEstimatorMad` from `.depr` are now superseded by this file.

### Fixed

- **`src/optimization/irls.cs` — IRLS weight capture timing**: After the main iteration loop, weights are now recomputed at the final converged `destination` position before being published to `finalIrlsWeights`. Previously, `wirls` reflected distances from the pre-update position of the last iteration step rather than the converged location — causing scatter matrices to be computed at a subtly stale position. Behaviour now matches `EuclideanMedian.IrlsLoop` which recomputes weights post-convergence. The singularity/regularise branch is mirrored identically in the recompute pass.

## 2026-05-11 — Interactive Gen Panel, FigureEight Möbius Spine, Colour & Default Rendering

### Added

- **`src/synthetic/MobiusEllipsoid.cs` — `MobiusSpineShape` enum + FigureEight variant**: New `MobiusSpineShape { Circle, FigureEight }` enum. `FigureEight` uses Lissajous parametrization `cx=R·sinθ, cy=R/2·sin2θ`. Frenet frame: `T=(cosθ, cos2θ)/‖·‖`, `N=cross(ẑ,T)=(-Ty,Tx,0)`. Half-twist applied identically to circle case so Möbius monodromy is preserved. Speed `‖Ṫ‖>0` everywhere (cos θ=0 and cos 2θ=0 cannot coincide). Width splay: `effHalfWidth = halfWidth·effBias·(1−splayFactor·(1−|sinθ|))` — full width at loop extremes, narrowed at center crossing.
- **`src/synthetic/MobiusEllipsoid.cs` — `MobiusPlacement.CenterCrossing`**: New enum value representing the figure-eight self-intersection at the origin — maximum topological stress point for the bow-tie shape.
- **`src/synthetic/MobiusEllipsoid.cs` — `spineShape`/`splayFactor` params**: New optional params `MobiusSpineShape spineShape = MobiusSpineShape.FigureEight` and `double splayFactor = 0.7` added to `GenerateMobiusAndEllipsoid`. Echoed to `parameters` dict as `spineShape` and `splayFactor`.
- **`src/synthetic/MobiusEllipsoid.cs` — shape-aware placement helpers**: `ResolveMobiusEllipsoidCenter` now accepts `MobiusSpineShape spineShape`. For `FigureEight`, `OrthogonalCenterCross` maps to crossing origin `(0,0,z)` instead of left-lobe tip `(-R,0,z)`. `PeripheralElbow` maps to the Y-extreme of the right lobe `(R/√2, R/2, ·)`. `ApproximateMobiusCovariance` accepts optional `spineShape`: for `FigureEight` returns anisotropic `diag(R²/2+varZ, R²/8+varZ, varZ)` reflecting the asymmetric loop reach.
- **`projects/VizApi/Program.cs` — `SpineShape`/`SplayFactor` in `RegenRequest`**: New record fields `string SpineShape = "FigureEight"` and `double SplayFactor = 0.7`. Parsed via `Enum.TryParse<MobiusSpineShape>` and forwarded to `GenerateMobiusAndEllipsoid`.
- **`src/viz-core/schema_catalog.cs` — `spineShape`/`splayFactor` schema entries**: `spineShape` enum (`Circle`, `FigureEight`) and `splayFactor` float (0–1, step 0.05) added to Möbius params section after `radialBias`. `CenterCrossing` added to Möbius placement enum values.

### Changed

- **`src/viz-core/viewer.html` — interactive gen panel auto-regen**: `doRegen()` extracted as a named function. `doRegenDebounced()` wrapper (380 ms) added for continuous inputs. Event listeners added per control type: `int` fields fire `doRegen` on `Enter` keydown; `bool` checkboxes and `enum` dropdowns fire `doRegen` on `change`; `float` range sliders and `vec3` triples call `doRegenDebounced` on `input`. The Regenerate button still fires `doRegen` directly.
- **`src/viz-core/viewer.html` — graph wire colour**: Wire tint now mixes cluster hue 35% with white 65% before scaling to 0.42 brightness (was `clusterColor × 0.18`). Pastel/chalky wires remain in the same hue family as the point cloud but are visually distinct from the fully saturated point colours.
- **`projects/VizApi/Program.cs` — default `RegenRequest` values**:
  - `CrescentPoints`: 300 → 10 000
  - `MobiusPoints`: 400 → 10 000
  - `EllipsoidPoints`: 180 → 5 000
  - `CrossSection`: `Ribbon` → `Annular`
  - `SpineShape`: `Circle` → `FigureEight`
  - `Placement` (record default): `NearOpenFace` → `OrthogonalElbowIntersect`
  - Crescent builder fallback: `NearOpenFace` → `OrthogonalElbowIntersect`
  - Möbius builder fallback: `NearSeam` → `CenterCrossing`
- **`src/viz-core/scene_renderer.cs` — `ShowGaussianEllipsoids` default**: `true` → `false`. Ellipsoids are hidden on initial load; user explicitly enables them via the Ellipsoids toggle.

## 2026-05-10 — Pass 7: VectorFieldLayer — Wing-2 Empirical Local Tangent Flow

### Added

- **`src/estimators/LocalTangent.cs`** — New static class `Estimators.Tangent.LocalTangent`. Computes per-point unit tangent vectors via local PCA over k-NN neighbourhoods. Algorithm per point: (1) arithmetic mean of k neighbours, (2) centred D×D scatter matrix, (3) power iteration (20 steps) → leading eigenvector. Returns flat `double[N×D]`. Points with fewer than `minNeighbors` (default 3) neighbours receive a zero vector. All scratch buffers rented from `ArrayPool<double>.Shared` — zero-alloc hot path. Wing-2 (Diagnostic TDA) flow source; air-gapped from generator internals.
- **`src/viz-core/viz_core.cs` — `VectorFieldLayer`**: New `sealed class VectorFieldLayer : INamedLayer`. Properties: `Name`, `Vectors` (`ReadOnlyMemory<double>`, N×D flat), `N`, `D`. Optional `IReadOnlyList<VectorFieldLayer>? vectorFieldLayers = null` parameter added to `VizDataset` constructor (defaults to `Array.Empty<VectorFieldLayer>()`). `VectorFieldLayers` property on `VizDataset`.
- **`src/viz-core/scene_renderer.cs` — `ShowVectorField` hint + `ActiveVectorFieldLayers`**: `SceneRenderHints.ShowVectorField` bool property (default `false`). `ScenePackage.ActiveVectorFieldLayers` property; passed through from `dataset.VectorFieldLayers` in `SceneBuilder.Build` (no filter — all VectorFieldLayers are always active).
- **`src/viz-core/serializer.cs` — `VectorFieldLayerJson`**: New internal DTO: `name`, `n`, `d`, `vectors double[]`. `ScenePackageJson` gains `[JsonPropertyName("vector_field_layers")] public List<VectorFieldLayerJson> VectorFieldLayers`. `SerializeVectorFieldLayers` method added. `SceneRenderHintsJson` gains `[JsonPropertyName("show_vector_field")] public bool ShowVectorField`. `SerializeHints` updated accordingly.
- **`src/viz-core/schema_catalog.cs` — `showFlow` in `OverlaySection`**: `new() { Key = "showFlow", Label = "flow", Type = "bool" }` appended to `OverlaySection.Params`. Appears in all generator schemas automatically.
- **`projects/VizApi/VizApi.csproj`**: `<ProjectReference Include="..\StatisticalEstimators\StatisticalEstimators.csproj" />` added so `LocalTangent` is reachable from VizApi.
- **`projects/VizApi/Program.cs` — LocalTangent wiring**:
  - `using Estimators.Tangent;` added.
  - `ShowFlow bool = false` added to `RegenRequest`; echoed into `mergedParams["showFlow"]`.
  - `SelectNeighbors(n, dist, proximity)` — new private static helper that extracts the `ProximityGraph.Select*` dispatch previously inlined in `BuildEdgeLayer`. `BuildEdgeLayer` now calls `SelectNeighbors` internally (no signature change).
  - In `BuildPackage`: after edge construction, calls `SelectNeighbors` a second time with the same `spec` to obtain adjacency for LocalTangent (same O(n²k) cost). Builds `double[][] points2d` and `int[][] adjacency` from the selection; calls `LocalTangent.Compute(points2d, adjacency)`. Constructs `VectorFieldLayer("Local PCA Flow", tangentVectors, n, Math.Min(d, 3))`. Passes `vectorFieldLayers` list to `VizDataset`.
- **`src/viz-core/viewer.html` — `buildFlowField` + flow field rendering (Pass 7)**:
  - Module-level `buildFlowField(layer, cloudRadius)` function. Renders an `InstancedMesh` of `THREE.ConeGeometry(r, h, 6)` cones (r = 2.5% of cloud radius, h = 10%). Per-instance `Matrix4` via `Quaternion.setFromUnitVectors(Y_axis, tangent)`. Zero-vector instances get a zero-scale matrix (invisible). Per-instance color from `PALETTE` via cluster label. Color stored in `InstancedBufferAttribute`.
  - Module-level `_flowMeshes` array; `_tmpM_zero` helper for zero-scale matrix.
  - Initial render block reads `SCENE.vector_field_layers`; sets `mesh.visible = SCENE.hints.show_vector_field ?? false`.
  - `#chk-flow` checkbox in the controls panel ("Flow field"); initial state from `SCENE.hints.show_vector_field`. Event handler: Tier-1 `.visible` flip on all `_flowMeshes`.
  - `rehydrateFromJson`: disposes and clears `_flowMeshes` (geometry + material dispose); rebuilds from `newScene.vector_field_layers` with `buildFlowField`; respects current checkbox state.

## 2026-05-10 — GMM Overlay Architectural Generalization

### Changed

- **`src/viz-core/viz_core.cs` — `SpineLayer.TypicalScale`**: New `double` property (default `0`) carrying the manifold cross-section radius as known to the generator — e.g. `crescentWidth` for arcs, `√(halfWidth × halfThickness)` for Möbius tubes. Zero means unknown; overlays fall back to a step-size estimate. Added as optional `typicalScale = 0.0` parameter to the constructor so all existing call sites compile unchanged.
- **`src/viz-core/adapter.cs` — `BuildSpineLayers`**: Propagates `TypicalScale` from each geometry type: `ArcGeometry → arc.NoiseScale`; `MobiusTubeGeometry → Math.Sqrt(mob.HalfWidth * mob.HalfThickness)`; `ManifoldGeometry → 0.0`.
- **`projects/VizApi/Program.cs` — two-phase refactor**: `BuildCrescentPackage` and `BuildMobiusPackage` replaced by:
  - `BuildCrescentAdapted(req)` / `BuildMobiusAdapted(req)` — Phase 1: pure generator-specific synthesis + adaptation; return `(VizDataset, title)` tuple.
  - `BuildPackage(req)` — Phase 2 orchestrator: dispatches Phase 1, then runs the shared pipeline (edges, GMM overlays, `mergedParams`, scene construction) for every generator.
- **`projects/VizApi/Program.cs` — `BuildGmmOverlaysFromSpines`**: New generator-agnostic static helper. Iterates all `SpineLayers`, computes `arcLength` from consecutive-sample distances, then `sigmaLong = arcLength / K` and `sigmaPerp = TypicalScale × 0.9` (or `stepSize × 2` if `TypicalScale == 0`). Calls `BuildGmmOverlayLayer` per spine and yields the resulting `GaussianLayer`. Works identically for crescent arcs and Möbius tubes without any knowledge of generator params.
- **`src/viz-core/schema_catalog.cs` — `OverlaySection`**: New shared `ParamSection` containing the `gmmComponents` slider (`DisplayValues=["1","2","4","8","16"]`). Appended to both `CrescentAndEllipsoidSchema` and `MobiusAndEllipsoidSchema`. `gmmComponents` removed from the Crescent-specific section. Adding a future generator requires only one `Build*Adapted` function + including `OverlaySection`.

## 2026-05-10 — GMM K-component Slider (Recursive GMM Reframe)

### Changed

- **`projects/VizApi/Program.cs` — `BuildRecursiveGmmLayer` → `BuildGmmOverlayLayer`**: Renamed to reflect the unified concept. Added `offset` parameter to centre each component within its arc segment (pass `stride/2`). Layer name is now `"GMM K={K}"` (was `"Recursive GMM"`).
- **`projects/VizApi/Program.cs` — `RegenRequest`: `RecursiveSplits` → `GmmComponents`**: Slider position 0–4 maps to `K = 2ᵇᵒˢ = 1, 2, 4, 8, 16`. `K=1` default (single global component, same conceptual level as the analytic ellipsoid). `sigmaLong = crescentRadius × arcHalfAngle / K` (one arc-segment per component); `sigmaPerp = crescentWidth × 0.9` (fixed to tube cross-section). Echo key renamed to `gmmComponents`.
- **`src/viz-core/schema_catalog.cs` — `recursiveSplits` → `gmmComponents`**: Slider `Min=0, Max=4, Step=1`, `DisplayValues=["1","2","4","8","16"]` so the panel shows component count rather than raw position. Label changed to "GMM K".
- **`src/viz-core/param_schema.cs` — `DisplayValues`**: New optional `string[]` property on `ParamSpec` (`[JsonPropertyName("display_values")]`). When present, the viewer uses `display_values[round(pos)]` as the label for a float-range slider instead of the numeric value.
- **`src/viz-core/viewer.html` — `buildParamControl` float branch**: `fmtVal` helper now checks `param.display_values` first, falls back to integer rounding if `step ≥ 1`, else `toFixed(2)`.
- **`src/viz-core/viewer.html` — `buildGaussianGroup` dim logic**: `isRecursive` flag replaced by `gmmK = parseInt(layer.name.match(/GMM K=(\d+)/))`. `dimGmm = gmmK > 1` drives reduced opacity (`0.10`) and grey wireframe for multi-component layers; K=1 renders at full opacity with cluster color, identical to the analytic ellipsoid layer.

## 2026-05-10 — Recursive GMM Layer, Wireframe Overlay, Crescent/Möbius Generator Geometry Fixes

### Added

- **`projects/VizApi/Program.cs` — `BuildRecursiveGmmLayer`**: Static helper that tiles ellipsoidal Gaussians along a crescent arc spine. Walks `SpineLayer.SpineSamples` (crescent cluster, `ClusterIdx == 0`) at stride `M/16` (≈16 components). At each sample computes a local orthonormal frame — T = normalised spine tangent, N = T⊥ projected into XY (in-plane normal), B ≈ Z (out-of-plane) — and builds a `3×3` covariance `R · diag(σL², σP², σP²) · Rᵀ` where `σL = crescentWidth × 2.5` (along arc) and `σP = crescentWidth × 0.9` (cross-plane). Layer emitted as `GaussianLayer("Recursive GMM", ...)` and appended after the analytic layers in `BuildCrescentPackage`. C# does all the frame math; JS reads the pre-baked flat `cholesky_l` buffer like any other gaussian layer.
- **`viewer.html` — wireframe overlay geometry**: `UNIT_SPHERE_EDGES = new THREE.EdgesGeometry(UNIT_SPHERE_GEO)` declared alongside the shared solid sphere geometry. `buildGaussianGroup` now adds a `THREE.LineSegments(UNIT_SPHERE_EDGES, ...)` sibling to every solid ellipsoid mesh. Wireframes share the same `Matrix4` as the corresponding solid mesh (applied once, `matrixAutoUpdate = false`). Tagged `userData.isWireframe = true` for selective toggling. Visible = `false` by default.
- **`viewer.html` — "Wireframes" checkbox** (`#chk-wireframe`): Added to the right panel below "Ellipsoids". `chkWireframe` event handler traverses all `gaussianMeshGroups` and sets `.visible` on every child with `userData.isWireframe = true`. Independent of the solid-fill toggle.
- **`viewer.html` — Recursive layer dim**: `buildGaussianGroup` checks `layer.name.toLowerCase().includes('recursive')` once per layer (outside the per-component loop). If true, solid-fill opacity is dimmed to `0.10` (vs `0.22` default) and wireframe color is set to neutral grey `0xaaaaaa` so the tiled ellipsoids do not overwhelm the scene.

### Changed — Generator geometry

- **`src/synthetic/CrescentEllipsoid.cs` — generation loop (Gaussian density bias + cosine taper)**: Replaced uniform angle sampling (`rng.NextDouble()`) with Gaussian bias (`u = N(0, 0.35)`, clamped to `[−0.5, 0.5]`). This concentrates mass at the elbow (u≈0 → angle≈π) and naturally thins the tails. Added cosine taper: `localWidth = crescentWidth × cos(u × π)`, so radial and Z noise scale from full width at the elbow to zero at the tips. Tangential noise is fixed at `crescentWidth × 0.12` (very small; prevents tangential smearing). Produces a meaty, dense elbow with sharp tapering tips instead of a uniform tube.
- **`src/synthetic/CrescentEllipsoid.cs` — default parameters**: `crescentWidth` `0.35 → 0.40` (thicker elbow); `arcHalfAngle` `0.72 → 2.04` rad (total arc `~117° → ~234°`, a C-shape instead of a half-circle).
- **`src/synthetic/CrescentEllipsoid.cs` — `ellipsoidAxes` default**: `[0.6, 1.1, 0.4]` (Y-major) → `[3.0, 0.5, 0.5]` (**X-major**). All preset rotations below are calibrated for X being the long axis.
- **`src/synthetic/CrescentEllipsoid.cs` — `NearOpenFace` center**: Was a hardcoded offset near `(0.45, −2.7, 0.9)` (in front of the closed end, off-geometry). Now `(0 + shift, 0, depth)` — the geometric centre of the crescent hollow, equidistant from all arc points and strictly inside the open face of the C.
- **`src/synthetic/CrescentEllipsoid.cs` — preset rotations (X-major)**: `OrthogonalElbowIntersect` → `Ry(π/2)` (X→−Z, stabs through crescent plane at elbow). `IntersectUpperTip` → `Rz(π − φ)` (X points to outward radial at upper tip). `IntersectLowerTip` → `Rz(π + φ)`. `NearOpenFace` → `Rz(π/2)` (X→Y, long axis spans gap between tips).
- **`src/synthetic/MobiusEllipsoid.cs` — `ellipsoidAxes` default**: `[0.6, 1.0, 0.45]` → `[3.0, 0.5, 0.5]` (X-major; consistent with crescent convention).
- **`src/synthetic/MobiusEllipsoid.cs` — `NearSeam` center**: Was `R + gap + shift, 0, 0.6 + depth` (outside the spine). Now `R + shift, 0, depth` — centred on the spine at θ=0 so the ellipsoid stabs through the ribbon rather than sitting beside it.
- **`src/synthetic/MobiusEllipsoid.cs` — preset rotations (X-major)**: `NearSeam` → `Ry(π/2)` (at θ=0 the ribbon is horizontal; B=Z is the face normal; X pierces through). `OrthogonalCenterCross` → identity (at θ=π the strip has twisted 90°; B=X is the face normal; X-major already aligned). `PeripheralElbow` → `Rz(¾π)` (X→outward radial at θ≈¾π). Default fallback → `Ry(π/2)`.
- **`src/viz-core/schema_catalog.cs`**: Crescent `arcHalfAngle` `Max 1.45 → 3.1` (allows full 234° arc); crescent `ellipsoidAxes` `Max 3.0 → 5.0`.
- **`projects/VizApi/Program.cs` — `RegenRequest` defaults**: `CrescentWidth` `0.35 → 0.40`; `ArcHalfAngle` `0.72 → 2.04`.
- **`projects/VizApi/Program.cs` — `using System.Linq`**: Added to support `FirstOrDefault` on `IReadOnlyList<SpineLayer>` in the recursive GMM builder call site.

## 2026-05-09 — MobiusAndEllipsoid Generator + Schema-Driven Regen Panel

### Added (Generator picker)

- **`src/viz-core/schema_catalog.cs` — `KnownGenerators`**: New `public static readonly string[] KnownGenerators` listing all registered generators in display order (`"CrescentAndEllipsoid"`, `"MobiusAndEllipsoid"`). Single authoritative list; must stay in sync with the `BuildPackage` switch in `VizApi/Program.cs`. Adding a new generator requires one new entry here and one new `BuildXxxPackage` case.
- **`serializer.cs` — `generator_catalog` on `ScenePackageJson`**: `GeneratorCatalog` (`"generator_catalog"`) emitted from `SchemaCatalog.KnownGenerators` on every render. Every HTML page bakes the full generator list into its JSON payload; no server round-trip needed to populate the picker.
- **`viewer.html` — `<select id="sel-generator">` picker**: Added to the generator panel header alongside the hide/show toggle. Populated at load from `SCENE.generator_catalog`. Selecting a different generator immediately posts `{ generator: name }` to `POST /api/regen` (C# `RegenRequest` record defaults fill all other fields), then calls `rehydrateFromJson` on the response.
- **`viewer.html` — `rebuildGenPanel(gp, schema)`**: Extracted from the old `initGenPanel` IIFE. Clears `gen-body` and rebuilds all section headers, parameter controls, and the Regenerate button from a supplied `gp` + `schema`. Called at load and from `rehydrateFromJson` after every generator switch.
- **`viewer.html` — `currentGenSchema` module-scope variable**: Tracks the schema of the currently active generator; updated by every `rebuildGenPanel` call. `harvestParams(schema)` reads it (parameter removed from call site) and reads `sel-generator` for the discriminator value.
- **`viewer.html` — `rehydrateFromJson` generator sync**: After rebuilding geometry layers, rehydrate now syncs `sel-generator.value` to `newScene.generator_params.generator` and calls `rebuildGenPanel` with the new schema. Switching generator → Regen → tweak params → Regen now works correctly end-to-end.

### Fixed

- **`viewer.html` — duplicate `const PALETTE` (ES module SyntaxError)**: The 3-stop scalar diverging palette introduced in Pass 5 was declared `const PALETTE`, colliding with the 12-color cluster palette declared at module scope. ES modules are always strict-mode; a duplicate `const` binding in the same scope is a `SyntaxError` that kills the entire module — all smoke scenes rendered black (canvas blank, only static HTML controls visible). Renamed to `const SCALAR_PALETTE` in the declaration and both references inside `scalarToRgb`. All 8 smoke scenes now render correctly.

### Added

- **`src/synthetic/MobiusEllipsoid.cs`**: New `SyntheticData.GenerateMobiusAndEllipsoid` generator. Cluster 0 is a solid Möbius tube sampled from a twisted slab swept around a spine circle of configurable radius; Cluster 1 is an anisotropic ellipsoid whose placement relative to the tube is controlled by `MobiusPlacement` (`NearSeam`, `OrthogonalCenterCross`, `PeripheralElbow`, `Manual`). Frame math: `T = [−sin θ, cos θ, 0]`, `N = [cos(½θ)·cos θ, cos(½θ)·sin θ, sin(½θ)]`, `B = [−sin(½θ)·cos θ, −sin(½θ)·sin θ, cos(½θ)]` — orthonormal with correct Möbius monodromy (frame flips 180° over one circuit). Cross-section shape selectable via `TubeCrossSection` (`GaussianIsotropic`, `GaussianAnisotropic`, `UniformDisk`, `Annular`). Optional 4D lift via `Project4DTo3D` (rotation in x₃–x₄ plane, useful for verifying metric robustness in 4D). θ-density bias peaks at seam (θ=0) and apex (θ=π). `MobiusTubeGeometry : ClusterGeometry` DTO carries `SpineSamples`, `LocalFrames` (M×3×3 [T,N,B]), `SpineRadius`, `HalfWidth`, `HalfThickness`, `TwistCount`, `CrossSection`, `RadialBias`. Covariance approximated analytically by `ApproximateMobiusCovariance`.
- **`SpineLayerKind.MobiusTube`** (`src/viz-core/viz_core.cs`): Third discriminant added to the `SpineLayerKind` enum alongside `Arc` and `Manifold`. `SpineLayer` with `kind = MobiusTube` carries `TangentBases` (M×3×3 local frames [T,N,B] from the generator).
- **`src/viz-core/param_schema.cs`**: `ParamSpec`, `ParamSection`, `GeneratorParamSchema` types. A `GeneratorParamSchema` describes all interactive controls for a generator (type, label, min/max/step for sliders, enum values for dropdowns, vec3 component labels). Emitted as `SCENE.generator_param_schema` in the baked JSON; the viewer builds its regen panel from this data — no hardcoded HTML per generator.
- **`src/viz-core/schema_catalog.cs`**: `SchemaCatalog.ForGenerator(name)` static lookup. Currently registers `CrescentAndEllipsoid` and `MobiusAndEllipsoid`. Adding a new generator requires only one new `ParamSection[]` constant and one new `case` in the `switch` — no viewer changes needed.
- **`VizApi` Möbius dispatch**: `BuildMobiusPackage(RegenRequest)` added to `projects/VizApi/Program.cs`. `BuildPackage` now dispatches on `req.Generator` (`MobiusAndEllipsoid` → Möbius builder, default → crescent builder). `RegenRequest` gains a `Generator` discriminator field and all Möbius-specific fields (`MobiusPoints`, `SpineRadius`, `HalfWidth`, `HalfThickness`, `NoiseSigma`, `TwistCount`, `RadialBias`, `CrossSection`); crescent and shared fields retained with backward-compatible defaults.
- **VizCoreSmoke expanded** (`projects/VizCoreSmoke/Program.cs`): 7 Möbius smoke scenes added (`NearSeam` 3D, `OrthogonalCenterCross` 3D, `PeripheralElbow` 3D, `OrthogonalCenterCross` 4D projected, plus `UniformDisk`/`Annular`/`GaussianAnisotropic` cross-section variants). All 8 total scenes (1 crescent + 7 Möbius) render successfully to `~/viz-smoke-*.html`.

### Changed

- **`adapter.cs` — `MobiusTubeGeometry` case**: `BuildSpineLayers` extended with `case MobiusTubeGeometry mob:` — produces a `SpineLayer` with `kind = MobiusTube`, passes `mob.LocalFrames` as `TangentBases`. `BuildBestFitGaussianLayer` extended to exclude `MobiusTubeGeometry` from the non-convex cluster skip list.
- **`adapter.cs` — `SchemaCatalog` lookup**: `Adapt` now extracts the `"generator"` key from `source.Parameters`, calls `SchemaCatalog.ForGenerator(generatorName)`, and passes the result to `VizDataset` as `generatorParamSchema`. Schema attaches automatically for any registered generator.
- **`VizDataset` / `ScenePackage`**: Each gains a `GeneratorParamSchema?` property threaded from `adapter.cs` → `SceneBuilder.Build` → `serializer.cs` (`"generator_param_schema"` JSON key, `[JsonIgnoreCondition.WhenWritingNull]`).
- **`viewer.html` — schema-driven regen panel**: `initGenPanel()` replaced with a schema-driven builder. Reads `SCENE.generator_param_schema`; builds all controls dynamically via `buildParamControl(param, currentValues)` (handles `int`, `float`, `bool`, `enum`, `vec3` types). `harvestParams(schema, generator)` collects values generically by walking the schema. `#gen-body` HTML is now an empty div — all controls are DOM-created at runtime. Works for any registered generator with zero viewer changes.

### Added (Pass 5)

- **`serializer.cs` — `min`/`max` on `ScalarLayerJson`**: `SerializeScalarLayers` now scans `layer.Values.Span` to pre-bake `Min` and `Max` per layer. C# does the scan; JS reads two scalar fields — no array scan in the browser.
- **`viewer.html` — `scalarToRgb(t)` + `PALETTE`**: 3-stop diverging palette blue→white→red (stops at t=0/0.5/1, `#3288bd`/`#ffffff`/`#d53e4f`). Linear interpolation between adjacent stops by reading pre-baked `layer.min`/`layer.max`. No math beyond a lerp per channel (index lookup + subtraction/multiply).
- **`viewer.html` — `applyScalarLayer(layer)`**: Writes into the existing point cloud `color` `BufferAttribute` in-place (Tier 2: O(N) once on selection change, `needsUpdate = true`). Passing `null` restores GT label palette.
- **`viewer.html` — "Scalar" selector** (`#sel-scalarlayer`): Dropdown populated at load from `SCENE.scalar_layers`. Selecting an entry calls `applyScalarLayer` + `buildLegend`. Rehydrate clears and repopulates the selector, resets to `null` (label colors).
- **`viewer.html` — legend scalar mode**: `buildLegend` short-circuits when `activeScalarLayer !== null`: renders a CSS gradient bar (blue→white→red) with min/max labels instead of the cluster swatch list.

### Added (Pass 3)

- **`src/linalg/Cholesky.cs` — `WriteLTo(Span<double> dst)`**: New method on `CholeskyDecomposition` that writes the lower-triangular factor L row-major into a caller-supplied span (length dim×dim). Mirrors the existing `WriteInverseTo` pattern.
- **`serializer.cs` — `cholesky_l` field on `GaussianLayerJson`**: `SerializeGaussianLayers` now calls `ComputeCholeskiFactors(layer)` to pre-bake the Cholesky factor per component into a flat `[K×3×3]` array emitted as `"cholesky_l"`. Geometry-space is always 3×3 regardless of ambient D. C# does all the math; JS reads a flat buffer only.
- **`VizCore.csproj` — `LinearAlgebra` project reference**: Added so `serializer.cs` can call `CholeskyDecomposition` directly during serialization.
- **`viewer.html` — GaussianLayer ellipsoid rendering**: `buildGaussianGroup(layer)` builds one `THREE.Mesh(UNIT_SPHERE_GEO, ...)` per component. `UNIT_SPHERE_GEO` is a single shared `SphereGeometry(1,24,16)`. For each component, reads `cholesky_l[k*9..k*9+8]` directly into `Matrix4.elements` (upper-3×3 columns + translation from `means`); no math in JS. Material: `MeshPhongMaterial`, opacity 0.22, `transparent:true`, `depthWrite:false`, cluster-palette colored via `ComponentToClusterMap` (or component index if null). `matrixAutoUpdate = false`.
- **`viewer.html` — "Ellipsoids" checkbox** (`#chk-gaussians`): Tier 1 `.visible` flip on `gaussianMeshGroups[]`. Initial state from `SCENE.hints.show_gaussian_ellipsoids`. Shared `UNIT_SPHERE_GEO` is never disposed on rehydrate; only per-component materials are disposed.

### Added (Pass 4)

- **`viewer.html` — SpineLayer curve overlay**: `buildSpineObjects(layer, cloudRadius)` builds a `LineLoop` through the M spine samples (flat `[M×D]` projected to 3D) per layer. Curve color is 90% cluster color; a white `SphereGeometry` seam marker is parented at sample 0 and inherits `.visible`. Build-once at load time; toggle is Tier 1 `.visible` flip only.
- **`viewer.html` — T/N/B frame arrows (MobiusTube)**: For layers with `kind === 'MobiusTube'`, reads `tangent_bases` (flat `[M×3×3]`, row order T/N/B) and places one `ArrowHelper` per axis at every `round(M/16)`-th sample (~16 frames evenly spaced for readability). Arrow length = `cloudRadius × 0.07`; colors T=`#ff4444`, N=`#44cc44`, B=`#4488ff`. Arrow group is added to scene as a sibling of the curve group and shares the same `.visible` gate.
- **`viewer.html` — Spine controls panel**: Two checkboxes added to `#controls-panel`: "Spine curves" (`#chk-spinecurves`) and "Frame arrows" (`#chk-framearrows`). Initial state seeded from `SCENE.hints.show_spine_overlays` and `show_tangent_bases`. Frame-arrows visibility is gated by spine-curves checkbox (arrows only show when curves are on). Both are Tier 1 `.visible` flips.
- **`viewer.html` — rehydrate support**: `rehydrateFromJson` cleans up old spine objects via `.traverse` dispose (ArrowHelper internals), clears `spineLayerObjects[]`, then rebuilds from `newScene.spine_layers` using current checkbox state.

### Fixed

- **`adapter.cs` — `geomDim` / 4D crash**: `BuildGaussianLayer` and `BuildBestFitGaussianLayer` were indexing covariance arrays using the ambient dimension `d` (which equals 4 in 4D datasets). Covariance matrices are always 3×3 regardless of ambient dimension. Both methods now derive `geomDim = entries[0].geom.Covariance.GetLength(0)` (always 3) and use it for means/covariances array sizing and the `GaussianLayer` `d:` parameter. This resolved `IndexOutOfRangeException` in the 4D smoke scene.
- **`MobiusEllipsoid.cs` — `thetaDensity` was radially scaling cross-section, not biasing density**: The original code multiplied `rN` and `rB` by `thetaDensity`, physically fattening the tube at θ≈0 and θ≈π rather than increasing point frequency. Fixed with θ-rejection sampling: draw θ uniformly, accept with probability `(1 + 0.4|cos θ|) / 1.4`. The cross-section radius is now independent of θ.
- **`MobiusEllipsoid.cs` — `GaussianAnisotropic` phi twist was dead code**: `phi += 0.7 * sht` added a deterministic constant to a uniform circular random variable, leaving its distribution unchanged. `rN` and `rB` were then sampled as independent half-normals; the twist had no geometric effect. Removed. `GaussianAnisotropic` anisotropy is now entirely determined by the `halfWidth`/`halfThickness` ratio, which is the intended behavior.
- **`MobiusEllipsoid.cs` — ellipsoid 4D extent was hardcoded**: `point[3] = 0.35 * SampleStandardNormal(rng)` used a freestanding constant inconsistent with `ellipsoidAxes`. Changed to `ellipsoidAxes[2] * SampleStandardNormal(rng)` so the 4D spread is parameterized consistently with the 3D axes.
- **`MobiusEllipsoid.cs` — spine samples were random-θ and unordered**: Spine samples were collected from the stochastic point loop (`p % spineStep == 0`) so `SpineSamples` and `LocalFrames` were random draws in random θ-order — misleading as a curve overlay. Replaced with a dedicated deterministic loop over `s = 0..spineRes-1` on a uniform θ grid after the point cloud generation. Spine samples are now arc-length uniform and ordered.

---

## 2026-05-08 — Geometric Primitives Layer: Manifolds, Losses, IRLS, Euclidean Estimators

### Added

- **`src/manifolds/IRiemannianManifold.cs`**: Unified non-generic span-based interface (`Distance`, `LogMap`, `ExpMap`, `AddScaled`, `Norm`, `static abstract IsFlat`). Single canonical shape used by all manifold structs and the IRLS solver.
- **`src/manifolds/EuclideanVectorManifold.cs`**: Flat Euclidean R^d implementing `IRiemannianManifold`. `IsFlat = true` enables JIT dead-code elimination of log/exp paths in the solver. 1D case is `EuclideanVectorManifold(dimension: 1)`.
- **`src/manifolds/RiemannianProductManifold.cs`**: Two-factor product manifold `ProductManifold<TA, TB>`. `IsFlat = TA.IsFlat && TB.IsFlat`. `Distance` returns the coupled `√(dA² + dB²)` form required by the Park & You (2026) product-manifold median. `LogMap`/`ExpMap` slice the concatenated tangent buffer per factor.
- **`src/losses/IRobustLoss.cs`**: `IRobustLoss` interface (`static abstract IsClosedForm`, `IsSingularAtZero`, `Weight(double r)`). Concrete structs: `L2Loss` (`Weight = 1`, closed-form) and `L1Loss` (`Weight = 1/r`, singular at zero).
- **`src/optimization/irls-options.cs`**: `IrlsOptions` struct with `MaxIterations`, `Tolerance`, `Epsilon`, `HybridMode`, `SubgradientThreshold`, `Eta0`, `SingularityPolicy`, `ConvergenceCriterion`, and a `Default` static. Enums: `HybridMode` (Hybrid / WeiszfeldOnly / SubgradientOnly), `SingularityPolicy` (Regularise / OptimalityCheck), `ConvergenceCriterion` (Absolute / RelativeToNorm).
- **`src/optimization/irls.cs`**: Unified IRLS solver `Irls.Solve<TManifold, TLoss>`. Three-axis dispatch: (1) `TLoss.IsClosedForm` → Karcher closed-form short-circuit; (2) `TManifold.IsFlat` → ambient weighted average vs tangent round-trip; (3) `HybridMode` + distance threshold → Weiszfeld vs projected subgradient. Optional `finalIrlsWeights` span captures converged per-point weights for downstream scatter computation.
- **`src/estimators/GeometricMean.cs`** (`Estimators.Mean`): Hot-path Riemannian (Fréchet) mean via `Irls.Solve<TManifold, L2Loss>`. `Compute` (location only) and `ComputeWithScatter` (location + Karcher scatter), both zero-alloc on the hot path via `ArrayPool`.
- **`src/estimators/GeometricMedian.cs`** (`Estimators.Median`): Hot-path geometric median via `Irls.Solve<TManifold, L1Loss>`. Same `Compute` / `ComputeWithScatter` shape as `GeometricMean`; scatter uses converged L1 IRLS weights via `WeiszfeldScatter`.
- **`src/estimators/ScatterAccumulator.cs`** (`Estimators.Shared`, internal): Shared `Σ = (c_D / Σ wᵢ) · Σ wᵢ vᵢvᵢᵀ` accumulation primitive called by both `KarcherScatter` and `WeiszfeldScatter`.
- **`src/estimators/KarcherScatter.cs`** (`Estimators.Mean`): L2-weighted scatter companion to `GeometricMean`. Internal `Accumulate` (from converged IRLS weights) and public standalone `Compute` (fixed external location, uniform or supplied weights).
- **`src/estimators/WeiszfeldScatter.cs`** (`Estimators.Median`): L1-weighted scatter companion to `GeometricMedian`. Internal `Accumulate` and public standalone `Compute` (recomputes L1 weights at the supplied location in one pass).
- **`src/estimators/ConsistencyFactors.cs`** (`Estimators.Shared`): Calibration factors `c_D` for the Weiszfeld scatter under Gaussian (`D/(D−1)`) and spherical Laplace (`1/(D−1)`) reference distributions. Both derived in closed form from chi-distribution moments; Lanczos `LogGamma` not required. Throws for `dim ≤ 1` — the Weiszfeld scatter has no finite consistency factor in 1D (use MAD).
- **`src/estimators/EuclideanMean.cs`** (`Estimators.Euclidean`): Flat-space drop-in for `GeometricMean`. `Compute` (single-pass weighted average) and `ComputeWithScatter` (two-pass sample covariance). Shared `ValidateInputs` used by `EuclideanMedian`. Private `ComputeCore` returns `totalWeight`, eliminating a redundant sum in `ComputeWithScatter`.
- **`src/estimators/EuclideanMedian.cs`** (`Estimators.Euclidean`): Flat-space drop-in for `GeometricMedian`. Self-contained regularised Weiszfeld IRLS (no manifold abstraction). `Compute` and `ComputeWithScatter`; post-convergence weight recompute ensures scatter reflects the final location.

### Fixed

- **`irls.cs` — subgradient sign**: Was accumulating `−w·log_p(xᵢ)` (ascent), then stepping in the same direction. Fixed to accumulate `+w·log_p(xᵢ)` (descent toward data) and step along `+subgrad`.
- **`irls.cs` — `IrlsOptions` default sentinel**: `default(IrlsOptions)` zeros `MaxIterations`, causing zero-iteration returns. Sentinel `if (opts.MaxIterations == 0) opts = IrlsOptions.Default` added at entry to `Irls.Solve`.
- **`irls.cs` — `AmbientWeightedAverage` zero-weight case**: Previously left `dst` all-zeros when all weights were zero. Now accepts a `fallback` span and copies the current position, preserving the iterate.
- **`irls.cs` — `CheckOptimality` comment**: Removed spurious `/ ||log_p(x_j)||` from comment; code correctly uses unnormalized `wirls[j]` (which already encodes `weights[j]/r_j`).
- **`ConsistencyFactors.cs` — Gaussian formula**: Was computing `1/E[1/r]`; correct value is `D·E[1/r]/E[r] = D/(D−1)`. Previous formula gave ≈0.399 for D=2 vs the correct 2.0.
- **`ConsistencyFactors.cs` — Laplace formula**: Was returning `dim − 1` (the reciprocal). Correct value is `1/(D−1)`.
- **`EuclideanMedian.cs` — `finalIrlsWeights` capture timing**: Was written inside the per-iteration loop at the pre-update position. Fixed: removed inline write; added post-convergence recompute pass at the final `destination`.
- **`EuclideanMedian.cs` — `maxIterations` validation**: `< 0` changed to `< 1`; `maxIterations = 0` now throws instead of silently returning the warm-start.

### Notes

- `PointEstimates.cs` (convergence diagnostics struct) shelved to `.depr` — IRLS returns `void`; diagnostics deferred to a future sidecar mechanism.
- User-facing ergonomic wrappers (tuple returns, 1D scalar overloads) shelved to `.depr` — to be addressed in a separate UI/CLI project.
- The Weiszfeld scatter and MAD are structurally distinct estimators that co-exist as scatter companions to the median. MAD is the order-statistic scalar companion; Weiszfeld scatter is the IRLS-consistent multivariate generalisation. They are not the same formula evaluated at different dimensions.

---

## 2026-05-08 — VizCore: VizApi Compute Gateway + Interactive Regeneration (Steps 1–4)

### Added

- **`VizApi` project** (`projects/VizApi/`): ASP.NET Core Minimal API acting as the compute gateway for the interactive viewer. `GET /` returns viewer HTML with a default scene; `POST /api/regen` accepts a `RegenRequest` JSON body, runs the full C# pipeline (`CrescentAndEllipsoid → SyntheticDatasetAdapter → SceneBuilder → JsonExportRenderTarget`), and returns a fresh `ScenePackage` JSON. References `VizCore`, `SyntheticDatasets`, `ProximityGraphs`.

- **Generator panel in `viewer.html`**: Collapsible side-panel (220 px, fixed-height scrollable) with controls for all `CrescentEllipsoid` parameters (crescent/ellipsoid points, radius, width, arc half-angle, axes, placement, intersect depth/radial shift, gap scale, seed), a **Shared** section (KNN k), and a **Graph** section (metric dropdown, neighbor-rule dropdown, conditional epsilon input). **Regenerate** button posts all control values to `POST /api/regen` and calls `rehydrateFromJson()` on the returned scene.

- **`rehydrateFromJson(newScene)`** in `viewer.html`: Tier-3 scene rebuild (geometry dispose + rebuild from new `ScenePackage` data). Rebuilds `pointsMeshRef` and `edgeMeshes` from `newScene.points`/`label_layers`/`edge_layers`; preserves orbit controls, lights, and render loop across regenerations.

### Changed

- **`viewer.html` — JS math stripped**: All JavaScript shadow implementations of distance metrics and proximity graph algorithms removed. The viewer is a pure renderer/view-state client; all computation runs in C# via `VizApi`. Architecture invariant enforced: no parallel reimplementations in JS.

- **`viewer.html` — NeighborRule: checkboxes → dropdown**: Multi-checkbox group replaced with a single `<select id="gp-rule">` dropdown. The epsilon input is shown/hidden via a `change` listener (visible only when `EpsilonBall` is selected). Dropdown initialises from the first edge layer name in the loaded scene.

- **`RegenRequest`** (`projects/VizApi/Program.cs`): `string[]? NeighborRules = null` → `string NeighborRule = "Knn"`. API now accepts a single rule; `foreach` loop replaced with a single `ProximitySpec` switch + one `BuildEdgeLayer` call.

### Renamed

- **`ProximityKind` → `NeighborRule`** (`viz_core.cs`, `serializer.cs`, `VizApi/Program.cs`, `VizCoreSmoke/Program.cs`): Language-server rename (6 sites) + 3 manual string stragglers. Enum members unchanged: `Knn`, `MutualKnn`, `EpsilonBall`, `MstAugmented`.

---

## 2026-05-07 - VizCore: HTML Renderer Refactored to JSON-First Architecture

### Changed

- **`ThreeJsHtmlRenderTarget` refactored** (`src/viz-core/html_render_target.cs`): Removed all inline HTML/CSS/JS string generation and all duplicate serialization logic. The class is now a ~50-line thin wrapper: loads `viewer.html` as an embedded assembly resource (static field, loaded once), calls `JsonExportRenderTarget(compact: true)` to serialize the `ScenePackage`, and injects the JSON at the single `__SCENE_DATA__` placeholder. The viewer template owns all HTML, CSS, and Three.js rendering — the C# side only performs the injection. Smoke test confirmed: `~/viz-smoke.html` output unchanged.
- **`viewer.html` added** (`src/viz-core/viewer.html`): New embedded resource. Contains the complete Three.js viewer (importmap, CSS layout, canvas, legend, orbit controls, auto-fit camera, cluster coloring, resize handler, render loop). Reads directly from the `JsonExportRenderTarget` schema (`SCENE.points.features`, `SCENE.label_layers`, `SCENE.hints` etc.). All future rendering passes (edges, ellipsoids, spines, scalars, 2D panels) will be implemented in this file.
- **`VizCore.csproj`** (`projects/VizCore/VizCore.csproj`): Added `<EmbeddedResource>` entry for `viewer.html` with `LogicalName="Viz.viewer.html"`.

### Notes

- `JsonExportRenderTarget` is now the canonical serialization path for all output formats — the HTML renderer no longer has a parallel data-extraction code path. Adding a new layer type to `serializer.cs` automatically makes it available to the HTML viewer without any change to `html_render_target.cs`.

---

## 2026-05-07 - VizCore: JSON Export Renderer, Best-Fit Gaussian Overlay, CrescentEllipsoid v2 Fixes

### Added

- **`JsonExportRenderTarget`** (`src/viz-core/serializer.cs`, namespace `Viz.Renderers`): New `IRenderTarget` implementation that serialises a `ScenePackage` to a schema-versioned JSON snapshot (`schema_version: 1`). All layer arrays are flat row-major with explicit `*_shape` fields. Enums are serialised as strings via `JsonStringEnumConverter`. `[JsonIgnoreCondition.WhenWritingNull]` keeps absent layers out of the output. Constructor: `JsonExportRenderTarget(bool compact = false)` — selects between indented (dev/readable) and compact (automation/diffing) output. Both `JsonSerializerOptions` instances are static; no per-call allocation. Registered in `projects/VizCore/VizCore.csproj`.
- **Best-Fit Gaussian overlay in `SyntheticDatasetAdapter`** (`src/viz-core/adapter.cs`): `BuildBestFitGaussianLayer` method added. For each `ArcGeometry` cluster that has a non-null `ClusterCovariances[i]` entry (populated by `CrescentEllipsoid.BuildCrescentApproxCovariance`), emits a `GaussianLayer` named `"Best-Fit Gaussian"` — the single Gaussian a best-fit model would report for a crescent cluster. Intended to be rendered alongside the analytic ground-truth ellipsoid to visualise the misleading shape. Helpers added: `ComputeClusterMean` (iterates labels, averages matching feature rows), `ComputeClusterWeight` (fraction of points in cluster).

### Fixed

- **`CrescentEllipsoid` v2** (`src/synthetic/CrescentEllipsoid.cs`): Removed dead `scaleVec` block — the ternary `ellipsoidAxes[0] * gapScale == 0 ? ellipsoidAxes[0] : ellipsoidAxes[0]` always evaluated to the same value and the result was never passed to `BuildCovariance`.
- **`CrescentEllipsoid` v2 nullable annotations** (`src/synthetic/CrescentEllipsoid.cs`): `double[] ellipsoidAxes = null`, `double[] ellipsoidCenter = null`, and `double[] ellipsoidEulerXYZ = null` corrected to `double[]?` to satisfy NRT analysis; eliminated three nullable-related warnings.

### Notes

- The best-fit Gaussian layer is opt-in via `CrescentEllipsoid` — it requires `BuildCrescentApproxCovariance` to have been called on the generator and its result stored in `SyntheticDataset.ClusterCovariances[clusterIdx]`. The adapter produces the layer only when that slot is non-null.
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
