using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PDFtoImage;
using SkiaSharp;
using Svg.Skia;
using Tempest.Workspace.Viewing;

namespace Tempest.Desktop.Viewing;

/// <summary>
/// A PDF, rasterised page by page at the scale the viewer asks for
/// (`TD-80`).
/// </summary>
/// <remarks>
/// <para>
/// Backed by PDFium, which renders the page's real content — vector
/// artwork, embedded raster images and text alike. This is a genuine
/// render rather than a text extraction laid out to look like one: a
/// drawing whose content is entirely paths appears as those paths, which
/// is exactly the case a text-based approach cannot serve and is the case
/// mock-ups 2 and 3 are about.
/// </para>
/// <para>
/// Rendering happens per request, at the requested scale, rather than once
/// at a fixed resolution. Zooming into a detail therefore re-rasterises at
/// the new scale and shows more of the drawing, instead of magnifying the
/// pixels of an earlier render.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class PdfDocumentPageSource : IDocumentPageSource, ITiledDocumentPageSource
{
    /// <summary>The resolution a PDF page's "natural size" is expressed at.</summary>
    /// <remarks>
    /// A PDF page is measured in points (1/72 inch). Treating one point as
    /// one unit at 72 DPI makes "actual size" mean the page at its true
    /// dimensions, and makes the viewport's content units the page's own.
    /// </remarks>
    public const double BaseDpi = 72.0;

    /// <summary>The largest edge, in pixels, any single rasterised page may have.</summary>
    /// <remarks>
    /// A hard ceiling on one render, not on the zoom: at 32x an A0 drawing
    /// would otherwise ask PDFium for a bitmap of hundreds of megapixels
    /// and take the application down with it. Past this point the page is
    /// rasterised at the highest scale that fits and the view scales that
    /// bitmap, so deep zoom degrades in sharpness rather than in
    /// availability.
    /// </remarks>
    public const int MaxRasterEdge = 8000;

    private readonly byte[]? _content;
    private readonly Stream? _stream;
    private readonly Size[] _pageSizes;

    /// <summary>Loads <paramref name="content"/> as a PDF.</summary>
    /// <exception cref="DocumentRenderException">The bytes are not a PDF this platform can open.</exception>
    public PdfDocumentPageSource(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        _content = content;

        try
        {
            var pageCount = Conversion.GetPageCount(_content);
            if (pageCount <= 0)
                throw new DocumentRenderException("This PDF reports no pages.");

            var sizes = Conversion.GetPageSizes(_content);
            _pageSizes = sizes.Count == pageCount
                ? [.. sizes.Select(s => new Size(s.Width, s.Height))]
                : [.. Enumerable.Repeat(new Size(595, 842), pageCount)];
        }
        catch (DocumentRenderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // PDFium reports a malformed file by throwing, and the viewer's
            // contract is that a document it cannot open is a state on
            // screen rather than a crash — so the exception is translated
            // here, at the boundary, into this layer's own type.
            throw new DocumentRenderException("This PDF could not be opened.", ex);
        }
    }

    /// <summary>
    /// Loads a PDF from <paramref name="content"/> without ever holding
    /// the whole file in one array (`TD-96`): PDFtoImage's own
    /// Stream-accepting overloads read (and seek within) the stream
    /// directly, exactly as they read a byte array, so a 200 MB scanned
    /// drawing opened through <see cref="Tempest.Core.EngineeringDomain.IAttachmentContentStore.OpenReadAsync"/>
    /// never becomes a 200 MB array in this layer either.
    /// </summary>
    /// <remarks>
    /// Takes ownership of <paramref name="content"/>: it is kept open for
    /// this page source's whole lifetime (every <see cref="RenderPage"/>
    /// call reads from it again) and is disposed by <see cref="Dispose"/>.
    /// A PDF's own cross-reference table sits at the end of the file, so
    /// the stream must support seeking — the same requirement PDFium
    /// itself imposes.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="content"/> cannot seek.</exception>
    /// <exception cref="DocumentRenderException">The bytes are not a PDF this platform can open.</exception>
    public PdfDocumentPageSource(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanSeek)
            throw new ArgumentException(
                "A PDF stream must support seeking: PDFium reads its cross-reference table from the end of the file.",
                nameof(content));

        _stream = content;

        try
        {
            var pageCount = Conversion.GetPageCount(_stream, leaveOpen: true);
            if (pageCount <= 0)
                throw new DocumentRenderException("This PDF reports no pages.");

            var sizes = Conversion.GetPageSizes(_stream, leaveOpen: true);
            _pageSizes = sizes.Count == pageCount
                ? [.. sizes.Select(s => new Size(s.Width, s.Height))]
                : [.. Enumerable.Repeat(new Size(595, 842), pageCount)];
        }
        catch (DocumentRenderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DocumentRenderException("This PDF could not be opened.", ex);
        }
    }

    /// <inheritdoc />
    public int PageCount => _pageSizes.Length;

    /// <inheritdoc />
    public Size PageSize(int pageIndex) => _pageSizes[Math.Clamp(pageIndex, 0, PageCount - 1)];

    /// <inheritdoc />
    public Bitmap RenderPage(int pageIndex, double scale)
    {
        var index = Math.Clamp(pageIndex, 0, PageCount - 1);
        var page = PageSize(index);
        var effective = EffectiveScale(page, scale);

        try
        {
            var options = new PDFtoImage.RenderOptions(Dpi: (int)Math.Round(BaseDpi * effective));

            // `TD-96`: the one branch this stream-backed constructor adds
            // to rendering — everything else about producing the bitmap is
            // unchanged and shared with the byte-array-backed instance.
            using var skia = _content is { } bytes
                ? Conversion.ToImage(bytes, new Index(index), options: options)
                : Conversion.ToImage(_stream!, new Index(index), leaveOpen: true, options: options);

            return ToAvaloniaBitmap(skia);
        }
        catch (Exception ex)
        {
            throw new DocumentRenderException($"Page {index + 1} of this PDF could not be rendered.", ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // The byte-array constructor holds nothing native between calls:
        // each render opens and closes its own PDFium document. The
        // stream-backed constructor (`TD-96`) does hold something — the
        // stream itself, kept open across renders so paging through a
        // drawing does not re-open its content store's connection every
        // time — and disposing it here is what releases that connection.
        _stream?.Dispose();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Unlike <see cref="RenderPage"/>, this never applies <see cref="MaxRasterEdge"/>:
    /// one tile is <see cref="TileGrid.TileSize"/> pixels on a side by
    /// construction, always comfortably under that ceiling regardless of
    /// scale, which is the entire reason tiling raises it — a whole A0
    /// sheet at 400% zoom would hit the cap and degrade; the same sheet as
    /// a grid of 512px tiles never does, because no single render this
    /// method ever issues is large enough to.
    /// </remarks>
    public Bitmap RenderTile(int pageIndex, double scale, int column, int row, int tileSize)
    {
        var index = Math.Clamp(pageIndex, 0, PageCount - 1);
        var page = PageSize(index);
        var effective = double.IsFinite(scale) && scale > 0 ? scale : 1;

        var (x, y, width, height) = TileGrid.TileContentRect(page.Width, page.Height, effective, column, row);
        if (width <= 0 || height <= 0)
            throw new DocumentRenderException($"Tile ({column},{row}) of page {index + 1} lies entirely outside the page.");

        var pixelWidth = Math.Max(1, (int)Math.Round(width * effective));
        var pixelHeight = Math.Max(1, (int)Math.Round(height * effective));

        try
        {
            var options = new PDFtoImage.RenderOptions(
                Width: pixelWidth,
                Height: pixelHeight,
                Bounds: new System.Drawing.RectangleF((float)x, (float)y, (float)width, (float)height));

            using var skia = _content is { } bytes
                ? Conversion.ToImage(bytes, new Index(index), options: options)
                : Conversion.ToImage(_stream!, new Index(index), leaveOpen: true, options: options);

            return ToAvaloniaBitmap(skia);
        }
        catch (Exception ex)
        {
            throw new DocumentRenderException($"Tile ({column},{row}) of page {index + 1} could not be rendered.", ex);
        }
    }

    private static double EffectiveScale(Size page, double scale)
    {
        var requested = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var longestEdge = Math.Max(page.Width, page.Height);
        if (longestEdge <= 0)
            return requested;

        return Math.Min(requested, MaxRasterEdge / longestEdge);
    }

    internal static Bitmap ToAvaloniaBitmap(SKBitmap skia)
    {
        // Copied through Avalonia's own WriteableBitmap rather than
        // re-encoding to PNG and decoding again: the pixels are already in
        // memory, and a re-encode of a large drawing costs more than the
        // render did.
        var bitmap = new WriteableBitmap(
            new PixelSize(skia.Width, skia.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var buffer = bitmap.Lock())
        {
            // Read straight into Avalonia's locked buffer. Source and
            // destination are the same size, so this is a format
            // conversion and a copy, never a resample.
            var info = new SKImageInfo(skia.Width, skia.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var image = SKImage.FromBitmap(skia);
            if (image is null || !image.ReadPixels(info, buffer.Address, buffer.RowBytes, 0, 0))
                throw new DocumentRenderException("A rendered PDF page could not be converted for display.");
        }

        return bitmap;
    }
}

/// <summary>
/// A raster image, rendered at full fidelity by Avalonia itself (`TD-80`).
/// </summary>
/// <remarks>
/// One page, always. The bitmap is decoded once and drawn at whatever
/// scale the viewport asks for, because scaling a raster is all that can
/// be done with one — unlike a PDF, there is no more detail to re-render
/// at.
/// </remarks>
public sealed class ImageDocumentPageSource : IDocumentPageSource
{
    /// <summary>
    /// The largest decoded pixel count (width &#215; height) this source will
    /// ask Avalonia's own decoder to materialise.
    /// </summary>
    /// <remarks>
    /// `WP 21.5F` Offensive Security Audit, OSA-01: unlike
    /// <see cref="PdfDocumentPageSource"/>'s <see cref="PdfDocumentPageSource.MaxRasterEdge"/>,
    /// nothing here previously bounded the decoded bitmap at all — a small
    /// file that declares an enormous pixel grid (a classic decompression
    /// bomb shape) was decoded to its full, real size with nothing to stop
    /// it. 40 million pixels is generous for a real photograph or scanned
    /// drawing (roughly 8000&#215;5000) while keeping one decoded bitmap's own
    /// memory (4 bytes/pixel, BGRA32) under ~160&#160;MB.
    /// </remarks>
    public const long MaxDecodedPixels = 40_000_000;

    private readonly Bitmap _bitmap;

    /// <summary>Loads <paramref name="content"/> as an image.</summary>
    /// <exception cref="DocumentRenderException">The bytes are not an image this platform can decode, or declare more pixels than <see cref="MaxDecodedPixels"/> allows.</exception>
    public ImageDocumentPageSource(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            GuardAgainstOversizedImage(stream);
            _bitmap = new Bitmap(stream);

            // A decoder that returns a zero-sized bitmap rather than
            // throwing has still failed, and letting that through would
            // open an empty tab instead of saying the format is not one
            // this platform can show.
            if (_bitmap.PixelSize.Width <= 0 || _bitmap.PixelSize.Height <= 0)
                throw new DocumentRenderException("This image decoded to nothing.");
        }
        catch (DocumentRenderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DocumentRenderException("This image could not be decoded.", ex);
        }
    }

    /// <summary>
    /// Peeks a seekable image stream's own declared dimensions through
    /// <see cref="SKCodec"/> — parsing only the header, never the pixel
    /// data — and refuses before Avalonia's own decoder ever allocates a
    /// bitmap for more than <see cref="MaxDecodedPixels"/> pixels.
    /// Restores <paramref name="stream"/>'s position before returning
    /// either way, so the real decode afterwards sees the same stream it
    /// would have without this check.
    /// </summary>
    /// <exception cref="DocumentRenderException">The declared pixel count exceeds <see cref="MaxDecodedPixels"/>.</exception>
    private static void GuardAgainstOversizedImage(Stream stream)
    {
        if (!stream.CanSeek)
            return;

        var position = stream.Position;
        try
        {
            // disposeManagedStream: false - SKCodec.Create(Stream) would
            // otherwise take ownership and close the real stream once the
            // codec itself is disposed, leaving the real decode below (and
            // every other caller of this stream) working against a closed
            // stream.
            using var managedStream = new SKManagedStream(stream, disposeManagedStream: false);
            using var codec = SKCodec.Create(managedStream);
            if (codec is null)
                return;

            var declaredPixels = (long)codec.Info.Width * codec.Info.Height;
            if (declaredPixels > MaxDecodedPixels)
            {
                throw new DocumentRenderException(
                    $"This image declares {codec.Info.Width}x{codec.Info.Height} pixels " +
                    $"({declaredPixels:N0} total) - more than this platform will decode.");
            }
        }
        finally
        {
            stream.Position = position;
        }
    }

    /// <summary>
    /// Loads <paramref name="content"/> as an image without ever holding
    /// the whole file in one array (`TD-96`): Avalonia's own
    /// <see cref="Bitmap(Stream)"/> decodes straight from the stream.
    /// </summary>
    /// <remarks>
    /// Unlike the PDF page source, nothing here needs the stream again
    /// once decoding finishes — there is exactly one page, already fully
    /// decoded into <see cref="_bitmap"/> — so <paramref name="content"/>
    /// is disposed before this constructor returns rather than kept for
    /// the page source's lifetime.
    /// </remarks>
    /// <exception cref="DocumentRenderException">The bytes are not an image this platform can decode.</exception>
    public ImageDocumentPageSource(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            GuardAgainstOversizedImage(content);
            _bitmap = new Bitmap(content);

            if (_bitmap.PixelSize.Width <= 0 || _bitmap.PixelSize.Height <= 0)
                throw new DocumentRenderException("This image decoded to nothing.");
        }
        catch (DocumentRenderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DocumentRenderException("This image could not be decoded.", ex);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <inheritdoc />
    public int PageCount => 1;

    /// <inheritdoc />
    /// <remarks>
    /// The image's pixel size, not its DIP size: an image's own units are
    /// its pixels, and "actual size" must mean one image pixel per screen
    /// pixel rather than whatever DPI the file happens to claim.
    /// </remarks>
    public Size PageSize(int pageIndex) => new(_bitmap.PixelSize.Width, _bitmap.PixelSize.Height);

    /// <inheritdoc />
    public Bitmap RenderPage(int pageIndex, double scale) => _bitmap;

    /// <inheritdoc />
    public void Dispose() => _bitmap.Dispose();
}

/// <summary>
/// Text, laid out into pages the viewer can turn (`TD-80`).
/// </summary>
/// <remarks>
/// Paginated rather than rendered as one endless page, so the same page
/// navigation serves a long CSV datasheet as serves a multi-page PDF —
/// one behaviour for the user, one code path for the viewer.
/// </remarks>
public sealed class TextDocumentPageSource : IDocumentPageSource
{
    /// <summary>Lines per page.</summary>
    public const int LinesPerPage = 48;

    /// <summary>The page size text is laid out onto, in the viewport's own units.</summary>
    public static readonly Size TextPageSize = new(816, 1056);

    /// <summary>
    /// The most source bytes this page source will decode and hold as one
    /// managed <see cref="string"/> before truncating.
    /// </summary>
    /// <remarks>
    /// `WP 21.5F` Offensive Security Audit, OSA-01: unlike the PDF and
    /// image page sources, nothing previously bounded this one at all —
    /// the whole file was decoded into one <see cref="string"/> (and then
    /// one <c>string[]</c> of lines) unconditionally, regardless of size.
    /// 25&#160;MB is generous for a real datasheet, log or CSV export while
    /// keeping one open document's worst-case decoded-text memory bounded
    /// (roughly double this in UTF-16 <see cref="char"/>s, plus the line
    /// array) rather than unlimited.
    /// </remarks>
    public const int MaxSourceBytes = 25_000_000;

    private const string TruncationNotice =
        "\n\n[TempestOS stopped reading this file at 25 MB for display - the stored file itself is unaffected.]";

    private const double Margin = 48;
    private const double LineHeight = 20;
    private const double FontSize = 13;

    private readonly string[][] _pages;

    /// <summary>Loads <paramref name="content"/> as text.</summary>
    public TextDocumentPageSource(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var truncated = content.Length > MaxSourceBytes;
        var bounded = truncated ? content.AsSpan(0, MaxSourceBytes) : content.AsSpan();

        // Decoded permissively: a datasheet with one malformed byte is
        // still a datasheet worth reading, and replacement characters say
        // more to an engineer than a refusal to open the file does.
        var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            .GetString(bounded)
            .TrimStart('﻿');

        if (truncated)
            text += TruncationNotice;

        var lines = text.ReplaceLineEndings("\n").Split('\n');
        _pages = lines.Length == 0
            ? [[string.Empty]]
            : [.. lines.Chunk(LinesPerPage)];

        if (_pages.Length == 0)
            _pages = [[string.Empty]];
    }

    /// <summary>
    /// Loads <paramref name="content"/> as text, read through a bounded
    /// buffer rather than requiring a <c>byte[]</c> up front (`TD-96`) —
    /// the same permissive UTF-8 decoding and the same
    /// <see cref="MaxSourceBytes"/> cap as the byte-array constructor,
    /// applied to whatever the stream yields.
    /// </summary>
    public TextDocumentPageSource(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        string text;
        using (content)
        {
            var (bytes, truncated) = ReadBounded(content, MaxSourceBytes);
            using var reader = new StreamReader(
                new MemoryStream(bytes, writable: false),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false), detectEncodingFromByteOrderMarks: true);
            text = reader.ReadToEnd().TrimStart('﻿');
            if (truncated)
                text += TruncationNotice;
        }

        _pages = BuildPages(text);
    }

    /// <summary>
    /// Reads at most <paramref name="maxBytes"/> from <paramref name="stream"/>,
    /// never buffering more than one byte past that limit — the streamed-path
    /// equivalent of the byte-array constructor's own <see cref="MaxSourceBytes"/>
    /// slice, so a huge stream cannot be read to completion in memory before
    /// this source gets a chance to bound it.
    /// </summary>
    private static (byte[] Bytes, bool Truncated) ReadBounded(Stream stream, int maxBytes)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while (buffer.Length <= maxBytes && (read = stream.Read(chunk, 0, chunk.Length)) > 0)
            buffer.Write(chunk, 0, read);

        if (buffer.Length <= maxBytes)
            return (buffer.ToArray(), false);

        var bytes = new byte[maxBytes];
        Array.Copy(buffer.GetBuffer(), bytes, maxBytes);
        return (bytes, true);
    }

    /// <summary>Lays out <paramref name="text"/> into <see cref="LinesPerPage"/>-line pages, at least one even for empty text.</summary>
    private static string[][] BuildPages(string text)
    {
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var pages = lines.Length == 0 ? [] : lines.Chunk(LinesPerPage).ToArray();
        return pages.Length == 0 ? [[string.Empty]] : pages;
    }

    /// <inheritdoc />
    public int PageCount => _pages.Length;

    /// <inheritdoc />
    public Size PageSize(int pageIndex) => TextPageSize;

    /// <inheritdoc />
    public Bitmap RenderPage(int pageIndex, double scale)
    {
        var index = Math.Clamp(pageIndex, 0, PageCount - 1);
        var effective = double.IsFinite(scale) && scale > 0 ? Math.Min(scale, 4) : 1;

        var pixelWidth = Math.Max(1, (int)Math.Round(TextPageSize.Width * effective));
        var pixelHeight = Math.Max(1, (int)Math.Round(TextPageSize.Height * effective));

        var target = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96, 96));
        using (var context = target.CreateDrawingContext())
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, pixelWidth, pixelHeight));

            var y = Margin * effective;
            foreach (var line in _pages[index])
            {
                var formatted = new FormattedText(
                    line,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default),
                    FontSize * effective,
                    Brushes.Black);

                context.DrawText(formatted, new Point(Margin * effective, y));
                y += LineHeight * effective;
            }
        }

        return target;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Each render owns its own RenderTargetBitmap, handed to the view.
    }
}

/// <summary>
/// An SVG, rasterised through <c>Svg.Skia</c> (MIT-licensed; version pinned
/// in <c>Tempest.Desktop.csproj</c>'s own remarks) to the same
/// <see cref="SKBitmap"/>-backed page a PDF produces (`TD-99`).
/// </summary>
/// <remarks>
/// One page, always — an SVG has no pagination concept. Rendered at the
/// scale the viewer asks for, exactly like <see cref="PdfDocumentPageSource"/>:
/// zooming into a detail re-rasterises the vector content at the new scale
/// rather than magnifying an earlier render's pixels, which is the entire
/// reason a vector format is worth a real rasteriser rather than a
/// generic image decoder.
/// </remarks>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class SvgDocumentPageSource : IDocumentPageSource
{
    /// <summary>The largest edge, in pixels, any single rasterised page may have — the same ceiling <see cref="PdfDocumentPageSource.MaxRasterEdge"/> applies, and for the same reason: an SVG's own <c>viewBox</c> can claim any size at all, and a deep zoom must not ask Skia for a bitmap of hundreds of megapixels.</summary>
    public const int MaxRasterEdge = 8000;

    private readonly SKSvg _svg;
    private readonly Size _pageSize;

    /// <summary>Loads <paramref name="content"/> as an SVG.</summary>
    /// <exception cref="DocumentRenderException">The bytes are not an SVG this platform can read.</exception>
    public SvgDocumentPageSource(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var stream = new MemoryStream(content, writable: false);
        _svg = Load(stream);
        _pageSize = NaturalSize(_svg);
    }

    /// <summary>
    /// Loads an SVG from <paramref name="content"/> without requiring a
    /// <c>byte[]</c> up front (`TD-96`'s own established convention for
    /// every page source) — <see cref="SKSvg.Load(Stream)"/> reads the
    /// stream directly.
    /// </summary>
    /// <exception cref="DocumentRenderException">The bytes are not an SVG this platform can read.</exception>
    public SvgDocumentPageSource(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            _svg = Load(content);
            _pageSize = NaturalSize(_svg);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <inheritdoc />
    public int PageCount => 1;

    /// <inheritdoc />
    public Size PageSize(int pageIndex) => _pageSize;

    /// <inheritdoc />
    public Bitmap RenderPage(int pageIndex, double scale)
    {
        try
        {
            var effective = EffectiveScale(_pageSize, scale);
            var pixelWidth = Math.Max(1, (int)Math.Round(_pageSize.Width * effective));
            var pixelHeight = Math.Max(1, (int)Math.Round(_pageSize.Height * effective));

            using var bitmap = new SKBitmap(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale((float)effective);
                canvas.DrawPicture(_svg.Picture);
            }

            return PdfDocumentPageSource.ToAvaloniaBitmap(bitmap);
        }
        catch (Exception ex)
        {
            throw new DocumentRenderException($"This SVG could not be read: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _svg.Dispose();

    private static SKSvg Load(Stream stream)
    {
        SKSvg svg;

        try
        {
            string markup;
            using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
                markup = reader.ReadToEnd();

            var sanitised = SvgMarkupSanitiser.Sanitise(markup);

            svg = new SKSvg();
            SKPicture? picture;
            using (var sanitisedStream = new MemoryStream(Encoding.UTF8.GetBytes(sanitised)))
                picture = svg.Load(sanitisedStream);

            if (picture is null)
            {
                svg.Dispose();
                throw new DocumentRenderException("This SVG could not be read: the file has no readable SVG content.");
            }

            if (picture.CullRect.Width <= 0 || picture.CullRect.Height <= 0)
            {
                svg.Dispose();
                throw new DocumentRenderException("This SVG could not be read: it has no visible size.");
            }
        }
        catch (DocumentRenderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Svg.Skia reports a malformed document by throwing (an
            // XmlException over broken markup, most commonly) — translated
            // here, at the boundary, into this platform's own type, exactly
            // as PdfDocumentPageSource does for PDFium.
            throw new DocumentRenderException($"This SVG could not be read: {ex.Message}", ex);
        }

        return svg;
    }

    private static Size NaturalSize(SKSvg svg)
    {
        var bounds = svg.Picture!.CullRect;
        return new Size(bounds.Width, bounds.Height);
    }

    private static double EffectiveScale(Size page, double scale)
    {
        var requested = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var longestEdge = Math.Max(page.Width, page.Height);
        if (longestEdge <= 0)
            return requested;

        return Math.Min(requested, MaxRasterEdge / longestEdge);
    }
}

/// <summary>
/// Strips an SVG document of anything that could make rendering it touch
/// the network or the local disk, or run script — none of which a static
/// rasteriser has any legitimate reason to do (`TD-184`). Deliberately not
/// a member of <see cref="SvgDocumentPageSource"/>: this is pure text
/// processing with nothing platform-specific about it, so it carries none
/// of that class's own <see cref="System.Runtime.Versioning.SupportedOSPlatformAttribute"/>
/// declarations and can be called (and tested) from anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists, specifically:</b> <c>Svg.Skia</c>'s own image
/// resolution (<c>Svg.Model.SvgExtensions.GetImageFromWeb</c>) calls
/// <c>System.Net.WebRequest.Create(uri).GetResponse()</c> for any
/// <c>&lt;image&gt;</c> reference that is not a <c>data:</c> URI —
/// <c>http://</c>, <c>https://</c> <b>and</b> <c>file://</c> alike, the last
/// of which reads an arbitrary local file and folds its bytes into the
/// rendered picture. An SVG is untrusted, attacker-controlled input the
/// moment it reaches this platform (an attachment's own bytes), so a
/// reference resolved against the document's own base URI runs at this
/// platform's own expense the instant the document renders — before any
/// content is even shown, with no further action from the user. Every
/// <c>href</c>/<c>xlink:href</c> that is not a same-document fragment
/// (<c>#id</c>) or an embedded <c>data:</c> URI is blanked by
/// <see cref="Sanitise"/>, before the bytes ever reach <c>SKSvg.Load</c>, so
/// the vulnerable call is never given anything to resolve.
/// </para>
/// <para>
/// A <c>&lt;!DOCTYPE&gt;</c> declaration is removed outright — the XXE
/// vector, and an SVG has no legitimate reason to declare one. A
/// <c>&lt;script&gt;</c> element is removed too: this rasteriser has no
/// script engine to run it, so it is already inert, but stripping it is
/// cheap, unambiguous defence in depth against a future rendering path (or a
/// future library upgrade) that does.
/// </para>
/// </remarks>
internal static class SvgMarkupSanitiser
{
    private static readonly Regex DoctypePattern = new(
        @"<!DOCTYPE[^>[]*(\[[^\]]*\])?[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ScriptElementPattern = new(
        @"<script\b[^>]*>.*?</script\s*>|<script\b[^>]*/\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex ExternalReferencePattern = new(
        @"(?<attr>xlink:href|href)\s*=\s*(?<quote>[""'])(?!\s*(#|data:))(?<value>[^""']*)\k<quote>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// A dangerous <c>href</c>/<c>xlink:href</c> is replaced with this,
    /// never with an empty string. Both are equally inert content — an
    /// empty <c>data:</c> URI resolves locally to nothing — but an empty
    /// <em>string</em> is a same-document <b>relative</b> reference per
    /// RFC 3986, which some SVG readers resolve against the document's own
    /// base URI regardless of the value being empty; a document loaded
    /// from a bare stream (every attachment this platform opens) carries
    /// no base URI at all, and resolving a relative reference against a
    /// null one throws. A <c>data:</c> URI is absolute — Svg.Skia's own
    /// image resolution (<c>Svg.Model.SvgExtensions.GetImageUri</c>)
    /// returns it immediately, before any base-URI resolution is even
    /// attempted — so this is the one substitution that is both inert and
    /// never touches that code path at all.
    /// </summary>
    private const string InertReferenceValue = "data:,";

    /// <summary>Returns <paramref name="markup"/> with every DOCTYPE, script element and non-fragment/non-<c>data:</c> href removed or blanked.</summary>
    public static string Sanitise(string markup)
    {
        ArgumentNullException.ThrowIfNull(markup);

        var sanitised = DoctypePattern.Replace(markup, string.Empty);
        sanitised = ScriptElementPattern.Replace(sanitised, string.Empty);
        sanitised = ExternalReferencePattern.Replace(sanitised, m => $"{m.Groups["attr"].Value}=\"{InertReferenceValue}\"");
        return sanitised;
    }
}

/// <summary>A document this platform could not open or render.</summary>
public sealed class DocumentRenderException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="DocumentRenderException"/> class.</summary>
    public DocumentRenderException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="DocumentRenderException"/> class.</summary>
    public DocumentRenderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Chooses the page source for a document's format (`TD-80`).
/// </summary>
/// <remarks>
/// The one place format maps to renderer. A format with no source here is
/// <see cref="DocumentViewStatus.Unsupported"/> — reported as such rather
/// than opened as an empty page, so "we cannot render this" and "this file
/// is damaged" stay different answers.
/// </remarks>
public static class DocumentPageSourceFactory
{
    /// <summary>
    /// A page source for <paramref name="content"/>, or
    /// <see langword="null"/> when this platform cannot render that format.
    /// </summary>
    /// <exception cref="DocumentRenderException">The format is supported but these particular bytes could not be opened.</exception>
    public static IDocumentPageSource? Create(ViewableDocumentFormat format, byte[] content) => format switch
    {
        // Guarded rather than suppressed. PDFium ships native binaries for
        // Windows, Linux and macOS, which is every platform this desktop
        // application runs on — but "every platform we ship to" is not the
        // same claim as "every platform this code could be compiled for",
        // and the analyser is right to insist on the difference. On
        // anything else a PDF genuinely is a format this build cannot
        // render, and reporting it Unsupported is the true answer rather
        // than a suppression of the question.
        ViewableDocumentFormat.Pdf when OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            => new PdfDocumentPageSource(content),
        ViewableDocumentFormat.Image => new ImageDocumentPageSource(content),
        ViewableDocumentFormat.Text => new TextDocumentPageSource(content),
        ViewableDocumentFormat.Svg when OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            => new SvgDocumentPageSource(content),

        // ExternalOnly falls to the same null the default arm returns for
        // any other unmatched format — named here rather than left to fall
        // through silently, because unlike a genuinely unhandled format
        // this one is not a gap: a DWG or DXF is never meant to gain an
        // in-app source, only the "open externally" path (`TD-99`).
        ViewableDocumentFormat.ExternalOnly => null,
        _ => null,
    };

    /// <summary>
    /// A page source for <paramref name="content"/>, read without ever
    /// holding the whole file in one array (`TD-96`) — the attachment
    /// viewer's own read path
    /// (<see cref="Tempest.Desktop.Viewing.AttachmentViewerLauncher"/>)
    /// uses this instead of <see cref="Create"/> whenever it has a
    /// verified stream in hand rather than a materialised array.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> for exactly the same reason <see cref="Create"/>
    /// returns <see langword="null"/> — a format with no source here — and
    /// additionally for a format <see cref="Create"/> supports but this
    /// method does not (yet) have a stream-based source for: either way,
    /// the caller's own fallback to <see cref="Create"/> over a fully read
    /// array is what actually renders it, so no format regresses to
    /// unsupported by calling this method first.
    /// </remarks>
    /// <exception cref="DocumentRenderException">The format is supported but these particular bytes could not be opened.</exception>
    public static IDocumentPageSource? CreateFromStream(ViewableDocumentFormat format, Stream content) => format switch
    {
        ViewableDocumentFormat.Pdf when OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            => new PdfDocumentPageSource(content),
        ViewableDocumentFormat.Image => new ImageDocumentPageSource(content),
        ViewableDocumentFormat.Text => new TextDocumentPageSource(content),
        ViewableDocumentFormat.Svg when OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            => new SvgDocumentPageSource(content),
        _ => null,
    };
}
