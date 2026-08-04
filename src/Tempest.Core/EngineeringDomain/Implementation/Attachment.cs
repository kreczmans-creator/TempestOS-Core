namespace Tempest.Core.EngineeringDomain;

public sealed class Attachment : IAttachment
{
    public Guid Id { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public long SizeInBytes { get; }

    public Attachment(string fileName, string contentType, long sizeInBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        Id = Guid.NewGuid();
        FileName = fileName;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
    }
}
