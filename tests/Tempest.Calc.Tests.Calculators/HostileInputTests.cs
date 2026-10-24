using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>Phase 6: every calculator fed hostile text in each free-text input and table column renders a sheet without throwing and without the text escaping its cell.</summary>
public sealed class HostileInputTests
{
    private static readonly string[] Hostile =
    [
        "=cmd()|' /C calc'!A0",
        "<script>alert(1)</script>",
        "line1\r\nline2\t\u0000\u001b[31m",
        "\"; DROP TABLE search_index; --",
        new string('x', 5000),
        "𝔘𝔫𝔦𝔠𝔬𝔡𝔢 ‮ reversed",
    ];

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Hostile_Text_Is_Contained(string id)
    {
        var calculator = AllCalculators.Get(id);
        var template = calculator.Template;
        var baseInputs = CalcTemplateHasher.DefaultInputs(template).Values.ToDictionary(v => v.Symbol);

        foreach (var hostile in Hostile)
        {
            var inputs = new Dictionary<string, CalcInputValue>(baseInputs);

            foreach (var spec in template.Inputs)
            {
                if (spec.Kind == CalcInputKind.Text)
                    inputs[spec.Symbol] = CalcInputValue.Choice(spec.Symbol, hostile);

                if (spec.Kind == CalcInputKind.Table && inputs.TryGetValue(spec.Symbol, out var table) && table.Rows is { Count: > 0 } rows)
                {
                    var textColumns = (spec.Columns ?? []).Where(c => c.Kind == CalcInputKind.Text).Select(c => c.Key).ToList();

                    if (textColumns.Count > 0)
                    {
                        var poisoned = rows.Select(r => new CalcRow(r.Cells.ToDictionary(kv => kv.Key, kv => textColumns.Contains(kv.Key) ? new CalcCell(Text: hostile) : kv.Value, StringComparer.Ordinal))).ToList();
                        inputs[spec.Symbol] = table with { Rows = poisoned };
                    }
                }
            }

            var set = new CalcInputSet([.. inputs.Values]);
            var result = CalcRunner.Run(calculator, set);
            var sheet = CalcSheetTextRenderer.Render(CalcSheetBuilder.Build(template, set, result,
                new CalcSheetHeader("Hostile", "H", template.Title, "A", CalcSheetStatus.Draft, "TempestOS", null, null, null, null),
                GoldenFiles.FixedFooter(template, template.TestCases.Count, template.TestCases.Count)));

            Assert.DoesNotContain("\u0000", sheet, StringComparison.Ordinal);
            Assert.DoesNotContain("\u001b", sheet, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", sheet, StringComparison.Ordinal);
            Assert.True(sheet.Length < 400_000, "a hostile string must not blow the sheet up");
        }
    }
}
