namespace Tempest.Desktop;

/// <summary>
/// The outcome a Desktop View's <c>ActionCompleted</c> event carries
/// alongside its human-readable message (`TD-58`) — so subscribers can
/// report failures with the right severity and refresh dependent
/// surfaces exactly when the workspace actually changed, instead of
/// unconditionally rebuilding on every message (the `ADR-0104`
/// "report-then-refresh" consolidation, applied to the event convention
/// itself).
/// </summary>
/// <param name="Succeeded">Whether the action succeeded — drives feedback severity.</param>
/// <param name="WorkspaceChanged">Whether the action changed workspace data — drives whether dependent surfaces (Project Explorer, Cockpit, Property Inspector) need refreshing.</param>
public readonly record struct ActionOutcome(bool Succeeded, bool WorkspaceChanged)
{
    /// <summary>A successful action that changed workspace data — report success, refresh dependents.</summary>
    public static ActionOutcome Changed => new(Succeeded: true, WorkspaceChanged: true);

    /// <summary>A successful action that changed nothing (e.g. opening an object for editing) — report success, refresh nothing.</summary>
    public static ActionOutcome NoChange => new(Succeeded: true, WorkspaceChanged: false);

    /// <summary>A failed or refused action — report the failure, refresh nothing (nothing changed).</summary>
    public static ActionOutcome Failed => new(Succeeded: false, WorkspaceChanged: false);

    /// <summary>Maps a command result: success implies the workspace changed; failure implies it did not.</summary>
    public static ActionOutcome From(bool succeeded) => succeeded ? Changed : Failed;
}
