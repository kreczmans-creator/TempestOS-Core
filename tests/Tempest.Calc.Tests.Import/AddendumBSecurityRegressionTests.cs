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
}
