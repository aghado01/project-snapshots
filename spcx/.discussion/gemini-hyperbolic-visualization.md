You said

if i have synthetic data generating native hyperbolic embeddings e.g. blatt hierarchy in the unit B^3, how do i visualize that?

Visualizing data in the Poincaré ball model ()—especially a structured hierarchy like the Blatt model—requires accounting for the "crowding" effect, where points cluster near the boundary as distance increases linearly in hyperbolic space.

Here are the most effective ways to visualize these embeddings:

### 1\. The Interactive 3D Poincaré Ball

For , the most direct method is a 3D scatter plot where the sphere boundary is rendered at .

- **Projection:** Plot the coordinates directly within a unit sphere.

- **The Depth Problem:** Because distance grows exponentially toward the edge, points at the "leaves" of your hierarchy will look like a solid shell.

- **Solution:** Use **interactive clipping planes** or a **radial slider** to filter points by their hyperbolic distance from the origin (). This allows you to "peel" the hierarchy layers.

### 2\. Hyperbolic Tree / Cone Tree Maps

Since you are working with a hierarchy, the geometric "branching" is often more important than the raw coordinates.

- **Cone Trees:** In 3D, represent the Blatt hierarchy as a tree where the root is at the origin. Use semi-transparent links to show the parent-child relationships.

- **Angular Distortion:** To make it readable, apply a transformation that "pulls" the nodes toward the center visually while maintaining their angular relationship.

### 3\. Geodesic Path Plotting

In a native hyperbolic embedding, the "straight line" between two points is a circular arc orthogonal to the boundary.

- Instead of drawing straight Euclidean lines between nodes, calculate the **geodesic arcs**.

- This visually reinforces the curvature of the space and makes the hierarchical structure look like an organic, "root-like" system branching toward the surface.

### 4\. 2.D Cross-Sections (Poincaré Disk)

If 3D is too cluttered, take a 2D slice through the center of the ball.

- **The "Escher" View:** Use a 2D Poincaré Disk projection. It is often easier to inspect the branching factor and leaf density in 2D.

- **Radial Heatmaps:** Map the "depth" of the hierarchy to a color gradient (e.g., root is dark blue, leaves are bright yellow).

### Implementation Toolkit

If you are using Python, these libraries handle the heavy lifting:

| Tool                   | Use Case                                                                   |
| ---------------------- | -------------------------------------------------------------------------- |
| **Plotly / IPyVolume** | Interactive 3D balls with rotation and zoom.                               |
| **NetworkX**           | Calculating paths and tree depths.                                         |
| **Hypeyr**             | A specialized library for hyperbolic geometry and Poincaré visualizations. |
| **Matplotlib (3D)**    | Quick static renders of the unit sphere and point clouds.                  |

### A Quick Transformation Tip

If your embedding looks "squashed" at the edges, try converting the Poincaré coordinates to the **Klein Model**. It maps hyperbolic lines to Euclidean straight lines, which can sometimes make the internal structure of a hierarchy easier to follow visually, though it sacrifices the conformal (angle-preserving) nature of the Poincaré model.

You said

how about exposing intrinsic embedding dimensions in a B-ball . i just wrote mapper for myself

To expose **intrinsic embedding dimensions** within your Poincaré ball using the MAPPER implementation you just wrote, you should leverage the **nerve graph structure** to detect local manifold dimensionality.

In a hyperbolic hierarchy (like your `BlattHierarchy`), the "dimension" isn't uniform; it transitions from a 1D "trunk" near the origin to a high-dimensional "canopy" near the boundary.

Here is how to wire your existing `Mapper.cs` and `HyperbolicFilters.cs` to expose these intrinsic dimensions:

### 1\. The Lens: `PoincareRadial` as the Depth Axis

You have already implemented `PoincareRadialFilter`, which is the correct "sweep" variable for hyperbolic data.

- **The Logic:** In , hierarchical depth maps directly to radial distance. By using this as your `IFilter`, your MAPPER bins become concentric "shells".

- **Implementation:** Use `BalancedHistogramCover` from your `ICover.cs` rather than `UniformCover`. Because points concentrate exponentially near the boundary, a uniform value-width cover will leave your root bin empty and your boundary bins over-populated.

### 2\. Local Dimensionality via Preimage PCA

Inside your `Mapper.Build` orchestrator, you can calculate the **Intrinsic Dimension (ID)** for each `MapperNode` by performing a local PCA on the preimage.

Modify your `BuildFromBins` logic (or a post-processor) to compute the **Participation Ratio** or **Variance Explained** for each node:

```
// Example Logic for intrinsic dimension exposure
foreach (var node in result.Nodes) {
    double[][] subset = GetDataForIndices(node.MemberIndices);
    // Use your existing PCA primitive (referenced in pca_filter.cs)
    var pca = Pca.Compute(subset, numComponents: subset[0].Length);

    // Intrinsic Dimension proxy: how many components are needed
    // to explain 90% of local variance?
    int localDim = ComputeEffectiveDimension(pca.Eigenvalues, threshold: 0.9);

    // Store this in a new ScalarLayer for the Viz engine
    intrinsicDimValues[node.BinId] = localDim;
}
```

### 3\. Visualizing "Dimensionality Creep"

To see the B-ball's intrinsic structure in your `viewer.html` engine, map the node dimensionality to a `ScalarLayer`.

- **The Expectation:** For a true `BlattHierarchy`, you should see the `localDim` start at **1** (near the origin/root) and increase as you move toward the boundary as branches proliferate.

- **The Nerve Diagnostic:** Check `MapperDiagnostics.IsTreeLike`. If your intrinsic dimension is low (1D) but `IsTreeLike` is false (high `LoopCount`), your cover overlap is likely too high, causing "spurious cycles" in the canopy.

### 4\. Multi-D Intrinsic Mapping

If you suspect your data isn't a pure tree, use your `CompositeFilter` and `ProductCover` to expose dimension against other properties:

- **Filter 0:** `PoincareRadial` (Hierarchy Depth).

- **Filter 1:** `PoincareLocalDensity` (Branching thickness).

- **Result:** This 2D MAPPER will produce a "tiling" of the ball where you can identify regions where the intrinsic dimension collapses (e.g., a long, thin branch) versus regions where it expands (a dense cluster of leaves).

### Recommended Settings for Hyperbolic MAPPER:

| Component     | Implementation                              | Reason                                 |
| ------------- | ------------------------------------------- | -------------------------------------- |
| **Filter**    | `HyperbolicFilters.PoincareRadial`          | Maps depth to the sweep axis.          |
| **Cover**     | `BalancedHistogramCover(n=20, overlap=0.3)` | Prevents boundary "crowding" in bins.  |
| **Clusterer** | `KMeansPlusPlusClusterer(auto-k)`           | Handles branching at each depth level. |

This setup transforms MAPPER from a simple graph-builder into a **topological probe** for the intrinsic geometry of your hyperbolic space.
