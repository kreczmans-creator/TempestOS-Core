using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Assets;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.BusinessGovernance;

// C3 must never determine ownership or conclude compliance. These tests
// check that it says what it knows and names whose determination the rest
// is.
public class AssetTests
{
    private static DateOnly Today => BusinessGovernanceFixtures.Today;

    private static async Task<IValidationResult> ValidateIPAsync(IPAsset asset)
    {
        var catalog = BusinessGovernanceFixtures.BuildIPCatalog();
        var service = new IPAssetValidationService(catalog, BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(asset, BusinessGovernanceFixtures.Verified());
    }

    private static async Task<IValidationResult> ValidateDataAsync(DataAsset asset)
    {
        var catalog = BusinessGovernanceFixtures.BuildDataCatalog();
        var service = new DataAssetValidationService(catalog, BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(asset, BusinessGovernanceFixtures.Verified());
    }

    [Fact]
    public void OwnershipDefaultsToNotDetermined()
    {
        // Holding an asset in TempestOS establishes nothing about owning
        // it.
        var asset = new IPAsset
        {
            Reference = "IP-9",
            Name = "A drawing somebody put in the system",
            Governance = BusinessGovernanceFixtures.Governance(),
        };

        Assert.Equal(IPOwnership.NotDetermined, asset.Ownership);
        Assert.False(asset.IsOwnershipDetermined);
    }

    [Fact]
    public async Task OwnershipAssertedWithNoEvidence_IsAnError()
    {
        var result = await ValidateIPAsync(BusinessGovernanceFixtures.IPAsset_() with { OwnershipEvidence = [] });

        Assert.Contains(AssetValidationRules.OwnershipIsUnevidenced, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task UndeterminedOwnership_IsAWarning_NotAnError()
    {
        // Not knowing is a reportable state, not an invalid record.
        var result = await ValidateIPAsync(BusinessGovernanceFixtures.IPAsset_() with
        {
            Ownership = IPOwnership.NotDetermined,
            OwnershipEvidence = [],
        });

        Assert.Contains(AssetValidationRules.OwnershipNotDetermined, result.Warnings.Select(d => d.Code));
        Assert.DoesNotContain(AssetValidationRules.OwnershipIsUnevidenced, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task DisputedOwnership_IsAnError()
    {
        var result = await ValidateIPAsync(BusinessGovernanceFixtures.IPAsset_() with { Ownership = IPOwnership.Disputed });

        Assert.Contains(AssetValidationRules.OwnershipIsDisputed, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task UsingSomebodyElsesIPWithNoLicence_IsAnError()
    {
        var result = await ValidateIPAsync(BusinessGovernanceFixtures.IPAsset_() with
        {
            Ownership = IPOwnership.ThirdParty,
            OwnerName = "Fictional Vendor Ltd",
        });

        Assert.Contains(AssetValidationRules.UseWithoutLicence, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AnExpiredLicence_IsAnError()
    {
        var result = await ValidateIPAsync(BusinessGovernanceFixtures.IPAsset_() with
        {
            Ownership = IPOwnership.ThirdParty,
            OwnerName = "Fictional Vendor Ltd",
            Licence = new IPLicence(
                "Fictional Vendor Ltd",
                "Fixture licence",
                "Internal use only.",
                Period: new EffectivePeriod(Today.AddYears(-2), Today.AddDays(-1)),
                Evidence: [new BusinessEvidence(BusinessEvidenceKind.ExecutedDocument, "Fixture licence.", Reference: "LIC-1")]),
        });

        Assert.Contains(AssetValidationRules.LicenceHasExpired, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AnAssetWithNoStatedOrigin_IsReported()
    {
        // Background and foreground is the distinction that decides what a
        // consultancy keeps and what it hands over.
        var result = await ValidateIPAsync(BusinessGovernanceFixtures.IPAsset_() with { Origin = IPOrigin.Unspecified });

        Assert.Contains(AssetValidationRules.IPOriginShouldBeStated, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ARegistrationRenewalFallingDue_IsReported()
    {
        var result = await ValidateIPAsync(BusinessGovernanceFixtures.IPAsset_() with
        {
            RegistrationReference = "FIX-TM-1",
            RegistrationRenewalDue = Today.AddDays(30),
        });

        Assert.Contains(AssetValidationRules.RegistrationRenewalDue, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public void NoIPOwnershipValue_MeansTempestOSDeterminedIt()
    {
        // Every value describes a position somebody recorded from a
        // contract. None of them can be reached by the system.
        var names = Enum.GetNames<IPOwnership>();

        Assert.Contains(nameof(IPOwnership.NotDetermined), names);
        Assert.Contains(nameof(IPOwnership.Disputed), names);
        Assert.DoesNotContain("Inferred", names);
        Assert.DoesNotContain("Assumed", names);
    }

    [Fact]
    public async Task DataHeldForNoStatedReason_IsAnError()
    {
        var result = await ValidateDataAsync(BusinessGovernanceFixtures.DataAsset_() with { ProcessingPurpose = null });

        Assert.Contains(AssetValidationRules.ProcessingPurposeNotStated, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task DataWithNoRetentionRule_IsReportedAsKeptIndefinitelyByDefault()
    {
        var asset = BusinessGovernanceFixtures.DataAsset_() with { Retention = null };

        var result = await ValidateDataAsync(asset);

        Assert.True(asset.IsRetainedIndefinitely);
        Assert.Contains(AssetValidationRules.NoRetentionRule, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ARetentionRuleThatDoesNotSayWhatHappensAtTheEnd_IsReported()
    {
        var result = await ValidateDataAsync(BusinessGovernanceFixtures.DataAsset_() with
        {
            Retention = new RetentionRule("Six years.", RetainForMonths: 72, BasisState: DeterminationState.Recorded),
        });

        Assert.Contains(AssetValidationRules.RetentionStatesNoDisposal, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ARetentionPeriodSomebodyAssumed_IsDistinguishedFromOneSomebodyEstablished()
    {
        var result = await ValidateDataAsync(BusinessGovernanceFixtures.DataAsset_() with
        {
            Retention = new RetentionRule(
                "Six years.", RetainForMonths: 72, DisposalMethod: "Secure deletion",
                Basis: "Somebody's recollection", BasisState: DeterminationState.Assumed),
        });

        Assert.Contains(AssetValidationRules.RetentionBasisNotDetermined, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task PersonalDataWithNoComplianceReview_SaysNobodyQualifiedHasSaidSo()
    {
        // Never "not compliant" — a determination TempestOS cannot make.
        var result = await ValidateDataAsync(BusinessGovernanceFixtures.DataAsset_() with
        {
            Category = DataCategory.PersonalData,
            AccessRequirements = ["Project team only."],
        });

        var finding = result.Warnings.Single(d => d.Code == AssetValidationRules.PersonalDataNeedsComplianceReview);

        Assert.Contains("cannot determine whether", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not compliant", finding.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PersonalDataWithNoAccessRequirements_IsReported()
    {
        var result = await ValidateDataAsync(BusinessGovernanceFixtures.DataAsset_() with
        {
            Category = DataCategory.SpecialCategoryPersonalData,
            ComplianceReviewState = DeterminationState.Recorded,
        });

        Assert.Contains(AssetValidationRules.PersonalDataNeedsAccessRequirements, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AnOutstandingComplianceReviewWithNobodyNamed_IsReported()
    {
        var result = await ValidateDataAsync(BusinessGovernanceFixtures.DataAsset_() with
        {
            Category = DataCategory.EmployeeData,
            ComplianceReviewState = DeterminationState.ReviewRequired,
            ComplianceReviewOwner = null,
            AccessRequirements = ["HR only."],
        });

        Assert.Contains(AssetValidationRules.ComplianceReviewHasNoOwner, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ClientDataWithNothingSaidAboutMovingIt_IsReported()
    {
        var result = await ValidateDataAsync(BusinessGovernanceFixtures.DataAsset_() with { TransferRestrictions = [] });

        Assert.Contains(AssetValidationRules.ClientDataNeedsTransferRestrictions, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AWellFormedDataAsset_ValidatesCleanly()
    {
        var result = await ValidateDataAsync(BusinessGovernanceFixtures.DataAsset_());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task SearchingForUnevidencedOwnership_FindsExactlyThose()
    {
        var catalog = BusinessGovernanceFixtures.BuildIPCatalog();

        await catalog.RegisterAsync("ip-1", BusinessGovernanceFixtures.IPAsset_(), BusinessGovernanceFixtures.Verified());
        await catalog.RegisterAsync(
            "ip-2",
            BusinessGovernanceFixtures.IPAsset_("IP-2") with { OwnershipEvidence = [] },
            BusinessGovernanceFixtures.Verified());

        var found = await catalog.SearchAsync(new IPAssetQuery { OwnershipIsEvidenced = false });

        Assert.Equal("IP-2", Assert.Single(found).Definition.Reference);
    }
}
