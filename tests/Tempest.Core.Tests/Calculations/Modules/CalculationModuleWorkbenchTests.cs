using Tempest.Core.Bearings;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Engineering;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The calculator workbench (`WP 21.7B`, completed by `WP 21.7C`) over a
/// real host: every run is named as a Calculation object, Re-run and
/// Compare go through the canonical <c>calculations.rerun</c> and
/// <c>calculations.compare-with-previous</c> commands, a second Calculate
/// records against the same object through <c>calculations.execute</c>,
/// and every product calculation is a Template the commands can reach.
/// </summary>
public class CalculationModuleWorkbenchTests
{
    private const string EngineerId = "engineer-21-7c";

    private static CalculationModuleDescriptor Module(string id) => CalculationModuleDescriptors.For(id)!;

    private static CalculationFormField F(string name, string text, string? unit = null) => new(name, text, unit);

    private static List<CalculationFormField> BoltShear(string safetyFactor) =>
        [F("Diameter", "20", "mm"), F("UltimateShearStrength", "400", "MPa"), F("ShearPlanes", "2"), F("SafetyFactor", safetyFactor)];

    [Fact]
    public void TheCatalogue_ListsAllSixteenProductCalculations_GroupedByCategory()
    {
        var groups = CalculationModuleWorkbench.Catalogue();

        Assert.Equal(16, groups.Sum(g => g.Modules.Count));
        Assert.All(groups, g => Assert.NotEmpty(g.Modules));
        Assert.Equal(groups.Count, groups.Select(g => g.Category).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(groups, g => g.Category == "Structural" && g.Modules.Any(m => m.Id == BeamDeflectionCalculationDefinition.Id) && g.Modules.Any(m => m.Id == BoltShearCapacityCalculationDefinition.Id));
        Assert.Contains(groups, g => g.Category == "Fatigue" && g.Modules.Single().Id == FatigueMinerCalculationDefinition.Id);
    }

    [Fact]
    public async Task EveryProductCalculation_IsATemplate_OnceTheWorkbenchExists()
    {
        using var temp = new TempDirectory();
        var session = await StartAsync(temp.Path);

        foreach (var module in CalculationModuleDescriptors.All)
            Assert.NotNull(session.Templates.FindByCalculationId(module.Id));

        // Registering again is harmless: nothing is registered twice.
        CalculationModuleWorkbench.RegisterMissingTemplates(session.Templates);
        Assert.Equal(CalculationModuleDescriptors.All.Count, CalculationModuleDescriptors.All.Count(m => session.Templates.FindByCalculationId(m.Id) is not null));
    }

    [Fact]
    public async Task Calculate_NamesTheRunAsACalculationObject_AndListsItInTheRegister()
    {
        using var temp = new TempDirectory();
        var session = await StartAsync(temp.Path);

        var attempt = await session.Workbench.CalculateAsync(Module(BoltShearCapacityCalculationDefinition.Id), BoltShear("1.5"), "Bracket bolts");

        Assert.True(attempt.Succeeded, attempt.Outcome.Reason);
        var run = attempt.Run!;
        Assert.Equal("Bracket bolts", run.DisplayName);
        Assert.Equal(1, run.RunCount);
        Assert.NotEqual(Guid.Empty, run.CalculationObjectId);
        Assert.Contains(run.Run.Results, r => r.Label == "Allowable shear capacity" && r.Display == "167552 N");

        var named = (await session.Register.ListAsync()).Single(n => n.ObjectId == run.CalculationObjectId);
        Assert.Equal(run.Run.RecordId, named.RecordId);
        Assert.Equal("Bracket bolts", named.DisplayName);

        // No name given: the title and the time — and two runs in the same
        // second still get their own names rather than a refusal.
        var unnamed = await session.Workbench.CalculateAsync(Module(BoltShearCapacityCalculationDefinition.Id), BoltShear("1.5"));
        var again = await session.Workbench.CalculateAsync(Module(BoltShearCapacityCalculationDefinition.Id), BoltShear("1.5"));
        Assert.True(unnamed.Succeeded, unnamed.Outcome.Reason);
        Assert.True(again.Succeeded, again.Outcome.Reason);
        Assert.StartsWith(Module(BoltShearCapacityCalculationDefinition.Id).Title, unnamed.Run!.DisplayName, StringComparison.Ordinal);
        Assert.NotEqual(unnamed.Run.DisplayName, again.Run!.DisplayName);
        Assert.NotEqual(run.CalculationObjectId, unnamed.Run.CalculationObjectId);
        Assert.NotEqual(unnamed.Run.CalculationObjectId, again.Run.CalculationObjectId);

        // A name the engineer typed twice is refused in the domain's own words.
        await Assert.ThrowsAsync<Tempest.Core.EngineeringDomain.DuplicateBusinessIdentifierException>(
            () => session.Workbench.CalculateAsync(Module(BoltShearCapacityCalculationDefinition.Id), BoltShear("1.5"), "Bracket bolts"));
    }

    [Fact]
    public async Task AFailedCalculate_NamesNothing()
    {
        using var temp = new TempDirectory();
        var session = await StartAsync(temp.Path);
        var before = (await session.Register.ListAsync()).Count;

        var mistyped = await session.Workbench.CalculateAsync(Module(BoltShearCapacityCalculationDefinition.Id), BoltShear("abc"));
        Assert.False(mistyped.Succeeded);
        Assert.Equal(CalculationModuleRefusal.InputIncomplete, mistyped.Outcome.Refusal);
        Assert.Contains(mistyped.Outcome.Problems, p => p.InputName == "SafetyFactor");

        var rejected = await session.Workbench.CalculateAsync(Module(BoltShearCapacityCalculationDefinition.Id), BoltShear("0"));
        Assert.False(rejected.Succeeded);
        Assert.Equal(CalculationModuleRefusal.InputInvalid, rejected.Outcome.Refusal);

        Assert.Equal(before, (await session.Register.ListAsync()).Count);
    }

    [Fact]
    public async Task Rerun_GoesThroughTheCanonicalCommand_AndShowsTheNewRecordWithItsPredecessor()
    {
        using var temp = new TempDirectory();
        var session = await StartAsync(temp.Path);
        var first = (await session.Workbench.CalculateAsync(Module(BoltShearCapacityCalculationDefinition.Id), BoltShear("1.5"), "Bracket bolts")).Run!;

        var rerun = await session.Workbench.RerunAsync(first);

        Assert.True(rerun.Succeeded, rerun.Outcome.Reason);
        var second = rerun.Run!;
        Assert.Equal(first.CalculationObjectId, second.CalculationObjectId);
        Assert.Equal("Bracket bolts", second.DisplayName);
        Assert.Equal(2, second.RunCount);
        Assert.NotEqual(first.Run.RecordId, second.Run.RecordId);
        Assert.Equal(first.Run.RecordId, second.Run.PredecessorRecordId);
        Assert.Equal(first.Run.Results, second.Run.Results);
        Assert.Contains("calc.bolt-shear-capacity", rerun.CommandMessage, StringComparison.Ordinal);

        // The compare command, on the identical input, finds nothing changed.
        var comparison = await session.Workbench.CompareAsync(second);
        Assert.False(comparison.HasChanges);
        Assert.Empty(comparison.Rows);
        Assert.Equal(first.Run.RecordId, comparison.Comparison.RecordIdA);
        Assert.Equal(second.Run.RecordId, comparison.Comparison.RecordIdB);
    }

    [Fact]
    public async Task CalculateAgainOnTheSameCalculation_RecordsAgainstIt_AndCompareTabulatesWhatChanged()
    {
        using var temp = new TempDirectory();
        var session = await StartAsync(temp.Path);
        var module = Module(BoltShearCapacityCalculationDefinition.Id);
        var first = (await session.Workbench.CalculateAsync(module, BoltShear("1.5"), "Bracket bolts")).Run!;

        // Compare needs two records; with one it refuses in the command's words.
        var refused = await Assert.ThrowsAsync<CalculationException>(() => session.Workbench.CompareAsync(first));
        Assert.Contains("fewer than two", refused.Message, StringComparison.Ordinal);

        var again = await session.Workbench.CalculateAsync(module, BoltShear("2"), onto: first);

        Assert.True(again.Succeeded, again.Outcome.Reason);
        var second = again.Run!;
        Assert.Equal(first.CalculationObjectId, second.CalculationObjectId);
        Assert.Equal(2, second.RunCount);
        Assert.Contains(second.Run.Results, r => r.Label == "Allowable shear capacity" && r.Display == "125664 N");
        Assert.Contains("calc.bolt-shear-capacity", again.CommandMessage, StringComparison.Ordinal);

        var comparison = await session.Workbench.CompareAsync(second);

        Assert.True(comparison.HasChanges);
        var factor = Assert.Single(comparison.Rows, r => r.Section == "Input" && r.Field == "Safety factor");
        Assert.Equal("1.5", factor.Before);
        Assert.Equal("2", factor.After);
        var capacity = Assert.Single(comparison.Rows, r => r.Section == "Result" && r.Field == "Allowable shear capacity");
        Assert.NotEqual(capacity.Before, capacity.After);
        Assert.DoesNotContain(comparison.Rows, r => r.Field == "Diameter");
        Assert.Equal(first.Run.RecordId, comparison.Comparison.RecordIdA);
        Assert.Equal(second.Run.RecordId, comparison.Comparison.RecordIdB);

        // A different module never records onto another module's calculation.
        var other = await session.Workbench.CalculateAsync(
            Module(FatigueMinerCalculationDefinition.Id),
            [F("CurveReference", "EN 1993-1-9 detail category 71"), F("ReferenceStressRange", "71", "MPa"), F("ReferenceCycles", "2000000"), F("Slope", "3"), new("Blocks", Rows: ["100 MPa, 100000"])],
            onto: second);
        Assert.True(other.Succeeded, other.Outcome.Reason);
        Assert.NotEqual(second.CalculationObjectId, other.Run!.CalculationObjectId);
        Assert.Equal(1, other.Run.RunCount);

        // A rejected input on the same calculation records nothing and says why.
        var rejected = await session.Workbench.CalculateAsync(module, BoltShear("0"), onto: second);
        Assert.False(rejected.Succeeded);
        Assert.Equal(CalculationModuleRefusal.InputInvalid, rejected.Outcome.Refusal);
        Assert.Equal(2, (await CalculationRecordReader.GetResultHistoryAsync(session.Domain, second.CalculationObjectId)).Count);
    }

    [Fact]
    public async Task APinnedMaterial_SurvivesTheCanonicalRerun_AndIsCitedAgain()
    {
        using var temp = new TempDirectory();
        var session = await StartAsync(temp.Path);
        await new ReferenceSeedService().ApplyAsync(session.Materials, MaterialSeed.Instance);
        var review = new ReferenceReviewService(session.Principals);
        await review.VerifyAsync(session.Materials, MaterialSeed.S355J2, new ReferenceReviewStatement("Siderticino datasheet, mechanical properties table"));
        await review.ReleaseAsync(session.Materials, MaterialSeed.S355J2, "Required for a WP 21.7C workbench test.");

        var first = await session.Workbench.CalculateAsync(
            Module(BeamDeflectionCalculationDefinition.Id),
            [
                new("MaterialPin", RecordId: MaterialSeed.S355J2),
                new("Support", Choice: nameof(BeamSupport.SimplySupported)), new("Loading", Choice: nameof(BeamLoading.PointLoad)),
                F("Load", "10", "kN"), F("Span", "2000", "mm"), F("SecondMomentOfArea", "2000000", "mm^4"), F("ExtremeFibreDistance", "50", "mm"), F("DeflectionLimit", "8", "mm"),
            ],
            "Beam on S355J2");

        Assert.True(first.Succeeded, first.Outcome.Reason);
        Assert.Contains(MaterialSeed.S355J2, first.Run!.Run.ReferencedMaterialIds);

        var rerun = await session.Workbench.RerunAsync(first.Run);
        Assert.True(rerun.Succeeded, rerun.Outcome.Reason);
        Assert.Contains(MaterialSeed.S355J2, rerun.Run!.Run.ReferencedMaterialIds);
        Assert.Contains(rerun.Run.Run.Results, r => r.Label == "Maximum deflection" && r.Display == "3.96825 mm");

        // A tighter limit recorded onto the same calculation: the comparison
        // table reads the outcome as words and the utilisation to six figures.
        var tighter = await session.Workbench.CalculateAsync(
            Module(BeamDeflectionCalculationDefinition.Id),
            [
                new("MaterialPin", RecordId: MaterialSeed.S355J2),
                new("Support", Choice: nameof(BeamSupport.SimplySupported)), new("Loading", Choice: nameof(BeamLoading.PointLoad)),
                F("Load", "10", "kN"), F("Span", "2000", "mm"), F("SecondMomentOfArea", "2000000", "mm^4"), F("ExtremeFibreDistance", "50", "mm"), F("DeflectionLimit", "3", "mm"),
            ],
            onto: rerun.Run);
        Assert.True(tighter.Succeeded, tighter.Outcome.Reason);

        var comparison = await session.Workbench.CompareAsync(tighter.Run!);
        Assert.Contains(comparison.Rows, r => r.Section == "Input" && r.Field == "Deflection limit" && r.Before == "8 mm" && r.After == "3 mm");
        Assert.Contains(comparison.Rows, r => r.Section == "Result" && r.Field == "Outcome" && r.Before == "Meets criteria" && r.After == "Does not meet criteria");
        Assert.Contains(comparison.Rows, r => r.Section == "Result" && r.Field == "Deflection utilisation" && r.Before == "0.496032" && r.After == "1.32275");
    }

    // ---- The host ----

    private sealed record Session(
        CalculationModuleWorkbench Workbench,
        CalculationTemplateRegistry Templates,
        EngineeringCalculationRegister Register,
        EngineeringDomainContext Domain,
        IMaterialCatalog Materials,
        CurrentPrincipalAccessor Principals);

    private static async Task<Session> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder([])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        await manager.StartAsync();

        var services = host.Services!;
        var domain = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var dispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var engine = (ICalculationEngine)services.GetService(typeof(ICalculationEngine));
        var principals = (CurrentPrincipalAccessor)services.GetService(typeof(ICurrentPrincipalAccessor));
        principals.SetCurrent(new PlatformPrincipal(new PlatformIdentity(EngineerId, EngineerId), []));

        var templates = CalculationsWorkspaceRegistration.Register(
            manager, domain, engine, dispatcher, (ICommandRegistry)services.GetService(typeof(ICommandRegistry)));

        var materials = (IMaterialCatalog)services.GetService(typeof(IMaterialCatalog));
        var service = new CalculationModuleService(
            materials,
            (IFastenerCatalog)services.GetService(typeof(IFastenerCatalog)),
            (IBearingCatalog)services.GetService(typeof(IBearingCatalog)),
            engine);
        var register = new EngineeringCalculationRegister(domain, dispatcher);
        var workbench = new CalculationModuleWorkbench(service, templates, register, dispatcher, domain);

        return new Session(workbench, templates, register, domain, materials, principals);
    }
}
