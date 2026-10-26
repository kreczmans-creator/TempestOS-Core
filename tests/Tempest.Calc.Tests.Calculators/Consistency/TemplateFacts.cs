using System.Text.RegularExpressions;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>One quantity a template names: an input, a table column, an output of a default run or a port.</summary>
internal sealed record QuantityFact(string Symbol, string Description, Dimension Dimension, string Unit, string Origin, bool IsOptional = false);

/// <summary>One <c>c.Warn(...)</c> or <c>c.Block(...)</c> in a calculator's source: the rule id and the message as written (an interpolation hole is replaced by <c>x</c>).</summary>
internal sealed record EmitSite(CalcNoticeSeverity Severity, string? RuleId, string? Message, bool HasHoles);

/// <summary>Everything the consistency contract needs to know about one template, worked out once.</summary>
internal sealed partial class TemplateFacts
{
    private readonly Lazy<CalcRunResult> _defaultRun;
    private readonly Lazy<IReadOnlyList<CalcRunResult>> _caseRuns;
    private readonly Lazy<IReadOnlyList<EmitSite>> _sites;
    private readonly Lazy<IReadOnlyList<QuantityFact>> _quantities;

    public TemplateFacts(ICalcCalculator calculator)
    {
        Calculator = calculator;
        _defaultRun = new(() => CalcRunner.Run(calculator, CalcTemplateHasher.DefaultInputs(calculator.Template)));
        _caseRuns = new(() => [.. calculator.Template.TestCases.Select(c => CalcRunner.Run(calculator, CaseInputs(calculator.Template, c)))]);
        _sites = new(() => ScanSource(calculator));
        _quantities = new(Quantities);
    }

    public ICalcCalculator Calculator { get; }

    public CalcTemplate Template => Calculator.Template;

    public string Id => Template.Id;

    /// <summary>A run on the template's defaults over its first known-answer case (what <see cref="CalcTemplateHasher.DefaultInputs"/> gives).</summary>
    public CalcRunResult DefaultRun => _defaultRun.Value;

    /// <summary>A run of every known-answer case.</summary>
    public IReadOnlyList<CalcRunResult> CaseRuns => _caseRuns.Value;

    /// <summary>Every notice any of those runs raised, with the run it came from.</summary>
    public IEnumerable<CalcNotice> Notices => CaseRuns.Prepend(DefaultRun).SelectMany(r => r.Notices);

    public IReadOnlyList<EmitSite> Sites => _sites.Value;

    public IReadOnlyList<QuantityFact> QuantityFacts => _quantities.Value;

    private static CalcInputSet CaseInputs(CalcTemplate template, CalcTestCase testCase)
    {
        var set = CalcTemplateHasher.NewSheetInputs(template);

        foreach (var v in testCase.Inputs)
            set.Set(v);

        return set;
    }

    private IReadOnlyList<QuantityFact> Quantities()
    {
        var list = new List<QuantityFact>();

        foreach (var i in Template.Inputs)
        {
            if (i.Kind is CalcInputKind.Quantity or CalcInputKind.Vector && i.DefaultUnit.Length > 0)
                list.Add(new QuantityFact(i.Symbol, i.Description, i.Dimension, i.DefaultUnit, "input", i.IsOptional));
            else if (i.Kind is CalcInputKind.Number)
                list.Add(new QuantityFact(i.Symbol, i.Description, Dimension.None, string.Empty, "input", i.IsOptional));

            foreach (var c in i.Columns ?? [])
            {
                if (c.Kind == CalcInputKind.Quantity && c.DefaultUnit.Length > 0 && UnitCatalog.TryParse(c.DefaultUnit, out var unit, out _))
                    list.Add(new QuantityFact(c.Key, c.Label, unit.Dimension, c.DefaultUnit, "column", c.IsOptional));
                else if (c.Kind == CalcInputKind.Number)
                    list.Add(new QuantityFact(c.Key, c.Label, Dimension.None, string.Empty, "column", c.IsOptional));
            }
        }

        foreach (var o in DefaultRun.Outputs)
        {
            if (o.Value is ScalarValue s)
                list.Add(new QuantityFact(o.Symbol, o.Description, s.Dimension, s.DisplayUnit.Symbol, "output"));
        }

        foreach (var p in Template.OutputPorts ?? [])
        {
            if (p.Unit.Length > 0 && UnitCatalog.TryParse(p.Unit, out var unit, out _))
                list.Add(new QuantityFact(p.Symbol, p.Description, unit.Dimension, p.Unit, "port"));
        }

        return list;
    }

    // ---- source scan -------------------------------------------------

    [GeneratedRegex("""\.(?<kind>Warn|Block)\(\s*(?<id>"[^"]*"|[^,"]+?)\s*,\s*(?<msg>\$?"(?:[^"\\]|\\.)*"|[^;]*?)\)\s*;""", RegexOptions.Singleline)]
    private static partial Regex EmitCall();

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex Hole();

    private static IReadOnlyList<EmitSite> ScanSource(ICalcCalculator calculator)
    {
        var type = calculator.GetType();
        var stem = type.Name;
        var folder = Path.Combine(WorkedExamples.RepositoryRoot, "src", "Tempest.Calc", type.Assembly.GetName().Name!);
        var sites = new List<EmitSite>();

        if (!Directory.Exists(folder))
            return sites;

        foreach (var file in Directory.EnumerateFiles(folder, stem + "*.cs").Where(f => Path.GetFileName(f) == stem + ".cs" || Path.GetFileName(f).StartsWith(stem + ".", StringComparison.Ordinal)))
        {
            foreach (Match m in EmitCall().Matches(File.ReadAllText(file)))
            {
                var severity = m.Groups["kind"].Value == "Block" ? CalcNoticeSeverity.Block : CalcNoticeSeverity.Warning;
                var id = m.Groups["id"].Value.Trim();
                var msg = m.Groups["msg"].Value.Trim();
                var literal = Literal(msg);
                sites.Add(new EmitSite(severity, id.StartsWith('"') && id.EndsWith('"') ? id[1..^1] : null, literal, literal is not null && msg.StartsWith('$') && Regex.IsMatch(msg, @"(?<!\{)\{(?!\{)")));
            }
        }

        return sites;
    }

    /// <summary>The text of a string literal (interpolated or plain) with each interpolation hole replaced by <c>x</c>; <see langword="null"/> for anything that is not one literal.</summary>
    private static string? Literal(string expression)
    {
        var text = expression.StartsWith("$\"", StringComparison.Ordinal) ? expression[2..] : expression.StartsWith('"') ? expression[1..] : null;

        if (text is null || !text.EndsWith('"') || text.Length < 1)
            return null;

        text = text[..^1];

        // A message built from several pieces ("a" + b) is not one literal.
        if (text.Contains("\" +", StringComparison.Ordinal) || text.Contains("+ \"", StringComparison.Ordinal))
            return null;

        text = text.Replace("\\\"", "\"", StringComparison.Ordinal).Replace("{{", "\u0001", StringComparison.Ordinal).Replace("}}", "\u0002", StringComparison.Ordinal);

        // A hole holding a quoted string ends the literal early; such a message is not scanned.
        if (text.Count(c => c == '{') != text.Count(c => c == '}'))
            return null;

        while (Hole().IsMatch(text))
            text = Hole().Replace(text, "x");

        return text.Replace('\u0001', '{').Replace('\u0002', '}');
    }
}
