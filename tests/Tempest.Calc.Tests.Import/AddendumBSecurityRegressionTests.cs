using System.Diagnostics;
using System.Text;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Import;

/// <summary>
/// Regression tests for the Addendum B security review (SEC-B-01, SEC-B-03, SEC-B-04): each exploit the reviewer built is rebuilt here
/// and must be refused or reported, never accepted silently. SEC-B-02 (the material licence lock) is in the Library tests.
/// </summary>
public sealed class AddendumBSecurityRegressionTests
{
    private static CadParseResult Csv(string csv, CadSourceFormat format, NumberProfile number) =>
        CadImporter.Parse(Samples.Text(csv, "p.csv"), format, new CadParseSettings { Number = number });

    // ---- SEC-B-01: W1 CSV, a decimal comma or thousands separator split by the comma delimiter -----------------

    [Theory]
    [InlineData("name,value,\nL,12,5\n", "comma")]
    [InlineData("name,value,\nP,1,250\n", "point")]
    [InlineData("name,value,description\nL,12,5\nW,3,0\n", "comma")]
    [InlineData("name,value,unit,description\nL,12,5\n", "comma")]
    [InlineData("name,value,description\nL,1,012\n", "point")]
    [InlineData("name,value,description\nL,12,5\nW,3.5,ok\n", "point")]
    public void SEC_B_01_A_Split_Decimal_Is_Refused_Not_Read_As_A_Whole_Number(string csv, string mark)
    {
        var number = mark == "comma" ? NumberProfile.Comma : NumberProfile.Point;

        foreach (var format in new[] { CadSourceFormat.GenericCsv, CadSourceFormat.OnshapeCsv })
        {
            var ex = Assert.Throws<ImportRefusedException>(() => Csv(csv, format, number));
            Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
            Assert.Contains("Nothing was imported", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SEC_B_01_An_Ambiguous_Whole_Number_File_Is_Blocked_Until_Corrected()
    {
        // Nothing here proves a split, but every value is a whole number with digits right of it: the mapper must refuse to use it.
        var r = Csv("name,value,description\nL,12,5\nW,3,9\n", CadSourceFormat.GenericCsv, NumberProfile.Point);
        Assert.Contains(r.Notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("decimal comma", StringComparison.Ordinal));

        // A lone whole number whose unit column holds bare digits is the same thing.
        var unit = Csv("name,value,unit,description\nL,12,5\n", CadSourceFormat.GenericCsv, NumberProfile.Point);
        Assert.Contains(unit.Notices, n => n.Severity == CalcNoticeSeverity.Block);

        // One such value among values that are not all whole numbers is reported, not blocked.
        var mixed = Csv("name,value,description\nL,12,5\nW,2e1,z\n", CadSourceFormat.GenericCsv, NumberProfile.Point);
        Assert.Contains(mixed.Notices, n => n.Severity == CalcNoticeSeverity.Warning && n.Message.Contains("decimal comma", StringComparison.Ordinal));
        Assert.DoesNotContain(mixed.Notices, n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Theory]
    [InlineData("name,value,description\nL,12,steel\nW,3,\n", "point")]
    [InlineData("name,value,unit,description\nL,12,mm,5\n", "point")]
    [InlineData("name,value,description\nL,\"12,5\",x\n", "comma")]
    [InlineData("name,value\nL,12\nW,3\n", "point")]
    [InlineData("name,value,description\nL,12\n", "point")]
    [InlineData("name;value;description\nL;12,5;x\n", "comma")]
    public void SEC_B_01_Honest_Files_Are_Still_Read_Without_A_Block(string csv, string mark)
    {
        var r = Csv(csv, CadSourceFormat.GenericCsv, mark == "comma" ? NumberProfile.Comma : NumberProfile.Point);
        Assert.NotEmpty(r.Parameters);
        Assert.DoesNotContain(r.Notices, n => n.Severity == CalcNoticeSeverity.Block);
    }

    // ---- P-B-01 (Purple): a decimal comma with a comma delimiter makes every unquoted number ambiguous -------

    [Theory]
    [InlineData("name,value,description\nL,12,5 mm\n", CadSourceFormat.GenericCsv)]
    [InlineData("name,expression,value,description\n#w,40,5 mm,\n", CadSourceFormat.OnshapeCsv)]
    [InlineData("name,expression,value,description\n#w,40,5,\n", CadSourceFormat.OnshapeCsv)]
    [InlineData("name,value,expression\nL,12,5\n", CadSourceFormat.GenericCsv)]
    [InlineData("name,value,text\nL,12,5\n", CadSourceFormat.GenericCsv)]
    [InlineData("name,value\nL,12\n", CadSourceFormat.GenericCsv)]
    [InlineData("name,value\nL,1e3\n", CadSourceFormat.GenericCsv)]
    public void P_B_01_Under_A_Decimal_Comma_An_Unquoted_Number_With_A_Comma_Delimiter_Is_Refused(string csv, CadSourceFormat format)
    {
        var ex = Assert.Throws<ImportRefusedException>(() => Csv(csv, format, NumberProfile.Comma));
        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("quotes", ex.Message, StringComparison.Ordinal);
        Assert.Contains("semicolon", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Nothing was imported", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("name,value,description\nL,12,5 mm\n", CadSourceFormat.GenericCsv)]
    [InlineData("name,expression,value,description\n#w,40,5 mm,\n", CadSourceFormat.OnshapeCsv)]
    [InlineData("name,expression,value,description\n#w,40,5,\n", CadSourceFormat.OnshapeCsv)]
    [InlineData("name,value,expression\nL,12,5\n", CadSourceFormat.GenericCsv)]
    [InlineData("name,value,text\nL,12,5\n", CadSourceFormat.GenericCsv)]
    public void P_B_01_Under_A_Decimal_Point_The_Same_Shapes_Are_Blocked_Not_Read_Silently(string csv, CadSourceFormat format)
    {
        var r = Csv(csv, format, NumberProfile.Point);
        Assert.Contains(r.Notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("decimal comma", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("name,value,unit\nL,\"12,5\",mm\n", CadSourceFormat.GenericCsv)]
    [InlineData("name,expression,value,description\n#w,\"40,5 mm\",,\n", CadSourceFormat.OnshapeCsv)]
    [InlineData("name;value;unit\nL;12,5;mm\nW;3;mm\n", CadSourceFormat.GenericCsv)]
    [InlineData("name\tvalue\tunit\nL\t12,5\tmm\nW\t3\tmm\n", CadSourceFormat.GenericCsv)]
    public void P_B_01_Quoted_Semicolon_And_Tab_Files_Are_Still_Read_Under_A_Decimal_Comma(string csv, CadSourceFormat format)
    {
        var r = Csv(csv, format, NumberProfile.Comma);
        Assert.NotEmpty(r.Parameters);
        Assert.DoesNotContain(r.Notices, n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void P_B_03_The_Message_Names_The_Column_The_Remainder_Landed_In()
    {
        // A quoted whole number is not ambiguous by itself, but a digit field right of it is still a split.
        var ex = Assert.Throws<ImportRefusedException>(() => Csv("name,value,rev\nL,\"12\",5\n", CadSourceFormat.GenericCsv, NumberProfile.Comma));
        Assert.Contains("'rev' column (which is not read)", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("unnamed column", ex.Message, StringComparison.Ordinal);

        var unit = Assert.Throws<ImportRefusedException>(() => Csv("name,value,unit\nL,\"12\",5\n", CadSourceFormat.GenericCsv, NumberProfile.Comma));
        Assert.Contains("'unit' column", unit.Message, StringComparison.Ordinal);

        var point = Csv("name,value,rev\nL,12,5\n", CadSourceFormat.GenericCsv, NumberProfile.Point);
        Assert.Contains(point.Notices, n => n.Message.Contains("'rev' column (which is not read)", StringComparison.Ordinal));
    }

    // ---- SEC-B-03: one XML start tag with millions of attributes ----------------------------------------------

    private static string Flood(int attributes, string value)
    {
        var sb = new StringBuilder("<row><c ");

        for (var i = 0; i < attributes; i++)
            sb.Append('a').Append(i).Append("=\"").Append(value).Append("\" ");

        return sb.Append("><v>1</v></c></row>").ToString();
    }

    [Theory]
    [InlineData("")]
    [InlineData(">")]      // '>' is legal inside a quoted value: it must not end the tag for the counter
    public void SEC_B_03_A_Start_Tag_With_Millions_Of_Attributes_Is_Refused_Fast_Not_Parsed_In_One_Uninterruptible_Read(string value)
    {
        var bytes = Samples.Workbook(Flood(1_500_000, value));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var budget = new ImportBudget(ImportLimits.Default with { Timeout = TimeSpan.FromSeconds(1) }, cts.Token);
        var clock = Stopwatch.StartNew();

        var ex = Assert.Throws<ImportRefusedException>(() => XlsxReader.Read(bytes, budget));

        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("start tag", ex.Message, StringComparison.Ordinal);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"took {clock.Elapsed.TotalSeconds:0.0} s");
    }

    [Fact]
    public void SEC_B_03_A_Tag_Just_Over_The_Cap_Is_Refused_And_One_Just_Under_It_Is_Read()
    {
        // 64 KB of attributes: refused. A few hundred attributes (a real element never has more than a dozen) are still read, and their cell with them.
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => XlsxReader.Read(Samples.Workbook(Flood(10_000, "x"))));

        var ok = XlsxReader.Read(Samples.Workbook(Flood(300, "x")));
        Assert.Single(ok.Sheets[0].Rows);
    }

    [Fact]
    public void SEC_B_03_Long_Text_Comments_And_Attribute_Values_Are_Not_Mistaken_For_Tags()
    {
        var longText = new string('x', 4_000);
        var sheet = $"<row><c t=\"inlineStr\"><is><t>{longText}</t></is></c><c a=\"1&gt;2\" t=\"inlineStr\"><is><t>b</t></is></c></row><!-- {new string('c', 100_000)} --><row><c><v>2</v></c></row>";
        var book = XlsxReader.Read(Samples.Workbook(sheet));
        Assert.Equal(2, book.Sheets[0].Rows.Count);
    }

    [Fact]
    public void SEC_B_03_Cancel_Is_Honoured_While_The_Reader_Pulls_Bytes_Of_One_Tag()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var bytes = Samples.Workbook(Flood(2_000, "x"));
        Assert.ThrowsAny<OperationCanceledException>(() => XlsxReader.Read(bytes, new ImportBudget(ImportLimits.Default, cts.Token)));
    }

    // ---- SEC-B-04: an underflowing literal is not read as zero ------------------------------------------------

    [Theory]
    [InlineData("1e-400", "point")]
    [InlineData("-2e-999", "point")]
    [InlineData("1,5e-400", "comma")]
    [InlineData("0.0001e-320", "point")]
    [InlineData("0.000000000000000000001e-310", "point")]
    public void SEC_B_04_A_Non_Zero_Literal_That_Underflows_To_Zero_Is_Refused_Like_An_Overflow(string text, string mark)
    {
        var profile = mark == "comma" ? NumberProfile.Comma : NumberProfile.Point;
        Assert.False(LocaleNumber.TryParse(text, profile, out var value, out var problem));
        Assert.Equal(0, value);
        Assert.Contains("too small", problem, StringComparison.Ordinal);
        Assert.False(LocaleNumber.TryParse("1e400", NumberProfile.Point, out _, out var overflow));
        Assert.Contains("outside the range", overflow, StringComparison.Ordinal);
        Assert.Throws<ImportRefusedException>(() => LocaleNumber.Parse(text, profile));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0.0", 0)]
    [InlineData("-0", 0)]
    [InlineData("0e-400", 0)]
    [InlineData("000.000e5", 0)]
    [InlineData("1e-300", 1e-300)]
    [InlineData("5e-324", double.Epsilon)]
    public void SEC_B_04_Zero_And_Representable_Small_Numbers_Are_Still_Read(string text, double expected)
    {
        Assert.True(LocaleNumber.TryParse(text, NumberProfile.Point, out var value, out _));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void SEC_B_04_An_Underflowing_Value_In_A_Cad_Csv_Is_Not_Imported_As_A_Number()
    {
        var r = Csv("name,value\ntiny,1e-400\nzero,0\n", CadSourceFormat.GenericCsv, NumberProfile.Point);
        Assert.Null(r.Parameters.Single(p => p.Name == "tiny").Number);
        Assert.Equal(0, r.Parameters.Single(p => p.Name == "zero").Number);
    }

    // ---- P-B-02 (Purple): workbook cells and JSON numbers do not underflow to zero either ----------------------

    private static byte[] NxWorkbook(string number) => Samples.Workbook(
        "<row><c t=\"inlineStr\"><is><t>Name</t></is></c><c t=\"inlineStr\"><is><t>Value</t></is></c></row>" +
        $"<row><c t=\"inlineStr\"><is><t>gap</t></is></c><c><v>{number}</v></c></row>");

    [Theory]
    [InlineData("1E-400")]
    [InlineData("-2.5e-999")]
    [InlineData("0.0001E-320")]
    public void P_B_02_A_Workbook_Cell_That_Underflows_To_Zero_Is_Refused(string number)
    {
        var ex = Assert.Throws<ImportRefusedException>(() => XlsxReader.Read(NxWorkbook(number)));
        Assert.Contains("too small", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ImportRefusedException>(() => CadImporter.Parse(Samples.Bytes(NxWorkbook(number), "t.xlsx"), CadSourceFormat.NxXlsx));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0E-400")]
    [InlineData("1E-300")]
    [InlineData("12.5")]
    public void P_B_02_Zero_And_Representable_Workbook_Cells_Are_Still_Read(string number)
    {
        var r = CadImporter.Parse(Samples.Bytes(NxWorkbook(number), "t.xlsx"), CadSourceFormat.NxXlsx);
        Assert.Equal(double.Parse(number, System.Globalization.CultureInfo.InvariantCulture), r.Parameters.Single(p => p.Name == "gap").Number);
    }

    [Theory]
    [InlineData("1e-400")]
    [InlineData("-3E-999")]
    [InlineData("0.5e-330")]
    public void P_B_02_A_Json_Number_That_Underflows_To_Zero_Is_Refused(string number)
    {
        var json = $"[{{\"name\":\"gap\",\"value\":{number},\"unit\":\"mm\"}}]";
        var ex = Assert.Throws<ImportRefusedException>(() => CadImporter.Parse(Samples.Text(json, "p.json"), CadSourceFormat.GenericJson));
        Assert.Equal(ImportRefusalCode.NumberFormat, ex.Code);
        Assert.Contains("too small", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("0e-400", 0)]
    [InlineData("1e-300", 1e-300)]
    public void P_B_02_Zero_And_Representable_Json_Numbers_Are_Still_Read(string number, double expected)
    {
        var json = $"[{{\"name\":\"gap\",\"value\":{number},\"unit\":\"mm\"}}]";
        var r = CadImporter.Parse(Samples.Text(json, "p.json"), CadSourceFormat.GenericJson);
        Assert.Equal(expected, r.Parameters.Single().Number);
    }
}
