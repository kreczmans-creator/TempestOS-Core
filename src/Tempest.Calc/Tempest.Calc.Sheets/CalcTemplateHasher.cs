using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Tempest.Calc.Engine;

namespace Tempest.Calc.Sheets;

/// <summary>
/// The content hash of a template: SHA-256 over its declarative content
/// and over the formulas of a run on its defaults, so a changed input
/// limit, a changed assumption or a changed formula all change the hash.
/// Shown in the footer as the first 16 hex characters.
/// </summary>
public static class CalcTemplateHasher
{
    public static string Hash(ICalcCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        return Hash(calculator.Template, Formulas(calculator));
    }

    public static string Hash(CalcTemplate template, IReadOnlyList<string> formulas)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(formulas);

        var text = CanonicalText(template, formulas);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>The first 16 hex characters of a full hash, as printed on sheets.</summary>
    public static string Short(string fullHash) => string.IsNullOrEmpty(fullHash) ? string.Empty : fullHash[..Math.Min(16, fullHash.Length)];

    /// <summary>The formulas a run on the template's defaults produces; an empty list when the defaults are not a runnable set.</summary>
    public static IReadOnlyList<string> Formulas(ICalcCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);

        var inputs = DefaultInputs(calculator.Template);
        var result = CalcRunner.Run(calculator, inputs);
        return result.Steps.Where(s => s.Kind != CalcStepKind.Input).Select(s => $"{s.Symbol} = {s.Formula}").ToList();
    }

    /// <summary>
    /// The inputs a new sheet opens with: the defaults the template <b>declares</b>, and nothing else. A known-answer case's
    /// values are test data, not defaults, and an input with no declared default opens blank for the user to enter (Green
    /// G-A-02 / G-B-06: a test case's rho = 0 or R = 95 % must not open a new shaft or support sheet labelled as a default). Tables
    /// have no declared default and open empty. <see cref="DefaultInputs"/> (test case first) remains for known-answer runs,
    /// the template hash, derived output ports and tests.
    /// </summary>
    public static CalcInputSet NewSheetInputs(CalcTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var set = new CalcInputSet();
        AddDeclaredDefaults(template, set);
        return set;
    }

    /// <summary>An input set built from every default the template declares, over the first test case's inputs (used where a runnable set is needed: hashing, known-answer runs, derived ports).</summary>
    public static CalcInputSet DefaultInputs(CalcTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var set = new CalcInputSet();

        if (template.TestCases.Count > 0)
        {
            foreach (var v in template.TestCases[0].Inputs)
                set.Set(v);
        }

        AddDeclaredDefaults(template, set);
        return set;
    }

    private static void AddDeclaredDefaults(CalcTemplate template, CalcInputSet set)
    {
        foreach (var spec in template.Inputs)
        {
            if (set.Find(spec.Symbol) is not null)
                continue;

            switch (spec.Kind)
            {
                case CalcInputKind.Quantity when spec.Default is { } d:
                    set.Set(CalcInputValue.Quantity(spec.Symbol, d, spec.DefaultUnit));
                    break;
                case CalcInputKind.Number when spec.Default is { } d:
                    set.Set(CalcInputValue.Of(spec.Symbol, d));
                    break;
                case CalcInputKind.Choice when spec.DefaultChoice is { } c:
                    set.Set(CalcInputValue.Choice(spec.Symbol, c));
                    break;
                case CalcInputKind.Boolean:
                    set.Set(CalcInputValue.Switch(spec.Symbol, spec.Default is { } f && f != 0));
                    break;
            }
        }
    }

    public static string CanonicalText(CalcTemplate t, IReadOnlyList<string> formulas)
    {
        var sb = new StringBuilder();
        sb.Append("id=").Append(t.Id).Append('\n');
        sb.Append("version=").Append(t.Version).Append('\n');
        sb.Append("discipline=").Append(t.Discipline).Append('\n');
        sb.Append("title=").Append(t.Title).Append('\n');
        sb.Append("scope=").Append(t.Scope).Append('\n');
        sb.Append("method=").Append(t.Method).Append('\n');

        foreach (var i in t.Inputs)
        {
            sb.Append("input=").Append(i.Symbol).Append('|').Append(i.Description).Append('|').Append(i.Kind).Append('|').Append(i.DefaultUnit).Append('|')
              .Append(N(i.Default)).Append('|').Append(N(i.Min)).Append('|').Append(N(i.Max)).Append('|').Append(N(i.HardMin)).Append('|').Append(N(i.HardMax)).Append('|')
              .Append(string.Join(",", i.Choices ?? [])).Append('|').Append(i.DefaultChoice).Append('|').Append(i.IsOptional).Append('\n');

            // Addendum B, W3: only a template that maps an input to a material property changes its hash, so every earlier template keeps its hash.
            if (i.MaterialProperty is { Length: > 0 } material)
                sb.Append("  material=").Append(material).Append('\n');

            foreach (var c in i.Columns ?? [])
                sb.Append("  column=").Append(c.Key).Append('|').Append(c.Label).Append('|').Append(c.Kind).Append('|').Append(c.DefaultUnit).Append('|').Append(string.Join(",", c.Choices ?? [])).Append('\n');
        }

        foreach (var a in t.Assumptions)
            sb.Append("assumption=").Append(a.Statement).Append('|').Append(a.Basis).Append('\n');

        foreach (var c in t.ClauseRefs)
            sb.Append("clause=").Append(c.Standard).Append('|').Append(c.Clause).Append('|').Append(c.Title).Append('\n');

        foreach (var r in t.ApplicabilityRules)
            sb.Append("rule=").Append(r.Id).Append('|').Append(r.Description).Append('|').Append(r.Severity).Append('\n');

        foreach (var tc in t.TestCases)
        {
            sb.Append("test=").Append(tc.Id).Append('|').Append(tc.Source.Title).Append('|').Append(tc.Source.Location).Append('|').Append(tc.Status).Append('\n');

            foreach (var e in tc.Expected)
                sb.Append("  expect=").Append(e.Symbol).Append('|').Append(N(e.Value)).Append('|').Append(e.Unit).Append('|').Append(N(e.RelativeTolerance)).Append('\n');
        }

        // Declared output ports are part of a template's contract; a template that declares none hashes as before.
        foreach (var o in t.OutputPorts ?? [])
            sb.Append("port=").Append(o.Symbol).Append('|').Append(o.Unit).Append('|').Append(o.Description).Append('\n');

        foreach (var k in t.RecordKinds ?? [])
            sb.Append("records=").Append(k.Kind).Append('|').Append(string.Join(",", k.Symbols)).Append('\n');

        if (t.IndependentCheck)
            sb.Append("independent-check\n");

        foreach (var f in formulas)
            sb.Append("formula=").Append(f).Append('\n');

        return sb.ToString();
    }

    private static string N(double? v) => v is null ? string.Empty : v.Value.ToString("R", CultureInfo.InvariantCulture);
}
