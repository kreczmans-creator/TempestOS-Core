using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Knowledge;

/// <summary>The diagnostic codes every P06 library can report.</summary>
public static class KnowledgeGovernanceRules
{
    /// <summary>The content does not say where it came from.</summary>
    public const string OriginNotStated = "TEMPEST-KNG-001";

    /// <summary>Content taken from an external source cites nothing.</summary>
    public const string ExternalContentIsUncited = "TEMPEST-KNG-002";

    /// <summary>A citation is too vague for anybody to find the work.</summary>
    /// <remarks>
    /// A title and an author are not enough. Editions differ, and an
    /// engineering value from the wrong edition is a wrong value.
    /// </remarks>
    public const string CitationIsNotSpecific = "TEMPEST-KNG-003";

    /// <summary>Nobody is named as having written the content.</summary>
    public const string AuthorNotNamed = "TEMPEST-KNG-004";

    /// <summary>Nobody has reviewed the content.</summary>
    public const string ContentIsUnreviewed = "TEMPEST-KNG-005";

    /// <summary>The same person wrote and reviewed the content.</summary>
    public const string ContentIsSelfReviewed = "TEMPEST-KNG-006";

    /// <summary>The content is marked reviewed but names no reviewer.</summary>
    public const string ReviewIsNotAttributable = "TEMPEST-KNG-007";

    /// <summary>Machine-generated content has not been reviewed by a person.</summary>
    /// <remarks>
    /// The finding `P06` exists to make unavoidable. Generated content is
    /// legitimate raw material and becomes knowledge only when somebody
    /// competent has checked it.
    /// </remarks>
    public const string MachineGeneratedContentIsUnreviewed = "TEMPEST-KNG-008";

    /// <summary>Fictional test content has been registered in a knowledge library.</summary>
    /// <remarks>
    /// An error wherever it appears outside a test. Fixtures exist to
    /// exercise the code and must never become reference content.
    /// </remarks>
    public const string FictionalContentRegistered = "TEMPEST-KNG-009";

    /// <summary>The content has run past its own stated validity.</summary>
    public const string ContentHasExpired = "TEMPEST-KNG-010";

    /// <summary>The content is deprecated but says nothing about why.</summary>
    public const string RetirementHasNoReason = "TEMPEST-KNG-011";

    /// <summary>The content is superseded but names no replacement.</summary>
    public const string SupersededWithoutReplacement = "TEMPEST-KNG-012";

    /// <summary>The content does not say which discipline it belongs to.</summary>
    public const string DisciplineNotStated = "TEMPEST-KNG-013";

    /// <summary>The content does not say how much grounding it assumes.</summary>
    public const string LevelNotStated = "TEMPEST-KNG-014";

    /// <summary>Two elements share one reference.</summary>
    public const string DuplicateReference = "TEMPEST-KNG-015";

    /// <summary>A record the content rests on has since been superseded.</summary>
    public const string PinnedSourceSuperseded = "TEMPEST-KNG-016";
}

/// <summary>
/// The provenance checks every P06 library shares.
/// </summary>
/// <remarks>
/// Static helpers rather than a base class: the five knowledge kinds share
/// these facts and share no hierarchy, and each library's own validation
/// service already derives from
/// <see cref="ReferenceData.ReferenceValidationService{TDefinition}"/>.
/// </remarks>
public static class KnowledgeGovernanceValidation
{
    /// <summary>Evaluates the provenance facts common to every piece of knowledge.</summary>
    /// <param name="provenance">The facts to evaluate.</param>
    /// <param name="applicability">Where the knowledge applies.</param>
    /// <param name="subject">How to name the content in a diagnostic.</param>
    /// <param name="asAt">The date staleness is judged against.</param>
    /// <param name="errors">Errors found, appended to.</param>
    /// <param name="warnings">Warnings found, appended to.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static void Evaluate(
        KnowledgeProvenance provenance,
        KnowledgeApplicability applicability,
        string subject,
        DateOnly asAt,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(applicability);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);

        if (provenance.IsFictional)
            errors.Add(Diagnostic(
                KnowledgeGovernanceRules.FictionalContentRegistered,
                $"{subject} is marked as fictional test content. Fixtures exist to exercise the code and must never "
                + "become reference knowledge."));

        if (provenance.Origin == KnowledgeOrigin.Unspecified)
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.OriginNotStated,
                $"{subject} does not say where it came from, so there is nothing to trust it on."));

        if (KnowledgeOrigins.RequiresCitation(provenance.Origin) && provenance.Citations.Count == 0)
            errors.Add(Diagnostic(
                KnowledgeGovernanceRules.ExternalContentIsUncited,
                $"{subject} is recorded as taken from an external source and cites nothing. An uncited external claim "
                + "cannot be checked."));

        foreach (var citation in provenance.Citations.Where(c => !c.IsSpecific))
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.CitationIsNotSpecific,
                $"{subject} cites \"{citation.Description}\" without an identifier, edition or year. Editions differ, "
                + "and a value from the wrong edition is a wrong value."));

        if (string.IsNullOrWhiteSpace(provenance.AuthoredByPrincipalId))
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.AuthorNotNamed,
                $"{subject} names nobody who wrote it."));

        if (provenance.ReviewState == KnowledgeReviewState.Unreviewed)
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.ContentIsUnreviewed,
                $"{subject} has not been reviewed, so it must not be presented as authoritative."));

        if (provenance.IsReviewed && string.IsNullOrWhiteSpace(provenance.ReviewedByPrincipalId))
            errors.Add(Diagnostic(
                KnowledgeGovernanceRules.ReviewIsNotAttributable,
                $"{subject} is marked reviewed and names no reviewer. An unattributable review is not a review."));

        if (provenance.IsSelfReviewed)
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.ContentIsSelfReviewed,
                $"{subject} was reviewed by the person who wrote it. Often unavoidable; recorded so it is never invisible."));

        if (provenance.IsMachineGenerated && !provenance.IsReviewed)
            errors.Add(Diagnostic(
                KnowledgeGovernanceRules.MachineGeneratedContentIsUnreviewed,
                $"{subject} was produced by a machine and no person has reviewed it. Generated content is raw material; "
                + "a person's review is what makes it knowledge."));

        if (provenance.ReviewState == KnowledgeReviewState.Deprecated
            && string.IsNullOrWhiteSpace(provenance.RetirementReason))
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.RetirementHasNoReason,
                $"{subject} is deprecated and says nothing about why. A reader finding it needs to know what changed."));

        if (provenance.ReviewState == KnowledgeReviewState.Superseded && provenance.SupersedesReference is null)
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.SupersededWithoutReplacement,
                $"{subject} is superseded but names no replacement."));

        if (applicability.Disciplines.Count == 0)
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.DisciplineNotStated,
                $"{subject} names no discipline, so it will be offered for every enquiry."));

        if (applicability.Level == KnowledgeLevel.Unspecified)
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.LevelNotStated,
                $"{subject} does not say how much grounding it assumes."));

        if (applicability.IsExpiredAt(asAt))
            warnings.Add(Diagnostic(
                KnowledgeGovernanceRules.ContentHasExpired,
                $"{subject} ran past its own validity on {applicability.Validity!.To:O}."));
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
            errors.Add(Diagnostic(KnowledgeGovernanceRules.DuplicateReference, $"{message} '{duplicate}'."));
    }

    /// <summary>Builds a diagnostic.</summary>
    public static IValidationDiagnostic Diagnostic(string code, string message) => new ValidationDiagnostic(code, message);
}
