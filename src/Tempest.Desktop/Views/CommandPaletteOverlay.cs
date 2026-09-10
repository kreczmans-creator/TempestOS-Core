using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Tempest.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// One object the Command Palette's own search source found (`WP 18.1B`
/// §2), display-ready: <c>MainWindow</c> has already resolved the hit's
/// own current title and its project's own name, so this overlay never
/// needs to know anything about persistence or the domain to render it.
/// </summary>
/// <param name="ObjectId">The found object's own id.</param>
/// <param name="Kind">The found object's own canonical Kind.</param>
/// <param name="Title">The found object's own current title.</param>
/// <param name="ProjectName">The project the object sits under, or <see langword="null"/> if it sits under none (or is itself a project).</param>
public sealed record PaletteObjectHit(Guid ObjectId, string Kind, string Title, string? ProjectName);

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
/// <para>
/// <b>Objects (`WP 18.1B` §2).</b> When the query is non-empty, an
/// "Objects" section under the commands lists the top ten global search
/// hits <see cref="ObjectSearchSource"/> returns, with their Kind and
/// project; selecting one raises <see cref="ObjectSelected"/> rather than
/// invoking a command. The search itself runs on a background task
/// (<see cref="ObjectSearchSource"/> is awaited, never blocked on) with
/// the latest query winning: a generation counter discards a stale
/// search's own result if a newer query has already superseded it, so a
/// slow first keystroke can never overwrite what a fast second one found.
/// </para>
/// </remarks>
public sealed class CommandPaletteOverlay : Border
{
    private readonly ICommandRegistry _registry;
    private readonly TextBox _query = new() { Watermark = "Type a command...", Margin = new Avalonia.Thickness(8) };
    private readonly ListBox _results = new() { MaxHeight = 400 };
    private List<PaletteRow> _rows = [];
    private int _searchGeneration;

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

    /// <summary>Raised when the user selects a found object from the Objects section (`WP 18.1B` §2) rather than a command.</summary>
    public event Action<PaletteObjectHit>? ObjectSelected;

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

    /// <summary>
    /// The global object search this palette's own Objects section reads
    /// from (`WP 18.1B` §2) — set by <c>MainWindow</c> over
    /// <c>IQueryablePersistenceStore.SearchAsync</c>. Left unwired, the
    /// palette shows commands only, exactly as before this Work Package.
    /// Always awaited, never blocked on: the one search a keystroke
    /// started may still be running when a later keystroke starts another;
    /// see this class's own remarks on the generation counter that decides
    /// which result wins.
    /// </summary>
    public Func<string, CancellationToken, Task<IReadOnlyList<PaletteObjectHit>>>? ObjectSearchSource { get; set; }

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

        // A watermark alone names nothing to a screen reader (`WP 16.5A`,
        // `TD-65`) — the same real gap the header search field, the
        // Explorer filter, and the Digital Thread search box all had.
        AutomationProperties.SetName(_query, "Command palette query");
        ToolTip.SetTip(_query, "Type a command...");
        AutomationProperties.SetName(_results, "Command palette results");

        var panel = new StackPanel();
        panel.Children.Add(_query);
        panel.Children.Add(_results);
        Child = panel;

        // `PropertyChanged`, not the `TextChanged` routed event — the same
        // reliability gap `ObjectEditorView`/`ProjectExplorerView` already
        // documented and fixed (`WP 10.3A`/`WP 10.5B`): `TextChanged` does
        // not reliably fire for a purely programmatic `.Text =` assignment
        // (only for real keystrokes), where `PropertyChanged` fires for
        // both. A real keyboard already worked here; a caller that sets
        // `_query.Text` itself (a "clear search" affordance, a restored
        // query) would not have re-filtered.
        _query.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                ApplyFilter();
        };
        _query.KeyDown += OnQueryKeyDown;
        _results.DoubleTapped += async (_, _) => await InvokeSelectedAsync().ConfigureAwait(true);

        // Real modal behaviour (`WP 16.5A`, `TD-65`) — see
        // `DialogModality`'s own remarks.
        DialogModality.Install(this);
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

    /// <summary>
    /// Re-filters the command list synchronously, renders it immediately,
    /// then — if the query is non-empty and <see cref="ObjectSearchSource"/>
    /// is wired — starts a background object search and appends its own
    /// "Objects" section once it returns, provided no newer query has
    /// superseded it in the meantime (`WP 18.1B` §2).
    /// </summary>
    private async void ApplyFilter()
    {
        var query = _query.Text ?? string.Empty;
        var generation = ++_searchGeneration;

        var filteredCommands = string.IsNullOrWhiteSpace(query)
            ? _registry.Items
            : (IReadOnlyList<CommandDescriptor>)
              [.. _registry.Items.Where(d => d.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) || d.Id.Contains(query, StringComparison.OrdinalIgnoreCase))];

        // Evaluated once per render, against the same context Enter will
        // use - so what the row shows and what pressing Enter does cannot
        // disagree.
        var context = CurrentContext();
        var availability = filteredCommands.Select(d => _registry.Evaluate(d.Id, context)).ToList();

        RenderRows(filteredCommands, availability, objectHits: null);

        if (string.IsNullOrWhiteSpace(query) || ObjectSearchSource is null)
            return;

        IReadOnlyList<PaletteObjectHit> hits;
        try
        {
            hits = await ObjectSearchSource(query, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // A search failure is not a reason to stop showing commands —
            // the Objects section simply stays absent for this query.
            return;
        }

        // The latest query wins (`WP 18.1B` §2): a search this slow to
        // return has already been superseded by a later keystroke's own
        // search, whose result must not be overwritten by this one landing
        // late.
        if (generation != _searchGeneration)
            return;

        RenderRows(filteredCommands, availability, hits);
    }

    /// <summary>Builds <see cref="_rows"/> and this palette's own visible list from a command render pass and (once it has returned) an object search pass.</summary>
    private void RenderRows(IReadOnlyList<CommandDescriptor> filteredCommands, IReadOnlyList<CommandAvailability> availability, IReadOnlyList<PaletteObjectHit>? objectHits)
    {
        var rows = new List<PaletteRow>(filteredCommands.Count + (objectHits?.Count ?? 0) + 1);

        for (var i = 0; i < filteredCommands.Count; i++)
            rows.Add(PaletteRow.ForCommand(filteredCommands[i], availability[i]));

        if (objectHits is { Count: > 0 })
        {
            rows.Add(PaletteRow.Header("Objects"));
            foreach (var hit in objectHits)
                rows.Add(PaletteRow.ForObject(hit));
        }

        _rows = rows;

        // ADR-0070: an unavailable command stays listed, visibly disabled,
        // carrying its own reason - never hidden. A header row is
        // rendered disabled too, purely as a label; InvokeSelectedAsync
        // never treats it as a target (see IsHeader there).
        _results.ItemsSource = _rows.Select(row => new ListBoxItem { Content = row.Text, IsEnabled = row.IsEnabled }).ToList();

        var firstSelectable = _rows.FindIndex(r => !r.IsHeader);
        if (firstSelectable >= 0)
        {
            _results.SelectedIndex = firstSelectable;
            _results.ScrollIntoView(firstSelectable);
        }
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
                if (_results.SelectedIndex < _rows.Count - 1)
                {
                    _results.SelectedIndex++;
                    _results.ScrollIntoView(_results.SelectedIndex);
                }
                e.Handled = true;
                break;
            case Key.Up:
                if (_results.SelectedIndex > 0)
                {
                    _results.SelectedIndex--;
                    _results.ScrollIntoView(_results.SelectedIndex);
                }
                e.Handled = true;
                break;
        }
    }

    private async Task InvokeSelectedAsync()
    {
        if (_results.SelectedIndex < 0 || _results.SelectedIndex >= _rows.Count)
            return;

        var row = _rows[_results.SelectedIndex];
        if (row.IsHeader)
            return;

        Close();

        if (row.ObjectHit is { } hit)
        {
            ObjectSelected?.Invoke(hit);
            return;
        }

        var descriptor = row.Command!;
        var context = CurrentContext();

        // The row already showed this, evaluated against this same context.
        if (!row.Availability!.IsAvailable)
        {
            CommandUnavailable?.Invoke(descriptor, row.Availability.Reason!);
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

    /// <summary>
    /// One visible row (`WP 18.1B` §2): a command (with its own evaluated
    /// availability), a found object, or a plain section header — exactly
    /// one of <see cref="Command"/>/<see cref="ObjectHit"/> is set, or
    /// neither for a header.
    /// </summary>
    private sealed class PaletteRow
    {
        private PaletteRow(string text, bool isEnabled, bool isHeader, CommandDescriptor? command, CommandAvailability? availability, PaletteObjectHit? objectHit)
        {
            Text = text;
            IsEnabled = isEnabled;
            IsHeader = isHeader;
            Command = command;
            Availability = availability;
            ObjectHit = objectHit;
        }

        public string Text { get; }

        public bool IsEnabled { get; }

        public bool IsHeader { get; }

        public CommandDescriptor? Command { get; }

        public CommandAvailability? Availability { get; }

        public PaletteObjectHit? ObjectHit { get; }

        public static PaletteRow ForCommand(CommandDescriptor descriptor, CommandAvailability availability)
        {
            var name = descriptor.Category is null ? descriptor.DisplayName : $"{descriptor.Category}: {descriptor.DisplayName}";
            var text = availability.IsAvailable ? name : $"{name} — {availability.Reason}";

            return new PaletteRow(text, availability.IsAvailable, isHeader: false, descriptor, availability, objectHit: null);
        }

        public static PaletteRow ForObject(PaletteObjectHit hit)
        {
            var text = hit.ProjectName is { Length: > 0 }
                ? $"◈ {hit.Title} — {hit.Kind} · {hit.ProjectName}"
                : $"◈ {hit.Title} — {hit.Kind}";

            return new PaletteRow(text, isEnabled: true, isHeader: false, command: null, availability: null, hit);
        }

        public static PaletteRow Header(string title) =>
            new(title.ToUpperInvariant(), isEnabled: false, isHeader: true, command: null, availability: null, objectHit: null);
    }
}
