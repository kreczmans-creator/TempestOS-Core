namespace Tempest.Calc.Mechanical;

/// <summary>
/// The Euler–Bernoulli geometry of a cantilever snap-fit beam (M8). A beam of length l with a tip load P has, at
/// t = x/l from the root, the bending moment M = P·l·(1 − t); the surface strain is ε(t) = M·(h/2)/(E·I) and the tip
/// deflection is y = ∫ M·m/(E·I) dx with the unit-load moment m = l·(1 − t). With a linear taper of the thickness
/// h(t) = h₀·(1 − a·t) (a = 1 − h_tip/h₀) or of the width b(t) = b₀·(1 − a·t), and the other dimension constant,
/// <c>y = K_y·ε_max·l²/h₀</c> with <c>K_y = 2·J/φ_max</c>, where φ(t) is the strain shape, φ_max its maximum and J the deflection
/// integral below. Constant section: φ = 1, J = 1/3, K_y = 2/3 (the 0.67 of the design guides is this value rounded).
/// <list type="bullet">
/// <item>Thickness taper: φ(t) = (1 − t)/(1 − a·t)², J = ∫₀¹ (1 − t)²/(1 − a·t)³ dt = [2r − r²/2 − ln r − 3/2]/a³ with r = h_tip/h₀. The strain is largest at the root when a ≤ 1/2 (r ≥ 1/2); for a deeper taper it peaks inside the beam at t* = (2a − 1)/a.</item>
/// <item>Width taper: φ(t) = (1 − t)/(1 − a·t) (largest at the root for every a), J = ∫₀¹ (1 − t)²/(1 − a·t) dt = [1/2 − 2r + 3r²/2 − r²·ln r]/a³ with r = b_tip/b₀.</item>
/// </list>
/// </summary>
public static class SnapFitGeometry
{
    /// <summary>Below this taper (a = 1 − r) the closed forms lose digits to cancellation, so the integral is taken numerically.</summary>
    private const double ClosedFormLimit = 0.02;

    /// <summary>The deflection factor of the constant section, K_y = 2/3.</summary>
    public const double ConstantKy = 2.0 / 3.0;

    /// <summary>J for a linear thickness taper with tip/root thickness ratio <paramref name="ratio"/> (0 &lt; ratio ≤ 1).</summary>
    public static double ThicknessJ(double ratio)
    {
        var a = 1 - ratio;
        return a < ClosedFormLimit
            ? Simpson(t => (1 - t) * (1 - t) / Math.Pow(1 - a * t, 3))
            : (2 * ratio - ratio * ratio / 2 - Math.Log(ratio) - 1.5) / (a * a * a);
    }

    /// <summary>J for a linear width taper with tip/root width ratio <paramref name="ratio"/> (0 &lt; ratio ≤ 1).</summary>
    public static double WidthJ(double ratio)
    {
        var a = 1 - ratio;
        return a < ClosedFormLimit
            ? Simpson(t => (1 - t) * (1 - t) / (1 - a * t))
            : (0.5 - 2 * ratio + 1.5 * ratio * ratio - ratio * ratio * Math.Log(ratio)) / (a * a * a);
    }

    /// <summary>
    /// The largest of the strain shape φ(t) = (1 − t)/(1 − a·t)² of a thickness taper and where it is (t = 0 at the root).
    /// </summary>
    public static (double PhiMax, double TCrit) ThicknessPhiMax(double ratio)
    {
        var a = 1 - ratio;

        if (a <= 0.5)
            return (1, 0);

        var t = (2 * a - 1) / a;
        return ((1 - t) / Math.Pow(1 - a * t, 2), t);
    }

    /// <summary>The deflection factor K_y of a thickness taper: y = K_y·ε_max·l²/h₀.</summary>
    public static double ThicknessKy(double ratio) => 2 * ThicknessJ(ratio) / ThicknessPhiMax(ratio).PhiMax;

    /// <summary>The deflection factor K_y of a width taper: y = K_y·ε_max·l²/h.</summary>
    public static double WidthKy(double ratio) => 2 * WidthJ(ratio);

    private static double Simpson(Func<double, double> f)
    {
        const int n = 2000;
        var h = 1.0 / n;
        var sum = f(0) + f(1);

        for (var i = 1; i < n; i++)
            sum += f(i * h) * (i % 2 == 1 ? 4 : 2);

        return sum * h / 3;
    }
}
