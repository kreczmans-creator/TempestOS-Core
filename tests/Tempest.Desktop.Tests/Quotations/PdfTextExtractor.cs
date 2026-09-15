using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Tempest.Desktop.Tests.Quotations;

/// <summary>
/// A minimal text extractor for a PDF <see cref="Tempest.Desktop.Quotations.QuotationSheetRenderer"/>
/// (and <see cref="Tempest.Desktop.IssueSheets.IssueSheetRenderer"/>, and
/// (`WP 21.2A`) this Work Package's own six new renderers) produce
/// (`WP 19.5B`) — test-only, no package added. SkiaSharp's own PDF backend
/// draws text through an embedded, CID-keyed subset font: the content
/// stream's own <c>Tj</c>/<c>TJ</c> operators carry glyph ids, not literal
/// ASCII, so a raw byte search for the rendered words never matches (proven
/// empirically before writing this: it always returns nothing). What does
/// work, and is what every real PDF reader does for exactly this font
/// shape, is reading the glyph ids back through the PDF's own embedded
/// <c>ToUnicode</c> CMap (<c>beginbfchar</c>/<c>beginbfrange</c>) — small,
/// standard, and fully documented, so this needs no third-party PDF
/// library (the brief's own kill switch: no second PDF library).
/// </summary>
/// <remarks>
/// <b>Per-font CMaps, tracked through <c>Tf</c> (`WP 21.2A`).</b> Every
/// renderer before this Work Package drew with exactly one typeface
/// (<c>SKTypeface.Default</c>), so one shared CID→Unicode dictionary was
/// enough. <see cref="Tempest.Desktop.Documents.DocumentFonts"/> draws
/// through up to three distinct embedded faces in the same document, and
/// SkiaSharp gives each its own font object and its own <c>ToUnicode</c>
/// CMap stream, subset-numbered from a low CID independently of the
/// others — so a CID that means one glyph in Chakra Petch's own CMap can
/// coincide with a completely different CID in Space Mono's (proven
/// empirically: with a single merged dictionary, "DESCRIPTION" — drawn in
/// Chakra Petch, whose CMap happened to be parsed first — extracted as
/// "BESCRgPTgON" once Space Mono's own CMap entries for the same numeric
/// CIDs overwrote them). Fixed by resolving, for each drawn glyph, which
/// font was actually selected for it — the content stream's own <c>Tf</c>
/// operator, resolved through the page's <c>/Resources/Font</c> dictionary
/// to the font object that carries that CID's own <c>ToUnicode</c> stream —
/// and decoding against that font's own CMap alone.
/// </remarks>
internal static class PdfTextExtractor
{
    /// <summary>
    /// Extracts every character SkiaSharp's own content stream(s) draw,
    /// concatenated across every <c>Tj</c>/<c>TJ</c> statement found, each
    /// glyph decoded against the specific embedded font's own <c>ToUnicode</c>
    /// CMap that was actually selected (<c>Tf</c>) to draw it — see this
    /// class's own remarks for why a single shared CMap stopped being
    /// enough once more than one embedded font could appear in the same
    /// document (`WP 21.2A`). No artificial separator is inserted between
    /// statements: a real space character drawn by the renderer already
    /// decodes correctly (its own CID maps to <c>U+0020</c> like any other
    /// glyph), and every caller of this method asserts with
    /// <see cref="Xunit.Assert.Contains(string,string,System.StringComparison)"/>
    /// (substring, not equality), so two genuinely distinct draw calls
    /// landing with no space between them changes nothing any assertion
    /// here checks for.
    /// </summary>
    public static string ExtractText(byte[] pdfBytes)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);

        var raw = Encoding.Latin1.GetString(pdfBytes);

        // Every indirect object's own body, by object number — enough to
        // resolve `<fontObjNum> 0 obj ... /ToUnicode <n> 0 R ... endobj`
        // and to find each ToUnicode stream's own bytes by object number.
        var objectBodies = new Dictionary<int, string>();
        foreach (Match obj in Regex.Matches(raw, @"(?<num>\d+)\s+0\s+obj(?<body>.*?)endobj", RegexOptions.Singleline | RegexOptions.Compiled))
            objectBodies[int.Parse(obj.Groups["num"].Value, CultureInfo.InvariantCulture)] = obj.Groups["body"].Value;

        // Font resource name ("F9") -> font object number, from the page's
        // own `/Font <</F9 9 0 R /F10 10 0 R>>` resource dictionary.
        var fontResourceToObject = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match fontDict in Regex.Matches(raw, @"/Font\s*<<(?<entries>.*?)>>", RegexOptions.Singleline | RegexOptions.Compiled))
        {
            foreach (Match entry in Regex.Matches(fontDict.Groups["entries"].Value, @"/(?<name>F\d+)\s+(?<num>\d+)\s+0\s+R", RegexOptions.Compiled))
                fontResourceToObject[entry.Groups["name"].Value] = int.Parse(entry.Groups["num"].Value, CultureInfo.InvariantCulture);
        }

        // Font object number -> that font's own ToUnicode CMap, decoded
        // once and cached — each font object's body names its own
        // ToUnicode stream's object number directly.
        var cmapByFontObject = new Dictionary<int, Dictionary<int, char>>();
        Dictionary<int, char> CMapFor(int fontObjectNumber)
        {
            if (cmapByFontObject.TryGetValue(fontObjectNumber, out var cached))
                return cached;

            var cmap = new Dictionary<int, char>();
            if (objectBodies.TryGetValue(fontObjectNumber, out var fontBody))
            {
                var toUnicodeMatch = Regex.Match(fontBody, @"/ToUnicode\s+(?<num>\d+)\s+0\s+R", RegexOptions.Compiled);
                if (toUnicodeMatch.Success)
                {
                    var toUnicodeObjectNumber = int.Parse(toUnicodeMatch.Groups["num"].Value, CultureInfo.InvariantCulture);
                    var decoded = DecodeObjectStream(pdfBytes, raw, toUnicodeObjectNumber);
                    if (decoded is not null)
                        ParseToUnicodeCMap(decoded, cmap);
                }
            }

            cmapByFontObject[fontObjectNumber] = cmap;
            return cmap;
        }

        var builder = new StringBuilder();
        var tokenPattern = new Regex(@"/(?<font>F\d+)\s+[\d.]+\s+Tf|<(?<hex>[0-9A-Fa-f]+)>\s*(?:Tj|TJ)?", RegexOptions.Compiled);

        foreach (var content in EnumerateFlateDecodedStreams(pdfBytes))
        {
            if (!content.Contains("BT", StringComparison.Ordinal) || (!content.Contains(" Tj", StringComparison.Ordinal) && !content.Contains(" TJ", StringComparison.Ordinal)))
                continue;

            var activeCMap = new Dictionary<int, char>();

            foreach (Match token in tokenPattern.Matches(content))
            {
                if (token.Groups["font"].Success)
                {
                    var resourceName = token.Groups["font"].Value;
                    activeCMap = fontResourceToObject.TryGetValue(resourceName, out var fontObjectNumber)
                        ? CMapFor(fontObjectNumber)
                        : [];
                    continue;
                }

                var hex = token.Groups["hex"].Value;
                for (var i = 0; i + 4 <= hex.Length; i += 4)
                {
                    var code = ParseHex(hex.Substring(i, 4));
                    builder.Append(activeCMap.TryGetValue(code, out var ch) ? ch : ' ');
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>Flate-decodes <c>&lt;objectNumber&gt; 0 obj ... stream ... endstream</c>'s own stream bytes, or <see langword="null"/> if that object was not found or is not itself a stream.</summary>
    private static string? DecodeObjectStream(byte[] pdfBytes, string raw, int objectNumber)
    {
        var marker = $"{objectNumber.ToString(CultureInfo.InvariantCulture)} 0 obj";
        var objIdx = raw.IndexOf(marker, StringComparison.Ordinal);
        if (objIdx < 0)
            return null;

        var streamIdx = raw.IndexOf("stream", objIdx, StringComparison.Ordinal);
        var endObjIdx = raw.IndexOf("endobj", objIdx, StringComparison.Ordinal);
        if (streamIdx < 0 || (endObjIdx >= 0 && streamIdx > endObjIdx))
            return null;

        var start = streamIdx + "stream".Length;
        if (start < raw.Length && raw[start] == '\r')
            start++;
        if (start < raw.Length && raw[start] == '\n')
            start++;

        var endIdx = raw.IndexOf("endstream", start, StringComparison.Ordinal);
        if (endIdx < 0)
            return null;

        var slice = pdfBytes[start..Math.Min(endIdx, pdfBytes.Length)];

        try
        {
            using var input = new MemoryStream(slice);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return Encoding.Latin1.GetString(output.ToArray());
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Every <c>stream...endstream</c> block in <paramref name="pdfBytes"/> that successfully Zlib-inflates, decoded as Latin-1 (byte-for-byte — this format is never true text encoding, just a convenient string view over bytes).</summary>
    private static IEnumerable<string> EnumerateFlateDecodedStreams(byte[] pdfBytes)
    {
        var raw = Encoding.Latin1.GetString(pdfBytes);
        var index = 0;

        while (true)
        {
            var streamIdx = raw.IndexOf("stream", index, StringComparison.Ordinal);
            if (streamIdx < 0)
                yield break;

            var start = streamIdx + "stream".Length;
            if (start < raw.Length && raw[start] == '\r')
                start++;
            if (start < raw.Length && raw[start] == '\n')
                start++;

            var endIdx = raw.IndexOf("endstream", start, StringComparison.Ordinal);
            if (endIdx < 0)
                yield break;

            var slice = pdfBytes[start..Math.Min(endIdx, pdfBytes.Length)];
            index = endIdx + "endstream".Length;

            string? decoded = null;
            try
            {
                using var input = new MemoryStream(slice);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                decoded = Encoding.Latin1.GetString(output.ToArray());
            }
            catch (InvalidDataException)
            {
                // Not a Flate-encoded stream (a raw image, say) — skipped.
            }

            if (decoded is not null)
                yield return decoded;
        }
    }

    /// <summary>
    /// Parses a <c>ToUnicode</c> CMap's own <c>beginbfchar</c>/<c>beginbfrange</c>
    /// sections into <paramref name="into"/> — the standard PDF mechanism a
    /// real reader uses to map a CID-keyed font's glyph ids back to
    /// Unicode. Each section is matched only between its own
    /// <c>begin</c>/<c>end</c> markers — a <c>bfrange</c> triple's first two
    /// entries (<c>lo</c>, <c>hi</c>) would otherwise also satisfy the
    /// two-entry <c>bfchar</c> pattern, misreading a range's own upper bound
    /// as a destination character.
    /// </summary>
    private static void ParseToUnicodeCMap(string content, Dictionary<int, char> into)
    {
        foreach (Match section in Regex.Matches(content, @"beginbfchar(.*?)endbfchar", RegexOptions.Singleline | RegexOptions.Compiled))
        {
            foreach (Match m in Regex.Matches(section.Groups[1].Value, @"<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]{4,})>", RegexOptions.Compiled))
            {
                var code = ParseHex(m.Groups[1].Value);
                var unicode = ParseHex(m.Groups[2].Value[..4]);
                into[code] = (char)unicode;
            }
        }

        foreach (Match section in Regex.Matches(content, @"beginbfrange(.*?)endbfrange", RegexOptions.Singleline | RegexOptions.Compiled))
        {
            foreach (Match m in Regex.Matches(section.Groups[1].Value, @"<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]{4,})>", RegexOptions.Compiled))
            {
                var lo = ParseHex(m.Groups[1].Value);
                var hi = ParseHex(m.Groups[2].Value);
                var dstLo = ParseHex(m.Groups[3].Value[..4]);

                for (var code = lo; code <= hi; code++)
                    into[code] = (char)(dstLo + (code - lo));
            }
        }
    }

    private static int ParseHex(string value) =>
        int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
