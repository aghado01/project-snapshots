using System;
using System.Collections.Generic;
using Graphs.Primitives;
using Maths.LinAlg;
using Repo.TestHarness;
using Synthetic;
using TDA.Primitives;
using Xunit;

namespace VizCore.Tests;

public sealed class SpectralDenseDispatchTests
{
    [Fact]
    public void ComputeBottomK_FlatColumnMajorMatchesRectangular()
    {
        CsrGraph graph = BuildConnectedFixtureGraph();

        IReadOnlyList<EigenPair> rectangular = Spectral.ComputeBottomK(
            graph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            lapType: LaplacianType.Combinatorial,
            solverKind: SolverKind.Dense,
            denseOptions: default,
            denseMaterialization: DenseLaplacianMaterialization.Rectangular);

        IReadOnlyList<EigenPair> flat = Spectral.ComputeBottomK(
            graph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            lapType: LaplacianType.Combinatorial,
            solverKind: SolverKind.Dense,
            denseOptions: default,
            denseMaterialization: DenseLaplacianMaterialization.FlatColumnMajor);

        var run = HarnessArtifacts.Create(
            runKind: "test-runs",
            suiteName: nameof(SpectralDenseDispatchTests),
            runName: nameof(ComputeBottomK_FlatColumnMajorMatchesRectangular),
            metadata: new Dictionary<string, object?>
            {
                ["Comparison"] = "RectangularVsFlatColumnMajor",
                ["NodeCount"] = graph.NodeCount,
            });

        double[,] laplacian = BuildCombinatorialLaplacian(graph);
        string comparisonPath = run.WriteJson(
            "comparison.json",
            BuildComparisonRecord(graph, rectangular, flat, laplacian));

        Console.WriteLine($"RunRoot\t{run.RunDirectory}");
        Console.WriteLine($"Manifest\t{run.ManifestPath}");
        Console.WriteLine($"Comparison\t{comparisonPath}");

        AssertBottomPairsMatch(rectangular, flat, graph, eigenvalueTolerance: 1e-8, residualTolerance: 1e-7);
    }

    [Fact]
    public void ComputeBottomK_FmaVariantMatchesDefaultFastVariant()
    {
#if EIGEN_REFERENCE
        return;
#else
        CsrGraph graph = BuildConnectedFixtureGraph();

        IReadOnlyList<EigenPair> baseline = Spectral.ComputeBottomK(
            graph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            lapType: LaplacianType.Combinatorial,
            solverKind: SolverKind.Dense,
            denseOptions: default,
            denseMaterialization: DenseLaplacianMaterialization.FlatColumnMajor);

        IReadOnlyList<EigenPair> fma = Spectral.ComputeBottomK(
            graph,
            seed: FixtureGoldenHelpers.SpectralSeed,
            k: 8,
            lapType: LaplacianType.Combinatorial,
            solverKind: SolverKind.Dense,
            denseOptions: new DenseEigenOptions(DenseEigenFastVariant.Fma),
            denseMaterialization: DenseLaplacianMaterialization.FlatColumnMajor);

        var run = HarnessArtifacts.Create(
            runKind: "test-runs",
            suiteName: nameof(SpectralDenseDispatchTests),
            runName: nameof(ComputeBottomK_FmaVariantMatchesDefaultFastVariant),
            metadata: new Dictionary<string, object?>
            {
                ["Comparison"] = "DefaultVsFma",
                ["NodeCount"] = graph.NodeCount,
            });

        double[,] laplacian = BuildCombinatorialLaplacian(graph);
        string comparisonPath = run.WriteJson(
            "comparison.json",
            BuildComparisonRecord(graph, baseline, fma, laplacian));

        Console.WriteLine($"RunRoot\t{run.RunDirectory}");
        Console.WriteLine($"Manifest\t{run.ManifestPath}");
        Console.WriteLine($"Comparison\t{comparisonPath}");

        AssertBottomPairsMatch(baseline, fma, graph, eigenvalueTolerance: 1e-8, residualTolerance: 1e-7);
#endif
    }

    private static CsrGraph BuildConnectedFixtureGraph()
    {
        double[][] points = SyntheticData.GenerateCrescentAndEllipsoid(
            crescentPoints: 160,
            ellipsoidPoints: 0,
            seed: 42).Features;

        return FixtureGoldenHelpers.BuildDraftGraph(
            points,
            k: FixtureGoldenHelpers.ConnectedFixtureK,
            ensureConnected: true);
    }

    private static void AssertBottomPairsMatch(
        IReadOnlyList<EigenPair> expected,
        IReadOnlyList<EigenPair> actual,
        CsrGraph graph,
        double eigenvalueTolerance,
        double residualTolerance)
    {
        Assert.Equal(expected.Count, actual.Count);

        double[,] laplacian = BuildCombinatorialLaplacian(graph);
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.InRange(Math.Abs(expected[i].Lambda - actual[i].Lambda), 0.0, eigenvalueTolerance);
            Assert.InRange(ComputeResidualNorm(laplacian, expected[i].Lambda, expected[i].Vector), 0.0, residualTolerance);
            Assert.InRange(ComputeResidualNorm(laplacian, actual[i].Lambda, actual[i].Vector), 0.0, residualTolerance);
        }
    }

    private static ComparisonRecord BuildComparisonRecord(
        CsrGraph graph,
        IReadOnlyList<EigenPair> expected,
        IReadOnlyList<EigenPair> actual,
        double[,] laplacian)
    {
        var comparisons = new List<EigenPairComparison>(expected.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            comparisons.Add(new EigenPairComparison(
                Index: i,
                ExpectedLambda: expected[i].Lambda,
                ActualLambda: actual[i].Lambda,
                AbsoluteLambdaDelta: Math.Abs(expected[i].Lambda - actual[i].Lambda),
                ExpectedResidual: ComputeResidualNorm(laplacian, expected[i].Lambda, expected[i].Vector),
                ActualResidual: ComputeResidualNorm(laplacian, actual[i].Lambda, actual[i].Vector)));
        }

        return new ComparisonRecord(
            NodeCount: graph.NodeCount,
            PairCount: expected.Count,
            Comparisons: comparisons);
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

    private sealed record ComparisonRecord(
        int NodeCount,
        int PairCount,
        IReadOnlyList<EigenPairComparison> Comparisons);

    private sealed record EigenPairComparison(
        int Index,
        double ExpectedLambda,
        double ActualLambda,
        double AbsoluteLambdaDelta,
        double ExpectedResidual,
        double ActualResidual);
}
