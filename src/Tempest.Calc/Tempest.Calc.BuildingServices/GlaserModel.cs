namespace Tempest.Calc.BuildingServices;

/// <summary>One layer of a wall for the Glaser calculation: thickness d (m), conductivity λ (W/m·K) and vapour resistance factor μ.</summary>
public sealed record GlaserLayer(double D, double Lambda, double Mu)
{
    /// <summary>Thermal resistance d/λ, m²K/W.</summary>
    public double R => D / Lambda;

    /// <summary>Water-vapour-diffusion-equivalent air layer thickness s_d = μ·d, m.</summary>
    public double Sd => Mu * D;
}

/// <summary>What happened in one month at every node of the wall (node 0 is the inside surface, node N the outside surface; nodes 1…N−1 are the layer interfaces).</summary>
/// <param name="ThetaE">Outside temperature of the month, °C.</param>
/// <param name="Theta">Temperature at each node, °C.</param>
/// <param name="Psat">Saturation vapour pressure at each node, Pa.</param>
/// <param name="P">Vapour pressure at each node after the tangent construction, Pa.</param>
/// <param name="Flux">Net moisture arriving at each node, kg/(m²·s): positive condenses, negative evaporates (zero at nodes that are neither a condensation plane nor wet).</param>
/// <param name="Mc">Condensate on each node at the end of the month, kg/m².</param>
/// <param name="Wet">Whether the node held condensate at the start of the month (so it was held at saturation).</param>
public sealed record GlaserMonth(double ThetaE, IReadOnlyList<double> Theta, IReadOnlyList<double> Psat, IReadOnlyList<double> P, IReadOnlyList<double> Flux, IReadOnlyList<double> Mc, IReadOnlyList<bool> Wet)
{
    /// <summary>Total condensate on the wall at the end of the month, kg/m².</summary>
    public double McTotal => Mc.Sum();
}

/// <summary>
/// A year of interstitial condensation (Glaser method, monthly steps) in the form ISO 13788 describes: months in the order of the
/// climate, <see cref="StartIndex"/> the month the accumulation starts in (−1 when nothing condenses).
/// </summary>
public sealed record GlaserYear(IReadOnlyList<GlaserMonth> Months, int StartIndex)
{
    /// <summary>The condensate left on the wall at the end of the 12-month cycle, kg/m² (zero when it all dried out).</summary>
    public double ResidualMc => StartIndex < 0 ? 0 : Months[(StartIndex + Months.Count - 1) % Months.Count].McTotal;

    /// <summary>The most condensate held on any one interface in any month, kg/m².</summary>
    public double PeakInterfaceMc => Months.Count == 0 ? 0 : Months.Max(m => m.Mc.Max());

    /// <summary>The most condensate held on one interface over the year, per node, kg/m².</summary>
    public IReadOnlyList<double> PeakPerNode => Months.Count == 0 ? [] : [.. Enumerable.Range(0, Months[0].Mc.Count).Select(k => Months.Max(m => m.Mc[k]))];
}

/// <summary>
/// The physics of the Glaser calculation: Magnus saturation pressure over water and ice, the series-resistance temperature at every
/// layer interface, the vapour-pressure profile by the tangent construction, and the monthly accumulation carried forward over the year.
/// Closed-form physics and constants only: no climate or material data of any standard.
/// </summary>
public static class GlaserModel
{
    /// <summary>Water-vapour permeability of still air δ0, kg/(m·s·Pa) (the brief's 2×10⁻¹⁰).</summary>
    public const double Delta0 = 2e-10;

    /// <summary>Specific gas constant of water vapour, J/(kg·K): R/M_w = 8.3145/0.018015, rounded (derived).</summary>
    public const double VapourGasConstant = 461.5;

    /// <summary>The month length used where the climate gives none: 365/12 days in seconds.</summary>
    public const double AverageMonthSeconds = 365.0 / 12.0 * 86_400.0;

    /// <summary>The most layers a wall may have.</summary>
    public const int MaxLayers = 30;

    /// <summary>Net moisture flux below which a node is not counted as condensing, kg/(m²·s) (about 2.6e-6 g/m² per month).</summary>
    private const double FluxFloor = 1e-15;

    /// <summary>Condensate below this is taken as dried out, kg/m² (1e-9 g/m²).</summary>
    private const double DryFloor = 1e-12;

    /// <summary>Saturation vapour pressure in Pa at θ °C by the Magnus form: water at and above 0 °C, ice below.</summary>
    public static double SaturationPressure(double celsius) =>
        celsius >= 0
            ? 610.5 * Math.Exp(17.269 * celsius / (237.3 + celsius))
            : 610.5 * Math.Exp(21.875 * celsius / (265.5 + celsius));

    /// <summary>The temperature in °C at which a vapour pressure in Pa is saturated: the inverse of <see cref="SaturationPressure"/> (clamped to 1 Pa at the low end).</summary>
    public static double DewPoint(double pressure)
    {
        var x = Math.Log(Math.Max(pressure, 1.0) / 610.5);
        return x >= 0 ? 237.3 * x / (17.269 - x) : 265.5 * x / (21.875 - x);
    }

    /// <summary>Total thermal resistance R_si + Σ d/λ + R_se, m²K/W.</summary>
    public static double TotalResistance(IReadOnlyList<GlaserLayer> layers, double rsi, double rse) => rsi + layers.Sum(l => l.R) + rse;

    /// <summary>Temperature at every node (inside surface, each interface, outside surface) for the inside and outside air temperatures, °C.</summary>
    public static double[] NodeTemperatures(IReadOnlyList<GlaserLayer> layers, double rsi, double rse, double thetaInt, double thetaE)
    {
        var rt = TotalResistance(layers, rsi, rse);
        var theta = new double[layers.Count + 1];
        var r = rsi;
        theta[0] = thetaInt - (thetaInt - thetaE) * r / rt;

        for (var k = 0; k < layers.Count; k++)
        {
            r += layers[k].R;
            theta[k + 1] = thetaInt - (thetaInt - thetaE) * r / rt;
        }

        return theta;
    }

    /// <summary>Cumulative diffusion-equivalent air layer thickness at every node, m (zero at the inside surface).</summary>
    public static double[] CumulativeSd(IReadOnlyList<GlaserLayer> layers)
    {
        var s = new double[layers.Count + 1];

        for (var k = 0; k < layers.Count; k++)
            s[k + 1] = s[k] + layers[k].Sd;

        return s;
    }

    /// <summary>The vapour pressure in Pa of air at a temperature in °C and a relative humidity (a fraction, 0…1).</summary>
    public static double VapourPressure(double celsius, double relativeHumidity) => relativeHumidity * SaturationPressure(celsius);

    /// <summary>
    /// The vapour-pressure profile that follows the diffusion line from the inside to the outside air pressure but never rises above
    /// the saturation line: the tangent construction. Between the contact points it is straight; the contact points are the
    /// interfaces where it touches saturation (condensation planes), plus every interface in <paramref name="pinned"/> (wet, held at saturation).
    /// <paramref name="touched"/> is set at the interior nodes the profile rests on.
    /// </summary>
    public static double[] Profile(IReadOnlyList<double> s, IReadOnlyList<double> psat, double pInt, double pExt, IReadOnlyList<bool> pinned, bool[] touched)
    {
        var n = s.Count - 1;
        var p = new double[n + 1];
        p[0] = pInt;
        p[n] = pExt;
        var a = 0;
        var pa = pInt;

        double Value(int j) => j == n ? pExt : psat[j];

        while (a < n)
        {
            var b = n;

            for (var k = a + 1; k < n; k++)
            {
                if (pinned[k])
                {
                    b = k;
                    break;
                }
            }

            // The next contact point is the one the line from here to it leaves every saturation point in between on or above it: the smallest slope.
            var best = -1;
            var bestSlope = 0.0;

            for (var j = a + 1; j <= b; j++)
            {
                var slope = (Value(j) - pa) / (s[j] - s[a]);

                if (best < 0 || slope <= bestSlope + 1e-12 * Math.Max(1.0, Math.Abs(bestSlope)))
                {
                    best = j;
                    bestSlope = slope;
                }
            }

            for (var k = a + 1; k <= best; k++)
                p[k] = pa + bestSlope * (s[k] - s[a]);

            if (best < n)
            {
                p[best] = psat[best];
                touched[best] = true;
            }

            pa = Value(best);
            a = best;
        }

        return p;
    }

    /// <summary>
    /// Net moisture arriving at each node in kg/(m²·s): the flux into the node through the layer inside it minus the flux out through the layer
    /// outside it, g = δ0·Δp/Δs_d. Positive condenses. Zero at a node that is neither a contact point nor wet; surfaces are zero.
    /// </summary>
    public static double[] NodeFlux(IReadOnlyList<double> s, IReadOnlyList<double> p, IReadOnlyList<bool> touched, IReadOnlyList<bool> pinned)
    {
        var n = s.Count - 1;
        var flux = new double[n + 1];

        for (var k = 1; k < n; k++)
        {
            if (!touched[k] && !pinned[k])
                continue;

            var into = Delta0 * (p[k - 1] - p[k]) / (s[k] - s[k - 1]);
            var outOf = Delta0 * (p[k] - p[k + 1]) / (s[k + 1] - s[k]);
            flux[k] = into - outOf;
        }

        return flux;
    }

    /// <summary>
    /// A year of condensation. The arrays are per month in the order of the climate: outside temperature °C, the inside and outside vapour
    /// pressures Pa, and the month length s. Accumulation starts in the first month of condensation (the first month that condenses
    /// after a month that does not; month 1 where every month condenses) and runs for twelve months carrying the condensate forward;
    /// a wet interface is held at saturation and dries at the month's rate until nothing is left. The 12-month length is the number of months given.
    /// </summary>
    public static GlaserYear Simulate(IReadOnlyList<GlaserLayer> layers, double rsi, double rse, double thetaInt, IReadOnlyList<double> thetaE, IReadOnlyList<double> pInt, IReadOnlyList<double> pExt, IReadOnlyList<double> seconds)
    {
        var n = layers.Count;
        var months = thetaE.Count;
        var s = CumulativeSd(layers);
        var theta = new double[months][];
        var psat = new double[months][];
        var condenses = new bool[months];
        var none = new bool[n + 1];

        for (var m = 0; m < months; m++)
        {
            theta[m] = NodeTemperatures(layers, rsi, rse, thetaInt, thetaE[m]);
            psat[m] = [.. theta[m].Select(SaturationPressure)];
            var touched = new bool[n + 1];
            var p = Profile(s, psat[m], pInt[m], pExt[m], none, touched);
            var flux = NodeFlux(s, p, touched, none);
            condenses[m] = flux.Any(f => f > FluxFloor);
        }

        var results = new GlaserMonth?[months];

        if (!condenses.Any(x => x))
        {
            for (var m = 0; m < months; m++)
            {
                var touched = new bool[n + 1];
                var p = Profile(s, psat[m], pInt[m], pExt[m], none, touched);
                results[m] = new GlaserMonth(thetaE[m], theta[m], psat[m], p, new double[n + 1], new double[n + 1], none);
            }

            return new GlaserYear(results!, -1);
        }

        var start = 0;

        for (var m = 0; m < months; m++)
        {
            if (condenses[m] && !condenses[(m + months - 1) % months])
            {
                start = m;
                break;
            }
        }

        var mc = new double[n + 1];

        for (var i = 0; i < months; i++)
        {
            var m = (start + i) % months;
            var wet = new bool[n + 1];

            for (var k = 1; k < n; k++)
                wet[k] = mc[k] > 0;

            var touched = new bool[n + 1];
            var p = Profile(s, psat[m], pInt[m], pExt[m], wet, touched);
            var flux = NodeFlux(s, p, touched, wet);

            for (var k = 1; k < n; k++)
            {
                if (!wet[k] && !(flux[k] > FluxFloor))
                {
                    flux[k] = 0;
                    continue;
                }

                mc[k] = Math.Max(0.0, mc[k] + flux[k] * seconds[m]);

                if (mc[k] < DryFloor)
                    mc[k] = 0;
            }

            results[m] = new GlaserMonth(thetaE[m], theta[m], psat[m], p, flux, [.. mc], wet);
        }

        return new GlaserYear(results!, start);
    }
}
