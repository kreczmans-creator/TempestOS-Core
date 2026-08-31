using Avalonia.Controls;
using Avalonia.Input;
using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Command Palette Host (`WP 10.0B`) — a real overlay over
/// <see cref="ICommandRegistry.Items"/>, opened globally (`Ctrl+K`,
/// Keyboard Shortcut Framework), fuzzy/substring-filtered as the query
/// changes, dispatching the selected command via
/// <see cref="ICommandRegistry.InvokeAsync"/> on <c>Enter</c> — the
/// identical global entry point `ADR-0070` already established, unchanged.
/// </summary>
/// <remarks>
/// <para>
/// <b>TD-77 Stage 5 — the palette can finally run what it lists.</b> It
/// used to gate on <see cref="CommandDescriptor.CreateDefault"/>, which no
/// production discipline command has ever set, so pressing <c>Enter</c> on
/// any of the seventy-four real commands reported "unavailable" and did
/// nothing. It now invokes through
/// <see cref="ICommandRegistry.InvokeAsync(string, CommandContext, CommandParameterPrompt?, CancellationToken)"/>,
/// which builds the real command from the descriptor's own binding and
/// collects any values it declares through the supplied prompt.
/// </para>
/// <para>
/// <b>Unavailable commands stay listed.</b> <c>ADR-0070</c> requires a
/// command that cannot run to be shown disabled with its own reason rather
/// than hidden, so every registered command still appears; the ones
/// <see cref="ICommandRegistry.Evaluate"/> blocks are rendered disabled
/// with that reason beside them, and <c>Enter</c> reports it instead of
/// running anything.
/// </para>
/// </remarks>
public sealed class CommandPaletteOverlay : Border
{
    private readonly ICommandRegistry _registry;
    private readonly TextBox _query = new() { Watermark = "Type a command...", Margin = new Avalonia.Thickness(8) };
    private readonly ListBox _results = new() { MaxHeight = 320 };
    private IReadOnlyList<CommandDescriptor> _filtered = [];
    private IReadOnlyList<CommandAvailability> _availability = [];

    /// <summary>Raised after a command is successfully invoked from this palette.</summary>
    public event Action<CommandDescriptor, CommandResult>? CommandInvoked;

    /// <summary>
    /// Raised when the user confirms a command
    /// <see cref="ICommandRegistry.Evaluate"/> cannot currently run,
    /// carrying that command's own reason — the specific one it declared
    /// (a destination picker, a wrong-Kind selection, structured input),
    /// never a generic sentence.
    /// </summary>
    public event Action<CommandDescriptor, string>? CommandUnavailable;

    /// <summary>
    /// The Workspace's own current selection, as the Command Framework sees
    /// it — supplied by <c>MainWindow</c>. Left unwired, the palette
    /// evaluates against an empty context, which is exactly what a shell
    /// with no selection would report.
    /// </summary>
    public Func<CommandContext>? ContextSource { get; set; }

    /// <summary>
    /// Collects the values and confirmations a command's own binding
    /// declares. Left unwired, a command needing either is reported through
    /// <see cref="CommandUnavailable"/> rather than run without asking.
    /// </summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>
    /// An optional override for how an available command is actually
    /// invoked (`WP 10.6A`) — <see langword="null"/> invokes through
    /// <see cref="ICommandRegistry"/> directly. Set by <c>MainWindow</c> to
    /// route a Macro's own multi-step invocation through
    /// <see cref="Tasks.IBackgroundTaskRunner"/>, the one real "could take
    /// a moment" case in this platform, while every other single-step
    /// command continues to invoke directly.
    /// </summary>
    public Func<CommandDescriptor, CommandContext, Task<CommandInvocation>>? InvokeOverride { get; set; }

    /// <summary>Initialises a new instance of the <see cref="CommandPaletteOverlay"/> class, initially hidden.</summary>
    public CommandPaletteOverlay(ICommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;

        IsVisible = false;
        MaxWidth = 480;
        Padding = new Avalonia.Thickness(1);
        BorderThickness = new Avalonia.Thickness(1);
        // Theme-reactive (`WP 10.5A`, closes `TD-39`) — bound to
        // `ApplicationPalette`'s own overlay/panel resources, not a fixed
        // brush; automatically repaints on `ThemeService.ToggleAsync`.
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.OverlayBackgroundBrushKey);
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        Margin = new Avalonia.Thickness(0, 60, 0, 0);

        var panel = new StackPanel();
        panel.Children.Add(_query);
        panel.Children.Add(_results);
        Child = panel;

        _query.TextChanged += (_, _) => ApplyFilter();
        _query.KeyDown += OnQueryKeyDown;
        _results.DoubleTapped += async (_, _) => await InvokeSelectedAsync().ConfigureAwait(true);
    }

    /// <summary>Opens the palette: clears the query, re-reads every registered command, and gives the query box focus.</summary>
    public void Open()
    {
        _query.Text = string.Empty;
        ApplyFilter();
        IsVisible = true;
        _query.Focus();
    }

    /// <summary>Closes the palette without invoking anything.</summary>
    public void Close() => IsVisible = false;

    private void ApplyFilter()
    {
        var query = _query.Text ?? string.Empty;
        _filtered = string.IsNullOrWhiteSpace(query)
            ? _registry.Items
            : [.. _registry.Items.Where(d => d.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) || d.Id.Contains(query, StringComparison.OrdinalIgnoreCase))];

        // Evaluated once per render, against the same context Enter will
        // use - so what the row shows and what pressing Enter does cannot
        // disagree.
        var context = CurrentContext();
        _availability = [.. _filtered.Select(d => _registry.Evaluate(d.Id, context))];

        // ADR-0070: still listed, still findable, visibly disabled, and
        // carrying its own reason - never hidden.
        _results.ItemsSource = _filtered
            .Select((descriptor, index) => new ListBoxItem
            {
                Content = RowText(descriptor, _availability[index]),
                IsEnabled = _availability[index].IsAvailable,
            })
            .ToList();

        if (_filtered.Count > 0)
            _results.SelectedIndex = 0;
    }

    /// <summary>The label for one row: the command, and — when it cannot run — why not.</summary>
    private static string RowText(CommandDescriptor descriptor, CommandAvailability availability)
    {
        var name = descriptor.Category is null ? descriptor.DisplayName : $"{descriptor.Category}: {descriptor.DisplayName}";

        return availability.IsAvailable ? name : $"{name} — {availability.Reason}";
    }

    private CommandContext CurrentContext() => ContextSource?.Invoke() ?? CommandContext.Empty;

    private async void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Enter:
                await InvokeSelectedAsync().ConfigureAwait(true);
                e.Handled = true;
                break;
            case Key.Down:
                if (_results.SelectedIndex < _filtered.Count - 1)
                    _results.SelectedIndex++;
                e.Handled = true;
                break;
            case Key.Up:
                if (_results.SelectedIndex > 0)
                    _results.SelectedIndex--;
                e.Handled = true;
                break;
        }
    }

    private async Task InvokeSelectedAsync()
    {
        if (_results.SelectedIndex < 0 || _results.SelectedIndex >= _filtered.Count)
            return;

        var index = _results.SelectedIndex;
        var descriptor = _filtered[index];
        var context = CurrentContext();
        Close();

        // The row already showed this, evaluated against this same context.
        if (index < _availability.Count && !_availability[index].IsAvailable)
        {
            CommandUnavailable?.Invoke(descriptor, _availability[index].Reason!);
            return;
        }

        var invocation = InvokeOverride is not null
            ? await InvokeOverride(descriptor, context).ConfigureAwait(true)
            : await _registry.InvokeAsync(descriptor.Id, context, ParameterPrompt).ConfigureAwait(true);

        switch (invocation.Outcome)
        {
            case CommandOutcome.Executed:
                // The result travels with the event (`TD-58`) — the
                // subscriber refreshes dependent surfaces only on success.
                CommandInvoked?.Invoke(descriptor, invocation.Result!);
                break;

            case CommandOutcome.Cancelled:
                // Closing a prompt is not an error and did not change
                // anything: no toast, no status text, no history entry.
                break;

            default:
                // Re-evaluated between render and Enter, or refused for a
                // value-level reason the row could not know about.
                CommandUnavailable?.Invoke(descriptor, invocation.Reason!);
                break;
        }
    }

    /// <summary>Gets whether the palette is currently open.</summary>
    public bool IsOpen => IsVisible;
}
