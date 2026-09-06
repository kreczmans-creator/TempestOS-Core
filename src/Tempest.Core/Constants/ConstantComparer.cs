using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>The stable property keys a constants comparison uses for its own rows.</summary>
public static class ConstantComparisonProperties
{
    /// <summary>The constant's symbol.</summary>
    public const string Symbol = "ConstantSymbol";

    /// <summary>The constant's name.</summary>
    public const string ConstantName = "ConstantName";

    /// <summary>The category.</summary>
    public const string Category = "ConstantCategory";

    /// <summary>The value, in the unit the source quoted it in.</summary>
    public const string Value = "ConstantValue";

    /// <summary>The dimension the value carries.</summary>
    public const string Dimension = "ConstantDimension";

    /// <summary>What kind of uncertainty statement the source made.</summary>
    public const string UncertaintyKind = "ConstantUncertaintyKind";

    /// <summary>The absolute uncertainty.</summary>
    public const string AbsoluteUncertainty = "ConstantAbsoluteUncertainty";

    /// <summary>The relative uncertainty.</summary>
    public const string RelativeUncertainty = "ConstantRelativeUncertainty";

    /// <summary>Where the constant applies.</summary>
    public const string Applicability = "ConstantApplicability";

    /// <summary>The record's own validation state.</summary>
    public const string ValidationState = "ConstantValidationState";

    /// <summary>Every property key, in the order a comparison lays its rows out.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Symbol, ConstantName, Category, Value, Dimension,
        UncertaintyKind, AbsoluteUncertainty, RelativeUncertainty, Applicability, ValidationState,
    ];
}

/// <summary>Builds a structured, side-by-side comparison of constant records.</summary>
/// <remarks>
/// <para>
/// The comparison a constants library is actually asked for: two editions
/// of the same constant, or the same constant as two sources publish it,
/// laid out so the difference and the uncertainties around it are visible
/// at once. It says what each record holds and never which is right.
/// </para>
/// <para>
/// A canonical value is offered only where the records being compared all
/// carry the same dimension. Ordering values of different dimensions by
/// their base-unit magnitudes would be arithmetic on numbers that are not
/// comparable.
/// </para>
/// </remarks>
public static class ConstantComparer
{
    /// <summary>Compares <paramref name="constants"/> across every property in <see cref="ConstantComparisonProperties.All"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="constants"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="constants"/> is empty, or contains a <see langword="null"/>.</exception>
    public static ReferenceComparisonResult Compare(IReadOnlyList<IReferenceRecord<ConstantDefinition>> constants)
    {
        ArgumentNullException.ThrowIfNull(constants);

        var comparable = constants
            .Where(constant => constant?.Definition.Value is not null)
            .Select(constant => constant.Definition.Value!.DimensionName)
            .Distinct(StringComparer.Ordinal)
            .Count() <= 1;

        return ReferenceComparer.Compare(
            constants,
            ConstantComparisonProperties.All,
            (constant, property) => CellFor(constant, property, comparable),
            constant => constant.Definition.Value?.DimensionName ?? "Unrecorded");
    }

    private static ReferenceComparisonCell CellFor(IReferenceRecord<ConstantDefinition> constant, string property, bool dimensionsAgree)
    {
        var definition = constant.Definition;
        var uncertainty = definition.Uncertainty;

        return property switch
        {
            ConstantComparisonProperties.Symbol => ReferenceComparisonCell.Text(definition.Symbol),
            ConstantComparisonProperties.ConstantName => ReferenceComparisonCell.Text(definition.Name),
            ConstantComparisonProperties.Category => definition.Category == ConstantCategory.Unspecified
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(definition.Category.ToString()),
            ConstantComparisonProperties.Value => Quantity(definition.Value, dimensionsAgree),
            ConstantComparisonProperties.Dimension => ReferenceComparisonCell.Text(definition.Value?.DimensionName),

            ConstantComparisonProperties.UncertaintyKind => uncertainty.Kind == ConstantUncertaintyKind.NotRecorded
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(uncertainty.Kind.ToString()),

            // An exact constant has no uncertainty to record — nothing to
            // record is not the same as nobody recording it.
            ConstantComparisonProperties.AbsoluteUncertainty => uncertainty.IsExact
                ? ReferenceComparisonCell.NotApplicable
                : Quantity(uncertainty.Absolute, dimensionsAgree),
            ConstantComparisonProperties.RelativeUncertainty => uncertainty.IsExact
                ? ReferenceComparisonCell.NotApplicable
                : uncertainty.Relative is { } relative
                    ? new ReferenceComparisonCell(ReferencePropertyAvailability.Recorded, relative.ToString(), relative)
                    : ReferenceComparisonCell.NotRecorded,

            ConstantComparisonProperties.Applicability => ReferenceComparisonCell.Text(definition.Applicability),
            ConstantComparisonProperties.ValidationState => ReferenceComparisonCell.Text(constant.ValidationState.ToString()),
            _ => ReferenceComparisonCell.NotRecorded,
        };
    }

    private static ReferenceComparisonCell Quantity(ReferenceQuantityValue? value, bool dimensionsAgree) =>
        value is null
            ? ReferenceComparisonCell.NotRecorded
            : new ReferenceComparisonCell(
                ReferencePropertyAvailability.Recorded,
                value.Value.ToString(),
                dimensionsAgree ? value.CanonicalValue : null);
}
