using System.Globalization;

namespace Tempest.Desktop.RealShell;

/// <summary>
/// The four things a person does - point at something, click it, type into
/// it, pick from a drop-down - expressed against what the application is
/// actually showing. Every one of them ends in <see cref="OsInput"/>: the
/// visual tree is read to decide *where* to click and to check the result,
/// never to perform the action.
/// </summary>
internal static class Act
{
    /// <summary>
    /// How far inside the window's own bottom edge a click target must sit
    /// before it is safe to click: the status bar sits across the last
    /// ~26 px and would swallow the press. The top needs no such room -
    /// the header is a real, clickable surface.
    /// </summary>
    private const int BottomMargin = 30;

    private const int TopMargin = 6;

    internal static string LastProblem { get; private set; } = string.Empty;

    /// <summary>The application's own main window, in screen coordinates.</summary>
    internal static (int Left, int Top, int Right, int Bottom) WindowRect() =>
        Ui.OnUiThread(() =>
        {
            if (Ui.Lifetime?.MainWindow is not { } window)
                return (0, 0, 1600, 1000);

            var origin = window.Position;
            return (origin.X, origin.Y, origin.X + (int)window.Bounds.Width, origin.Y + (int)window.Bounds.Height);
        });

    /// <summary>
    /// The point on this control that a click would actually reach, or
    /// <see langword="null"/> when none would.
    ///
    /// <para>
    /// A control's own bounds are not enough to know that. A control can be
    /// clipped away by the scroll viewport it sits in, covered by an
    /// overlay, or scrolled under the shell header, and still report its
    /// full rectangle - clicking its arithmetic centre then lands on
    /// whatever is really drawn there. The first run of this journey typed
    /// a span into the field above the one it meant for exactly that
    /// reason. So every candidate point is hit-tested against the live
    /// tree first, and the click is only sent to a point the control
    /// itself would receive.
    /// </para>
    /// </summary>
    private static (int X, int Y)? ClickPoint(UiNode node)
    {
        if (!node.HasArea)
            return null;

        var (left, top, right, bottom) = WindowRect();

        var xs = new[] { node.CentreX, node.Left + Math.Max(4, node.Width / 4), node.Left + ((node.Width * 3) / 4) };
        var ys = new[] { node.CentreY, node.Top + Math.Max(4, node.Height / 4), node.Top + ((node.Height * 3) / 4) };

        foreach (var y in ys.Distinct())
        {
            foreach (var x in xs.Distinct())
            {
                if (x <= left + 2 || x >= right - 2 || y <= top + TopMargin || y >= bottom - BottomMargin)
                    continue;

                if (Ui.HitTestIds(x, y).Contains(node.Id))
                    return (x, y);
            }
        }

        return null;
    }

    private static bool IsClickable(UiNode node) => ClickPoint(node) is not null;

    /// <summary>
    /// Resolves a control and waits for it to stop moving: a scroll that is
    /// still settling would otherwise have the click land where the control
    /// was, not where it is.
    /// </summary>
    private static UiNode? Settled(string caption, int timeoutMs = 4_000)
    {
        var node = BringIntoView(caption);
        if (node is null)
            return null;

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(160);
            var again = Ui.Clickable(caption);
            if (again is null)
                return null;

            if (again.Left == node.Left && again.Top == node.Top)
                return IsClickable(again) ? again : BringIntoView(caption);

            node = again;
        }

        return node;
    }

    /// <summary>
    /// Scrolls whatever the control is inside until it is comfortably
    /// within the window, with the real mouse wheel. The direction is
    /// re-decided after every step from where the control actually moved,
    /// so it works without knowing which scroll viewer owns it.
    /// </summary>
    internal static UiNode? BringIntoView(string caption, int timeoutMs = 25_000)
    {
        var node = Ui.Clickable(caption);
        if (node is null)
            return null;

        if (IsClickable(node))
            return node;

        var (left, top, right, bottom) = WindowRect();
        var pointerX = Math.Clamp(node.CentreX, left + 40, right - 40);
        var pointerY = (top + bottom) / 2;
        var previousDistance = int.MaxValue;
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            var distance = Distance(node, top, bottom);
            var button = node.CentreY > bottom - BottomMargin ? 5 : 4;

            // A long list is thousands of pixels deep; one notch at a time
            // would spend the whole budget scrolling. Notches are ~50 px.
            OsInput.Scroll(pointerX, pointerY, button, Math.Max(1, distance / 50));
            Thread.Sleep(180);

            node = Ui.Clickable(caption);
            if (node is null)
                return null;

            if (IsClickable(node))
                return node;

            var moved = Distance(node, top, bottom);
            if (moved >= previousDistance)
            {
                // This container reads the wheel the other way round (or is
                // not the one that scrolls); nudge it back the other way.
                OsInput.Scroll(pointerX, pointerY, button == 5 ? 4 : 5, Math.Max(1, moved / 50));
                Thread.Sleep(180);

                node = Ui.Clickable(caption);
                if (node is null)
                    return null;

                if (IsClickable(node))
                    return node;
            }

            previousDistance = moved;
        }

        return IsClickable(node) ? node : null;
    }

    private static int Distance(UiNode node, int top, int bottom) =>
        node.CentreY < top + TopMargin
            ? (top + TopMargin) - node.CentreY
            : Math.Max(0, node.CentreY - (bottom - BottomMargin));

    /// <summary>Clicks a caption - an automation name, a button's text, a row - as a real mouse click, scrolling to it first if it is off-screen.</summary>
    internal static bool Click(string caption, int settleMs = 700)
    {
        var node = Settled(caption);
        if (node is null)
        {
            LastProblem = $"nothing clickable is called '{caption}'";
            return false;
        }

        if (ClickPoint(node) is not { } point)
        {
            LastProblem = $"'{caption}' is on screen but nothing would reach it at any point on it";
            return false;
        }

        OsInput.Click(point.X, point.Y);
        Thread.Sleep(settleMs);
        return true;
    }

    /// <summary>Clicks a tree item by its own row rather than its centre - a tree item's bounds cover its expanded children too.</summary>
    internal static bool ClickRow(string caption, int settleMs = 700)
    {
        var node = Settled(caption);
        if (node is null)
        {
            LastProblem = $"no tree row is called '{caption}'";
            return false;
        }

        var rowY = node.Top + 13;
        if (!Ui.HitTestIds(node.CentreX, rowY).Contains(node.Id))
        {
            if (ClickPoint(node) is not { } fallback)
            {
                LastProblem = $"'{caption}' is on screen but nothing would reach its own row";
                return false;
            }

            OsInput.Click(fallback.X, fallback.Y);
            Thread.Sleep(settleMs);
            return true;
        }

        OsInput.Click(node.CentreX, rowY);
        Thread.Sleep(settleMs);
        return true;
    }

    /// <summary>
    /// Types into a named field the way a person retypes a value: click it,
    /// select all, delete, type. The value is then read back, and one retry
    /// is allowed - a click that lands while the surface is still laying
    /// out leaves the caret where it was, and the honest fix is to do it
    /// again rather than to write the value in from code.
    /// </summary>
    internal static bool TypeInto(string fieldName, string value)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var node = Settled(fieldName);
            if (node is null)
            {
                LastProblem = $"no field is called '{fieldName}'";
                return false;
            }

            if (ClickPoint(node) is not { } point)
            {
                LastProblem = $"'{fieldName}' is on screen but nothing would reach it";
                Thread.Sleep(250);
                continue;
            }

            OsInput.Click(point.X, point.Y);

            // The click has to have actually moved keyboard focus before a
            // single character is typed: typing into a field that never took
            // focus silently overwrites the one that still has it. This is
            // the whole reason the runner reads the focused element at all.
            if (!Ui.WaitUntil(() => string.Equals(Ui.FocusedName(), fieldName, StringComparison.Ordinal), 2_500, 100))
            {
                LastProblem = $"clicking '{fieldName}' left focus on '{Ui.FocusedName()}'";
                Thread.Sleep(250);
                continue;
            }

            OsInput.ClearAndType(value);
            Thread.Sleep(320);

            var readBack = Ui.ByName(fieldName)?.Text ?? string.Empty;
            if (string.Equals(readBack.Trim(), value, StringComparison.Ordinal))
                return true;

            LastProblem = $"'{fieldName}' reads '{readBack}' after typing '{value}'";
        }

        return false;
    }

    /// <summary>Picks an item out of a named drop-down: click it open, then click the item in its own popup.</summary>
    internal static bool Choose(string comboName, string itemFragment)
    {
        if (!Click(comboName, settleMs: 500))
            return false;

        if (!Ui.WaitUntil(() => Ui.ByTextContaining(itemFragment) is { HasArea: true }, 6_000))
        {
            LastProblem = $"'{comboName}' offers nothing reading '{itemFragment}'";
            OsInput.Key("Escape");
            return false;
        }

        var item = Ui.ByTextContaining(itemFragment)!;
        var target = ClickPoint(item) ?? (item.CentreX, item.CentreY);
        OsInput.Click(target.Item1, target.Item2);
        Thread.Sleep(700);
        return true;
    }

    /// <summary>Confirms a confirmation dialog through its own Continue button.</summary>
    internal static bool Confirm(string expectedFragment)
    {
        if (!Ui.WaitForText(expectedFragment, 8_000))
        {
            LastProblem = $"no confirmation reading '{expectedFragment}' appeared";
            return false;
        }

        return Click("Continue", settleMs: 1_200);
    }

    /// <summary>What the status bar's own "Selected object" segment - the application's own report of what it just did - now reads.</summary>
    internal static string Status() => Ui.ByName("Selected object")?.Text ?? string.Empty;

    internal static (Verdict Verdict, string Observed) Verified(string observed) => (Verdict.Verified, observed);

    internal static (Verdict Verdict, string Observed) Unknown(string observed) => (Verdict.Unknown, observed);

    internal static (Verdict Verdict, string Observed) Failed(string observed) => (Verdict.Failed, observed);

    internal static string Describe(params string[] fragments)
    {
        var found = fragments
            .Select(fragment => Ui.FirstTextContaining(fragment))
            .Where(text => !string.IsNullOrEmpty(text))
            .Select(text => text!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return found.Count == 0 ? "nothing on screen matched" : string.Join(" / ", found);
    }

    internal static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
