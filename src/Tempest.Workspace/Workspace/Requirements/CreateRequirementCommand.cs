using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

/// <summary>
/// Creates a new Requirement. Plain <see cref="ICommand"/>, not
/// <see cref="IWorkspaceCommand"/> — mirrors <c>CreateMechanicalObjectCommand</c>'s
/// own identical reasoning: there is no pre-existing target object/view to
/// refresh, only a new one to be navigated to once created.
/// </summary>
public sealed class CreateRequirementCommand : ICommand
{
    public CreateRequirementCommand(string identifier, string statement, string? category = null, Guid? groupId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);

        Identifier = identifier;
        Statement = statement;
        Category = category;
        GroupId = groupId;
    }

    /// <summary>The group the new requirement is placed in, or <see langword="null"/> for none (`WP 17.9.3`: the selected group, when one is selected).</summary>
    public Guid? GroupId { get; }

    /// <summary>Gets the new requirement's own business identifier.</summary>
    public string Identifier { get; }

    /// <summary>Gets the new requirement's own statement.</summary>
    public string Statement { get; }

    /// <summary>Gets the new requirement's own classification, or <see langword="null"/> to leave it uncategorised.</summary>
    public string? Category { get; }
}

/// <summary>Handles <see cref="CreateRequirementCommand"/>.</summary>
public sealed class CreateRequirementCommandHandler : ICommandHandler<CreateRequirementCommand>
{
    private readonly IRequirementsService _requirementsService;
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="requirementsService">Where the requirement is created.</param>
    /// <param name="dispatcher">
    /// Dispatches this create's own compensation (`WP 21.6A`, mirrors
    /// <c>EngineeringDomain</c>'s own <c>Create*ObjectCommandHandler</c>
    /// shape) — optional; <see langword="null"/> means no
    /// <see cref="CommandResult.Compensation"/> is attached.
    /// </param>
    public CreateRequirementCommandHandler(IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(CreateRequirementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _requirementsService.CreateAsync(command.Identifier, command.Statement, command.Category, cancellationToken)
                .ConfigureAwait(false);

            // Undo soft-deletes the requirement this call created; redo
            // restores it — the same make-a-new-object inversion
            // `WorkspaceCommandBindings.CreationCompensation` already uses
            // for an engineering object's own Create/Copy (`WP 21.1A`).
            var compensation = _dispatcher is null ? null : new CommandCompensation(
                $"Create '{created.Identifier}'",
                undo: ct => _dispatcher.DispatchAsync(new DeleteRequirementCommand(created.Id), ct),
                redo: ct => _dispatcher.DispatchAsync(new UndeleteRequirementCommand(created.Id), ct));

            if (command.GroupId is { } groupId)
            {
                var group = await _requirementsService.FindGroupAsync(groupId, cancellationToken).ConfigureAwait(false);
                await _requirementsService.MoveToGroupAsync(created.Id, groupId, cancellationToken).ConfigureAwait(false);
                return CommandResult.Success(
                    $"Created Requirement '{created.Identifier}' in group '{group?.Name ?? groupId.ToString()}'.",
                    created.Id, RequirementsService.RequirementDocumentKind, compensation);
            }

            return CommandResult.Success(
                $"Created Requirement '{created.Identifier}'. It is not in any group; the Project Explorer lists it under \"Ungrouped\".",
                created.Id, RequirementsService.RequirementDocumentKind, compensation);
        }
        catch (DuplicateRequirementIdentifierException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
