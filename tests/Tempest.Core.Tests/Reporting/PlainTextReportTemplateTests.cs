using System.Text;
using Tempest.Core.Reporting;

namespace Tempest.Core.Tests.Reporting;

public class PlainTextReportTemplateTests
{
    [Fact]
    public void ContentType_IsTextPlain() =>
        Assert.Equal("text/plain", new PlainTextReportTemplate<RecordedReportDefinitionA>().ContentType);

    [Fact]
    public void Apply_IncludesTheDefinitionsOwnName()
    {
        var template = new PlainTextReportTemplate<RecordedReportDefinitionA>();
        var definition = new RecordedReportDefinitionA();

        var result = template.Apply(definition, new ReportRequest(new Dictionary<string, string>()), new Dictionary<string, string>());

        var text = Encoding.UTF8.GetString(result.Content);
        Assert.Contains(definition.Name, text);
    }

    [Fact]
    public void Apply_IncludesEveryDataKeyAndValue()
    {
        var template = new PlainTextReportTemplate<RecordedReportDefinitionA>();
        var data = new Dictionary<string, string> { ["Greeting"] = "Hello", ["Count"] = "3" };

        var result = template.Apply(new RecordedReportDefinitionA(), new ReportRequest(new Dictionary<string, string>()), data);

        var text = Encoding.UTF8.GetString(result.Content);
        Assert.Contains("Greeting: Hello", text);
        Assert.Contains("Count: 3", text);
    }

    [Fact]
    public void Apply_ReturnsTheTemplatesOwnContentType()
    {
        var template = new PlainTextReportTemplate<RecordedReportDefinitionA>();

        var result = template.Apply(new RecordedReportDefinitionA(), new ReportRequest(new Dictionary<string, string>()), new Dictionary<string, string>());

        Assert.Equal(template.ContentType, result.ContentType);
    }

    [Fact]
    public void Apply_NullDefinition_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new PlainTextReportTemplate<RecordedReportDefinitionA>().Apply(null!, new ReportRequest(new Dictionary<string, string>()), new Dictionary<string, string>()));

    [Fact]
    public void Apply_NullRequest_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new PlainTextReportTemplate<RecordedReportDefinitionA>().Apply(new RecordedReportDefinitionA(), null!, new Dictionary<string, string>()));

    [Fact]
    public void Apply_NullData_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() =>
            new PlainTextReportTemplate<RecordedReportDefinitionA>().Apply(new RecordedReportDefinitionA(), new ReportRequest(new Dictionary<string, string>()), null!));
}
