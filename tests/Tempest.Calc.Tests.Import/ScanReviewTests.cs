using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsCheck;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Import;

/// <summary>Regression tests for the findings of the W2 review (H1–H2, M1–M7, L1–L11); every reference figure from python3.</summary>
public sealed class ScanReviewTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 25, 9, 30, 15, TimeSpan.Zero);

    private static IReadOnlyList<ScanObservation> Obs(IEnumerable<double> values, double? nominal = 10, double? lsl = 9.9, double? usl = 10.1) =>
        values.Select((v, i) => new ScanObservation(0, i + 2, "F", string.Empty, $"S{i + 1:000}", nominal, v, lsl, usl, false)).ToList();

    private static readonly IReadOnlyList<ScanFileInfo> OneFile = [new("a.csv", new string('a', 64), null, null, 20, 20)];

    private static ScanAnalysis Read(string text, ScanProfile profile, ScanSettings? settings = null) => ScanImporter.Analyse(Samples.Text(text), profile, settings);

    private static ScanProfile Comma(params ScanFeatureEntry[] features) =>
        new("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), features, Delimiter: ScanDelimiter.Comma);

    // ---- H1 -------------------------------------------------------------------

    [Fact]
    public void A_Header_That_Ends_In_A_Delimiter_Cannot_Hide_A_Split_Decimal_Comma()
    {
        // "12,5" split into two fields lands the 5 under the blank header.
        var ex = Assert.Throws<ImportRefusedException>(() => Read("Feature,Actual,\nD1,12,5\nD1,13,0\n", Comma()));

        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("column 3 has no header but holds a value", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Nothing was imported", ex.Message, StringComparison.Ordinal);

        // A blank cell under a blank header is harmless.
        Assert.Equal(2, Read("Feature,Actual,\nD1,12.5,\nD1,13.0,\n", Comma()).Data.Observations.Count);
    }

    [Fact]
    public void A_German_Deviation_Export_Read_As_Decimal_Point_Is_Refused_Not_Read_As_Zero()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Point", Deviation: "Dev"), [], Delimiter: ScanDelimiter.Comma);

        // "0,012" is two fields; the row is wider than the header whether or not the extra fields are blank.
        var ex = Assert.Throws<ImportRefusedException>(() => Read("Point,Dev,\nGroove,0,012,\nGroove,-0,020,\n", profile));

        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("4 fields but the header row has 3", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Row_Wider_Than_The_Header_Is_Refused_Even_When_The_Extra_Fields_Are_Blank()
    {
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => Read("Feature,Actual\nA,1.5,\nA,1.6,\n", Comma()));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => Read("Feature,Actual\nA,1.5,,\n", Comma()));
    }

    [Fact]
    public void A_Zero_Spread_Is_Never_Pushed_Whatever_The_Target()
    {
        var profile = Comma(new ScanFeatureEntry("A", "D"));
        var a = Read("Feature,Actual\nA,50.0\nA,50.0\nA,50.0\n", profile);

        var result = ScanMapper.Map(a, MapperTemplate, MapperCurrent, At);

        Assert.False(result.CanApply);
        Assert.Contains("standard deviation of zero", result.Refusals[0], StringComparison.Ordinal);
        Assert.Contains("decimals", result.Refusals[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Whole_Numbers_Only_In_A_Comma_Delimited_Table_With_No_Declared_Number_Format_Block_The_Import_Until_The_Format_Is_Stated()
    {
        var profile = Comma();
        var whole = Read("Feature,Actual\nA,12\nA,13\nA,12\n", profile);
        var block = Assert.Single(whole.Data.Notices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.Contains("every one of the 3 values is a whole number", block.Message, StringComparison.Ordinal);
        Assert.Contains("State the number format in the profile", block.Message, StringComparison.Ordinal);

        // The mapper refuses on that block: nothing is applied.
        var mapped = ScanMapper.Map(Read("Feature,Actual\nA,12\nA,13\nA,12\n", Comma(new ScanFeatureEntry("A", "D"))), MapperTemplate, MapperCurrent, At);
        Assert.False(mapped.CanApply);
        Assert.Contains("whole number", mapped.Refusals[0], StringComparison.Ordinal);

        // Lifted only by declaring the format (either one: the person has said what the file uses).
        foreach (var declared in new[] { NumberProfile.Point, NumberProfile.Comma })
            Assert.DoesNotContain(Read("Feature,Actual\nA,12\nA,13\n", profile with { Number = declared }).Data.Notices, n => n.Message.Contains("whole number", StringComparison.Ordinal));

        // Or by the person's setting at import time.
        Assert.DoesNotContain(Read("Feature,Actual\nA,12\nA,13\n", profile, new ScanSettings { Number = NumberProfile.Point }).Data.Notices, n => n.Message.Contains("whole number", StringComparison.Ordinal));

        // A value written with a decimal point shows the file uses one (50.0 is not a whole number as written); other delimiters are not the pattern.
        Assert.DoesNotContain(Read("Feature,Actual\nA,12\nA,13.5\n", profile).Data.Notices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.DoesNotContain(Read("Feature,Actual\nA,50.0\nA,50.0\n", profile).Data.Notices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.DoesNotContain(Read("Feature;Actual\nA;12\nA;13\n", profile with { Delimiter = ScanDelimiter.Semicolon }).Data.Notices, n => n.Message.Contains("whole number", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Split_Decimal_Comma_In_A_Named_Unmapped_Column_Is_Refused_Row_By_Row_And_A_Decimal_Point_Elsewhere_Does_Not_Clear_It()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Point", Deviation: "Dev"), [], Delimiter: ScanDelimiter.Comma);

        // "1,012" is "1" and "012": the 012 lands under Status, a column nothing is mapped to.
        var ex = Assert.Throws<ImportRefusedException>(() => Read("Point,Dev,Status\nGroove,1,012\nGroove,-0,020\n", profile));
        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("'1' is followed by '012'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("line 2", ex.Message, StringComparison.Ordinal);

        // One row that really does carry a decimal point does not make the table safe: the pattern is checked in every row.
        var mixed = Assert.Throws<ImportRefusedException>(() => Read("Point,Dev,Status\nGroove,0.5,ok\nGroove,1,012\nGroove,0.25,ok\n", profile));
        Assert.Contains("line 3", mixed.Message, StringComparison.Ordinal);

        // The same table with the format stated is read as it says; the pattern needs a whole number and a leading-zero neighbour that nothing is mapped to.
        Assert.Equal(2, Read("Point,Dev,Status\nGroove,1,012\nGroove,-0,020\n", profile with { Number = NumberProfile.Point }).Data.Observations.Count);
        Assert.Equal(2, Read("Point,Dev,Status\nGroove,1,12\nGroove,2,5\n", profile).Data.Observations.Count);
        Assert.Equal(2, Read("Point,Dev,Status\nGroove,1.5,012\nGroove,2.5,020\n", profile).Data.Observations.Count);

        // A mapped neighbour (a serial such as 007) is a serial, not a fraction.
        var withSerial = new ScanProfile("p", 1, "P", new ScanColumns("Point", Deviation: "Dev", Serial: "Serial"), [], Delimiter: ScanDelimiter.Comma);
        Assert.Equal(2, Read("Point,Dev,Serial\nGroove,1,007\nGroove,2,008\n", withSerial).Data.Observations.Count);
    }

    [Fact]
    public void A_Split_Fraction_Without_A_Leading_Zero_Is_Refused_When_The_File_Mixes_Decimal_Marks()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Point", Deviation: "Dev"), [], Delimiter: ScanDelimiter.Comma);

        // "1,5" is "1" and "5"; another row writes 0.5, so the file uses both marks. The order of the rows does not matter.
        var ex = Assert.Throws<ImportRefusedException>(() => Read("Point,Dev,Status\nG,1,5\nG,0.5,ok\n", profile));
        Assert.Equal(ImportRefusalCode.MalformedStructure, ex.Code);
        Assert.Contains("line 2 has '1' followed by '5'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("mixes decimal marks", ex.Message, StringComparison.Ordinal);
        Assert.Throws<ImportRefusedException>(() => Read("Point,Dev,Status\nG,0.5,ok\nG,1,5\n", profile));

        // Stating the format lifts it; so does a file with no point decimal anywhere (a count column of whole numbers is not evidence) and a mapped neighbour.
        Assert.Equal(2, Read("Point,Dev,Status\nG,1,5\nG,0.5,ok\n", profile with { Number = NumberProfile.Point }).Data.Observations.Count);
        Assert.Equal(2, Read("Point,Dev,Status\nG,1,5\nG,2,6\n", profile).Data.Observations.Count);
        var withCount = new ScanProfile("p", 1, "P", new ScanColumns("Point", Deviation: "Dev", Serial: "Status"), [], Delimiter: ScanDelimiter.Comma);
        Assert.Equal(2, Read("Point,Dev,Status\nG,1,5\nG,0.5,6\n", withCount).Data.Observations.Count);
    }

    // ---- H2 -------------------------------------------------------------------

    [Fact]
    public void The_Statistics_Phase_Honours_The_Budget_And_The_Callers_Cancellation()
    {
        var many = Obs(Enumerable.Range(0, 200_000).Select(i => 10 + (i % 97) * 1e-3));

        var expired = new ImportBudget(ImportLimits.Scan with { Timeout = TimeSpan.FromMilliseconds(1) });
        Thread.Sleep(20);
        Samples.Refuses(ImportRefusalCode.Timeout, () => ScanStatistics.Compute("F", string.Empty, many, OneFile, new ScanSettings(), expired));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = new ImportBudget(ImportLimits.Scan, cts.Token);
        Assert.ThrowsAny<OperationCanceledException>(() => ScanStatistics.Compute("F", string.Empty, many, OneFile, new ScanSettings(), cancelled));
    }

    // ---- M4 -------------------------------------------------------------------

    [Fact]
    public void The_Same_File_Twice_Is_Refused_Rather_Than_Doubling_N_And_Hiding_The_Small_Sample_Warning()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []);
        var table = "Feature,Actual\nA,1.0\nA,1.1\nA,0.9\n";

        var ex = Assert.Throws<ImportRefusedException>(() => ScanImporter.Analyse([new ScanFileInput(Samples.Text(table, "one.csv")), new ScanFileInput(Samples.Text(table, "copy.csv"))], profile));

        Assert.Contains("'copy.csv' is the same file as 'one.csv'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("identical SHA-256", ex.Message, StringComparison.Ordinal);

        // A different file with the same parts is allowed (it is a different file) - the serial warning is the check for that.
        var other = ScanImporter.Analyse([new ScanFileInput(Samples.Text(table, "one.csv")), new ScanFileInput(Samples.Text(table + "A,1.05\n", "two.csv"))], profile);
        Assert.Equal(7, other.Stats[0].Count);
    }

    [Fact]
    public void The_Tables_Together_Are_Held_To_A_Total_Byte_Cap()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), []);
        var files = Enumerable.Range(0, 4).Select(i => new ScanFileInput(Samples.Text("Feature,Actual\n" + string.Concat(Enumerable.Range(0, 100).Select(j => $"A,{i}.{j:000}\n")), $"f{i}.csv"))).ToList();

        Samples.Refuses(ImportRefusalCode.TooLarge, () => ScanImporter.Analyse(files, profile, new ScanSettings { MaxTotalBytes = 1000 }));
        Assert.Equal(400, ScanImporter.Analyse(files, profile, new ScanSettings { MaxTotalBytes = 10_000 }).Stats[0].Count);
    }

    // ---- M5, L4 ---------------------------------------------------------------

    [Fact]
    public void Leaving_An_Outlier_Out_Does_Not_Move_Later_Parts_Into_Other_Subgroups()
    {
        double[] v =
        [
            10.00, 10.01, 9.99, 12.50, 10.00,
            10.02, 10.01, 10.03, 10.02, 10.01,
            9.99, 10.00, 9.98, 10.01, 10.00,
            10.01, 10.02, 10.00, 10.03, 10.01,
        ];

        var s = ScanStatistics.Compute("F", string.Empty, Obs(v), OneFile, new ScanSettings { SubgroupSize = 5, ExcludeOutliers = true });

        // python3: the subgroups stay as cut (the first loses its outlier and keeps four values): pooled sigma 0.010066445913694117 on 15 degrees of freedom, not the 0.012909944487357782 of re-cut groups;
        // divided by c4(16) = 0.983483531615841 the within sigma is 0.010235500229632903, Cp = 0.2/(6 x 0.0102355) = 3.2566394006645165, Cpk = 0.0926316/(3 x 0.0102355) = 3.016676497457689
        // (uncorrected 3.3113 and 3.0673).
        Assert.Equal(19, s.Count);
        Assert.Equal(10.00736842105263, s.Mean, 12);
        Assert.Equal(0.01326738074424858, s.Sigma, 12);
        Assert.Equal(0.010235500229632903, s.SigmaWithin, 12);
        Assert.Equal(0.983483531615841, s.WithinC4!.Value, 12);
        Assert.Equal(3.2566394006645165, s.Cp!.Value, 9);
        Assert.Equal(3.016676497457689, s.Cpk!.Value, 9);
        Assert.Equal(2.512427582798002, s.Pp!.Value, 9);
    }

    [Fact]
    public void A_Zero_Within_Sigma_Silences_Only_The_Within_Indices_And_Says_Which()
    {
        double[] w = [10.0, 10.0, 10.1, 10.1, 10.2, 10.2, 10.3, 10.3];

        var s = ScanStatistics.Compute("F", string.Empty, Obs(w, lsl: 9.9, usl: 10.6), OneFile, new ScanSettings { SubgroupSize = 2 });

        Assert.Equal(0, s.SigmaWithin);
        Assert.Null(s.Cp);
        Assert.Null(s.Cpk);
        Assert.Equal(0.9761033642897521, s.Pp!.Value, 9);
        Assert.Equal(0.697216688778395, s.Ppk!.Value, 9);
        Assert.Contains(s.Warnings, x => x.Contains("within-subgroup standard deviation is zero", StringComparison.Ordinal));
        Assert.DoesNotContain(s.Warnings, x => x.Contains("every value is the same", StringComparison.Ordinal));
    }

    // ---- L1 -------------------------------------------------------------------

    [Theory]
    [InlineData("<<b>script>alert(1)<</b>/script>", "script")]
    [InlineData("<<<b>i>mg src=x>>", "mg src=x")]
    [InlineData("a<scr<b>ipt>b", "ab")]
    public void Nested_Markup_Is_Stripped_Until_Nothing_Is_Left(string raw, string note)
    {
        var clean = ScanText.Clean(raw, 200, out var changed);

        Assert.True(changed, note);
        Assert.DoesNotContain("<script", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("</", clean, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http\u200B://evil.example/x")]
    [InlineData("java\u00ADscript:alert(1)")]
    [InlineData("HTTPS\u2060://evil")]
    [InlineData("=HYPERLINK (\"http://evil\",\"x\")")]
    [InlineData("= HYPERLINK(\"http://evil\")")]
    [InlineData("=IMAGE (\"http://evil/x.png\")")]
    [InlineData("=cmd|' /C calc'!A0")]
    [InlineData("www.evil.example")]
    public void Links_Hidden_By_Invisible_Characters_Or_Spaces_Are_Still_Removed(string raw)
    {
        var clean = ScanText.Clean(raw, 200, out var changed);

        Assert.True(changed);
        Assert.Contains("[link removed]", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Plain_Names_Pass_Through_And_The_Cap_Is_Enforced()
    {
        Assert.Equal("Hole Ø5 < 0.1 (D_g)", ScanText.Clean("Hole Ø5 < 0.1 (D_g)", 200, out var changed));
        Assert.False(changed);

        var cut = ScanText.Clean(new string('x', 500), 50, out var cutChanged);
        Assert.Equal(50, cut.Length);
        Assert.True(cutChanged);

        Assert.Equal(10, ScanText.Clean(string.Concat(Enumerable.Repeat("😀", 40)), 11, out _).Length is 10 or 11 ? 10 : 0);
    }

    // ---- L2 -------------------------------------------------------------------

    [Fact]
    public void Escaped_Control_And_Formatting_Characters_Unknown_Properties_And_Loose_Target_Spellings_Are_Refused()
    {
        const string Head = "{\"id\":\"p\",\"version\":1,\"title\":\"";
        const string Tail = "\",\"columns\":{\"feature\":\"F\",\"actual\":\"A\"},\"features\":[]}";

        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanProfileJson.Read(Head + "bad\\u0000title" + Tail));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanProfileJson.Read(Head + "evil\\u202Etitle" + Tail));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanProfileJson.Read(Head + "ok" + Tail.Replace("\"features\":[]", "\"features\":[],\"surprise\":1", StringComparison.Ordinal)));
        Samples.Refuses(ImportRefusalCode.MalformedStructure, () => ScanProfileJson.Read(Head + "ok\",\"features\":[{\"feature\":\"zero\\u200Bwidth\",\"target\":\"D\"}],\"x\":\"" + Tail.Replace("\",\"columns", "\",\"columns", StringComparison.Ordinal)));

        Assert.False(ScanTarget.TryParse("D:mean,sigma", out _));
        Assert.False(ScanTarget.TryParse("D: sigma", out _));
        Assert.False(ScanTarget.TryParse("D:1", out _));
        Assert.False(ScanProfile.IsPosition("#03", out _));
        Assert.True(ScanProfile.IsPosition("#3", out _));

        // A BOM in front of the text is dropped, as the panel does, so a direct caller is not refused for it.
        Assert.Equal("p", ScanProfileJson.Read("\uFEFF" + Head + "ok" + Tail).Id);
    }

    // ---- L3 -------------------------------------------------------------------

    [Fact]
    public void A_Deviation_Only_Table_With_Absolute_Limits_Is_Reported_And_Used_Without_Limits()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Deviation: "Dev", UpperTolerance: "Usl", LowerTolerance: "Lsl"), [], Delimiter: ScanDelimiter.Comma, ToleranceMode: ScanToleranceMode.AbsoluteLimits);

        var a = Read("Feature,Dev,Usl,Lsl\nA,0.01,10.05,9.95\nA,-0.02,10.05,9.95\nA,0.00,10.05,9.95\n", profile);

        var s = Assert.Single(a.Stats);
        Assert.Null(s.Usl);
        Assert.Null(s.Cpk);
        Assert.Equal(3, a.Data.Problems.Single(p => p.Kind == ScanProblemKind.Tolerance).Count);
    }

    // ---- L6 / profile text --------------------------------------------------------

    [Fact]
    public void A_Grouping_Mark_That_Changed_A_Value_Is_Reported()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), [], Delimiter: ScanDelimiter.Semicolon, Number: new NumberProfile(DecimalMark.Comma, '.'));

        var a = Read("Feature;Actual\nA;1.500\nA;1,5\nA;2,5\n", profile);

        Assert.Equal([1500.0, 1.5, 2.5], a.Data.Observations.Select(o => o.Value).ToArray());
        var grouping = a.Data.Problems.Single(p => p.Kind == ScanProblemKind.Grouping);
        Assert.Equal(1, grouping.Count);
        Assert.Contains("grouping mark", grouping.Examples[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Two_Names_That_Differ_Only_In_Case_Are_Reported_As_Merged()
    {
        var a = Read("Feature,Actual\nD,10.0\nd,10.1\nD,10.2\nd,10.1\n", Comma());

        var s = Assert.Single(a.Stats);
        Assert.Equal(4, s.Count);
        var merged = a.Data.Problems.Single(p => p.Kind == ScanProblemKind.Text && p.Column == "feature");
        Assert.Equal(1, merged.Count);
        Assert.Contains("'d' was read as the same feature as 'D'", merged.Examples[0], StringComparison.Ordinal);

        // Case-sensitive: two features.
        Assert.Equal(2, Read("Feature,Actual\nD,10.0\nd,10.1\nD,10.2\nd,10.1\n", Comma() with { IgnoreCase = false }).Stats.Count);
    }

    // ---- M2 policy / M3 sigma column ----------------------------------------------

    private static CalcTemplate MapperTemplate { get; } = new(
        "test.target", "1.0.0", CalcDiscipline.Mechanical, "T", "s", "m",
        [new CalcInputSpec("D", "D", CalcInputKind.Quantity, "mm", Default: 1)],
        [], [], [], []);

    private static readonly IReadOnlyList<CalcInputValue> MapperCurrent = [CalcInputValue.Quantity("D", 1, "mm")];

    private static CalcTemplate IsoFit { get; } = new(
        "mech.iso286-fit", "1.1.0", CalcDiscipline.Mechanical, "M2", "s", "m",
        [
            new CalcInputSpec("D", "Nominal size", CalcInputKind.Quantity, "mm", Default: 25),
            new CalcInputSpec("ES_override", "ES", CalcInputKind.Quantity, "µm", IsOptional: true),
            new CalcInputSpec("EI_override", "EI", CalcInputKind.Quantity, "µm", IsOptional: true),
            new CalcInputSpec("es_override", "es", CalcInputKind.Quantity, "µm", IsOptional: true),
            new CalcInputSpec("ei_override", "ei", CalcInputKind.Quantity, "µm", IsOptional: true),
        ],
        [], [], [], []);

    private static ScanMappingResult MapIso(string target, string table = "Feature,Nominal,Actual\nSeat,25,25.010\nSeat,25,25.014\nSeat,25,25.012\n")
    {
        var profile = new ScanProfile("iso", 1, "ISO", new ScanColumns("Feature", Nominal: "Nominal", Actual: "Actual"), [new ScanFeatureEntry("Seat", target)], Delimiter: ScanDelimiter.Comma);
        return ScanMapper.Map(Read(table, profile), IsoFit, [CalcInputValue.Quantity("D", 25, "mm")], At);
    }

    [Fact]
    public void M2_Takes_The_Nominal_Or_Deviations_From_It_But_Never_A_Measured_Mean_As_The_Basic_Size()
    {
        var mean = MapIso("D");
        Assert.False(mean.CanApply);
        Assert.Contains("basic size", mean.Refusals[0], StringComparison.Ordinal);
        Assert.Contains("count the deviation twice", mean.Refusals[0], StringComparison.Ordinal);
        Assert.Contains("D:nominal", mean.Refusals[0], StringComparison.Ordinal);

        Assert.False(MapIso("D:sigma").CanApply);

        var nominal = MapIso("D:nominal");
        Assert.True(nominal.CanApply, string.Join(" ", nominal.Refusals));
        Assert.Equal(25, nominal.Inputs.Single(i => i.Symbol == "D").Number!.Value, 12);

        // Deviation statistics from the basic size, in micrometres: max 25.014 - 25 = 14, min 10, mean 12.
        Assert.Equal(14, MapIso("ES_override:max").Inputs.Single(i => i.Symbol == "ES_override").Number!.Value, 9);
        Assert.Equal(10, MapIso("EI_override:min").Inputs.Single(i => i.Symbol == "EI_override").Number!.Value, 9);
        var ei = MapIso("ei_override").Inputs.Single(i => i.Symbol == "ei_override");
        Assert.Equal(12, ei.Number!.Value, 9);
        Assert.Equal("µm", ei.Unit);

        var sigma = MapIso("es_override:sigma");
        Assert.False(sigma.CanApply);
        Assert.Contains("takes a deviation from the basic size, so a spread is not one", sigma.Refusals[0], StringComparison.Ordinal);
    }

    [Fact]
    public void M2_Deviations_Also_Come_From_A_Deviation_Only_Table_And_Need_A_Nominal_Otherwise()
    {
        var profile = new ScanProfile("iso", 1, "ISO", new ScanColumns("Feature", Deviation: "Dev"), [new ScanFeatureEntry("Seat", "ES_override:max")], Delimiter: ScanDelimiter.Comma);
        var result = ScanMapper.Map(Read("Feature,Dev\nSeat,0.010\nSeat,0.014\nSeat,0.012\n", profile), IsoFit, [CalcInputValue.Quantity("D", 25, "mm")], At);
        Assert.True(result.CanApply, string.Join(" ", result.Refusals));
        Assert.Equal(14, result.Inputs.Single(i => i.Symbol == "ES_override").Number!.Value, 9);

        var noNominal = new ScanProfile("iso", 1, "ISO", new ScanColumns("Feature", Actual: "Actual"), [new ScanFeatureEntry("Seat", "ES_override")], Delimiter: ScanDelimiter.Comma);
        var refused = ScanMapper.Map(Read("Feature,Actual\nSeat,25.010\nSeat,25.014\n", noNominal), IsoFit, [CalcInputValue.Quantity("D", 25, "mm")], At);
        Assert.False(refused.CanApply);
        Assert.Contains("states no nominal", refused.Refusals[0], StringComparison.Ordinal);
    }

    private static CalcTemplate Stack(bool withSigma) => new(
        "test.stack", "1.0.0", CalcDiscipline.Mechanical, "S", "s", "m",
        [
            new CalcInputSpec("contributors", "C", CalcInputKind.Table, Columns: new[]
            {
                new CalcColumnSpec("nominal", "N", CalcInputKind.Quantity, "mm"),
                new CalcColumnSpec("upper", "U", CalcInputKind.Quantity, "mm"),
                new CalcColumnSpec("lower", "L", CalcInputKind.Quantity, "mm", IsOptional: true),
                new CalcColumnSpec("distribution", "D", CalcInputKind.Choice, Choices: ["normal"], IsOptional: true),
                new CalcColumnSpec("shift", "S", CalcInputKind.Quantity, "mm", IsOptional: true),
            }.Concat(withSigma ? [new CalcColumnSpec("sigma", "σ", CalcInputKind.Quantity, "mm", IsOptional: true)] : []).ToList()),
            new CalcInputSpec("k", "k", CalcInputKind.Number, Default: 3),
        ],
        [], [], [], []);

    private static IReadOnlyList<CalcInputValue> StackCurrent(double k = 3) =>
    [
        CalcInputValue.Table("contributors", [CalcRow.Of(("nominal", new CalcCell(51.5, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm")))]),
        CalcInputValue.Of("k", k),
    ];

    private static ScanAnalysis FiftyOne() =>
        Read("Feature,Nominal,Actual\nG,51.5,51.480\nG,51.5,51.490\nG,51.5,51.485\nG,51.5,51.495\nG,51.5,51.480\nG,51.5,51.490\n", new ScanProfile("g", 1, "G", new ScanColumns("Feature", Nominal: "Nominal", Actual: "Actual"), [new ScanFeatureEntry("G", "contributors[0]")], Delimiter: ScanDelimiter.Comma));

    [Fact]
    public void A_Stack_With_A_Sigma_Column_Gets_The_Measured_Sigma_Itself_And_The_Band_As_Plus_Minus_K_Sigma()
    {
        var a = FiftyOne();
        var result = ScanMapper.Map(a, Stack(withSigma: true), StackCurrent(k: 2.5), At);

        Assert.True(result.CanApply, string.Join(" ", result.Refusals));
        var row = result.Inputs.Single(i => i.Symbol == "contributors").Rows![0];
        var sigma = a.Stats[0].Sigma;
        Assert.Equal(sigma, row.Get("sigma")!.Number!.Value, 12);
        Assert.Equal(2.5 * sigma, row.Get("upper")!.Number!.Value, 12);
        Assert.Equal(-2.5 * sigma, row.Get("lower")!.Number!.Value, 12);
        Assert.DoesNotContain(result.Snapshot[0].Pushed!, v => v.Path == "@k");
        Assert.Contains(result.Snapshot[0].Pushed!, v => v.Path == "contributors[0].sigma" && v.Provenance == row.Get("sigma")!.Provenance);
    }

    [Fact]
    public void Without_A_Sigma_Column_The_Record_Keeps_The_K_The_Band_Was_Written_For()
    {
        var result = ScanMapper.Map(FiftyOne(), Stack(withSigma: false), StackCurrent(k: 2.5), At);

        Assert.True(result.CanApply, string.Join(" ", result.Refusals));
        Assert.Equal(2.5, result.Snapshot[0].Pushed!.Single(v => v.Path == "@k").Number);
        Assert.Null(result.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("sigma"));
    }

    // ---- the verifier ---------------------------------------------------------------

    private static ScanImportRecord RecordOf(ScanMappingResult mapping) =>
        new(ScanImportRecord.KeyFor("g"), "g", 1, "scan table", null, null, [new ScanImportFile("a.csv", new string('a', 64), null, null)], At, "tester", mapping.Snapshot);

    private static (ScanMappingResult Mapping, ScanImportRecord Record) Imported(bool withSigma = true, double k = 3)
    {
        var mapping = ScanMapper.Map(FiftyOne(), Stack(withSigma), StackCurrent(k), At);
        return (mapping, RecordOf(mapping));
    }

    private static IReadOnlyList<CalcInputValue> WithCell(IReadOnlyList<CalcInputValue> inputs, string column, Func<CalcCell, CalcCell> change) =>
        inputs.Select(i => i.Symbol != "contributors" ? i : i with
        {
            Rows = [new CalcRow(new Dictionary<string, CalcCell>(i.Rows![0].Cells) { [column] = change(i.Rows[0].Cells[column]) }), .. i.Rows.Skip(1)],
        }).ToList();

    [Fact]
    public void Values_The_Import_Wrote_Verify_Clean()
    {
        var (mapping, record) = Imported();

        var verified = ScanImportVerifier.Verify(mapping.Inputs, [record]);

        Assert.Empty(verified.Downgraded);
        Assert.Equal(string.Empty, ScanImportVerifier.Describe(verified.Downgraded));
    }

    [Fact]
    public void An_Edited_Value_Keeps_No_Scan_Claim_And_The_Person_Is_Told()
    {
        var (mapping, record) = Imported();
        var edited = WithCell(mapping.Inputs, "upper", c => c with { Number = 0.5 });

        var verified = ScanImportVerifier.Verify(edited, [record]);

        Assert.Equal(["'contributors[0].upper'"], verified.Downgraded);
        var cell = verified.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("upper")!;
        Assert.Equal(CalcProvenanceKind.Manual, cell.Provenance!.Kind);
        Assert.Equal(0.5, cell.Number);
        Assert.Contains("kept as a manual entry", ScanImportVerifier.Describe(verified.Downgraded), StringComparison.Ordinal);
        Assert.Equal(CalcProvenanceKind.ScanImport, verified.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("lower")!.Provenance!.Kind);
    }

    [Fact]
    public void Forged_Free_Text_In_The_Reference_Is_Caught_Because_The_Whole_Provenance_Must_Be_The_One_Written()
    {
        var (mapping, record) = Imported();

        foreach (var (from, to) in new[] { ("scan table", "ZEISS Calypso 8"), ("|n=6|", "|S001..S300 (n=300)|"), ("|G", "|Seal groove (KC)") })
        {
            var forged = WithCell(mapping.Inputs, "upper", c => c with { Provenance = c.Provenance! with { Reference = c.Provenance.Reference!.Replace(from, to, StringComparison.Ordinal) } });
            Assert.Single(ScanImportVerifier.Verify(forged, [record]).Downgraded);
        }

        var changedProfile = WithCell(mapping.Inputs, "upper", c => c with { Provenance = c.Provenance! with { Revision = "other@2" } });
        Assert.Single(ScanImportVerifier.Verify(changedProfile, [record]).Downgraded);
    }

    [Fact]
    public void A_Claim_On_A_Whole_Table_Is_Never_Honoured_And_A_Hand_Typed_Row_With_Copied_Provenance_Fails_Its_Own_Place()
    {
        var (mapping, record) = Imported();
        var copied = mapping.Inputs.Single(i => i.Symbol == "contributors").Rows![0].Get("upper")!.Provenance!;

        var wholeTable = mapping.Inputs.Select(i => i.Symbol == "contributors" ? i with { Provenance = copied } : i).ToList();
        var verified = ScanImportVerifier.Verify(wholeTable, [record]);
        Assert.Contains("'contributors'", verified.Downgraded);
        Assert.Equal(CalcProvenanceKind.Manual, verified.Inputs.Single(i => i.Symbol == "contributors").Provenance!.Kind);

        var typedRow = mapping.Inputs.Select(i => i.Symbol != "contributors" ? i : i with
        {
            Rows = [.. i.Rows!, CalcRow.Of(("nominal", new CalcCell(1, "mm")), ("upper", new CalcCell(0.1, "mm", Provenance: copied)))],
        }).ToList();
        Assert.Equal(["'contributors[1].upper'"], ScanImportVerifier.Verify(typedRow, [record]).Downgraded);
    }

    [Fact]
    public void A_Row_Moved_Away_From_Its_Place_Loses_The_Claim_And_A_Repeated_Dimension_Cannot_Borrow_The_First_Ones()
    {
        var (mapping, record) = Imported();
        var moved = mapping.Inputs.Select(i => i.Symbol != "contributors" ? i : i with { Rows = [CalcRow.Of(("nominal", new CalcCell(2, "mm"))), .. i.Rows!] }).ToList();
        Assert.True(ScanImportVerifier.Verify(moved, [record]).Downgraded.Count >= 4);

        var seen = new Dictionary<string, int>();
        var first = CalcRow.Of(("dim", new CalcCell(Text: "D_s")));
        Assert.Equal("measured{D_s}", ScanImportVerifier.RowKey("measured", 0, first, seen));
        Assert.Equal("measured{D_s}~2", ScanImportVerifier.RowKey("measured", 1, first, seen));
        Assert.Equal("measured[2]", ScanImportVerifier.RowKey("measured", 2, CalcRow.Of(("x", new CalcCell(1))), seen));
    }

    [Fact]
    public void Where_The_Band_Was_Written_For_One_K_Changing_K_Lapses_The_Claim_And_With_A_Sigma_Column_It_Does_Not()
    {
        var (without, withoutRecord) = Imported(withSigma: false, k: 3);
        var changedK = without.Inputs.Select(i => i.Symbol == "k" ? i with { Number = 6 } : i).ToList();
        Assert.True(ScanImportVerifier.Verify(changedK, [withoutRecord]).Downgraded.Count >= 3);
        Assert.Empty(ScanImportVerifier.Verify(without.Inputs, [withoutRecord]).Downgraded);

        var (with, withRecord) = Imported(withSigma: true, k: 3);
        var changed = with.Inputs.Select(i => i.Symbol == "k" ? i with { Number = 6 } : i).ToList();
        Assert.Empty(ScanImportVerifier.Verify(changed, [withRecord]).Downgraded);
    }

    [Fact]
    public void An_Earlier_Record_Is_Kept_While_A_Value_Still_Stands_On_It_And_Dropped_When_Nothing_Does()
    {
        var (mapping, first) = Imported();
        var other = first with { ImportedAt = At.AddHours(1), Features = [first.Features[0] with { SetSha256 = new string('b', 64), Pushed = [] }] };

        // The later record names another file: the values still stand on the earlier one.
        var kept = ScanImportVerifier.Merge([first], other, mapping.Inputs);
        Assert.Equal(2, kept.Count);
        Assert.Same(other, kept[^1]);
        Assert.Empty(ScanImportVerifier.Verify(mapping.Inputs, kept).Downgraded);

        // Nothing refers to the earlier file any more.
        var scrubbed = mapping.Inputs.Select(i => i.Symbol != "contributors" ? i : i with { Rows = [CalcRow.Of(("nominal", new CalcCell(1, "mm")))] }).ToList();
        Assert.Single(ScanImportVerifier.Merge([first], other, scrubbed));

        // The cap drops the oldest.
        var many = Enumerable.Range(0, 30).Select(n => first with { ImportedAt = At.AddMinutes(n) }).ToList();
        Assert.Equal(5, ScanImportVerifier.Merge(many, other, mapping.Inputs, maxRecords: 5).Count);
    }

    // ---- N4: a profile version that drops a feature does not strip its values ----------------------------------------

    [Fact]
    public void A_New_Profile_Version_That_Drops_A_Feature_Keeps_The_Record_That_Still_Backs_That_Features_Values()
    {
        var table = "Feature,Nominal,Actual\nG,51.5,51.480\nG,51.5,51.490\nG,51.5,51.485\nH,10,10.01\nH,10,10.02\nH,10,10.00\n";
        ScanProfile Version(int version, params ScanFeatureEntry[] entries) => new("g", version, "G", new ScanColumns("Feature", Nominal: "Nominal", Actual: "Actual"), entries, Delimiter: ScanDelimiter.Comma, Number: NumberProfile.Point);
        var template = TwoRowStack();
        IReadOnlyList<CalcInputValue> current =
        [
            CalcInputValue.Table("contributors",
            [
                CalcRow.Of(("nominal", new CalcCell(51.5, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm"))),
                CalcRow.Of(("nominal", new CalcCell(10, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm"))),
            ]),
            CalcInputValue.Of("k", 3),
        ];

        var one = ScanMapper.Map(Read(table, Version(1, new ScanFeatureEntry("G", "contributors[0]"), new ScanFeatureEntry("H", "contributors[1]"))), template, current, At);
        Assert.True(one.CanApply, string.Join(" ", one.Refusals));
        var first = RecordOf(one) with { ProfileVersion = 1 };

        // Version 2 maps G only: the same file, but H's values are not rewritten and still stand on version 1's record.
        var two = ScanMapper.Map(Read(table, Version(2, new ScanFeatureEntry("G", "contributors[0]"))), template, one.Inputs, At.AddMinutes(5));
        Assert.True(two.CanApply, string.Join(" ", two.Refusals));
        var second = RecordOf(two) with { ProfileVersion = 2, ImportedAt = At.AddMinutes(5) };

        var merged = ScanImportVerifier.Merge([first], second, two.Inputs);
        Assert.Equal(2, merged.Count);
        Assert.Empty(ScanImportVerifier.Verify(two.Inputs, merged).Downgraded);

        // Without the earlier record H's values would lose their provenance - what the hash rule alone got wrong, since both records name the same file.
        Assert.Contains("'contributors[1].upper'", ScanImportVerifier.Verify(two.Inputs, [second]).Downgraded);

        // Once nothing cites the old provenance any more, the old record goes.
        var rewritten = ScanMapper.Map(Read(table, Version(3, new ScanFeatureEntry("G", "contributors[0]"), new ScanFeatureEntry("H", "contributors[1]"))), template, two.Inputs, At.AddMinutes(10));
        var third = RecordOf(rewritten) with { ProfileVersion = 3, ImportedAt = At.AddMinutes(10) };
        Assert.Single(ScanImportVerifier.Merge(merged, third, rewritten.Inputs));
    }

    private static CalcTemplate TwoRowStack() => new(
        "test.stack2", "1.0.0", CalcDiscipline.Mechanical, "S", "s", "m",
        [
            new CalcInputSpec("contributors", "C", CalcInputKind.Table, Columns:
            [
                new CalcColumnSpec("nominal", "N", CalcInputKind.Quantity, "mm"),
                new CalcColumnSpec("upper", "U", CalcInputKind.Quantity, "mm"),
                new CalcColumnSpec("lower", "L", CalcInputKind.Quantity, "mm", IsOptional: true),
                new CalcColumnSpec("shift", "S", CalcInputKind.Quantity, "mm", IsOptional: true),
                new CalcColumnSpec("sigma", "σ", CalcInputKind.Quantity, "mm", IsOptional: true),
            ]),
            new CalcInputSpec("k", "k", CalcInputKind.Number, Default: 3),
        ],
        [], [], [], []);

    // ---- N5: full-width forms ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("＝HYPERLINK(\"http://evil\")")]
    [InlineData("＝ＨＹＰＥＲＬＩＮＫ（\"x\"）")]
    [InlineData("=ＨＹＰＥＲＬＩＮＫ(\"x\")")]
    [InlineData("\u3000＝IMAGE (\"x\")")]
    [InlineData("ｈｔｔｐ：／／evil.example/x")]
    [InlineData("ｈｔｔｐｓ://evil")]
    [InlineData("ｗｗｗ．evil．example")]
    [InlineData("ｊａｖａｓｃｒｉｐｔ：alert(1)")]
    public void Full_Width_Forms_Of_Link_Formulas_And_Schemes_Are_Removed_Like_The_ASCII_Ones(string raw)
    {
        var clean = ScanText.Clean(raw, 200, out var changed);

        Assert.True(changed);
        Assert.Contains("[link removed]", clean, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", clean, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("＝", clean, StringComparison.Ordinal);
    }

    [Fact]
    public void Full_Width_Letters_In_An_Ordinary_Name_Are_Left_Alone()
    {
        Assert.Equal("穴Ａ１ Ｄ", ScanText.Clean("穴Ａ１ Ｄ", 50, out var changed));
        Assert.False(changed);
    }
}
