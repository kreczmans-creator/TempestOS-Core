using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>A refused pack template leaves nothing behind: the chain is run in memory before anything is stored.</summary>
public sealed class PackDryRunTests
{
    private static LibraryTestHost Host() => new(calculators: [.. ElectricalCalculators.All], packTemplates: CalcPackTemplates.All);

    [Fact]
    public async Task A_Pack_Refused_At_A_Blocked_Step_Creates_No_Calculations()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MotorCircuit, project, "Conveyor motor circuit");

        Assert.Null(made);
        Assert.StartsWith("Step 'e4' is blocked", refusal);
        Assert.Empty(await host.Store.ListInstancesAsync(project));
        Assert.Empty(await host.Store.ListPacksAsync(project));
    }

    [Fact]
    public async Task The_Dry_Run_Agrees_With_The_Stored_Chain()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var tables = new Dictionary<string, IReadOnlyList<CalcInputValue>>(StringComparer.Ordinal)
        {
            ["e4"] = [ConductorSizingCalculator.TypicalRatings(), ConductorSizingCalculator.SyntheticAmpacity()],
        };

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.MotorCircuit, project, "Conveyor motor circuit", tables);

        Assert.Null(refusal);
        Assert.Equal(6, (await host.Store.ListInstancesAsync(project)).Count);
        Assert.All(made!.Instances.Values, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task A_Pack_Refused_Further_Down_The_Chain_Stores_Nothing_Either()
    {
        var host = new LibraryTestHost(calculators: [.. Tempest.Calc.BuildingServices.BuildingServicesCalculators.All, .. ElectricalCalculators.All], packTemplates: CalcPackTemplates.All);
        var project = await host.ProjectAsync();
        var noRooms = new Dictionary<string, IReadOnlyList<CalcInputValue>>(StringComparer.Ordinal) { ["b6"] = [] };

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(CalcPackTemplates.WetHeating, project, "Heating", noRooms);

        Assert.Null(made);
        Assert.StartsWith("Step '", refusal);
        Assert.Empty(await host.Store.ListInstancesAsync(project));
    }
}
