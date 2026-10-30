using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// C-2 verification F5: a stored check keeps whether it has a utilisation, so the register can print PASS or FAIL alone for a yes/no check (a fit class) instead of "PASS u = 0.00".
/// A calculation saved before the field existed reads as a ratio check.
/// </summary>
public sealed class CheckSnapshotTests
{
    [Fact]
    public async Task A_Saved_Calculation_Keeps_Which_Of_Its_Checks_Have_A_Utilisation_And_Reloads_The_Same()
    {
        var fit = new Iso286FitCalculator();
        var host = new LibraryTestHost(calculators: [fit]);
        var project = await host.ProjectAsync();

        var yesNo = await host.Service.CreateAsync(fit.Template.Id + "@" + fit.Template.Version, project, "Fit", fit.Template.TestCases.Single(t => t.Id == "ka-h7g6-25").Inputs);
        var ratio = await host.Service.CreateAsync("test.direct-stress", project, "Stress", LibraryTestHost.ProvenancedInputs());

        Assert.NotEmpty(yesNo.Checks);
        Assert.All(yesNo.Checks, c => Assert.False(c.HasUtilisation));
        Assert.All(ratio.Checks, c => Assert.True(c.HasUtilisation));

        var reloaded = await host.Store.FindInstanceAsync(yesNo.Id);
        Assert.All(reloaded!.Checks, c => Assert.False(c.HasUtilisation));
    }

    [Fact]
    public void A_Check_Stored_Before_The_Field_Existed_Loads_As_A_Ratio_Check()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<CalcCheckSnapshot>("""{"Name":"Yield","Demand":"0.8","Capacity":"1","Utilisation":0.826,"Passed":true}""")!;
        Assert.True(old.HasUtilisation);
        Assert.Equal("1.01", CalcDisplay.StoredUtilisation(1.004, passed: false));
    }
}
