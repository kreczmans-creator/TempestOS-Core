using System.Globalization;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Core.Calculations.Modules.Diagrams;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Reads a worksheet template's reference diagram against the form's
/// current inputs (ADR-0158, D-41): the variant the choices select and
/// every bound label as now entered ("L = 6 m", "P = ?").
/// </summary>
public static class CalcDiagramReader
{
    public const string BooleanTrue = "true";
    public const string BooleanFalse = "false";

    /// <summary>The diagram of <paramref name="template"/> read against <paramref name="inputs"/>, or <see langword="null"/> where it has none.</summary>
    public static CalculationDiagramReading? Read(CalcTemplate template, CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);

        if (CalcDiagrams.For(template.Id) is not { } spec)
            return null;

        var variant = SelectVariant(spec, template, inputs);
        var shapes = variant.Elements.Select(e => new DiagramShapeReading(e, LabelOf(e, template, inputs))).ToList();
        var labels = shapes.Where(s => s.Element is { InputName: not null, Symbol: not null } && s.Label is not null)
            .Select(s => Spoken(s.Element.Symbol!, ValueText(template, s.Element.InputName!, inputs)))
            .Distinct(StringComparer.Ordinal).ToList();
        var summary = $"{template.Title}. {CalculationDiagramReader.NotToScale} {variant.Caption}." + (labels.Count == 0 ? string.Empty : $" {string.Join("; ", labels)}.");
        return new CalculationDiagramReading(spec, variant, shapes, summary);
    }

    /// <summary>The first variant whose every condition holds, else the last.</summary>
    public static DiagramVariant SelectVariant(CalculationDiagramSpec spec, CalcTemplate template, CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Variants.FirstOrDefault(v => v.When.All(c => string.Equals(ConditionValue(template, c.Key, inputs), c.Value, StringComparison.Ordinal)))
            ?? spec.Variants[^1];
    }

    /// <summary>The value a label shows for <paramref name="symbol"/>: as entered, with its unit; "?" when it cannot be read; "not given" for an empty optional input.</summary>
    public static string ValueText(CalcTemplate template, string symbol, CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);

        var spec = template.FindInput(symbol);

        if (spec is null)
            return CalculationDiagramReader.Unknown;

        var value = inputs.Find(symbol);

        if (value is null)
            return spec.IsOptional ? "not given" : CalculationDiagramReader.Unknown;

        return value switch
        {
            { Rows: { } rows } => rows.Count == 0 ? CalculationDiagramReader.Unknown : rows.Count == 1 ? "1 row" : $"{rows.Count} rows",
            { Vector: { } points } => points.Count == 1 ? "1 point" : $"{points.Count} points",
            { Number: { } n } when double.IsFinite(n) => CalcValue.FormatNumber(n, 5) + (value.Unit is { Length: > 0 } u ? " " + Pretty(u) : string.Empty),
            { Flag: { } f } => f ? "yes" : "no",
            { Text: { Length: > 0 } t } => t,
            _ => CalculationDiagramReader.Unknown,
        };
    }

    /// <summary>A unit as an engineer writes it: °C, mm², kN·m.</summary>
    public static string Pretty(string unit) =>
        unit.Replace("degC", "°C", StringComparison.Ordinal).Replace("deg", "°", StringComparison.Ordinal)
            .Replace("^2", "²", StringComparison.Ordinal).Replace("^3", "³", StringComparison.Ordinal).Replace("^4", "⁴", StringComparison.Ordinal)
            .Replace("*", "·", StringComparison.Ordinal);

    private static string? LabelOf(DiagramElement element, CalcTemplate template, CalcInputSet inputs) =>
        element.Symbol is not { } symbol ? null : element.InputName is { } name ? $"{symbol} = {ValueText(template, name, inputs)}" : symbol;

    private static string Spoken(string symbol, string value) =>
        $"{symbol.Replace('_', ' ')} = {(value == CalculationDiagramReader.Unknown ? "not readable yet" : value)}";

    /// <summary>A choice's text, <c>true</c>/<c>false</c> for a flag, a number as entered, or <c>""</c> for an empty optional input.</summary>
    private static string ConditionValue(CalcTemplate template, string symbol, CalcInputSet inputs)
    {
        var value = inputs.Find(symbol);
        return template.FindInput(symbol)?.Kind switch
        {
            CalcInputKind.Boolean => value?.Flag == true ? BooleanTrue : BooleanFalse,
            CalcInputKind.Choice => value?.Text ?? string.Empty,
            _ => value?.Number is { } n ? n.ToString(CultureInfo.InvariantCulture) : value?.Text ?? string.Empty,
        };
    }
}
