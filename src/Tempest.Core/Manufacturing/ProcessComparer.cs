using Tempest.Core.ReferenceData;

namespace Tempest.Core.Manufacturing;

/// <summary>The stable property keys a process comparison uses for its own rows.</summary>
public static class ProcessComparisonProperties
{
    /// <summary>The process's name.</summary>
    public const string ProcessName = "ProcessName";

    /// <summary>The process family.</summary>
    public const string ProcessFamilyKey = "ProcessFamily";

    /// <summary>The broad group the family belongs to.</summary>
    public const string ProcessGroupKey = "ProcessGroup";

    /// <summary>The published dimensional tolerance band.</summary>
    public const string AchievableTolerance = "AchievableTolerance";

    /// <summary>The published surface roughness band.</summary>
    public const string SurfaceRoughness = "SurfaceRoughness";

    /// <summary>The published wall thickness band.</summary>
    public const string WallThickness = "WallThickness";

    /// <summary>The published part-size band.</summary>
    public const string PartSize = "PartSize";

    /// <summary>The published part-mass band.</summary>
    public const string PartMass = "PartMass";

    /// <summary>The published draft angle band.</summary>
    public const string DraftAngle = "DraftAngle";

    /// <summary>The published process temperature band.</summary>
    public const string ProcessTemperature = "ProcessTemperature";

    /// <summary>The published cycle time band.</summary>
    public const string CycleTime = "CycleTime";

    /// <summary>The production scales sources associated with the process.</summary>
    public const string ProductionScales = "ProductionScales";

    /// <summary>How many materials the record associates with the process.</summary>
    public const string MaterialCount = "ProcessMaterialCount";

    /// <summary>How many constraints the record states.</summary>
    public const string ConstraintCount = "ProcessConstraintCount";

    /// <summary>The record's own validation state.</summary>
    public const string ValidationState = "ProcessValidationState";

    /// <summary>Every property key, in the order a comparison lays its rows out.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        ProcessName, ProcessFamilyKey, ProcessGroupKey,
        AchievableTolerance, SurfaceRoughness, WallThickness, PartSize, PartMass,
        DraftAngle, ProcessTemperature, CycleTime,
        ProductionScales, MaterialCount, ConstraintCount, ValidationState,
    ];
}

/// <summary>Builds a structured, side-by-side comparison of manufacturing process records.</summary>
/// <remarks>
/// <b>Structure only, never a verdict.</b> Laying two processes' published
/// bands side by side is not choosing between them: the comparison says
/// nothing about which suits a part, which costs less, or which a supplier
/// could hold. A capability a process does not have is reported as not
/// applicable — a draft angle on a turning operation is not a data gap.
/// </remarks>
public static class ProcessComparer
{
    /// <summary>Compares <paramref name="processes"/> across every property in <see cref="ProcessComparisonProperties.All"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="processes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="processes"/> is empty, or contains a <see langword="null"/>.</exception>
    public static ReferenceComparisonResult Compare(IReadOnlyList<IReferenceRecord<ProcessDefinition>> processes) =>
        ReferenceComparer.Compare(
            processes,
            ProcessComparisonProperties.All,
            CellFor,
            process => process.Definition.Group.ToString());

    private static ReferenceComparisonCell CellFor(IReferenceRecord<ProcessDefinition> process, string property)
    {
        var definition = process.Definition;
        var capabilities = definition.Capabilities;
        var family = definition.Family;
        var known = ProcessFamilyTraits.IsApplicabilityKnown(family);

        return property switch
        {
            ProcessComparisonProperties.ProcessName => ReferenceComparisonCell.Text(definition.Name),
            ProcessComparisonProperties.ProcessFamilyKey => family == ProcessFamily.Unspecified
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(family.ToString()),
            ProcessComparisonProperties.ProcessGroupKey => definition.Group == ProcessGroup.Unspecified
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(definition.Group.ToString()),

            ProcessComparisonProperties.AchievableTolerance => ReferenceComparer.Ranged(capabilities.AchievableTolerance),
            ProcessComparisonProperties.SurfaceRoughness => Applicable(
                !known || ProcessFamilyTraits.HasSurfaceRoughnessCapability(family),
                () => ReferenceComparer.Ranged(capabilities.SurfaceRoughness)),
            ProcessComparisonProperties.WallThickness => Applicable(
                !known || ProcessFamilyTraits.HasWallThicknessCapability(family),
                () => ReferenceComparer.Ranged(capabilities.WallThickness)),
            ProcessComparisonProperties.PartSize => Applicable(
                !known || !ProcessFamilyTraits.IsJoining(family),
                () => ReferenceComparer.Ranged(capabilities.PartSize)),
            ProcessComparisonProperties.PartMass => Applicable(
                !known || !ProcessFamilyTraits.IsJoining(family),
                () => ReferenceComparer.Ranged(capabilities.PartMass)),
            ProcessComparisonProperties.DraftAngle => Applicable(
                !known || ProcessFamilyTraits.UsesAMouldOrDie(family),
                () => ReferenceComparer.Ranged(capabilities.DraftAngle)),
            ProcessComparisonProperties.ProcessTemperature => Applicable(
                !known || ProcessFamilyTraits.HasProcessTemperature(family),
                () => ReferenceComparer.Ranged(capabilities.ProcessTemperature)),
            ProcessComparisonProperties.CycleTime => ReferenceComparer.Ranged(capabilities.CycleTime),

            ProcessComparisonProperties.ProductionScales => definition.ProductionScales.Count == 0
                ? ReferenceComparisonCell.NotRecorded
                : ReferenceComparisonCell.Text(string.Join(", ", definition.ProductionScales)),
            ProcessComparisonProperties.MaterialCount => Count(definition.MaterialCompatibility.Count),
            ProcessComparisonProperties.ConstraintCount => Count(definition.Constraints.Count),
            ProcessComparisonProperties.ValidationState => ReferenceComparisonCell.Text(process.ValidationState.ToString()),
            _ => ReferenceComparisonCell.NotRecorded,
        };
    }

    private static ReferenceComparisonCell Applicable(bool applies, Func<ReferenceComparisonCell> cell) =>
        applies ? cell() : ReferenceComparisonCell.NotApplicable;

    /// <summary>
    /// A count is always recorded, including when it is zero: "this record
    /// names no materials" is a fact the record states, unlike a band
    /// nobody published.
    /// </summary>
    private static ReferenceComparisonCell Count(int count) =>
        new(ReferencePropertyAvailability.Recorded, count.ToString(), count);
}
