using Tempest.Calc.Engine.Values;
using Tempest.Calc.Import;
using Tempest.Calc.Library;
using Tempest.Calc.Library.ManualTesting;
using Tempest.Calc.Sheets;
using static Tempest.Calc.Tests.Library.RecordImport.RecordImportTestSupport;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// The Addendum C security and red-team review (<c>docs/reviews/security-review.md</c> and <c>red-team.md</c>, "Addendum C"): each finding's
/// proof of concept, kept as the regression test of its fix. Every number is invented for the test.
/// </summary>
public sealed class AddendumCSecurityRegressionTests
{
    // ------------------------------------------------------------------ SEC-C-01 / R-C-01: a decimal comma split by a comma delimiter

    /// <summary>
    /// Under a declared decimal comma, a comma-separated file's unquoted '14,8' is the two fields 14 and 8. A bearing row that also leaves its
    /// last value off keeps the header's width, so it was read silently as C = 14 kN and C_0 = 8 kN.
    /// </summary>
    [Theory]
    [InlineData("designation[text],C[kN],C_0[kN]\r\n6205,14,8\r\n", "24")]
    [InlineData("CSA[mm2],AWG[-],I_t[A]\r\n4,,27\r\n6,,36\r\n", "04")]
    [InlineData("medium[text],insulation[text],theta_a[degC],C_a[-]\r\nair,PVC,30,\"0,94\"\r\n", "06")]
    public async Task SEC_C_01_Under_A_Decimal_Comma_An_Unquoted_Number_In_A_Comma_Separated_File_Is_Refused(string csv, string template)
    {
        var host = Host();
        var preview = await host.Service.RecordImport.PreviewAsync(Request(template, Utf8(csv)) with { Number = NumberProfile.Comma });

        Assert.False(preview.CanCommit);
        Assert.Contains(preview.Errors, e => e.Line == 2 && e.Message.Contains("without quotes", StringComparison.Ordinal));
        Assert.Empty(preview.Planned);

        var commit = await host.Service.RecordImport.CommitAsync(Request(template, Utf8(csv)) with { Number = NumberProfile.Comma });
        Assert.False(commit.Committed);
        Assert.Empty(await host.Store.ListRecordsAsync(RecordTemplates.Find(template)!.Kind));
    }

    [Fact]
    public async Task SEC_C_01_Quoted_Numbers_Semicolon_Files_A_Decimal_Point_And_A_Typed_Grid_Are_Still_Read()
    {
        var host = Host();
        var quoted = await host.Service.RecordImport.CommitAsync(Request("24", Utf8("designation[text],C[kN],C_0[kN]\r\n6205,\"14,8\",\"7,8\"\r\n")) with { Number = NumberProfile.Comma });
        Assert.True(quoted.Committed, quoted.Refusal);
        Assert.Equal(14.8, quoted.Records[0].Values.Single(v => v.Symbol == "C").Number);

        var semicolon = await host.Service.RecordImport.CommitAsync(Request("24", Utf8("designation[text];C[kN];C_0[kN]\r\n6206;19,5;11,2\r\n"), Provenance(title: "Other")) with { Number = NumberProfile.Comma });
        Assert.True(semicolon.Committed, semicolon.Refusal);

        var point = await host.Service.RecordImport.PreviewAsync(Request("24", Utf8("designation[text],C[kN],C_0[kN]\r\n6207,14.8,7.8\r\n"), Provenance(title: "Third")));
        Assert.True(point.CanCommit, Describe(point));

        // A grid quotes every cell that holds a comma, so its numbers cannot be split and it is not refused under a decimal comma.
        var grid = RecordImportService.GridToCsv(RecordTemplates.Find("24")!, ["", "kN", "kN"], [["6208", "14,8", "7"]]);
        var typed = await host.Service.RecordImport.PreviewAsync(Request("24", grid.Bytes, Provenance(title: "Typed"), fileName: null) with { Number = NumberProfile.Comma });
        Assert.True(typed.CanCommit, Describe(typed));
    }

    // ------------------------------------------------------------------ SEC-C-02 / R-C-02: a short expected value let an unconservative output pass

    /// <summary>
    /// An expected value written as an exact '1' or '5' was taken as rounded to a whole number, so ±0.5 was allowed: a utilisation of 1.4 against
    /// an expected 1, or 4.55 A against 5 A, was a Match and the auto-compare suggested Pass. The allowance is now capped at 1 % of the expected value.
    /// </summary>
    [Theory]
    [InlineData("u_r0", "1", "", "1.4")]
    [InlineData("I_b", "5", "A", "4.55 A")]
    [InlineData("u", "0.6", "", "0.64")]
    [InlineData("P", "4", "kN", "3.7 kN")]
    [InlineData("x", "1.5E+06", "N", "1.47E+06 N")]
    public void SEC_C_02_An_Output_More_Than_One_Percent_From_A_Short_Expected_Value_Does_Not_Agree(string symbol, string expected, string unit, string shown)
    {
        var result = GuidedTolerance.CompareShown(symbol, expected, unit, shown);
        Assert.Equal(GuidedCompareOutcome.Mismatch, result.Outcome);
        Assert.Equal($"±1 % of {expected}", result.Allowance);
    }

    [Theory]
    [InlineData("1", "", "1")]
    [InlineData("5", "A", "5.0004 A")]
    [InlineData("0.6", "", "0.60001")]
    [InlineData("4", "kN", "4.039 kN")]
    [InlineData("0.0001", "", "0.00010004")]
    [InlineData("-7", "um", "-7.06 um")]
    public void SEC_C_02_A_Correct_Value_As_The_Form_Shows_It_Still_Agrees(string expected, string unit, string shown) =>
        Assert.True(GuidedTolerance.CompareShown("x", expected, unit, shown).IsMatch);

    [Fact]
    public void SEC_C_02_A_Utilisation_Rounded_To_The_Forms_Three_Figures_Still_Agrees()
    {
        Assert.True(GuidedTolerance.CompareShown("u", "0.7285", string.Empty, "0.729", 3).IsMatch);
        Assert.True(GuidedTolerance.CompareShown("u", "1", string.Empty, "1", 3).IsMatch);
        Assert.False(GuidedTolerance.CompareShown("u", "1", string.Empty, "1.02", 3).IsMatch);
    }

    [Fact]
    public void SEC_C_02_An_Engine_Regression_In_A_Worked_Example_Is_Suggested_As_A_Fail_Not_A_Pass()
    {
        var step = GuidedTestScript.Current.Find("example:elec.load-budget/pump-skid-panel")!;
        var calculator = ShippedCalculators.All.Single(c => c.Template.Id == step.TemplateId);
        var (inputs, _) = CalcInputJson.Read(step.InputsJson!, calculator.Template);
        var run = CalcRunner.Run(calculator, inputs);

        // The engine's own run agrees (the example's governing check is a FAIL on purpose, and the form shows that FAIL); the same run with I_b
        // under-reported by 9 % does not.
        var own = GuidedAutoCompare.Compare(step, run);
        Assert.Equal(ManualTestOutcome.Pass, own.Suggested);
        var regressed = run with { Outputs = [.. run.Outputs.Select(o => o.Symbol == "I_b" ? o with { Value = ScalarValue.Of(4.55, "A") } : o)] };
        var compared = GuidedAutoCompare.Compare(step, regressed);
        Assert.Equal(ManualTestOutcome.Fail, compared.Suggested);
        Assert.Contains(compared.Mismatches, m => m.Symbol == "I_b");
    }

    // ------------------------------------------------------------------ SEC-C-03 / R-C-03: the demonstration's removal deleted a profile it did not write

    [Fact]
    public async Task SEC_C_03_Remove_Keeps_A_Scan_Profile_The_Person_Saved_Under_The_Demonstrations_Id()
    {
        var host = DemoHost.New();
        var demo = DemoHost.Service(host);
        var mine = CalcDemoProject.StackProfile with { Title = "My own seal-stack profile" };
        Assert.Null((await host.Service.SaveScanProfileAsync(mine)).Refusal);

        Assert.True((await demo.InstallAsync()).Succeeded);
        var removed = await demo.RemoveAsync();
        Assert.True(removed.Succeeded, removed.Message);

        // The person's version stays; the version the install did write is gone.
        Assert.Equal("My own seal-stack profile", (await host.Service.FindScanProfileAsync(CalcDemoProject.StackProfileId, 1))?.Title);
        Assert.Null(await host.Service.FindScanProfileAsync(CalcDemoProject.GlandProfileId, 1));
    }

    // ------------------------------------------------------------------ SEC-C-04 / R-C-04: a provenance origin no record can have

    [Theory]
    [InlineData("7")]
    [InlineData("1")]
    [InlineData("\"Derived, ManufacturerExample\"")]
    [InlineData("Indicative")]
    public void SEC_C_04_A_Provenance_Origin_Given_As_A_Number_Or_A_Combination_Is_Refused(string origin)
    {
        var file = Utf8("kind,title,source_title,edition,amendment,table_or_clause,licence_holder,licensed_source,origin\r\n" +
                        $"cable.ampacity,T,S,2018,,Table 1,Me,true,{origin}\r\n" +
                        "cable.ampacity,U,S,2018,,Table 1,Me,true,Derived\r\n");
        var rows = RecordImportService.ReadProvenanceFile(file, out var problems);

        var row = Assert.Single(rows);
        Assert.Equal(CalcDataOrigin.Derived, row.Provenance.Origin);
        Assert.Contains(problems, p => p.StartsWith("Line 2: origin must be", StringComparison.Ordinal));
        Assert.NotNull((Provenance() with { Origin = (CalcDataOrigin)7 }).Problem());
    }
}
