using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>Evaluates a <see cref="StandardQuery"/> against one standard record.</summary>
/// <remarks>A pure predicate kept out of <see cref="StandardCatalog"/> so query semantics can be tested without a store.</remarks>
internal static class StandardQueryEvaluator
{
    public static bool Matches(IReferenceRecord<StandardDefinition> record, StandardQuery query)
    {
        var definition = record.Definition;

        if (!Contains(definition.Designation, query.DesignationContains))
            return false;

        if (!Contains(definition.Title, query.TitleContains))
            return false;

        if (!Contains(definition.ScopeSummary, query.ScopeSummaryContains))
            return false;

        if (query.BodyCode is not null
            && !string.Equals(definition.Body.Code, query.BodyCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.BodyKinds.Count > 0 && !query.BodyKinds.Contains(definition.Body.Kind))
            return false;

        if (query.Classifications.Count > 0 && !query.Classifications.Contains(definition.Classification))
            return false;

        if (query.Disciplines.Count > 0 && !definition.Disciplines.Any(query.Disciplines.Contains))
            return false;

        if (query.PublicationStatuses.Count > 0 && !query.PublicationStatuses.Contains(definition.PublicationStatus))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        if (query.Edition is not null
            && (definition.Edition is null || !string.Equals(definition.Edition.Trim(), query.Edition.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;

        // A standard with no recorded publication date cannot satisfy a
        // bound on one: an unrecorded date is never read as a date.
        if (query.PublishedOnOrAfter is { } from && (definition.PublicationDate is null || definition.PublicationDate < from))
            return false;

        if (query.PublishedOnOrBefore is { } to && (definition.PublicationDate is null || definition.PublicationDate > to))
            return false;

        if (query.EquivalentToDesignationContaining is { } equivalent
            && !definition.Equivalences.Any(e => e.Designation.Contains(equivalent, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.NormativelyReferencesDesignationContaining is { } referenced
            && !definition.NormativeReferences.Any(r => r.Designation.Contains(referenced, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.ReplacesDesignationContaining is { } replaced
            && !definition.ReplacesDesignations.Any(d => d.Contains(replaced, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static bool Contains(string? candidate, string? expected) =>
        expected is null || (candidate is not null && candidate.Contains(expected, StringComparison.OrdinalIgnoreCase));
}
