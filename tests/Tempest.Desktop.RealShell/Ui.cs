using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Tempest.Desktop.RealShell;

/// <summary>One control as this runner sees it: what it is called, what it reads, and where it is on the physical screen.</summary>
internal sealed record UiNode(
    int Id,
    string Name,
    string Text,
    string TypeName,
    int Left,
    int Top,
    int Width,
    int Height,
    bool Visible,
    bool Enabled)
{
    internal int CentreX => Left + (Width / 2);

    internal int CentreY => Top + (Height / 2);

    internal bool HasArea => Width > 0 && Height > 0;

    public override string ToString() =>
        $"{TypeName,-24} name='{Name}' text='{Text}' at {Left},{Top} {Width}x{Height} {(Visible ? "visible" : "hidden")} {(Enabled ? "enabled" : "disabled")}";
}

/// <summary>
/// Read-only introspection of the live visual tree, always on the UI thread
/// through <see cref="Dispatcher.UIThread"/>. This is how the runner *finds*
/// a control and *reads* what it shows; it is never how the runner makes
/// something happen - that is <see cref="OsInput"/>'s job alone.
/// </summary>
internal static class Ui
{
    internal static T OnUiThread<T>(Func<T> read) =>
        Dispatcher.UIThread.InvokeAsync(read).GetTask().GetAwaiter().GetResult();

    internal static IClassicDesktopStyleApplicationLifetime? Lifetime =>
        Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    /// <summary>Every visual in every open top-level window, main window first.</summary>
    private static IEnumerable<Visual> AllVisuals()
    {
        var lifetime = Lifetime;
        if (lifetime is null)
            yield break;

        var windows = new List<Window>();
        if (lifetime.MainWindow is { } main)
            windows.Add(main);

        foreach (var window in lifetime.Windows)
        {
            if (!windows.Contains(window))
                windows.Add(window);
        }

        var seen = new HashSet<Visual>();

        foreach (var window in windows)
        {
            if (seen.Add(window))
                yield return window;

            foreach (var visual in window.GetVisualDescendants())
            {
                if (seen.Add(visual))
                    yield return visual;
            }

            // A drop-down, a flyout or a context menu is not in the window's
            // own visual tree: X11 gives each one its own popup root. Their
            // items are exactly what a user clicks, so they are walked too,
            // reached through the open Popup's own child.
            var popups = window.GetVisualDescendants().OfType<Popup>()
                .Concat(window.GetLogicalDescendants().OfType<Popup>())
                .Distinct();

            foreach (var popup in popups)
            {
                if (!popup.IsOpen || popup.Child is not { } child)
                    continue;

                var start = child.GetVisualRoot() is Visual popupRoot && !ReferenceEquals(popupRoot, window)
                    ? popupRoot
                    : child;

                if (seen.Add(start))
                    yield return start;

                foreach (var visual in start.GetVisualDescendants())
                {
                    if (seen.Add(visual))
                        yield return visual;
                }
            }
        }
    }

    /// <summary>Every top-level surface a click can land on, topmost first: open popups, then the windows.</summary>
    private static IEnumerable<Visual> Roots()
    {
        var lifetime = Lifetime;
        if (lifetime is null)
            yield break;

        var windows = new List<Window>();
        if (lifetime.MainWindow is { } main)
            windows.Add(main);

        foreach (var window in lifetime.Windows)
        {
            if (!windows.Contains(window))
                windows.Add(window);
        }

        foreach (var window in windows)
        {
            foreach (var popup in window.GetVisualDescendants().OfType<Popup>()
                         .Concat(window.GetLogicalDescendants().OfType<Popup>()).Distinct())
            {
                if (popup.IsOpen && popup.Child?.GetVisualRoot() is Visual root && !ReferenceEquals(root, window))
                    yield return root;
            }
        }

        foreach (var window in windows)
            yield return window;
    }

    /// <summary>
    /// What is actually on top at this screen point, as the chain of ids
    /// from the hit element up to its root. This is how the runner refuses
    /// to click blind: a control whose bounds say it is there but which is
    /// clipped away, covered by an overlay, or scrolled under a header is
    /// simply not in the chain, and the click is not sent.
    /// </summary>
    internal static IReadOnlyList<int> HitTestIds(int screenX, int screenY) =>
        OnUiThread(() =>
        {
            foreach (var root in Roots())
            {
                try
                {
                    var client = root.PointToClient(new PixelPoint(screenX, screenY));
                    if (client.X < 0 || client.Y < 0 || client.X > root.Bounds.Width || client.Y > root.Bounds.Height)
                        continue;

                    var chain = new List<int>();
                    foreach (var hit in root.GetVisualsAt(client))
                    {
                        for (var visual = hit; visual is not null; visual = visual.GetVisualParent())
                        {
                            var id = RuntimeHelpers.GetHashCode(visual);
                            if (!chain.Contains(id))
                                chain.Add(id);
                        }
                    }

                    if (chain.Count == 0)
                        continue;

                    return chain;
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // A root between layout passes cannot be hit-tested; try the next.
                }
            }

            return [];
        });

    private static UiNode Describe(Visual visual)
    {
        var name = visual.GetValue(AutomationProperties.NameProperty) ?? string.Empty;
        var text = TextOf(visual);
        var visible = visual.IsEffectivelyVisible;
        var enabled = visual is not Control control || control.IsEffectivelyEnabled;

        var left = 0;
        var top = 0;
        var width = 0;
        var height = 0;

        try
        {
            if (visible && visual.GetVisualRoot() is not null)
            {
                var topLeft = visual.PointToScreen(new Point(0, 0));
                var bottomRight = visual.PointToScreen(new Point(visual.Bounds.Width, visual.Bounds.Height));
                left = topLeft.X;
                top = topLeft.Y;
                width = bottomRight.X - topLeft.X;
                height = bottomRight.Y - topLeft.Y;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A visual that is between layout passes has no screen point yet;
            // it is reported with no area and the caller waits for the next poll.
        }

        return new UiNode(RuntimeHelpers.GetHashCode(visual), name, text, visual.GetType().Name, left, top, width, height, visible, enabled);
    }

    private static string TextOf(Visual visual) => visual switch
    {
        TextBlock block => block.Text ?? string.Empty,
        TextBox box => box.Text ?? string.Empty,
        NumericUpDown spinner => spinner.Text ?? spinner.Value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
        ComboBox combo => combo.SelectionBoxItem?.ToString() ?? string.Empty,
        ContentControl content when content.Content is string s => s,
        _ => string.Empty,
    };

    /// <summary>A snapshot of every visible control, taken in one UI-thread pass.</summary>
    internal static IReadOnlyList<UiNode> Snapshot() =>
        OnUiThread(() => AllVisuals().Select(Describe).Where(node => node.Visible).ToList() as IReadOnlyList<UiNode>);

    /// <summary>Every piece of visible text on screen, in tree order.</summary>
    internal static IReadOnlyList<string> VisibleText() =>
        Snapshot().Select(node => node.Text).Where(text => text.Length > 0).ToList();

    internal static bool ShowsText(string fragment) =>
        VisibleText().Any(text => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    internal static string? FirstTextContaining(string fragment) =>
        VisibleText().FirstOrDefault(text => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    internal static int CountTextContaining(string fragment) =>
        VisibleText().Count(text => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>The visible, clickable control carrying this exact automation name.</summary>
    internal static UiNode? ByName(string name) =>
        Snapshot().FirstOrDefault(node =>
            string.Equals(node.Name, name, StringComparison.Ordinal) && node.HasArea);

    /// <summary>The visible control whose automation name contains this fragment.</summary>
    internal static UiNode? ByNameContaining(string fragment) =>
        Snapshot().FirstOrDefault(node =>
            node.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase) && node.HasArea);

    /// <summary>The visible control whose own text is exactly this - a button caption, a list row, a tab header.</summary>
    internal static UiNode? ByText(string text) =>
        Snapshot().FirstOrDefault(node =>
            string.Equals(node.Text, text, StringComparison.Ordinal) && node.HasArea);

    internal static UiNode? ByTextContaining(string fragment) =>
        Snapshot().FirstOrDefault(node =>
            node.Text.Contains(fragment, StringComparison.OrdinalIgnoreCase) && node.HasArea);

    /// <summary>
    /// The control to click for a caption: a control actually named that,
    /// or - failing that - the text that reads it, whose own hit test lands
    /// on whatever button or row carries it.
    /// </summary>
    internal static UiNode? Clickable(string caption) =>
        ByName(caption) ?? ByText(caption) ?? ByNameContaining(caption) ?? ByTextContaining(caption);

    /// <summary>Polls a condition on the UI thread until it holds or the budget runs out.</summary>
    internal static bool WaitUntil(Func<bool> condition, int timeoutMs = 20_000, int pollMs = 150)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (condition())
                    return true;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // The tree is mid-rebuild; try again on the next poll.
            }

            Thread.Sleep(pollMs);
        }

        return false;
    }

    internal static bool WaitForText(string fragment, int timeoutMs = 20_000) =>
        WaitUntil(() => ShowsText(fragment), timeoutMs);

    internal static bool WaitForControl(string caption, int timeoutMs = 20_000) =>
        WaitUntil(() => Clickable(caption) is not null, timeoutMs);

    /// <summary>
    /// The automation name of whatever currently holds keyboard focus -
    /// how the runner tells a click that landed from one that did not,
    /// before it types into the wrong field.
    /// </summary>
    internal static string FocusedName() =>
        OnUiThread(() =>
        {
            if (Lifetime?.MainWindow is not { } window)
                return string.Empty;

            var focused = TopLevel.GetTopLevel(window)?.FocusManager?.GetFocusedElement();

            // The element that actually takes focus is often a template
            // part - a NumericUpDown's inner TextBox, say - which carries
            // no automation name of its own, so the nearest named ancestor
            // is the honest answer to "what is the user typing into?".
            for (var visual = focused as Visual; visual is not null; visual = visual.GetVisualParent())
            {
                var name = visual.GetValue(AutomationProperties.NameProperty);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }

            return string.Empty;
        });

    /// <summary>The status bar's own segments, which is where this application reports what it just did.</summary>
    internal static string StatusLine()
    {
        var texts = VisibleText();
        return string.Join(" | ", texts.TakeLast(14));
    }
}
