using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Evidence;

/// <summary>
/// The typed view of <see cref="Evidence"/> a caller who knows it holds
/// evidence (not just any <see cref="IEngineeringObject"/>) can read
/// directly (`ADR-0148`) — mirrors
/// <c>Tempest.Core.EngineeringDomain.IPart</c>'s own shape, composing
/// every facet <see cref="Evidence"/> inherits from
/// <c>EngineeringObjectBase</c> that this Kind actually means, plus its own
/// declared state.
/// </summary>
/// <remarks>
/// Deliberately does not compose <c>IHasMetadata</c> or
/// <c>IHasLifecycle</c>: both declare a member (<c>Classification</c>,
/// <c>Status</c>) this Kind specialises to its own, differently-typed
/// meaning (<see cref="Classification"/>, <see cref="Status"/>) rather than
/// the generic one, exactly as <see cref="Evidence"/>'s own <c>new</c>
/// properties do. <c>Evidence</c> still structurally implements both —
/// every fact either facet carries remains reachable by casting to it
/// directly, the same way any other generic facet is.
/// </remarks>
public interface IEvidenceRecord :
    IEngineeringObject, IHasBusinessIdentifier, IHasRevisions, IHasRelationships, ITraceable,
    IValidatable, IHasAttachments, IRenamable, IHasParent, IDeletable
{
    /// <summary>What kind of engineering record this is.</summary>
    EvidenceClassification Classification { get; }

    /// <summary>The Part, Assembly, Requirement or Deliverable this evidence is about, by id — a tag, never validated as a structure. <see langword="null"/> if none was given.</summary>
    Guid? SubjectId { get; }

    /// <summary>The principal who created this evidence — stable across revisions, so a later <c>Revise</c> cannot change who the independence rule holds responsible for the original work.</summary>
    string AuthorIdentityId { get; }

    /// <summary>The governed reference records this evidence stood on, each pinned to the revision held at citation time.</summary>
    IReadOnlyList<EvidenceCitation> Citations { get; }

    /// <summary>The named, typed figures declared against this evidence.</summary>
    IReadOnlyList<DeclaredFigure> DeclaredFigures { get; }

    /// <summary>This evidence's own lifecycle position.</summary>
    EvidenceStatus Status { get; }

    /// <summary>The most recent check recorded against this evidence, or <see langword="null"/> if none has been.</summary>
    CheckRecord? Check { get; }

    /// <summary>The issue recorded against this evidence, or <see langword="null"/> if it has not been issued.</summary>
    IssueRecord? Issue { get; }
}
