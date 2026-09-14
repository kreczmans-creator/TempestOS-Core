namespace Tempest.Workspace.Viewing;

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

    /// <summary>
    /// A drawing format this platform stores but does not render — DWG or
    /// DXF today. Distinct from <see cref="Unsupported"/> so the viewer can
    /// say, honestly, that the file opens in its own application rather
    /// than that nothing can be done with it (`TD-99`, Product Owner
    /// decision 2026-09-15 §5).
    /// </summary>
    ExternalOnly,
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

    // DWG and DXF have no IANA-registered media type, so unlike PDF or PNG
    // there is no single string to check: these are the ones CAD tools and
    // browsers commonly send. The file extension (checked first, in
    // FromContentType) is the primary signal for either format; this list
    // only catches a correctly labelled upload sent with a generic or
    // missing file name.
    private static readonly HashSet<string> ExternalOnlyContentTypes = new(StringComparer.Ordinal)
    {
        "application/acad", "application/x-acad", "application/autocad_dwg",
        "image/x-dwg", "application/dwg", "application/x-dwg", "drawing/x-dwg", "image/vnd.dwg",
        "application/dxf", "application/x-dxf", "image/vnd.dxf", "drawing/x-dxf",
    };

    /// <summary>
    /// The format of <paramref name="content"/>, described as
    /// <paramref name="contentType"/> and, where known, named
    /// <paramref name="fileName"/>.
    /// </summary>
    public static ViewableDocumentFormat Detect(string? contentType, ReadOnlySpan<byte> content, string? fileName = null)
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

        return FromContentType(contentType, fileName);
    }

    /// <summary>
    /// The format <paramref name="contentType"/> claims, ignoring any
    /// content, refined by <paramref name="fileName"/>'s extension where
    /// the content type alone is ambiguous or absent.
    /// </summary>
    public static ViewableDocumentFormat FromContentType(string? contentType, string? fileName = null)
    {
        // The extension is checked first and unconditionally: DWG and DXF
        // have no reliable content type at all (see ExternalOnlyContentTypes
        // above), so a name-only signal has to be enough for them, exactly
        // as it already is for text (below), which has no magic bytes.
        var extension = string.IsNullOrWhiteSpace(fileName) ? null : Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is ".dwg" or ".dxf")
            return ViewableDocumentFormat.ExternalOnly;

        if (string.IsNullOrWhiteSpace(contentType))
            return ViewableDocumentFormat.Unsupported;

        var type = contentType.Trim().ToLowerInvariant();
        var separator = type.IndexOf(';');
        if (separator >= 0)
            type = type[..separator].Trim();

        if (type is "application/pdf" or "application/x-pdf")
            return ViewableDocumentFormat.Pdf;

        if (ExternalOnlyContentTypes.Contains(type))
            return ViewableDocumentFormat.ExternalOnly;

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
