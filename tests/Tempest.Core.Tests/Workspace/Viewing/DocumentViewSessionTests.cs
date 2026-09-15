using Tempest.Workspace.Viewing;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.Workspace.Viewing;

/// <summary>
/// One open document's behaviour (`TD-80`): page navigation, the three
/// ways an open can fail, and what each of them does to the controls.
/// </summary>
public class DocumentViewSessionTests
{
    private static DocumentViewSession Ready(int pageCount = 1) =>
        DocumentViewSession.Ready(
            Guid.NewGuid(), "drawing.pdf", "application/pdf", ViewableDocumentFormat.Pdf,
            pageCount, 595, 842, 800, 600);

    [Fact]
    public void ANewSession_OpensOnPageOne_Fitted()
    {
        var session = Ready(12);

        Assert.True(session.IsReady);
        Assert.Equal(1, session.CurrentPage);
        Assert.Equal(12, session.PageCount);
        Assert.True(session.Viewport.IsFitted);
    }

    [Fact]
    public void PageNumbers_AreOneBased_AsTheUserReadsThem()
    {
        var session = Ready(3).NextPage();

        Assert.Equal(2, session.CurrentPage);
        Assert.True(session.CanGoToPreviousPage);
        Assert.True(session.CanGoToNextPage);
    }

    [Fact]
    public void PageNavigation_StopsAtBothEnds_RatherThanWrappingOrThrowing()
    {
        // A held-down page key is not an error.
        var session = Ready(3);

        Assert.Equal(1, session.PreviousPage().PreviousPage().CurrentPage);
        Assert.Equal(3, session.NextPage().NextPage().NextPage().NextPage().CurrentPage);
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(2, 2)]
    [InlineData(99, 3)]
    public void GoToPage_ClampsIntoTheDocument(int requested, int expected)
    {
        Assert.Equal(expected, Ready(3).GoToPage(requested).CurrentPage);
    }

    [Fact]
    public void ASinglePageDocument_OffersNoPageNavigation()
    {
        var session = Ready(1);

        Assert.False(session.IsMultiPage);
        Assert.False(session.CanGoToNextPage);
        Assert.False(session.CanGoToPreviousPage);
    }

    [Fact]
    public void TurningAPage_RefitsTheView()
    {
        // A new page is a new thing to look at; keeping the previous
        // page's zoom and offset would open it part-way down and enlarged.
        var session = Ready(5).WithViewport(Ready(5).Viewport.ZoomTo(6.0));
        Assert.False(session.Viewport.IsFitted);

        var turned = session.NextPage();

        Assert.True(turned.Viewport.IsFitted);
    }

    [Fact]
    public void TurningToThePageAlreadyShowing_ChangesNothing()
    {
        var session = Ready(5).WithViewport(Ready(5).Viewport.ZoomTo(6.0));

        var same = session.GoToPage(1);

        Assert.Same(session, same);
        Assert.Equal(6.0, same.Viewport.Zoom, 0.0001);
    }

    [Theory]
    [InlineData(AttachmentContentStatus.Available, DocumentViewStatus.Ready)]
    [InlineData(AttachmentContentStatus.Missing, DocumentViewStatus.Missing)]
    [InlineData(AttachmentContentStatus.Corrupt, DocumentViewStatus.Corrupt)]
    public void ContentStatus_MapsToViewStatus_WithoutCollapsingMissingIntoCorrupt(
        AttachmentContentStatus content, DocumentViewStatus expected)
    {
        // The distinction `TD-31` was careful to preserve has to survive
        // the trip into the viewer, or the user is told their file is
        // damaged when it was simply never stored.
        Assert.Equal(expected, DocumentViewSession.StatusFor(content));
    }

    [Theory]
    [InlineData(DocumentViewStatus.Missing)]
    [InlineData(DocumentViewStatus.Corrupt)]
    [InlineData(DocumentViewStatus.Unsupported)]
    public void AnUnavailableSession_IsStillATabWithAName(DocumentViewStatus status)
    {
        // A failed open is something the user can see and close, not a
        // dialog that appears and leaves nothing behind.
        var session = DocumentViewSession.Unavailable(Guid.NewGuid(), "missing.pdf", "application/pdf", status);

        Assert.False(session.IsReady);
        Assert.Equal(status, session.Status);
        Assert.Equal("missing.pdf", session.FileName);
    }

    [Fact]
    public void AnUnavailableSession_IgnoresNavigationAndZoom()
    {
        var session = DocumentViewSession.Unavailable(Guid.NewGuid(), "gone.pdf", "application/pdf", DocumentViewStatus.Missing);

        Assert.Same(session, session.NextPage());
        Assert.Same(session, session.GoToPage(4));
        Assert.Same(session, session.WithViewport(session.Viewport.ZoomIn()));
        Assert.False(session.CanGoToNextPage);
    }

    [Fact]
    public void AReadyStatus_CannotBeCreatedThroughTheUnavailablePath()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DocumentViewSession.Unavailable(Guid.NewGuid(), "x.pdf", "application/pdf", DocumentViewStatus.Ready));
    }

    [Fact]
    public void AnUnsupportedSession_CarriesTheMaterialisedPath_ForOpenExternally()
    {
        var session = DocumentViewSession.Unavailable(
            Guid.NewGuid(), "part.dwg", "application/acad", DocumentViewStatus.Unsupported,
            ViewableDocumentFormat.ExternalOnly, materialisedPath: @"C:\temp\part.dwg");

        Assert.Equal(@"C:\temp\part.dwg", session.MaterialisedPath);
    }

    [Theory]
    [InlineData(DocumentViewStatus.Missing)]
    [InlineData(DocumentViewStatus.Corrupt)]
    public void AMissingOrCorruptSession_NeverCarriesAMaterialisedPath_EvenIfOneWasPassed(DocumentViewStatus status)
    {
        // There are no bytes to materialise for either status — nothing was
        // ever stored, or what was stored failed its own integrity check —
        // so a path passed in regardless (a caller's mistake) is dropped
        // rather than handed to a button that would open the wrong thing.
        var session = DocumentViewSession.Unavailable(
            Guid.NewGuid(), "x.pdf", "application/pdf", status,
            materialisedPath: @"C:\temp\should-not-appear.pdf");

        Assert.Null(session.MaterialisedPath);
    }

    [Fact]
    public void APageCountBelowOne_IsTreatedAsASinglePage()
    {
        var session = DocumentViewSession.Ready(
            Guid.NewGuid(), "odd.pdf", "application/pdf", ViewableDocumentFormat.Pdf, 0, 100, 100, 400, 400);

        Assert.Equal(1, session.PageCount);
        Assert.Equal(1, session.CurrentPage);
    }

    [Fact]
    public void EveryOperation_LeavesTheOriginalSessionUntouched()
    {
        var session = Ready(5);

        session.NextPage();
        session.WithViewport(session.Viewport.ZoomIn());

        Assert.Equal(1, session.CurrentPage);
        Assert.True(session.Viewport.IsFitted);
    }
}

/// <summary>
/// How the viewer decides what a file is (`TD-80`).
/// </summary>
public class DocumentFormatDetectorTests
{
    [Fact]
    public void APdfIsRecognisedByItsSignature()
    {
        Assert.Equal(ViewableDocumentFormat.Pdf, DocumentFormatDetector.Detect("application/pdf", "%PDF-1.7\n"u8));
    }

    [Fact]
    public void AnImageIsRecognisedByItsSignature()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0];

        Assert.Equal(ViewableDocumentFormat.Image, DocumentFormatDetector.Detect("image/png", png));
        Assert.Equal(ViewableDocumentFormat.Image, DocumentFormatDetector.Detect("image/jpeg", jpeg));
    }

    [Fact]
    public void TheBytesBeatTheLabel_WhenTheyDisagree()
    {
        // Renaming a file rather than converting it is a thing people do,
        // and a viewer that trusts the extension shows an error over a
        // picture it could have rendered.
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];

        Assert.Equal(ViewableDocumentFormat.Image, DocumentFormatDetector.Detect("application/pdf", png));
        Assert.Equal(ViewableDocumentFormat.Pdf, DocumentFormatDetector.Detect("image/png", "%PDF-1.4"u8));
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/csv")]
    [InlineData("text/markdown")]
    [InlineData("application/json")]
    [InlineData("application/xml")]
    [InlineData("text/csv; charset=utf-8")]
    public void TextIsRecognisedByItsContentType_HavingNoSignatureToSpeakOf(string contentType)
    {
        Assert.Equal(ViewableDocumentFormat.Text, DocumentFormatDetector.Detect(contentType, "Property,Value\n"u8));
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("image/tiff")]
    public void AnImageTypeThisPlatformCannotDecode_IsUnsupported_NotOptimisticallyImage(string contentType)
    {
        // Claiming Image for a format the decoder will reject promises a
        // render that cannot happen, and turns "we have no viewer for
        // this" into "this file is broken".
        Assert.Equal(ViewableDocumentFormat.Unsupported, DocumentFormatDetector.Detect(contentType, "<svg/>"u8));
    }

    [Theory]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseIsUnsupported(string? contentType)
    {
        Assert.Equal(ViewableDocumentFormat.Unsupported, DocumentFormatDetector.Detect(contentType, [0x50, 0x4B, 0x03, 0x04]));
    }

    [Fact]
    public void AnEmptyFile_DoesNotCrashTheDetector()
    {
        Assert.Equal(ViewableDocumentFormat.Unsupported, DocumentFormatDetector.Detect(null, []));
        Assert.Equal(ViewableDocumentFormat.Text, DocumentFormatDetector.Detect("text/plain", []));
    }

    [Theory]
    [InlineData("drawing.dwg")]
    [InlineData("drawing.dxf")]
    [InlineData("DRAWING.DWG")]
    [InlineData("Pump Housing.Dxf")]
    public void ADwgOrDxfFileName_IsExternalOnly_EvenWithNoUsefulContentType(string fileName)
    {
        // Neither format has an IANA-registered content type, so a real
        // upload commonly arrives as "application/octet-stream" or with no
        // content type claimed at all. The extension has to be enough on
        // its own, and case must not matter — a Windows-authored file name
        // is exactly as likely to be "DRAWING.DWG" as "drawing.dwg".
        Assert.Equal(
            ViewableDocumentFormat.ExternalOnly,
            DocumentFormatDetector.Detect("application/octet-stream", [0x00, 0x01, 0x02], fileName));
        Assert.Equal(
            ViewableDocumentFormat.ExternalOnly,
            DocumentFormatDetector.FromContentType(null, fileName));
    }

    [Theory]
    [InlineData("application/acad")]
    [InlineData("image/vnd.dwg")]
    [InlineData("application/dxf")]
    public void ADwgOrDxfContentType_IsExternalOnly_WithNoFileNameToGoBy(string contentType)
    {
        // The extension is the primary signal (above); this is the
        // fallback for a correctly labelled upload whose name was typed
        // without one.
        Assert.Equal(ViewableDocumentFormat.ExternalOnly, DocumentFormatDetector.FromContentType(contentType));
    }

    [Fact]
    public void ADwgFileName_BeatsAContentTypeThatWouldOtherwiseSayText()
    {
        // The extension is checked before content type is even parsed, so
        // a DWG mislabelled as text/plain — which happens, since many
        // upload paths default to it for anything with no better guess —
        // still opens externally rather than trying (and failing) to lay
        // it out as a datasheet.
        Assert.Equal(ViewableDocumentFormat.ExternalOnly, DocumentFormatDetector.FromContentType("text/plain", "part.dwg"));
    }

    [Fact]
    public void AnSvgIsStillUnsupported_NoRendererIsReferencedInThisBuild()
    {
        // TD-99's SVG half was stopped, not shipped: no SVG rasteriser
        // (Svg.Skia, SkiaSharp.Extended or Avalonia.Svg.Skia) is referenced
        // anywhere in this solution, and the brief's own fallback for that
        // case was to report and stop rather than add one. SVG stays
        // exactly the Unsupported format it already was.
        Assert.Equal(ViewableDocumentFormat.Unsupported, DocumentFormatDetector.Detect("image/svg+xml", "<svg/>"u8, "icon.svg"));
    }
}
