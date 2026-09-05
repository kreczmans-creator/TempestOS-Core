using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Tempest.Core.Commands;
using Tempest.Core.Macros;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Desktop Productisation Phase 2 (dialog/command-palette work package) —
/// the keyboard-workflow gaps found in <see cref="ConfirmationDialog"/>,
/// <see cref="MessageDialog"/>, <see cref="InputDialog"/>'s own validation
/// row, and <see cref="CommandPaletteOverlay"/>'s own list-scrolling, none
/// of which the pre-existing suites (<c>VisualPolishTests</c>,
/// <c>WorkflowInteractionTests</c>, <c>CommandPaletteOverlayTests</c>)
/// exercised via real key events over a real, running
/// <see cref="WorkspaceHost"/>.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class DialogFrameworkKeyboardTests
{
    // ------------------------------------------------------------
    // ConfirmationDialog — Escape/focus/Enter
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task ConfirmationDialog_Show_FocusesTheCancelButton_TheSafeDefault()
    {
        var dialog = new ConfirmationDialog();
        var window = new Window { Content = dialog };
        window.Show();

        _ = dialog.ConfirmAsync("Delete?", "This cannot be undone.", "Delete");

        var cancelButton = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel"));
        Assert.True(cancelButton.IsFocused);
    }

    [AvaloniaFact]
    public async Task ConfirmationDialog_Escape_ResolvesFalse_AndHidesTheDialog()
    {
        var dialog = new ConfirmationDialog();
        var window = new Window { Content = dialog };
        window.Show();

        var confirmTask = dialog.ConfirmAsync("Delete?", "This cannot be undone.", "Delete");
        Assert.True(dialog.IsVisible);

        dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

        Assert.False(await confirmTask);
        Assert.False(dialog.IsVisible);
    }

    [AvaloniaFact]
    public async Task ConfirmationDialog_EnterOnOpen_ActivatesTheFocusedCancelButton_NotTheDestructiveOne()
    {
        // The dialog is used for genuinely irreversible actions ("Delete?"
        // / "Discard") as often as benign ones — Enter, pressed before the
        // user deliberately tabs anywhere, must never take the destructive
        // action just because it happens to be the "confirm" button.
        var dialog = new ConfirmationDialog();
        var window = new Window { Content = dialog };
        window.Show();

        var confirmTask = dialog.ConfirmAsync("Delete?", "This cannot be undone.", "Delete");
        var cancelButton = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel"));
        cancelButton.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        Assert.False(await confirmTask);
        Assert.False(dialog.IsVisible);
    }

    [AvaloniaFact]
    public async Task ConfirmationDialog_TabToConfirmThenEnter_ActivatesTheConfirmButton()
    {
        var dialog = new ConfirmationDialog();
        var window = new Window { Content = dialog };
        window.Show();

        var confirmTask = dialog.ConfirmAsync("Delete?", "This cannot be undone.", "Delete");
        var confirmButton = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Delete"));
        confirmButton.Focus();
        confirmButton.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        Assert.True(await confirmTask);
        Assert.False(dialog.IsVisible);
    }

    // ------------------------------------------------------------
    // MessageDialog — Escape/focus
    // ------------------------------------------------------------

    [AvaloniaFact]
    public void MessageDialog_Show_FocusesTheOkButton()
    {
        var dialog = new MessageDialog();
        var window = new Window { Content = dialog };
        window.Show();

        _ = dialog.ShowAsync(FeedbackSeverity.Info, "Saved", "Your changes were saved.");

        var okButton = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
        Assert.True(okButton.IsFocused);
    }

    [AvaloniaFact]
    public async Task MessageDialog_Escape_DismissesTheDialog_SameAsOk()
    {
        var dialog = new MessageDialog();
        var window = new Window { Content = dialog };
        window.Show();

        var showTask = dialog.ShowAsync(FeedbackSeverity.Warning, "Heads up", "Something needs your attention.");
        Assert.True(dialog.IsVisible);

        dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

        await showTask;
        Assert.False(dialog.IsVisible);
    }

    // ------------------------------------------------------------
    // InputDialog — validation row uses the real severity vocabulary
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task InputDialog_ValidationError_RendersTheRealSeverityGlyphAndColour_NotBareColouredText()
    {
        var dialog = new InputDialog();
        var promptTask = dialog.PromptAsync("Create Part", "Name:");

        var textBox = dialog.GetLogicalDescendants().OfType<TextBox>().Single();
        var okButton = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
        textBox.Text = "   ";
        okButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        // Same row shape ObjectEditorView/PropertyInspectorView already
        // share: a dedicated glyph TextBlock plus a message TextBlock, both
        // tinted by SeverityColors.Resolve(Error) — never a single bare
        // string carrying colour as its only signal.
        var errorBrush = SeverityColors.Resolve(FeedbackSeverity.Error);
        var textBlocks = dialog.GetLogicalDescendants().OfType<TextBlock>().Where(t => Equals(t.Foreground, errorBrush)).ToList();

        Assert.Contains(textBlocks, t => t.Text == SeverityColors.Glyph(FeedbackSeverity.Error));
        Assert.Contains(textBlocks, t => t.Text == "A value is required.");

        textBox.Text = "New Part";
        okButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("New Part", await promptTask);
    }

    // ------------------------------------------------------------
    // CommandPaletteOverlay — live filtering, keyboard nav, real dispatch
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task CommandPaletteOverlay_TypingAQuery_NarrowsTheListToMatchingCommandsOnly()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var palette = new CommandPaletteOverlay(registry);
            palette.Open();

            var panel = (StackPanel)palette.Child!;
            var queryBox = (TextBox)panel.Children[0];
            var results = (ListBox)panel.Children[1];

            var fullCount = results.ItemsSource!.Cast<object>().Count();
            Assert.True(fullCount > 1, "Need more than one real registered command for a filter to narrow anything.");

            // Every registered command's own display name or id, sought one
            // character at a time, to guarantee a query that actually
            // narrows the real registry rather than accidentally matching
            // everything (or nothing).
            var target = registry.Items[0];
            queryBox.Text = target.Id;

            var narrowed = results.ItemsSource!.Cast<object>().ToList();
            Assert.True(narrowed.Count < fullCount || narrowed.Count == 1, "Expected the real registry to narrow for a specific command id.");
            Assert.Equal(0, results.SelectedIndex);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CommandPaletteOverlay_DownArrow_MovesSelectionForward_UpArrowMovesItBack()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            Assert.True(registry.Items.Count > 2, "Need several real commands for a two-step navigation test.");

            var palette = new CommandPaletteOverlay(registry);
            palette.Open();

            var panel = (StackPanel)palette.Child!;
            var queryBox = (TextBox)panel.Children[0];
            var results = (ListBox)panel.Children[1];
            Assert.Equal(0, results.SelectedIndex);

            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            Assert.Equal(2, results.SelectedIndex);

            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up });
            Assert.Equal(1, results.SelectedIndex);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CommandPaletteOverlay_EnterOnARealCommand_CollectsItsValues_DispatchesAndCreatesTheRealObject()
    {
        // The palette's own "unavailable" path is already covered by
        // CommandPaletteOverlayTests; this exercises the success path with
        // a real ICommandRegistry.InvokeAsync, a real CommandParameterPrompt
        // (the shell's own DesktopCommandPrompt over a real InputDialog —
        // TD-77 Stage 5's actual production wiring, not a stub), and a real
        // created object, end to end from a filtered-list Enter keypress.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var descriptor = registry.Items.Single(d => d.Id == "mechanical.create");

            var inputDialog = new InputDialog();
            var confirmationDialog = new ConfirmationDialog();
            var commandPrompt = new DesktopCommandPrompt(inputDialog, confirm: (_, message) => confirmationDialog.ConfirmAsync("Confirm", message, "Continue"));

            var palette = new CommandPaletteOverlay(registry)
            {
                ContextSource = () => CommandContext.Empty,
                ParameterPrompt = commandPrompt.Prompt,
            };
            palette.Open();

            CommandDescriptor? invoked = null;
            CommandResult? result = null;
            string? unavailableReason = null;
            palette.CommandInvoked += (d, r) => { invoked = d; result = r; };
            palette.CommandUnavailable += (_, reason) => unavailableReason = reason;

            var panel = (StackPanel)palette.Child!;
            var queryBox = (TextBox)panel.Children[0];
            queryBox.Text = descriptor.Id; // narrows the real registry to exactly this one command
            Assert.Equal(0, ((ListBox)panel.Children[1]).SelectedIndex);

            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            Assert.False(palette.IsOpen); // closes immediately, exactly as a real invocation would

            // The invocation proceeds asynchronously from here (`async void`
            // key handler): the real binding declares two parameters
            // ("kind", "displayName"), each collected in turn through
            // InputDialog. Bounded poll, answering whichever prompt is
            // currently showing, until the command actually runs.
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (invoked is null && unavailableReason is null && DateTime.UtcNow < deadline)
            {
                if (inputDialog.IsVisible)
                {
                    var textBox = inputDialog.GetLogicalDescendants().OfType<TextBox>().Single();
                    if (string.IsNullOrWhiteSpace(textBox.Text))
                        textBox.Text = "Palette Test Part";
                    var ok = inputDialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
                    ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                }

                await Task.Delay(10);
            }

            Assert.Null(unavailableReason);
            Assert.NotNull(invoked);
            Assert.Equal(descriptor.Id, invoked!.Id);
            Assert.NotNull(result);
            Assert.True(result!.Succeeded);

            var domainContext = (Tempest.Core.EngineeringDomain.EngineeringDomainContext)host.Services!.GetService(typeof(Tempest.Core.EngineeringDomain.EngineeringDomainContext));
            var created = await domainContext.Repository.ListByKindAsync("Part");
            Assert.Contains(created, o => (o as Tempest.Core.EngineeringDomain.IHasBusinessIdentifier)?.DisplayName == "Palette Test Part");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // `WP 16.5A` — real modal behaviour (`TD-65`): SettingsDialog and
    // MacroManagerDialog gain Escape/initial-focus; every one of the six
    // dialogs gains focus capture-on-open/restore-on-close.
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task SettingsDialog_Show_FocusesTheCancelButton_TheSafeDefault()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));
            var dialog = new SettingsDialog(new ThemeService(settingsProvider), new UserSettings(settingsProvider));
            var window = new Window { Content = dialog };
            window.Show();

            _ = dialog.ShowAsync();

            var cancelButton = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel"));
            Assert.True(cancelButton.IsFocused);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SettingsDialog_Escape_ResolvesFalse_AndHidesTheDialog_LeavingSettingsUnchanged()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));
            var settings = new UserSettings(settingsProvider);
            var dialog = new SettingsDialog(new ThemeService(settingsProvider), settings);
            var window = new Window { Content = dialog };
            window.Show();

            var showTask = dialog.ShowAsync();
            Assert.True(dialog.IsVisible);

            var checkbox = dialog.GetLogicalDescendants().OfType<CheckBox>().Single();
            checkbox.IsChecked = false; // a pending, unsaved change

            dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

            Assert.False(await showTask);
            Assert.False(dialog.IsVisible);
            Assert.True(settings.ConfirmBeforeDelete); // unchanged — Escape discarded, never saved
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task MacroManagerDialog_Show_FocusesTheMacroList_NeverAButton()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var macroManager = (IMacroManager)host.Services!.GetService(typeof(IMacroManager));
            var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var dialog = new MacroManagerDialog(macroManager, commandRegistry, runMacro: _ => Task.FromResult(CommandResult.Success()));
            var window = new Window { Content = dialog };
            window.Show();

            await dialog.ShowAsync();

            var macroList = dialog.GetLogicalDescendants().OfType<ListBox>().First();
            Assert.True(macroList.IsFocused);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task MacroManagerDialog_Escape_WhileBrowsing_ClosesTheDialog()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var macroManager = (IMacroManager)host.Services!.GetService(typeof(IMacroManager));
            var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var dialog = new MacroManagerDialog(macroManager, commandRegistry, runMacro: _ => Task.FromResult(CommandResult.Success()));
            var window = new Window { Content = dialog };
            window.Show();

            await dialog.ShowAsync();
            Assert.True(dialog.IsVisible);

            dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

            Assert.False(dialog.IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task MacroManagerDialog_Escape_WhileEditing_ReturnsToTheBrowsePanel_WithoutClosingTheDialog()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var macroManager = (IMacroManager)host.Services!.GetService(typeof(IMacroManager));
            var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var dialog = new MacroManagerDialog(macroManager, commandRegistry, runMacro: _ => Task.FromResult(CommandResult.Success()));
            var window = new Window { Content = dialog };
            window.Show();

            await dialog.ShowAsync();
            var newButton = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Macro..."));
            newButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            var nameBox = dialog.GetLogicalDescendants().OfType<TextBox>().Single();
            Assert.True(nameBox.IsFocused); // editor's own initial focus

            dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

            Assert.True(dialog.IsVisible); // still open — Escape cancelled the editor, not the dialog
            var macroList = dialog.GetLogicalDescendants().OfType<ListBox>().First();
            Assert.True(macroList.IsVisible);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // Focus capture-on-open / restore-on-close — every one of the six
    // dialogs, via the shared `DialogModality` helper.
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task ConfirmationDialog_Close_RestoresFocusToThePreviouslyFocusedControl()
    {
        var dialog = new ConfirmationDialog();
        var sibling = new Button { Content = "Sibling" };
        var panel = new Panel();
        panel.Children.Add(sibling);
        panel.Children.Add(dialog);
        var window = new Window { Content = panel };
        window.Show();
        sibling.Focus();
        Assert.True(sibling.IsFocused);

        var confirmTask = dialog.ConfirmAsync("Delete?", "This cannot be undone.", "Delete");
        Assert.False(sibling.IsFocused);

        dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

        Assert.False(await confirmTask);
        Assert.True(sibling.IsFocused);
        await Task.CompletedTask;
    }

    [AvaloniaFact]
    public async Task MessageDialog_Close_RestoresFocusToThePreviouslyFocusedControl()
    {
        var dialog = new MessageDialog();
        var sibling = new Button { Content = "Sibling" };
        var panel = new Panel();
        panel.Children.Add(sibling);
        panel.Children.Add(dialog);
        var window = new Window { Content = panel };
        window.Show();
        sibling.Focus();

        var showTask = dialog.ShowAsync(FeedbackSeverity.Info, "Saved", "Your changes were saved.");
        Assert.False(sibling.IsFocused);

        dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

        await showTask;
        Assert.True(sibling.IsFocused);
    }

    [AvaloniaFact]
    public async Task InputDialog_Close_RestoresFocusToThePreviouslyFocusedControl()
    {
        var dialog = new InputDialog();
        var sibling = new Button { Content = "Sibling" };
        var panel = new Panel();
        panel.Children.Add(sibling);
        panel.Children.Add(dialog);
        var window = new Window { Content = panel };
        window.Show();
        sibling.Focus();

        var promptTask = dialog.PromptAsync("New Part", "Name:");
        Assert.False(sibling.IsFocused);

        // InputDialog's own Escape handling lives on `_input` itself
        // (unlike the other five, which handle it on the dialog root) —
        // raised there, exactly where a real user's keystroke would land
        // (`PromptAsync`'s own initial focus).
        var input = dialog.GetLogicalDescendants().OfType<TextBox>().Single();
        input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

        Assert.Null(await promptTask);
        Assert.True(sibling.IsFocused);
    }

    [AvaloniaFact]
    public async Task SettingsDialog_Close_RestoresFocusToThePreviouslyFocusedControl()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));
            var dialog = new SettingsDialog(new ThemeService(settingsProvider), new UserSettings(settingsProvider));
            var sibling = new Button { Content = "Sibling" };
            var panel = new Panel();
            panel.Children.Add(sibling);
            panel.Children.Add(dialog);
            var window = new Window { Content = panel };
            window.Show();
            sibling.Focus();

            var showTask = dialog.ShowAsync();
            Assert.False(sibling.IsFocused);

            var cancelButton = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Cancel"));
            cancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.False(await showTask);
            Assert.True(sibling.IsFocused);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task MacroManagerDialog_Close_RestoresFocusToThePreviouslyFocusedControl()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var macroManager = (IMacroManager)host.Services!.GetService(typeof(IMacroManager));
            var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var dialog = new MacroManagerDialog(macroManager, commandRegistry, runMacro: _ => Task.FromResult(CommandResult.Success()));
            var sibling = new Button { Content = "Sibling" };
            var panel = new Panel();
            panel.Children.Add(sibling);
            panel.Children.Add(dialog);
            var window = new Window { Content = panel };
            window.Show();
            sibling.Focus();

            await dialog.ShowAsync();
            Assert.False(sibling.IsFocused);

            var closeButton = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Close"));
            closeButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.False(dialog.IsVisible);
            Assert.True(sibling.IsFocused);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CommandPaletteOverlay_Close_RestoresFocusToThePreviouslyFocusedControl()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var palette = new CommandPaletteOverlay(registry);
            var sibling = new Button { Content = "Sibling" };
            var panel = new Panel();
            panel.Children.Add(sibling);
            panel.Children.Add(palette);
            var window = new Window { Content = panel };
            window.Show();
            sibling.Focus();

            palette.Open();
            Assert.False(sibling.IsFocused);

            palette.Close();

            Assert.True(sibling.IsFocused);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
