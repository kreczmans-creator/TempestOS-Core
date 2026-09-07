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

// Registration validation for `Group C` (P07): proves the business-
// governance layer is wired into the real, unmodified TempestHost, that
// each library is one instance over one store, and that adding it changed
// nothing about `Group A` or `Group B`.
[Collection("Console output capture")]
public class BusinessGovernanceHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            await body(host);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }
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
    [InlineData(typeof(IRateCardCatalog), typeof(RateCardCatalog))]
    [InlineData(typeof(IRateCardValidationService), typeof(RateCardValidationService))]
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
    public async Task Host_RegistersEveryBusinessGovernanceLibraryAndService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryBusinessLibrary_IsAnOrdinarySingleton()
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
                         typeof(IRateCardCatalog), typeof(IFinancialAssumptionCatalog), typeof(IFinancialScenarioCatalog),
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

    [Fact]
    public async Task AddingP07ChangedNothingAboutP01OrP02()
    {
        // P07 reads the platform's own document store, persistence and
        // identity. It does not read the reference libraries or the
        // reasoning layer, and neither of those changed to accommodate it.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType<Tempest.Core.Materials.MaterialCatalog>(
                host.Services!.GetService(typeof(Tempest.Core.Materials.IMaterialCatalog)));
            Assert.IsType<Tempest.Core.EngineeringIntelligence.RuleCatalog>(
                host.Services!.GetService(typeof(Tempest.Core.EngineeringIntelligence.IRuleCatalog)));
            Assert.IsType<ContractTemplateCatalog>(host.Services!.GetService(typeof(IContractTemplateCatalog)));

            return Task.CompletedTask;
        });
    }
}
