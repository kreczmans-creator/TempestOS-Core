using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Tempest.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The handful of things every Desktop test needs and none of them should
/// own — `WP-F`.
/// </summary>
/// <remarks>
/// <para>
/// Each of these was copied between test classes rather than shared, and the
/// copying cost something real: <see cref="FindButton"/> was duplicated in a
/// simplified form that searched the whole Ribbon rather than the command's
/// own tab, and silently matched the wrong button because several
/// disciplines register commands with the same DisplayName. The version here
/// is the tab-scoped one, which is the correct one.
/// </para>
/// <para>
/// <b>Deliberately small, and deliberately test-only.</b> This is Desktop
/// test infrastructure, not an abstraction the product gained to make tests
/// convenient — it reaches for reflection and the logical tree precisely
/// because those are things a test may do and production may not. It mirrors
/// <c>Tempest.Core.Tests.Templates.RepositoryPaths</c>, which already plays
/// this role in the Core suite. Nothing is generalised beyond the call sites
/// that exist.
/// </para>
/// </remarks>
internal static class DesktopTestHelpers
{
    /// <summary>
    /// The multiplier every render deadline in this suite is scaled by,
    /// read once from <c>TEMPEST_TEST_TIMEOUT_FACTOR</c> (`WP 17.0A`). A
    /// fixed ten-second deadline passed on a quiet runner and failed on a
    /// loaded Windows workstation, which is not a defect in the product;
    /// CI sets 3, a developer running the suite alongside a build can set
    /// more, and an unset variable is 1 so nothing changes by default.
    /// </summary>
    public static double TimeoutFactor { get; } = ReadTimeoutFactor();

    private static double ReadTimeoutFactor()
    {
        var raw = Environment.GetEnvironmentVariable("TEMPEST_TEST_TIMEOUT_FACTOR");
        return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var factor) && factor > 0
            ? factor
            : 1.0;
    }

    /// <summary>A deadline <paramref name="baseSeconds"/> from now, scaled by <see cref="TimeoutFactor"/>.</summary>
    public static DateTime Deadline(double baseSeconds) =>
        DateTime.UtcNow.AddSeconds(baseSeconds * TimeoutFactor);

    /// <summary>
    /// Asserts <paramref name="control"/> is placed where a person could
    /// use it: laid out at a real size, and not drawn on top of any visible
    /// sibling (`WP 17.0A`). The `v0.16.0` calculation workspace rendered
    /// both of its columns in the same Grid column and every assertion in
    /// the suite passed, because "visible, enabled, non-zero size" is true
    /// of two controls printed over each other.
    /// </summary>
    public static void AssertPlaced(Control control, string what)
    {
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0, $"{what} rendered at {control.Bounds}.");
        AssertNoSiblingOverlap(control, what);
    }

    /// <summary>
    /// Asserts no visible sibling of <paramref name="control"/> intersects
    /// it. Siblings are the other children of its parent Panel; an overlay
    /// deliberately stacked in a Grid cell is not a sibling of the content
    /// it covers in any layout this product uses.
    /// </summary>
    /// <param name="control">The control to check against its own siblings.</param>
    /// <param name="what">Describes <paramref name="control"/> in a failure message.</param>
    /// <param name="isExemptOverlay">
    /// `WP 19.3A`: when supplied, a sibling this predicate accepts is never
    /// checked against <paramref name="control"/> — the layout walk's own
    /// explicit allow-list of intentional overlays (dialogs, the command
    /// palette, the toast host) stacked in <c>MainWindow</c>'s root
    /// <c>Grid</c>. <see langword="null"/> (every other call site) keeps
    /// this method's original behaviour exactly.
    /// </param>
    public static void AssertNoSiblingOverlap(Control control, string what, Func<Control, bool>? isExemptOverlay = null)
    {
        if (control.Parent is not Panel parent)
            return;

        var mine = control.Bounds;
        foreach (var sibling in parent.Children)
        {
            if (ReferenceEquals(sibling, control) || !sibling.IsVisible || IsDecorationOnly(sibling) || (isExemptOverlay?.Invoke(sibling) ?? false))
                continue;

            var theirs = sibling.Bounds;
            if (theirs.Width <= 0 || theirs.Height <= 0)
                continue;

            var overlap = mine.Intersect(theirs);
            Assert.False(
                overlap.Width > 0.5 && overlap.Height > 0.5,
                $"{what} at {mine} is drawn over its sibling {Describe(sibling)} at {theirs} (overlap {overlap}).");
        }
    }

    /// <summary>
    /// The layout walk's own explicit allow-list of intentional overlays
    /// (`WP 19.3A`) — every overlay <c>MainWindow</c> stacks directly in its
    /// root <c>Grid</c> over the shell's real content (`WP 10.5A`'s dialog
    /// framework, the Evidence dialogs it grew, the command palette and the
    /// toast host). These are checked as neither "mine" nor "theirs" by
    /// <see cref="AssertLayoutIsSound"/>: a modal dialog covering the whole
    /// window while open is the product working as designed, not a defect,
    /// and while closed each already reports zero bounds — this list is the
    /// explicit, structural version of that fact rather than a reliance on
    /// every dialog happening to be closed whenever the walk runs.
    /// <see cref="BusyOverlay"/> is included for the same reason even though
    /// nothing in the walk opens it.
    /// </summary>
    private static readonly HashSet<Type> IntentionalOverlayTypes =
    [
        typeof(ToastHost),
        typeof(CommandPaletteOverlay),
        typeof(BusyOverlay),
        typeof(ConfirmationDialog),
        typeof(InputDialog),
        typeof(MessageDialog),
        typeof(SettingsDialog),
        typeof(MacroManagerDialog),
        typeof(CitationPicker),
        typeof(SubjectPicker),
        typeof(DeclaredFigureEntry),
        typeof(CheckEntry),
        typeof(IssueEntry),
        typeof(ReviseReferenceRecordEntry),
    ];

    private static bool IsIntentionalOverlay(Control control) => IntentionalOverlayTypes.Contains(control.GetType());

    /// <summary>
    /// The layout walk's own whole-tree check (`WP 19.3A`, `TD-83`, the
    /// `WP 17.0A` overlap class): walks every <b>logical</b> descendant of
    /// <paramref name="root"/> and asserts, everywhere in the tree rather
    /// than at one caller's own control, the same two properties a person
    /// needs a screen to actually be usable — <see cref="AssertPlaced"/>'s
    /// premise extended from a single control to a whole rendered area.
    /// </summary>
    /// <param name="root">The rendered area to walk — a rail module's content, a project tab, or a whole window.</param>
    /// <param name="area">Names <paramref name="root"/> in every failure message (the area and the window size, so a failure is diagnosable from the message alone).</param>
    /// <remarks>
    /// <para>
    /// <b>The logical tree, deliberately, not the visual one.</b> A first
    /// version walked <see cref="Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(Avalonia.Visual)"/>
    /// and failed on its very first control: every
    /// <see cref="Window"/>'s own default template stacks a background
    /// <see cref="Border"/> and a <c>VisualLayerManager</c> as siblings in
    /// the same template <see cref="Panel"/>, coextensive with the whole
    /// window, by design — template plumbing every Avalonia control has,
    /// not a product layout defect. The logical tree
    /// (<see cref="LogicalExtensions.GetLogicalDescendants"/>) skips
    /// exactly this: a <see cref="ContentPresenter"/> re-parents its own
    /// content to be a logical child of the templated control itself, which
    /// is why <c>window.GetLogicalDescendants().OfType‹T›()</c> is already
    /// this suite's own convention for finding real application content
    /// (<see cref="EvidenceWorkspaceJourneyTests"/> and every journey test
    /// beside it) — this walk follows the identical convention, for the
    /// identical reason.
    /// </para>
    /// <para>
    /// <b>(a) No two visible, hit-testable siblings in the same Panel
    /// intersect.</b> Reuses <see cref="AssertNoSiblingOverlap"/> itself,
    /// called once per non-decoration, non-allow-listed child of every
    /// <see cref="Panel"/> found anywhere in the logical tree — the
    /// identical decoration-only exclusion, extended from one caller's own
    /// control to every panel, plus <see cref="IsIntentionalOverlay"/> so a
    /// dialog, the palette or the toast host is never checked against
    /// whatever they legitimately sit over.
    /// </para>
    /// <para>
    /// <b>(b) Every visible control's bounds lie within its parent's
    /// bounds</b>, a 1px tolerance for sub-pixel arrangement. A
    /// <see cref="ScrollViewer"/>'s own content is exempt by design — it may
    /// genuinely be taller or wider than the viewport that shows it (its
    /// content is a direct logical child of the <see cref="ScrollViewer"/>
    /// itself, exactly like any other <c>ContentControl</c>, so this is a
    /// simple type check); the <see cref="ScrollViewer"/>'s own bounds (the
    /// viewport) are what get checked against <em>its</em> parent instead,
    /// when the walk visits the <see cref="ScrollViewer"/> itself. A
    /// <see cref="Viewbox"/>'s content is exempt for the same shape of
    /// reason — fitted by a render <c>Scale</c> transform rather than by
    /// arrangement, so its un-scaled <c>Bounds</c> legitimately disagrees
    /// with the smaller size it actually renders at (every rail and Ribbon
    /// icon, <see cref="Icons.IconGeometry"/>).
    /// </para>
    /// </remarks>
    public static void AssertLayoutIsSound(Control root, string area)
    {
        ArgumentNullException.ThrowIfNull(root);

        foreach (var logical in new ILogical[] { root }.Concat(root.GetLogicalDescendants()))
        {
            if (logical is not Control control || !control.IsVisible)
                continue;

            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (!child.IsVisible || IsDecorationOnly(child) || IsIntentionalOverlay(child))
                        continue;

                    AssertNoSiblingOverlap(child, $"[{area}] {Describe(child)}", IsIntentionalOverlay);
                }
            }

            // The *visual* parent, deliberately, not the logical one used
            // above to decide what to visit: `Bounds` is a visual-tree
            // coordinate, relative to whatever control actually hosts this
            // one on screen. For an ordinary product-authored container
            // (a `Panel` a view added a child to directly) the two parents
            // are the same control. They diverge for anything a
            // `ContentPresenter` places — a `TabItem`'s own `Content`
            // renders inside the `TabControl`'s selected-content host, not
            // inside the `TabItem`'s own small header-button bounds, and a
            // `ListBoxItem` renders inside the list's internal items panel,
            // not directly inside the `ListBox`'s own outer bounds. Using
            // the logical parent there compared real content against the
            // wrong rectangle and failed on every `TabControl` in the
            // shell; the visual parent is the rectangle the control is
            // actually drawn into, whatever template stands between them.
            if (control.GetVisualParent() is not Control parent || !parent.IsVisible)
                continue;

            // `ScrollViewer` content is exempt by design (stated above),
            // checked against its own presenter's coextensive bounds; a
            // `Viewbox`'s own content is the same shape of exemption for a
            // different reason — it fits its child by a render `Scale`
            // transform, not by arranging it inside the `Viewbox`'s own
            // bounds, so the child's un-scaled `Bounds` legitimately
            // disagrees with the smaller size it actually renders at (an
            // icon's own 24x24 `StreamGeometry`, for instance, scaled down
            // to the 16x16 a rail button actually shows).
            if (parent is ScrollViewer or ScrollContentPresenter or Viewbox)
                continue;

            if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0)
                continue;

            const double tolerance = 1.0;
            var bounds = control.Bounds;
            var withinParent =
                bounds.X >= -tolerance
                && bounds.Y >= -tolerance
                && bounds.Right <= parent.Bounds.Width + tolerance
                && bounds.Bottom <= parent.Bounds.Height + tolerance;

            Assert.True(
                withinParent,
                $"[{area}] {Describe(control)} at {bounds} lies outside its parent {Describe(parent)} ({parent.Bounds.Width:0.#}x{parent.Bounds.Height:0.#}).");
        }
    }

    /// <summary>
    /// A sibling that carries no content of its own — an empty Border used
    /// as a selection accent, a Shape, or anything not hit-testable — is
    /// decoration laid deliberately over or beside content, and is not what
    /// this assertion exists to catch.
    /// </summary>
    private static bool IsDecorationOnly(Control control) =>
        !control.IsHitTestVisible
        || control is Avalonia.Controls.Shapes.Shape
        || control is Decorator { Child: null };

    private static string Describe(Control control) =>
        Avalonia.Automation.AutomationProperties.GetName(control) is { Length: > 0 } name
            ? $"'{name}' ({control.GetType().Name})"
            : control.GetType().Name;

    /// <summary>
    /// The repository root, found by walking up from the test assembly to
    /// <c>global.json</c> — the same marker <c>Directory.Build.props</c>
    /// relies on, so a source-reading test needs no hand-maintained relative
    /// path from its output directory.
    /// </summary>
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (global.json) above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// Reads a private field — for the shell's own collaborators, which
    /// <c>MainWindow</c> holds privately and exposes no seam for.
    /// </summary>
    /// <remarks>
    /// Retained rather than replaced by a production accessor: the private
    /// boundary is itself the architectural rule (`ADR-0103` — a collaborator
    /// is constructed by the composition root and never handed out), so
    /// widening it to suit a test would change the thing under test.
    /// </remarks>
    public static T GetPrivateField<T>(object instance, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");

        return (T)field.GetValue(instance)!;
    }

    /// <summary>
    /// The Ribbon button for <paramref name="commandId"/>, found inside that
    /// command's own discipline tab.
    /// </summary>
    /// <remarks>
    /// <b>Tab-scoped, and that is load-bearing.</b> Several disciplines
    /// register commands sharing a DisplayName ("Request Review" exists in
    /// three), so a Ribbon-wide search returns whichever the tree yields
    /// first — a test that passes while clicking the wrong button. Scoped by
    /// <see cref="CommandDescriptor.Category"/>, which is what builds the
    /// tabs.
    /// </remarks>
    public static Button FindButton(RibbonView ribbon, ICommandRegistry registry, string commandId)
    {
        ArgumentNullException.ThrowIfNull(ribbon);
        ArgumentNullException.ThrowIfNull(registry);

        var descriptor = registry.Items.Single(d => d.Id == commandId);
        var tab = ((TabControl)ribbon.Content!).Items.OfType<TabItem>().Single(t => Equals(t.Tag, descriptor.Category));

        return ((Control)tab.Content!).GetLogicalDescendants()
            .OfType<Button>()
            .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == descriptor.DisplayName));
    }

    /// <summary>Clicks the Ribbon button for <paramref name="commandId"/>, exactly as a user does.</summary>
    public static void Click(RibbonView ribbon, ICommandRegistry registry, string commandId) =>
        FindButton(ribbon, registry, commandId)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Switches to <paramref name="areaId"/>, finds the first object of
    /// <paramref name="kind"/> anywhere in that area's tree, and selects it.
    /// </summary>
    public static async Task<ProjectExplorerNode> SelectFirstAsync(IWorkspace workspace, string areaId, string kind)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        await workspace.Navigation.SwitchAreaAsync(areaId);
        var node = await FindAsync(workspace.ProjectExplorer, await workspace.ProjectExplorer.GetRootNodesAsync(), kind);
        Assert.NotNull(node);
        await workspace.Selection.SelectAsync(node!.Id, node.Kind!);

        return node;
    }

    /// <summary>The first object node of <paramref name="kind"/> in <paramref name="nodes"/> or below it.</summary>
    public static async Task<ProjectExplorerNode?> FindAsync(
        IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes, string kind)
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(nodes);

        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object && node.Kind == kind)
                return node;

            if (node.HasChildren && await FindAsync(explorer, await explorer.GetChildrenAsync(node.Id), kind) is { } found)
                return found;
        }

        return null;
    }
}
