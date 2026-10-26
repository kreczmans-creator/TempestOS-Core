using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>One quantity type of the glossary: the unit it is written in, the exceptions, and the decimals at each unit.</summary>
public sealed record GlossaryQuantityType(string Quantity, string Symbols, IReadOnlyList<string> Units, IReadOnlyList<string> Exceptions, IReadOnlyDictionary<string, int> Decimals);

/// <summary>A bare symbol the standard reserves for one meaning.</summary>
public sealed record GlossaryReserved(string Symbol, string Meaning, string Row);

/// <summary>One meaning of one symbol: what it measures, the units it is written in, the descriptions the templates give it and the templates that use it.</summary>
public sealed record GlossaryMeaning(string Quantity, IReadOnlyList<string> Units, IReadOnlyList<string> Descriptions, IReadOnlyList<string> Templates);

/// <summary>A symbol and every meaning it has across the catalogue; more than one meaning is a clash for phases B and C to resolve.</summary>
public sealed record GlossarySymbol(string Symbol, IReadOnlyList<GlossaryMeaning> Meanings);

/// <summary>The glossary's data, written from the live catalogue: <c>docs/calcs/glossary-data.json</c> is what <c>scripts/generate-glossary.py</c> turns into <c>docs/calcs/glossary.md</c>.</summary>
public sealed record GlossaryDocument(int Templates, IReadOnlyList<GlossaryQuantityType> QuantityTypes, IReadOnlyList<GlossaryReserved> Reserved, IReadOnlyList<GlossarySymbol> Symbols);

internal static class GlossaryData
{
    private static readonly Dictionary<string, string> TypeSymbols = new(StringComparer.Ordinal)
    {
        ["Force"] = "F", ["Line force, distributed load, stiffness"] = "w, f, k", ["Moment per unit length"] = "m", ["Moment, torque"] = "M, T", ["Energy"] = "E, W", ["Stress, pressure"] = "sigma, tau, p, E",
        ["Friction rate"] = "dp/L", ["Length"] = "L, d, b, h, t, delta", ["Area"] = "A", ["Second moment of area"] = "I", ["Warping constant"] = "I_w", ["Volume, section modulus"] = "V, W",
        ["Volumetric flow"] = "Q", ["Mass flow"] = "m_dot", ["Mass"] = "m", ["Mass per length"] = "w", ["Mass per area"] = "m_a", ["Density"] = "rho", ["Specific volume"] = "v",
        ["Time"] = "t", ["Time, rating life"] = "L_h", ["Rotational speed, frequency"] = "n, f", ["Angular acceleration"] = "alpha", ["Angle"] = "theta", ["Angle (slope)"] = "theta",
        ["Percent quantity"] = "RH, R, u_k", ["Velocity"] = "v", ["Acceleration"] = "a", ["Temperature"] = "T_<qualifier>", ["Temperature, temperature difference"] = "dT",
        ["Power, heat flow"] = "P, Phi", ["Apparent and reactive power"] = "S, Q_r", ["Voltage"] = "U", ["Current"] = "I", ["Charge"] = "C", ["Resistance, impedance"] = "R, Z",
        ["Resistivity"] = "rho_res", ["Heat transfer coefficient, U-value"] = "U, h_conv", ["Thermal conductivity"] = "lambda", ["Thermal conductance"] = "UA", ["Thermal resistance"] = "R",
        ["Heat flux"] = "q", ["Heat flow per length"] = "q_l", ["Specific heat"] = "c_p", ["Specific energy"] = "h", ["Dynamic viscosity"] = "mu_visc", ["Moment of inertia (mass)"] = "J",
    };

    public static GlossaryDocument Build(IReadOnlyList<TemplateFacts> facts)
    {
        var types = CalcDisplay.Rules.GroupBy(r => r.Quantity).Select(g =>
        {
            var dimension = g.First().Dimension;
            var standard = CalcUnitStandard.Quantities.Where(q => q.Dimension == dimension && QuantityMatches(q.Name, g.Key)).ToList();
            var units = standard.SelectMany(q => q.Units).Distinct(StringComparer.Ordinal).ToList();
            var exceptions = standard.SelectMany(q => q.Exceptions.Select(e => $"{e.TemplateIdPrefix.TrimEnd('.')}: {string.Join(", ", e.Units)} ({e.Reason})")).Distinct(StringComparer.Ordinal).ToList();
            return new GlossaryQuantityType(g.Key, TypeSymbols.GetValueOrDefault(g.Key, string.Empty), units, exceptions, g.ToDictionary(r => r.Unit, r => r.Decimals, StringComparer.Ordinal));
        }).OrderBy(t => t.Quantity, StringComparer.Ordinal).ToList();

        var reserved = CalcSymbolStandard.Symbols.Select(s => new GlossaryReserved(s.Symbol, s.Meaning, s.Row)).ToList();
        var rows = new List<(string Symbol, string Quantity, string Unit, string Description, string Template)>();

        foreach (var f in facts)
        {
            foreach (var q in f.QuantityFacts.Where(q => q.Origin != "port"))
                rows.Add((q.Symbol, QuantityName(q.Dimension, q.Unit), CalcUnitText.Pretty(q.Unit), q.Description, f.Id));
        }

        var symbols = rows.GroupBy(r => r.Symbol, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new GlossarySymbol(
            g.Key,
            [.. g.GroupBy(r => r.Quantity, StringComparer.Ordinal).OrderByDescending(m => m.Select(r => r.Template).Distinct().Count()).ThenBy(m => m.Key, StringComparer.Ordinal).Select(m => new GlossaryMeaning(
                m.Key,
                [.. m.Select(r => r.Unit).Where(u => u.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                [.. m.Select(r => r.Description).Distinct(StringComparer.Ordinal).OrderBy(d => d, StringComparer.Ordinal)],
                [.. m.Select(r => r.Template).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]))])).ToList();

        return new GlossaryDocument(facts.Count, types, reserved, symbols);
    }

    private static bool QuantityMatches(string standardName, string ruleName) => string.Equals(standardName, ruleName, StringComparison.Ordinal);

    /// <summary>The name of a quantity type for a dimension shown in a unit: the precision table's name for the pair, else for the dimension, else the dimension itself; "Plain number" when it has none.</summary>
    public static string QuantityName(Dimension dimension, string unit)
    {
        if (dimension.IsDimensionless && string.IsNullOrEmpty(unit))
            return "Plain number";

        var shown = CalcUnitText.Pretty(unit);
        var exact = CalcDisplay.Rules.FirstOrDefault(r => r.Dimension == dimension && r.Unit == (shown is "bar a" or "bar g" ? "bar" : shown));
        var any = exact ?? CalcDisplay.Rules.FirstOrDefault(r => r.Dimension == dimension);
        return any is not null ? any.Quantity : dimension.IsDimensionless ? $"Dimensionless ({shown})" : dimension.ToString();
    }

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToJson(GlossaryDocument document) => JsonSerializer.Serialize(document, Options) + "\n";

    public static GlossaryDocument FromJson(string json) => JsonSerializer.Deserialize<GlossaryDocument>(json, Options)!;
}
