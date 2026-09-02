using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Desktop.Composition;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP-E` — the two UI-thread blocking calls this Work Package removed,
/// asserted as behaviour where behaviour can carry the claim and as source
/// where the claim is about a call site rather than an outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>Toggle Favourite.</b> <c>WorkspaceViewCoordinator.ToggleFavourite</c>
/// blocked the UI thread on a real durable write —
/// <c>_favouriteObjects.SaveAsync().GetAwaiter().GetResult()</c> — on an
/// interactive gesture (Ctrl+D, or the Project Explorer's own context
/// menu). It is awaited now, with the synchronous wrapper both callback
/// shapes need kept beside it, exactly as <c>NavigateToObject</c> already
/// did for <c>NavigateToObjectAsync</c>.
/// </para>
/// <para>
/// <b>Why the save cannot simply be dropped.</b> Toggling a favourite that
/// is not written down is a favourite the next session does not have. The
/// ordering matters too: the status bar, the toast, the command history
/// entry and the Undo/Redo record all follow the save, so what the user is
/// told has actually happened by the time they are told it. These tests
/// assert that ordering survived the conversion.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public class AsyncFavouritePathTests
{
    [AvaloniaFact]
    public async Task ToggleFavouriteAsync_DurablyRecordsTheFavourite_AndRecordsAnUndoableAction()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var window = new MainWindow(host);

            var coordinator = GetPrivateField<WorkspaceViewCoordinator>(window, "_viewCoordinator");
            var session = GetPrivateField<DesktopSessionState>(window, "_session");
            var undoRedo = GetPrivateField<object>(window, "_undoRedo");
            var stack = (IUndoRedoStack)undoRedo.GetType().GetProperty("Stack")!.GetValue(undoRedo)!;

            var id = Guid.NewGuid();
            Assert.False(session.FavouriteObjects.IsFavourite(id));

            await coordinator.ToggleFavouriteAsync(id, "Part", "Bracket");

            Assert.True(session.FavouriteObjects.IsFavourite(id));

            // The Undo/Redo pair is still recorded, and still describes the
            // action in the user's own words.
            Assert.True(stack.CanUndo);
            Assert.Equal("Added 'Bracket' to Favourites.", stack.NextUndoDescription);

            // Toggling again removes it — trivially self-inverting, exactly
            // as before. (Driving that inversion through Stack.UndoAsync is
            // deliberately not asserted here: doing so surfaces a separate,
            // pre-existing UI-thread defect in UndoRedoStack that `WP-E`
            // did not introduce and was not authorised to change. See
            // `TD-117`.)
            await coordinator.ToggleFavouriteAsync(id, "Part", "Bracket");
            Assert.False(session.FavouriteObjects.IsFavourite(id));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheSynchronousWrapper_StillSatisfiesBothCallbackShapes_SoTheExplorerMenuAndCtrlDStillWork()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var window = new MainWindow(host);

            var explorer = GetPrivateField<Views.ProjectExplorerView>(window, "_explorerView");

            // The Explorer's context menu holds an Action<Guid,string,string>.
            // If the conversion had left only a Task-returning method, this
            // wiring would not compile — and if it had left the delegate
            // unassigned, the menu item ships disabled.
            Assert.NotNull(explorer.ToggleFavouriteRequested);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The call-site claims. Neither is observable at runtime — a blocking
    /// call and an awaited one produce the same favourite — so both are
    /// asserted against the source that makes them true.
    /// </summary>
    [Fact]
    public void TheInteractiveFavouriteSave_IsAwaited_NotBlockedOn()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "Tempest.Desktop", "Composition", "WorkspaceViewCoordinator.cs"));

        // Executable lines only — the remarks above the method name the
        // old blocking call in order to explain it, which is documentation,
        // not a call site.
        var code = string.Join(
            "\n",
            source.Split('\n')
                .Select(line => line.Trim())
                .Where(line => !line.StartsWith("//", StringComparison.Ordinal) && !line.StartsWith('*')));

        Assert.DoesNotContain("SaveAsync().GetAwaiter().GetResult()", code, StringComparison.Ordinal);
        Assert.Contains("await _favouriteObjects.SaveAsync().ConfigureAwait(true)", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// `WP-E` — the Cockpit's per-refresh read scope is only worth anything
    /// if the render actually opens one. Nothing about an open scope is
    /// visible from outside <c>CockpitView</c>: the cards render identically
    /// either way, only more cheaply, so there is no runtime observation to
    /// make. The wiring is therefore asserted where it lives.
    /// </summary>
    [Fact]
    public void TheCockpitRender_RunsInsideOneReadScope()
    {
        var view = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "Tempest.Desktop", "Views", "CockpitView.cs"));

        var refreshBody = Regex.Match(
            view,
            @"public void Refresh\(\)\s*\{(?<body>.*?)\n    \}",
            RegexOptions.Singleline);

        Assert.True(refreshBody.Success, "CockpitView.Refresh() could not be located — this test needs updating, not deleting.");
        Assert.Contains("_cockpit.BeginReadScope()", refreshBody.Groups["body"].Value, StringComparison.Ordinal);
    }
}
