using System.Diagnostics;
using System.Text;
using Avalonia.Headless.XUnit;
using Tempest.Desktop.Viewing;
using Tempest.Workspace.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// DB-08 review: the SVG reader against the hostile-input classes the review measured (`odp-review-final.md`, F1 to F11, I1), each
/// scaled to a document under 1 MB that must build in under two seconds (scaled by <see cref="DesktopTestHelpers.TimeoutFactor"/>)
/// once the fix is in. The full-size probes, which took seconds to minutes or killed the process before the fixes, live in
/// <see cref="SvgBoundedProbeTests"/> and run one per process under memory and time limits when CI asks for them.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SvgHostileInputTests
{
    private const string Ns = "xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"";

    /// <summary>The time a scaled-down hostile document may take: two seconds, scaled for a loaded runner.</summary>
    private static TimeSpan Budget => TimeSpan.FromSeconds(2 * DesktopTestHelpers.TimeoutFactor);

    private static string Svg(string body, string size = "width=\"100\" height=\"100\"") => $"<svg {Ns} {size}>{body}</svg>";

    /// <summary>Builds <paramref name="markup"/> and returns the picture with the time it took, failing when that is over <see cref="Budget"/>.</summary>
    private static SvgPictureBuilder.SvgPicture BuildWithin(string markup, string what)
    {
        Assert.True(markup.Length < 1024 * 1024, "a hostile test input stays under 1 MB");
        var watch = Stopwatch.StartNew();
        var picture = SvgPictureBuilder.Build(markup);
        watch.Stop();
        Assert.True(watch.Elapsed < Budget, $"{what}: building took {watch.Elapsed.TotalSeconds:F2} s (limit {Budget.TotalSeconds:F0} s).");
        return picture;
    }

    // ------------------------------------------------------------------
    // F1: regular expressions — linear, and never on the UI thread for long
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void An_Rgb_Colour_With_A_Long_Run_Of_Spaces_Is_Read_In_Linear_Time()
    {
        // Before: `\s*[,\s]\s*` on the backtracking engine tried the run in quadratically many ways (40 000 spaces: 4.3 s; 60 000: ~10 s).
        using var picture = BuildWithin(Svg("<rect width=\"10\" height=\"10\" fill=\"rgb(1" + new string(' ', 60_000) + "x)\"/>"), "rgb() with 60 000 spaces");
        Assert.Equal(new SkiaSharp.SKSize(100, 100), picture.Size);
    }

    [AvaloniaFact]
    public void A_Style_Sheet_Of_Unclosed_Comment_Openers_Is_Read_In_Linear_Time()
    {
        // Before: `/\*.*?\*/` rescanned to the end of the sheet from every opener (100 000 openers: 1.5 s; 200 000: ~6 s).
        using var picture = BuildWithin(Svg("<style>" + string.Concat(Enumerable.Repeat("/*a", 200_000)) + "</style><rect width=\"10\" height=\"10\"/>"), "200 000 comment openers");
        Assert.Equal(new SkiaSharp.SKSize(100, 100), picture.Size);
    }

    [AvaloniaFact]
    public void Fifty_Thousand_Script_Openers_Pass_The_Sanitiser_In_Linear_Time()
    {
        // Before: `<script\b[^>]*>.*?</script\s*>` rescanned to the end from every opener (the review's `script-50k-openers`, "tens of seconds").
        var markup = Svg(string.Concat(Enumerable.Repeat("<script>", 50_000)), "width=\"10\" height=\"10\"");
        Assert.True(markup.Length < 1024 * 1024);

        var watch = Stopwatch.StartNew();
        var sanitised = SvgMarkupSanitiser.Sanitise(markup);
        watch.Stop();

        // Nothing closes the elements, so nothing is removed; the XML reader then refuses the file (50 000 levels deep: see the nesting test).
        Assert.Equal(markup, sanitised);
        Assert.True(watch.Elapsed < Budget, $"the sanitiser took {watch.Elapsed.TotalSeconds:F2} s.");
    }

    [AvaloniaFact]
    public void A_Colour_Longer_Than_64_Characters_Is_Not_A_Colour()
    {
        // Before: the pattern accepted any amount of space inside the brackets, so this was rgb(1, 2, 3) after a long match.
        var padded = "rgb(1, 2, 3" + new string(' ', 70) + ")";
        Assert.True(padded.Length > SvgPictureBuilder.MaxColourLength);
        Assert.Null(SvgPictureBuilder.ParseColour(padded, SkiaSharp.SKColors.Black));

        // The longest honest colour is well within the limit.
        Assert.Equal(new SkiaSharp.SKColor(255, 255, 255, 255), SvgPictureBuilder.ParseColour("rgba(100%, 100%, 100%, 100%)", SkiaSharp.SKColors.Black));
    }

    // ------------------------------------------------------------------
    // F10: a deadline, honoured by the reader and applied by the viewer
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Reader_Stops_At_A_Cancelled_Token_Before_Drawing_An_Element()
    {
        var markup = Svg(string.Concat(Enumerable.Repeat("<rect width=\"1\" height=\"1\"/>", 1_000)));
        Assert.Throws<OperationCanceledException>(() => SvgPictureBuilder.Build(markup, new CancellationToken(canceled: true)));

        // The page source lets the caller's own cancellation through as what it is, not as "could not be read".
        Assert.ThrowsAny<OperationCanceledException>(() => DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(markup), new CancellationToken(canceled: true)));

        // And without one, the same document opens.
        using var picture = SvgPictureBuilder.Build(markup, CancellationToken.None);
        Assert.Equal(new SkiaSharp.SKSize(100, 100), picture.Size);
    }

    [AvaloniaFact]
    public async Task A_Document_That_Takes_Too_Long_To_Read_Is_Reported_Rather_Than_Freezing_The_Window()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var (documentId, attachmentId) = await DocumentViewerAcceptanceTests.CreateDocumentWithAttachmentAsync(
                host, "DWG-SLOW", "slow.svg", "image/svg+xml", DocumentPageSourceTests.RedRectangleSvg());
            var (owner, attachment) = await DocumentViewerAcceptanceTests.ResolveAsync(host, documentId, attachmentId);

            var window = new MainWindow(host);
            Assert.Equal(TimeSpan.FromSeconds(15), AttachmentViewerLauncher.DefaultReadDeadline);

            // An immediate deadline stands in for a document that keeps the reader busy for fifteen seconds.
            window.AttachmentViewers.ReadDeadline = TimeSpan.Zero;
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            var session = viewer.Session!;
            Assert.Equal(DocumentViewStatus.Corrupt, session.Status);
            Assert.Equal(ViewableDocumentFormat.Svg, session.Format);
            Assert.Contains("took too long to read", session.UnavailableDetail, StringComparison.Ordinal);
            Assert.True(viewer.IsShowingUnavailableState);
            Assert.Equal("This attachment could not be read", viewer.UnavailableHeadline);

            // With the real deadline the same attachment opens in a second tab-free re-open.
            window.AttachmentViewers.Close(attachment.Id);
            window.AttachmentViewers.ReadDeadline = AttachmentViewerLauncher.DefaultReadDeadline;
            var reopened = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);
            Assert.Equal(DocumentViewStatus.Ready, reopened.Session!.Status);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
