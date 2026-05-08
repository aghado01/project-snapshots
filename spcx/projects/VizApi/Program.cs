using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using ProximityGraphs;
using Viz;
using Viz.Adapters.Synthetic;
using Viz.Renderers;
using static SyntheticDatasets.SyntheticData;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// ── GET / → initial viewer HTML with default scene ────────────────────────────
app.MapGet("/", () =>
{
    var package = BuildPackage(new RegenRequest());
    using var ms = new MemoryStream();
    new ThreeJsHtmlRenderTarget().Render(package, ms);
    string html = Encoding.UTF8.GetString(ms.ToArray());
    return Results.Content(html, "text/html; charset=utf-8");
});

// ── POST /api/regen → ScenePackage JSON ───────────────────────────────────────
// Receives generator params from viewer.html; returns full ScenePackageJson.
// Returns 501 for unsupported generator types (only CrescentAndEllipsoid wired).
app.MapPost("/api/regen", (RegenRequest req) =>
{
    var package = BuildPackage(req);
    using var ms = new MemoryStream();
    new JsonExportRenderTarget(compact: true).Render(package, ms);
    string json = Encoding.UTF8.GetString(ms.ToArray());
    return Results.Content(json, "application/json");
});

app.Run();

// ── Scene construction ────────────────────────────────────────────────────────

static ScenePackage BuildPackage(RegenRequest req)
{
    EllipsoidPlacement placement = Enum.TryParse<EllipsoidPlacement>(req.Placement, out var p)
        ? p
        : EllipsoidPlacement.NearOpenFace;

    var dataset = GenerateCrescentAndEllipsoid(
        crescentPoints: req.CrescentPoints,
        crescentRadius: req.CrescentRadius,
        crescentWidth: req.CrescentWidth,
        arcHalfAngle: req.ArcHalfAngle,
        ellipsoidPoints: req.EllipsoidPoints,
        ellipsoidAxes: req.EllipsoidAxes,
        placement: placement,
        intersectDepth: req.IntersectDepth,
        intersectRadialShift: req.IntersectRadialShift,
        gapScale: req.GapScale,
        seed: req.Seed);

    var adapted = new SyntheticDatasetAdapter().Adapt(dataset);

    double[] features = adapted.Points.Features.ToArray();
    int n = adapted.Points.N;
    int d = adapted.Points.D;
    int[] gtLabels = ExtractGtLabels(adapted);

    VizMetric metric = Enum.TryParse<VizMetric>(req.Metric, out var m) ? m : VizMetric.Euclidean;
    Func<int, int, double> dist = metric switch
    {
        VizMetric.Manhattan => (i, j) => ManhattanDist(features, d, i, j),
        VizMetric.Cosine => (i, j) => CosineDist(features, d, i, j),
        _ => (i, j) => EuclideanDist(features, d, i, j),
    };

    ProximitySpec spec = req.NeighborRule switch
    {
        "MutualKnn" => new MutualKnnSpec(req.KnnK),
        "EpsilonBall" => new EpsilonBallSpec(req.EpsilonBallEpsilon),
        "MstAugmented" => new MstAugmentedSpec(req.KnnK),
        _ => new KnnSpec(req.KnnK),
    };
    var edgeLayers = new List<EdgeLayer>
    {
        BuildEdgeLayer(features, n, d, gtLabels, metric, dist, spec),
    };

    var vizDataset = new VizDataset(
        adapted.Points,
        adapted.LabelLayers,
        adapted.ScalarLayers,
        edgeLayers,
        adapted.GaussianLayers,
        adapted.TemporalSequences,
        adapted.SpineLayers,
        generatorParams: adapted.GeneratorParams);

    return SceneBuilder.Build(vizDataset, new SceneDescriptor
    {
        Title = "Crescent + Ellipsoid",
        Hints = SceneRenderHints.Default,
    });
}

static int[] ExtractGtLabels(VizDataset ds)
{
    var layer = ds.LabelLayers.Count > 0 ? ds.LabelLayers[0] : null;
    return layer?.Labels.ToArray() ?? Array.Empty<int>();
}

static EdgeLayer BuildEdgeLayer(
    double[] features, int n, int d, int[] gtLabels,
    VizMetric metric, Func<int, int, double> dist, ProximitySpec proximity)
{
    NeighborSelection sel = proximity switch
    {
        KnnSpec s => ProximityGraph.SelectKnn(n, s.K, dist),
        MutualKnnSpec s => ProximityGraph.SelectMutualKnn(n, s.K, dist),
        EpsilonBallSpec s => ProximityGraph.SelectEpsilonBall(n, s.Epsilon, dist),
        MstAugmentedSpec s => ProximityGraph.SelectMstAugmented(n, s.K, dist),
        _ => throw new NotSupportedException($"NeighborRule {proximity.Kind} not wired"),
    };

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

    string name = metric.ToString().ToLowerInvariant() + ":" + proximity switch
    {
        KnnSpec s => $"knn:k={s.K}",
        MutualKnnSpec s => $"mutual_knn:k={s.K}",
        EpsilonBallSpec s => $"epsilon_ball:eps={s.Epsilon:G4}",
        MstAugmentedSpec s => $"mst_aug:k={s.K}",
        _ => proximity.Kind.ToString().ToLowerInvariant(),
    };

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

static double ManhattanDist(double[] features, int d, int i, int j)
{
    double sum = 0;
    for (int k = 0; k < d; k++)
        sum += Math.Abs(features[i * d + k] - features[j * d + k]);
    return sum;
}

static double CosineDist(double[] features, int d, int i, int j)
{
    double dot = 0, ni = 0, nj = 0;
    for (int k = 0; k < d; k++)
    {
        double a = features[i * d + k], b = features[j * d + k];
        dot += a * b; ni += a * a; nj += b * b;
    }
    double denom = Math.Sqrt(ni) * Math.Sqrt(nj);
    return denom < 1e-12 ? 1.0 : 1.0 - dot / denom;
}

// ── Request model ─────────────────────────────────────────────────────────────
// JSON property names are camelCase (default System.Text.Json / ASP.NET Core).
// All fields have defaults matching CrescentAndEllipsoid defaults.

record RegenRequest(
    int CrescentPoints = 300,
    double CrescentRadius = 3.0,
    double CrescentWidth = 0.35,
    double ArcHalfAngle = 0.72,
    int EllipsoidPoints = 200,
    double[]? EllipsoidAxes = null,
    string Placement = "NearOpenFace",
    double IntersectDepth = 0.0,
    double IntersectRadialShift = 0.0,
    double GapScale = 1.0,
    int Seed = 42,
    int KnnK = 7,
    string Metric = "Euclidean",
    string NeighborRule = "Knn",
    double EpsilonBallEpsilon = 0.5);
