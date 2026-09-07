using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence;

/// <summary>
/// The diagnostic codes every `P03` validation service reports, for the
/// commercial context all five work packages share.
/// </summary>
/// <remarks>
/// One series rather than five copies. Each package keeps its own series
/// for its own domain semantics; nobody re-implements "does this figure
/// say what quantity it applies to?".
/// </remarks>
public static class CommercialContextRules
{
    /// <summary>The figure does not say what quantity it applies to, so it is a number without a meaning.</summary>
    public const string QuantityBasisMissing = "TEMPEST-CIC-001";

    /// <summary>The figure does not say when it applies, so nothing can tell whether it is current.</summary>
    public const string ValidityMissing = "TEMPEST-CIC-002";

    /// <summary>The figure's own validity has run out.</summary>
    public const string FigureIsStale = "TEMPEST-CIC-003";

    /// <summary>The figure does not say where it applies.</summary>
    public const string GeographyMissing = "TEMPEST-CIC-004";

    /// <summary>Nobody recorded when the figure was actually observed.</summary>
    public const string ObservationDateMissing = "TEMPEST-CIC-005";

    /// <summary>The figure was observed long enough ago that it should be re-checked.</summary>
    public const string ObservationIsOld = "TEMPEST-CIC-006";

    /// <summary>Nothing evidences the figure.</summary>
    public const string EvidenceMissing = "TEMPEST-CIC-007";

    /// <summary>Evidence is described but cannot be retrieved.</summary>
    public const string EvidenceNotLocatable = "TEMPEST-CIC-008";

    /// <summary>The record's provenance names neither a source organisation nor a source document.</summary>
    public const string SourceNotIdentified = "TEMPEST-CIC-009";

    /// <summary>The figure is not tied to a supplier, so it is a market figure and should say so.</summary>
    public const string SupplierContextMissing = "TEMPEST-CIC-010";
}

/// <summary>
/// The shared checks every `P03` validation service runs over the
/// commercial context a record carries, and the quality state it derives.
/// </summary>
/// <remarks>
/// A static helper rather than a base class, for the same reason `P07`'s
/// is: each package's validation service already derives from
/// <see cref="ReferenceValidationService{TDefinition}"/> and C# has one
/// base.
/// </remarks>
public static class CommercialContextValidator
{
    /// <summary>How old an observed figure may be before it is reported as needing a re-check.</summary>
    public const int ObservationStaleAfterDays = 365;

    /// <summary>
    /// Derives the record's usability from its context, provenance and
    /// source.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deterministic and ordered worst-first, so the same record always
    /// reports the same quality. The order matters: a record that is both
    /// incomplete and stale is <see cref="CommercialQuality.Incomplete"/>,
    /// because fixing the staleness would still leave it unusable.
    /// </para>
    /// <para>
    /// <see cref="CommercialQuality.Contradicted"/> is never returned
    /// here: it depends on other records, which a single-record check
    /// cannot see. The cost and lead-time libraries report it from their
    /// own cross-record checks.
    /// </para>
    /// </remarks>
    /// <param name="applicability">The record's own commercial context.</param>
    /// <param name="source">Where the figure came from.</param>
    /// <param name="provenance">The record's reference-data provenance.</param>
    /// <param name="asAt">The date staleness is judged against.</param>
    /// <param name="requiresQuantityBasis">Whether a figure of this kind is meaningless without a quantity band.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    public static CommercialQuality DeriveQuality(
        CommercialApplicability applicability,
        CommercialSource source,
        ReferenceProvenance provenance,
        DateOnly asAt,
        bool requiresQuantityBasis = true)
    {
        ArgumentNullException.ThrowIfNull(applicability);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(provenance);

        var missesBasis = requiresQuantityBasis && !applicability.HasQuantityBasis;
        var missesSource = string.IsNullOrWhiteSpace(provenance.SourceOrganisation)
                           && string.IsNullOrWhiteSpace(provenance.SourceDocument);

        if (missesBasis || missesSource || !applicability.HasValidity)
            return CommercialQuality.Incomplete;

        if (applicability.IsStaleAt(asAt))
            return CommercialQuality.Stale;

        return provenance.VerificationStatus == ReferenceVerificationStatus.VerifiedAgainstSource
            ? CommercialQuality.Verified
            : CommercialQuality.Unverified;
    }

    /// <summary>
    /// Checks the shared commercial context and appends what it finds to
    /// <paramref name="errors"/> and <paramref name="warnings"/>.
    /// </summary>
    /// <remarks>
    /// A missing quantity basis is an <b>error</b> where the record's kind
    /// requires one: a price nobody can attach a quantity to is not an
    /// incomplete price, it is not a price. Everything else is a warning,
    /// because a partial commercial record is still worth more than
    /// nothing recorded at all.
    /// </remarks>
    /// <param name="subject">What the record is, for the diagnostic text.</param>
    /// <param name="applicability">The record's own commercial context.</param>
    /// <param name="source">Where the figure came from.</param>
    /// <param name="provenance">The record's reference-data provenance.</param>
    /// <param name="asAt">The date staleness is judged against.</param>
    /// <param name="errors">The error list to append to.</param>
    /// <param name="warnings">The warning list to append to.</param>
    /// <param name="requiresQuantityBasis">Whether a figure of this kind is meaningless without a quantity band.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="subject"/> is empty or whitespace.</exception>
    public static void Evaluate(
        string subject,
        CommercialApplicability applicability,
        CommercialSource source,
        ReferenceProvenance provenance,
        DateOnly asAt,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        bool requiresQuantityBasis = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(applicability);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);

        if (requiresQuantityBasis && !applicability.HasQuantityBasis)
            errors.Add(Diagnostic(
                CommercialContextRules.QuantityBasisMissing,
                $"{subject} does not say what quantity it applies to. A commercial figure without a quantity basis means one "
                + "thing at five and something else entirely at five thousand."));

        if (!applicability.HasValidity)
            warnings.Add(Diagnostic(
                CommercialContextRules.ValidityMissing,
                $"{subject} does not say when it applies, so nothing can tell whether it is still current."));
        else if (applicability.IsStaleAt(asAt))
            warnings.Add(Diagnostic(
                CommercialContextRules.FigureIsStale,
                $"{subject} was valid to {applicability.Validity!.To:O} and has expired. It remains evidence of what was true "
                + "then; it is not evidence of what is true now."));

        if (!applicability.Geography.IsStated)
            warnings.Add(Diagnostic(
                CommercialContextRules.GeographyMissing,
                $"{subject} does not say where it applies. An unstated scope is a gap, not a claim that the figure holds "
                + "everywhere."));

        if (!applicability.IsSupplierSpecific)
            warnings.Add(Diagnostic(
                CommercialContextRules.SupplierContextMissing,
                $"{subject} names no supplier, so it is a market or published figure. That is legitimate, and it is not the "
                + "same as a price somebody has actually been offered."));

        if (source.ObservedOn is null)
            warnings.Add(Diagnostic(
                CommercialContextRules.ObservationDateMissing,
                $"{subject} does not record when the figure was actually observed, which is not the same as the date on the "
                + "document it came from."));
        else if (source.IsOlderThan(asAt, ObservationStaleAfterDays))
            warnings.Add(Diagnostic(
                CommercialContextRules.ObservationIsOld,
                $"{subject} was observed on {source.ObservedOn:O}, {source.AgeInDaysAt(asAt)} days ago, and should be "
                + "re-checked before it is relied on."));

        if (!source.HasEvidence)
            warnings.Add(Diagnostic(
                CommercialContextRules.EvidenceMissing,
                $"{subject} records no evidence, so what it asserts rests on nothing a reader can check."));

        foreach (var evidence in source.Evidence.Where(e => !e.IsLocatable))
            warnings.Add(Diagnostic(
                CommercialContextRules.EvidenceNotLocatable,
                $"{subject} cites evidence \"{evidence.Description}\" that is neither held in TempestOS nor identified by an "
                + "external reference, so nobody can retrieve it."));

        if (string.IsNullOrWhiteSpace(provenance.SourceOrganisation) && string.IsNullOrWhiteSpace(provenance.SourceDocument))
            warnings.Add(Diagnostic(
                CommercialContextRules.SourceNotIdentified,
                $"{subject} names neither a source organisation nor a source document, so where the figure came from is "
                + "unrecorded."));
    }

    private static IValidationDiagnostic Diagnostic(string code, string message) => new ValidationDiagnostic(code, message);
}
