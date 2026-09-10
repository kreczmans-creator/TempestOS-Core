using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Workspace.Projects;

/// <summary>
/// An immutable snapshot of one project, as the product shell needs to
/// show it — identity, name, lifecycle state and structural parent.
/// </summary>
/// <remarks>
/// <para>
/// <b>A projection of the real domain object, never a second model.</b>
/// The project itself is the existing <see cref="IProject"/> engineering
/// object (`WP 8.2C`'s own `Portfolio → Programme → Project` hierarchy),
/// carrying real lifecycle, relationships, revisions, traceability and
/// principal attribution. This record is the read-side shape the shell
/// renders, produced by <see cref="IProjectDirectory"/> — exactly the
/// "descriptor and snapshot type" convention this codebase already
/// applies to <c>CommandDescriptor</c>, <c>NavigationItem</c> and
/// <c>ProjectExplorerNode</c>.
/// </para>
/// <para>
/// Deliberately <b>not</b> the pre-platform <c>Tempest.Core.Models.ProjectModel</c>
/// (`TD-01`'s own bootstrap-era cluster): that type is a mutable POCO with
/// denormalised counts, persisted by a service that news up its own
/// repository and writes folders straight to disk, bypassing
/// <see cref="Tempest.Core.Persistence.IPersistenceStore"/>, the audit
/// framework, revisions and lifecycle. Its useful *concepts* (a
/// human-readable project identifier, customer, owner) are carried
/// forward here and in project metadata; its implementation is not
/// reused, and is retired rather than wired in.
/// </para>
/// </remarks>
/// <param name="Id">The project's own engineering-object Id — the identity every other surface keys off.</param>
/// <param name="Identifier">The human-readable project identifier (for example <c>P-0027</c>), or <see langword="null"/> if unset.</param>
/// <param name="DisplayName">The project's own display name.</param>
/// <param name="Status">The project's own current lifecycle state.</param>
/// <param name="ProgrammeId">The owning programme, or <see langword="null"/> for a standalone project.</param>
/// <param name="ClientOrganisationId">The client's own record id in the Organisation catalogue (`WP 19.0A`, `ADR-0150`). <see langword="null"/> if unset.</param>
/// <param name="PurchaseOrderReference">The client's own purchase-order reference. <see langword="null"/> if unset.</param>
/// <param name="Budget">The project's own budget. <see langword="null"/> if unset.</param>
/// <param name="RateCardPin">The Released rate card this project bills against, pinned to the revision read. <see langword="null"/> if none is pinned.</param>
/// <param name="StartDate">When the project starts. <see langword="null"/> if unset.</param>
/// <param name="TargetDate">When the project is targeted to complete. <see langword="null"/> if unset.</param>
/// <param name="ProjectManagerIdentityId">The identity id of the principal managing this project. <see langword="null"/> if unset.</param>
public sealed record ProjectSummary(
    Guid Id,
    string? Identifier,
    string DisplayName,
    LifecycleState Status,
    Guid? ProgrammeId,
    string? ClientOrganisationId = null,
    string? PurchaseOrderReference = null,
    Money? Budget = null,
    ReferencePin? RateCardPin = null,
    DateOnly? StartDate = null,
    DateOnly? TargetDate = null,
    string? ProjectManagerIdentityId = null)
{
    /// <summary>The label a shell surface shows for this project — the identifier and name together when both exist, mirroring the mock-ups' own "P-0027 Apollo Pump Redesign" form.</summary>
    public string Label => string.IsNullOrWhiteSpace(Identifier) ? DisplayName : $"{Identifier} {DisplayName}";
}
