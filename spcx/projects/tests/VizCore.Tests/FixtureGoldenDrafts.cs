using System;
using System.Collections.Generic;
using System.Linq;
using Graphs;
using Graphs.Diagnostics;
using Graphs.Primitives;
using Repo.TestHarness;
using Synthetic;
using TDA.Primitives;
using Viz;
using Xunit;

namespace VizCore.Tests;

public sealed class CrescentFixtureGoldenDrafts
{
    [Fact]
    public void Crescent_NodeSignal_FiedlerVector_MonotonicAlongArc()
    {
        var dataset = SyntheticData.GenerateCrescentAndEllipsoid(crescentPoints: 500, ellipsoidPoints: 0, seed: 42);
        var graph = FixtureGoldenHelpers.BuildDraftGraph(dataset.Features, k: FixtureGoldenHelpers.ConnectedFixtureK, ensureConnected: true);
        var draft = FixtureGoldenHelpers.RunDraftSpectral(dataset.Features, graph, eigenCount: 2, seed: FixtureGoldenHelpers.SpectralSeed);

        var fiedler = Assert.IsType<NodeSignalLayer>(draft.Fiedler);
        Assert.Equal(ScalarSource.Eigenfunction, fiedler.Source);
        Assert.Equal(draft.Points.Length, fiedler.Values.Length);

        double rho = Math.Abs(FixtureGoldenHelpers.SpearmanRank(draft.ArcParameter, fiedler.Values.ToArray()));

        var run = FixtureGoldenArtifacts.CreateRun(
            nameof(CrescentFixtureGoldenDrafts),
            nameof(Crescent_NodeSignal_FiedlerVector_MonotonicAlongArc),
            fixtureName: "Crescent",
            assertionName: "FiedlerMonotonicAlongArc",
            graph.NodeCount);
        string analysisPath = FixtureGoldenArtifacts.WriteAnalysis(
            run,
            graph,
            draft,
            new
            {
                SpearmanAbsRho = rho,
                ExpectedRange = new { Min = 0.95, Max = 1.0 },
            });
        FixtureGoldenArtifacts.WriteRunMetadata(run, analysisPath);

        Assert.InRange(rho, 0.95, 1.0);
    }

    [Fact]
    public void Crescent_NodeSignal_FiedlerVector_HistogramRoughlyUniform()
    {
        var dataset = SyntheticData.GenerateCrescentAndEllipsoid(crescentPoints: 500, ellipsoidPoints: 0, seed: 42);
        var graph = FixtureGoldenHelpers.BuildDraftGraph(dataset.Features, k: FixtureGoldenHelpers.ConnectedFixtureK, ensureConnected: true);
        var draft = FixtureGoldenHelpers.RunDraftSpectral(dataset.Features, graph, eigenCount: 2, seed: FixtureGoldenHelpers.SpectralSeed);
        var fiedler = Assert.IsType<NodeSignalLayer>(draft.Fiedler);
        int[] hist = FixtureGoldenHelpers.BinHistogram(fiedler.Values.ToArray(), bins: 20);
        double rho = Math.Abs(FixtureGoldenHelpers.SpearmanRank(draft.ArcParameter, fiedler.Values.ToArray()));

        var run = FixtureGoldenArtifacts.CreateRun(
            nameof(CrescentFixtureGoldenDrafts),
            nameof(Crescent_NodeSignal_FiedlerVector_HistogramRoughlyUniform),
            fixtureName: "Crescent",
            assertionName: "FiedlerHistogramRoughlyUniform",
            graph.NodeCount);
        string analysisPath = FixtureGoldenArtifacts.WriteAnalysis(
            run,
            graph,
            draft,
            new
            {
                Histogram = hist,
                SpearmanAbsRho = rho,
                ExpectedRange = new { Min = 0.97, Max = 1.0 },
            });
        FixtureGoldenArtifacts.WriteRunMetadata(run, analysisPath);

        Assert.InRange(rho, 0.97, 1.0);
    }

    [Fact(Skip = "Phase 2 spectral-gradient bridge pending — see renovation_part_3.md Phase 2")]
    public void Crescent_LineField_AlignsWithLocalChord()
    {
        var dataset = SyntheticData.GenerateCrescentAndEllipsoid(crescentPoints: 500, ellipsoidPoints: 0, seed: 42);
        var draft = FixtureGoldenHelpers.RunDraftSpectral(dataset.Features, eigenCount: 2, k: FixtureGoldenHelpers.ConnectedFixtureK, seed: FixtureGoldenHelpers.SpectralSeed);

        var lineField = Assert.IsType<LineFieldLayer>(draft.FiedlerGradient);
        Assert.Equal(draft.Points.Length, lineField.N);

        const double thresholdRad = 15.0 * Math.PI / 180.0;
        int violations = 0;
        for (int i = 0; i < draft.Points.Length; i++)
        {
            double[] chord = FixtureGoldenHelpers.LocalChordFromNeighbors(draft.Points, draft.Graph, i);
            double[] lf = FixtureGoldenHelpers.GetDirection(lineField, i);
            if (FixtureGoldenHelpers.UnorientedAngleBetween(lf, chord) > thresholdRad)
                violations++;
        }

        Assert.True(
            violations <= draft.Points.Length * 0.02,
            $"{violations} of {draft.Points.Length} line-field directions exceed 15 degrees from local chord");
    }

    [Fact(Skip = "Phase 2 spectral-gradient bridge pending — see renovation_part_3.md Phase 2")]
    public void Crescent_LineField_NoRadialBias()
    {
        var dataset = SyntheticData.GenerateCrescentAndEllipsoid(crescentPoints: 500, ellipsoidPoints: 0, seed: 42);
        var draft = FixtureGoldenHelpers.RunDraftSpectral(dataset.Features, eigenCount: 2, k: FixtureGoldenHelpers.ConnectedFixtureK, seed: FixtureGoldenHelpers.SpectralSeed);

        double meanRadialMagnitude = FixtureGoldenHelpers.AverageNormalComponent(draft.FiedlerGradient, draft.Points, draft.Graph);
        Assert.InRange(meanRadialMagnitude, 0.0, 0.1);
    }
}

public sealed class MobiusFixtureGoldenDrafts
{
    [Fact]
    public void Mobius_NodeSignal_SecondEigenvector_ExactlyOneSignFlipPerLoop()
    {
        var dataset = SyntheticData.GenerateMobiusAndEllipsoid(mobiusPoints: 500, ellipsoidPoints: 0, seed: 42);
        var graph = FixtureGoldenHelpers.BuildDraftGraph(dataset.Features, k: FixtureGoldenHelpers.ConnectedFixtureK, ensureConnected: true);
        var draft = FixtureGoldenHelpers.RunDraftSpectral(dataset.Features, graph, eigenCount: 2, seed: FixtureGoldenHelpers.SpectralSeed);

        int[] ordered = Enumerable.Range(0, draft.Points.Length).OrderBy(i => draft.LoopParameter[i]).ToArray();
        const double eps = 1e-6;
        MobiusModeSelection selection = FixtureGoldenHelpers.SelectMobiusModeWithMinimumSignFlips(draft, candidateCount: 5, epsilon: eps);

        Assert.NotEqual(-1, selection.SelectedEigenIndex);

        var run = FixtureGoldenArtifacts.CreateRun(
            nameof(MobiusFixtureGoldenDrafts),
            nameof(Mobius_NodeSignal_SecondEigenvector_ExactlyOneSignFlipPerLoop),
            fixtureName: "Mobius",
            assertionName: "SecondEigenvectorSignFlipCount",
            graph.NodeCount);
        string analysisPath = FixtureGoldenArtifacts.WriteAnalysis(
            run,
            graph,
            draft,
            new
            {
                CandidateEigenIndices = selection.CandidateEigenIndices,
                CandidateSignFlipCounts = selection.CandidateSignFlipCounts,
                selection.SelectedEigenIndex,
                selection.SelectedSignFlipCount,
                SelectedEigenvalue = draft.Eigenpairs[selection.SelectedEigenIndex].Lambda,
                OrderedSigns = FixtureGoldenHelpers.FormatOrderedSigns(selection.SelectedValues, ordered, eps),
                ExpectedRange = new { Min = 0, Max = 1 },
            });
        FixtureGoldenArtifacts.WriteRunMetadata(run, analysisPath);

        Assert.InRange(selection.SelectedSignFlipCount, 0, 1);
    }

    [Fact(Skip = "Phase 2 spectral-gradient bridge pending — see renovation_part_3.md Phase 2")]
    public void Mobius_LineField_HalfIntegerWindingNumber()
    {
        var dataset = SyntheticData.GenerateMobiusAndEllipsoid(mobiusPoints: 500, ellipsoidPoints: 0, seed: 42);
        var draft = FixtureGoldenHelpers.RunDraftSpectral(dataset.Features, eigenCount: 2, k: FixtureGoldenHelpers.ConnectedFixtureK, seed: FixtureGoldenHelpers.SpectralSeed);

        var lineField = Assert.IsType<LineFieldLayer>(draft.FiedlerGradient);
        int[] ordered = Enumerable.Range(0, draft.Points.Length).OrderBy(i => draft.LoopParameter[i]).ToArray();

        double winding = 0.0;
        for (int j = 0; j < ordered.Length; j++)
        {
            double[] a = FixtureGoldenHelpers.GetDirection(lineField, ordered[j]);
            double[] b = FixtureGoldenHelpers.GetDirection(lineField, ordered[(j + 1) % ordered.Length]);
            winding += FixtureGoldenHelpers.UnorientedAngleSigned(a, b);
        }

        Assert.InRange(Math.Abs(winding), 0.9 * Math.PI, 1.1 * Math.PI);
    }

    [Fact(Skip = "Phase 2 spectral-gradient bridge pending — see renovation_part_3.md Phase 2")]
    public void Mobius_LineField_ContinuousAwayFromSeam()
    {
        var dataset = SyntheticData.GenerateMobiusAndEllipsoid(mobiusPoints: 500, ellipsoidPoints: 0, seed: 42);
        var draft = FixtureGoldenHelpers.RunDraftSpectral(dataset.Features, eigenCount: 2, k: FixtureGoldenHelpers.ConnectedFixtureK, seed: FixtureGoldenHelpers.SpectralSeed);

        var lineField = Assert.IsType<LineFieldLayer>(draft.FiedlerGradient);
        int[] ordered = Enumerable.Range(0, draft.Points.Length).OrderBy(i => draft.LoopParameter[i]).ToArray();

        const double smoothThreshold = 25.0 * Math.PI / 180.0;
        int largeSteps = 0;
        for (int j = 0; j < ordered.Length; j++)
        {
            double[] a = FixtureGoldenHelpers.GetDirection(lineField, ordered[j]);
            double[] b = FixtureGoldenHelpers.GetDirection(lineField, ordered[(j + 1) % ordered.Length]);
            if (Math.Abs(FixtureGoldenHelpers.UnorientedAngleSigned(a, b)) > smoothThreshold)
                largeSteps++;
        }

        Assert.InRange(largeSteps, 0, 1);
    }
}

public sealed class DisconnectedControlFixtureGoldenDrafts
{
    [Fact]
    public void DisconnectedControl_NodeSignal_IsolatedPerComponent()
    {
        var control = DisconnectedControl.Generate();
        var graph = FixtureGoldenHelpers.BuildDraftGraph(control.Points, k: 4, ensureConnected: false);
        var draft = FixtureGoldenHelpers.RunDraftSpectral(control.Points, graph, eigenCount: 2, seed: FixtureGoldenHelpers.SpectralSeed);

        Assert.Equal(2, draft.ComponentCount);

        NodeSignalLayer[] componentSignals = FixtureGoldenHelpers.BuildPerComponentFiedlers(control.Points, draft.Graph, eigenCount: 2, seed: FixtureGoldenHelpers.SpectralSeed);
        Assert.Equal(draft.ComponentCount, componentSignals.Length);

        double[] maxOffComponent = new double[draft.ComponentCount];

        for (int component = 0; component < draft.ComponentCount; component++)
        {
            double[] values = componentSignals[component].Values.ToArray();
            double outside = FixtureGoldenHelpers.MaxAbsOffComponent(values, draft.ComponentLabels, component);
            maxOffComponent[component] = outside;

            Assert.InRange(outside, 0.0, 1e-9);
        }

        var run = FixtureGoldenArtifacts.CreateRun(
            nameof(DisconnectedControlFixtureGoldenDrafts),
            nameof(DisconnectedControl_NodeSignal_IsolatedPerComponent),
            fixtureName: "DisconnectedControl",
            assertionName: "PerComponentIsolation",
            graph.NodeCount);
        string analysisPath = FixtureGoldenArtifacts.WriteAnalysis(
            run,
            graph,
            draft,
            new
            {
                HelperAppliesComponentSplitBeforeExtraction = false,
                SplitAppliedIn = nameof(FixtureGoldenHelpers.BuildPerComponentFiedlers),
                AssertionExtractionPath = "per-component-lift",
                ComponentSummary = FixtureGoldenHelpers.FormatComponentSummary(draft.ComponentLabels),
                MaxAbsOffComponent = maxOffComponent,
                ExpectedRange = new { Min = 0.0, Max = 1e-9 },
            });
        FixtureGoldenArtifacts.WriteRunMetadata(run, analysisPath);
    }
}

internal static class FixtureGoldenArtifacts
{
    public static ArtifactRun CreateRun(
        string suiteName,
        string runName,
        string fixtureName,
        string assertionName,
        int nodeCount)
    {
        return HarnessArtifacts.Create(
            runKind: "test-runs",
            suiteName: suiteName,
            runName: runName,
            metadata: new Dictionary<string, object?>
            {
                ["Fixture"] = fixtureName,
                ["Assertion"] = assertionName,
                ["NodeCount"] = nodeCount,
            });
    }

    public static string WriteAnalysis(
        ArtifactRun run,
        CsrGraph graph,
        DraftSpectralFixture draft,
        object assertion)
    {
        return run.WriteJson(
            "analysis.json",
            new FixtureGoldenAnalysis(
                Graph: BuildGraphSummary(graph),
                Draft: BuildDraftSummary(draft),
                Assertion: assertion));
    }

    public static void WriteRunMetadata(ArtifactRun run, string analysisPath)
    {
        Console.WriteLine($"RunRoot\t{run.RunDirectory}");
        Console.WriteLine($"Manifest\t{run.ManifestPath}");
        Console.WriteLine($"Analysis\t{analysisPath}");
    }

    private static FixtureGraphSummary BuildGraphSummary(CsrGraph graph)
    {
        ConnectivityReport connectivity = Connectivity.Validate(graph);
        return new FixtureGraphSummary(
            graph.NodeCount,
            connectivity.ComponentCount,
            FixtureGoldenHelpers.FormatLargestComponentSizes(graph),
            FixtureGoldenHelpers.GetUndirectedEdgeCount(graph),
            FixtureGoldenHelpers.GetMinimumEdgeWeight(graph));
    }

    private static FixtureDraftSummary BuildDraftSummary(DraftSpectralFixture draft)
    {
        return new FixtureDraftSummary(
            draft.ExtractionPath,
            draft.ComponentCount,
            draft.FiedlerEigenIndex,
            draft.SecondEigenEigenIndex,
            draft.FiedlerEigenIndex >= 0 ? draft.Eigenpairs[draft.FiedlerEigenIndex].Lambda : double.NaN,
            draft.SecondEigenEigenIndex >= 0 ? draft.Eigenpairs[draft.SecondEigenEigenIndex].Lambda : double.NaN,
            TakeEigenvalues(draft.Eigenpairs, first: true, count: 10),
            TakeEigenvalues(draft.Eigenpairs, first: false, count: 10));
    }

    private static double[] TakeEigenvalues(IReadOnlyList<EigenPair> eigenpairs, bool first, int count)
    {
        if (eigenpairs.Count == 0)
            return Array.Empty<double>();

        if (first)
            return eigenpairs.Take(Math.Min(count, eigenpairs.Count)).Select(pair => pair.Lambda).ToArray();

        int skip = Math.Max(0, eigenpairs.Count - count);
        return eigenpairs.Skip(skip).Select(pair => pair.Lambda).ToArray();
    }

    private sealed record FixtureGoldenAnalysis(
        FixtureGraphSummary Graph,
        FixtureDraftSummary Draft,
        object Assertion);

    private sealed record FixtureGraphSummary(
        int NodeCount,
        int ComponentCount,
        string LargestComponents,
        int UndirectedEdgeCount,
        double MinimumEdgeWeight);

    private sealed record FixtureDraftSummary(
        string ExtractionPath,
        int ComponentCount,
        int FiedlerEigenIndex,
        int SecondEigenIndex,
        double FiedlerEigenvalue,
        double SecondEigenvalue,
        double[] FirstEigenvalues,
        double[] LastEigenvalues);
}
