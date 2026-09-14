using Tempest.Core.Commands;
using Tempest.Core.Evidence;
using Tempest.Core.ReferenceData;

namespace Tempest.Workspace.Evidence;

/// <summary>Removes a citation from a piece of evidence (<see cref="IEvidenceService.RemoveCitationAsync"/>) — the Citations section's own <b>Remove</b> action (`WP 18.2A`, §4).</summary>
public sealed class RemoveEvidenceCitationCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="RemoveEvidenceCitationCommand"/> class.</summary>
    public RemoveEvidenceCitationCommand(Guid targetObjectId, string targetKind, ReferencePin pin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentNullException.ThrowIfNull(pin);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Pin = pin;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the citation's own pin — the library, record and revision to remove.</summary>
    public ReferencePin Pin { get; }
}

/// <summary>Handles <see cref="RemoveEvidenceCitationCommand"/>.</summary>
public sealed class RemoveEvidenceCitationCommandHandler : ICommandHandler<RemoveEvidenceCitationCommand>
{
    private readonly IEvidenceService _service;

    /// <summary>Initialises a new instance of the <see cref="RemoveEvidenceCitationCommandHandler"/> class.</summary>
    public RemoveEvidenceCitationCommandHandler(IEvidenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RemoveEvidenceCitationCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var evidence = await _service.RemoveCitationAsync(command.TargetObjectId, command.Pin, cancellationToken).ConfigureAwait(false);
            return CommandResult.Success($"Removed citation '{command.Pin}'.", evidence.Id, command.TargetKind);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
