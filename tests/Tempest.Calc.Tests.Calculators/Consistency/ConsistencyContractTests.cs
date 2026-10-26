using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2: the catalogue-wide contract of <c>docs/calcs/sheet-standard.md</c>. Each rule (<see cref="ConsistencyRules"/>) is run over all 54 templates.
/// A template that does not meet a rule yet is named in <c>ConsistencyAllowList.json</c> with the number of offences it has today: the entry is keyed by
/// rule, template and count, so a template that is on the list and gains a new offence fails (the count rose), and so does one that has fixed an offence and
/// not lowered its entry. The allow-list only shrinks: the write mode lowers or removes entries and refuses to add one, or to raise a count.
///
/// To lower the list and rewrite the work list after fixing templates, run
/// <c>TEMPEST_WRITE_CONSISTENCY_ALLOWLIST=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter ConsistencyContractTests</c>
/// and commit <c>ConsistencyAllowList.json</c> and <c>docs/calcs/consistency-work-list.md</c>. A reviewer rejects a change that raises a number.
/// </summary>
public sealed class ConsistencyContractTests
{
    public const string WriteVariable = "TEMPEST_WRITE_CONSISTENCY_ALLOWLIST";

    private static readonly Lazy<IReadOnlyList<TemplateFacts>> Facts = new(() => [.. AllCalculators.All.Select(c => new TemplateFacts(c))]);

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>> Found = new(FindAll);

    public static TheoryData<string> RuleIds => [.. ConsistencyRules.All.Select(r => r.Id)];

    private static string AllowListPath([CallerFilePath] string callerFilePath = "") => Path.Combine(Path.GetDirectoryName(callerFilePath)!, "ConsistencyAllowList.json");

    private static string WorkListPath() => Path.Combine(WorkedExamples.RepositoryRoot, "docs", "calcs", "consistency-work-list.md");

    /// <summary>Rule id to template id to what is wrong, for every rule and every template that breaks it.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> FindAll()
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal);

        foreach (var rule in ConsistencyRules.All)
        {
            var offenders = new SortedDictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            foreach (var f in Facts.Value)
            {
                var offences = rule.Offences(f).ToList();

                if (offences.Count > 0)
                    offenders[f.Id] = offences;
            }

            result[rule.Id] = offenders;
        }

        return result;
    }

    /// <summary>The allow-list: rule id, then template id, then the number of offences the template has today.</summary>
    private static SortedDictionary<string, SortedDictionary<string, int>> LoadAllowList()
    {
        var path = AllowListPath();
        var raw = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(File.ReadAllText(path)) ?? [] : [];
        return new(raw.ToDictionary(p => p.Key, p => new SortedDictionary<string, int>(p.Value, StringComparer.Ordinal), StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void Every_Template_Meets_The_Rule_Or_Is_On_The_Allow_List_With_Its_Count_And_Gains_No_Offence(string ruleId)
    {
        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
        {
            WriteFiles();
            return;
        }

        var rule = ConsistencyRules.All.Single(r => r.Id == ruleId);
        var offenders = Found.Value[ruleId];
        var allowed = LoadAllowList().GetValueOrDefault(ruleId) ?? [];

        var newOffenders = offenders.Where(p => !allowed.ContainsKey(p.Key)).Select(p => $"  {p.Key}: {p.Value[0]} ({p.Value.Count} offence(s))").ToList();
        var moreOffences = offenders.Where(p => allowed.TryGetValue(p.Key, out var count) && p.Value.Count > count).Select(p => $"  {p.Key}: {p.Value.Count} offences, the allow-list has {allowed[p.Key]}; the new one is among: {string.Join(" | ", p.Value.Select(Short).Take(4))}").ToList();
        var fewerOffences = allowed.Where(p => (offenders.TryGetValue(p.Key, out var o) ? o.Count : 0) < p.Value).Select(p => $"{p.Key} ({p.Value} → {(offenders.TryGetValue(p.Key, out var o) ? o.Count : 0)})").ToList();

        Assert.True(newOffenders.Count == 0,
            $"{rule.Id} (audit row {rule.Rows}: {rule.Summary}) is broken by templates not on the allow-list; fix them (the list may not grow):{Environment.NewLine}{string.Join(Environment.NewLine, newOffenders)}");
        Assert.True(moreOffences.Count == 0,
            $"{rule.Id} is broken more often than the allow-list says; fix the new offence (the count may not rise):{Environment.NewLine}{string.Join(Environment.NewLine, moreOffences)}");
        Assert.True(fewerOffences.Count == 0,
            $"{rule.Id}: {string.Join(", ", fewerOffences)} have fewer offences than the allow-list says; lower the entry ({WriteVariable}=1 rewrites it, lowering only).");
    }

    private static string Short(string offence) => offence.Length <= 90 ? offence : offence[..89] + "…";

    [Fact]
    public void The_Allow_List_Is_Keyed_By_Rule_Template_And_Count_And_Names_Only_Known_Ones_In_Sorted_Order()
    {
        var path = AllowListPath();
        Assert.True(File.Exists(path), $"{path} is missing; it is the committed list of templates that do not meet a rule yet.");
        var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(File.ReadAllText(path))!;
        var rules = ConsistencyRules.All.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var templates = AllCalculators.All.Select(c => c.Template.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(raw, pair =>
        {
            Assert.Contains(pair.Key, rules);
            Assert.NotEmpty(pair.Value);
            Assert.Equal(pair.Value.Keys.Order(StringComparer.Ordinal), pair.Value.Keys);
            Assert.All(pair.Value, entry =>
            {
                Assert.Contains(entry.Key, templates);
                Assert.True(entry.Value > 0, $"{pair.Key} / {entry.Key}: a count of {entry.Value} is not an entry; remove it.");
            });
        });
        Assert.Equal(raw.Keys.Order(StringComparer.Ordinal), raw.Keys);
    }

    [Fact]
    public void The_Write_Mode_Never_Adds_An_Entry_And_Never_Raises_A_Count()
    {
        // The rule the write mode applies (so the list can only shrink): an offender with no entry, or with more offences than its entry, is refused.
        var allowed = new SortedDictionary<string, SortedDictionary<string, int>> { ["X01-rule"] = new(StringComparer.Ordinal) { ["a.template"] = 2 } };

        Assert.Empty(Refusals(allowed, new Dictionary<string, IReadOnlyDictionary<string, int>> { ["X01-rule"] = new Dictionary<string, int> { ["a.template"] = 2 } }));
        Assert.Empty(Refusals(allowed, new Dictionary<string, IReadOnlyDictionary<string, int>> { ["X01-rule"] = new Dictionary<string, int> { ["a.template"] = 1 } }));
        Assert.Empty(Refusals(allowed, new Dictionary<string, IReadOnlyDictionary<string, int>> { ["X01-rule"] = new Dictionary<string, int>() }));
        Assert.Single(Refusals(allowed, new Dictionary<string, IReadOnlyDictionary<string, int>> { ["X01-rule"] = new Dictionary<string, int> { ["a.template"] = 3 } }));
        Assert.Single(Refusals(allowed, new Dictionary<string, IReadOnlyDictionary<string, int>> { ["X01-rule"] = new Dictionary<string, int> { ["b.template"] = 1 } }));
        Assert.Single(Refusals(allowed, new Dictionary<string, IReadOnlyDictionary<string, int>> { ["X02-rule"] = new Dictionary<string, int> { ["a.template"] = 1 } }));
    }

    [Fact]
    public void Every_Rule_Has_A_Unique_Id_And_Names_An_Audit_Row()
    {
        Assert.Equal(ConsistencyRules.All.Count, ConsistencyRules.All.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ConsistencyRules.All, r =>
        {
            Assert.Matches("^[A-L][0-9]{2}-[a-z0-9-]+$", r.Id);
            Assert.False(string.IsNullOrWhiteSpace(r.Rows));
            Assert.False(string.IsNullOrWhiteSpace(r.Summary));
        });
        Assert.Equal(54, AllCalculators.All.Count);
    }

    [Fact]
    public void The_Rules_That_Every_Template_Already_Meets_Hold_Without_An_Allow_List()
    {
        // These are the renderer-level and catalogue-level rules this phase applied to all 54 templates: they have no entry, so they cannot regress.
        var allowList = LoadAllowList();
        string[] holding = ["E29-output-unit-has-a-precision", "E30-utilisation-two-decimals", "F34-severity-fits-the-condition", "I47-title-form", "I48-category-vocabulary"];
        Assert.All(holding, id => Assert.False(allowList.ContainsKey(id), $"{id} must hold for every template."));
    }

    /// <summary>What the write mode would have to add or raise: an offender with no entry, or with more offences than its entry. Lowering and removing are always allowed.</summary>
    private static List<string> Refusals(IReadOnlyDictionary<string, SortedDictionary<string, int>> allowed, IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> now)
    {
        var refused = new List<string>();

        foreach (var (rule, templates) in now)
        {
            foreach (var (template, count) in templates.Where(p => p.Value > 0))
            {
                if (!allowed.TryGetValue(rule, out var entries) || !entries.TryGetValue(template, out var before))
                    refused.Add($"{rule}: {template} has {count} offence(s) and is not on the allow-list (an entry may not be added)");
                else if (count > before)
                    refused.Add($"{rule}: {template} has {count} offences against {before} on the allow-list (a count may not be raised)");
            }
        }

        return refused;
    }

    private static void WriteFiles()
    {
        var found = Found.Value;
        var before = LoadAllowList();
        var now = found.ToDictionary(p => p.Key, p => (IReadOnlyDictionary<string, int>)p.Value.ToDictionary(t => t.Key, t => t.Value.Count, StringComparer.Ordinal), StringComparer.Ordinal);
        var refused = Refusals(before, now);

        // The write mode only lowers: a new offender or a higher count is a defect to fix, not an entry to write.
        Assert.True(refused.Count == 0, $"{WriteVariable}=1 refuses to add an entry or raise a count; fix these first:{Environment.NewLine}{string.Join(Environment.NewLine, refused)}");

        var allow = new SortedDictionary<string, SortedDictionary<string, int>>(StringComparer.Ordinal);

        foreach (var (rule, offenders) in found.Where(p => p.Value.Count > 0))
            allow[rule] = new SortedDictionary<string, int>(offenders.ToDictionary(p => p.Key, p => p.Value.Count, StringComparer.Ordinal), StringComparer.Ordinal);

        File.WriteAllText(AllowListPath(), JsonSerializer.Serialize(allow, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.WriteAllText(WorkListPath(), WorkList(found));
    }

    private static string WorkList(IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> found)
    {
        var sb = new StringBuilder();
        sb.Append("# Consistency work list\n\n");
        sb.Append("Generated by `ConsistencyContractTests` (`").Append(WriteVariable).Append("=1`); do not edit. It lists, per rule of [the sheet standard](sheet-standard.md), the templates that do not meet it yet and what is wrong in each. `tests/Tempest.Calc.Tests.Calculators/Consistency/ConsistencyAllowList.json` holds the same templates with the number of offences each has today; it only shrinks. Phases B and C work these lists to empty.\n\n");

        var perRule = found.Where(p => p.Value.Count > 0).OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        sb.Append("## Counts\n\n| Rule | Audit row | Templates | Offences | Mechanical | Structural | Building services | Electrical | Fluid power |\n|---|---|---|---|---|---|---|---|---|\n");

        foreach (var (rule, offenders) in perRule)
        {
            var row = ConsistencyRules.All.Single(r => r.Id == rule).Rows;
            int Of(CalcDiscipline d) => offenders.Keys.Count(id => AllCalculators.Get(id).Template.Discipline == d);
            sb.Append($"| `{rule}` | {row} | {offenders.Count} | {offenders.Values.Sum(v => v.Count)} | {Of(CalcDiscipline.Mechanical)} | {Of(CalcDiscipline.Structural)} | {Of(CalcDiscipline.BuildingServices)} | {Of(CalcDiscipline.Electrical)} | {Of(CalcDiscipline.FluidPower)} |\n");
        }

        sb.Append("\n## By template\n\n");
        var byTemplate = perRule.SelectMany(p => p.Value.Keys.Select(id => (id, rule: p.Key))).GroupBy(x => x.id).OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var g in byTemplate)
            sb.Append($"- `{g.Key}` ({g.Count()}): {string.Join(", ", g.Select(x => x.rule).Order(StringComparer.Ordinal))}\n");

        sb.Append("\n## By rule\n");

        foreach (var (rule, offenders) in perRule)
        {
            var def = ConsistencyRules.All.Single(r => r.Id == rule);
            sb.Append($"\n### `{rule}` — audit row {def.Rows}\n\n{def.Summary}\n\n");

            foreach (var (id, offences) in offenders)
                sb.Append($"- `{id}` ({offences.Count}): {string.Join("; ", offences.Take(3).Select(o => o.Replace("\n", " ", StringComparison.Ordinal).Replace("|", "/", StringComparison.Ordinal)))}{(offences.Count > 3 ? $"; … ({offences.Count} in all)" : string.Empty)}\n");
        }

        return sb.ToString();
    }
}
