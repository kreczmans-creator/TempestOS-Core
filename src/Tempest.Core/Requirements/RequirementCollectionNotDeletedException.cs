namespace Tempest.Core.Requirements;

/// <summary>Thrown by <see cref="IRequirementsService.UndeleteCollectionAsync"/> when the collection is not currently deleted (`v1.0.0` RC, mirrors <see cref="RequirementNotDeletedException"/>).</summary>
public sealed class RequirementCollectionNotDeletedException : RequirementsException
{
    /// <summary>The collection Id that is not currently deleted.</summary>
    public Guid CollectionId { get; }

    /// <summary>Initialises a new instance of the <see cref="RequirementCollectionNotDeletedException"/> class.</summary>
    public RequirementCollectionNotDeletedException(Guid collectionId)
        : base($"Requirement collection '{collectionId}' is not deleted, so it cannot be restored.")
    {
        CollectionId = collectionId;
    }
}
