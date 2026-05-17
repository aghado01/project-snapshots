using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Graphs.Primitives;
using Maths.LinAlg;
using Repo.TestHarness;
using Synthetic;
using TDA.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace VizCore.Tests;

public sealed class EigenFastTimingHarness
{
    private static int _spectralSink;
    private readonly ITestOutputHelper _output;

    public EigenFastTimingHarness(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void CompareReferenceAndFastTimings()
    {
#if DEBUG
        _output.WriteLine("Benchmark harness only reports meaningful timings in Release builds.");
        return;
#else
        var run = HarnessArtifacts.Create(
            runKind: "test-runs",
            suiteName: nameof(EigenFastTimingHarness),
            runName: nameof(CompareReferenceAndFastTimings),
            metadata: new Dictionary<string, object?>
            {
                ["DenseEigenBackend"] = GetDenseEigenBackendLabel(),
                ["Configuration"] = "Release",
                ["EnableBenchmarks"] = true,
            });

        string directPath = run.WriteJson("direct-comparison.json", Array.Empty<DirectBenchmarkCase>());
        string variantPath = run.WriteJson("fast-variant-comparison.json", Array.Empty<FastVariantBenchmarkCase>());
        string spectralPath = run.WriteJson("spectral-dispatch-comparison.json", Array.Empty<SpectralBenchmarkCase>());
        string summaryPath = run.WriteText("summary.txt", "pending");

        var directCases = new List<DirectBenchmarkCase>();
        var variantCases = new List<FastVariantBenchmarkCase>();
        var spectralCases = new List<SpectralBenchmarkCase>();

        foreach (int size in new[] { 32, 64, 128, 256, 512 })
        {
            double[,] matrix = BuildRandomSymmetricMatrix(size, seed: 10_000 + size);
            directCases.Add(MeasureCase($"rand-{size}", matrix));
        }

        DisconnectedControl.Fixture control = DisconnectedControl.Generate(pointsPerComponent: 64, separation: 100.0);
        CsrGraph graph = FixtureGoldenHelpers.BuildDraftGraph(control.Points, k: 4, ensureConnected: true);
        directCases.Add(MeasureCase("lap-128", BuildCombinatorialLaplacian(graph)));

#if !EIGEN_REFERENCE
        foreach (int size in new[] { 128, 256, 512 })
        {
            double[,] matrix = BuildRandomSymmetricMatrix(size, seed: 20_000 + size);
            variantCases.Add(MeasureFastVariantCase($"rand-{size}", matrix));
        }
#endif

        double[][] crescent = SyntheticData.GenerateCrescentAndEllipsoid(crescentPoints: 500, ellipsoidPoints: 0, seed: 42).Features;
        CsrGraph crescentGraph = FixtureGoldenHelpers.BuildDraftGraph(crescent, k: FixtureGoldenHelpers.ConnectedFixtureK, ensureConnected: true);
        spectralCases.Add(MeasureSpectralCase(
            "spectral-crescent-500",
            crescentGraph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            denseOptions: default,
            materialization: DenseLaplacianMaterialization.Rectangular));
        spectralCases.Add(MeasureSpectralCase(
            "spectral-crescent-500",
            crescentGraph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            denseOptions: default,
            materialization: DenseLaplacianMaterialization.FlatColumnMajor));

#if !EIGEN_REFERENCE
        spectralCases.Add(MeasureSpectralCase(
            "spectral-crescent-500",
            crescentGraph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            denseOptions: new DenseEigenOptions(DenseEigenFastVariant.Fma),
            materialization: DenseLaplacianMaterialization.FlatColumnMajor));
#endif

        double[][] mobius = SyntheticData.GenerateMobiusAndEllipsoid(mobiusPoints: 500, ellipsoidPoints: 0, seed: 42).Features;
        CsrGraph mobiusGraph = FixtureGoldenHelpers.BuildDraftGraph(mobius, k: FixtureGoldenHelpers.ConnectedFixtureK, ensureConnected: true);
        spectralCases.Add(MeasureSpectralCase(
            "spectral-mobius-500",
            mobiusGraph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            denseOptions: default,
            materialization: DenseLaplacianMaterialization.Rectangular));
        spectralCases.Add(MeasureSpectralCase(
            "spectral-mobius-500",
            mobiusGraph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            denseOptions: default,
            materialization: DenseLaplacianMaterialization.FlatColumnMajor));

#if !EIGEN_REFERENCE
        spectralCases.Add(MeasureSpectralCase(
            "spectral-mobius-500",
            mobiusGraph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            denseOptions: new DenseEigenOptions(DenseEigenFastVariant.Fma),
            materialization: DenseLaplacianMaterialization.FlatColumnMajor));
#endif

        directPath = run.WriteJson("direct-comparison.json", directCases);
        variantPath = run.WriteJson("fast-variant-comparison.json", variantCases);
        spectralPath = run.WriteJson("spectral-dispatch-comparison.json", spectralCases);
        summaryPath = run.WriteText("summary.txt", BuildSummary(directCases, variantCases, spectralCases));

        _output.WriteLine($"RunRoot\t{run.RunDirectory}");
        _output.WriteLine($"Manifest\t{run.ManifestPath}");
        _output.WriteLine($"DirectComparison\t{directPath}");
        _output.WriteLine($"FastVariantComparison\t{variantPath}");
        _output.WriteLine($"SpectralDispatchComparison\t{spectralPath}");
        _output.WriteLine($"Summary\t{summaryPath}");
#endif
    }

    private static string BuildSummary(
        IReadOnlyList<DirectBenchmarkCase> directCases,
        IReadOnlyList<FastVariantBenchmarkCase> variantCases,
        IReadOnlyList<SpectralBenchmarkCase> spectralCases)
    {
        var builder = new StringBuilder();
        builder.AppendLine("direct-comparison");
        builder.AppendLine("Label\tEigenMs\tEigenFastMs\tSpeedup\tResidual");
        foreach (DirectBenchmarkCase item in directCases)
            builder.AppendLine($"{item.Label}\t{item.EigenMilliseconds:F3}\t{item.EigenFastMilliseconds:F3}\t{item.Speedup:F2}x\t{item.Residual:E3}");

        if (variantCases.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("fast-variant-comparison");
            builder.AppendLine("Label\tDefaultMs\tFmaMs\tSpeedup\tResidual");
            foreach (FastVariantBenchmarkCase item in variantCases)
                builder.AppendLine($"{item.Label}\t{item.DefaultMilliseconds:F3}\t{item.FmaMilliseconds:F3}\t{item.Speedup:F2}x\t{item.Residual:E3}");
        }

        builder.AppendLine();
        builder.AppendLine("spectral-dispatch-comparison");
        builder.AppendLine("Label\tMaterialization\tFastVariant\tComputeBottomKMs\tLowestLambda\tCount");
        foreach (SpectralBenchmarkCase item in spectralCases)
            builder.AppendLine($"{item.Label}\t{item.Materialization}\t{item.FastVariant}\t{item.ComputeBottomKMilliseconds:F3}\t{item.LowestLambda:G17}\t{item.Count}");

        return builder.ToString();
    }

    private DirectBenchmarkCase MeasureCase(string label, double[,] matrix)
    {
        WarmUp(matrix);

        double eigenMedian = MeasureMedianMilliseconds(() => Eigen.DecomposeSymmetric(matrix), iterations: 7);
        double fastMedian = MeasureMedianMilliseconds(() => EigenFast.DecomposeSymmetric(matrix), iterations: 7);
        EigenResult fast = EigenFast.DecomposeSymmetric(matrix);
        double residual = ComputeMaxResidualNorm(matrix, fast);
        double speedup = eigenMedian / fastMedian;

        return new DirectBenchmarkCase(label, eigenMedian, fastMedian, speedup, residual);
    }

    private FastVariantBenchmarkCase MeasureFastVariantCase(string label, double[,] matrix)
    {
        WarmUp(matrix);
        _ = EigenFast.DecomposeSymmetric(matrix, fastVariant: DenseEigenFastVariant.Fma);

        double defaultMedian = MeasureMedianMilliseconds(
            () => EigenFast.DecomposeSymmetric(matrix),
            iterations: 7);
        double fmaMedian = MeasureMedianMilliseconds(
            () => EigenFast.DecomposeSymmetric(matrix, fastVariant: DenseEigenFastVariant.Fma),
            iterations: 7);
        EigenResult fma = EigenFast.DecomposeSymmetric(matrix, fastVariant: DenseEigenFastVariant.Fma);
        double residual = ComputeMaxResidualNorm(matrix, fma);
        double speedup = defaultMedian / fmaMedian;

        return new FastVariantBenchmarkCase(label, defaultMedian, fmaMedian, speedup, residual);
    }

    private SpectralBenchmarkCase MeasureSpectralCase(
        string label,
        CsrGraph graph,
        int seed,
        int k,
        DenseEigenOptions denseOptions,
        DenseLaplacianMaterialization materialization)
    {
        WarmUpSpectral(graph, seed, k, denseOptions, materialization);

        double median = MeasureMedianMilliseconds(() =>
        {
            IReadOnlyList<EigenPair> pairs = Spectral.ComputeBottomK(
                graph,
                seed: seed,
                k: k,
                lapType: LaplacianType.Combinatorial,
                solverKind: SolverKind.Dense,
                denseOptions: denseOptions,
                denseMaterialization: materialization);
            _spectralSink = pairs.Count;
        }, iterations: 5);

        IReadOnlyList<EigenPair> result = Spectral.ComputeBottomK(
            graph,
            seed: seed,
            k: k,
            lapType: LaplacianType.Combinatorial,
            solverKind: SolverKind.Dense,
            denseOptions: denseOptions,
            denseMaterialization: materialization);

        double lowestLambda = result.Count > 0 ? result[0].Lambda : double.NaN;
        return new SpectralBenchmarkCase(
            label,
            materialization.ToString(),
            denseOptions.FastVariant.ToString(),
            median,
            lowestLambda,
            result.Count);
    }

    private static void WarmUp(double[,] matrix)
    {
        _ = Eigen.DecomposeSymmetric(matrix);
        _ = EigenFast.DecomposeSymmetric(matrix);
    }

    private static void WarmUpSpectral(
        CsrGraph graph,
        int seed,
        int k,
        DenseEigenOptions denseOptions,
        DenseLaplacianMaterialization materialization)
    {
        IReadOnlyList<EigenPair> pairs = Spectral.ComputeBottomK(
            graph,
            seed: seed,
            k: k,
            lapType: LaplacianType.Combinatorial,
            solverKind: SolverKind.Dense,
            denseOptions: denseOptions,
            denseMaterialization: materialization);
        _spectralSink = pairs.Count;
    }

    private static string GetDenseEigenBackendLabel()
    {
#if EIGEN_REFERENCE
        return "Eigen";
#else
        return "EigenFast";
#endif
    }

    private static double MeasureMedianMilliseconds(Action action, int iterations)
    {
        var samples = new List<double>(iterations);

        for (int i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        return samples[samples.Count / 2];
    }

    private static double ComputeMaxResidualNorm(double[,] matrix, EigenResult result)
    {
        double max = 0.0;

        for (int i = 0; i < result.Eigenvalues.Length; i++)
        {
            double residual = ComputeResidualNorm(matrix, result.Eigenvalues[i], result.Eigenvectors[i]);
            if (residual > max)
                max = residual;
        }

        return max;
    }

    private static double ComputeResidualNorm(double[,] matrix, double eigenvalue, double[] eigenvector)
    {
        int n = eigenvector.Length;
        double sumSquares = 0.0;

        for (int row = 0; row < n; row++)
        {
            double projected = 0.0;
            for (int col = 0; col < n; col++)
                projected += matrix[row, col] * eigenvector[col];

            double residual = projected - eigenvalue * eigenvector[row];
            sumSquares += residual * residual;
        }

        return Math.Sqrt(sumSquares);
    }

    private static double[,] BuildRandomSymmetricMatrix(int size, int seed)
    {
        var random = new Random(seed);
        var matrix = new double[size, size];

        for (int i = 0; i < size; i++)
        {
            for (int j = i; j < size; j++)
            {
                double value = random.NextDouble() * 2.0 - 1.0;
                matrix[i, j] = value;
                matrix[j, i] = value;
            }
        }

        return matrix;
    }

    private static double[,] BuildCombinatorialLaplacian(CsrGraph graph)
    {
        int n = graph.NodeCount;
        var laplacian = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            int start = graph.RowPointers[i];
            int end = graph.RowPointers[i + 1];
            double degree = 0.0;

            for (int edge = start; edge < end; edge++)
            {
                degree += graph.Weights[edge];
                laplacian[i, graph.Targets[edge]] -= graph.Weights[edge];
            }

            laplacian[i, i] = degree;
        }

        return laplacian;
    }

    private readonly record struct DirectBenchmarkCase(
        string Label,
        double EigenMilliseconds,
        double EigenFastMilliseconds,
        double Speedup,
        double Residual);

    private readonly record struct FastVariantBenchmarkCase(
        string Label,
        double DefaultMilliseconds,
        double FmaMilliseconds,
        double Speedup,
        double Residual);

    private readonly record struct SpectralBenchmarkCase(
        string Label,
        string Materialization,
        string FastVariant,
        double ComputeBottomKMilliseconds,
        double LowestLambda,
        int Count);
}
