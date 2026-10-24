using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Tests.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>The direct-stress test calculator with an independent check demanded (Addendum A, §A3; DA-12).</summary>
internal sealed class IndependentStressCalculator : ICalcCalculator
{
    private readonly DirectStressCalculator _inner = new();

    public IndependentStressCalculator() => Template = _inner.Template with { Id = "test.independent", IndependentCheck = true };

    public CalcTemplate Template { get; }

    public void Run(CalcRunContext c) => _inner.Run(c);
}

public sealed class IndependentCheckTests
{
    [Fact]
    public async Task The_Author_Cannot_Check_A_Calculation_That_Demands_An_Independent_Check()
    {
        var host = new LibraryTestHost(calculators: [new IndependentStressCalculator()]);
        var instance = await host.Service.CreateAsync("test.independent", await host.ProjectAsync(), "E5 circuit 1", LibraryTestHost.ProvenancedInputs());

        var refused = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("needs an independent check", refused.Reason, StringComparison.Ordinal);

        // The same calculation authored by someone else is checked as usual.
        await host.Store.SaveInstanceAsync(instance with { CreatedBy = "another.engineer" }, instance.UpdatedAt);
        var check = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
    }

    [Fact]
    public async Task A_Pack_Holding_Such_A_Calculation_Demands_It_Too()
    {
        var host = new LibraryTestHost(calculators: [new IndependentStressCalculator()]);
        var project = await host.ProjectAsync();
        var plain = await host.Service.CreateAsync("test.direct-stress", project, "Plain", LibraryTestHost.ProvenancedInputs());
        var strict = await host.Service.CreateAsync("test.independent", project, "Strict", LibraryTestHost.ProvenancedInputs());
        var pack = await host.Service.CreatePackAsync(project, "Circuit pack", [plain.Id, strict.Id]);

        var refused = await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(refused.Succeeded);
        Assert.Contains("'Strict' needs an independent check", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Flag_Changes_The_Template_Hash()
    {
        Assert.NotEqual(CalcTemplateHasher.Hash(new DirectStressCalculator()), CalcTemplateHasher.Hash(new IndependentStressCalculator()));
    }
}
