namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// One pending decision, as written to
/// <see cref="ReviewDecisionIntakeOptions.IntakeDirectory"/> by
/// <c>agents/tempest-core-agent.*</c> (pulled from Tempest-Dashboard's own
/// pending queue) and consumed by
/// <see cref="ReviewDecisionIntakeHostedService"/> (`ADR-0162`). One file
/// per intent, named <c>&lt;intentId&gt;.json</c> so two concurrently
/// pulled decisions can never collide on a filename.
/// </summary>
/// <param name="SchemaVersion">This record's own payload shape version. Currently always 1.</param>
/// <param name="IntentId">A client-generated id, unique per submission — the filename, and the only thing this service ever needs to tell two decisions apart.</param>
/// <param name="ReviewItemId">The target object's id, matching <c>reviews.json</c>'s own <c>items[].id</c> (`ADR-0157`).</param>
/// <param name="Kind">The target object's own Kind string, matching <c>reviews.json</c>'s own <c>items[].kind</c> — one of <see cref="ReviewQueueExportAdapter.LifecycleKinds"/>'s own <c>Kind</c> values, or <c>Requirement</c>.</param>
/// <param name="Decision">Either <see cref="DecisionApprove"/> or <see cref="DecisionReject"/>. Any other value is refused, never guessed.</param>
/// <param name="RequestedBy">Free text the dashboard's own Settings screen captured — unverified, not a credential (`ADR-0162` Decision 4, `TD-190`).</param>
/// <param name="RequestedAt">When the decision was submitted on the dashboard, for the result log only — never consulted for ordering or staleness here (the dashboard's own "pending" timeout is the dashboard's concern).</param>
public sealed record ReviewDecisionIntent(
    int SchemaVersion,
    Guid IntentId,
    Guid ReviewItemId,
    string Kind,
    string Decision,
    string? RequestedBy,
    DateTimeOffset RequestedAt)
{
    /// <summary>The <see cref="Decision"/> value meaning "approve" — the target transitions to <see cref="Tempest.Core.EngineeringDomain.LifecycleState.Approved"/> (or the family's own equivalent).</summary>
    public const string DecisionApprove = "approve";

    /// <summary>The <see cref="Decision"/> value meaning "reject/return" — the target transitions back to <see cref="Tempest.Core.EngineeringDomain.LifecycleState.Draft"/> (or the family's own equivalent).</summary>
    public const string DecisionReject = "reject";

    /// <summary>The schema version this type currently writes and expects.</summary>
    public const int CurrentSchemaVersion = 1;
}

/// <summary>
/// The outcome <see cref="ReviewDecisionIntakeHostedService"/> records for
/// one processed <see cref="ReviewDecisionIntent"/> — written alongside the
/// consumed intent file as <c>&lt;intentId&gt;.result.json</c>, for anyone
/// reading the intake directory by hand to see what happened without
/// reconstructing it from logs.
/// </summary>
/// <param name="IntentId">Matches the intent this is the outcome of.</param>
/// <param name="Succeeded">Whether the target command dispatched and returned success.</param>
/// <param name="Message">The command's own <c>CommandResult.Message</c> on success, or a plain-words reason on failure — never a raw exception's <c>.ToString()</c>.</param>
/// <param name="ProcessedAt">When this service finished handling the intent.</param>
public sealed record ReviewDecisionOutcome(Guid IntentId, bool Succeeded, string Message, DateTimeOffset ProcessedAt);
