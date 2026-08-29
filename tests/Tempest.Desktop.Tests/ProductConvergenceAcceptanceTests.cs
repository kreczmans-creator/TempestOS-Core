using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Shell;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The five mandatory convergence journeys (`TD-89`), driven end to end
/// through the real <see cref="MainWindow"/> over a real
/// <see cref="WorkspaceHost"/>.
/// </summary>
/// <remarks>
/// <para>
/// These prove the product model, not the screens: at every stage each
/// journey asserts the <b>application state</b> (navigation location,
/// project context, engineering scope, the real object graph) together
/// with the surface the shell actually rendered — so a window that looks
/// right over the wrong context fails, and correct state that the shell
/// does not follow fails too.
/// </para>
/// <para>
/// Journey 3 is the one that would previously have been impossible:
/// engineering work with no project at all.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProductConvergenceAcceptanceTests
{
    private static EngineeringDomainContext DomainOf(WorkspaceHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    private static ICommandDispatcher DispatcherOf(WorkspaceHost host) =>
        (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

    // ================================================================
    // Journey 1 — New project
    // ================================================================

    [AvaloniaFact]
    public async Task Journey1_NewProject_ThroughToEngineeringAndBack()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            // Launch → Home
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            var scope = host.EngineeringScope!;

            Assert.Equal(ShellArea.Home, navigator.Current.Area);

            // → Projects
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            Assert.NotNull(window.GetLogicalDescendants().OfType<ProjectBrowserView>().SingleOrDefault());

            // → Create project
            var project = await host.ProjectDirectory!.CreateAsync("P-0100", "Convergence Trial");

            // → Project Workspace
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(project.Id, host.ProjectContext!.Current!.Id);
            Assert.NotNull(window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().SingleOrDefault());

            // → Engineering, in this project's scope
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);
            Assert.Equal(EngineeringScopeKind.Project, scope.Current.Kind);
            Assert.Equal(project.Id, scope.Current.ProjectId);

            // → Create an engineering object through the real production command
            var result = await DispatcherOf(host).DispatchAsync(new CreateMechanicalObjectCommand(
                MechanicalObjectFactoryRegistry.Assembly, "Trial Assembly", "ASM-900", parentId: project.Id),
                CancellationToken.None);
            Assert.True(result.Succeeded, result.Message);

            var inScope = await scope.ListObjectsAsync();
            Assert.Contains(inScope, o => ((IHasBusinessIdentifier)o).Identifier == "ASM-900");

            // → Return to the project, context intact
            await navigator.ReturnToProjectAsync();
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(project.Id, host.ProjectContext.Current!.Id);
            Assert.Contains(
                await host.ProjectDirectory.ListProjectContentsAsync(project.Id),
                id => inScope.Any(o => o.Id == id));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // Journey 2 — Existing project, calculation, validation
    // ================================================================

    [AvaloniaFact]
    public async Task Journey2_ExistingProject_EngineeringWorkStaysAssociatedWithIt()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid projectId;
        Guid partId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            _ = new MainWindow(first);
            var project = await first.ProjectDirectory!.CreateAsync("P-0200", "Existing Project");
            projectId = project.Id;

            var domain = DomainOf(first);
            var part = (Part)await new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, domain,
                (d, r) => new Part(d, r, domain, "PN-200", "Bracket", EngineeringObjectMetadata.Empty))
                .CreateAsync("Bracket for the existing project.");
            partId = part.Id;
            await ((IHasParent)part).MoveAsync(projectId);
        }
        finally
        {
            await first.ShutdownAsync();
            await first.DisposeAsync();
        }

        // Launch → open the existing project
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);
            var navigator = second.ShellNavigator!;

            await navigator.OpenProjectAsync(projectId);
            await window.RenderCurrentModuleAsync();
            Assert.Equal(projectId, second.ProjectContext!.Current!.Id);

            // → Engineering
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            Assert.Equal(EngineeringScopeKind.Project, second.EngineeringScope!.Current.Kind);

            // → Open a calculation against the rehydrated part, through the
            //   real template registry, and validate the object.
            var input = new BoltShearCapacityInput(
                new Quantity<Length>(12, LengthUnits.Millimetre),
                new Quantity<Pressure>(400, PressureUnits.Megapascal),
                ShearPlanes: 2,
                SafetyFactor: 1.5);

            var summary = await second.CalculationTemplates!.ExecuteAsync(
                BoltShearCapacityCalculationDefinition.Id, partId, JsonSerializer.Serialize(input));

            Assert.NotEqual(Guid.Empty, summary.RecordId);

            var part = (Part)(await DomainOf(second).Repository.FindAsync(partId))!;
            var validation = await part.ValidateAsync();
            Assert.NotNull(validation);

            // The calculation is linked to the part, which is in the project.
            var relationships = await part.GetRelationshipsAsync();
            Assert.Contains(relationships, r =>
                r.RelationshipKind == CalculationTemplateRegistry.CalculatedByRelationshipKind && r.TargetId == summary.RecordId);

            // → Return to the project: the work is still its work.
            await navigator.ReturnToProjectAsync();
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Contains(partId, await second.ProjectDirectory!.ListProjectContentsAsync(projectId));
            Assert.True(await second.EngineeringScope.ContainsAsync(partId));
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Journey 3 — Standalone calculation, with no project at all
    // ================================================================

    [AvaloniaFact]
    public async Task Journey3_StandaloneCalculation_NeedsNoProject_AndSurvivesAReopen()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid calculationId;
        Guid calculationSetId;
        Guid recordId;

        var first = new WorkspaceHost(root);
        try
        {
            // Launch → Engineering, with no project open at all.
            await first.StartAsync();
            var window = new MainWindow(first);
            var navigator = first.ShellNavigator!;

            Assert.False(first.ProjectContext!.HasProject);

            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);
            Assert.True(navigator.Current.IsStandaloneEngineering);
            Assert.Equal(EngineeringScopeKind.Standalone, first.EngineeringScope!.Current.Kind);
            Assert.False(first.ProjectContext.HasProject);

            // → A standalone Calculation, through the real production command.
            var dispatcher = DispatcherOf(first);
            var created = await dispatcher.DispatchAsync(new CreateCalculationObjectCommand(
                CalculationObjectFactoryRegistry.CalculationKind, "Quick Bolt Check", "QC-1"), CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);

            var domain = DomainOf(first);
            var calculation = (await domain.Repository.ListByKindAsync(CalculationObjectFactoryRegistry.CalculationKind))
                .Single(o => ((IHasBusinessIdentifier)o).Identifier == "QC-1");
            calculationId = calculation.Id;

            // It belongs to no project — that is the whole point.
            Assert.Null(((IHasParent)calculation).ParentId);
            Assert.True(await first.EngineeringScope.ContainsAsync(calculationId));

            // → Run the calculation, for real.
            var input = new BoltShearCapacityInput(
                new Quantity<Length>(10, LengthUnits.Millimetre),
                new Quantity<Pressure>(500, PressureUnits.Megapascal),
                ShearPlanes: 1,
                SafetyFactor: 2.0);

            var summary = await first.CalculationTemplates!.ExecuteAsync(
                BoltShearCapacityCalculationDefinition.Id, calculationId, JsonSerializer.Serialize(input));
            recordId = summary.RecordId;
            Assert.NotEqual(Guid.Empty, recordId);

            // → Save it into a Calculation Set.
            var setResult = await dispatcher.DispatchAsync(new CreateCalculationObjectCommand(
                CalculationObjectFactoryRegistry.CalculationSetKind, "Quick Checks", "QS-1",
                memberCalculationIds: [calculationId]), CancellationToken.None);
            Assert.True(setResult.Succeeded, setResult.Message);

            calculationSetId = (await domain.Repository.ListByKindAsync(CalculationObjectFactoryRegistry.CalculationSetKind))
                .Single(o => ((IHasBusinessIdentifier)o).Identifier == "QS-1").Id;

            await first.ShutdownAsync();
        }
        finally
        {
            await first.DisposeAsync();
        }

        // → Close and reopen: the standalone work comes back, still with no project.
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);
            await window.RenderCurrentModuleAsync();

            Assert.False(second.ProjectContext!.HasProject);

            var domain = DomainOf(second);
            var calculation = Assert.IsType<Calculation>(await domain.Repository.FindAsync(calculationId));
            Assert.Equal("QC-1", calculation.Identifier);
            Assert.Equal("Quick Bolt Check", calculation.DisplayName);
            Assert.Null(calculation.ParentId);

            var set = Assert.IsType<CalculationSet>(await domain.Repository.FindAsync(calculationSetId));
            Assert.Contains(calculationId, set.MemberCalculationIds);

            // The recorded execution is still readable, and still linked.
            var relationships = await calculation.GetRelationshipsAsync();
            Assert.Contains(relationships, r =>
                r.RelationshipKind == CalculationTemplateRegistry.CalculatedByRelationshipKind && r.TargetId == recordId);

            // And it is still standalone: in the standalone scope, in no project.
            await second.ShellNavigator!.GoToEngineeringAsync();
            Assert.Equal(EngineeringScopeKind.Standalone, second.EngineeringScope!.Current.Kind);

            var inScope = await second.EngineeringScope.ListObjectsAsync();
            Assert.Contains(inScope, o => o.Id == calculationId);
            Assert.Contains(inScope, o => o.Id == calculationSetId);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Journey 4 — Context restoration
    // ================================================================

    [AvaloniaFact]
    public async Task Journey4_ProjectAndEngineeringContext_AreRestoredAcrossARestart()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        Guid projectId;
        Guid partId;

        var first = new WorkspaceHost(root);
        try
        {
            await first.StartAsync();
            _ = new MainWindow(first);

            var project = await first.ProjectDirectory!.CreateAsync("P-0400", "Restored Project");
            projectId = project.Id;

            await first.ShellNavigator!.OpenProjectAsync(projectId);

            var domain = DomainOf(first);
            var part = (Part)await new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, domain,
                (d, r) => new Part(d, r, domain, "PN-400", "Restored Part", EngineeringObjectMetadata.Empty))
                .CreateAsync("Part in the restored project.");
            partId = part.Id;
            await ((IHasParent)part).MoveAsync(projectId);

            // The user was in Engineering when they closed the application.
            await first.ShellNavigator.GoToEngineeringAsync();
            await first.ShutdownAsync();
        }
        finally
        {
            await first.DisposeAsync();
        }

        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second);
            await window.RenderCurrentModuleAsync();

            // Location, project context and engineering scope all restored.
            Assert.Equal(ShellArea.Engineering, second.ShellNavigator!.Current.Area);
            Assert.Equal(projectId, second.ShellNavigator.Current.ProjectId);
            Assert.True(second.ProjectContext!.HasProject);
            Assert.Equal(projectId, second.ProjectContext.Current!.Id);
            Assert.Equal(EngineeringScopeKind.Project, second.EngineeringScope!.Current.Kind);

            // And the engineering work itself is back, in scope.
            var inScope = await second.EngineeringScope.ListObjectsAsync();
            Assert.Contains(inScope, o => o.Id == partId);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Journey 5 — Navigation integrity
    // ================================================================

    [AvaloniaFact]
    public async Task Journey5_ContextIsCorrectAtEveryStageOfAFullNavigationCircuit()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            var context = host.ProjectContext!;
            var scope = host.EngineeringScope!;

            // 1. A global module — declared, unimplemented, and still real.
            await navigator.GoToModuleAsync(ShellArea.Tasks);
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.Tasks, navigator.Current.Area);
            Assert.False(context.HasProject);
            var declared = window.GetLogicalDescendants().OfType<DeclaredCapabilityView>().SingleOrDefault();
            Assert.NotNull(declared);

            // 2. Project
            var project = await host.ProjectDirectory!.CreateAsync("P-0500", "Circuit");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(project.Id, context.Current!.Id);

            // 3. A project area — declared, unimplemented, still project-aware.
            await navigator.GoToProjectAreaAsync(ProjectArea.Risks);
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ProjectArea.Risks, navigator.Current.ProjectArea);
            Assert.Equal(project.Id, navigator.Current.ProjectId);
            Assert.Equal(project.Id, context.Current!.Id);

            // 4. Engineering, in the project's scope
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);
            Assert.Equal(project.Id, scope.Current.ProjectId);
            Assert.Equal(EngineeringScopeKind.Project, scope.Current.Kind);

            // 5. An engineering object, genuinely in that project
            var domain = DomainOf(host);
            var part = (Part)await new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, domain,
                (d, r) => new Part(d, r, domain, "PN-500", "Circuit Part", EngineeringObjectMetadata.Empty))
                .CreateAsync("Part in the circuit project.");
            await ((IHasParent)part).MoveAsync(project.Id);

            Assert.True(await scope.ContainsAsync(part.Id));

            // 6. Back to the project — context preserved throughout
            await navigator.ReturnToProjectAsync();
            await window.RenderCurrentModuleAsync();

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(project.Id, navigator.Current.ProjectId);
            Assert.Equal(project.Id, context.Current!.Id);
            Assert.Contains(part.Id, await host.ProjectDirectory.ListProjectContentsAsync(project.Id));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // The shell surface itself
    // ================================================================

    [AvaloniaFact]
    public async Task TheRail_OffersEveryDesignedModule_AndMarksTheOnesWithNoCapabilityBehindThem()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();
            var buttons = rail.GetLogicalDescendants().OfType<Button>().ToList();

            foreach (var module in ShellAreas.RailModules)
            {
                var button = buttons.SingleOrDefault(
                    b => Avalonia.Automation.AutomationProperties.GetName(b) == module.Title);
                Assert.True(button is not null, $"The rail must offer the '{module.Title}' module.");

                var help = Avalonia.Automation.AutomationProperties.GetHelpText(button!) ?? string.Empty;
                if (module.Availability == NavigationAvailability.Declared)
                    Assert.Contains(DeclaredCapabilityView.NotImplementedBadge, help, StringComparison.Ordinal);
                else
                    Assert.DoesNotContain(DeclaredCapabilityView.NotImplementedBadge, help, StringComparison.Ordinal);
            }
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheProjectWorkspace_OffersEveryDesignedArea_AndNamesTheOpenProjectOnDeclaredOnes()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var project = await host.ProjectDirectory!.CreateAsync("P-0600", "Tabbed");
            await host.ShellNavigator!.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var workspace = window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().Single();
            var tabs = workspace.GetLogicalDescendants().OfType<TabItem>().ToList();

            Assert.Equal(ProjectAreas.All.Count, tabs.Count);
            foreach (var area in ProjectAreas.All)
                Assert.Contains(tabs, t => Equals(t.Tag, area.Area));

            // A declared-only area is project-aware: it names the open project.
            await host.ShellNavigator.GoToProjectAreaAsync(ProjectArea.Timeline);
            await window.RenderCurrentModuleAsync();

            var declared = workspace.GetLogicalDescendants().OfType<DeclaredCapabilityView>().ToList();
            var texts = declared.SelectMany(d => d.GetLogicalDescendants().OfType<TextBlock>())
                .Select(t => t.Text ?? string.Empty).ToList();

            Assert.Contains(texts, t => t.Contains("P-0600 Tabbed", StringComparison.Ordinal));
            Assert.Contains(texts, t => t.Contains("TD-81", StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
