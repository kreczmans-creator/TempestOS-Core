using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Creates a new Requirement Group, optionally nested under an existing
/// parent group (<see cref="IRequirementsService.CreateGroupAsync"/>).
/// Plain <see cref="ICommand"/>, not <see cref="IWorkspaceCommand"/> —
/// mirrors <c>CreateMechanicalObjectCommand</c>'s own identical reasoning.
/// </summary>
public sealed class CreateRequirementGroupCommand : ICommand
{
    public CreateRequirementGroupCommand(string name, Guid? parentGroupId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        ParentGroupId = parentGroupId;
    }

    /// <summary>Gets the new group's own name.</summary>
    public string Name { get; }

    /// <summary>Gets the new group's own parent group, or <see langword="null"/> for a root group.</summary>
    public Guid? ParentGroupId { get; }
}

/// <summary>Handles <see cref="CreateRequirementGroupCommand"/>.</summary>
public sealed class CreateRequirementGroupCommandHandler : ICommandHandler<CreateRequirementGroupCommand>
{
    private readonly IRequirementsService _requirementsService;

    public CreateRequirementGroupCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(CreateRequirementGroupCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _requirementsService.CreateGroupAsync(command.Name, command.ParentGroupId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Created Requirement Group '{created.Name}' ('{created.Id}').");
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
