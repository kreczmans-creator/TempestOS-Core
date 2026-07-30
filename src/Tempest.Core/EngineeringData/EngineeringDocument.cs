namespace Tempest.Core.EngineeringData;

/// <summary>
/// The concrete <see cref="IEngineeringDocument"/> implementation — an
/// immutable snapshot of a document's own identity and current-revision
/// pointer at the moment it was read or created.
/// </summary>
internal sealed class EngineeringDocument : IEngineeringDocument
{
    public EngineeringDocument(Guid id, string kind, int currentRevisionNumber, DateTimeOffset createdAt)
    {
        Id = id;
        Kind = kind;
        CurrentRevisionNumber = currentRevisionNumber;
        CreatedAt = createdAt;
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public int CurrentRevisionNumber { get; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAt { get; }
}
