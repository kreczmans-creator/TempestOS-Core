using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>Whether a material property is one number (with a unit) or a small table.</summary>
public enum MaterialPropertyShape
{
    Scalar,
    Table,
}

/// <summary>
/// One property a material record can hold (Addendum B, W3): its symbol, what it is, the unit it is kept in internally (SI),
/// its valid range, and whether it can also be given as a temperature table (the symbol with "(T)": <c>E(T)</c>).
/// </summary>
/// <param name="Key">The property symbol: the symbol of the value in the record, and what a template input names in <see cref="CalcInputSpec.MaterialProperty"/>.</param>
/// <param name="Title">What it is, for lists and messages.</param>
/// <param name="SiUnit">The coherent SI unit (empty: dimensionless); <see langword="null"/> when the unit depends on the coefficient (any unit parses).</param>
/// <param name="Shape">One number, or a table.</param>
/// <param name="ExclusiveMin">The value must be larger than this, or <see langword="null"/>.</param>
/// <param name="Min">The value must be at least this, or <see langword="null"/>.</param>
/// <param name="Max">The value must be at most this, or <see langword="null"/>.</param>
/// <param name="ExclusiveMax">The value must be smaller than this, or <see langword="null"/>.</param>
/// <param name="TemperatureDependent">Whether a temperature table <c>Key(T)</c> may be given.</param>
public sealed record MaterialPropertyDef(
    string Key,
    string Title,
    string? SiUnit,
    MaterialPropertyShape Shape = MaterialPropertyShape.Scalar,
    double? ExclusiveMin = null,
    double? Min = null,
    double? Max = null,
    double? ExclusiveMax = null,
    bool TemperatureDependent = false)
{
    /// <summary>The symbol of this property's temperature table.</summary>
    public string TemperatureKey => Key + MaterialProperties.TemperatureSuffix;

    /// <summary>The dimension the property is kept in, or <see langword="null"/> when any unit is accepted.</summary>
    public Dimension? Dimension => SiUnit is null ? null : UnitCatalog.Parse(SiUnit).Dimension;

    /// <summary>Why <paramref name="si"/> is not a valid value of this property, or <see langword="null"/>.</summary>
    public string? RangeProblem(double si)
    {
        if (!double.IsFinite(si))
            return $"{Key} ({Title}) must be a finite number.";

        if (ExclusiveMin is { } xl && si <= xl)
            return $"{Key} ({Title}) must be larger than {Format(xl)}; got {Format(si)}.";

        if (Min is { } lo && si < lo)
            return $"{Key} ({Title}) must be at least {Format(lo)}; got {Format(si)}.";

        if (Max is { } hi && si > hi)
            return $"{Key} ({Title}) must be at most {Format(hi)}; got {Format(si)}.";

        if (ExclusiveMax is { } xh && si >= xh)
            return $"{Key} ({Title}) must be smaller than {Format(xh)}; got {Format(si)}.";

        return null;
    }

    private static string Format(double x) => x.ToString("G6", CultureInfo.InvariantCulture);
}

/// <summary>
/// The catalogue of material properties (Addendum B, W3). Values are kept as typed (number and unit) and read in SI. Only
/// the properties listed here can be saved in a <c>material.properties</c> record; a typing slip in a symbol is refused,
/// not stored under a name nothing will ever ask for.
/// </summary>
public static class MaterialProperties
{
    /// <summary>The suffix that makes a property's temperature table: <c>E(T)</c>.</summary>
    public const string TemperatureSuffix = "(T)";

    /// <summary>The table property of stress–strain points: columns <c>eps</c> (strain) and <c>Es</c> (secant modulus).</summary>
    public const string SecantCurve = "Es_curve";

    /// <summary>The table property of friction coefficients by material pair: columns <c>pair</c> (text) and <c>mu</c>.</summary>
    public const string FrictionPairs = "mu_pairs";

    /// <summary>The column of a temperature table that holds the temperature.</summary>
    public const string TemperatureColumn = "T";

    /// <summary>The column of a temperature table that holds the property.</summary>
    public const string ValueColumn = "v";

    public static IReadOnlyList<MaterialPropertyDef> All { get; } =
    [
        new("E", "Young's modulus", "Pa", ExclusiveMin: 0, TemperatureDependent: true),
        new("G", "shear modulus", "Pa", ExclusiveMin: 0, TemperatureDependent: true),
        new("nu", "Poisson's ratio", string.Empty, ExclusiveMin: -1, ExclusiveMax: 0.5, TemperatureDependent: true),
        new("rho", "density", "kg/m^3", ExclusiveMin: 0, TemperatureDependent: true),
        new("alpha", "coefficient of thermal expansion", "1/K", TemperatureDependent: true),
        new("lambda", "thermal conductivity", "W/(m*K)", ExclusiveMin: 0, TemperatureDependent: true),
        new("f_y", "yield strength", "Pa", ExclusiveMin: 0, TemperatureDependent: true),
        new("f_u", "ultimate tensile strength", "Pa", ExclusiveMin: 0, TemperatureDependent: true),
        new("Rm_A", "R_m(d) curve coefficient A (R_m = A / d^m; the unit follows m)", null, ExclusiveMin: 0),
        new("Rm_m", "R_m(d) curve exponent m", string.Empty, Min: 0, Max: 5),
        new("eps_perm_once", "permissible strain, one-time loading", string.Empty, ExclusiveMin: 0, Max: 1),
        new("eps_perm_repeated", "permissible strain, repeated loading", string.Empty, ExclusiveMin: 0, Max: 1),
        new(SecantCurve, "secant modulus Es against strain (points)", "Pa", MaterialPropertyShape.Table),
        new(FrictionPairs, "friction coefficient by material pair", string.Empty, MaterialPropertyShape.Table),
    ];

    /// <summary>The property for a symbol (<c>E</c>), or <see langword="null"/>. A temperature table symbol (<c>E(T)</c>) gives its property too.</summary>
    public static MaterialPropertyDef? Find(string? key, out bool temperatureTable)
    {
        temperatureTable = false;

        if (string.IsNullOrEmpty(key))
            return null;

        if (key.EndsWith(TemperatureSuffix, StringComparison.Ordinal))
        {
            var def = All.FirstOrDefault(p => p.TemperatureDependent && string.Equals(p.Key + TemperatureSuffix, key, StringComparison.Ordinal));
            temperatureTable = def is not null;
            return def;
        }

        return All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether <paramref name="unit"/> is a unit of <paramref name="def"/>'s dimension (a dimensionless property takes no unit).
    /// Returns the parsed unit, or the reason it is not.
    /// </summary>
    internal static (Unit? Unit, string? Problem) ParseUnit(MaterialPropertyDef def, string? unit, string what)
    {
        var text = unit?.Trim() ?? string.Empty;

        if (text.Length == 0 && def.SiUnit is { Length: > 0 })
            return (null, $"{what}: give the unit ({def.SiUnit} or any unit of the same kind); a number without a unit is not read as SI.");

        if (!UnitCatalog.TryParse(text, out var parsed, out var error))
            return (null, $"{what}: unit '{text}' is not understood ({error}).");

        if (def.Dimension is { } dimension && parsed.Dimension != dimension)
            return (null, $"{what}: unit '{text}' does not measure {def.Title}; use a unit of {(def.SiUnit is { Length: > 0 } si ? si : "a plain number")}.");

        return (parsed, null);
    }

    /// <summary>The temperature unit named, or the reason it is not a temperature unit.</summary>
    internal static (Unit? Unit, string? Problem) ParseTemperatureUnit(string? unit, string what)
    {
        var text = string.IsNullOrWhiteSpace(unit) ? "K" : unit.Trim();

        if (!UnitCatalog.TryParse(text, out var parsed, out var error))
            return (null, $"{what}: unit '{text}' is not understood ({error}).");

        return parsed.Dimension != Dimension.Theta
            ? (null, $"{what}: unit '{text}' is not a temperature unit (K, degC or degF).")
            : (parsed, null);
    }

    /// <summary>
    /// Why a <c>material.properties</c> record cannot be saved, or <see langword="null"/>: unknown properties, units of the
    /// wrong kind, values out of range, temperature tables that are not strictly rising, a value without its source.
    /// </summary>
    public static string? Problem(CalcDataRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!MaterialLibrary.IsMaterial(record.Kind))
            return $"A material record's kind is {MaterialLibrary.Kind}.";

        foreach (var value in record.Values)
        {
            var def = Find(value.Symbol, out var temperatureTable);

            if (def is null)
                return $"'{value.Symbol}' is not a material property; the properties are {string.Join(", ", All.Select(p => p.Key))}, and the temperature tables {string.Join(", ", All.Where(p => p.TemperatureDependent).Select(p => p.TemperatureKey))}.";

            if (value.Source is not { } source
                || string.IsNullOrWhiteSpace(source.Source) || string.IsNullOrWhiteSpace(source.Edition) || string.IsNullOrWhiteSpace(source.Reference) || string.IsNullOrWhiteSpace(source.EnteredBy))
                return $"{value.Symbol}: a material value carries its source, edition, page or table reference and who entered it.";

            var problem = temperatureTable ? TemperatureTableProblem(def, value) : def.Shape == MaterialPropertyShape.Table ? CurveProblem(def, value) : ScalarProblem(def, value);

            if (problem is not null)
                return problem;
        }

        return null;
    }

    private static string? ScalarProblem(MaterialPropertyDef def, CalcDataValue value)
    {
        if (value.Number is not { } number || value.Rows is not null)
            return $"{def.Key} ({def.Title}) is one number with its unit.";

        var (unit, problem) = ParseUnit(def, value.Unit, def.Key);

        return problem ?? def.RangeProblem(unit!.ToSi(number));
    }

    private static string? TemperatureTableProblem(MaterialPropertyDef def, CalcDataValue value)
    {
        var name = def.TemperatureKey;

        if (value.Rows is not { Count: >= 2 } rows || value.Number is not null)
            return $"{name} is a table of at least two rows, columns {TemperatureColumn} (temperature) and {ValueColumn} ({def.Title}).";

        double? previous = null;

        for (var i = 0; i < rows.Count; i++)
        {
            var label = $"{name}[{i + 1}]";
            var t = rows[i].Get(TemperatureColumn);
            var v = rows[i].Get(ValueColumn);

            if (t?.Number is not { } tNumber || v?.Number is not { } vNumber)
                return $"{label}: each row needs a temperature ({TemperatureColumn}) and a value ({ValueColumn}).";

            var (tUnit, tProblem) = ParseTemperatureUnit(t.Unit, $"{label}.{TemperatureColumn}");

            if (tProblem is not null)
                return tProblem;

            var (vUnit, vProblem) = ParseUnit(def, v.Unit, $"{label}.{ValueColumn}");

            if (vProblem is not null)
                return vProblem;

            var tSi = tUnit!.ToSi(tNumber);

            if (!double.IsFinite(tSi) || tSi <= 0)
                return $"{label}: the temperature must be above absolute zero.";

            if (previous is { } p && tSi <= p)
                return $"{name}: the temperatures must rise strictly from row to row (row {i + 1} does not).";

            previous = tSi;

            if (def.RangeProblem(vUnit!.ToSi(vNumber)) is { } range)
                return $"{label}: {range}";
        }

        return null;
    }

    private static string? CurveProblem(MaterialPropertyDef def, CalcDataValue value)
    {
        if (value.Rows is not { Count: > 0 } rows || value.Number is not null)
            return $"{def.Key} is a table ({def.Title}).";

        if (def.Key == SecantCurve)
        {
            if (rows.Count < 2)
                return $"{def.Key} needs at least two points: columns eps (strain) and Es (secant modulus).";

            double? previous = null;

            for (var i = 0; i < rows.Count; i++)
            {
                var label = $"{def.Key}[{i + 1}]";

                if (rows[i].Get("eps")?.Number is not { } eps || !double.IsFinite(eps) || eps < 0)
                    return $"{label}.eps: the strain is a number, not negative.";

                if (previous is { } p && eps <= p)
                    return $"{def.Key}: the strains must rise strictly from point to point (point {i + 1} does not).";

                previous = eps;
                var cell = rows[i].Get("Es");

                if (cell?.Number is not { } es)
                    return $"{label}.Es: the secant modulus is a number with its unit.";

                var (unit, problem) = ParseUnit(def, cell.Unit, $"{label}.Es");

                if (problem is not null)
                    return problem;

                if (def.RangeProblem(unit!.ToSi(es)) is { } range)
                    return $"{label}: {range}";
            }

            return null;
        }

        // Friction coefficients by pair.
        var pairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < rows.Count; i++)
        {
            var label = $"{def.Key}[{i + 1}]";

            if (string.IsNullOrWhiteSpace(rows[i].Get("pair")?.Text) || !pairs.Add(rows[i].Get("pair")!.Text!.Trim()))
                return $"{label}.pair: name the pair of materials once (for example \"steel / steel, dry\").";

            if (rows[i].Get("mu")?.Number is not { } mu || !double.IsFinite(mu) || mu < 0 || mu > 5)
                return $"{label}.mu: a friction coefficient is a number from 0 to 5.";
        }

        return null;
    }
}
