using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>Evaluates a <see cref="ConstantQuery"/> against one constant record.</summary>
/// <remarks>A pure predicate kept out of <see cref="ConstantCatalog"/> so query semantics can be tested without a store.</remarks>
internal static class ConstantQueryEvaluator
{
    public static bool Matches(IReferenceRecord<ConstantDefinition> record, ConstantQuery query)
    {
        var definition = record.Definition;

        // Case-sensitive on purpose: a constant's symbol is
        // case-significant, and folding it here would find the wrong
        // constant.
        if (query.SymbolContains is { } symbol
            && !definition.Symbol.Contains(symbol, StringComparison.Ordinal)
            && !definition.AlternativeSymbols.Any(alternative => alternative.Contains(symbol, StringComparison.Ordinal)))
            return false;

        if (query.NameContains is not null && !definition.Name.Contains(query.NameContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Categories.Count > 0 && !query.Categories.Contains(definition.Category))
            return false;

        if (query.DimensionName is not null
            && (definition.Value is null || !string.Equals(definition.Value.DimensionName, query.DimensionName, StringComparison.Ordinal)))
            return false;

        if (query.UncertaintyKinds.Count > 0 && !query.UncertaintyKinds.Contains(definition.Uncertainty.Kind))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        if (query.CitesStandardContaining is not null
            && !definition.Standards.Any(s => s.Designation.Contains(query.CitesStandardContaining, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.ApplicabilityContains is not null
            && (definition.Applicability is null
                || !definition.Applicability.Contains(query.ApplicabilityContains, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }
}
