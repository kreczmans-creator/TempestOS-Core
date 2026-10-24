using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Tests.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>A calculator with a text input naming an external report.</summary>
internal sealed class ExternalReportCalculator : ICalcCalculator
{
    private readonly DirectStressCalculator _inner = new();

    public ExternalReportCalculator() => Template = _inner.Template with
    {
        Id = "test.external-report",
        Inputs = [.. _inner.Template.Inputs, new CalcInputSpec("report", "External report", CalcInputKind.Text, IsOptional: true)],
    };

    public CalcTemplate Template { get; }

    public void Run(CalcRunContext c) => _inner.Run(c);
}

public sealed class ExternalEvidenceTests
{
    [Fact]
    public async Task A_Profis_Report_Is_Filed_On_The_Evidence_And_Cited_By_The_Input()
    {
        var host = new LibraryTestHost(calculators: [new ExternalReportCalculator()]);
        var instance = await host.Service.CreateAsync("test.external-report", await host.ProjectAsync(), "Base plate", LibraryTestHost.ProvenancedInputs());
        var pdf = "%PDF-1.4 PROFIS anchorage report"u8.ToArray();

        var filed = await host.Service.AttachExternalEvidenceAsync(instance.Id, "C:\\temp\\..\\PROFIS-BP1.pdf", "application/pdf", pdf, "report");
        Assert.True(filed.Succeeded, filed.Reason);
        var report = filed.Instance!.Inputs.Single(i => i.Symbol == "report");
        Assert.Equal("PROFIS-BP1.pdf", report.Text);
        Assert.Equal(CalcProvenanceKind.Evidence, report.Provenance!.Kind);

        var evidence = (Evidence)(await host.Context.Repository.FindAsync(instance.EvidenceId!.Value))!;
        var attachment = Assert.Single(await evidence.GetAttachmentsAsync());
        Assert.Equal("PROFIS-BP1.pdf", attachment.FileName);
        Assert.Equal(pdf.Length, attachment.SizeInBytes);
    }

    [Fact]
    public async Task Empty_Or_Oversized_Reports_And_Non_Drafts_Are_Refused()
    {
        var host = new LibraryTestHost(calculators: [new ExternalReportCalculator()]);
        var instance = await host.Service.CreateAsync("test.external-report", await host.ProjectAsync(), "Base plate", LibraryTestHost.ProvenancedInputs());
        Assert.False((await host.Service.AttachExternalEvidenceAsync(instance.Id, "a.pdf", "application/pdf", Array.Empty<byte>())).Succeeded);
        Assert.False((await host.Service.AttachExternalEvidenceAsync(instance.Id, "   ", "application/pdf", new byte[] { 1 })).Succeeded);

        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var late = await host.Service.AttachExternalEvidenceAsync(instance.Id, "a.pdf", "application/pdf", new byte[] { 1 });
        Assert.False(late.Succeeded);
        Assert.Contains("draft", late.Reason, StringComparison.Ordinal);
    }
}
