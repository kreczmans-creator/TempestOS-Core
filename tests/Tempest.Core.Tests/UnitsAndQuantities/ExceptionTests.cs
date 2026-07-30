using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.UnitsAndQuantities;

public class ExceptionTests
{
    [Fact]
    public void IncompatibleUnitsException_MessageConstructor_SetsMessage()
    {
        var exception = new IncompatibleUnitsException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }
}
