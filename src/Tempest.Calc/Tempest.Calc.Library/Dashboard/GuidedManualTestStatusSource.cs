using Guided = Tempest.Calc.Library.ManualTesting;

namespace Tempest.Calc.Library.Dashboard;

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
/// as script steps, not per pack template, so a chain row reads "not tested".
/// </remarks>
public sealed class GuidedManualTestStatusSource : IBulkDashboardManualTestSource
{
    private readonly Guided.IManualTestStatusSource _runs;

    public GuidedManualTestStatusSource(Guided.IManualTestStatusSource runs) => _runs = runs ?? throw new ArgumentNullException(nameof(runs));

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, DashboardManualTestStatus>> AllAsync(CancellationToken cancellationToken = default)
    {
        var latest = await _runs.LatestByTemplateAsync(cancellationToken).ConfigureAwait(false);
        return latest.ToDictionary(kv => kv.Key, kv => Map(kv.Value), StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<DashboardManualTestStatus> StatusAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var latest = await _runs.LatestByTemplateAsync(cancellationToken).ConfigureAwait(false);
        return latest.TryGetValue(id, out var status) ? Map(status) : DashboardManualTestStatus.NotTested;
    }

    /// <summary>The dashboard's status for one template's guided result, with a note that says how much of the script it covers and on which build.</summary>
    public static DashboardManualTestStatus Map(Guided.ManualTestStatus status)
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
        var build = status.BuildCommit.Length > 0 ? status.BuildCommit : "unknown";
        var note = $"{status.StepsRecorded} of {status.StepsInScript} worked examples recorded{blocked}; the latest by {status.Tester} on build {build}.";
        return new DashboardManualTestStatus(outcome, status.LastRecordedUtc, note.Length > VerificationSnapshot.MaxTextLength ? note[..VerificationSnapshot.MaxTextLength] : note);
    }
}
