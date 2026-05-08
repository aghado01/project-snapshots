# VizCore — Implemented Architecture

> **Status:** Pass 1 + Pass 2 complete. Pass 1: 2026-05-06 (point cloud + GT label coloring). Pass 2: 2026-05-08 (EdgeLayer rendering, VizApi compute gateway, interactive regeneration panel).

## Layer model (`src/viz-core/viz_core.cs`, namespace `Viz`)

```
INamedLayer
  ├── LabelLayer       — N per-point integer labels (GroundTruth / SpinColor / EquilibriumCluster / GmmComponent / GmmCluster / Custom)
  ├── ScalarLayer      — N per-point double annotations (coherence, Mahalanobis, percolation, responsibility, …)
  ├── EdgeLayer        — E-entry sparse graph (src/dst/weight + optional GT cluster annotations for false-bridge coloring)
  ├── GaussianLayer    — K ellipsoids (means K×D, covariances K×D×D, weights K, optional ComponentToClusterMap)
  └── SpineLayer       — M×D clean generating curve/manifold overlay (Arc | Manifold); not indexed into N-point array

TemporalLabelSequence  — ordered LabelLayer frames along Temperature / Iteration / Depth / Custom axis
                         NOT : INamedLayer — resolved by direct name match, not filtered into a collection

PointCloud             — N×D row-major ReadOnlyMemory<double>, optional human label

VizDataset             — PointCloud + IReadOnlyList of each layer type (all optional)
```

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
- Pass 3 — `GaussianLayer` ellipsoid meshes + `ComponentToClusterMap` color toggle (GT ellipsoid vs. best-fit Gaussian side-by-side)
- Pass 4 — `SpineLayer` overlay curves, `TemporalLabelSequence` scrubbing
- Pass 5 — `ScalarLayer` per-point heatmap coloring (coherence, Mahalanobis, responsibility, log-likelihood — replaces label palette with continuous diverging scale for the active scalar)
- Pass 6 — Tangent frame arrows (per-point or per-spine-sample directional glyphs from `SpineLayer.TangentBases`; expresses local manifold geometry in the 3D scene)
- 2D panel layout (see design decision below) — prerequisite for the temperature-scrubbing and χ trace views to be useful

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
          SpineLayer[]             (from ArcGeometry + ManifoldGeometry entries)
        Does NOT produce EdgeLayer — metric + proximity choices belong to the diagnostic harness
```

## Projects

| Project                                     | Output             | References                                        |
| ------------------------------------------- | ------------------ | ------------------------------------------------- |
| `projects/VizCore/VizCore.csproj`           | `VizCore.dll`      | `SyntheticDatasets`                               |
| `projects/VizCoreSmoke/VizCoreSmoke.csproj` | `VizCoreSmoke.exe` | `VizCore`                                         |
| `projects/VizApi/VizApi.csproj`             | `VizApi.exe`       | `VizCore`, `SyntheticDatasets`, `ProximityGraphs` |

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
- **Live API compute (current interactive path)** — the interactive path is `VizApi`, an ASP.NET Core Minimal API (`projects/VizApi/`). The viewer posts a `RegenRequest` to `POST /api/regen`; the server runs the full C# pipeline and returns a fresh `ScenePackage`. The pre-computed edge-grid strategy (baking multiple named `EdgeLayer`s at generation time) remains the right approach for the static-first/portfolio path (§6.1 of `visualization-engine.md`), but is not the primary interactive mechanism while `VizApi` serves the diagnostic dashboard.
- **2D subplot layout** — some views (temperature scrubbing, χ susceptibility trace, merge-tree dendrogram) are inherently 2D charts, not 3D scene geometry. The current renderer emits a single full-page canvas. At some point the HTML template must grow a layout that accommodates 2D subplots alongside the 3D scene — exact layout TBD, but the structural split (3D canvas + subplot area) is a prerequisite for the SPC temperature-scrubbing use case. The χ trace panel is what locates T_c and makes scrubbing interpretable. D3.js is the likely candidate for the 2D panels (see `visualization-engine.md` §9).
