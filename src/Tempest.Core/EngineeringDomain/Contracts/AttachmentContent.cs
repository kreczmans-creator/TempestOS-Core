namespace Tempest.Core.EngineeringDomain;

/// <summary>
/// Why a read of an attachment's content did not produce bytes — or that
/// it did (`TD-31`).
/// </summary>
public enum AttachmentContentStatus
{
    /// <summary>The content was found and matched the metadata recorded for it.</summary>
    Available,

    /// <summary>
    /// No content record exists. Either the attachment predates `TD-31`
    /// and only ever carried metadata, or its content has since been
    /// removed. Not an error: an attachment is allowed to describe a file
    /// this platform does not hold.
    /// </summary>
    Missing,

    /// <summary>
    /// A content record exists but does not match the metadata that
    /// describes it — a different length, or a different hash. The bytes
    /// are not returned.
    /// </summary>
    Corrupt,
}

/// <summary>
/// The outcome of reading one attachment's stored bytes (`TD-31`).
/// </summary>
/// <remarks>
/// <para>
/// A three-valued result rather than a nullable <c>byte[]</c>, because
/// "we never held this file" and "we held it and what came back is not it"
/// are different facts, and a caller that cannot tell them apart will
/// eventually present the second as the first. The Document Viewer
/// (`TD-80`) has to say "no content stored" and "this attachment is
/// damaged" differently, and the distinction has to survive the read to
/// be sayable at all.
/// </para>
/// <para>
/// <see cref="Bytes"/> is non-empty only for <see cref="AttachmentContentStatus.Available"/>.
/// Corrupt content is deliberately not handed back: bytes that failed
/// their own integrity check are not evidence of anything, and returning
/// them alongside a flag invites exactly the caller that ignores the flag.
/// </para>
/// </remarks>
public sealed class AttachmentContentResult
{
    private static readonly byte[] Empty = [];

    private AttachmentContentResult(AttachmentContentStatus status, byte[] bytes)
    {
        Status = status;
        Bytes = bytes;
    }

    /// <summary>Gets what happened.</summary>
    public AttachmentContentStatus Status { get; }

    /// <summary>Gets the stored bytes — empty unless <see cref="Status"/> is <see cref="AttachmentContentStatus.Available"/>.</summary>
    public byte[] Bytes { get; }

    /// <summary>Gets whether the content was found intact.</summary>
    public bool IsAvailable => Status is AttachmentContentStatus.Available;

    /// <summary>The content was found and verified.</summary>
    public static AttachmentContentResult Available(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        return new AttachmentContentResult(AttachmentContentStatus.Available, bytes);
    }

    /// <summary>No content is stored for this attachment.</summary>
    public static AttachmentContentResult Missing() => new(AttachmentContentStatus.Missing, Empty);

    /// <summary>Content is stored but does not match the metadata describing it.</summary>
    public static AttachmentContentResult Corrupt() => new(AttachmentContentStatus.Corrupt, Empty);
}
