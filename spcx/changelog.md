# Changelog

## 2026-06-03 — Hyperbolic bandwidth calibration harness and general H^d intrinsic coupling

### Added

- **`projects/tests/VizCore.Tests/HyperbolicBandwidthValidationHarness.cs`** — added a dedicated hyperbolic bandwidth and intrinsic coupling harness with artifact output, selectable factor-validation models, a load-bearing H^3 Laplacian calibration check, and intrinsic A/B reporting across ambient dimensions 2, 3, and 8.
- **`projects/tests/VizCore.Tests/GeometryFidelityCorrectnessHarness.cs`** — added a new Poincare geometry-fidelity correctness harness that records manifold invariants and shared-base tangent distortion diagnostics to JSON artifacts.

### Changed

- **`src/graphs/Bandwidth.cs`**, **`src/graphs/distance/MetricProperties.cs`** — replaced the single hyperbolic placeholder bandwidth path with per-kernel hyperbolic factors, kept the confirmed Laplacian constant at `1.67`, and corrected the `LogScaleHyperbolic` summary to describe the pure `log(d)` route.
- **`src/graphs/GraphEnums.cs`**, **`src/graphs/GraphProjection.cs`**, **`src/graphs/GraphCompiler.cs`**, **`src/graphs/GraphMetric.cs`**, **`src/graphs/pipeline/scalers/GlobalBandwidthScaler.cs`** — expanded the hyperbolic coupling surface with explicit fidelity selection, threaded ambient dimension through the graph substrate/compiler path, and generalized the intrinsic Van Vleck correction to H^d while preserving the prior H^3 path exactly for `d = 3`.
- **`src/graphs/pipeline/scalers/GlobalBandwidthScaler.cs`** — removed the per-edge `CouplingFidelity.Tangent` implementation and retained intrinsic hyperbolic fidelity support.
- **`projects/tests/VizCore.Tests/GraphBuilderBandwidthTests.cs`** — removed Tangent coupling regression coverage; preserved the hyperbolic manifold invariants and bandwidth calibration tests.
- **`projects/tests/VizCore.Tests/GeometryFidelityCorrectnessHarness.cs`** — preserved shared-base tangent distortion diagnostics while removing Tangent coupling A/B comparisons after the `CouplingFidelity.Tangent` deletion.
- **`projects/tests/VizCore.Tests/DistanceGeodesicTests.cs`** — added Poincare manifold invariants for log/exp round-trip, radial isometry, and directed radius symmetry over dimensions 2, 3, and 8.

### Validated

- `dotnet test .\projects\tests\VizCore.Tests\VizCore.Tests.csproj -nologo --filter "FullyQualifiedName~VizCore.Tests.GraphBuilderBandwidthTests|FullyQualifiedName~VizCore.Tests.GeometryFidelityCorrectnessHarness|FullyQualifiedName~DistanceGeodesicTests.PoincareBallManifold_"` succeeded.
- `dbd .\ps.core.pwshspc.sln -nologo` succeeded.

## 2026-06-02 — LOBPCG spectral integration, metric channel completion, and bandwidth fixes

### Added

- **`src/maths/linalg/Lobpcg.cs`**, **`src/maths/linalg/DenseSymmetricOperator.cs`**, **`src/graphs/spectral/SpectralSolverPolicy.cs`**, **`projects/tests/VizCore.Tests/LobpcgDenseTests.cs`**, **`projects/tests/VizCore.Tests/SpectralSolverPolicyTests.cs`** — added a dense-symmetric LOBPCG solver path, explicit spectral solver policy selection, and regression coverage for the new eigensolver route.
- **`src/graphs/GraphMetric.cs`** — added an explicit graph metric model so compiler, SPC graph construction, and persisted manifests share the same metric selection channel.
- **`projects/VizApi/VizApiExtraMetric.cs`** — added VizApi-side extra metric plumbing for the expanded graph and spectral workflows.

### Changed

- **`src/graphs/spectral/CoherentField.cs`**, **`src/graphs/spectral/GraphSpectralOperators.cs`**, **`src/graphs/spectral/Spectral.cs`**, **`src/maths/geometry/SpectralBridge.cs`**, **`src/tda/mapper/Filters/FiedlerFilter.cs`** — wired the new LOBPCG-backed spectral stack through graph operators, geometry helpers, and mapper Fiedler filtering.
- **`src/graphs/GraphCompiler.cs`**, **`src/clustering/graphical/spc/runtime/initialization/SpcGraphBuilder.cs`**, **`src/user-repl/commands/GraphConstructionManifest.cs`**, **`src/user-repl/commands/GraphConstructionPersistence.cs`**, **`src/user-repl/commands/SpcCommand.cs`**, **`projects/VizApi/Program.cs`** — completed the graph metric channel plumbing across SPC graph construction, REPL manifest persistence, smoke harnesses, and VizApi entrypoints.
- **`src/synthetic/SyntheticData.cs`**, **`src/synthetic/manifolds/GaussianManifold.cs`**, **`src/synthetic/manifolds/HyperbolicHierarchy.cs`**, **`src/synthetic/manifolds/Simplex.cs`**, **`src/user-repl/SpcUserDataset.cs`**, **`src/user-repl/SpcUserSession.cs`**, **`src/viz-core/schema_catalog.cs`**, **`src/viz-core/serializer.cs`**, **`src/viz-core/viewer.html`**, **`src/viz-core/viz_graph.cs`**, **`src/viz-core/viz_layers.cs`** — aligned synthetic dataset generation and VizCore serialization/viewer surfaces with the updated spectral and metric-aware workflows.
- **`src/graphs/Bandwidth.cs`**, **`tests/tda/mapper/HyperbolicHierarchyTest.cs`** — fixed the log-MAD bandwidth path and refreshed the hyperbolic hierarchy mapper fixture coverage.

## 2026-06-01 — Metric registry consolidation and repo-audit cross-reference hardening

### Added

- **`src/maths/distance/Canberra.cs`**, **`src/maths/distance/EarthMover.cs`**, **`src/maths/distance/Jaccard.cs`**, **`src/maths/distance/Mahalanobis.cs`**, **`src/maths/distance/Ncd.cs`** — promoted additional distance metrics into the supported math surface for registry-backed dispatch.

### Changed

- **`src/graphs/distance/MetricRegistry.cs`**, **`projects/Graphs.Primitives/Graphs.Primitives.csproj`**, **`projects/Maths.Distance/Maths.Distance.csproj`**, **`projects/Clustering.Graphical.SPC/Clustering.Graphical.SPC.csproj`**, **`projects/Clustering.Statistical.GMM/Clustering.Statistical.GMM.csproj`**, **`projects/VizApi/DistanceFactory.cs`** — consolidated metric registration and project wiring so SPC, GMM, graph primitives, and VizApi resolve the same distance surface.
- **`src/repo-audit/ArtifactWriter.cs`**, **`src/repo-audit/CrossReference.cs`**, **`src/repo-audit/InProcessCompiler.cs`**, **`projects/RepoAudit/RepoAudit.csproj`**, **`projects/tests/RepoAudit.Tests/RepoAudit.Tests.csproj`**, **`tests/repo-audit/CrossReferenceGhostTests.cs`** — tightened repo-audit cross-reference analysis and added regression coverage for ghost-reference cases.

## 2026-05-31 — HDBSCAN session parity, JSON artifact plumbing, and manifest serialization cleanup

### Added

- **`projects/Archivory.Jso/Archivory.Jso.csproj`**, **`src/archivory/jso/JsonArtifactConventions.cs`**, **`src/archivory/jso/JsonArtifactWriter.cs`** — added JSON artifact conventions and writer support for manifest-backed graph construction outputs.

### Changed

- **`src/clustering/graphical/hdbscan/HdbscanMetricDispatch.cs`**, **`src/clustering/graphical/hdbscan/HdbscanSession.cs`**, **`src/clustering/graphical/hdbscan/HdbscanSettings.cs`**, **`src/user-repl/commands/HdbscanCommand.cs`**, **`src/user-repl/commands/HdbscanPreset.cs`**, **`src/tda/mapper/Clusterers/MapperHdbScan.cs`**, **`projects/tests/Clustering.HdbScanSmoke/Program.cs`** — aligned HDBSCAN runtime settings, metric dispatch, mapper integration, and REPL/smoke command handling.
- **`src/graphs/GraphProjection.cs`**, **`src/graphs/coupling/IKernelDescriptor.cs`**, **`src/graphs/coupling/KernelDescriptorJsonConverter.cs`**, **`src/user-repl/UserReplJsonContext.cs`**, **`src/user-repl/commands/RunManifest.cs`**, **`src/user-repl/commands/ManifestMaterialization.cs`**, **`src/user-repl/commands/GraphConstructionPersistence.cs`**, **`src/user-repl/commands/SpcCommand.cs`** — improved graph projection serialization and manifest materialization so persisted graph-construction runs retain kernel and metric metadata.
- **`src/archivory/ArtifactFile.cs`**, **`src/archivory/BinarySerialization.cs`**, **`projects/Archivory/Archivory.csproj`**, **`projects/Graphs.Proximity/Graphs.Proximity.csproj`**, **`projects/UserRepl.Commands/UserRepl.Commands.csproj`**, **`ps.core.pwshspc.sln`** — synchronized archivory, proximity, and command-project wiring for the new JSON artifact and graph persistence pipeline.

## 2026-05-30 — Graph pipeline migration, legacy preset sunset, and graph-health test updates

### Changed

- **`src/graphs/GraphCompiler.cs`**, **`src/graphs/GraphCompilerConfig.cs`**, **`src/graphs/GraphEnums.cs`**, **`src/graphs/GraphLegacyPresets.cs`**, **`src/graphs/GraphSelection.cs`** — continued the graph compiler migration, formalized the modern configuration surface, and sunset the remaining legacy preset shim.
- **`src/graphs/pipeline/IMetricRefiner.cs`**, **`src/graphs/pipeline/refinement/EuclideanRefiner.cs`**, **`src/graphs/pipeline/refinement/PathNeighborRefiner.cs`**, **`src/graphs/pipeline/IEdgeScaler.cs`**, **`src/graphs/pipeline/filters/MutualKnnFilter.cs`**, **`src/graphs/pipeline/filters/PassThroughFilter.cs`**, **`src/graphs/pipeline/generators/KnnGenerator.cs`**, **`src/graphs/pipeline/scalers/GlobalBandwidthScaler.cs`**, **`src/graphs/pipeline/scalers/LocalMutualProximityScaler.cs`**, **`src/graphs/proximity/EpsilonBall.cs`**, **`src/graphs/proximity/KNNGraph.cs`**, **`src/graphs/neighbors/DirectedKnn.cs`**, **`src/graphs/neighbors/Symmetrization.cs`**, **`src/graphs/primitives/BoundedMinHeap.cs`**, **`src/graphs/primitives/Neighbor.cs`**, **`src/graphs/primitives/traversal/Dijkstra.cs`** — reorganized graph pipeline components, neighbor primitives, and proximity builders around the newer graph-construction stack.
- **`src/user-repl/commands/ExtractCommand.cs`**, **`src/user-repl/commands/GraphHealthCommand.cs`**, **`src/user-repl/commands/ManifestMaterialization.cs`**, **`src/user-repl/commands/RunManifest.cs`**, **`src/user-repl/commands/SpcCommand.cs`**, **`src/user-repl/commands/SpcCommandHelp.cs`**, **`src/user-repl/commands/SpcPreset.cs`**, **`projects/VizApi/Program.cs`** — updated the user REPL and VizApi command surfaces to match the migrated graph compiler, graph health, and manifest persistence flow.
- **`projects/tests/VizCore.Tests/GraphBuilderBandwidthTests.cs`**, **`projects/tests/VizCore.Tests/GraphDiagnosticsTests.cs`**, **`projects/tests/VizCore.Tests/GraphPathNeighborTests.cs`**, **`projects/tests/VizCore.Tests/MixtureKernelTests.cs`**, **`projects/tests/VizCore.Tests/TopologyFilterTests.cs`**, **`projects/tests/VizCore.Tests/SpcGraphBuilderTests.cs`**, **`projects/tests/VizCore.Tests/SpcSharedRunDirectoryFacts.cs`**, **`tests/tda/mapper/HyperbolicHierarchyTest.cs`**, **`tests/tda/mapper/PersistentMapperTests.cs`** — refreshed graph, SPC, and mapper coverage after the compiler/pipeline migration.

## 2026-05-29 — HDBSCAN MST refactor, graph primitive cleanup, and TDA test wiring

### Added

- **`projects/tests/TDA.Mapper/TDA.Mapper.Tests.csproj`** — added `tests/graphs/primitives/**/*.cs` compile inclusion so graph primitive implementations are available to TDA mapper tests.

### Changed

- **`src/clustering/graphical/hdbscan/Hdbscan.cs`** — refactored HDBSCAN to compute the mutual-reachability MST via `Graphs.Primitives.Mst.Prim`, removed the legacy `HdbscanMst` edge-builder, introduced reusable core-distance and MST materialization buffers, and now sorts the resulting `MstEdge[]` before dendrogram construction.
- **`src/graphs/distance/IDistanceMetric.cs`** — added a low-level `Distance(ref double a, ref double b, int dim)` overload for allocation-free metric evaluation in hot `Prim` loops.
- **`src/graphs/primitives/mst/Boruvka.cs`** — updated documentation to note sibling `Kruskal` support for sortable edge-list consumers.
- **`src/graphs/primitives/mst/Prim.cs`** — added low-level interop imports and ref-based data access for performance in mutual-reachability MST construction.
- **`src/graphs/primitives/traversal/Dijkstra.cs`** — moved the traversal primitive namespace from `Graphs.Primitives` to `Graphs.Primitives.Traversal`.

### Removed

- **`src/graphs/primitives/mst/HdbscanMst.cs`** — removed the legacy HDBSCAN-specific MST builder in favor of the shared `Prim` implementation.

## 2026-05-25 — User REPL subcommand refactor, SPC/HDBSCAN workflow, and dendrogram support

### Added

- **`projects/Clustering.Dendrograms/Clustering.Dendrograms.csproj`**, **`src/clustering/dendrogram/Dendrogram.cs`**, **`src/clustering/dendrogram/DendrogramNode.cs`** — added dendrogram clustering support and renderer-friendly node structures.
- **`src/user-repl/SubcommandRouter.cs`**, **`src/user-repl/commands/SpcCommand.cs`**, **`src/user-repl/commands/HdbscanCommand.cs`**, **`src/user-repl/commands/GraphHealthCommand.cs`**, **`src/user-repl/commands/ExtractCommand.cs`**, **`src/user-repl/commands/SpcPreset.cs`**, **`src/user-repl/commands/HdbscanPreset.cs`** — refactored the REPL into a modular subcommand architecture and added dedicated SPC, HDBSCAN, graph-health, extract, and preset command surfaces.
- **`src/clustering/graphical/spc/runtime/scheduling/AdaptiveSweepStrategy.cs`**, **`src/clustering/graphical/spc/runtime/scheduling/FixedGridSweepStrategy.cs`**, **`src/clustering/graphical/spc/runtime/scheduling/SweepKernel.cs`**, **`src/clustering/graphical/spc/runtime/scheduling/ISweepStrategy.cs`**, **`src/clustering/graphical/spc/runtime/scheduling/SpcSeedHelper.cs`** — added adaptive and fixed-grid SPC sweep scheduling infrastructure.
- **`src/graphs/GraphBuildResult.cs`**, **`src/graphs/GraphBuilder.cs`**, **`src/graphs/diagnostics/GraphHealth.cs`**, **`src/graphs/Bandwidth.cs`** — improved graph construction and diagnostics support for workflow validation.

### Changed

- **`projects/UserRepl/UserRepl.csproj`**, **`projects/UserRepl/Program.cs`** — migrated the user REPL project to the new command router and subcommand-based CLI.
- **`src/user-repl/SpcUserSession.cs`**, **`src/user-repl/SpcUserRunResult.cs`** — updated SPC session/result plumbing for the new REPL command infrastructure.
- **`src/clustering/graphical/spc/export/SpcCsvWriter.cs`**, **`src/clustering/graphical/spc/export/SpcTabularProjections.cs`**, **`src/clustering/graphical/spc/export/SpcOutputPathHelper.cs`** — improved SPC export helpers and path handling.
- **`src/clustering/graphical/spc/partitions/hierarchical/BlattPartitionStrategy.cs`**, **`src/clustering/graphical/spc/partitions/hierarchical/IHierarchicalPartitionStrategy.cs`**, **`src/clustering/graphical/spc/partitions/hierarchical/PartitionHierarchy.cs`** — introduced hierarchical SPC partitioning and transition detection support.
- **`src/user-repl/SpcCli.cs`** — removed the legacy SPC CLI after migrating the REPL to a modular subcommand router.

## 2026-05-24 — SPC runtime reorganization, repo-audit cleanup, and Archivory/tabular consolidation

### Added

- **`projects/Archivory.Tabular/Archivory.Tabular.csproj`** — added a dedicated tabular companion project and consolidated tabular projection infrastructure into Archivory.
- **`src/archivory/tabular/TabularBuilder.cs`**, **`src/archivory/tabular/TabularData.cs`**, **`src/archivory/tabular/TabularProjection.cs`** — restored tabular projection infrastructure in the Archivory layer.
- **`src/maths/linalg/SpectralMath.cs`** — added spectral math helper support for the graph spectral workflow.

### Changed

- **`src/repo-audit/ArtifactWriter.cs`**, **`src/repo-audit/CrossReference.cs`**, **`src/repo-audit/InProcessCompiler.cs`**, **`src/repo-audit/RunAudit.cs`**, **`src/repo-audit/GlobCompiler.cs`**, **`src/repo-audit/GitRenameDetector.cs`** — upgraded repo-audit analyzer integration and fixed syntax regressions introduced by earlier Copilot edits.
- **`scripts/repo-audit.ps1`** — updated launcher integration for the current `RepoAudit` executable pipeline.
- **`src/clustering/spc/runtime/initialization/SpcGraphBuilder.cs`**, **`src/clustering/spc/runtime/initialization/SpcGraphConfig.cs`** — migrated `spc.graph` graph construction into the runtime initialization layer.
- **`src/clustering/spc/runtime/sampler/PottsRunSerialization.cs`** — consolidated old `PottsRunnerIO` serialization into the new runtime serialization helper.
- **`projects/Clustering.Graphical.SPC/Clustering.Graphical.SPC.csproj`**, **`project.toml`**, **`ps.core.pwshspc.sln`** — synchronized project references after SPC and Archivory refactor work.
- **`src/clustering/graphical/spc/*`** and **`src/clustering/statistical/gmm/*`** — refactored SPC and clustering algorithm categories across graphical, statistical, and geometric namespaces.

### Notes

- This entry catches up the changelog to the May 24 repo state by recording the SPC runtime path cleanup, tabular/archive refactor, and repo-audit fixes that were committed after the previous 2026-05-23 entry.

## 2026-05-23 — RepoAudit analyzer fix, SPC dataset export, and CSR graph validation

### Added

- **`projects/RepoAudit/RepoAudit.csproj`** — added explicit `Microsoft.CodeAnalysis` package reference so the in-process repo-audit compiler can resolve Roslyn core types and symbols.
- **`src/repo-audit/CsprojParser.cs`** — began parsing `<AllowUnsafeBlocks>` into project compiler settings.
- **`src/repo-audit/InProcessCompiler.cs`** — propagates `AllowUnsafeBlocks` into Roslyn `CSharpCompilationOptions` for semantic analysis runs.
- **`src/repo-audit/ArtifactWriter.cs`** — serializes `allowUnsafeBlocks` into repo-audit analysis artifacts.
- **`src/repo-audit/IgnoreEngine.cs`**, **`src/repo-audit/RunAudit.cs`** — repaired ignore-engine and CLI integration for the current `RepoAudit` pipeline.
- **`src/clustering/spc/export/SpcTabularProjections.cs`** — added missing `CreateDatasetProjection(...)` helper for SPC dataset tabular export.
- **`projects/Archivory/Archivory.csproj`** — added new Archivory project to host reusable archive and binary serialization primitives.
- **`src/archivory/BinarySerialization.cs`** — introduced `IBinarySerializer<T>` and `BinarySerializerBase<T>` as the foundation for atomic binary archive serialization.
- **`src/clustering/spc/runtime/sampler/PottsRunnerIO.cs`** — refactored SPC concrete serializers to compose on Archivory's binary serializer base and tighten I/O schema ownership.
- **`src/clustering/spc/runtime/scheduling/SpcScheduleHelpers.cs`** — added CSR graph validation so externally supplied `CsrGraph` instances are accepted safely.

### Changed

- **`projects/Clustering.SPC/Clustering.SPC.csproj`** — added `Archivory` project reference so SPC checkpoint and observation serializers can compose on the new binary archive layer.
- **`project.toml`** — updated repo inventory to include the new `Archivory` project and its source root.
- **`src/clustering/hdbscan/Hdbscan.cs`** — restored `using Graphs.Proximity` after `ImplicitPrims` was moved/renamed.
- **`src/graphs/proximity/MRPGraph.cs`**, **`src/graphs/distance/IDistanceMetric.cs`** — cleaned up XML docs and cref references after proximity/metric refactor.
- **`scripts/repo-audit.ps1`** — updated launcher integration for the current `RepoAudit` executable project path.

### Validated

- `dotnet build .\projects\RepoAudit\RepoAudit.csproj -c Release` succeeded.

## 2026-05-22 — Independent SPC user module, tabular projection support, and harness runtime migration

### Added

- **`src/user/SpcUserApi.cs`** — added a standalone user-facing SPC session API for synthetic dataset generation, graph configuration, run directory management, and CSV export.
- **`src/user/SpcTabularProjections.cs`** — added tabular projection helpers for `SweepProfile`, `ProfileCriteria`, `Partition`, `SpcSessionResult`, and synthetic `SpcUserDataset` exports.
- **`projects/tests/Spc.User/Spc.User.csproj`** — added a new interactive console project that consumes the user-facing SPC API and writes both SPC CSVs and tabular exports.
- **`projects/tests/Spc.User/Program.cs`** — added a simple CLI harness for dataset selection, graph configuration, SPC run execution, and artifact export.

### Changed

- **`src/tabular/TabularProjection.cs`** — added generic projection support and utility extensions to build CSV-ready tables from arbitrary object sequences.
- **`projects/tests/Spc.BlattSmoke/Program.cs`**, **`projects/tests/Spc.HyperbolicSmoke/Program.cs`**, **`projects/tests/Spc.BlattAnalyze/Program.cs`** — updated existing SPC smoke harness programs to match the migrated `Clustering.SPC.Runtime.Sampler.Potts` runtime and `SpcExecutor` execution API while preserving harness semantics.

### Validated

- `dotnet build .\projects\tests\Spc.User\Spc.User.csproj -c Release` succeeded.
- `dotnet build .\projects\tests\Spc.BlattSmoke\Spc.BlattSmoke.csproj -c Release` succeeded.
- `dotnet build .\projects\tests\Spc.HyperbolicSmoke\Spc.HyperbolicSmoke.csproj -c Release` succeeded.
- `dotnet build .\projects\tests\Spc.BlattAnalyze\Spc.BlattAnalyze.csproj -c Release` succeeded.

## 2026-05-21 — SPC runtime/analysis cleanup, profiling scaffolding, and audit validation

### Changed

- **`src/clustering/spc/runtime/execution/SpcRunResult.cs`** — fixed malformed XML docs and attached remarks to the record declaration; tightened the result contract for downstream SPC analyses.
- **`src/clustering/spc/partitions/IPartitionStrategy.cs`** — corrected an XML cref reference to `Observables.Projections.IGraphSignal`.
- **`src/clustering/spc/profiling/`** — added profiling analysis scaffolding and signal analyzer contracts to support the SPC diagnostics topology.
- **`src/clustering/spc/graph/SpcGraphConfig.cs`** and **`src/clustering/spc/graph/SpcGraphBuilder.cs`** — added typed SPC graph construction config and feature-array graph builder support for production proximity graph construction.
- **`src/clustering/spc/SpcClusteringSession.cs`** — added feature-array overload and typed SPC evaluation entry point for end-to-end session orchestration.
- **`src/clustering/spc/observables/reductions/MeanEnergy.cs`** and **`src/clustering/spc/observables/reductions/Magnetization.cs`** — introduced first-class SPC reduction helpers for energy and magnetization signal materialization.
- **`projects/tests/VizCore.Tests/SpcGraphBuilderTests.cs`** — added regression coverage for SPC graph builder behavior.
- **`projects/Clustering.SPC/Clustering.SPC.csproj`** — added `Graphs.Coupling` and `Clustering.Evaluation` references for SPC graph and evaluation dependencies.
- **`src/clustering/spc/runtime/scheduling/SpcScheduleHelpers.cs`** and **`src/clustering/spc/runtime/scheduler/AdaptiveScheduler.cs`** — cleaned up scheduler helper extraction and adaptive SPC scheduling boundaries.

### Validated

- `dotnet build .\projects\Clustering.SPC\Clustering.SPC.csproj -c Release` succeeded.
- `scripts/repo-audit.ps1` completed successfully with **0 violations**.

## 2026-05-20 — SPC observable tier refactor and evaluation project wiring

### Changed

- **`src/clustering/spc/IPottsConfig.cs`** — collapsed the runner config surface to a single `EdgeObservables` gate and renamed the public enum to `PottsObservableTier`.
- **`src/clustering/spc/PottsModel.cs`** — updated the public API to accept `PottsObservableTier` and dispatch to the correct runner specialization.
- **`src/clustering/spc/PottsRunner.cs`** — made Blatt magnetization unconditional; only per-edge bond/spin observables remain gated on `EdgeObservables`.
- **`src/clustering/spc/AdaptiveScheduler.cs`** and **`src/clustering/spc/SpcScheduler.cs`** — migrated old `PottsSusceptibility` references to `PottsObservableTier` and aligned scheduler task APIs.
- **`projects/tests/Spc.BlattSmoke/Spc.BlattSmoke.csproj`**, **`projects/tests/Spc.HyperbolicSmoke/Spc.HyperbolicSmoke.csproj`** — added `Clustering.Evaluation` project reference so smoke harnesses can consume the evaluation helpers.
- **`projects/tests/Spc.BlattSmoke/Program.cs`**, **`projects/tests/Spc.HyperbolicSmoke/Program.cs`** — updated harness code to use the new `ObservableTier` constant and import the evaluation namespace.
- **`src/clustering/evaluation/external/Purity.cs`** — added a static `Purity.Compute` helper for one-shot purity calculations.
- **`src/clustering/evaluation/internal/IInternalClusterEvaluator.cs`**, **`src/clustering/spc/graphsignals/IGraphSignal.cs`**, **`src/clustering/spc/partitions/IPartitionStrategy.cs`** — fixed XML doc invalid `<paramref>` references.
- **`projects/Clustering.SPC/Clustering.SPC.csproj`** — added missing `Maths.LinAlg` project reference to satisfy `SpinCorrelationEigenvectorCentrality` dependency and remove the repo-audit ghost dependency.

### Validated

- `dotnet build .\projects\Clustering.Evaluation\Clustering.Evaluation.csproj -c Release` succeeded.
- `dotnet build .\projects\Clustering.SPC\Clustering.SPC.csproj -c Release` succeeded.
- `dotnet build .\projects\tests\Spc.BlattSmoke\Spc.BlattSmoke.csproj -c Release` succeeded.
- `dotnet build .\projects\tests\Spc.HyperbolicSmoke\Spc.HyperbolicSmoke.csproj -c Release` succeeded.

## 2026-05-20 — Changelog_Archive.md updated

### Added

- **`src/clustering/spc/partitions/IPartitionStrategy.cs`**, **`Partition.cs`**, **`ThresholdBondFrequency.cs`**, **`ThresholdSpinAgreement.cs`**, **`UnionFindLabeler.cs`** — new SPC partitioning abstraction layer with threshold-bond-frequency and threshold-spin-agreement strategies and union-find labeler support.
- **`src/clustering/spc/BlattCanonicalCut.cs`** — updated to consume the new SPC partition strategy abstractions and the graph scalar field interface surface.
- **`src/clustering/spc/graphsignals/IGraphSignal.cs`**, **`BondFrequencyBinaryEntropySum.cs`**, **`BondFrequencyDegree.cs`**, **`SpinCorrelationEigenvectorCentrality.cs`** — first-class graph scalar field signal primitives for SPC diagnostics.
- **`src/clustering/evaluation/IClusterEvaluator.cs`**, **`external/IExternalClusterEvaluator.cs`**, **`internal/IInternalClusterEvaluator.cs`**, **`EvaluationHelpers.cs`**, **`Purity.cs`**, **`CalinskiHarabaszEvaluator.cs`**, **`DaviesBouldinEvaluator.cs`**, **`SilhouetteEvaluator.cs`** — new clustering evaluation interface surface with internal/external metric primitives.
- **`src/clustering/spc/SpcRunResult.cs`** — new model for SPC run results.
- `.discussion/` planning notes for SPC triangulation, graph-signal lenses, and evaluator design.

### Notes

- This step completes the SPC partition strategy and graph-scalar-field interface layer, converting several prototype helpers into structured SPC evaluation primitives.

## 2026-05-19 — HDBSCAN added, LMPGraph integration started, mapper module updates

Written: 2026-05-19

### Added

- **`projects/Clustering.HdbScan/Clustering.HdbScan.csproj`** and **`src/clustering/hdbscan/Hdbscan.cs`** — new HDBSCAN implementation and smoke harness via **`projects/tests/Clustering.HdbScanSmoke/Program.cs`**.
- **`src/graphs/primitives/IDistanceMetric.cs`**, **`src/graphs/distance/euclidean/EuclideanMetric.cs`**, **`src/graphs/proximity/LMPGraph.cs`** — LMPGraph support and Euclidean distance metric abstraction.
- **`src/graphs/spectral/CoherentField.cs`**, **`GraphLaplacian.cs`**, **`GraphLaplacianSmoother.cs`**, **`Spectral.cs`** — new spectral graph support and coherent field primitives.
- **`src/tda/mapper/Clusterers/MapperConnectedComponents.cs`**, **`MapperHdbScan.cs`**, **`MapperSpc.cs`**, **`src/tda/mapper/Filters/FiedlerFilter.cs`**, **`GraphFilters.cs`** — mapper module integration for HDBSCAN/SPC graph filtering.
- **`projects/tests/VizCore.Tests/EigenFastTimingHarness.cs`**, **`FixtureGoldenDrafts.cs`**, **`FixtureGoldenHelpers.cs`**, **`SpectralDenseDispatchTests.cs`** — new spectral and clustering harness/test updates.
- **`project.toml`**, **`TODO.md`**, and `.discussion` planning support updated to track the new clustering and graph work.

### Notes

- The new HDBSCAN and LMPGraph surfaces establish a richer clustering evaluation path and begin the mapper integration for SPC graph scalar-field workflows.

## 2026-05-18 — repo-audit: linking analysis (SymbolLinker + XML doc crefs)

Written: 2026-05-18

### Added

- **`src/code-analysis/SymbolLinker.cs`** — new module: `LinkedDependency` record (referenced assembly, list of used type simple-names, boolean effectiveness flag); `ProjectLinkage` record (assembly + per-reference slots); `SymbolLinker.BuildLinkage(projects)` intersects each project's referenced-type-name set against each referenced assembly's declared type set; `FindEffectivelyUnlinkedProjects` returns assemblies where every reference slot is empty. All operating on already-parsed `ProjectAnalysis` / `FileAnalysis` data — no extra I/O.
- **`src/code-analysis/TypeOntology.cs`** — `SourceFileAnalysis` and `FileAnalysis` both gain `IReadOnlyList<string> ReferencedTypeNames`.
- **`src/code-analysis/SyntaxWalker.cs`** — `CollectTypeReferences` walks explicit type-position AST nodes (`VariableDeclarationSyntax`, `ParameterSyntax`, `MethodDeclarationSyntax` return type, `PropertyDeclarationSyntax`, `FieldDeclarationSyntax`, `SimpleBaseTypeSyntax`, `ObjectCreationExpressionSyntax`, `CastExpressionSyntax`, `TypeOfExpressionSyntax`, `TypePatternSyntax`) and calls `CollectDocCrefs` at the end. `CollectDocCrefs` walks `DocumentationCommentTriviaSyntax` trivia and extracts names from all `XmlCrefAttributeSyntax` targets via `ExtractCrefNames` (`NameMemberCrefSyntax` including parameter types, `QualifiedCrefSyntax` both sides, `TypeCrefSyntax`). `ExtractSimpleNames` unwraps nullable/array/tuple/qualified wrappers and skips `PredefinedTypeSyntax`, `var`, `dynamic`. `ProjectAnalyzer.Analyze` propagates `ReferencedTypeNames` into the public `FileAnalysis`.
- **`src/code-analysis/Program.cs`** — `--link` flag; when set, calls `SymbolLinker.BuildLinkage` and prints a `--- Linking Analysis ---` section showing, per project, each `ProjectReference` with its type-level usage list or a `(no type-level references detected)` marker; lists any projects where all references came up empty.

### Validated

- `dotnet run -- --link` against repo root: `TDA.Mapper → Clustering.GMM: [GaussianMixtureModel, ModalMergeStrategy, ModeAscent]`; `VizCore → Synthetic: [SyntheticDataset]` (cref-sourced — only appears in doc comments, invisible to type-position walk); `Clustering.GMM → Estimators: [EuclideanMedian]` (also cref-sourced, was empty before). XML doc crefs augmented 6+ previously-empty or under-populated slots.
- `dotnet build projects/CodeAnalysis/CodeAnalysis.csproj` — 0 warnings, 0 errors.
