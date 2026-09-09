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

    public CreateRequirementCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(CreateRequirementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _requirementsService.CreateAsync(command.Identifier, command.Statement, command.Category, cancellationToken)
                .ConfigureAwait(false);

            if (command.GroupId is { } groupId)
            {
                var group = await _requirementsService.FindGroupAsync(groupId, cancellationToken).ConfigureAwait(false);
                await _requirementsService.MoveToGroupAsync(created.Id, groupId, cancellationToken).ConfigureAwait(false);
                return CommandResult.Success($"Created Requirement '{created.Identifier}' in group '{group?.Name ?? groupId.ToString()}'.", created.Id, RequirementsService.RequirementDocumentKind);
            }

            return CommandResult.Success($"Created Requirement '{created.Identifier}'. It is not in any group; the Project Explorer lists it under \"Ungrouped\".", created.Id, RequirementsService.RequirementDocumentKind);
        }
        catch (DuplicateRequirementIdentifierException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
