using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>Black B-A-05: the worksheet's Export inputs (CSV) and Import inputs give a worked example back whole — tables included.</summary>
public sealed class CsvRoundTripTests
{
    [Theory]
    [MemberData(nameof(WorkedExamples.Keys), MemberType = typeof(WorkedExamples))]
    [Trait("Verification", "WorkedExample")]
    public void A_Worked_Example_Survives_The_CSV_Export_And_Import_Unchanged(string key)
    {
        var example = WorkedExamples.Get(key);
        var calculator = AllCalculators.Get(example.TemplateId);

        var csv = CalcCsv.ExportInputs(calculator.Template, example.Inputs);
        var (back, notices) = CalcCsv.ImportInputs(calculator.Template, csv);

        Assert.True(notices.Count == 0, string.Join("; ", notices.Select(n => n.Message)));

        // The same inputs: every symbol, every table row and cell, every provenance.
        Assert.Equal(CalcInputJson.Write(Normalise(calculator.Template, example.Inputs)), CalcInputJson.Write(Normalise(calculator.Template, back)));

        // The same answer: no check, warning or block appears or goes away.
        var before = CalcRunner.Run(calculator, example.Inputs);
        var after = CalcRunner.Run(calculator, back);
        Assert.Equal(before.Outputs.Select(o => $"{o.Symbol}={o.ValueText}"), after.Outputs.Select(o => $"{o.Symbol}={o.ValueText}"));
        Assert.Equal(before.Checks.Select(c => $"{c.Name}:{c.Passed}"), after.Checks.Select(c => $"{c.Name}:{c.Passed}"));
        Assert.Equal(before.Warnings.Select(w => w.RuleId), after.Warnings.Select(w => w.RuleId));
        Assert.Equal(before.Blocks.Select(w => w.RuleId), after.Blocks.Select(w => w.RuleId));
    }

    /// <summary>
    /// The inputs the template knows, with each table row's cells in key order and its empty cells (an optional column left blank)
    /// dropped: the sheet reads a blank cell and no cell alike, and a dictionary has no order to keep.
    /// </summary>
    private static CalcInputSet Normalise(CalcTemplate template, CalcInputSet inputs) => new(inputs.Values.Where(v => template.FindInput(v.Symbol) is not null).Select(v => v.Rows is null ? v : v with
    {
        Rows = [.. v.Rows.Select(r => new CalcRow(r.Cells.Where(c => c.Value.Number is not null || c.Value.Text is not null || c.Value.Flag is not null).OrderBy(c => c.Key, StringComparer.Ordinal).ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal)))],
    }));
}
