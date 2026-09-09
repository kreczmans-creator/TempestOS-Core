using Tempest.Core.Commands;
using Tempest.Core.Evidence;

namespace Tempest.Workspace.Evidence;

/// <summary>
/// Retags a piece of evidence to a Part, Assembly, Requirement or
/// Deliverable — or clears the tag (<see cref="IEvidenceService.SetSubjectAsync"/>)
/// (`WP 18.2B`, closing a gap `WP 18.2A` disclosed: the physical review's
/// own step E7 tags a record to a Part <em>after</em> creating it).
/// </summary>
public sealed class SetEvidenceSubjectCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetEvidenceSubjectCommand"/> class.</summary>
    public SetEvidenceSubjectCommand(Guid targetObjectId, string targetKind, Guid? subjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        SubjectId = subjectId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the subject to tag this evidence to, or <see langword="null"/> to clear the tag.</summary>
    public Guid? SubjectId { get; }
}

/// <summary>Handles <see cref="SetEvidenceSubjectCommand"/>.</summary>
public sealed class SetEvidenceSubjectCommandHandler : ICommandHandler<SetEvidenceSubjectCommand>
{
    private readonly IEvidenceService _service;

    /// <summary>Initialises a new instance of the <see cref="SetEvidenceSubjectCommandHandler"/> class.</summary>
    public SetEvidenceSubjectCommandHandler(IEvidenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    /// <remarks>Refused, not thrown, once the evidence is <see cref="EvidenceStatus.Issued"/>.</remarks>
    public async Task<CommandResult> HandleAsync(SetEvidenceSubjectCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.SetSubjectAsync(command.TargetObjectId, command.SubjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success(command.SubjectId is { } id ? $"Subject set to '{id:N}'." : "Subject cleared.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The subject change was refused.");
    }
}
