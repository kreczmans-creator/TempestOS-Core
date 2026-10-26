using Guided = Tempest.Calc.Library.ManualTesting;

namespace Tempest.Calc.Library.Dashboard;

/// <summary>
/// Addendum C integration (C-4 with C-5): the dashboard's "Manual test" column from the guided test's run records. C-5's
/// <see cref="Guided.IManualTestStatusSource"/> gives, per worksheet template, the worst of the latest result of each of its worked examples
/// (Fail over Blocked over Pass) over the finished runs of the current script; this adapter turns that into the dashboard's status. It only
/// reads: it holds the read-only interface, never the run service.
/// </summary>
/// <remarks>
/// The mapping: Fail is <see cref="ManualTestOutcome.Failed"/>; Pass on every worked example the script holds for the template is
/// <see cref="ManualTestOutcome.Passed"/>; Pass on only some of them, or Blocked (a step that could not be tried), is
/// <see cref="ManualTestOutcome.Partial"/>; a template with no result is <see cref="ManualTestOutcome.NotTested"/>. The guided test records chains
/// as script steps, not per pack template, so a chain row reads "not tested".
/// </remarks>
public sealed class GuidedManualTestStatusSource : IBulkManualTestStatusSource
{
    private readonly Guided.IManualTestStatusSource _runs;

    public GuidedManualTestStatusSource(Guided.IManualTestStatusSource runs) => _runs = runs ?? throw new ArgumentNullException(nameof(runs));

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, ManualTestStatus>> AllAsync(CancellationToken cancellationToken = default)
    {
        var latest = await _runs.LatestByTemplateAsync(cancellationToken).ConfigureAwait(false);
        return latest.ToDictionary(kv => kv.Key, kv => Map(kv.Value), StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public async Task<ManualTestStatus> StatusAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        var latest = await _runs.LatestByTemplateAsync(cancellationToken).ConfigureAwait(false);
        return latest.TryGetValue(id, out var status) ? Map(status) : ManualTestStatus.NotTested;
    }

    /// <summary>The dashboard's status for one template's guided result, with a note that says how much of the script it covers and on which build.</summary>
    public static ManualTestStatus Map(Guided.ManualTestStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var complete = status.StepsInScript > 0 && status.StepsRecorded >= status.StepsInScript;
        var outcome = status.Outcome switch
        {
            Guided.ManualTestOutcome.Fail => ManualTestOutcome.Failed,
            Guided.ManualTestOutcome.Pass when complete => ManualTestOutcome.Passed,
            _ => ManualTestOutcome.Partial,
        };
        var blocked = status.Outcome == Guided.ManualTestOutcome.Blocked ? ", one or more could not be tried" : string.Empty;
        var build = status.BuildCommit.Length > 0 ? status.BuildCommit : "unknown";
        var note = $"{status.StepsRecorded} of {status.StepsInScript} worked examples recorded{blocked}; the latest by {status.Tester} on build {build}.";
        return new ManualTestStatus(outcome, status.LastRecordedUtc, note.Length > VerificationSnapshot.MaxTextLength ? note[..VerificationSnapshot.MaxTextLength] : note);
    }
}
