using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Fasteners;

/// <summary>Evaluates a <see cref="FastenerQuery"/> against one fastener record.</summary>
/// <remarks>A pure predicate kept out of <see cref="FastenerCatalog"/> so query semantics can be tested without a store.</remarks>
internal static class FastenerQueryEvaluator
{
    public static bool Matches(IReferenceRecord<FastenerDefinition> record, FastenerQuery query)
    {
        var definition = record.Definition;

        if (query.DesignationContains is not null
            && !definition.Designation.Contains(query.DesignationContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!MatchesExactly(definition.Manufacturer, query.Manufacturer))
            return false;

        if (query.Families.Count > 0 && !query.Families.Contains(definition.Family))
            return false;

        if (query.HeadTypes.Count > 0 && !query.HeadTypes.Contains(definition.HeadType))
            return false;

        if (query.DriveTypes.Count > 0 && !query.DriveTypes.Contains(definition.DriveType))
            return false;

        // An unthreaded fastener cannot satisfy a thread criterion. That is
        // not the same as failing it: it is simply not a candidate.
        if (query.ThreadSystems.Count > 0 && (definition.Thread is null || !query.ThreadSystems.Contains(definition.Thread.System)))
            return false;

        if (query.ThreadDesignation is not null
            && (definition.Thread is null
                || !string.Equals(definition.Thread.Designation, query.ThreadDesignation.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.Handedness is { } handedness && (definition.Thread is null || definition.Thread.Handedness != handedness))
            return false;

        if (!MatchesExactly(definition.Mechanical.PropertyClass, query.PropertyClass))
            return false;

        if (query.FinishDesignation is not null && !MatchesExactly(definition.Finish?.Designation, query.FinishDesignation))
            return false;

        if (query.MaterialId is not null && !string.Equals(definition.MaterialId, query.MaterialId, StringComparison.Ordinal))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        if (query.CitesStandardContaining is not null
            && !definition.Standards.Any(s => s.Designation.Contains(query.CitesStandardContaining, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.RecordsTighteningTorque is { } wantsTorque && definition.TorqueReferences.Count > 0 != wantsTorque)
            return false;

        if (!Within(definition.Thread?.NominalDiameter, query.NominalDiameterMinimum, query.NominalDiameterMaximum))
            return false;

        if (!Within(definition.Dimensions.NominalLength, query.NominalLengthMinimum, query.NominalLengthMaximum))
            return false;

        if (!Within(definition.Dimensions.WidthAcrossFlats, query.WidthAcrossFlatsMinimum, query.WidthAcrossFlatsMaximum))
            return false;

        if (!Within(definition.Mechanical.ProofStrength, query.ProofStrengthMinimum, maximum: null))
            return false;

        if (!Within(definition.Mechanical.TensileStrength, query.TensileStrengthMinimum, maximum: null))
            return false;

        return true;
    }

    private static bool Within<TDimension>(
        ReferenceValue<TDimension>? recorded,
        Quantity<TDimension>? minimum,
        Quantity<TDimension>? maximum)
        where TDimension : IDimension
    {
        if (minimum is null && maximum is null)
            return true;

        // A fastener that does not record the value cannot satisfy a bound
        // on it — an unrecorded value is never treated as zero, and never
        // assumed to fall inside a range.
        if (recorded is null)
            return false;

        var value = recorded.CanonicalValue;

        if (minimum is { } min && value < min.BaseValue)
            return false;

        if (maximum is { } max && value > max.BaseValue)
            return false;

        return true;
    }

    private static bool MatchesExactly(string? candidate, string? expected) =>
        expected is null || (candidate is not null && string.Equals(candidate.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase));
}
