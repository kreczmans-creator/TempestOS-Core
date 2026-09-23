using Tempest.Core.BusinessGovernance;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringDomain;

public interface IPortfolio : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    IReadOnlyList<Guid> ProgrammeIds { get; }
}

public interface IProgramme : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships
{
    Guid? PortfolioId { get; }
    IReadOnlyList<Guid> ProjectIds { get; }
}

/// <summary>
/// A project — gains its commercial core in `WP 19.0A` (`ADR-0150`): who
/// it is for, what it is bounded by, and what it bills against. Every
/// commercial field is optional and read-only here; each is written by
/// its own <c>ProjectCommercialService</c> act, one transaction with an
/// audit row, mirroring how <c>Evidence</c>'s own fields are written.
/// </summary>
public interface IProject : IEngineeringObject, IHasBusinessIdentifier, IHasMetadata, IHasLifecycle, IHasRelationships, ITraceable, IRenamable, IHasParent, IDeletable
{
    Guid? ProgrammeId { get; }

    /// <summary>The client's own record id in the Organisation catalogue — a tag, never validated as a structure (mirrors <c>Evidence.SubjectId</c>). <see langword="null"/> if unset.</summary>
    string? ClientOrganisationId { get; }

    /// <summary>The client's own purchase-order reference. <see langword="null"/> if unset.</summary>
    string? PurchaseOrderReference { get; }

    /// <summary>The project's own budget. <see langword="null"/> if unset.</summary>
    Money? Budget { get; }

    /// <summary>The Released rate card this project bills against, pinned to the revision read (`ADR-0150`). <see langword="null"/> if none is pinned.</summary>
    ReferencePin? RateCardPin { get; }

    /// <summary>When the project starts. <see langword="null"/> if unset.</summary>
    DateOnly? StartDate { get; }

    /// <summary>When the project is targeted to complete. <see langword="null"/> if unset.</summary>
    DateOnly? TargetDate { get; }

    /// <summary>The identity id of the principal managing this project. <see langword="null"/> if unset.</summary>
    string? ProjectManagerIdentityId { get; }
}
