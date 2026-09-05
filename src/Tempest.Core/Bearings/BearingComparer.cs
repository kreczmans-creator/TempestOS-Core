using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Bearings;

/// <summary>The stable property keys a <see cref="BearingComparisonResult"/> uses for its own rows.</summary>
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
/// the point of <see cref="BearingPropertyAvailability.NotApplicable"/>.
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
    public static BearingComparisonResult Compare(IReadOnlyList<IBearing> bearings)
    {
        ArgumentNullException.ThrowIfNull(bearings);

        if (bearings.Count == 0)
            throw new ArgumentException("A comparison needs at least one bearing.", nameof(bearings));

        if (bearings.Any(bearing => bearing is null))
            throw new ArgumentException("A comparison cannot include a null bearing.", nameof(bearings));

        var rows = BearingComparisonProperties.All
            .Select(property => new BearingComparisonRow(
                property,
                bearings.Select(bearing => CellFor(bearing, property)).ToList()))
            .ToList();

        return new BearingComparisonResult(
            bearings.Select(bearing => bearing.BearingId).ToList(),
            rows)
        {
            IsSingleFamily = bearings.Select(bearing => bearing.Definition.Family).Distinct().Count() == 1,
        };
    }

    private static BearingComparisonCell CellFor(IBearing bearing, string property)
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
                ? BearingComparisonCell.NotRecorded
                : Text(construction.Class.ToString()),
            BearingComparisonProperties.RingMaterial => Text(construction?.RingMaterialId),
            BearingComparisonProperties.RollingElementMaterial => ApplicableText(
                construction?.RollingElementMaterialId, family, BearingFamilyTraits.HasRollingElements(family)),
            BearingComparisonProperties.CageMaterial => ApplicableText(
                construction?.CageMaterialId, family, BearingFamilyTraits.HasCage(family)),

            BearingComparisonProperties.ValidationState => Text(bearing.ValidationState.ToString()),

            _ => BearingComparisonCell.NotRecorded
        };
    }

    private static BearingComparisonCell SealingCell(BearingConfiguration? configuration)
    {
        var sealing = configuration?.Sealing;
        if (sealing is null)
            return BearingComparisonCell.NotRecorded;

        // The manufacturer's own designation is shown alongside the common
        // classification, never instead of it: a reader comparing an
        // unmapped "2RS1" against a mapped "ContactSeal" must be able to
        // see both, or the mapping's own uncertainty disappears.
        var display = sealing.ManufacturerDesignation is null
            ? sealing.Type.ToString()
            : $"{sealing.Type} ({sealing.ManufacturerDesignation})";

        return new BearingComparisonCell(BearingPropertyAvailability.Recorded, display);
    }

    private static BearingComparisonCell RowsCell(BearingConfiguration? configuration, BearingFamily family)
    {
        if (BearingFamilyTraits.IsApplicabilityKnown(family) && !BearingFamilyTraits.HasRowConfiguration(family))
            return BearingComparisonCell.NotApplicable;

        var rows = configuration?.Rows ?? BearingRowConfiguration.Unspecified;
        return rows == BearingRowConfiguration.Unspecified
            ? BearingComparisonCell.NotRecorded
            : new BearingComparisonCell(BearingPropertyAvailability.Recorded, rows.ToString());
    }

    private static BearingComparisonCell ContactAngleCell(BearingConfiguration? configuration, BearingFamily family)
    {
        if (BearingFamilyTraits.IsApplicabilityKnown(family) && !BearingFamilyTraits.HasContactAngle(family))
            return BearingComparisonCell.NotApplicable;

        return Dimensioned(configuration?.ContactAngle);
    }

    private static BearingComparisonCell Speed(BearingDefinition definition, BearingSpeedRatingKind kind)
    {
        var rating = definition.SpeedRatings.FirstOrDefault(speed => speed.Kind == kind);
        return rating is null
            ? BearingComparisonCell.NotRecorded
            : new BearingComparisonCell(BearingPropertyAvailability.Recorded, rating.Rating.Value.ToString(), rating.Rating.CanonicalValue);
    }

    private static BearingComparisonCell Rated<TDimension>(BearingRatedValue<TDimension>? rated)
        where TDimension : IDimension =>
        rated is null
            ? BearingComparisonCell.NotRecorded
            : new BearingComparisonCell(BearingPropertyAvailability.Recorded, rated.Value.ToString(), rated.CanonicalValue);

    private static BearingComparisonCell Dimensioned<TDimension>(Quantity<TDimension>? quantity)
        where TDimension : IDimension =>
        quantity is null
            ? BearingComparisonCell.NotRecorded
            : new BearingComparisonCell(
                BearingPropertyAvailability.Recorded,
                quantity.Value.ToString(),
                quantity.Value.Value * quantity.Value.Unit.ToBaseUnitFactor);

    private static BearingComparisonCell Text(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? BearingComparisonCell.NotRecorded
            : new BearingComparisonCell(BearingPropertyAvailability.Recorded, value);

    private static BearingComparisonCell ApplicableText(string? value, BearingFamily family, bool applicable) =>
        BearingFamilyTraits.IsApplicabilityKnown(family) && !applicable
            ? BearingComparisonCell.NotApplicable
            : Text(value);
}
