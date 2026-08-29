namespace Tempest.Core.EngineeringDomain;

public sealed class Attachment : IAttachment
{
    public Guid Id { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long SizeInBytes { get; }

    /// <inheritdoc />
    public string? ContentHash { get; }

    public Attachment(string fileName, string contentType, long sizeInBytes)
        : this(Guid.NewGuid(), fileName, contentType, sizeInBytes, contentHash: null)
    {
    }

    /// <summary>
    /// A new attachment whose bytes this platform holds (`TD-31`) — the
    /// size and hash both describe the content actually stored, so neither
    /// can be asserted independently of it.
    /// </summary>
    public Attachment(string fileName, string contentType, long sizeInBytes, string contentHash)
        : this(Guid.NewGuid(), fileName, contentType, sizeInBytes, contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
    }

    /// <summary>
    /// Reconstructs an attachment that already has an identity (`TD-85`) —
    /// so an attachment recorded before a restart comes back as the same
    /// attachment, not a new one wearing the same file name.
    /// </summary>
    internal Attachment(Guid id, string fileName, string contentType, long sizeInBytes, string? contentHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        Id = id;
        FileName = fileName;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
        ContentHash = contentHash;
    }
}
