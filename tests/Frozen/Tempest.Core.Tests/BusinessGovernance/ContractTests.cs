using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Contracts;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.BusinessGovernance;

// C1's whole claim is that revising a template cannot alter a contract
// already issued from it. Most of these tests exist to hold that.
public class ContractTests
{
    private static DateOnly Today => BusinessGovernanceFixtures.Today;

    private static async Task<(ContractTemplateCatalog Templates, ContractService Service)> ServiceAsync(
        ContractTemplate? template = null,
        bool release = true)
    {
        var templates = BusinessGovernanceFixtures.BuildTemplateCatalog();
        var contracts = BusinessGovernanceFixtures.BuildContractCatalog();

        await templates.RegisterAsync("tpl-1", template ?? BusinessGovernanceFixtures.Template(), BusinessGovernanceFixtures.Verified());

        if (release)
            await BusinessGovernanceFixtures.ReleaseAsync(templates, "tpl-1");

        return (templates, new ContractService(templates, contracts, new CurrentPrincipalAccessor()));
    }

    [Fact]
    public async Task AContractPreparedFromATemplate_PinsTheRevisionItRead()
    {
        var (templates, service) = await ServiceAsync();
        var released = await templates.FindByCodeAsync("CT-CONSULT-1");

        var contract = await service.PrepareFromTemplateAsync(
            "CT-CONSULT-1", "CON-1", "Fixture engagement",
            BusinessGovernanceFixtures.Parties(), BusinessGovernanceFixtures.Governance());

        Assert.Equal(released!.RevisionNumber, contract.TemplatePin!.RevisionNumber);
        Assert.Equal("BusinessContractTemplates", contract.TemplatePin.Library);
        Assert.True(contract.IsFromTemplate);
    }

    [Fact]
    public async Task APreparedContract_IsADraft_AndAlreadyStatesTheAuthorityItWillNeed()
    {
        // A contract sitting unsigned should report as waiting on a named
        // person, not merely as incomplete.
        var (_, service) = await ServiceAsync();

        var contract = await service.PrepareFromTemplateAsync(
            "CT-CONSULT-1", "CON-1", "Fixture engagement",
            BusinessGovernanceFixtures.Parties(), BusinessGovernanceFixtures.Governance());

        Assert.Equal(ContractStatus.Draft, contract.Status);
        Assert.False(contract.IsBinding);
        Assert.Contains(
            contract.Governance.OutstandingAuthorities,
            a => a.Kind == BusinessAuthorityKind.CommercialCommitment && a.HasNamedHolder);
    }

    [Fact]
    public async Task NothingInTheContractServiceExecutesAContract()
    {
        // A structural guard. If somebody later adds an Execute or Sign
        // method, this is what stops it.
        await Task.CompletedTask;

        var methods = typeof(IContractService).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(methods, name =>
            name.Contains("Execute", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Sign", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Approve", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Commit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnUnreleasedTemplate_IsRefusedRatherThanUsed()
    {
        var (_, service) = await ServiceAsync(release: false);

        await Assert.ThrowsAsync<UnreleasedContractTemplateException>(
            () => service.PrepareFromTemplateAsync(
                "CT-CONSULT-1", "CON-1", "Fixture engagement",
                BusinessGovernanceFixtures.Parties(), BusinessGovernanceFixtures.Governance()));
    }

    [Fact]
    public async Task ARevisedTemplate_DoesNotAlterAContractAlreadyIssuedFromIt()
    {
        // The guarantee C1 exists to give. The template is superseded by a
        // successor with different terms; the issued contract still
        // resolves to the wording it was drawn from.
        var (templates, service) = await ServiceAsync();

        var contract = await service.PrepareFromTemplateAsync(
            "CT-CONSULT-1", "CON-1", "Fixture engagement",
            BusinessGovernanceFixtures.Parties(), BusinessGovernanceFixtures.Governance());

        var successor = BusinessGovernanceFixtures.Template("CT-CONSULT-2") with
        {
            DefaultCommercialTerms = BusinessGovernanceFixtures.Terms() with
            {
                LiabilityCap = BusinessGovernanceFixtures.Gbp_(50_000m),
            },
        };

        await templates.RegisterAsync("tpl-2", successor, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(templates, "tpl-2");
        await templates.SupersedeAsync("tpl-1", "tpl-2", "Liability cap reduced.");

        var resolved = await service.ResolveTemplateAsync(contract);

        Assert.Equal("CT-CONSULT-1", resolved!.Definition.Code);
        Assert.Equal(
            BusinessGovernanceFixtures.Gbp_(250_000m),
            resolved.Definition.DefaultCommercialTerms!.LiabilityCap);
    }

    [Fact]
    public async Task AReleasedTemplate_CannotBeEditedInPlace()
    {
        var (templates, _) = await ServiceAsync();

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(
            () => templates.ReviseAsync(
                "tpl-1",
                BusinessGovernanceFixtures.Template() with { Purpose = "Something else." },
                BusinessGovernanceFixtures.Verified(),
                "Attempted in-place edit."));
    }

    private static async Task<IValidationResult> ValidateContractAsync(
        IssuedContract contract,
        ContractTemplateCatalog? templates = null)
    {
        var contracts = BusinessGovernanceFixtures.BuildContractCatalog();
        var service = new IssuedContractValidationService(contracts, templates, BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(contract, BusinessGovernanceFixtures.Verified());
    }

    private static IssuedContract Contract(ContractStatus status = ContractStatus.Draft) => new()
    {
        Reference = "CON-1",
        Title = "Fixture engagement",
        Parties = BusinessGovernanceFixtures.Parties(),
        Governance = BusinessGovernanceFixtures.Governance(),
        Status = status,
        CommercialTerms = BusinessGovernanceFixtures.Terms(),
        TemplatePin = new ReferencePin("BusinessContractTemplates", "tpl-1", 1),
    };

    [Fact]
    public async Task AnExecutedContractWithNobodysCommercialAuthority_IsAnError()
    {
        // A contract binds the organisation; who bound it must be on the
        // record.
        var result = await ValidateContractAsync(Contract(ContractStatus.Executed) with
        {
            ExecutedOn = Today,
            Term = new EffectivePeriod(Today, Today.AddYears(1)),
        });

        Assert.Contains(
            ContractValidationRules.ExecutedContractNeedsCommitmentAuthority,
            result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AnExecutedContractWithNoDate_IsAnError()
    {
        var result = await ValidateContractAsync(Contract(ContractStatus.Executed) with
        {
            Governance = BusinessGovernanceFixtures.Governance() with
            {
                Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.CommercialCommitment)],
            },
        });

        Assert.Contains(ContractValidationRules.ExecutedContractNeedsDate, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AContractStillMarkedExecutedWhoseTermEnded_IsReported()
    {
        var result = await ValidateContractAsync(Contract(ContractStatus.Executed) with
        {
            ExecutedOn = Today.AddYears(-2),
            Term = new EffectivePeriod(Today.AddYears(-2), Today.AddMonths(-1)),
            Governance = BusinessGovernanceFixtures.Governance() with
            {
                Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.CommercialCommitment)],
            },
        });

        Assert.Contains(ContractValidationRules.ContractTermHasEnded, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AContractWithOneParty_IsAnError()
    {
        var result = await ValidateContractAsync(Contract() with
        {
            Parties = [BusinessGovernanceFixtures.Parties()[0]],
        });

        Assert.Contains(ContractValidationRules.ContractNeedsTwoParties, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task ABespokeContract_IsReported_NotRejected()
    {
        // The other party's paper is legitimate and carries none of the
        // template library's review.
        var result = await ValidateContractAsync(Contract() with { TemplatePin = null });

        Assert.Contains(ContractValidationRules.ContractIsBespoke, result.Warnings.Select(d => d.Code));
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ADepartureFromAClauseNeedingLegalReview_IsReported()
    {
        var templates = BusinessGovernanceFixtures.BuildTemplateCatalog();

        await templates.RegisterAsync("tpl-1", BusinessGovernanceFixtures.Template(), BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(templates, "tpl-1");

        var record = await templates.FindByCodeAsync("CT-CONSULT-1");

        var result = await ValidateContractAsync(
            Contract() with
            {
                TemplatePin = ReferencePin.For(templates.LibraryName, record!),
                Departures =
                [
                    new TemplateDeparture("7", "Liability cap raised to unlimited.", "Client insisted."),
                ],
            },
            templates);

        Assert.Contains(ContractValidationRules.DepartureNeedsLegalReview, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ATemplatePinNamingARevisionTheLibraryDoesNotHold_IsReported()
    {
        var templates = BusinessGovernanceFixtures.BuildTemplateCatalog();

        var result = await ValidateContractAsync(
            Contract() with { TemplatePin = new ReferencePin("BusinessContractTemplates", "nope", 1) },
            templates);

        Assert.Contains(ContractValidationRules.TemplatePinMustResolve, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task PaymentTermsThatDoNotAddUp_AreReported()
    {
        var result = await ValidateContractAsync(Contract() with
        {
            CommercialTerms = BusinessGovernanceFixtures.Terms() with
            {
                PaymentTerms =
                [
                    new PaymentTerm(PaymentTrigger.OnMilestone, "Half on start.", PercentageOfTotal: 50m, DaysToPay: 30),
                    new PaymentTerm(PaymentTrigger.OnCompletion, "A third on completion.", PercentageOfTotal: 30m, DaysToPay: 30),
                ],
            },
        });

        Assert.Contains(ContractValidationRules.PaymentTermsDoNotTotal, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AContractWithNoLiabilityCap_IsReported()
    {
        var result = await ValidateContractAsync(Contract() with
        {
            CommercialTerms = BusinessGovernanceFixtures.Terms() with { LiabilityCap = null },
        });

        Assert.Contains(ContractValidationRules.ContractHasNoLiabilityCap, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ATemplateOmittingAnExpectedCategory_IsReported()
    {
        var templates = BusinessGovernanceFixtures.BuildTemplateCatalog();
        var service = new ContractTemplateValidationService(templates, BusinessGovernanceFixtures.Clock());

        var template = BusinessGovernanceFixtures.Template() with
        {
            Clauses = [new ContractClause("1", "Parties", ClauseCategory.Parties)],
        };

        var result = await service.ValidateDefinitionAsync(template, BusinessGovernanceFixtures.Verified());

        Assert.Contains(ContractValidationRules.TemplateOmitsExpectedCategory, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ATemplateWithNoLegalReview_IsReported()
    {
        var templates = BusinessGovernanceFixtures.BuildTemplateCatalog();
        var service = new ContractTemplateValidationService(templates, BusinessGovernanceFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            BusinessGovernanceFixtures.Template() with { LegalReviewState = DeterminationState.NotDetermined },
            BusinessGovernanceFixtures.Verified());

        Assert.Contains(ContractValidationRules.TemplateHasNoLegalReview, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task TheObligationReport_FindsOverdueObligationsAndStaleExecutions()
    {
        var contracts = BusinessGovernanceFixtures.BuildContractCatalog();
        var templates = BusinessGovernanceFixtures.BuildTemplateCatalog();
        var service = new ContractService(templates, contracts, new CurrentPrincipalAccessor());

        await contracts.RegisterAsync(
            "con-1",
            Contract(ContractStatus.Executed) with
            {
                ExecutedOn = Today.AddYears(-2),
                Term = new EffectivePeriod(Today.AddYears(-2), Today.AddMonths(-1)),
                Obligations =
                [
                    new ContractObligation("OB-1", "Deliver the fixture report.", "TestFixture Engineering Ltd",
                        "Fictional Client Ltd", DueBy: Today.AddDays(-10)),
                ],
            },
            BusinessGovernanceFixtures.Verified());

        var position = await service.ReportObligationsAsync(Today);

        Assert.Equal("OB-1", Assert.Single(position.OverdueObligations).Obligation.Reference);
        Assert.Contains("CON-1", position.ExpiredButStillExecuted);
        Assert.Contains("CON-1", position.AwaitingCommitmentAuthority);
        Assert.True(position.HasFindings);
    }

    [Fact]
    public async Task ASupersededContract_IsLeftOutOfTheObligationReport()
    {
        // Reporting a superseded record's obligations as live would
        // double-count them against the record that replaced it.
        var contracts = BusinessGovernanceFixtures.BuildContractCatalog();
        var templates = BusinessGovernanceFixtures.BuildTemplateCatalog();
        var service = new ContractService(templates, contracts, new CurrentPrincipalAccessor());

        var overdue = new ContractObligation("OB-1", "Deliver.", "A", "B", DueBy: Today.AddDays(-10));

        await contracts.RegisterAsync("con-1", Contract() with { Obligations = [overdue] }, BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(contracts, "con-1");
        await contracts.RegisterAsync(
            "con-2",
            Contract() with { Reference = "CON-2", Obligations = [overdue] },
            BusinessGovernanceFixtures.Verified());
        await BusinessGovernanceFixtures.ReleaseAsync(contracts, "con-2");
        await contracts.SupersedeAsync("con-1", "con-2", "Replaced by CON-2.");

        var position = await service.ReportObligationsAsync(Today);

        Assert.Equal("CON-2", Assert.Single(position.OverdueObligations).ContractReference);
    }

    [Fact]
    public void ADeliverableAcceptedByNobody_IsNotAccepted()
    {
        // All three conditions, not just the state flag.
        var stateOnly = new ContractDeliverable("D-1", "A report.", AcceptanceState: DeterminationState.Recorded);
        var complete = new ContractDeliverable(
            "D-2", "A report.", AcceptanceState: DeterminationState.Recorded,
            AcceptedOn: Today, AcceptedByPrincipalId: "client-1");

        Assert.False(stateOnly.IsAccepted);
        Assert.True(complete.IsAccepted);
    }

    [Fact]
    public void ADepartureWithoutAReason_CannotBeConstructed()
    {
        Assert.Throws<ArgumentException>(() => new TemplateDeparture("7", "Cap removed.", "  "));
    }

    [Fact]
    public void AnExpiredContractHasStillBeenExecuted_AndItsSurvivingObligationsStillBind()
    {
        Assert.True(ContractStatuses.HasBeenExecuted(ContractStatus.Expired));
        Assert.True(ContractStatuses.HasBeenExecuted(ContractStatus.Terminated));
        Assert.False(ContractStatuses.IsBinding(ContractStatus.Expired));
        Assert.False(ContractStatuses.SupportsContractedRevenue(ContractStatus.InNegotiation));
    }
}
