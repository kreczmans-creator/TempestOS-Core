using System.Text.RegularExpressions;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2, rows 10 to 19 and 29 to 30: <c>docs/calcs/glossary.md</c> is the one glossary of terms and symbols. Its data file is written from the live
/// catalogue (<c>TEMPEST_WRITE_GLOSSARY=1</c>) and <c>scripts/generate-glossary.py</c> turns it into the document. A template that adds a symbol the
/// glossary lacks, or uses a glossary symbol for another quantity type, fails here until the glossary is regenerated and committed in the same commit.
/// A symbol that means more than one thing today is listed in the document, marked <c>RESOLVE</c>, for phases B and C.
/// </summary>
public sealed partial class GlossaryTests
{
    public const string WriteVariable = "TEMPEST_WRITE_GLOSSARY";

    private static readonly Lazy<IReadOnlyList<TemplateFacts>> Facts = new(() => [.. AllCalculators.All.Select(c => new TemplateFacts(c))]);

    private static string CalcsFolder => Path.Combine(WorkedExamples.RepositoryRoot, "docs", "calcs");

    private static string DataPath => Path.Combine(CalcsFolder, "glossary-data.json");

    private static string GlossaryPath => Path.Combine(CalcsFolder, "glossary.md");

    [GeneratedRegex(@"^\| `(?<symbol>[^`]+)` \| (?<meaning>[^|]*) \| (?<quantity>[^|]*) \| (?<units>[^|]*) \| (?<descriptions>[^|]*) \| (?<templates>[^|]*) \| (?<status>[^|]*) \|$")]
    private static partial Regex SymbolRow();

    private sealed record Row(string Symbol, string Meaning, string Quantity, IReadOnlyList<string> Templates, string Status);

    /// <summary>The meaning id of a glossary cell: <c>`eta:efficiency`</c> is "efficiency"; a dash is no declared meaning.</summary>
    private static string MeaningId(string symbol, string cell)
    {
        var text = cell.Trim().Trim('`');
        return text == "—" ? string.Empty : text.StartsWith(symbol + ":", StringComparison.Ordinal) ? text[(symbol.Length + 1)..] : text;
    }

    private static IReadOnlyList<Row> GlossaryRows()
    {
        Assert.True(File.Exists(GlossaryPath), $"{GlossaryPath} is missing; run scripts/generate-glossary.py.");
        var text = File.ReadAllText(GlossaryPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = text.IndexOf("\n## All symbols", StringComparison.Ordinal);
        Assert.True(start >= 0, "glossary.md has no 'All symbols' section.");

        return [.. text[start..].Split('\n').Select(l => SymbolRow().Match(l)).Where(m => m.Success).Select(m => new Row(
            m.Groups["symbol"].Value, MeaningId(m.Groups["symbol"].Value, m.Groups["meaning"].Value), m.Groups["quantity"].Value.Trim(),
            [.. m.Groups["templates"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)], m.Groups["status"].Value.Trim()))];
    }

    /// <summary>
    /// C-2 verification F8: the glossary called the member check's <c>rho</c> a "density ratio". It is the shear reduction factor of EN 1993-1-1 clause 6.2.8 (0 up to half the
    /// plastic shear resistance, (2V_Ed/V_pl,Rd - 1)^2 above it). And <c>S_e</c> was one meaning for the bending endurance limit of a shaft or a fatigue curve and the shear
    /// endurance strength of a spring wire at N_c cycles, which are different quantities with different values.
    /// </summary>
    [Fact]
    public void The_Shear_Reduction_Factor_Is_Not_A_Density_Ratio_And_The_Endurance_Of_A_Spring_Is_Not_The_Bending_Endurance_Limit()
    {
        var rows = GlossaryRows();

        var rho = Assert.Single(rows, r => r.Symbol == "rho" && r.Templates.Contains("struct.en1993-member"));
        Assert.Equal("shear-reduction-factor", rho.Meaning);
        Assert.Equal(["struct.en1993-member"], rho.Templates);
        Assert.DoesNotContain(rows, r => r.Meaning.Contains("density-ratio", StringComparison.Ordinal));

        var bending = Assert.Single(rows, r => r.Symbol == "S_e" && r.Meaning == "bending-endurance-limit");
        Assert.Equal(["mech.fatigue-miner", "mech.shaft"], bending.Templates);
        var spring = Assert.Single(rows, r => r.Symbol == "S_e" && r.Meaning == "spring-shear-endurance-strength");
        Assert.Equal(["mech.spring"], spring.Templates);
        Assert.Equal("RESOLVE (2 meanings)", spring.Status);

        // The description the sheet prints for the output says what it is, and the glossary row quotes it.
        var member = Facts.Value.Single(f => f.Id == "struct.en1993-member");
        var run = member.CaseRuns.Prepend(member.DefaultRun).First(r => r.FindOutput("rho") is not null);
        Assert.Equal("Shear reduction factor ρ", run.FindOutput("rho")!.Description);
        var text = File.ReadAllText(GlossaryPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("| Plain number | — | Shear reduction factor ρ | struct.en1993-member |", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Glossary_Data_File_Matches_The_Catalogue()
    {
        var live = GlossaryData.ToJson(GlossaryData.Build(Facts.Value));

        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
            File.WriteAllText(DataPath, live);

        Assert.True(File.Exists(DataPath), $"{DataPath} is missing; run with {WriteVariable}=1.");
        Assert.True(string.Equals(File.ReadAllText(DataPath).Replace("\r\n", "\n", StringComparison.Ordinal), live, StringComparison.Ordinal),
            $"docs/calcs/glossary-data.json does not match the catalogue: a template added, removed or changed a symbol. Run with {WriteVariable}=1, then scripts/generate-glossary.py, and commit both.");
    }

    [Fact]
    public void Every_Symbol_Of_Every_Template_Is_In_The_Glossary_With_The_Quantity_Type_The_Template_Gives_It()
    {
        var rows = GlossaryRows();
        var problems = new List<string>();

        foreach (var f in Facts.Value)
        {
            foreach (var q in f.QuantityFacts.Where(q => q.Origin != "port").DistinctBy(q => q.Symbol + "|" + GlossaryData.QuantityName(q.Dimension, q.Unit)))
            {
                var quantity = GlossaryData.QuantityName(q.Dimension, q.Unit);
                var meaning = GlossaryData.MeaningOf(GlossaryData.Meanings.Value, q.Symbol, f.Id, q.Origin);
                var known = rows.Where(r => r.Symbol == q.Symbol).ToList();

                if (known.Count == 0)
                    problems.Add($"{f.Id}: {q.Symbol} is not in the glossary");
                else if (!known.Any(r => r.Quantity == quantity && r.Meaning == meaning && r.Templates.Contains(f.Id, StringComparer.Ordinal)))
                    problems.Add($"{f.Id}: {q.Symbol} is {quantity} ({(meaning.Length == 0 ? "no meaning declared" : meaning)}), which the glossary does not list for this template ({string.Join("; ", known.Select(r => r.Meaning + " " + r.Quantity))})");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(30)) + (problems.Count > 30 ? $"{Environment.NewLine}… {problems.Count} in all" : string.Empty));
    }

    [Fact]
    public void The_Glossary_Lists_No_Symbol_A_Template_No_Longer_Uses()
    {
        var live = Facts.Value.SelectMany(f => f.QuantityFacts.Where(q => q.Origin != "port").Select(q => (f.Id, q.Symbol, Quantity: GlossaryData.QuantityName(q.Dimension, q.Unit)))).ToHashSet();
        var stale = GlossaryRows().SelectMany(r => r.Templates.Select(t => (t, r.Symbol, r.Quantity))).Where(x => !live.Contains((x.t, x.Symbol, x.Quantity))).Select(x => $"{x.t}: {x.Symbol} ({x.Quantity})").ToList();

        Assert.True(stale.Count == 0, string.Join(Environment.NewLine, stale.Take(30)));
    }

    [Fact]
    public void A_Symbol_With_Several_Meanings_Is_Marked_For_Resolution_And_One_With_A_Single_Meaning_Is_Not()
    {
        var rows = GlossaryRows();

        foreach (var group in rows.GroupBy(r => r.Symbol))
        {
            var several = group.Count() > 1;
            Assert.All(group, r => Assert.Equal(several, r.Status.StartsWith("RESOLVE", StringComparison.Ordinal)));
        }

        Assert.Contains(rows, r => r.Symbol == "T" && r.Status.StartsWith("RESOLVE", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.Symbol == "theta" && r.Status.StartsWith("RESOLVE", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_Use_Of_A_Symbol_That_Two_Templates_Share_Declares_Its_Meaning_And_Two_Meanings_Of_One_Quantity_Type_Are_Told_Apart()
    {
        var declared = GlossaryData.Meanings.Value;
        var problems = new List<string>();
        var uses = Facts.Value.SelectMany(f => f.QuantityFacts.Where(q => q.Origin != "port").Select(q => (q.Symbol, Template: f.Id, q.Origin))).Distinct().ToList();
        var users = uses.GroupBy(x => x.Symbol).ToDictionary(g => g.Key, g => g.Select(x => x.Template).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());

        foreach (var (symbol, templates) in users.Where(p => p.Value.Count > 1))
        {
            if (!declared.TryGetValue(symbol, out var meanings))
            {
                problems.Add($"{symbol} is used by {templates.Count} templates and has no declared meaning (GlossaryMeanings.json)");
                continue;
            }

            foreach (var (_, template, origin) in uses.Where(u => u.Symbol == symbol))
            {
                var count = GlossaryData.Declarations(meanings, template, origin);

                if (count != 1)
                    problems.Add($"{symbol} in {template} ({origin}) is declared {count} times (it must be declared exactly once)");
            }

            foreach (var listed in meanings.SelectMany(m => m.Value).Where(x => !uses.Any(u => u.Symbol == symbol && (x == u.Template || x == $"{u.Template}#{u.Origin}"))))
                problems.Add($"{symbol} lists {listed}, which does not use it");
        }

        foreach (var (symbol, meanings) in declared)
        {
            if (!users.TryGetValue(symbol, out var templates) || templates.Count < 2)
                problems.Add($"{symbol} is declared and fewer than two templates use it");

            foreach (var id in meanings.Keys.Where(id => !MeaningIdForm().IsMatch(id)))
                problems.Add($"{symbol}: the meaning id '{id}' is not lower-case words joined by '-'");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Take(30)) + (problems.Count > 30 ? $"{Environment.NewLine}… {problems.Count} in all" : string.Empty));
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex MeaningIdForm();

    [Fact]
    public void Eta_And_N_Are_Listed_With_Their_Separate_Meanings_Even_Where_They_Share_A_Quantity_Type()
    {
        var rows = GlossaryRows();

        // eta: three meanings, all plain numbers, which the glossary used to merge into one line.
        Assert.Equal(["efficiency", "perry-factor", "shear-area-factor"], rows.Where(r => r.Symbol == "eta").Select(r => r.Meaning).Order(StringComparer.Ordinal));
        Assert.All(rows.Where(r => r.Symbol == "eta"), r => Assert.Equal("Plain number", r.Quantity));
        Assert.Contains(rows, r => r.Symbol == "eta" && r.Meaning == "perry-factor" && r.Templates.SequenceEqual(["struct.column-buckling"]));
        Assert.Contains(rows, r => r.Symbol == "eta" && r.Meaning == "shear-area-factor" && r.Templates.SequenceEqual(["struct.en1993-member"]));

        // n: the plain-number uses are separate meanings (the standard retires n for a count by renaming, so the number falls), and the speed uses are separate again.
        var n = rows.Where(r => r.Symbol == "n").ToList();
        Assert.True(n.Count(r => r.Quantity == "Plain number") >= 2);
        Assert.Contains(n, r => r.Quantity != "Plain number");
        Assert.Equal(n.Count, n.Select(r => r.Meaning).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(n, r => r.Meaning == "gas-exponent" && r.Templates.SequenceEqual(["fp.accumulator"]));
        Assert.All(n, r => Assert.StartsWith("RESOLVE", r.Status, StringComparison.Ordinal));
    }

    [Fact]
    public void The_Quantity_Type_Table_States_The_Units_And_Decimals_Of_The_Code()
    {
        Assert.True(File.Exists(GlossaryPath));
        var text = File.ReadAllText(GlossaryPath).Replace("\r\n", "\n", StringComparison.Ordinal);

        foreach (var rule in CalcDisplay.Rules)
        {
            var row = text.Split('\n').FirstOrDefault(l => l.StartsWith($"| {rule.Quantity} |", StringComparison.Ordinal));
            Assert.True(row is not null, $"glossary.md has no row for the quantity type '{rule.Quantity}'.");
            Assert.True(row!.Contains($"{rule.Unit} {rule.Decimals}", StringComparison.Ordinal), $"glossary.md does not give {rule.Unit} {rule.Decimals} decimals for '{rule.Quantity}'.");
        }

        foreach (var quantity in CalcUnitStandard.Quantities)
        {
            var row = text.Split('\n').FirstOrDefault(l => l.StartsWith($"| {quantity.Name} |", StringComparison.Ordinal))
                ?? text.Split('\n').FirstOrDefault(l => quantity.Units.Any(u => l.Contains($"| {u}", StringComparison.Ordinal)) && l.StartsWith("| ", StringComparison.Ordinal));
            Assert.True(row is not null, $"glossary.md does not state the standard unit of '{quantity.Name}'.");
        }

        foreach (var reserved in CalcSymbolStandard.Symbols)
            Assert.Contains($"| `{reserved.Symbol}` | {reserved.Meaning} | {reserved.Row} |", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Glossary_Covers_All_54_Templates()
    {
        var text = File.ReadAllText(GlossaryPath);
        Assert.Equal(54, AllCalculators.All.Count);
        Assert.Contains("in 54 templates", text, StringComparison.Ordinal);
        Assert.All(AllCalculators.All, c => Assert.Contains(c.Template.Id, text, StringComparison.Ordinal));
    }
}
