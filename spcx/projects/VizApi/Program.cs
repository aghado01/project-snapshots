using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Estimators.Tangent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using ProximityGraphs;
using SyntheticDatasets;
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
// Dispatches on req.Generator; add new generators to the switch below.
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

// Phase 1 dispatch: generator-specific synthesis + adaptation only.
// Phase 2 (shared): edges, GMM overlays, mergedParams, scene build — all in BuildPackage.
static ScenePackage BuildPackage(RegenRequest req)
{
    var (adapted, title) = req.Generator switch
    {
        "MobiusAndEllipsoid" => BuildMobiusAdapted(req),
        _ => BuildCrescentAdapted(req),
    };

    // ── Shared: proximity graph ───────────────────────────────────────────────
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

    // ── Shared: Wing-2 empirical local tangent flow ───────────────────────────
    // Reuses the same proximity graph topology as the edge layer (same k, metric,
    // rule) but builds it independently so BuildEdgeLayer stays unchanged.
    // The second call is O(n²·k) which is identical cost to edge layer construction.
    NeighborSelection flowSel = SelectNeighbors(n, dist, spec);
    var points2d = new double[n][];
    for (int i = 0; i < n; i++)
    {
        points2d[i] = new double[d];
        Array.Copy(features, i * d, points2d[i], 0, d);
    }
    var adjacency = new int[n][];
    for (int i = 0; i < n; i++)
    {
        var nbrs = flowSel.AllNeighbors[i];
        adjacency[i] = new int[nbrs.Length];
        for (int j = 0; j < nbrs.Length; j++) adjacency[i][j] = nbrs[j].Index;
    }
    var tangentVectors = LocalTangent.Compute(points2d, adjacency);
    var vectorFieldLayers = new List<VectorFieldLayer>
    {
        new VectorFieldLayer("Local PCA Flow", tangentVectors, n, Math.Min(d, 3)),
    };

    // ── Shared: GMM overlay — works for any generator with SpineLayers ────────
    // GmmComponents slider pos 0-4 → K = 1, 2, 4, 8, 16 Gaussian components.
    // sigmaLong and sigmaPerp are derived from each spine's own geometry via
    // SpineLayer.TypicalScale (cross-section radius propagated from the generator).
    int gmmPos = Math.Clamp((int)Math.Round(req.GmmComponents), 0, 4);
    int gmmK = 1 << gmmPos;
    var gaussianLayers = new List<GaussianLayer>(adapted.GaussianLayers);
    foreach (var gmmLayer in BuildGmmOverlaysFromSpines(adapted.SpineLayers, gmmK))
        gaussianLayers.Add(gmmLayer);

    // ── Shared: merge params so they round-trip without resetting on regen ────
    var mergedParams = adapted.GeneratorParams != null
        ? new Dictionary<string, object>(adapted.GeneratorParams)
        : new Dictionary<string, object>();
    mergedParams["seed"] = req.Seed;
    mergedParams["knnK"] = req.KnnK;
    mergedParams["metric"] = req.Metric;
    mergedParams["neighborRule"] = req.NeighborRule;
    mergedParams["epsilonBallEpsilon"] = req.EpsilonBallEpsilon;
    mergedParams["gmmComponents"] = req.GmmComponents;
    mergedParams["showFlow"] = req.ShowFlow;

    var vizDataset = new VizDataset(
        adapted.Points,
        adapted.LabelLayers,
        adapted.ScalarLayers,
        edgeLayers,
        gaussianLayers,
        adapted.TemporalSequences,
        adapted.SpineLayers,
        generatorParams: mergedParams,
        generatorParamSchema: adapted.GeneratorParamSchema,
        vectorFieldLayers: vectorFieldLayers);

    return SceneBuilder.Build(vizDataset, new SceneDescriptor
    {
        Title = title,
        Hints = SceneRenderHints.Default,
    });
}

// ── GMM overlay: generator-agnostic, driven purely by spine geometry ──────────
// For each SpineLayer, auto-derives:
//   sigmaLong = arcLength / K           (one arc-segment width)
//   sigmaPerp = TypicalScale × 0.9      (from generator) or stepSize × 2 (fallback)
static IEnumerable<GaussianLayer> BuildGmmOverlaysFromSpines(
    IReadOnlyList<SpineLayer> spineLayers, int K)
{
    foreach (var spine in spineLayers)
    {
        var ss = spine.SpineSamples;
        int M = ss.Length;
        if (M < 2) continue;

        double arcLength = 0;
        for (int i = 1; i < M; i++)
        {
            double dx = ss[i][0] - ss[i - 1][0];
            double dy = ss[i][1] - ss[i - 1][1];
            double dz = ss[i].Length > 2 ? ss[i][2] - ss[i - 1][2] : 0.0;
            arcLength += Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        double stepSize = arcLength / (M - 1);
        double sigmaLong = arcLength / K;
        double sigmaPerp = spine.TypicalScale > 0 ? spine.TypicalScale * 0.9 : stepSize * 2.0;

        int stride = Math.Max(1, M / K);
        int offset = stride / 2;
        yield return BuildGmmOverlayLayer(spine, sigmaLong, sigmaPerp, stride, offset, $"GMM K={K}");
    }
}

// ── Phase 1: Crescent synthesis + adaptation (no edges, no overlay) ──────────
static (VizDataset adapted, string title) BuildCrescentAdapted(RegenRequest req)
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

    return (new SyntheticDatasetAdapter().Adapt(dataset), "Crescent + Ellipsoid");
}

// ── Phase 1: Möbius synthesis + adaptation (no edges, no overlay) ────────────
static (VizDataset adapted, string title) BuildMobiusAdapted(RegenRequest req)
{
    MobiusPlacement placement = Enum.TryParse<MobiusPlacement>(req.Placement, out var mp)
        ? mp
        : MobiusPlacement.NearSeam;

    TubeCrossSection crossSection = Enum.TryParse<TubeCrossSection>(req.CrossSection, out var cs)
        ? cs
        : TubeCrossSection.GaussianIsotropic;

    var dataset = GenerateMobiusAndEllipsoid(
        mobiusPoints: req.MobiusPoints,
        spineRadius: req.SpineRadius,
        halfWidth: req.HalfWidth,
        halfThickness: req.HalfThickness,
        noiseSigma: req.NoiseSigma,
        twistCount: req.TwistCount,
        crossSection: crossSection,
        radialBias: req.RadialBias,
        ellipsoidPoints: req.EllipsoidPoints,
        ellipsoidAxes: req.EllipsoidAxes,
        placement: placement,
        intersectDepth: req.IntersectDepth,
        intersectRadialShift: req.IntersectRadialShift,
        gapScale: req.GapScale,
        dimensions: req.Dimensions,
        seed: req.Seed);

    return (new SyntheticDatasetAdapter().Adapt(dataset), "Möbius + Ellipsoid");
}

static int[] ExtractGtLabels(VizDataset ds)
{
    var layer = ds.LabelLayers.Count > 0 ? ds.LabelLayers[0] : null;
    return layer?.Labels.ToArray() ?? Array.Empty<int>();
}

// ── Proximity graph helpers ───────────────────────────────────────────────────

static NeighborSelection SelectNeighbors(int n, Func<int, int, double> dist, ProximitySpec proximity) =>
    proximity switch
    {
        KnnSpec s => ProximityGraph.SelectKnn(n, s.K, dist),
        MutualKnnSpec s => ProximityGraph.SelectMutualKnn(n, s.K, dist),
        EpsilonBallSpec s => ProximityGraph.SelectEpsilonBall(n, s.Epsilon, dist),
        MstAugmentedSpec s => ProximityGraph.SelectMstAugmented(n, s.K, dist),
        _ => throw new NotSupportedException($"NeighborRule {proximity.Kind} not wired"),
    };

static EdgeLayer BuildEdgeLayer(
    double[] features, int n, int d, int[] gtLabels,
    VizMetric metric, Func<int, int, double> dist, ProximitySpec proximity)
{
    NeighborSelection sel = SelectNeighbors(n, dist, proximity);

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

// ── GMM overlay helper ───────────────────────────────────────────────────────
// Places K ellipsoidal Gaussians evenly along a spine, with the long axis
// aligned to the spine tangent and short axes equal to sigmaPerp.
// offset centres the components within their arc segments (pass stride/2).
static GaussianLayer BuildGmmOverlayLayer(
    SpineLayer spineLayer,
    double sigmaLong,
    double sigmaPerp,
    int stride = 4,
    int offset = 0,
    string name = "GMM K=1")
{
    var ss = spineLayer.SpineSamples;   // M × D (D=3 for crescent in XY)
    int M = ss.Length;
    var indices = new List<int>();
    for (int i = offset; i < M; i += stride)
        indices.Add(i);

    int K = indices.Count;
    const int geomDim = 3;
    var means = new double[K * geomDim];
    var covs = new double[K * geomDim * geomDim];
    var weights = new double[K];
    double w = 1.0 / K;

    for (int ci = 0; ci < K; ci++)
    {
        int i = indices[ci];
        int j = Math.Min(i + stride, M - 1);

        // Tangent: direction along spine.
        double tx = ss[j][0] - ss[i][0];
        double ty = ss[j][1] - ss[i][1];
        double tz = ss[j].Length > 2 ? ss[j][2] - ss[i][2] : 0.0;
        double tLen = Math.Sqrt(tx * tx + ty * ty + tz * tz) + 1e-12;
        tx /= tLen; ty /= tLen; tz /= tLen;

        // Binormal approximation: crescent lives in XY plane, so B ≈ Z.
        // N = B × T, then re-orthogonalise B = T × N.
        double nx = -ty * 1.0;  // cross([0,0,1], T) = [-Ty, Tx, 0]
        double ny = tx * 1.0;
        double nz = 0.0;
        double nLen = Math.Sqrt(nx * nx + ny * ny) + 1e-12;
        nx /= nLen; ny /= nLen;

        double bx = ty * nz - tz * ny;
        double by = tz * nx - tx * nz;
        double bz = tx * ny - ty * nx;  // should be ~1.0

        // Covariance: Σ = R * diag(σl², σp², σp²) * Rᵀ  where R = [T | N | B]
        double sl2 = sigmaLong * sigmaLong;
        double sp2 = sigmaPerp * sigmaPerp;
        double[] T3 = { tx, ty, tz };
        double[] N3 = { nx, ny, nz };
        double[] B3 = { bx, by, bz };

        int covBase = ci * geomDim * geomDim;
        for (int r = 0; r < geomDim; r++)
            for (int c = 0; c < geomDim; c++)
                covs[covBase + r * geomDim + c] =
                    T3[r] * T3[c] * sl2 + N3[r] * N3[c] * sp2 + B3[r] * B3[c] * sp2;

        means[ci * geomDim] = ss[i][0];
        means[ci * geomDim + 1] = ss[i].Length > 1 ? ss[i][1] : 0.0;
        means[ci * geomDim + 2] = ss[i].Length > 2 ? ss[i][2] : 0.0;
        weights[ci] = w;
    }

    return new GaussianLayer(name, means, covs, weights, K, geomDim);
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
// Generator-specific fields default to sensible values; the discriminator field
// "generator" determines which BuildXxx function is called.

record RegenRequest(
    // ── Discriminator ──────────────────────────────────────────────────────────
    string Generator = "CrescentAndEllipsoid",
    // ── CrescentAndEllipsoid ───────────────────────────────────────────────────
    int CrescentPoints = 300,
    double CrescentRadius = 3.0,
    double CrescentWidth = 0.40,
    double ArcHalfAngle = 2.04,
    double GmmComponents = 1,   // slider pos: 0→K=1, 1→K=2, 2→K=4, 3→K=8, 4→K=16
                                // ── MobiusAndEllipsoid ─────────────────────────────────────────────────────
    int MobiusPoints = 400,
    double SpineRadius = 2.5,
    double HalfWidth = 1.1,
    double HalfThickness = 0.12,
    double NoiseSigma = 0.06,
    int TwistCount = 1,
    double RadialBias = 1.0,
    string CrossSection = "Ribbon",
    // ── Shared ellipsoid ───────────────────────────────────────────────────────
    int EllipsoidPoints = 180,
    double[]? EllipsoidAxes = null,
    // ── Embedding (Möbius only) ────────────────────────────────────────────
    int Dimensions = 3,
    // ── Placement (shared shape) ───────────────────────────────────────────────
    string Placement = "NearOpenFace",
    double IntersectDepth = 0.0,
    double IntersectRadialShift = 0.0,
    double GapScale = 1.0,
    // ── Graph ──────────────────────────────────────────────────────────────────
    int Seed = 42,
    int KnnK = 5,
    string Metric = "Euclidean",
    string NeighborRule = "Knn",
    double EpsilonBallEpsilon = 0.5,
    // ── Overlay ────────────────────────────────────────────────────────────────
    bool ShowFlow = false);
