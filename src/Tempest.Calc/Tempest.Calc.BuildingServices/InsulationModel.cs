using Tempest.Calc.Engine.Numerics;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// The physics of B9: steady heat flow through a layer of insulation on a pipe (cylinder) or a flat surface, the outer surface
/// temperature that follows from it, and the surface coefficient h_se either given or built from convection and radiation.
/// Closed-form physics only: no thickness table and no property table of any standard. Temperatures are in degrees Celsius,
/// lengths in metres; a pipe is per metre run (W/m, K·m/W), a flat surface per square metre (W/m², K·m²/W).
/// </summary>
public static class InsulationModel
{
    /// <summary>Stefan–Boltzmann constant, W/(m²·K⁴) (CODATA 2018).</summary>
    public const double StefanBoltzmann = 5.670374419e-8;

    /// <summary>The thickest layer searched for, m.</summary>
    public const double MaxThickness = 1.0;

    /// <summary>Points of the thickness grid the criteria are scanned on (the grid is finer near zero).</summary>
    public const int GridPoints = 400;

    /// <summary>Prandtl number of air, taken as constant over the film temperatures of buildings (0.71).</summary>
    public const double PrandtlAir = 0.71;

    /// <summary>
    /// How the outer surface exchanges heat with the air and surroundings: a fixed h_se, or convection (free, forced and mixed) plus
    /// radiation to surroundings at the ambient temperature.
    /// </summary>
    /// <param name="Fixed">h_se in W/(m²·K) as given, or <see langword="null"/> to use the correlation.</param>
    /// <param name="Emissivity">Total hemispherical emissivity of the outer surface (correlation).</param>
    /// <param name="Wind">Air speed over the surface, m/s (0 = still air).</param>
    /// <param name="Length">Length of the flat surface in the flow direction or its height, m (flat only).</param>
    public sealed record Surface(double? Fixed, double Emissivity, double Wind, double Length)
    {
        /// <summary>h_c and h_r in W/(m²·K) for a surface at <paramref name="tsC"/> in air at <paramref name="taC"/>, on a body of characteristic length <paramref name="d"/>.</summary>
        public (double Convection, double Radiation) Coefficients(double tsC, double taC, double d, bool pipe)
        {
            var dT = Math.Abs(tsC - taC);
            var ts = tsC + 273.15;
            var ta = taC + 273.15;
            var radiation = Emissivity * StefanBoltzmann * (ts * ts + ta * ta) * (ts + ta);
            var natural = pipe
                ? Math.Max(1.32 * Math.Pow(dT / d, 0.25), 1.24 * Math.Cbrt(dT))
                : Math.Max(1.42 * Math.Pow(dT / d, 0.25), 1.31 * Math.Cbrt(dT));

            if (Wind <= 0)
                return (natural, radiation);

            var film = (ts + ta) / 2;
            var k = AirConductivity(film);
            var re = Wind * d / AirKinematicViscosity(film);
            var nu = pipe
                ? 0.3 + 0.62 * Math.Sqrt(re) * Math.Cbrt(PrandtlAir) / Math.Pow(1 + Math.Pow(0.4 / PrandtlAir, 2.0 / 3), 0.25) * Math.Pow(1 + Math.Pow(re / 282_000, 5.0 / 8), 0.8)
                : re <= 5e5 ? 0.664 * Math.Sqrt(re) * Math.Cbrt(PrandtlAir) : (0.037 * Math.Pow(re, 0.8) - 871) * Math.Cbrt(PrandtlAir);
            var forced = nu * k / d;
            return (Math.Cbrt(natural * natural * natural + forced * forced * forced), radiation);
        }
    }

    /// <summary>Thermal conductivity of air in W/(m·K) at a film temperature in K: 0.0241·(T/273.15)^0.81 (derived fit, within 3 % over 250–400 K).</summary>
    public static double AirConductivity(double kelvin) => 0.0241 * Math.Pow(kelvin / 273.15, 0.81);

    /// <summary>Kinematic viscosity of air in m²/s at a film temperature in K, 1 atm: 1.33×10⁻⁵·(T/273.15)^1.7 (derived fit, within 3 % over 250–400 K).</summary>
    public static double AirKinematicViscosity(double kelvin) => 1.33e-5 * Math.Pow(kelvin / 273.15, 1.7);

    /// <summary>The fixed part of the problem: geometry, conductivity, temperatures, the other resistances and the surface.</summary>
    /// <param name="Pipe">True for a cylinder, false for a flat surface.</param>
    /// <param name="Di">Inner diameter of the insulation (pipe outer diameter), m (pipe).</param>
    /// <param name="Lambda">Thermal conductivity of the insulation, W/(m·K).</param>
    /// <param name="Ti">Process (fluid) temperature, °C.</param>
    /// <param name="Ta">Ambient air temperature, °C.</param>
    /// <param name="ROther">Resistance in series that is not the insulation or the outer surface (inner film, pipe wall): K·m/W for a pipe, K·m²/W flat.</param>
    public sealed record Setup(bool Pipe, double Di, double Lambda, double Ti, double Ta, double ROther, Surface Surface);

    /// <summary>The state at one insulation thickness. <see cref="Q"/> is positive from the process to the ambient (heat loss), negative for heat gain.</summary>
    public sealed record State(double Thickness, double De, double RIns, double Area, double Ts, double H, double Convection, double Radiation, double Q)
    {
        /// <summary>R_se, the outer surface resistance, K·m/W (pipe) or K·m²/W (flat).</summary>
        public double RSe => 1 / (H * Area);
    }

    /// <summary>The insulation resistance and the surface area per unit length (pipe) or per unit area (flat) at thickness <paramref name="t"/> m.</summary>
    public static (double De, double RIns, double Area) Geometry(Setup s, double t)
    {
        if (s.Pipe)
        {
            var de = s.Di + 2 * t;
            return (de, Math.Log(de / s.Di) / (2 * Math.PI * s.Lambda), Math.PI * de);
        }

        return (0, t / s.Lambda, 1);
    }

    /// <summary>Solves the heat balance at thickness <paramref name="t"/> m: the surface temperature, h_se and the heat flow.</summary>
    public static State Evaluate(Setup s, double t)
    {
        var (de, rIns, area) = Geometry(s, t);
        var rTot = s.ROther + rIns;
        var d = s.Pipe ? de : s.Surface.Length;
        var delta = s.Ti - s.Ta;

        if (s.Surface.Fixed is { } fixedH)
        {
            var q = delta / (rTot + 1 / (fixedH * area));
            return new State(t, de, rIns, area, s.Ta + q / (fixedH * area), fixedH, 0, 0, q);
        }

        // Bare, with no wall or film in series: the surface is the process temperature.
        if (rTot <= 0)
        {
            var (cb, rb) = s.Surface.Coefficients(s.Ti, s.Ta, d, s.Pipe);
            return new State(t, de, rIns, area, s.Ti, cb + rb, cb, rb, area * (cb + rb) * delta);
        }

        // q = (Ti − Ts)/R_tot = A·h(Ts)·(Ts − Ta): the left side falls and the right side rises with Ts, so there is one root between Ta and Ti.
        double Balance(double ts)
        {
            var (c, r) = s.Surface.Coefficients(ts, s.Ta, d, s.Pipe);
            return (s.Ti - ts) / rTot - area * (c + r) * (ts - s.Ta);
        }

        var lo = Math.Min(s.Ti, s.Ta);
        var hi = Math.Max(s.Ti, s.Ta);
        var surface = RootFinding.Bisect(Balance, lo, hi, 1e-13, 300);
        var (hc, hr) = s.Surface.Coefficients(surface, s.Ta, d, s.Pipe);
        return new State(t, de, rIns, area, surface, hc + hr, hc, hr, (s.Ti - surface) / rTot);
    }

    /// <summary>Thickness at grid point <paramref name="i"/>: m, quadratic so the grid is finer where thin layers matter.</summary>
    public static double GridThickness(int i)
    {
        var x = (double)i / GridPoints;
        return MaxThickness * x * x;
    }

    /// <summary>How a required thickness search ended.</summary>
    public enum SearchOutcome
    {
        /// <summary>A thickness was found (zero when the bare surface already complies and no thicker layer breaks it).</summary>
        Found,

        /// <summary>The criterion is still violated at the thickest layer searched.</summary>
        Unattainable,
    }

    /// <summary>
    /// The least thickness beyond which <paramref name="violation"/> (positive when the criterion is broken) is not positive for any
    /// thicker layer up to <see cref="MaxThickness"/>: the grid is scanned from the thick end for the last broken point and the root
    /// bracketed after it is found by bisection. Zero when the criterion holds everywhere on the grid.
    /// </summary>
    public static (SearchOutcome Outcome, double Thickness) RequiredThickness(Setup s, IReadOnlyList<State> grid, Func<State, double> violation)
    {
        var last = -1;

        for (var i = grid.Count - 1; i >= 0; i--)
        {
            if (violation(grid[i]) > 0)
            {
                last = i;
                break;
            }
        }

        if (last < 0)
            return (SearchOutcome.Found, 0);

        if (last == grid.Count - 1)
            return (SearchOutcome.Unattainable, MaxThickness);

        var t = RootFinding.Bisect(x => violation(Evaluate(s, x)), grid[last].Thickness, grid[last + 1].Thickness, 1e-13, 300);
        return (SearchOutcome.Found, t);
    }

    /// <summary>The states on the thickness grid, from bare to <see cref="MaxThickness"/>.</summary>
    public static List<State> Scan(Setup s)
    {
        var grid = new List<State>(GridPoints + 1);

        for (var i = 0; i <= GridPoints; i++)
            grid.Add(Evaluate(s, GridThickness(i)));

        return grid;
    }
}
