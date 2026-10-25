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
}
