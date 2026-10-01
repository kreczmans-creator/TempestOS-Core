using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

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
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="requirementsService">Where the group is created.</param>
    /// <param name="dispatcher">Dispatches this create's own compensation (`v1.0.0` RC) — optional.</param>
    public CreateRequirementGroupCommandHandler(IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(CreateRequirementGroupCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _requirementsService.CreateGroupAsync(command.Name, command.ParentGroupId, cancellationToken).ConfigureAwait(false);

            // Undo soft-deletes the group this call created (refused, with the
            // reason, if it has since gained live children); redo restores it —
            // the same inversion `CreateRequirementCommandHandler` uses.
            var compensation = _dispatcher is null ? null : new CommandCompensation(
                $"Create group '{created.Name}'",
                undo: ct => _dispatcher.DispatchAsync(new DeleteRequirementGroupCommand(created.Id), ct),
                redo: ct => _dispatcher.DispatchAsync(new UndeleteRequirementGroupCommand(created.Id), ct));

            return CommandResult.Success($"Created Requirement Group '{created.Name}'.", created.Id, RequirementsService.RequirementGroupDocumentKind, compensation);
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
