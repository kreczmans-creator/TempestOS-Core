using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Import;

/// <summary>Mapping profiles: typed targets, unit-dimension checks, refusals, unmapped and expression reporting, provenance, diff.</summary>
public sealed class MappingTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 25, 9, 30, 15, TimeSpan.Zero);

    private static CalcTemplate Template { get; } = new(
        "test.cad-target", "1.0.0", CalcDiscipline.Mechanical, "Target", "scope", "method",
        [
            new CalcInputSpec("W", "Width", CalcInputKind.Quantity, "mm", Default: 10),
            new CalcInputSpec("F", "Force", CalcInputKind.Quantity, "kN", Default: 1),
            new CalcInputSpec("alpha", "Angle", CalcInputKind.Quantity, "deg", Default: 0),
            new CalcInputSpec("eta", "Efficiency", CalcInputKind.Number, Default: 0.9),
            new CalcInputSpec("n", "Count", CalcInputKind.Number, Default: 1),
            new CalcInputSpec("grade", "Grade", CalcInputKind.Choice, Choices: ["S235", "S275", "S355"], DefaultChoice: "S275"),
            new CalcInputSpec("ribbed", "Ribbed", CalcInputKind.Boolean),
            new CalcInputSpec("note", "Note", CalcInputKind.Text, IsOptional: true),
            new CalcInputSpec(
                "rows", "Rows", CalcInputKind.Table,
                Columns:
                [
                    new CalcColumnSpec("name", "Name", CalcInputKind.Text),
                    new CalcColumnSpec("nominal", "Nominal", CalcInputKind.Quantity, "mm"),
                    new CalcColumnSpec("flag", "Flag", CalcInputKind.Boolean, IsOptional: true),
                    new CalcColumnSpec("kind", "Kind", CalcInputKind.Choice, Choices: ["size", "position"], IsOptional: true),
                ],
                MaxRows: 10),
        ],
        [], [], [], []);

    private static IReadOnlyList<CalcInputValue> Current() =>
    [
        CalcInputValue.Quantity("W", 10, "mm"),
        CalcInputValue.Quantity("F", 1, "kN"),
        CalcInputValue.Quantity("alpha", 0, "deg"),
        CalcInputValue.Of("eta", 0.9),
        CalcInputValue.Of("n", 1),
        CalcInputValue.Choice("grade", "S275"),
        CalcInputValue.Switch("ribbed", false),
        CalcInputValue.Table("rows",
        [
            CalcRow.Of(("name", new CalcCell(Text: "plate")), ("nominal", new CalcCell(5, "mm"))),
            CalcRow.Of(("name", new CalcCell(Text: "gap")), ("nominal", new CalcCell(2, "mm"))),
        ]),
    ];

    private static CadParseResult Csv(string text, CadSystem system = CadSystem.Generic) =>
        CadImporter.Parse(Samples.Text(text, "p.csv"), system == CadSystem.Onshape ? CadSourceFormat.OnshapeCsv : CadSourceFormat.GenericCsv);

    private static CadMappingProfile Profile(params CadMappingEntry[] entries) => new("test-profile", 3, "Test profile", entries);

    private static CadMappingEntry E(string name, string target, string? AssumedUnit = null, bool Required = false) => new(name, target, AssumedUnit, Required);

    private static CadImportedParameter P(string name, CadParameterKind kind, string? target = null, double? number = null, string? unit = null, double? si = null, string? text = null, bool? flag = null) =>
        new(name, kind, target, number, unit, si, text, flag);

    private static CadMappingResult Map(CadParseResult parse, CadMappingProfile profile, IReadOnlyList<CalcInputValue>? current = null) =>
        CadMapper.Map(parse, profile, Template, current ?? Current(), At);

    private static CadMappedRow Row(CadMappingResult r, string name) => r.Rows.First(x => x.CadName == name);

    // ---- the happy path ------------------------------------------------

    [Fact]
    public void Mapped_Values_Replace_The_Inputs_Carry_Their_Unit_And_Their_Provenance()
    {
        var parse = Csv("name,value,unit\nplate_w,40.5,mm\nforce,12,kN\nmaterial,S355,\nribbed,true,\nplate_t,6,mm\n");
        var result = Map(parse, Profile(E("plate_w", "W"), E("force", "F"), E("material", "grade"), E("ribbed", "ribbed")));

        Assert.True(result.CanApply, string.Join(" ", result.Refusals));
        var w = result.Inputs.Single(i => i.Symbol == "W");
        Assert.Equal(40.5, w.Number);
        Assert.Equal("mm", w.Unit);
        Assert.Equal(12, result.Inputs.Single(i => i.Symbol == "F").Number);
        Assert.Equal("S355", result.Inputs.Single(i => i.Symbol == "grade").Text);
        Assert.True(result.Inputs.Single(i => i.Symbol == "ribbed").Flag);

        var info = CadImportProvenance.TryParse(w.Provenance);
        Assert.NotNull(info);
        Assert.Equal("CAD", info.SystemAndVersion);
        Assert.Equal("plate_w", info.CadName);
        Assert.Equal(parse.Source.Sha256, info.FileSha256);
        Assert.Equal("test-profile", info.ProfileId);
        Assert.Equal(3, info.ProfileVersion);

        // Inputs the profile does not reach keep their values and provenance.
        Assert.Null(result.Inputs.Single(i => i.Symbol == "alpha").Provenance);
        Assert.Equal(0, result.Inputs.Single(i => i.Symbol == "alpha").Number);
    }

    [Fact]
    public void A_Name_In_The_File_But_Not_In_The_Profile_Is_Reported_Never_Silently_Dropped()
    {
        var parse = Csv("name,value,unit,expression\nplate_w,40.5,mm,\nplate_t,6,mm,\nflange,,,2 * plate_w\n");
        var result = Map(parse, Profile(E("plate_w", "W")));

        Assert.Equal(CadRowStatus.Mapped, Row(result, "plate_w").Status);
        Assert.Equal(CadRowStatus.Unmapped, Row(result, "plate_t").Status);
        Assert.Contains("not imported", Row(result, "plate_t").Message, StringComparison.Ordinal);
        Assert.Equal(CadRowStatus.Expression, Row(result, "flange").Status);
        Assert.Contains("never evaluated", Row(result, "flange").Message, StringComparison.Ordinal);
        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(3, result.Snapshot.Count);
        Assert.True(result.CanApply);
    }

    [Fact]
    public void A_Name_In_The_Profile_But_Not_In_The_File_Is_Reported_And_A_Required_One_Refuses()
    {
        var parse = Csv("name,value,unit\nplate_w,40.5,mm\n");
        var optional = Map(parse, Profile(E("plate_w", "W"), E("force", "F")));
        Assert.Equal(CadRowStatus.Missing, Row(optional, "force").Status);
        Assert.True(optional.CanApply);

        var required = Map(parse, Profile(E("plate_w", "W"), E("force", "F", Required: true)));
        Assert.False(required.CanApply);
        Assert.Contains("requires 'force'", string.Join(" ", required.Refusals), StringComparison.Ordinal);
    }

    [Fact]
    public void A_File_Matching_Nothing_In_The_Profile_Imports_Nothing()
    {
        var result = Map(Csv("name,value\nzzz,1\n"), Profile(E("plate_w", "W")));
        Assert.False(result.CanApply);
        Assert.Contains("nothing to import", string.Join(" ", result.Refusals), StringComparison.Ordinal);
    }

    [Fact]
    public void Names_Match_Exactly_Unless_The_Profile_Ignores_Case()
    {
        var parse = Csv("name,value,unit\nPLATE_W,40.5,mm\n");
        Assert.False(Map(parse, Profile(E("plate_w", "W"))).CanApply);
        Assert.True(Map(parse, Profile(E("plate_w", "W")) with { IgnoreCase = true }).CanApply);
    }

    // ---- units ---------------------------------------------------------

    [Fact]
    public void A_Value_In_Another_Unit_Of_The_Same_Dimension_Is_Converted_To_The_Inputs_Unit()
    {
        var result = Map(Csv("name,value,unit\nlen,1,in\nload,2500,N\n"), Profile(E("len", "W"), E("load", "F")));
        Assert.Equal(25.4, result.Inputs.Single(i => i.Symbol == "W").Number!.Value, 9);
        Assert.Equal("mm", result.Inputs.Single(i => i.Symbol == "W").Unit);
        Assert.Equal(2.5, result.Inputs.Single(i => i.Symbol == "F").Number!.Value, 12);
    }

    [Fact]
    public void The_Unit_The_Person_Chose_For_An_Input_Is_Kept_When_It_Is_Of_The_Right_Kind()
    {
        var current = Current().Select(i => i.Symbol == "W" ? CalcInputValue.Quantity("W", 0.4, "in") : i).ToList();
        var result = Map(Csv("name,value,unit\nlen,25.4,mm\n"), Profile(E("len", "W")), current);
        var w = result.Inputs.Single(i => i.Symbol == "W");
        Assert.Equal("in", w.Unit);
        Assert.Equal(1.0, w.Number!.Value, 9);
    }

    [Fact]
    public void A_Unit_Of_A_Different_Dimension_Is_Refused_With_A_Message_And_Nothing_Is_Applied()
    {
        var parse = Csv("name,value,unit\nplate_w,40.5,mm\nload,12,N\n");
        var result = Map(parse, Profile(E("plate_w", "W"), E("load", "W2")) with { Entries = [E("plate_w", "W"), E("load", "alpha")] });

        Assert.False(result.CanApply);
        var refused = Row(result, "load");
        Assert.Equal(CadRowStatus.Refused, refused.Status);
        Assert.Contains("'N'", refused.Message, StringComparison.Ordinal);
        Assert.Contains("not the same kind of quantity", refused.Message, StringComparison.Ordinal);

        // The good row is shown as mapped, but the result is not applicable and the inputs it would give are those it was given.
        Assert.Equal(CadRowStatus.Mapped, Row(result, "plate_w").Status);
        Assert.False(result.CanApply);
    }

    [Fact]
    public void A_Length_Into_A_Force_Input_Is_Refused()
    {
        var result = Map(Csv("name,value,unit\nlen,40.5,mm\n"), Profile(E("len", "F")));
        Assert.False(result.CanApply);
        Assert.Contains("takes kN", Row(result, "len").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Value_With_No_Unit_Needs_An_Assumed_Unit_In_The_Profile()
    {
        var parse = Csv("name,value\nlen,40.5\n");
        var without = Map(parse, Profile(E("len", "W")));
        Assert.False(without.CanApply);
        Assert.Contains("has no unit", Row(without, "len").Message, StringComparison.Ordinal);

        var with = Map(parse, Profile(E("len", "W", AssumedUnit: "mm")));
        Assert.True(with.CanApply);
        Assert.Equal(40.5, with.Inputs.Single(i => i.Symbol == "W").Number);
        Assert.Contains("assumed", Row(with, "len").Value, StringComparison.Ordinal);

        var inches = Map(parse, Profile(E("len", "W", AssumedUnit: "in")));
        Assert.Equal(40.5 * 25.4, inches.Inputs.Single(i => i.Symbol == "W").Number!.Value, 9);

        var wrong = Map(parse, Profile(E("len", "W", AssumedUnit: "N")));
        Assert.False(wrong.CanApply);
    }

    [Fact]
    public void An_Assumed_Unit_Never_Overrides_A_Unit_The_File_States()
    {
        var result = Map(Csv("name,value,unit\nlen,1,in\n"), Profile(E("len", "W", AssumedUnit: "mm")));
        Assert.Equal(25.4, result.Inputs.Single(i => i.Symbol == "W").Number!.Value, 9);
    }

    [Fact]
    public void Angles_Are_Not_Confused_With_Ratios_Or_Counts()
    {
        // 15 deg and 0.2617993877991494 rad both land in a degree input as 15.
        var ok = Map(Csv("name,value,unit\na,0.2617993877991494,rad\n"), Profile(E("a", "alpha")));
        Assert.Equal(15, ok.Inputs.Single(i => i.Symbol == "alpha").Number!.Value, 9);

        // An angle in a plain-number input, a percentage in an angle input and an unlabelled number in an angle input are refused.
        Assert.False(Map(Csv("name,value,unit\na,15,deg\n"), Profile(E("a", "n"))).CanApply);
        Assert.False(Map(Csv("name,value,unit\na,15,%\n"), Profile(E("a", "alpha"))).CanApply);
        var unlabelled = Map(Csv("name,value\na,15\n"), Profile(E("a", "alpha")));
        Assert.False(unlabelled.CanApply);
        Assert.Contains("angle", Row(unlabelled, "a").Message, StringComparison.Ordinal);
        Assert.True(Map(Csv("name,value\na,15\n"), Profile(E("a", "alpha", AssumedUnit: "deg"))).CanApply);
    }

    [Fact]
    public void A_Percentage_Is_A_Fraction_And_A_Length_In_A_Plain_Number_Input_Is_Refused()
    {
        var pct = Map(Csv("name,value,unit\ne,90,%\n"), Profile(E("e", "eta")));
        Assert.Equal(0.9, pct.Inputs.Single(i => i.Symbol == "eta").Number!.Value, 12);

        var plain = Map(Csv("name,value\nc,4\n"), Profile(E("c", "n")));
        Assert.Equal(4, plain.Inputs.Single(i => i.Symbol == "n").Number);
        Assert.Null(plain.Inputs.Single(i => i.Symbol == "n").Unit);

        var length = Map(Csv("name,value,unit\nc,4,mm\n"), Profile(E("c", "n")));
        Assert.False(length.CanApply);
        Assert.Contains("plain number", Row(length, "c").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Unit_The_Engine_Does_Not_Know_Is_Refused_Not_Guessed()
    {
        var result = Map(Csv("name,value,unit\nlen,10,furlongs\n"), Profile(E("len", "W")));
        Assert.False(result.CanApply);
        Assert.Contains("furlongs", Row(result, "len").Message, StringComparison.Ordinal);
    }

    // ---- kinds ---------------------------------------------------------

    [Fact]
    public void An_Expression_Can_Never_Be_Mapped_To_An_Input_As_A_Number()
    {
        var parse = Csv("name,expression,unit\nflange,2 * plate_w,mm\n");
        var result = Map(parse, Profile(E("flange", "W")));

        Assert.False(result.CanApply);
        Assert.Equal(CadRowStatus.Refused, Row(result, "flange").Status);
        Assert.Contains("never evaluated", Row(result, "flange").Message, StringComparison.Ordinal);
        Assert.Equal(10, result.Inputs.Single(i => i.Symbol == "W").Number); // unchanged
    }

    [Fact]
    public void A_Text_Cannot_Fill_A_Number_And_A_Number_Cannot_Fill_A_Text_Or_A_Yes_No()
    {
        Assert.False(Map(Csv("name,value\nx,S275\n"), Profile(E("x", "W"))).CanApply);
        Assert.False(Map(Csv("name,value\nx,5\n"), Profile(E("x", "note"))).CanApply);
        Assert.False(Map(Csv("name,value\nx,5\n"), Profile(E("x", "ribbed"))).CanApply);
        Assert.False(Map(Csv("name,value\nx,true\n"), Profile(E("x", "W"))).CanApply);
        Assert.True(Map(Csv("name,value\nx,Weld note\n"), Profile(E("x", "note"))).CanApply);
    }

    [Fact]
    public void A_Choice_Takes_Only_A_Listed_Value_And_Keeps_The_Listed_Spelling()
    {
        var ok = Map(Csv("name,value\ng,s355\n"), Profile(E("g", "grade")));
        Assert.Equal("S355", ok.Inputs.Single(i => i.Symbol == "grade").Text);

        var bad = Map(Csv("name,value\ng,S999\n"), Profile(E("g", "grade")));
        Assert.False(bad.CanApply);
        Assert.Contains("S235", Row(bad, "g").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Text_Longer_Than_The_Cap_Is_Refused()
    {
        var result = Map(Csv($"name,value\nx,{new string('a', 300)}\n"), Profile(E("x", "note")));
        Assert.False(result.CanApply);
        Assert.Contains("200", Row(result, "x").Message, StringComparison.Ordinal);
    }

    // ---- targets -------------------------------------------------------

    [Fact]
    public void A_Cell_Of_An_Existing_Table_Row_Takes_The_Value_With_Provenance_And_Other_Cells_Are_Kept()
    {
        var result = Map(Csv("name,value,unit\nt,7.5,mm\nk,position,\nf,true,\n"), Profile(E("t", "rows[1].nominal"), E("k", "rows[0].kind"), E("f", "rows[0].flag")));

        Assert.True(result.CanApply, string.Join(" ", result.Refusals));
        var table = result.Inputs.Single(i => i.Symbol == "rows");
        Assert.Equal(7.5, table.Rows![1].Get("nominal")!.Number);
        Assert.Equal("mm", table.Rows[1].Get("nominal")!.Unit);
        Assert.Equal("gap", table.Rows[1].Get("name")!.Text);
        Assert.Equal(5, table.Rows[0].Get("nominal")!.Number);
        Assert.Equal("position", table.Rows[0].Get("kind")!.Text);
        Assert.True(table.Rows[0].Get("flag")!.Flag);
        Assert.NotNull(CadImportProvenance.TryParse(table.Rows[1].Get("nominal")!.Provenance));
    }

    [Theory]
    [InlineData("rows[5].nominal", "no row 5")]
    [InlineData("rows[0].nope", "no column 'nope'")]
    [InlineData("rows", "map a cell")]
    [InlineData("W[0].x", "not a table")]
    [InlineData("missing", "not an input")]
    [InlineData("rows[0].name", "text input")]
    public void A_Target_That_Does_Not_Exist_Or_Does_Not_Fit_Is_Refused_With_A_Message(string target, string fragment)
    {
        var result = Map(Csv("name,value,unit\nx,7.5,mm\n"), Profile(E("x", target)));
        Assert.False(result.CanApply);
        Assert.Contains(fragment, Row(result, "x").Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("W", true)]
    [InlineData("rows[12].nominal", true)]
    [InlineData("_a1", true)]
    [InlineData("1a", false)]
    [InlineData("a b", false)]
    [InlineData("rows[].x", false)]
    [InlineData("rows[1]", false)]
    [InlineData("rows[1].", false)]
    [InlineData("rows[99999].x", false)]
    [InlineData("a[1].b.c", false)]
    [InlineData("", false)]
    public void Target_Syntax(string text, bool valid) => Assert.Equal(valid, CadTarget.TryParse(text, out _));

    // ---- file against profile ------------------------------------------

    [Fact]
    public void A_Name_Assigned_Twice_Is_Refused_Because_There_Is_No_Single_Value()
    {
        var parse = CadImporter.Parse(Samples.Load("creo/bracket-b.txt"), CadSourceFormat.CreoRelations);
        var result = Map(parse, Profile(E("d7", "n")));
        Assert.False(result.CanApply);
        Assert.Contains("2 times", string.Join(" ", result.Refusals), StringComparison.Ordinal);
    }

    [Fact]
    public void A_Profile_For_One_System_Does_Not_Map_Another_Systems_File()
    {
        var parse = Csv("name,value,unit\nplate_w,1,mm\n");
        var result = Map(parse, Profile(E("plate_w", "W")) with { System = CadSystem.Nx });
        Assert.False(result.CanApply);
        Assert.Contains("for NX files", string.Join(" ", result.Refusals), StringComparison.Ordinal);

        var template = Map(parse, Profile(E("plate_w", "W")) with { TemplateId = "other.template" });
        Assert.False(template.CanApply);
    }

    [Fact]
    public void An_Unsound_Profile_Refuses_Everything_With_Its_Reasons()
    {
        var result = Map(Csv("name,value,unit\nplate_w,1,mm\n"), new CadMappingProfile("Bad Id", 0, "", []));
        Assert.False(result.CanApply);
        Assert.True(result.Refusals.Count >= 3);
    }

    [Fact]
    public void Real_Nx_And_SolidWorks_Samples_Map_Through_Profiles()
    {
        var nx = CadImporter.Parse(Samples.Load("nx/bracket-a.exp"), CadSourceFormat.NxExpressions);
        var nxResult = Map(nx, Profile(E("plate_w", "W"), E("bend_angle", "alpha"), E("count", "n"), E("material", "grade"), E("with_rib", "ribbed")) with { System = CadSystem.Nx });
        Assert.True(nxResult.CanApply, string.Join(" ", nxResult.Refusals));
        Assert.Equal(90, nxResult.Inputs.Single(i => i.Symbol == "alpha").Number);
        Assert.Equal(3, nxResult.Rows.Count(r => r.Status == CadRowStatus.Expression));
        Assert.Equal("NX 2306", CadImportProvenance.TryParse(nxResult.Inputs.Single(i => i.Symbol == "W").Provenance)!.SystemAndVersion);

        var sw = CadImporter.Parse(Samples.Load("solidworks/bracket-design-table.xlsx"), CadSourceFormat.SolidWorksDesignTable);
        var swResult = Map(sw, Profile(E("Plate_W@Sketch1", "W", AssumedUnit: "mm")) with { System = CadSystem.SolidWorks });
        Assert.True(swResult.CanApply);
        Assert.Equal(40.5, swResult.Inputs.Single(i => i.Symbol == "W").Number);
    }

    // ---- provenance ----------------------------------------------------

    [Fact]
    public void Provenance_Carries_System_Version_Part_Revision_Hash_Time_Name_And_Profile_And_Reads_Back()
    {
        var parse = CadImporter.Parse(Samples.Load("nx/bracket-a.exp"), CadSourceFormat.NxExpressions);
        var provenance = CadImportProvenance.Create(parse.Source, Profile(E("a", "W")), "plate_w", At);

        Assert.Equal(CalcProvenanceKind.CadImport, provenance.Kind);
        Assert.True(provenance.IsWithinLimits);
        Assert.True(provenance.IsSufficientToIssue);
        Assert.Contains(parse.Source.Sha256, provenance.Reference, StringComparison.Ordinal);
        Assert.Equal("test-profile@3", provenance.Revision);

        var info = CadImportProvenance.TryParse(provenance)!;
        Assert.Equal("NX 2306", info.SystemAndVersion);
        Assert.Equal("BRACKET-A", info.Part);
        Assert.Equal("B", info.PartRevision);
        Assert.Equal(parse.Source.Sha256, info.FileSha256);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 9, 30, 0, TimeSpan.Zero), info.ImportedAt);
        Assert.Equal("plate_w", info.CadName);
        Assert.Equal(("test-profile", 3), (info.ProfileId, info.ProfileVersion));
        Assert.StartsWith("cad import: cad:NX 2306|BRACKET-A|B|", provenance.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Long_Names_Are_Shortened_To_Fit_The_Cap_But_The_Hash_Never_Is()
    {
        var source = new CadSourceInfo(CadSystem.SolidWorks, CadSourceFormat.SolidWorksEquations, "f.txt", new string('a', 64), "SolidWorks 2025 SP5.0 with extras", new string('P', 90), "REV-LONG-123456", null);
        var provenance = CadImportProvenance.Create(source, Profile(E("a", "W")), new string('N', 128), At);

        Assert.True(provenance.Reference!.Length <= CalcProvenance.MaxReferenceLength, provenance.Reference.Length.ToString());
        Assert.True(provenance.Revision!.Length <= CalcProvenance.MaxRevisionLength);
        Assert.Contains(new string('a', 64), provenance.Reference, StringComparison.Ordinal);
        Assert.NotNull(CadImportProvenance.TryParse(provenance));
    }

    [Fact]
    public void A_Bar_In_A_Name_Cannot_Forge_Another_Field()
    {
        var parse = Csv("name,value,unit\nplate_w,1,mm\n");
        var source = parse.Source with { PartName = "evil|99|bad" };
        var info = CadImportProvenance.TryParse(CadImportProvenance.Create(source, Profile(E("a", "W")), "x|y", At))!;
        Assert.Equal("evil/99/bad", info.Part);
        Assert.Equal("x/y", info.CadName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("calc:abc")]
    [InlineData("cad:only|four|parts|here")]
    public void Provenance_That_Is_Not_A_Cad_Import_Does_Not_Read_As_One(string? reference) =>
        Assert.Null(CadImportProvenance.TryParse(new CalcProvenance(CalcProvenanceKind.CadImport, reference, "p@1")));

    [Fact]
    public void Another_Kind_Of_Provenance_Does_Not_Read_As_A_Cad_Import()
    {
        Assert.Null(CadImportProvenance.TryParse(CalcProvenance.Manual));
        Assert.Null(CadImportProvenance.TryParse(null));
    }

    // ---- diff ----------------------------------------------------------

    private static CadImportRecord Record(params CadImportedParameter[] parameters) =>
        new("k", "f.exp", new string('0', 64), CadSystem.Nx, CadSourceFormat.NxExpressions, "NX 2306", "P", "B", null, "p", 1, At, "me", parameters);

    [Fact]
    public void A_First_Import_Has_No_Diff_And_Says_So()
    {
        var diff = CadImportDiff.Compute(null, [new CadImportedParameter("a", CadParameterKind.Number, "W", 1, "mm", 0.001)]);
        Assert.True(diff.FirstImport);
        Assert.Empty(diff.Changes);
        Assert.Contains("First import", diff.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Re_Import_Reports_Added_Removed_And_Changed_And_Which_Move_An_Input()
    {
        var before = Record(
            P("same", CadParameterKind.Number, "W", 6, "mm", 0.006),
            P("moved", CadParameterKind.Number, "F", 12, "kN", 12000),
            P("gone", CadParameterKind.Number, "n", 4, null, 4),
            P("note", CadParameterKind.Text, null, text: "old"),
            P("expr", CadParameterKind.Expression, null, text: "a*2"));

        var after = new[]
        {
            new CadImportedParameter("same", CadParameterKind.Number, "W", 6, "mm", 0.006),
            new CadImportedParameter("moved", CadParameterKind.Number, "F", 15, "kN", 15000),
            new CadImportedParameter("note", CadParameterKind.Text, null, Text: "new"),
            new CadImportedParameter("expr", CadParameterKind.Expression, null, Text: "a*3"),
            new CadImportedParameter("fresh", CadParameterKind.Number, "alpha", 30, "deg", 0.5235987755982988),
        };

        var diff = CadImportDiff.Compute(before, after);

        Assert.False(diff.FirstImport);
        Assert.Equal(1, diff.Count(CadChangeKind.Added));
        Assert.Equal(1, diff.Count(CadChangeKind.Removed));
        Assert.Equal(3, diff.Count(CadChangeKind.Changed));
        Assert.Equal("12 kN", diff.Changes.Single(c => c.Name == "moved").Before);
        Assert.Equal("15 kN", diff.Changes.Single(c => c.Name == "moved").After);
        Assert.True(diff.Changes.Single(c => c.Name == "moved").AffectsInput);
        Assert.False(diff.Changes.Single(c => c.Name == "note").AffectsInput);
        Assert.Equal("= a*2", diff.Changes.Single(c => c.Name == "expr").Before);
        Assert.True(diff.Changes.Single(c => c.Name == "gone").AffectsInput);
        Assert.DoesNotContain(diff.Changes, c => c.Name == "same");
        Assert.Contains("1 added, 1 removed, 3 changed", diff.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Same_Value_Written_In_Another_Unit_Is_Not_A_Change_But_A_New_Target_Is()
    {
        var before = Record(P("len", CadParameterKind.Number, "W", 25.4, "mm", 0.0254));
        var inches = new[] { new CadImportedParameter("len", CadParameterKind.Number, "W", 1, "in", 0.0254) };
        Assert.False(CadImportDiff.Compute(before, inches).HasChanges);

        var retargeted = new[] { new CadImportedParameter("len", CadParameterKind.Number, "F", 25.4, "mm", 0.0254) };
        Assert.True(CadImportDiff.Compute(before, retargeted).HasChanges);
    }

    [Fact]
    public void Two_Revisions_Of_The_Same_Nx_Part_Diff_As_Changed_And_Added_Through_The_Mapper()
    {
        var profile = Profile(E("plate_t", "W", "mm"), E("plate_w", "F", "kN"), E("count", "n"));
        var revB = CadImporter.Parse(Samples.Load("nx/bracket-a.exp"), CadSourceFormat.NxExpressions);
        var revC = CadImporter.Parse(Samples.Load("nx/bracket-a-rev-c.exp"), CadSourceFormat.NxExpressions);
        Assert.Equal("B", revB.Source.PartRevision);
        Assert.Equal("C", revC.Source.PartRevision);

        var first = Map(revB, profile with { Entries = [E("plate_t", "W"), E("count", "n")] });
        var second = Map(revC, profile with { Entries = [E("plate_t", "W"), E("count", "n")] });
        Assert.True(first.CanApply && second.CanApply);

        var previous = new CadImportRecord("k", "bracket-a.exp", revB.Source.Sha256, CadSystem.Nx, CadSourceFormat.NxExpressions, "NX 2306", "BRACKET-A", "B", null, "test-profile", 3, At, "me", first.Snapshot);
        var diff = CadImportDiff.Compute(previous, second.Snapshot);

        Assert.Equal(["new_rib_t", "plate_t"], diff.Changes.Select(c => c.Name).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("6 mm", diff.Changes.Single(c => c.Name == "plate_t").Before);
        Assert.Equal("8 mm", diff.Changes.Single(c => c.Name == "plate_t").After);
        Assert.True(diff.Changes.Single(c => c.Name == "plate_t").AffectsInput);
        Assert.False(diff.Changes.Single(c => c.Name == "new_rib_t").AffectsInput);
        Assert.NotEqual(revB.Source.Sha256, revC.Source.Sha256);
    }

    // ---- reports -------------------------------------------------------

    [Fact]
    public void The_Csv_Reports_Defuse_Every_Text_That_Came_From_The_File()
    {
        var hostile = "=cmd|' /C calc'!A0";
        var parse = Csv($"name,value\n\"{hostile}\",5\n");
        var result = Map(parse, Profile(E(hostile, "W", AssumedUnit: "mm")));
        var csv = CadImportReport.RowsCsv(result.Rows);

        foreach (var line in csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var fields = CsvReader.Read(line).Rows[0];
            Assert.All(fields, f => Assert.False(CsvSafeWriter.IsFormulaLike(f), f));
        }

        Assert.Contains("'=cmd", csv, StringComparison.Ordinal);

        var diff = CadImportDiff.Compute(Record(P(hostile, CadParameterKind.Number, null, 1, null, 1)), [new CadImportedParameter(hostile, CadParameterKind.Number, null, 2, null, 2)]);
        var changes = CadImportReport.ChangesCsv(diff);
        Assert.All(CsvReader.Read(changes).Rows.Skip(1).SelectMany(r => r), f => Assert.False(CsvSafeWriter.IsFormulaLike(f), f));
    }

    // ---- profile json --------------------------------------------------

    [Fact]
    public void A_Profile_Round_Trips_Through_Json()
    {
        var profile = new CadMappingProfile("nx-bracket", 2, "NX bracket tolerance inputs", [E("plate_w", "W", "mm", true), E("count", "rows[0].nominal")], CadSystem.Nx, "mech.tolerance-stack", true, new NumberProfile(DecimalMark.Comma, '.'), "Notes");
        var back = CadMappingProfileJson.Read(CadMappingProfileJson.Write(profile));

        Assert.Equal(profile.Id, back.Id);
        Assert.Equal(2, back.Version);
        Assert.Equal(CadSystem.Nx, back.System);
        Assert.True(back.IgnoreCase);
        Assert.Equal(new NumberProfile(DecimalMark.Comma, '.'), back.Number);
        Assert.Equal(profile.Entries, back.Entries);
        Assert.Equal("nx-bracket@2", back.Label);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"id\":\"Upper\",\"version\":1,\"title\":\"t\",\"entries\":[{\"cadName\":\"a\",\"target\":\"W\"}]}")]
    [InlineData("{\"id\":\"ok\",\"version\":0,\"title\":\"t\",\"entries\":[{\"cadName\":\"a\",\"target\":\"W\"}]}")]
    [InlineData("{\"id\":\"ok\",\"version\":1,\"title\":\"t\",\"entries\":[{\"cadName\":\"a\",\"target\":\"W\"},{\"cadName\":\"a\",\"target\":\"F\"}]}")]
    [InlineData("{\"id\":\"ok\",\"version\":1,\"title\":\"t\",\"entries\":[{\"cadName\":\"a\",\"target\":\"W\"},{\"cadName\":\"b\",\"target\":\"W\"}]}")]
    [InlineData("{\"id\":\"ok\",\"version\":1,\"title\":\"t\",\"entries\":[{\"cadName\":\"a\",\"target\":\"W X\"}]}")]
    [InlineData("{\"id\":\"ok\",\"version\":1,\"title\":\"t\",\"entries\":[{\"cadName\":\"a\",\"target\":\"W\",\"assumedUnit\":\"zorkmid\"}]}")]
    public void An_Unsound_Or_Malformed_Profile_Is_Refused(string json) =>
        Assert.Equal(ImportRefusalCode.MalformedStructure, Assert.Throws<ImportRefusedException>(() => CadMappingProfileJson.Read(json)).Code);

    [Fact]
    public void An_Oversized_Profile_And_A_Control_Character_Are_Refused()
    {
        Samples.Refuses(ImportRefusalCode.TooLarge, () => CadMappingProfileJson.Read(new string(' ', CadMappingProfileJson.MaxBytes + 1)));
        Samples.Refuses(ImportRefusalCode.ControlCharacter, () => CadMappingProfileJson.Read("{\"id\":\"a\u0001b\"}"));
    }

    [Fact]
    public void A_Profile_Id_And_Label_Fit_The_Provenance_Revision_Cap()
    {
        var longest = new CadMappingProfile(new string('a', CadMappingProfile.MaxIdChars), 999_999, "t", [E("a", "W")]);
        Assert.Empty(longest.Validate());
        Assert.True(longest.Label.Length <= CalcProvenance.MaxRevisionLength);
        Assert.NotEmpty(new CadMappingProfile(new string('a', CadMappingProfile.MaxIdChars + 1), 1, "t", [E("a", "W")]).Validate());
    }
}
