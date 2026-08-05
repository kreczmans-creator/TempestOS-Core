namespace Tempest.Core.Requirements;

/// <summary>
/// The plain, JSON-serializable shape a requirement group is stored as.
/// </summary>
/// <remarks>
/// <b>Disclosed `WP 9.1A` correction:</b> this record originally carried
/// no parent reference at all — the hierarchy was recorded entirely
/// through <see cref="EngineeringData.IEngineeringDocumentStore.LinkAsync"/>
/// (<see cref="RequirementRelationshipKinds.GroupedUnder"/>), resolved by
/// <c>RequirementsService.FindGroupAsync</c> via
/// <c>.FirstOrDefault()</c> over <c>GetReferencesAsync</c>'s own returned
/// list. That resolution was never actually ambiguous before this Work
/// Package, because nothing ever recorded a second <c>groupedUnder</c>
/// link for the same group — but `WP 9.1A`'s own <c>MoveGroupAsync</c>
/// needs to, and <see cref="Persistence.IPersistenceStore"/>'s own key
/// ordering carries no guarantee (the identical class of risk `WP 9.0B`'s
/// own `TD-27` already found and fixed for a different repository).
/// <see cref="ParentGroupId"/> is now this record's own live, current
/// value, exactly mirroring how every other mutation
/// (<c>RequirementDto.Status</c>, and now <c>Owner</c>/<c>Priority</c>)
/// already stores its own current value directly. The <c>groupedUnder</c>
/// relationship link is still recorded on every create/move, for Digital
/// Thread compatibility — it stops being the *resolution* mechanism,
/// without stopping being part of the historical record.
/// </remarks>
internal sealed record RequirementGroupDto(string Name, Guid? ParentGroupId = null, bool IsDeleted = false);
