using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Tempest.App.Workspace.Viewing;
using Tempest.Desktop.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Review board finding #4 (`WP 16.5A-R1`) — panning a zoomed document
/// previously worked only through <see cref="DocumentViewerView"/>'s own
/// pointer handlers (<c>OnPointerPressed</c>/<c>OnPointerMoved</c>/
/// <c>OnPointerReleased</c>), with no <c>KeyDown</c> handler anywhere in
/// the class: a zoomed document could not be panned without a mouse. This
/// mirrors <c>DigitalThreadGraphTests.ArrowKeys_PanTheView_EachInItsOwnDirection</c>'s
/// own established idiom exactly — raising a real <see cref="KeyEventArgs"/>
/// directly on the view (the same handler-attachment shape
/// <c>OnGraphKeyDown</c> uses: attached to the control itself, so it fires
/// regardless of which of the view's own descendants currently holds
/// focus) and reading the resulting, real <see cref="DocumentViewSession.Viewport"/>
/// state back — never a hand-simulated call directly into a private
/// handler method.
/// </summary>
public sealed class DocumentViewerKeyboardPanTests
{
    [AvaloniaFact]
    public void ArrowKeys_PanTheZoomedDocument_EachInItsOwnDirection()
    {
        var viewer = new DocumentViewerView();

        // A real `TextDocumentPageSource` page is always laid out onto
        // its own fixed, real page size (`TextDocumentPageSource.TextPageSize`,
        // 816x1056) regardless of the text content — a small, deliberately
        // narrow viewport beneath that is guaranteed genuinely scrollable
        // once zoomed to `ActualSize`.
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Text, "One short line."u8.ToArray())!;
        var session = DocumentViewSession.Ready(
            Guid.NewGuid(), "note.txt", "text/plain", ViewableDocumentFormat.Text,
            pageCount: 1,
            contentWidth: TextDocumentPageSource.TextPageSize.Width,
            contentHeight: TextDocumentPageSource.TextPageSize.Height,
            viewportWidth: 200,
            viewportHeight: 150);
        viewer.Open(session, source);

        viewer.ActualSize();
        Assert.True(viewer.Session!.Viewport.IsScrollable, "This test's own premise requires the document to be zoomed past its own viewport.");

        // Push fully into the bottom-right corner first (the pointer path
        // — `PanBy` — is already covered elsewhere; this just establishes
        // a known, deterministic starting corner), so every arrow
        // direction below is guaranteed real room to move, regardless of
        // exactly where `ActualSize` itself happened to centre the view.
        viewer.PanBy(100_000, 100_000);
        var corner = viewer.Session!.Viewport;
        Assert.True(corner.OffsetX > 0, "This test's own premise requires real horizontal scroll room.");
        Assert.True(corner.OffsetY > 0, "This test's own premise requires real vertical scroll room.");

        viewer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left });
        Assert.True(viewer.Session!.Viewport.OffsetX < corner.OffsetX, "Left did not reveal content further left.");
        Assert.Equal(corner.OffsetY, viewer.Session!.Viewport.OffsetY);

        viewer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up });
        Assert.True(viewer.Session!.Viewport.OffsetY < corner.OffsetY, "Up did not reveal content further up.");

        var afterLeftUp = viewer.Session!.Viewport;

        viewer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
        Assert.True(viewer.Session!.Viewport.OffsetX > afterLeftUp.OffsetX, "Right did not reveal content further right.");

        viewer.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
        Assert.True(viewer.Session!.Viewport.OffsetY > afterLeftUp.OffsetY, "Down did not reveal content further down.");
    }
}
