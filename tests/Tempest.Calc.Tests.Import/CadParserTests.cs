using System.Text;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Import;

/// <summary>Golden sample files per CAD system (invented parts) and the rules every parser holds: literals become values, expressions stay text.</summary>
public sealed class CadParserTests
{
    private static CadParseResult Parse(string sample, CadSourceFormat format, CadParseSettings? settings = null) =>
        CadImporter.Parse(Samples.Load(sample), format, settings);

    private static CadParameter Get(CadParseResult r, string name) => r.Parameters.Single(p => p.Name == name);

    private static void AssertQuantity(CadParseResult r, string name, double value, string? unit)
    {
        var p = Get(r, name);
        Assert.Equal(CadParameterKind.Number, p.Kind);
        Assert.Equal(value, p.Number!.Value, 12);
        Assert.Equal(unit, p.Unit);
    }

    // ---- NX .exp -------------------------------------------------------

    [Fact]
    public void Nx_Exp_Golden_Sample_Literals_Become_Values_With_Units()
    {
        var r = Parse("nx/bracket-a.exp", CadSourceFormat.NxExpressions);

        Assert.Equal(CadSystem.Nx, r.Source.System);
        Assert.Equal("NX 2306", r.Source.CadVersion);
        Assert.Equal("BRACKET-A", r.Source.PartName);
        Assert.Equal("B", r.Source.PartRevision);
        Assert.Equal(64, r.Source.Sha256.Length);
        Assert.Equal("bracket-a.exp", r.Source.FileName);

        AssertQuantity(r, "plate_t", 6, "mm");
        AssertQuantity(r, "plate_w", 40.5, "mm");
        AssertQuantity(r, "hole_d", 8.5, "mm");
        AssertQuantity(r, "bend_angle", 90, "deg");
        AssertQuantity(r, "gap_min", 0.2, "mm");
        AssertQuantity(r, "count", 4, null);
        AssertQuantity(r, "slot_l", 12, "mm");
        AssertQuantity(r, "mass_est", 0.35, "kg");

        Assert.Equal("S275", Get(r, "material").Text);
        Assert.Equal(CadParameterKind.Text, Get(r, "material").Kind);
        Assert.True(Get(r, "with_rib").Flag);
        Assert.Equal(CadParameterKind.Boolean, Get(r, "with_rib").Kind);
    }

    [Fact]
    public void Nx_Exp_Expressions_Are_Kept_As_Read_Only_Text_And_Never_Evaluated()
    {
        var r = Parse("nx/bracket-a.exp", CadSourceFormat.NxExpressions);

        foreach (var (name, source) in new[] { ("flange_w", "plate_w*2"), ("edge", "(plate_w-hole_d)/2"), ("diag", "sqrt(plate_t^2+9)") })
        {
            var p = Get(r, name);
            Assert.Equal(CadParameterKind.Expression, p.Kind);
            Assert.Equal(source, p.Text);
            Assert.Null(p.Number);
            Assert.Null(p.Flag);
        }

        Assert.Equal("mm", Get(r, "edge").RawUnit);
    }

    [Fact]
    public void An_Unknown_Unit_Leaves_The_Value_Unmappable_With_A_Reason()
    {
        var p = Get(Parse("nx/bracket-a.exp", CadSourceFormat.NxExpressions), "odd_unit_len");
        Assert.Equal(10, p.Number);
        Assert.Null(p.Unit);
        Assert.Equal("furlongs", p.RawUnit);
        Assert.Contains("furlongs", p.UnitProblem, StringComparison.Ordinal);
    }

    [Fact]
    public void Nx_Exp_Comment_Handling_And_Odd_Lines()
    {
        var file = Samples.Text("// header\n\n[mm]a=1 // trailing\n[mm]b=\"x // not a comment\"\nbroken line\n(Point) p0=Point(1,2,3)\n[mm]c=\n", "x.exp");
        var r = CadImporter.Parse(file, CadSourceFormat.NxExpressions);

        Assert.Equal(1, Get(r, "a").Number);
        Assert.Equal(CadParameterKind.Text, Get(r, "b").Kind);
        Assert.Equal("x // not a comment", Get(r, "b").Text);
        Assert.Equal(CadParameterKind.Expression, Get(r, "p0").Kind);
        Assert.Equal(2, r.Notices.Count(n => n.Severity == CalcNoticeSeverity.Warning));
        Assert.DoesNotContain(r.Parameters, p => p.Name == "c");
    }

    [Fact]
    public void An_Empty_Or_Comment_Only_File_Is_Refused()
    {
        Samples.Refuses(ImportRefusalCode.Empty, () => CadImporter.Parse(Samples.Text("// nothing\n"), CadSourceFormat.NxExpressions));
        Samples.Refuses(ImportRefusalCode.Empty, () => CadImporter.Parse(Samples.Text(""), CadSourceFormat.CreoRelations));
    }

    [Fact]
    public void The_Persons_Entries_Override_What_The_File_Says_About_Itself()
    {
        var r = Parse("nx/bracket-a.exp", CadSourceFormat.NxExpressions, new CadParseSettings { CadVersion = "NX 2312", PartName = "BR-A-Machined", Revision = "D" });
        Assert.Equal("NX 2312", r.Source.CadVersion);
        Assert.Equal("BR-A-Machined", r.Source.PartName);
        Assert.Equal("D", r.Source.PartRevision);
    }

    [Fact]
    public void The_Part_Name_Falls_Back_To_The_File_Name_When_Neither_File_Nor_Person_Gives_One()
    {
        var r = CadImporter.Parse(Samples.Text("[mm]a=1\n", "C:\\work\\widget.exp"), CadSourceFormat.NxExpressions);
        Assert.Equal("widget", r.Source.PartName);
        Assert.Null(r.Source.CadVersion);
    }

    // ---- NX xlsx -------------------------------------------------------

    [Fact]
    public void Nx_Xlsx_Golden_Sample_Reads_Values_And_Keeps_Formulas_Read_Only()
    {
        var r = Parse("nx/bracket-a.xlsx", CadSourceFormat.NxXlsx);

        AssertQuantity(r, "plate_t", 6, "mm");
        AssertQuantity(r, "plate_w", 40.5, "mm");
        AssertQuantity(r, "bend_angle", 90, "deg");
        AssertQuantity(r, "count", 4, null);
        Assert.Equal("S275", Get(r, "material").Text);

        var flange = Get(r, "flange_w");
        Assert.Equal(CadParameterKind.Expression, flange.Kind);
        Assert.Equal("plate_w*2", flange.Text);
        Assert.Null(flange.Number);
        Assert.Equal("81", flange.Reported);

        // gap_max's value cell is a spreadsheet formula (=C2*0.1) saved with the result 0.8: the saved result is read, the formula is not evaluated.
        var gap = Get(r, "gap_max");
        Assert.Equal(0.8, gap.Number);
        Assert.True(gap.FromFormulaCell);
        Assert.Contains(r.Notices, n => n.Message.Contains("no formula was evaluated", StringComparison.Ordinal));
    }

    // ---- Creo ----------------------------------------------------------

    [Fact]
    public void Creo_Golden_Sample_Reads_Relations_And_Parameters()
    {
        var r = Parse("creo/bracket-b.txt", CadSourceFormat.CreoRelations);

        Assert.Equal(CadSystem.Creo, r.Source.System);
        AssertQuantity(r, "d5", 25.4, null);
        AssertQuantity(r, "PLATE_T", 6, null);
        AssertQuantity(r, "MASS", 1.25, "kg");
        AssertQuantity(r, "COUNT", 4, null);
        Assert.Equal("Steel S275", Get(r, "MATERIAL").Text);
        Assert.True(Get(r, "PRODUCTION").Flag);

        Assert.Equal(CadParameterKind.Expression, Get(r, "d6").Kind);
        Assert.Equal("d5 / 2", Get(r, "d6").Text);
    }

    [Fact]
    public void Creo_Assignments_Inside_An_If_Block_Are_Never_Fixed_Values()
    {
        var r = Parse("creo/bracket-b.txt", CadSourceFormat.CreoRelations);
        Assert.Equal(2, r.Parameters.Count(p => p.Name == "d7"));
        Assert.All(r.Parameters.Where(p => p.Name == "d7"), p =>
        {
            Assert.Equal(CadParameterKind.Expression, p.Kind);
            Assert.Contains("IF", p.Comment, StringComparison.Ordinal);
        });
        Assert.True(r.Duplicates.ContainsKey("d7"));
    }

    [Fact]
    public void A_Decimal_Comma_In_A_Decimal_Point_File_Is_Kept_As_An_Expression_With_A_Warning_Never_Read_As_A_Number()
    {
        var r = Parse("creo/bracket-b.txt", CadSourceFormat.CreoRelations);
        var width = Get(r, "WIDTH");
        Assert.Equal(CadParameterKind.Expression, width.Kind);
        Assert.Null(width.Number);
        Assert.Contains(r.Notices, n => n.Message.Contains("WIDTH", StringComparison.Ordinal) && n.Message.Contains("decimal", StringComparison.Ordinal));

        var comma = Parse("creo/bracket-b.txt", CadSourceFormat.CreoRelations, new CadParseSettings { Number = NumberProfile.Comma });
        Assert.Equal(40.5, Get(comma, "WIDTH").Number);
    }

    // ---- SolidWorks ----------------------------------------------------

    [Fact]
    public void SolidWorks_Equations_Golden_Sample()
    {
        var r = Parse("solidworks/bracket-c-equations.txt", CadSourceFormat.SolidWorksEquations);

        Assert.Equal(CadSystem.SolidWorks, r.Source.System);
        AssertQuantity(r, "Plate_W", 40.5, "mm");
        AssertQuantity(r, "Plate_T", 6, "mm");
        AssertQuantity(r, "Hole_D@Sketch1", 8.5, null);
        AssertQuantity(r, "Angle@Sketch1", 90, "deg");
        AssertQuantity(r, "Länge@Skizze1", 120, "mm");
        AssertQuantity(r, "Count", 3, null);

        Assert.Equal(CadParameterKind.Expression, Get(r, "Flange_W@Sketch1").Kind);
        Assert.Equal("\"Plate_W\" * 2", Get(r, "Flange_W@Sketch1").Text);
        Assert.Equal(CadParameterKind.Expression, Get(r, "Edge@Sketch1").Kind);
        Assert.DoesNotContain(r.Parameters, p => p.Text?.Contains("Edge distance", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void SolidWorks_Design_Table_Golden_Sample_Reads_The_First_Configuration_And_Lists_All()
    {
        var r = Parse("solidworks/bracket-design-table.xlsx", CadSourceFormat.SolidWorksDesignTable);

        Assert.Equal("Bracket-A", r.Source.PartName);
        Assert.Equal("Default", r.Source.Configuration);
        Assert.Equal(["Default", "Wide", "Thick"], r.Source.Configurations);

        AssertQuantity(r, "Plate_W@Sketch1", 40.5, null);
        AssertQuantity(r, "Plate_T@Sketch1", 6, null);
        AssertQuantity(r, "Hole_D@Sketch1", 8.5, null);
        Assert.Equal("Bracket", Get(r, "$PRP@Description").Text);

        // The Flange cell is a formula (=B3*2) saved with 81: the saved result.
        var flange = Get(r, "Flange_W@Sketch1");
        Assert.Equal(81, flange.Number);
        Assert.True(flange.FromFormulaCell);
    }

    [Fact]
    public void A_Design_Table_Configuration_Is_Chosen_By_Name_And_An_Unknown_One_Is_Refused()
    {
        var wide = Parse("solidworks/bracket-design-table.xlsx", CadSourceFormat.SolidWorksDesignTable, new CadParseSettings { Configuration = "wide" });
        Assert.Equal("Wide", wide.Source.Configuration);
        AssertQuantity(wide, "Plate_W@Sketch1", 50, null);
        Assert.Equal(100, Get(wide, "Flange_W@Sketch1").Number);

        var ex = Samples.Refuses(ImportRefusalCode.MalformedStructure, () => Parse("solidworks/bracket-design-table.xlsx", CadSourceFormat.SolidWorksDesignTable, new CadParseSettings { Configuration = "Nope" }));
        Assert.Contains("Default", ex.Message, StringComparison.Ordinal);
    }

    // ---- Onshape -------------------------------------------------------

    [Fact]
    public void Onshape_Csv_Golden_Sample()
    {
        var r = Parse("onshape/variables.csv", CadSourceFormat.OnshapeCsv);

        Assert.Equal(CadSystem.Onshape, r.Source.System);
        AssertQuantity(r, "plate_w", 40.5, "mm");
        AssertQuantity(r, "plate_t", 6, "mm");
        AssertQuantity(r, "tilt", 15, "deg");
        AssertQuantity(r, "parts", 4, null);

        var flange = Get(r, "flange");
        Assert.Equal(CadParameterKind.Expression, flange.Kind);
        Assert.Equal("2 * #plate_w", flange.Text);
        Assert.Equal("81 mm", flange.Reported);
        Assert.Equal("Plate width", Get(r, "plate_w").Comment);
    }

    [Fact]
    public void Onshape_Json_Golden_Sample_Reads_Variable_Tables_And_States_Its_Unit_Assumption()
    {
        var r = Parse("onshape/variables.json", CadSourceFormat.OnshapeJson);

        AssertQuantity(r, "plate_w", 40.5, "mm");
        AssertQuantity(r, "plate_t", 6, "mm");
        Assert.Equal(CadParameterKind.Expression, Get(r, "flange").Kind);

        // "tilt" has only a value and a type: read as radians from its type, and the sheet is told.
        var tilt = Get(r, "tilt");
        Assert.Equal("rad", tilt.Unit);
        Assert.Equal(0.2617993877991494, tilt.Number!.Value, 12);
        Assert.Contains(r.Notices, n => n.Message.Contains("tilt", StringComparison.Ordinal) && n.Message.Contains("rad", StringComparison.Ordinal));
    }

    // ---- generic -------------------------------------------------------

    [Fact]
    public void Generic_Csv_Golden_Sample()
    {
        var r = Parse("generic/parameters.csv", CadSourceFormat.GenericCsv);

        Assert.Equal(CadSystem.Generic, r.Source.System);
        AssertQuantity(r, "plate_w", 40.5, "mm");
        AssertQuantity(r, "plate_t", 6, "mm");
        AssertQuantity(r, "force", 12, "kN");
        Assert.Equal("S275", Get(r, "material").Text);
        Assert.Equal(CadParameterKind.Text, Get(r, "material").Kind);
        Assert.True(Get(r, "ribbed").Flag);
        Assert.Equal(CadParameterKind.Expression, Get(r, "flange").Kind);
        Assert.Equal("2 * plate_w", Get(r, "flange").Text);
        AssertQuantity(r, "Straße \"A\"", 7, "mm");
    }

    [Fact]
    public void Generic_Json_Golden_Sample_Reads_The_Schema_Header_Too()
    {
        var r = Parse("generic/parameters.json", CadSourceFormat.GenericJson);

        Assert.Equal("Inventor 2025.1", r.Source.CadVersion);
        Assert.Equal("Bracket-A", r.Source.PartName);
        Assert.Equal("B", r.Source.PartRevision);
        AssertQuantity(r, "plate_w", 40.5, "mm");
        AssertQuantity(r, "plate_t", 6, "mm");
        AssertQuantity(r, "force", 12, "kN");
        Assert.Equal("S275", Get(r, "material").Text);
        Assert.True(Get(r, "ribbed").Flag);
        Assert.Equal(CadParameterKind.Expression, Get(r, "flange").Kind);
        Assert.Equal("Design load", Get(r, "force").Comment);
    }

    [Fact]
    public void A_Bare_Json_List_Is_Read_As_Parameters()
    {
        var r = CadImporter.Parse(Samples.Text("[{\"name\":\"a\",\"value\":2.5,\"unit\":\"m\"},{\"name\":\"b\",\"value\":\"3 mm\"}]", "x.json"), CadSourceFormat.GenericJson);
        AssertQuantity(r, "a", 2.5, "m");
        AssertQuantity(r, "b", 3, "mm");
    }

    // ---- decimal comma -------------------------------------------------

    [Fact]
    public void Decimal_Comma_Csv_Is_Read_Only_Under_A_Decimal_Comma_Profile()
    {
        var r = Parse("generic/decimal-comma.csv", CadSourceFormat.GenericCsv, new CadParseSettings { Number = NumberProfile.Comma });
        AssertQuantity(r, "plate_w", 40.5, "mm");
        AssertQuantity(r, "plate_t", 6.25, "mm");
        AssertQuantity(r, "tilt", 15, "deg");

        // The same file under the default profile never reads "40,5" as 40.5 (or as 405): the values stay text and say why.
        var wrong = Parse("generic/decimal-comma.csv", CadSourceFormat.GenericCsv);
        Assert.All(wrong.Parameters, p => Assert.Null(p.Number));
        Assert.All(wrong.Parameters, p => Assert.NotEqual(CadParameterKind.Number, p.Kind));
    }

    [Fact]
    public void An_Invalid_Number_Format_Is_Refused_Up_Front()
    {
        Samples.Refuses(ImportRefusalCode.NumberFormat, () => CadImporter.Parse(Samples.Text("[mm]a=1\n"), CadSourceFormat.NxExpressions, new CadParseSettings { Number = new NumberProfile(DecimalMark.Comma, ',') }));
    }

    // ---- Unicode and names ---------------------------------------------

    [Fact]
    public void Unicode_Names_Are_Normalised_And_Matched_As_One()
    {
        // "é" as one character and as e + combining acute are the same name once normalised.
        var composed = "caf\u00E9";
        var decomposed = "cafe\u0301";
        var r = CadImporter.Parse(Samples.Text($"[mm]{composed}=1\n[mm]ΔL_é=2\n[mm]長さ=3\n", "u.exp"), CadSourceFormat.NxExpressions);
        Assert.Contains(r.Parameters, p => p.Name == composed);
        Assert.Contains(r.Parameters, p => p.Name == "ΔL_é");
        Assert.Contains(r.Parameters, p => p.Name == "長さ");

        var d = CadImporter.Parse(Samples.Text($"[mm]{decomposed}=1\n", "u.exp"), CadSourceFormat.NxExpressions);
        Assert.Equal(composed, d.Parameters[0].Name);
        Assert.Equal(composed.Length, d.Parameters[0].Name.Length);
    }

    [Theory]
    [InlineData("a\u200Bb")]
    [InlineData("a\u202Eb")]
    [InlineData("a\uFEFFb")]
    [InlineData("a\uE000b")]
    public void A_Name_With_An_Invisible_Or_Direction_Changing_Character_Is_Refused(string name) =>
        Samples.Refuses(ImportRefusalCode.ControlCharacter, () => CadImporter.Parse(Samples.Text($"\"{name}\"= 5\n", "s.txt"), CadSourceFormat.SolidWorksEquations));

    [Fact]
    public void A_Very_Long_Name_And_Too_Many_Parameters_Are_Refused()
    {
        Samples.Refuses(ImportRefusalCode.FieldTooLong, () => CadImporter.Parse(Samples.Text($"\"{new string('n', 200)}\"= 5\n"), CadSourceFormat.SolidWorksEquations));

        var many = string.Concat(Enumerable.Range(0, 5001).Select(i => $"[mm]p{i}=1\n"));
        Samples.Refuses(ImportRefusalCode.TooManyParameters, () => CadImporter.Parse(Samples.Text(many, "m.exp"), CadSourceFormat.NxExpressions));
    }

    // ---- hostile text --------------------------------------------------

    [Fact]
    public void Formula_Looking_Texts_Are_Data_Never_Run_And_Survive_Intact()
    {
        var r = CadImporter.Parse(Samples.Text("name,value\nnote,\"=cmd|' /C calc'!A0\"\nplate,-2+3\n", "x.csv"), CadSourceFormat.GenericCsv);
        Assert.Equal("=cmd|' /C calc'!A0", Get(r, "note").Text);
        Assert.Equal(CadParameterKind.Expression, Get(r, "note").Kind);

        // A CSV value is data: text that is not a number is kept as text, and can never be mapped to a number input.
        Assert.Equal(CadParameterKind.Text, Get(r, "plate").Kind);
        Assert.Null(Get(r, "plate").Number);
    }

    [Fact]
    public void Candidate_Formats_Follow_The_Extension_And_A_Sniff_Of_Text_Files()
    {
        Assert.Equal([CadSourceFormat.NxExpressions], CadImporter.CandidatesFor("a.exp"));
        Assert.Equal(CadSourceFormat.NxXlsx, CadImporter.CandidatesFor("a.XLSX")[0]);
        Assert.Equal(CadSourceFormat.GenericCsv, CadImporter.CandidatesFor("a.csv")[0]);
        Assert.Equal(CadSourceFormat.GenericJson, CadImporter.CandidatesFor("a.json")[0]);
        Assert.Equal(CadSourceFormat.SolidWorksEquations, CadImporter.CandidatesFor("a.txt", Samples.Load("solidworks/bracket-c-equations.txt"))[0]);
        Assert.Equal(CadSourceFormat.CreoRelations, CadImporter.CandidatesFor("a.txt", Samples.Load("creo/bracket-b.txt"))[0]);
        Assert.Empty(CadImporter.CandidatesFor("a.dll"));
    }

    [Fact]
    public void Parsing_Honours_The_Size_Cap_And_The_Callers_Cancellation()
    {
        var limits = new ImportLimits { MaxBytes = 100 };
        Samples.Refuses(ImportRefusalCode.TooLarge, () => CadImporter.Parse(Samples.Text(new string('a', 500)), CadSourceFormat.NxExpressions, new CadParseSettings { Limits = limits }));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var many = string.Concat(Enumerable.Range(0, 2000).Select(i => $"[mm]p{i}=1\n"));
        Assert.ThrowsAny<OperationCanceledException>(() => CadImporter.Parse(Samples.Text(many, "m.exp"), CadSourceFormat.NxExpressions, null, cts.Token));
    }

    [Fact]
    public void Utf8_With_A_Byte_Order_Mark_Is_Read_The_Same()
    {
        var plain = Encoding.UTF8.GetBytes("[mm]a=1\n");
        var bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(plain).ToArray();
        Assert.Equal(1, CadImporter.Parse(Samples.Bytes(bom, "a.exp"), CadSourceFormat.NxExpressions).Parameters[0].Number);
    }
}
