using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

/// <summary>
/// Restores one soft-deleted Requirement (<see cref="IRequirementsService.UndeleteAsync"/>)
/// — the Undo half of <c>requirements.delete</c>'s own compensation, and the
/// Redo half of <c>requirements.create</c>'s (`WP 21.6A`, mirrors
/// <c>EngineeringDomain</c>'s own <c>Undelete*ObjectCommand</c> shape for an
/// engineering object).
/// </summary>
/// <remarks>
/// Never registered as a <see cref="CommandDescriptor"/>: reached only as a
/// compensation, dispatched directly through <see cref="ICommandDispatcher"/>
/// — never a Ribbon- or Palette-visible command, and never invocable by a
/// person choosing a soft-deleted requirement, since none is ever shown in
/// the Project Explorer to choose.
/// </remarks>
public sealed class UndeleteRequirementCommand : IWorkspaceCommand
{
    public UndeleteRequirementCommand(Guid targetObjectId)
    {
        TargetObjectId = targetObjectId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;
}

/// <summary>Handles <see cref="UndeleteRequirementCommand"/>.</summary>
public sealed class UndeleteRequirementCommandHandler : ICommandHandler<UndeleteRequirementCommand>
{
    private readonly IRequirementsService _requirementsService;

    public UndeleteRequirementCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(UndeleteRequirementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var restored = await _requirementsService.UndeleteAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Restored '{restored.Identifier}'.", restored.Id, RequirementsService.RequirementDocumentKind);
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (RequirementNotDeletedException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (RequirementGroupDeletedException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
