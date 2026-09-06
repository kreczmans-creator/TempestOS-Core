using Tempest.Core.ReferenceData;

namespace Tempest.Core.Fasteners;

/// <summary>The stable property keys a fastener comparison uses for its own rows.</summary>
public static class FastenerComparisonProperties
{
    /// <summary>The fastener's designation.</summary>
    public const string Designation = "Designation";

    /// <summary>The fastener family.</summary>
    public const string Family = "Family";

    /// <summary>The manufacturer of record.</summary>
    public const string Manufacturer = "FastenerManufacturer";

    /// <summary>The thread designation.</summary>
    public const string ThreadDesignation = "ThreadDesignation";

    /// <summary>The nominal thread diameter.</summary>
    public const string NominalDiameter = "NominalDiameter";

    /// <summary>The thread pitch.</summary>
    public const string Pitch = "Pitch";

    /// <summary>Which way the thread turns.</summary>
    public const string Handedness = "Handedness";

    /// <summary>The head form.</summary>
    public const string HeadType = "HeadType";

    /// <summary>The driving feature.</summary>
    public const string DriveType = "DriveType";

    /// <summary>The nominal length.</summary>
    public const string NominalLength = "NominalLength";

    /// <summary>The spanner size across flats.</summary>
    public const string WidthAcrossFlats = "WidthAcrossFlats";

    /// <summary>The property class or grade designation.</summary>
    public const string PropertyClass = "PropertyClass";

    /// <summary>The published proof strength.</summary>
    public const string ProofStrength = "ProofStrength";

    /// <summary>The published minimum tensile strength.</summary>
    public const string TensileStrength = "TensileStrength";

    /// <summary>The published proof load.</summary>
    public const string ProofLoad = "ProofLoad";

    /// <summary>The published hardness band, on its own scale.</summary>
    public const string Hardness = "Hardness";

    /// <summary>The surface finish or coating.</summary>
    public const string Finish = "Finish";

    /// <summary>How many published tightening torques the record holds.</summary>
    public const string TorqueReferenceCount = "TorqueReferenceCount";

    /// <summary>The record's own validation state.</summary>
    public const string ValidationState = "FastenerValidationState";

    /// <summary>Every property key, in the order a comparison lays its rows out.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Designation, Family, Manufacturer, ThreadDesignation, NominalDiameter, Pitch, Handedness,
        HeadType, DriveType, NominalLength, WidthAcrossFlats, PropertyClass,
        ProofStrength, TensileStrength, ProofLoad, Hardness, Finish, TorqueReferenceCount, ValidationState,
    ];
}

/// <summary>Builds a structured, side-by-side comparison of fastener records.</summary>
/// <remarks>
/// Pure and synchronous, and states no verdict: it says what each record
/// holds, never which fastener is stronger where it matters, which suits a
/// joint, or which should be chosen. Fasteners of different families
/// compare correctly — a thread on a washer is reported as not applicable
/// rather than as a gap.
/// </remarks>
public static class FastenerComparer
{
    /// <summary>Compares <paramref name="fasteners"/> across every property in <see cref="FastenerComparisonProperties.All"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="fasteners"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="fasteners"/> is empty, or contains a <see langword="null"/>.</exception>
    public static ReferenceComparisonResult Compare(IReadOnlyList<IReferenceRecord<FastenerDefinition>> fasteners) =>
        ReferenceComparer.Compare(
            fasteners,
            FastenerComparisonProperties.All,
            CellFor,
            fastener => fastener.Definition.Family.ToString());

    private static ReferenceComparisonCell CellFor(IReferenceRecord<FastenerDefinition> fastener, string property)
    {
        var definition = fastener.Definition;
        var family = definition.Family;
        var known = FastenerFamilyTraits.IsApplicabilityKnown(family);
        var thread = definition.Thread;
        var threaded = !known || FastenerFamilyTraits.IsThreaded(family);

        return property switch
        {
            FastenerComparisonProperties.Designation => ReferenceComparisonCell.Text(definition.Designation),
            FastenerComparisonProperties.Family => family == FastenerFamily.Unspecified
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(family.ToString()),
            FastenerComparisonProperties.Manufacturer => ReferenceComparisonCell.Text(definition.Manufacturer),

            FastenerComparisonProperties.ThreadDesignation => threaded
                ? ReferenceComparisonCell.Text(thread?.Designation)
                : ReferenceComparisonCell.NotApplicable,
            FastenerComparisonProperties.NominalDiameter => threaded
                ? ReferenceComparer.Sourced(thread?.NominalDiameter)
                : ReferenceComparisonCell.NotApplicable,
            FastenerComparisonProperties.Pitch => threaded
                ? ReferenceComparer.Sourced(thread?.Pitch)
                : ReferenceComparisonCell.NotApplicable,
            FastenerComparisonProperties.Handedness => threaded
                ? thread is null or { Handedness: ThreadHandedness.Unspecified }
                    ? ReferenceComparisonCell.NotRecorded
                    : ReferenceComparisonCell.Text(thread.Handedness.ToString())
                : ReferenceComparisonCell.NotApplicable,

            FastenerComparisonProperties.HeadType => Enumerated(
                definition.HeadType == FastenerHeadType.Unspecified ? null : definition.HeadType.ToString(),
                FastenerFamilyTraits.HasHead(family),
                known),
            FastenerComparisonProperties.DriveType => Enumerated(
                definition.DriveType == FastenerDriveType.Unspecified ? null : definition.DriveType.ToString(),
                FastenerFamilyTraits.HasDriveFeature(family),
                known),

            FastenerComparisonProperties.NominalLength => FastenerFamilyTraits.HasNominalLength(family) || !known
                ? ReferenceComparer.Sourced(definition.Dimensions.NominalLength)
                : ReferenceComparisonCell.NotApplicable,
            FastenerComparisonProperties.WidthAcrossFlats => ReferenceComparer.Sourced(definition.Dimensions.WidthAcrossFlats),

            FastenerComparisonProperties.PropertyClass => Enumerated(
                definition.Mechanical.PropertyClass,
                FastenerFamilyTraits.TakesPropertyClass(family),
                known),
            FastenerComparisonProperties.ProofStrength => ReferenceComparer.Sourced(definition.Mechanical.ProofStrength),
            FastenerComparisonProperties.TensileStrength => ReferenceComparer.Sourced(definition.Mechanical.TensileStrength),
            FastenerComparisonProperties.ProofLoad => FastenerFamilyTraits.TakesProofLoad(family) || !known
                ? ReferenceComparer.Sourced(definition.Mechanical.ProofLoad)
                : ReferenceComparisonCell.NotApplicable,
            FastenerComparisonProperties.Hardness => Hardness(definition.Mechanical.Hardness),

            FastenerComparisonProperties.Finish => ReferenceComparisonCell.Text(definition.Finish?.Designation),
            FastenerComparisonProperties.TorqueReferenceCount => FastenerFamilyTraits.TakesTighteningTorque(family) || !known
                ? new ReferenceComparisonCell(
                    ReferencePropertyAvailability.Recorded,
                    definition.TorqueReferences.Count.ToString(),
                    definition.TorqueReferences.Count)
                : ReferenceComparisonCell.NotApplicable,
            FastenerComparisonProperties.ValidationState => ReferenceComparisonCell.Text(fastener.ValidationState.ToString()),
            _ => ReferenceComparisonCell.NotRecorded,
        };
    }

    private static ReferenceComparisonCell Enumerated(string? value, bool applies, bool applicabilityKnown) =>
        ReferenceComparisonCell.Applicable(value, applies, applicabilityKnown);

    /// <summary>
    /// A hardness band displays with its own scale attached and carries no
    /// canonical value: hardness numbers on different scales are not
    /// comparable, so offering one to sort by would invite exactly the
    /// cross-scale comparison <see cref="FastenerHardness"/> exists to
    /// prevent.
    /// </summary>
    private static ReferenceComparisonCell Hardness(FastenerHardness? hardness)
    {
        if (hardness is null || !hardness.IsRecorded)
            return ReferenceComparisonCell.NotRecorded;

        var display = (hardness.Minimum, hardness.Maximum) switch
        {
            ({ } min, { } max) => $"{min} to {max} {hardness.Scale}",
            ({ } min, null) => $"{min} {hardness.Scale} or more",
            (null, { } max) => $"up to {max} {hardness.Scale}",
            _ => null,
        };

        return new ReferenceComparisonCell(ReferencePropertyAvailability.Recorded, display);
    }
}
