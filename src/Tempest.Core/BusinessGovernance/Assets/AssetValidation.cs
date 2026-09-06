using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Assets;

/// <summary>The diagnostic codes C3's validation services report.</summary>
public static class AssetValidationRules
{
    /// <summary>Nobody has established who owns the asset.</summary>
    public const string OwnershipNotDetermined = "TEMPEST-BGI-001";

    /// <summary>Ownership is stated with nothing behind it.</summary>
    public const string OwnershipIsUnevidenced = "TEMPEST-BGI-002";

    /// <summary>Two sources give different answers about who owns the asset.</summary>
    public const string OwnershipIsDisputed = "TEMPEST-BGI-003";

    /// <summary>The organisation uses an asset it does not own and holds no recorded licence.</summary>
    public const string UseWithoutLicence = "TEMPEST-BGI-004";

    /// <summary>The licence the organisation relies on has run out.</summary>
    public const string LicenceHasExpired = "TEMPEST-BGI-005";

    /// <summary>The licence rests on nothing a reader could check.</summary>
    public const string LicenceIsUnevidenced = "TEMPEST-BGI-006";

    /// <summary>The asset does not say what kind of intellectual property it is.</summary>
    public const string IPTypeShouldBeStated = "TEMPEST-BGI-007";

    /// <summary>The asset does not say where it came from, so background and foreground cannot be told apart.</summary>
    public const string IPOriginShouldBeStated = "TEMPEST-BGI-008";

    /// <summary>Registration renewal falls due soon.</summary>
    public const string RegistrationRenewalDue = "TEMPEST-BGI-009";

    /// <summary>Two IP assets share one reference.</summary>
    public const string DuplicateIPAssetReference = "TEMPEST-BGI-010";

    /// <summary>The data asset does not say why the organisation holds it.</summary>
    public const string ProcessingPurposeNotStated = "TEMPEST-BGI-011";

    /// <summary>The data asset does not say what kind of information it holds.</summary>
    public const string DataCategoryShouldBeStated = "TEMPEST-BGI-012";

    /// <summary>No retention rule has been set, so the information is kept indefinitely by default.</summary>
    public const string NoRetentionRule = "TEMPEST-BGI-013";

    /// <summary>A retention rule does not say what happens to the information at the end of the period.</summary>
    public const string RetentionStatesNoDisposal = "TEMPEST-BGI-014";

    /// <summary>The retention period rests on nothing anybody has determined.</summary>
    public const string RetentionBasisNotDetermined = "TEMPEST-BGI-015";

    /// <summary>Personal data is held and nobody qualified has reviewed the organisation's position on it.</summary>
    public const string PersonalDataNeedsComplianceReview = "TEMPEST-BGI-016";

    /// <summary>A compliance review is outstanding and nobody is named to do it.</summary>
    public const string ComplianceReviewHasNoOwner = "TEMPEST-BGI-017";

    /// <summary>Personal data is held with nothing stated about who may see it.</summary>
    public const string PersonalDataNeedsAccessRequirements = "TEMPEST-BGI-018";

    /// <summary>Client data is held with nothing stated about moving it.</summary>
    public const string ClientDataNeedsTransferRestrictions = "TEMPEST-BGI-019";

    /// <summary>Two data assets share one reference.</summary>
    public const string DuplicateDataAssetReference = "TEMPEST-BGI-020";
}

/// <summary>Governance of the intellectual property register itself.</summary>
public interface IIPAssetValidationService : IReferenceValidationService<IPAsset>
{
}

/// <summary>The concrete <see cref="IIPAssetValidationService"/> implementation.</summary>
/// <remarks>
/// Nothing here determines ownership. Every check asks whether the record
/// says who owns the asset and shows why — questions of record-keeping,
/// which are answerable. Whether the contract actually assigns what the
/// record claims is a question for whoever reads the contract.
/// </remarks>
public sealed class IPAssetValidationService : ReferenceValidationService<IPAsset>, IIPAssetValidationService
{
    /// <summary>How far ahead a registration renewal is reported.</summary>
    public const int RenewalWarningDays = 90;

    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="IPAssetValidationService"/> class.</summary>
    /// <param name="catalog">The IP register whose records this service validates.</param>
    /// <param name="timeProvider">The clock expiry checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public IPAssetValidationService(IIPAssetCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        IPAsset definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"IP asset '{definition.Reference}' ({definition.Name})";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings, expectEvidence: false);

        switch (definition.Ownership)
        {
            case IPOwnership.NotDetermined:
                warnings.Add(Diagnostic(
                    AssetValidationRules.OwnershipNotDetermined,
                    $"{subject} records no ownership position. Holding the asset in TempestOS establishes nothing about owning it; "
                    + "somebody has to read the contract."));
                break;

            case IPOwnership.Disputed:
                errors.Add(Diagnostic(
                    AssetValidationRules.OwnershipIsDisputed,
                    $"{subject} has a disputed ownership position, so the organisation cannot safely rely on, licence or assign it "
                    + "until the dispute is resolved."));
                break;

            default:
                if (definition.IsOwnershipAsserted)
                    errors.Add(Diagnostic(
                        AssetValidationRules.OwnershipIsUnevidenced,
                        $"{subject} records ownership as {definition.Ownership} with no evidence. An ownership position nobody can "
                        + "produce a document for is an assertion."));
                break;
        }

        if (definition.NeedsLicenceAndHasNone)
            errors.Add(Diagnostic(
                AssetValidationRules.UseWithoutLicence,
                $"{subject} is owned by a {definition.Ownership.ToString().ToLowerInvariant()} and the organisation records no "
                + "licence, so what it is entitled to do with the asset is unrecorded."));

        if (definition.Licence is { } licence)
        {
            if (definition.LicenceHasExpiredBy(today))
                errors.Add(Diagnostic(
                    AssetValidationRules.LicenceHasExpired,
                    $"{subject} is used under a licence from {licence.Licensor} that ran to {licence.Period!.To:O}."));

            if (!licence.IsEvidenced)
                warnings.Add(Diagnostic(
                    AssetValidationRules.LicenceIsUnevidenced,
                    $"{subject} relies on a licence from {licence.Licensor} with nothing recorded that evidences the grant."));
        }

        if (definition.Type == IPType.Unspecified)
            warnings.Add(Diagnostic(
                AssetValidationRules.IPTypeShouldBeStated,
                $"{subject} does not say what kind of intellectual property it is."));

        if (definition.Origin == IPOrigin.Unspecified)
            warnings.Add(Diagnostic(
                AssetValidationRules.IPOriginShouldBeStated,
                $"{subject} does not say where it came from, so background and foreground IP cannot be told apart — the "
                + "distinction that decides what a consultancy keeps and what it hands over."));

        if (definition.RenewalDueWithin(today, RenewalWarningDays))
            warnings.Add(Diagnostic(
                AssetValidationRules.RegistrationRenewalDue,
                $"{subject} has a registration renewal due on {definition.RegistrationRenewalDue:O}."));

        return Task.CompletedTask;
    }
}

/// <summary>Governance of the data-asset register itself.</summary>
public interface IDataAssetValidationService : IReferenceValidationService<DataAsset>
{
}

/// <summary>The concrete <see cref="IDataAssetValidationService"/> implementation.</summary>
/// <remarks>
/// <b>Nothing here is a compliance determination.</b> No check concludes
/// that the organisation complies with anything, and none concludes that
/// it does not. What they establish is whether the organisation could
/// answer the questions it would be asked: what data is held, why, for how
/// long, who may see it, and who has reviewed the position.
/// </remarks>
public sealed class DataAssetValidationService : ReferenceValidationService<DataAsset>, IDataAssetValidationService
{
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="DataAssetValidationService"/> class.</summary>
    /// <param name="catalog">The data-asset register whose records this service validates.</param>
    /// <param name="timeProvider">The clock review checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public DataAssetValidationService(IDataAssetCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        DataAsset definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Data asset '{definition.Reference}' ({definition.Name})";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings, expectEvidence: false);

        if (!definition.HasStatedPurpose)
            errors.Add(Diagnostic(
                AssetValidationRules.ProcessingPurposeNotStated,
                $"{subject} does not say why the organisation holds it. Information kept for no stated reason is information "
                + "nobody can justify keeping."));

        if (definition.Category == DataCategory.Unspecified)
            warnings.Add(Diagnostic(
                AssetValidationRules.DataCategoryShouldBeStated,
                $"{subject} does not say what kind of information it holds, so how it must be handled cannot be worked out."));

        EvaluateRetention(definition, subject, warnings);
        EvaluatePersonalData(definition, subject, warnings);

        if (definition.Category == DataCategory.ClientData && definition.TransferRestrictions.Count == 0)
            warnings.Add(Diagnostic(
                AssetValidationRules.ClientDataNeedsTransferRestrictions,
                $"{subject} holds client information with nothing recorded about moving it — to a subcontractor, out of the "
                + "organisation, or out of the jurisdiction."));

        return Task.CompletedTask;
    }

    private void EvaluateRetention(DataAsset definition, string subject, List<IValidationDiagnostic> warnings)
    {
        if (definition.Retention is not { } retention)
        {
            warnings.Add(Diagnostic(
                AssetValidationRules.NoRetentionRule,
                $"{subject} has no retention rule, so it is kept indefinitely by default rather than by decision."));

            return;
        }

        if (!retention.StatesDisposal)
            warnings.Add(Diagnostic(
                AssetValidationRules.RetentionStatesNoDisposal,
                $"{subject}'s retention rule does not say what happens at the end of the period, so nothing will actually be done."));

        if (retention.BasisState != DeterminationState.Recorded)
            warnings.Add(Diagnostic(
                AssetValidationRules.RetentionBasisNotDetermined,
                $"{subject}'s retention period rests on \"{retention.Basis}\", which is {retention.BasisState}. A period somebody "
                + "assumed is not one somebody established."));
    }

    private void EvaluatePersonalData(DataAsset definition, string subject, List<IValidationDiagnostic> warnings)
    {
        if (!definition.IsPersonalData)
            return;

        if (definition.ComplianceReviewState is DeterminationState.NotDetermined or DeterminationState.Disputed)
            warnings.Add(Diagnostic(
                AssetValidationRules.PersonalDataNeedsComplianceReview,
                $"{subject} holds information about identifiable people and its compliance position is "
                + $"{definition.ComplianceReviewState}. TempestOS cannot determine whether the organisation's handling is lawful; "
                + "it can record that nobody qualified has said so."));

        if (definition.ComplianceReviewState != DeterminationState.Recorded
            && string.IsNullOrWhiteSpace(definition.ComplianceReviewOwner))
            warnings.Add(Diagnostic(
                AssetValidationRules.ComplianceReviewHasNoOwner,
                $"{subject} needs a compliance review and names nobody to carry it out, so it is nobody's task."));

        if (definition.AccessRequirements.Count == 0)
            warnings.Add(Diagnostic(
                AssetValidationRules.PersonalDataNeedsAccessRequirements,
                $"{subject} holds information about identifiable people with nothing recorded about who may see it."));
    }
}
