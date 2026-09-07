using Tempest.Core.EngineeringDomain;
using Tempest.Core.Manufacturing;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Suppliers;

/// <summary>The diagnostic codes D1's validation service reports.</summary>
public static class SupplierValidationRules
{
    /// <summary>Nobody has established whether this is a distinct supplier.</summary>
    public const string IdentityNotAssessed = "TEMPEST-CID-001";

    /// <summary>The record may be a duplicate of another supplier, and the ambiguity is unresolved.</summary>
    public const string IdentityIsAmbiguous = "TEMPEST-CID-002";

    /// <summary>Identity is recorded as confirmed with no registration number or equivalent hard identifier behind it.</summary>
    public const string ConfirmedIdentityNeedsHardIdentifier = "TEMPEST-CID-003";

    /// <summary>The supplier is superseded and does not name what replaced it.</summary>
    public const string SupersededSupplierNeedsReplacement = "TEMPEST-CID-004";

    /// <summary>The supplier is barred and does not say why.</summary>
    public const string BarredSupplierNeedsReason = "TEMPEST-CID-005";

    /// <summary>Two aliases on one supplier are the same name.</summary>
    public const string DuplicateAlias = "TEMPEST-CID-006";

    /// <summary>The supplier records no capability at all.</summary>
    public const string SupplierHasNoCapabilities = "TEMPEST-CID-007";

    /// <summary>Two capabilities on one supplier share a reference.</summary>
    public const string DuplicateCapabilityReference = "TEMPEST-CID-008";

    /// <summary>A capability is recorded as verified or proven with nothing evidencing it.</summary>
    public const string CapabilityIsUnevidenced = "TEMPEST-CID-009";

    /// <summary>A capability names an `A7` process the manufacturing library does not hold.</summary>
    public const string CapabilityProcessMustResolve = "TEMPEST-CID-010";

    /// <summary>A process capability does not say which materials it covers.</summary>
    public const string CapabilityMaterialsNotStated = "TEMPEST-CID-011";

    /// <summary>Nobody has assessed a recorded capability.</summary>
    public const string CapabilityNotAssessed = "TEMPEST-CID-012";

    /// <summary>Two certifications on one supplier share a reference.</summary>
    public const string DuplicateCertificationReference = "TEMPEST-CID-013";

    /// <summary>A certification has run out.</summary>
    public const string CertificationHasExpired = "TEMPEST-CID-014";

    /// <summary>A certification records no validity period, so it cannot be shown to be current.</summary>
    public const string CertificationHasNoValidity = "TEMPEST-CID-015";

    /// <summary>A certification has no certificate behind it.</summary>
    public const string CertificationIsUnevidenced = "TEMPEST-CID-016";

    /// <summary>Two sites on one supplier share a reference.</summary>
    public const string DuplicateSiteReference = "TEMPEST-CID-017";

    /// <summary>A site claims a capability the supplier does not record.</summary>
    public const string SiteClaimsUnknownCapability = "TEMPEST-CID-018";

    /// <summary>The supplier records no site, so nothing says where the work would be done.</summary>
    public const string SupplierHasNoSites = "TEMPEST-CID-019";

    /// <summary>Nobody recorded where the supplier information came from.</summary>
    public const string SupplierSourceNotRecorded = "TEMPEST-CID-020";

    /// <summary>Two suppliers share one reference.</summary>
    public const string DuplicateSupplierReference = "TEMPEST-CID-021";
}

/// <summary>Governance of the supplier database itself.</summary>
public interface ISupplierValidationService : IReferenceValidationService<SupplierRecord>
{
}

/// <summary>The concrete <see cref="ISupplierValidationService"/> implementation.</summary>
/// <remarks>
/// The findings that matter are the ones that would let a supplier be
/// recommended for work they cannot do: a capability marked verified with
/// nothing behind it, a certificate that expired last year, an identity
/// nobody has separated from a near-duplicate. None of them is a
/// judgement about the supplier; all are questions about the record.
/// </remarks>
public sealed class SupplierValidationService : ReferenceValidationService<SupplierRecord>, ISupplierValidationService
{
    private readonly IProcessCatalog? _processes;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="SupplierValidationService"/> class.</summary>
    /// <param name="catalog">The supplier database whose records this service validates.</param>
    /// <param name="processes">The `A7` manufacturing library, for confirming that a capability's named process exists. Optional: a supplier may be recorded before the process library is populated.</param>
    /// <param name="standardResolver">Resolves a certification's cited standard against `A2`. Optional.</param>
    /// <param name="timeProvider">The clock expiry checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public SupplierValidationService(
        ISupplierCatalog catalog,
        IProcessCatalog? processes = null,
        IStandardResolver? standardResolver = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver)
    {
        _processes = processes;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        SupplierRecord definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Supplier '{definition.Reference}' ({definition.Identity.LegalName})";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        EvaluateIdentity(definition, subject, errors, warnings);
        await EvaluateCapabilitiesAsync(definition, subject, errors, warnings, cancellationToken).ConfigureAwait(false);
        EvaluateCertifications(definition, subject, today, errors, warnings);
        EvaluateSites(definition, subject, errors, warnings);

        if (!definition.Source.HasEvidence && definition.Source.ObservedOn is null)
            warnings.Add(Diagnostic(
                SupplierValidationRules.SupplierSourceNotRecorded,
                $"{subject} records neither where the information came from nor when it was gathered."));

        await EvaluateStandardReferencesAsync(
            definition.Certifications.Select(c => c.Standard),
            warnings,
            cancellationToken).ConfigureAwait(false);
    }

    private void EvaluateIdentity(
        SupplierRecord definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        var identity = definition.Identity;

        switch (identity.Confidence)
        {
            case IdentityConfidence.NotAssessed:
                warnings.Add(Diagnostic(
                    SupplierValidationRules.IdentityNotAssessed,
                    $"{subject} has an unassessed identity. Nobody has established that it is a distinct supplier rather than "
                    + "another record for one already held."));
                break;

            case IdentityConfidence.Ambiguous:
                warnings.Add(Diagnostic(
                    SupplierValidationRules.IdentityIsAmbiguous,
                    $"{subject} may be a duplicate of "
                    + (identity.PossibleDuplicatesOf.Count > 0
                        ? string.Join(", ", identity.PossibleDuplicatesOf)
                        : "another supplier the record does not name")
                    + ". The records stay separate until somebody with the facts decides."));
                break;

            case IdentityConfidence.Confirmed when !identity.HasHardIdentifier:
                errors.Add(Diagnostic(
                    SupplierValidationRules.ConfirmedIdentityNeedsHardIdentifier,
                    $"{subject} records its identity as Confirmed with no registration number or equivalent. A name is not an "
                    + "identity: names collide, change and are reused."));
                break;
        }

        foreach (var duplicate in identity.Aliases
                     .GroupBy(a => a.MatchKey, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.First().Name))
            warnings.Add(Diagnostic(
                SupplierValidationRules.DuplicateAlias,
                $"{subject} records the alias '{duplicate}' more than once."));

        if (definition.Status == SupplierStatus.Superseded && string.IsNullOrWhiteSpace(definition.SupersededByReference))
            errors.Add(Diagnostic(
                SupplierValidationRules.SupersededSupplierNeedsReplacement,
                $"{subject} is Superseded and does not name what replaced it, so anything referencing it leads nowhere."));

        if (definition.Status == SupplierStatus.Barred && string.IsNullOrWhiteSpace(definition.StatusReason))
            warnings.Add(Diagnostic(
                SupplierValidationRules.BarredSupplierNeedsReason,
                $"{subject} is Barred and does not say why, so nobody can tell whether the bar still applies."));
    }

    private async Task EvaluateCapabilitiesAsync(
        SupplierRecord definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (definition.Capabilities.Count == 0)
        {
            warnings.Add(Diagnostic(
                SupplierValidationRules.SupplierHasNoCapabilities,
                $"{subject} records no capability, so nothing can find it when somebody asks who can do a piece of work."));

            return;
        }

        foreach (var duplicate in definition.Capabilities
                     .GroupBy(c => c.Reference, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
            errors.Add(Diagnostic(
                SupplierValidationRules.DuplicateCapabilityReference,
                $"{subject} declares capability '{duplicate}' more than once."));

        foreach (var capability in definition.Capabilities)
        {
            if (capability.IsUnevidenced)
                errors.Add(Diagnostic(
                    SupplierValidationRules.CapabilityIsUnevidenced,
                    $"Capability '{capability.Reference}' on {subject} is recorded as {capability.Assurance} with nothing "
                    + "evidencing it. An independently established capability that nobody can produce a certificate, audit note "
                    + "or sample for is somebody's recollection wearing a stronger label."));

            if (capability.Assurance == CapabilityAssurance.NotAssessed)
                warnings.Add(Diagnostic(
                    SupplierValidationRules.CapabilityNotAssessed,
                    $"Capability '{capability.Reference}' on {subject} has not been assessed, so it is unclear whether the "
                    + "supplier claims it or somebody merely assumed it."));

            if (capability.ProcessRecordId is { } processId)
            {
                if (capability.MaterialRecordIds.Count == 0)
                    warnings.Add(Diagnostic(
                        SupplierValidationRules.CapabilityMaterialsNotStated,
                        $"Capability '{capability.Reference}' on {subject} names a process but no materials, so it cannot be "
                        + "told apart from a supplier who works only in one alloy."));

                if (_processes is not null
                    && await _processes.FindAsync(processId, cancellationToken).ConfigureAwait(false) is null)
                    warnings.Add(Diagnostic(
                        SupplierValidationRules.CapabilityProcessMustResolve,
                        $"Capability '{capability.Reference}' on {subject} names process '{processId}', which the `A7` "
                        + "manufacturing library does not hold."));
            }
        }
    }

    private void EvaluateCertifications(
        SupplierRecord definition,
        string subject,
        DateOnly today,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        foreach (var duplicate in definition.Certifications
                     .GroupBy(c => c.Reference, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
            errors.Add(Diagnostic(
                SupplierValidationRules.DuplicateCertificationReference,
                $"{subject} declares certification '{duplicate}' more than once."));

        foreach (var certification in definition.Certifications)
        {
            if (certification.HasExpiredBy(today))
                warnings.Add(Diagnostic(
                    SupplierValidationRules.CertificationHasExpired,
                    $"{subject} holds a {certification.Standard.Designation} certificate that ran to "
                    + $"{certification.Validity!.To:O}. Anything qualified on the strength of it needs re-checking."));

            if (certification.Validity is null)
                warnings.Add(Diagnostic(
                    SupplierValidationRules.CertificationHasNoValidity,
                    $"{subject} records a {certification.Standard.Designation} certificate with no validity period, so it "
                    + "cannot be shown to be current and will never satisfy a certification requirement."));

            if (!certification.IsEvidenced)
                warnings.Add(Diagnostic(
                    SupplierValidationRules.CertificationIsUnevidenced,
                    $"{subject} records a {certification.Standard.Designation} certificate with no copy held, so the "
                    + "organisation could not produce it."));
        }
    }

    private void EvaluateSites(
        SupplierRecord definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (definition.Sites.Count == 0)
            warnings.Add(Diagnostic(
                SupplierValidationRules.SupplierHasNoSites,
                $"{subject} records no site, so nothing says where the work would actually be done — and lead time and "
                + "carriage both depend on it."));

        foreach (var duplicate in definition.Sites
                     .GroupBy(s => s.Reference, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
            errors.Add(Diagnostic(
                SupplierValidationRules.DuplicateSiteReference,
                $"{subject} declares site '{duplicate}' more than once."));

        var declared = definition.Capabilities.Select(c => c.Reference).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var site in definition.Sites)
        {
            foreach (var capability in site.CapabilityReferences.Where(c => !declared.Contains(c)))
                warnings.Add(Diagnostic(
                    SupplierValidationRules.SiteClaimsUnknownCapability,
                    $"Site '{site.Reference}' on {subject} claims capability '{capability}', which the supplier does not "
                    + "record."));
        }
    }
}
