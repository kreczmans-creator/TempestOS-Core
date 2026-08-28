using Tempest.Companion.Client;
using Tempest.Companion.Contracts;
using Tempest.Companion.Offline;

namespace Tempest.Companion.Services;

/// <summary>
/// The Companion's fetch-with-fallback orchestrator (<c>ADR-0115</c>):
/// try the live platform; on success store the snapshot and return
/// <see cref="DataFreshness.Live"/>; on boundary failure fall back to the
/// stored snapshot (<see cref="DataFreshness.Cached"/>, or
/// <see cref="DataFreshness.Stale"/> past <see cref="StaleAfter"/>), and
/// only when nothing was ever stored report
/// <see cref="DataFreshness.Unavailable"/>. Authorization failures
/// (401/403) deliberately do <b>not</b> fall back to cache — a caller the
/// platform refused must not keep reading previously cached engineering
/// data (`WP 14.0A` security review).
/// </summary>
public sealed class CompanionDataService
{
    /// <summary>The age past which a cached snapshot is flagged <see cref="DataFreshness.Stale"/>.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    private readonly ICompanionApiClient _client;
    private readonly SnapshotCache _cache;
    private readonly Func<DateTimeOffset> _utcNow;
    private bool? _isConnected;

    /// <summary>Raised when the platform's reachability changes — <see langword="true"/> once a fetch succeeds, <see langword="false"/> once one fails.</summary>
    public event Action<bool>? ConnectionStateChanged;

    /// <summary>
    /// Initialises a new instance of the <see cref="CompanionDataService"/> class.
    /// </summary>
    /// <param name="client">The API boundary.</param>
    /// <param name="cache">The snapshot cache.</param>
    /// <param name="utcNow">The clock, overridable by tests; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    public CompanionDataService(ICompanionApiClient client, SnapshotCache cache, Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(cache);

        _client = client;
        _cache = cache;
        _utcNow = utcNow ?? (static () => DateTimeOffset.UtcNow);
    }

    /// <summary>Gets whether the last fetch reached the platform — <see langword="null"/> before the first fetch.</summary>
    public bool? IsConnected => _isConnected;

    /// <summary>Refreshes the Cockpit summary.</summary>
    public Task<SnapshotResult<CockpitSummaryDto>> GetCockpitAsync(CancellationToken cancellationToken = default) =>
        FetchAsync("cockpit", _client.GetCockpitAsync, cancellationToken);

    /// <summary>Refreshes the Project list.</summary>
    public Task<SnapshotResult<ProjectListDto>> GetProjectsAsync(CancellationToken cancellationToken = default) =>
        FetchAsync("projects", _client.GetProjectsAsync, cancellationToken);

    /// <summary>Refreshes the triage summary.</summary>
    public Task<SnapshotResult<AttentionDto>> GetAttentionAsync(CancellationToken cancellationToken = default) =>
        FetchAsync("attention", _client.GetAttentionAsync, cancellationToken);

    /// <summary>Refreshes recent activity.</summary>
    public Task<SnapshotResult<ActivityDto>> GetActivityAsync(CancellationToken cancellationToken = default) =>
        FetchAsync("activity", _client.GetActivityAsync, cancellationToken);

    /// <summary>Refreshes recent notifications.</summary>
    public Task<SnapshotResult<NotificationListDto>> GetNotificationsAsync(CancellationToken cancellationToken = default) =>
        FetchAsync("notifications", _client.GetNotificationsAsync, cancellationToken);

    /// <summary>
    /// Executes the set-document-status quick action. Never queued: a
    /// mutation either reaches the authoritative platform now or it does
    /// not happen (<c>ADR-0115</c>'s own no-offline-writes decision,
    /// <c>AT-24</c>) — the failure surfaces immediately instead of a
    /// pending write silently diverging from the system of record.
    /// </summary>
    public async Task<CompanionActionOutcome> SetDocumentStatusAsync(SetObjectStatusRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var outcome = await _client.SetDocumentStatusAsync(request, cancellationToken).ConfigureAwait(false);
            SetConnected(true);
            return outcome;
        }
        catch (CompanionApiException ex) when (ex.Reason == CompanionApiFailureReason.Unreachable)
        {
            SetConnected(false);
            throw;
        }
    }

    private async Task<SnapshotResult<T>> FetchAsync<T>(string key, Func<CancellationToken, Task<T>> fetch, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            var data = await fetch(cancellationToken).ConfigureAwait(false);
            var now = _utcNow();
            _cache.Store(key, data, now);
            SetConnected(true);
            return SnapshotResult<T>.Live(data, now);
        }
        catch (CompanionApiException ex)
        {
            SetConnected(ex.Reason != CompanionApiFailureReason.Unreachable);

            // 401/403: fail closed - never serve cached engineering data
            // to a caller the platform just refused.
            if (ex.Reason is CompanionApiFailureReason.Unauthorized or CompanionApiFailureReason.Forbidden)
                return SnapshotResult<T>.Unavailable(ex.Message);

            if (_cache.Load<T>(key) is { } cached)
                return SnapshotResult<T>.FromCache(cached.Data, cached.FetchedAtUtc, stale: _utcNow() - cached.FetchedAtUtc > StaleAfter, ex.Message);

            return SnapshotResult<T>.Unavailable(ex.Message);
        }
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected == connected)
            return;

        _isConnected = connected;
        ConnectionStateChanged?.Invoke(connected);
    }
}
