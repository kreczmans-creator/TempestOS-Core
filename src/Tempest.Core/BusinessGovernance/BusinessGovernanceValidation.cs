using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.BusinessGovernance;

/// <summary>
/// The diagnostic codes every `P07` validation service reports, for the
/// governance facts all seven work packages share.
/// </summary>
/// <remarks>
/// One series rather than seven copies. Each package keeps its own series
/// for its own domain semantics; nobody re-implements "does this record
/// name an owner?".
/// </remarks>
public static class BusinessGovernanceRules
{
    /// <summary>The record does not say how sensitive it is, so nobody handling it knows how to.</summary>
    public const string ClassificationShouldBeStated = "TEMPEST-BGV-001";

    /// <summary>The record has no review scheduled, so nothing will prompt anybody to look at it again.</summary>
    public const string ReviewShouldBeScheduled = "TEMPEST-BGV-002";

    /// <summary>The record's review is already overdue.</summary>
    public const string ReviewIsOverdue = "TEMPEST-BGV-003";

    /// <summary>The record asserts something with no evidence behind it at all.</summary>
    public const string EvidenceShouldBeRecorded = "TEMPEST-BGV-004";

    /// <summary>Evidence is described but cannot be retrieved: no document, no record, no external reference.</summary>
    public const string EvidenceShouldBeLocatable = "TEMPEST-BGV-005";

    /// <summary>An authority the record itself says it needs has not been exercised.</summary>
    public const string OutstandingAuthorityRequired = "TEMPEST-BGV-006";

    /// <summary>An outstanding authority names nobody who is expected to exercise it.</summary>
    public const string OutstandingAuthorityHasNoHolder = "TEMPEST-BGV-007";

    /// <summary>An outstanding authority is past the date it was needed by.</summary>
    public const string OutstandingAuthorityIsOverdue = "TEMPEST-BGV-008";

    /// <summary>An effective period has already ended, so the record no longer applies.</summary>
    public const string EffectivePeriodHasExpired = "TEMPEST-BGV-009";

    /// <summary>An effective period has no recorded end where one would be expected.</summary>
    public const string EffectivePeriodHasNoEnd = "TEMPEST-BGV-010";
}

/// <summary>
/// The shared checks every `P07` validation service runs over the
/// governance facts a record carries.
/// </summary>
/// <remarks>
/// A static helper rather than a base class, because each package's
/// validation service already derives from
/// <see cref="Tempest.Core.ReferenceData.ReferenceValidationService{TDefinition}"/>
/// and C# has one base. The helper takes the diagnostic lists the caller
/// is already filling, so a package's own domain checks and these shared
/// ones land in the same result.
/// </remarks>
public static class BusinessGovernanceValidator
{
    /// <summary>
    /// Checks <paramref name="facts"/> and appends what it finds to
    /// <paramref name="errors"/> and <paramref name="warnings"/>.
    /// </summary>
    /// <remarks>
    /// Everything here is a warning rather than an error, with one
    /// exception. A record with no classification, no review and no
    /// evidence is a poor record, not an invalid one, and refusing to
    /// register it would push people to work outside the system. An
    /// outstanding authority that is past its own due date is escalated to
    /// an error, because that is a governance failure the organisation has
    /// already committed to noticing.
    /// </remarks>
    /// <param name="subject">What the record is, for the diagnostic text — "Contract template 'CT-1'".</param>
    /// <param name="facts">The governance facts to check.</param>
    /// <param name="asAt">The date overdue checks are made against.</param>
    /// <param name="errors">The error list to append to.</param>
    /// <param name="warnings">The warning list to append to.</param>
    /// <param name="expectEvidence">Whether the record is the kind that should always have evidence behind it.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="subject"/> is empty or whitespace.</exception>
    public static void Evaluate(
        string subject,
        BusinessGovernanceFacts facts,
        DateOnly asAt,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        bool expectEvidence = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);

        if (facts.Classification == ConfidentialityClassification.Unclassified)
            warnings.Add(Diagnostic(
                BusinessGovernanceRules.ClassificationShouldBeStated,
                $"{subject} does not say how sensitive it is, so nobody handling it can tell whether it may leave the organisation."));

        if (!facts.Review.IsScheduled)
            warnings.Add(Diagnostic(
                BusinessGovernanceRules.ReviewShouldBeScheduled,
                $"{subject} has no review scheduled. Nothing will prompt anybody to check whether it is still true."));
        else if (facts.Review.IsOverdueAt(asAt))
            warnings.Add(Diagnostic(
                BusinessGovernanceRules.ReviewIsOverdue,
                $"{subject} was due for review on {facts.Review.NextReviewDue:O} and has not been reviewed since "
                + (facts.Review.HasBeenReviewed ? $"{facts.Review.LastReviewedOn:O}." : "it was created.")));

        if (expectEvidence && !facts.HasEvidence)
            warnings.Add(Diagnostic(
                BusinessGovernanceRules.EvidenceShouldBeRecorded,
                $"{subject} records no evidence. What it asserts rests on nothing a reader can check."));

        foreach (var evidence in facts.Evidence.Where(e => !e.IsLocatable))
            warnings.Add(Diagnostic(
                BusinessGovernanceRules.EvidenceShouldBeLocatable,
                $"{subject} cites evidence \"{evidence.Description}\" that is neither held in TempestOS nor identified by an "
                + "external reference, so nobody can retrieve it."));

        foreach (var requirement in facts.OutstandingAuthorities)
        {
            if (requirement.IsOverdueAt(asAt))
                errors.Add(Diagnostic(
                    BusinessGovernanceRules.OutstandingAuthorityIsOverdue,
                    $"{subject} needed {Describe(requirement.Kind)} by {requirement.RequiredBy:O} and has not had it: "
                    + requirement.Description));
            else
                warnings.Add(Diagnostic(
                    BusinessGovernanceRules.OutstandingAuthorityRequired,
                    $"{subject} still needs {Describe(requirement.Kind)}: {requirement.Description}"));

            if (!requirement.HasNamedHolder)
                warnings.Add(Diagnostic(
                    BusinessGovernanceRules.OutstandingAuthorityHasNoHolder,
                    $"{subject} needs {Describe(requirement.Kind)} but names nobody expected to give it, so it is nobody's task."));
        }
    }

    /// <summary>Checks an effective period and appends what it finds to <paramref name="warnings"/>.</summary>
    /// <param name="subject">What the record is, for the diagnostic text.</param>
    /// <param name="period">The period to check. <see langword="null"/> where the record has none.</param>
    /// <param name="asAt">The date expiry is judged against.</param>
    /// <param name="warnings">The warning list to append to.</param>
    /// <param name="expectAnEnd">Whether a record of this kind should always state when it stops applying.</param>
    /// <exception cref="ArgumentNullException"><paramref name="warnings"/> is <see langword="null"/>.</exception>
    public static void EvaluatePeriod(
        string subject,
        EffectivePeriod? period,
        DateOnly asAt,
        List<IValidationDiagnostic> warnings,
        bool expectAnEnd = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(warnings);

        if (period is null)
            return;

        if (period.HasExpiredBy(asAt))
            warnings.Add(Diagnostic(
                BusinessGovernanceRules.EffectivePeriodHasExpired,
                $"{subject} stopped applying on {period.To:O} and is still registered as current."));

        if (expectAnEnd && period.IsOpenEnded)
            warnings.Add(Diagnostic(
                BusinessGovernanceRules.EffectivePeriodHasNoEnd,
                $"{subject} records no end date. A record of this kind normally has one, so this is more likely a gap than an "
                + "open-ended commitment — and the two are not the same."));
    }

    private static IValidationDiagnostic Diagnostic(string code, string message) => new ValidationDiagnostic(code, message);

    private static string Describe(BusinessAuthorityKind kind) => kind switch
    {
        BusinessAuthorityKind.Verification => "verification",
        BusinessAuthorityKind.InternalApproval => "internal approval",
        BusinessAuthorityKind.CommercialCommitment => "a commercial commitment",
        BusinessAuthorityKind.RiskAcceptance => "a risk acceptance",
        BusinessAuthorityKind.ExpenditureAuthorisation => "expenditure authorisation",
        BusinessAuthorityKind.LegalDetermination => "a legal determination",
        BusinessAuthorityKind.AccountingDetermination => "an accounting determination",
        BusinessAuthorityKind.DirectorDecision => "a director's decision",
        _ => "an unstated authority",
    };
}
