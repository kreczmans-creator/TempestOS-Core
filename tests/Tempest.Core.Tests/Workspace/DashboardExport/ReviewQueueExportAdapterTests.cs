using System.Text.Json.Nodes;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Workspace.Verification;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// Proves <see cref="ReviewQueueExportAdapter"/> (<c>reviews.json</c>,
/// `ADR-0157`) against a real, running host: which items are queued per
/// family, which are not (drafts, deletions, rejected checks, issued
/// evidence), the project each is reported against, its date basis and
/// submitter, and the oldest-first order the dashboard renders as-is.
/// </summary>
public class ReviewQueueExportAdapterTests
{
    private static async Task<JsonNode> ExportAsync(ITempestHost host, TimeProvider? time = null)
    {
        var adapter = new ReviewQueueExportAdapter(
            DashboardExportTestHost.Domain(host),
            DashboardExportTestHost.Requirements(host),
            time);

        using var stream = new MemoryStream();
        await adapter.ExportAsync(stream);
        stream.Position = 0;

        return JsonNode.Parse(stream) ?? throw new InvalidOperationException("Export produced no JSON.");
    }

    private static JsonObject ItemFor(JsonNode json, Guid id) =>
        json["items"]!.AsArray().Single(i => i!["id"]!.GetValue<Guid>() == id)!.AsObject();

    private static bool Contains(JsonNode json, Guid id) =>
        json["items"]!.AsArray().Any(i => i!["id"]!.GetValue<Guid>() == id);

    [Fact]
    public async Task ExportAsync_NoData_WritesAnEmptyQueueWithEveryDisciplineAtZero()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var json = await ExportAsync(host);

        Assert.Equal(1, json["schemaVersion"]!.GetValue<int>());
        Assert.EndsWith("Z", json["generatedAt"]!.GetValue<string>());
        Assert.Equal(0, json["total"]!.GetValue<int>());
        Assert.Empty(json["items"]!.AsArray());

        var byDiscipline = json["byDiscipline"]!.AsObject();
        Assert.Equal(["documents", "calculations", "verification", "evidence", "requirements"], byDiscipline.Select(p => p.Key));
        Assert.All(byDiscipline, p => Assert.Equal(0, p.Value!.GetValue<int>()));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_LifecycleKinds_QueuesOnlyLiveInReviewObjects_WithProjectAndSubmitter()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var domain = DashboardExportTestHost.Domain(host);
        var documents = new DocumentObjectFactoryRegistry(domain);
        var calculations = new CalculationObjectFactoryRegistry(domain);
        var verification = new VerificationActivityFactoryRegistry(domain);

        var project = await DashboardExportTestHost.CreateProjectAsync(host, "PRJ-7", "Harbour Bridge");

        var drawing = await documents.CreateAsync(DocumentObjectFactoryRegistry.Drawing, "DWG-100", "GA Drawing", "content", project.Id);
        await ((IHasLifecycle)drawing).TransitionAsync(LifecycleState.InReview);

        var draftDocument = await documents.CreateAsync(DocumentObjectFactoryRegistry.Document, "DOC-1", "Still a draft", "content", project.Id);

        var deletedModel = await documents.CreateAsync(DocumentObjectFactoryRegistry.CadModel, "CAD-1", "Deleted model", "content", project.Id);
        await ((IHasLifecycle)deletedModel).TransitionAsync(LifecycleState.InReview);
        await ((IDeletable)deletedModel).DeleteAsync();

        var approved = await documents.CreateAsync(DocumentObjectFactoryRegistry.Document, "DOC-2", "Already approved", "content", project.Id);
        await ((IHasLifecycle)approved).TransitionAsync(LifecycleState.InReview);
        await ((IHasLifecycle)approved).TransitionAsync(LifecycleState.Approved);

        var calculation = await calculations.CreateAsync(CalculationObjectFactoryRegistry.CalculationKind, "CALC-1", "Beam check", "content", parentId: null);
        await ((IHasLifecycle)calculation).TransitionAsync(LifecycleState.InReview);

        var activity = await verification.CreateAsync("Load test", "content", drawing.Id, "Test", project.Id);
        await ((IHasLifecycle)activity).TransitionAsync(LifecycleState.InReview);

        var json = await ExportAsync(host);

        Assert.Equal(3, json["total"]!.GetValue<int>());
        Assert.False(Contains(json, draftDocument.Id));
        Assert.False(Contains(json, deletedModel.Id));
        Assert.False(Contains(json, approved.Id));

        var drawingItem = ItemFor(json, drawing.Id);
        Assert.Equal("Drawing", drawingItem["kind"]!.GetValue<string>());
        Assert.Equal("documents", drawingItem["discipline"]!.GetValue<string>());
        Assert.Equal("DWG-100", drawingItem["identifier"]!.GetValue<string>());
        Assert.Equal("GA Drawing", drawingItem["title"]!.GetValue<string>());
        Assert.Equal("inReview", drawingItem["status"]!.GetValue<string>());
        Assert.Equal("submitted", drawingItem["sinceBasis"]!.GetValue<string>());
        Assert.Equal(DashboardExportTestHost.PrincipalId, drawingItem["submittedBy"]!.GetValue<string>());
        Assert.Equal(project.Id, drawingItem["project"]!["id"]!.GetValue<Guid>());
        Assert.Equal("PRJ-7", drawingItem["project"]!["identifier"]!.GetValue<string>());
        Assert.Equal("Harbour Bridge", drawingItem["project"]!["name"]!.GetValue<string>());
        Assert.Equal(0, drawingItem["ageDays"]!.GetValue<int>());

        // A quick calculation with no project is standalone work, reported
        // honestly with no project rather than dropped (`TD-89`).
        var calculationItem = ItemFor(json, calculation.Id);
        Assert.Equal("calculations", calculationItem["discipline"]!.GetValue<string>());
        Assert.Null(calculationItem["project"]);

        Assert.Equal("verification", ItemFor(json, activity.Id)["discipline"]!.GetValue<string>());

        Assert.Equal(1, json["byDiscipline"]!["documents"]!.GetValue<int>());
        Assert.Equal(1, json["byDiscipline"]!["calculations"]!.GetValue<int>());
        Assert.Equal(1, json["byDiscipline"]!["verification"]!.GetValue<int>());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_Evidence_QueuesOnlyCheckedAndNotRejected_DatedByTheCheck()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var evidenceService = DashboardExportTestHost.Evidence(host);

        var draft = await evidenceService.CreateAsync(null, "Draft Evidence", EvidenceClassification.Drawing);

        var awaitingIssue = await evidenceService.CreateAsync(null, "Checked Evidence", EvidenceClassification.Report);
        await evidenceService.RecordCheckAsync(awaitingIssue.Id, "Checker One", "Client Co", "Looks fine.", CheckOutcome.AcceptedWithComments);

        var rejected = await evidenceService.CreateAsync(null, "Rejected Evidence", EvidenceClassification.Test);
        await evidenceService.RecordCheckAsync(rejected.Id, "Checker Two", "Client Co", "Not acceptable.", CheckOutcome.Rejected);

        var issued = await evidenceService.CreateAsync(null, "Issued Evidence", EvidenceClassification.Calculation);
        await evidenceService.RecordCheckAsync(issued.Id, "Checker Three", "Client Co", "Accepted.", CheckOutcome.Accepted);
        await evidenceService.IssueAsync(issued.Id, "ISS-001", "A", "Client Co");

        var json = await ExportAsync(host);

        Assert.Equal(1, json["total"]!.GetValue<int>());
        Assert.False(Contains(json, draft.Id));
        Assert.False(Contains(json, rejected.Id));
        Assert.False(Contains(json, issued.Id));

        var item = ItemFor(json, awaitingIssue.Id);
        Assert.Equal("Evidence", item["kind"]!.GetValue<string>());
        Assert.Equal("evidence", item["discipline"]!.GetValue<string>());
        Assert.Equal("checked", item["status"]!.GetValue<string>());
        Assert.Equal("checked", item["sinceBasis"]!.GetValue<string>());
        Assert.Equal("Checker One", item["submittedBy"]!.GetValue<string>());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_Requirements_QueuesReviewed_DatedByCreation_ProjectFromItsLinks()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var requirementsService = DashboardExportTestHost.Requirements(host);
        var documents = new DocumentObjectFactoryRegistry(DashboardExportTestHost.Domain(host));

        var project = await DashboardExportTestHost.CreateProjectAsync(host, "PRJ-9", "Ferry Terminal");
        var drawing = await documents.CreateAsync(DocumentObjectFactoryRegistry.Drawing, "DWG-9", "Pontoon GA", "content", project.Id);

        var draft = await requirementsService.CreateAsync("REQ-1", "Draft requirement.");

        var reviewed = await requirementsService.CreateAsync("REQ-2", "The pontoon shall carry 5 kN/m².");
        await requirementsService.SetStatusAsync(reviewed.Id, RequirementStatus.Reviewed);
        await requirementsService.LinkAsync(reviewed.Id, drawing.Id, "satisfiedBy");

        var unlinked = await requirementsService.CreateAsync("REQ-3", "Unlinked requirement.");
        await requirementsService.SetStatusAsync(unlinked.Id, RequirementStatus.Reviewed);

        var json = await ExportAsync(host);

        Assert.Equal(2, json["total"]!.GetValue<int>());
        Assert.False(Contains(json, draft.Id));

        var item = ItemFor(json, reviewed.Id);
        Assert.Equal("Requirement", item["kind"]!.GetValue<string>());
        Assert.Equal("requirements", item["discipline"]!.GetValue<string>());
        Assert.Equal("REQ-2", item["identifier"]!.GetValue<string>());
        Assert.Equal("The pontoon shall carry 5 kN/m².", item["title"]!.GetValue<string>());
        Assert.Equal("reviewed", item["status"]!.GetValue<string>());
        Assert.Equal("created", item["sinceBasis"]!.GetValue<string>());
        Assert.Null(item["submittedBy"]);
        Assert.Equal("PRJ-9", item["project"]!["identifier"]!.GetValue<string>());

        Assert.Null(ItemFor(json, unlinked.Id)["project"]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_SortsOldestFirst_AndAgesAgainstTheExportClock()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var documents = new DocumentObjectFactoryRegistry(DashboardExportTestHost.Domain(host));

        // Created in identifier order but submitted out of it (3, 1, 2), so
        // the expected order can only come from submission time — an
        // identifier or creation-order sort would give 1, 2, 3 (board N5).
        var created = new Dictionary<int, IEngineeringObject>();
        foreach (var n in new[] { 1, 2, 3 })
            created[n] = await documents.CreateAsync(DocumentObjectFactoryRegistry.Document, $"DOC-{n}", $"Document {n}", "content", parentId: null);

        var ids = new List<Guid>();
        foreach (var n in new[] { 3, 1, 2 })
        {
            await ((IHasLifecycle)created[n]).TransitionAsync(LifecycleState.InReview);
            ids.Add(created[n].Id);
        }

        var now = DateTimeOffset.UtcNow.AddDays(15).AddHours(1);
        var json = await ExportAsync(host, new FakeTimeProvider(now));
        var items = json["items"]!.AsArray();

        Assert.Equal(ids, items.Select(i => i!["id"]!.GetValue<Guid>()));

        var since = items.Select(i => DateTimeOffset.Parse(i!["since"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(since.OrderBy(s => s), since);
        Assert.All(items, i => Assert.Equal(15, i!["ageDays"]!.GetValue<int>()));

        await manager.ShutdownAsync();
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(7.9, 7)]
    [InlineData(14.01, 14)]
    [InlineData(-2.0, 0)]
    public void AgeInDays_IsWholeDaysAndNeverNegative(double days, int expected)
    {
        var since = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(expected, ReviewQueueExportAdapter.AgeInDays(since, since.AddDays(days)));
    }
}
