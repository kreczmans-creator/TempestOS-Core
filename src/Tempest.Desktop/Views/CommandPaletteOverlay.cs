using Avalonia.Controls;
using Avalonia.Input;
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
/// <b>Genuine, disclosed defect found and fixed, `WP 10.3B`</b>: pressing
/// <c>Enter</c> on a selected command whose own
/// <see cref="CommandDescriptor.CreateDefault"/> is <see langword="null"/>
/// (every one of this platform's own real discipline commands, confirmed
/// by direct `grep` — none has ever set it) previously closed the palette
/// and did **nothing else** — no error, no feedback, silently. Found
/// while building the Engineering Ribbon's own identical "cannot invoke
/// by Id alone" case (`RibbonView`), which needed to report this
/// honestly rather than silently no-op; fixed here too, at its own
/// original source, before this Work Package's own commit — the same
/// "found while building something else, fixed before sign-off"
/// discipline `WP 10.2A`'s own Property Inspector fix already
/// established.
/// </remarks>
public sealed class CommandPaletteOverlay : Border
{
    private readonly ICommandRegistry _registry;
    private readonly TextBox _query = new() { Watermark = "Type a command...", Margin = new Avalonia.Thickness(8) };
    private readonly ListBox _results = new() { MaxHeight = 320 };
    private IReadOnlyList<CommandDescriptor> _filtered = [];

    /// <summary>Raised after a command is successfully invoked from this palette.</summary>
    public event Action<CommandDescriptor, CommandResult>? CommandInvoked;

    /// <summary>
    /// Raised when the user selects and confirms a command whose own
    /// <see cref="CommandDescriptor.CreateDefault"/> is <see langword="null"/>
    /// — it cannot be invoked by Id alone (it needs a selected object or
    /// other context this palette does not collect). The genuine, disclosed
    /// fix for what was previously a silent no-op (see class remarks).
    /// </summary>
    public event Action<CommandDescriptor>? CommandUnavailable;

    /// <summary>
    /// An optional override for how a selected, invokable command is
    /// actually invoked (`WP 10.6A`) — <see langword="null"/> (the
    /// default) preserves the exact pre-`WP 10.6A` behaviour
    /// (<see cref="ICommandRegistry.InvokeAsync"/> directly). Set by
    /// <c>MainWindow</c> to route a Macro's own multi-step invocation
    /// through <see cref="Tasks.IBackgroundTaskRunner"/> — the one real
    /// "could take a moment" case in this platform — while every other,
    /// single-step command continues to invoke directly, unchanged.
    /// </summary>
    public Func<CommandDescriptor, Task<CommandResult>>? InvokeOverride { get; set; }

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

        _results.ItemsSource = _filtered.Select(d => d.Category is null ? d.DisplayName : $"{d.Category}: {d.DisplayName}").ToList();
        if (_filtered.Count > 0)
            _results.SelectedIndex = 0;
    }

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

        var descriptor = _filtered[_results.SelectedIndex];
        Close();

        if (descriptor.CreateDefault is not null)
        {
            // The result now travels with the event (`TD-58`) — the
            // subscriber refreshes dependent surfaces only on success.
            var result = InvokeOverride is not null
                ? await InvokeOverride(descriptor).ConfigureAwait(true)
                : await _registry.InvokeAsync(descriptor.Id).ConfigureAwait(true);

            CommandInvoked?.Invoke(descriptor, result);
        }
        else
        {
            // Genuine, disclosed fix, WP 10.3B (see class remarks) — this
            // used to be a silent no-op for every real discipline command,
            // since none has ever set CreateDefault.
            CommandUnavailable?.Invoke(descriptor);
        }
    }

    /// <summary>Gets whether the palette is currently open.</summary>
    public bool IsOpen => IsVisible;
}
