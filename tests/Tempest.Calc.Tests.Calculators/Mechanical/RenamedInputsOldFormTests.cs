using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// C-2 phase B (mechanical, second group): an input renamed to follow the symbol standard keeps its old symbol as an alias, so a calculation, an inputs file, a CSV
/// file, a pack binding or an example written before the rename still loads and gives the same values. Each case runs a template on its defaults twice, once with the
/// input under its current symbol and once under the old one, and requires the same outputs, checks and notices.
/// </summary>
public sealed class RenamedInputsOldFormTests
{
    /// <summary>The template, the old symbol of the input and its current symbol.</summary>
    public static TheoryData<string, string, string> Renames => new()
    {
        { "mech.thin-vessel", "P", "p_design" },
        { "mech.thin-vessel", "R", "R_in" },
        { "mech.thin-vessel", "n", "n_design" },
        { "mech.tolerance-stack", "cpk_target", "cpk_req" },
        { "mech.shaft", "R", "R_rel" },
        { "mech.spring", "n", "n_active" },
        { "mech.spring", "l_1", "l_leg_1" },
        { "mech.spring", "l_2", "l_leg_2" },
    };

    private static CalcInputSet Current(ICalcCalculator calculator) => CalcTemplateHasher.DefaultInputs(calculator.Template);

    private static CalcInputSet Old(CalcInputSet current, string oldSymbol, string currentSymbol) =>
        new([.. current.Values.Select(v => v.Symbol == currentSymbol ? v with { Symbol = oldSymbol } : v)]);

    private static void SameRun(CalcRunResult expected, CalcRunResult actual)
    {
        Assert.Equal(expected.Outputs.Count, actual.Outputs.Count);

        foreach (var output in expected.Outputs)
        {
            var other = actual.FindOutput(output.Symbol);
            Assert.NotNull(other);

            if (output.Value is ScalarValue s)
                Assert.Equal(s.Si, ((ScalarValue)other!.Value).Si);
            else
                Assert.Equal(output.ValueText, other!.ValueText);
        }

        Assert.Equal(expected.Checks.Select(c => (c.Name, c.Utilisation, c.Passed)), actual.Checks.Select(c => (c.Name, c.Utilisation, c.Passed)));
        Assert.Equal(expected.Notices.Select(n => (n.RuleId, n.Message)), actual.Notices.Select(n => (n.RuleId, n.Message)));
    }

    [Theory]
    [MemberData(nameof(Renames))]
    public void A_Run_With_The_Old_Symbol_Gives_The_Same_Values_As_The_Current_One(string templateId, string oldSymbol, string currentSymbol)
    {
        var calculator = AllCalculators.Get(templateId);
        var current = Current(calculator);
        Assert.NotNull(current.Find(currentSymbol));

        var old = Old(current, oldSymbol, currentSymbol);
        Assert.Null(old.Find(currentSymbol));
        Assert.NotNull(old.Find(oldSymbol));

        SameRun(CalcRunner.Run(calculator, current), CalcRunner.Run(calculator, old));
    }

    [Theory]
    [MemberData(nameof(Renames))]
    public void The_Old_Symbol_Is_An_Alias_Of_The_Input_And_Is_Stored_Under_The_Current_Symbol(string templateId, string oldSymbol, string currentSymbol)
    {
        var template = AllCalculators.Get(templateId).Template;
        var spec = template.ResolveInput(oldSymbol);

        Assert.NotNull(spec);
        Assert.Equal(currentSymbol, spec!.Symbol);
        Assert.Contains(oldSymbol, spec.Aliases!);
        Assert.Null(template.FindInput(oldSymbol));
        Assert.Equal(currentSymbol, CalcInputAliases.Canonical(template, oldSymbol));

        var canonical = CalcInputAliases.Canonicalise(template, [.. Old(Current(AllCalculators.Get(templateId)), oldSymbol, currentSymbol).Values]);
        Assert.Contains(canonical, v => v.Symbol == currentSymbol);
        Assert.DoesNotContain(canonical, v => v.Symbol == oldSymbol);
    }

    [Theory]
    [MemberData(nameof(Renames))]
    public void An_Inputs_File_And_A_Csv_File_Written_With_The_Old_Symbol_Load_Under_The_Current_One(string templateId, string oldSymbol, string currentSymbol)
    {
        var calculator = AllCalculators.Get(templateId);
        var current = Current(calculator);
        var expected = CalcRunner.Run(calculator, current);

        var json = CalcInputJson.Write(Old(current, oldSymbol, currentSymbol));
        Assert.Contains($"\"Symbol\": \"{oldSymbol}\"", json, StringComparison.Ordinal);
        var (fromJson, jsonNotices) = CalcInputJson.Read(json, calculator.Template);
        Assert.DoesNotContain(jsonNotices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.NotNull(fromJson.Find(currentSymbol));
        Assert.Null(fromJson.Find(oldSymbol));
        SameRun(expected, CalcRunner.Run(calculator, fromJson));

        var csv = CalcCsv.ExportInputs(calculator.Template, current).Replace("\r\n", "\n", StringComparison.Ordinal);
        var oldCsv = string.Join('\n', csv.Split('\n').Select(line => line.StartsWith(currentSymbol + ",", StringComparison.Ordinal) ? oldSymbol + line[currentSymbol.Length..] : line));
        Assert.NotEqual(csv, oldCsv);
        var (fromCsv, csvNotices) = CalcCsv.ImportInputs(calculator.Template, oldCsv);
        Assert.DoesNotContain(csvNotices, n => n.Severity == CalcNoticeSeverity.Block);
        Assert.NotNull(fromCsv.Find(currentSymbol));
        Assert.Null(fromCsv.Find(oldSymbol));
        SameRun(expected, CalcRunner.Run(calculator, fromCsv));
    }

    [Theory]
    [MemberData(nameof(Renames))]
    public void An_Input_Given_Under_Both_Symbols_With_Different_Values_Is_Refused(string templateId, string oldSymbol, string currentSymbol)
    {
        var calculator = AllCalculators.Get(templateId);
        var current = Current(calculator);
        var value = current.Find(currentSymbol)!;
        var other = value with { Symbol = oldSymbol, Number = (value.Number ?? 0) * 1.5 + 1 };

        Assert.Throws<CalcInputAliasConflictException>(() => CalcInputAliases.Canonicalise(calculator.Template, [.. current.Values, other]));
    }
}
