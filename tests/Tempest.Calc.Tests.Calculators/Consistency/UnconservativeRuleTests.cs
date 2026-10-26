using System.Runtime.CompilerServices;
using System.Text.Json;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2 (fix, M1): every applicability rule of every template declares what it says about the result, <see cref="CalcRuleEffect"/>: conservative, or
/// unconservative (the sheet can still pass although the result may be on the unsafe side). Nothing here matches words. The contract:
/// <list type="bullet">
/// <item>an unconservative rule is a <b>block</b> (the sheet gives no result), or</item>
/// <item>it is <b>paired with a check that fails when it fires</b>: <see cref="Probes"/> runs the template on inputs that make the rule fire and requires the named check to fail, or</item>
/// <item>it is on <c>UnconservativeAllowList.json</c> with the reason it is not yet one of those and what remains to do. The list only shrinks: it has
/// <see cref="Ceiling"/> entries; removing an entry lowers the ceiling in the same commit, and adding one fails the build unless a reviewer raises it.</item>
/// </list>
/// </summary>
public sealed class UnconservativeRuleTests
{
    /// <summary>The number of entries on the allow-list today. Lower it with every entry removed; never raise it.</summary>
    public const int Ceiling = 72;

    /// <summary>One unconservative warning proved to be paired with a failing check: the template, the rule, the check that fails and inputs that make the rule fire.</summary>
    private sealed record Probe(string Template, string Rule, string CheckStartsWith, Func<CalcInputSet> Inputs);

    private static readonly IReadOnlyList<Probe> Probes =
    [
        new("struct.en1993-member", "k-default", "Member interaction", () => UnconservativePassTests.ShsMember()),
        new("mech.shaft", "critical-speed", "Critical speed", () => UnconservativePassTests.RoomyShaft().With(CalcInputValue.Quantity("n", 4000, "rpm"))),
        new("mech.spring", "buckling", "Working deflection against the buckling", () => UnconservativePassTests.SlenderSpringInputs(60)),
        new("elec.motor-sizing", "speed-above-rated", "Motor speed against the rated speed", UnconservativePassTests.MotorAboveRated),
        new("struct.ltb-interaction", "table-b1", "Interaction, equation 6.62", ProbeInputs.LtbTableB1),
        new("mech.shaft", "critical-speed-skipped", "Critical speed", ProbeInputs.ShaftWithoutCriticalSpeed),
        new("mech.mechanism", "contact", "Follower stays on the cam", ProbeInputs.WeakSpringCam),
        new("bs.pipe-pump", "duty-unstable", "Duty point against a stable crossing", ProbeInputs.UnstablePump),
        new("fp.accumulator", "exponent", "Gas exponent against a fast discharge", ProbeInputs.SlowEmergencyAccumulator),
        new("fp.accumulator", "realgas", "Real-gas factor against", ProbeInputs.IdealGasAccumulator),
        new("elec.pcb-trace", "copper", "Copper weight against the range", ProbeInputs.HalfOunceTrace),
        new("struct.base-plate", "prying", "Prying force against the bolt tension", ProbeInputs.PryingBasePlate),
        new("struct.beam-formulas", "deep-beam", "Shortest span of a beam that is not deep", ProbeInputs.DeepBeam),
        new("struct.plate-bending", "ritz-converge", "Ritz change against the tolerance", ProbeInputs.LongClampedPlate),
    ];

    private static string AllowListPath([CallerFilePath] string callerFilePath = "") => Path.Combine(Path.GetDirectoryName(callerFilePath)!, "UnconservativeAllowList.json");

    /// <summary>Template id, then rule id, then the reason.</summary>
    private static IReadOnlyDictionary<string, Dictionary<string, string>> AllowList() =>
        JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(AllowListPath())) ?? [];

    private static IEnumerable<(string Template, CalcApplicabilityRule Rule)> UnconservativeWarnings() =>
        AllCalculators.All.SelectMany(c => c.Template.ApplicabilityRules.Where(r => r.Effect == CalcRuleEffect.Unconservative && r.Severity == CalcNoticeSeverity.Warning).Select(r => (c.Template.Id, r)));

    [Fact]
    public void Every_Rule_Of_Every_Template_Declares_Its_Effect_As_A_Defined_Value()
    {
        var rules = AllCalculators.All.SelectMany(c => c.Template.ApplicabilityRules).ToList();
        Assert.True(rules.Count > 300, $"{rules.Count} rules found");
        Assert.All(rules, r => Assert.True(Enum.IsDefined(r.Effect), $"{r.Id}: the effect is not declared"));
        Assert.Contains(rules, r => r.Effect == CalcRuleEffect.Unconservative);
        Assert.Contains(rules, r => r.Effect == CalcRuleEffect.Conservative);
    }

    [Fact]
    public void An_Unconservative_Rule_Is_A_Block_Or_Is_Paired_With_A_Failing_Check_Or_Is_On_The_Allow_List_With_A_Reason()
    {
        var allowed = AllowList();
        var probed = Probes.Select(p => (p.Template, p.Rule)).ToHashSet();
        var problems = new List<string>();

        foreach (var (template, rule) in UnconservativeWarnings())
        {
            var listed = allowed.TryGetValue(template, out var rules) && rules.ContainsKey(rule.Id);

            if (!probed.Contains((template, rule.Id)) && !listed)
                problems.Add($"{template}: '{rule.Id}' is unconservative and is neither a block nor paired with a failing check; make it one, or list it in UnconservativeAllowList.json with its reason (the list may not grow)");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_Allow_List_Names_Only_Unconservative_Warnings_Each_With_A_Reason_And_Lists_No_Rule_That_Is_Paired()
    {
        var unconservative = UnconservativeWarnings().Select(x => (x.Template, x.Rule.Id)).ToHashSet();
        var probed = Probes.Select(p => (p.Template, p.Rule)).ToHashSet();
        var problems = new List<string>();

        foreach (var (template, rules) in AllowList())
        {
            foreach (var (rule, reason) in rules)
            {
                if (!unconservative.Contains((template, rule)))
                    problems.Add($"{template}: '{rule}' is on the allow-list and is not an unconservative warning of that template; remove the entry");
                else if (probed.Contains((template, rule)))
                    problems.Add($"{template}: '{rule}' is paired with a failing check; remove the entry");

                if (reason.Trim().Length < 20 || !reason.TrimEnd().EndsWith('.'))
                    problems.Add($"{template}: '{rule}' needs a reason that says why it is not yet a block or a failing check, in a sentence");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void The_Allow_List_Only_Shrinks_Its_Entry_Count_Is_The_Ceiling()
    {
        var entries = AllowList().Sum(p => p.Value.Count);
        Assert.True(entries <= Ceiling, $"UnconservativeAllowList.json has {entries} entries, above the ceiling of {Ceiling}: the list may not grow.");
        Assert.True(entries == Ceiling, $"UnconservativeAllowList.json has {entries} entries and the ceiling is {Ceiling}: lower UnconservativeRuleTests.Ceiling to {entries} in the same commit.");
    }

    [Fact]
    public void Each_Probe_Makes_Its_Rule_Fire_And_Fails_The_Check_It_Is_Paired_With()
    {
        var unconservative = UnconservativeWarnings().Select(x => (x.Template, x.Rule.Id)).ToHashSet();

        foreach (var probe in Probes)
        {
            Assert.Contains((probe.Template, probe.Rule), unconservative);
            var calculator = AllCalculators.Get(probe.Template);
            var result = CalcRunner.Run(calculator, probe.Inputs());
            Assert.False(result.IsBlocked, $"{probe.Template}: the probe is blocked: {string.Join("; ", result.Blocks.Select(b => b.Message))}");
            Assert.Contains(result.Notices, n => n.RuleId == probe.Rule && n.Severity == CalcNoticeSeverity.Warning);
            var check = Assert.Single(result.Checks, c => c.Name.StartsWith(probe.CheckStartsWith, StringComparison.Ordinal));
            Assert.False(check.Passed, $"{probe.Template}: '{check.Name}' passes although '{probe.Rule}' fired");
            Assert.False(result.Passed);
        }
    }

    [Fact]
    public void The_Two_Member_Rules_The_Audit_Named_Are_Unconservative_And_Are_Blocks()
    {
        var rules = AllCalculators.Get("struct.en1993-member").Template.ApplicabilityRules;

        foreach (var id in new[] { "net-section", "no-ltb" })
        {
            var rule = Assert.Single(rules, r => r.Id == id);
            Assert.Equal(CalcRuleEffect.Unconservative, rule.Effect);
            Assert.Equal(CalcNoticeSeverity.Block, rule.Severity);
        }
    }

    [Fact]
    public void A_Rule_Does_Not_Change_The_Content_Hash_When_Only_Its_Effect_Changes()
    {
        // The effect is a label for the contract, not part of the calculation: it is not in the canonical text, so declaring it did not bump any template.
        var calculator = AllCalculators.Get("bs.glaser");
        var template = calculator.Template;
        var flipped = template with { ApplicabilityRules = [.. template.ApplicabilityRules.Select(r => r with { Effect = r.Effect == CalcRuleEffect.Conservative ? CalcRuleEffect.Unconservative : CalcRuleEffect.Conservative })] };
        var formulas = CalcTemplateHasher.Formulas(calculator);
        Assert.Equal(CalcTemplateHasher.Hash(template, formulas), CalcTemplateHasher.Hash(flipped, formulas));
    }
}
