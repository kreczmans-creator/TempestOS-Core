using System.Text.Json.Nodes;
using Tempest.Workspace;
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
/// checked directly rather than assumed. The schema-v2 sections
/// (<c>health</c>/<c>kpis</c>/<c>attention</c>/<c>blockedItems</c>/
/// <c>overdueActions</c>) are checked the same way, against the desktop's
/// own <see cref="EngineeringCockpit"/> (<see cref="DashboardExportTestHost.Cockpit"/>)
/// — "the Pi renders what the desktop cockpit computes".
/// </summary>
public class EngineeringStatusExportAdapterTests
{
    private static async Task<JsonNode> ExportAsync(ITempestHost host)
    {
        var adapter = new EngineeringStatusExportAdapter(
            DashboardExportTestHost.Domain(host),
            DashboardExportTestHost.Requirements(host),
            DashboardExportTestHost.RequirementValidation(host),
            DashboardExportTestHost.NavigationProvider(host),
            DashboardExportTestHost.CommandRegistry(host));

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

        Assert.Equal(2, json["schemaVersion"]!.GetValue<int>());
        Assert.Equal(0, json["evidence"]!["total"]!.GetValue<int>());
        Assert.Equal(0, json["requirements"]!["total"]!.GetValue<int>());
        Assert.Equal(0, json["verification"]!["recorded"]!.GetValue<int>());
        Assert.Equal(0, json["bom"]!["linedObjects"]!.GetValue<int>());
        Assert.Empty(json["items"]!.AsArray());

        // Schema v2: an empty project is honestly "unknown", with nothing
        // blocked and nothing overdue — never a fabricated "healthy".
        Assert.Equal("unknown", json["health"]!["overall"]!.GetValue<string>());
        Assert.Empty(json["blockedItems"]!.AsArray());
        Assert.Empty(json["overdueActions"]!.AsArray());

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

    // ---- Schema v2: the Cockpit's own health/KPIs/attention/blocked/overdue, verbatim ----

    /// <summary>Seeds a Blocked Verification discipline (one Failed activity) and a live Requirement — enough for a real (non-Unknown) overall health, a non-placeholder KPI, a blocked item and a real attention item.</summary>
    private static async Task SeedCockpitSignalsAsync(ITempestHost host)
    {
        var requirementsService = DashboardExportTestHost.Requirements(host);
        await requirementsService.CreateAsync("REQ-V2-1", "A requirement the Cockpit can see.");

        var registry = new VerificationActivityFactoryRegistry(DashboardExportTestHost.Domain(host));
        var failed = await registry.CreateAsync("Failed Activity", "Verifies REQ-V2-1.", Guid.NewGuid(), "Test", parentId: null);
        await DashboardExportTestHost.Verification(host).RecordAsync(failed.Id, VerificationOutcome.Fail, "Test", new VerificationContext());
    }

    [Fact]
    public async Task ExportAsync_Health_MatchesEngineeringCockpitWordForWord()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        await SeedCockpitSignalsAsync(host);

        var cockpit = await DashboardExportTestHost.Cockpit(manager);
        var json = await ExportAsync(host);
        var health = json["health"]!;

        // A real signal, not the empty-project default — so the equality
        // below is proving something.
        Assert.Equal(EngineeringHealthStatus.Blocked, cockpit.Health);
        Assert.Equal("blocked", health["overall"]!.GetValue<string>());
        Assert.Equal(EngineeringStatusExportAdapter.FormatHealth(cockpit.Health), health["overall"]!.GetValue<string>());

        var byDiscipline = health["byDiscipline"]!;
        Assert.Equal(EngineeringStatusExportAdapter.FormatHealth(cockpit.RequirementsStatus), byDiscipline["requirements"]!.GetValue<string>());
        Assert.Equal(EngineeringStatusExportAdapter.FormatHealth(cockpit.VerificationStatus), byDiscipline["verification"]!.GetValue<string>());
        Assert.Equal(EngineeringStatusExportAdapter.FormatHealth(cockpit.CalculationStatus), byDiscipline["calculations"]!.GetValue<string>());
        Assert.Equal(EngineeringStatusExportAdapter.FormatHealth(cockpit.DocumentationStatus), byDiscipline["documents"]!.GetValue<string>());
        Assert.Equal(EngineeringStatusExportAdapter.FormatHealth(cockpit.ManufacturingStatus), byDiscipline["manufacturing"]!.GetValue<string>());

        // Exactly the five disciplines Health itself rolls up — Mechanical
        // has no status of its own, Review is a hard-coded Unknown Health
        // excludes; neither is exported as if it were a signal.
        Assert.Equal(
            ["calculations", "documents", "manufacturing", "requirements", "verification"],
            byDiscipline.AsObject().Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList());

        // Blocked by Verification specifically, and the Cockpit's own
        // closed vocabulary throughout.
        Assert.Equal("blocked", byDiscipline["verification"]!.GetValue<string>());
        Assert.All(byDiscipline.AsObject(), p => Assert.Contains(p.Value!.GetValue<string>(), new[] { "unknown", "healthy", "attention", "blocked" }));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_Kpis_MatchRequirementsKpiCardsLabelForLabel()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        await SeedCockpitSignalsAsync(host);

        var cockpit = await DashboardExportTestHost.Cockpit(manager);
        var json = await ExportAsync(host);
        var exported = json["kpis"]!["requirements"]!.AsArray();

        Assert.Equal(cockpit.RequirementsKpiCards.Count, exported.Count);
        for (var i = 0; i < exported.Count; i++)
        {
            var card = cockpit.RequirementsKpiCards[i];
            var node = exported[i]!;

            Assert.Equal(card.Label, node["label"]!.GetValue<string>());
            Assert.Equal(card.Value, node["value"]!.GetValue<string>());
            Assert.Equal(card.IsPlaceholder, node["isPlaceholder"]!.GetValue<bool>());
            Assert.Equal(card.PercentValue, node["percentValue"]?.GetValue<int?>());
        }

        // At least one real, non-placeholder card with the seeded count —
        // the equality above is not comparing two empty lists.
        Assert.Contains(exported, node => node!["label"]!.GetValue<string>() == "Total Requirements" && node["value"]!.GetValue<string>() == "1" && !node["isPlaceholder"]!.GetValue<bool>());

        // Every KPI set the desktop renders is present, keyed the same way.
        Assert.Equal(
            ["calculations", "documents", "manufacturing", "overview", "requirements", "verification"],
            json["kpis"]!.AsObject().Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList());
        Assert.Equal(cockpit.VerificationKpiCards.Select(c => c.Label), json["kpis"]!["verification"]!.AsArray().Select(n => n!["label"]!.GetValue<string>()));
        Assert.Equal(cockpit.KpiCards.Select(c => c.Label), json["kpis"]!["overview"]!.AsArray().Select(n => n!["label"]!.GetValue<string>()));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_Attention_CarriesEveryCockpitAttentionItemVerbatim_TaggedByDiscipline()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        await SeedCockpitSignalsAsync(host);

        var cockpit = await DashboardExportTestHost.Cockpit(manager);
        var json = await ExportAsync(host);
        var attention = json["attention"]!.AsArray();

        // Same items, same order, same words — and each says where it came from.
        Assert.Equal(cockpit.AttentionItemsByDiscipline.Count, attention.Count);
        for (var i = 0; i < attention.Count; i++)
        {
            var expected = cockpit.AttentionItemsByDiscipline[i];
            Assert.Equal(expected.Discipline, attention[i]!["discipline"]!.GetValue<string>());
            Assert.Equal(expected.Item.Title, attention[i]!["title"]!.GetValue<string>());
            Assert.Equal(expected.Item.Detail, attention[i]!["detail"]!.GetValue<string>());
        }

        // A real, seeded item the Cockpit reports, verbatim, attributed to
        // the discipline that produced it.
        var live = Assert.Single(cockpit.AttentionItems, item => item.Title == "Requirements Management is live");
        Assert.Contains(attention, node =>
            node!["discipline"]!.GetValue<string>() == "requirements"
            && node["title"]!.GetValue<string>() == live.Title
            && node["detail"]!.GetValue<string>() == live.Detail);

        // The composition root's own trailing fixed entry is "cockpit", not a discipline.
        Assert.Equal("cockpit", attention[^1]!["discipline"]!.GetValue<string>());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_BlockedItems_MatchEngineeringCockpitExactly()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);
        await SeedCockpitSignalsAsync(host);

        var cockpit = await DashboardExportTestHost.Cockpit(manager);
        var json = await ExportAsync(host);

        Assert.NotEmpty(cockpit.BlockedItems);
        Assert.Equal(cockpit.BlockedItems, json["blockedItems"]!.AsArray().Select(n => n!.GetValue<string>()).ToList());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ExportAsync_OverdueActions_MatchEngineeringCockpitExactly()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await DashboardExportTestHost.StartAsync(temp.Path);

        var overdue = await DashboardExportTestHost.CreateTaskAsync(host, "TASK-1", "Overdue task");
        await overdue.AssignAsync("engineer");
        await overdue.SetDueDateAsync(DateTimeOffset.UtcNow.AddDays(-3));

        var notYetDue = await DashboardExportTestHost.CreateTaskAsync(host, "TASK-2", "Future task");
        await notYetDue.SetDueDateAsync(DateTimeOffset.UtcNow.AddDays(30));

        var cockpit = await DashboardExportTestHost.Cockpit(manager);
        var json = await ExportAsync(host);
        var actions = json["overdueActions"]!.AsArray();

        var expected = Assert.Single(cockpit.OverdueActions);
        var exported = Assert.Single(actions)!;

        Assert.Equal(expected.Title, exported["title"]!.GetValue<string>());
        Assert.Equal("Overdue task", exported["title"]!.GetValue<string>());
        Assert.Equal(expected.Owner, exported["owner"]!.GetValue<string>());
        Assert.Equal(expected.DueDate, exported["dueDate"]!.GetValue<DateTimeOffset>());
        Assert.Equal(expected.DaysOverdue, exported["daysOverdue"]!.GetValue<int>());
        Assert.Equal(3, exported["daysOverdue"]!.GetValue<int>());

        await manager.ShutdownAsync();
    }
}
