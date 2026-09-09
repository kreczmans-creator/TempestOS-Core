using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets;

/// <summary>
/// How complete and how trustworthy an engineering asset is, as a second
/// axis from its lifecycle state.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ReferenceValidationState"/> says how far a record got
/// through governance. This says whether the asset is fit to be used,
/// which is a different question with a different answer: a Released
/// template whose effective period ended last year is governed
/// impeccably and <see cref="Superseded"/> in substance.
/// </para>
/// <para>
/// Seven values because the domain has seven materially different
/// answers, and §34 forbids collapsing them into a Boolean. In
/// particular, <see cref="Incomplete"/> and <see cref="Unverified"/> are
/// distinct: something is missing, versus everything is present and
/// nobody has checked it.
/// </para>
/// </remarks>
public enum AssetStanding
{
    /// <summary>The asset contradicts itself and cannot be used as it stands.</summary>
    Invalid,

    /// <summary>Something the asset needs is missing.</summary>
    Incomplete,

    /// <summary>Complete, and nobody has checked it.</summary>
    Unverified,

    /// <summary>Complete and checked, but past its own effective period.</summary>
    Stale,

    /// <summary>Complete, checked, and current.</summary>
    Verified,

    /// <summary>The question does not arise for this asset.</summary>
    NotApplicable,

    /// <summary>Replaced by something later.</summary>
    Superseded
}

/// <summary>What <see cref="AssetStanding"/> means, and what it does not.</summary>
public static class AssetStandings
{
    /// <summary>The standings, weakest first.</summary>
    public static IReadOnlyList<AssetStanding> WeakestFirst { get; } =
    [
        AssetStanding.Invalid,
        AssetStanding.Incomplete,
        AssetStanding.Unverified,
        AssetStanding.Stale,
        AssetStanding.Superseded,
        AssetStanding.NotApplicable,
        AssetStanding.Verified,
    ];

    /// <summary>Whether the asset is fit to be relied on for engineering work.</summary>
    /// <remarks>
    /// True only for <see cref="AssetStanding.Verified"/>. An unverified
    /// asset may be perfectly correct; it has simply not been checked,
    /// and that is not the same thing.
    /// </remarks>
    public static bool IsUsable(AssetStanding standing) => standing == AssetStanding.Verified;

    /// <summary>Whether somebody ought to look at it.</summary>
    public static bool NeedsAttention(AssetStanding standing) =>
        standing is AssetStanding.Invalid or AssetStanding.Incomplete or AssetStanding.Stale;

    /// <summary>How strong a standing is, for ordering. Higher is stronger.</summary>
    public static int Rank(AssetStanding standing) => standing switch
    {
        AssetStanding.Invalid => 0,
        AssetStanding.Incomplete => 1,
        AssetStanding.Unverified => 2,
        AssetStanding.Stale => 3,
        AssetStanding.Superseded => 4,
        AssetStanding.NotApplicable => 5,
        AssetStanding.Verified => 6,
        _ => 0
    };

    /// <summary>
    /// The weakest standing in a set, which is the standing of the whole.
    /// </summary>
    /// <remarks>
    /// An empty set is <see cref="AssetStanding.Incomplete"/>, never
    /// <see cref="AssetStanding.Verified"/>. Nothing having been checked
    /// is not the same as everything having passed.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="standings"/> is <see langword="null"/>.</exception>
    public static AssetStanding Weakest(IEnumerable<AssetStanding> standings)
    {
        ArgumentNullException.ThrowIfNull(standings);

        var considered = standings.Where(s => s != AssetStanding.NotApplicable).ToList();

        return considered.Count == 0
            ? AssetStanding.Incomplete
            : considered.OrderBy(Rank).First();
    }
}

/// <summary>The diagnostic codes every P05 library can report.</summary>
/// <remarks>
/// Shared rules only. Each package adds its own under its own prefix
/// rather than extending this one, so a diagnostic code says which
/// library raised it.
/// </remarks>
public static class AssetGovernanceRules
{
    /// <summary>Nobody is named as accountable for the asset.</summary>
    public const string OwnerNotNamed = "TEMPEST-EAG-001";

    /// <summary>Nobody is named as having produced the asset.</summary>
    public const string AuthorNotNamed = "TEMPEST-EAG-002";

    /// <summary>Nobody has reviewed the asset.</summary>
    public const string NotReviewed = "TEMPEST-EAG-003";

    /// <summary>A review left findings nobody has closed.</summary>
    public const string OutstandingReviewFindings = "TEMPEST-EAG-004";

    /// <summary>The same person authored and reviewed the asset.</summary>
    /// <remarks>
    /// A warning, never an error. In a small organisation it is often
    /// unavoidable and everybody knows it; what must not happen is that
    /// it goes unrecorded.
    /// </remarks>
    public const string SelfReviewed = "TEMPEST-EAG-005";

    /// <summary>The asset is recorded as approved but names nobody who approved it.</summary>
    public const string ApprovalNotAttributable = "TEMPEST-EAG-006";

    /// <summary>The asset has run past its own effective period.</summary>
    public const string AssetHasExpired = "TEMPEST-EAG-007";

    /// <summary>A piece of evidence cannot be located.</summary>
    public const string EvidenceNotLocatable = "TEMPEST-EAG-008";

    /// <summary>The asset rests entirely on judgement, with nothing independent behind it.</summary>
    public const string NoIndependentEvidence = "TEMPEST-EAG-009";

    /// <summary>Two elements of the asset share one reference.</summary>
    public const string DuplicateReference = "TEMPEST-EAG-010";

    /// <summary>The asset pins a record that has since been superseded.</summary>
    public const string PinnedSourceSuperseded = "TEMPEST-EAG-011";

    /// <summary>The asset pins a record the library no longer holds.</summary>
    public const string PinnedSourceMissing = "TEMPEST-EAG-012";
}

/// <summary>
/// The governance checks every P05 library shares, so no package
/// restates them.
/// </summary>
/// <remarks>
/// Static helpers rather than a base class: the five asset types share
/// these facts and share no hierarchy, and each library's own validation
/// service already derives from
/// <see cref="ReferenceValidationService{TDefinition}"/>.
/// </remarks>
public static class AssetGovernanceValidation
{
    /// <summary>Evaluates the governance facts common to every engineering asset.</summary>
    /// <param name="governance">The facts to evaluate.</param>
    /// <param name="subject">How to name the asset in a diagnostic.</param>
    /// <param name="errors">Errors found, appended to.</param>
    /// <param name="warnings">Warnings found, appended to.</param>
    /// <param name="requireIndependentEvidence">Whether resting entirely on judgement is worth reporting for this asset kind.</param>
    /// <exception cref="ArgumentNullException"><paramref name="governance"/>, <paramref name="errors"/> or <paramref name="warnings"/> is <see langword="null"/>.</exception>
    public static void Evaluate(
        AssetGovernanceFacts governance,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        bool requireIndependentEvidence = false)
    {
        ArgumentNullException.ThrowIfNull(governance);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);

        if (governance.Ownership is null)
            warnings.Add(Diagnostic(
                AssetGovernanceRules.OwnerNotNamed,
                $"{subject} names nobody accountable for it."));

        if (governance.Authorship is null)
            warnings.Add(Diagnostic(
                AssetGovernanceRules.AuthorNotNamed,
                $"{subject} names nobody who produced it."));

        if (governance.Reviews.Count == 0)
            warnings.Add(Diagnostic(
                AssetGovernanceRules.NotReviewed,
                $"{subject} has not been reviewed by anybody."));

        if (governance.HasOutstandingFindings)
            warnings.Add(Diagnostic(
                AssetGovernanceRules.OutstandingReviewFindings,
                $"{subject} was reviewed with findings that nobody has recorded as closed."));

        if (governance.Authorship is { } author
            && governance.Reviews.Any(r => string.Equals(r.ReviewedByPrincipalId, author.AuthoredByPrincipalId, StringComparison.Ordinal)))
            warnings.Add(Diagnostic(
                AssetGovernanceRules.SelfReviewed,
                $"{subject} was reviewed by the person who wrote it. Often unavoidable; recorded so it is never invisible."));

        foreach (var evidence in governance.Evidence.Where(e => !e.IsLocatable))
            warnings.Add(Diagnostic(
                AssetGovernanceRules.EvidenceNotLocatable,
                $"{subject} offers evidence — \"{evidence.Description}\" — that names no document, record or reference, "
                + "so nobody can go and check it."));

        if (requireIndependentEvidence
            && governance.Evidence.Count > 0
            && !governance.Evidence.Any(e => e.IsIndependent))
            warnings.Add(Diagnostic(
                AssetGovernanceRules.NoIndependentEvidence,
                $"{subject} rests entirely on internal records and judgement, with nothing independent behind it."));
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
            errors.Add(Diagnostic(AssetGovernanceRules.DuplicateReference, $"{message} '{duplicate}'."));
    }

    /// <summary>Builds a diagnostic.</summary>
    public static IValidationDiagnostic Diagnostic(string code, string message) => new ValidationDiagnostic(code, message);
}
