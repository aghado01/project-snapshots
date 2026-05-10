# Changelog

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
