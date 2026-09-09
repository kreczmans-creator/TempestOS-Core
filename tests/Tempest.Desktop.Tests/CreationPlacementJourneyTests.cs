using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Views;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 17.9.3` (`TD-172`): what the design-freeze surface audit found about
/// where a created object goes, discipline by discipline, walked through the
/// shell's own context and the real command registry. Every object a user
/// makes must land where they are looking, or the tree must say where it is.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CreationPlacementJourneyTests
{
    private static Task<CommandInvocation> InvokeAsync(ICommandRegistry registry, string id, CommandContext context, params (string Name, string Value)[] values) =>
        registry.InvokeAsync(id, context, (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(values.ToDictionary(v => v.Name, v => v.Value)));

    private static async Task<(WorkspaceHost Host, MainWindow Window, Guid ProjectId, CommandPaletteOverlay Palette, ICommandRegistry Registry)> OpenAProjectAsync()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var window = new MainWindow(host);
        var navigator = host.ShellNavigator!;

        await navigator.GoToProjectsAsync();
        await window.RenderCurrentModuleAsync();
        var project = await host.ProjectDirectory!.CreateAsync("P-0002", "Placement Test Project");
        await navigator.OpenProjectAsync(project.Id);
        await window.RenderCurrentModuleAsync();
        await navigator.GoToEngineeringAsync();
        await window.RenderCurrentModuleAsync();
        Assert.Equal(ShellArea.Engineering, navigator.Current.Area);

        var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
        var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        return (host, window, project.Id, palette, registry);
    }

    [AvaloniaFact]
    public async Task ADocumentCreatedWithNothingSelected_LandsUnderTheOpenProject_AndIsStillListedUnderItsCategory()
    {
        var (host, _, projectId, palette, registry) = await OpenAProjectAsync();
        try
        {
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(DocumentsWorkspaceExplorerModule.NavigationItemId);
            await workspace.Selection.ClearAsync();

            var invocation = await InvokeAsync(registry, "documents.create", palette.ContextSource!(), ("kind", "Document"), ("displayName", "Placement Test Spec"));
            Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
            Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var created = (await domain.Repository.ListByKindAsync("Document")).Single(o => ((IHasBusinessIdentifier)o).DisplayName == "Placement Test Spec");
            Assert.Equal(projectId, ((IHasParent)created).ParentId);

            // Still findable in the Documents tree: a document placed under a
            // project is a category member, not a vanished one.
            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var listed = false;
            foreach (var root in roots.Where(r => r.HasChildren))
                listed |= (await workspace.ProjectExplorer.GetChildrenAsync(root.Id)).Any(n => n.Id == created.Id);
            Assert.True(listed, "The document placed under the project is not listed under any category of the Documents tree.");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ACalculationCreatedWithNothingSelected_LandsUnderTheOpenProject_AndIsARootOfTheCalculationsTree()
    {
        var (host, _, projectId, palette, registry) = await OpenAProjectAsync();
        try
        {
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(CalculationsWorkspaceExplorerModule.NavigationItemId);
            await workspace.Selection.ClearAsync();

            var invocation = await InvokeAsync(registry, "calculations.create", palette.ContextSource!(), ("kind", "Calculation"), ("displayName", "Placement Test Calc"));
            Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
            Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var created = (await domain.Repository.ListByKindAsync("Calculation")).Single(o => ((IHasBusinessIdentifier)o).DisplayName == "Placement Test Calc");
            Assert.Equal(projectId, ((IHasParent)created).ParentId);

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            Assert.Contains(roots, n => n.Id == created.Id);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ARequirementCreatedWithNothingSelected_IsListedUnderUngrouped_AndOneCreatedWithAGroupSelected_GoesIntoThatGroup()
    {
        var (host, _, _, palette, registry) = await OpenAProjectAsync();
        try
        {
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(RequirementsWorkspaceExplorerModule.NavigationItemId);
            await workspace.Selection.ClearAsync();

            var loose = await InvokeAsync(registry, "requirements.create", palette.ContextSource!(), ("identifier", "REQ-PL-1"), ("statement", "The bracket shall carry 12 kN."));
            Assert.True(loose.Result!.Succeeded, loose.Result.Message);
            Assert.Contains("Ungrouped", loose.Result.Message, StringComparison.Ordinal);

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var ungrouped = Assert.Single(roots, r => r.Id == RequirementsNodeProvider.UngroupedNodeId);
            Assert.Contains(await workspace.ProjectExplorer.GetChildrenAsync(ungrouped.Id), n => n.Title.StartsWith("REQ-PL-1", StringComparison.Ordinal));

            var group = await InvokeAsync(registry, "requirements.create-group", palette.ContextSource!(), ("name", "Structural"));
            Assert.True(group.Result!.Succeeded, group.Result.Message);
            var groupNode = Assert.Single(await workspace.ProjectExplorer.GetRootNodesAsync(), r => r.Title == "Structural");

            await workspace.Selection.SelectAsync(groupNode.Id, groupNode.Kind!);
            var grouped = await InvokeAsync(registry, "requirements.create", palette.ContextSource!(), ("identifier", "REQ-PL-2"), ("statement", "The bracket shall weigh under 50 g."));
            Assert.True(grouped.Result!.Succeeded, grouped.Result.Message);
            Assert.Contains("Structural", grouped.Result.Message, StringComparison.Ordinal);
            Assert.Contains(await workspace.ProjectExplorer.GetChildrenAsync(groupNode.Id), n => n.Title.StartsWith("REQ-PL-2", StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AManufacturingOperation_NeedsAPartSelected_AndSaysSo_ThenSucceedsAgainstOne()
    {
        var (host, _, projectId, palette, registry) = await OpenAProjectAsync();
        try
        {
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await workspace.Selection.ClearAsync();

            var refused = await InvokeAsync(registry, "manufacturing.create", palette.ContextSource!(), ("kind", "ManufacturingOperation"), ("displayName", "Drill 4 holes"), ("method", "Inspection"));
            Assert.Equal(CommandOutcome.Executed, refused.Outcome);
            Assert.False(refused.Result!.Succeeded);
            Assert.Contains("Select the Part", refused.Result.Message, StringComparison.Ordinal);

            var part = await InvokeAsync(registry, "mechanical.create", palette.ContextSource!(), ("kind", "Part"), ("displayName", "Placement Test Bracket"));
            Assert.True(part.Result!.Succeeded, part.Result.Message);
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var bracket = (await domain.Repository.ListByKindAsync("Part")).Single(o => ((IHasBusinessIdentifier)o).DisplayName == "Placement Test Bracket");
            Assert.Equal(projectId, ((IHasParent)bracket).ParentId);

            await workspace.Selection.SelectAsync(bracket.Id, "Part");
            var operation = await InvokeAsync(registry, "manufacturing.create", palette.ContextSource!(), ("kind", "ManufacturingOperation"), ("displayName", "Drill 4 holes"), ("method", "Inspection"));
            Assert.True(operation.Result!.Succeeded, operation.Result.Message);

            var created = (await domain.Repository.ListByKindAsync("ManufacturingOperation")).Single(o => ((IHasBusinessIdentifier)o).DisplayName == "Drill 4 holes");
            Assert.Equal(bracket.Id, ((IManufacturingOperation)created).PartId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ThePropertyInspector_ShowsAParentByName_NotByGuid()
    {
        var (host, window, projectId, palette, registry) = await OpenAProjectAsync();
        try
        {
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await workspace.Selection.ClearAsync();

            var part = await InvokeAsync(registry, "mechanical.create", palette.ContextSource!(), ("kind", "Part"), ("displayName", "Named Parent Test Part"));
            Assert.True(part.Result!.Succeeded, part.Result.Message);
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var created = (await domain.Repository.ListByKindAsync("Part")).Single(o => ((IHasBusinessIdentifier)o).DisplayName == "Named Parent Test Part");

            var inspector = GetPrivateField<PropertyInspectorView>(window, "_inspectorView");
            inspector.SetCurrentSelection(created.Id, "Part");
            await inspector.RefreshFromSourceAsync();
            Dispatcher.UIThread.RunJobs();

            var texts = inspector.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(texts, t => t.Contains("Placement Test Project (Project)", StringComparison.Ordinal));
            Assert.DoesNotContain(texts, t => t == projectId.ToString());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
