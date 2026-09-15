using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

/// <summary>Sets one Requirement's own current owner (<see cref="IRequirementsService.SetOwnerAsync"/>).</summary>
public sealed class SetRequirementOwnerCommand : IWorkspaceCommand
{
    public SetRequirementOwnerCommand(Guid targetObjectId, string? owner, string? ownerPersonId = null)
    {
        TargetObjectId = targetObjectId;
        Owner = owner;
        OwnerPersonId = ownerPersonId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the requirement's own new owner, or <see langword="null"/> to clear it.</summary>
    public string? Owner { get; }

    /// <summary>Gets the <see cref="Tempest.Core.People.IPersonCatalog"/> record id <see cref="Owner"/> was picked from (`WP 20.10F`), or <see langword="null"/> where it was typed rather than picked.</summary>
    public string? OwnerPersonId { get; }
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
            var updated = await _requirementsService.SetOwnerAsync(command.TargetObjectId, command.Owner, command.OwnerPersonId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Owner set to '{updated.Owner ?? "(none)"}' for '{updated.Identifier}'.");
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
