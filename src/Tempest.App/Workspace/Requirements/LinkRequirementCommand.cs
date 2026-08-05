using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Records a typed, directed relationship from one Requirement to any
/// other document (<see cref="IRequirementsService.LinkAsync"/>) — one
/// generic command covering Allocation/DependsOn/DerivesFrom/References/
/// Satisfies all at once, since the underlying Domain method is already
/// generic over <paramref name="relationshipKind"/> parameter; never one
/// command per relationship kind.
/// </summary>
public sealed class LinkRequirementCommand : IWorkspaceCommand
{
    public LinkRequirementCommand(Guid targetObjectId, Guid targetDocumentId, string relationshipKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relationshipKind);

        TargetObjectId = targetObjectId;
        TargetDocumentId = targetDocumentId;
        RelationshipKind = relationshipKind;
    }

    /// <inheritdoc />
    /// <remarks>The relationship's own source requirement.</remarks>
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the relationship's own target document — another requirement, a group, a collection, an allocated engineering object, or any other document.</summary>
    public Guid TargetDocumentId { get; }

    /// <summary>Gets the relationship's own kind — typically one of <see cref="RequirementRelationshipKinds"/>, though any non-blank string is accepted (`ADR-0073`).</summary>
    public string RelationshipKind { get; }
}

/// <summary>Handles <see cref="LinkRequirementCommand"/>.</summary>
public sealed class LinkRequirementCommandHandler : ICommandHandler<LinkRequirementCommand>
{
    private readonly IRequirementsService _requirementsService;

    public LinkRequirementCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(LinkRequirementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await _requirementsService.LinkAsync(command.TargetObjectId, command.TargetDocumentId, command.RelationshipKind, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Linked '{command.TargetObjectId}' --[{command.RelationshipKind}]--> '{command.TargetDocumentId}'.");
    }
}
