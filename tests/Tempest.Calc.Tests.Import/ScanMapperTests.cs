using Tempest.Calc.Import.Scan;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Import;

/// <summary>Pushing fitted distributions into tolerance-stack contributor rows, measured-dimension rows and single inputs: units by dimension, refusals, provenance, the re-import diff.</summary>
public sealed class ScanMapperTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 25, 9, 30, 15, TimeSpan.Zero);

    /// <summary>The shape of M1's contributor table (the real columns), and of F3's measured table, so the mapper is proved on the same schemas the Library chain uses.</summary>
    private static CalcTemplate Template { get; } = new(
        "test.scan-target", "1.0.0", CalcDiscipline.Mechanical, "Target", "scope", "method",
        [
            new CalcInputSpec(
                "contributors", "Contributors", CalcInputKind.Table,
                Columns:
                [
                    new CalcColumnSpec("name", "Name", CalcInputKind.Text),
                    new CalcColumnSpec("direction", "Direction", CalcInputKind.Choice, Choices: ["adds", "subtracts"]),
                    new CalcColumnSpec("nominal", "Nominal", CalcInputKind.Quantity, "mm"),
                    new CalcColumnSpec("upper", "Upper", CalcInputKind.Quantity, "mm"),
                    new CalcColumnSpec("lower", "Lower", CalcInputKind.Quantity, "mm", IsOptional: true),
                    new CalcColumnSpec("kind", "Kind", CalcInputKind.Choice, Choices: ["size", "position", "profile"], IsOptional: true),
                    new CalcColumnSpec("distribution", "Distribution", CalcInputKind.Choice, Choices: ["normal", "uniform", "triangular"], IsOptional: true),
                    new CalcColumnSpec("shift", "Mean shift", CalcInputKind.Quantity, "mm", IsOptional: true),
                ],
                MaxRows: 200),
            new CalcInputSpec("k", "k", CalcInputKind.Number, Default: 3),
            new CalcInputSpec(
                "measured", "Measured", CalcInputKind.Table, IsOptional: true,
                Columns:
                [
                    new CalcColumnSpec("dim", "Dimension", CalcInputKind.Choice, Choices: ["W", "d_r", "D_s", "D_g", "D_c", "h_f", "b", "e_f"]),
                    new CalcColumnSpec("mean", "Mean", CalcInputKind.Quantity, "mm"),
                    new CalcColumnSpec("sigma", "Sigma", CalcInputKind.Quantity, "mm"),
                    new CalcColumnSpec("dist", "Distribution", CalcInputKind.Choice, Choices: ["Normal", "Uniform", "Triangular"], IsOptional: true),
                ],
                MaxRows: 3),
            new CalcInputSpec("D", "Diameter", CalcInputKind.Quantity, "mm", Default: 50),
            new CalcInputSpec("F", "Force", CalcInputKind.Quantity, "kN", Default: 1),
            new CalcInputSpec("eta", "Efficiency", CalcInputKind.Number, Default: 0.9),
            new CalcInputSpec("grade", "Grade", CalcInputKind.Choice, Choices: ["a", "b"], DefaultChoice: "a"),
        ],
        [], [], [], []);

    private static IReadOnlyList<CalcInputValue> Current(double k = 3) =>
    [
        CalcInputValue.Table("contributors",
        [
            CalcRow.Of(("name", new CalcCell(Text: "Groove root")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(51.4, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm"))),
            CalcRow.Of(("name", new CalcCell(Text: "Bore")), ("direction", new CalcCell(Text: "subtracts")), ("nominal", new CalcCell(56.9, "mm")), ("upper", new CalcCell(0.046, "mm")), ("lower", new CalcCell(0, "mm"))),
        ]),
        CalcInputValue.Of("k", k),
        CalcInputValue.Quantity("D", 50, "mm"),
        CalcInputValue.Quantity("F", 1, "kN"),
        CalcInputValue.Of("eta", 0.9),
        CalcInputValue.Choice("grade", "a"),
    ];

    private static ScanFeatureEntry E(string feature, string target, string? stage = null, bool required = false) => new(feature, target, stage, required);

    private static ScanAnalysis Zeiss(params ScanFeatureEntry[] entries) =>
        ScanImporter.Analyse(Samples.Load("scan/zeiss-groove.csv"), SampleProfiles.Zeiss(entries), new ScanSettings { SoftwareVersion = "2024", Alignment = "RPS-1 best fit" });

    private static ScanMappingResult Map(ScanAnalysis a, IReadOnlyList<CalcInputValue>? current = null) => ScanMapper.Map(a, Template, current ?? Current(), At);

    private static CalcRow Row(ScanMappingResult r, string table, int index) => r.Inputs.Single(i => i.Symbol == table).Rows![index];

    // ---- a tolerance-stack contributor ---------------------------------------

    [Fact]
    public void A_Contributor_Row_Takes_The_Fitted_Normal_As_Nominal_Shift_And_A_Band_That_Reads_Back_Sigma()
    {
        var a = Zeiss(E("D_g groove root", "contributors[0]"));
        var result = Map(a);

        Assert.True(result.CanApply, string.Join(" ", result.Refusals));
        var row = Row(result, "contributors", 0);
        var stat = a.Stats.Single(s => s.Feature == "D_g groove root");

        // Name and direction are the stack's own; the scan's nominal and the fit replace the rest.
        Assert.Equal("Groove root", row.Get("name")!.Text);
        Assert.Equal("adds", row.Get("direction")!.Text);
        Assert.Equal(51.5, row.Get("nominal")!.Number!.Value, 12);
        Assert.Equal("mm", row.Get("nominal")!.Unit);
        Assert.Equal(stat.Mean - 51.5, row.Get("shift")!.Number!.Value, 12);
        Assert.Equal(3 * stat.Sigma, row.Get("upper")!.Number!.Value, 12);
        Assert.Equal(-3 * stat.Sigma, row.Get("lower")!.Number!.Value, 12);
        Assert.Equal("normal", row.Get("distribution")!.Text);

        // The stack reads back: mid-limit = nominal + (u + l)/2 + shift = the measured mean, σ_i = half-band/k = the measured σ.
        var mid = row.Get("nominal")!.Number!.Value + (row.Get("upper")!.Number!.Value + row.Get("lower")!.Number!.Value) / 2 + row.Get("shift")!.Number!.Value;
        Assert.Equal(stat.Mean, mid, 12);
        Assert.Equal(stat.Sigma, (row.Get("upper")!.Number!.Value - row.Get("lower")!.Number!.Value) / 2 / 3, 12);

        // The other row is untouched.
        Assert.Equal(56.9, Row(result, "contributors", 1).Get("nominal")!.Number);
    }

    [Fact]
    public void The_Band_Uses_The_Stacks_Own_K()
    {
        var a = Zeiss(E("D_g groove root", "contributors[0]"));
        var result = Map(a, Current(k: 2));
        var stat = a.Stats.Single(s => s.Feature == "D_g groove root");

        Assert.Equal(2 * stat.Sigma, Row(result, "contributors", 0).Get("upper")!.Number!.Value, 12);
    }

    [Fact]
    public void Every_Written_Cell_Carries_The_Scan_Provenance_With_Software_Version_Stage_Serials_Alignment_Hash_Profile_And_Feature()
    {
        var a = Zeiss(E("D_g groove root", "contributors[0]"));
        var row = Row(Map(a), "contributors", 0);

        foreach (var key in new[] { "nominal", "upper", "lower", "shift", "distribution" })
        {
            var info = ScanImportProvenance.TryParse(row.Get(key)!.Provenance);
            Assert.NotNull(info);
            Assert.Equal("ZEISS Inspect 2024", info.SoftwareAndVersion);
            Assert.Null(info.Stage);
            Assert.Equal("S001..S030 (n=30)", info.Serials);
            Assert.Equal("RPS-1 best fit", info.Alignment);
            Assert.Equal(a.Data.Files[0].Sha256, info.Sha256);
            Assert.Equal(At, info.ImportedAt.ToUniversalTime().AddSeconds(15));
            Assert.Equal("D_g groove root", info.Feature);
            Assert.Equal("zeiss-groove", info.ProfileId);
            Assert.Equal(1, info.ProfileVersion);
        }

        Assert.Equal(CalcProvenanceKind.ScanImport, row.Get("upper")!.Provenance!.Kind);
        Assert.True(row.Get("upper")!.Provenance!.IsWithinLimits);
        Assert.Equal("scan import: " + row.Get("upper")!.Provenance!.Reference + " rev zeiss-groove@1", row.Get("upper")!.Provenance!.ToString());
    }

    [Fact]
    public void A_Row_That_Is_A_Position_Contributor_Does_Not_Take_A_Size_Fit()
    {
        var rows = Current().ToList();
        var table = rows[0];
        var row = table.Rows![0];
        var cells = new Dictionary<string, CalcCell>(row.Cells) { ["kind"] = new CalcCell(Text: "position") };
        rows[0] = table with { Rows = [new CalcRow(cells), table.Rows[1]] };

        var result = Map(Zeiss(E("D_g groove root", "contributors[0]")), rows);

        Assert.False(result.CanApply);
        Assert.Contains("position contributor", result.Refusals[0], StringComparison.Ordinal);
    }

    [Fact]
    public void A_Row_Beyond_The_Table_Is_Refused_And_Told_To_Add_The_Contributor_First()
    {
        var result = Map(Zeiss(E("D_g groove root", "contributors[5]")));

        Assert.False(result.CanApply);
        Assert.Contains("has 2 row(s); there is no row 5", result.Refusals[0], StringComparison.Ordinal);
        Assert.Equal(ScanRowStatus.Refused, result.Rows[0].Status);
    }

    [Fact]
    public void Without_A_Shift_Column_A_Fit_Away_From_The_Nominal_Is_Refused_Not_Folded_Into_The_Band()
    {
        var template = new CalcTemplate(
            "test.no-shift", "1.0.0", CalcDiscipline.Mechanical, "T", "s", "m",
            [
                new CalcInputSpec(
                    "contributors", "C", CalcInputKind.Table,
                    Columns:
                    [
                        new CalcColumnSpec("nominal", "Nominal", CalcInputKind.Quantity, "mm"),
                        new CalcColumnSpec("upper", "U", CalcInputKind.Quantity, "mm"),
                        new CalcColumnSpec("lower", "L", CalcInputKind.Quantity, "mm", IsOptional: true),
                    ]),
            ],
            [], [], [], []);
        var current = new[] { CalcInputValue.Table("contributors", [CalcRow.Of(("nominal", new CalcCell(51.5, "mm")), ("upper", new CalcCell(0.05, "mm")), ("lower", new CalcCell(-0.05, "mm")))]) };

        var result = ScanMapper.Map(Zeiss(E("D_g groove root", "contributors[0]")), template, current, At);

        Assert.False(result.CanApply);
        Assert.Contains("no mean-shift column", result.Refusals[0], StringComparison.Ordinal);
    }

    // ---- a measured dimension -------------------------------------------------

    [Fact]
    public void A_Measured_Row_Is_Added_With_Mean_Sigma_And_The_Normal_Distribution_And_Replaced_On_A_Second_Push()
    {
        var a = Zeiss(E("D_s bore", "measured{D_s}"), E("b groove width", "measured{b}"));
        var first = Map(a);

        Assert.True(first.CanApply, string.Join(" ", first.Refusals));
        var table = first.Inputs.Single(i => i.Symbol == "measured");
        Assert.Equal(2, table.Rows!.Count);
        var ds = table.Rows[0];
        var stat = a.Stats.Single(s => s.Feature == "D_s bore");
        Assert.Equal("D_s", ds.Get("dim")!.Text);
        Assert.Equal(stat.Mean, ds.Get("mean")!.Number!.Value, 12);
        Assert.Equal("mm", ds.Get("mean")!.Unit);
        Assert.Equal(stat.Sigma, ds.Get("sigma")!.Number!.Value, 12);
        Assert.Equal("Normal", ds.Get("dist")!.Text);
        Assert.NotNull(ScanImportProvenance.TryParse(ds.Get("mean")!.Provenance));
        Assert.Null(table.Provenance); // an import never gives a whole table a scan provenance

        // A second push for D_s replaces its row and leaves the other alone.
        var again = ScanMapper.Map(Zeiss(E("D_s bore", "measured{D_s}")), Template, first.Inputs, At);
        Assert.Equal(2, again.Inputs.Single(i => i.Symbol == "measured").Rows!.Count);
    }

    [Fact]
    public void A_Measured_Key_That_The_Table_Does_Not_Offer_Or_A_Full_Table_Is_Refused()
    {
        var unknown = Map(Zeiss(E("D_s bore", "measured{Q_z}")));
        Assert.False(unknown.CanApply);
        Assert.Contains("is not one of the dimensions", unknown.Refusals[0], StringComparison.Ordinal);

        var rows = Current().Append(CalcInputValue.Table("measured",
        [
            CalcRow.Of(("dim", new CalcCell(Text: "W")), ("mean", new CalcCell(3.5, "mm")), ("sigma", new CalcCell(0.01, "mm"))),
            CalcRow.Of(("dim", new CalcCell(Text: "d_r")), ("mean", new CalcCell(50, "mm")), ("sigma", new CalcCell(0.1, "mm"))),
            CalcRow.Of(("dim", new CalcCell(Text: "h_f")), ("mean", new CalcCell(2.7, "mm")), ("sigma", new CalcCell(0.01, "mm"))),
        ])).ToList();
        var full = Map(Zeiss(E("D_s bore", "measured{D_s}")), rows);
        Assert.False(full.CanApply);
        Assert.Contains("already has 3 rows", full.Refusals[0], StringComparison.Ordinal);

        // Replacing an existing key is fine even when the table is full.
        var replace = Map(Zeiss(E("D_s bore", "measured{W}")), rows);
        Assert.True(replace.CanApply, string.Join(" ", replace.Refusals));
    }

    [Fact]
    public void Each_Table_Target_Is_Checked_Against_The_Schema_It_Needs()
    {
        var stackOnMeasured = Map(Zeiss(E("D_g groove root", "measured[0]")));
        Assert.False(stackOnMeasured.CanApply);

        var measuredOnStack = Map(Zeiss(E("D_g groove root", "contributors{D_g}")));
        Assert.False(measuredOnStack.CanApply);
        Assert.Contains("no dim, mean and sigma columns", measuredOnStack.Refusals[0], StringComparison.Ordinal);

        var rowOnScalar = Map(Zeiss(E("D_g groove root", "D[0]")));
        Assert.False(rowOnScalar.CanApply);
        Assert.Contains("is not a table", rowOnScalar.Refusals[0], StringComparison.Ordinal);
    }

    // ---- single inputs ----------------------------------------------------------

    [Theory]
    [InlineData("D", "mean", 51.48546666666667)]
    [InlineData("D:sigma", "sigma", 0.009926395787663347)]
    [InlineData("D:min", "min", 51.458)]
    [InlineData("D:max", "max", 51.505)]
    [InlineData("D:nominal", "nominal", 51.5)]
    public void A_Single_Input_Takes_The_Named_Statistic_In_Its_Own_Unit(string target, string stat, double expected)
    {
        var result = Map(Zeiss(E("D_g groove root", target)));

        Assert.True(result.CanApply, stat + ": " + string.Join(" ", result.Refusals));
        var d = result.Inputs.Single(i => i.Symbol == "D");
        Assert.Equal(expected, d.Number!.Value, 12);
        Assert.Equal("mm", d.Unit);
        Assert.NotNull(ScanImportProvenance.TryParse(d.Provenance));
    }

    [Fact]
    public void Values_Are_Converted_Between_Units_By_Dimension_And_A_Spread_Is_Scaled_Without_An_Offset()
    {
        var profile = new ScanProfile("micron", 1, "Micron table", new ScanColumns("Feature", Actual: "Actual"), [E("A", "contributors[0]"), E("B", "D:sigma")], Unit: "µm");
        var text = "Feature,Actual\nA,51012\nA,51020\nA,50996\nA,51004\nB,10\nB,12\nB,14\n";

        var a = ScanImporter.Analyse(Samples.Text(text), profile);
        var result = Map(a);

        Assert.True(result.CanApply, string.Join(" ", result.Refusals));
        var row = Row(result, "contributors", 0);
        var mean = (51012 + 51020 + 50996 + 51004) / 4.0;
        Assert.Equal(mean / 1000, row.Get("nominal")!.Number!.Value + row.Get("shift")!.Number!.Value, 9);
        var sigmaMm = a.Stats[0].Sigma / 1000;
        Assert.Equal(3 * sigmaMm, row.Get("upper")!.Number!.Value, 9);
        Assert.Equal(2.0 / 1000, result.Inputs.Single(i => i.Symbol == "D").Number!.Value, 12);
    }

    [Fact]
    public void A_Unit_Of_Another_Kind_Is_Refused_By_Dimension()
    {
        var result = Map(Zeiss(E("D_g groove root", "F")));

        Assert.False(result.CanApply);
        Assert.Contains("not the same kind of quantity", result.Refusals[0], StringComparison.Ordinal);

        var plain = Map(Zeiss(E("D_g groove root", "eta")));
        Assert.False(plain.CanApply);
        Assert.Contains("plain number", plain.Refusals[0], StringComparison.Ordinal);

        var choice = Map(Zeiss(E("D_g groove root", "grade")));
        Assert.False(choice.CanApply);
        Assert.Contains("takes a statistic only when it is a number or a quantity", choice.Refusals[0], StringComparison.Ordinal);
    }

    [Fact]
    public void An_Unknown_Input_And_A_Wrong_Template_Are_Refused()
    {
        var unknown = Map(Zeiss(E("D_g groove root", "nope")));
        Assert.Contains("is not an input of test.scan-target", unknown.Refusals[0], StringComparison.Ordinal);

        var profile = SampleProfiles.Zeiss(E("D_g groove root", "D")) with { TemplateId = "mech.other" };
        var wrong = Map(ScanImporter.Analyse(Samples.Load("scan/zeiss-groove.csv"), profile));
        Assert.False(wrong.CanApply);
        Assert.Contains("is for the template mech.other", wrong.Refusals[0], StringComparison.Ordinal);
    }

    // ---- stages, missing, unmapped ----------------------------------------------

    [Fact]
    public void A_Feature_Measured_At_Several_Stages_Needs_The_Stage_Named_And_Then_Fits_That_Population()
    {
        var profile = SampleProfiles.GomTwoStage(E("Bore D25", "D"));
        var a = ScanImporter.Analyse(Samples.Load("scan/gom-two-stage.csv"), profile);

        var ambiguous = Map(a);
        Assert.False(ambiguous.CanApply);
        Assert.Contains("measured at 2 stages ('OP10 rough', 'OP20 finish')", ambiguous.Refusals[0], StringComparison.Ordinal);

        var named = Map(ScanImporter.Analyse(Samples.Load("scan/gom-two-stage.csv"), SampleProfiles.GomTwoStage(E("Bore D25", "D", stage: "OP20 finish"))));
        Assert.True(named.CanApply, string.Join(" ", named.Refusals));
        Assert.Equal(25.003666666666664, named.Inputs.Single(i => i.Symbol == "D").Number!.Value, 12);
        var info = ScanImportProvenance.TryParse(named.Inputs.Single(i => i.Symbol == "D").Provenance)!;
        Assert.Equal("OP20 finish", info.Stage);
        Assert.Equal("P01..P12 (n=12)", info.Serials);
        Assert.Equal("GOM Inspect", info.SoftwareAndVersion);
        Assert.Null(info.Alignment);

        // The other stage is in the tables but not in the profile: reported, not imported, not dropped silently.
        var other = Assert.Single(named.Rows, r => r.Status == ScanRowStatus.Unmapped);
        Assert.Equal("OP10 rough", other.Stage);
    }

    [Fact]
    public void Features_In_The_Tables_But_Not_In_The_Profile_Are_Reported_As_Unmapped_And_Snapshotted()
    {
        var result = Map(Zeiss(E("D_g groove root", "D")));

        Assert.True(result.CanApply);
        Assert.Equal(2, result.Count(ScanRowStatus.Unmapped));
        Assert.Contains(result.Rows, r => r.Status == ScanRowStatus.Unmapped && r.Feature == "b groove width" && r.Message.Contains("not imported", StringComparison.Ordinal));
        Assert.Equal(3, result.Snapshot.Count);
        Assert.Null(result.Snapshot.Single(s => s.Feature == "b groove width").Target);
        Assert.Equal("D", result.Snapshot.Single(s => s.Feature == "D_g groove root").Target);
    }

    [Fact]
    public void A_Feature_The_Profile_Lists_But_The_Tables_Lack_Is_Reported_And_Refused_When_Required()
    {
        var optional = Map(Zeiss(E("D_g groove root", "D"), E("not there", "F")));
        Assert.True(optional.CanApply);
        Assert.Contains(optional.Rows, r => r.Status == ScanRowStatus.Missing && r.Message.Contains("'F' is unchanged", StringComparison.Ordinal));

        var required = Map(Zeiss(E("D_g groove root", "D"), E("not there", "F", required: true)));
        Assert.False(required.CanApply);
        Assert.Contains("requires 'not there'", required.Refusals[0], StringComparison.Ordinal);
    }

    [Fact]
    public void While_Anything_Is_Refused_Nothing_Is_Applied_And_A_Profile_That_Maps_Nothing_Only_Shows_The_Statistics()
    {
        var mixed = Map(Zeiss(E("D_g groove root", "D"), E("b groove width", "F")));
        Assert.False(mixed.CanApply);
        Assert.Contains(mixed.Rows, r => r.Status == ScanRowStatus.Mapped);

        var none = Map(Zeiss());
        Assert.False(none.CanApply);
        Assert.Contains("maps no feature", none.Refusals[0], StringComparison.Ordinal);
        Assert.Equal(3, none.Count(ScanRowStatus.Unmapped));
    }

    [Fact]
    public void A_Feature_With_One_Part_Cannot_Be_Fitted_And_Says_So()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), [E("A", "D")]);
        var a = ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,50.1\n"), profile);

        var result = Map(a);

        Assert.False(result.CanApply);
        Assert.Contains("cannot be fitted", result.Refusals[0], StringComparison.Ordinal);
        Assert.Contains("at least 2 parts", result.Refusals[0], StringComparison.Ordinal);
    }

    [Fact]
    public void The_Statistics_Warnings_Of_The_Pushed_Features_Are_Carried_With_The_Result()
    {
        var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), [E("A", "D")]);
        var a = ScanImporter.Analyse(Samples.Text("Feature,Actual\nA,50.1\nA,50.2\nA,50.15\n"), profile);

        var result = Map(a);

        Assert.True(result.CanApply);
        Assert.Contains(result.Warnings, w => w.Contains("fewer than 30 parts", StringComparison.Ordinal));
    }

    [Fact]
    public void Deviation_Only_Tables_Move_The_Contributors_Shift_And_Leave_Its_Nominal()
    {
        var profile = new ScanProfile("dev", 1, "Deviations", new ScanColumns("Feature", Deviation: "Dev"), [E("A", "contributors[0]")]);
        var a = ScanImporter.Analyse(Samples.Text("Feature,Dev\nA,0.010\nA,-0.020\nA,0.000\nA,0.006\n"), profile);

        var result = Map(a);

        Assert.True(result.CanApply, string.Join(" ", result.Refusals));
        var row = Row(result, "contributors", 0);
        Assert.Equal(51.4, row.Get("nominal")!.Number!.Value, 12);
        Assert.Equal(-0.001, row.Get("shift")!.Number!.Value, 12);
        Assert.NotNull(ScanImportProvenance.TryParse(row.Get("shift")!.Provenance));
    }

    // ---- record and diff -----------------------------------------------------------

    [Fact]
    public void The_Diff_Reports_Added_Removed_And_Changed_Features_Between_Two_Imports()
    {
        var first = Map(Zeiss(E("D_g groove root", "D"), E("b groove width", "contributors[1]")));
        var record = new ScanImportRecord(ScanImportRecord.KeyFor("zeiss-groove"), "zeiss-groove", 1, "ZEISS Inspect", "2024", "RPS-1", [new ScanImportFile("a.csv", new string('a', 64), null, null)], At, "tester", first.Snapshot);

        Assert.True(ScanImportDiff.Compute(null, first.Snapshot).FirstImport);
        var same = ScanImportDiff.Compute(record, first.Snapshot);
        Assert.False(same.FirstImport);
        Assert.False(same.HasChanges);
        Assert.Equal("No change since the previous import.", same.Summary);

        var changed = first.Snapshot.Select(s => s.Feature == "D_g groove root" ? s with { Mean = s.Mean + 0.01 } : s).Where(s => s.Feature != "b groove width").Append(first.Snapshot[0] with { Feature = "new feature" }).ToList();
        var diff = ScanImportDiff.Compute(record, changed);

        Assert.Equal(1, diff.Count(ScanChangeKind.Added));
        Assert.Equal(1, diff.Count(ScanChangeKind.Removed));
        Assert.Equal(1, diff.Count(ScanChangeKind.Changed));
        Assert.Contains("1 added, 1 removed, 1 changed", diff.Summary, StringComparison.Ordinal);
        Assert.True(diff.Changes.Single(c => c.Kind == ScanChangeKind.Changed).AffectsInput);
        Assert.Contains("n = 30, mean", diff.Changes.Single(c => c.Kind == ScanChangeKind.Changed).After, StringComparison.Ordinal);
        Assert.Contains("added", ScanImportReport.ChangesCsv(diff), StringComparison.Ordinal);
    }

    // ---- provenance ------------------------------------------------------------------

    [Fact]
    public void Provenance_Stays_Within_Its_Cap_By_Shortening_Text_Never_The_Hash_And_Reads_Back()
    {
        var profile = SampleProfiles.Zeiss(E("x", "D"));
        var source = new ScanProvenanceSource(new string('S', 80), new string('V', 80), new string('T', 80), new string('N', 80), new string('A', 80), new string('c', 64));

        var p = ScanImportProvenance.Create(source, profile, new string('F', 120), At);

        Assert.True(p.IsWithinLimits, p.Reference);
        Assert.Contains(new string('c', 64), p.Reference, StringComparison.Ordinal);
        var info = ScanImportProvenance.TryParse(p)!;
        Assert.Equal(new string('c', 64), info.Sha256);
        Assert.Equal("zeiss-groove", info.ProfileId);
    }

    [Fact]
    public void A_Pipe_In_Entered_Text_Cannot_Forge_A_Field_And_Other_Provenance_Is_Not_Read_As_A_Scan()
    {
        var profile = SampleProfiles.Zeiss(E("x", "D"));
        var p = ScanImportProvenance.Create(new ScanProvenanceSource("ZEISS Inspect", "1|2", "a|b", "n=3", "c|d", new string('c', 64)), profile, "x|y", At);

        var info = ScanImportProvenance.TryParse(p)!;
        Assert.Equal("ZEISS Inspect 1/2", info.SoftwareAndVersion);
        Assert.Equal("a/b", info.Stage);
        Assert.Equal("x/y", info.Feature);

        Assert.Null(ScanImportProvenance.TryParse(null));
        Assert.Null(ScanImportProvenance.TryParse(CalcProvenance.Drawing("DRG-1", "A")));
        Assert.Null(ScanImportProvenance.TryParse(new CalcProvenance(CalcProvenanceKind.ScanImport, "scan:not|enough", "p@1")));
        Assert.Null(ScanImportProvenance.TryParse(new CalcProvenance(CalcProvenanceKind.ScanImport, p.Reference, "noversion")));
    }
}
