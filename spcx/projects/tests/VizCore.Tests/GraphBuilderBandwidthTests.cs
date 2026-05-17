using System;
using System.Collections.Generic;
using Graphs;
using Graphs.Primitives;
using Xunit;

namespace VizCore.Tests;

public sealed class GraphBuilderBandwidthTests
{
    [Fact]
    public void Build_EnsureConnected_PreservesWeightsOnExistingEdges()
    {
        DisconnectedControl.Fixture control = DisconnectedControl.Generate(pointsPerComponent: 8, separation: 100.0);
        double[][] points = control.Points;

        CsrGraph withoutRepair = GraphBuilder.Build(
            n: points.Length,
            dist: (i, j) => EuclideanDistance(points[i], points[j]),
            rule: ProximityRule.Knn,
            k: 1,
            kernel: KernelType.Gaussian,
            ensureConnected: false);

        CsrGraph withRepair = GraphBuilder.Build(
            n: points.Length,
            dist: (i, j) => EuclideanDistance(points[i], points[j]),
            rule: ProximityRule.Knn,
            k: 1,
            kernel: KernelType.Gaussian,
            ensureConnected: true);

        Dictionary<long, double> withoutWeights = BuildUndirectedWeightMap(withoutRepair);
        Dictionary<long, double> withWeights = BuildUndirectedWeightMap(withRepair);

        Assert.NotEmpty(withoutWeights);
        Assert.True(withWeights.Count > withoutWeights.Count, "Fixture must add at least one MST bridge edge.");

        foreach ((long edgeKey, double expectedWeight) in withoutWeights)
        {
            Assert.True(withWeights.TryGetValue(edgeKey, out double repairedWeight), $"Missing original edge {edgeKey} after EnsureConnected.");
            Assert.InRange(Math.Abs(repairedWeight - expectedWeight), 0.0, 1e-12);
        }
    }

    private static Dictionary<long, double> BuildUndirectedWeightMap(CsrGraph graph)
    {
        var map = new Dictionary<long, double>();

        for (int source = 0; source < graph.NodeCount; source++)
        {
            int rowStart = graph.RowPointers[source];
            int rowEnd = graph.RowPointers[source + 1];
            for (int edge = rowStart; edge < rowEnd; edge++)
            {
                int target = graph.Targets[edge];
                if (target <= source)
                    continue;

                long key = (((long)source) << 32) | (uint)target;
                map[key] = graph.Weights[edge];
            }
        }

        return map;
    }

    private static double EuclideanDistance(double[] left, double[] right)
    {
        double sum = 0.0;
        for (int i = 0; i < left.Length; i++)
        {
            double diff = left[i] - right[i];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }
}
