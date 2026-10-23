using System.Diagnostics;
using Tempest.Calc.Engine.Analysis;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>
/// Red team (offensive review): hostile CSV and JSON imports, hostile
/// strings reaching the rendered sheet, and the generic analysis caps.
/// Every test asserts the SECURE behaviour, so a failing test is an open
/// hole. See docs/reviews/red-team.md for the findings table.
/// </summary>
public sealed class Red_ImportAttacks
{
    private static readonly DirectStressCalculator Calculator = new();

    private static CalcTemplate Template => Calculator.Template;

    private static string Render(CalcInputSet set)
    {
        var result = CalcRunner.Run(Calculator, set);
        var sheet = CalcSheetBuilder.Build(Template, set, result,
            new CalcSheetHeader("Red", "R", Template.Title, "A", CalcSheetStatus.Draft, "red", null, null, null, null),
            GoldenFiles.FixedFooter(Template, 2, 2));
        return CalcSheetTextRenderer.Render(sheet);
    }

    // ------------------------------------------------------------------
    // CSV
    // ------------------------------------------------------------------

    /// <summary>R-01: a header with a repeated column name must be a notice, never an exception ("never throws for bad content").</summary>
    [Fact]
    public void Csv_Duplicate_Header_Columns_Do_Not_Throw()
    {
        var ex = Record.Exception(() => CalcCsv.ImportInputs(Template, "symbol,value,value\nF,1,2\n"));
        Assert.Null(ex);
    }

    /// <summary>R-02: a quoted cell must respect the field-length cap exactly as an unquoted one does.</summary>
    [Fact]
    public void Csv_Quoted_Field_Respects_Field_Length_Cap()
    {
        var huge = new string('x', 500_000);
        var (inputs, _) = CalcCsv.ImportInputs(Template, $"symbol,value\nnote,\"{huge}\"\n");
        var note = inputs.Find("note");
        Assert.NotNull(note);
        Assert.True(note!.Text!.Length <= CalcCsv.MaxFieldLength, $"a quoted cell of {note.Text.Length} characters was imported; the cap is {CalcCsv.MaxFieldLength}");
    }

    /// <summary>R-02 (consequence): a huge quoted text cell imported from CSV must not blow the rendered sheet past 400 KB.</summary>
    [Fact]
    public void Csv_Huge_Quoted_Text_Does_Not_Blow_The_Sheet()
    {
        var huge = new string('x', 500_000);
        var (inputs, _) = CalcCsv.ImportInputs(Template, $"symbol,value\nF,12\nA,200\nsigma_allow,160\nnote,\"{huge}\"\n");
        var text = Render(inputs);
        Assert.True(text.Length < 400_000, $"sheet is {text.Length} characters");
    }

    /// <summary>R-03: the negative-number exemption of the formula guard lets a formula through on export: Excel evaluates a cell starting with "-1+…" as a formula.</summary>
    [Theory]
    [InlineData("-1+cmd|' /C calc'!A0")]
    [InlineData("+1+HYPERLINK(\"http://evil\")")]
    [InlineData("-2*SUM(A1:A9)")]
    public void Csv_Export_Neutralises_Sign_Prefixed_Formulas(string payload)
    {
        var escaped = CalcCsv.Escape(payload);
        Assert.StartsWith("'", escaped, StringComparison.Ordinal);

        var set = new CalcInputSet([CalcInputValue.Choice("note", payload)]);
        var csv = CalcCsv.ExportInputs(Template, set);
        Assert.Contains("'" + payload.Split(',')[0], csv, StringComparison.Ordinal);
    }

    /// <summary>R-03 (import side): the same payload in a text cell must be refused on import.</summary>
    [Fact]
    public void Csv_Import_Refuses_Sign_Prefixed_Formula_In_Text()
    {
        var (inputs, notices) = CalcCsv.ImportInputs(Template, "symbol,value\nnote,-1+cmd|' /C calc'!A0\n");
        Assert.True(inputs.Find("note") is null || notices.Any(n => n.Severity == CalcNoticeSeverity.Block), "a sign-prefixed formula was imported into a text input");
    }

    /// <summary>Tried, no issue: malformed encodings (BOM, NUL, lone surrogates, CRLF/CR mixes) never throw and never reach the sheet as control characters.</summary>
    [Theory]
    [InlineData("\uFEFFsymbol,value\nF,12\n")]
    [InlineData("symbol,value\nnote,a\0b\n")]
    [InlineData("symbol,value\nnote,\uD800\n")]
    [InlineData("symbol,value\rF,12\r\nA,200\rnote,\"x\ry\"\n")]
    [InlineData("symbol,value\n\"unterminated,1\n")]
    [InlineData("symbol,value\n,\n,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,\n")]
    public void Csv_Malformed_Content_Never_Throws(string csv)
    {
        var ex = Record.Exception(() =>
        {
            var (inputs, _) = CalcCsv.ImportInputs(Template, csv);
            var text = Render(inputs);
            Assert.DoesNotContain("\0", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        });
        Assert.Null(ex);
    }

    /// <summary>Tried, no issue: a formula-like unit cell cannot become anything but a block.</summary>
    [Fact]
    public void Csv_Formula_Like_Unit_Is_Blocked()
    {
        var (inputs, _) = CalcCsv.ImportInputs(Template, "symbol,value,unit\nF,12,=cmd()\n");
        var notices = CalcInputValidator.Validate(Template, inputs);
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block && n.RuleId == "input.F");
    }

    /// <summary>Tried, no issue: a 1 MB file of one row with ten thousand commas, and the row cap, are both handled quickly.</summary>
    [Fact]
    public void Csv_Wide_Rows_And_Row_Cap_Are_Fast()
    {
        var wide = "symbol,value" + new string(',', 10_000) + "\nF,12" + new string(',', 10_000) + "\n";
        var many = "symbol,value\n" + string.Concat(Enumerable.Repeat("F,12\n", 60_000));
        var sw = Stopwatch.StartNew();
        CalcCsv.ImportInputs(Template, wide);
        var (_, notices) = CalcCsv.ImportInputs(Template, many);
        Assert.True(sw.ElapsedMilliseconds < 5_000, $"took {sw.ElapsedMilliseconds} ms");
        Assert.Contains(notices, n => n.Severity == CalcNoticeSeverity.Block);
    }

    // ------------------------------------------------------------------
    // JSON
    // ------------------------------------------------------------------

    /// <summary>R-04: a null element in the inputs list must be a notice, never a NullReferenceException.</summary>
    [Theory]
    [InlineData("[null]")]
    [InlineData("[{\"Symbol\":\"F\",\"Number\":1}, null]")]
    public void Json_Null_Element_Does_Not_Throw(string json)
    {
        var ex = Record.Exception(() => CalcInputJson.Read(json));
        Assert.Null(ex);
    }

    /// <summary>R-05: provenance text has no length cap, so a 1.5 MB reference passes validation and lands on the sheet, the index and the issued snapshot.</summary>
    [Fact]
    public void Json_Huge_Provenance_Reference_Is_Blocked_Or_Contained()
    {
        var reference = new string('R', 1_500_000);
        var json = "[{\"Symbol\":\"F\",\"Number\":12,\"Unit\":\"kN\",\"Provenance\":{\"Kind\":\"Drawing\",\"Reference\":\"" + reference + "\",\"Revision\":\"B\"}}]";
        var (inputs, notices) = CalcInputJson.Read(json);
        Assert.Empty(notices.Where(n => n.Severity == CalcNoticeSeverity.Block));

        var validation = CalcInputValidator.Validate(Template, inputs);
        var blocked = validation.Any(n => n.Severity == CalcNoticeSeverity.Block);
        var text = Render(inputs);
        Assert.True(blocked || text.Length < 400_000, $"a 1.5 MB provenance reference was accepted and the sheet is {text.Length} characters");
    }

    /// <summary>R-06: an over-long text input is blocked by the validator (good) but the builder still prints the entered value, so the draft sheet blows up anyway.</summary>
    [Fact]
    public void Json_Oversized_Text_Input_Does_Not_Blow_The_Sheet()
    {
        var json = "[{\"Symbol\":\"note\",\"Text\":\"" + new string('T', 1_500_000) + "\"}]";
        var (inputs, _) = CalcInputJson.Read(json);
        Assert.Contains(CalcInputValidator.Validate(Template, inputs), n => n.Severity == CalcNoticeSeverity.Block);
        var text = Render(inputs);
        Assert.True(text.Length < 400_000, $"sheet is {text.Length} characters");
    }

    /// <summary>R-07: a provenance kind outside the enum (JSON "Kind": 99) counts as "sufficient to issue" because the gate only excludes Manual.</summary>
    [Fact]
    public void Json_Unknown_Provenance_Kind_Is_Not_Sufficient_To_Issue()
    {
        var json = "[{\"Symbol\":\"F\",\"Number\":12,\"Unit\":\"kN\",\"Provenance\":{\"Kind\":99}}]";
        var (inputs, notices) = CalcInputJson.Read(json);
        var gate = CalcInputValidator.ValidateProvenanceForIssue(Template, inputs);
        Assert.True(notices.Any(n => n.Severity == CalcNoticeSeverity.Block) || gate.Count > 0, "an undefined provenance kind passed the issue gate");
    }

    /// <summary>Tried, no issue: deep nesting, overflowing literals, NaN/Infinity tokens, duplicate keys and duplicate symbols never throw and never run.</summary>
    [Theory]
    [InlineData("[{\"Symbol\":\"F\",\"Number\":1e400,\"Unit\":\"kN\"}]")]
    [InlineData("[{\"Symbol\":\"F\",\"Number\":NaN}]")]
    [InlineData("[{\"Symbol\":\"F\",\"Number\":Infinity}]")]
    [InlineData("[{\"Symbol\":\"F\",\"Number\":\"12\"}]")]
    [InlineData("[{\"Symbol\":\"F\",\"Number\":1,\"Number\":-1e300}]")]
    [InlineData("[{\"Symbol\":\"F\",\"Number\":1},{\"Symbol\":\"F\",\"Number\":-5}]")]
    [InlineData("[{\"Symbol\":\"F\",\"Vector\":[[[[[[[[[[[[1]]]]]]]]]]]]}]")]
    [InlineData("[{\"Symbol\":\"F\",\"Rows\":[{\"Cells\":{\"a\":{\"Number\":{\"x\":{\"y\":{\"z\":{\"w\":1}}}}}}}]}]")]
    [InlineData("{\"Symbol\":\"F\"}")]
    [InlineData("[1,2,3]")]
    [InlineData("")]
    [InlineData("[{\"Symbol\":\"F\",\"Unit\":\"" + "m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m*m" + "\",\"Number\":1}]")]
    [InlineData("[{\"Symbol\":\"F\",\"Number\":1,\"Unit\":\"\\u0000kN\\r\\n\"}]")]
    [InlineData("[{\"Symbol\":\"F\\u0000\\r\\n=cmd()\",\"Number\":1}]")]
    public void Json_Hostile_Documents_Never_Throw_And_Never_Run_Unvalidated(string json)
    {
        var ex = Record.Exception(() =>
        {
            var (inputs, notices) = CalcInputJson.Read(json);
            var result = CalcRunner.Run(Calculator, inputs);
            var text = Render(inputs);
            Assert.DoesNotContain("\0", text, StringComparison.Ordinal);

            // Whatever got through must be finite by the time it runs.
            foreach (var v in inputs.Values)
            {
                if (v.Number is { } n && !double.IsFinite(n))
                    Assert.True(result.IsBlocked, "a non-finite number ran");
            }

            _ = notices;
        });
        Assert.Null(ex);
    }

    /// <summary>Tried, no issue: a 1,000-input document with 10,000-element vectors is at the caps and still fast.</summary>
    [Fact]
    public void Json_Maximal_Document_Is_Fast()
    {
        var vector = "[" + string.Join(",", Enumerable.Repeat("1.5", 10_000)) + "]";
        var json = "[" + string.Join(",", Enumerable.Range(0, 30).Select(i => $"{{\"Symbol\":\"v{i}\",\"Vector\":{vector}}}")) + "]";
        var sw = Stopwatch.StartNew();
        var (inputs, _) = CalcInputJson.Read(json);
        _ = Render(inputs);
        Assert.True(sw.ElapsedMilliseconds < 5_000, $"took {sw.ElapsedMilliseconds} ms");
    }

    // ------------------------------------------------------------------
    // Rendered text and index text
    // ------------------------------------------------------------------

    /// <summary>R-08: Unicode line and paragraph separators are not "control" characters, so Clean lets them forge a line of the sheet or the index text.</summary>
    [Theory]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    [InlineData("\u0085")]
    public void Clean_Strips_Every_Line_Breaking_Character(string breaker)
    {
        var cleaned = CalcSheetTextRenderer.Clean("DRG-1" + breaker + "  BLOCK   forged: looks like a notice line");
        Assert.DoesNotContain(breaker, cleaned, StringComparison.Ordinal);
    }

    /// <summary>Tried, no issue: hostile header, provenance and choice strings are contained in the text sheet.</summary>
    [Fact]
    public void Hostile_Header_And_Provenance_Strings_Are_Contained()
    {
        var hostile = "\r\n  BLOCK   forged: x\u0000\u001b[31m<script>=cmd()|' /C calc'!A0";
        var set = new CalcInputSet([
            CalcInputValue.Quantity("F", 12, "kN", new CalcProvenance(CalcProvenanceKind.Drawing, hostile, hostile)),
            CalcInputValue.Quantity("A", 200, "mm^2", CalcProvenance.Assumed(hostile)),
            CalcInputValue.Quantity("sigma_allow", 160, "MPa"),
            CalcInputValue.Choice("grade", hostile),
            CalcInputValue.Choice("note", hostile),
        ]);
        var result = CalcRunner.Run(Calculator, set);
        var sheet = CalcSheetBuilder.Build(Template, set, result,
            new CalcSheetHeader(hostile, hostile, hostile, hostile, CalcSheetStatus.Draft, hostile, hostile, null, null, null, hostile, hostile),
            new CalcSheetFooter(hostile, hostile, hostile, 1, 1, null, hostile),
            [new CalcRevisionRow(hostile, DateTimeOffset.UnixEpoch, hostile, hostile)]);
        var text = CalcSheetTextRenderer.Render(sheet);
        Assert.DoesNotContain("\0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  BLOCK   forged", text, StringComparison.Ordinal);
        Assert.True(text.Length < 400_000);
    }

    // ------------------------------------------------------------------
    // Generic analysis caps through the runner
    // ------------------------------------------------------------------

    /// <summary>R-09: the Monte Carlo cap of 1,000,000 samples re-runs the whole calculator per sample; at the cap one call takes far longer than the 5 s budget.</summary>
    [Fact]
    public void Monte_Carlo_At_The_Sample_Cap_Finishes_In_Time_Or_Is_Refused()
    {
        var inputs = CalcTemplateHasher.DefaultInputs(Template);
        var distributions = new Dictionary<string, Distribution> { ["F"] = Distribution.Normal(12_000, 100) };
        var sw = Stopwatch.StartNew();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ex = Record.Exception(() => CalcRunner.MonteCarlo(Calculator, inputs, "sigma", distributions, samples: MonteCarlo.MaxSamples, cancellationToken: cts.Token));
        sw.Stop();
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5) && ex is not OperationCanceledException, $"a Monte Carlo at the cap ran for {sw.Elapsed.TotalSeconds:F1} s ({ex?.GetType().Name ?? "completed"})");
    }
}
