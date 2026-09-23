using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-63`: <c>WorkspaceViewCoordinator.CloseDocumentAsync</c>'s
/// own dirty-tab confirmation (`TD-40`, `WP 10.5A`) — a real
/// <see cref="ConfirmationDialog"/> before a dirty
/// <see cref="ObjectEditorView"/> tab's unsaved edits are discarded — is
/// wired from two production entry points, the tab's own close glyph
/// (<c>MainWindowComposer.Coordinators.cs</c>) and <c>Ctrl+W</c>
/// (<c>MainWindowComposer.Wire.cs</c>), but no test drove either through
/// the real, composed application: every existing coverage called
/// <c>CloseDocumentAsync</c> directly. Both entry points are driven here
/// through a real <see cref="MainWindow"/>, dirtying a tab the same way a
/// person does — editing its Name field, which raises the real
/// <see cref="ObjectEditorView.DirtyChanged"/> event — rather than
/// calling the buffered dirty-state seam by hand, and confirming Cancel
/// leaves the tab open with its edit intact.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class DirtyTabCloseConfirmationJourneyTests
{
    [AvaloniaFact]
    public async Task ClosingADirtyTab_ByTheCloseGlyph_ShowsTheConfirmationDialog_AndCancelKeepsTheTabOpen()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (window, nameBox, tabCountBeforeClose) = await OpenADirtiedPartTabAsync(host, "P-TD63A", "TD-63 Glyph Part");

            var confirmationDialog = GetPrivateField<ConfirmationDialog>(window, "_confirmationDialog");
            Assert.False(confirmationDialog.IsVisible, "The confirmation dialog was already showing before Close was requested.");

            // The real close glyph — found the way a screen reader would,
            // by its own automation name (`DocumentAreaView.BuildHeader`) —
            // never `TabCloseRequested` invoked by hand. `.Distinct()`:
            // `GetLogicalDescendants()` revisits a `TabItem`'s own header
            // content through more than one path (the same duplicate-
            // traversal artefact `CreatedObjectOpensRightUpTests.EditorFor`
            // already guards against for `ObjectEditorView`), not two
            // separate close buttons.
            var closeButton = window.GetLogicalDescendants().OfType<Button>().Distinct()
                .Single(b => AutomationProperties.GetName(b) == "Close TD-63 Glyph Part");
            closeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => confirmationDialog.IsVisible);
            Assert.True(confirmationDialog.IsVisible, "Closing a dirty tab by its close glyph did not show the real ConfirmationDialog.");

            var cancelButton = confirmationDialog.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Cancel");
            cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => !confirmationDialog.IsVisible);
            Assert.False(confirmationDialog.IsVisible, "Cancel did not dismiss the confirmation dialog.");
            Assert.Equal(tabCountBeforeClose, GetPrivateField<DocumentAreaView>(window, "_documentArea").TabCount);
            Assert.Equal("TD-63 Glyph Part Edited", nameBox.Text);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ClosingADirtyTab_ByCtrlW_ShowsTheConfirmationDialog_AndCancelKeepsTheTabOpen()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (window, nameBox, tabCountBeforeClose) = await OpenADirtiedPartTabAsync(host, "P-TD63B", "TD-63 CtrlW Part");

            var confirmationDialog = GetPrivateField<ConfirmationDialog>(window, "_confirmationDialog");
            Assert.False(confirmationDialog.IsVisible, "The confirmation dialog was already showing before Ctrl+W was pressed.");

            // `OpenCreatedObjectAsync`'s own real navigation (`WP 17.9.4`)
            // already leaves the newly-created object's own tab selected —
            // Ctrl+W's own route (`MainWindowComposer.Wire.cs`,
            // `DocumentAreaView.ActiveClosableViewId`) targets exactly that
            // tab, unchanged here. The identical real-KeyDown-on-the-window
            // pattern this suite already established for Ctrl+Z/Ctrl+Y
            // (`MainWindowCompositionTests.CtrlZCtrlY_...`).
            window.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.W,
                KeyModifiers = KeyModifiers.Control,
                Source = window,
            });

            await RenderUntilAsync(window, () => confirmationDialog.IsVisible);
            Assert.True(confirmationDialog.IsVisible, "Ctrl+W on a dirty tab did not show the real ConfirmationDialog.");

            var cancelButton = confirmationDialog.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Cancel");
            cancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => !confirmationDialog.IsVisible);
            Assert.False(confirmationDialog.IsVisible, "Cancel did not dismiss the confirmation dialog.");
            Assert.Equal(tabCountBeforeClose, GetPrivateField<DocumentAreaView>(window, "_documentArea").TabCount);
            Assert.Equal("TD-63 CtrlW Part Edited", nameBox.Text);
        }
        finally
        {
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Opens a real <c>Part</c> right up in its own editor tab
    /// (<c>WP 17.9.4</c>'s own "opens right up" path, reached here via the
    /// Ribbon's own <c>mechanical.create</c> command — exactly as a person
    /// creating one does), then dirties it the same way a person does:
    /// editing its Name field, which raises the real
    /// <see cref="ObjectEditorView.DirtyChanged"/> event synchronously,
    /// through to <see cref="DocumentAreaView.MarkDirty"/> — never the
    /// buffered dirty-state seam called by hand.
    /// </summary>
    private static async Task<(MainWindow Window, TextBox NameBox, int TabCountBeforeClose)> OpenADirtiedPartTabAsync(
        WorkspaceHost host, string projectCode, string displayName)
    {
        var window = new MainWindow(host);
        var navigator = host.ShellNavigator!;

        await navigator.GoToProjectsAsync();
        await window.RenderCurrentModuleAsync();
        var project = await host.ProjectDirectory!.CreateAsync(projectCode, $"{displayName} Project");
        await navigator.OpenProjectAsync(project.Id);
        await window.RenderCurrentModuleAsync();
        await navigator.GoToEngineeringAsync();
        await window.RenderCurrentModuleAsync();
        LayOut(window);

        var ribbon = GetPrivateField<RibbonView>(window, "_ribbon");
        var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        ribbon.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
            new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = displayName });
        Click(ribbon, registry, "mechanical.create");

        ObjectEditorView? editor = null;
        await RenderUntilAsync(window, () =>
        {
            editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().Distinct()
                .FirstOrDefault(e => e.GetLogicalDescendants().OfType<TextBox>().Any(t => t.Text == displayName));
            return editor is not null;
        });
        Assert.NotNull(editor);
        LayOut(window);

        var tabCountBeforeClose = GetPrivateField<DocumentAreaView>(window, "_documentArea").TabCount;

        var nameBox = editor!.GetLogicalDescendants().OfType<TextBox>().First(t => t.Text == displayName);
        nameBox.Text = displayName + " Edited"; // a real edit — ObjectEditorView.UpdateDirty/DirtyChanged fires synchronously

        return (window, nameBox, tabCountBeforeClose);
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(20);
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
