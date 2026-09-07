using Tempest.Core.Configuration;
using Tempest.Core.EngineeringIntelligence;
using Tempest.Core.EngineeringIntelligence.Decisions;
using Tempest.Core.EngineeringIntelligence.DesignRules;
using Tempest.Core.EngineeringIntelligence.MaterialSelection;
using Tempest.Core.EngineeringIntelligence.Reviews;
using Tempest.Core.EngineeringIntelligence.TradeStudies;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation for `Group B` (P02): proves the reasoning layer
// is wired into the real, unmodified TempestHost, that each catalogue is a
// single instance over one store, and that a reasoning service reads the
// same catalogue the container hands out rather than a second one.
[Collection("Console output capture")]
public class EngineeringIntelligenceHostRegistrationTests
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
    [InlineData(typeof(IRuleCatalog), typeof(RuleCatalog))]
    [InlineData(typeof(IRuleValidationService), typeof(RuleValidationService))]
    [InlineData(typeof(IDecisionTreeCatalog), typeof(DecisionTreeCatalog))]
    [InlineData(typeof(IDecisionTreeValidationService), typeof(DecisionTreeValidationService))]
    [InlineData(typeof(IReviewDefinitionCatalog), typeof(ReviewDefinitionCatalog))]
    [InlineData(typeof(IReviewDefinitionValidationService), typeof(ReviewDefinitionValidationService))]
    [InlineData(typeof(ITradeStudyCatalog), typeof(TradeStudyCatalog))]
    [InlineData(typeof(ITradeStudyValidationService), typeof(TradeStudyValidationService))]
    [InlineData(typeof(IMaterialSelectionService), typeof(MaterialSelectionService))]
    [InlineData(typeof(IManufacturingDecisionService), typeof(ManufacturingDecisionService))]
    [InlineData(typeof(IDesignRuleService), typeof(DesignRuleService))]
    [InlineData(typeof(IEngineeringReviewService), typeof(EngineeringReviewService))]
    [InlineData(typeof(ITradeStudyService), typeof(TradeStudyService))]
    public async Task Host_RegistersEveryReasoningLibraryAndService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryReasoningLibrary_IsAnOrdinarySingleton()
    {
        // Two catalogues over one store would each hold their own write
        // locks, and the check-then-write atomicity the shared base
        // depends on would be silently lost.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            foreach (var serviceType in new[]
                     {
                         typeof(IRuleCatalog), typeof(IDecisionTreeCatalog),
                         typeof(IReviewDefinitionCatalog), typeof(ITradeStudyCatalog),
                     })
            {
                Assert.Same(host.Services!.GetService(serviceType), host.Services!.GetService(serviceType));
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AReasoningService_ReadsTheSameRuleLibraryTheContainerHandsOut()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var rules = (IRuleCatalog)host.Services!.GetService(typeof(IRuleCatalog))!;
            var designRules = (IDesignRuleService)host.Services!.GetService(typeof(IDesignRuleService))!;

            await rules.RegisterAsync(
                "rule-host-probe",
                new RuleDefinition
                {
                    Code = "HOST-PROBE-1",
                    Name = "Host registration probe",
                    Statement = "A probe rule registered by a test. Not real engineering guidance.",
                    Severity = RuleSeverity.Requirement,
                    Applicability = new RuleApplicability { SubjectKinds = [AssessmentSubjectKinds.Material] },
                    Condition = new PropertyRecordedExpression("YieldStrength"),
                },
                new ReferenceProvenance(
                    SourceOrganisation: "TestFixture Engineering",
                    SourceDocument: "Host registration probe (not a real publication)",
                    ExtractionMethod: ReferenceExtractionMethod.ManualTranscription)
                {
                    VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
                    ReviewerPrincipalId = "reviewer-1",
                    VerificationDate = new DateOnly(2026, 2, 1),
                });

            await rules.SetValidationStateAsync("rule-host-probe", ReferenceValidationState.Checked, "Checked.");
            await rules.SetValidationStateAsync("rule-host-probe", ReferenceValidationState.Validated, "Rules pass.");
            await rules.SetValidationStateAsync("rule-host-probe", ReferenceValidationState.Released, "Released.");

            // The design-rule service sees what the catalogue wrote, which
            // it could only do if both are the same instance.
            var assessment = await designRules.AssessAsync(new HostProbeSubject());

            Assert.Equal(1, assessment.Scope.RunRuleCount);
        });
    }

    [Fact]
    public async Task TheReasoningLayerRegisters_WithoutAnyGroupARegistrationChanging()
    {
        // `P02` reads `P01` and never the other way round. If wiring the
        // reasoning layer had needed a reference library to change, this
        // is where that would show up.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType<Tempest.Core.Materials.MaterialCatalog>(
                host.Services!.GetService(typeof(Tempest.Core.Materials.IMaterialCatalog)));
            Assert.IsType<Tempest.Core.Manufacturing.ProcessCatalog>(
                host.Services!.GetService(typeof(Tempest.Core.Manufacturing.IProcessCatalog)));
            Assert.IsType<RuleCatalog>(host.Services!.GetService(typeof(IRuleCatalog)));

            return Task.CompletedTask;
        });
    }

    /// <summary>A minimal subject, so the probe does not depend on any reference library's own adapter.</summary>
    private sealed class HostProbeSubject : IAssessmentSubject
    {
        public string SubjectKind => AssessmentSubjectKinds.Material;

        public string SubjectId => "host-probe";

        public string DisplayName => "Host registration probe subject";

        public string? Family => null;

        public bool IsApplicabilityKnown => false;

        public ReferencePin? Pin => null;

        public SubjectQuantity GetQuantity(string propertyName) => SubjectQuantity.NotRecorded;

        public SubjectText GetText(string attributeName) => SubjectText.NotRecorded;
    }
}
