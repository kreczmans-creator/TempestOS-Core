using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Workspace;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 21.1A` — a journey per command family through the real
/// <see cref="MainWindow"/>: a real Command Palette invocation (the same
/// filter/select/Enter path <c>SurfaceCommandIntegrationTests</c> already
/// proves, driven here through the window's own real palette so the real
/// recording wired in <c>MainWindowComposer.Wire</c> actually runs), Ctrl+Z,
/// then Ctrl+Y — proving the whole pipeline end to end: the handler builds a
/// compensation, the Palette's own <c>CommandInvoked</c> subscriber records
/// it onto the real Undo/Redo stack, and the real keyboard shortcuts
/// (<c>KeyboardShortcuts</c>, unchanged by this Work Package) reverse and
/// re-apply it against the real domain.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class UndoCompensationJourneyTests
{
    private static async Task<(WorkspaceHost Host, MainWindow Window, Guid ProjectId, ICommandRegistry Registry, ICommandDispatcher Dispatcher, EngineeringDomainContext Domain)> OpenAProjectAsync(string identifier)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var window = new MainWindow(host);
        var navigator = host.ShellNavigator!;

        await navigator.GoToProjectsAsync();
        await window.RenderCurrentModuleAsync();
        var project = await host.ProjectDirectory!.CreateAsync(identifier, $"{identifier} Project");
        await navigator.OpenProjectAsync(project.Id);
        await window.RenderCurrentModuleAsync();
        await navigator.GoToEngineeringAsync();
        await window.RenderCurrentModuleAsync();

        var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        return (host, window, project.Id, registry, dispatcher, domain);
    }

    private static CommandParameterPrompt Answering(params (string Name, string Value)[] answers) =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
            answers.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));

    private static TextBox Query(CommandPaletteOverlay palette) => (TextBox)((StackPanel)palette.Child!).Children[0];

    private static ListBox Results(CommandPaletteOverlay palette) => (ListBox)((StackPanel)palette.Child!).Children[1];

    /// <summary>
    /// Drives the window's own real Command Palette exactly as a person
    /// does: filter, select, Enter — the identical path
    /// <c>SurfaceCommandIntegrationTests.InvokeAsync</c> already proves,
    /// against the real window's own real palette (never a standalone one)
    /// so the real recording wired in <c>MainWindowComposer.Wire</c> — the
    /// point of this test class — actually runs.
    /// </summary>
    private static async Task<CommandResult> InvokeThroughPaletteAsync(
        MainWindow window, ICommandRegistry registry, string commandId, CommandParameterPrompt? prompt = null)
    {
        LayOut(window);
        var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");

        // `MainWindowComposer.Wire` sets a real `InvokeOverride` (macro
        // routing through the Background Task Runner) that would otherwise
        // shadow `ParameterPrompt` entirely and fall through to the real,
        // dialog-driven `views.CommandPrompt.Prompt` — which has nothing to
        // answer it in a headless test and simply hangs. Cleared here so
        // this helper's own `prompt` (or none, for a parameterless command)
        // reaches `ICommandRegistry.InvokeAsync` directly, exactly as
        // `CommandPaletteOverlay`'s own fallback branch already does for a
        // non-macro command.
        palette.InvokeOverride = null;
        palette.ParameterPrompt = prompt;

        palette.Open();
        LayOut(window);
        Query(palette).Text = commandId;

        var expected = registry.Items
            .Where(d => d.DisplayName.Contains(commandId, StringComparison.OrdinalIgnoreCase) || d.Id.Contains(commandId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var filterDeadline = Deadline(2);
        while (((System.Collections.IEnumerable)Results(palette).ItemsSource!).Cast<ListBoxItem>().Count() != expected.Count
               && DateTime.UtcNow < filterDeadline)
            await Task.Delay(10);

        var index = expected.FindIndex(d => d.Id == commandId);
        Assert.True(index >= 0, $"'{commandId}' was not among the filtered results.");
        Results(palette).SelectedIndex = index;

        CommandResult? result = null;
        string? unavailableReason = null;
        void OnInvoked(CommandDescriptor _, CommandResult r) => result = r;
        void OnUnavailable(CommandDescriptor _, string reason) => unavailableReason = reason;

        palette.CommandInvoked += OnInvoked;
        palette.CommandUnavailable += OnUnavailable;
        try
        {
            Query(palette).RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.Enter,
            });

            var invokeDeadline = Deadline(3);
            while (result is null && unavailableReason is null && DateTime.UtcNow < invokeDeadline)
                await Task.Delay(10);
        }
        finally
        {
            palette.CommandInvoked -= OnInvoked;
            palette.CommandUnavailable -= OnUnavailable;
        }

        Assert.Null(unavailableReason);
        Assert.NotNull(result);
        return result!;
    }

    private static IUndoRedoStack GetStack(MainWindow window)
    {
        var undoRedo = GetPrivateField<object>(window, "_undoRedo");
        return (IUndoRedoStack)undoRedo.GetType().GetProperty("Stack")!.GetValue(undoRedo)!;
    }

    private static void PressCtrl(MainWindow window, Avalonia.Input.Key key) => window.RaiseEvent(new Avalonia.Input.KeyEventArgs
    {
        RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
        Key = key,
        KeyModifiers = Avalonia.Input.KeyModifiers.Control,
        Source = window,
    });

    private static async Task<bool> IsDeletedAsync(EngineeringDomainContext domain, Guid id) =>
        ((IDeletable)(await domain.Repository.FindAsync(id))!).IsDeleted;

    private static async Task PollUntilAsync(Func<Task<bool>> condition, int seconds = 3)
    {
        var deadline = Deadline(seconds);
        while (!await condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
    }

    // ==================================================================
    // Create
    // ==================================================================

    [AvaloniaFact]
    public async Task Create_ThroughThePalette_CtrlZ_SoftDeletes_CtrlY_Restores()
    {
        var (host, window, projectId, registry, _, domain) = await OpenAProjectAsync("UNDO-J-CREATE-1");
        try
        {
            await host.Workspace!.Selection.ClearAsync();

            var result = await InvokeThroughPaletteAsync(
                window, registry, "documents.create",
                Answering(("kind", "Document"), ("displayName", "Journey Create Doc")));
            Assert.True(result.Succeeded, result.Message);
            var createdId = result.SubjectId!.Value;

            Assert.True(GetStack(window).CanUndo);

            PressCtrl(window, Avalonia.Input.Key.Z);
            await PollUntilAsync(() => IsDeletedAsync(domain, createdId));
            Assert.True(await IsDeletedAsync(domain, createdId));

            PressCtrl(window, Avalonia.Input.Key.Y);
            await PollUntilAsync(async () => !await IsDeletedAsync(domain, createdId));
            Assert.False(await IsDeletedAsync(domain, createdId));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Delete
    // ==================================================================

    [AvaloniaFact]
    public async Task Delete_ThroughThePalette_CtrlZ_Restores_CtrlY_SoftDeletes()
    {
        var (host, window, projectId, registry, dispatcher, domain) = await OpenAProjectAsync("UNDO-J-DELETE-1");
        try
        {
            var created = await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Journey Delete Doc", parentId: projectId), default);
            var targetId = created.SubjectId!.Value;

            await host.Workspace!.Selection.SelectAsync(targetId, "Document");

            var result = await InvokeThroughPaletteAsync(window, registry, "documents.delete", Answering());
            Assert.True(result.Succeeded, result.Message);
            Assert.True(await IsDeletedAsync(domain, targetId));
            Assert.True(GetStack(window).CanUndo);

            PressCtrl(window, Avalonia.Input.Key.Z);
            await PollUntilAsync(async () => !await IsDeletedAsync(domain, targetId));
            Assert.False(await IsDeletedAsync(domain, targetId));

            PressCtrl(window, Avalonia.Input.Key.Y);
            await PollUntilAsync(() => IsDeletedAsync(domain, targetId));
            Assert.True(await IsDeletedAsync(domain, targetId));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Move
    // ==================================================================

    [AvaloniaFact]
    public async Task Move_ThroughThePalette_CtrlZ_MovesBack_CtrlY_MovesForward()
    {
        var (host, window, projectId, registry, dispatcher, domain) = await OpenAProjectAsync("UNDO-J-MOVE-1");
        try
        {
            var parentA = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Journey Parent A", parentId: projectId), default)).SubjectId!.Value;
            var parentB = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Journey Parent B", parentId: projectId), default)).SubjectId!.Value;
            var targetId = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Journey Movable", parentId: parentA), default)).SubjectId!.Value;

            await host.Workspace!.Selection.SelectAsync(targetId, "Document");

            var result = await InvokeThroughPaletteAsync(window, registry, "documents.move", Answering(("destinationId", parentB.ToString())));
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(parentB, ((IHasParent)(await domain.Repository.FindAsync(targetId))!).ParentId);
            Assert.True(GetStack(window).CanUndo);

            PressCtrl(window, Avalonia.Input.Key.Z);
            await PollUntilAsync(async () => ((IHasParent)(await domain.Repository.FindAsync(targetId))!).ParentId == parentA);
            Assert.Equal(parentA, ((IHasParent)(await domain.Repository.FindAsync(targetId))!).ParentId);

            PressCtrl(window, Avalonia.Input.Key.Y);
            await PollUntilAsync(async () => ((IHasParent)(await domain.Repository.FindAsync(targetId))!).ParentId == parentB);
            Assert.Equal(parentB, ((IHasParent)(await domain.Repository.FindAsync(targetId))!).ParentId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Copy
    // ==================================================================

    [AvaloniaFact]
    public async Task Copy_ThroughThePalette_CtrlZ_DeletesTheCopy_CtrlY_RestoresIt()
    {
        var (host, window, projectId, registry, dispatcher, domain) = await OpenAProjectAsync("UNDO-J-COPY-1");
        try
        {
            var sourceId = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Journey Copy Source", parentId: projectId), default)).SubjectId!.Value;

            await host.Workspace!.Selection.SelectAsync(sourceId, "Document");

            var result = await InvokeThroughPaletteAsync(window, registry, "documents.copy", Answering(("destinationId", string.Empty)));
            Assert.True(result.Succeeded, result.Message);
            var copyId = result.SubjectId!.Value;
            Assert.NotEqual(sourceId, copyId);
            Assert.True(GetStack(window).CanUndo);

            PressCtrl(window, Avalonia.Input.Key.Z);
            await PollUntilAsync(() => IsDeletedAsync(domain, copyId));
            Assert.True(await IsDeletedAsync(domain, copyId));
            Assert.False(await IsDeletedAsync(domain, sourceId));

            PressCtrl(window, Avalonia.Input.Key.Y);
            await PollUntilAsync(async () => !await IsDeletedAsync(domain, copyId));
            Assert.False(await IsDeletedAsync(domain, copyId));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // A status change
    // ==================================================================

    [AvaloniaFact]
    public async Task StatusChange_ThroughThePalette_CtrlZ_ReversesTheTransition_CtrlY_ReappliesIt()
    {
        var (host, window, projectId, registry, dispatcher, domain) = await OpenAProjectAsync("UNDO-J-STATUS-1");
        try
        {
            var targetId = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Journey Reviewable", parentId: projectId), default)).SubjectId!.Value;

            await host.Workspace!.Selection.SelectAsync(targetId, "Document");

            var result = await InvokeThroughPaletteAsync(window, registry, "documents.request-review");
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(LifecycleState.InReview, ((IHasLifecycle)(await domain.Repository.FindAsync(targetId))!).Status);
            Assert.True(GetStack(window).CanUndo);

            PressCtrl(window, Avalonia.Input.Key.Z);
            await PollUntilAsync(async () => ((IHasLifecycle)(await domain.Repository.FindAsync(targetId))!).Status == LifecycleState.Draft);
            Assert.Equal(LifecycleState.Draft, ((IHasLifecycle)(await domain.Repository.FindAsync(targetId))!).Status);

            PressCtrl(window, Avalonia.Input.Key.Y);
            await PollUntilAsync(async () => ((IHasLifecycle)(await domain.Repository.FindAsync(targetId))!).Status == LifecycleState.InReview);
            Assert.Equal(LifecycleState.InReview, ((IHasLifecycle)(await domain.Repository.FindAsync(targetId))!).Status);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // A field edit (the editor's own Save — content/Revise)
    // ==================================================================

    [AvaloniaFact]
    public async Task FieldEdit_ReviseThroughTheRealEditor_CtrlZ_RestoresOldContent_CtrlY_ReappliesNewContent()
    {
        var (host, window, projectId, _, dispatcher, domain) = await OpenAProjectAsync("UNDO-J-FIELD-1");
        try
        {
            var created = await dispatcher.DispatchAsync(
                new CreateDocumentObjectCommand("Document", "Journey Field Edit Doc", parentId: projectId, initialContent: "Original content."), default);
            var targetId = created.SubjectId!.Value;

            await window.OpenObjectAsync(targetId, "Document");
            LayOut(window);
            var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().Last();

            var contentBox = GetPrivateField<TextBox>(editor, "_contentBox");
            var saveButton = GetPrivateField<Button>(editor, "_saveButton");

            const string revised = "Revised content, for the undo journey.";
            contentBox.Text = revised;
            saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await PollUntilAsync(async () => ((IHasRevisions)(await domain.Repository.FindAsync(targetId))!).Content == revised);
            Assert.Equal(revised, ((IHasRevisions)(await domain.Repository.FindAsync(targetId))!).Content);
            Assert.True(GetStack(window).CanUndo);

            PressCtrl(window, Avalonia.Input.Key.Z);
            await PollUntilAsync(async () => ((IHasRevisions)(await domain.Repository.FindAsync(targetId))!).Content == "Original content.");
            Assert.Equal("Original content.", ((IHasRevisions)(await domain.Repository.FindAsync(targetId))!).Content);

            PressCtrl(window, Avalonia.Input.Key.Y);
            await PollUntilAsync(async () => ((IHasRevisions)(await domain.Repository.FindAsync(targetId))!).Content == revised);
            Assert.Equal(revised, ((IHasRevisions)(await domain.Repository.FindAsync(targetId))!).Content);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

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
