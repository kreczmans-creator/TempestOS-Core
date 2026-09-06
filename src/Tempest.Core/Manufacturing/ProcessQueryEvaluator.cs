using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Manufacturing;

/// <summary>Evaluates a <see cref="ProcessQuery"/> against one process record.</summary>
/// <remarks>A pure predicate kept out of <see cref="ProcessCatalog"/> so query semantics can be tested without a store.</remarks>
internal static class ProcessQueryEvaluator
{
    public static bool Matches(IReferenceRecord<ProcessDefinition> record, ProcessQuery query)
    {
        var definition = record.Definition;

        if (query.NameContains is not null && !definition.Name.Contains(query.NameContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Families.Count > 0 && !query.Families.Contains(definition.Family))
            return false;

        if (query.Groups.Count > 0 && !query.Groups.Contains(definition.Group))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        // A source that explicitly said a material is not processed this
        // way must never make the process a match for that material.
        if (query.ProcessesMaterialFamily is { } family
            && !definition.MaterialCompatibility.Any(entry => entry.Family == family && IsPositive(entry.Suitability)))
            return false;

        if (query.ProcessesMaterialId is { } materialId
            && !definition.MaterialCompatibility.Any(entry =>
                string.Equals(entry.MaterialId, materialId, StringComparison.Ordinal) && IsPositive(entry.Suitability)))
            return false;

        if (query.ProductionScales.Count > 0 && !definition.ProductionScales.Any(query.ProductionScales.Contains))
            return false;

        if (query.ConstraintKinds.Count > 0 && !definition.Constraints.Any(c => query.ConstraintKinds.Contains(c.Kind)))
            return false;

        if (query.CitesStandardContaining is not null
            && !definition.Standards.Any(s => s.Designation.Contains(query.CitesStandardContaining, StringComparison.OrdinalIgnoreCase)))
            return false;

        var capabilities = definition.Capabilities;

        if (!BandContains(capabilities.AchievableTolerance, query.ToleranceBandContains))
            return false;

        if (!BandContains(capabilities.SurfaceRoughness, query.SurfaceRoughnessBandContains))
            return false;

        if (!BandContains(capabilities.WallThickness, query.WallThicknessBandContains))
            return false;

        if (!BandContains(capabilities.PartSize, query.PartSizeBandContains))
            return false;

        if (!BandContains(capabilities.PartMass, query.PartMassBandContains))
            return false;

        return true;
    }

    private static bool IsPositive(ProcessMaterialSuitability suitability) =>
        suitability is ProcessMaterialSuitability.Suitable or ProcessMaterialSuitability.ConditionallySuitable;

    private static bool BandContains<TDimension>(ReferenceRange<TDimension>? band, Quantity<TDimension>? candidate)
        where TDimension : IDimension
    {
        if (candidate is not { } value)
            return true;

        // A process that published no band for this capability is not a
        // process whose band contains the value — an unrecorded band is
        // never read as unbounded.
        return band is { IsRecorded: true } && band.Contains(value);
    }
}
