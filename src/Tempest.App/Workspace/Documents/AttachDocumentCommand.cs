using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Documents;

/// <summary>
/// Attaches a new <see cref="IAttachment"/> to one Document Domain object
/// (<see cref="IHasAttachments.AttachAsync"/>) — this Work Package's own
/// realisation of the "Attachments" management capability its own scope
/// names. The one genuinely new command this Work Package introduces:
/// <see cref="IHasAttachments"/> has existed since `WP 8.2C`, but no
/// Workspace command anywhere has wrapped it until now — every other
/// Document Management verb this Work Package's own scope names already had
/// a direct Calculations/Mechanical precedent to mirror.
/// </summary>
public sealed class AttachDocumentCommand : IWorkspaceCommand
{
    public AttachDocumentCommand(Guid targetObjectId, string targetKind, string fileName, string contentType, long sizeInBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        FileName = fileName;
        ContentType = contentType;
        SizeInBytes = sizeInBytes;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the attachment's own file name.</summary>
    public string FileName { get; }

    /// <summary>Gets the attachment's own content (MIME) type.</summary>
    public string ContentType { get; }

    /// <summary>Gets the attachment's own size, in bytes.</summary>
    public long SizeInBytes { get; }
}

/// <summary>Handles <see cref="AttachDocumentCommand"/>.</summary>
public sealed class AttachDocumentCommandHandler : ICommandHandler<AttachDocumentCommand>
{
    private readonly EngineeringDomainContext _context;

    public AttachDocumentCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(AttachDocumentCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasAttachments attachable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot carry attachments.");

        var attachment = new Attachment(command.FileName, command.ContentType, command.SizeInBytes);
        await attachable.AttachAsync(attachment, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Attached '{command.FileName}' to '{command.TargetObjectId}'.");
    }
}
