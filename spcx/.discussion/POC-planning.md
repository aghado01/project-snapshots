# POC Planning — Outstanding Work, Open Questions, Warnings

Written: 2026-05-16. **Live doc.** Keep here: open workstreams, unresolved design questions, post-landing notes, warnings worth carrying forward. Move out: anything that has landed — close those out in `changelog.md`, not here. Tracks are grouped by subject so reading any one section gives the full picture (workstream + questions + warnings + tech debt for that track all in one place).

---

## Still deferred from diagnostics work (as of 2026-05-17)

Part 1 (`Graphs.Diagnostics`), the mixture kernel, and SPC Tier 0+Tier 1 all landed — see `changelog.md`. Items that remain explicitly deferred:

- `Graphs.Recommendations` — waiting for a concrete consumer to materialize.
- Broader cleanup of `ConnectivityReport` field ownership — defer until call sites consume the more focused reports directly.
- `TDA.Mapper.Diagnostics` — the last diagnostics-track item. Same shape pattern as the SPC pass (cross-layer consumption of `Graphs.Diagnostics` on the nerve graph). Brief not yet drafted.

## Recommended sequence

Track ordering for picking up the next chunk of work (revised 2026-05-17 toward demo-submission critical path):

1. **Spectral bridge** — producer + test refactor for predicate-as-oracle framing. Critical path for Euclidean manifold intrinsic flow approximations (curved embedded structures like Crescent and Möbius where geodesic isn't natively available from the metric). EigenFast + DenseEigen are now landed (see Cross-Cutting below), so the bridge work runs on the fast baseline.
2. **Mapper / GraphMapper updates** — whatever the Mapper surface needs to support the demo storytelling story; details TBD.
3. **`TDA.Mapper.Diagnostics`** — last diagnostics-track item; same compose-with-Graphs.Diagnostics pattern as SPC.
4. **Persistent Mapper over sweep** — barcode-shaped output; first concrete consumer of the eventual `Barcode` primitive.
5. **SIFTS + Filtration + `PersistentHomology` + `Barcode` primitives** — model-free PH track; second concrete consumer.
6. **Temporal-frame adapter** — generalizable seam for ordered frame producers (SPC-T, persistent Mapper, PH filtration); previously slot 2.
7. **2D diagnostic-panel subsystem** — consumer of barcodes, nerve diffs, χ/H/C curves.
8. **Demo storytelling abstractions** — `Demo` / `SceneSlot` primitive, `PipelineConfig` coordination, scene-switcher / progression model. Deferred until critical-path infrastructure is in place; rich design discussion already captured.

Parallel-track / lower-priority:

- **EigenFast promotion 2** (four consumer migrations + CI matrix) — see Cross-Cutting > Remaining. Doesn't add new capability; can run alongside or after item 1.

`Graphs.Diagnostics`, the mixture coupling kernel, SPC critical-T Tier 0+Tier 1, plus EigenFast + DenseEigen + Spectral migration all landed 2026-05-17 — see `changelog.md`.

## Submission constraint

**Demo-repo URL must exist at click-submit time.** Not required to be complete; required to not be dead. A skeletal mirror with clear intent + minimal scaffolding meets the bar. Iteration continues after submission. This shapes the critical path: features that show up in the demo repo's user-facing surface (viewer, README, working synthetic-fixture rendering) take priority over features that exist only as internal primitives.

---

## Track — Spectral Bridge & Iterative Eigensolver

### Workstream — Honest spectral-gradient bridge

A second data-driven producer of line-field / oriented-arrow vector data, mapping low-frequency graph-Laplacian eigenfunctions to directional fields on the underlying manifold. Complements the existing empirical `LocalTangent` producer.

**Framing — "geometrically honest" applies to the producer, not the predicate:**

- **Producer (spectral bridge): pure, data-driven, no generator awareness.** This is a semantic requirement on the math. The producer does not know which generator emitted its input or with what parameters; it operates on the graph it receives. The line field it emits reflects the apparent topology of the actual point cloud — sparse, dense, 1-twist, 3-twist, noisy, clean. Whatever the data is, the producer should respond accordingly. The "geometrically honest" claim is about *this*.
- **Test predicate (validation oracle): may use any ground truth available, including generator config.** This is standard test-design — the test setup has access to generator ground truth, and using it as an oracle is what validation is for. The predicate "cheating" by reading the generator's config to derive expected winding number is fine; it's leveraging available ground truth to confirm that the data-driven producer landed in the right place.
- **The two should not be conflated.** Producers can never peek at the generator. Predicates often should. Whether a given fixture hardcodes its expected predicate values or computes them from the generator config is a fixture-design choice, not a correctness concern.
- **Cross-validation story:** two independent data-driven producers (spectral bridge + `LocalTangent`), neither knowing about the generator, agreeing on observable properties within tolerance. That agreement is the strongest honest evidence; disagreement is also data (e.g., one producer may be more sensitive to local density variation than the other). This is *producer-vs-producer agreement*, distinct from *producer-vs-oracle validation*.

**Workstream content:**

- Crescent is the first target case.
- Möbius stays out of the oriented-arrow path until a line-field-native bridge story exists.
- Side-by-side comparison against `LocalTangent`; do not silently replace the empirical tangent path.

**Two distinct exit criteria — test fixtures vs demo scenes:**

These can land at different points. The producer is shared; the surrounding context is not.

- **Test exit (validation-flavored):** the four currently-skipped facts in `FixtureGoldenDrafts.cs` can be reintroduced as real assertions for their specific fixture configs. Predicates use ground-truth oracles to validate the producer landed in the right place.
  - `Crescent_LineField_AlignsWithLocalChord`
  - `Crescent_LineField_NoRadialBias`
  - `Mobius_LineField_HalfIntegerWindingNumber` (assumes the 1-twist Möbius fixture; different twist counts → different test fixtures with different expected winding)
  - `Mobius_LineField_ContinuousAwayFromSeam`
- **Demo exit (showcase-flavored):** the producer's output looks right on the demo scenes routed through the synthetic adapter — line fields visibly track local manifold structure on Crescent, exhibit expected winding on Möbius, are visually coherent under natural generator configs (not edge cases). No predicates; judged visually. Presentation concerns like normalization, density, color mapping are part of this exit and not the test exit.

Same producer, different downstream consumers, different design constraints. Test fixtures may be deliberately contrived to exercise edge cases; demo scenes want natural-looking input that a user might plausibly encounter.

**Warning — the four skipped test facts are not a Phase 1 regression signal.** Keep them skipped until the bridge exists; treat their absence as bridge-pending scaffolding, not test failure.

**Open question:** What exact projection criterion makes the spectral-bridge producer sensitive to apparent topology in the data? Wants design first. The math needs to extract directional information from scalar eigenfunctions in a way that respects local graph structure (likely via a discrete-gradient or level-set-normal construction), and the resulting line field needs to behave sensibly across whatever data it receives — without the bridge knowing what fixture or generator that data came from. Predicates can then validate against ground truth on each fixture; that validation is a separate concern from the producer's data-driven semantics.

### Tech debt — LOBPCG at scale (reframed by measured EigenFast results)

- Iterative `LOBPCG` is the sparse successor that *eventually* replaces the dense path for large graphs.
- **Urgency reframed by real-measured EigenFast speedups:** the now-landed `EigenFast` shows ~1.7×–13× speedups over classical `Eigen` depending on matrix size (rand-32: 5.8–13×; rand-64: 5×; rand-128: 1.7–1.9×; rand-256: 4×–4.7×; rand-512: 3.0–4.1×). The dense-solver comfort range is materially extended; LOBPCG becomes load-bearing only when graphs scale past where `EigenFast` is comfortable. Not an imminent blocker.
- Sparse Lanczos remains the natural successor when iterative quality becomes load-bearing.

**Open question:** Long-term disconnected-spectral policy — per-component field, explicit no-signal diagnostic, or hard failure in the harness?

### Landed (2026-05-17) — EigenFast + DenseEigen + Spectral migration

Full record in [eigenfast-promotion-finished.md](.discussion/issues/20260617/eigenFast/eigenfast-promotion-finished.md). Overview index at [copilot-brief-eigenfast-promotion.md](.discussion/issues/20260617/eigenFast/copilot-brief-eigenfast-promotion.md). Remaining work in [copilot-brief-eigenfast-promotion-2.md](.discussion/issues/20260617/eigenFast/copilot-brief-eigenfast-promotion-2.md).

The pass went further than originally scoped — Pass 1 plus the first consumer migration plus an unplanned cross-cutting harness pass all landed together:

- **`EigenFast.cs`** — v3 with the `Math.Sign(0)` fix. Zone 1 SIMD retained. FMA kept as opt-in variant, not the default runtime path (future investigation noted below).
- **`DenseEigen.cs`** — single compile-time-routed production dispatcher. `EIGEN_REFERENCE` toggles between `Eigen` and `EigenFast`. Exposes both rectangular `double[,]` and flat column-major entrypoints via `DenseEigenOptions` and the `DenseEigenFastVariant` fast-family seam.
- **`Spectral` migrated to `DenseEigen`** — the primary dense consumer. New pooled flat column-major Laplacian materialization path lives alongside the existing rectangular path. `SpectralDenseDispatchTests` covers the vertical dense-dispatch seam.
- **`EigenFastTests` + `EigenFastTimingHarness`** — parity tests and opt-in benchmark harness (gated via `EnableBenchmarks=true`).
- **Artifact-first harness discipline** — cross-cutting infrastructure side-effect. `projects/tests/HarnessArtifacts.cs` and `Directory.Build.targets` provide a shared compile-linked helper; benchmark, fixture, smoke, and analyze harnesses now emit timestamped manifests + JSON payloads to `artifacts/<kind>/<suite>/<timestamp>/` instead of console dumps. Real infrastructure win beyond the EigenFast scope.

### Remaining — EigenFast promotion 2

Four downstream consumers still call `Eigen.DecomposeSymmetric(...)` directly and need migration through `DenseEigen`:

1. `src/graphs/diagnostics/AlgebraicConnectivity.cs`
2. `src/tda/mapper/GraphMapper.cs`
3. `src/maths/linalg/PCA.cs`
4. `src/maths/linalg/ICA.cs`

Plus: CI build matrix (default + `EIGEN_REFERENCE` configs), per-consumer regression coverage, optional `Spectral` benchmark rerun under `EIGEN_REFERENCE` to close the consumer-level A/B measurement gap.

**Demo-readiness priority:** lower than the spectral-bridge or temporal-adapter tracks. These four migrations are correctness/optimization, not new capabilities. They can land in parallel with or after the bridge design pass.

### Future investigation — FMA dense-eigen path

Noted in the finished doc: the FMA variant of `EigenFast` was measured during the promotion session and kept as opt-in rather than promoted to default. Future work to rigorously test the conditions under which FMA materially helps is a backlog item, not a blocker.

---

## Track — Temporal Adapter & SPC Critical-T Triangulation

### Workstream — Temporal-frame adapter

- Reframe the old "SPC adapter" phase as a generic sweep-axis / frame-adapter phase.
- Treat SPC-T as the first concrete producer, not the abstraction boundary itself.
- Carrier model aligned with the existing temporal scene contract (`TemporalLabelSequence`, frame-index semantics).
- Seam supports SPC-T, SIFTS-ε, Blatt thresholds, and similar ordered frame producers later. (SIFTS itself lives in the PH/Filtration/Barcodes track; the adapter just consumes whatever ordered frames it produces.)

**Open question:** Confirm the temporal-adapter split as a real seam rather than keeping a second non-synthetic adapter inside `VizCore`. If a second non-synthetic source lands soon, the recommended answer is to split now rather than accrete SPC-specific adapter logic and re-split later.

### Sub-workstream — SPC critical-T multi-signal triangulation (Blatt complement)

**Tier 0 (graph-derived initial grid) and Tier 1 (three signal curves) landed 2026-05-17** — `CriticalTemperatureEstimator`, `Susceptibility`/`LabelEntropy`/`SpecificHeat`/`BondFrequency` modules under `Clustering.SPC.Diagnostics`, plus the `AdaptiveSchedulerConfig` nullability change that wires graph-derived bounds into `AdaptiveScheduler.Run`. See `changelog.md`.

Remaining tiers below are speculative-or-deferred, not in flight.

**Tier 2 — Multi-signal stability scoring (modest, after tier 1 informs):**

- Augment `AdaptivePatchDiagnostics.StabilityScore` with per-signal peak locations and a consensus score:
  - `T_c_chi = argmax χ(T)`, `T_c_H = argmax |dH/dT|`, `T_c_C = argmax C(T)`.
  - Agreement = `1 − var{T_c_chi, T_c_H, T_c_C} / range(T)`.
  - High agreement → three-signal consensus on T_c.
  - Low agreement → flag the patch as ambiguous; widen the dense sweep or surface a diagnostic warning.

**Tier 3 — Smarter parallel coarse probe (scale-deferred, not speculative):**

- Replace `ComputeHalfMaxBand` (currently χ-only) with `ComputeTransitionBand` that unions transition regions across all three signals.
- Concentrate dense-refinement budget where `dH/dT`, `dC/dT`, or `dχ/dT` is non-trivial; prune coarse-grid regions where H is saturated near 0 or log(N).
- **Motivation is budget allocation, not search speedup.** At current PoC scale (≤ 150-node patches, ~1s wall-clock for the full adaptive sweep), uniform log-spaced coarse + half-max dense is fine and triangulation is overkill. As patches scale to thousands of nodes or MAPPER-SPC composes many patches, the wasted samples in trivially-saturated entropy regimes compound — that is where tier 3 earns its keep. Cold-parallel stays the right paradigm; the lever is _which_ T values get the parallel budget, not how warm-state chaining affects it.
- Defer until either patch size or pipeline composition makes the wasted-sample fraction visible in profiling. Tier 1/2 outputs are the inputs for the eventual tier 3 implementation.

**Cautions for downstream tier 2/3 work (preserved from the landed tier 1 design):**

- Bond-cluster H, not Potts-color H. The latter is a flat signal under SW dynamics. (Already enforced in the landed `LabelEntropy` implementation.)
- Single-config H is noisy; rely on the per-cycle-accumulated `ClusterSizeHistogram`, not point-in-time spin distributions. (Already enforced.)
- Not a Kolmogorov-complexity proxy (block entropy ≠ K(x)); do not market it as such.
- Not a graph-topology diagnostic (k-mer over CSR `Targets[]` was considered and rejected as interpretively muddy).

**Drop policy:** Tier 2 is droppable if real-fixture tier 1 data shows H and C carry no information beyond χ. Tier 3 is parking-lot.

---

## Track — Hyperbolic & Metric-Specific Storytelling

### Workstream

- Pair metrics with the right synthetic generators rather than relying on off-manifold experimentation. First honest pairings:
  - `HyperbolicBlobs ↔ Poincare`
  - `Simplex ↔ FisherRaoSimplex`
  - `GaussianManifold ↔ FisherRaoHalfPlane`
- Add the first geometry-respecting diagnostics: radial peeling, radial depth coloring, optional cross-section / disk inspection view.

**Open question:** Are hyperbolic geodesic arcs reserved for explicit hierarchy links only, or do they become part of a broader diagnostic-path concept?

---

## Track — Mapper

### Workstream — Static Mapper diagnostics

- Add Mapper-local intrinsic-dimension diagnostics with the correct anchor model.
- Static `MapperDiagnostics` factor-out into the four-report shape (`NerveTopologyReport`, `MapperNodeReport`, `CoverageReport`, `IntrinsicDimensionReport`) is queued behind the `Graphs.Diagnostics` merged plan and the `Clustering.SPC.Diagnostics` brief. See Cross-Cutting > In-flight.

### Workstream — Persistent Mapper over sweep

- Run `Mapper.Build` per sweep slice and diff nerves across the sweep axis using existing graph primitives (`UnionFind`, `BfsShells`).
- **Output is barcode-shaped** — birth/death of nerve features across the sweep axis. This is the second concrete consumer of the `Barcode` primitive (the first being PH on SIFTS). See PH/Filtration/Barcodes track.

**Open questions:**

1. Should Mapper diagnostics first lift back to member points, or should the viewer gain an explicit nerve-overlay surface earlier?
2. If nerve overlays land, should PH barcodes and Mapper nerve diffs share the same future 2D-panel subsystem? (Both are 2D-rendered birth/death structures; sharing seems natural.)

---

## Track — Persistent Homology, Filtration, and Barcodes

**Barcodes are a first-class concept in this track**, used by at least two producers in the current roadmap. A unified `Barcode` primitive serves both; both producers emit `(birth, death, dim?)` tuples; the consumer surfaces (2D panel renderer, diagnostic exports) treat them uniformly.

Concrete consumers of the `Barcode` primitive:

1. **PH on filtrations** (this track) — typical use case: SIFTS distance-matrix filtration → PH reduction → barcode.
2. **Persistent Mapper over sweep** (Mapper track) — nerve features birth/death across the sweep axis.

A potential third use sits in the SPC critical-T sub-workstream — cluster birth/death over T is barcode-shaped, but SPC clusters don't have the strict persistence property of PH/Mapper bars. Track as speculative, not a confirmed third consumer.

### Workstream — Primitives

- `src/tda/primitives/Barcode.cs` — first-class barcode primitive. Shape: array of `(birth, death, dim?, generator?)` records. `generator` is optional metadata for later viewer back-references to triangles / edges / filtration steps that gave rise to each bar.
- `src/tda/primitives/Filtration.cs` — explicit filtration construction step. Sits above triangles / `FlagComplex` (already exists) as a prerequisite input. Generic — not SIFTS-specific.
- `src/tda/primitives/PersistentHomology.cs` — reduction-based barcode output. Consumes `Filtration`, produces `Barcode[]`.

### Workstream — SIFTS as first concrete consumer

- `src/ETL/Sifts` for text ingestion, segmentation, embeddings, and distance-matrix construction. Feeds `Filtration`; does not know about PH or barcodes.

**Open questions:**

1. **Is SIFTS inside the demo scope, or is it a follow-on PoC?** If inside, PH/filtration work should parallelize with the temporal-adapter phase rather than waiting for slot 5 of the recommended sequence.
2. **Should `Barcode` output become a first-class scene `BarcodeLayer`, or live under a diagnostic payload separate from the 3D layer model?** Recommendation: separate from the 3D layer model since barcodes are inherently 2D; pairs with the 2D panel subsystem.
3. **How should barcode data tie back to triangle / edge / filtration identities for later viewer coordination?** (Generator-metadata field on the `Barcode` primitive is the seam.)

**Tech debt:**

- **PH consumer surfaces are absent.** Triangles now exist as a viewer-capable scene surface (FlagComplex + TriangleLayer landed), but there is still no filtration object, PH primitive, barcode contract, or 2D barcode view consuming that data. This track is the catch-up.
- **SIFTS boundary needs confirmation.** The split above (`src/ETL/Sifts` for ETL, `TDA.Primitives` for Filtration / PH / Barcode) is a proposed clean cut, not yet a confirmed implementation contract.

---

## Track — 2D Diagnostic Panel Subsystem

### Workstream

- Keep barcode views, nerve diffs, radial summaries, seam indicators, and other 2D diagnostics outside the ordinary 3D layer model.
- Wants design first; not yet ready for delegated implementation.
- First concrete consumers when this lands:
  - PH barcodes (PH/Filtration/Barcodes track)
  - Persistent-Mapper nerve diffs (Mapper track)
  - χ(T), H(T), C(T) curves (SPC critical-T sub-workstream)

---

## Cross-Cutting

### Diagnostics — landed (2026-05-17)

Three diagnostic passes landed together:

- `Graphs.Diagnostics` — factor-out + MST-bandwidth fix + core surface (`AlgebraicConnectivity`, `EdgeWeights`, `Degree`, `NeighborhoodScale`, `MstBridge`, `Hubness`, `Cycles`) + `FlagComplex.CountTriangles`.
- Mixture coupling kernel — `MixtureWeights`, `MixtureBandwidth`, `BandwidthEstimation.ForMixture`, `Graphs.Coupling.Mixture`, `GraphBuilder.BuildWithMixture(...)`.
- `Clustering.SPC.Diagnostics` Tier 0+Tier 1 — `CriticalTemperatureEstimator`, `Susceptibility`/`LabelEntropy`/`SpecificHeat`/`BondFrequency`, plus `AdaptiveSchedulerConfig` nullability that wires graph-derived bounds.
- Supporting: `Maths.Information.Shannon` primitive, `Spectral.cs` dense-only routing, `LOBPCG.cs` excluded from `TDA.Primitives` compilation pending repair.

Cross-layer composition pattern is validated: SPC heuristics consume `Graphs.Diagnostics` outputs directly with no duplicated edge walks. `TDA.Mapper.Diagnostics` is queued to mirror the same pattern on the nerve graph.

### Post-landing observations and follow-ups

**Post-landing simplification options for `CycleReport`** — revisit after first-fixture data:

- Drop `MeanCycleLength` + `MaxCycleLength`; keep `(M, TriangleCount, Girth, TriangleSaturation)`. Removes the BFS-spanning-forest + LCA-walk pass.
- More aggressive: drop `Girth` too — just `(M, TriangleCount, TriangleSaturation)`. Pure O(V+E). Loses stringy-topology detection; keeps triangle-degeneracy assay.
- Shannon entropy over cycle-length distribution is deferred but recoverable as a single-method extension over the same BFS pass.
- k-mer / block-entropy over CSR `Targets[]` was considered and rejected (interpretively muddy).

### Delegation guidance

- **Ready for implementation once designed:** metric/generator pairings; mechanical temporal-adapter plumbing; mechanical PH/triangle algorithm plumbing; `TDA.Mapper.Diagnostics` break-up (four reports replacing the inline `MapperResult.Diagnostics`).
- **Wants design first:** spectral-gradient projection criterion; `Barcode` primitive shape and viewer-coordination seam; Mapper nerve-overlay policy; 2D diagnostic-panel architecture; SPC critical-T Tier 2 consensus scoring (gated on Tier 1 data from real fixtures).
