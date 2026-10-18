using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

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
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="requirementsService">Where the collection is created.</param>
    /// <param name="dispatcher">Dispatches this create's own compensation (`v1.0.0` RC) — optional.</param>
    public CreateRequirementCollectionCommandHandler(IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(CreateRequirementCollectionCommand command, CancellationToken cancellationToken)
    {
        var created = await _requirementsService.CreateCollectionAsync(command.Name, cancellationToken).ConfigureAwait(false);

        // Undo soft-deletes the collection this call created; redo restores it.
        var compensation = _dispatcher is null ? null : new CommandCompensation(
            $"Create collection '{created.Name}'",
            undo: ct => _dispatcher.DispatchAsync(new DeleteRequirementCollectionCommand(created.Id), ct),
            redo: ct => _dispatcher.DispatchAsync(new UndeleteRequirementCollectionCommand(created.Id), ct));

        return CommandResult.Success($"Created Requirement Collection '{created.Name}'.", created.Id, RequirementsService.RequirementCollectionDocumentKind, compensation);
    }
}
