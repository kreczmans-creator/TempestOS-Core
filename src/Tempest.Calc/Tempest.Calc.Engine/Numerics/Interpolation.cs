namespace Tempest.Calc.Engine.Numerics;

/// <summary>Piecewise-linear interpolation on a table whose abscissae are strictly increasing. Outside the table the end value is held (flat) unless extrapolation is asked for.</summary>
public static class Interpolation
{
    public static double Linear(double x, IReadOnlyList<double> xs, IReadOnlyList<double> ys, bool extrapolate = false)
    {
        ArgumentNullException.ThrowIfNull(xs);
        ArgumentNullException.ThrowIfNull(ys);

        if (xs.Count < 2 || xs.Count != ys.Count)
            throw new CalcEvaluationException($"interp needs two tables of equal length with at least two points; got {xs.Count} and {ys.Count}.");

        for (var i = 1; i < xs.Count; i++)
        {
            if (!(xs[i] > xs[i - 1]))
                throw new CalcEvaluationException("interp needs strictly increasing x values.");
        }

        if (double.IsNaN(x))
            throw new CalcEvaluationException("interp cannot interpolate at an undefined x.");

        if (x <= xs[0])
            return extrapolate ? Segment(x, xs[0], xs[1], ys[0], ys[1]) : ys[0];

        if (x >= xs[^1])
            return extrapolate ? Segment(x, xs[^2], xs[^1], ys[^2], ys[^1]) : ys[^1];

        var lo = 0;
        var hi = xs.Count - 1;

        while (hi - lo > 1)
        {
            var mid = (lo + hi) / 2;

            if (xs[mid] <= x)
                lo = mid;
            else
                hi = mid;
        }

        return Segment(x, xs[lo], xs[hi], ys[lo], ys[hi]);
    }

    private static double Segment(double x, double x0, double x1, double y0, double y1) => y0 + (y1 - y0) * (x - x0) / (x1 - x0);
}
