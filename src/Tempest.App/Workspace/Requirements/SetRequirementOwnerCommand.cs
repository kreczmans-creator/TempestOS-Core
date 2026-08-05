using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>Sets one Requirement's own current owner (<see cref="IRequirementsService.SetOwnerAsync"/>).</summary>
public sealed class SetRequirementOwnerCommand : IWorkspaceCommand
{
    public SetRequirementOwnerCommand(Guid targetObjectId, string? owner)
    {
        TargetObjectId = targetObjectId;
        Owner = owner;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the requirement's own new owner, or <see langword="null"/> to clear it.</summary>
    public string? Owner { get; }
}

/// <summary>Handles <see cref="SetRequirementOwnerCommand"/>.</summary>
public sealed class SetRequirementOwnerCommandHandler : ICommandHandler<SetRequirementOwnerCommand>
{
    private readonly IRequirementsService _requirementsService;

    public SetRequirementOwnerCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(SetRequirementOwnerCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _requirementsService.SetOwnerAsync(command.TargetObjectId, command.Owner, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Owner set to '{updated.Owner ?? "(none)"}' for '{updated.Identifier}'.");
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
