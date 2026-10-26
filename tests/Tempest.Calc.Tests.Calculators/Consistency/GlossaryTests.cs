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

    [GeneratedRegex(@"^\| `(?<symbol>[^`]+)` \| (?<quantity>[^|]*) \| (?<units>[^|]*) \| (?<descriptions>[^|]*) \| (?<templates>[^|]*) \| (?<status>[^|]*) \|$")]
    private static partial Regex SymbolRow();

    private sealed record Row(string Symbol, string Quantity, IReadOnlyList<string> Templates, string Status);

    private static IReadOnlyList<Row> GlossaryRows()
    {
        Assert.True(File.Exists(GlossaryPath), $"{GlossaryPath} is missing; run scripts/generate-glossary.py.");
        var text = File.ReadAllText(GlossaryPath).Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = text.IndexOf("\n## All symbols", StringComparison.Ordinal);
        Assert.True(start >= 0, "glossary.md has no 'All symbols' section.");

        return [.. text[start..].Split('\n').Select(l => SymbolRow().Match(l)).Where(m => m.Success).Select(m => new Row(
            m.Groups["symbol"].Value, m.Groups["quantity"].Value.Trim(),
            [.. m.Groups["templates"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)], m.Groups["status"].Value.Trim()))];
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
                var known = rows.Where(r => r.Symbol == q.Symbol).ToList();

                if (known.Count == 0)
                    problems.Add($"{f.Id}: {q.Symbol} is not in the glossary");
                else if (!known.Any(r => r.Quantity == quantity && r.Templates.Contains(f.Id, StringComparer.Ordinal)))
                    problems.Add($"{f.Id}: {q.Symbol} is {quantity}, which the glossary does not list for this template ({string.Join("; ", known.Select(r => r.Quantity))})");
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
