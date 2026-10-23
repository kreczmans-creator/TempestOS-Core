using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>ADR-0167 §4: the legacy surface's Compare, reimplemented over calc instances of one template.</summary>
public sealed class CompareTests
{
    private static async Task<(LibraryTestHost Host, CalcInstance Issued, CalcInstance Revision)> RevisedAsync()
    {
        var host = new LibraryTestHost();
        var project = await host.ProjectAsync();
        var instance = await host.Service.CreateAsync("test.direct-stress", project, "CMP-1", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(instance.Id, "ISS-C1", "Client Co")).Succeeded);
        var revised = await host.Service.ReviseAsync(instance.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        return (host, instance, revised.Instance!);
    }

    [Fact]
    public async Task A_Revision_Compares_With_The_Revision_It_Superseded()
    {
        var (host, _, revision) = await RevisedAsync();
        Assert.True((await host.Service.UpdateInputsAsync(revision.Id, LibraryTestHost.ProvenancedInputs(f: 16))).Succeeded);

        var (comparison, reason) = await host.Service.CompareWithPredecessorAsync(revision.Id);
        Assert.Null(reason);
        Assert.True(comparison!.HasChanges);
        Assert.Equal("A", comparison.Before.Revision);
        Assert.Equal("B", comparison.After.Revision);

        var force = Assert.Single(comparison.Rows, r => r.Kind == CalcDifferenceKind.Input);
        Assert.Equal(("F", "12 kN", "16 kN"), (force.Symbol, force.Before, force.After));
        Assert.Equal(1.0 / 3, force.Change!.Value, 9);

        var stress = Assert.Single(comparison.Rows, r => r.Kind == CalcDifferenceKind.Output && r.Symbol == "sigma");
        Assert.Equal(("60 MPa", "80 MPa"), (stress.Before, stress.After));
        Assert.Equal(1.0 / 3, stress.Change!.Value, 9);
        Assert.Contains(comparison.Rows, r => r.Kind == CalcDifferenceKind.Check);
        Assert.Contains("F: 12 kN → 16 kN  (+33.3 %)", comparison.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Unchanged_Revision_Reports_Nothing_Differs()
    {
        var (host, _, revision) = await RevisedAsync();
        var (comparison, _) = await host.Service.CompareWithPredecessorAsync(revision.Id);
        Assert.False(comparison!.HasChanges);
        Assert.EndsWith("Nothing differs.\n", comparison.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_First_Revision_Or_A_Missing_Calculation_Is_Refused_With_A_Reason()
    {
        var (host, issued, _) = await RevisedAsync();
        var (none, reason) = await host.Service.CompareWithPredecessorAsync(issued.Id);
        Assert.Null(none);
        Assert.Contains("no earlier revision", reason, StringComparison.Ordinal);
        Assert.Contains("exists", (await host.Service.CompareAsync(issued.Id, Guid.NewGuid())).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Different_Templates_And_Legacy_Archives_Are_Not_Compared()
    {
        var (_, issued, revision) = await RevisedAsync();
        Assert.Throws<ArgumentException>(() => CalcInstanceComparer.Compare(issued, revision with { TemplateReference = "test.other@1.0.0" }));
        Assert.Throws<ArgumentException>(() => CalcInstanceComparer.Compare(issued with { TemplateReference = CalcInstance.LegacyReferencePrefix + "calc.x@1" }, revision));
    }

    [Fact]
    public async Task Table_Inputs_Compare_Row_By_Row_And_Long_Comparisons_Are_Capped()
    {
        var (_, issued, revision) = await RevisedAsync();
        static CalcInputValue Rows(int n, double first) => CalcInputValue.Table("rows", [.. Enumerable.Range(0, n).Select(i => CalcRow.Of(("x", new CalcCell(i == 0 ? first : i, "mm"))))]);

        var small = CalcInstanceComparer.Compare(issued with { Inputs = [Rows(2, 0)] }, revision with { Inputs = [Rows(3, 5)] });
        Assert.Equal(["rows[1]", "rows[3]"], small.Rows.Where(r => r.Kind == CalcDifferenceKind.Input).Select(r => r.Symbol));
        Assert.Equal(("x = 0 mm", "x = 5 mm"), (small.Rows[0].Before, small.Rows[0].After));
        Assert.Equal("—", small.Rows[1].Before);

        var large = CalcInstanceComparer.Compare(issued with { Inputs = [] }, revision with { Inputs = [Rows(CalcInstanceComparer.MaxRows + 20, 0)] });
        Assert.Equal(CalcInstanceComparer.MaxRows, large.Rows.Count);
        Assert.True(large.Omitted >= 20);
        Assert.Contains("more difference(s)", large.Text, StringComparison.Ordinal);
    }
}
