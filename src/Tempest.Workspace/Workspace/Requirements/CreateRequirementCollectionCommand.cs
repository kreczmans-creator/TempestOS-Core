using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Creates a new, empty Requirement Collection (a Requirement Set)
/// (<see cref="IRequirementsService.CreateCollectionAsync"/>). Plain
/// <see cref="ICommand"/>, not <see cref="IWorkspaceCommand"/> — mirrors
/// <c>CreateMechanicalObjectCommand</c>'s own identical reasoning.
/// </summary>
public sealed class CreateRequirementCollectionCommand : ICommand
{
    public CreateRequirementCollectionCommand(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    /// <summary>Gets the new collection's own name.</summary>
    public string Name { get; }
}

/// <summary>Handles <see cref="CreateRequirementCollectionCommand"/>.</summary>
public sealed class CreateRequirementCollectionCommandHandler : ICommandHandler<CreateRequirementCollectionCommand>
{
    private readonly IRequirementsService _requirementsService;

    public CreateRequirementCollectionCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(CreateRequirementCollectionCommand command, CancellationToken cancellationToken)
    {
        var created = await _requirementsService.CreateCollectionAsync(command.Name, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Created Requirement Collection '{created.Name}' ('{created.Id}').");
    }
}
