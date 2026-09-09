using Tempest.Core.ReferenceData;

namespace Tempest.Core.Components;

/// <summary>The stable property keys a component comparison uses for its own rows.</summary>
/// <remarks>
/// One row list across springs, gears and drive elements, with the rows
/// that do not apply to a record's own family reported as
/// <see cref="ReferencePropertyAvailability.NotApplicable"/>. A per-family
/// row list would make two families uncomparable, which is precisely what
/// a reader comparing a timing pulley against a sprocket needs to do.
/// </remarks>
public static class ComponentComparisonProperties
{
    /// <summary>The component's designation.</summary>
    public const string Designation = "ComponentDesignation";

    /// <summary>The component family.</summary>
    public const string Family = "ComponentFamily";

    /// <summary>The broad group the family belongs to.</summary>
    public const string Group = "ComponentGroup";

    /// <summary>The manufacturer of record.</summary>
    public const string Manufacturer = "ComponentManufacturer";

    /// <summary>A spring's own force-per-deflection rate.</summary>
    public const string SpringRate = "SpringRate";

    /// <summary>A spring's own torque-per-angle rate.</summary>
    public const string TorsionalRate = "TorsionalRate";

    /// <summary>A spring's own free length.</summary>
    public const string FreeLength = "FreeLength";

    /// <summary>A spring's own wire diameter.</summary>
    public const string WireDiameter = "WireDiameter";

    /// <summary>A gear's own tooth count.</summary>
    public const string NumberOfTeeth = "NumberOfTeeth";

    /// <summary>A gear's own module.</summary>
    public const string Module = "Module";

    /// <summary>A gear's own pressure angle.</summary>
    public const string PressureAngle = "PressureAngle";

    /// <summary>A gear's own face width.</summary>
    public const string FaceWidth = "FaceWidth";

    /// <summary>A drive element's own profile designation.</summary>
    public const string DriveProfile = "DriveProfile";

    /// <summary>A drive element's own pitch.</summary>
    public const string DrivePitch = "DrivePitch";

    /// <summary>The bore fitted to a shaft.</summary>
    public const string BoreDiameter = "ComponentBoreDiameter";

    /// <summary>The overall outside diameter.</summary>
    public const string OutsideDiameter = "ComponentOutsideDiameter";

    /// <summary>The component's own mass.</summary>
    public const string Mass = "ComponentMass";

    /// <summary>The greatest rotational speed the source states.</summary>
    public const string MaximumSpeed = "ComponentMaximumSpeed";

    /// <summary>The torque the source rates the component at.</summary>
    public const string RatedTorque = "ComponentRatedTorque";

    /// <summary>The record's own validation state.</summary>
    public const string ValidationState = "ComponentValidationState";

    /// <summary>Every property key, in the order a comparison lays its rows out.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Designation, Family, Group, Manufacturer,
        SpringRate, TorsionalRate, FreeLength, WireDiameter,
        NumberOfTeeth, Module, PressureAngle, FaceWidth,
        DriveProfile, DrivePitch,
        BoreDiameter, OutsideDiameter, Mass, MaximumSpeed, RatedTorque, ValidationState,
    ];
}

/// <summary>Builds a structured, side-by-side comparison of component records.</summary>
/// <remarks>
/// Pure and synchronous, and states no verdict: it says what each record
/// holds, never which component is better or which should be chosen.
/// Components of different families compare correctly — a tooth count on a
/// spring is reported as not applicable rather than as a gap.
/// </remarks>
public static class ComponentComparer
{
    /// <summary>Compares <paramref name="components"/> across every property in <see cref="ComponentComparisonProperties.All"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="components"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="components"/> is empty, or contains a <see langword="null"/>.</exception>
    public static ReferenceComparisonResult Compare(IReadOnlyList<IReferenceRecord<ComponentDefinition>> components) =>
        ReferenceComparer.Compare(
            components,
            ComponentComparisonProperties.All,
            CellFor,
            component => component.Definition.Family.ToString());

    private static ReferenceComparisonCell CellFor(IReferenceRecord<ComponentDefinition> component, string property)
    {
        var definition = component.Definition;
        var family = definition.Family;
        var known = ComponentFamilyTraits.IsApplicabilityKnown(family);

        // An unclassified family is conservatively treated as possibly
        // carrying every detail: "not known to apply" is never reported as
        // "known not to apply".
        var isSpring = !known || ComponentFamilyTraits.HasSpringDetail(family);
        var isGear = !known || ComponentFamilyTraits.HasGearDetail(family);
        var isDrive = !known || ComponentFamilyTraits.HasDriveElementDetail(family);

        return property switch
        {
            ComponentComparisonProperties.Designation => ReferenceComparisonCell.Text(definition.Designation),
            ComponentComparisonProperties.Family => family == ComponentFamily.Unspecified
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(family.ToString()),
            ComponentComparisonProperties.Group => definition.Group == ComponentGroup.Unspecified
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(definition.Group.ToString()),
            ComponentComparisonProperties.Manufacturer => ReferenceComparisonCell.Text(definition.Manufacturer),

            ComponentComparisonProperties.SpringRate => Detail(
                isSpring && (!known || !ComponentFamilyTraits.HasTorsionalRate(family)),
                () => ReferenceComparer.Sourced(definition.Spring?.Rate)),
            ComponentComparisonProperties.TorsionalRate => Detail(
                isSpring && (!known || ComponentFamilyTraits.HasTorsionalRate(family)),
                () => ReferenceComparer.Sourced(definition.Spring?.TorsionalRate)),
            ComponentComparisonProperties.FreeLength => Detail(isSpring, () => ReferenceComparer.Sourced(definition.Spring?.FreeLength)),
            ComponentComparisonProperties.WireDiameter => Detail(
                isSpring && (!known || ComponentFamilyTraits.IsHelicalSpring(family)),
                () => ReferenceComparer.Sourced(definition.Spring?.WireDiameter)),

            ComponentComparisonProperties.NumberOfTeeth => Detail(isGear, () => Count(definition.Gear?.NumberOfTeeth)),
            ComponentComparisonProperties.Module => Detail(isGear, () => ReferenceComparer.Sourced(definition.Gear?.Module)),
            ComponentComparisonProperties.PressureAngle => Detail(isGear, () => ReferenceComparer.Sourced(definition.Gear?.PressureAngle)),
            ComponentComparisonProperties.FaceWidth => Detail(isGear, () => ReferenceComparer.Sourced(definition.Gear?.FaceWidth)),

            ComponentComparisonProperties.DriveProfile => Detail(isDrive, () => ReferenceComparisonCell.Text(definition.DriveElement?.ProfileDesignation)),
            ComponentComparisonProperties.DrivePitch => Detail(isDrive, () => ReferenceComparer.Sourced(definition.DriveElement?.Pitch)),

            ComponentComparisonProperties.BoreDiameter => Detail(
                !known || ComponentFamilyTraits.HasBore(family),
                () => ReferenceComparer.Sourced(definition.Dimensions.BoreDiameter)),
            ComponentComparisonProperties.OutsideDiameter => ReferenceComparer.Sourced(definition.Dimensions.OutsideDiameter),
            ComponentComparisonProperties.Mass => ReferenceComparer.Sourced(definition.Dimensions.Mass),
            ComponentComparisonProperties.MaximumSpeed => Detail(
                !known || ComponentFamilyTraits.Rotates(family),
                () => ReferenceComparer.Sourced(definition.Ratings.MaximumSpeed)),
            ComponentComparisonProperties.RatedTorque => Detail(
                !known || ComponentFamilyTraits.TransmitsTorque(family),
                () => ReferenceComparer.Sourced(definition.Ratings.RatedTorque)),
            ComponentComparisonProperties.ValidationState => ReferenceComparisonCell.Text(component.ValidationState.ToString()),
            _ => ReferenceComparisonCell.NotRecorded,
        };
    }

    private static ReferenceComparisonCell Detail(bool applies, Func<ReferenceComparisonCell> cell) =>
        applies ? cell() : ReferenceComparisonCell.NotApplicable;

    private static ReferenceComparisonCell Count(int? count) =>
        count is null
            ? ReferenceComparisonCell.NotRecorded
            : new ReferenceComparisonCell(ReferencePropertyAvailability.Recorded, count.Value.ToString(), count.Value);
}
