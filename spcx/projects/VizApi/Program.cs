using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CouplingKernels;
using DistanceMetrics;
using Estimators.Tangent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using ProximityGraphs;
using StatisticalEstimators;
using SyntheticDatasets;
using Viz;
using Viz.Adapters.Gmm;
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
    KernelType kernelType = Enum.TryParse<KernelType>(req.Kernel, out var kt) ? kt : KernelType.Gaussian;
    ProximityRule rule = req.NeighborRule switch
    {
        "MutualKnn" => ProximityRule.MutualKnn,
        "EpsilonBall" => ProximityRule.EpsilonBall,
        _ => ProximityRule.Knn,
    };
    bool ensureConnected = req.NeighborRule == "MstAugmented";

    Func<int, int, double> dist = metric switch
    {
        VizMetric.Manhattan => (i, j) => ManhattanDist(features, d, i, j),
        VizMetric.Cosine => (i, j) => CosineDist(features, d, i, j),
        VizMetric.FisherRaoSimplex => (i, j) => FisherRaoSimplex.Distance(RowOf(features, d, i), RowOf(features, d, j)),
        VizMetric.FisherRaoHalfPlane => (i, j) => FisherRaoHalfPlane.Distance(RowOf(features, d, i), RowOf(features, d, j)),
        _ => (i, j) => EuclideanDist(features, d, i, j),
    };

    // ── Shared: single graph build — Phase 1 (parallel) + Phase 2 (sequential)
    // CsrGraph is immutable after Build returns; consumed by both edge layer
    // serialization and LocalTangent (Wing-2) without re-running O(N²) neighbor search.
    CsrGraph csrGraph = GraphBuilder.Build(
        n, dist,
        rule: rule,
        k: req.KnnK,
        epsilon: req.EpsilonBallEpsilon,
        kernel: kernelType,
        bandwidth: req.Bandwidth,
        ensureConnected: ensureConnected);

    // ── Edge layer: serialize CsrGraph — weights are coupling strengths in (0,1] ──
    ProximitySpec spec = req.NeighborRule switch
    {
        "MutualKnn" => new MutualKnnSpec(req.KnnK),
        "EpsilonBall" => new EpsilonBallSpec(req.EpsilonBallEpsilon),
        "MstAugmented" => new MstAugmentedSpec(req.KnnK),
        _ => new KnnSpec(req.KnnK),
    };
    var edgeLayers = new List<EdgeLayer> { BuildEdgeLayerFromCsr(csrGraph, n, gtLabels, metric, spec) };

    // ── Wing-2: local tangent flow — reuses same CsrGraph adjacency ──────────
    var points2d = new double[n][];
    for (int i = 0; i < n; i++)
    {
        points2d[i] = new double[d];
        Array.Copy(features, i * d, points2d[i], 0, d);
    }
    var adjacency = new int[n][];
    for (int i = 0; i < n; i++)
    {
        int rowStart = csrGraph.RowPointers[i];
        int rowEnd = csrGraph.RowPointers[i + 1];
        int len = rowEnd - rowStart;
        adjacency[i] = new int[len];
        for (int j = 0; j < len; j++)
            adjacency[i][j] = csrGraph.Targets[rowStart + j];
    }
    var tangentVectors = LocalTangent.Compute(points2d, adjacency);

    // BFS orientation propagation: aligns tangent signs across the graph so
    // incoherence is a data signal (manifold discontinuity, false bridges) not
    // an artefact of independent power-iteration sign choices.
    LocalTangent.PropagateOrientation(
        tangentVectors, n, d, csrGraph.RowPointers, csrGraph.Targets);

    // Per-point coherence: mean dot(t_i, t_j) over CSR neighbours.
    // High = intrinsic edge; near-zero = ambient shortcut or degenerate neighbourhood.
    double[] coherence = LocalTangent.ComputeCoherence(
        tangentVectors, n, d, csrGraph.RowPointers, csrGraph.Targets);

    var vectorFieldLayers = new List<VectorFieldLayer>
    {
        new VectorFieldLayer("Local PCA Flow", tangentVectors, n, Math.Min(d, 3)),
    };

    // ── Shared: GMM overlays ─────────────────────────────────────────────────
    // Spine-aware overlay: derives σ from spine geometry (analytic, not fitted).
    // Fitted EM overlay: runs a real GMM on the point cloud at the same K.
    int gmmPos = Math.Clamp((int)Math.Round(req.GmmComponents), 0, 4);
    int gmmK = 1 << gmmPos;
    var gaussianLayers = new List<GaussianLayer>(adapted.GaussianLayers);
    foreach (var gmmLayer in BuildGmmOverlaysFromSpines(adapted.SpineLayers, gmmK))
        gaussianLayers.Add(gmmLayer);

    var fittedGmm = new GaussianMixtureModel(k: gmmK, dimension: d);
    fittedGmm.RobustInitialize(points2d);
    fittedGmm.Fit(points2d);
    gaussianLayers.Add(GmmVizAdapter.ToGaussianLayer(fittedGmm, $"GMM (EM) K={gmmK}"));

    var scalarLayers = new List<ScalarLayer>(adapted.ScalarLayers)
    {
        new ScalarLayer("Flow Coherence", coherence, ScalarLayerKind.CoherenceScore),
    };

    // ── Shared: merge params so they round-trip without resetting on regen ────
    var mergedParams = adapted.GeneratorParams != null
        ? new Dictionary<string, object>(adapted.GeneratorParams)
        : new Dictionary<string, object>();
    mergedParams["seed"] = req.Seed;
    mergedParams["knnK"] = req.KnnK;
    mergedParams["metric"] = req.Metric;
    mergedParams["neighborRule"] = req.NeighborRule;
    mergedParams["epsilonBallEpsilon"] = req.EpsilonBallEpsilon;
    mergedParams["kernel"] = req.Kernel;
    mergedParams["bandwidth"] = req.Bandwidth;
    mergedParams["gmmComponents"] = req.GmmComponents;
    mergedParams["showFlow"] = req.ShowFlow;

    var vizDataset = new VizDataset(
        adapted.Points,
        adapted.LabelLayers,
        scalarLayers,
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
        yield return BuildGmmOverlayLayer(spine, sigmaLong, sigmaPerp, stride, offset, $"GMM (Oracle) K={K}");
    }
}

// ── Phase 1: Crescent synthesis + adaptation (no edges, no overlay) ──────────
static (VizDataset adapted, string title) BuildCrescentAdapted(RegenRequest req)
{
    EllipsoidPlacement placement = Enum.TryParse<EllipsoidPlacement>(req.Placement, out var p)
        ? p
        : EllipsoidPlacement.OrthogonalElbowIntersect;

    EllipsoidShellMode shellMode = Enum.TryParse<EllipsoidShellMode>(req.EllipsoidShellMode, out var sm)
        ? sm : EllipsoidShellMode.Solid;

    var dataset = GenerateCrescentAndEllipsoid(
        crescentPoints: req.CrescentPoints,
        crescentRadius: req.CrescentRadius,
        crescentWidth: req.CrescentWidth,
        arcHalfAngle: req.ArcHalfAngle,
        ellipsoidPoints: req.EllipsoidPoints,
        ellipsoidAxes: req.EllipsoidAxes,
        ellipsoidShellMode: shellMode,
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
        : MobiusPlacement.CenterCrossOrtho;

    TubeCrossSection crossSection = Enum.TryParse<TubeCrossSection>(req.CrossSection, out var cs)
        ? cs
        : TubeCrossSection.GaussianIsotropic;

    MobiusSpineShape spineShape = Enum.TryParse<MobiusSpineShape>(req.SpineShape, out var ss)
        ? ss
        : MobiusSpineShape.Circle;

    EllipsoidShellMode shellMode = Enum.TryParse<EllipsoidShellMode>(req.EllipsoidShellMode, out var sm)
        ? sm : EllipsoidShellMode.Solid;

    var dataset = GenerateMobiusAndEllipsoid(
        mobiusPoints: req.MobiusPoints,
        spineRadius: req.SpineRadius,
        halfWidth: req.HalfWidth,
        halfThickness: req.HalfThickness,
        noiseSigma: req.NoiseSigma,
        twistCount: req.TwistCount,
        crossSection: crossSection,
        radialBias: req.RadialBias,
        spineShape: spineShape,
        splayFactor: req.SplayFactor,
        ellipsoidPoints: req.EllipsoidPoints,
        ellipsoidAxes: req.EllipsoidAxes,
        ellipsoidShellMode: shellMode,
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

// Serialize a CsrGraph into an EdgeLayer.
// Weights are coupling strengths in (0,1] — high weight = high similarity = bright.
// Iterates upper triangle only (j > i) to emit each undirected edge once.
static EdgeLayer BuildEdgeLayerFromCsr(
    CsrGraph graph, int n, int[] gtLabels,
    VizMetric metric, ProximitySpec proximity)
{
    // Count edges in upper triangle
    int edgeCount = 0;
    for (int i = 0; i < n; i++)
        for (int idx = graph.RowPointers[i]; idx < graph.RowPointers[i + 1]; idx++)
            if (graph.Targets[idx] > i) edgeCount++;

    var src = new int[edgeCount];
    var dst = new int[edgeCount];
    var weight = new double[edgeCount];
    int e = 0;

    for (int i = 0; i < n; i++)
        for (int idx = graph.RowPointers[i]; idx < graph.RowPointers[i + 1]; idx++)
        {
            int j = graph.Targets[idx];
            if (j <= i) continue;
            src[e] = i;
            dst[e] = j;
            weight[e] = graph.Weights[idx];  // coupling strength, not distance
            e++;
        }

    int[]? edgeClusterSrc = null;
    int[]? edgeClusterDst = null;
    if (gtLabels.Length > 0)
    {
        edgeClusterSrc = new int[edgeCount];
        edgeClusterDst = new int[edgeCount];
        for (int ei = 0; ei < edgeCount; ei++)
        {
            edgeClusterSrc[ei] = gtLabels[src[ei]];
            edgeClusterDst[ei] = gtLabels[dst[ei]];
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

// Extracts row i from a flat N×D array as a new double[].
// Used by metrics that require double[] inputs (FisherRaoSimplex, FisherRaoHalfPlane).
// Allocation is acceptable here — these metrics are selected explicitly and are
// not on the Euclidean/Manhattan hot path.
static double[] RowOf(double[] features, int d, int i)
{
    var row = new double[d];
    Array.Copy(features, i * d, row, 0, d);
    return row;
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
    int CrescentPoints = 10000,
    double CrescentRadius = 3.0,
    double CrescentWidth = 0.40,
    double ArcHalfAngle = 2.04,
    double GmmComponents = 1,   // slider pos: 0→K=1, 1→K=2, 2→K=4, 3→K=8, 4→K=16
                                // ── MobiusAndEllipsoid ─────────────────────────────────────────────────────
    int MobiusPoints = 10000,
    double SpineRadius = 2.5,
    double HalfWidth = 1.1,
    double HalfThickness = 0.12,
    double NoiseSigma = 0.06,
    int TwistCount = 1,
    double RadialBias = 1.0,
    string CrossSection = "Annular",
    string SpineShape = "FigureEight",
    double SplayFactor = 0.7,
    // ── Shared ellipsoid ───────────────────────────────────────────────────────
    int EllipsoidPoints = 5000,
    double[]? EllipsoidAxes = null,
    string EllipsoidShellMode = "Solid",
    // ── Embedding (Möbius only) ────────────────────────────────────────────
    int Dimensions = 3,
    // ── Placement (shared shape) ───────────────────────────────────────────────
    string Placement = "OrthogonalElbowIntersect",
    double IntersectDepth = 0.0,
    double IntersectRadialShift = 0.0,
    double GapScale = 1.0,
    // ── Graph ──────────────────────────────────────────────────────────────────
    int Seed = 42,
    int KnnK = 5,
    string Metric = "Euclidean",
    string NeighborRule = "Knn",
    double EpsilonBallEpsilon = 0.5,
    string Kernel = "Gaussian",
    double Bandwidth = 0.0,    // 0 = auto-estimate via BandwidthEstimation
                               // ── Overlay ────────────────────────────────────────────────────────────────
    bool ShowFlow = false);
