using Tempest.Core.Calculations;

namespace Tempest.Core.Tests.Calculations;

public class ExceptionTests
{
    [Fact]
    public void CalculationException_MessageConstructor_SetsMessage()
    {
        var exception = new CalculationException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void DuplicateCalculationException_IsACalculationException()
    {
        var exception = new DuplicateCalculationException("test.calc");

        Assert.IsAssignableFrom<CalculationException>(exception);
        Assert.Equal("test.calc", exception.CalculationId);
        Assert.Contains("test.calc", exception.Message);
    }

    [Fact]
    public void CalculationDefinitionNotFoundException_IsACalculationException()
    {
        var exception = new CalculationDefinitionNotFoundException("test.calc");

        Assert.IsAssignableFrom<CalculationException>(exception);
        Assert.Equal("test.calc", exception.CalculationId);
        Assert.Contains("test.calc", exception.Message);
    }

    [Fact]
    public void CalculationInputInvalidException_IsACalculationException()
    {
        var exception = new CalculationInputInvalidException("bad input");

        Assert.IsAssignableFrom<CalculationException>(exception);
        Assert.Equal("bad input", exception.Message);
    }
}
