namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Realistic file bytes for the attachment-content tests (`TD-31`).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not <c>new byte[64]</c>. A store that silently mangled
/// content — decoded it as text, stopped at a NUL, normalised a line
/// ending, or round-tripped through UTF-8 — would round-trip an array of
/// zeroes perfectly and fail on every real file. These carry the byte
/// patterns that actually break such a store: NUL bytes mid-stream, 0x1A
/// (the DOS end-of-file that some text paths still honour), bare CR and
/// bare LF, byte sequences that are not valid UTF-8 at all, and 0xFF/0xFE
/// leading bytes that look like a byte-order mark.
/// </para>
/// <para>
/// The file types are the ones the document workflow's own classifications
/// imply (Specification, Report, Procedure, Standard, Datasheet): a PDF
/// report, a PNG screenshot, a ZIP-container Office document, a JPEG
/// photograph, and a CSV datasheet — plus an exhaustive all-256-values
/// blob, which is the one that proves fidelity rather than sampling it.
/// </para>
/// </remarks>
internal static class AttachmentContentSamples
{
    /// <summary>A small but structurally real PDF: header, one object, xref, trailer, EOF marker.</summary>
    internal static byte[] Pdf()
    {
        var body =
            "%PDF-1.7\n" +
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n" +
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] >>\nendobj\n" +
            "xref\n0 4\n0000000000 65535 f \n" +
            "trailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n0\n%%EOF\n";

        // A real PDF is a binary container with a text-ish skeleton: the
        // %-comment after the header is conventionally high-bytes, exactly
        // so that naive text handling is detected. Reproduced here.
        var bytes = new List<byte>(System.Text.Encoding.ASCII.GetBytes(body));
        bytes.InsertRange(9, new byte[] { 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A });
        return [.. bytes];
    }

    /// <summary>A valid 1x1 PNG: signature, IHDR, IDAT and IEND with real CRCs.</summary>
    internal static byte[] Png() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89,
        0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54,
        0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05,
        0x00, 0x01,
        0x0D, 0x0A, 0x2D, 0xB4,
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
        0xAE, 0x42, 0x60, 0x82,
    ];

    /// <summary>A ZIP local file header — the container every .docx/.xlsx actually is.</summary>
    internal static byte[] OfficeDocumentContainer()
    {
        var bytes = new List<byte>
        {
            0x50, 0x4B, 0x03, 0x04,
            0x14, 0x00, 0x00, 0x00, 0x08, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x11, 0x00, 0x00, 0x00,
            0x11, 0x00, 0x00, 0x00,
            0x13, 0x00,
            0x00, 0x00,
        };

        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("[Content_Types].xml"));
        bytes.AddRange(new byte[] { 0x00, 0x1A, 0xFF, 0xFE, 0x0D, 0x0A, 0x00, 0x80, 0x81, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89 });
        bytes.AddRange(new byte[] { 0x50, 0x4B, 0x05, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        return [.. bytes];
    }

    /// <summary>A JPEG skeleton: SOI, a JFIF APP0 segment, and EOI.</summary>
    internal static byte[] Jpeg() =>
    [
        0xFF, 0xD8,
        0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00,
        0x01, 0x01, 0x01, 0x00, 0x48, 0x00, 0x48, 0x00, 0x00,
        0xFF, 0xDB, 0x00, 0x43, 0x00,
        0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07,
        0xFF, 0xD9,
    ];

    /// <summary>A datasheet as CSV, with a UTF-8 BOM and a non-ASCII unit symbol.</summary>
    internal static byte[] Csv()
    {
        var bytes = new List<byte> { 0xEF, 0xBB, 0xBF };
        bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(
            "Property,Value,Unit\r\nYield Strength,250,MPa\r\nDensity,7850,kg/m³\r\nTemperature,20,°C\r\n"));
        return [.. bytes];
    }

    /// <summary>
    /// Every one of the 256 byte values, twice, in both directions.
    /// </summary>
    /// <remarks>
    /// The exhaustive fidelity check: any value the store cannot carry
    /// unchanged is present here, so a single round-trip assertion covers
    /// the whole alphabet rather than the handful a sampled file happens
    /// to contain.
    /// </remarks>
    internal static byte[] EveryByteValue()
    {
        var bytes = new byte[512];
        for (var i = 0; i < 256; i++)
        {
            bytes[i] = (byte)i;
            bytes[511 - i] = (byte)i;
        }

        return bytes;
    }

    /// <summary>A larger blob, deterministic but incompressible-looking, for the multi-megabyte path.</summary>
    internal static byte[] LargeDeterministicBlob(int length)
    {
        var bytes = new byte[length];
        // A cheap deterministic LCG: the same bytes every run, so a failure
        // is reproducible, but not a run of one repeated value.
        uint state = 0x1234_5678;
        for (var i = 0; i < length; i++)
        {
            state = (state * 1664525) + 1013904223;
            bytes[i] = (byte)(state >> 24);
        }

        return bytes;
    }

    /// <summary>Every sample above, with the file name and MIME type the document workflow would carry.</summary>
    internal static IEnumerable<(string FileName, string ContentType, byte[] Bytes)> All()
    {
        yield return ("specification.pdf", "application/pdf", Pdf());
        yield return ("screenshot.png", "image/png", Png());
        yield return ("report.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", OfficeDocumentContainer());
        yield return ("photograph.jpg", "image/jpeg", Jpeg());
        yield return ("datasheet.csv", "text/csv", Csv());
        yield return ("every-byte.bin", "application/octet-stream", EveryByteValue());
    }
}
