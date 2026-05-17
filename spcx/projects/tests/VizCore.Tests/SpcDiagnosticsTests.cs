using System;
using System.Collections.Generic;
using Clustering.SPC;
using Clustering.SPC.Diagnostics;
using Clustering.SPC.Heuristics;
using Clustering.SPC.Potts;
using Graphs.Primitives;
using Maths.Information;
using Xunit;

namespace VizCore.Tests;

public sealed class SpcDiagnosticsTests
{
    [Fact]
    public void Shannon_EntropyNats_Counts_DegenerateAndUniformCasesBehaveAsExpected()
    {
        Assert.Equal(0.0, Shannon.EntropyNats(new int[] { 0, 0, 0 }));
        Assert.Equal(0.0, Shannon.EntropyNats(new int[] { 5, 0, 0 }));
        Assert.InRange(Math.Abs(Shannon.EntropyNats(new int[] { 1, 1, 1, 1 }) - Math.Log(4.0)), 0.0, 1e-12);
    }

    [Fact]
    public void Shannon_EntropyNats_Counts_MatchesHandComputedSkewedDistribution()
    {
        double expected = -(0.99 * Math.Log(0.99) + 0.01 * Math.Log(0.01));

        Assert.InRange(Math.Abs(Shannon.EntropyNats(new int[] { 99, 1 }) - expected), 0.0, 1e-12);
    }

    [Fact]
    public void BondFrequency_EntropyNats_ComputesOverNormalizedPerEdgeFrequencies()
    {
        var edges = new PottsModelEdgeObservables
        {
            Temperature = 1.0,
            CycleCount = 4,
            BondFormedCount = new[] { 2, 0, 2, 0 },
            SpinAgreementCount = new[] { 0, 0, 0, 0 },
        };

        double entropy = BondFrequency.EntropyNats(edges);

        Assert.InRange(Math.Abs(entropy - Math.Log(2.0)), 0.0, 1e-12);
    }

    [Fact]
    public void Susceptibility_From_GroupsReplicasAndSortsTemperatures()
    {
        var frames = new List<PottsModelStepResult>
        {
            CreateFrame(temperature: 2.0, cycleCount: 5, nodeCount: 3, sumSqClusterSizes: 30.0),
            CreateFrame(temperature: 1.0, cycleCount: 4, nodeCount: 3, sumSqClusterSizes: 12.0),
            CreateFrame(temperature: 1.0, cycleCount: 4, nodeCount: 3, sumSqClusterSizes: 24.0),
        };

        SusceptibilityCurve curve = Susceptibility.From(frames);

        Assert.Equal(new[] { 1.0, 2.0 }, curve.Temperatures);
        Assert.InRange(Math.Abs(curve.Chi[0] - 1.5), 0.0, 1e-12);
        Assert.InRange(Math.Abs(curve.Chi[1] - 2.0), 0.0, 1e-12);
    }

    [Fact]
    public void LabelEntropy_From_UsesClusterSizeHistogramPerTemperature()
    {
        var frames = new List<PottsModelStepResult>
        {
            CreateFrame(temperature: 2.0, clusterSizeHistogram: new[] { 1, 0, 0, 0 }),
            CreateFrame(temperature: 1.0, clusterSizeHistogram: new[] { 2, 0, 0, 0 }),
            CreateFrame(temperature: 1.0, clusterSizeHistogram: new[] { 0, 2, 0, 0 }),
        };

        LabelEntropyCurve curve = LabelEntropy.From(frames);

        Assert.Equal(new[] { 1.0, 2.0 }, curve.Temperatures);
        Assert.InRange(Math.Abs(curve.Entropy[0] - Math.Log(2.0)), 0.0, 1e-12);
        Assert.Equal(0.0, curve.Entropy[1]);
    }

    [Fact]
    public void SpecificHeat_From_ComputesBetaSquaredEnergyVariance()
    {
        var frames = new List<PottsModelStepResult>
        {
            CreateFrame(temperature: 2.0, cycleCount: 3, runningSumEnergy: 6.0, runningSumEnergySq: 15.0),
            CreateFrame(temperature: 2.0, cycleCount: 3, runningSumEnergy: 6.0, runningSumEnergySq: 15.0),
        };

        SpecificHeatCurve curve = SpecificHeat.From(frames);

        Assert.Equal(new[] { 2.0 }, curve.Temperatures);
        Assert.InRange(Math.Abs(curve.Cv[0] - 0.25), 0.0, 1e-12);
    }

    [Fact]
    public void CriticalTemperatureEstimator_Estimate_MatchesUniformWeightSanityCheck()
    {
        CsrGraph graph = BuildGraph(3,
            (0, 1, 1.0),
            (1, 2, 1.0),
            (0, 2, 1.0));

        double estimate = CriticalTemperatureEstimator.Estimate(graph, q: 4);

        Assert.InRange(Math.Abs(estimate - (2.0 / Math.Log(4.0))), 0.0, 1e-12);
    }

    [Fact]
    public void AdaptiveScheduler_Run_DefaultBracket_ChoosesTemperatureInsideEstimatedBracket()
    {
        CsrGraph graph = BuildGraph(4,
            (0, 1, 1.0),
            (0, 2, 1.0),
            (0, 3, 1.0),
            (1, 2, 1.0),
            (1, 3, 1.0),
            (2, 3, 1.0));

        var config = new AdaptiveSchedulerConfig
        {
            Q = 4,
            CoarseSteps = 4,
            CoarseBurn = 2,
            CoarseCycles = 4,
            DenseSteps = 4,
            DenseBurn = 2,
            DenseCycles = 4,
            DenseReplicas = 1,
            DenseRounds = 1,
            FinalBurn = 2,
            FinalCycles = 5,
            EarlyStopStability = 2.0,
            BaseSeed = 12345,
        };

        (double tempMin, double tempMax) = CriticalTemperatureEstimator.EstimateBracket(
            graph,
            config.Q,
            config.ColdEndOvershoot,
            config.HotEndOvershoot);

        AdaptiveResult result = AdaptiveScheduler.Run(graph, config);

        Assert.True(double.IsFinite(result.Diagnostics.ChosenTemperature));
        Assert.InRange(result.Diagnostics.ChosenTemperature, tempMin - 1e-12, tempMax + 1e-12);
        Assert.NotNull(result.EdgeObservables);
    }

    [Fact]
    public void AdaptiveScheduler_Run_ExplicitTempBoundsOverrideEstimatorBracket()
    {
        CsrGraph graph = BuildGraph(4,
            (0, 1, 1.0),
            (0, 2, 1.0),
            (0, 3, 1.0),
            (1, 2, 1.0),
            (1, 3, 1.0),
            (2, 3, 1.0));

        var config = new AdaptiveSchedulerConfig
        {
            Q = 4,
            TempMin = 0.3,
            TempMax = 0.4,
            CoarseSteps = 4,
            CoarseBurn = 2,
            CoarseCycles = 4,
            DenseSteps = 4,
            DenseBurn = 2,
            DenseCycles = 4,
            DenseReplicas = 1,
            DenseRounds = 1,
            FinalBurn = 2,
            FinalCycles = 5,
            EarlyStopStability = 2.0,
            BaseSeed = 54321,
        };

        AdaptiveResult result = AdaptiveScheduler.Run(graph, config);

        Assert.True(double.IsFinite(result.Diagnostics.ChosenTemperature));
        Assert.InRange(result.Diagnostics.ChosenTemperature, 0.3, 0.4);
        Assert.NotNull(result.EdgeObservables);
    }

    private static PottsModelStepResult CreateFrame(
        double temperature,
        int cycleCount = 4,
        int nodeCount = 4,
        double sumSqClusterSizes = 0.0,
        int[]? clusterSizeHistogram = null,
        double runningSumEnergy = 0.0,
        double runningSumEnergySq = 0.0)
    {
        return new PottsModelStepResult
        {
            Temperature = temperature,
            Q = 4,
            Susceptibility = PottsSusceptibility.FkOnly,
            CycleCount = cycleCount,
            Spins = new int[nodeCount],
            ClusterSizeHistogram = clusterSizeHistogram ?? new int[nodeCount],
            RngState0 = 1,
            RngState1 = 2,
            RngState2 = 3,
            RngState3 = 4,
            RunningSumSqClusterSizes = sumSqClusterSizes,
            RunningSumSqClusterSizesExcl = 0.0,
            RunningSumEnergy = runningSumEnergy,
            RunningSumEnergySq = runningSumEnergySq,
        };
    }

    private static CsrGraph BuildGraph(int nodeCount, params (int Source, int Target, double Weight)[] edges)
    {
        var graphEdges = new Edge[edges.Length];
        for (int i = 0; i < edges.Length; i++)
            graphEdges[i] = new Edge(edges[i].Source, edges[i].Target, edges[i].Weight);

        return CsrGraph.FromEdges(graphEdges, nodeCount);
    }
}
