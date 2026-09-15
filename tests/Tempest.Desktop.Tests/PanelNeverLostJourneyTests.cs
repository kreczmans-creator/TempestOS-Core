using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// PO finding T4 (`WP 20.10D`), read literally: "Structure → dock the
/// Requirements tree beside a requirement's editor; edit the requirement's
/// title — Its disappeared somewhere and broken away - need to review it
/// all." Driven through the real window exactly as the application reaches
/// this surface: open a project, enter its Structure tab, open a
/// requirement's editor, and drag the Requirements tree panel (the Project
/// Explorer, in this context — the same panel every discipline's own tree
/// shares, `WP 19.2B`) by its own tab header along the path a user would
/// take to dock it beside that editor.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class PanelNeverLostJourneyTests
{
    [AvaloniaFact]
    public async Task DraggingTheRequirementsTreeBesideTheEditor_NeverLosesThePanel_EitherWay()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-T4", "Panel Never Lost Project");

            // Enter the project's Structure tab — the exact surface T4 was
            // on ("Structure → dock the Requirements tree...").
            await navigator.OpenProjectAsync(project.Id, ProjectArea.Engineering);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            // Open a requirement's editor — the surface the Requirements
            // tree was being dragged beside.
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
            var invocation = await registry.InvokeAsync(
                "requirements.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["identifier"] = "REQ-T4-1", ["statement"] = "The Requirements tree shall never be lost." }));
            Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);
            await window.OpenCreatedObjectAsync(invocation.Result.SubjectId!.Value, invocation.Result.SubjectKind!);
            LayOut(window);

            var dockingComposer = GetPrivateField<WorkspaceDockingComposer>(window, "_dockingComposer");
            var controller = dockingComposer.Layout;

            // The Requirements tree's own tab header — `LayoutTabGroupView`
            // tags every tab Button with its panel id.
            var explorerTabButton = window.GetLogicalDescendants().OfType<Button>()
                .First(b => Equals(b.Tag, dockingComposer.ExplorerPanelId));

            var documentGroupId = controller.Tree.FindGroupContaining(dockingComposer.DocumentPanelId)!.Id;
            var documentCandidate = controller.CurrentCandidates().Single(c => c.NodeId == documentGroupId);

            var pointer = new Pointer(0, PointerType.Mouse, true);

            // --- Gesture 1: the accidental miss T4 describes ---
            // Press on the Requirements tree's own tab, move across the
            // drop targets toward the open requirement editor, and release
            // a couple of pixels short of its own edge — inside the
            // workspace, but over no candidate. Before this Work Package
            // this floated the panel at the wrong screen coordinates, with
            // no owner and nothing listing it — exactly "disappeared
            // somewhere and broken away".
            PressTab(explorerTabButton, pointer);
            var nearMiss = new Point(documentCandidate.X - 2, documentCandidate.Y + documentCandidate.Height / 2);
            MovePointer(controller.Host, pointer, nearMiss);
            ReleasePointer(controller.Host, pointer, nearMiss);

            Assert.False(controller.Tree.IsFloating(dockingComposer.ExplorerPanelId));
            Assert.Contains(dockingComposer.ExplorerPanelId, controller.Tree.DockedPanels);
            Assert.Empty(controller.FloatingWindows);

            // The open requirement editor itself is untouched by the miss.
            var editor = window.GetLogicalDescendants().OfType<TextBox>().FirstOrDefault(t => t.Text == "REQ-T4-1");
            Assert.NotNull(editor);

            // --- Gesture 2: the one deliberate tear-out ---
            // Released past the workspace's own edge entirely, not merely
            // over no candidate.
            PressTab(explorerTabButton, pointer);
            var tornOut = new Point(-40, controller.Host.Bounds.Height / 2);
            MovePointer(controller.Host, pointer, tornOut);
            ReleasePointer(controller.Host, pointer, tornOut);

            Assert.True(controller.Tree.IsFloating(dockingComposer.ExplorerPanelId));
            var floatingWindow = Assert.Single(controller.FloatingWindows).Value;

            // Owned by the shell, and shown above it — the two guarantees
            // that make a floating window findable at all rather than
            // disappearing behind the main window or off-screen.
            Assert.Same(window, floatingWindow.Owner);
            Assert.True(floatingWindow.IsVisible);

            // Closing it (the title bar's own close button — never routed
            // through the model) redocks the Requirements tree instead of
            // discarding it.
            floatingWindow.Close();

            Assert.False(controller.Tree.IsFloating(dockingComposer.ExplorerPanelId));
            Assert.Contains(dockingComposer.ExplorerPanelId, controller.Tree.DockedPanels);
            Assert.Empty(controller.FloatingWindows);

            // And "Show Panel" from the Command Palette reaches it too,
            // regardless of how it was lost — the one listing that always
            // works.
            var showPanelInvocation = await registry.InvokeAsync(
                $"shell.showPanel:{dockingComposer.ExplorerPanelId}", CommandContext.Empty, prompt: null, CancellationToken.None);
            Assert.Equal(CommandOutcome.Executed, showPanelInvocation.Outcome);
            Assert.Contains(dockingComposer.ExplorerPanelId, controller.Tree.DockedPanels);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static void PressTab(Button tab, Pointer pointer) =>
        tab.RaiseEvent(new PointerPressedEventArgs(
            tab, pointer, tab, new Point(tab.Bounds.Width / 2, tab.Bounds.Height / 2),
            0, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));

    private static void MovePointer(WorkspaceLayoutHost host, Pointer pointer, Point position) =>
        host.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent, host, pointer, host, position,
            0, new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));

    private static void ReleasePointer(WorkspaceLayoutHost host, Pointer pointer, Point position) =>
        host.RaiseEvent(new PointerReleasedEventArgs(
            host, pointer, host, position,
            0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1900, 1050));
            window.Arrange(new Rect(0, 0, 1900, 1050));
        }
    }
}
