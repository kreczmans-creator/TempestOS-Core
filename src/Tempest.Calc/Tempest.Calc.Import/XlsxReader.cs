using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Tempest.Calc.Import;

/// <summary>What a worksheet cell holds, as the file stored it.</summary>
public enum XlsxCellKind
{
    Empty,
    Number,
    Text,
    Boolean,
    Error,
}

/// <summary>
/// One cell. <see cref="Text"/> is the stored value: the invariant number text, the string, <c>true</c>/<c>false</c> or the
/// error code. <see cref="IsFormula"/> says the cell carries a formula; the value is the result Excel saved with it (the
/// cached value), and the formula itself is never read, kept or evaluated.
/// </summary>
public sealed record XlsxCell(int Column, string Text, XlsxCellKind Kind, bool IsFormula)
{
    /// <summary>The A1-style reference of the cell in row <paramref name="row"/>.</summary>
    public string Reference(int row) => XlsxReader.ColumnName(Column) + row.ToString(CultureInfo.InvariantCulture);
}

public sealed record XlsxRow(int Number, IReadOnlyList<XlsxCell> Cells)
{
    public XlsxCell? At(int column) => Cells.FirstOrDefault(c => c.Column == column);
}

public sealed record XlsxSheet(string Name, bool Hidden, IReadOnlyList<XlsxRow> Rows)
{
    public XlsxCell? At(int row, int column) => Rows.FirstOrDefault(r => r.Number == row)?.At(column);
}

public sealed record XlsxWorkbook(IReadOnlyList<XlsxSheet> Sheets, IReadOnlyList<string> Notices, int FormulaCellCount, IReadOnlyList<string>? SheetNames = null);

/// <summary>
/// A safe .xlsx reader over <see cref="System.IO.Compression"/> and <see cref="XmlReader"/>, with no package behind it.
/// Before anything is opened the archive's entries are checked (entry count, names, declared sizes, expanded:compressed
/// ratio); while an entry is read, the bytes actually produced are counted against the same caps, so an archive whose
/// central directory lies is stopped too. A workbook with macros is refused. XML is read with DTDs prohibited and no
/// resolver, so an entity or an external reference cannot be expanded. Only cell values are read: shared strings, inline
/// strings, numbers, booleans and error codes, and for a formula cell the cached value only; formulas are never
/// evaluated, external links are never followed, embedded objects are ignored.
/// </summary>
public static class XlsxReader
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// Reads ONE worksheet of the workbook in <paramref name="file"/>: the one named <paramref name="sheetName"/>, else the first
    /// visible one. The sheet is chosen from the workbook part before any sheet is read, so a workbook declaring hundreds of sheets
    /// costs one sheet. <see cref="XlsxWorkbook.SheetNames"/> lists every declared sheet.
    /// </summary>
    public static XlsxWorkbook Read(ImportedFile file, ImportBudget? budget = null, string? sheetName = null) => Read(file?.Bytes ?? throw new ArgumentNullException(nameof(file)), budget, sheetName);

    public static XlsxWorkbook Read(byte[] bytes, ImportBudget? budget = null, string? sheetName = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        budget ??= new ImportBudget();
        var limits = budget.Limits;

        if (bytes.Length > limits.MaxBytes)
            throw new ImportRefusedException(ImportRefusalCode.TooLarge, $"The file is larger than {limits.MaxBytes / (1024.0 * 1024.0):0.#} MB and was not read.");

        if (bytes.Length >= 8 && bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0)
            throw new ImportRefusedException(ImportRefusalCode.Unsupported, "This is an old .xls workbook or a password-protected one. Save it as an unprotected .xlsx and import it again.");

        if (bytes.Length < 4 || bytes[0] != (byte)'P' || bytes[1] != (byte)'K')
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The file is not an .xlsx workbook (it is not a zip archive).");

        var notices = new List<string>();

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entries = GuardArchive(zip, bytes.Length, limits, notices);

            RefuseMacros(entries, budget);

            var workbookEntry = Require(entries, "xl/workbook.xml");
            var sheetsDeclared = ReadWorkbook(workbookEntry, budget);
            var rels = entries.TryGetValue("xl/_rels/workbook.xml.rels", out var relEntry) ? ReadRelationships(relEntry, budget) : [];
            if (sheetsDeclared.Count == 0)
                throw new ImportRefusedException(ImportRefusalCode.Empty, "The workbook has no worksheets.");

            // Every sheet's part is resolved and no two sheets may share one: a workbook whose sheets all point at the same large part
            // would otherwise make the reader parse it once per sheet.
            var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var declared in sheetsDeclared)
            {
                if (!rels.TryGetValue(declared.RelationId, out var target))
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"Sheet '{declared.Name}' has no relationship in the workbook.");

                var targetPath = ResolveTarget(target);

                if (targets.TryGetValue(targetPath, out var other))
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The sheets '{other}' and '{declared.Name}' point at the same part of the file, which no real workbook does.");

                targets[targetPath] = declared.Name;
            }

            DeclaredSheet chosen;

            if (sheetName is { Length: > 0 })
            {
                chosen = sheetsDeclared.FirstOrDefault(d => string.Equals(d.Name, sheetName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The workbook has no sheet named '{Clip(sheetName)}'. It has: {string.Join(", ", sheetsDeclared.Select(d => d.Name).Take(10))}.");
            }
            else
            {
                chosen = sheetsDeclared.FirstOrDefault(d => !d.Hidden) ?? sheetsDeclared[0];
            }

            var chosenPath = ResolveTarget(rels[chosen.RelationId]);

            if (!entries.TryGetValue(chosenPath, out var sheetEntry))
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"Sheet '{chosen.Name}' points at '{chosenPath}', which is not in the file.");

            var shared = entries.TryGetValue("xl/sharedStrings.xml", out var ssEntry) ? ReadSharedStrings(ssEntry, budget) : [];
            var formulas = 0;
            var cells = 0;
            var rows = ReadSheet(sheetEntry, shared, budget, ref cells, ref formulas);
            var sheets = new List<XlsxSheet> { new(chosen.Name, chosen.Hidden, rows) };

            return new XlsxWorkbook(sheets, notices, formulas, sheetsDeclared.Select(d => d.Name).ToList());
        }
        catch (InvalidDataException e)
        {
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The .xlsx file is damaged or is not a workbook: " + Clip(e.Message));
        }
        catch (XmlException e)
        {
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The workbook holds XML that is not accepted (no DTDs or entities are read): " + Clip(e.Message));
        }
    }

    /// <summary>The A1 column name of a 1-based column number.</summary>
    public static string ColumnName(int column)
    {
        if (column < 1)
            return "?";

        var sb = new StringBuilder();

        while (column > 0)
        {
            column--;
            sb.Insert(0, (char)('A' + (column % 26)));
            column /= 26;
        }

        return sb.ToString();
    }

    /// <summary>The 1-based column and row of an A1 reference, or <see langword="null"/> when it is not one.</summary>
    public static (int Column, int Row)? ParseReference(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var i = 0;
        var column = 0;

        while (i < reference.Length && reference[i] is >= 'A' and <= 'Z')
        {
            column = checked((column * 26) + (reference[i] - 'A' + 1));

            if (column > 16384)
                return null;

            i++;
        }

        if (i == 0 || i == reference.Length || !int.TryParse(reference.AsSpan(i), NumberStyles.None, CultureInfo.InvariantCulture, out var row) || row < 1 || row > 1_048_576)
            return null;

        return (column, row);
    }

    // ------------------------------------------------------------------

    private static Dictionary<string, ZipArchiveEntry> GuardArchive(ZipArchive zip, long compressedTotal, ImportLimits limits, List<string> notices)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

        if (zip.Entries.Count > limits.MaxZipEntries)
            throw new ImportRefusedException(ImportRefusalCode.ZipBomb, $"The workbook holds more than {limits.MaxZipEntries} parts and was not read.");

        long total = 0;

        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;

            if (name.Length is 0 or > 260 || name.StartsWith('/') || name.Contains('\\', StringComparison.Ordinal) || name.Split('/').Contains(".."))
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The workbook holds a part with an unsafe name and was not read.");

            if (!entries.TryAdd(name, entry))
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The workbook holds the part '{name}' twice and was not read.");

            if (name.EndsWith('/'))
                continue;

            if (entry.Length > limits.MaxZipEntryBytes)
                throw new ImportRefusedException(ImportRefusalCode.ZipBomb, $"A part of the workbook expands to more than {limits.MaxZipEntryBytes / (1024 * 1024)} MB and was not read.");

            total += entry.Length;

            if (total > limits.MaxZipUncompressedBytes)
                throw new ImportRefusedException(ImportRefusalCode.ZipBomb, $"The workbook expands to more than {limits.MaxZipUncompressedBytes / (1024 * 1024)} MB and was not read.");

            if (entry.Length >= limits.ZipRatioFloorBytes && (entry.CompressedLength <= 0 || (double)entry.Length / entry.CompressedLength > limits.MaxZipRatio))
                throw new ImportRefusedException(ImportRefusalCode.ZipBomb, $"The part '{name}' expands {(entry.CompressedLength <= 0 ? "without bound" : $"{(double)entry.Length / entry.CompressedLength:0}-fold")}, which no real workbook does. It was not read.");
        }

        if (total >= limits.ZipRatioFloorBytes && compressedTotal > 0 && (double)total / compressedTotal > limits.MaxZipRatio)
            throw new ImportRefusedException(ImportRefusalCode.ZipBomb, $"The workbook expands {(double)total / compressedTotal:0}-fold, which no real workbook does. It was not read.");

        if (entries.Keys.Any(k => k.StartsWith("xl/externalLinks/", StringComparison.OrdinalIgnoreCase)))
            notices.Add("The workbook links to other workbooks; the links were not followed.");

        if (entries.Keys.Any(k => k.StartsWith("xl/embeddings/", StringComparison.OrdinalIgnoreCase) || k.StartsWith("xl/activeX/", StringComparison.OrdinalIgnoreCase)))
            notices.Add("The workbook holds embedded objects; they were ignored.");

        return entries;
    }

    private static void RefuseMacros(Dictionary<string, ZipArchiveEntry> entries, ImportBudget budget)
    {
        if (entries.Keys.Any(k => k.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || k.Contains("macrosheets/", StringComparison.OrdinalIgnoreCase) || k.EndsWith(".vba", StringComparison.OrdinalIgnoreCase)))
            throw new ImportRefusedException(ImportRefusalCode.Macros, "The workbook carries macros. Save it as a plain .xlsx without macros and import it again.");

        // The package's declarations are read as XML (whatever encoding the part uses), not searched as text: a content type, a part name
        // or a relationship that names a macro project refuses the workbook even when the part itself was renamed.
        foreach (var name in new[] { "[Content_Types].xml", "_rels/.rels", "xl/_rels/workbook.xml.rels" })
        {
            if (!entries.TryGetValue(name, out var declaration))
                continue;

            using var xml = Xml(Open(declaration, budget, 1024 * 1024), budget);

            while (xml.Read())
            {
                budget.Tick();
                CheckDepth(xml, budget);

                if (xml.NodeType != XmlNodeType.Element || !xml.HasAttributes)
                    continue;

                while (xml.MoveToNextAttribute())
                {
                    budget.Tick();
                    var value = xml.Value;

                    if (value.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) || value.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || value.Contains("macroSheet", StringComparison.OrdinalIgnoreCase) || value.Contains("xlMacrosheet", StringComparison.OrdinalIgnoreCase))
                        throw new ImportRefusedException(ImportRefusalCode.Macros, "The workbook is macro-enabled. Save it as a plain .xlsx without macros and import it again.");
                }

                xml.MoveToElement();
            }
        }
    }

    private static void CheckDepth(XmlReader xml, ImportBudget budget)
    {
        if (xml.Depth > budget.Limits.MaxXmlDepth)
            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The workbook nests its XML more than {budget.Limits.MaxXmlDepth} levels deep, which no real workbook does.");
    }

    /// <summary>Skips an element and everything in it, checking the nesting depth and the time as it goes (XmlReader.Skip does neither).</summary>
    private static void SkipSubtree(XmlReader xml, ImportBudget budget)
    {
        if (xml.NodeType != XmlNodeType.Element || xml.IsEmptyElement)
        {
            xml.Read();
            return;
        }

        var depth = xml.Depth;

        while (xml.Read())
        {
            budget.Tick();
            CheckDepth(xml, budget);

            if (xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth)
            {
                xml.Read();
                return;
            }
        }
    }

    private static ZipArchiveEntry Require(Dictionary<string, ZipArchiveEntry> entries, string name) =>
        entries.TryGetValue(name, out var e) ? e : throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The file is not a workbook: '{name}' is missing.");

    private static string ResolveTarget(string target)
    {
        var t = target.Replace('\\', '/');

        if (t.StartsWith('/'))
            return t.TrimStart('/');

        var segments = new List<string> { "xl" };

        foreach (var part in t.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "..")
            {
                if (segments.Count == 0)
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "A workbook part points outside the package.");

                segments.RemoveAt(segments.Count - 1);
            }
            else if (part != ".")
            {
                segments.Add(part);
            }
        }

        return string.Join('/', segments);
    }

    /// <summary>The entry's bytes through a counter that stops at the cap (not at what the zip directory claims).</summary>
    private static Stream Open(ZipArchiveEntry entry, ImportBudget budget, long? cap = null)
    {
        var limit = Math.Min(cap ?? budget.Limits.MaxZipEntryBytes, Math.Max(entry.Length, 0) + 1024);
        return new CountingLimitStream(entry.Open(), limit, entry.FullName, budget);
    }

    private static string ReadAll(ZipArchiveEntry entry, ImportBudget budget, long cap)
    {
        using var s = Open(entry, budget, cap);
        using var reader = new StreamReader(s, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);

        try
        {
            return reader.ReadToEnd();
        }
        catch (DecoderFallbackException)
        {
            throw new ImportRefusedException(ImportRefusalCode.InvalidEncoding, $"The part '{entry.FullName}' is not valid text.");
        }
    }

    private static XmlReader Xml(Stream stream, ImportBudget budget) =>
        XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = budget.Limits.MaxZipEntryBytes,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            CheckCharacters = true,
            CloseInput = true,
        });

    private sealed record DeclaredSheet(string Name, string RelationId, bool Hidden);

    private static List<DeclaredSheet> ReadWorkbook(ZipArchiveEntry entry, ImportBudget budget)
    {
        var sheets = new List<DeclaredSheet>();
        using var xml = Xml(Open(entry, budget), budget);

        while (xml.Read())
        {
            budget.Tick();
            CheckDepth(xml, budget);

            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "sheet" && xml.NamespaceURI == MainNs)
            {
                var name = xml.GetAttribute("name") ?? string.Empty;
                var id = xml.GetAttribute("id", RelNs) ?? string.Empty;
                var state = xml.GetAttribute("state") ?? "visible";

                if (name.Length is 0 or > 255 || id.Length == 0)
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The workbook declares a sheet without a name or relationship.");

                sheets.Add(new DeclaredSheet(name, id, state != "visible"));

                if (sheets.Count > 255)
                    throw new ImportRefusedException(ImportRefusalCode.TooManyRows, "The workbook has more than 255 sheets.");
            }
        }

        return sheets;
    }

    private static Dictionary<string, string> ReadRelationships(ZipArchiveEntry entry, ImportBudget budget)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        using var xml = Xml(Open(entry, budget), budget);

        while (xml.Read())
        {
            budget.Tick();
            CheckDepth(xml, budget);

            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Relationship" && xml.NamespaceURI == PackageRelNs)
            {
                if (string.Equals(xml.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))
                    continue;

                var id = xml.GetAttribute("Id");
                var target = xml.GetAttribute("Target");

                if (id is not null && target is not null)
                    map[id] = target;
            }
        }

        return map;
    }

    private static List<string> ReadSharedStrings(ZipArchiveEntry entry, ImportBudget budget)
    {
        var strings = new List<string>();
        using var xml = Xml(Open(entry, budget), budget);

        while (!xml.EOF)
        {
            budget.Tick();
            CheckDepth(xml, budget);

            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "si" && xml.NamespaceURI == MainNs)
            {
                if (strings.Count >= budget.Limits.MaxSharedStrings)
                    throw new ImportRefusedException(ImportRefusalCode.TooManyRows, "The workbook has too many distinct strings.");

                strings.Add(ReadStringItem(xml, budget));
            }
            else
            {
                xml.Read();
            }
        }

        return strings;
    }

    /// <summary>
    /// The text of an &lt;si&gt; or &lt;is&gt; element: every &lt;t&gt; of its runs, leaving out phonetic hints. The reader is
    /// on the start tag when called and just past the end tag when it returns.
    /// </summary>
    private static string ReadStringItem(XmlReader xml, ImportBudget budget)
    {
        if (xml.IsEmptyElement)
        {
            xml.Read();
            return string.Empty;
        }

        var depth = xml.Depth;
        var sb = new StringBuilder();
        xml.Read();

        while (!(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
        {
            if (xml.EOF)
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The workbook ends inside a string.");

            budget.Tick();
            CheckDepth(xml, budget);

            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "t")
            {
                sb.Append(xml.ReadElementContentAsString());

                if (sb.Length > budget.Limits.MaxFieldChars)
                    throw new ImportRefusedException(ImportRefusalCode.FieldTooLong, $"A cell holds more than {budget.Limits.MaxFieldChars} characters.");
            }
            else if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "rPh")
            {
                SkipSubtree(xml, budget);
            }
            else
            {
                xml.Read();
            }
        }

        xml.Read();
        budget.ChargeChars(sb.Length);
        return sb.ToString();
    }

    private static List<XlsxRow> ReadSheet(ZipArchiveEntry entry, List<string> shared, ImportBudget budget, ref int cellCount, ref int formulaCount)
    {
        var limits = budget.Limits;
        var rows = new List<XlsxRow>();
        using var xml = Xml(Open(entry, budget), budget);

        var rowNumber = 0;
        List<XlsxCell>? cells = null;
        var nextColumn = 0;

        var seenColumns = new HashSet<int>();

        while (!xml.EOF)
        {
            budget.Tick();
            CheckDepth(xml, budget);

            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "row" && xml.NamespaceURI == MainNs)
            {
                var r = xml.GetAttribute("r");
                rowNumber = r is not null && int.TryParse(r, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 1 and <= 1_048_576 ? parsed : rowNumber + 1;

                if (rows.Count >= limits.MaxRows)
                    throw new ImportRefusedException(ImportRefusalCode.TooManyRows, $"A sheet has more than {limits.MaxRows} rows.");

                nextColumn = 0;
                seenColumns.Clear();

                if (xml.IsEmptyElement)
                    rows.Add(new XlsxRow(rowNumber, []));
                else
                    cells = [];

                xml.Read();
            }
            else if (xml.NodeType == XmlNodeType.EndElement && xml.LocalName == "row" && cells is not null)
            {
                rows.Add(new XlsxRow(rowNumber, cells));
                cells = null;
                xml.Read();
            }
            else if (cells is not null && xml.NodeType == XmlNodeType.Element && xml.LocalName == "c" && xml.NamespaceURI == MainNs)
            {
                var reference = xml.GetAttribute("r");
                int column;

                if (reference is not null)
                {
                    var parsed = ParseReference(reference) ?? throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"'{Clip(reference)}' is not a cell reference.");
                    column = parsed.Column;
                }
                else
                {
                    column = nextColumn + 1;
                }

                if (column > limits.MaxColumns)
                    throw new ImportRefusedException(ImportRefusalCode.TooManyColumns, $"A sheet uses more than {limits.MaxColumns} columns.");

                nextColumn = column;

                if (!seenColumns.Add(column))
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The cell {ColumnName(column)}{rowNumber} appears twice in its row, so the sheet has no single value for it.");

                if (++cellCount > limits.MaxCells)
                    throw new ImportRefusedException(ImportRefusalCode.TooManyRows, $"The workbook has more than {limits.MaxCells} cells.");

                var cell = ReadCell(xml, column, xml.GetAttribute("t") ?? "n", shared, budget);

                if (cell.IsFormula)
                    formulaCount++;

                if (cell.Kind != XlsxCellKind.Empty || cell.IsFormula)
                    cells.Add(cell);
            }
            else
            {
                xml.Read();
            }
        }

        return rows;
    }

    /// <summary>Reads one &lt;c&gt;: the reader is on its start tag when called and just past its end tag when it returns.</summary>
    private static XlsxCell ReadCell(XmlReader xml, int column, string type, List<string> shared, ImportBudget budget)
    {
        if (xml.IsEmptyElement)
        {
            xml.Read();
            return new XlsxCell(column, string.Empty, XlsxCellKind.Empty, false);
        }

        var depth = xml.Depth;
        string? value = null;
        string? inline = null;
        var formula = false;
        xml.Read();

        while (!(xml.NodeType == XmlNodeType.EndElement && xml.Depth == depth))
        {
            if (xml.EOF)
                throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "The workbook ends inside a cell.");

            budget.Tick();
            CheckDepth(xml, budget);

            if (xml.NodeType != XmlNodeType.Element)
            {
                xml.Read();
                continue;
            }

            switch (xml.LocalName)
            {
                case "f":
                    // The formula text is skipped, never read: it is not evaluated and not stored.
                    formula = true;
                    SkipSubtree(xml, budget);
                    break;

                case "v":
                    value = xml.ReadElementContentAsString();

                    if (value.Length > budget.Limits.MaxFieldChars)
                        throw new ImportRefusedException(ImportRefusalCode.FieldTooLong, $"A cell holds more than {budget.Limits.MaxFieldChars} characters.");

                    budget.ChargeChars(value.Length);
                    break;

                case "is":
                    inline = ReadStringItem(xml, budget);
                    break;

                default:
                    SkipSubtree(xml, budget);
                    break;
            }
        }

        xml.Read();
        return Finish(column, type, value, inline, formula, shared);
    }

    private static XlsxCell Finish(int column, string type, string? value, string? inline, bool formula, List<string> shared)
    {
        switch (type)
        {
            case "s":
                if (value is null)
                    return new XlsxCell(column, string.Empty, XlsxCellKind.Empty, formula);

                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index >= shared.Count)
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "A cell points at a shared string that does not exist.");

                return new XlsxCell(column, shared[index], shared[index].Length == 0 ? XlsxCellKind.Empty : XlsxCellKind.Text, formula);

            case "inlineStr":
                return new XlsxCell(column, inline ?? string.Empty, string.IsNullOrEmpty(inline) ? XlsxCellKind.Empty : XlsxCellKind.Text, formula);

            case "str":
            case "d":
                return new XlsxCell(column, value ?? string.Empty, string.IsNullOrEmpty(value) ? XlsxCellKind.Empty : XlsxCellKind.Text, formula);

            case "b":
                return value is null ? new XlsxCell(column, string.Empty, XlsxCellKind.Empty, formula) : new XlsxCell(column, value.Trim() == "1" ? "true" : "false", XlsxCellKind.Boolean, formula);

            case "e":
                return new XlsxCell(column, value ?? "#ERROR", XlsxCellKind.Error, formula);

            default:
                if (string.IsNullOrWhiteSpace(value))
                    return new XlsxCell(column, string.Empty, XlsxCellKind.Empty, formula);

                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, "A numeric cell holds something that is not a finite number.");

                if (LocaleNumber.UnderflowedToZero(value, number))
                    throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"A numeric cell holds '{Clip(value.Trim())}', which is too small to represent and would be read as 0.");

                return new XlsxCell(column, value.Trim(), XlsxCellKind.Number, formula);
        }
    }

    private static string Clip(string s) => s.Length > 80 ? s[..80] + "…" : s;

    /// <summary>A read-only stream that fails the import once more bytes have come out than the cap allows.</summary>
    private sealed class CountingLimitStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _limit;
        private readonly string _name;
        private readonly ImportBudget _budget;
        private long _read;
        private int _tagState;
        private int _tagBytes;
        private byte _quote;

        private const int TagText = 0;
        private const int TagOpened = 1;
        private const int TagInside = 2;
        private const int TagSkip = 3;

        /// <summary>The longest XML start tag read; <c>&lt;c r="A1" s="3" t="s"&gt;</c> is 20 bytes, so this is generous by three orders of magnitude.</summary>
        internal const int MaxStartTagBytes = 64 * 1024;

        public CountingLimitStream(Stream inner, long limit, string name, ImportBudget budget)
        {
            _inner = inner;
            _limit = limit;
            _name = name;
            _budget = budget;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Count(_inner.Read(buffer, offset, count));
            ScanTags(buffer.AsSpan(offset, n));
            return n;
        }

        public override int Read(Span<byte> buffer)
        {
            var n = Count(_inner.Read(buffer));
            ScanTags(buffer[..n]);
            return n;
        }

        private int Count(int n)
        {
            _read += n;
            _budget.ChargeBytes(n);

            if (_read > _limit)
                throw new ImportRefusedException(ImportRefusalCode.ZipBomb, $"The part '{_name}' expands past its declared size, so the workbook was not read.");

            // XmlReader parses every attribute of a start tag inside one Read() call, where neither the time budget nor Cancel is looked at. Looking here, on every
            // chunk it pulls, keeps a long tag interruptible; the tag length cap below bounds how much one call can take (Addendum B review, SEC-B-03).
            _budget.Check();
            return n;
        }

        /// <summary>
        /// Follows the bytes for the start tags only: a tag that runs past <see cref="MaxStartTagBytes"/> is refused before the XML reader sees it. A
        /// quoted attribute value may hold <c>&gt;</c> but never <c>&lt;</c>, so tracking the quote state is exact for a well-formed tag, and any
        /// <c>&lt;</c> starts a fresh tag, so a malformed one cannot hide the next. Comments, CDATA and processing instructions (<c>&lt;!</c>, <c>&lt;?</c>)
        /// carry no attributes and are skipped to their <c>&gt;</c>. Only ASCII bytes are examined, so multi-byte UTF-8 text cannot be mistaken for markup.
        /// </summary>
        private void ScanTags(ReadOnlySpan<byte> data)
        {
            var i = 0;

            while (i < data.Length)
            {
                if (_tagState == TagText)
                {
                    var at = data[i..].IndexOf((byte)'<');

                    if (at < 0)
                        return;

                    i += at + 1;
                    _tagState = TagOpened;
                    _tagBytes = 1;
                    continue;
                }

                var b = data[i++];

                if (b == (byte)'<')
                {
                    _tagState = TagOpened;
                    _tagBytes = 1;
                    _quote = 0;
                    continue;
                }

                switch (_tagState)
                {
                    case TagOpened:
                        _quote = 0;
                        _tagBytes = 2;
                        _tagState = b is (byte)'!' or (byte)'?' ? TagSkip : b == (byte)'>' ? TagText : TagInside;
                        break;

                    case TagSkip:
                        if (b == (byte)'>')
                            _tagState = TagText;

                        break;

                    default:
                        if (++_tagBytes > MaxStartTagBytes)
                            throw new ImportRefusedException(ImportRefusalCode.MalformedStructure, $"The part '{_name}' holds an XML start tag longer than {MaxStartTagBytes / 1024} KB, which no real workbook has, so the workbook was not read.");

                        if (_quote != 0)
                        {
                            if (b == _quote)
                                _quote = 0;
                        }
                        else if (b is (byte)'"' or (byte)'\'')
                        {
                            _quote = b;
                        }
                        else if (b == (byte)'>')
                        {
                            _tagState = TagText;
                        }

                        break;
                }
            }
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();

            base.Dispose(disposing);
        }
    }
}
