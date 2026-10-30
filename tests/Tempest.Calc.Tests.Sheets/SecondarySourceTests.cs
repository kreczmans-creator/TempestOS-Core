using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>Addendum B §B4: a known-answer case from a secondary source is marked, hashed only when marked, and counted.</summary>
public sealed class SecondarySourceTests
{
    [Fact]
    public void A_Primary_Source_Is_Not_Secondary_By_Default()
    {
        Assert.False(new CalcReferenceSource("Book", "2nd ed.", "Ex. 1").Secondary);
        Assert.True(new CalcReferenceSource("Solutions manual", "10th ed.", "Prob. 10-3", Secondary: true).Secondary);
    }
}
