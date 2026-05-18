# yes, copilots new framing is my framing that i just worked through with copilot as i was thinking through it. i bring up eigenfast because its connected to the spectral bridge and the LOBPCG saga. as i understand it, LOBPCG is the sparse successor of dense eigen and functionality replaces it eventually, but as you point out, maybe not necessary for a while if eigenfast proves its meddle. correction: im not sure fixture predicates need parameter awareness so much as the calculations for flow for euclidean space where we are trying to calculate tangent on curved embedded structures like crescent or mobius, need to be sensitive to the apparent topology in the data. fixture probably shouldn't be aware of generator params because that wouldn't be honest. correct me if im wrong

Both threads folded in.

**Spectral bridge framing corrected** — "geometrically honest" now explicitly means honest-to-the-data, not honest-to-a-Platonic-manifold. Producer is data-driven; fixtures are closed bundles with config-specific predicate values; predicates are NOT generator-aware. Cross-validation story between spectral bridge and `LocalTangent` becomes the strongest honest test (two independent data-driven producers agreeing on observable properties without either knowing about the generator).

The open design question for the bridge is now sharper: "What math extracts directional information from scalar eigenfunctions in a way that respects local graph structure, such that the resulting line field behaves sensibly across the range of fixture configs without the bridge knowing what those configs are?" That's the projection criterion question, properly stated.

**LOBPCG-at-scale reframed by EigenFast** — added in the planning doc as "urgency reframed" with the EigenFast-extends-comfort-range note. Added a separate "In-flight — EigenFast promotion" subsection pointing at both the v3 source and Copilot's execution plan, capturing the staged-coexistence framing (not replacing `Eigen`), the downstream consumer list, and the hot-swap deferral.

The framing chain is now coherent:

- EigenFast extends dense range → LOBPCG-at-scale becomes a later concern, not imminent debt.
- LOBPCG eventually replaces dense for genuinely large sparse graphs.
- The spectral bridge work iterates faster on EigenFast (smaller wall-clock per fixture cycle) but the bridge math itself is unaffected by which dense solver runs underneath.

One thing worth flagging that I didn't bake into the doc: your correction about the predicates implies a meaningful test-design item — auditing the four currently-skipped fact names to make sure they're tied to a specific fixture config, not to a Platonic Möbius/Crescent. If the existing skipped-fact scaffolding has any "Möbius = winding 0.5" assumptions baked in without being tied to a specific 1-twist fixture spec, those should be tightened when the bridge work fires (so the predicates are honest to _this_ fixture rather than to "Möbius in general"). Worth a note in the eventual bridge design pass, not necessarily worth pre-capturing as planning content.

---

# so thinking some more: for test fixtures, we can absolutely "cheat" for the sake of validation. but we shouldn't conflate test fixtures using ground truth knowledge to validate with the fundamental semantics of how the tangent flow calculations work. so probably my misunderstanding

Doc tightened. The framing now distinguishes clearly:

- **Producer**: pure data-driven, no generator awareness. "Geometrically honest" applies here.
- **Predicate**: validation oracle, may use any ground truth available including generator config. Test-design concern, not a correctness concern.
- **Cross-validation**: producer-vs-producer agreement is distinct from producer-vs-oracle validation. Both are honest, just different kinds of evidence.

The bridge work, when it fires, becomes design-pure: the math just has to produce a line field that respects whatever data structure it sees, and the fixtures handle their own validation with whatever oracle they need.

---

# its also worth pointing out that test fixtures != demo fixtures. the point of test fixtures is to do their job, which is to test and validate core functionalities for correctness etc. but then the core functionality is on it's own, and if it has passed validation, then it has gained some trust that it can be deployed. the demo "fixtures" (maybe not the right semantics) are exactly just a demonstration of what core functionality is capable of doing,. and should be demonstrating things that are valid and correct with natural semantics if that makes sense

Doc updated. The spectral-bridge track now has two distinct exits framed explicitly — test (predicate-validated against ground-truth oracles) and demo (visually judged on natural-looking content). Same producer underneath; different consumers above.

Worth carrying this distinction forward to other tracks too. The temporal adapter has a similar structure (frame-producer machinery is one thing; the demo-side scenes that showcase SPC-T are another). And persistent Mapper + PH barcodes will have it (test fixtures to validate the barcode primitive's birth/death output vs demo scenes that show off the persistence story).

I won't pre-emptively edit every track for it — the spectral-bridge section is where the conversation grounded the distinction, and that's where it most needs to be on paper. If the distinction becomes load-bearing for another track later, we can add it then.

The broader naming question — "demo fixture" vs something else — is open. The viewer already uses "scene" and "scene package" for the demo-side artifacts, so "demo scene" probably wins in practice. Test side stays "test fixture" / "golden fixture" by convention. Worth being deliberate about not using "fixture" for both senses in future briefs; mixing the terms is what got us tangled here.

---

# on the note of demo scenes i wanted to discuss abstractions. we've gotten so buried in test fixture details so far but we are approaching a transition point in the work where it becomes about actual implementation in practice and showing 'this is what we've built and it works because rigorous testing says so"

Real transition point worth naming. The shape of the work changes when "we tested it works" becomes "look at what it does." A few framings worth putting on the table before we dig in:

## The abstraction we don't yet have a name for

We have:

- **Producers** (data-driven, test-validated): graph builders, SPC diagnostics, spectral primitives, Mapper, eventually PH.
- **Layers** (renderable atoms in the 3D layer model): `EdgeLayer`, `NodeSignalLayer`, `LineFieldLayer`, `TriangleLayer`.
- **Scene packages**: bundles of layers + metadata, the unit the viewer renders.
- **Adapters**: translate producer output into scene-package shape (currently just the synthetic adapter; temporal-frame adapter queued).
- **Viewer**: renders one scene at a time, supports temporal label sequences.

What we don't yet have a clean primitive for is the thing _between_ "a scene" and "a finished demo." Call it a **demo** or a **story** — a coherent piece of content the user is supposed to watch and understand. A demo might be:

- One static scene (current synthetic adapter, basically).
- A temporal sweep (SPC-T producing a sequence of frames).
- A 3D scene plus attached 2D panels (barcode + nerve-diff + χ/H/C curves rendered alongside the 3D content).
- A side-by-side comparison (spectral-bridge line field vs LocalTangent line field on the same generator).

These have very different shapes. The viewer's existing temporal-label-sequence machinery handles part of (2); nothing yet handles (3) or (4) cleanly.

## The compositional question

Are demo scenes:

- **Monolithic** — one generator config + one producer pipeline → one scene? Simpler. Each demo is a script that builds its own scene.
- **Composable** — scenes built from independent layer producers that don't know about each other? Harder. Lets you mix "Crescent skeleton + spectral-bridge line field + SPC label coloring" without each producer knowing the others exist.

We've been moving toward composable on the layer side (TriangleLayer landed independently of edge/scalar/line-field). But the _generator → producers → scene_ pipeline isn't composable yet — the synthetic adapter knows about specific scenes by name.

The temporal-frame adapter is the first real test of composition: it has to accept _any_ ordered frame producer, not just SPC-T. If we get that abstraction right, it sets the template for the future adapters (PH, persistent Mapper, hyperbolic-fixture sweeps).

## The narrative dimension

A test fixture passes or fails — binary, no narrative required. A demo scene has to tell a viewer something. The strong demo angles I see hanging in the planning doc:

- **"Two independent producers agree on this data"** — spectral bridge vs LocalTangent. Producer-vs-producer cross-validation as a _visible_ demo, not just a test assertion.
- **"Look at this thermodynamic transition"** — SPC-T sweep showing χ(T), H(T), C(T) curves triangulating T_c on a clean cluster fixture, then visualizing the granulation. Blatt-paper callback.
- **"Persistent features across a sweep"** — Mapper or PH barcodes showing which topological features actually persist vs which are noise.
- **"Hyperbolic geometry matters"** — `HyperbolicBlobs ↔ Poincare` pairing showing the right metric reveals structure invisible to Euclidean assumptions.

Each of those is a demo, not a scene. Each may compose multiple producers and possibly span multiple frames + 2D panels.

## Where I'd suggest digging in

Pick whichever thread is most pressing:

1. **Demo-as-primitive question** — should `Demo` (or `Story` or whatever) become a first-class abstraction in the codebase, sitting above `ScenePackage`? Or does it stay as orchestration scripts that compose existing pieces?
2. **Temporal-frame adapter design** — concrete first instance of "abstraction between producer and viewer." Locking this shape sets the template.
3. **2D-panel subsystem architecture** — separate-from-3D-layer-model, but how separate? Same renderer surface? Different bundle in the scene package? Its own root concept?
4. **Composition contract for adapters** — when multiple producers contribute to one scene, who owns the assembly? Synthetic adapter today knows about everything; that scales poorly.

Which one matters most for what you're seeing right now?

---

# I favor composability and taking a monolithic shortcut is very likely a bad idea, one which has already bitten me in the past (recall all of that project cleanup work that prefaced this demo readiness work). i think one abstraction missing is some idea of a progression of scenes and some attention to user experience. for a demo i want to be able to walk through the progression of technologies built and highlight what each new layer adds to the catalog of capabilities, as well as provide visual intuition for how things work (e.g. SPC-T progression to visualize SW color label percolation dynamics as a function of thte slider, and how this is is embedded in the mapper work flow). ideally it would be interactive in a way where user can explore model-free limits and then do somethiig like 'run clustering' that would kick off a dynamic scene that plays forward the mapper-SPC workflow , or alternatively in themodel free path, the PH workflow that complements Spc-mapper. the idea being of getting towards "look at how this is useful". We've already also got a foil for SPC which is the k-means clusterer, which we know is inferior, and being able to have a drop down for k-means vs spc as alternate cluster options could meaningfully demonstrate the utility of using SPC clustering, but this also requires seamless integration of both. there's also GMM that's also available for the clusterers inventory. there's also different synthetic data test cases that can progressively stress test the methods and strawmen, and important proof of concept is the hyperbolic hierarchy that demonstrates on of my key innovations (graph construction with poincare + mutualKNN-MST + kernel mixtures versus the nominal much more narrow SPC implementation). In this case hyperbolic hierarchy is a logical progression from the original blatt hierarchy, where the new SPC is validated and agrees with blatts original work, but then we show how euclidean or hamming knn + gaussian coupling falls apart in poincare hierarchy. so from a ux standpoint this kind of leads to an abstraction of a scene switcher for progressive story telling. we already have drop down for mobius vs crescent vs hyperbolic hierarchy, but last i checked the viz engine wasn't fully coordinated (which admittedly was before any of the recent work). e.g. metric/graph/coupling/bandwidth was not correctly integrated, flow calculations were broken and rendering hair balls, geodesic intrinsic flows weren't in place for synthetic fixtures with curved space and the correct geodesic distance like poincare for hyperbolic (for euclidean we need the spectral bridge stuff since we don't have the proper geodesic privilege), and quality of life things like some synthetic generators in the drop down didn't have their own point cloud regeneration interface, which was very bad because it was horribly inconsistent internally. I think what i'm getting at here is some notion of cross scene progressions as a sort of special demo capability.

You're naming the abstraction that's been missing: not "a scene," but "**a progression of scenes that tell a story about a catalog of capabilities**." That's the demo primitive. Let me unpack what it carries and how the rest of the structure falls out.

## The missing primitive — call it `Demo` or `Progression`

An ordered list of **`SceneSlot`**s, where each slot is:

- **A renderable state** — fixture + full pipeline config + which layers/panels are active.
- **A capability annotation** — what's being introduced or demonstrated at this slot ("here's where we add coupling kernels," "here's where mixture beats single-kernel"). The label the user sees, but also the metadata the demo uses to organize the story.
- **An interaction surface** — which controls are exposed _at this slot_ (regen button, SPC-T slider, clusterer drop-down, A/B swap). Earlier slots may expose fewer controls; later slots unlock more.
- **A transition contract** — how to move into this slot from the previous one. Some transitions are static (jump cut to new fixture); some are dynamic (the "run clustering" button kicks off a forward-play of the SPC-T sweep into the next slot).

A `Demo` is then just `IReadOnlyList<SceneSlot>` plus the viewer machinery to step through it. The viewer doesn't need to know what the slots are about; it knows how to render the current slot's scene and surface the current slot's controls.

## Your narrative arc maps directly to slots

Sketching it concretely against your story:

| Slot         | Capability introduced                        | What's visible                                                   | Controls exposed                                         |
| ------------ | -------------------------------------------- | ---------------------------------------------------------------- | -------------------------------------------------------- |
| 1            | Synthetic data generation                    | Raw point cloud (Crescent, Möbius, Hyperbolic Hierarchy)         | Generator drop-down + per-generator regen + seed         |
| 2            | Proximity graph construction                 | + edge skeleton                                                  | Metric / proximity rule / k                              |
| 3            | Coupling kernels                             | + edge weights as colors                                         | Kernel selector (single or mixture) / bandwidth          |
| 4            | Clustering (the "run clustering" transition) | + label colors                                                   | Clusterer drop-down (SPC / k-means / GMM)                |
| 4-comparison | The SPC-vs-foils story                       | Same input, side-by-side or A/B clusterers                       | Toggle between clusterers                                |
| 5            | SPC-T physics                                | + temperature slider, SW color label percolation as slider moves | Temperature slider, χ/H/C 2D panel                       |
| 6            | Spectral / line-field visualization          | + line field overlay                                             | Producer toggle (spectral bridge vs LocalTangent)        |
| 7            | Mapper composition                           | + nerve overlay                                                  | Mapper params, filter selector                           |
| 8            | Hyperbolic-hierarchy validation moment       | Standard SPC fails on Poincaré data; mixture + mKNN-MST succeeds | A/B between "narrow SPC" and "mixture SPC" — payoff slot |
| 9            | Model-free PH track                          | + barcode 2D panel                                               | Filtration scale slider                                  |

That's not the only ordering — but the abstraction supports _any_ ordering because each slot is self-describing. You can reorganize, branch, A/B compare, splice in an extra slot, without rewriting anything.

## The coordinated-controls problem is separate

The drop-down coordination issue you remember (metric/graph/coupling/bandwidth not flowing through cleanly) is a different abstraction failure, sitting under the slot model. Fix:

**A single `PipelineConfig` object** that the slot owns, with all the knobs:

```
PipelineConfig {
  GeneratorConfig (with regen / seed / params)
  Metric (with metric-specific params)
  ProximityRule + k or ε
  Coupling (single kernel OR mixture weights)
  Bandwidth (auto OR mixture-MAD OR explicit)
  Clusterer (SPC / k-means / GMM, with per-clusterer params)
  ...
}
```

The UI drives this object; the pipeline reads from it. Drop-downs aren't independent widgets — they're views on shared config. A metric change re-derives the coupling default; a coupling change re-derives the bandwidth default; etc. One source of truth, no incoherent states.

The slot's job is to declare which fields of `PipelineConfig` are user-editable at this slot (default for the others) and which are locked. Slot 2 might lock the clusterer field (no clustering yet); slot 8 might lock everything except the SPC-narrow-vs-mixture toggle.

## The metric-aware producer routing

The Poincaré-vs-Euclidean point you raised is a real architectural distinction:

- **Spaces with natively computable geodesic flow** (Poincaré, Fisher-Rao simplex/half-plane) get their line field directly from the metric.
- **Spaces without that** (Euclidean over a curved embedded manifold like Crescent or Möbius) need the spectral bridge — eigenfunctions of the graph Laplacian as a topological proxy for tangent flow.

These should be different producers selected by metric, not user-toggled blindly. The slot declares its metric; the producer routing for line fields follows from that. The user can still toggle "show me the spectral bridge result anyway, even on Poincaré" for comparison/educational purposes — but the _default_ producer is metric-appropriate.

This connects to your "model-free vs clustering" framing: in the metric's native flow space, you can show a lot of structure _before_ clustering even runs. Clustering then becomes "now let's group these flows into regions" rather than "this is the only thing we have to show."

## Comparison-view is its own UX primitive

The SPC-vs-k-means foil deserves a first-class "comparison slot" type — same input, multiple producers, side-by-side or A/B-toggle rendering. This affects the viewer (it has to know how to render two scenes simultaneously or switch between them on a control press). Worth being explicit about as a distinct slot variant from single-result slots.

## What I'd suggest as the next concrete step

The temporal-frame adapter design that's queued in POC-planning is actually a piece of this. SPC-T sequencing is a _transition kind_ (continuous forward-play through a slider's range). If we design the temporal adapter in the context of "this is a kind of slot transition" rather than "this is a one-off SPC integration," the abstraction lands cleanly.

So the proposed sequence:

1. **Design pass on `Demo` / `SceneSlot` shape** — the primitive itself, what it contains, how it's expressed in code. This is the foundational abstraction; everything else hangs off it.
2. **Audit existing viewer for the coordinated-controls gaps** — what's currently broken (metric/coupling/bandwidth disconnect, missing regen UIs, hairball flows, missing geodesic producers for Poincaré). Catalog before we touch anything.
3. **Sketch the `PipelineConfig` object** — single source of truth for the slot's configuration.
4. **Land temporal-frame adapter as the first transition kind** under the slot model — SPC-T as concrete producer.
5. Then start filling in slots from the narrative arc, with the new diagnostic surfaces (`SPC.Diagnostics`, `Graphs.Diagnostics`, future `Mapper.Diagnostics`) feeding the 2D panel content.

The whole thing is honestly a re-platforming of the viewer's interaction model, not just an additive feature. That's why your instinct to avoid monolithic shortcuts is right — if we build "the demo" as a single script that drives the current viewer, it works for v1 but locks out the comparison views, the slot reordering, the A/B toggles, all the things that make it actually _useful_.

Want to dig into the `Demo` / `SceneSlot` shape first, or the `PipelineConfig` coordination model? Those are the two foundational pieces and they can be designed somewhat independently.

---

# these all sound right, but we aren't there yet. i just wanted to brain dump some bigger picture things that are important as we continue to move through modular updates and enhancements. lets start with the spectral bridge business which will include the eigenfast update and integration with the aforementioned test framing, which is part of the critical path for euclidean manifold intrinsic flow approximations. then we can move on to mapper/graph mapper updates, diagnostics, persistent mapper and SIFTS + primitives and we can revisit these story telling items later. i wanted to touch on the bigger picture now so it can inform framing and help constrain work to the demo readiness goals. it may be the case that i wont be able to finish all of this before submitting my applicaiton. but the real thiing i need before i submit the app is a demo repository mirror o this project (i'm just going to copy part of the repo over to a new demo repo) that I can provide a url for in my application. its ok if its not finished at that point, the critical thing is that it _exists_ whe i click submit so its there whenever someone decides to look. i'll work feverishly on updating as work continues so that it's as far as i can get it before anyone sees it. what i cant have is a dead URL in my application. what i can have is a skeletal repo that says what is intended to be there and to ensure that something is there near term after i click submit

Brief at [.discussion/issues/20260617/eigenFast/copilot-brief-eigenfast-promotion.md](.discussion/issues/20260617/eigenFast/copilot-brief-eigenfast-promotion.md).

Three steps, scope-limited so it lands cleanly without downstream churn:

1. **Promote `EigenFast.cs`** from the discussion folder to `src/maths/linalg/EigenFast.cs`. Verify `Math.Sign(0)` fix is present (line 66-67), `EigenResult` constructor compatibility, `AllowUnsafeBlocks` is on. Both `Eigen` and `EigenFast` coexist.

2. **Comparison test surface** — net-new `EigenFastTests.cs`, uses `Eigen` as oracle. Five cases: `theta == 0` edge (the bug fix), existing known-spectrum fixtures, disconnected Laplacian, deterministic random 50×50, near-degenerate spectrum. Residual-based validation for vectors; descending-sort and elementwise-eigenvalue match.

3. **Opt-in timing harness** — `EigenFastTimingHarness.cs` marked with a `Benchmark` trait so it doesn't run in routine CI. Sizes `{32, 64, 128, 256}` + one graph-Laplacian case. Reports median ms and speedup ratio. Release-only.

Explicitly out-of-scope: downstream consumer migration (Spectral, GraphMapper, AlgebraicConnectivity, PCA, ICA), removing/refactoring `Eigen`, hot-swap dispatch seam, LOBPCG re-enablement. Migration is a separate pass per consumer with its own regression coverage — listed in the brief as follow-up sequence so the order isn't lost.

When this lands, we'll know: (a) is the fast solver correct against the reference, and (b) is it actually faster at the sizes that matter. Then the bridge design discussion can run against a stable baseline, and downstream migration follows on a per-consumer basis.

Hand it off when ready. I'll be here for the bridge design pass whenever you want to start that thread.

---

# `Explicitly out-of-scope: downstream consumer migration (Spectral, GraphMapper, AlgebraicConnectivity, PCA, ICA), removing/refactoring Eigen, hot-swap dispatch seam, LOBPCG re-enablement. Migration is a separate pass per consumer with its own regression coverage — listed in the brief as follow-up sequence so the order isn't lost.` I would say LOBPCG re-enablement is deferred for real since we have fast dense runway. but immediately following the eigenfast promotion and testing, the downstream consumer integration, hotswap dispatch seam , migration regregression coverge become important. also, i thought we already discussed that "Eigen.cs" remains as the proven correctness oracle. that was the whole point of my design session with copilot

Brief restructured. Summary of corrections:

**1. `Eigen.cs` as oracle promoted from "out of scope" to "settled architectural premise"** — at the top of the brief now, framed as load-bearing decision from your design session, not a deferred option. No semantic ambiguity about whether it's under reconsideration.

**2. Migration / hot-swap / regression promoted from "out of scope" to "Next pass — immediately follows this brief"** — explicitly framed as sequenced after Steps 1-3 land, not parking-lot. Sketched in enough detail (with the concrete hot-swap shape, the consumer order, the regression model, and the GA promotion gates) that the next brief can package it without relitigating design decisions.

**3. Only `LOBPCG` re-enablement remains as genuinely-out-of-scope** — framed as "deferred for real" because the fast dense runway means the iterative path isn't on critical path until graph sizes push past comfort. This is the one item that's actually deferred, not just sequenced.

**Hot-swap shape captured concretely**: extend `Spectral.SolverKind` from `{ Iterative, Dense }` to include `DenseReference` and `DenseFast`. Spectral gets the dispatch seam (it already has the backend-selection concept). PCA/ICA/GraphMapper/AlgebraicConnectivity migrate directly to `EigenFast` with no seam — one-line changes, reversible during validation, then stable.

This matches the "narrow local dispatch over global abstraction" recommendation from your design session and avoids over-engineering a facade or interface that would obscure which solver is actually running where.
