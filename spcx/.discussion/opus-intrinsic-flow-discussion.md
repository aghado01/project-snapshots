Let me polish the existing EigenFast Pass 2 brief first, then dig into the spectral bridge + geodesic flow discussion.

Brief polished. Now the design discussion — this is more substantive than I'd initially framed because working through it surfaces a useful unification.

## The unification: spectral bridge is already metric-agnostic

I'd previously framed spectral bridge and geodesic flow as two distinct producers for two distinct fixture classes. Working through the math, that's actually not quite right.

The graph Laplacian's eigenfunctions encode whatever distance was used to build the graph. If you build a Crescent graph from Euclidean kNN, the spectrum is Euclidean-aware. If you build a Poincaré HyperbolicBlobs graph from Poincaré mKNN-MST + mixture kernels, the spectrum is Poincaré-aware. The spectral bridge transformation itself (extracting line-field-shaped output from scalar eigenfunctions) doesn't care which metric baked the structure into the graph.

So actually we have **one spectral-bridge producer** that works across both fixture classes, plus **`LocalTangent` generalized to be metric-aware** (currently Euclidean-only, presumably). Both producers consume a metric implicitly — the spectral bridge via the graph-construction metric, `LocalTangent` via the metric tensor directly on the point cloud.

That's a cleaner story than "two producers for two fixture classes." It's "two producers for any metric, cross-validating each other in their respective metric contexts."

## Two producers, shared output, no shared interface

| Producer                      | Input                                         | How metric enters                                                          | Output                |
| ----------------------------- | --------------------------------------------- | -------------------------------------------------------------------------- | --------------------- |
| Spectral bridge               | `CsrGraph` + `Spectral.ComputeBottomK` output | Implicit — encoded in graph construction (kNN distance, kernel, bandwidth) | LineFieldLayer-shaped |
| `LocalTangent` (metric-aware) | Point cloud + `IRiemannianManifold`           | Explicit — caller passes the metric                                        | LineFieldLayer-shaped |

**Output type shared**, **input shapes different**. I'd argue against forcing them through a common `ILineFieldProducer` interface — the inputs differ too much for the interface to mean anything beyond "produces a line field." Premature abstraction. Just share the output type.

The viewer's `LineFieldLayer` consumes whichever LineField is produced. It doesn't need to know which path generated it.

## Spectral bridge math — the open question

Four candidate approaches for extracting directional information from scalar Laplacian eigenfunctions:

1. **Single-eigenfunction gradient on the graph.** At node _i_, compute the gradient as the weighted sum over neighbors _j_ of `w_ij · (φ(j) − φ(i)) · (x_j − x_i)`, where φ is the eigenfunction and x is the node's ambient position. Normalize. For line field (unoriented), drop sign.

2. **Cross-product of two eigenfunctions' gradients.** In 3D ambient space, `∇φ_1 × ∇φ_2` gives a direction normal to both. Useful for finding the tangent direction _along_ a curved surface where neither single gradient is itself tangent.

3. **Level-set normal of a single eigenfunction.** The direction in which the eigenfunction is changing fastest — equivalent to (1) but framed differently. The unoriented line is perpendicular to this for a line field.

4. **Local-gradient PCA.** At each node, look at a neighborhood, compute the per-eigenfunction gradient field over that neighborhood, take the first principal direction. Combines spectral signal with geometric averaging.

**Picking the right one is the design question.** My honest read:

- **For Crescent** (2D-embedded-in-2D-or-3D banana): the Fiedler vector's gradient direction _is_ the local chord direction. Option 1 should work cleanly. Option 3 is the same thing, framed differently.
- **For Möbius** (2D-embedded-in-3D twisted surface): option 1 will give a vector that may or may not be tangent to the surface (it's a graph-derived direction, not surface-constrained). Option 2 (cross-product of two eigenfunction gradients) is more likely to give a tangent direction on the surface. But option 2 produces oriented vectors that may not respect the half-integer winding — _unless_ the line-field interpretation (drop sign) absorbs the non-orientability naturally.

The math needs working through. My recommendation: **start with option 1 for Crescent**, get the predicate-test-validated path working, then revisit option 2 for Möbius once we see how option 1 behaves on the non-flat case.

**Open sub-question:** which eigenfunctions? The first non-trivial one alone? The first few? A weighted combination? This is the kind of thing where the test predicates (does this align with local chord on Crescent? does the winding number come out right on Möbius?) inform the answer iteratively.

## `LocalTangent` metric-aware upgrade

What I don't know without reading the file: how Euclidean-baked-in is the current `LocalTangent`. Likely it does local Euclidean PCA on the neighborhood point set. To generalize:

```csharp
namespace Maths.Geometry;

public static class LocalTangent
{
    // Existing Euclidean path stays as-is (or becomes a thin wrapper)
    public static LineField Estimate(double[][] points, int k = ?);

    // New metric-aware path
    public static LineField Estimate(double[][] points, IRiemannianManifold metric, int k = ?);
}
```

Implementation difference: instead of plain covariance of neighbor offsets, the metric-aware version uses the metric tensor at each point to compute the local-tangent-space covariance. Principal eigenvector of that covariance is the local tangent direction. The math is well-defined for any `IRiemannianManifold`; `Poincaré`, `FisherRaoSimplex`, `FisherRaoHalfPlane` all implement the necessary metric-tensor query (assuming the existing interface supports this — worth verifying when the work starts).

**Honestly:** this is a smaller deliverable than the spectral bridge. The math is more straightforward (it's just local PCA in the right inner product). The interesting question is whether the existing `IRiemannianManifold` interface exposes enough to compute tangent-space PCA at arbitrary points. If yes, this is mechanical. If no, the interface needs widening first.

## Cross-validation pairings, properly framed

| Fixture class                          | Producer A                            | Producer B                        | Cross-validation question        |
| -------------------------------------- | ------------------------------------- | --------------------------------- | -------------------------------- |
| Euclidean-embedded (Crescent, Möbius)  | Spectral bridge (Euclidean kNN graph) | `LocalTangent` (Euclidean metric) | Do they agree on the line field? |
| Natively curved (Poincaré, Fisher-Rao) | Spectral bridge (metric-aware graph)  | `LocalTangent` (metric-aware)     | Do they agree on the line field? |

Two producers per fixture class, both data-driven, neither knowing about the generator. Agreement within tolerance → high-confidence intrinsic flow. Disagreement → signal about which producer is more sensitive to local density / sparsity / metric singularities (e.g., near the Poincaré boundary where the metric diverges).

This makes the demo story richer than I'd framed earlier. It's not "we have two producers for Euclidean and one for hyperbolic." It's **"we have two independent producers that work in any metric, and on each fixture you can watch them either agree (validation) or disagree (diagnostic)."**

## Hierarchy flow is a separate concept

Worth distinguishing from tangent flow explicitly because both might show up in the same hyperbolic demo:

- **Tangent flow** (this discussion): local direction of the data manifold. Unoriented line field.
- **Hierarchy flow**: direction from child node toward parent in a hyperbolic-tree embedding. Oriented arrows. Currently mentioned in POC-planning under "hyperbolic geodesic arcs reserved for explicit hierarchy links."

Different layers (`LineFieldLayer` vs whatever the oriented-arrow layer is called), different producers, different visual semantics. Demo can show both on the same hyperbolic fixture — tangent flow as the local manifold structure, hierarchy flow as the explicit parent-pointer arrows.

Don't conflate them in design. Tangent flow is what the spectral bridge / metric-aware `LocalTangent` track delivers. Hierarchy flow is its own track tied to the hierarchical generator semantics.

## Implications for POC-planning

I'd suggest a couple of structural changes to the planning doc:

1. **Rename or reframe the "Spectral Bridge" track** to "Intrinsic Line-Field Producers" or similar, with two workstreams: spectral bridge and metric-aware `LocalTangent`. Both produce line-field-shaped output, both are data-driven, both go through the same test-fixture / demo-exit discipline. The "Hyperbolic & Metric-Specific Storytelling" track inherits from this for its tangent-flow content.

2. **Split out hierarchy flow** as its own sub-workstream under the hyperbolic track, distinct from tangent flow.

3. **Clarify in the planning doc** that the spectral bridge is metric-agnostic by virtue of the graph being built with the right metric. That's a meaningful refinement of the framing — it ties the bridge work to the existing graph-construction metric machinery (kNN, mKNN-MST, mixture kernels) rather than treating it as a separate Euclidean-specific concern.

Want me to make these planning-doc updates now, or hold them until we lock the math choices first? The two work in parallel — locking the math doesn't depend on the doc structure, and the doc structure doesn't depend on the math.
