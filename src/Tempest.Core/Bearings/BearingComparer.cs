using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Bearings;

/// <summary>The stable property keys a <see cref="ReferenceComparisonResult"/> uses for its own rows.</summary>
public static class BearingComparisonProperties
{
    /// <summary>Manufacturer.</summary>
    public const string Manufacturer = "Manufacturer";

    /// <summary>Manufacturer part number.</summary>
    public const string PartNumber = "PartNumber";

    /// <summary>Bearing designation.</summary>
    public const string Designation = "Designation";

    /// <summary>Bearing family.</summary>
    public const string Family = "Family";

    /// <summary>Bore diameter.</summary>
    public const string Bore = "Bore";

    /// <summary>Outside diameter.</summary>
    public const string OutsideDiameter = "OutsideDiameter";

    /// <summary>Nominal width.</summary>
    public const string Width = "Width";

    /// <summary>Mass.</summary>
    public const string Mass = "Mass";

    /// <summary>Basic dynamic radial load rating, C.</summary>
    public const string BasicDynamicRadial = "BasicDynamicRadial";

    /// <summary>Basic static radial load rating, C0.</summary>
    public const string BasicStaticRadial = "BasicStaticRadial";

    /// <summary>Basic dynamic axial load rating, Ca.</summary>
    public const string BasicDynamicAxial = "BasicDynamicAxial";

    /// <summary>Basic static axial load rating, C0a.</summary>
    public const string BasicStaticAxial = "BasicStaticAxial";

    /// <summary>Fatigue load limit, Pu.</summary>
    public const string FatigueLoadLimit = "FatigueLoadLimit";

    /// <summary>Reference speed.</summary>
    public const string ReferenceSpeed = "ReferenceSpeed";

    /// <summary>Limiting speed.</summary>
    public const string LimitingSpeed = "LimitingSpeed";

    /// <summary>Sealing arrangement.</summary>
    public const string Sealing = "Sealing";

    /// <summary>Internal clearance class.</summary>
    public const string InternalClearanceClass = "InternalClearanceClass";

    /// <summary>Precision class.</summary>
    public const string PrecisionClass = "PrecisionClass";

    /// <summary>Row configuration.</summary>
    public const string Rows = "Rows";

    /// <summary>Nominal contact angle.</summary>
    public const string ContactAngle = "ContactAngle";

    /// <summary>Construction class.</summary>
    public const string ConstructionClass = "ConstructionClass";

    /// <summary>Ring material reference.</summary>
    public const string RingMaterial = "RingMaterial";

    /// <summary>Rolling-element material reference.</summary>
    public const string RollingElementMaterial = "RollingElementMaterial";

    /// <summary>Cage material reference.</summary>
    public const string CageMaterial = "CageMaterial";

    /// <summary>Validation state.</summary>
    public const string ValidationState = "ValidationState";

    /// <summary>Every property key above, in the order a comparison lays its rows out.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Manufacturer, PartNumber, Designation, Family,
        Bore, OutsideDiameter, Width, Mass,
        BasicDynamicRadial, BasicStaticRadial, BasicDynamicAxial, BasicStaticAxial, FatigueLoadLimit,
        ReferenceSpeed, LimitingSpeed,
        Sealing, InternalClearanceClass, PrecisionClass, Rows, ContactAngle,
        ConstructionClass, RingMaterial, RollingElementMaterial, CageMaterial,
        ValidationState
    ];
}

/// <summary>
/// Builds a structured, side-by-side comparison of bearing records.
/// </summary>
/// <remarks>
/// <para>
/// Pure and synchronous — it reads only the records it is given, touches
/// no store, and is therefore usable anywhere from a workspace view to a
/// future selection engine. This is the "foundation for comparing
/// bearings" A4 owes downstream capabilities: it exposes the structure,
/// and stops there.
/// </para>
/// <para>
/// Bearings of different families compare perfectly well here — that is
/// the point of <see cref="ReferencePropertyAvailability.NotApplicable"/>.
/// Comparing a tapered roller bearing against a deep-groove ball bearing
/// yields a contact-angle row where one cell holds a value and the other
/// says the property does not apply, rather than a blank that reads as a
/// missing measurement.
/// </para>
/// </remarks>
public static class BearingComparer
{
    /// <summary>
    /// Compares <paramref name="bearings"/> across every property in
    /// <see cref="BearingComparisonProperties.All"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="bearings"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="bearings"/> is empty, or contains a <see langword="null"/>.</exception>
    public static ReferenceComparisonResult Compare(IReadOnlyList<IReferenceRecord<BearingDefinition>> bearings) =>
        ReferenceComparer.Compare(
            bearings,
            BearingComparisonProperties.All,
            CellFor,
            bearing => bearing.Definition.Family.ToString());

    private static ReferenceComparisonCell CellFor(IReferenceRecord<BearingDefinition> bearing, string property)
    {
        var definition = bearing.Definition;
        var family = definition.Family;
        var configuration = definition.Configuration;
        var construction = definition.Construction;
        var ratings = definition.LoadRatings;

        return property switch
        {
            BearingComparisonProperties.Manufacturer => Text(definition.Identity.Manufacturer),
            BearingComparisonProperties.PartNumber => Text(definition.Identity.ManufacturerPartNumber),
            BearingComparisonProperties.Designation => Text(definition.Identity.Designation),
            BearingComparisonProperties.Family => Text(family.ToString()),

            BearingComparisonProperties.Bore => Dimensioned(definition.Geometry.Bore),
            BearingComparisonProperties.OutsideDiameter => Dimensioned(definition.Geometry.OutsideDiameter),
            BearingComparisonProperties.Width => Dimensioned(definition.Geometry.Width),
            BearingComparisonProperties.Mass => Dimensioned(definition.Mass),

            BearingComparisonProperties.BasicDynamicRadial => Rated(ratings?.BasicDynamicRadial),
            BearingComparisonProperties.BasicStaticRadial => Rated(ratings?.BasicStaticRadial),
            BearingComparisonProperties.BasicDynamicAxial => Rated(ratings?.BasicDynamicAxial),
            BearingComparisonProperties.BasicStaticAxial => Rated(ratings?.BasicStaticAxial),
            BearingComparisonProperties.FatigueLoadLimit => Rated(ratings?.FatigueLoadLimit),

            BearingComparisonProperties.ReferenceSpeed => Speed(definition, BearingSpeedRatingKind.ReferenceSpeed),
            BearingComparisonProperties.LimitingSpeed => Speed(definition, BearingSpeedRatingKind.LimitingSpeed),

            BearingComparisonProperties.Sealing => SealingCell(configuration),
            BearingComparisonProperties.InternalClearanceClass => ApplicableText(
                configuration?.InternalClearanceClass, family, BearingFamilyTraits.HasInternalClearance(family)),
            BearingComparisonProperties.PrecisionClass => Text(configuration?.PrecisionClass),
            BearingComparisonProperties.Rows => RowsCell(configuration, family),
            BearingComparisonProperties.ContactAngle => ContactAngleCell(configuration, family),

            BearingComparisonProperties.ConstructionClass => construction is null || construction.Class == BearingConstructionClass.Unspecified
                ? ReferenceComparisonCell.NotRecorded
                : Text(construction.Class.ToString()),
            BearingComparisonProperties.RingMaterial => Text(construction?.RingMaterialId),
            BearingComparisonProperties.RollingElementMaterial => ApplicableText(
                construction?.RollingElementMaterialId, family, BearingFamilyTraits.HasRollingElements(family)),
            BearingComparisonProperties.CageMaterial => ApplicableText(
                construction?.CageMaterialId, family, BearingFamilyTraits.HasCage(family)),

            BearingComparisonProperties.ValidationState => Text(bearing.ValidationState.ToString()),

            _ => ReferenceComparisonCell.NotRecorded
        };
    }

    private static ReferenceComparisonCell SealingCell(BearingConfiguration? configuration)
    {
        var sealing = configuration?.Sealing;
        if (sealing is null)
            return ReferenceComparisonCell.NotRecorded;

        // The manufacturer's own designation is shown alongside the common
        // classification, never instead of it: a reader comparing an
        // unmapped "2RS1" against a mapped "ContactSeal" must be able to
        // see both, or the mapping's own uncertainty disappears.
        var display = sealing.ManufacturerDesignation is null
            ? sealing.Type.ToString()
            : $"{sealing.Type} ({sealing.ManufacturerDesignation})";

        return new ReferenceComparisonCell(ReferencePropertyAvailability.Recorded, display);
    }

    private static ReferenceComparisonCell RowsCell(BearingConfiguration? configuration, BearingFamily family)
    {
        if (BearingFamilyTraits.IsApplicabilityKnown(family) && !BearingFamilyTraits.HasRowConfiguration(family))
            return ReferenceComparisonCell.NotApplicable;

        var rows = configuration?.Rows ?? BearingRowConfiguration.Unspecified;
        return rows == BearingRowConfiguration.Unspecified
            ? ReferenceComparisonCell.NotRecorded
            : new ReferenceComparisonCell(ReferencePropertyAvailability.Recorded, rows.ToString());
    }

    private static ReferenceComparisonCell ContactAngleCell(BearingConfiguration? configuration, BearingFamily family)
    {
        if (BearingFamilyTraits.IsApplicabilityKnown(family) && !BearingFamilyTraits.HasContactAngle(family))
            return ReferenceComparisonCell.NotApplicable;

        return Dimensioned(configuration?.ContactAngle);
    }

    private static ReferenceComparisonCell Speed(BearingDefinition definition, BearingSpeedRatingKind kind)
    {
        var rating = definition.SpeedRatings.FirstOrDefault(speed => speed.Kind == kind);
        return ReferenceComparer.Sourced(rating?.Rating);
    }

    // Cell construction itself is shared with every other Group A library
    // — only which cell a bearing property maps to is bearing-specific.
    private static ReferenceComparisonCell Rated<TDimension>(ReferenceValue<TDimension>? rated)
        where TDimension : IDimension => ReferenceComparer.Sourced(rated);

    private static ReferenceComparisonCell Dimensioned<TDimension>(Quantity<TDimension>? quantity)
        where TDimension : IDimension => ReferenceComparer.Dimensioned(quantity);

    private static ReferenceComparisonCell Text(string? value) => ReferenceComparisonCell.Text(value);

    private static ReferenceComparisonCell ApplicableText(string? value, BearingFamily family, bool applicable) =>
        ReferenceComparisonCell.Applicable(value, applicable, BearingFamilyTraits.IsApplicabilityKnown(family));
}
