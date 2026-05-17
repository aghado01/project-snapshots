using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Clustering.SPC;
using Clustering.SPC.Cuts;
using Clustering.SPC.Potts;
using Graphs.Distance.Euclidean;
using Graphs.Primitives;
using Repo.TestHarness;
using Synthetic;

namespace Spc.BlattSmoke;

/// <summary>
/// End-to-end smoke harness: Blatt 1997 Euclidean hierarchy → mutual-kNN
/// CsrGraph → SpcScheduler dump. Produces one checkpoint file per
/// (Temperature, Replica) task in an artifacts directory ready for
/// offline χ(T) / purity analysis.
/// </summary>
/// <remarks>
/// <para>Graph construction is inlined here rather than going through
/// <c>GraphBuilder.Build</c> from <c>Graphs.Proximity</c> — that project
/// has its own pending rename ripple. Distance computation itself comes
/// from <c>Graphs.Distance.Euclidean</c> (the real assembly), so the
/// only inline math is the kNN selection + Gaussian kernel application,
/// not the metric. When Graphs.Proximity stabilizes, swap the helpers
/// below for the canonical builder; no other code in this harness
/// changes.</para>
/// </remarks>
internal static class Program
{
    // ── Blatt 1997 canonical configuration ────────────────────────────────
    private const int                 K              = 10;
    private const int                 Q              = 20;
    private const int                 CyclesPerTask  = 1000;
    private const int                 BurnInCycles   = 200;
    private const int                 NumReplicas    = 3;
    private const int                 BaseSeed       = 42;
    private const PottsSusceptibility Susceptibility = PottsSusceptibility.FkAndEdges;

    private static int Main(string[] args)
    {
        // Top-down T schedule per SPC convention: T_max → T_min.
        // 0.20 → 0.005 step 0.005 brackets Blatt's reported T_fs ≈ 0.075.
        double[] temperatures = Enumerable.Range(0, 40)
            .Select(i => 0.20 - i * 0.005)
            .Where(t => t > 0)
            .ToArray();

        ArtifactRun run = args.Length > 0
            ? HarnessArtifacts.Attach(
                runKind: "smoke-runs",
                suiteName: "Spc.BlattSmoke",
                runName: "Main",
                runDirectory: args[0],
                metadata: new Dictionary<string, object?>
                {
                    ["OutputDirectoryMode"] = "user-specified",
                })
            : HarnessArtifacts.Create(
                runKind: "smoke-runs",
                suiteName: "Spc.BlattSmoke",
                runName: "Main",
                metadata: new Dictionary<string, object?>
                {
                    ["OutputDirectoryMode"] = "default",
                });
        string outputDir = run.RunDirectory;

        // ── Stage 1: synthetic hierarchy ──────────────────────────────────
        var dataset = SyntheticData.GenerateBlattHierarchy(seed: BaseSeed);
        int n = dataset.Features.Length;
        int levels = dataset.LabelsByLevel?.Length ?? 0;

        // ── Stage 2: pairwise Euclidean distances ─────────────────────────
        var distances = ComputePairwiseDistances(dataset.Features);

        // ── Stage 3: mutual-kNN selection ─────────────────────────────────
        var pairs = SelectMutualKnn(distances, K);

        // ── Stage 4: bandwidth — median of 1-NN distances ─────────────────
        double bandwidth = MedianOneNnDistance(distances);

        // ── Stage 5: Gaussian-weighted Edge[] → CsrGraph ──────────────────
        var edges = new Edge[pairs.Count];
        double twoBwSq = 2.0 * bandwidth * bandwidth;
        for (int e = 0; e < pairs.Count; e++)
        {
            var (i, j) = pairs[e];
            double d = distances[i, j];
            double coupling = Math.Exp(-(d * d) / twoBwSq);
            edges[e] = new Edge(i, j, coupling);
        }
        var graph = CsrGraph.FromEdges(edges, n);

        // ── Stage 6: flat task list ───────────────────────────────────────
        var tasks = SpcScheduler.BuildTaskList(
            temperatures:         temperatures,
            numReplicas:          NumReplicas,
            cyclesPerTask:        CyclesPerTask,
            q:                    Q,
            susceptibility:       Susceptibility,
            checkpointDirectory:  outputDir,
            baseSeed:             BaseSeed,
            burnInCyclesPerTask:  BurnInCycles);

        // ── Stage 7: run ──────────────────────────────────────────────────
        var sw = Stopwatch.StartNew();
        SpcScheduler.Run(graph, tasks);
        sw.Stop();

        int written = Directory.GetFiles(outputDir, "*.spcx").Length;

        // ── Stage 8: BlattCanonicalCut + per-level purity per T ───────────
        var groundTruth = dataset.LabelsByLevel;
        string? cutsPath = null;
        int cutRowCount = 0;
        if (groundTruth is not null && groundTruth.Length > 0)
            (cutsPath, cutRowCount) = WriteCutsCsv(outputDir, graph, groundTruth, theta: 0.5);

        string summaryPath = run.WriteJson(
            "summary.json",
            new
            {
                Dataset = new
                {
                    PointCount = n,
                    dataset.ClusterCount,
                    GroundTruthLevels = levels,
                },
                Graph = new
                {
                    MutualK = K,
                    UndirectedEdgeCount = pairs.Count,
                    Bandwidth = bandwidth,
                    CsrEntries = graph.Targets.Length,
                },
                Schedule = new
                {
                    Temperatures = temperatures,
                    NumReplicas,
                    TaskCount = tasks.Count,
                    CyclesPerTask,
                    BurnInCycles,
                    Susceptibility = Susceptibility.ToString(),
                },
                Run = new
                {
                    CheckpointsWritten = written,
                    ExpectedCheckpoints = tasks.Count,
                    WallSeconds = sw.Elapsed.TotalSeconds,
                },
                Cuts = new
                {
                    Path = cutsPath,
                    RowCount = cutRowCount,
                },
            });

        Console.WriteLine($"RunRoot\t{run.RunDirectory}");
        Console.WriteLine($"Manifest\t{run.ManifestPath}");
        Console.WriteLine($"Summary\t{summaryPath}");
        if (cutsPath is not null)
            Console.WriteLine($"CutsCsv\t{cutsPath}");

        return 0;
    }

    private static readonly string[] LevelNames = { "coarse", "medium", "fine" };

    private static (string? Path, int RowCount) WriteCutsCsv(string outputDir, CsrGraph graph, int[][] levels, double theta)
    {
        var spceFiles = Directory.GetFiles(outputDir, "*.spce");
        if (spceFiles.Length == 0)
        {
            return (null, 0);
        }

        // Group sidecars by temperature so all replicas at the same T pool
        // into one set of accumulators before the cut is applied. The cut
        // policy operates on (pooledSpinAgreementCount, pooledCycleCount);
        // pooling adds independent MC samples, which is what variance
        // reduction across replicas wants.
        var byT = spceFiles
            .Select(PottsModelEdgeObservablesIO.ReadFromFile)
            .GroupBy(e => e.Temperature)
            .OrderByDescending(g => g.Key);   // top-down

        var rows = new List<(double T, int Replicas, int Clusters, double[] Purities)>();
        foreach (var group in byT)
        {
            var replicas = group.ToList();
            int csrLength = replicas[0].SpinAgreementCount.Length;
            var pooledSpin = new int[csrLength];
            int pooledCycles = 0;
            foreach (var e in replicas)
            {
                pooledCycles += e.CycleCount;
                for (int i = 0; i < csrLength; i++)
                    pooledSpin[i] += e.SpinAgreementCount[i];
            }

            var cut = BlattCanonicalCut.Apply(graph, pooledSpin, pooledCycles, theta);
            var p = new double[levels.Length];
            for (int li = 0; li < levels.Length; li++)
                p[li] = Purity.Compute(cut.Labels, levels[li]);
            rows.Add((group.Key, replicas.Count, cut.ClusterCount, p));
        }

        var purityCols = string.Join(",", LevelNames.Take(levels.Length).Select(n => $"purity_{n}"));
        var lines = new List<string> { $"T,replicas,clusters,{purityCols}" };
        foreach (var (t, r, k, p) in rows)
        {
            string ps = string.Join(",", p.Select(x => x.ToString("F4", CultureInfo.InvariantCulture)));
            lines.Add(string.Format(CultureInfo.InvariantCulture, "{0:F5},{1},{2},{3}", t, r, k, ps));
        }
        string cutsPath = Path.Combine(outputDir, "cuts.csv");
        File.WriteAllLines(cutsPath, lines);
        return (cutsPath, rows.Count);
    }

    // ── Inline graph construction (TODO: swap for GraphBuilder.Build) ─────

    private static double[,] ComputePairwiseDistances(double[][] points)
    {
        int n = points.Length;
        var d = new double[n, n];
        Parallel.For(0, n, i =>
        {
            for (int j = i + 1; j < n; j++)
            {
                double dist = Euclidean.Distance(points[i], points[j]);
                d[i, j] = dist;
                d[j, i] = dist;
            }
        });
        return d;
    }

    private static List<(int i, int j)> SelectMutualKnn(double[,] distances, int k)
    {
        int n = distances.GetLength(0);
        var knn = new int[n][];

        Parallel.For(0, n, i =>
        {
            var pairs = new (double d, int j)[n - 1];
            int idx = 0;
            for (int j = 0; j < n; j++)
            {
                if (i == j) continue;
                pairs[idx++] = (distances[i, j], j);
            }
            Array.Sort(pairs, (a, b) => a.d.CompareTo(b.d));
            knn[i] = pairs.Take(k).Select(p => p.j).ToArray();
        });

        var sets = knn.Select(row => new HashSet<int>(row)).ToArray();
        var edges = new List<(int, int)>(n * k / 2);
        for (int i = 0; i < n; i++)
        {
            foreach (int j in knn[i])
            {
                if (j > i && sets[j].Contains(i))
                    edges.Add((i, j));
            }
        }
        return edges;
    }

    private static double MedianOneNnDistance(double[,] distances)
    {
        int n = distances.GetLength(0);
        var oneNn = new double[n];
        Parallel.For(0, n, i =>
        {
            double min = double.PositiveInfinity;
            for (int j = 0; j < n; j++)
            {
                if (i == j) continue;
                double d = distances[i, j];
                if (d < min) min = d;
            }
            oneNn[i] = min;
        });
        Array.Sort(oneNn);
        return n % 2 == 0
            ? 0.5 * (oneNn[n / 2 - 1] + oneNn[n / 2])
            : oneNn[n / 2];
    }
}
