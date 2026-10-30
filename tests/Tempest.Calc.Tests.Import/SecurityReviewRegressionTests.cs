using System.Diagnostics;
using System.Text;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;

namespace Tempest.Calc.Tests.Import;

/// <summary>
/// Regression tests for the Opus security review of W1 (H1-H3, M1, M3, M5, M6, L1-L6): each exploit the reviewer built is rebuilt here
/// in code and must now be refused (or read correctly, or reported), never accepted silently.
/// </summary>
public sealed class SecurityReviewRegressionTests
{
    private const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static byte[] U(string s) => new UTF8Encoding(false).GetBytes(s);

    /// <summary>A workbook with the given sheets (name, relationship id, target) and parts; all sheets' relationships are declared.</summary>
    private static byte[] MultiSheet(IReadOnlyList<(string Name, string Target)> sheets, IEnumerable<(string Name, byte[] Data)> parts, string? sharedStrings = null)
    {
        var workbook = $"<?xml version=\"1.0\"?><workbook xmlns=\"{MainNs}\" xmlns:r=\"{RelNs}\"><sheets>{string.Concat(sheets.Select((s, i) => $"<sheet name=\"{s.Name}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>"))}</sheets></workbook>";
        var rels = $"<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{string.Concat(sheets.Select((s, i) => $"<Relationship Id=\"rId{i + 1}\" Type=\"worksheet\" Target=\"{s.Target}\"/>"))}</Relationships>";
        var all = new List<(string Name, byte[] Data)>
        {
            ("[Content_Types].xml", U("<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>")),
            ("xl/workbook.xml", U(workbook)),
            ("xl/_rels/workbook.xml.rels", U(rels)),
        };
        all.AddRange(parts);

        if (sharedStrings is not null)
            all.Add(("xl/sharedStrings.xml", U(sharedStrings)));

        return Samples.Zip(all);
    }

    private static (string, byte[]) Sheet(string name, string sheetData) =>
        ($"xl/worksheets/{name}.xml", U($"<?xml version=\"1.0\"?><worksheet xmlns=\"{MainNs}\"><sheetData>{sheetData}</sheetData></worksheet>"));

    private static string Inline(int row, params string[] cells) =>
        $"<row r=\"{row}\">{string.Concat(cells.Select((c, i) => double.TryParse(c, System.Globalization.CultureInfo.InvariantCulture, out _) ? $"<c r=\"{XlsxReader.ColumnName(i + 1)}{row}\"><v>{c}</v></c>" : $"<c r=\"{XlsxReader.ColumnName(i + 1)}{row}\" t=\"inlineStr\"><is><t>{c}</t></is></c>"))}</row>";

    // ---- H1: the archive limits cannot be multiplied by sharing a part -------------

    [Fact]
    public void H1_Many_Sheets_Pointing_At_One_Part_Are_Refused_Before_Any_Is_Parsed()
    {
        var part = Sheet("sheet1", Inline(1, "a", "1"));
        var sheets = Enumerable.Range(1, 255).Select(i => ($"S{i}", "worksheets/sheet1.xml")).ToList();
        var ex = Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(MultiSheet(sheets, [part])));
        Assert.Contains("same part", ex.Message, StringComparison.Ordinal);

        // Two names for one part by different spellings of the same path are the same part.
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(MultiSheet([("A", "worksheets/sheet1.xml"), ("B", "worksheets/../worksheets/sheet1.xml")], [part])));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(MultiSheet([("A", "worksheets/sheet1.xml"), ("B", "/xl/worksheets/SHEET1.xml")], [part])));
    }

    [Fact]
    public void H1_Only_The_Chosen_Sheet_Is_Parsed_So_A_Workbook_Of_Many_Sheets_Costs_One()
    {
        var good = Sheet("sheet1", Inline(1, "a", "1"));
        var broken = ("xl/worksheets/sheet2.xml", U("<<< not xml"));
        var sheets = new List<(string, string)> { ("First", "worksheets/sheet1.xml"), ("Second", "worksheets/sheet2.xml") };
        var bytes = MultiSheet(sheets, [good, broken]);

        var book = XlsxReader.Read(bytes);
        Assert.Equal("First", Assert.Single(book.Sheets).Name);
        Assert.Equal(["First", "Second"], book.SheetNames);

        // Asking for the other sheet reads it (and meets its damage); a name that is not there says which are.
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(bytes, null, "Second"));
        Assert.Contains("First", Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(bytes, null, "Nope")).Message, StringComparison.Ordinal);
        Assert.Equal("First", XlsxReader.Read(bytes, null, "first").Sheets[0].Name);
    }

    [Fact]
    public void H1_One_Running_Byte_Counter_Is_Charged_By_Every_Part_That_Is_Read()
    {
        var bytes = Samples.Workbook("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>");
        var limits = new ImportLimits { MaxZipUncompressedBytes = 4000 };
        XlsxReader.Read(bytes, new ImportBudget(limits));

        var spent = new ImportBudget(limits);
        spent.ChargeBytes(3990);
        Samples.Refuses(ImportRefusalCode.ZipBomb, () => XlsxReader.Read(bytes, spent));
        Samples.Refuses(ImportRefusalCode.ZipBomb, () => new ImportBudget(new ImportLimits { MaxZipUncompressedBytes = 100 }).ChargeBytes(101));
    }

    [Fact]
    public void H1_The_Text_A_Workbook_May_Keep_In_Memory_Is_Capped_Across_Everything_Read()
    {
        var strings = Samples.Sst(Enumerable.Range(0, 20).Select(i => new string('x', 40)).ToArray());
        var cells = string.Concat(Enumerable.Range(1, 20).Select(i => $"<row r=\"{i}\"><c r=\"A{i}\" t=\"s\"><v>{i - 1}</v></c></row>"));
        var bytes = Samples.Workbook(cells, strings);

        XlsxReader.Read(bytes, new ImportBudget(new ImportLimits { MaxRetainedChars = 1000 }));
        Samples.Refuses(ImportRefusalCode.TooLarge, () => XlsxReader.Read(bytes, new ImportBudget(new ImportLimits { MaxRetainedChars = 500 })));

        var inline = string.Concat(Enumerable.Range(1, 20).Select(i => $"<row r=\"{i}\"><c r=\"A{i}\" t=\"inlineStr\"><is><t>{new string('y', 40)}</t></is></c></row>"));
        Samples.Refuses(ImportRefusalCode.TooLarge, () => XlsxReader.Read(Samples.Workbook(inline), new ImportBudget(new ImportLimits { MaxRetainedChars = 500 })));
    }

    // ---- M1: nesting depth ----------------------------------------------------------

    private static string Nest(int depth, string inner = "") => string.Concat(Enumerable.Repeat("<a>", depth)) + inner + string.Concat(Enumerable.Repeat("</a>", depth));

    [Fact]
    public void M1_Deeply_Nested_Xml_Is_Refused_Wherever_It_Hides()
    {
        var deep = 200;
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook($"<row r=\"1\"><c r=\"A1\"><f>{Nest(deep)}</f><v>1</v></c></row>")));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook($"<row r=\"1\"><c r=\"A1\">{Nest(deep)}<v>1</v></c></row>")));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook($"{Nest(deep)}<row r=\"1\"/>")));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row>", $"<?xml version=\"1.0\"?><sst xmlns=\"{MainNs}\"><si><rPh>{Nest(deep)}</rPh><t>x</t></si></sst>")));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"/>", extra: [("xl/_rels/workbook.xml.rels", U($"<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{Nest(deep)}</Relationships>"))])));
    }

    [Fact]
    public void M1_A_Realistic_Depth_Is_Still_Read()
    {
        var book = XlsxReader.Read(Samples.Workbook($"<row r=\"1\"><c r=\"A1\"><f>{Nest(30)}</f><v>7</v></c></row>"));
        Assert.Equal("7", book.Sheets[0].At(1, 1)!.Text);
    }

    [Fact]
    public void M1_A_Big_Deeply_Nested_Part_Costs_Almost_Nothing_Before_It_Is_Refused()
    {
        // Megabytes of nesting that does not compress past the ratio test: refused as soon as the depth passes 64, long before the part has been read.
        var bomb = $"<row r=\"1\"><c r=\"A1\"><f>{string.Concat(Enumerable.Range(0, 400_000).Select(i => $"<a x=\"{(i * 7919) % 100003}\">"))}</f></c></row>";
        var bytes = Samples.Workbook(bomb);
        Assert.True(bytes.Length < ImportLimits.DefaultMaxBytes);
        var before = GC.GetTotalAllocatedBytes(true);
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(bytes));
        Assert.True(GC.GetTotalAllocatedBytes(true) - before < 100_000_000);
    }

    // ---- M3: duplicates in a workbook header ------------------------------------------

    [Fact]
    public void M3_A_Repeated_Header_Column_Or_A_Repeated_Text_In_A_Row_Is_A_Refusal_Or_A_Result_Never_An_Internal_Error()
    {
        var header = Samples.Workbook(Inline(1, "Name", "Value", "Unit", "Value") + Inline(2, "len", "12", "mm", "13"));
        var ex = Samples.Refuses(ImportRefusalCode.MalformedStructure, () => CadImporter.Parse(Samples.Bytes(header, "x.xlsx"), CadSourceFormat.NxXlsx));
        Assert.Contains("repeats the column 'value'", ex.Message, StringComparison.Ordinal);

        // A headerless first row with a repeated text ('mm' twice) is read positionally.
        var data = Samples.Workbook(Inline(1, "len", "12", "mm", "mm"));
        var result = CadImporter.Parse(Samples.Bytes(data, "x.xlsx"), CadSourceFormat.NxXlsx);
        Assert.Equal(12, result.Parameters.Single(p => p.Name == "len").Number);
        Assert.Equal("mm", result.Parameters.Single(p => p.Name == "len").Unit);
    }

    // ---- L2: duplicate cells and duplicate JSON keys -----------------------------------

    [Fact]
    public void L2_A_Cell_That_Appears_Twice_In_A_Row_Is_Refused_Not_Resolved_By_Taking_The_First()
    {
        var ex = Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"2\"><c r=\"B2\"><v>12</v></c><c r=\"B2\"><v>99</v></c></row>")));
        Assert.Contains("B2", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"parameters\":[{\"name\":\"a\",\"value\":12,\"value\":99}]}")]
    [InlineData("{\"parameters\":[{\"name\":\"a\",\"name\":\"b\",\"value\":12}]}")]
    [InlineData("{\"parameters\":[],\"parameters\":[{\"name\":\"a\",\"value\":1}]}")]
    public void L2_A_Json_Object_With_The_Same_Key_Twice_Is_Refused(string json) =>
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => CadImporter.Parse(Samples.Text(json, "x.json"), CadSourceFormat.GenericJson));

    [Fact]
    public void L2_A_Profile_With_A_Repeated_Key_Is_Refused()
    {
        const string Json = "{\"id\":\"p\",\"version\":1,\"title\":\"t\",\"entries\":[{\"cadName\":\"a\",\"target\":\"W\"}],\"entries\":[{\"cadName\":\"b\",\"target\":\"F\"}]}";
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => CadMappingProfileJson.Read(Json));
    }

    // ---- L1: macros in other encodings and renamed parts --------------------------------

    [Fact]
    public void L1_A_Macro_Declaration_In_A_Utf16_Content_Types_Part_Is_Found_And_A_Renamed_Part_Does_Not_Hide_It()
    {
        const string Types = "<?xml version=\"1.0\" encoding=\"UTF-16\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/xl/hidden.bin\" ContentType=\"application/vnd.ms-office.vbaProject\"/></Types>";
        var utf16 = Samples.Workbook("<row r=\"1\"/>", extra: [("[Content_Types].xml", Encoding.Unicode.GetBytes(Types)), ("xl/hidden.bin", [1, 2, 3])]);
        Samples.Refuses(ImportRefusalCode.Macros, () => XlsxReader.Read(utf16));

        var viaRelationship = "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId9\" Type=\"http://schemas.microsoft.com/office/2006/relationships/vbaProject\" Target=\"hidden.bin\"/></Relationships>";
        Samples.Refuses(ImportRefusalCode.Macros, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"/>", extra: [("xl/_rels/workbook.xml.rels", U(viaRelationship))])));
        Samples.Refuses(ImportRefusalCode.Macros, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"/>", extra: [("xl/VBAProject.BIN", [1])])));
    }

    // ---- H3: ragged rows (the decimal comma and thousands-separator trap) ------------------

    [Theory]
    [InlineData("name,value\nL,12,5\n", "Comma")]
    [InlineData("name,value\nP,1,250\n", "Point")]
    [InlineData("name,unit,value\nL,mm,12,5\n", "Comma")]
    [InlineData("name,value,unit\nL,12,5,mm\n", "Comma")]
    public void H3_A_Row_With_More_Fields_Than_The_Header_Is_Refused_And_Names_Its_Line(string csv, string profile)
    {
        var settings = new CadParseSettings { Number = profile == "Comma" ? NumberProfile.Comma : NumberProfile.Point };
        var ex = Samples.Refuses(ImportRefusalCode.MalformedStructure, () => CadImporter.Parse(Samples.Text(csv, "x.csv"), CadSourceFormat.GenericCsv, settings));
        Assert.Contains("Line 2 has", ex.Message, StringComparison.Ordinal);
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => CadImporter.Parse(Samples.Text(csv, "x.csv"), CadSourceFormat.OnshapeCsv, settings));
    }

    [Fact]
    public void H3_The_Same_Number_In_Quotes_Is_Read_As_It_Was_Meant()
    {
        var comma = CadImporter.Parse(Samples.Text("name,unit,value\nL,mm,\"12,5\"\n", "x.csv"), CadSourceFormat.GenericCsv, new CadParseSettings { Number = NumberProfile.Comma });
        Assert.Equal(12.5, comma.Parameters[0].Number);

        // A short row (fewer fields than the header) is still fine: the missing fields are empty.
        var short_ = CadImporter.Parse(Samples.Text("name,value,unit,description\nL,12\n", "x.csv"), CadSourceFormat.GenericCsv);
        Assert.Equal(12, short_.Parameters[0].Number);
    }

    // ---- M6 and L5: how a number was read ------------------------------------------------

    [Fact]
    public void M6_A_Value_Read_With_A_Declared_Thousands_Separator_Carries_A_Warning_Naming_The_Result()
    {
        var grouped = new CadParseSettings { Number = new NumberProfile(DecimalMark.Comma, '.') };
        var read = CadImporter.Parse(Samples.Text("name;value;unit\nd1;1.500;mm\nd2;1,5;mm\n", "x.csv"), CadSourceFormat.GenericCsv, grouped);
        Assert.Equal(1500, read.Parameters.Single(p => p.Name == "d1").Number);
        Assert.Equal(1.5, read.Parameters.Single(p => p.Name == "d2").Number);
        var warning = Assert.Single(read.Notices, n => n.Message.Contains("d1", StringComparison.Ordinal));
        Assert.Contains("thousands separator", warning.Message, StringComparison.Ordinal);
        Assert.Contains("1500", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(read.Notices, n => n.Message.Contains("d2", StringComparison.Ordinal));

        var point = CadImporter.Parse(Samples.Text("name,value\nd1,\"1,250\"\n", "x.csv"), CadSourceFormat.GenericCsv, new CadParseSettings { Number = new NumberProfile(DecimalMark.Point, ',') });
        Assert.Equal(1250, point.Parameters[0].Number);
        Assert.Contains(point.Notices, n => n.Message.Contains("thousands separator", StringComparison.Ordinal));
    }

    [Fact]
    public void L5_A_Declared_Unit_That_Disagrees_With_The_Unit_Written_On_The_Value_Is_Reported()
    {
        var r = CadImporter.Parse(Samples.Text("[MilliMeter]len=12 in\n[mm]ok=12 mm\n[mm]plain=12\n", "x.exp"), CadSourceFormat.NxExpressions);
        Assert.Equal("in", r.Parameters.Single(p => p.Name == "len").Unit);
        Assert.Contains(r.Notices, n => n.Message.Contains("len", StringComparison.Ordinal) && n.Message.Contains("differ", StringComparison.Ordinal));
        Assert.DoesNotContain(r.Notices, n => n.Message.Contains("ok:", StringComparison.Ordinal));
        Assert.Single(r.Notices);
    }

    // ---- L4: encoding oddities ------------------------------------------------------------

    [Fact]
    public void L4_A_Doubled_Byte_Order_Mark_Does_Not_Poison_The_First_Header()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, 0xEF, 0xBB, 0xBF }.Concat(U("name,value\nx,1\n")).ToArray();
        Assert.Equal(1, CadImporter.Parse(Samples.Bytes(bytes, "x.csv"), CadSourceFormat.GenericCsv).Parameters[0].Number);
    }

    [Theory]
    [InlineData("\u202E")]
    [InlineData("\u202A")]
    [InlineData("\u2066")]
    [InlineData("\u200F")]
    [InlineData("\uFEFF")]
    public void L4_Invisible_Direction_Changing_Characters_Are_Refused_In_The_Text_Itself(string character) =>
        Samples.Refuses(ImportRefusalCode.ControlCharacter, () => BoundedInputReader.DecodeText(Samples.Text("name,value\nx" + character + "y,1\n")));

    // ---- M5: paths ----------------------------------------------------------------------------

    [Theory]
    [InlineData("COM¹")]
    [InlineData("dir/LPT².txt")]
    [InlineData("com³.exp")]
    public void M5_The_Superscript_Forms_Of_The_Reserved_Device_Names_Are_Reserved(string path) =>
        Assert.Equal(ImportRefusalCode.UnsafePath, Assert.Throws<ImportRefusedException>(() => ImportPath.Canonicalise(path)).Code);

    [Fact]
    public void M5_A_Device_Path_Written_With_A_Dot_Segment_Is_Still_A_Device_Path()
    {
        if (OperatingSystem.IsWindows())
            return;

        foreach (var path in new[] { "/./dev/zero", "/dev/./tty", "/tmp/../dev/null", "/proc/./self/environ" })
            Assert.Equal(ImportRefusalCode.UnsafePath, Assert.Throws<ImportRefusedException>(() => ImportPath.Canonicalise(path)).Code);
    }

    [Fact]
    public async Task M5_A_Folder_That_Is_A_Link_Cannot_Carry_A_Path_Out_Of_The_Root()
    {
        var parent = Path.Combine(Path.GetTempPath(), "w1-escape-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "root");
        var outside = Path.Combine(parent, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(outside, "secret.exp"), "[mm]a=1\n");

            try
            {
                Directory.CreateSymbolicLink(Path.Combine(root, "link"), outside);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return; // links need a privilege this machine does not give; the Linux CI leg runs this.
            }

            Samples.Refuses(ImportRefusalCode.UnsafePath, () => ImportPath.Canonicalise("link/secret.exp", root));
            var ex = await Assert.ThrowsAsync<ImportRefusedException>(() => BoundedInputReader.ReadFileAsync("link/secret.exp", root));
            Assert.Equal(ImportRefusalCode.UnsafePath, ex.Code);

            // A real folder inside the root is fine, and so is a link that stays inside.
            Directory.CreateDirectory(Path.Combine(root, "real"));
            await File.WriteAllTextAsync(Path.Combine(root, "real", "ok.exp"), "[mm]a=1\n");
            Assert.Equal("ok.exp", (await BoundedInputReader.ReadFileAsync("real/ok.exp", root)).FileName);
            Directory.CreateSymbolicLink(Path.Combine(root, "alias"), Path.Combine(root, "real"));
            Assert.Equal("ok.exp", (await BoundedInputReader.ReadFileAsync("alias/ok.exp", root)).FileName);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task M5_A_Named_Pipe_Does_Not_Hold_The_Caller_Past_The_Time_Limit()
    {
        if (OperatingSystem.IsWindows())
            return;

        var folder = Path.Combine(Path.GetTempPath(), "w1-fifo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var fifo = Path.Combine(folder, "pipe.exp");

        try
        {
            using (var make = Process.Start(new ProcessStartInfo("mkfifo", fifo) { RedirectStandardError = true, UseShellExecute = false }))
            {
                if (make is null)
                    return;

                await make.WaitForExitAsync();

                if (make.ExitCode != 0)
                    return; // no mkfifo here
            }

            var limits = new ImportLimits { Timeout = TimeSpan.FromMilliseconds(600) };
            var watch = Stopwatch.StartNew();
            var ex = await Assert.ThrowsAsync<ImportRefusedException>(() => BoundedInputReader.ReadFileAsync(fifo, folder, limits));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(6), $"took {watch.Elapsed}");
            Assert.True(ex.Code is ImportRefusalCode.Timeout or ImportRefusalCode.UnsafePath, ex.Code.ToString());

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // Residual: the kind of the file is read before any open, so the pipe is refused at once and nothing is left blocked on it.
                Assert.Equal(ImportRefusalCode.UnsafePath, ex.Code);
                Assert.Contains("regular file", ex.Message, StringComparison.Ordinal);
                Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(500), $"took {watch.Elapsed}");
                Assert.False(FileKind.IsRegularFile(fifo));
                Assert.True(FileKind.IsRegularFile(Path.Combine(folder)) == false);
                Assert.False(FileKind.IsRegularFile("/dev/null"));
            }
            else
            {
                await using var writer = new FileStream(fifo, FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 1, true);
            }
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task Residual_A_Regular_File_Is_Told_From_A_Pipe_Without_Opening_It()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        var file = Path.Combine(Path.GetTempPath(), "w1-kind-" + Guid.NewGuid().ToString("N") + ".exp");
        await File.WriteAllTextAsync(file, "[mm]a=1\n");

        try
        {
            Assert.True(FileKind.IsRegularFile(file));
            Assert.Null(FileKind.IsRegularFile(file + ".missing"));
        }
        finally
        {
            File.Delete(file);
        }
    }

    // ---- N2: canonical targets; N5: a byte order mark in front of a profile --------------

    [Theory]
    [InlineData("rows[01].x")]
    [InlineData("rows[007].nominal")]
    [InlineData("rows[00].x")]
    public void N2_A_Row_Index_With_A_Leading_Zero_Is_Not_A_Target(string target)
    {
        Assert.False(CadTarget.TryParse(target, out _));
        var profile = new CadMappingProfile("p", 1, "t", [new("a", target)], CadSystem.Nx, "any");
        Assert.NotEmpty(profile.Validate());
    }

    [Theory]
    [InlineData("rows[0].x")]
    [InlineData("rows[10].x")]
    public void N2_A_Canonical_Row_Index_Is_Kept_And_Prints_As_Written(string target)
    {
        Assert.True(CadTarget.TryParse(target, out var parsed));
        Assert.Equal(target, parsed.ToString());
    }

    [Fact]
    public void N5_A_Profile_File_With_A_Byte_Order_Mark_Is_Read()
    {
        const string Json = "{\"id\":\"p\",\"version\":1,\"title\":\"t\",\"entries\":[{\"cadName\":\"a\",\"target\":\"W\"}]}";
        var profile = CadMappingProfileJson.Read("\uFEFF" + Json);
        Assert.Equal("p", profile.Id);
    }

    [Fact]
    public async Task M5_The_Callers_Cancellation_Still_Applies_To_The_Open_And_The_Read_Of_A_File()
    {
        var file = Path.Combine(Path.GetTempPath(), "w1-cancel-" + Guid.NewGuid().ToString("N") + ".exp");
        await File.WriteAllTextAsync(file, "[mm]a=1\n");

        try
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BoundedInputReader.ReadFileAsync(file, null, null, cts.Token));
            Assert.Equal(8, (await BoundedInputReader.ReadFileAsync(file)).Length);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task M5_A_Cancelled_Read_Does_Not_Leave_The_File_Open_Behind_The_Caller()
    {
        // Windows CI, push run 703: the worker of a cancelled read still held the file, so the caller's delete failed.
        var file = Path.Combine(Path.GetTempPath(), "w1-cancel-held-" + Guid.NewGuid().ToString("N") + ".exp");
        await File.WriteAllTextAsync(file, "[mm]a=1\n");

        try
        {
            for (var i = 0; i < 20; i++)
            {
                using var cts = new CancellationTokenSource();
                await cts.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BoundedInputReader.ReadFileAsync(file, null, null, cts.Token));

                using var exclusive = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Assert.Equal(8, exclusive.Length);
            }
        }
        finally
        {
            File.Delete(file);
        }
    }
}
