using System;
using System.Collections.Generic;
using System.IO;
using ProximityGraphs;
using Viz;
using Viz.Adapters.Synthetic;
using Viz.Renderers;
using static SyntheticDatasets.SyntheticData;

// ── Generate ──────────────────────────────────────────────────────────────────
var dataset = GenerateCrescentAndEllipsoid();

// ── Adapt ─────────────────────────────────────────────────────────────────────
var adapted = new SyntheticDatasetAdapter().Adapt(dataset);

// ── Build EdgeLayers ──────────────────────────────────────────────────────────
// Copy features to double[] so they can be captured in the distance lambdas.
// (ReadOnlyMemory<double> cannot be captured as a ref struct would.)
double[] features = adapted.Points.Features.ToArray();
int n = adapted.Points.N;
int d = adapted.Points.D;

// Extract GT labels for false-bridge coloring. -1 = noise/unlabelled.
int[] gtLabels = ExtractGtLabels(adapted);

var edgeLayers = new List<EdgeLayer>
{
    BuildEdgeLayer(features, n, d, gtLabels, VizMetric.Euclidean, new KnnSpec(7)),
    BuildEdgeLayer(features, n, d, gtLabels, VizMetric.Euclidean, new MutualKnnSpec(7)),
};

// ── Merge into dataset ────────────────────────────────────────────────────────
var vizDataset = new VizDataset(
    adapted.Points,
    adapted.LabelLayers,
    adapted.ScalarLayers,
    edgeLayers,
    adapted.GaussianLayers,
    adapted.TemporalSequences,
    adapted.SpineLayers,
    generatorParams: adapted.GeneratorParams);

// ── Build scene ───────────────────────────────────────────────────────────────
var descriptor = new SceneDescriptor
{
    Title = "Crescent + Ellipsoid (smoke test)",
    Hints = SceneRenderHints.Default,
};
var package = SceneBuilder.Build(vizDataset, descriptor);

// ── Render to disk ────────────────────────────────────────────────────────────
string outPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "viz-smoke.html");

using (var stream = File.Create(outPath))
    new ThreeJsHtmlRenderTarget().Render(package, stream);

Console.WriteLine($"Written: {outPath}");
Console.WriteLine("Open in browser to inspect.");

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
