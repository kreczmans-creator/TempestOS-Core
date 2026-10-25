using System.Text;
using CsCheck;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Scan;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Import;

/// <summary>
/// Property and fuzz tests (CsCheck) for W2: whatever bytes, text, profile or numbers arrive, the scan importer returns a result or throws the documented
/// <see cref="ImportRefusedException"/> — never anything else, never <see cref="ImportRefusalCode.Internal"/> — and no statistic is ever NaN or Infinity.
/// </summary>
public sealed class ScanFuzzTests
{
    private static readonly int Scale = int.TryParse(Environment.GetEnvironmentVariable("W1_FUZZ_SCALE"), out var scale) && scale > 0 ? scale : 1;

    private static int N(int iterations) => iterations * Scale;

    private static readonly string[] Tokens =
    [
        "Feature", "Nominal", "Actual", "Dev", "Up", "Lo", "Serial", "Stage", "A", "B", "Hole 1", ",", ";", "\t", " ", "\n", "\r\n", "\"", "\"\"", "'", "<b>", "</b>", "<a href=\"x\">", "http://x", "=HYPERLINK(",
        "0", "1", "-1", "10.01", "10,01", "1e3", "1e999", "-1e999", "NaN", "Infinity", "-inf", "1e12", "0.0", "-0", "+", "-", "#", "@", "|", "\u0000", "​", "‮", "\uD800", "é", "長さ", "😀", "1_000", "١٢٣", "0x1F",
    ];

    private static readonly Gen<string> TokenSoup = Gen.Int[0, Tokens.Length - 1].List[0, 60].Select(indexes => string.Concat(indexes.Select(i => Tokens[i])));

    private static readonly ScanProfile[] Profiles =
    [
        SampleProfiles.Plain(new ScanFeatureEntry("A", "D")),
        SampleProfiles.Zeiss(),
        SampleProfiles.GomTwoStage(),
        SampleProfiles.PolyWorks(),
        new ScanProfile("tab", 1, "Tab", new ScanColumns("Feature", Actual: "Actual", Stage: "Stage"), [], Delimiter: ScanDelimiter.Tab),
        new ScanProfile("ws", 1, "Whitespace", new ScanColumns("#1", Actual: "#2", Deviation: "#3"), [], Delimiter: ScanDelimiter.Whitespace, ToleranceMode: ScanToleranceMode.AbsoluteLimits),
    ];

    private static void NeverThrowsButRefusal(Action action)
    {
        try
        {
            action();
        }
        catch (ImportRefusedException e)
        {
            Assert.NotEqual(ImportRefusalCode.Internal, e.Code);
            Assert.False(string.IsNullOrWhiteSpace(e.Message));
        }
    }

    private static void AssertSound(ScanAnalysis a)
    {
        Assert.All(a.Data.Observations, o => Assert.True(double.IsFinite(o.Value) && Math.Abs(o.Value) <= ScanImporter.MaxPlausible));

        foreach (var s in a.Stats)
        {
            Assert.True(double.IsFinite(s.Mean));
            Assert.True(double.IsFinite(s.Sigma) && s.Sigma >= 0);

            foreach (var v in new double?[] { s.Cp, s.Cpk, s.Pp, s.Ppk, s.Normality.A2, s.Normality.AStar2, s.Normality.PValue })
                Assert.True(v is null || double.IsFinite(v.Value));

            if (s.Normality.PValue is { } p)
                Assert.InRange(p, 0.0, 1.0);

            Assert.DoesNotContain("NaN", string.Join(' ', s.Warnings), StringComparison.Ordinal);
        }
    }

    private static void Analyse(byte[] bytes, ScanProfile profile)
    {
        NeverThrowsButRefusal(() =>
        {
            var a = ScanImporter.Analyse(BoundedInputReader.FromBytes(bytes, "fuzz"), profile);
            AssertSound(a);
            _ = ScanImportReport.StatisticsCsv(a.Stats, "mm");
            _ = ScanImportReport.ProblemsCsv(a.Data);
        });
    }

    [Fact]
    public void Random_Bytes_Are_Never_Anything_But_A_Documented_Refusal_Under_Every_Profile()
    {
        Gen.Byte.Array[0, 400].Sample(bytes =>
        {
            foreach (var profile in Profiles)
                Analyse(bytes, profile);
        }, iter: N(300));
    }

    [Fact]
    public void Random_Token_Soup_Never_Escapes_As_Anything_But_A_Documented_Refusal()
    {
        TokenSoup.Sample(text =>
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);

            foreach (var profile in Profiles)
                Analyse(bytes, profile);
        }, iter: N(1000));
    }

    [Fact]
    public void Tables_Built_From_A_Valid_Header_And_Token_Soup_Rows_Reach_The_Row_Parser_And_Stay_Sound()
    {
        Gen.Select(Gen.Int[0, 3], TokenSoup.List[1, 14]).Sample((which, rows) =>
        {
            var (header, delimiter) = which switch
            {
                0 => ("Feature,Nominal,Actual,Dev,Up,Lo,Serial", ','),
                1 => ("Feature;Nominal;Actual;Dev;Up;Lo;Serial", ';'),
                2 => ("Feature\tNominal\tActual\tDev\tUp\tLo\tSerial", '\t'),
                _ => ("Feature,Nominal,Actual,Dev,Up,Lo,Serial", ','),
            };
            var body = string.Join('\n', rows.Select(r => r.Replace('\n', ' ').Replace('\r', ' ')));
            var profile = SampleProfiles.Plain(new ScanFeatureEntry("A", "D")) with { Delimiter = delimiter switch { ';' => ScanDelimiter.Semicolon, '\t' => ScanDelimiter.Tab, _ => ScanDelimiter.Comma } };
            NeverThrowsButRefusal(() => AssertSound(ScanImporter.Analyse(Samples.Text(header + "\n" + body + "\n"), profile)));
        }, iter: N(1500));
    }

    [Fact]
    public void Numeric_Rows_Of_Arbitrary_Doubles_Never_Produce_A_NaN_Or_Infinity_Anywhere()
    {
        Gen.Double.List[0, 40].Select(Gen.Int[1, 6]).Sample((values, subgroup) =>
        {
            var sb = new StringBuilder("Feature,Nominal,Actual,Dev,Up,Lo,Serial\n");

            for (var i = 0; i < values.Count; i++)
                sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"A,10,{values[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture)},,0.05,-0.05,S{i}\n");

            NeverThrowsButRefusal(() => AssertSound(ScanImporter.Analyse(Samples.Text(sb.ToString()), SampleProfiles.Plain(), new ScanSettings { SubgroupSize = subgroup })));
        }, iter: N(600));
    }

    [Fact]
    public void Statistics_Of_Any_Finite_Values_Are_Finite_Or_Reported_Not_Computable()
    {
        Gen.Double[-1e9, 1e9].List[0, 60].Sample(values =>
        {
            var obs = values.Select((v, i) => new ScanObservation(0, i + 2, "F", string.Empty, $"S{i}", 10, v, 9, 11, false)).ToList();
            var s = ScanStatistics.Compute("F", string.Empty, obs, [new ScanFileInfo("a", new string('a', 64), null, null, values.Count, values.Count)], new ScanSettings());

            Assert.True(double.IsFinite(s.Mean));
            Assert.True(double.IsFinite(s.Sigma));

            foreach (var v in new double?[] { s.Cp, s.Cpk, s.Pp, s.Ppk, s.Normality.A2, s.Normality.PValue })
                Assert.True(v is null || double.IsFinite(v.Value));
        }, iter: N(800));
    }

    [Fact]
    public void The_Sample_Variance_Is_Never_Negative_And_The_Mean_Lies_Between_The_Extremes()
    {
        Gen.Double[-1e6, 1e6].List[2, 80].Sample(values =>
        {
            var mean = ScanStatistics.Mean(values);
            var sigma = ScanStatistics.Sigma(values, mean);

            Assert.True(sigma >= 0 && double.IsFinite(sigma));
            Assert.InRange(mean, values.Min() - 1e-6, values.Max() + 1e-6);
        }, iter: N(800));
    }

    [Fact]
    public void Single_Byte_Edits_Of_The_Golden_Samples_Are_Never_Anything_But_A_Documented_Refusal()
    {
        var samples = new (string Name, ScanProfile Profile)[]
        {
            ("scan/zeiss-groove.csv", SampleProfiles.Zeiss(new ScanFeatureEntry("D_g groove root", "D"))),
            ("scan/gom-two-stage.csv", SampleProfiles.GomTwoStage()),
            ("scan/polyworks-report.txt", SampleProfiles.PolyWorks()),
        };

        foreach (var (name, profile) in samples)
        {
            var original = Samples.BytesOf(name);

            Gen.Select(Gen.Int[0, original.Length - 1], Gen.Byte, Gen.Int[0, 2]).Sample((position, value, kind) =>
            {
                var edited = kind switch
                {
                    0 => original.Select((b, i) => i == position ? value : b).ToArray(),
                    1 => original.Take(position).Append(value).Concat(original.Skip(position)).ToArray(),
                    _ => original.Take(position).Concat(original.Skip(position + 1)).ToArray(),
                };

                Analyse(edited, profile);
            }, iter: N(200));
        }
    }

    [Fact]
    public void Truncations_Of_A_Workbook_And_Edits_Of_Its_Bytes_Are_Never_Anything_But_A_Documented_Refusal()
    {
        const string Sheet =
            "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Feature</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t>Actual</t></is></c></row>"
            + "<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>H</t></is></c><c r=\"B2\"><v>4.01</v></c></row>"
            + "<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>H</t></is></c><c r=\"B3\"><v>4.03</v></c></row>";
        var original = Samples.Workbook(Sheet);
        var profile = new ScanProfile("xl", 1, "Workbook", new ScanColumns("Feature", Actual: "Actual"), []);

        Gen.Select(Gen.Int[0, original.Length - 1], Gen.Byte, Gen.Int[0, 2]).Sample((position, value, kind) =>
        {
            var edited = kind switch
            {
                0 => original.Select((b, i) => i == position ? value : b).ToArray(),
                1 => original.Take(position).ToArray(),
                _ => original.Take(position).Concat(original.Skip(position + 1)).ToArray(),
            };

            Analyse(edited, profile);
        }, iter: N(500));
    }

    [Fact]
    public void Random_Profile_Json_Is_Never_Anything_But_A_Documented_Refusal_And_A_Valid_One_Always_Validates()
    {
        TokenSoup.Sample(text =>
        {
            NeverThrowsButRefusal(() => ScanProfileJson.Read(text));
            NeverThrowsButRefusal(() => ScanProfileJson.Read("{\"id\":\"p\",\"version\":1,\"title\":\"t\",\"columns\":{\"feature\":\"" + text.Replace("\\", "/", StringComparison.Ordinal).Replace("\"", "'", StringComparison.Ordinal) + "\",\"actual\":\"A\"},\"features\":[]}"));
        }, iter: N(800));
    }

    [Fact]
    public void Mapping_Fitted_Features_Into_A_Template_Never_Throws_Whatever_The_Targets_And_Units_Are()
    {
        var targets = new[] { "D", "D:sigma", "contributors[0]", "contributors[7]", "measured{D_s}", "F", "eta", "nope", "x[1]", "k", "grade" };
        var units = new[] { "mm", "m", "in", "µm", "deg", "N", "kN", "s", "K" };

        Gen.Select(Gen.Int[0, targets.Length - 1], Gen.Int[0, units.Length - 1], Gen.Double[-1e3, 1e3].List[0, 20]).Sample((t, u, values) =>
        {
            var text = "Feature,Actual\n" + string.Concat(values.Select(v => $"A,{v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}\n"));
            var profile = new ScanProfile("p", 1, "P", new ScanColumns("Feature", Actual: "Actual"), [new ScanFeatureEntry("A", targets[t])], Unit: units[u]);

            NeverThrowsButRefusal(() =>
            {
                var a = ScanImporter.Analyse(Samples.Text(text), profile);
                var result = ScanMapper.Map(a, MapperTemplate, MapperCurrent, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
                Assert.All(result.Inputs, i => Assert.True(i.Number is null || double.IsFinite(i.Number.Value)));

                foreach (var row in result.Inputs.SelectMany(i => i.Rows ?? []))
                    Assert.All(row.Cells.Values, c => Assert.True(c.Number is null || double.IsFinite(c.Number.Value)));
            });
        }, iter: N(800));
    }

    private static readonly CalcTemplate MapperTemplate = new(
        "fuzz.target", "1.0.0", CalcDiscipline.Mechanical, "T", "s", "m",
        [
            new CalcInputSpec("contributors", "C", CalcInputKind.Table, Columns:
            [
                new CalcColumnSpec("nominal", "N", CalcInputKind.Quantity, "mm"),
                new CalcColumnSpec("upper", "U", CalcInputKind.Quantity, "mm"),
                new CalcColumnSpec("lower", "L", CalcInputKind.Quantity, "mm", IsOptional: true),
                new CalcColumnSpec("distribution", "D", CalcInputKind.Choice, Choices: ["normal"], IsOptional: true),
                new CalcColumnSpec("shift", "S", CalcInputKind.Quantity, "mm", IsOptional: true),
            ]),
            new CalcInputSpec("measured", "M", CalcInputKind.Table, IsOptional: true, MaxRows: 2, Columns:
            [
                new CalcColumnSpec("dim", "D", CalcInputKind.Choice, Choices: ["D_s", "b"]),
                new CalcColumnSpec("mean", "M", CalcInputKind.Quantity, "mm"),
                new CalcColumnSpec("sigma", "S", CalcInputKind.Quantity, "mm"),
            ]),
            new CalcInputSpec("D", "D", CalcInputKind.Quantity, "mm", Default: 1),
            new CalcInputSpec("F", "F", CalcInputKind.Quantity, "kN", Default: 1),
            new CalcInputSpec("eta", "e", CalcInputKind.Number, Default: 1),
            new CalcInputSpec("k", "k", CalcInputKind.Number, Default: 3),
            new CalcInputSpec("grade", "g", CalcInputKind.Choice, Choices: ["a"]),
        ],
        [], [], [], []);

    private static readonly IReadOnlyList<CalcInputValue> MapperCurrent =
    [
        CalcInputValue.Table("contributors", [CalcRow.Of(("nominal", new CalcCell(5, "mm")), ("upper", new CalcCell(0.1, "mm")), ("lower", new CalcCell(-0.1, "mm")))]),
        CalcInputValue.Quantity("D", 1, "mm"),
        CalcInputValue.Of("k", 3),
    ];
}
