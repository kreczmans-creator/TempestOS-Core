using System.Text.Json;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// End-to-end `WP 9.2A` Workspace integration test — the same composition
/// <c>Program.cs</c> itself performs (<see cref="CalculationsWorkspaceRegistration.Register"/>,
/// called after <see cref="WorkspaceManager.StartAsync"/> exactly as
/// `Program.cs` calls it), against the real, running <see cref="ITempestHost"/>,
/// the real <see cref="EngineeringCalculationsWorkspaceSampleModule"/>'s own
/// representative graph (which itself depends on the real
/// <see cref="MechanicalProductStructureSampleModule"/>'s and
/// <see cref="RequirementsWorkspaceSampleModule"/>'s own graphs for
/// cross-discipline Digital Thread links), and the real Command Framework
/// — mirroring <c>RequirementsWorkspaceIntegrationTests</c>'s own identical
/// shape.
/// </summary>
[Collection("Console output capture")]
public class CalculationsWorkspaceIntegrationTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager, ITempestHost Host)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder(
        [
            typeof(MechanicalProductStructureSampleModule),
            typeof(RequirementsWorkspaceExplorerModule),
            typeof(RequirementsWorkspaceSampleModule),
            typeof(CalculationsWorkspaceExplorerModule),
            typeof(EngineeringCalculationsWorkspaceSampleModule),
        ])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        var originalOut = Console.Out;
        IWorkspace workspace;
        try
        {
            Console.SetOut(new StringWriter());
            workspace = await manager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        // Same disclosed, pre-existing platform timing characteristic
        // RequirementsWorkspaceIntegrationTests's own StartAsync already
        // works around — waiting on the seeded module's own HasRegistered
        // flag (the last module in ordinal Id order: Mechanical, then
        // Requirements, then this one).
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (GetSeededModule(host) is not { HasRegistered: true } && DateTime.UtcNow < deadline)
            await Task.Delay(5);

        var services = host.Services!;
        var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var calculationEngine = (ICalculationEngine)services.GetService(typeof(ICalculationEngine));
        var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));

        CalculationsWorkspaceRegistration.Register(manager, domainContext, calculationEngine, commandDispatcher, commandRegistry);

        return (workspace, manager, host);
    }

    private static EngineeringCalculationsWorkspaceSampleModule GetSeededModule(ITempestHost host) =>
        (EngineeringCalculationsWorkspaceSampleModule)host.Services!.GetService(typeof(EngineeringCalculationsWorkspaceSampleModule));

    [Fact]
    public async Task ProjectExplorer_CalculationsArea_RootsIncludeTemplatesSetAndUnparentedCalculations()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(CalculationsWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();

        Assert.Contains(roots, n => n.Id == CalculationsNodeProvider.TemplatesNodeId);
        Assert.Contains(roots, n => n.Id == seeded.BoltCalculationSetId);
        Assert.Contains(roots, n => n.Id == seeded.BeamCalculationId);
        Assert.Contains(roots, n => n.Id == seeded.PressureVesselCalculationId);
        Assert.Contains(roots, n => n.Id == seeded.MaterialSelectionCalculationId);
        // Bolt shear and bearing are also un-parented (IHasParent.ParentId
        // is never set for them - only Set membership is) - the same
        // multi-parent tree overlap RequirementsNodeProvider already
        // establishes for Collection membership, so both also appear here,
        // in addition to under the Calculation Set (see the next test).
        Assert.Contains(roots, n => n.Id == seeded.BoltShearCalculationId);
        Assert.Contains(roots, n => n.Id == seeded.BearingCalculationId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_DrillIntoBoltCalculationSet_FindsBoltShearAndBearingChecks()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Navigation.SwitchAreaAsync(CalculationsWorkspaceExplorerModule.NavigationItemId);
        var children = await workspace.ProjectExplorer.GetChildrenAsync(seeded.BoltCalculationSetId!.Value);

        Assert.Equal(2, children.Count);
        Assert.Contains(children, n => n.Id == seeded.BoltShearCalculationId);
        Assert.Contains(children, n => n.Id == seeded.BearingCalculationId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ProjectExplorer_DrillIntoTemplates_FindsAllFiveRepresentativeTemplates()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);

        await workspace.Navigation.SwitchAreaAsync(CalculationsWorkspaceExplorerModule.NavigationItemId);
        var children = await workspace.ProjectExplorer.GetChildrenAsync(CalculationsNodeProvider.TemplatesNodeId);

        Assert.Equal(5, children.Count);
        Assert.Contains(children, n => n.Title == "Bolt Shear Capacity");
        Assert.Contains(children, n => n.Title == "Beam Bending Stress");
        Assert.Contains(children, n => n.Title == "Bearing Load Capacity");
        Assert.Contains(children, n => n.Title == "Pressure Vessel Wall Thickness");
        Assert.Contains(children, n => n.Title == "Material Selection Margin");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_ApprovedBoltShearCalculation_PropertyInspectorShowsApprovedStatus()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.BoltShearCalculationId!.Value, "Calculation");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Status" && f.Value == "Approved");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Approved" && f.Value == "Yes");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Result History" && f.Value == "1 execution(s)");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_MaterialSelectionCalculation_PropertyInspectorShowsConditionalOutcome()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.MaterialSelectionCalculationId!.Value, "Calculation");

        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Latest Result Outcome" && f.Value == "Conditional");
        Assert.Contains(workspace.PropertyInspector.CurrentFacets, f => f.Name == "Referenced Materials" && f.Value.Contains(MaterialsSampleModule.SampleMaterialId));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_BearingCalculation_ShowsBasedOnCalculationDigitalThreadLink()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);

        await workspace.Selection.SelectAsync(seeded.BearingCalculationId!.Value, "Calculation");

        Assert.Contains(
            workspace.PropertyInspector.CurrentFacets,
            f => f.Name == "Based On Calculation(s)" && f.Value == seeded.BoltShearCalculationId!.Value.ToString());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Selection_BoltShearCalculation_ShowsUsedByTheMechanicalWingAssembly()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var seeded = GetSeededModule(host);
        var mechanical = (MechanicalProductStructureSampleModule)host.Services!.GetService(typeof(MechanicalProductStructureSampleModule));

        await workspace.Selection.SelectAsync(seeded.BoltShearCalculationId!.Value, "Calculation");

        Assert.Contains(
            workspace.PropertyInspector.CurrentFacets,
            f => f.Name == "Used By (Digital Thread)" && f.Value.Contains(mechanical.WingAssemblyId!.Value.ToString()));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task CommandRegistry_ListsAllFourteenCalculationsCommands()
    {
        using var temp = new TempDirectory();
        var (_, manager, host) = await StartAsync(temp.Path);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

        var calculationsCommands = commandRegistry.Items.Where(d => d.Category == "Calculations").ToList();

        Assert.Equal(14, calculationsCommands.Count);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FullLifecycle_CreateExecuteRecalculateSetStatusDelete_AllSucceed()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path);
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

        await workspace.Navigation.SwitchAreaAsync(CalculationsWorkspaceExplorerModule.NavigationItemId);

        // Create.
        var createResult = await commandDispatcher.DispatchAsync(
            new CreateCalculationObjectCommand("Calculation", "Integration Test Calculation", "CALC-IT-001"), default);
        Assert.True(createResult.Succeeded);
        var created = (await domainContext.Repository.ListByKindAsync("Calculation"))
            .Single(c => ((IHasBusinessIdentifier)c).Identifier == "CALC-IT-001");

        // Execute — the Material Selection Margin Template, one of the five
        // real Templates EngineeringCalculationsWorkspaceSampleModule
        // registers with the engine and CalculationsWorkspaceRegistration
        // registers with the Workspace's own CalculationTemplateRegistry.
        var inputJson = JsonSerializer.Serialize(new MaterialSelectionMarginInput(
            MaterialsSampleModule.SampleMaterialId, new Quantity<Pressure>(250, PressureUnits.Megapascal), new Quantity<Pressure>(100, PressureUnits.Megapascal)));
        var executeResult = await commandDispatcher.DispatchAsync(
            new ExecuteCalculationCommand(created.Id, "Calculation", MaterialSelectionMarginCalculationDefinition.Id, inputJson), default);
        Assert.True(executeResult.Succeeded);
        Assert.Single(await CalculationRecordReader.GetResultHistoryAsync(domainContext, created.Id));

        // Recalculate.
        var recalculateResult = await commandDispatcher.DispatchAsync(
            new RecalculateCalculationCommand(created.Id, "Calculation", MaterialSelectionMarginCalculationDefinition.Id, inputJson), default);
        Assert.True(recalculateResult.Succeeded);
        Assert.Equal(2, (await CalculationRecordReader.GetResultHistoryAsync(domainContext, created.Id)).Count);

        // Set Status (Lock -> Approved, via the SetCalculationStatusCommand the Lock/Unlock/Review/Approve/Archive palette entries all dispatch through).
        var reviewResult = await commandDispatcher.DispatchAsync(
            new SetCalculationStatusCommand(created.Id, "Calculation", LifecycleState.InReview), default);
        Assert.True(reviewResult.Succeeded);
        var approveResult = await commandDispatcher.DispatchAsync(
            new SetCalculationStatusCommand(created.Id, "Calculation", LifecycleState.Approved), default);
        Assert.True(approveResult.Succeeded);

        // Delete.
        var deleteResult = await commandDispatcher.DispatchAsync(new DeleteCalculationObjectCommand(created.Id, "Calculation"), default);
        Assert.True(deleteResult.Succeeded);

        await manager.ShutdownAsync();
    }

    // ---- Engineering Cockpit KPIs (WP 9.2A), against the real seeded graph ----

    [Fact]
    public async Task Cockpit_CalculationsKpiCards_ReflectTheRealSeededGraph()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        var cards = cockpit.CalculationsKpiCards.ToDictionary(c => c.Label, c => c.Value);

        // Five live Calculations: Bolt Shear (Approved), Bearing (Draft),
        // Beam (InReview), Pressure Vessel (Draft, revised after execution),
        // Material Selection (Draft, Conditional outcome).
        Assert.Equal("5", cards["Total Calculations"]);
        Assert.Equal("1", cards["Approved"]);
        Assert.Equal("1", cards["Review"]);
        Assert.Equal("1", cards["Failed"]);
        Assert.NotEqual("0", cards["Out-of-date"]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Cockpit_AttentionItems_ReportsCalculationsAreLive()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Contains(cockpit.AttentionItems, item => item.Title == "Calculations are live");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Cockpit_CalculationStatus_IsBlockedBecauseOfTheConditionalOutcome()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path);
        var cockpit = ((Tempest.App.Workspace.Workspace)workspace).Cockpit;

        Assert.Equal(EngineeringHealthStatus.Blocked, cockpit.CalculationStatus);

        await manager.ShutdownAsync();
    }
}
