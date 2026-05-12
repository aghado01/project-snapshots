# VizCore — Implemented Architecture

> **Status:** Pass 1–9 complete. Pass 1: 2026-05-06. Pass 2: 2026-05-08. Pass 2.1: 2026-05-09. Pass 3: 2026-05-09. Pass 4: 2026-05-09. Pass 5: 2026-05-09. Generator Picker: 2026-05-09. Pass 6: 2026-05-10. Pass 7: 2026-05-10 (VectorFieldLayer + `LocalTangent` empirical tangent flow; InstancedMesh cone glyphs; `showFlow` schema checkbox; `StatisticalEstimators` project reference in VizApi; Wing-2 Diagnostic TDA pipeline complete). Pass 8: 2026-05-11 (FigureEight Möbius spine + `MobiusSpineShape` enum + splay; `CenterCrossing` placement; shape-aware placement helpers; auto-regen on field change with debounce; graph wire pastel contrast; `ShowGaussianEllipsoids` default off; updated `RegenRequest` defaults: 10k crescent / 10k Möbius / 5k ellipsoid, `Annular` cross-section, `FigureEight` spine, `OrthogonalElbowIntersect`/`CenterCrossing` fallbacks). Pass 9: 2026-05-11 (`GraphBuilder.Build` replaces dual `SelectNeighbors` — single O(N²k) graph build shared by `BuildEdgeLayerFromCsr` and `LocalTangent`; `VizKernel` enum; `kernel`/`bandwidth` in `RegenRequest` + schema + `mergedParams` round-trip; edge weights are coupling strengths in (0,1] replacing raw distances; `FisherRaoSimplex`/`FisherRaoHalfPlane` metric split wired in VizApi dispatch; `CouplingKernels` transitive reference via `ProximityGraphs`; source tree reorganization: `src/clustering/spc/`, `src/clustering/gmm/`, `src/graphs/construction/`, `src/graphs/coupling/`, `src/graphs/tda/`; `Edge`, `CsrGraph`, `UnionFind`, `GraphBuilder` extracted to `ProximityGraphs` namespace; `MstAugmented` refactored as `ensureConnected` flag with Borůvka O(N²/phase) repair replacing Kruskal O(N² log N); `DisjointSet`/`FastUnionFind` removed).

## Layer model (`src/viz-core/viz_core.cs`, namespace `Viz`)

```
INamedLayer
  ├── LabelLayer       — N per-point integer labels (GroundTruth / SpinColor / EquilibriumCluster / GmmComponent / GmmCluster / Custom)
  ├── ScalarLayer      — N per-point double annotations (coherence, Mahalanobis, percolation, responsibility, …)
  ├── EdgeLayer        — E-entry sparse graph (src/dst/weight + optional GT cluster annotations for false-bridge coloring)
  ├── GaussianLayer    — K ellipsoids (means K×D, covariances K×D×D, weights K, optional ComponentToClusterMap)
  ├── SpineLayer       — M×D clean generating curve/manifold overlay (Arc | Manifold | MobiusTube); not indexed into N-point array
  │                      MobiusTube carries TangentBases (M×3×3 local frames [T,N,B]) from the generator
  └── VectorFieldLayer — N per-point unit tangent vectors (flat double[], N×D); Wing 2 (Diagnostic TDA):
                         empirical local PCA over k-NN adjacency via LocalTangent.Compute.
                         Air-gapped from generator internals — computed from Features only.

GeneratorParamSchema   — sections of ParamSpec entries (type, label, min/max/step, enum values, vec labels)
                         emitted as SCENE.generator_param_schema; viewer builds regen panel from this, no hardcoded HTML per generator
                         SchemaCatalog.ForGenerator(name) is the C#-side registry; add one entry per new generator

TemporalLabelSequence  — ordered LabelLayer frames along Temperature / Iteration / Depth / Custom axis
                         NOT : INamedLayer — resolved by direct name match, not filtered into a collection

PointCloud             — N×D row-major ReadOnlyMemory<double>, optional human label

VizDataset             — PointCloud + IReadOnlyList of each layer type (all optional)
```

### Three-Wings model

The viz engine organizes its diagnostic capabilities into three wings. Each wing is strictly air-gapped: it receives only the data contract from below, never generator internals.

**Wing 1 — Generative Oracle** (`SpineLayer`, `GaussianLayer[analytic]`, `LabelLayer[GroundTruth]`)
Verifies generator geometry. `SpineLayer` curves + T/N/B frame arrows are calibration material — they prove the density scaffold was shaped correctly. This wing is synthetic-only and its geometry is NOT a source of flow vectors or diagnostic signals for downstream wings.

**Wing 2 — Diagnostic TDA** (`VectorFieldLayer`)
Empirical methods applied to `Features` + the k-NN adjacency graph that VizApi builds itself. Air-gapped from generator source code. `LocalTangent.Compute` (namespace `Estimators.Tangent`): per-point arithmetic mean of k-NN neighbourhood → centred D×D scatter matrix → leading eigenvector via power iteration (20 steps, zero-alloc `ArrayPool<double>` scratch) → unit tangent. Returns flat double[] length N×D. VizApi ships this as a `VectorFieldLayer`; viewer renders InstancedMesh cones, Tier-1 `.visible` toggle. Future Wing-2 estimators: curvature, local intrinsic dimensionality, Vietoris-Rips persistence.

**Wing 3 — Algorithmic Simulator** (deferred)
Temporal/iterative clustering dynamics. SPC temperature scrubbing, χ trace, merge-tree. Requires Phase 5 (SPC artifact codecs).

**Air-gap principle:** Generators export `Features` + `Labels` only once sampling is complete. Parametric scaffolding (arc parametrization, T/N/B frames) ensures coherent structure is present in the point coordinates — but the scaffolding itself is not forwarded. `SpineLayer` entries are Wing-1 verification geometry computed by the adapter from the same parameters; they do not flow into Wing 2 or 3. VizApi receives a `SyntheticDataset` DTO and runs all empirical analysis over `Features` and a k-NN graph it builds from scratch.

## Scene pipeline (`src/viz-core/scene_renderer.cs`, namespace `Viz`)

```
SceneDescriptor
  (per-type active-name lists; null = all through)
        │
        ▼  SceneBuilder.Build(VizDataset, SceneDescriptor)
ScenePackage
  (resolved active layers + SceneRenderHints)
        │
        ▼  IRenderTarget.Render(ScenePackage, Stream)
   output
```

`SceneBuilder.Filter<T where T : INamedLayer>` — type-safe; no `dynamic`.

## Render target — Pass 1 (`src/viz-core/html_render_target.cs`, namespace `Viz.Renderers`)

`ThreeJsHtmlRenderTarget : IRenderTarget`

- Emits a self-contained `.html` file (no build step, no external assets beyond CDN).
- Three.js r160 via **importmap + `<script type="module">`**. `OrbitControls` imported as named ES module — not accessed via `THREE.OrbitControls` global.
- Point positions downcast to `Float32` for payload efficiency.
- Prefers `LabelLayerKind.GroundTruth`; falls back to first active label layer.
- 12-color qualitative palette; deterministic overflow hash for K > 12.
- `depthWrite: false` on point material (forward-compatible with ellipsoid + spine overlays).
- Auto-fits camera to bounding sphere. Cluster legend with per-cluster counts.

**Planned passes:**

- ~~Pass 2~~ ✓ — `EdgeLayer` line rendering, false-bridge coloring, VizApi compute gateway, `rehydrateFromJson`, interactive generator panel (2026-05-08)
- ~~Pass 2.1~~ ✓ — `MobiusAndEllipsoid` generator, `SpineLayerKind.MobiusTube`, schema-driven regen panel (`param_schema.cs`, `schema_catalog.cs`), `VizApi` multi-generator dispatch, geomDim 4D fix (2026-05-09)
- ~~Pass 3~~ ✓ — `GaussianLayer` ellipsoid meshes: `WriteLTo` on `CholeskyDecomposition`, `cholesky_l` flat `[K×3×3]` pre-baked in serializer (C# does the math), shared `UNIT_SPHERE_GEO`, per-component `Matrix4` built by indexing into `cholesky_l` flat array, `MeshPhongMaterial` opacity 0.22, `ComponentToClusterMap` coloring, "Ellipsoids" checkbox (Tier 1 `.visible` flip), rehydrate disposes materials only (geometry shared) (2026-05-09)
- ~~Pass 4~~ ✓ — `SpineLayer` overlay curves in viewer.html: `LineLoop` through `SpineSamples`, white seam marker at sample 0, T/N/B `ArrowHelper` frame arrows subsampled to ~16 evenly-spaced frames (MobiusTube only), "Spine curves" + "Frame arrows" checkboxes (Tier 1 `.visible` flip; arrows gated by curves), full rehydrate dispose/rebuild (2026-05-09)
- ~~Pass 5~~ ✓ — `ScalarLayer` heatmap: `min`/`max` pre-baked in serializer (C# scans `Values.Span`), 3-stop diverging palette blue→white→red in JS (`scalarToRgb`), `applyScalarLayer` writes into `color` `BufferAttribute` in-place (Tier 2, once on selection), scalar selector dropdown, legend gradient bar (2026-05-09)
- ~~Pass 6~~ ✓ — GMM overlay: `BuildGmmOverlayLayer` walks arc spine, builds per-sample T/N/B frame + covariance; `GmmComponents` slider pos 0–4 → K=1,2,4,8,16 (`DisplayValues` on `ParamSpec`); K=1 full-opacity cluster-hue, K>1 additive-blended grey-blue; `buildEdgeMesh` → `{main,bridges}` pair (intra-cluster dim + crimson false-bridge objects, Tier 1 `.visible` toggles); semantic 12-entry `PALETTE`; `UNIT_SPHERE_EDGES` wireframes; generator geometry fixes (Gaussian density bias, cosine taper, X-major axes, corrected placements). **Generalization (2026-05-10):** `BuildGmmOverlaysFromSpines` replaces bolted-on crescent-specific call; `SpineLayer.TypicalScale` propagated from geometry types (`arc.NoiseScale`, `√(halfWidth×halfThickness)`); `BuildPackage` is sole shared orchestrator (two-phase: `Build*Adapted` → Phase 1, shared edges+GMM+scene → Phase 2); `OverlaySection` in schema shared across all generators.
- ~~Pass 7~~ ✓ — **Wing-2 Diagnostic TDA pipeline** (2026-05-10): `VectorFieldLayer : INamedLayer` (flat double[] N×D unit tangents, `N`/`D` metadata). `LocalTangent.Compute` in `src/estimators/LocalTangent.cs` (namespace `Estimators.Tangent`): per-point k-NN mean → centred scatter → power iteration eigenvector; zero-alloc `ArrayPool<double>` scratch; zero vector for degenerate neighbourhoods (< `minNeighbors=3`). `IReadOnlyList<VectorFieldLayer> VectorFieldLayers` added to `VizDataset` (optional ctor param, defaults to empty). `SceneRenderHints.ShowVectorField bool`, `ScenePackage.ActiveVectorFieldLayers`. `VectorFieldLayerJson` DTO in serializer. `showFlow` bool checkbox in `OverlaySection` (schema_catalog). `StatisticalEstimators` project reference added to `VizApi.csproj`. `VizApi/Program.cs`: `SelectNeighbors` helper extracted (shared by `BuildEdgeLayer` and LocalTangent path); `LocalTangent.Compute` called with same adjacency spec → `VectorFieldLayer`. `viewer.html`: `buildFlowField(layer, cloudR)` module-level function → `InstancedMesh` of `ConeGeometry(r=2.5%cloudR, h=10%cloudR, 6-seg)`; per-instance `Matrix4` via `Quaternion.setFromUnitVectors(Y, tangent)`; per-instance color from `PALETTE[label]`; `_flowMeshes[]` module array; `#chk-flow` checkbox Tier-1 `.visible` toggle; `rehydrateFromJson` disposes/rebuilds flow meshes.
- ~~Pass 8~~ ✓ — **FigureEight Möbius spine, UX polish** (2026-05-11): `MobiusSpineShape` enum + `FigureEight` variant with splay; `CenterCrossing` placement; shape-aware placement helpers; auto-regen on field change with debounce; graph wire pastel contrast; `ShowGaussianEllipsoids` default off; updated `RegenRequest` defaults: 10k crescent / 10k Möbius / 5k ellipsoid, `Annular` cross-section, `FigureEight` spine, `OrthogonalElbowIntersect`/`CenterCrossing` fallbacks.
- ~~Pass 9~~ ✓ — **GraphBuilder wiring, VizKernel enum, source reorganization** (2026-05-11): `GraphBuilder.Build` replaces dual `SelectNeighbors` call — single O(N²k) graph build shared by edge layer and Wing-2 LocalTangent. `BuildEdgeLayerFromCsr` replaces `BuildEdgeLayer`: edge weights are coupling strengths in (0,1] (close = bright) instead of raw distances. `VizKernel { Gaussian, Cauchy, Laplacian, Linear }` enum in viz_core.cs. `kernel`/`bandwidth` added to `RegenRequest`, `mergedParams` round-trip, schema `GraphSection`. `MstAugmented` rule mapped to `ensureConnected: true` at VizApi boundary. `FisherRaoSimplex`/`FisherRaoHalfPlane` metric dispatch. Source tree reorganized: SPC files to `src/clustering/spc/`, GMM to `src/clustering/gmm/`, coupling kernels to `src/graphs/coupling/`, topology selectors to `src/graphs/construction/`, `CsrGraph`/`UnionFind` to `src/graphs/tda/`. `Edge`, `CsrGraph`, `UnionFind`, `GraphBuilder` extracted to `ProximityGraphs` namespace. `FastUnionFind`/`DisjointSet` removed. Borůvka O(N²/phase) replaces Kruskal O(N² log N) in `EnsureConnected`. All `.csproj` include paths updated.
- 2D panel layout — prerequisite for temperature-scrubbing and χ trace views

## Render target — JSON export (`src/viz-core/serializer.cs`, namespace `Viz.Renderers`)

`JsonExportRenderTarget : IRenderTarget`

- Serialises a `ScenePackage` to a schema-versioned JSON snapshot (`schema_version: 1`).
- `[JsonIgnoreCondition.WhenWritingNull]` — absent layers produce no keys; consumers can detect layer presence by key existence.
- All layer arrays are flat row-major with explicit `*_shape` fields. Enum fields serialised as strings via `JsonStringEnumConverter`.
- Constructor: `JsonExportRenderTarget(bool compact = false)` — selects between indented (readable/dev) and compact (automation/diffing) output. Both `JsonSerializerOptions` instances are static; no allocation per call.
- Intended role: checkpoint codec for automated test fixtures, snapshot diffing in CI, and as the handoff format between the offline C# pipeline and the browser client once a static artifact contract is defined.

## Adapter (`src/viz-core/adapter.cs`, namespace `Viz.Adapters.Synthetic`)

```
IVizDatasetAdapter<TSource>
  └── SyntheticDatasetAdapter : IVizDatasetAdapter<SyntheticDataset>
        Produces:
          PointCloud               (from Features)
          LabelLayer[GroundTruth]  (from Labels)
          GaussianLayer            (from EllipsoidGeometry entries — analytic covariance, empirical weight)
          GaussianLayer["Best-Fit Gaussian"]           (from ArcGeometry + ClusterCovariances — the single
                                   Gaussian a best-fit model would report for an arc cluster; rendered
                                   alongside the ground-truth ellipsoid to show the misleading shape
                                   a single Gaussian gives for a crescent/manifold cluster)
          SpineLayer[]             (from ArcGeometry + ManifoldGeometry + MobiusTubeGeometry entries;
                                   MobiusTube SpineLayer carries LocalFrames as TangentBases)
          GeneratorParamSchema     (from SchemaCatalog.ForGenerator; null for unregistered generators)
        Does NOT produce EdgeLayer — metric + proximity choices belong to the diagnostic harness

Note (geomDim): BuildGaussianLayer and BuildBestFitGaussianLayer derive geomDim from
  Covariance.GetLength(0) (always 3) rather than ambient d (which equals 4 in 4D datasets).
  Using d caused IndexOutOfRangeException on 4D smoke scenes — fixed 2026-05-09.
```

## Projects

| Project                                     | Output             | References                                                                                                                       |
| ------------------------------------------- | ------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| `projects/VizCore/VizCore.csproj`           | `VizCore.dll`      | `SyntheticDatasets`                                                                                                              |
| `projects/VizCoreSmoke/VizCoreSmoke.csproj` | `VizCoreSmoke.exe` | `VizCore`                                                                                                                        |
| `projects/VizApi/VizApi.csproj`             | `VizApi.exe`       | `VizCore`, `SyntheticDatasets`, `ProximityGraphs`, `CouplingKernels` (transitive via `ProximityGraphs`), `StatisticalEstimators` |

Key source files compiled into VizCore:

- `src/viz-core/viz_core.cs` — layer model, enums, VizDataset
- `src/viz-core/scene_renderer.cs` — ScenePackage, SceneBuilder, IRenderTarget
- `src/viz-core/html_render_target.cs` — ThreeJsHtmlRenderTarget
- `src/viz-core/serializer.cs` — JsonExportRenderTarget
- `src/viz-core/adapter.cs` — SyntheticDatasetAdapter
- `src/viz-core/param_schema.cs` — ParamSpec, ParamSection, GeneratorParamSchema
- `src/viz-core/schema_catalog.cs` — SchemaCatalog (`ForGenerator` registry + `KnownGenerators` list for the generator picker)

Key source files compiled into StatisticalEstimators (referenced by VizApi):

- `src/estimators/LocalTangent.cs` — Wing-2 empirical local tangent estimator (namespace `Estimators.Tangent`; `LocalTangent.Compute(points, adjacency, minNeighbors, powerIterations)` → flat double[] N×D unit tangents)

Smoke pipeline:

```
SyntheticData.GenerateCrescentAndEllipsoid()
  → SyntheticDatasetAdapter.Adapt()
    → VizDataset (PointCloud + LabelLayer[GT] + GaussianLayer[analytic] + SpineLayer[arc])
      → SceneBuilder.Build(dataset, descriptor)
        → ScenePackage
          → ThreeJsHtmlRenderTarget.Render(package, stream)
            → ~/viz-smoke.html
```

## Design decisions

- **`SpineLayer` is not a `ScalarLayer`** — different point set size M vs N; a fake per-point mapping would confuse the renderer.
- **`TemporalLabelSequence` does not implement `INamedLayer`** — it is resolved once by direct name match in `SceneBuilder.Build`, not filtered into a collection. Implementing the interface would imply a filtering contract it does not participate in.
- **No `EdgeLayer` from the adapter** — the adapter's contract is the ground-truth view. Edge computation requires metric and proximity rule choices that are configuration decisions belonging to the diagnostic harness.
- **No `GaussianLayer` for the crescent** — a single Gaussian cannot faithfully represent a crescent. Callers wanting a diagnostic fit add it separately.
- **`VizCore.dll` currently references `SyntheticDatasets`** — `adapter.cs` is compiled into `VizCore.csproj`, which creates a transitive dependency on `SyntheticDatasets`. This is intentional for the current phase (we need real data to test the rendering pipeline and a standalone adapter has no payoff until there are multiple data sources). Known debt: once a second data source or a production use of `VizCore.dll` appears, `adapter.cs` should move to a separate `VizAdapters.csproj` class library so `VizCore.dll` becomes zero-dependency.
- **`tangent_scatter.cs` is excluded from all builds and needs reintegration** — `src/viz-core/tangent_scatter.cs` is an early draft of tangent-space covariance estimation for the viz engine. It is NOT included in `VizCore.csproj` (which uses `EnableDefaultCompileItems=false`) and does not compile against the current manifold API (it references a generic `IRiemannianManifold<TPoint,TTangent>.ToArray()` that no longer exists). The correct implementations are already in `src/estimators/`: `KarcherScatter.Compute` (Riemannian sample covariance around a fixed location, span-based, with consistency factor) and `WeiszfeldScatter` (robustness-weighted variant). The viz engine intent: the `GaussianLayer` viewer (Pass 3) will expose a **estimator dropdown** alongside the ellipsoid toggle — choices include empirical (current `BuildBestFitGaussianLayer`), Karcher/Riemannian, and Weiszfeld-robust — so users can visually compare how initialization estimator choice affects ellipsoid shape on non-convex clusters (crescent, Möbius tube). To implement: add `VizApi/Program.cs` cases that call `GeometricMean.ComputeWithScatter` or `KarcherScatter.Compute` per cluster group, package each as an additional named `GaussianLayer` in the `ScenePackage`, and surface the dropdown in the schema-driven regen panel via a new `estimator` enum `ParamSpec` in `schema_catalog.cs`. `tangent_scatter.cs` can be deleted once this is done.
- **Live API compute (current interactive path)** — the interactive path is `VizApi`, an ASP.NET Core Minimal API (`projects/VizApi/`). The viewer posts a `RegenRequest` to `POST /api/regen`; the server runs the full C# pipeline and returns a fresh `ScenePackage`. The pre-computed edge-grid strategy (baking multiple named `EdgeLayer`s at generation time) remains the right approach for the static-first/portfolio path (§6.1 of `visualization-engine.md`), but is not the primary interactive mechanism while `VizApi` serves the diagnostic dashboard.
- **Schema-driven regen panel + generator picker** — `SCENE.generator_param_schema` is emitted as a `GeneratorParamSchema` JSON object by every registered generator. `viewer.html` reads this at load time and constructs all controls dynamically via `rebuildGenPanel(gp, schema)` (called from `initGenPanel` and from `rehydrateFromJson` on every generator switch). `currentGenSchema` is module-scope state tracking the active generator’s schema; `harvestParams` reads it and reads `sel-generator` for the discriminator. `#gen-body` is an empty div; no hardcoded HTML per generator exists in the viewer. The `<select id="sel-generator">` dropdown is populated from `SCENE.generator_catalog` (a `string[]` emitted from `SchemaCatalog.KnownGenerators`); selecting a different generator immediately dispatches a `{ generator: name }` regen with C# record defaults, then calls `rehydrateFromJson`. To add a new generator to the panel: (1) add a `GeneratorParamSchema` constant and one entry in `KnownGenerators` in `schema_catalog.cs`, (2) add a `BuildXxxPackage` case in `VizApi/Program.cs`. The viewer requires no changes.
- **2D subplot layout** — some views (temperature scrubbing, χ susceptibility trace, merge-tree dendrogram) are inherently 2D charts, not 3D scene geometry. The current renderer emits a single full-page canvas. At some point the HTML template must grow a layout that accommodates 2D subplots alongside the 3D scene — exact layout TBD, but the structural split (3D canvas + subplot area) is a prerequisite for the SPC temperature-scrubbing use case. The χ trace panel is what locates T_c and makes scrubbing interpretable. D3.js is the likely candidate for the 2D panels (see `visualization-engine.md` §9).
