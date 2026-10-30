using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>C-2: <c>docs/calcs/sheet-standard.md</c> is held to the code tables and the contract it describes, so the page cannot drift from what is enforced.</summary>
public sealed class SheetStandardDocTests
{
    private static string Text => File.ReadAllText(Path.Combine(WorkedExamples.RepositoryRoot, "docs", "calcs", "sheet-standard.md")).Replace("\r\n", "\n", StringComparison.Ordinal);

    [Fact]
    public void The_Standard_Names_Every_Contract_Rule()
    {
        var text = Text;
        Assert.All(ConsistencyRules.All, rule => Assert.True(text.Contains($"`{rule.Id}`", StringComparison.Ordinal), $"sheet-standard.md does not mention the rule {rule.Id}."));
    }

    [Fact]
    public void The_Standard_Has_One_Section_For_Each_Category_Of_The_Audit_With_A_Rule_An_Example_And_Its_Enforcement()
    {
        var text = Text;

        foreach (var category in "ABCDEFGHIJKL")
        {
            var start = text.IndexOf($"\n## {category}. ", StringComparison.Ordinal);
            Assert.True(start >= 0, $"sheet-standard.md has no section {category}.");
            var end = text.IndexOf("\n## ", start + 5, StringComparison.Ordinal);
            var section = end < 0 ? text[start..] : text[start..end];
            Assert.Contains("**Rule.**", section, StringComparison.Ordinal);
            Assert.Contains("**Good example", section, StringComparison.Ordinal);
            Assert.Contains("**Enforced by**", section, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_Standard_Lists_The_Headings_The_Port_Areas_The_Categories_And_The_Decisions()
    {
        var text = Text;
        Assert.All(CalcSheetStandard.Headings, h => Assert.Contains(h, text, StringComparison.Ordinal));
        Assert.All(CalcPortNames.Areas, pair => Assert.True(text.Contains($"`{pair.Key}`", StringComparison.Ordinal) && pair.Value.All(a => text.Contains($"`{a}`", StringComparison.Ordinal)), $"sheet-standard.md does not give {pair.Key} its area."));
        Assert.All(CalcCatalogueStandard.Categories, pair => Assert.All(pair.Value, c => Assert.Contains(c, text, StringComparison.Ordinal)));

        foreach (var n in Enumerable.Range(1, 39))
            Assert.Contains($"| C2-{n:00} |", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Standard_Names_The_Public_Name_Of_Every_Port_Of_The_Nine_Calculators_The_Audit_Maps()
    {
        var text = Text;
        string[] ids = ["mech.bearing-life", "mech.shaft", "mech.weld-group", "struct.connection-ec3", "struct.load-combinations", "elec.load-budget", "elec.fault-protection", "bs.heat-exchanger", "bs.sealed-system"];

        foreach (var id in ids)
        {
            var ports = AllCalculators.Get(id).Template.OutputPorts!;
            Assert.All(ports, p =>
            {
                Assert.NotNull(p.Name);
                Assert.Null(CalcPortNames.Problem(id, p.Name));
                Assert.Contains($"`{p.Symbol}` → `{p.Name}`", text, StringComparison.Ordinal);
            });
        }
    }

    [Fact]
    public void The_Glossary_Points_Back_To_The_Standard_And_The_Standard_To_The_Glossary()
    {
        Assert.Contains("glossary.md", Text, StringComparison.Ordinal);
        Assert.Contains("sheet-standard.md", File.ReadAllText(Path.Combine(WorkedExamples.RepositoryRoot, "docs", "calcs", "glossary.md")), StringComparison.Ordinal);
    }
}
