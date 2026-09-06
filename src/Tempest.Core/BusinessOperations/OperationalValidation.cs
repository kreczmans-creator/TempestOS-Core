using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.BusinessOperations;

/// <summary>The diagnostic codes every P04 library can report.</summary>
public static class OperationalGovernanceRules
{
    /// <summary>An outstanding record names nobody responsible for it.</summary>
    public const string RecordHasNoOwner = "TEMPEST-BOG-001";

    /// <summary>An outstanding record names no date by which it is needed.</summary>
    public const string RecordHasNoDueDate = "TEMPEST-BOG-002";

    /// <summary>A closed record carries no closing date, or a cancelled one no reason.</summary>
    public const string RecordIsIncompletelyClosed = "TEMPEST-BOG-003";

    /// <summary>The record is past its own due date.</summary>
    public const string RecordIsOverdue = "TEMPEST-BOG-004";

    /// <summary>The record names nobody who raised it.</summary>
    public const string RecordHasNoOriginator = "TEMPEST-BOG-005";

    /// <summary>A party is named in text but resolves to no governed record.</summary>
    /// <remarks>
    /// A warning and never an error. A quotation arrives from a company
    /// nobody has entered yet, and refusing to record it until somebody
    /// does is how operational software becomes the thing people work
    /// around.
    /// </remarks>
    public const string PartyIsUnresolved = "TEMPEST-BOG-006";

    /// <summary>A party the referenced library does not hold.</summary>
    public const string PartyMustResolve = "TEMPEST-BOG-007";

    /// <summary>Two elements of the record share one reference.</summary>
    public const string DuplicateReference = "TEMPEST-BOG-008";

    /// <summary>A record the entry pins has since been superseded.</summary>
    public const string PinnedSourceSuperseded = "TEMPEST-BOG-009";

    /// <summary>A record was closed while something it depends on is still open.</summary>
    public const string ClosedWithOutstandingDependency = "TEMPEST-BOG-010";
}

/// <summary>The operational checks every P04 library shares.</summary>
/// <remarks>
/// Static helpers rather than a base class, following `P05`'s
/// <c>AssetGovernanceValidation</c>: the `P04` record types share these
/// facts and share no hierarchy.
/// </remarks>
public static class OperationalValidation
{
    /// <summary>Evaluates the facts common to every operational record.</summary>
    /// <param name="facts">The facts to evaluate.</param>
    /// <param name="subject">How to name the record in a diagnostic.</param>
    /// <param name="asAt">The date overdue is judged against.</param>
    /// <param name="errors">Errors found, appended to.</param>
    /// <param name="warnings">Warnings found, appended to.</param>
    /// <param name="requireDueDate">Whether an outstanding record of this kind ought to carry a due date.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static void Evaluate(
        OperationalFacts facts,
        string subject,
        DateOnly asAt,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        bool requireDueDate = true)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);

        if (facts.IsOutstanding && !facts.IsOwned)
            warnings.Add(Diagnostic(
                OperationalGovernanceRules.RecordHasNoOwner,
                $"{subject} is {facts.State} and names nobody responsible for it."));

        if (requireDueDate && facts.IsOutstanding && facts.DueBy is null)
            warnings.Add(Diagnostic(
                OperationalGovernanceRules.RecordHasNoDueDate,
                $"{subject} is outstanding with no date by which it is needed."));

        if (facts.IsIncompletelyClosed)
            errors.Add(Diagnostic(
                OperationalGovernanceRules.RecordIsIncompletelyClosed,
                facts.State == OperationalState.Closed
                    ? $"{subject} is closed and carries no closing date."
                    : $"{subject} was cancelled without a stated reason."));

        if (facts.DaysOverdueAt(asAt) is { } days)
            warnings.Add(Diagnostic(
                OperationalGovernanceRules.RecordIsOverdue,
                $"{subject} was due on {facts.DueBy:O} and is {days} day(s) overdue."));

        if (string.IsNullOrWhiteSpace(facts.RaisedByPrincipalId))
            warnings.Add(Diagnostic(
                OperationalGovernanceRules.RecordHasNoOriginator,
                $"{subject} names nobody who raised it."));
    }

    /// <summary>Reports a party carried as a name rather than a governed record.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="party"/> or <paramref name="warnings"/> is <see langword="null"/>.</exception>
    public static void EvaluateParty(
        PartyReference party,
        string subject,
        List<IValidationDiagnostic> warnings)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(warnings);

        if (!party.IsResolved)
            warnings.Add(Diagnostic(
                OperationalGovernanceRules.PartyIsUnresolved,
                $"{subject} names \"{party.DisplayName}\" in text and points at no governed record, so nothing else "
                + "the organisation knows about that party is reachable from here."));
    }

    /// <summary>Reports a reference appearing more than once where it must key a collection.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="references"/> or <paramref name="errors"/> is <see langword="null"/>.</exception>
    public static void EvaluateDuplicateReferences(
        IEnumerable<string> references,
        string message,
        List<IValidationDiagnostic> errors)
    {
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(errors);

        var duplicates = references
            .GroupBy(r => r, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(r => r, StringComparer.Ordinal);

        foreach (var duplicate in duplicates)
            errors.Add(Diagnostic(OperationalGovernanceRules.DuplicateReference, $"{message} '{duplicate}'."));
    }

    /// <summary>Builds a diagnostic.</summary>
    public static IValidationDiagnostic Diagnostic(string code, string message) => new ValidationDiagnostic(code, message);
}
