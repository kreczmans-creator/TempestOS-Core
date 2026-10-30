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

    // ------------------------------------------------------------------ Purple P-A-02

    [Fact]
    public async Task P_A_02_An_Evidence_Source_Must_Point_At_A_File_Attached_To_The_Calculations_Own_Evidence()
    {
        var host = new LibraryTestHost(calculators: [new ExternalReportCalculator()]);
        var project = await host.ProjectAsync();
        var instance = await host.Service.CreateAsync("test.external-report", project, "Base plate", LibraryTestHost.ProvenancedInputs());

        // A made-up evidence id (typed in the form or carried in an imported file) is refused.
        var made = LibraryTestHost.ProvenancedInputs().Append(new CalcInputValue("report", Text: "profis.pdf", Provenance: CalcProvenance.FromEvidence("0a1b2c/3d4e5f"))).ToList();
        var refused = await host.Service.UpdateInputsAsync(instance.Id, made);
        Assert.False(refused.Succeeded);
        Assert.Contains("report", refused.Reason, StringComparison.Ordinal);
        Assert.Contains("attached to this calculation's Evidence", refused.Reason, StringComparison.Ordinal);

        // So is free text, and so is a new calculation created with one.
        var typed = LibraryTestHost.ProvenancedInputs().Append(new CalcInputValue("report", Text: "profis.pdf", Provenance: CalcProvenance.FromEvidence("Site survey"))).ToList();
        Assert.False((await host.Service.UpdateInputsAsync(instance.Id, typed)).Succeeded);
        await Assert.ThrowsAsync<CalcLinkRefusedException>(() => host.Service.CreateAsync("test.external-report", project, "Other", made));

        // A real attachment of this calculation is accepted.
        var filed = await host.Service.AttachExternalEvidenceAsync(instance.Id, "profis.pdf", "application/pdf", "%PDF report"u8.ToArray(), "report");
        Assert.True(filed.Succeeded, filed.Reason);
        var reference = filed.Instance!.Inputs.Single(i => i.Symbol == "report").Provenance!.Reference!;

        // ... but another calculation's attachment is not this one's.
        var other = await host.Service.CreateAsync("test.external-report", project, "Other plate", LibraryTestHost.ProvenancedInputs());
        var borrowed = LibraryTestHost.ProvenancedInputs().Append(new CalcInputValue("report", Text: "profis.pdf", Provenance: CalcProvenance.FromEvidence(reference))).ToList();
        Assert.False((await host.Service.UpdateInputsAsync(other.Id, borrowed)).Succeeded);

        // A well-formed id of an attachment that is not there is refused too.
        var evidenceId = instance.EvidenceId!.Value.ToString("N");
        var missing = LibraryTestHost.ProvenancedInputs().Append(new CalcInputValue("report", Text: "x.pdf", Provenance: CalcProvenance.FromEvidence($"{evidenceId}/{Guid.NewGuid():N}"))).ToList();
        Assert.False((await host.Service.UpdateInputsAsync(instance.Id, missing)).Succeeded);
    }

    [Fact]
    public async Task P_A_02_A_Revision_Keeps_Citing_The_File_Its_Earlier_Revision_Filed()
    {
        var host = new LibraryTestHost(calculators: [new ExternalReportCalculator()]);
        var instance = await host.Service.CreateAsync("test.external-report", await host.ProjectAsync(), "Base plate", LibraryTestHost.ProvenancedInputs());
        Assert.True((await host.Service.AttachExternalEvidenceAsync(instance.Id, "profis.pdf", "application/pdf", "%PDF report"u8.ToArray(), "report")).Succeeded);
        host.Principal = "reviewer";
        Assert.True((await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(instance.Id, "ISS-1", "Client Co")).Succeeded);

        var revised = (await host.Service.ReviseAsync(instance.Id)).Instance!;
        var edited = revised.Inputs.Select(i => i.Symbol == "F" ? i with { Number = 13 } : i).ToList();
        var updated = await host.Service.UpdateInputsAsync(revised.Id, edited);
        Assert.True(updated.Succeeded, updated.Reason);
    }
}
