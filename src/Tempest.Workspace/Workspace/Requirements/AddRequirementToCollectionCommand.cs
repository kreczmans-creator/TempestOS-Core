using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Adds one Requirement to an existing Requirement Collection
/// (<see cref="IRequirementsService.AddToCollectionAsync"/>). There is no
/// symmetric "remove from collection" command — <see cref="EngineeringData.IEngineeringDocumentStore"/>
/// has no unlink primitive, and adding one purely for this command's own
/// symmetry would fake a removal that would not actually work; a genuine,
/// disclosed scope reduction from this Work Package's own plan (see
/// `WP9.1A Implementation Report.md`).
/// </summary>
public sealed class AddRequirementToCollectionCommand : IWorkspaceCommand
{
    public AddRequirementToCollectionCommand(Guid targetObjectId, Guid collectionId)
    {
        TargetObjectId = targetObjectId;
        CollectionId = collectionId;
    }

    /// <inheritdoc />
    /// <remarks>The requirement being added — not the collection.</remarks>
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the collection to add <see cref="TargetObjectId"/> to.</summary>
    public Guid CollectionId { get; }
}

/// <summary>Handles <see cref="AddRequirementToCollectionCommand"/>.</summary>
public sealed class AddRequirementToCollectionCommandHandler : ICommandHandler<AddRequirementToCollectionCommand>
{
    private readonly IRequirementsService _requirementsService;

    public AddRequirementToCollectionCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(AddRequirementToCollectionCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await _requirementsService.AddToCollectionAsync(command.CollectionId, command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Added '{command.TargetObjectId}' to collection '{command.CollectionId}'.");
    }
}
