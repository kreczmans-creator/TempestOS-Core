namespace Tempest.Core.Projects;

/// <summary>
/// One project sign-off: who, when and what was said (`WP 19.5C`) —
/// mirrors <c>Tempest.Core.Evidence.CheckRecord</c>/<c>IssueRecord</c>'s
/// own shape, carried as a single field on <see cref="Tempest.Core.EngineeringDomain.Project"/>
/// (<c>WriteJson</c>/<c>TypeJson</c>) rather than a Kind of its own — a
/// sign-off has no independent lifecycle, no citation, nothing else a
/// caller would ever look up by id; it is one fact recorded once, at the
/// moment the project closes.
/// </summary>
/// <param name="PrincipalId">Who signed off — the acting principal at the moment <see cref="IProjectLifecycleService.SignOffAsync"/> ran, never a name typed by hand.</param>
/// <param name="SignedOn">The date this sign-off was recorded — also the date the project's own <see cref="Tempest.Core.EngineeringDomain.Project.ClosedOn"/> is set to.</param>
/// <param name="Statement">The sign-off statement, verbatim.</param>
public sealed record ProjectSignOff(string PrincipalId, DateOnly SignedOn, string Statement);
