using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Materials;

/// <summary>Evaluates a <see cref="MaterialQuery"/> against one material record.</summary>
/// <remarks>A pure predicate kept out of <see cref="MaterialCatalog"/> so query semantics can be tested without a store.</remarks>
internal static class MaterialQueryEvaluator
{
    public static bool Matches(IReferenceRecord<MaterialDefinition> record, MaterialQuery query)
    {
        var definition = record.Definition;

        if (query.NameContains is not null && !definition.Name.Contains(query.NameContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.DesignationContains is not null
            && (definition.Designation is null || !definition.Designation.Contains(query.DesignationContains, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!MatchesExactly(definition.Grade, query.Grade))
            return false;

        if (!MatchesExactly(definition.Condition, query.Condition))
            return false;

        if (!MatchesExactly(definition.Supplier, query.Supplier))
            return false;

        if (query.Families.Count > 0 && !query.Families.Contains(definition.Family))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        if (query.CitesStandardContaining is not null
            && !definition.Standards.Any(s => s.Designation.Contains(query.CitesStandardContaining, StringComparison.OrdinalIgnoreCase)))
            return false;

        foreach (var required in query.RecordsProperties)
        {
            if (!definition.Properties.ContainsKey(required))
                return false;
        }

        if (!WithinRange(definition, MaterialPropertyNames.Density, query.DensityMinimum, query.DensityMaximum))
            return false;

        if (!WithinRange(definition, MaterialPropertyNames.YieldStrength, query.YieldStrengthMinimum, maximum: (Quantity<Pressure>?)null))
            return false;

        if (!WithinRange(definition, MaterialPropertyNames.UltimateTensileStrength, query.UltimateTensileStrengthMinimum, maximum: (Quantity<Pressure>?)null))
            return false;

        if (!WithinRange(definition, MaterialPropertyNames.YoungsModulus, query.YoungsModulusMinimum, query.YoungsModulusMaximum))
            return false;

        return true;
    }

    private static bool WithinRange<TDimension>(
        MaterialDefinition definition,
        string propertyName,
        Quantity<TDimension>? minimum,
        Quantity<TDimension>? maximum)
        where TDimension : IDimension
    {
        if (minimum is null && maximum is null)
            return true;

        // A material that does not record the property cannot satisfy a
        // bound on it — an unrecorded value is never treated as zero, and
        // never assumed to fall inside a range.
        if (!definition.Properties.TryGetValue(propertyName, out var property))
            return false;

        var value = property.CanonicalValue;

        if (minimum is { } min && value < min.BaseValue)
            return false;

        if (maximum is { } max && value > max.BaseValue)
            return false;

        return true;
    }

    private static bool MatchesExactly(string? candidate, string? expected) =>
        expected is null || (candidate is not null && string.Equals(candidate.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase));
}
