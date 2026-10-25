using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;

namespace Tempest.Calc.Import.Cad;

/// <summary>
/// Unit text from a CAD file, resolved through the engine's own unit catalogue. CAD systems spell units their own way
/// (NX writes <c>MilliMeter</c>, <c>Degrees</c>); a small alias table maps those to engine symbols, and everything else
/// must parse as an engine unit exactly. A unit the engine does not know is reported, never guessed.
/// </summary>
public static class CadUnits
{
    /// <summary>The longest unit text read from a file.</summary>
    public const int MaxChars = 32;

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["millimeter"] = "mm", ["millimeters"] = "mm", ["millimetre"] = "mm", ["millimetres"] = "mm", ["milimeter"] = "mm",
        ["centimeter"] = "cm", ["centimeters"] = "cm", ["centimetre"] = "cm", ["centimetres"] = "cm",
        ["meter"] = "m", ["meters"] = "m", ["metre"] = "m", ["metres"] = "m",
        ["kilometer"] = "km", ["kilometre"] = "km",
        ["micron"] = "µm", ["microns"] = "µm", ["micrometer"] = "µm", ["micrometre"] = "µm",
        ["inch"] = "in", ["inches"] = "in", ["foot"] = "ft", ["feet"] = "ft", ["yard"] = "yd", ["mil"] = "mil", ["thou"] = "thou",
        ["degree"] = "deg", ["degrees"] = "deg", ["radian"] = "rad", ["radians"] = "rad",
        ["newton"] = "N", ["newtons"] = "N", ["kilonewton"] = "kN", ["poundforce"] = "lbf", ["poundsforce"] = "lbf",
        ["kilogram"] = "kg", ["kilograms"] = "kg", ["gram"] = "g", ["grams"] = "g", ["pound"] = "lb", ["pounds"] = "lb", ["poundmass"] = "lb",
        ["second"] = "s", ["seconds"] = "s", ["millisecond"] = "ms", ["minute"] = "min", ["minutes"] = "min", ["hour"] = "h", ["hours"] = "h",
        ["kelvin"] = "K", ["celsius"] = "degC", ["degreescelsius"] = "degC", ["degreecelsius"] = "degC", ["fahrenheit"] = "degF", ["degreesfahrenheit"] = "degF",
        ["pascal"] = "Pa", ["pascals"] = "Pa", ["kilopascal"] = "kPa", ["megapascal"] = "MPa", ["gigapascal"] = "GPa",
        ["watt"] = "W", ["watts"] = "W", ["kilowatt"] = "kW", ["horsepower"] = "hp",
        ["joule"] = "J", ["joules"] = "J", ["hertz"] = "Hz", ["volt"] = "V", ["volts"] = "V", ["ampere"] = "A", ["amp"] = "A", ["amps"] = "A", ["ohm"] = "ohm", ["ohms"] = "ohm",
        ["liter"] = "L", ["litre"] = "L", ["liters"] = "L", ["litres"] = "L", ["percent"] = "%",
        ["squaremillimeter"] = "mm^2", ["squaremeter"] = "m^2", ["cubicmillimeter"] = "mm^3", ["cubicmeter"] = "m^3",
    };

    private static readonly HashSet<string> Unitless = new(StringComparer.OrdinalIgnoreCase) { "constant", "number", "unitless", "none", "count", "-", "1" };

    private static readonly HashSet<string> AngleSymbols = new(StringComparer.Ordinal) { "deg", "°", "rad", "mrad", "urad", "µrad", "rev" };

    /// <summary>
    /// Resolves unit text (brackets and surrounding space are ignored). <paramref name="symbol"/> is the engine unit symbol
    /// to store; it is empty for an explicit "unitless" word. Returns <see langword="false"/> for text the engine does not know.
    /// </summary>
    public static bool TryResolve(string? text, out string symbol, out Unit unit)
    {
        symbol = string.Empty;
        unit = Unit.One;
        var t = (text ?? string.Empty).Trim().Trim('[', ']').Trim();

        if (t.Length == 0 || t.Length > MaxChars)
            return false;

        if (Unitless.Contains(t))
            return true;

        if (TryParseEngine(t, out var exact))
        {
            symbol = t;
            unit = exact;
            return true;
        }

        if (Aliases.TryGetValue(t, out var alias) && TryParseEngine(alias, out var aliased))
        {
            symbol = alias;
            unit = aliased;
            return true;
        }

        return false;
    }

    /// <summary>Whether the unit is a plane-angle unit (dimensionless to the engine, but not interchangeable with a ratio or a count).</summary>
    public static bool IsAngle(string? symbol) => !string.IsNullOrEmpty(symbol) && AngleSymbols.Contains(symbol);

    /// <summary>
    /// Whether the unit text has an angle in it (rpm, rev, rad, deg, grad, including SI-prefixed radians). The engine gives rpm, rad/s, Hz and
    /// 1/min the same dimension (per time), but rpm and rad/s carry a factor of 2π relative to Hz and 1/s; a mapping may only convert between
    /// units that either both carry an angle or both do not.
    /// </summary>
    public static bool CarriesAngle(string? symbol)
    {
        if (string.IsNullOrEmpty(symbol))
            return false;

        var start = -1;

        for (var i = 0; i <= symbol.Length; i++)
        {
            var letter = i < symbol.Length && (char.IsLetter(symbol[i]) || symbol[i] == '\u00B0' || symbol[i] == '\u00B5');

            if (letter && start < 0)
            {
                start = i;
            }
            else if (!letter && start >= 0)
            {
                var atom = symbol[start..i];
                start = -1;

                if (AngleSymbols.Contains(atom) || atom is "rpm" or "grad" || (atom.Length > 3 && atom.EndsWith("rad", StringComparison.Ordinal)))
                    return true;
            }
        }

        return false;
    }

    /// <summary>The engine's parser can throw something other than a unit error on hostile text (a huge exponent), so the call is contained here.</summary>
    internal static bool TryParseEngine(string text, out Unit unit)
    {
        unit = Unit.One;

        try
        {
            return UnitCatalog.TryParse(text, out unit, out _);
        }
        catch (Exception e) when (e is OverflowException or FormatException or ArgumentException or CalcException or InvalidOperationException)
        {
            unit = Unit.One;
            return false;
        }
    }
}
