using System.Globalization;
using System.Text;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>
/// Synthetic files built to sit exactly at a documented import cap (or one past it): a CAD expression file, a CAD parameter CSV and
/// scan deviation tables. Every value is made up; nothing here is a real drawing or a real measurement.
/// </summary>
internal static class SoakImportFiles
{
    /// <summary>The five parameters the CAD profile of the soak maps (the rest of a file is filler the profile does not name).</summary>
    public static readonly (string Name, string Value)[] MappedParameters =
    [
        ("plate_len", "40"),
        ("gap_min", "0.4"),
        ("gap_max", "0.8"),
    ];

    /// <summary>An NX expression file of exactly <paramref name="parameters"/> parameters, padded with comment lines to exactly <paramref name="totalBytes"/> bytes (0: no padding).</summary>
    public static byte[] NxExpressions(int parameters, long totalBytes)
    {
        var text = new StringBuilder();
        text.Append("// Expression file created by NX 2306 (synthetic, soak)\n// Part: SOAK-PART\n// Revision: A\n");

        foreach (var (name, value) in MappedParameters)
            text.Append(CultureInfo.InvariantCulture, $"[mm]{name}={value}\n");

        for (var i = MappedParameters.Length; i < parameters; i++)
            text.Append(CultureInfo.InvariantCulture, $"[mm]p{i:00000}={12.5 + i % 100 * 0.25:0.00}\n");

        return Pad(Encoding.UTF8.GetBytes(text.ToString()), totalBytes, "// ");
    }

    /// <summary>A generic CAD parameter CSV (name, value, unit) of exactly <paramref name="parameters"/> parameters.</summary>
    public static byte[] GenericCsv(int parameters)
    {
        var text = new StringBuilder("name,value,unit\r\n");

        foreach (var (name, value) in MappedParameters)
            text.Append(CultureInfo.InvariantCulture, $"{name},{value},mm\r\n");

        for (var i = MappedParameters.Length; i < parameters; i++)
            text.Append(CultureInfo.InvariantCulture, $"p{i:00000},{12.5 + i % 100 * 0.25:0.00},mm\r\n");

        return Encoding.UTF8.GetBytes(text.ToString());
    }

    /// <summary>
    /// A scan deviation table of the feature <paramref name="feature"/> filled with rows (element, nominal, actual, deviation) until it is exactly
    /// <paramref name="totalBytes"/> long. Deviations are a bounded pseudo-random spread round zero (millimetres), the same every time.
    /// </summary>
    public static (byte[] Bytes, int Rows) ScanTable(string feature, long totalBytes)
    {
        var header = Encoding.UTF8.GetBytes("Element,Nominal,Actual,Deviation\r\n");
        using var stream = new MemoryStream((int)Math.Min(totalBytes, int.MaxValue));
        stream.Write(header);
        var state = 0x9E3779B97F4A7C15UL;
        var rows = 0;

        while (true)
        {
            state = state * 6364136223846793005UL + 1442695040888963407UL;
            var u = ((state >> 33) & 0x3FFFFF) / (double)0x3FFFFF;
            state = state * 6364136223846793005UL + 1442695040888963407UL;
            var v = ((state >> 33) & 0x3FFFFF) / (double)0x3FFFFF;
            var deviation = (u + v - 1) * 0.04;
            var row = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{feature},40,{40 + deviation:0.0000},{deviation:0.0000}\r\n"));

            if (stream.Length + row.Length > totalBytes)
                break;

            stream.Write(row);
            rows++;
        }

        // The last row is shorter than a whole one would be: fill the remainder with comment-free padding that the table reader ignores (a blank line is not a row).
        var bytes = stream.ToArray();
        return (Pad(bytes, totalBytes, null), rows);
    }

    /// <summary>
    /// A scan table of exactly <paramref name="totalRows"/> rows, as the importer counts them (the header row is one of them), the data rows the smallest
    /// size one can be (four bytes: feature "H", one digit).
    /// </summary>
    public static byte[] ScanRows(int totalRows)
    {
        var rows = totalRows - 1;
        var bytes = new byte[4 + rows * 4];
        var header = Encoding.ASCII.GetBytes("E,D\n");
        header.CopyTo(bytes, 0);
        var at = header.Length;

        for (var i = 0; i < rows; i++)
        {
            bytes[at++] = (byte)'H';
            bytes[at++] = (byte)',';
            bytes[at++] = (byte)('0' + i % 2);
            bytes[at++] = (byte)'\n';
        }

        return bytes.AsSpan(0, at).ToArray();
    }

    /// <summary>
    /// An inputs CSV (<c>symbol,value,unit,provenance,reference,revision</c>) for the frame calculator of <paramref name="totalRows"/> rows, the header
    /// among them, each a cell of its loads table (<c>loads[n].F_x</c>). With <paramref name="totalBytes"/> above zero the rows are padded (trailing
    /// zeros in the number, which is read as the same number) until the file is exactly that long; the last line has a line break after it unless
    /// <paramref name="finalLineBreak"/> is false (then the file is two bytes shorter).
    /// </summary>
    public static string InputsCsv(int totalRows, long totalBytes, bool finalLineBreak)
    {
        const string header = "symbol,value,unit,provenance,reference,revision\r\n";
        var data = totalRows - 1;
        var plain = header.Length + Enumerable.Range(1, data).Sum(n => Row(n, 0).Length);
        var spare = totalBytes > plain ? totalBytes - plain : 0;
        var perRow = data == 0 ? 0 : spare / data;
        var extra = data == 0 ? 0 : spare % data;
        var text = new StringBuilder(header, (int)Math.Max(plain, totalBytes));

        for (var n = 1; n <= data; n++)
            text.Append(Row(n, (int)(perRow + (n <= extra ? 1 : 0))));

        var csv = text.ToString();
        return finalLineBreak || csv.Length < 2 ? csv : csv[..^2];

        static string Row(int n, int padding) => string.Create(CultureInfo.InvariantCulture, $"loads[{n}].F_x,{n % 90 + 1}.0{new string('0', padding)},kN,,,\r\n");
    }

    /// <summary>The same bytes, grown to exactly <paramref name="totalBytes"/> by a final run of blank lines or comment lines.</summary>
    private static byte[] Pad(byte[] bytes, long totalBytes, string? commentPrefix)
    {
        if (totalBytes <= bytes.Length)
            return bytes;

        using var stream = new MemoryStream((int)totalBytes);
        stream.Write(bytes);
        var remaining = totalBytes - bytes.Length;

        if (commentPrefix is null)
        {
            // Blank lines: "\r\n" pairs, then one "\n" if the gap is odd.
            while (remaining >= 2)
            {
                stream.WriteByte((byte)'\r');
                stream.WriteByte((byte)'\n');
                remaining -= 2;
            }

            if (remaining == 1)
                stream.WriteByte((byte)'\n');

            return stream.ToArray();
        }

        var fill = new string('x', 100 - commentPrefix.Length - 1);
        var line = Encoding.UTF8.GetBytes(commentPrefix + fill + "\n");

        while (remaining >= line.Length + 4)
        {
            stream.Write(line);
            remaining -= line.Length;
        }

        if (remaining >= 4)
            stream.Write(Encoding.UTF8.GetBytes(commentPrefix + new string('x', (int)remaining - commentPrefix.Length - 1) + "\n"));
        else
            while (remaining-- > 0)
                stream.WriteByte((byte)'\n');

        return stream.ToArray();
    }
}
