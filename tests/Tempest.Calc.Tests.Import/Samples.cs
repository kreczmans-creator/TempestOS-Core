using System.IO.Compression;
using System.Text;
using Tempest.Calc.Import;

namespace Tempest.Calc.Tests.Import;

/// <summary>Test helpers: the golden sample files, in-memory files, and archives built to order.</summary>
internal static class Samples
{
    public static string PathOf(string relative) => Path.Combine(AppContext.BaseDirectory, "Samples", relative.Replace('/', Path.DirectorySeparatorChar));

    public static byte[] BytesOf(string relative) => File.ReadAllBytes(PathOf(relative));

    public static ImportedFile Load(string relative) => BoundedInputReader.FromBytes(BytesOf(relative), relative);

    public static ImportedFile Text(string text, string name = "file.txt") => BoundedInputReader.FromBytes(new UTF8Encoding(false).GetBytes(text), name);

    public static ImportedFile Bytes(byte[] bytes, string name = "file.bin") => BoundedInputReader.FromBytes(bytes, name);

    /// <summary>Asserts the action refuses with <paramref name="code"/> and returns the exception.</summary>
    public static ImportRefusedException Refuses(ImportRefusalCode code, Action action)
    {
        var ex = Assert.Throws<ImportRefusedException>(action);
        Assert.Equal(code, ex.Code);
        return ex;
    }

    /// <summary>A minimal valid .xlsx with one sheet; <paramref name="sheetXml"/> is the &lt;sheetData&gt; content.</summary>
    public static byte[] Workbook(string sheetData, string? sharedStrings = null, IEnumerable<(string Name, byte[] Data)>? extra = null, string? workbookXml = null, string? contentTypes = null)
    {
        var parts = new List<(string Name, byte[] Data)>
        {
            ("[Content_Types].xml", U(contentTypes ?? "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>")),
            ("_rels/.rels", U("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>")),
            ("xl/workbook.xml", U(workbookXml ?? "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"S\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>")),
            ("xl/_rels/workbook.xml.rels", U("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>")),
            ("xl/worksheets/sheet1.xml", U($"<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>{sheetData}</sheetData></worksheet>")),
        };

        if (sharedStrings is not null)
            parts.Add(("xl/sharedStrings.xml", U(sharedStrings)));

        foreach (var part in extra ?? [])
        {
            var existing = parts.FindIndex(p => p.Name == part.Name);

            if (existing >= 0)
                parts[existing] = part;
            else
                parts.Add(part);
        }

        return Zip(parts);
    }

    public static byte[] Zip(IEnumerable<(string Name, byte[] Data)> parts, CompressionLevel level = CompressionLevel.Optimal)
    {
        using var ms = new MemoryStream();

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in parts)
            {
                var entry = zip.CreateEntry(name, level);
                using var s = entry.Open();
                s.Write(data);
            }
        }

        return ms.ToArray();
    }

    public static string Sst(params string[] strings) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" + string.Concat(strings.Select(s => $"<si><t>{System.Security.SecurityElement.Escape(s)}</t></si>")) + "</sst>";

    private static byte[] U(string s) => new UTF8Encoding(false).GetBytes(s);
}
