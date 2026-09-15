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

    // ----------------------------------------------------------------
    // TD-22 / TD-29
    // ----------------------------------------------------------------

    [Fact]
    public void CalculationReadbackException_IsACalculationException_AndNamesKeyAndBothTypes()
    {
        var exception = new CalculationReadbackException("step-1", typeof(int), typeof(string).FullName!);

        Assert.IsAssignableFrom<CalculationException>(exception);
        Assert.Equal("step-1", exception.Key);
        Assert.Equal(typeof(int).FullName, exception.RequestedTypeName);
        Assert.Equal(typeof(string).FullName, exception.ActualTypeName);
        Assert.Contains("step-1", exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(int).FullName!, exception.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(string).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculationBoundExceededException_IsACalculationException_AndNamesTheDefinitionAndKind()
    {
        var exception = new CalculationBoundExceededException("calc.x", CalculationBoundKind.IntermediateResultCount, 200);

        Assert.IsAssignableFrom<CalculationException>(exception);
        Assert.Equal("calc.x", exception.CalculationId);
        Assert.Equal(CalculationBoundKind.IntermediateResultCount, exception.Kind);
        Assert.Equal(200, exception.Limit);
        Assert.Contains("calc.x", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculationRecordHasNoInputException_IsACalculationException_AndNamesTheRecord()
    {
        var recordId = Guid.NewGuid();
        var exception = new CalculationRecordHasNoInputException(recordId);

        Assert.IsAssignableFrom<CalculationException>(exception);
        Assert.Equal(recordId, exception.RecordId);
        Assert.Contains(recordId.ToString(), exception.Message, StringComparison.Ordinal);
    }
}
