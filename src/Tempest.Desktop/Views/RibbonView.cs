using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Engineering Ribbon (`WP 10.3B`) — a real, tabbed command surface
/// over <see cref="ICommandRegistry.Items"/>, grouped by
/// <see cref="CommandDescriptor.Category"/> into one tab per discipline,
/// replacing this Work Package's own two, now-consolidated predecessors:
/// the Navigation Framework's own standalone area-switch button row
/// (`WP 10.0B`) and (together with the Quick Access Toolbar) the old,
/// two-button minimal Toolbar. A view over the existing registry, exactly
/// like <see cref="CommandPaletteOverlay"/> already is (`ADR-0070`) — this
/// class registers nothing of its own, and every dispatch below reaches a
/// real, already-registered command handler, never a new one.
/// </summary>
/// <remarks>
/// <para>
/// <b>Selection-aware, real dispatch — three verbs, honestly scoped.</b>
/// `CommandDescriptor.CreateDefault` is `null` for every one of this
/// platform's own ~80 registered descriptors (confirmed by direct
/// `grep` — no discipline has ever set it), so
/// <see cref="ICommandRegistry.InvokeAsync"/> cannot invoke any of them
/// by Id alone. Rather than inventing a new, generic parameter-binding
/// mechanism (explicitly out of scope — "No new command framework"),
/// this class reuses the three real, already-Kind-keyed dispatch verbs
/// `ADR-0096`/`ADR-0097` already built for exactly this problem:
/// <b>Delete</b> dispatches immediately
/// (<see cref="IWorkspaceManager.DeleteObjectAsync"/>, needs no
/// additional input beyond the current selection); <b>Rename</b>/
/// <b>Edit</b> (Revise) route to the real editing surface that already
/// collects the required text input — the Object Editor tab
/// (`WP 10.3A`) — rather than duplicating a text box inside the ribbon
/// itself. Every other command (Create, Move, Copy, Duplicate,
/// Execute, Set Status, ...) is shown, per `ADR-0070`'s own "disabled,
/// not hidden" discoverability principle, but honestly reports it needs
/// more input than a button click supplies, rather than silently doing
/// nothing (the identical, genuine, previously-undisclosed defect
/// `CommandPaletteOverlay.InvokeSelectedAsync` had for the exact same
/// reason — found while building this class, fixed alongside it, see
/// `WP10.3B Engineering Review.md` §2).
/// </para>
/// <para>
/// <b>Command grouping and icons are both derived, not authored.</b> No
/// descriptor sets <see cref="CommandDescriptor.Icon"/> either (the
/// identical, confirmed-by-`grep` finding) — real per-command icons
/// remain disclosed future work (`FCR-0069`). This class instead
/// classifies every command by its own Id's own verb suffix
/// (`.rename`, `.delete`, `.create`, ...) into one of five groups
/// (Create/Edit/Organize/Lifecycle/Actions), each with its own
/// deterministic glyph — a real, disclosed, rendering-time heuristic,
/// never a fabricated per-command choice.
/// </para>
/// </remarks>
public sealed class RibbonView : UserControl
{
    private readonly ICommandRegistry _commandRegistry;
    private readonly IWorkspaceManager _manager;
    private readonly IWorkspace _workspace;
    private readonly Action<string?> _setHint;
    private readonly Action<IWorkspaceView> _openDocument;
    private readonly TabControl _tabs = new();
    private readonly List<string> _recentCommandIds = [];
    private readonly List<(Button Button, CommandDescriptor Descriptor)> _selectionAwareButtons = [];
    private readonly Dictionary<string, ContentControl> _recentSectionHosts = new(StringComparer.Ordinal);
    private bool _suppressTabSelection;

    /// <summary>Raised after a ribbon action completes (successfully or not), carrying a human-readable status message and its <see cref="ActionOutcome"/> — mirrors every other Desktop View's own identical <c>ActionCompleted</c> convention (`TD-58`: the outcome is what lets the subscriber refresh dependent surfaces only when the workspace actually changed).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Raised when the user clicks a discipline tab directly (not via <see cref="SelectTabForArea"/>) — the caller's own cue to switch the Navigation area to match.</summary>
    public event Action<string>? CategorySelected;

    /// <summary>An optional confirmation gate (`WP 10.5B`, Dialog Framework — "Delete Confirmation") — mirrors <see cref="ProjectExplorerView.ConfirmDeleteAsync"/> exactly, including its own identical "unwired means proceed immediately" default.</summary>
    public Func<string, Task<bool>>? ConfirmDeleteAsync { get; set; }

    /// <summary>Initialises a new instance of the <see cref="RibbonView"/> class.</summary>
    public RibbonView(ICommandRegistry commandRegistry, IWorkspaceManager manager, IWorkspace workspace, Action<string?> setHint, Action<IWorkspaceView> openDocument)
    {
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(setHint);
        ArgumentNullException.ThrowIfNull(openDocument);
        _commandRegistry = commandRegistry;
        _manager = manager;
        _workspace = workspace;
        _setHint = setHint;
        _openDocument = openDocument;

        Content = _tabs;
        _tabs.SelectionChanged += (_, _) =>
        {
            if (!_suppressTabSelection && _tabs.SelectedItem is TabItem { Tag: string category })
                CategorySelected?.Invoke(category);
        };

        Rebuild();
    }

    /// <summary>Rebuilds every tab from <see cref="ICommandRegistry.Items"/>'s own current contents — called once at construction; safe to call again if a future caller ever registers commands after construction (none does today, but no assumption is baked in that none ever will).</summary>
    public void Rebuild()
    {
        var selected = (_tabs.SelectedItem as TabItem)?.Tag as string;
        _tabs.Items.Clear();
        _selectionAwareButtons.Clear();
        _recentSectionHosts.Clear();

        var byCategory = _commandRegistry.Items
            .GroupBy(d => d.Category ?? "General")
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in byCategory)
        {
            var tab = new TabItem { Header = BuildTabHeader(group.Key), Tag = group.Key, Content = BuildTabContent(group.Key, group.ToList()) };
            _tabs.Items.Add(tab);
        }

        if (selected is not null)
            SelectTabForCategory(selected);
        else if (_tabs.Items.Count > 0)
            _tabs.SelectedIndex = 0;

        // Genuine, disclosed defect found and fixed before sign-off,
        // WP 10.3B (see WP10.3B Engineering Review.md §3): every
        // newly-built selection-aware button otherwise defaults to
        // Avalonia's own Button.IsEnabled = true regardless of whether a
        // real selection exists yet, until the next external
        // RefreshEnablement() call (a real selection change) happens to
        // occur — dishonestly showing Delete/Rename/Edit as available
        // immediately after opening, before the user has selected
        // anything at all.
        RefreshEnablement();
    }

    /// <summary>
    /// Selects the tab matching <paramref name="areaTitle"/> — "Context-
    /// sensitive ribbon tabs" (`WP 10.3B`). Matches by substring, not an
    /// exact Id/Category mapping: every real discipline's own Navigation
    /// area title already contains its own Category word (e.g. "Mechanical
    /// Product Structure" contains "Mechanical") — a deliberate, disclosed,
    /// presentation-layer heuristic that needs no coupling to
    /// <c>Tempest.Samples</c>' own module Id constants, keeping this class
    /// area-agnostic exactly like <see cref="MainWindow.BuildNavigationFramework"/>
    /// already was before this Work Package consolidated it in here.
    /// </summary>
    public void SelectTabForArea(string? areaTitle)
    {
        if (areaTitle is null)
            return;

        var category = _tabs.Items
            .OfType<TabItem>()
            .Select(t => t.Tag as string)
            .FirstOrDefault(c => c is not null && areaTitle.Contains(c, StringComparison.OrdinalIgnoreCase));

        if (category is not null)
            SelectTabForCategory(category);
    }

    private void SelectTabForCategory(string category)
    {
        var tab = _tabs.Items.OfType<TabItem>().FirstOrDefault(t => Equals(t.Tag, category));
        if (tab is null)
            return;

        _suppressTabSelection = true;
        _tabs.SelectedItem = tab;
        _suppressTabSelection = false;
    }

    /// <summary>
    /// Recomputes every selection-aware button's own enabled state —
    /// "Context-sensitive enable/disable"/"Selection-aware commands"
    /// (`WP 10.3B`). Called by <c>MainWindow</c> whenever the Workspace's
    /// own current selection changes, and after this class's own Delete
    /// dispatch completes.
    /// </summary>
    public void RefreshEnablement()
    {
        var selection = _workspace.Selection.Current;

        foreach (var (button, descriptor) in _selectionAwareButtons)
        {
            var verb = ClassifyVerbSuffix(descriptor.Id);
            var enabled = selection is not null && verb switch
            {
                "rename" => _manager.CanRename(selection.Kind),
                "edit" => _manager.CanRevise(selection.Kind),
                "delete" => _manager.CanDelete(selection.Kind),
                _ => true,
            };

            button.IsEnabled = enabled;
            ToolTip.SetTip(button, enabled
                ? descriptor.Description ?? descriptor.DisplayName
                : $"{descriptor.DisplayName} — select an object this command applies to first.");
        }
    }

    private Control BuildTabContent(string category, IReadOnlyList<CommandDescriptor> descriptors)
    {
        var root = new StackPanel { Orientation = Orientation.Vertical, Spacing = DesignTokens.SpaceXs, Margin = DesignTokens.PanelPadding };

        // A stable per-tab host for the "Recently Used" row, so
        // RecordRecent can update just this row instead of tearing down
        // and rebuilding every tab and button on every command click
        // (`TD-58` — the full rebuild also destroyed keyboard focus).
        var recentSectionHost = new ContentControl { IsVisible = false };
        _recentSectionHosts[category] = recentSectionHost;
        root.Children.Add(recentSectionHost);
        UpdateRecentSection(category);

        var groupsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceLg };
        foreach (var groupedByVerb in descriptors.GroupBy(d => ClassifyGroup(d.Id)).OrderBy(g => GroupOrder(g.Key)))
            groupsRow.Children.Add(BuildGroup(groupedByVerb.Key, groupedByVerb.ToList()));

        var scroller = new ScrollViewer { Content = groupsRow, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        root.Children.Add(scroller);

        return root;
    }

    /// <summary>
    /// Builds one discipline tab's own header — the real name, plus a
    /// small, real accent dot in that discipline's own colour
    /// (`DisciplineColors`, `WP 10.5C`, "engineering colour language") —
    /// the same real distinction Visual Studio's own "one colour per
    /// project type" and Creo's own "one colour per ribbon group"
    /// conventions both make, applied here for the first time to
    /// TempestOS's own discipline tabs.
    /// </summary>
    private static Control BuildTabHeader(string category)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new Avalonia.CornerRadius(4), Background = DisciplineColors.Resolve(category), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = category, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private Control? BuildRecentSection(string category)
    {
        var recentInCategory = _recentCommandIds
            .Select(id => _commandRegistry.Items.FirstOrDefault(d => d.Id == id))
            .Where(d => d is not null && d!.Category == category)
            .Cast<CommandDescriptor>()
            .Take(5)
            .ToList();

        if (recentInCategory.Count == 0)
            return null;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
        foreach (var descriptor in recentInCategory)
            row.Children.Add(BuildCommandButton(descriptor, large: false, registerForEnablement: false));

        return BuildSectionWithLabel("Recently Used", row);
    }

    private Control BuildGroup(string groupName, IReadOnlyList<CommandDescriptor> descriptors)
    {
        var large = groupName == "Create";
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var descriptor in descriptors.OrderBy(d => d.Id, StringComparer.Ordinal))
            wrap.Children.Add(BuildCommandButton(descriptor, large, registerForEnablement: true));

        return BuildSectionWithLabel(groupName, wrap);
    }

    private static Border BuildSectionWithLabel(string label, Control content)
    {
        var stack = new StackPanel { Spacing = DesignTokens.SpaceXs };
        stack.Children.Add(content);
        stack.Children.Add(new TextBlock { Text = label, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center });

        var divider = new Border
        {
            BorderThickness = new Avalonia.Thickness(0, 0, 1, 0),
            Padding = new Avalonia.Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceXs),
            Child = stack,
        };

        // A genuine, real theme-reactive fix (`WP 10.5C`) — this group
        // divider's own border was a fixed `Brushes.Gray` since `WP
        // 10.3B`, the identical `TD-39` class of defect this Work
        // Package also found and fixed in `CockpitCardControl`.
        ThemeReactiveBrush.Bind(divider, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return divider;
    }

    private Button BuildCommandButton(CommandDescriptor descriptor, bool large, bool registerForEnablement)
    {
        var glyph = GlyphFor(descriptor.Id);
        Control content = large
            ? new StackPanel
            {
                Spacing = DesignTokens.SpaceXs,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = glyph, FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = descriptor.DisplayName, FontSize = DesignTokens.FontSizeCaption, TextWrapping = Avalonia.Media.TextWrapping.Wrap, TextAlignment = Avalonia.Media.TextAlignment.Center, MaxWidth = 64 },
                },
            }
            : new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = DesignTokens.SpaceXs,
                Children =
                {
                    new TextBlock { Text = glyph, FontSize = DesignTokens.FontSizeBody },
                    new TextBlock { Text = descriptor.DisplayName, FontSize = DesignTokens.FontSizeCaption },
                },
            };

        var button = new Button
        {
            Content = content,
            MinHeight = large ? 56 : DesignTokens.MinControlSize,
            MinWidth = large ? 64 : DesignTokens.MinControlSize,
            Margin = new Avalonia.Thickness(DesignTokens.SpaceXs),
        };

        ToolTip.SetTip(button, descriptor.Description ?? descriptor.DisplayName);
        button.PointerEntered += (_, _) => _setHint(descriptor.Description ?? descriptor.DisplayName);
        button.PointerExited += (_, _) => _setHint(null);
        button.Click += async (_, _) => await OnCommandButtonClickAsync(descriptor).ConfigureAwait(true);

        if (registerForEnablement)
            _selectionAwareButtons.Add((button, descriptor));

        return button;
    }

    private async Task OnCommandButtonClickAsync(CommandDescriptor descriptor)
    {
        var verb = ClassifyVerbSuffix(descriptor.Id);
        var selection = _workspace.Selection.Current;

        if (verb == "delete")
        {
            if (selection is null || !_manager.CanDelete(selection.Kind))
            {
                ActionCompleted?.Invoke($"'{descriptor.DisplayName}' needs a selected object first.", ActionOutcome.Failed);
                return;
            }

            if (ConfirmDeleteAsync is { } confirm && !await confirm($"Delete the selected {selection.Kind}? This cannot be undone.").ConfigureAwait(true))
                return;

            var result = await _manager.DeleteObjectAsync(selection.ObjectId, selection.Kind).ConfigureAwait(true);
            RecordRecent(descriptor.Id);
            RefreshEnablement();
            ActionCompleted?.Invoke(
                result.Succeeded ? $"Deleted via '{descriptor.DisplayName}'." : result.Message ?? "Delete failed.",
                ActionOutcome.From(result.Succeeded));
            return;
        }

        if (verb is "rename" or "edit")
        {
            var canEdit = selection is not null && (verb == "rename" ? _manager.CanRename(selection.Kind) : _manager.CanRevise(selection.Kind));
            if (!canEdit)
            {
                ActionCompleted?.Invoke($"'{descriptor.DisplayName}' needs a selected object this command applies to.", ActionOutcome.Failed);
                return;
            }

            var view = await _workspace.Navigation.OpenAsync(selection!.ObjectId, selection.Kind).ConfigureAwait(true);
            _openDocument(view);
            RecordRecent(descriptor.Id);
            ActionCompleted?.Invoke(
                $"Opened for editing via '{descriptor.DisplayName}' — use the Name/Content fields in the editor tab.",
                ActionOutcome.NoChange);
            return;
        }

        if (descriptor.CreateDefault is not null)
        {
            var result = await _commandRegistry.InvokeAsync(descriptor.Id).ConfigureAwait(true);
            RecordRecent(descriptor.Id);
            ActionCompleted?.Invoke(
                result.Succeeded ? $"'{descriptor.DisplayName}' completed." : result.Message ?? "Command failed.",
                ActionOutcome.From(result.Succeeded));
            return;
        }

        // Real "object creation/duplicate workflow" (`WP 10.5B` scope,
        // extended broadly by `WP 10.7A`) — `ObjectCreationHandlers` names
        // every command Id this platform has wired to a genuine dispatch
        // flow: Create/Duplicate/status-transitions across all six
        // disciplines. What remains unwired (Copy — no destination-parent
        // picker dialog exists anywhere in this platform, `FCR-0073`) is
        // named honestly by the fallback message below, never claiming a
        // command works when it still falls through here.
        if (ObjectCreationHandlers.TryGetValue(descriptor.Id, out var createFlow))
        {
            await createFlow().ConfigureAwait(true);
            RecordRecent(descriptor.Id);
            return;
        }

        // Honest fallback (`WP 10.8A`) — deliberately names no alternative
        // surface. Confirmed by direct investigation that neither
        // alternative this message used to name actually helps: the
        // Command Palette cannot invoke any real discipline command by Id
        // (no `CommandDescriptor.CreateDefault` is ever set for one,
        // `CommandPaletteOverlay`'s own remarks), and the Project
        // Explorer's own context menu offers only Open/Rename/Delete/
        // Favourite — never Copy or any other command that reaches this
        // branch. Claiming either would be exactly the "misleading
        // messaging" this Work Package's own controlling instruction
        // forbids.
        ActionCompleted?.Invoke(
            $"'{descriptor.DisplayName}' isn't available yet — no destination picker or additional-input UI exists in this platform to collect what it needs.",
            ActionOutcome.Failed);
    }

    /// <summary>
    /// Real, wired Create/Duplicate/Move/Copy flows, keyed by
    /// <see cref="CommandDescriptor.Id"/> — set once by
    /// <see cref="MainWindow"/> after construction (mirrors
    /// <see cref="ConfirmDeleteAsync"/>'s own identical "optional, set
    /// post-construction, unwired means fall through to the honest
    /// message" discipline). Empty by default — a caller that never wires
    /// this (any existing test constructing this view directly) sees the
    /// identical pre-`WP 10.5B` fallback behaviour, unaffected.
    /// </summary>
    public IDictionary<string, Func<Task>> ObjectCreationHandlers { get; } = new Dictionary<string, Func<Task>>();

    private void RecordRecent(string id)
    {
        _recentCommandIds.Remove(id);
        _recentCommandIds.Insert(0, id);
        if (_recentCommandIds.Count > 5)
            _recentCommandIds.RemoveAt(_recentCommandIds.Count - 1);

        // Only the "Recently Used" rows changed — updating them in place
        // (instead of the previous full Rebuild()) keeps every existing
        // tab and button alive, preserving keyboard focus and avoiding a
        // second RefreshEnablement pass per click (`TD-58`).
        foreach (var category in _recentSectionHosts.Keys)
            UpdateRecentSection(category);
    }

    /// <summary>Recomputes one tab's own "Recently Used" row inside its stable host — the incremental complement to <see cref="Rebuild"/> (`TD-58`).</summary>
    private void UpdateRecentSection(string category)
    {
        if (!_recentSectionHosts.TryGetValue(category, out var host))
            return;

        var section = BuildRecentSection(category);
        host.Content = section;
        host.IsVisible = section is not null;
    }

    /// <summary>
    /// Classifies <paramref name="id"/> into one of five ribbon groups by
    /// its own well-known Id suffix — a real, deterministic, disclosed
    /// heuristic derived from data every discipline's own registration
    /// already provides, never a fabricated per-command choice (see class
    /// remarks).
    /// </summary>
    private static string ClassifyGroup(string id) => ClassifyVerbSuffix(id) switch
    {
        "create" => "Create",
        "rename" or "edit" or "move" or "copy" or "duplicate" => "Organize",
        "delete" => "Organize",
        "lock" or "unlock" or "request-review" or "approve" or "archive" or "release" or "set-status" => "Lifecycle",
        _ => "Actions",
    };

    private static int GroupOrder(string group) => group switch
    {
        "Create" => 0,
        "Organize" => 1,
        "Lifecycle" => 2,
        _ => 3,
    };

    private static string ClassifyVerbSuffix(string id)
    {
        var lastDot = id.LastIndexOf('.');
        return lastDot >= 0 ? id[(lastDot + 1)..] : id;
    }

    private static string GlyphFor(string id) => ClassifyVerbSuffix(id) switch
    {
        "create" or "create-group" or "create-collection" => "➕",
        "rename" => "✏️",
        "edit" or "revise" => "📝",
        "delete" or "delete-group" or "delete-collection" => "🗑️",
        "move" or "move-group" => "↔️",
        "copy" => "📋",
        "duplicate" => "📑",
        "execute" => "▶️",
        "recalculate" => "🔄",
        "lock" => "🔒",
        "unlock" => "🔓",
        "request-review" => "👁️",
        "approve" => "✅",
        "archive" => "🗄️",
        "release" => "📤",
        "attach" => "📎",
        "link" => "🔗",
        "record-result" or "record-inspection-result" => "📊",
        "compare-baselines" => "⚖️",
        "validate-configuration" => "✔️",
        "set-bom-line" or "set-status" or "set-owner" or "set-priority" => "⚙️",
        "add-to-collection" => "📥",
        "bulk-set-status" or "bulk-set-owner" or "bulk-set-priority" => "🗂️",
        _ => "🔹",
    };
}
