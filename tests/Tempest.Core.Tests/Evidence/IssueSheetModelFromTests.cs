using Tempest.Workspace.Evidence;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.Materials;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Evidence;

/// <summary>
/// <see cref="IssueSheetModel.From"/> — that it maps every field of a real,
/// issued <see cref="Tempest.Core.Evidence.Evidence"/> record, taking what
/// the Evidence itself knows from the record and what it does not from
/// <see cref="IssueSheetContext"/> (`WP 18.2B`, part 1). Lives in
/// <c>Tempest.Core.Tests</c> rather than <c>Tempest.Desktop.Tests</c>
/// because the model lives in <c>Tempest.Workspace</c> and this assembly
/// already references it (brief-18.2B-renderer.md's own test 5).
/// </summary>
public sealed class IssueSheetModelFromTests
{
    [Fact]
    public async Task From_MapsEveryFieldOfAnIssuedCheckedRecord()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);
        EvidenceTestHost.SignIn(host);

        var projectId = await EvidenceTestHost.CreateProjectAsync(host);
        var service = EvidenceTestHost.Service(host);
        var materials = EvidenceTestHost.Materials(host);
        var domain = EvidenceTestHost.Domain(host);

        var project = await domain.Repository.FindAsync(projectId) as Project;
        Assert.NotNull(project);

        var evidence = await service.CreateAsync(projectId, "Bracket calculation", EvidenceClassification.Calculation);

        // ---- One citation, with a source citation snapshot (`WP 18.0B` meets `WP 18.0A`) ----
        const string materialId = "FX-STEEL-ISSUE-SHEET-MODEL";
        await materials.RegisterAsync(
            materialId, MaterialFixtures.Steel(materialId), MaterialFixtures.Verified(),
            new SourceCitation("BSI", "BS EN 10025-2", "2019", "Table 7", null, "S355"));
        await MaterialFixtures.ReleaseAsync((MaterialCatalog)materials, materialId);
        var cite = await service.CiteAsync(evidence.Id, materials.LibraryName, materialId);
        Assert.True(cite.Succeeded);

        // ---- Two declared figures ----
        await service.DeclareFigureAsync(evidence.Id, "Utilisation", DeclaredFigureRole.Result, "0.82 1");
        await service.DeclareFigureAsync(evidence.Id, "Max stress", DeclaredFigureRole.Input, "142 MPa");

        // ---- Check: leading/trailing whitespace and a quotation mark, stored verbatim ----
        var check = await service.RecordCheckAsync(
            evidence.Id, "  J. Reviewer  ", "Client Co", "  Reviewed and \"accepted\".  ", CheckOutcome.AcceptedWithComments);
        Assert.True(check.Succeeded);

        // ---- Issue ----
        var issue = await service.IssueAsync(evidence.Id, "ISS-042", "B", "Client Ltd");
        Assert.True(issue.Succeeded);
        var issued = issue.Evidence!;

        var context = new IssueSheetContext(
            ProjectCode: project!.Identifier!,
            ProjectName: project.DisplayName,
            AuthorDisplayName: "Priya Patel",
            CheckerPrincipalDisplayName: null,
            ApplicationVersionText: "TempestOS 0.18.0 (2e655db)",
            StoreSequence: 777L,
            GeneratedAtUtc: new DateTimeOffset(2026, 3, 4, 9, 5, 0, TimeSpan.Zero));

        var model = IssueSheetModel.From(issued, context);

        // ---- Project and issue facts, from the context and IssueRecord ----
        Assert.Equal(project.Identifier, model.ProjectCode);
        Assert.Equal(project.DisplayName, model.ProjectName);
        Assert.Equal("Client Ltd", model.Client);
        Assert.Equal("B", model.Revision);
        Assert.Equal("ISS-042", model.IssueReference);
        Assert.Equal(issued.Issue!.DateUtc, model.IssueDateUtc);

        // ---- Evidence's own business identifier and title ----
        Assert.Equal(issued.Identifier, model.EvidenceReference); // null today: Evidence is always created with identifier: null
        Assert.Equal("Bracket calculation", model.Title);
        Assert.Equal(EvidenceClassification.Calculation, model.Classification);

        // ---- Author: resolved display name from the context, date from the record itself ----
        Assert.Equal("Priya Patel", model.AuthorDisplayName);
        Assert.Equal(issued.CreatedAt, model.AuthorDateUtc);

        // ---- Checker: verbatim name/organisation, no principal (the independence rule was off) ----
        Assert.Equal("  J. Reviewer  ", model.CheckerName);
        Assert.Equal("Client Co", model.CheckerOrganisation);
        Assert.Null(model.CheckerPrincipalDisplayName);
        Assert.Equal(issued.Check!.DateUtc, model.CheckDateUtc);
        Assert.Equal(CheckOutcome.AcceptedWithComments, model.Outcome);

        // ---- Citations: library, record id, revision, source snapshot ----
        var citationRow = Assert.Single(model.Citations);
        Assert.Equal(materials.LibraryName, citationRow.Library);
        Assert.Equal(materialId, citationRow.RecordId);
        Assert.Equal(cite.Citation!.Pin.RevisionNumber, citationRow.Revision);
        Assert.NotNull(citationRow.SourceCitationSnapshot);
        Assert.Contains("BS EN 10025-2", citationRow.SourceCitationSnapshot, StringComparison.Ordinal);

        // ---- Declared figures: name, role, value-with-unit text, carried through unchanged ----
        Assert.Equal(2, model.DeclaredFigures.Count);
        Assert.Contains(model.DeclaredFigures, f => f.Name == "Utilisation" && f.Role == DeclaredFigureRole.Result && f.Quantity == "0.82 1");
        Assert.Contains(model.DeclaredFigures, f => f.Name == "Max stress" && f.Role == DeclaredFigureRole.Input && f.Quantity == "142 MPa");

        // ---- What only the context could supply ----
        Assert.Equal("TempestOS 0.18.0 (2e655db)", model.ApplicationVersionText);
        Assert.Equal(777L, model.StoreSequence);
        Assert.Equal(new DateTimeOffset(2026, 3, 4, 9, 5, 0, TimeSpan.Zero), model.GeneratedAtUtc);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task From_ARecordThatHasNotBeenIssued_Throws()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);
        EvidenceTestHost.SignIn(host);

        var projectId = await EvidenceTestHost.CreateProjectAsync(host);
        var service = EvidenceTestHost.Service(host);
        var evidence = await service.CreateAsync(projectId, "Draft calculation", EvidenceClassification.Calculation);

        var context = new IssueSheetContext("PRJ", "Project", "Author", null, "TempestOS 0.18.0 (2e655db)", 1L, DateTimeOffset.UnixEpoch);

        Assert.Throws<InvalidOperationException>(() => IssueSheetModel.From(evidence, context));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }
}
