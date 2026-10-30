namespace Tempest.Calc.BuildingServices.Fluids;

/// <summary>
/// Liquid water properties at atmospheric pressure, 0–100 °C, from
/// published correlations: density by Kell (1975), dynamic viscosity by a
/// Vogel-type fit, saturation vapour pressure by Hyland &amp; Wexler (1983). Each is a formula, not a table.
/// </summary>
public static class Water
{
    public const double MinCelsius = 0;
    public const double MaxCelsius = 100;

    /// <summary>Density in kg/m³ (Kell 1975, J. Chem. Eng. Data 20(1)).</summary>
    public static double Density(double celsius)
    {
        var t = celsius;
        return (999.83952 + 16.945176 * t - 7.9870401e-3 * t * t - 46.170461e-6 * t * t * t + 105.56302e-9 * Math.Pow(t, 4) - 280.54253e-12 * Math.Pow(t, 5)) / (1 + 16.879850e-3 * t);
    }

    /// <summary>Dynamic viscosity in Pa·s (Vogel form μ = A·exp(B/(T − C)), A = 0.02939 mPa·s, B = 507.88 K, C = 149.3 K; within ~1 % of IAPWS over 0–100 °C).</summary>
    public static double Viscosity(double celsius) => 0.02939e-3 * Math.Exp(507.88 / (celsius + 273.15 - 149.3));

    /// <summary>Saturation vapour pressure over liquid water in Pa (Hyland &amp; Wexler 1983, as given in ASHRAE Fundamentals Ch. 1; 0–200 °C).</summary>
    public static double VapourPressure(double celsius)
    {
        var t = celsius + 273.15;
        return Math.Exp(-5.8002206e3 / t + 1.3914993 - 4.8640239e-2 * t + 4.1764768e-5 * t * t - 1.4452093e-8 * t * t * t + 6.5459673 * Math.Log(t));
    }

    /// <summary>Saturation vapour pressure over ice in Pa (Hyland &amp; Wexler 1983; −100–0 °C).</summary>
    public static double VapourPressureOverIce(double celsius)
    {
        var t = celsius + 273.15;
        return Math.Exp(-5.6745359e3 / t + 6.3925247 - 9.677843e-3 * t + 6.2215701e-7 * t * t + 2.0747825e-9 * t * t * t - 9.484024e-13 * Math.Pow(t, 4) + 4.1635019 * Math.Log(t));
    }
}

/// <summary>Darcy friction factor correlations.</summary>
public static class Friction
{
    public const double LaminarLimit = 2300;
    public const double TurbulentLimit = 4000;
    public const int ColebrookIterations = 100;

    public static double Laminar(double re) => 64 / re;

    /// <summary>Colebrook–White by fixed-point iteration on 1/√f from the Swamee–Jain start; returns the iteration count used.</summary>
    public static (double F, int Iterations) Colebrook(double re, double relativeRoughness)
    {
        var x = 1 / Math.Sqrt(SwameeJain(re, relativeRoughness));

        for (var i = 1; i <= ColebrookIterations; i++)
        {
            var next = -2 * Math.Log10(relativeRoughness / 3.7 + 2.51 * x / re);

            if (Math.Abs(next - x) <= 1e-12 * Math.Abs(next))
                return (1 / (next * next), i);

            x = next;
        }

        return (1 / (x * x), ColebrookIterations);
    }

    /// <summary>Swamee–Jain explicit approximation (valid 5e3 ≤ Re ≤ 1e8, 1e-6 ≤ ε/D ≤ 1e-2).</summary>
    public static double SwameeJain(double re, double relativeRoughness) =>
        0.25 / Math.Pow(Math.Log10(relativeRoughness / 3.7 + 5.74 / Math.Pow(re, 0.9)), 2);
}
