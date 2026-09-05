namespace Tempest.Core.Requirements;

/// <summary>
/// The reconcile/repair path `TD-67`'s own register entry names as
/// missing: <see cref="RequirementsService.CreateAsync"/>,
/// <see cref="RequirementsService.CreateCollectionAsync"/> and
/// <see cref="RequirementsService.CreateGroupAsync"/> each write their own
/// backing <see cref="EngineeringData.IEngineeringDocument"/> before
/// registering it in this service's own identifier index or registry —
/// a crash, or an index/registry write failure, between the two leaves a
/// document nothing can find through the normal read paths
/// (<see cref="RequirementsService.FindByIdentifierAsync"/>,
/// <see cref="RequirementsService.ListAsync"/>,
/// <see cref="RequirementsService.ListCollectionsAsync"/>,
/// <see cref="RequirementsService.ListGroupsAsync"/>), yet the document
/// and its revision keep consuming storage forever.
/// </summary>
/// <remarks>
/// <para>
/// <b>An explicit, operator-invoked repair, never an automatic one.</b>
/// Neither method runs at startup or on any other schedule — this
/// platform does not repair a user's own data behind their back. A
/// caller (an admin command, a diagnostics page, a test) decides when to
/// look and when to act.
/// </para>
/// <para>
/// <b>What "repair" means here.</b> A missing index/registry entry is
/// re-written from the orphan document's own recorded identity — the
/// exact entry <see cref="RequirementsService.CreateAsync"/>/
/// <see cref="RequirementsService.CreateCollectionAsync"/>/
/// <see cref="RequirementsService.CreateGroupAsync"/> would have written,
/// never inferred or guessed. If a genuine identifier collision exists
/// (the index already names a <em>different</em> document under the same
/// identifier), the entry is left alone and the finding is reported
/// unrepaired — overwriting it would silently reassign an identifier
/// away from whichever document is actually current. A stale index/
/// registry entry (naming a document that no longer exists, or is no
/// longer of the expected Kind) is removed — this platform's own read
/// paths already tolerate one silently (<see cref="RequirementsService.ListAsync"/>'s
/// own disclosed stale-entry skip), so removing it changes nothing a
/// caller could observe except closing the account for good.
/// </para>
/// </remarks>
public interface IRequirementsReconciliationService
{
    /// <summary>Scans every Requirement, Requirement Collection and Requirement Group without changing anything.</summary>
    Task<RequirementsReconciliationReport> DetectAsync(CancellationToken cancellationToken = default);

    /// <summary>Repeats <see cref="DetectAsync"/>'s own scan and repairs every finding it can — see this type's own remarks for exactly what "repair" does and does not do.</summary>
    Task<RequirementsReconciliationReport> SweepAsync(CancellationToken cancellationToken = default);
}
