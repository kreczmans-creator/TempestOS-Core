using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2: the catalogue-wide contract of <c>docs/calcs/sheet-standard.md</c>. Each rule (<see cref="ConsistencyRules"/>) is run over all 54 templates.
/// A template that does not meet a rule yet is named in <c>ConsistencyAllowList.json</c> (one entry per template and rule); the allow-list only
/// shrinks. The test fails when a template breaks a rule and is not on the list (a new offender), and when a template is on the list and now
/// meets the rule (remove the entry). Phases B and C empty the list; nothing may be added to it.
///
/// To rewrite the list and the work list after fixing templates, run
/// <c>TEMPEST_WRITE_CONSISTENCY_ALLOWLIST=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter ConsistencyContractTests</c>
/// and commit <c>ConsistencyAllowList.json</c> and <c>docs/calcs/consistency-work-list.md</c>. A reviewer rejects a change that adds an entry.
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

    private static SortedDictionary<string, SortedSet<string>> LoadAllowList()
    {
        var path = AllowListPath();
        var raw = File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(path)) ?? [] : [];
        return new(raw.ToDictionary(p => p.Key, p => new SortedSet<string>(p.Value, StringComparer.Ordinal), StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void Every_Template_Meets_The_Rule_Or_Is_On_The_Allow_List_And_The_List_Names_No_Template_That_Now_Meets_It(string ruleId)
    {
        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
        {
            WriteFiles();
            return;
        }

        var rule = ConsistencyRules.All.Single(r => r.Id == ruleId);
        var offenders = Found.Value[ruleId];
        var allowed = LoadAllowList().GetValueOrDefault(ruleId) ?? [];

        var newOffenders = offenders.Keys.Where(id => !allowed.Contains(id)).Select(id => $"  {id}: {offenders[id][0]}").ToList();
        var nowConform = allowed.Where(id => !offenders.ContainsKey(id)).ToList();

        Assert.True(newOffenders.Count == 0,
            $"{rule.Id} (audit row {rule.Rows}: {rule.Summary}) is broken by templates not on the allow-list; fix them (the list may not grow):{Environment.NewLine}{string.Join(Environment.NewLine, newOffenders)}");
        Assert.True(nowConform.Count == 0,
            $"{rule.Id}: {string.Join(", ", nowConform)} now meet the rule; remove them from ConsistencyAllowList.json ({WriteVariable}=1 rewrites it).");
    }

    [Fact]
    public void The_Allow_List_Names_Only_Known_Rules_And_Templates_In_Sorted_Order()
    {
        var path = AllowListPath();
        Assert.True(File.Exists(path), $"{path} is missing; run with {WriteVariable}=1 to create it.");
        var raw = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(path))!;
        var rules = ConsistencyRules.All.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var templates = AllCalculators.All.Select(c => c.Template.Id).ToHashSet(StringComparer.Ordinal);

        Assert.All(raw, pair =>
        {
            Assert.Contains(pair.Key, rules);
            Assert.NotEmpty(pair.Value);
            Assert.Equal(pair.Value.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), pair.Value);
            Assert.All(pair.Value, id => Assert.Contains(id, templates));
        });
        Assert.Equal(raw.Keys.Order(StringComparer.Ordinal), raw.Keys);
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

    private static void WriteFiles()
    {
        var found = Found.Value;
        var allow = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var (rule, offenders) in found.Where(p => p.Value.Count > 0))
            allow[rule] = [.. offenders.Keys];

        File.WriteAllText(AllowListPath(), JsonSerializer.Serialize(allow, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.WriteAllText(WorkListPath(), WorkList(found));
    }

    private static string WorkList(IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> found)
    {
        var sb = new StringBuilder();
        sb.Append("# Consistency work list\n\n");
        sb.Append("Generated by `ConsistencyContractTests` (`").Append(WriteVariable).Append("=1`); do not edit. It lists, per rule of [the sheet standard](sheet-standard.md), the templates that do not meet it yet and what is wrong in each. `tests/Tempest.Calc.Tests.Calculators/Consistency/ConsistencyAllowList.json` holds the same templates; it only shrinks. Phases B and C work these lists to empty.\n\n");

        var perRule = found.Where(p => p.Value.Count > 0).OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        sb.Append("## Counts\n\n| Rule | Audit row | Templates | Mechanical | Structural | Building services | Electrical | Fluid power |\n|---|---|---|---|---|---|---|---|\n");

        foreach (var (rule, offenders) in perRule)
        {
            var row = ConsistencyRules.All.Single(r => r.Id == rule).Rows;
            int Of(CalcDiscipline d) => offenders.Keys.Count(id => AllCalculators.Get(id).Template.Discipline == d);
            sb.Append($"| `{rule}` | {row} | {offenders.Count} | {Of(CalcDiscipline.Mechanical)} | {Of(CalcDiscipline.Structural)} | {Of(CalcDiscipline.BuildingServices)} | {Of(CalcDiscipline.Electrical)} | {Of(CalcDiscipline.FluidPower)} |\n");
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
                sb.Append($"- `{id}`: {string.Join("; ", offences.Take(3).Select(o => o.Replace("\n", " ", StringComparison.Ordinal).Replace("|", "/", StringComparison.Ordinal)))}{(offences.Count > 3 ? $"; … ({offences.Count} in all)" : string.Empty)}\n");
        }

        return sb.ToString();
    }
}
