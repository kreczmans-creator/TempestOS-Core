using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>`v0.25.2` PO request: standard parts list smallest to largest, M3 before M10.</summary>
public sealed class DesignationOrderTests
{
    [Fact]
    public void Designations_SortByTheirNumbers_NotAsText()
    {
        string[] given = ["M10 hex bolt", "M3 hex bolt", "M24 hex bolt", "M5 hex bolt", "M12 hex bolt", "M8 hex bolt", "m4 hex bolt"];

        Assert.Equal(
            ["M3 hex bolt", "m4 hex bolt", "M5 hex bolt", "M8 hex bolt", "M10 hex bolt", "M12 hex bolt", "M24 hex bolt"],
            given.OrderBy(d => d, DesignationOrder.Comparer));
    }
}
