using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Components;

/// <summary>Evaluates a <see cref="ComponentQuery"/> against one component record.</summary>
/// <remarks>A pure predicate kept out of <see cref="ComponentCatalog"/> so query semantics can be tested without a store.</remarks>
internal static class ComponentQueryEvaluator
{
    public static bool Matches(IReferenceRecord<ComponentDefinition> record, ComponentQuery query)
    {
        var definition = record.Definition;

        if (query.DesignationContains is not null
            && !definition.Designation.Contains(query.DesignationContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!MatchesExactly(definition.Manufacturer, query.Manufacturer))
            return false;

        if (query.Families.Count > 0 && !query.Families.Contains(definition.Family))
            return false;

        if (query.Groups.Count > 0 && !query.Groups.Contains(definition.Group))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        if (query.MaterialId is not null && !string.Equals(definition.MaterialId, query.MaterialId, StringComparison.Ordinal))
            return false;

        if (query.CitesStandardContaining is not null
            && !definition.Standards.Any(s => s.Designation.Contains(query.CitesStandardContaining, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!Within(definition.Dimensions.BoreDiameter, query.BoreDiameterMinimum, query.BoreDiameterMaximum))
            return false;

        if (!Within(definition.Dimensions.OutsideDiameter, query.OutsideDiameterMinimum, query.OutsideDiameterMaximum))
            return false;

        if (!Within(definition.Spring?.Rate, query.SpringRateMinimum, query.SpringRateMaximum))
            return false;

        if (!Within(definition.Spring?.FreeLength, query.FreeLengthMinimum, query.FreeLengthMaximum))
            return false;

        // A component with no gear detail cannot satisfy a gear criterion.
        if (query.NumberOfTeeth is { } teeth && definition.Gear?.NumberOfTeeth != teeth)
            return false;

        if (!Within(definition.Gear?.Module, query.ModuleMinimum, query.ModuleMaximum))
            return false;

        if (query.HelixHand is { } hand && definition.Gear?.HelixHand != hand)
            return false;

        if (query.DriveProfileDesignation is not null
            && !MatchesExactly(definition.DriveElement?.ProfileDesignation, query.DriveProfileDesignation))
            return false;

        if (!Within(definition.Ratings.RatedTorque, query.RatedTorqueMinimum, maximum: null))
            return false;

        if (!Within(definition.Ratings.MaximumSpeed, query.MaximumSpeedMinimum, maximum: null))
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

        // A component that does not record the value cannot satisfy a bound
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
