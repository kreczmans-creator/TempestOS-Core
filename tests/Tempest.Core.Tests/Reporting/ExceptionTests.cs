using Tempest.Core.Reporting;

namespace Tempest.Core.Tests.Reporting;

public class ExceptionTests
{
    [Fact]
    public void ReportingException_MessageConstructor_SetsMessage()
    {
        var exception = new ReportingException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void DuplicateReportDefinitionException_IsAReportingException()
    {
        var exception = new DuplicateReportDefinitionException("sample.report");

        Assert.IsAssignableFrom<ReportingException>(exception);
        Assert.Equal("sample.report", exception.DefinitionId);
        Assert.Contains("sample.report", exception.Message);
    }

    [Fact]
    public void ReportDefinitionNotFoundException_IsAReportingException()
    {
        var exception = new ReportDefinitionNotFoundException("sample.report");

        Assert.IsAssignableFrom<ReportingException>(exception);
        Assert.Equal("sample.report", exception.DefinitionId);
        Assert.Contains("sample.report", exception.Message);
    }
}
