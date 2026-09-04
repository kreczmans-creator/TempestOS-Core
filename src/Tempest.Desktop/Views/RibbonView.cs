using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Composition;
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
/// <b>Every button asks the command framework, and does what it says.</b>
/// A click evaluates the command against the current selection
/// (<see cref="ICommandRegistry.Evaluate"/>) and then invokes it through
/// <see cref="ICommandRegistry.InvokeAsync(string, CommandContext, CommandParameterPrompt?, CancellationToken)"/>,
/// which builds the real command from the descriptor's own binding
/// (TD-77). Enablement asks the same question. Nothing here re-derives
/// what a command needs, and there is no generic "isn't available yet"
/// message left: an unavailable command reports its own declared reason,
/// visible and disabled rather than hidden (<c>ADR-0070</c>).
/// </para>
/// <para>
/// <b>Two product decisions route elsewhere, named explicitly.</b>
/// Rename/Edit open the Object Editor, which is the real surface for
/// collecting text (<c>ADR-0096</c>/<c>ADR-0097</c>); Delete dispatches
/// through <see cref="IWorkspaceManager.DeleteObjectAsync"/>, which is
/// where a successful delete clears the selection (<c>TD-58</c>). Both are
/// lists of command Ids in <see cref="SurfaceCommandPolicy"/> — never
/// recovered by parsing an Id, which is what previously left
/// <c>requirements.delete-group</c> and <c>requirements.revise</c>
/// unreachable.
/// </para>
/// <para>
/// <b>Command grouping and icons are still derived, not authored.</b> No
/// descriptor sets <see cref="CommandDescriptor.Icon"/> (real per-command
/// icons remain <c>FCR-0069</c>), so this class picks a tab group and a
/// vector icon (<see cref="Icons.IconGeometry"/>) from the Id's own
/// trailing word — a rendering heuristic, and the only thing that suffix
/// is still read for.
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
    private readonly List<Control> _tabContents = [];
    private bool _suppressTabSelection;
    private bool _isCollapsed;

    /// <summary>Raised after a ribbon action completes (successfully or not), carrying a human-readable status message and its <see cref="ActionOutcome"/> — mirrors every other Desktop View's own identical <c>ActionCompleted</c> convention (`TD-58`: the outcome is what lets the subscriber refresh dependent surfaces only when the workspace actually changed).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Raised when the user clicks a discipline tab directly (not via <see cref="SelectTabForArea"/>) — the caller's own cue to switch the Navigation area to match.</summary>
    public event Action<string>? CategorySelected;

    /// <summary>Raised after <see cref="SetCollapsed"/> changes the ribbon's own collapsed state, carrying the new state — the caller's own cue to persist it (`TD-70`).</summary>
    public event Action<bool>? CollapsedChanged;

    /// <summary>
    /// Gets whether the ribbon is minimised to its own tab strip
    /// (`TD-70`) — the standard ribbon affordance for reclaiming vertical
    /// space on a laptop or split screen. Every tab header stays visible
    /// and clickable; only the command content area is hidden, so no
    /// command becomes unreachable.
    /// </summary>
    public bool IsCollapsed => _isCollapsed;

    /// <summary>Minimises the ribbon to its own tab strip, or restores it (`TD-70`).</summary>
    public void SetCollapsed(bool collapsed)
    {
        if (_isCollapsed == collapsed)
            return;

        _isCollapsed = collapsed;
        ApplyCollapsedState();
        CollapsedChanged?.Invoke(collapsed);
    }

    /// <summary>Toggles <see cref="IsCollapsed"/> — the double-click/keyboard affordance's own target.</summary>
    public void ToggleCollapsed() => SetCollapsed(!_isCollapsed);

    private void ApplyCollapsedState()
    {
        foreach (var content in _tabContents)
            content.IsVisible = !_isCollapsed;
    }

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

        // The ribbon is a surface with a hairline beneath it; the tab strip
        // itself stays this control's own Content (a view over the
        // registry, and what every test reaches for).
        _tabs.Padding = new Avalonia.Thickness(0);
        ThemeReactiveBrush.Bind(_tabs, BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
        BorderThickness = new Avalonia.Thickness(0, 0, 0, 1);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, BrandPalette.HairlineBrushKey);
        Content = _tabs;

        // Double-click a tab header to minimise/restore — the convention
        // every ribbon application shares (`TD-70`).
        _tabs.DoubleTapped += (_, e) =>
        {
            if (e.Source is Visual source && source.FindAncestorOfType<TabItem>(includeSelf: true) is not null)
            {
                ToggleCollapsed();
                e.Handled = true;
            }
        };

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
        _tabContents.Clear();

        var byCategory = _commandRegistry.Items
            .GroupBy(d => d.Category ?? "General")
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in byCategory)
        {
            var content = BuildTabContent(group.Key, group.ToList());
            _tabContents.Add(content);
            var tab = new TabItem { Header = BuildTabHeader(group.Key), Tag = group.Key, Content = content };
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

        // A rebuild recreates every content panel — re-apply the current
        // minimised state so it survives (`TD-70`).
        ApplyCollapsedState();
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
        // TD-77 Stage 5: one question, asked of the command framework.
        // This used to classify each command by the text after the last
        // dot in its Id and then re-derive eligibility from the manager -
        // which left every Id the parser had not anticipated
        // ("requirements.delete-group", "requirements.revise") permanently
        // enabled and permanently unreachable, and left Move/Copy/Link
        // looking available right up until the click failed.
        var context = CurrentContext();

        foreach (var (button, descriptor) in _selectionAwareButtons)
        {
            var availability = _commandRegistry.Evaluate(descriptor.Id, context);

            button.IsEnabled = availability.IsAvailable;

            // ADR-0070: an unavailable command stays visible and says why,
            // in its own words rather than one generic sentence.
            ToolTip.SetTip(button, availability.IsAvailable
                ? descriptor.Description ?? descriptor.DisplayName
                : availability.Reason);
        }
    }

    /// <summary>
    /// The Workspace's own live selection, as the Command Framework sees
    /// it — built through the one shared adapter, never assembled here.
    /// </summary>
    private CommandContext CurrentContext() => WorkspaceCommandContext.From(_workspace.Selection);

    private Control BuildTabContent(string category, IReadOnlyList<CommandDescriptor> descriptors)
    {
        var root = new StackPanel { Orientation = Orientation.Vertical, Spacing = DesignTokens.SpaceXs, Margin = new Avalonia.Thickness(DesignTokens.SpaceLg, DesignTokens.SpaceSm, DesignTokens.SpaceLg, DesignTokens.SpaceMd) };

        // A stable per-tab host for the "Recently Used" row, so
        // RecordRecent can update just this row instead of tearing down
        // and rebuilding every tab and button on every command click
        // (`TD-58` — the full rebuild also destroyed keyboard focus).
        var recentSectionHost = new ContentControl { IsVisible = false };
        _recentSectionHosts[category] = recentSectionHost;
        root.Children.Add(recentSectionHost);
        UpdateRecentSection(category);

        var groupsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        foreach (var groupedByVerb in descriptors.GroupBy(d => ClassifyGroup(d.Id)).OrderBy(g => GroupOrder(g.Key)))
            groupsRow.Children.Add(BuildGroup(groupedByVerb.Key, groupedByVerb.ToList()));

        var scroller = new ScrollViewer { Content = groupsRow, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };

        // The real, root cause fix (`WP-Z4`, responsive Ribbon closure):
        // a ScrollViewer's `Auto` horizontal scrollbar only ever occupies
        // room its own final size already has *slack* for — it never
        // grows itself purely to make room for its own scrollbar. Nothing
        // upstream of this ScrollViewer (a vertical StackPanel, itself
        // sized to content) ever gives it more height than the button
        // rows alone need, so at any width narrow enough that
        // `groupsRow` genuinely overflows, the ScrollViewer had nowhere
        // to draw the one affordance that would tell the user horizontal
        // scrolling was even possible — the command groups past that
        // width were simply clipped, in total silence. Reserving one
        // scrollbar's worth of height (`DesignTokens.SpaceXl`, the
        // platform's own existing spacing step, not a new magic number)
        // costs nothing at a width wide enough that no scrollbar ever
        // appears, and is exactly what lets `Auto` actually show one the
        // moment it is needed. Read once after the first real layout
        // pass, from `groupsRow`'s own measured height — itself
        // independent of window width — never a literal pixel total.
        void ReserveScrollbarHeightOnce(object? _, EventArgs __)
        {
            scroller.LayoutUpdated -= ReserveScrollbarHeightOnce;
            scroller.MinHeight = groupsRow.DesiredSize.Height + DesignTokens.SpaceXl;
        }

        scroller.LayoutUpdated += ReserveScrollbarHeightOnce;
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
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm + 2, VerticalAlignment = VerticalAlignment.Center };
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
        // The group's own name is a wide-tracked micro label in the
        // structural face beneath its buttons — the design system's
        // chrome-label treatment, so the group reads as a ribbon section
        // rather than as a caption.
        var caption = new TextBlock
        {
            Text = label,
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeLabel,
            FontWeight = DesignTokens.WeightLabel,
            LetterSpacing = DesignTokens.LabelTracking,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(0, DesignTokens.SpaceXs, 0, 0),
        };
        ThemeReactiveBrush.Bind(caption, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);

        var stack = new StackPanel { Spacing = DesignTokens.SpaceXs };
        stack.Children.Add(content);
        stack.Children.Add(caption);

        var divider = new Border
        {
            BorderThickness = new Avalonia.Thickness(0, 0, 1, 0),
            Padding = new Avalonia.Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceXs, DesignTokens.SpaceLg, DesignTokens.SpaceXs),
            Child = stack,
        };

        // Theme-reactive (`WP 10.5C`, closes the `TD-39` class of defect
        // here) — the brand's hairline, never a fixed grey.
        ThemeReactiveBrush.Bind(divider, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        return divider;
    }

    private Button BuildCommandButton(CommandDescriptor descriptor, bool large, bool registerForEnablement)
    {
        // One monochrome vector icon per verb (`IconGeometry`), tinted by
        // the button's own foreground — never a colour emoji.
        var icon = IconFor(descriptor.Id);
        Control content = large
            ? new StackPanel
            {
                Spacing = DesignTokens.SpaceSm,
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    Icons.IconGeometry.Build(icon, 22, strokeThickness: 1.5),
                    new TextBlock { Text = descriptor.DisplayName, FontSize = DesignTokens.FontSizeCaption, TextWrapping = Avalonia.Media.TextWrapping.Wrap, TextAlignment = Avalonia.Media.TextAlignment.Center, MaxWidth = 68, LineHeight = 13 },
                },
            }
            : new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = DesignTokens.SpaceSm + 1,
                Children =
                {
                    Icons.IconGeometry.Build(icon, 14),
                    new TextBlock { Text = descriptor.DisplayName, FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center },
                },
            };

        var button = new Button
        {
            Content = content,
            MinHeight = large ? 60 : DesignTokens.MinControlSize,
            MinWidth = large ? 68 : DesignTokens.MinControlSize,
            Margin = new Avalonia.Thickness(DesignTokens.SpaceXs, 0),
            Padding = large ? new Avalonia.Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceMd) : new Avalonia.Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceSm),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        // `large` is exactly the Create group's own tiles (see `BuildGroup`)
        // — the ribbon's one commit-shaped action per discipline, styled
        // accent-filled like every other primary create/commit action in
        // the shell (`ProjectRisksView`/`ProjectTasksView`/`ProjectTimelineView`'s
        // own "+ New" buttons, `ObjectEditorView.Save`, ...). Every other
        // command (Organize/Lifecycle/Actions, and any command reappearing
        // in a compact "Recently Used" row) stays the flat, secondary
        // treatment — before this fix every ribbon button looked identical
        // regardless of importance, sized apart but never distinguished by
        // weight or colour.
        button.Classes.Add(large ? ChromeStyles.Primary : ChromeStyles.Flat);

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
        var context = CurrentContext();

        // The single availability implementation, consulted rather than
        // re-derived. Everything below assumes only what it established.
        var availability = _commandRegistry.Evaluate(descriptor.Id, context);
        if (!availability.IsAvailable)
        {
            // The command's own reason - a Move says it needs a destination
            // picker, a wrong-Kind selection says which Kinds it applies to.
            // There is no generic fallback message any more.
            ActionCompleted?.Invoke(availability.Reason!, ActionOutcome.Failed);
            return;
        }

        // Product decision, not a classification: Rename/Edit open the real
        // editing surface (ADR-0096/ADR-0097). Named explicitly in
        // SurfaceCommandPolicy, never recovered from the Id's own text.
        if (SurfaceCommandPolicy.ObjectEditorCommandIds.Contains(descriptor.Id))
        {
            await OpenForEditingAsync(descriptor, context).ConfigureAwait(true);
            return;
        }

        // Product decision: deleting goes through the Workspace manager,
        // because that is where a successful delete clears the selection
        // (TD-58). See SurfaceCommandPolicy.DeleteCommandIds.
        if (SurfaceCommandPolicy.DeleteCommandIds.Contains(descriptor.Id))
        {
            await DeleteAsync(descriptor, context).ConfigureAwait(true);
            return;
        }

        var invocation = await _commandRegistry
            .InvokeAsync(descriptor.Id, context, ParameterPrompt)
            .ConfigureAwait(true);

        switch (invocation.Outcome)
        {
            case CommandOutcome.Executed:
                var result = invocation.Result!;
                RecordRecent(descriptor.Id);
                RefreshEnablement();
                ActionCompleted?.Invoke(
                    result.Succeeded
                        ? $"'{descriptor.DisplayName}' completed."
                        : result.Message ?? $"'{descriptor.DisplayName}' failed.",
                    ActionOutcome.From(result.Succeeded));
                break;

            case CommandOutcome.Cancelled:
                // Declining is not failing. Nothing ran, nothing changed,
                // and nothing is reported - no toast, no status text, and
                // no history entry.
                break;

            default:
                ActionCompleted?.Invoke(invocation.Reason!, ActionOutcome.Failed);
                break;
        }
    }

    /// <summary>
    /// Opens the selected object in the Object Editor — the real surface
    /// that collects a new name or new content, with the object in front of
    /// the user rather than a one-line box floating over the ribbon
    /// (<c>ADR-0096</c>/<c>ADR-0097</c>, deliberately kept by TD-77 Stage 5).
    /// </summary>
    private async Task OpenForEditingAsync(CommandDescriptor descriptor, CommandContext context)
    {
        var primary = context.Primary!;
        var view = await _workspace.Navigation.OpenAsync(primary.ObjectId, primary.Kind).ConfigureAwait(true);
        _openDocument(view);
        RecordRecent(descriptor.Id);
        ActionCompleted?.Invoke(
            $"Opened for editing via '{descriptor.DisplayName}' — use the Name/Content fields in the editor tab.",
            ActionOutcome.NoChange);
    }

    /// <summary>
    /// Deletes through <see cref="IWorkspaceManager.DeleteObjectAsync"/>,
    /// which is where a successful delete clears the selection (<c>TD-58</c>).
    /// The confirmation text is the binding's own
    /// <see cref="CommandBinding.ConfirmationMessage"/>, so Core still says
    /// what to ask; whether to ask at all remains
    /// <see cref="ConfirmDeleteAsync"/>'s settings-controlled decision.
    /// </summary>
    private async Task DeleteAsync(CommandDescriptor descriptor, CommandContext context)
    {
        var primary = context.Primary!;
        var message = descriptor.Binding?.ConfirmationMessage
            ?? $"Delete the selected {primary.Kind}? This cannot be undone.";

        if (ConfirmDeleteAsync is { } confirm && !await confirm(message).ConfigureAwait(true))
            return;

        var result = await _manager.DeleteObjectAsync(primary.ObjectId, primary.Kind).ConfigureAwait(true);
        RecordRecent(descriptor.Id);
        RefreshEnablement();
        ActionCompleted?.Invoke(
            result.Succeeded ? $"Deleted via '{descriptor.DisplayName}'." : result.Message ?? "Delete failed.",
            ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// Collects the values and confirmations a command's own binding
    /// declares — supplied by <c>MainWindow</c> after construction, exactly
    /// as <see cref="ConfirmDeleteAsync"/> is. Left unwired (any test
    /// constructing this view directly), a command needing input reports
    /// that honestly through <see cref="ActionCompleted"/> rather than
    /// running without asking.
    /// </summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

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
    private static string ClassifyGroup(string id) => PresentationVerbSuffix(id) switch
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

    /// <summary>
    /// The text after the last dot in <paramref name="id"/> — used to pick
    /// a tab group and a glyph, and for nothing else.
    /// </summary>
    /// <remarks>
    /// <b>Presentation only, and named to say so.</b> This was once
    /// <c>ClassifyVerbSuffix</c> and decided which command dispatched where
    /// and which button was enabled; TD-77 Stage 5 moved both of those to
    /// <see cref="ICommandRegistry.Evaluate"/> and
    /// <see cref="SurfaceCommandPolicy"/>. Choosing a folder icon from a
    /// name is a rendering heuristic and stays fine; deciding what a
    /// command <i>is</i> from its name was the defect that made
    /// <c>requirements.delete-group</c> and <c>requirements.revise</c>
    /// unreachable.
    /// </remarks>
    private static string PresentationVerbSuffix(string id)
    {
        var lastDot = id.LastIndexOf('.');
        return lastDot >= 0 ? id[(lastDot + 1)..] : id;
    }

    private static Avalonia.Media.StreamGeometry IconFor(string id) => PresentationVerbSuffix(id) switch
    {
        "create" or "create-group" or "create-collection" => Icons.IconGeometry.Plus,
        "rename" => Icons.IconGeometry.Pencil,
        "edit" or "revise" => Icons.IconGeometry.Edit,
        "delete" or "delete-group" or "delete-collection" => Icons.IconGeometry.Trash,
        "move" or "move-group" => Icons.IconGeometry.Move,
        "copy" => Icons.IconGeometry.Copy,
        "duplicate" => Icons.IconGeometry.Duplicate,
        "execute" => Icons.IconGeometry.Play,
        "recalculate" => Icons.IconGeometry.Refresh,
        "lock" => Icons.IconGeometry.Lock,
        "unlock" => Icons.IconGeometry.Unlock,
        "request-review" => Icons.IconGeometry.Eye,
        "approve" => Icons.IconGeometry.CheckCircle,
        "archive" => Icons.IconGeometry.Archive,
        "release" => Icons.IconGeometry.Upload,
        "attach" => Icons.IconGeometry.Paperclip,
        "link" => Icons.IconGeometry.Link,
        "record-result" or "record-inspection-result" => Icons.IconGeometry.Chart,
        "compare-baselines" => Icons.IconGeometry.Scales,
        "validate-configuration" => Icons.IconGeometry.CheckCircle,
        "set-bom-line" or "set-status" or "set-owner" or "set-priority" => Icons.IconGeometry.Sliders,
        "add-to-collection" => Icons.IconGeometry.Inbox,
        "bulk-set-status" or "bulk-set-owner" or "bulk-set-priority" => Icons.IconGeometry.Layers,
        _ => Icons.IconGeometry.Dot,
    };
}
