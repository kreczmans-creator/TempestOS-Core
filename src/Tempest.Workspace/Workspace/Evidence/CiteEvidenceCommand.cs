using Tempest.Core.Commands;
using Tempest.Core.Evidence;

namespace Tempest.Workspace.Evidence;

/// <summary>Cites a released reference record against a piece of evidence, pinned to the revision held (<see cref="IEvidenceService.CiteAsync"/>).</summary>
public sealed class CiteEvidenceCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="CiteEvidenceCommand"/> class.</summary>
    public CiteEvidenceCommand(Guid targetObjectId, string targetKind, string library, string recordId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Library = library;
        RecordId = recordId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the library the cited record belongs to (e.g. <c>"Materials"</c>).</summary>
    public string Library { get; }

    /// <summary>Gets the cited record's own identity within that library.</summary>
    public string RecordId { get; }
}

/// <summary>Handles <see cref="CiteEvidenceCommand"/>.</summary>
public sealed class CiteEvidenceCommandHandler : ICommandHandler<CiteEvidenceCommand>
{
    private readonly IEvidenceService _service;

    /// <summary>Initialises a new instance of the <see cref="CiteEvidenceCommandHandler"/> class.</summary>
    public CiteEvidenceCommandHandler(IEvidenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    /// <remarks>An unreleased or unresolvable record is refused, not thrown — reported in the status bar exactly as <see cref="EvidenceCitationResult.Reason"/> names it.</remarks>
    public async Task<CommandResult> HandleAsync(CiteEvidenceCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.CiteAsync(command.TargetObjectId, command.Library, command.RecordId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Cited '{result.Citation!.Pin}'.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The citation was refused.");
    }
}
