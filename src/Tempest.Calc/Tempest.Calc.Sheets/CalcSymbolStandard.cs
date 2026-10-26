using System.Text.RegularExpressions;
using Tempest.Calc.Engine.Units;

namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2, rows 10 to 19: the symbols a bare letter stands for, once, for every template. A symbol that is reserved means that quantity wherever
/// it appears (torque is <c>T</c>, time is <c>t</c>, temperature is <c>T_&lt;qualifier&gt;</c>, a volume is <c>V</c>); another quantity takes a
/// qualified symbol (<c>T_amb</c>, <c>t_f</c>, <c>mu_visc</c>). The list is the glossary's "Reserved symbols" table, and a contract test
/// holds every template to it (<c>docs/calcs/glossary.md</c>).
/// </summary>
public static class CalcSymbolStandard
{
    /// <summary>A bare symbol and the one kind of quantity it stands for.</summary>
    public sealed record Reserved(string Symbol, string Meaning, string Row, Func<Dimension, string, bool> Fits);

    private static readonly Dimension Resistivity = Dimension.Resistance * Dimension.L;

    private static bool Angle(Dimension d, string unit) => d.IsDimensionless && CalcUnitText.Pretty(unit) is "°" or "rad" or "rev";

    private static bool Plain(Dimension d, string unit) => d.IsDimensionless && unit.Length == 0;

    /// <summary>The reserved bare symbols (rows 11, 12, 14 to 17).</summary>
    public static IReadOnlyList<Reserved> Symbols { get; } =
    [
        new("T", "Torque or moment (a temperature is T_<qualifier>)", "12", (d, _) => d == Dimension.Torque),
        new("t", "Time (a thickness is t_<part>)", "12", (d, _) => d == Dimension.T),
        new("theta", "Angle (never a temperature)", "11", Angle),
        new("V", "Volume (a voltage is U, a shear force is V_Ed)", "14", (d, _) => d == Dimension.Volume),
        new("Q", "Volumetric flow (a heat flow is Phi)", "14", (d, _) => d == Dimension.VolumeFlow),
        new("P", "Power (a force is F)", "14", (d, _) => d == Dimension.Power),
        new("U", "Voltage", "14", (d, _) => d == Dimension.Voltage),
        new("F", "Force", "14", (d, _) => d == Dimension.Force),
        new("Phi", "Heat flow", "14", (d, _) => d == Dimension.Power),
        new("p", "Not used: a pressure is p_<qualifier> (p_atm, p_int, p_ext, p_pre)", "15", (_, _) => false),
        new("mu", "Friction coefficient (a viscosity is mu_visc)", "16", Plain),
        new("rho", "Density (a resistivity is rho_res)", "16", (d, _) => d == Dimension.Density),
        new("nu", "Poisson's ratio", "16", Plain),
        new("eta", "Efficiency (a factor is eta_<qualifier>)", "16", Plain),
        new("eps", "Effectiveness (a roughness is eps_rough)", "17", Plain),
        new("h", "Depth or height (a coefficient is h_conv)", "17", (d, _) => d == Dimension.L),
        new("n", "Rotational speed", "17", (d, _) => d == Dimension.Frequency),
        new("R", "Resistance (a reliability is R_rel)", "17", (d, _) => d == Dimension.Resistance),
        new("D", "A diameter or a depth", "17", (d, _) => d == Dimension.L),
        new("L", "Length", "17", (d, _) => d == Dimension.L),
    ];

    private static readonly Regex TemperatureSymbol = new(@"^(T|T_[A-Za-z0-9_,]+|dT|dT_[A-Za-z0-9_,]+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The spellings the audit retires in favour of one symbol per quantity (row 18), with the symbol to use.</summary>
    public static IReadOnlyDictionary<string, string> Retired { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["cosphi"] = "cos_phi",
        ["T_z"] = "M_z",
        ["V_R"] = "F_v_Rd",
        ["T_R"] = "F_t_Rd",
        ["v_max"] = "v_limit",
    };

    /// <summary>Whether a symbol of dimension <paramref name="dimension"/> and unit <paramref name="unit"/> is spelled as a temperature must be (row 10).</summary>
    public static bool IsTemperatureSpelling(string symbol) => TemperatureSymbol.IsMatch(symbol);

    /// <summary>The problems one symbol has against the standard: empty when it conforms. <paramref name="unit"/> is the unit it is shown in (empty for a plain number).</summary>
    public static IReadOnlyList<(string Row, string Problem)> Problems(string symbol, Dimension dimension, string unit)
    {
        var problems = new List<(string, string)>();

        if (dimension == Dimension.Theta && !IsTemperatureSpelling(symbol))
            problems.Add(("10", $"{symbol} is a temperature and is not written T or T_<qualifier>"));

        if (symbol.StartsWith("theta", StringComparison.Ordinal) && dimension == Dimension.Theta)
            problems.Add(("11", $"{symbol} is a temperature; theta is an angle only"));

        foreach (var reserved in Symbols)
        {
            if (string.Equals(reserved.Symbol, symbol, StringComparison.Ordinal) && !reserved.Fits(dimension, unit))
                problems.Add((reserved.Row, $"{symbol} is {Describe(dimension, unit)}; {symbol} means {reserved.Meaning}"));
        }

        if (Retired.TryGetValue(symbol, out var use))
            problems.Add(("18", $"{symbol} is retired; write {use}"));

        return problems;
    }

    private static string Describe(Dimension dimension, string unit) => unit.Length > 0 ? $"a quantity in {CalcUnitText.Pretty(unit)}" : dimension.IsDimensionless ? "a plain number" : $"a quantity of dimension {dimension}";
}
