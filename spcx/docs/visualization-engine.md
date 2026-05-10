# Visualization Engine — Design

> **Status:** DRAFT v0.7 — Phases 2–4 and 6–7 implemented; Wing 1/2 architecture settled (interactive VizApi server, schema-driven regen panel, edge rendering with false-bridge highlighting, spine/frame overlays, scalar heatmap, generator picker, GMM overlay with K-slider, semantic PALETTE, Wing-2 empirical local tangent flow via `LocalTangent` + `VectorFieldLayer` + InstancedMesh cones)
> **Date:** 2026-05-10
> **Purpose:** Define the shape of a visualization layer that consumes the same primitives as the computing engine (synthetic datasets, distance metrics, proximity graphs, SPC state, GMM fits) to produce both pedagogical/marketing artifacts and a diagnostic tool that helps users understand configuration trade-offs.
> **Sibling documents:** [state-engine-design.md](./state-engine-design.md) for the artifact contract this layer consumes, [spc-maturity.md](./spc-maturity.md) and [gmm-maturity-extentions.md](./gmm-maturity-extentions.md) for the semantic objects rendered.

---

## 1. Why this exists

> **Organizing principle.** SPC (or an equivalent graph prior) owns _cluster identity_; recursive GMM owns _local spatial approximation_. The visualization engine's job is to make that division of labour visible. Once a viewer sees the two responsibilities operating side by side, the crescent-plus-ellipsoid problem stops looking like "merge these Gaussians" and starts looking like "preserve topology while fitting density at multiple scales."

The `pwshspc` library composes synthetic datasets, distance metrics, proximity graph rules, coupling kernels, the SPC simulation, robust estimators, and the GMM into an end-to-end clustering pipeline. The conceptual content of that pipeline — _why_ a given combination of metric and graph rule succeeds where another fails on a particular topology — is not captured in static documentation or unit tests. It lives in a user's mental model.

A visualization engine has two distinct jobs:

- **Diagnostic / educational.** Let a user sweep through metric × graph × kernel × GMM-strategy combinations on a fixed dataset and watch the pipeline's behaviour change in real time. The user builds intuition for _which_ configuration matches _which_ data topology, which is otherwise hard-won through failed runs.
- **Portfolio / marketing.** Produce hosted, interactive case studies of the pipeline operating on chosen exotic topologies (intersecting manifolds, hyperbolic embeddings, hierarchical scales). Onlookers should be able to drag a temperature slider, rotate a 3D scene, toggle metric lenses, and see the algorithm's mechanics without reading the source.

### 1.1 The three wings (diagnostic capability model)

The diagnostic dashboard is organized into three wings that operate on the same point cloud with strictly increasing analytical distance from the generator. The wings are air-gapped from one another: each receives only the data contract below it, never generator internals.

| Wing       | Name                  | Input contract                                                                             | Purpose                                                                                                                                                                                                                                                                                                                                 |
| ---------- | --------------------- | ------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Wing 1** | Generative Oracle     | `Features` + `Labels` + `SpineLayer`                                                       | Verifies generator geometry. Spine curves + T/N/B arrows prove that the intended density scaffold is present in the data. `SpineLayer` is calibration material only — it is never a source of flow vectors or diagnostic signals for downstream wings.                                                                                  |
| **Wing 2** | Diagnostic TDA        | `Features` + `Labels` + k-NN adjacency (computed by VizApi independently of the generator) | Empirical topology diagnostics with zero model assumptions. `LocalTangent.Compute` runs local PCA over k-NN neighbourhoods → a pre-baked `VectorFieldLayer` (flat N×D unit tangents). This is how structure is _found_, not _asserted_. Future Wing-2 analyses: curvature estimation, local intrinsic dimensionality, Rips persistence. |
| **Wing 3** | Algorithmic Simulator | `Features` + `Labels` + SPC/GMM artifacts                                                  | Temporal/iterative algorithm visualization — SPC temperature scrubbing, χ trace, merge-tree. Deferred (requires Phase 5).                                                                                                                                                                                                               |

**Air-gap rule:** generators export `Features` and `Labels` only once sampling is complete. The parametric equations that produced those coordinates are scaffolding discarded at the generator boundary. `SpineLayer` entries in the adapter represent Wing-1 verification geometry derived from the same parameters, but they do not cross into Wing 2 or 3. VizApi's diagnostic path never touches generator internals — it receives a `SyntheticDataset` DTO and runs empirical methods over `Features` and the k-NN graph it builds itself.

**Topological fidelity:** generators are designed with topological awareness — `CrescentEllipsoid` uses cosine-tapered noise along the arc parametrization, `MobiusEllipsoid` uses the T/N/B frame to distribute cross-section mass — precisely so that coherent empirical structure (tangent flow, local PCA alignment) can be _found_ by Wing 2. The generator's job is to put the structure there; Wing 2's job is to find it without being told where it is.

Both jobs are served by the same engine. The diagnostic case has more knobs exposed and the portfolio case bakes a curated subset, but the underlying primitives are identical. Visualization is treated as a **first-class consumer of the project's primitives, not a marketing afterthought** — the architectural invariants in §2 enforce this discipline.

## 2. Anchors and invariants

1. **Artifact-driven.** The visualization engine is a _consumer_ of state-engine artifacts. It does not run alternative compute paths. If the state engine cannot already produce an artifact for a quantity, that quantity does not appear in the visualization until the codec exists.
2. **Shared primitives.** The same `DistanceMetrics`, `ProximityGraphs`, `CouplingKernels`, `SyntheticDatasets`, and `GaussianMixture` projects that drive headless runs drive the live visualization. There are no parallel reimplementations in JavaScript, Python, or another runtime.
3. **Static-first.** The default deployment shape is a static site backed by pre-generated artifact bundles. Live recomputation is opt-in via a separate compute path (see §6) and is not required for any visualization.
4. **Pedagogical fidelity.** Visualizations must reflect what the engine actually computes, not a stylized reduction. If two metric choices produce the same graph in the visual, they produce the same graph in the engine.
5. **Two truths simultaneously.** When the GMM strategy is recursive, the renderer must show _both_ the topological partition (which logical cluster a point belongs to) _and_ the density approximation (which Gaussian component fits it locally) at the same time. Showing only ellipsoids hides why the model is correct on a non-convex shape; showing only SPC partitions hides how the curved density is being covered piecewise. This is the visual analog of the `ManifoldMixture` separation between `DensityModel` and `ComponentToClusterMap`.

## 3. Scope

In scope:

- 2D / 3D rendering of synthetic and (later) real point clouds with cluster-coloured assignments and adjustable opacity.
- Edge rendering for proximity graphs (k-NN, Mutual k-NN, ε-ball, MST-augmented) under a selectable distance metric (Euclidean, Poincaré, Fisher-Rao, Jensen-Shannon, Mahalanobis, …).
- Tangent-vector / local-frame overlays for manifold-aware metrics on synthetic generators where the analytic frame is known.
- SPC temperature scrubbing — render spin assignments, equilibrium clusters, and bond cluster sizes at a chosen T from the artifact stream.
- Replay of `spc/spin-observation/v3` delta chains by `MaterializeAt(epoch)` in the browser.
- Susceptibility (`thermo/chi-trace/v1`) and merge-tree (`thermo/merge-tree/v1`) renderings; cross-axis interaction (clicking a peak scrolls the 3D scene to T\*).
- GMM handoff visualisation — render coherence-weighted core points, initial means and covariances, and the recursive shattering of one cluster into tiled components.
- A "GMM strategy" toggle: flat multivariate vs. recursive multivariate (with `ManifoldMixture` topology preserved via component-to-cluster mapping; see [gmm-maturity-extentions.md](./gmm-maturity-extentions.md)).
- A configuration-matrix dashboard exposing dataset, metric, graph rule, kernel, T, and GMM strategy as user controls.

Out of scope (initial):

- Authoring synthetic datasets in the browser. Datasets are pre-generated by the C# `SyntheticDatasets` project and published as artifacts.
- Real-time fitting of large datasets in the browser. Live fitting is restricted to small N (≤ a few thousand) as a demo path; production results are precomputed.
- Editorial / annotation tooling beyond simple captions on case-study pages.

### 3.1 Five canonical views

The scope items above resolve into five named views. Each view answers a specific pedagogical question; together they let a user trace a point from raw coordinates to a final cluster label and see where each stage of the pipeline contributed.

| View                  | What it shows                                                                                                                             | Pedagogical role                                                                                  | Backing layer(s)                                                                              |
| --------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- |
| **Raw cloud + truth** | Synthetic point cloud coloured by ground-truth labels; optional ambient ↔ intrinsic lens for manifold-aware datasets                      | "What is the underlying topology, and where is geometric overlap real vs. metric-induced?"        | `PointCloud`, `LabelLayer[GroundTruth]`, `SpineLayer`                                         |
| **Graph**             | k-NN / mutual k-NN / ε-ball / MST-augmented connectivity over the same points under a selectable metric                                   | "Where are the false bridges? Which proximity rule severs them cleanly?"                          | `EdgeLayer` (with `EdgeClusterSrc`/`EdgeClusterDst` annotation)                               |
| **SPC sweep**         | Spin assignments and equilibrium clusters scrubbing across temperature; coherence-coloured stable-core vs. ambiguous-boundary distinction | "Where did the topological split actually come from, and how confident is each point's identity?" | `TemporalLabelSequence`, `LabelLayer[EquilibriumCluster]`, χ trace, merge-tree                |
| **Recursive GMM**     | Parent node, recursive child tiling, local ellipsoids per leaf, split-criterion outcomes, and the local tangent chart on manifold data    | "How is the curved density actually being covered piecewise?"                                     | `GaussianLayer`, `LabelLayer[GmmComponent]`                                                   |
| **Assignment**        | Side-by-side rendering of component id and cluster id with the `ComponentToClusterMap` remap drawn explicitly between them                | "Why are these K density bumps a single logical cluster?"                                         | `LabelLayer[GmmComponent]` + `LabelLayer[GmmCluster]` + `GaussianLayer.ComponentToClusterMap` |

The Assignment view is the visual proof that flattening does not lose topology. Without it a viewer of the Recursive GMM view sees K Gaussian bumps and reasonably concludes there are K clusters; the Assignment view is what makes the `ManifoldMixture` round-trip legible.

These views are not separate UI tabs by mandate — a single dashboard can compose multiple views in one canvas. They are the named pedagogical units that the renderer is responsible for being able to produce.

## 4. Three-layer architecture

| Layer          | Responsibility                                                                           | Inputs                                                                    | Outputs                                                                                                              |
| -------------- | ---------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| **Geometry**   | Compute distances, tangent frames, gradient fields under a selected metric               | `double[][]` features, metric ID                                          | Distance matrix or KNN list, optional tangent frames                                                                 |
| **Topology**   | Build / threshold the proximity graph from a distance source                             | Distance matrix or KNN list, graph rule, k or ε                           | Edge list with weights, k-NN adjacency for Wing-2 estimators                                                         |
| **Estimators** | Wing-2 empirical analysis over the adjacency graph (air-gapped from generator internals) | k-NN adjacency, `double[][]` features                                     | Pre-baked flat arrays: `VectorFieldLayer` (local tangents), future: curvature, intrinsic dimensionality, persistence |
| **Render**     | Draw point clouds, edges, ellipsoids, flow glyphs, traces, dendrograms                   | Edge list, point coordinates, GMM parameters, vector field layers, traces | Frames on a canvas                                                                                                   |

C# performs all computation in layers 1–3 before the JSON reaches the browser. The browser does no math — it reads pre-baked flat arrays and renders them.

### 4.1 Render canvas primitives

The minimum primitive set:

- **Instanced point rendering** with per-point colour (cluster id) and alpha (opacity / coherence weight). Scales to ~10⁵ points on commodity hardware via WebGL or equivalent.
- **Line segments** for graph edges, with per-edge colour (kept-vs-pruned diagnostics) and width (coupling strength under a kernel).
- **Translucent meshes for Gaussian ellipsoids.** A K-component flat GMM contributes K ellipsoid meshes; the ellipsoid for component k is the Cholesky factor of `Σ_k` applied to a unit sphere, translated to `μ_k`. Opacity is configurable per component to let users see core points through their fitted ellipsoid.
- **2D SVG / canvas plots** for the χ trace, cluster-count trace, and the merge-tree dendrogram. These cross-link with the 3D scene by temperature.
- **Camera** with orbit / pan / zoom and persistent state across configuration changes (so toggling metric does not reset the view).

## 5. Data pipeline

The visualization engine consumes the same artifacts that the headless engine writes. There is no visualisation-only file format.

```
┌──────────────────┐
│   C# engine      │  produces run-root/{run-id}/ with manifest, artifact log,
│  (headless)      │  and stage directories per state-engine-design.md §26.
└────────┬─────────┘
         │ static publish (gh pages, vercel, …)
         ▼
┌──────────────────┐
│  Static host     │  serves run.manifest.json + .bin.br / .lpac.br / .json
└────────┬─────────┘
         │ HTTP fetch + native Brotli decode
         ▼
┌──────────────────┐
│  Browser client  │  reads manifest, opens stream readers, runs MaterializeAt,
│   (Three.js +    │  renders.
│   D3.js)         │
└──────────────────┘
```

Notes:

- **Brotli over the wire is free.** The engine's `.bin.br` artifacts are decoded by the browser without intermediate transcoding.
- **The manifest is the entry point on the client too.** The same manifest contract that drives the offline analysis consumer (state engine §3) drives the visualization client. There is no JSON-bundle adapter layer between the engine and the renderer.
- **Replay = MaterializeAt.** Scrubbing the temperature slider triggers `MaterializeAt(T_index)` against the spin observation stream, applying the anchor + delta chain to reconstruct the spin array at the chosen T. This is the same operation the headless replay path performs (see state-engine-design.md §20).
- **Cross-run viewing.** A user can load multiple run directories side-by-side to compare configurations on the same dataset (e.g., Euclidean vs. Poincaré on `HyperbolicBlobs`).

## 6. Compute model

Two coexisting paths:

### 6.1 Static-first (default)

All compute happens in C# offline. Artifacts are published to a static host. The client reads, decodes, and renders. No backend, no database, no live computation. This is the right shape for portfolio case studies and for any visualization the project itself ships.

### 6.1a Server-side API (VizApi — current interactive path)

A local ASP.NET Core Minimal API (`projects/VizApi/`) is the first-class interactive path. The browser viewer posts `RegenRequest` JSON to `POST /api/regen`; the server executes the full C# compute pipeline (`CrescentAndEllipsoid → SyntheticDatasetAdapter → SceneBuilder → JsonExportRenderTarget`) and returns a `ScenePackage` JSON. No Wasm, no bundler. The same C# assemblies that run headlessly also run behind the API — zero code duplication. This is the operative implementation of the diagnostic/educational use case described in §1.

### 6.2 Live (optional, WebAssembly)

For the diagnostic / educational use case, the user wants to see what happens _now_ when they change a knob — building a 12k-edge graph under a different metric, rerunning a small SPC sweep, fitting a GMM on a 2D point cloud they sketched. The shape that does not duplicate the engine in a second language: compile the existing C# assemblies (`DistanceMetrics`, `ProximityGraphs`, `CouplingKernels`, `GaussianMixture`, eventually `SpcCore`) to WebAssembly and call them from the browser via Blazor WebAssembly or `wasi-experimental`.

Constraints:

- Live computation is bounded to small N. The engine's zero-allocation hot path translates to Wasm with a real but tolerable speed penalty; the binding cost dominates for tiny payloads.
- The SW kernel itself (with its cancellation, RNG, and accumulator state) is the most expensive thing to compile. Live SPC is the last piece to be wired up, not the first.
- Live results are not persisted as artifacts; if the user wants to retain or share a result, they download a manifest bundle (engine emits one in-memory).

This path is opt-in per page. A portfolio case study does not pull the Wasm runtime; only an interactive sandbox does.

## 7. Configuration-matrix UI

The diagnostic dashboard exposes the engine's dispatch axes as user controls. Suggested surface:

| Control                  | Bound to                                      | Notes                                                                                                        |
| ------------------------ | --------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Dataset dropdown         | `SyntheticDatasets` enum                      | TwoMoons, BlattHierarchy, AnisotropicGaussian, GaussianManifold, HyperbolicBlobs, Simplex, SparseSupports, … |
| Metric dropdown          | `SpcMetric` enum / `IDistanceMetric` registry | Euclidean, Poincaré, Fisher-Rao, Jensen-Shannon, Mahalanobis, …                                              |
| Metric lens toggle       | ambient ↔ intrinsic                           | When the metric exposes a tangent frame, render local frames as small arrows overlaid on the points          |
| Graph rule dropdown      | `NeighborRule` enum (`Viz.NeighborRule`)      | Knn, MutualKnn, EpsilonBall, MstAugmented                                                                    |
| `k` / `ε` slider         | proximity rule parameter                      | Scrubs graph density                                                                                         |
| Coupling kernel dropdown | `CouplingKernel` enum                         | Gaussian (default), Cauchy, Laplacian, Linear                                                                |
| Temperature slider       | T index in `SpcBatchResult.Temperatures`      | Scrubs SW state via `MaterializeAt`                                                                          |
| GMM strategy toggle      | flat multivariate ↔ recursive multivariate    | Switches handoff target; recursive shows component-to-cluster mapping                                        |
| Cloud opacity slider     | per-point alpha                               | Lets the user see graph edges and ellipsoids through dense regions                                           |
| Camera reset             | view state                                    | One-click recentre                                                                                           |

The control state is itself serialisable, so a user can share a permalink that fully reconstructs a view — including the underlying run id, configuration, and current scrub positions.

## 8. Pedagogical scenarios

A small, curated set of scenarios serves both as test fixtures for the visualization engine and as the first portfolio case studies. Each scenario pairs a dataset with a configuration matrix that produces a clear "wrong-vs-right" contrast:

| Scenario                 | Dataset                                                                           | Trap config                 | Resolution config                                 | What the user sees                                                                                                                                    |
| ------------------------ | --------------------------------------------------------------------------------- | --------------------------- | ------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| **High-D reachability**  | `Simplex` / `SparseSupports`                                                      | `JensenShannon` + `Knn`     | `JensenShannon` + `MstAugmented`                  | k-NN fragments under distance concentration; MST bridges restore a connected graph without local distortion                                           |
| **Curvature**            | `HyperbolicBlobs` (Poincaré disk)                                                 | `Euclidean` + `EpsilonBall` | `Poincaré` + `EpsilonBall` (or `Knn`)             | Boundary clusters separate cleanly under the geodesic metric where they were tangled in ambient space                                                 |
| **Density variation**    | `BlattHierarchy`                                                                  | `EpsilonBall` (any metric)  | `Knn` (same metric)                               | ε-ball collapses dense regions and disconnects sparse ones; k-NN equalises connectivity                                                               |
| **Orthogonal manifolds** | `GenerateCrescentAndEllipsoid(placement: OrthogonalElbowIntersect)`               | Euclidean + Mutual k-NN     | manifold-aware metric + Mutual k-NN               | The "ghost intersection": orthogonal tangent frames cause the metric to refuse the bridge that ambient geometry would draw                            |
| **Recursive tiling**     | `GenerateCrescentAndEllipsoid(placement: OrthogonalElbowIntersect)`, post-handoff | flat multivariate GMM       | recursive multivariate GMM with `ManifoldMixture` | Flat GMM smears one ellipse across the crescent; recursive tiles it with several components and the topology is recovered via `ComponentToClusterMap` |

Each scenario is a self-contained run directory plus a small JSON of UI state. The visualization engine does not need scenario-specific code; it needs only the artifact contract and the controls.

## 9. Frontend technology choices (proposal)

The detailed framework choice is open, but the constraints argue for:

- **Three.js** (or equivalent WebGL wrapper) for the 3D scene. It supports instanced rendering, custom shaders, and mesh transparency at the scale required.
- **D3.js** for the 2D thermo/dendrogram views and the linked cross-axis interactions.
- **Vanilla TypeScript** for the control surface and the artifact-loading client. Avoid heavy framework dependencies that complicate static deployment.
- **Avalonia or another desktop UI** is a parallel option for an offline diagnostic tool that runs against local run directories. The render code is shareable in spirit but not in implementation between the two paths; the artifact contract is what makes them interchangeable for the user.

A web-first start lowers the barrier for portfolio embedding; an Avalonia version can follow once the artifact contract is mature.

## 10. Phasing

Suggested order. Each phase produces something deployable on its own.

1. ~~**Static loader + manifest reader.**~~ (deferred — static artifact contract not yet defined)
2. **[DONE] Synthetic dataset rendering.** `VizCore` layer model (`PointCloud`, `LabelLayer`, `ScalarLayer`, `EdgeLayer`, `GaussianLayer`, `SpineLayer`, `TemporalLabelSequence`, `VizDataset`), `SceneDescriptor` / `ScenePackage` / `SceneBuilder` pipeline, `SyntheticDatasetAdapter`, `JsonExportRenderTarget`. Projects: `VizCore`, `VizCoreSmoke`, `VizApi` (ASP.NET Core Minimal API, `POST /api/regen`, `ScenePackage` JSON). `viewer.html` is the live Three.js viewer with OrbitControls, auto-fit, cluster legend, and a schema-driven regen panel that round-trips all generator params.
3. **[DONE] Distance + graph layer.** `EdgeLayer` with `EdgeClusterSrc`/`EdgeClusterDst` annotation; `buildEdgeMesh` returns `{main, bridges}` pair — intra-cluster edges dim at opacity 0.09, false-bridge edges crimson at 0.85; Tier 1 `.visible` toggles. Metric dropdown (Euclidean / Manhattan / Cosine), graph rule dropdown (Knn / MutualKnn / EpsilonBall / MstAugmented), `k` and `ε` sliders. Passed as part of `RegenRequest`, merges into `generatorParams` for round-trip.
4. **[DONE] Tangent-frame overlay.** `SpineLayer` LineLoop curves rendered per cluster; `T/N/B` `ArrowHelper` frame arrows at each spine sample for `MobiusTube` kind; spine and arrow toggle checkboxes. `SpineLayer.TypicalScale` propagated from generator geometry.
5. **SPC temperature scrubbing.** Read `spc/spin-observation/v3` and `thermo/equilibrium-clusters/v1`; scrub T. χ trace and merge-tree views as 2D side panels.
6. **[DONE — first pass] GMM overlay.** `GaussianLayer` ellipsoid meshes with Cholesky-derived `Matrix4`; `UNIT_SPHERE_EDGES` wireframe siblings; `GmmComponents` slider (pos 0–4 → K = 1, 2, 4, 8, 16); K=1 cluster-hue normal-blended, K>1 additive-blended grey-blue; `BuildGmmOverlaysFromSpines` generator-agnostic overlay (sigma from arc geometry + `SpineLayer.TypicalScale`). Full handoff pipeline (`ComponentToClusterMap`-coloured tiling from SPC→GMM) is a later pass.
7. **[DONE] Wing-2 empirical local tangent flow.** `VectorFieldLayer : INamedLayer` holding flat N×D unit tangents. `LocalTangent.Compute` (in `src/estimators/`, namespace `Estimators.Tangent`) runs local PCA over the k-NN adjacency VizApi already builds for the EdgeLayer: per-point arithmetic mean → centred D×D scatter → leading eigenvector via power iteration (20 steps, zero-alloc `ArrayPool` scratch). Degenerate neighbourhoods (< `minNeighbors=3`) produce zero vectors rendered as invisible instances. `VizApi/Program.cs`: `SelectNeighbors` helper (extracted from `BuildEdgeLayer`) called a second time to get `int[][]` adjacency → `LocalTangent.Compute` → `VectorFieldLayer("Local PCA Flow", …)` added to `VizDataset`. Viewer: `InstancedMesh` of `ConeGeometry(r,h,6)` (r=2.5% cloudR, h=10%); per-instance `Matrix4` from `Quaternion.setFromUnitVectors(Y, tangent)`; per-instance color from `PALETTE[label]`; `#chk-flow` Tier-1 `.visible` toggle; `showFlow` bool in `OverlaySection` schema.
8. **[PARTIAL] Configuration-matrix dashboard.** Schema-driven regen panel and generator picker implemented (`KnownGenerators`, `generator_catalog`, `sel-generator` dropdown, `rebuildGenPanel`). Coupling kernel and temperature controls remain; full end-to-end case study deferred to after Phase 5.
9. **Live compute path (Wasm).** Compile C# primitives, expose a sandbox page.

Phases 1–7 do not require Wasm. Phase 8 is the only one that needs the live runtime.

## 11. Relationship to other docs

- The **artifact contract** consumed by the visualization client is defined in [state-engine-design.md](./state-engine-design.md) Parts I and II. Codecs added here (`thermo/merge-tree/v1`, `thermo/cluster-profile/v1`, the spin observation chain) all participate.
- The **semantic objects** rendered — `SpcTreeMatrix`, `SpcClusterProfile`, the GMM `ManifoldMixture` with its `ComponentToClusterMap` — are defined in [spc-maturity.md](./spc-maturity.md) §17 and [gmm-maturity-extentions.md](./gmm-maturity-extentions.md). Visualisation does not invent new ones; it renders these.
- The **shared partition-cutting layer** ([project-primer.md](./project-primer.md)) shows up as a UI affordance: criteria like `MaxK`, `ElbowCriterion`, `SusceptibilityPeak` are exposed as cut-controls when scrubbing the temperature slider or the GMM depth selector.

## 12. Viewer rendering contract

All interactive state changes in `viewer.html` must follow the **build-once / toggle-visibility** rule. This is a hard invariant, not a suggestion.

**Three cost tiers:**

| Operation                      | When it fires       | Cost                                                         |
| ------------------------------ | ------------------- | ------------------------------------------------------------ |
| `.visible = true/false`        | Any toggle          | Free — Three.js skips the draw call entirely                 |
| `attribute.needsUpdate = true` | Color/scalar change | O(N) buffer re-upload, fires once per user action            |
| Geometry rebuild / `scene.add` | Load time only      | GPU allocation + GC — never in event handlers or `animate()` |

**Rule for `animate()`:** it contains exactly two lines — `controls.update()` and `renderer.render(scene, camera)`. No conditional logic, no state reads. If something needs to be recomputed every frame, it belongs in a `controls.addEventListener('change', ...)` handler, not in the loop.

**Rule for layer toggles:** all layer objects (edge meshes, gaussian meshes, spine lines, scalar point clouds) are built at load time, added to the scene invisible, and shown/hidden via `.visible`. Switching between two `EdgeLayer`s costs one property write per mesh object.

**Rule for `SceneRenderHints`:** hint fields (`HighlightFalseBridges`, `ShowGaussianEllipsoids`, etc.) are treated as _suggested initial defaults_ for the viewer's UI controls — not hard commands. The user can override any of them without triggering a re-render.

**Temperature scrubbing:** the `TemporalLabelSequence` scrubber updates the color `BufferAttribute` in-place (`colors[i*3] = r; ... needsUpdate = true`). It never recreates the `Points` geometry. Same for scalar heatmap switching.

This invariant is also documented as a comment block at the top of the `<script>` section in `viewer.html`.

## 13. Open questions

- **Metric implementations in the browser without Wasm.** For Phase 3, do we precompute distance matrices in C# and ship them as artifacts (fast, large), or implement closed-form synthetic-case metrics in TypeScript (slower, no extra artifact)? Probably both, configured per scenario. Pre-computed `EdgeLayer`s (one per metric/graph/k combination) is the first-pass answer — build all at harness time, serialize all to JSON, toggle by `.visible` in the viewer.
- **Camera state persistence.** Across metric changes the camera should hold; across dataset changes it likely should not. Define the rule precisely.
- **Brotli artifact size budget for portfolio pages.** A typical SPC run with full anchor + delta chains can be tens of MB compressed. For portfolio case studies the run should be pruned (fewer T steps, smaller N, maybe anchor-only at the cost of replay fidelity). Define a "case-study mode" run profile in `RunConfig`.
- **Avalonia parity.** A desktop diagnostic build is desirable for users who want to point at a local run directory without copying it through a web host. Out of scope for v1; recorded as a planned parallel deployment.
- **Live compute compute-budget UI.** When the Wasm path is enabled, how does the dashboard advertise that some controls trigger long computations? A non-blocking progress affordance is required.
- **Upstreaming to the primer's project layout.** When the engine moves out of the design-first phase, `projects/Visualizer/` (or analogous) needs a place in the primer's "Where things are" table. Not yet, but flagged for when the SDK projects materialise.

---

> **Next steps:** Phase 5 — SPC temperature scrubbing. Requires `TemporalLabelSequence` scrubber in `viewer.html` wired to `MaterializeAt`; χ trace and merge-tree as 2D side panels; artifact stream codecs (`spc/spin-observation/v3`, `thermo/equilibrium-clusters/v1`) as defined in state-engine-design.md.

Phase 7 (full configuration-matrix dashboard) and Phase 5 both land alongside the merge-tree and cluster-profile codecs in the state engine.
