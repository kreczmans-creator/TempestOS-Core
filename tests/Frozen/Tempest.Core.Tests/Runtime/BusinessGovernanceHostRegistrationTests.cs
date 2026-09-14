using Tempest.Core.BusinessGovernance.Assets;
using Tempest.Core.BusinessGovernance.Contracts;
using Tempest.Core.BusinessGovernance.Development;
using Tempest.Core.BusinessGovernance.Finance;
using Tempest.Core.BusinessGovernance.Operating;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessGovernance.Risk;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// WP 18.0C (D-028): the archived half of the live
// tests/Tempest.Core.Tests/Runtime/BusinessGovernanceHostRegistrationTests.cs,
// split out the same day RateCard/RateCardCatalog stayed live and
// registered. Contracts, Risk, Assets (IP/data), Finance (Assumption/
// Scenario/Control), Development (Opportunity/Pipeline), Operating,
// Pricing.PricingService.
public class BusinessGovernanceArchivedHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        await body(host);

        await host.StopAsync();
        await runTask;
    }

    [Theory]
    [InlineData(typeof(IContractTemplateCatalog), typeof(ContractTemplateCatalog))]
    [InlineData(typeof(IContractTemplateValidationService), typeof(ContractTemplateValidationService))]
    [InlineData(typeof(IIssuedContractCatalog), typeof(IssuedContractCatalog))]
    [InlineData(typeof(IIssuedContractValidationService), typeof(IssuedContractValidationService))]
    [InlineData(typeof(IContractService), typeof(ContractService))]
    [InlineData(typeof(IBusinessRiskCatalog), typeof(BusinessRiskCatalog))]
    [InlineData(typeof(IBusinessRiskValidationService), typeof(BusinessRiskValidationService))]
    [InlineData(typeof(IInsurancePolicyCatalog), typeof(InsurancePolicyCatalog))]
    [InlineData(typeof(IInsurancePolicyValidationService), typeof(InsurancePolicyValidationService))]
    [InlineData(typeof(IRiskAndInsuranceService), typeof(RiskAndInsuranceService))]
    [InlineData(typeof(IIPAssetCatalog), typeof(IPAssetCatalog))]
    [InlineData(typeof(IIPAssetValidationService), typeof(IPAssetValidationService))]
    [InlineData(typeof(IDataAssetCatalog), typeof(DataAssetCatalog))]
    [InlineData(typeof(IDataAssetValidationService), typeof(DataAssetValidationService))]
    [InlineData(typeof(IPricingService), typeof(PricingService))]
    [InlineData(typeof(IFinancialAssumptionCatalog), typeof(FinancialAssumptionCatalog))]
    [InlineData(typeof(IFinancialAssumptionValidationService), typeof(FinancialAssumptionValidationService))]
    [InlineData(typeof(IFinancialScenarioCatalog), typeof(FinancialScenarioCatalog))]
    [InlineData(typeof(IFinancialScenarioValidationService), typeof(FinancialScenarioValidationService))]
    [InlineData(typeof(IFinancialControlService), typeof(FinancialControlService))]
    [InlineData(typeof(IOpportunityCatalog), typeof(OpportunityCatalog))]
    [InlineData(typeof(IOpportunityValidationService), typeof(OpportunityValidationService))]
    [InlineData(typeof(IPipelineService), typeof(PipelineService))]
    [InlineData(typeof(IOperatingScenarioCatalog), typeof(OperatingScenarioCatalog))]
    [InlineData(typeof(IOperatingScenarioValidationService), typeof(OperatingScenarioValidationService))]
    public async Task Host_RegistersEveryArchivedBusinessGovernanceLibraryAndService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryArchivedBusinessLibrary_IsAnOrdinarySingleton()
    {
        // Two catalogues over one store would each hold their own write
        // locks, and the check-then-write atomicity the shared base
        // depends on would be silently lost.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            foreach (var serviceType in new[]
                     {
                         typeof(IContractTemplateCatalog), typeof(IIssuedContractCatalog), typeof(IBusinessRiskCatalog),
                         typeof(IInsurancePolicyCatalog), typeof(IIPAssetCatalog), typeof(IDataAssetCatalog),
                         typeof(IFinancialAssumptionCatalog), typeof(IFinancialScenarioCatalog),
                         typeof(IOpportunityCatalog), typeof(IOperatingScenarioCatalog),
                     })
            {
                Assert.Same(host.Services!.GetService(serviceType), host.Services!.GetService(serviceType));
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AReasoningService_ReadsTheSameLibraryTheContainerHandsOut()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var contracts = (IIssuedContractCatalog)host.Services!.GetService(typeof(IIssuedContractCatalog))!;
            var service = (IContractService)host.Services!.GetService(typeof(IContractService))!;

            await contracts.RegisterAsync(
                "con-host-probe",
                new IssuedContract
                {
                    Reference = "CON-HOST-PROBE",
                    Title = "Host registration probe",
                    Parties = Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Parties(),
                    Governance = Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Governance(),
                    Obligations =
                    [
                        new ContractObligation("OB-1", "A probe obligation.", "A", "B",
                            DueBy: Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Today.AddDays(-1)),
                    ],
                },
                Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Verified());

            // The service sees what the catalogue wrote, which it could
            // only do if both are the same instance.
            var position = await service.ReportObligationsAsync(
                Tempest.Core.Tests.BusinessGovernance.BusinessGovernanceFixtures.Today);

            Assert.Single(position.OverdueObligations);
        });
    }
}
