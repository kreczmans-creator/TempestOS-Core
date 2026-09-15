using System.Globalization;
using System.IO.Compression;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Tempest.Core.EngineeringDomain;
using Tempest.Workspace.Viewing;

namespace Tempest.Desktop.Viewing;

/// <summary>
/// Burns a page's own annotations into a copy of its rendered bitmap, and
/// encodes that copy as a PNG or a single-page PDF (`TD-98`, "Save
/// annotated copy…") — the original attachment is never opened for
/// writing by anything here.
/// </summary>
/// <remarks>
/// The geometry drawn here is the same transform the on-screen overlay
/// uses (<see cref="AnnotationGeometry"/>), at the same scale the page was
/// actually rasterised at, so the exported copy matches what the user was
/// looking at pixel for pixel.
/// </remarks>
internal static class AnnotatedCopyExporter
{
    private const double StrokeThickness = 2.0;
    private const double ArrowHeadLength = 12.0;
    private const double ArrowHeadAngle = Math.PI / 7;

    /// <summary>
    /// A new bitmap the same size as <paramref name="page"/>, with
    /// <paramref name="annotations"/> drawn on top at <paramref name="zoom"/>
    /// and <paramref name="rotationDegrees"/> — the caller disposes it.
    /// </summary>
    public static RenderTargetBitmap ComposeAnnotatedPage(
        Bitmap page,
        IReadOnlyList<AttachmentAnnotation> annotations,
        double nativeWidth,
        double nativeHeight,
        int rotationDegrees,
        double zoom)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(annotations);

        var pixelSize = page.PixelSize;
        var target = new RenderTargetBitmap(pixelSize, new Vector(96, 96));

        using (var context = target.CreateDrawingContext())
        {
            context.DrawImage(page, new Rect(0, 0, pixelSize.Width, pixelSize.Height));

            foreach (var annotation in annotations)
                DrawAnnotation(context, annotation, nativeWidth, nativeHeight, rotationDegrees, zoom);
        }

        return target;
    }

    /// <summary>PNG-encodes <paramref name="composed"/> — the export format for every non-PDF source.</summary>
    public static byte[] EncodePng(Bitmap composed)
    {
        ArgumentNullException.ThrowIfNull(composed);

        using var stream = new MemoryStream();
        composed.Save(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Wraps <paramref name="composed"/> in a minimal, single-page,
    /// image-only PDF — the export format for a PDF source. No PDF-writing
    /// package is referenced anywhere in this solution (this WP's own
    /// files-owned list names no new dependency for it), so the file is
    /// built by hand: one raster-image XObject, FlateDecode-compressed raw
    /// RGB, drawn across one page whose own MediaBox is the image's own
    /// pixel size. Small, but a real, independently openable PDF — proven
    /// by <c>AnnotatedCopyExporterTests</c> feeding the bytes straight back
    /// through <see cref="Tempest.Desktop.Viewing.PdfDocumentPageSource"/>.
    /// </summary>
    public static byte[] EncodePdf(Bitmap composed)
    {
        ArgumentNullException.ThrowIfNull(composed);

        var (width, height, rgb) = ToRgbPixels(composed);
        return MinimalPdfWriter.WriteSinglePageImagePdf(width, height, rgb);
    }

    /// <summary>Decodes <paramref name="bitmap"/>'s own PNG encoding back into tightly-packed 8-bit RGB rows, top row first — what <see cref="MinimalPdfWriter"/> embeds.</summary>
    private static (int Width, int Height, byte[] Rgb) ToRgbPixels(Bitmap bitmap)
    {
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream);
        pngStream.Position = 0;

        using var decoded = SKBitmap.Decode(pngStream);
        using var normalised = new SKBitmap(new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));

        using (var canvas = new SKCanvas(normalised))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(decoded, 0, 0);
        }

        var rgba = normalised.Bytes;
        var rgb = new byte[normalised.Width * normalised.Height * 3];

        for (int src = 0, dst = 0; src < rgba.Length; src += 4, dst += 3)
        {
            rgb[dst] = rgba[src];
            rgb[dst + 1] = rgba[src + 1];
            rgb[dst + 2] = rgba[src + 2];
        }

        return (normalised.Width, normalised.Height, rgb);
    }

    private static void DrawAnnotation(
        DrawingContext context, AttachmentAnnotation annotation, double nativeWidth, double nativeHeight, int rotationDegrees, double zoom)
    {
        var brush = new SolidColorBrush(Color.Parse(annotation.ColorHex));
        var pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

        Point ToCanvas(AnnotationPoint point)
        {
            var (rx, ry) = AnnotationGeometry.ToRotatedContentSpace(point.X, point.Y, nativeWidth, nativeHeight, rotationDegrees);
            return new Point(rx * zoom, ry * zoom);
        }

        switch (annotation.Tool)
        {
            case AnnotationTool.Rectangle:
            {
                var rect = new Rect(ToCanvas(annotation.Points[0]), ToCanvas(annotation.Points[^1]));
                context.DrawGeometry(null, pen, new RectangleGeometry(rect));
                break;
            }

            case AnnotationTool.Ellipse:
            {
                var rect = new Rect(ToCanvas(annotation.Points[0]), ToCanvas(annotation.Points[^1]));
                context.DrawGeometry(null, pen, new EllipseGeometry(rect));
                break;
            }

            case AnnotationTool.Arrow:
            {
                var start = ToCanvas(annotation.Points[0]);
                var end = ToCanvas(annotation.Points[^1]);
                context.DrawLine(pen, start, end);

                var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
                var left = new Point(
                    end.X - ArrowHeadLength * Math.Cos(angle - ArrowHeadAngle), end.Y - ArrowHeadLength * Math.Sin(angle - ArrowHeadAngle));
                var right = new Point(
                    end.X - ArrowHeadLength * Math.Cos(angle + ArrowHeadAngle), end.Y - ArrowHeadLength * Math.Sin(angle + ArrowHeadAngle));

                context.DrawLine(pen, end, left);
                context.DrawLine(pen, end, right);
                break;
            }

            case AnnotationTool.Freehand:
            {
                var points = annotation.Points.Select(ToCanvas).ToList();
                for (var i = 1; i < points.Count; i++)
                    context.DrawLine(pen, points[i - 1], points[i]);
                break;
            }

            case AnnotationTool.TextNote:
            {
                var anchor = ToCanvas(annotation.Points[0]);
                var formatted = new FormattedText(
                    annotation.Text ?? string.Empty,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default),
                    14,
                    Brushes.White);

                const double padding = 4.0;
                var background = new Rect(anchor.X, anchor.Y, formatted.Width + padding * 2, formatted.Height + padding * 2);
                context.FillRectangle(brush, background);
                context.DrawText(formatted, new Point(anchor.X + padding, anchor.Y + padding));
                break;
            }
        }
    }
}

/// <summary>
/// Hand-writes a minimal, spec-valid, single-page PDF containing one
/// full-page raster image — no PDF-authoring package referenced (`TD-98`;
/// see <see cref="AnnotatedCopyExporter.EncodePdf"/>'s own remarks for why).
/// </summary>
internal static class MinimalPdfWriter
{
    /// <summary>
    /// A one-page PDF, <paramref name="pixelWidth"/> by
    /// <paramref name="pixelHeight"/> PDF units (one unit per pixel — this
    /// writer makes no claim about DPI, only about faithfully reproducing
    /// the rendered page), showing <paramref name="rgb"/> — tightly-packed
    /// 8-bit RGB, top row first, exactly <c>pixelWidth * pixelHeight * 3</c>
    /// bytes.
    /// </summary>
    public static byte[] WriteSinglePageImagePdf(int pixelWidth, int pixelHeight, byte[] rgb)
    {
        ArgumentNullException.ThrowIfNull(rgb);

        if (pixelWidth <= 0 || pixelHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "A PDF page needs a positive width and height.");

        var expectedLength = (long)pixelWidth * pixelHeight * 3;
        if (rgb.LongLength != expectedLength)
            throw new ArgumentException($"Expected {expectedLength} RGB bytes for a {pixelWidth}x{pixelHeight} image, got {rgb.LongLength}.", nameof(rgb));

        var imageBytes = Deflate(rgb);

        using var buffer = new MemoryStream();
        var offsets = new long[6];

        void WriteAscii(string text) => buffer.Write(System.Text.Encoding.ASCII.GetBytes(text));

        WriteAscii("%PDF-1.4\n");

        offsets[1] = buffer.Position;
        WriteAscii("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

        offsets[2] = buffer.Position;
        WriteAscii("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");

        offsets[3] = buffer.Position;
        WriteAscii(
            $"3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pixelWidth} {pixelHeight}] " +
            "/Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>\nendobj\n");

        offsets[4] = buffer.Position;
        WriteAscii(
            $"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {pixelWidth} /Height {pixelHeight} " +
            $"/ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {imageBytes.Length} >>\nstream\n");
        buffer.Write(imageBytes);
        WriteAscii("\nendstream\nendobj\n");

        // Both interpolated values are `int`, whose default `ToString()` is
        // already culture-invariant (no group separators), so no explicit
        // `CultureInfo.InvariantCulture` formatting is needed here.
        var content = $"q {pixelWidth} 0 0 {pixelHeight} 0 0 cm /Im0 Do Q";
        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);
        offsets[5] = buffer.Position;
        WriteAscii($"5 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
        buffer.Write(contentBytes);
        WriteAscii("\nendstream\nendobj\n");

        var xrefOffset = buffer.Position;
        WriteAscii($"xref\n0 {offsets.Length}\n");
        WriteAscii("0000000000 65535 f \n");
        for (var i = 1; i < offsets.Length; i++)
            WriteAscii($"{offsets[i]:D10} 00000 n \n");

        WriteAscii($"trailer\n<< /Size {offsets.Length} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

        return buffer.ToArray();
    }

    /// <summary>Zlib-wraps (RFC 1950) <paramref name="data"/> — exactly what PDF's own <c>/FlateDecode</c> filter expects, and the one compression scheme the BCL's <see cref="ZLibStream"/> produces with no manual header/checksum work.</summary>
    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(data);

        return output.ToArray();
    }
}
