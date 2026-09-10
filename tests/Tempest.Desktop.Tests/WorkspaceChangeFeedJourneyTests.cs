using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 18.1A`'s own acceptance journey: create a Part from the Ribbon —
/// the Explorer shows it, the Cockpit's own Recent Activity carries it,
/// and the Property Inspector shows it selected, with no explicit reload
/// call anywhere in this test. Then undo a rename of it, through the real
/// production <see cref="UndoRedoCoordinator"/> — which, since `WP 18.1A`,
/// issues no reload of its own at all — and every one of those three
/// surfaces reverts on its own, from the same <c>WorkspaceChanged</c> the
/// reversing write raises.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorkspaceChangeFeedJourneyTests
{
    [AvaloniaFact]
    public async Task CreateAPart_ThenUndoARename_ExplorerCockpitAndInspectorAllReloadFromTheFeedAlone()
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
            var project = await host.ProjectDirectory!.CreateAsync("P-0006", "Feed Journey Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var explorer = window.FindUnique<ProjectExplorerView>();
            var inspector = window.FindUnique<PropertyInspectorView>();

            // ---- Create, from the Ribbon — no explicit reload below ------
            var ribbon = GetPrivateField<RibbonView>(window, "_ribbon");
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            const string originalName = "Feed Journey Bracket";
            ribbon.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = originalName });

            Click(ribbon, registry, "mechanical.create");

            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            IEngineeringObject? created = null;
            await RenderUntilAsync(window, () =>
            {
                created = domain.Repository.ListByKindAsync("Part").GetAwaiter().GetResult()
                    .FirstOrDefault(o => ((IHasBusinessIdentifier)o).DisplayName == originalName);
                return created is not null && explorer.IsRevealed(created.Id) && EditorFor(window, originalName) is not null;
            });

            Assert.NotNull(created);
            var partId = created!.Id;

            // Explorer shows it (reveal + selection, `WP 17.9.4`, proven above
            // via the same RenderUntilAsync condition).
            Assert.True(explorer.IsRevealed(partId));

            // Cockpit's own count: opening the new Part after creating it
            // records it as Recent Activity — a real EngineeringCockpit read,
            // not a fabricated counter for this test.
            Assert.Contains(workspace.Cockpit.RecentActivity, item => item.ObjectId == partId);

            // Inspector shows it selected.
            Assert.Equal(partId, GetPrivateField<Guid>(inspector, "_currentId"));

            // ---- Rename, then undo through the REAL production coordinator ----
            // Not driven through the Object Editor's own Save button — that is
            // ObjectEditorView's own UI, already covered elsewhere. What this
            // test exists to prove is what happens AFTER the commit: nothing
            // here calls LoadAsync/Refresh/RefreshFromSourceAsync explicitly,
            // on any of the three views, before or after either write.
            const string renamedTo = "Feed Journey Bracket (renamed)";
            var manager = host.Manager!;
            await manager.RenameObjectAsync(partId, "Part", renamedTo);

            var undoRedo = GetPrivateField<UndoRedoCoordinator>(window, "_undoRedo");
            undoRedo.Stack.Record(new UndoableAction(
                $"Rename to '{renamedTo}'",
                undo: ct => manager.RenameObjectAsync(partId, "Part", originalName, ct),
                redo: ct => manager.RenameObjectAsync(partId, "Part", renamedTo, ct)));

            await RenderUntilAsync(window, () => explorer.IsRevealed(partId) && NodeTitle(explorer, partId) == renamedTo);
            Assert.Equal(renamedTo, NodeTitle(explorer, partId));

            await undoRedo.UndoAsync();

            // The Explorer's own tree, the Cockpit, and the Inspector each
            // reload themselves from WorkspaceChanged alone — UndoRedoCoordinator
            // itself (`WP 18.1A`) calls none of them.
            await RenderUntilAsync(window, () => NodeTitle(explorer, partId) == originalName);

            Assert.Equal(originalName, NodeTitle(explorer, partId));
            Assert.True(explorer.IsRevealed(partId), "The reverted Part should still be revealed/selected.");

            var reverted = await domain.Repository.FindAsync(partId);
            Assert.Equal(originalName, ((IHasBusinessIdentifier)reverted!).DisplayName);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The Explorer's own currently-loaded title for <paramref name="objectId"/>
    /// — read from the real, internal <c>ExplorerNodeItem</c> tree
    /// <c>ProjectExplorerView.LoadAsync</c> last built, never re-queried
    /// here: this test's whole point is that nothing needs to ask it to.
    /// </summary>
    private static string? NodeTitle(ProjectExplorerView explorer, Guid objectId) =>
        FindTitle(GetPrivateField<AvaloniaList<ExplorerNodeItem>>(explorer, "_allItems"), objectId);

    private static string? FindTitle(IEnumerable<ExplorerNodeItem> items, Guid objectId)
    {
        foreach (var item in items)
        {
            if (item.Node.Id == objectId)
                return item.Node.Title;

            if (FindTitle(item.Children, objectId) is { } found)
                return found;
        }

        return null;
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
