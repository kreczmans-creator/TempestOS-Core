using System.Text.Json.Nodes;
using Tempest.Workspace.Integration.DashboardExport;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// Proves <see cref="EngineeringStatusExportAdapter"/> against a real,
/// running <see cref="EngineeringDomainContext"/> — both its own plain
/// aggregation (Evidence, BOM, Digital Thread, no Cockpit equivalent for
/// any of the three) and, for Requirements/Verification, that its numbers
/// are the exact numbers a freshly-constructed <see cref="RequirementsCockpitReadModel"/>/
/// <see cref="VerificationCockpitReadModel"/> report — the product owner's
/// "Cockpit and Dashboard can never silently disagree" requirement,
/// checked directly rather than assumed.
/// </summary>
public class EngineeringStatusExportAdapterTests
{
    private static async Task<JsonNode> ExportAsync(ITempestHost host)
    {
        var adapter = new EngineeringStatusExportAdapter(
            DashboardExportTestHost.Domain(host),
            DashboardExportTestHost.Requirements(host),
            DashboardExportTestHost.RequirementValidation(host));

        using var stream = new MemoryStream();
        await adapter.ExportAsync(stream);
        stream.Position = 0;

        return JsonNode.Parse(stream) ?? throw new InvalidOperationException("Export produced no JSON.");
    }

    [Fact]
    public async Task ExportAsync_NoData_ReportsHonestZeroes()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var json = await ExportAsync(host);

        Assert.Equal(1, json["schemaVersion"]!.GetValue<int>());
        Assert.Equal(0, json["evidence"]!["total"]!.GetValue<int>());
        Assert.Equal(0, json["requirements"]!["total"]!.GetValue<int>());
        Assert.Equal(0, json["verification"]!["recorded"]!.GetValue<int>());
        Assert.Equal(0, json["bom"]!["linedObjects"]!.GetValue<int>());
        Assert.Empty(json["items"]!.AsArray());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_GeneratedAt_IsIso8601Utc()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var json = await ExportAsync(host);
        var generatedAt = json["generatedAt"]!.GetValue<string>();

        Assert.EndsWith("Z", generatedAt);
        Assert.True(DateTimeOffset.TryParse(generatedAt, out _));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_Evidence_AggregatesByStatusClassificationAndChecks()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var evidenceService = DashboardExportTestHost.Evidence(host);

        var draft = await evidenceService.CreateAsync(null, "Draft Evidence", EvidenceClassification.Drawing);

        var checkedOnly = await evidenceService.CreateAsync(null, "Checked Evidence", EvidenceClassification.Report);
        await evidenceService.RecordCheckAsync(checkedOnly.Id, "Checker One", "Client Co", "Looks fine.", CheckOutcome.AcceptedWithComments);

        var issuedNoSheet = await evidenceService.CreateAsync(null, "Issued Evidence", EvidenceClassification.Calculation);
        await evidenceService.RecordCheckAsync(issuedNoSheet.Id, "Checker Two", "Client Co", "Accepted.", CheckOutcome.Accepted);
        await evidenceService.IssueAsync(issuedNoSheet.Id, "ISS-001", "A", "Client Co");

        var rejected = await evidenceService.CreateAsync(null, "Rejected Evidence", EvidenceClassification.Test);
        await evidenceService.RecordCheckAsync(rejected.Id, "Checker Three", "Client Co", "Not acceptable.", CheckOutcome.Rejected);

        var json = await ExportAsync(host);
        var evidence = json["evidence"]!;

        Assert.Equal(4, evidence["total"]!.GetValue<int>());
        Assert.Equal(1, evidence["byStatus"]!["draft"]!.GetValue<int>());
        Assert.Equal(2, evidence["byStatus"]!["checked"]!.GetValue<int>()); // checkedOnly + rejected both moved to Checked
        Assert.Equal(1, evidence["byStatus"]!["issued"]!.GetValue<int>());
        Assert.Equal(0, evidence["byStatus"]!["superseded"]!.GetValue<int>());

        Assert.Equal(1, evidence["byClassification"]!["drawing"]!.GetValue<int>());
        Assert.Equal(1, evidence["byClassification"]!["report"]!.GetValue<int>());
        Assert.Equal(1, evidence["byClassification"]!["calculation"]!.GetValue<int>());
        Assert.Equal(1, evidence["byClassification"]!["test"]!.GetValue<int>());

        Assert.Equal(1, evidence["checks"]!["accepted"]!.GetValue<int>());
        Assert.Equal(1, evidence["checks"]!["acceptedWithComments"]!.GetValue<int>());
        Assert.Equal(1, evidence["checks"]!["rejected"]!.GetValue<int>());

        Assert.Equal(1, evidence["openIssuesSheetsPending"]!.GetValue<int>());

        var items = json["items"]!.AsArray();
        Assert.Contains(items, item => item!["status"]!.GetValue<string>() == "rejected");
        Assert.Contains(items, item => item!["detail"]!.GetValue<string>() == "Issued with no recorded issue sheet.");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_Requirements_MatchesRequirementsCockpitReadModelExactly()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var requirementsService = DashboardExportTestHost.Requirements(host);

        var draft = await requirementsService.CreateAsync("REQ-1", "Draft requirement.");

        var reviewed = await requirementsService.CreateAsync("REQ-2", "Reviewed requirement.");
        await requirementsService.SetStatusAsync(reviewed.Id, RequirementStatus.Reviewed);

        var approved = await requirementsService.CreateAsync("REQ-3", "Approved requirement.");
        await requirementsService.SetStatusAsync(approved.Id, RequirementStatus.Reviewed);
        await requirementsService.SetStatusAsync(approved.Id, RequirementStatus.Approved);

        var allocated = await requirementsService.CreateAsync("REQ-4", "Allocated requirement.");
        await requirementsService.SetStatusAsync(allocated.Id, RequirementStatus.Reviewed);
        await requirementsService.SetStatusAsync(allocated.Id, RequirementStatus.Approved);
        await requirementsService.SetStatusAsync(allocated.Id, RequirementStatus.Allocated);

        var verified = await requirementsService.CreateAsync("REQ-5", "Verified requirement.");
        await requirementsService.SetStatusAsync(verified.Id, RequirementStatus.Reviewed);
        await requirementsService.SetStatusAsync(verified.Id, RequirementStatus.Approved);
        await requirementsService.SetStatusAsync(verified.Id, RequirementStatus.Allocated);
        await requirementsService.SetStatusAsync(verified.Id, RequirementStatus.Verified);

        var satisfied = await requirementsService.CreateAsync("REQ-6", "Satisfied requirement.");
        await requirementsService.SetStatusAsync(satisfied.Id, RequirementStatus.Reviewed);
        await requirementsService.SetStatusAsync(satisfied.Id, RequirementStatus.Approved);
        await requirementsService.SetStatusAsync(satisfied.Id, RequirementStatus.Allocated);
        await requirementsService.SetStatusAsync(satisfied.Id, RequirementStatus.Verified);
        await requirementsService.SetStatusAsync(satisfied.Id, RequirementStatus.Satisfied);

        var obsolete = await requirementsService.CreateAsync("REQ-7", "Obsolete requirement.");
        await requirementsService.SetStatusAsync(obsolete.Id, RequirementStatus.Obsolete);

        // The exact same read model the desktop Cockpit constructs — this
        // test's own oracle.
        var cockpit = new RequirementsCockpitReadModel(requirementsService, DashboardExportTestHost.RequirementValidation(host));
        await cockpit.LoadAsync();

        var json = await ExportAsync(host);
        var byStatus = json["requirements"]!["byStatus"]!;

        Assert.Equal(7, json["requirements"]!["total"]!.GetValue<int>());
        Assert.Equal(cockpit.Count, json["requirements"]!["total"]!.GetValue<int>());

        Assert.Equal(cockpit.StatusCounts[RequirementStatus.Draft], byStatus["draft"]!.GetValue<int>());
        Assert.Equal(cockpit.StatusCounts[RequirementStatus.Reviewed], byStatus["reviewed"]!.GetValue<int>());
        Assert.Equal(cockpit.StatusCounts[RequirementStatus.Approved], byStatus["approved"]!.GetValue<int>());
        Assert.Equal(cockpit.StatusCounts[RequirementStatus.Allocated], byStatus["allocated"]!.GetValue<int>());
        Assert.Equal(cockpit.StatusCounts[RequirementStatus.Verified], byStatus["verified"]!.GetValue<int>());
        Assert.Equal(cockpit.StatusCounts[RequirementStatus.Satisfied], byStatus["satisfied"]!.GetValue<int>());
        Assert.Equal(cockpit.StatusCounts[RequirementStatus.Obsolete], byStatus["obsolete"]!.GetValue<int>());

        // requirementsUnverified: every status except Verified/Satisfied — 5 of the 7.
        Assert.Equal(5, json["verification"]!["requirementsUnverified"]!.GetValue<int>());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_Verification_MatchesVerificationCockpitReadModelExactly()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        var domain = DashboardExportTestHost.Domain(host);
        var verificationService = DashboardExportTestHost.Verification(host);

        var registry = new VerificationActivityFactoryRegistry(domain);

        var passedActivity = await registry.CreateAsync("Passed Activity", "Verifies REQ-1.", Guid.NewGuid(), "Test", parentId: null);
        await verificationService.RecordAsync(passedActivity.Id, VerificationOutcome.Pass, "Test", new VerificationContext());

        var failedActivity = await registry.CreateAsync("Failed Activity", "Verifies REQ-2.", Guid.NewGuid(), "Analysis", parentId: null);
        await verificationService.RecordAsync(failedActivity.Id, VerificationOutcome.Fail, "Analysis", new VerificationContext());

        var conditionalActivity = await registry.CreateAsync("Conditional Activity", "Verifies REQ-3.", Guid.NewGuid(), "Inspection", parentId: null);
        await verificationService.RecordAsync(conditionalActivity.Id, VerificationOutcome.Conditional, "Inspection", new VerificationContext());

        // Re-verified once more — contributes a second record to "recorded"
        // without changing "byOutcome" (still one Activity, latest = Pass).
        await verificationService.RecordAsync(passedActivity.Id, VerificationOutcome.Pass, "Test", new VerificationContext());

        var cockpit = new VerificationCockpitReadModel(domain);
        await cockpit.LoadAsync();

        var json = await ExportAsync(host);
        var verification = json["verification"]!;

        Assert.Equal(cockpit.TotalVerificationRecordsCount, verification["recorded"]!.GetValue<int>());
        Assert.Equal(4, verification["recorded"]!.GetValue<int>());

        Assert.Equal(cockpit.PassedVerificationCount, verification["byOutcome"]!["pass"]!.GetValue<int>());
        Assert.Equal(cockpit.FailedVerificationCount, verification["byOutcome"]!["fail"]!.GetValue<int>());
        Assert.Equal(cockpit.ConditionalVerificationCount, verification["byOutcome"]!["conditional"]!.GetValue<int>());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_Bom_CountsLinedObjectsAndPendingByLifecycle()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var draftPart = await DashboardExportTestHost.CreatePartAsync(host, "PART-1", "Draft Part");
        await draftPart.SetBomLineAsync(2m, "EA");

        var inReviewAssembly = await DashboardExportTestHost.CreateAssemblyAsync(host, "ASM-1", "In Review Assembly");
        await inReviewAssembly.SetBomLineAsync(1m, "EA");
        await inReviewAssembly.TransitionAsync(LifecycleState.InReview);

        var releasedAssembly = await DashboardExportTestHost.CreateAssemblyAsync(host, "ASM-2", "Released Assembly");
        await releasedAssembly.SetBomLineAsync(1m, "EA");
        await releasedAssembly.TransitionAsync(LifecycleState.InReview);
        await releasedAssembly.TransitionAsync(LifecycleState.Approved);
        await releasedAssembly.TransitionAsync(LifecycleState.Released);

        var json = await ExportAsync(host);
        var bom = json["bom"]!;

        Assert.Equal(3, bom["linedObjects"]!.GetValue<int>());
        Assert.Equal(1, bom["pendingByLifecycle"]!["draft"]!.GetValue<int>());
        Assert.Equal(1, bom["pendingByLifecycle"]!["inReview"]!.GetValue<int>());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_DigitalThread_TalliesRelationshipsByCategoryAndOrphans()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var assembly = await DashboardExportTestHost.CreateAssemblyAsync(host, "ASM-DT-1", "Thread Assembly");
        var part = await DashboardExportTestHost.CreatePartAsync(host, "PART-DT-1", "Thread Part");
        await assembly.LinkAsync(part.Id, "groupedUnder");

        var orphan = await DashboardExportTestHost.CreatePartAsync(host, "PART-DT-2", "Orphan Part");

        var json = await ExportAsync(host);
        var digitalThread = json["digitalThread"]!;

        Assert.Equal(1, digitalThread["relationshipsByCategory"]!["composition"]!.GetValue<int>());
        Assert.Equal(1, digitalThread["objectsWithNoRelationships"]!.GetValue<int>());

        _ = orphan;
        await manager.ShutdownAsync();
    }
}
