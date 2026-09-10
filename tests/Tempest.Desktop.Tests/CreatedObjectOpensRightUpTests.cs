using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 17.9.4`. The Product Owner's second Windows smoke test created a
/// mechanical object from the Ribbon and it "disappeared into the ether":
/// the Explorer was on another discipline, the project node was collapsed,
/// and nothing opened. The rule now: anything a user creates is taken to
/// and opened for editing, whichever tab they were on.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CreatedObjectOpensRightUpTests
{
    [AvaloniaFact]
    public async Task CreatingAPartFromTheRibbon_WhileTheExplorerShowsAnotherDiscipline_SwitchesRevealsAndOpensIt()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            var workspace = host.Workspace!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-0003", "Smoke Test Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            // The situation the smoke test was in: the Explorer is showing a
            // different discipline, so the mechanical tree is not on screen.
            await workspace.Navigation.SwitchAreaAsync(DocumentsWorkspaceExplorerModule.NavigationItemId);
            var explorer = window.FindUnique<ProjectExplorerView>();
            await explorer.LoadAsync();
            await workspace.Selection.ClearAsync();

            var ribbon = GetPrivateField<RibbonView>(window, "_ribbon");
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            ribbon.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Smoke Test Bracket" });

            Click(ribbon, registry, "mechanical.create");

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            IEngineeringObject? created = null;
            await RenderUntilAsync(window, () =>
            {
                created = domain.Repository.ListByKindAsync("Part").GetAwaiter().GetResult()
                    .FirstOrDefault(o => ((IHasBusinessIdentifier)o).DisplayName == "Smoke Test Bracket");
                return created is not null && explorer.IsRevealed(created.Id) && EditorFor(window, "Smoke Test Bracket") is not null;
            });

            Assert.NotNull(created);
            Assert.Equal(project.Id, ((IHasParent)created!).ParentId);

            // The build is in the title bar, so a stale executable can never pass as the current one again.
            Assert.StartsWith("TempestOS 0.18.0 (", window.Title, StringComparison.Ordinal);

            // Revealed: selected, with every ancestor expanded, in the tree
            // that lists it, which is the mechanical one, not the one that
            // was showing.
            Assert.True(explorer.IsRevealed(created.Id), "The new Part is not selected with its path expanded.");
            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            Assert.Contains(roots, r => r.Id == project.Id);

            // Opened: the editor tab with the object's fields is in the window.
            var editor = EditorFor(window, "Smoke Test Bracket");
            Assert.NotNull(editor);
            Assert.Contains(editor!.GetLogicalDescendants().OfType<TextBox>(), t => t.Text == "Smoke Test Bracket" && t.IsEnabled);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ARequirementCreatedFromThePalettePath_OpensUnderUngrouped_InTheRequirementsArea()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;
            var workspace = host.Workspace!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-0004", "Smoke Test Project 2");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var explorer = window.FindUnique<ProjectExplorerView>();
            await explorer.LoadAsync();

            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var invocation = await registry.InvokeAsync("requirements.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["identifier"] = "REQ-SMOKE-1", ["statement"] = "The bracket shall not drop out of sight." }));
            Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);
            Assert.NotNull(invocation.Result.SubjectId);
            Assert.Equal("Requirement", invocation.Result.SubjectKind);

            // The palette handler in MainWindow does exactly this on success.
            await window.OpenCreatedObjectAsync(invocation.Result.SubjectId!.Value, invocation.Result.SubjectKind!);
            LayOut(window);

            Assert.True(explorer.IsRevealed(invocation.Result.SubjectId.Value), "The new Requirement is not selected with its path expanded.");
            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            Assert.Contains(roots, r => r.Id == RequirementsNodeProvider.UngroupedNodeId);
            // A Requirement opens in its own discipline view (the generic editor
            // cannot resolve a Requirement yet, `TD-41`); what matters here is
            // that a tab for it is open in front of the user.
            Assert.True(TabOpenedFor(window, "REQ-SMOKE-1"), "No document tab shows the new Requirement.");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static bool TabOpenedFor(MainWindow window, string text)
    {
        var documentArea = GetPrivateField<DocumentAreaView>(window, "_documentArea");
        var tabs = GetPrivateField<TabControl>(documentArea, "_tabs");
        return tabs.Items.OfType<TabItem>().Any(tab =>
            (tab.Header?.ToString() ?? string.Empty).Contains(text, StringComparison.Ordinal)
            || (tab.Content as Control)?.GetLogicalDescendants().Any(c =>
                c is TextBlock { Text: { } t } && t.Contains(text, StringComparison.Ordinal)
                || c is TextBox { Text: { } b } && b.Contains(text, StringComparison.Ordinal)) == true);
    }

    private static ObjectEditorView? EditorFor(MainWindow window, string nameOrIdentifier) =>
        window.GetLogicalDescendants().OfType<ObjectEditorView>().Distinct()
            .FirstOrDefault(e => e.GetLogicalDescendants().OfType<TextBox>().Any(t => t.Text == nameOrIdentifier));

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1900, 1050));
            window.Arrange(new Avalonia.Rect(0, 0, 1900, 1050));
        }
    }
}
