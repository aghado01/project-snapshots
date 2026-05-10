using System;
using System.Collections.Generic;
using System.IO;
using ProximityGraphs;
using Viz;
using Viz.Adapters.Synthetic;
using Viz.Renderers;
using static SyntheticDatasets.SyntheticData;

string outDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

// ── Scene 1: Crescent + Ellipsoid (baseline) ──────────────────────────────────
RunScene(
    GenerateCrescentAndEllipsoid(),
    "Crescent + Ellipsoid (smoke test)",
    Path.Combine(outDir, "viz-smoke-crescent.html"));

// ── Scene 2–5: Möbius + Ellipsoid across placement modes ─────────────────────
foreach (var placement in new[] {
    MobiusPlacement.NearSeam,
    MobiusPlacement.OrthogonalCenterCross,
    MobiusPlacement.PeripheralElbow })
{
    RunScene(
        GenerateMobiusAndEllipsoid(placement: placement, dimensions: 3),
        $"Möbius + Ellipsoid — {placement} (3D)",
        Path.Combine(outDir, $"viz-smoke-mobius-{placement.ToString().ToLowerInvariant()}-3d.html"));
}

// ── Scene 6: Möbius 4D ────────────────────────────────────────────────────────
RunScene(
    GenerateMobiusAndEllipsoid(placement: MobiusPlacement.OrthogonalCenterCross, dimensions: 4),
    "Möbius + Ellipsoid — OrthogonalCenterCross (4D projected)",
    Path.Combine(outDir, "viz-smoke-mobius-orthogonal-4d.html"));

// ── Scene 7: Möbius cross-section variants ────────────────────────────────────
foreach (var xsec in new[] {
    TubeCrossSection.UniformDisk,
    TubeCrossSection.Annular,
    TubeCrossSection.GaussianAnisotropic })
{
    RunScene(
        GenerateMobiusAndEllipsoid(crossSection: xsec, placement: MobiusPlacement.NearSeam),
        $"Möbius + Ellipsoid — {xsec}",
        Path.Combine(outDir, $"viz-smoke-mobius-{xsec.ToString().ToLowerInvariant()}.html"));
}

static void RunScene(
    SyntheticDataset dataset,
    string title,
    string outPath)
{
    var adapted = new SyntheticDatasetAdapter().Adapt(dataset);

    double[] features = adapted.Points.Features.ToArray();
    int n = adapted.Points.N;
    int d = adapted.Points.D;
    int[] gtLabels = ExtractGtLabels(adapted);

    // Project 4D to 3D for edge-building so distances are in 3D ambient space
    double[] edgeFeatures = d == 4
        ? FlattenRows(Project4DTo3D(UnflattenRows(features, n, d)), n, 3)
        : features;
    int edgeDim = Math.Min(d, 3);

    var edgeLayers = new List<EdgeLayer>
    {
        BuildEdgeLayer(edgeFeatures, n, edgeDim, gtLabels, VizMetric.Euclidean, new KnnSpec(7)),
        BuildEdgeLayer(edgeFeatures, n, edgeDim, gtLabels, VizMetric.Euclidean, new MutualKnnSpec(7)),
    };

    var vizDataset = new VizDataset(
        adapted.Points,
        adapted.LabelLayers,
        adapted.ScalarLayers,
        edgeLayers,
        adapted.GaussianLayers,
        adapted.TemporalSequences,
        adapted.SpineLayers,
        generatorParams: adapted.GeneratorParams,
        generatorParamSchema: adapted.GeneratorParamSchema);

    var package = SceneBuilder.Build(vizDataset, new SceneDescriptor
    {
        Title = title,
        Hints = SceneRenderHints.Default,
    });

    using var stream = File.Create(outPath);
    new ThreeJsHtmlRenderTarget().Render(package, stream);
    Console.WriteLine($"Written: {outPath}");
}

static double[][] UnflattenRows(double[] flat, int n, int d)
{
    var rows = new double[n][];
    for (int i = 0; i < n; i++)
    {
        rows[i] = new double[d];
        Array.Copy(flat, i * d, rows[i], 0, d);
    }
    return rows;
}

static double[] FlattenRows(double[][] rows, int n, int d)
{
    var flat = new double[n * d];
    for (int i = 0; i < n; i++)
        Array.Copy(rows[i], 0, flat, i * d, d);
    return flat;
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static int[] ExtractGtLabels(VizDataset ds)
{
    // Prefer the GroundTruth label layer; fall back to first available.
    var layer = ds.LabelLayers.Count > 0 ? ds.LabelLayers[0] : null;
    return layer?.Labels.ToArray() ?? Array.Empty<int>();
}

static EdgeLayer BuildEdgeLayer(
    double[] features, int n, int d, int[] gtLabels,
    VizMetric metric, ProximitySpec proximity)
{
    Func<int, int, double> dist = metric switch
    {
        VizMetric.Euclidean => (i, j) => EuclideanDist(features, d, i, j),
        _ => throw new NotSupportedException($"Metric {metric} not yet wired in harness"),
    };

    NeighborSelection sel = proximity switch
    {
        KnnSpec s => ProximityGraph.SelectKnn(n, s.K, dist),
        MutualKnnSpec s => ProximityGraph.SelectMutualKnn(n, s.K, dist),
        _ => throw new NotSupportedException($"NeighborRule {proximity.Kind} not yet wired in harness"),
    };

    // Flatten per-node neighbor lists → parallel edge arrays
    int edgeCount = 0;
    foreach (var row in sel.AllNeighbors) edgeCount += row.Length;

    var src = new int[edgeCount];
    var dst = new int[edgeCount];
    var weight = new double[edgeCount];
    int idx = 0;

    for (int i = 0; i < n; i++)
        foreach (var nb in sel.AllNeighbors[i])
        {
            src[idx] = i;
            dst[idx] = nb.Index;
            weight[idx] = nb.Distance;
            idx++;
        }

    // False-bridge arrays: GT cluster of each edge endpoint.
    // Renderer colors edge magenta when edgeClusterSrc[e] != edgeClusterDst[e].
    int[]? edgeClusterSrc = null;
    int[]? edgeClusterDst = null;
    if (gtLabels.Length > 0)
    {
        edgeClusterSrc = new int[edgeCount];
        edgeClusterDst = new int[edgeCount];
        for (int e = 0; e < edgeCount; e++)
        {
            edgeClusterSrc[e] = gtLabels[src[e]];
            edgeClusterDst[e] = gtLabels[dst[e]];
        }
    }

    string name = LayerName(metric, proximity);
    return new EdgeLayer(name, src, dst, weight, metric, proximity, edgeClusterSrc, edgeClusterDst);
}

static double EuclideanDist(double[] features, int d, int i, int j)
{
    double sum = 0;
    for (int k = 0; k < d; k++)
    {
        double diff = features[i * d + k] - features[j * d + k];
        sum += diff * diff;
    }
    return Math.Sqrt(sum);
}

static string LayerName(VizMetric metric, ProximitySpec proximity)
{
    string m = metric.ToString().ToLowerInvariant();
    string p = proximity switch
    {
        KnnSpec s => $"knn:k={s.K}",
        MutualKnnSpec s => $"mutual_knn:k={s.K}",
        EpsilonBallSpec s => $"epsilon_ball:eps={s.Epsilon:G4}",
        MstAugmentedSpec s => $"mst_aug:k={s.K}",
        _ => proximity.Kind.ToString().ToLowerInvariant(),
    };
    return $"{m}:{p}";
}
