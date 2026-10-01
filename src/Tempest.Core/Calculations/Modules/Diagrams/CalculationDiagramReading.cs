using System.Globalization;

namespace Tempest.Core.Calculations.Modules.Diagrams;

/// <summary>One shape of a diagram as currently drawn: the spec's shape and its label as the form now reads.</summary>
/// <param name="Element">The shape.</param>
/// <param name="Label">Its label (<c>L = 2000 mm</c>, <c>L = ?</c>), or <see langword="null"/> where it has none.</param>
public sealed record DiagramShapeReading(DiagramElement Element, string? Label);

/// <summary>A diagram read against the form as it stands: the variant the form selects, every shape with its label, and the same in words for a screen reader.</summary>
/// <param name="Spec">The diagram.</param>
/// <param name="Variant">The variant drawn.</param>
/// <param name="Shapes">Every shape of that variant, in drawing order, labelled.</param>
/// <param name="Summary">The diagram in words.</param>
public sealed record CalculationDiagramReading(CalculationDiagramSpec Spec, DiagramVariant Variant, IReadOnlyList<DiagramShapeReading> Shapes, string Summary)
{
    /// <summary>The first label of the shapes bound to <paramref name="inputName"/>, or <see langword="null"/>.</summary>
    public string? LabelFor(string inputName) =>
        Shapes.FirstOrDefault(s => s.Element.InputName == inputName && s.Label is not null)?.Label;
}

/// <summary>
/// Reads a <see cref="CalculationDiagramSpec"/> against a form: which
/// variant its choices select and what each bound shape's label says.
/// A value the calculation could not read (blank, not a number, a unit
/// the dimension does not have) is labelled <c>?</c>, never guessed.
/// A row list is labelled with how many rows the form lists.
/// </summary>
public static class CalculationDiagramReader
{
    /// <summary>What a label shows in place of a value the form does not yet hold.</summary>
    public const string Unknown = "?";

    /// <summary>The words every summary opens with after the title: the diagram is never to scale, and shows inputs only.</summary>
    public const string NotToScale = "Reference diagram, not to scale, inputs only.";

    /// <summary>Reads <paramref name="spec"/> for <paramref name="module"/> against the form, as <paramref name="field"/> answers each input by name.</summary>
    public static CalculationDiagramReading Read(CalculationDiagramSpec spec, CalculationModuleDescriptor module, Func<string, CalculationFormField?> field)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(field);

        var variant = SelectVariant(spec, module, field);
        var shapes = variant.Elements.Select(e => new DiagramShapeReading(e, LabelOf(e, module, field))).ToList();

        var labels = shapes.Where(s => s.Element.InputName is not null && s.Label is not null).Select(s => s.Label!).Distinct(StringComparer.Ordinal).ToList();
        var summary = $"{module.Title}. {NotToScale} {variant.Caption}." + (labels.Count == 0 ? string.Empty : $" {string.Join("; ", labels)}.");
        return new CalculationDiagramReading(spec, variant, shapes, summary);
    }

    /// <summary>The variant the form's values select: the first whose every condition holds, else the last.</summary>
    public static DiagramVariant SelectVariant(CalculationDiagramSpec spec, CalculationModuleDescriptor module, Func<string, CalculationFormField?> field)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(field);

        return spec.Variants.FirstOrDefault(v => v.When.All(c => string.Equals(ConditionValue(module, c.Key, field(c.Key)), c.Value, StringComparison.Ordinal)))
            ?? spec.Variants[^1];
    }

    /// <summary>The value <paramref name="inputName"/> shows on a label: as typed, with its unit, or <see cref="Unknown"/>.</summary>
    public static string ValueText(CalculationModuleDescriptor module, string inputName, CalculationFormField? field)
    {
        ArgumentNullException.ThrowIfNull(module);

        var input = module.Inputs.FirstOrDefault(i => i.Name == inputName);
        if (input is null)
            return Unknown;

        var text = field?.Text?.Trim();
        switch (input.Kind)
        {
            case CalculationInputKind.Quantity:
                if (string.IsNullOrEmpty(text))
                    return input.IsOptional ? "not given" : Unknown;
                var unit = field!.UnitSymbol ?? input.DefaultUnitSymbol;
                return CalculationInputUnits.TryParse(input.DimensionName!, text, unit, out _) is null ? Unknown : $"{text} {unit}";

            case CalculationInputKind.Number:
                return !string.IsNullOrEmpty(text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
                    ? text
                    : Unknown;

            case CalculationInputKind.Text:
                return string.IsNullOrEmpty(text) ? Unknown : text;

            case CalculationInputKind.Choice:
                return field?.Choice is { Length: > 0 } choice ? CalculationModuleForm.Humanise(choice) : Unknown;

            case CalculationInputKind.Boolean:
                return field?.Flag == true ? "yes" : "no";

            case CalculationInputKind.List:
            {
                // A drawing is representative; its label says how many rows the form lists.
                var rows = field?.Rows?.Count(r => !string.IsNullOrWhiteSpace(r)) ?? 0;
                return rows == 0 ? Unknown : rows == 1 ? "1 row" : $"{rows} rows";
            }

            default:
                return Unknown;
        }
    }

    private static string? LabelOf(DiagramElement element, CalculationModuleDescriptor module, Func<string, CalculationFormField?> field)
    {
        if (element.Symbol is not { } symbol)
            return null;

        return element.InputName is { } name ? $"{symbol} = {ValueText(module, name, field(name))}" : symbol;
    }

    private static string? ConditionValue(CalculationModuleDescriptor module, string inputName, CalculationFormField? field)
    {
        var kind = module.Inputs.FirstOrDefault(i => i.Name == inputName)?.Kind;
        return kind == CalculationInputKind.Choice ? field?.Choice : field?.Text?.Trim();
    }
}
