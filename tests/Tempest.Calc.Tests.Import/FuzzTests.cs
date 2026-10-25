using System.Text;
using CsCheck;
using Tempest.Calc.Import;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Import;

/// <summary>
/// Property and fuzz tests (CsCheck): whatever bytes or text arrive, an importer either returns a result or throws the
/// documented <see cref="ImportRefusedException"/> — never anything else, and never <see cref="ImportRefusalCode.Internal"/>,
/// which is how a defect of the importer itself is reported.
/// </summary>
public sealed class FuzzTests
{
    /// <summary>Iterations are scaled by the W1_FUZZ_SCALE environment variable (default 1) so a long soak can be run on demand.</summary>
    private static readonly int Scale = int.TryParse(Environment.GetEnvironmentVariable("W1_FUZZ_SCALE"), out var scale) && scale > 0 ? scale : 1;

    private static int N(int iterations) => iterations * Scale;

    private static readonly CadSourceFormat[] Formats = Enum.GetValues<CadSourceFormat>();

    private static readonly (string Sample, CadSourceFormat Format)[] SampleFiles =
    [
        ("nx/bracket-a.exp", CadSourceFormat.NxExpressions),
        ("nx/bracket-a.xlsx", CadSourceFormat.NxXlsx),
        ("creo/bracket-b.txt", CadSourceFormat.CreoRelations),
        ("solidworks/bracket-c-equations.txt", CadSourceFormat.SolidWorksEquations),
        ("solidworks/bracket-design-table.xlsx", CadSourceFormat.SolidWorksDesignTable),
        ("onshape/variables.csv", CadSourceFormat.OnshapeCsv),
        ("onshape/variables.json", CadSourceFormat.OnshapeJson),
        ("generic/parameters.csv", CadSourceFormat.GenericCsv),
        ("generic/parameters.json", CadSourceFormat.GenericJson),
        ("generic/decimal-comma.csv", CadSourceFormat.GenericCsv),
    ];

    private static readonly string[] Tokens =
    [
        "[", "]", "[mm]", "[Degrees]", "(", ")", "(String)", "(Number)", "=", "==", "<=", "//", "/*", "*/", "\"", "'", ",", ";", "\t", " ", "\n", "\r\n",
        "0", "1", "-1", "40.5", "40,5", "1e3", "1e999", "NaN", "Infinity", "-", "+", "*", "^", "#", "@", "$", "|",
        "mm", "N", "kN", "deg", "rad", "%", "m^99999999999999999999", "µm", "Ω", "furlongs", "IF", "ELSE", "ENDIF", "true", "false", "YES_NO", "REAL", "STRING",
        "width", "plate_w", "Länge", "\u0301", "\u202E", "\u200B", "\u0000", "é", "長さ", "😀", "\uD800", "name", "value", "unit", "expression", "parameters",
        "{", "}", ":", "\"name\":", "\"value\":", "null", "[]", "{}",
    ];

    private static readonly Gen<string> TokenSoup = Gen.Int[0, Tokens.Length - 1].List[0, 40].Select(indexes => string.Concat(indexes.Select(i => Tokens[i])));

    /// <summary>Runs <paramref name="parse"/>: a result is fine, a documented refusal is fine (and never Internal), anything else fails the property.</summary>
    private static void NeverThrowsButRefusal(Action parse)
    {
        try
        {
            parse();
        }
        catch (ImportRefusedException e)
        {
            Assert.NotEqual(ImportRefusalCode.Internal, e.Code);
            Assert.False(string.IsNullOrWhiteSpace(e.Message));
        }
    }

    private static void ParseBytes(byte[] bytes, CadSourceFormat format, ImportLimits? limits = null) =>
        NeverThrowsButRefusal(() => CadImporter.Parse(BoundedInputReader.FromBytes(bytes, "fuzz"), format, new CadParseSettings { Limits = limits ?? ImportLimits.Default }));

    [Fact]
    public void Random_Bytes_Are_Never_Anything_But_A_Documented_Refusal_In_Any_Format()
    {
        Gen.Byte.Array[0, 400].Sample(bytes =>
        {
            foreach (var format in Formats)
                ParseBytes(bytes, format);
        }, iter: N(400));
    }

    [Fact]
    public void Random_Token_Soup_Never_Escapes_As_Anything_But_A_Documented_Refusal()
    {
        TokenSoup.Sample(text =>
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);

            foreach (var format in Formats)
                ParseBytes(bytes, format);
        }, iter: N(1200));
    }

    [Fact]
    public void Line_Structured_Token_Soup_Reaches_Past_The_First_Line_Of_Every_Text_Parser()
    {
        Gen.Select(Gen.Int[0, 3], TokenSoup.List[1, 12]).Sample((which, lines) =>
        {
            var format = new[] { CadSourceFormat.NxExpressions, CadSourceFormat.CreoRelations, CadSourceFormat.SolidWorksEquations, CadSourceFormat.GenericCsv }[which];
            var text = string.Join('\n', lines);
            ParseBytes(new UTF8Encoding(false).GetBytes(text), format);
        }, iter: N(1500));
    }

    [Fact]
    public void Single_Byte_Edits_Of_The_Golden_Samples_Are_Never_Anything_But_A_Documented_Refusal()
    {
        foreach (var (sample, format) in SampleFiles)
        {
            var original = Samples.BytesOf(sample);

            Gen.Select(Gen.Int[0, original.Length - 1], Gen.Byte, Gen.Int[0, 2]).Sample((position, value, kind) =>
            {
                var edited = kind switch
                {
                    0 => original.Select((b, i) => i == position ? value : b).ToArray(),
                    1 => original.Take(position).Append(value).Concat(original.Skip(position)).ToArray(),
                    _ => original.Take(position).Concat(original.Skip(position + 1)).ToArray(),
                };

                ParseBytes(edited, format);
            }, iter: N(250));
        }
    }

    private static readonly string[] XmlFragments =
    [
        "<c r=\"A1\"><v>1</v></c>", "<c r=\"B2\" t=\"s\"><v>0</v></c>", "<c r=\"B2\" t=\"s\"><v>99999999999</v></c>", "<row r=\"1\">", "</row>", "<row r=\"1048577\"/>", "<c r=\"XFE1\"><v>1</v></c>",
        "<f>", "</f>", "<f>SUM(A1:A9)</f>", "<v>", "</v>", "<v>NaN</v>", "<v>1e999</v>", "<is><t>x</t></is>", "<si><t>x</t></si>", "<si>", "</si>", "<sheet name=\"S\" sheetId=\"1\" r:id=\"rId1\"/>",
        "<sheet name=\"Dup\" sheetId=\"2\" r:id=\"rId1\"/>", "<sheet name=\"Gone\" sheetId=\"3\" r:id=\"rId77\"/>", "<Relationship Id=\"rId1\" Target=\"../../x.xml\"/>", "<Relationship Id=\"rId1\" Target=\"/xl/worksheets/sheet1.xml\" TargetMode=\"External\"/>",
        "<a>", "</a>", "<!DOCTYPE x [<!ENTITY e \"v\">]>", "&e;", "&#0;", "&amp;", "<![CDATA[x]]>", "<?pi x?>", " xmlns=\"urn:x\"", " t=\"b\"", " t=\"e\"", " t=\"inlineStr\"", " t=\"str\"", " t=\"d\"",
    ];

    [Fact]
    public void Structure_Aware_Mutations_Of_The_Workbook_Samples_Are_Never_Anything_But_A_Documented_Refusal()
    {
        foreach (var (sample, format) in SampleFiles.Where(f => f.Sample.EndsWith(".xlsx", StringComparison.Ordinal)))
        {
            var parts = new List<(string Name, string Text)>();

            using (var zip = new System.IO.Compression.ZipArchive(new MemoryStream(Samples.BytesOf(sample))))
            {
                foreach (var entry in zip.Entries)
                {
                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                    parts.Add((entry.FullName, reader.ReadToEnd()));
                }
            }

            Gen.Select(Gen.Int[0, parts.Count - 1], Gen.Int[0, 100_000], Gen.Int[0, XmlFragments.Length - 1], Gen.Int[0, 3], Gen.Int[1, 4]).Sample((part, position, fragment, kind, repeat) =>
            {
                var mutated = parts.Select(p => p).ToList();
                var text = mutated[part].Text;
                var at = position % (text.Length + 1);

                text = kind switch
                {
                    0 => text.Insert(at, string.Concat(Enumerable.Repeat(XmlFragments[fragment], repeat))),
                    1 => text.Remove(at, Math.Min(text.Length - at, repeat * 7)),
                    2 => text.Remove(at, Math.Min(text.Length - at, 5)).Insert(at, XmlFragments[fragment]),
                    _ => text + string.Concat(Enumerable.Repeat(text[at..Math.Min(text.Length, at + 40)], repeat * 50)),
                };
                mutated[part] = (mutated[part].Name, text);
                ParseBytes(Samples.Zip(mutated.Select(p => (p.Name, Encoding.UTF8.GetBytes(p.Text)))), format);
            }, iter: N(400));
        }
    }

    [Fact]
    public void Truncations_Of_The_Golden_Samples_Are_Never_Anything_But_A_Documented_Refusal()
    {
        foreach (var (sample, format) in SampleFiles)
        {
            var original = Samples.BytesOf(sample);

            for (var cut = 0; cut < original.Length; cut += Math.Max(1, original.Length / 60))
                ParseBytes(original[..cut], format);
        }
    }

    [Fact]
    public void Every_Format_Fed_Every_Sample_Is_A_Result_Or_A_Documented_Refusal()
    {
        foreach (var (sample, _) in SampleFiles)
        {
            var bytes = Samples.BytesOf(sample);

            foreach (var format in Formats)
                ParseBytes(bytes, format);
        }
    }

    [Fact]
    public void Tight_Limits_Turn_Every_Sample_Into_A_Refusal_Or_A_Result_Never_A_Crash()
    {
        var tight = new ImportLimits { MaxRows = 3, MaxColumns = 3, MaxFieldChars = 8, MaxNameChars = 8, MaxParameters = 3, MaxCells = 10, MaxZipEntries = 3 };

        foreach (var (sample, format) in SampleFiles)
            ParseBytes(Samples.BytesOf(sample), format, tight);
    }

    [Theory]
    [InlineData("[mm]a=1\n", CadSourceFormat.NxExpressions)]
    [InlineData("// comment line that goes on and on and on and on\n", CadSourceFormat.NxExpressions)]
    [InlineData("a = 1\n", CadSourceFormat.CreoRelations)]
    [InlineData("\"a\"= 1\n", CadSourceFormat.SolidWorksEquations)]
    [InlineData("a,1\n", CadSourceFormat.GenericCsv)]
    [InlineData("\"\"\"\"", CadSourceFormat.GenericCsv)]
    [InlineData("[", CadSourceFormat.GenericJson)]
    [InlineData("{\"a\":", CadSourceFormat.OnshapeJson)]
    [InlineData("(((((((((((((((((((((((((((((((((", CadSourceFormat.NxExpressions)]
    public void Worst_Case_Five_Megabyte_Inputs_Are_Refused_Or_Read_Well_Inside_The_Time_Limit(string unit, CadSourceFormat format)
    {
        var bytes = new byte[ImportLimits.DefaultMaxBytes];
        var pattern = Encoding.UTF8.GetBytes(unit);

        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = pattern[i % pattern.Length];

        var watch = System.Diagnostics.Stopwatch.StartNew();
        ParseBytes(bytes, format);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(9), $"{format} took {watch.Elapsed}");
    }

    [Fact]
    public void Deeply_Nested_Json_Is_Refused_Not_Overflowed()
    {
        var deep = new string('[', 50_000) + new string(']', 50_000);
        ParseBytes(Encoding.UTF8.GetBytes(deep), CadSourceFormat.GenericJson);
        var objects = string.Concat(Enumerable.Repeat("{\"a\":", 20_000));
        ParseBytes(Encoding.UTF8.GetBytes(objects), CadSourceFormat.OnshapeJson);
    }

    [Fact]
    public void Random_Csv_Text_Is_Read_Or_Refused_With_The_Documented_Exception()
    {
        TokenSoup.Sample(text => NeverThrowsButRefusal(() =>
        {
            var table = CsvReader.Read(text, CsvReader.DetectDelimiter(text));
            Assert.All(table.Rows, r => Assert.InRange(r.Count, 1, ImportLimits.Default.MaxColumns));
        }), iter: N(1500));
    }

    [Fact]
    public void Random_Strings_Never_Make_The_Number_Reader_Throw_And_What_It_Accepts_Is_Finite()
    {
        TokenSoup.Sample(text =>
        {
            foreach (var profile in new[] { NumberProfile.Point, NumberProfile.Comma, new NumberProfile(DecimalMark.Point, ','), new NumberProfile(DecimalMark.Comma, '.') })
            {
                if (LocaleNumber.TryParse(text, profile, out var value, out var problem))
                {
                    Assert.True(double.IsFinite(value));
                    Assert.Equal(string.Empty, problem);
                }
                else
                {
                    Assert.NotEmpty(problem);
                }
            }
        }, iter: N(3000));
    }

    [Fact]
    public void Numbers_Written_By_The_Invariant_Writer_Read_Back_Exactly_Under_A_Decimal_Point_Profile()
    {
        Gen.Double[-1e12, 1e12].Sample(d =>
        {
            var text = CsvSafeWriter.Number(d);
            Assert.Equal(d, LocaleNumber.Parse(text, NumberProfile.Point));
        }, iter: N(2000));

        Gen.Double[-1e12, 1e12].Sample(d =>
        {
            var text = d.ToString("R", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
            Assert.Equal(d, LocaleNumber.Parse(text, NumberProfile.Comma));
        }, iter: N(2000));
    }

    [Fact]
    public void Unit_Text_From_A_File_Never_Throws_Whatever_It_Says()
    {
        Assert.False(CadUnits.TryResolve("m^99999999999999999999999999", out _, out _));
        Assert.False(CadUnits.TryResolve("kg^-99999999999", out _, out _));

        TokenSoup.Sample(text =>
        {
            CadUnits.TryResolve(text, out var symbol, out var unit);
            Assert.NotNull(symbol);
            Assert.NotNull(unit);
        }, iter: N(3000));
    }

    [Fact]
    public void Written_Csv_Always_Reads_Back_And_No_Cell_Of_It_Is_A_Formula()
    {
        Gen.String[Gen.Char[' ', '~'], 0, 30].List[1, 6].Sample(values =>
        {
            var csv = CsvSafeWriter.Row(values.Select(v => CsvSafeWriter.Text(v)));
            var back = CsvReader.Read(csv);

            if (back.Rows.Count == 0)
            {
                Assert.All(values, v => Assert.Equal(string.Empty, v));
                return;
            }

            var row = back.Rows[0];
            Assert.Equal(values.Count, row.Count);

            for (var i = 0; i < values.Count; i++)
            {
                Assert.Equal(CsvSafeWriter.Neutralise(values[i]), row[i]);
                Assert.False(CsvSafeWriter.IsFormulaLike(row[i]));
            }
        }, iter: N(2000));
    }

    [Fact]
    public void Any_Text_Defused_By_The_Writer_Is_No_Longer_Formula_Like()
    {
        Gen.String[Gen.Char[' ', '~'], 0, 20].Sample(s => Assert.False(CsvSafeWriter.IsFormulaLike(CsvSafeWriter.Neutralise(s))), iter: N(3000));
        Gen.String.Sample(s => Assert.False(CsvSafeWriter.IsFormulaLike(CsvSafeWriter.Neutralise(s))), iter: N(3000));
    }

    [Fact]
    public void Mapping_Whatever_Parses_Never_Throws_And_Never_Yields_A_Non_Finite_Number()
    {
        var template = new CalcTemplate(
            "fuzz.target", "1.0.0", CalcDiscipline.Mechanical, "T", "s", "m",
            [
                new CalcInputSpec("W", "Width", CalcInputKind.Quantity, "mm", Default: 1),
                new CalcInputSpec("alpha", "Angle", CalcInputKind.Quantity, "deg", Default: 0),
                new CalcInputSpec("n", "Count", CalcInputKind.Number, Default: 1),
                new CalcInputSpec("grade", "Grade", CalcInputKind.Choice, Choices: ["A", "B"], DefaultChoice: "A"),
            ],
            [], [], [], []);
        var profile = new CadMappingProfile("fuzz", 1, "Fuzz", [new("width", "W", "mm"), new("plate_w", "alpha"), new("name", "n"), new("value", "grade"), new("a", "W")], null, null, true);
        var current = new List<CalcInputValue> { CalcInputValue.Quantity("W", 1, "mm"), CalcInputValue.Quantity("alpha", 0, "deg"), CalcInputValue.Of("n", 1), CalcInputValue.Choice("grade", "A") };

        TokenSoup.List[1, 5].Sample(rows =>
        {
            var text = "name,value,unit,expression\n" + string.Join('\n', rows.Select(r => r.Replace('\n', ' ').Replace('\r', ' ')));

            NeverThrowsButRefusal(() =>
            {
                var parse = CadImporter.Parse(Samples.Text(text, "f.csv"), CadSourceFormat.GenericCsv);
                var result = CadMapper.Map(parse, profile, template, current, DateTimeOffset.UnixEpoch);
                Assert.All(result.Inputs.Where(i => i.Number is not null), i => Assert.True(double.IsFinite(i.Number!.Value)));
                Assert.All(result.Inputs.Where(i => i.Provenance?.Kind == CalcProvenanceKind.CadImport), i => Assert.True(i.Provenance!.IsWithinLimits));
                Assert.NotNull(CadImportReport.RowsCsv(result.Rows));
            });
        }, iter: N(1500));
    }

    [Fact]
    public void Random_Paths_Are_Accepted_Or_Refused_With_The_Documented_Exception()
    {
        TokenSoup.Sample(path => NeverThrowsButRefusal(() => ImportPath.Canonicalise(path)), iter: N(3000));
        TokenSoup.Sample(path => Assert.NotEmpty(ImportPath.SafeLeafName(path)), iter: N(1000));
    }

    [Fact]
    public void Random_Profile_Json_Is_Read_Or_Refused_With_The_Documented_Exception()
    {
        TokenSoup.Sample(text => NeverThrowsButRefusal(() => CadMappingProfileJson.Read(text)), iter: N(1500));
    }

    [Fact]
    public void Provenance_Written_For_Any_Names_Fits_The_Caps_And_Reads_Back()
    {
        var profile = new CadMappingProfile("p", 7, "P", [new("a", "W")]);

        Gen.Select(Gen.String[0, 200], Gen.String[0, 200], Gen.String[0, 200], Gen.String[0, 200]).Sample((part, name, version, revision) =>
        {
            var source = new CadSourceInfo(CadSystem.Creo, CadSourceFormat.CreoRelations, "f", new string('f', 64), version, part, revision);
            var provenance = CadImportProvenance.Create(source, profile, name, DateTimeOffset.UnixEpoch);
            Assert.True(provenance.IsWithinLimits);
            Assert.Equal(new string('f', 64), CadImportProvenance.TryParse(provenance)?.FileSha256);
        }, iter: N(1500));
    }

    [Fact]
    public void Diffing_A_Snapshot_Against_Itself_Reports_No_Change()
    {
        var parameter = Gen.Select(Gen.Int[0, 5].Select(i => "n" + i), Gen.Double[-100, 100], Gen.Bool).Select((name, n, flag) =>
            new CadImportedParameter(name, flag ? CadParameterKind.Number : CadParameterKind.Text, null, flag ? n : null, null, flag ? n : null, flag ? null : name));

        parameter.List[0, 8].Sample(list =>
        {
            var record = new CadImportRecord("k", "f", new string('0', 64), CadSystem.Nx, CadSourceFormat.NxExpressions, null, null, null, null, "p", 1, DateTimeOffset.UnixEpoch, "me", list);
            Assert.False(CadImportDiff.Compute(record, list).HasChanges);
        }, iter: N(500));
    }
}
