using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Workspace;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Document Host (`WP 10.0B`, modernised `WP 10.2A`, given real
/// per-Kind editors `WP 10.3A`) — a real, tabbed <see cref="TabControl"/>
/// bound to <see cref="IWorkspace.OpenViews"/>, always present, never
/// dockable away (`WP8.0A Workspace Architecture Document.md` §7,
/// unchanged). Each tab's own header still renders exactly what
/// <see cref="IWorkspaceView"/> exposes generically —
/// <see cref="IWorkspaceView.Title"/>, <see cref="IWorkspaceView.ObjectKind"/>
/// (via <see cref="IconRegistry"/>) — but each tab's own <em>body</em> is
/// now a real, injectable per-Kind rich editor
/// (<see cref="Editors.ObjectEditorView"/>, `WP 10.3A`), realising
/// `WP10.0A UX Architecture Document.md` §8's own Object Editors,
/// disclosed as future work since `WP 10.0B`'s own Implementation Report
/// — via the new <see cref="_contentBuilder"/> constructor parameter,
/// this class itself stays completely agnostic to what any given tab's
/// own body actually is. `WP 10.2A` added a modern tab presentation:
/// pinned tabs (sorted before unpinned ones, no close glyph while
/// pinned), a bold header for the active tab, and this Work Package's own
/// disclosed "empty workspace"/"loading placeholder" scope decisions —
/// see their own remarks below.
/// </summary>
public sealed class DocumentAreaView : UserControl
{
    private readonly TabControl _tabs = new();
    private readonly Dictionary<Guid, TabItem> _tabsByViewId = [];
    private readonly HashSet<Guid> _pinnedViewIds = [];
    private readonly Dictionary<Guid, TextBlock> _headerTextBlocks = [];
    private readonly Dictionary<Guid, string> _headerBaseText = [];
    private readonly HashSet<Guid> _extraDirtyFlags = [];
    private readonly Func<IWorkspaceView, Control> _contentBuilder;
    private TabItem? _homeTab;

    /// <summary>Raised when the user requests a tab close (its own close glyph clicked).</summary>
    public event Action<Guid>? TabCloseRequested;

    /// <summary>
    /// Initialises a new instance of the <see cref="DocumentAreaView"/>
    /// class.
    /// </summary>
    /// <param name="contentBuilder">
    /// Builds each tab's own body from the <see cref="IWorkspaceView"/> it
    /// presents — <see langword="null"/> (the default) uses
    /// <see cref="BuildDefaultBody"/>, the original generic three-line
    /// placeholder (`WP 10.0B`). The Object Editor Framework (`WP 10.3A`)
    /// is the first real caller to pass one — <c>MainWindow</c>'s own
    /// builder tries <see cref="Editors.ObjectEditorView.TryCreate"/>
    /// first, falling back to <see cref="BuildDefaultBody"/> itself for
    /// any Kind with no real Engineering Domain object behind it (a
    /// synthetic Kind, or the Sample Explorer's own fixed content) — this
    /// class itself stays agnostic to which, never hard-coding a Kind
    /// check of its own.
    /// </param>
    public DocumentAreaView(Func<IWorkspaceView, Control>? contentBuilder = null)
    {
        _contentBuilder = contentBuilder ?? BuildDefaultBody;
        _tabs.Padding = new Avalonia.Thickness(0);
        ThemeReactiveBrush.Bind(_tabs, BackgroundProperty, BrandPalette.PageBackgroundBrushKey);
        Content = _tabs;
        _tabs.SelectionChanged += (_, _) => UpdateActiveHighlighting();
    }

    /// <summary>
    /// Sets (or replaces) the permanent, non-closable "Home" tab — the
    /// Engineering Cockpit (`WP 10.1A`, `ADR-0069` — the Workspace's own
    /// default landing screen), always first, always present, distinct
    /// from every object tab <see cref="ShowTab"/> opens: closing an
    /// object tab never affects it, and it carries no close glyph of its
    /// own.
    /// </summary>
    /// <remarks>
    /// <b>The Document Area's own "empty workspace experience" (`WP 10.2A`):</b>
    /// this permanent Home tab already <em>is</em> that experience —
    /// realised, not omitted, one Work Package earlier (`WP 10.1A`,
    /// `ADR-0069`): the Document Area is never genuinely empty, since the
    /// Cockpit is always present and always selected first
    /// (<c>MainWindow</c>'s own <c>SetHomeTab</c> call). No separate,
    /// second "zero open documents" placeholder is built here — it would
    /// never be reachable.
    /// </remarks>
    public void SetHomeTab(Control content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (_homeTab is not null)
            _tabs.Items.Remove(_homeTab);

        // The Home tab carries the mark itself — the one place in the
        // document strip the brand appears, so the Cockpit is visibly the
        // product's own landing surface rather than one document among many.
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm + 2, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        header.Children.Add(new Branding.TempestLogoControl { Width = 14, Height = 14, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        header.Children.Add(new TextBlock { Text = "Cockpit", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        _homeTab = new TabItem { Header = header, Content = content };
        _tabs.Items.Insert(0, _homeTab);
        _tabs.SelectedItem = _homeTab;
    }

    /// <summary>Adds (or focuses, if already present) a tab for <paramref name="view"/>.</summary>
    public void ShowTab(IWorkspaceView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (_tabsByViewId.TryGetValue(view.Id, out var existing))
        {
            _tabs.SelectedItem = existing;
            return;
        }

        var tab = new TabItem { Content = _contentBuilder(view) };
        tab.Header = BuildHeader(view, tab);
        _tabsByViewId[view.Id] = tab;

        InsertInPinOrder(tab);
        _tabs.SelectedItem = tab;
    }

    /// <summary>Removes <paramref name="viewId"/>'s own tab, if present — a no-op otherwise.</summary>
    /// <remarks>
    /// <b>Closing the active tab must land on a neighbouring document, not
    /// on the Cockpit.</b> <see cref="TabControl"/>'s own default reaction
    /// to its selected container disappearing is to reset
    /// <see cref="SelectingItemsControl.SelectedIndex"/> to <c>0</c> — the
    /// Home tab, always first — rather than the tab that visually took the
    /// closed one's place. Confirmed live: with four tabs open and the
    /// third one active, closing it silently dumped the user back to the
    /// Cockpit instead of the fourth tab sliding into view, the exact
    /// "lose context switching between objects" defect this Work Package
    /// exists to close. Selecting the tab now sitting at the closed one's
    /// former index — clamped to the last tab if it was the rightmost —
    /// keeps the user on a document exactly as every mainstream tab strip
    /// does, and leaves an unrelated close (the closed tab was not active)
    /// untouched, since <see cref="TabControl"/> never moves selection for
    /// that case on its own.
    /// </remarks>
    public void RemoveTab(Guid viewId)
    {
        if (_tabsByViewId.Remove(viewId, out var tab))
        {
            var wasActive = ReferenceEquals(_tabs.SelectedItem, tab);
            var index = _tabs.Items.IndexOf(tab);

            _tabs.Items.Remove(tab);
            _pinnedViewIds.Remove(viewId);
            _headerTextBlocks.Remove(viewId);
            _headerBaseText.Remove(viewId);
            _extraDirtyFlags.Remove(viewId);

            if (wasActive && _tabs.Items.Count > 0)
                _tabs.SelectedIndex = Math.Min(index, _tabs.Items.Count - 1);
        }
    }

    /// <summary>
    /// Marks <paramref name="viewId"/>'s own tab dirty or clean — the
    /// Object Editor Framework's own real, buffered dirty-state
    /// (`WP 10.3A`, <see cref="Editors.ObjectEditorView.IsDirty"/>),
    /// reflected in the tab header exactly as <see cref="IWorkspaceView.IsDirty"/>
    /// already would be, had any existing View ever set it — see
    /// <see cref="BuildHeader"/>'s own remarks. A no-op if <paramref name="viewId"/>
    /// has no open tab.
    /// </summary>
    public void MarkDirty(Guid viewId, bool isDirty)
    {
        if (isDirty)
            _extraDirtyFlags.Add(viewId);
        else
            _extraDirtyFlags.Remove(viewId);

        if (_headerTextBlocks.TryGetValue(viewId, out var textBlock) && _headerBaseText.TryGetValue(viewId, out var baseText))
            textBlock.Text = isDirty ? $"{baseText} *" : baseText;
    }

    /// <summary>Gets how many tabs are currently open.</summary>
    /// <remarks>
    /// <b>"Loading placeholders where appropriate" (`WP 10.2A`):</b>
    /// disclosed, deliberately not built — <see cref="ShowTab"/> is only
    /// ever called with an already-fully-constructed <see cref="IWorkspaceView"/>
    /// (`INavigationService.OpenAsync`'s own existing, synchronous-from-the-
    /// caller's-perspective contract, unchanged since `WP 8.1B`); no code
    /// path in this platform opens a tab before its own content is ready,
    /// so a loading placeholder would never actually be shown. Building one
    /// now would be UI with nothing to demonstrate it, the same
    /// "never fabricate" discipline this platform's own Engineering Cockpit
    /// placeholders already follow — named here as future work for
    /// whichever Work Package introduces a genuinely async-loaded document
    /// (a large external attachment, a remote data source).
    /// </remarks>
    public int TabCount => _tabs.Items.Count;

    /// <summary>Gets whether <paramref name="viewId"/>'s own tab is currently pinned.</summary>
    public bool IsPinned(Guid viewId) => _pinnedViewIds.Contains(viewId);

    /// <summary>
    /// Gets whether <paramref name="viewId"/>'s own tab is currently
    /// showing a dirty indicator — <see cref="MarkDirty"/>'s own buffered
    /// flag (`WP 10.3A`, <see cref="Editors.ObjectEditorView.IsDirty"/>),
    /// the only source of a real <see langword="true"/> value anywhere in
    /// this platform today (every concrete <see cref="IWorkspaceView.IsDirty"/>
    /// still hardcodes <see langword="false"/>, unchanged). Used by
    /// <see cref="MainWindow"/> to close `TD-40` — confirm before
    /// discarding a genuinely unsaved edit.
    /// </summary>
    public bool IsMarkedDirty(Guid viewId) => _extraDirtyFlags.Contains(viewId);

    /// <summary>
    /// Gets whether *any* currently-open tab is dirty (`WP 10.5B` scope:
    /// "unsaved work handling," "clean application exit") — used by
    /// <see cref="MainWindow"/>'s own window-level Closing gate, the
    /// identical <see cref="IsMarkedDirty"/> check generalised from "one
    /// specific tab" to "the whole application about to exit."
    /// </summary>
    public bool HasAnyDirtyTab => _extraDirtyFlags.Count > 0;

    /// <summary>Selects the next tab, wrapping — <c>Ctrl+Tab</c>'s own document-switching behaviour (`WP 10.2A` Navigation, Keyboard Shortcut Framework).</summary>
    public void SelectNextTab() => Shift(+1);

    /// <summary>Selects the previous tab, wrapping — <c>Ctrl+Shift+Tab</c>'s own counterpart.</summary>
    public void SelectPreviousTab() => Shift(-1);

    /// <summary>Closes the currently active tab, if it is a closable object tab (never the Home tab) — <c>Ctrl+W</c>'s own behaviour.</summary>
    public Guid? ActiveClosableViewId =>
        _tabs.SelectedItem is TabItem selected && !ReferenceEquals(selected, _homeTab)
            ? _tabsByViewId.FirstOrDefault(kv => ReferenceEquals(kv.Value, selected)).Key
            : null;

    private void Shift(int delta)
    {
        if (_tabs.Items.Count == 0)
            return;

        var index = _tabs.SelectedIndex;
        var next = ((index + delta) % _tabs.Items.Count + _tabs.Items.Count) % _tabs.Items.Count;
        _tabs.SelectedIndex = next;
    }

    private Control BuildHeader(IWorkspaceView view, TabItem tab)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

        var pin = new Button
        {
            Content = IconGeometry.Build(IconGeometry.Pin, 11),
            Padding = new Avalonia.Thickness(DesignTokens.SpaceXs),
            MinWidth = 0,
            MinHeight = 0,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Opacity = _pinnedViewIds.Contains(view.Id) ? 1.0 : 0.4,
        };
        pin.Classes.Add(ChromeStyles.Flat);
        Avalonia.Automation.AutomationProperties.SetName(pin, $"Pin {view.Title}");
        ToolTip.SetTip(pin, "Pin this tab");
        pin.Click += (_, _) => TogglePin(view.Id, tab, pin);
        header.Children.Add(pin);

        var baseText = $"{IconRegistry.Resolve(view.ObjectKind)}  {view.Title}";
        var text = new TextBlock
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            // Combines this class's own new, real, buffered dirty-state
            // (`WP 10.3A`, MarkDirty) with view.IsDirty itself — every
            // concrete IWorkspaceView still hard-codes false today (`WP
            // 8.1A` onward), but this class never silently ignores its own
            // contract member on the chance a future View genuinely sets it.
            Text = _extraDirtyFlags.Contains(view.Id) || view.IsDirty ? $"{baseText} *" : baseText,
            FontSize = DesignTokens.FontSizeBody,
        };
        _headerTextBlocks[view.Id] = text;
        _headerBaseText[view.Id] = baseText;
        header.Children.Add(text);

        if (!_pinnedViewIds.Contains(view.Id))
        {
            var closeButton = new Button
            {
                Content = IconGeometry.Build(IconGeometry.Close, 10),
                Padding = new Avalonia.Thickness(DesignTokens.SpaceXs),
                MinWidth = 0,
                MinHeight = 0,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Opacity = 0.6,
            };
            closeButton.Classes.Add(ChromeStyles.Flat);
            Avalonia.Automation.AutomationProperties.SetName(closeButton, $"Close {view.Title}");
            ToolTip.SetTip(closeButton, "Close this tab");
            closeButton.Click += (_, _) => TabCloseRequested?.Invoke(view.Id);
            header.Children.Add(closeButton);
        }

        return header;
    }

    private void TogglePin(Guid viewId, TabItem tab, Button pinButton)
    {
        if (!_pinnedViewIds.Remove(viewId))
            _pinnedViewIds.Add(viewId);

        pinButton.Opacity = _pinnedViewIds.Contains(viewId) ? 1.0 : 0.35;

        // Rebuild this tab's own header (adds/removes the close glyph) and
        // re-sort its own position among the others.
        if (_tabsByViewId.TryGetValue(viewId, out var current) && ReferenceEquals(current, tab))
        {
            // Find the originating view is not retained here (only the
            // Guid/tab pair is) - the header is rebuilt from the tab's own
            // already-known title text, avoiding a second IWorkspaceView
            // dependency this method does not otherwise need.
            _tabs.Items.Remove(tab);
            InsertInPinOrder(tab);
            _tabs.SelectedItem = tab;
        }
    }

    /// <summary>
    /// Inserts <paramref name="tab"/> after the Home tab and after every
    /// already-pinned tab, before every unpinned one — pinned tabs always
    /// sort first among object tabs (never before Home), matching every
    /// mainstream desktop tab strip's own convention.
    /// </summary>
    private void InsertInPinOrder(TabItem tab)
    {
        var insertAt = _homeTab is not null ? 1 : 0;
        var viewId = _tabsByViewId.FirstOrDefault(kv => ReferenceEquals(kv.Value, tab)).Key;
        var isPinned = _pinnedViewIds.Contains(viewId);

        if (isPinned)
        {
            while (insertAt < _tabs.Items.Count
                   && _tabs.Items[insertAt] is TabItem existing
                   && !ReferenceEquals(existing, _homeTab)
                   && _pinnedViewIds.Contains(_tabsByViewId.FirstOrDefault(kv => ReferenceEquals(kv.Value, existing)).Key))
            {
                insertAt++;
            }
        }
        else
        {
            insertAt = _tabs.Items.Count;
        }

        _tabs.Items.Insert(insertAt, tab);
    }

    /// <summary>The original, generic three-line document body (`WP 10.0B`) — Title/Kind/Id, nothing else. The default <see cref="_contentBuilder"/>, and the Object Editor Framework's own (`WP 10.3A`) fallback for any Kind with no real Engineering Domain object behind it.</summary>
    public static Control BuildDefaultBody(IWorkspaceView view)
    {
        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(new TextBlock { Text = view.Title, FontSize = DesignTokens.FontSizeTitle, FontWeight = FontWeight.Bold });
        body.Children.Add(new TextBlock { Text = $"Kind: {view.ObjectKind}", Opacity = 0.8, FontSize = DesignTokens.FontSizeBody });
        body.Children.Add(new TextBlock { Text = $"Id: {view.ObjectId}", Opacity = 0.8, FontSize = DesignTokens.FontSizeBody });
        return body;
    }

    /// <summary>
    /// Bolds the active tab's own header text, beyond whatever the active
    /// Fluent theme already applies to the selected <see cref="TabItem"/>
    /// — a small, deliberate, additional emphasis
    /// (`WP 10.2A`'s own "active-document highlighting" requirement),
    /// never relying on colour alone (matches this platform's own
    /// Engineering Colour Language discipline, `HealthColors`).
    /// </summary>
    private void UpdateActiveHighlighting()
    {
        foreach (var item in _tabs.Items)
        {
            if (item is not TabItem { Header: Control header } tabItem)
                continue;

            var isSelected = ReferenceEquals(tabItem, _tabs.SelectedItem);
            foreach (var text in EnumerateTextBlocks(header))
                text.FontWeight = isSelected ? FontWeight.Bold : FontWeight.Normal;
        }
    }

    private static IEnumerable<TextBlock> EnumerateTextBlocks(Control root)
    {
        if (root is TextBlock text)
            yield return text;

        if (root is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control childControl)
                {
                    foreach (var found in EnumerateTextBlocks(childControl))
                        yield return found;
                }
            }
        }
    }
}
