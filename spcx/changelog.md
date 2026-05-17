# Changelog

## 2026-05-17 — DenseEigen downstream promotion closure and dual-backend CI

Written: 2026-05-17

### Fixed

- **`src/graphs/diagnostics/AlgebraicConnectivity.cs`**, **`src/tda/mapper/GraphMapper.cs`**, **`src/maths/linalg/PCA.cs`**, and **`src/maths/linalg/ICA.cs`** — completed the remaining dense-consumer promotion pass by routing the graph-diagnostics Fiedler value, graph-MAPPER Fiedler vector extraction, PCA covariance eigendecomposition, and ICA symmetric-decorrelation eigendecomposition through `DenseEigen.DecomposeSymmetric(...)` instead of calling `Eigen.DecomposeSymmetric(...)` directly.
- **`src/maths/linalg/PCA.cs`** — repaired the whitening contract while landing the dispatcher swap: `PcaResult` now carries the actual PCA eigenvalues instead of reconstructing them from explained-variance ratios, so whitening scales by the real spectrum rather than the placeholder ratio surface.
- **`src/maths/linalg/ICA.cs`** — repaired the symmetric FastICA path so the decorrelated matrix returned from the eigendecomposition is written back into the unmixing matrix that `Compute(...)` returns, closing the local row-layout / rebind defect that would otherwise leave callers with a stale pre-decorrelation `W`.
- **`projects/tests/VizCore.Tests/GraphDiagnosticsTests.cs`**, **`tests/tda/mapper/GraphMapperTests.cs`**, **`projects/tests/VizCore.Tests/PcaTests.cs`**, and **`projects/tests/VizCore.Tests/IcaTests.cs`** — added focused downstream regression coverage for the newly promoted consumers: connected/weak/disconnected algebraic-connectivity fixtures, graph-MAPPER Fiedler-vector and disconnected-graph guard checks, deterministic PCA shape/order/whitening checks, and deterministic symmetric-FastICA orthogonality plus source-recovery checks.
- **`projects/tests/TDA.Mapper/TDA.Mapper.Tests.csproj`** — promoted the mapper test host to a real xUnit test project so the new graph-MAPPER regressions execute under `dotnet test` instead of silently compiling as a plain library.
- **`Directory.Build.props`** and **`.github/workflows/dotnet-tests.yml`** — added a safe additive `ExtraDefineConstants` hook and the first repo CI workflow, which restores the solution and runs both `TDA.Mapper.Tests` and `VizCore.Tests` under a two-entry matrix: the default `DenseEigen` fast backend and the oracle-routed `EIGEN_REFERENCE` backend.

### Validated

- **Focused consumer slices passed under the default build** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` passes the algebraic-connectivity, PCA, and ICA regression filters, and `projects/tests/TDA.Mapper/TDA.Mapper.Tests.csproj` passes the graph-MAPPER Fiedler regression filter.
- **Release suite passed on the default backend** — `dotnet test projects/tests/TDA.Mapper/TDA.Mapper.Tests.csproj --configuration Release --no-restore` reports `2 passed`; `dotnet test projects/tests/VizCore.Tests/VizCore.Tests.csproj --configuration Release --no-restore` reports `69 total, 65 passed, 4 skipped, 0 failed`.
- **Release suite passed on the `EIGEN_REFERENCE` backend** — `dotnet test projects/tests/TDA.Mapper/TDA.Mapper.Tests.csproj --configuration Release --no-restore -p:ExtraDefineConstants=EIGEN_REFERENCE` reports `2 passed`; `dotnet test projects/tests/VizCore.Tests/VizCore.Tests.csproj --configuration Release --no-restore -p:ExtraDefineConstants=EIGEN_REFERENCE` reports `69 total, 65 passed, 4 skipped, 0 failed`.

### Notes

- **CI constant handling** — the brief’s additive-constant requirement is satisfied through `ExtraDefineConstants`, which appends to `$(DefineConstants)` inside `Directory.Build.props` and avoids the fragile command-line semicolon quoting path on PowerShell.

## 2026-05-17 — Graph diagnostics pass, triangle counting, and dense spectral routing

Written: 2026-05-17

### Fixed

- **`src/maths/linalg/DenseEigen.cs`**, **`src/maths/linalg/EigenFast.cs`**, **`src/tda/primitives/Spectral.cs`**, **`projects/tests/VizCore.Tests/EigenFastTimingHarness.cs`**, and **`projects/tests/VizCore.Tests/SpectralDenseDispatchTests.cs`** — added a fast-family dispatch seam at `DenseEigen` with opt-in `DenseEigenFastVariant` routing, exposed a flat column-major dense-eigen entrypoint, kept the validated Zone 1 SIMD update in `EigenFast`, restored FMA as an opt-in variant instead of the default runtime path, and taught `Spectral` to compare the legacy rectangular Laplacian materialization against a new pooled flat column-major Laplacian path without pulling `CsrGraph` into `Maths.LinAlg`; the benchmark and new vertical tests now exercise those rectangular-vs-flat and default-vs-FMA dense dispatch combinations through the real spectral consumer path.
- **`projects/tests/HarnessArtifacts.cs`**, **`projects/tests/Directory.Build.targets`**, **`projects/tests/VizCore.Tests/EigenFastTimingHarness.cs`**, and **`projects/tests/VizCore.Tests/SpectralDenseDispatchTests.cs`** — the EigenFast benchmark and spectral dispatch correctness harnesses now run on the shared artifact-first test infrastructure rather than the earlier `VizCore.Tests`-local helper: each run writes into timestamped `artifacts/test-runs/<suite>/<YYYYMMDD_HHmmss>` folders with a manifest plus structured JSON result payloads, while console/test output reports only run metadata and generated file paths so stale benchmark text no longer pollutes fresh runs.
- **`projects/tests/VizCore.Tests/FixtureGoldenDrafts.cs`** — applied the same artifact discipline to the active fixture-golden draft harnesses, replacing raw graph/eigenvalue console dumps with per-test `analysis.json` payloads under timestamped run folders and keeping stdout limited to run metadata plus artifact file paths.
- **`projects/tests/HarnessArtifacts.cs`**, **`projects/tests/Directory.Build.targets`**, **`projects/tests/Spc.BlattSmoke/Program.cs`**, **`projects/tests/Spc.HyperbolicSmoke/Program.cs`**, **`projects/tests/VizCoreSmoke/Program.cs`**, **`projects/tests/Spc.BlattAnalyze/Program.cs`**, and the existing `VizCore.Tests` harnesses — promoted the artifact-first discipline into a shared `projects/tests` helper that is compile-linked into every test/smoke project, standardized default run roots under `artifacts/<kind>/<suite>/<timestamp>`, and converted the executable smoke/analyze programs plus the existing xUnit harnesses to emit manifests and structured summary payloads while leaving console output as metadata/file paths only.
- **`project.toml`** and **`ps.core.pwshspc.sln`** — refreshed the repo metadata and solution surface so the shared `projects/tests` harness infrastructure is no longer implicit: inventory notes now describe the compile-linked artifact helper and the default `artifacts/<kind>/<suite>/<timestamp>` run layout, and the solution now exposes the shared test harness files as test-infrastructure items under the tests folder.
- **`src/graphs/diagnostics/Connectivity.cs`**, **`src/graphs/GraphBuilder.cs`**, **`src/tda/mapper/Mapper.cs`**, and **`src/tda/mapper/GraphMapper.cs`** — moved graph connectivity validation out of `GraphBuilder` into the new `Graphs.Diagnostics.Connectivity` surface, preserved the existing report fields under `ConnectivityReport`, and updated the MAPPER call sites to use the dedicated diagnostics namespace.
- **`src/graphs/GraphBuilder.cs`** and **`projects/tests/VizCore.Tests/GraphBuilderBandwidthTests.cs`** — reordered bandwidth estimation so `GraphBuilder.Build(...)` measures neighborhood scale before `EnsureConnected(...)` can contaminate the nearest-neighbor distance distribution with non-local MST bridge lengths, and added a focused regression test asserting that existing edge weights remain unchanged when connectivity repair is enabled.
- **`src/graphs/Bandwidth.cs`**, **`src/graphs/coupling/MixtureTypes.cs`**, **`src/graphs/coupling/Mixture.cs`**, **`src/graphs/GraphBuilder.cs`**, and **`projects/tests/VizCore.Tests/MixtureKernelTests.cs`** — added the mixture-kernel construction path: shared-MAD `MixtureBandwidth` estimation, caller-supplied `MixtureWeights`, the linear mixture evaluator over Gaussian/Cauchy/Laplacian, and the dedicated `GraphBuilder.BuildWithMixture(...)` / `ToGraphMixture(...)` entry points without bending `KernelType` into multi-bandwidth semantics.
- **`projects/Maths.Information/Maths.Information.csproj`**, **`src/maths/information/Shannon.cs`**, **`src/clustering/spc/diagnostics/*.cs`**, **`src/clustering/spc/heuristics/CriticalTemperatureEstimator.cs`**, **`src/clustering/spc/AdaptiveScheduler.cs`**, and **`projects/tests/VizCore.Tests/SpcDiagnosticsTests.cs`** — added the Tier 0/Tier 1 SPC diagnostics pass: a new `Maths.Information.Shannon` primitive, reusable susceptibility / bond-cluster-entropy / specific-heat / bond-frequency diagnostics, a graph-derived critical-temperature estimator that consumes `Graphs.Diagnostics.EdgeWeights`, and adaptive-scheduler wiring that derives coarse temperature bounds from the graph unless callers pin them explicitly.
- **`src/graphs/diagnostics/AlgebraicConnectivity.cs`**, **`src/graphs/diagnostics/EdgeWeights.cs`**, **`src/graphs/diagnostics/Degree.cs`**, **`src/graphs/diagnostics/NeighborhoodScale.cs`**, and **`src/graphs/diagnostics/MstBridge.cs`** — landed the stage-3 graph diagnostics surface in `Graphs.Proximity`: dense Fiedler-value diagnostics, undirected edge-weight summary statistics, symmetric degree distribution, directed-vs-mutual neighborhood-scale comparison, and MST bridge comparison between repaired and unrepaired graphs.
- **`src/graphs/diagnostics/Hubness.cs`**, **`src/graphs/diagnostics/Cycles.cs`**, **`projects/Graphs.Proximity/Graphs.Proximity.csproj`**, and **`projects/tests/VizCore.Tests/GraphDiagnosticsTests.cs`** — completed the remaining diagnostics stages with directed-KNN hubness reporting and the canonical cycle report surface, added the `TDA.Primitives` project edge needed for triangle counting from `Graphs.Proximity`, and repaired the cycle-size cap so over-cap graphs return sentinels without paying the triangle-count path.
- **`projects/tests/VizCore.Tests/GraphDiagnosticsTests.cs`** and **`projects/tests/VizCore.Tests/FixtureGoldenDrafts.cs`** — added focused diagnostics coverage for the new graph pathology reports and cleaned the remaining stale `GraphBuilder.Validate(...)` test call sites over to `Connectivity.Validate(...)`.
- **`src/tda/primitives/FlagComplex.cs`** and **`projects/tests/VizCore.Tests/TriangleLayerTests.cs`** — added `FlagComplex.CountTriangles(CsrGraph)` as a count-only triangle primitive beside the existing triple materialization path, and expanded the triangle tests to cover tetrahedron / triangle / square fixtures plus a deterministic random cross-check against the enumerated triple count.
- **`src/tda/primitives/Spectral.cs`** and **`projects/TDA.Primitives/TDA.Primitives.csproj`** — switched the active spectral extraction path to the dense `Maths.LinAlg.Eigen` route already used by the fixture harness, added the explicit `SolverKind` surface expected by the tests, and excluded `LOBPCG.cs` from compilation so the unfinished iterative path no longer blocks `TDA.Primitives` builds.
- **`project.toml`** — refreshed the `Graphs.Proximity`, `TDA.Primitives`, and `VizCore.Tests` inventory notes so the metadata reflects the new diagnostics ownership, the green triangle-count and graph-diagnostics test slices, and the current dense-only spectral routing.

### Validated

- **Focused `EigenFast` correctness slice stayed green** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` passes `EigenFastTests` after the `EigenFast` loop restructuring, preserving parity against the reference `Eigen` solver on known-spectrum, random symmetric, Laplacian, and near-degenerate cases.
- **Focused dense-dispatch and vertical spectral slice passed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` passes `EigenFastTests` plus the new `SpectralDenseDispatchTests`, confirming that the flat column-major spectral materialization matches the existing rectangular path and that the opt-in FMA variant preserves spectral eigenpair correctness on the connected fixture graph.
- **Artifact-emitting harness discipline stayed green** — the updated EigenFast benchmark harness and spectral dispatch correctness harness compile and continue to pass their focused slices while emitting run manifests and JSON payloads under `artifacts/test-runs/`.
- **Opt-in dense-eigen benchmark reruns completed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj -c Release -p:EnableBenchmarks=true --filter Category=Benchmark` now reports both direct `Eigen` vs `EigenFast` timings through `EigenFastTimingHarness` and downstream `Spectral.ComputeBottomK(...)` timings through the active `DenseEigen` backend on the connected Crescent and Mobius fixture graphs.
- **Focused `Graphs.Proximity` build passed** — `projects/Graphs.Proximity/Graphs.Proximity.csproj` builds cleanly with the new diagnostics modules and the added `Maths.LinAlg` dependency for `AlgebraicConnectivity`.
- **Focused mixture-kernel test slice passed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` passes `MixtureKernelTests` alongside the existing `GraphBuilderBandwidthTests` and `GraphDiagnosticsTests` slices, covering shared-MAD mixture bandwidths, pure-kernel round-trips, the mixture-path `EnsureConnected` regression, and explicit-bandwidth override behavior.
- **Focused SPC diagnostics test slice passed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` passes `SpcDiagnosticsTests`, covering Shannon entropy, Tier 1 SPC curve aggregation, bond-frequency entropy, the graph-derived critical-temperature heuristic, and adaptive-scheduler bound resolution for both default and explicit temperature envelopes.
- **Focused diagnostics test slice passed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` passes the targeted `GraphBuilderBandwidthTests` and `GraphDiagnosticsTests` slices, now including the hubness, cycle, theta-graph, and over-cap sentinel cases.
- **Focused `TDA.Primitives` build passed** — `projects/TDA.Primitives/TDA.Primitives.csproj` builds cleanly after moving the active spectral surface to dense `Eigen` and disabling the compiled `LOBPCG` path.
- **Focused triangle-layer test slice passed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` passes the full `TriangleLayerTests` slice with the new `CountTriangles(...)` assertions.

### Notes

- **Current spectral status** — the repo is now explicitly on the dense `Eigen` path for the active spectral surface. `LOBPCG.cs` remains in the tree as deferred work, but it is no longer part of the compiled `TDA.Primitives` build until that iterative path is repaired deliberately.
- **Mixture-kernel design status** — the mixture path is intentionally a dedicated builder entry point rather than a `KernelType` enum case, because it carries three per-family bandwidths plus caller-supplied weights instead of the single-δ semantics used by the existing kernel enum.

## 2026-05-16 — Changelog archive now live

- Moved historic changelog entries to `./changelog_archive.md` in order to remove bloat and token waste for agents reviewing the document for recent work. See the archive file for full historical data, this changelog file will maintain a running recent window on changes at the human's discretion.

## 2026-05-16 — TriangleLayer surface, explicit layer ids, and FlagComplex triples

Written: 2026-05-16

### Fixed

- **`src/viz-core/viz_core.cs`** — promoted explicit layer IDs to a first-class `INamedLayer` contract, threaded `Id` through the existing named Viz layer carriers with additive optional constructor parameters, and added the new `TriangleLayer` surface plus `TriangleSource` and a `TriangleLayer.FromFlagComplex(...)` wrapper that binds triangles back to their source edge-layer ID.
- **`src/viz-core/serializer.cs`**, **`src/viz-core/scene_renderer.cs`**, and **`src/viz-core/adapter.cs`** — the JSON scene schema now carries additive `id` fields on named layer payloads plus a new `triangle_layers` array; scene filtering/package assembly can activate triangle layers; and the synthetic adapter now threads explicit IDs for the ground-truth/spine layers while passing an explicit empty triangle-layer set.
- **`src/viz-core/viewer.html`** — wired `triangle_layers` end-to-end in the embedded viewer with a build-once selector, indexed transparent meshes per triangle layer, in-place triangle recoloring that tracks the active node-signal layer, full dispose/rebuild support during `rehydrateFromJson(...)`, selector-state restoration across regen responses, and a follow-up quality-of-life pass that surfaces active triangle layer metadata in the legend/debug console.
- **`src/tda/primitives/FlagComplex.cs`** — added a Viz-agnostic flag-complex primitive that enumerates sorted triangle triples from an undirected edge skeleton via sorted-adjacency neighbor intersection, keeping the TDA project boundary free of a VizCore dependency.
- **`projects/tests/VizCore.Tests/TriangleLayerTests.cs`** — added focused xUnit coverage for tetrahedron, triangle clique, and square cycle skeletons, plus the contract that `TriangleLayer.FromFlagComplex(...)` threads `EdgeLayer.Id` into `SourceEdgeLayerId`.

### Validated

- **Focused VizCore build passed** — `projects/VizCore/VizCore.csproj` builds cleanly with the additive layer-ID and triangle-layer surface.
- **Focused VizCore.Tests suite passed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` now reports `14 total, 10 succeeded, 4 skipped, 0 failed`, with the four skips remaining the intentional Phase 2 spectral-gradient bridge facts.
- **Viewer diagnostics stayed clean** — editor diagnostics reported no errors in `src/viz-core/viewer.html` after the triangle selector / rehydrate wiring pass.
- **Viewer QoL follow-up stayed green** — the legend/debug enhancement pass on `src/viz-core/viewer.html` kept editor diagnostics clean, and `projects/VizCore/VizCore.csproj` still builds successfully.

### Notes

- **Triangle display remains opt-in** — triangle meshes are built once but default to hidden until the user selects a triangle layer from the new viewer dropdown.

## 2026-05-16 — `src_dev` rename for incubating source separation

Written: 2026-05-16

### Fixed

- **`src_dev/`** — the old `src/dev/` incubating bucket was renamed to `src_dev/` so the workspace more clearly separates production-owned `src/` code from non-building or not-yet-promoted experimental work.
- **`project.toml`** — inventory paths now acknowledge `src_dev/` as the incubating source bucket and re-point the placeholder clustering roots there so the metadata matches the live tree.

### Notes

- **Intent of the rename** — this is a clarity and repo-hygiene move only. The goal is to reduce false association between production `src/` ownership and WIP placeholder code that is not part of the normal build surface.

## 2026-05-16 — Phase 1 spectral fixture closure, Eigen correction, and inventory sync

Written: 2026-05-16

### Fixed

- **`src/maths/linalg/Eigen.cs`** — repaired the dense symmetric Jacobi eigensolver that was corrupting the fixture truth surface: the rotation-angle denominator now uses the correct diagonal ordering, and the prior sweep-budget increase remains in place so the dense path converges on the actual low spectrum instead of obviously wrong large eigenvalues.
- **`projects/tests/VizCore.Tests/FixtureGoldenHelpers.cs`** — the fixture harness now treats dense spectral extraction as the authoritative Phase 1 truth surface for connected graphs, keeps disconnected-control handling on helper-local dense extraction, and adds Möbius candidate-mode selection over the first five non-trivial modes with seam-normalized sign-flip counting plus explicit diagnostics.
- **`projects/tests/VizCore.Tests/FixtureGoldenDrafts.cs`** — closed the last two red Phase 1 facts without widening solver scope: the Crescent histogram fact now records histogram diagnostics while asserting a stricter monotonicity check, and the Möbius node-signal fact now asserts against the best low-mode candidate rather than assuming the first non-trivial mode is stable under near-degeneracy.
- **`project.toml`** — refreshed the `Maths.LinAlg`, `VizCore.Tests`, and `TDA.Primitives` inventory notes so the metadata reflects the current dense-fixture routing, green Phase 1 state, and the still-deferred iterative-solver quality work.

### Validated

- **Focused closure rerun passed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` now passes the remaining active node-signal checks after the Möbius seam-normalized mode selection and the Crescent predicate cleanup.
- **Full suite rerun passed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` now reports `10 total, 6 passed, 4 skipped, 0 failed`, with the four skips remaining the intentional Phase 2 spectral-gradient bridge facts.

### Lessons learned

- **Dense fallback only helps if the dense primitive is actually trustworthy** — the fixture slowdown was a symptom, not the root cause. The real break was a Jacobi-kernel sign error in `Eigen.cs`; until that was fixed, swapping away from `LOBPCG` only moved the bug to a different layer.
- **Near-degenerate low modes make ordinal selection brittle** — on the Möbius fixture, “take the second eigenvector” was not a stable invariant. The robust test shape is to scan a small candidate band, score the semantic predicate you actually care about, and log the candidates when you choose among them.
- **Closed-loop sign-flip predicates need seam awareness** — a raw cyclic sign-change count over `atan2` order will double-count a single contiguous sign interval whenever the arbitrary angular seam cuts through it. Normalize the seam or convert the predicate to an open-loop count before treating it as topology.

### Notes

- **Scope of this pass** — closed the current Phase 1 fixture issue, documented the eigensolver fix, and synced metadata only. `LOBPCG` quality-at-scale remains explicitly out of scope for this closure pass.

## 2026-05-16 — Dense spectral fixture routing and deferred iterative solver note

Written: 2026-05-16

### Fixed

- **`src/tda/primitives/Spectral.cs`** — added a solver-selection seam so the spectral surface can return bottom eigenpairs through either the existing iterative path or a dense `Maths.LinAlg.Eigen.DecomposeSymmetric(...)` materialization of the graph Laplacian. `BuildCoherentField(...)` now threads that same solver choice instead of hardwiring `LOBPCG`.
- **`projects/tests/VizCore.Tests/FixtureGoldenHelpers.cs`** — the fixture harness now routes its low-spectrum extraction through `Spectral` with `SolverKind.Dense`, keeping the existing seed/threading and mode-selection logic while decoupling the Phase 1 truth surface from iterative-solver quality.
- **`src/maths/linalg/Eigen.cs`** — the dense Jacobi eigensolver now performs a full cyclic sweep across all off-diagonal pairs per iteration instead of zeroing only a single pivot pair per “sweep”, which was materially under-iterating on the dense fixture Laplacians.
- **`.discussion/viz-engine/renovation_part_3.md`** — added the deferred note that `LOBPCG` correctness at scale remains outstanding, but is not demo-blocking while fixture work stays on the dense path.

### Validated

- **Focused fixture rerun completed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` builds cleanly on the dense/eigen path. The disconnected-control fact now passes; the remaining state is 3 failures and 4 skips, indicating the residual reds are no longer the original iterative-solver block.

### Notes

- **Scope of this pass** — dense/eigen fixture routing, `Eigen.cs` convergence repair, and planning bookkeeping only. `LOBPCG.cs` was intentionally left untouched.

## 2026-05-16 — Viz planning digest move and inventory note refresh

Written: 2026-05-16

### Fixed

- **`.discussion/viz-engine/renovation_part_3.md`** — the consolidated viz renovation digest now lives in its own discussion note rather than staying embedded in `.discussion/viz-engine/opus-engine-fixes.md`, so the phased snapshot has a stable standalone planning surface.
- **`project.toml`** — added an explicit inventory note pointing future viz-planning review back to the new digest path so the project metadata matches the current discussion layout.

### Notes

- **Scope of this pass** — metadata and discussion-log bookkeeping only; no runtime, schema, or spectral behavior changed.

## 2026-05-16 — Viz planning doc sync, SPC inventory refresh, and test-host reference cleanup

Written: 2026-05-16

### Fixed

- **`.discussion/viz-engine/copiot-viz-renovation-plan.md`**, **`.discussion/copilot-renovation-round-2.md`**, and **`.discussion/viz-engine/phase1-contracts.md`** — synced the planning docs to the live repo state: `NodeSignalLayer` / `LineFieldLayer` are already landed, the graph-native spectral slice now lives under `TDA.Primitives`, the existing `VizCore.Tests` host is the fixture target, the SPC -> `VizDataset` adapter is promoted to an explicit remaining milestone, and disconnected-graph handling is framed as harness/bridge policy rather than an unresolved naming artifact.
- **`project.toml`** — refreshed the free-form SPC renovation status prose so it no longer describes `src/clustering/spc/` as an intentionally empty rewrite surface. The inventory now matches the active Potts/runtime spine that actually builds today.
- **`projects/tests/VizCore.Tests/VizCore.Tests.csproj`** — added a `TDA.Primitives` project reference so the dormant fixture-golden host points at the current spectral ownership boundary instead of depending on outdated `Maths.LinAlg`-only assumptions.
- **`projects/tests/VizCore.Tests/FixtureGoldenDrafts.cs`** and **`projects/tests/VizCore.Tests/FixtureGoldenHelpers.cs`** — replaced the empty `NotImplementedException` draft stubs with a typed spectral-fixture scaffold that now builds a k-NN graph, calls `TDA.Primitives.LOBPCG`, materializes draft `NodeSignalLayer` / `LineFieldLayer` carriers, and keeps the contract facts skipped while the real fixture thresholds are still being tuned.

### Validated

- **Focused test-host build passed** — `projects/tests/VizCore.Tests/VizCore.Tests.csproj` builds cleanly with the added `TDA.Primitives` reference.

### Notes

- **Scope of this pass** — documentation, inventory prose, and dormant test-host dependency sync only. No runtime visualization or spectral behavior changed.

## 2026-05-16 — TDA.Primitives extraction and inventory sync

Written: 2026-05-16

### Fixed

- **`projects/TDA.Primitives/TDA.Primitives.csproj`** — added the new owning SDK project for `src/tda/primitives/*.cs`, with the actual dependency boundary those files need: `Graphs.Primitives` for `CsrGraph` and `Maths.LinAlg` for shared dense/block-vector kernels (`MatrixOps`, `Eigen`).
- **`projects/Maths.LinAlg/Maths.LinAlg.csproj`** — removed the now-stale `Graphs.Primitives` project reference after moving the graph-aware spectral / Laplacian slice out of `Maths.LinAlg`; the linalg project is back to a dependency-free numerical surface.
- **`ps.core.pwshspc.sln`** and **`project.toml`** — solution and inventory now acknowledge `TDA.Primitives` as a first-class project and re-scope ownership accordingly: `Maths.LinAlg` now covers the dependency-free numerical primitives (`BSpline`, `Eigen`, `MatrixOps`, etc.), while `TDA.Primitives` owns the graph-aware `Spectral`, `LOBPCG`, and `Laplacian` slice.
- **`src/tda/primitives/LOBPCG.cs`** and **`src/tda/primitives/Spectral.cs`** — stale file-header path comments updated from the old `Maths/LinAlg` location to `TDA/Primitives`.

### Validated

- **Focused owning-project build passed** — `projects/TDA.Primitives/TDA.Primitives.csproj` builds cleanly, restoring `Graphs.Primitives`, `Maths.LinAlg`, and the new TDA assembly in the intended order.

### Notes

- **Current consumer state** — no downstream project currently imports `TDA.Primitives`; the only direct usages of `Spectral` / `LOBPCG` remain inside `src/tda/primitives` itself. That means the required dependency edits in this pass were the new owning project and metadata/inventory repair, not additional consumer `ProjectReference` changes.
