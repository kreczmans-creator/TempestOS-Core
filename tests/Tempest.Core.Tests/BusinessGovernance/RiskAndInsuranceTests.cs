using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Risk;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.BusinessGovernance;

// C2 must keep three things apart that businesses routinely conflate:
// assessing a risk, accepting it, and insuring against it.
public class RiskAndInsuranceTests
{
    private static DateOnly Today => BusinessGovernanceFixtures.Today;

    [Theory]
    [InlineData(RiskLikelihood.NotAssessed, RiskImpact.Major, RiskExposure.NotAssessed)]
    [InlineData(RiskLikelihood.Likely, RiskImpact.NotAssessed, RiskExposure.NotAssessed)]
    [InlineData(RiskLikelihood.Rare, RiskImpact.Negligible, RiskExposure.Low)]
    [InlineData(RiskLikelihood.Possible, RiskImpact.Moderate, RiskExposure.High)]
    [InlineData(RiskLikelihood.AlmostCertain, RiskImpact.Major, RiskExposure.Extreme)]
    public void TheExposureMatrixIsDeterministic(RiskLikelihood likelihood, RiskImpact impact, RiskExposure expected)
    {
        Assert.Equal(expected, RiskExposures.From(likelihood, impact));
        Assert.Equal(expected, RiskExposures.From(likelihood, impact));
    }

    [Fact]
    public void AnUnassessedRiskIsNeverALowOne()
    {
        // The default must not read as "fine".
        Assert.Equal(RiskExposure.NotAssessed, RiskExposures.From(RiskLikelihood.NotAssessed, RiskImpact.NotAssessed));
        Assert.True(
            RiskExposures.Rank(RiskExposure.NotAssessed) > RiskExposures.Rank(RiskExposure.Medium));
    }

    [Fact]
    public void ARareButOrganisationEndingRisk_IsNeverMerelyLow()
    {
        // A severe impact at low probability is still something a director
        // has to see.
        Assert.Equal(RiskExposure.High, RiskExposures.From(RiskLikelihood.Rare, RiskImpact.Severe));
        Assert.Equal(RiskExposure.Extreme, RiskExposures.From(RiskLikelihood.Possible, RiskImpact.Severe));
    }

    [Fact]
    public void ARiskAssessedAsLow_IsStillNotAnAcceptedRisk()
    {
        // Assessment is not acceptance, whatever the band says.
        var risk = BusinessGovernanceFixtures.Risk() with
        {
            InherentLikelihood = RiskLikelihood.Rare,
            InherentImpact = RiskImpact.Negligible,
            ResidualLikelihood = RiskLikelihood.Rare,
            ResidualImpact = RiskImpact.Negligible,
        };

        Assert.Equal(RiskExposure.Low, risk.ResidualExposure);
        Assert.False(risk.IsAccepted);
        Assert.Null(risk.Acceptance);
    }

    [Fact]
    public void AResidualBetterThanInherentWithNothingImplemented_IsUnearned()
    {
        // The commonest and most dangerous error in a risk register: an
        // assessment of the plan rather than of the position.
        var risk = BusinessGovernanceFixtures.Risk() with
        {
            InherentLikelihood = RiskLikelihood.Likely,
            InherentImpact = RiskImpact.Major,
            ResidualLikelihood = RiskLikelihood.Unlikely,
            ResidualImpact = RiskImpact.Minor,
            Mitigations =
            [
                new RiskMitigation("M-1", "Planned but not done.", "owner-1", DueBy: Today.AddMonths(1)),
            ],
        };

        Assert.True(risk.ResidualIsUnearned);
    }

    [Fact]
    public void AResidualBetterThanInherentWithAMitigationInPlace_IsEarned()
    {
        var risk = BusinessGovernanceFixtures.Risk() with
        {
            InherentLikelihood = RiskLikelihood.Likely,
            InherentImpact = RiskImpact.Major,
            ResidualLikelihood = RiskLikelihood.Unlikely,
            ResidualImpact = RiskImpact.Minor,
            Mitigations =
            [
                new RiskMitigation("M-1", "Peer review of every calculation.", "owner-1", IsImplemented: true,
                    Evidence: [new BusinessEvidence(BusinessEvidenceKind.InternalRecord, "Review records.", Reference: "QMS-1")]),
            ],
        };

        Assert.False(risk.ResidualIsUnearned);
    }

    [Fact]
    public void ASeriousRiskNobodyAccepted_IsReported()
    {
        var risk = BusinessGovernanceFixtures.Risk();

        Assert.Equal(RiskExposure.High, risk.ResidualExposure);
        Assert.True(risk.IsCarriedWithoutAcceptance);
    }

    [Fact]
    public void RecordingAnAcceptance_DoesNotChangeTheExposure()
    {
        // Accepting a risk is a decision to carry it, not a reason to
        // think it smaller.
        var service = new RiskAndInsuranceService(
            BusinessGovernanceFixtures.BuildRiskCatalog(), BusinessGovernanceFixtures.BuildPolicyCatalog());
        var risk = BusinessGovernanceFixtures.Risk();

        var accepted = service.RecordAcceptance(
            risk, BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.RiskAcceptance));

        Assert.True(accepted.IsAccepted);
        Assert.Equal(risk.ResidualExposure, accepted.ResidualExposure);
        Assert.False(accepted.IsCarriedWithoutAcceptance);
        Assert.False(accepted.IsClosed);
    }

    [Fact]
    public void AnAcceptanceOfTheWrongKind_IsRefused()
    {
        var service = new RiskAndInsuranceService(
            BusinessGovernanceFixtures.BuildRiskCatalog(), BusinessGovernanceFixtures.BuildPolicyCatalog());

        Assert.Throws<ArgumentException>(() => service.RecordAcceptance(
            BusinessGovernanceFixtures.Risk(),
            BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)));
    }

    [Fact]
    public void AnAcceptanceIsNotOverwritten()
    {
        var service = new RiskAndInsuranceService(
            BusinessGovernanceFixtures.BuildRiskCatalog(), BusinessGovernanceFixtures.BuildPolicyCatalog());

        var accepted = service.RecordAcceptance(
            BusinessGovernanceFixtures.Risk(), BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.RiskAcceptance));

        Assert.Throws<InvalidOperationException>(() => service.RecordAcceptance(
            accepted, BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.RiskAcceptance, "director-2")));
    }

    [Fact]
    public void NothingInTheServiceAcceptsARiskOrAssertsCover()
    {
        var methods = typeof(IRiskAndInsuranceService).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(methods, name =>
            name.Contains("Accept", StringComparison.OrdinalIgnoreCase) && !name.Contains("Record", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, name => name.Contains("Insure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, name => name.Contains("Cover", StringComparison.OrdinalIgnoreCase) && name.StartsWith("Is", StringComparison.Ordinal));
    }

    [Fact]
    public void NoCoverageAssessmentValue_AssertsThatALossWouldBePaid()
    {
        // The strongest value the records support is that a policy
        // supports a claim, not that one is covered.
        var names = Enum.GetNames<CoverageAssessment>();

        Assert.Contains(nameof(CoverageAssessment.PolicySupportsClaim), names);
        Assert.DoesNotContain("Covered", names);
        Assert.DoesNotContain("Insured", names);
    }

    private static async Task<(BusinessRiskCatalog Risks, InsurancePolicyCatalog Policies, RiskAndInsuranceService Service)> BuildAsync(
        BusinessRisk risk,
        InsurancePolicy? policy = null)
    {
        var risks = BusinessGovernanceFixtures.BuildRiskCatalog();
        var policies = BusinessGovernanceFixtures.BuildPolicyCatalog();

        await risks.RegisterAsync("rsk-1", risk, BusinessGovernanceFixtures.Verified());

        if (policy is not null)
            await policies.RegisterAsync("pol-1", policy, BusinessGovernanceFixtures.Verified());

        return (risks, policies, new RiskAndInsuranceService(risks, policies));
    }

    [Fact]
    public async Task ARiskNamingNoPolicy_ClaimsNothingEitherWay()
    {
        var (_, _, service) = await BuildAsync(BusinessGovernanceFixtures.Risk());

        var position = await service.AssessCoverageAsync("RSK-1", Today);

        Assert.Equal(CoverageAssessment.NoPolicyNamed, position.Assessment);
    }

    [Fact]
    public async Task ARiskNamingAPolicyNobodyHolds_IsReportedAsNotFound()
    {
        var (_, _, service) = await BuildAsync(
            BusinessGovernanceFixtures.Risk() with { RelatedPolicyReferences = ["POL-MISSING"] });

        var position = await service.AssessCoverageAsync("RSK-1", Today);

        Assert.Equal(CoverageAssessment.PolicyNotFound, position.Assessment);
    }

    [Fact]
    public async Task ALapsedPolicyDoesNotAnswerALossOccurringNow()
    {
        var (_, _, service) = await BuildAsync(
            BusinessGovernanceFixtures.Risk() with { RelatedPolicyReferences = ["POL-1"] },
            BusinessGovernanceFixtures.Policy(to: Today.AddMonths(-1)));

        var position = await service.AssessCoverageAsync("RSK-1", Today);

        Assert.Equal(CoverageAssessment.PolicyLapsed, position.Assessment);
    }

    [Fact]
    public async Task APolicyWithNoStatedLimit_LeavesTheExtentUnknown()
    {
        var policy = BusinessGovernanceFixtures.Policy() with
        {
            Coverages =
            [
                new InsuranceCoverage(InsuranceCoverageType.ProfessionalIndemnity, "Cover with no stated limit."),
            ],
        };

        var (_, _, service) = await BuildAsync(
            BusinessGovernanceFixtures.Risk() with { RelatedPolicyReferences = ["POL-1"] }, policy);

        var position = await service.AssessCoverageAsync("RSK-1", Today);

        Assert.Equal(CoverageAssessment.CoverageLimitUnknown, position.Assessment);
    }

    [Fact]
    public async Task AnUnevidencedPolicyIsWeakerThanAnEvidencedOne()
    {
        var (_, _, service) = await BuildAsync(
            BusinessGovernanceFixtures.Risk() with { RelatedPolicyReferences = ["POL-1"] },
            BusinessGovernanceFixtures.Policy() with { PolicyDocumentId = null });

        var position = await service.AssessCoverageAsync("RSK-1", Today);

        Assert.Equal(CoverageAssessment.UnevidencedPolicy, position.Assessment);
    }

    [Fact]
    public async Task ACurrentEvidencedPolicy_SupportsAClaim_AndSaysNoMore()
    {
        var (_, _, service) = await BuildAsync(
            BusinessGovernanceFixtures.Risk() with { RelatedPolicyReferences = ["POL-1"] },
            BusinessGovernanceFixtures.Policy());

        var position = await service.AssessCoverageAsync("RSK-1", Today);

        Assert.Equal(CoverageAssessment.PolicySupportsClaim, position.Assessment);
        Assert.Contains("insurer's determination", position.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnExposureExceedingTheLimit_IsReported()
    {
        var (_, _, service) = await BuildAsync(
            BusinessGovernanceFixtures.Risk() with
            {
                RelatedPolicyReferences = ["POL-1"],
                EstimatedFinancialExposure = BusinessGovernanceFixtures.Gbp_(2_000_000m),
            },
            BusinessGovernanceFixtures.Policy());

        var position = await service.AssessCoverageAsync("RSK-1", Today);

        Assert.True(position.ExposureExceedsLimit);
    }

    [Fact]
    public async Task AnExposureComparisonAcrossCurrencies_IsRefusedRatherThanConverted()
    {
        var (_, _, service) = await BuildAsync(
            BusinessGovernanceFixtures.Risk() with
            {
                RelatedPolicyReferences = ["POL-1"],
                EstimatedFinancialExposure = new Money(2_000_000m, new CurrencyCode("EUR")),
            },
            BusinessGovernanceFixtures.Policy());

        var position = await service.AssessCoverageAsync("RSK-1", Today);

        Assert.Null(position.ExposureExceedsLimit);
    }

    private static async Task<IValidationResult> ValidateRiskAsync(BusinessRisk risk, InsurancePolicyCatalog? policies = null)
    {
        var risks = BusinessGovernanceFixtures.BuildRiskCatalog();
        var service = new BusinessRiskValidationService(risks, policies, BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(risk, BusinessGovernanceFixtures.Verified());
    }

    [Fact]
    public async Task ATreatmentOfAcceptWithNoAcceptance_IsAnError()
    {
        var result = await ValidateRiskAsync(BusinessGovernanceFixtures.Risk() with { Treatment = RiskTreatment.Accept });

        Assert.Contains(RiskValidationRules.AcceptTreatmentNeedsAcceptance, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AnUnearnedResidual_IsAnError()
    {
        var result = await ValidateRiskAsync(BusinessGovernanceFixtures.Risk() with
        {
            InherentLikelihood = RiskLikelihood.Likely,
            InherentImpact = RiskImpact.Major,
            ResidualLikelihood = RiskLikelihood.Rare,
            ResidualImpact = RiskImpact.Minor,
        });

        Assert.Contains(RiskValidationRules.ResidualExposureIsUnearned, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task ATransferWithNoPolicy_IsReported()
    {
        var result = await ValidateRiskAsync(BusinessGovernanceFixtures.Risk() with { Treatment = RiskTreatment.Transfer });

        Assert.Contains(RiskValidationRules.TransferTreatmentNeedsPolicy, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AMitigationClaimingToBeInPlaceWithNoEvidence_IsReported()
    {
        var result = await ValidateRiskAsync(BusinessGovernanceFixtures.Risk() with
        {
            Mitigations = [new RiskMitigation("M-1", "Done, apparently.", "owner-1", IsImplemented: true)],
        });

        Assert.Contains(RiskValidationRules.MitigationIsUnevidenced, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AnOverdueMitigation_IsReported()
    {
        var result = await ValidateRiskAsync(BusinessGovernanceFixtures.Risk() with
        {
            Mitigations = [new RiskMitigation("M-1", "Overdue.", "owner-1", DueBy: Today.AddDays(-1))],
        });

        Assert.Contains(RiskValidationRules.MitigationIsOverdue, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public void AMitigationNobodyOwns_CannotBeConstructed()
    {
        Assert.Throws<ArgumentException>(() => new RiskMitigation("M-1", "Somebody will do it.", "  "));
    }

    private static async Task<IValidationResult> ValidatePolicyAsync(InsurancePolicy policy)
    {
        var policies = BusinessGovernanceFixtures.BuildPolicyCatalog();
        var service = new InsurancePolicyValidationService(policies, BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(policy, BusinessGovernanceFixtures.Verified());
    }

    [Fact]
    public async Task APolicyMarkedActiveWhoseCoverEnded_IsAnError()
    {
        // Any risk relying on it is relying on a record nobody updated.
        var result = await ValidatePolicyAsync(BusinessGovernanceFixtures.Policy(to: Today.AddDays(-1)));

        Assert.Contains(RiskValidationRules.ActivePolicyHasExpiredPeriod, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task APolicyExpiringSoonWithNoRenewalArranged_IsReported()
    {
        var result = await ValidatePolicyAsync(BusinessGovernanceFixtures.Policy(to: Today.AddDays(30)) with
        {
            RenewalState = DeterminationState.NotDetermined,
        });

        Assert.Contains(RiskValidationRules.PolicyRenewalNotArranged, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task APolicyWithNoDocument_IsReported()
    {
        var result = await ValidatePolicyAsync(BusinessGovernanceFixtures.Policy() with { PolicyDocumentId = null });

        Assert.Contains(RiskValidationRules.PolicyHasNoDocument, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task TheRegisterPositionFindsWhatTheRegisterConceals()
    {
        var risks = BusinessGovernanceFixtures.BuildRiskCatalog();
        var policies = BusinessGovernanceFixtures.BuildPolicyCatalog();
        var service = new RiskAndInsuranceService(risks, policies);

        await risks.RegisterAsync("rsk-1", BusinessGovernanceFixtures.Risk(), BusinessGovernanceFixtures.Verified());
        await risks.RegisterAsync(
            "rsk-2",
            BusinessGovernanceFixtures.Risk("RSK-2") with
            {
                InherentLikelihood = RiskLikelihood.NotAssessed,
                InherentImpact = RiskImpact.NotAssessed,
                ResidualLikelihood = RiskLikelihood.NotAssessed,
                ResidualImpact = RiskImpact.NotAssessed,
            },
            BusinessGovernanceFixtures.Verified());
        await policies.RegisterAsync(
            "pol-1", BusinessGovernanceFixtures.Policy(to: Today.AddDays(-1)), BusinessGovernanceFixtures.Verified());

        var position = await service.ReportPositionAsync(Today);

        Assert.Contains("RSK-1", position.CarriedWithoutAcceptance);
        Assert.Contains("RSK-2", position.UnassessedRisks);
        Assert.Contains("POL-1", position.LapsedPoliciesStillActive);
        Assert.True(position.HasFindings);
    }
}
