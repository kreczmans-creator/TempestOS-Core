using Guided = Tempest.Calc.Library.ManualTesting;

namespace Tempest.Calc.Library.Dashboard;

/// <summary>
/// The build now running, as the title bar names it (<c>0.26.0 (abc1234)</c>) and its short commit, for telling a guided result recorded on another
/// build from one recorded on this one (SEC-C-06). Either may be empty when the build carries none.
/// </summary>
public sealed record RunningBuild(string Build, string Commit)
{
    /// <summary>Whether <paramref name="recorded"/> was recorded on a different build from this one. A running build that names nothing cannot be compared and flags nothing.</summary>
    public bool Differs(Guided.ManualTestStatus recorded)
    {
        ArgumentNullException.ThrowIfNull(recorded);

        if (string.IsNullOrWhiteSpace(Build) && string.IsNullOrWhiteSpace(Commit))
            return false;

        var recordedCommit = KnownCommit(recorded.BuildCommit);
        var runningCommit = KnownCommit(Commit);

        // Both name a commit: they are the same build when the commits are, whatever the version text says.
        if (recordedCommit is not null && runningCommit is not null)
            return !string.Equals(recordedCommit, runningCommit, StringComparison.OrdinalIgnoreCase);

        // One names a commit and the other does not: nothing shows them to be the same build.
        if (recordedCommit is not null || runningCommit is not null)
            return true;

        return !string.IsNullOrWhiteSpace(Build) && !string.IsNullOrWhiteSpace(recorded.Build)
            && !string.Equals(Build.Trim(), recorded.Build.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? KnownCommit(string? commit) =>
        string.IsNullOrWhiteSpace(commit) || string.Equals(commit, "unknown", StringComparison.OrdinalIgnoreCase) ? null : commit.Trim();
}

/// <summary>
/// Addendum C integration (C-4 with C-5): the dashboard's "Manual test" column from the guided test's run records. C-5's
/// <see cref="Guided.IManualTestStatusSource"/> gives, per worksheet template, the worst of the latest result of each of its worked examples
/// (Fail over Blocked over Pass) over the finished runs of the current script; this adapter turns that into the dashboard's status. It only
/// reads: it holds the read-only interface, never the run service.
/// </summary>
/// <remarks>
/// The mapping: Fail is <see cref="DashboardManualTestOutcome.Failed"/>; Pass on every worked example the script holds for the template is
/// <see cref="DashboardManualTestOutcome.Passed"/>; Pass on only some of them, or Blocked (a step that could not be tried), is
/// <see cref="DashboardManualTestOutcome.Partial"/>; a template with no result is <see cref="DashboardManualTestOutcome.NotTested"/>. The guided test records chains
/// as script steps, not per pack template, so a chain row reads "not tested". Every status names the build its latest counted result was recorded on, and
/// says when that is not the running build (SEC-C-06).
/// </remarks>
public sealed class GuidedManualTestStatusSource : IBulkDashboardManualTestSource
{
    private readonly Guided.IManualTestStatusSource _runs;
    private readonly Func<RunningBuild>? _running;

    /// <param name="runs">The guided test's run records, read-only.</param>
    /// <param name="running">The build now running; <see langword="null"/> flags no result as recorded on another build.</param>
    public GuidedManualTestStatusSource(Guided.IManualTestStatusSource runs, Func<RunningBuild>? running = null)
    {
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _running = running;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, DashboardManualTestStatus>> AllAsync(CancellationToken cancellationToken = default)
    {
        var latest = await _runs.LatestByTemplateAsync(cancellationToken).ConfigureAwait(false);
        var running = _running?.Invoke();
        return latest.ToDictionary(kv => kv.Key, kv => Map(kv.Value, running), StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<DashboardManualTestStatus> StatusAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var latest = await _runs.LatestByTemplateAsync(cancellationToken).ConfigureAwait(false);
        return latest.TryGetValue(id, out var status) ? Map(status, _running?.Invoke()) : DashboardManualTestStatus.NotTested;
    }

    /// <summary>The dashboard's status for one template's guided result, with a note that says how much of the script it covers and on which build.</summary>
    public static DashboardManualTestStatus Map(Guided.ManualTestStatus status) => Map(status, null);

    /// <summary>As <see cref="Map(Guided.ManualTestStatus)"/>, flagging the result when <paramref name="running"/> is a different build from the one it was recorded on.</summary>
    public static DashboardManualTestStatus Map(Guided.ManualTestStatus status, RunningBuild? running)
    {
        ArgumentNullException.ThrowIfNull(status);
        var complete = status.StepsInScript > 0 && status.StepsRecorded >= status.StepsInScript;
        var outcome = status.Outcome switch
        {
            Guided.ManualTestOutcome.Fail => DashboardManualTestOutcome.Failed,
            Guided.ManualTestOutcome.Pass when complete => DashboardManualTestOutcome.Passed,
            _ => DashboardManualTestOutcome.Partial,
        };
        var blocked = status.Outcome == Guided.ManualTestOutcome.Blocked ? ", one or more could not be tried" : string.Empty;
        var other = running?.Differs(status) == true;
        var build = status.Build.Length > 0 ? status.Build : status.BuildCommit.Length > 0 ? status.BuildCommit : "unknown";
        var note = $"{status.StepsRecorded} of {status.StepsInScript} worked examples recorded{blocked}; the latest by {status.Tester} on build {build}"
            + (other ? $", which is not the build running now ({(string.IsNullOrWhiteSpace(running!.Build) ? running.Commit : running.Build)}); run the guided test again on this build." : ".");
        return new DashboardManualTestStatus(
            outcome, status.LastRecordedUtc, note.Length > VerificationSnapshot.MaxTextLength ? note[..VerificationSnapshot.MaxTextLength] : note,
            status.Build.Length > 0 ? status.Build : null, status.BuildCommit.Length > 0 ? status.BuildCommit : null, other);
    }
}
