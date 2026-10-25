using System.Text;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators;

/// <summary>
/// Yellow Y-B-04: a printed sheet is read by an engineer who has never seen the build documents, so no rendered text may point
/// at one ("Addendum B §B5", "as quoted in Addendum B §B6 F3"). The sheets, the worked examples and the template's own text are
/// searched for the internal references.
/// </summary>
public sealed class SheetTextHygieneTests
{
    private static readonly string[] Forbidden = ["Addendum", "§B"];

    private static string Render(ICalcCalculator calculator, CalcInputSet inputs)
    {
        var template = calculator.Template;
        var result = CalcRunner.Run(calculator, inputs);
        var sheet = CalcSheetBuilder.Build(template, inputs, result, GoldenFiles.FixedHeader(template.Id, template.Title), GoldenFiles.FixedFooter(template, 0, 0));
        return CalcSheetTextRenderer.Render(sheet);
    }

    private static void AssertClean(string where, string text)
    {
        foreach (var word in Forbidden)
        {
            var at = text.IndexOf(word, StringComparison.Ordinal);
            Assert.True(at < 0, $"{where} prints '{word}': …{text[Math.Max(0, at - 60)..Math.Min(text.Length, at + 80)].ReplaceLineEndings(" ")}…");
        }
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Y_B_04_No_Sheet_Of_A_Calculator_Prints_An_Internal_Document_Reference(string id)
    {
        var calculator = AllCalculators.Get(id);
        var template = calculator.Template;

        // The sheet on the declared defaults, and the sheet of every known-answer case (the warnings each one raises are printed).
        AssertClean($"{id} on its defaults", Render(calculator, CalcTemplateHasher.DefaultInputs(template)));

        foreach (var testCase in template.TestCases)
        {
            var inputs = CalcTemplateHasher.NewSheetInputs(template);

            foreach (var value in testCase.Inputs)
                inputs.Set(value);

            AssertClean($"{id} / {testCase.Id}", Render(calculator, inputs));
        }

        // The template's own text, including what a known-answer case cites.
        var text = new StringBuilder();
        text.AppendLine(template.Title).AppendLine(template.Scope).AppendLine(template.Method);
        template.Inputs.ToList().ForEach(i => text.AppendLine(i.Description));
        template.Assumptions.ToList().ForEach(a => text.AppendLine(a.Statement).AppendLine(a.Basis));
        template.ClauseRefs.ToList().ForEach(r => text.AppendLine($"{r.Standard} {r.Clause} {r.Title}"));
        template.ApplicabilityRules.ToList().ForEach(r => text.AppendLine(r.Description));
        template.TestCases.ToList().ForEach(t => text.AppendLine(t.Title).AppendLine(t.Source.Title).AppendLine(t.Source.Location));
        (template.RecordKinds ?? []).ToList().ForEach(k => text.AppendLine(k.Title).AppendLine(k.Guidance));
        AssertClean($"{id} template text", text.ToString());
    }

    [Theory]
    [MemberData(nameof(WorkedExamples.Keys), MemberType = typeof(WorkedExamples))]
    public void Y_B_04_No_Worked_Example_Sheet_Prints_An_Internal_Document_Reference(string key)
    {
        var example = WorkedExamples.Get(key);
        AssertClean(key, Render(AllCalculators.Get(example.TemplateId), example.Inputs));
        AssertClean(key + " (notes)", example.Notes + example.Source.Title + example.Source.Location);

        foreach (var value in example.Inputs.Values)
            AssertClean($"{key} input {value.Symbol}", value.Provenance?.ToString() ?? string.Empty);
    }
}
