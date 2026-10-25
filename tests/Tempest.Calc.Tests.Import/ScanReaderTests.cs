using System.Text;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Import;

/// <summary>W2 readers: the four inspection-software layouts through user-defined column profiles, multi-stage and multi-file input, problems reported rather than dropped, profile mismatch, caps.</summary>
public sealed class ScanReaderTests
{
    private const string Header = "Feature,Nominal,Actual,Dev,Up,Lo,Serial\n";

    private static ScanAnalysis Read(string body, ScanProfile? profile = null, ScanSettings? settings = null, string header = Header) =>
        ScanImporter.Analyse(SampleProfiles.Input(header + body).File, profile ?? SampleProfiles.Plain(), settings);

    private static ScanProblemGroup? Problem(ScanAnalysis a, ScanProblemKind kind, string column) =>
        a.Data.Problems.FirstOrDefault(p => p.Kind == kind && p.Column == column);

    // ---- the four layouts ------------------------------------------------

    [Fact]
    public void A_Zeiss_Style_Table_Reads_Ninety_Observations_Of_Three_Features_Without_A_Problem()
    {
        var a = ScanImporter.Analyse(Samples.Load("scan/zeiss-groove.csv"), SampleProfiles.Zeiss());

        Assert.Equal(90, a.Data.Observations.Count);
        Assert.Equal(3, a.Stats.Count);
        Assert.Empty(a.Data.Problems);
        Assert.Equal(64, a.Data.Files[0].Sha256.Length);
        Assert.Equal(90, a.Data.Files[0].Rows);
        Assert.All(a.Stats, s => Assert.Equal(30, s.Count));
        Assert.All(a.Stats, s => Assert.Equal(30, s.Parts));
        Assert.Equal("S001..S030 (n=30)", a.Stats[0].SerialSummary);

        // The limits come from the nominal and the tolerance deviations: 51.5 ± 0.05.
        Assert.Equal(51.55, a.Stats[0].Usl!.Value, 12);
        Assert.Equal(51.45, a.Stats[0].Lsl!.Value, 12);
        Assert.Equal(51.5, a.Stats[0].Nominal);
    }

    [Fact]
    public void A_Gom_Style_Multi_Stage_Table_With_Decimal_Commas_And_Semicolons_Gives_One_Population_Per_Stage()
    {
        var a = ScanImporter.Analyse(Samples.Load("scan/gom-two-stage.csv"), SampleProfiles.GomTwoStage());

        Assert.Empty(a.Data.Problems);
        Assert.Equal(2, a.Stats.Count);

        var rough = a.Stats.Single(s => s.Stage == "OP10 rough");
        var finish = a.Stats.Single(s => s.Stage == "OP20 finish");
        Assert.Equal(12, rough.Count);
        Assert.Equal(12, finish.Count);
        Assert.Equal(24.851583333333334, rough.Mean, 12);
        Assert.Equal(0.039451831005632036, rough.Sigma, 12);
        Assert.Equal(-1.0765589616400444, rough.Cpk!.Value, 10);
        Assert.Equal(0.18755938864038, rough.Normality.AStar2!.Value, 8);
        Assert.Equal(25.003666666666664, finish.Mean, 12);
        Assert.Equal(0.0056138357221376806, finish.Sigma, 12);
        Assert.Equal(1.0292032157255235, finish.Cpk!.Value, 10);
        Assert.Equal(1.2469192805903395, finish.Cp!.Value, 10);
        Assert.Equal(0.5252266545539305, finish.Normality.AStar2!.Value, 8);
        Assert.Equal(0.1807869797672924, finish.Normality.PValue!.Value, 8);
    }

    [Fact]
    public void A_PolyWorks_Style_Text_Report_Is_Read_By_Position_Under_A_Header_Row_Below_Its_Preamble_With_Absolute_Limits()
    {
        var a = ScanImporter.Analyse(Samples.Load("scan/polyworks-report.txt"), SampleProfiles.PolyWorks());

        var pin = Assert.Single(a.Stats);
        Assert.Equal("Pin_dia", pin.Feature);
        Assert.Equal(10, pin.Count);
        Assert.Equal(10.0006, pin.Mean, 12);
        Assert.Equal(0.010699948078794019, pin.Sigma, 12);
        Assert.Equal(0.9345839742735348, pin.Cp!.Value, 10);
        Assert.Equal(0.9158922947880519, pin.Cpk!.Value, 10);
        Assert.Equal(9.97, pin.Lsl);
        Assert.Equal(10.03, pin.Usl);
        Assert.Empty(a.Data.Problems);
    }

    [Fact]
    public void A_Geomagic_Style_Tab_Delimited_Table_Is_Read_With_Its_Own_Header_Names()
    {
        var profile = new ScanProfile(
            "geomagic-tab", 1, "Geomagic Control X deviations",
            new ScanColumns("Name", Nominal: "Reference", Actual: "Measured", Deviation: "Deviation", UpperTolerance: "Tol+", LowerTolerance: "Tol-"),
            [], ScanSoftware.GeomagicControlX, Delimiter: ScanDelimiter.Tab);
        var text = "Name\tReference\tMeasured\tDeviation\tTol+\tTol-\r\nH1\t8.000\t8.012\t0.012\t0.05\t-0.05\r\nH1\t8.000\t7.994\t-0.006\t0.05\t-0.05\r\nH1\t8.000\t8.003\t0.003\t0.05\t-0.05\r\n";

        var a = ScanImporter.Analyse(Samples.Text(text, "g.txt"), profile);

        var h1 = Assert.Single(a.Stats);
        Assert.Equal(3, h1.Count);
        Assert.Equal((8.012 + 7.994 + 8.003) / 3, h1.Mean, 12);
        Assert.Equal(8.05, h1.Usl!.Value, 12);
    }

    [Fact]
    public void Auto_Delimiter_Finds_Comma_Semicolon_And_Tab_And_Never_Takes_The_Comma_Of_A_Decimal_Comma_File()
    {
        var comma = ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,1.5\nA,1.7\n"), new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []));
        Assert.Equal(2, comma.Stats[0].Count);

        var semicolon = ScanImporter.Analyse(Samples.Text("Feature;Actual\nA;1,5\nA;1,7\n"), new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), [], Number: NumberProfile.Comma));
        Assert.Equal(1.6, semicolon.Stats[0].Mean, 12);

        var tab = ScanImporter.Analyse(Samples.Text("Feature\tActual\nA\t1.5\nA\t1.7\n"), new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []));
        Assert.Equal(1.6, tab.Stats[0].Mean, 12);
    }

    [Fact]
    public void A_Workbook_Is_Read_With_The_Safe_Reader_And_Its_Formula_Cells_Give_Their_Saved_Values_Only()
    {
        const string Sheet =
            "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Feature</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Actual</t></is></c><c r=\"C1\" t=\"inlineStr\"><is><t>Serial</t></is></c></row>"
            + "<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>Hole</t></is></c><c r=\"B2\"><v>4.01</v></c><c r=\"C2\" t=\"inlineStr\"><is><t>A</t></is></c></row>"
            + "<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>Hole</t></is></c><c r=\"B3\"><f>1+1</f><v>4.03</v></c><c r=\"C3\" t=\"inlineStr\"><is><t>B</t></is></c></row>"
            + "<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>Hole</t></is></c><c r=\"B4\"><v>3.99</v></c><c r=\"C4\" t=\"inlineStr\"><is><t>C</t></is></c></row>";
        var file = Samples.Bytes(Samples.Workbook(Sheet), "scan.xlsx");
        var profile = new ScanProfile("xl", 1, "Workbook", new ScanColumns("Feature", Actual: "Actual", Serial: "Serial"), []);

        var a = ScanImporter.Analyse(file, profile);

        var hole = Assert.Single(a.Stats);
        Assert.Equal(3, hole.Count);
        Assert.Equal((4.01 + 4.03 + 3.99) / 3, hole.Mean, 12);
        Assert.Contains(a.Data.Notices, n => n.Message.Contains("formula cell", StringComparison.Ordinal));
    }

    // ---- stage and serial per file -----------------------------------------

    [Fact]
    public void Stage_And_Serial_Can_Be_Given_Per_File_When_The_Table_Has_No_Column_For_Them()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []);
        var rough = new ScanFileInput(Samples.Text("Feature,Actual\nBore,24.80\nBore,24.90\nBore,24.85\n", "rough.csv"), Stage: "rough");
        var finish = new ScanFileInput(Samples.Text("Feature,Actual\nBore,25.00\nBore,25.01\nBore,24.99\n", "finish.csv"), Stage: "finish", Serial: "Batch 7");

        var a = ScanImporter.Analyse([rough, finish], profile);

        Assert.Equal(2, a.Stats.Count);
        Assert.Equal(["rough", "finish"], a.Stats.Select(s => s.Stage).ToArray());
        Assert.Equal(a.Data.Files[0].Sha256, a.Stats[0].SetSha256);
        Assert.Equal(a.Data.Files[1].Sha256, a.Stats[1].SetSha256);
        Assert.Equal("Batch 7", a.Stats[1].SerialSummary);
        Assert.Equal("rough", a.Data.Files[0].Stage);
    }

    [Fact]
    public void The_Same_Stage_In_Several_Files_Is_One_Population_With_A_Set_Hash()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual", Serial: "Serial"), []);
        var one = new ScanFileInput(Samples.Text("Feature,Actual,Serial\nBore,25.00,A\nBore,25.01,B\n", "one.csv"), Stage: "finish");
        var two = new ScanFileInput(Samples.Text("Feature,Actual,Serial\nBore,24.99,C\nBore,25.02,D\n", "two.csv"), Stage: "finish");

        var a = ScanImporter.Analyse([one, two], profile);

        var bore = Assert.Single(a.Stats);
        Assert.Equal(4, bore.Count);
        Assert.Equal([0, 1], bore.Files);
        Assert.NotEqual(a.Data.Files[0].Sha256, bore.SetSha256);
        Assert.NotEqual(a.Data.Files[1].Sha256, bore.SetSha256);
        Assert.Equal("A..D (n=4)", bore.SerialSummary);
    }

    [Fact]
    public void A_Stage_Column_Wins_Over_The_Stage_Entered_For_The_File()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual", Stage: "Stage"), []);
        var file = new ScanFileInput(Samples.Text("Feature,Actual,Stage\nBore,1,X\nBore,2,\nBore,3,Y\n"), Stage: "entered");

        var a = ScanImporter.Analyse([file], profile);

        Assert.Equal(["X", "entered", "Y"], a.Stats.Select(s => s.Stage).ToArray());
    }

    // ---- values: blank, NaN, text, outliers ---------------------------------

    [Fact]
    public void A_Blank_Cell_Is_Missing_And_Reported_Not_Zero_And_The_Row_Falls_Back_To_Nominal_Plus_Deviation()
    {
        var a = Read("A,10,10.01,0.01,0.05,-0.05,S1\nA,10,,0.03,0.05,-0.05,S2\nA,10,,,0.05,-0.05,S3\nA,10,9.98,-0.02,0.05,-0.05,S4\n");

        var s = Assert.Single(a.Stats);
        Assert.Equal(3, s.Count);
        Assert.Equal([10.01, 10.03, 9.98], a.Data.Observations.Select(o => o.Value).ToArray());
        Assert.Equal(2, Problem(a, ScanProblemKind.Blank, "actual")!.Count);
        Assert.Equal(1, Problem(a, ScanProblemKind.Blank, "deviation")!.Count);
        var noValue = Problem(a, ScanProblemKind.NoValue, "actual")!;
        Assert.Equal(1, noValue.Count);
        Assert.Contains("line 4", noValue.Examples[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("nan")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("inf")]
    [InlineData("1e999")]
    [InlineData("-1e999")]
    [InlineData("0x10")]
    [InlineData("1_000")]
    [InlineData("١٢٣")]
    [InlineData("10 mm")]
    [InlineData("=1+1")]
    [InlineData("1.2.3")]
    public void Text_That_Is_Not_A_Plain_Number_Is_Never_Accepted_As_One_It_Is_Reported_As_Missing(string cell)
    {
        var a = Read($"A,10,{cell},,0.05,-0.05,S1\nA,10,10.02,0.02,0.05,-0.05,S2\nA,10,10.01,0.01,0.05,-0.05,S3\n");

        Assert.Equal(2, a.Data.Observations.Count);
        Assert.All(a.Data.Observations, o => Assert.True(double.IsFinite(o.Value)));
        Assert.All(a.Stats, s => Assert.True(double.IsFinite(s.Mean) && double.IsFinite(s.Sigma)));
        var notNumber = Problem(a, ScanProblemKind.NotANumber, "actual");
        Assert.NotNull(notNumber);
        Assert.Equal(1, notNumber.Count);
        Assert.Equal(1, Problem(a, ScanProblemKind.NoValue, "actual")!.Count);
    }

    [Fact]
    public void A_Number_In_The_Other_Decimal_Notation_Is_Refused_With_The_Remedy_Not_Reinterpreted()
    {
        var a = Read("A,10,\"10,01\",,0.05,-0.05,S1\nA,10,10.02,0.02,0.05,-0.05,S2\n");

        Assert.Single(a.Data.Observations);
        var problem = Problem(a, ScanProblemKind.NotANumber, "actual")!;
        Assert.Contains("decimal mark", problem.Examples[0], StringComparison.Ordinal);
        Assert.Contains("choose the other number format", problem.Examples[0], StringComparison.Ordinal);
    }

    [Fact]
    public void A_Value_Beyond_Any_Plausible_Scan_Is_Reported_And_The_Row_Is_Not_Used()
    {
        var a = Read("A,10,10.01,0.01,0.05,-0.05,S1\nA,10,1e12,,0.05,-0.05,S2\nA,10,10.02,0.02,0.05,-0.05,S3\n");

        Assert.Equal(2, a.Data.Observations.Count);
        Assert.NotNull(Problem(a, ScanProblemKind.OutOfRange, "actual"));
    }

    [Fact]
    public void Markup_Links_And_Invisible_Characters_Are_Removed_From_Names_And_Reported()
    {
        var csv = "Feature,Nominal,Actual,Dev,Up,Lo,Serial\n"
            + "\"<b>Hole</b> <a href=\"\"http://evil.example/x\"\">one</a>\",10,10.01,,0.05,-0.05,\"S<script>alert(1)</script>1\"\n"
            + "\"=HYPERLINK(\"\"http://evil.example\"\",\"\"click\"\")\",10,10.02,,0.05,-0.05,S2\n"
            + "\"Ho​le\",10,10.03,,0.05,-0.05,S3\n"
            + "Visit https://evil.example/path now,10,10.04,,0.05,-0.05,S4\n";

        var a = Read(string.Empty, header: csv);
        var names = a.Stats.Select(s => s.Feature).ToList();

        Assert.Equal(4, names.Count);
        Assert.Contains("Hole one", names);
        Assert.Contains("[link removed]", names);
        Assert.Contains("Hole", names);
        Assert.Contains("Visit [link removed] now", names);
        Assert.All(a.Data.Observations, o =>
        {
            Assert.DoesNotContain('<', o.Feature + o.Serial);
            Assert.DoesNotContain("http", o.Feature, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain('​', o.Feature);
        });
        Assert.Equal(4, Problem(a, ScanProblemKind.Text, "feature")!.Count);
        Assert.Equal(1, Problem(a, ScanProblemKind.Text, "serial")!.Count);
    }

    [Fact]
    public void Features_Are_Matched_Without_Regard_To_Case_And_Keep_The_First_Spelling()
    {
        var a = Read("hole 1,10,10.01,,0.05,-0.05,S1\nHOLE 1,10,10.02,,0.05,-0.05,S2\nHole 1,10,10.03,,0.05,-0.05,S3\n");

        var s = Assert.Single(a.Stats);
        Assert.Equal("hole 1", s.Feature);
        Assert.Equal(3, s.Count);
    }

    [Fact]
    public void A_Row_With_No_Feature_Name_Or_No_Cells_At_All_Is_Reported_And_Not_Used()
    {
        var a = Read(",10,10.01,,0.05,-0.05,S1\n,,,,,,\nA,10,10.02,,0.05,-0.05,S2\nA,10,10.00,,0.05,-0.05,S3\n");

        Assert.Equal(2, a.Data.Observations.Count);
        Assert.Equal(1, Problem(a, ScanProblemKind.NoFeature, "feature")!.Count);
        Assert.Equal(1, Problem(a, ScanProblemKind.NoFeature, "(row)")!.Count);
    }

    [Fact]
    public void A_Short_Row_Is_Reported_And_Its_Missing_Cells_Are_Blank()
    {
        var a = Read("A,10,10.01\nA,10,10.02,0.02,0.05,-0.05,S2\n");

        Assert.Equal(2, a.Data.Observations.Count);
        Assert.Equal(1, Problem(a, ScanProblemKind.ShortRow, "(row)")!.Count);
        Assert.Null(a.Data.Observations[0].Usl);
    }

    [Fact]
    public void Actual_Nominal_And_Deviation_That_Disagree_Are_Reported_And_The_Actual_Value_Is_Used()
    {
        var a = Read("A,10,10.01,0.01,0.05,-0.05,S1\nA,10,10.02,0.30,0.05,-0.05,S2\n");

        Assert.Equal([10.01, 10.02], a.Data.Observations.Select(o => o.Value).ToArray());
        Assert.Equal(1, Problem(a, ScanProblemKind.Inconsistent, "deviation")!.Count);
    }

    [Fact]
    public void Rounding_Level_Differences_Between_Actual_And_Deviation_Are_Not_Reported()
    {
        var a = Read("A,10,10.0132,0.013,0.05,-0.05,S1\nA,10,9.9871,-0.013,0.05,-0.05,S2\nA,10,10.0001,0.000,0.05,-0.05,S3\n");

        Assert.Null(Problem(a, ScanProblemKind.Inconsistent, "deviation"));
    }

    [Fact]
    public void Reversed_Or_Wrongly_Signed_Tolerances_Are_Reported_And_The_Row_Is_Used_Without_Limits()
    {
        var a = Read("A,10,10.01,,-0.05,0.05,S1\nA,10,10.02,,0.05,0.04,S2\nA,10,10.03,,0.05,-0.05,S3\n");

        Assert.Equal(3, a.Data.Observations.Count);
        Assert.Null(a.Data.Observations[0].Usl);
        Assert.Null(a.Data.Observations[1].Usl);
        Assert.Equal(10.05, a.Data.Observations[2].Usl!.Value, 12);
        Assert.Equal(2, Problem(a, ScanProblemKind.Tolerance, "lower tolerance")!.Count + (Problem(a, ScanProblemKind.Tolerance, "upper tolerance")?.Count ?? 0));
    }

    [Fact]
    public void A_Table_With_Only_Deviations_Is_A_Population_Of_Deviations_About_Zero()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Deviation: "Dev", UpperTolerance: "Up", LowerTolerance: "Lo"), []);
        var a = ScanImporter.Analyse(Samples.Text("Feature,Dev,Up,Lo\nA,0.01,0.05,-0.05\nA,-0.02,0.05,-0.05\nA,0.00,0.05,-0.05\n"), profile);

        var s = Assert.Single(a.Stats);
        Assert.True(s.DeviationOnly);
        Assert.Equal(0.0, s.Nominal);
        Assert.Equal(-0.01 / 3, s.Mean, 12);
        Assert.Equal(0.05, s.Usl);
        Assert.Equal(-0.05, s.Lsl);
    }

    [Fact]
    public void Outliers_Found_In_A_Table_Are_Reported_With_Their_Line_And_Kept_Unless_Asked()
    {
        var rows = string.Concat(new[] { 10.00, 10.01, 9.99, 10.02, 9.98, 10.01, 10.00, 9.99, 10.02, 10.00, 9.98, 10.50 }.Select((v, i) => $"A,10,{v:0.00},,0.1,-0.1,S{i + 1}\n"));

        var kept = Read(rows);
        Assert.Equal(12, kept.Stats[0].Count);
        var outlier = Assert.Single(kept.Stats[0].Outliers);
        Assert.Equal(10.50, outlier.Value);
        Assert.Equal("S12", outlier.Serial);
        Assert.Equal(13, outlier.Line);

        var excluded = Read(rows, settings: new ScanSettings { ExcludeOutliers = true });
        Assert.Equal(11, excluded.Stats[0].Count);
        Assert.Equal(12, excluded.Stats[0].Total);
    }

    // ---- ragged rows (the review of W1, H3) ----

    [Fact]
    public void A_Row_With_More_Fields_Than_The_Header_Is_Refused_Because_A_Split_Number_Would_Shift_The_Values()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), [], Delimiter: ScanDelimiter.Comma, Number: NumberProfile.Comma);

        var ex = Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,12,5\nA,13,0\n"), profile));

        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("line 2", ex.Message, StringComparison.Ordinal);
        Assert.Contains("3 fields but the header row has 2", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Nothing was imported", ex.Message, StringComparison.Ordinal);

        // A thousands separator under a decimal-point profile splits the same way.
        var point = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), [], Delimiter: ScanDelimiter.Comma);
        Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,1,250\n"), point));

        // Quoting keeps a field whole; trailing empty fields are harmless.
        var ok = ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,\"12,5\",,\nA,\"13,0\"\n"), profile);
        Assert.Equal(2, ok.Data.Observations.Count);
        Assert.Equal(12.5, ok.Data.Observations[0].Value);
    }

    [Fact]
    public void A_Whitespace_Report_Row_With_Extra_Words_Is_Refused_In_The_Same_Way()
    {
        var text = "Name Actual\nPin 10.01\nPin 10.02 extra\n";
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("#1", Actual: "#2"), [], Delimiter: ScanDelimiter.Whitespace);

        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanImporter.Analyse(Samples.Text(text), profile));
    }

    // ---- profile mismatch ----------------------------------------------------

    [Fact]
    public void A_Header_The_Profile_Does_Not_Find_Is_Refused_With_The_Headers_The_File_Has_And_Nothing_Is_Imported()
    {
        var ex = Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse(Samples.Text("Element,Soll,Ist\nA,1,1\n"), SampleProfiles.Plain()));

        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("does not match the profile 'plain@1'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'Element', 'Soll', 'Ist'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Nothing was imported", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Wrong_Delimiter_Is_A_Profile_Mismatch_Not_A_Silent_Misread()
    {
        var profile = SampleProfiles.GomTwoStage() with { Delimiter = ScanDelimiter.Comma };

        var ex = Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse(Samples.Load("scan/gom-two-stage.csv"), profile));

        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("does not match", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Wrong_Number_Format_Leaves_Every_Value_Reported_And_Nothing_To_Fit()
    {
        var profile = SampleProfiles.GomTwoStage() with { Number = NumberProfile.Point };

        var a = ScanImporter.Analyse(Samples.Load("scan/gom-two-stage.csv"), profile);

        Assert.Empty(a.Data.Observations);
        Assert.Contains(a.Data.Notices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.True(Problem(a, ScanProblemKind.NotANumber, "actual")!.Count >= 24);
    }

    [Fact]
    public void A_Position_Beyond_The_Header_And_A_Header_Row_Beyond_The_File_Are_Refused()
    {
        var far = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "#9"), []);
        Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,1\n"), far));

        var low = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), [], HeaderRow: 7);
        var ex = Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,1\n"), low));
        Assert.Contains("header on row 7", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Duplicate_Header_Is_Refused_And_Asks_For_A_Position()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Value"), []);

        var ex = Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse(Samples.Text("Feature,Value,Value\nA,1,2\n"), profile));

        Assert.Contains("appears 2 times", ex.Message, StringComparison.Ordinal);
        Assert.Contains("#2", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_Unsound_Profile_Is_Refused_Before_The_File_Is_Read()
    {
        var bad = new ScanProfile("Bad Id", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []);

        var ex = Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,1\n"), bad));

        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("profile id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Table_With_Only_A_Header_Reports_That_Nothing_Was_Usable()
    {
        var a = Read(string.Empty);

        Assert.Empty(a.Stats);
        Assert.Contains(a.Data.Notices, n => n.Severity == CalcNoticeSeverity.Block && n.Message.Contains("No usable row", StringComparison.Ordinal));
    }

    [Fact]
    public void An_Empty_File_Is_Refused()
    {
        var ex = Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse(Samples.Text(string.Empty), SampleProfiles.Plain()));

        Assert.Equal(ImportRefusalCode.Empty, ex.Code);
    }

    // ---- hardening -----------------------------------------------------------

    [Fact]
    public void A_Control_Character_UTF16_And_Invalid_UTF8_Are_Refused_Through_The_Shared_Bounded_Reader_Rules()
    {
        Samples.Refuses(ImportRefusalCode.ControlCharacter, () => ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,1\u0000\n"), SampleProfiles.Plain()));
        Samples.Refuses(ImportRefusalCode.InvalidEncoding, () => ScanImporter.Analyse(Samples.Bytes(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("Feature,Actual\nA,1\n")).ToArray()), SampleProfiles.Plain()));
        Samples.Refuses(ImportRefusalCode.InvalidEncoding, () => ScanImporter.Analyse(Samples.Bytes([0x46, 0x65, 0xC3, 0x28, 0x0A]), SampleProfiles.Plain()));
    }

    [Fact]
    public void A_Quote_Left_Open_Or_Text_After_A_Closing_Quote_Is_A_Malformed_Csv_Refusal()
    {
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanImporter.Analyse(Samples.Text("Feature,Actual\n\"A,1\n"), SampleProfiles.Plain()));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanImporter.Analyse(Samples.Text("Feature,Actual\n\"A\"x,1\n"), SampleProfiles.Plain()));
    }

    [Fact]
    public void A_Feature_Name_Over_The_Cap_Is_Refused()
    {
        var name = new string('x', 200);

        Samples.Refuses(ImportRefusalCode.FieldTooLong, () => Read($"{name},10,10.01,,0.05,-0.05,S1\n"));
    }

    [Fact]
    public void More_Than_Sixty_Four_Files_Is_Refused()
    {
        var files = Enumerable.Range(0, ScanImporter.MaxFiles + 1).Select(i => SampleProfiles.Input("Feature,Actual\nA,1\nA,2\n", $"f{i}.csv")).ToList();

        Samples.Refuses(ImportRefusalCode.TooManyRows, () => ScanImporter.Analyse(files, new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), [])));
    }

    [Fact]
    public void More_Distinct_Features_Than_The_Cap_Is_Refused()
    {
        var sb = new StringBuilder("Feature,Actual\n");

        for (var i = 0; i < 60; i++)
            sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"F{i},1\n");

        var settings = new ScanSettings { Limits = ImportLimits.Scan with { MaxParameters = 50 } };

        Samples.Refuses(ImportRefusalCode.TooManyParameters, () => ScanImporter.Analyse(Samples.Text(sb.ToString()), new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []), settings));
    }

    [Fact]
    public void The_Row_Cap_Is_Honoured_Exactly_At_A_Small_Limit()
    {
        var settings = new ScanSettings { Limits = ImportLimits.Scan with { MaxRows = 101 } };
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []);
        var atCap = "Feature,Actual\n" + string.Concat(Enumerable.Range(0, 100).Select(i => $"A,{i}\n"));
        var overCap = atCap + "A,100\n";

        Assert.Equal(100, ScanImporter.Analyse(Samples.Text(atCap), profile, settings).Data.Observations.Count);
        Samples.Refuses(ImportRefusalCode.TooManyRows, () => ScanImporter.Analyse(Samples.Text(overCap), profile, settings));
    }

    [Fact]
    public void The_Million_Row_Cap_Holds_At_Full_Scale_A_Table_Of_A_Million_Rows_And_One_Is_Refused_And_One_Under_Is_Read()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []);
        var sb = new StringBuilder("F,V\n", 4_100_000);

        for (var i = 0; i < 999_999; i++)
            sb.Append("a,").Append(i % 10).Append('\n');

        // A header plus 999,999 data rows is exactly 1,000,000 rows: read.
        var ok = ScanImporter.Analyse(Samples.Text(sb.ToString().Replace("F,V", "Feature,Actual", StringComparison.Ordinal)), profile);
        Assert.Equal(999_999, ok.Data.Observations.Count);
        Assert.Equal(999_999, ok.Stats[0].Count);
        Assert.True(double.IsFinite(ok.Stats[0].Sigma));

        // One row more: refused with the documented code, not read.
        sb.Append("a,1\n");
        Samples.Refuses(ImportRefusalCode.TooManyRows, () => ScanImporter.Analyse(Samples.Text(sb.ToString().Replace("F,V", "Feature,Actual", StringComparison.Ordinal)), profile));
    }

    [Fact]
    public void A_Time_Limit_Stops_A_Read_And_The_Callers_Cancellation_Is_Not_A_Refusal()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []);
        var big = "Feature,Actual\n" + string.Concat(Enumerable.Range(0, 200_000).Select(i => $"a,{i % 7}\n"));
        var slow = new ScanSettings { Limits = ImportLimits.Scan with { Timeout = TimeSpan.FromMilliseconds(1) } };

        Samples.Refuses(ImportRefusalCode.Timeout, () => ScanImporter.Analyse(Samples.Text(big), profile, slow));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ScanImporter.Analyse(Samples.Text(big), profile, cancellationToken: cts.Token));
    }

    [Fact]
    public void Settings_Outside_Their_Ranges_Are_Refused()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []);
        var file = Samples.Text("Feature,Actual\nA,1\n");

        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanImporter.Analyse(file, profile, new ScanSettings { SubgroupSize = 0 }));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanImporter.Analyse(file, profile, new ScanSettings { Alpha = 1.5 }));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanImporter.Analyse(file, profile, new ScanSettings { MinParts = 1 }));
    }

    [Fact]
    public void The_Report_Csvs_Defuse_Anything_A_Spreadsheet_Would_Run()
    {
        var a = Read("\"=cmd|' /C calc'!A0\",10,10.01,,0.05,-0.05,S1\n\"=cmd|' /C calc'!A0\",10,10.03,,0.05,-0.05,S2\n");
        var stats = ScanImportReport.StatisticsCsv(a.Stats, "mm");
        var problems = ScanImportReport.ProblemsCsv(a.Data);

        Assert.Contains("'=cmd|", stats, StringComparison.Ordinal);
        Assert.DoesNotContain("\n=cmd", stats, StringComparison.Ordinal);
        Assert.StartsWith("kind,column,count,examples", problems, StringComparison.Ordinal);
    }

    [Fact]
    public void Suggested_Columns_Come_From_The_Header_And_Are_Only_A_Starting_Point()
    {
        var zeiss = ScanColumnSuggestions.Suggest(["Element", "Nominal", "Actual", "Deviation", "Upper tol.", "Lower tol.", "Serial"]);
        Assert.NotNull(zeiss);
        Assert.Equal("Element", zeiss.Feature);
        Assert.Equal("Upper tol.", zeiss.UpperTolerance);
        Assert.Equal("Serial", zeiss.Serial);

        var polyworks = ScanColumnSuggestions.Suggest(["Name", "Nominal", "Measured", "Deviation", "+Tol", "-Tol"]);
        Assert.NotNull(polyworks);
        Assert.Equal("Measured", polyworks.Actual);
        Assert.Equal("+Tol", polyworks.UpperTolerance);
        Assert.Equal("-Tol", polyworks.LowerTolerance);

        Assert.Null(ScanColumnSuggestions.Suggest(["foo", "bar"]));
    }
}
