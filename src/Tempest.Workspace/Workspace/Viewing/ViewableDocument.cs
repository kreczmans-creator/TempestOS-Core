namespace Tempest.App.Workspace.Viewing;

/// <summary>
/// What kind of document the viewer is being asked to show (`TD-80`).
/// </summary>
public enum ViewableDocumentFormat
{
    /// <summary>Nothing here this platform knows how to render.</summary>
    Unsupported,

    /// <summary>A PDF — the document and drawing format the workflow's own classifications assume.</summary>
    Pdf,

    /// <summary>A raster image: PNG, JPEG, BMP, GIF or WebP.</summary>
    Image,

    /// <summary>Text: plain text, CSV, Markdown, XML, JSON or similar.</summary>
    Text,
}

/// <summary>
/// Decides a document's format from what it actually is, not only from
/// what it was labelled (`TD-80`).
/// </summary>
/// <remarks>
/// <para>
/// Magic bytes are consulted first and the declared content type second.
/// A content type is a claim made by whoever attached the file; the first
/// few bytes are the file. When a <c>.pdf</c> is really a PNG — which
/// happens whenever someone renames a file rather than converting it —
/// the viewer that trusted the label shows an error over a picture it
/// could have rendered.
/// </para>
/// <para>
/// The declared type is still used, and matters: it is the only signal for
/// text, which has no magic bytes to speak of, and it disambiguates
/// formats this platform does not sniff.
/// </para>
/// </remarks>
public static class DocumentFormatDetector
{
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] GifSignature = "GIF8"u8.ToArray();
    private static readonly byte[] BmpSignature = "BM"u8.ToArray();
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebpSignature = "WEBP"u8.ToArray();

    /// <summary>
    /// The format of <paramref name="content"/>, described as
    /// <paramref name="contentType"/>.
    /// </summary>
    public static ViewableDocumentFormat Detect(string? contentType, ReadOnlySpan<byte> content)
    {
        if (StartsWith(content, PdfSignature))
            return ViewableDocumentFormat.Pdf;

        if (StartsWith(content, PngSignature) ||
            StartsWith(content, JpegSignature) ||
            StartsWith(content, GifSignature) ||
            StartsWith(content, BmpSignature) ||
            (StartsWith(content, RiffSignature) && content.Length >= 12 && StartsWith(content[8..], WebpSignature)))
        {
            return ViewableDocumentFormat.Image;
        }

        return FromContentType(contentType);
    }

    /// <summary>The format <paramref name="contentType"/> claims, ignoring any content.</summary>
    public static ViewableDocumentFormat FromContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return ViewableDocumentFormat.Unsupported;

        var type = contentType.Trim().ToLowerInvariant();
        var separator = type.IndexOf(';');
        if (separator >= 0)
            type = type[..separator].Trim();

        if (type is "application/pdf" or "application/x-pdf")
            return ViewableDocumentFormat.Pdf;

        if (type.StartsWith("image/", StringComparison.Ordinal))
        {
            // Named rather than assumed: SVG and TIFF are both "image/*"
            // and neither is a raster this platform can decode, so calling
            // them Image would promise a render that cannot happen.
            return type is "image/png" or "image/jpeg" or "image/jpg" or "image/bmp" or "image/gif" or "image/webp"
                ? ViewableDocumentFormat.Image
                : ViewableDocumentFormat.Unsupported;
        }

        if (type.StartsWith("text/", StringComparison.Ordinal))
            return ViewableDocumentFormat.Text;

        return type is "application/json" or "application/xml" or "application/csv"
            ? ViewableDocumentFormat.Text
            : ViewableDocumentFormat.Unsupported;
    }

    private static bool StartsWith(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature) =>
        content.Length >= signature.Length && content[..signature.Length].SequenceEqual(signature);
}
