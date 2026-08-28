using Tempest.Companion.Client;
using Tempest.Companion.Contracts;

namespace Tempest.Companion.Tests;

/// <summary>A per-test temporary directory, deleted on dispose — the same isolation idiom <c>Tempest.Core.Tests</c>' own <c>TempDirectory</c> applies.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tempestos-companion-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
    }
}

/// <summary>
/// A configurable <see cref="ICompanionApiClient"/> for unit and view
/// tests — set <see cref="Failure"/> to make every call throw (the
/// offline/denied paths), otherwise the canned DTOs are returned. Used in
/// tests only; the production application always composes the real
/// <c>CompanionApiClient</c>.
/// </summary>
internal sealed class FakeCompanionApiClient : ICompanionApiClient
{
    public CompanionApiException? Failure { get; set; }

    public int CallCount { get; private set; }

    public CockpitSummaryDto Cockpit { get; set; } = CannedCockpit();

    public ProjectListDto Projects { get; set; } = new(DateTimeOffset.UtcNow, []);

    public AttentionDto Attention { get; set; } = new(DateTimeOffset.UtcNow, [], [], [], 0, [], []);

    public ActivityDto Activity { get; set; } = new(DateTimeOffset.UtcNow, []);

    public NotificationListDto Notifications { get; set; } = new(DateTimeOffset.UtcNow, []);

    public CompanionActionOutcome ActionOutcome { get; set; } = new(true, "done");

    public SetObjectStatusRequest? LastActionRequest { get; private set; }

    public Task<CockpitSummaryDto> GetCockpitAsync(CancellationToken cancellationToken = default) => Serve(Cockpit);

    public Task<ProjectListDto> GetProjectsAsync(CancellationToken cancellationToken = default) => Serve(Projects);

    public Task<AttentionDto> GetAttentionAsync(CancellationToken cancellationToken = default) => Serve(Attention);

    public Task<ActivityDto> GetActivityAsync(CancellationToken cancellationToken = default) => Serve(Activity);

    public Task<NotificationListDto> GetNotificationsAsync(CancellationToken cancellationToken = default) => Serve(Notifications);

    public Task<CompanionActionOutcome> SetDocumentStatusAsync(SetObjectStatusRequest request, CancellationToken cancellationToken = default)
    {
        LastActionRequest = request;
        return Serve(ActionOutcome);
    }

    private Task<T> Serve<T>(T value)
    {
        CallCount++;
        return Failure is { } failure ? Task.FromException<T>(failure) : Task.FromResult(value);
    }

    public static CockpitSummaryDto CannedCockpit(string health = "Attention") => new(
        GeneratedAtUtc: DateTimeOffset.UtcNow,
        PlatformVersion: "0.13.1",
        ProjectName: "Sample Project",
        Health: health,
        HealthScoreDisplay: "2/3 healthy (3/5 disciplines reporting)",
        DisciplineStatuses:
        [
            new("Requirements", "Healthy"),
            new("Calculations", "Attention"),
            new("Verification", "Unknown"),
            new("Documentation", "Healthy"),
            new("Manufacturing", "Unknown"),
            new("Review", "Unknown"),
        ],
        KpiCards: [new("Requirements", "12 total", false, null)],
        AttentionItems: [new("2 requirements failing validation", "Open the Requirements workspace to resolve.")],
        OpenDecisions: ["DEC-001 — Choose bearing supplier"],
        BlockedItems: ["CALC-7 blocked on missing material data"],
        OpenTaskCount: 3,
        UpcomingMilestones: ["PDR — due 2026-09-30"],
        RiskSummary: "2 open — 1 High, 1 Low.",
        DigitalThreadSummary: "14 link(s) tracked across 9 live object(s).",
        ContinueWhereILeftOff: new(Guid.NewGuid(), "Requirement", "REQ-0042 Pressure envelope", DateTimeOffset.UtcNow.AddMinutes(-10)),
        RecentActivity: [new(Guid.NewGuid(), "Calculation", "CALC-3 Bolt preload", DateTimeOffset.UtcNow.AddMinutes(-30))],
        RecentProjects: ["Sample Project"]);
}
