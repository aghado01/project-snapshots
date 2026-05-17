using System;
using Graphs.Distance.Euclidean;
using Graphs.Distance.Geodesic;
using Viz;

internal static class DistanceFactory
{
    public static Func<int, int, double> Create(double[] features, int d, VizMetric metric)
    {
        ArgumentNullException.ThrowIfNull(features);
        if (d < 1) throw new ArgumentOutOfRangeException(nameof(d));

        return metric switch
        {
            VizMetric.Euclidean => (i, j) => Euclidean.Distance(RowOf(features, d, i), RowOf(features, d, j)),
            VizMetric.Manhattan => (i, j) => Manhattan.Distance(RowOf(features, d, i), RowOf(features, d, j)),
            VizMetric.Cosine => (i, j) => Cosine.Distance(RowOf(features, d, i), RowOf(features, d, j)),
            VizMetric.Poincare => (i, j) => Poincare.Distance(RowOf(features, d, i), RowOf(features, d, j)),
            VizMetric.FisherRaoSimplex => (i, j) => FisherRaoSimplex.Distance(RowOf(features, d, i), RowOf(features, d, j)),
            VizMetric.FisherRaoHalfPlane => (i, j) => FisherRaoHalfPlane.Distance(RowOf(features, d, i), RowOf(features, d, j)),
            _ => throw new NotSupportedException($"Metric {metric} is not yet wired into graph binding.")
        };
    }

    private static double[] RowOf(double[] features, int d, int row)
    {
        var result = new double[d];
        Array.Copy(features, row * d, result, 0, d);
        return result;
    }
}
