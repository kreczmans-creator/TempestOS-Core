using Tempest.Core.Commands;
using Tempest.Core.Projects;
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
    /// <param name="identifier">The business identifier, or <see langword="null"/>/blank for the next one in sequence (`v0.25.3` independent test E2).</param>
    /// <param name="statement">The statement.</param>
    /// <param name="category">The classification, or <see langword="null"/>.</param>
    /// <param name="groupId">The group to place it in, or <see langword="null"/>.</param>
    /// <param name="projectId">The open project, whose identifier prefixes a generated identifier; <see langword="null"/> standalone.</param>
    public CreateRequirementCommand(string? identifier, string statement, string? category = null, Guid? groupId = null, Guid? projectId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);

        Identifier = string.IsNullOrWhiteSpace(identifier) ? null : identifier.Trim();
        Statement = statement;
        Category = category;
        GroupId = groupId;
        ProjectId = projectId;
    }

    /// <summary>The open project, whose identifier prefixes a generated identifier, or <see langword="null"/>.</summary>
    public Guid? ProjectId { get; }

    /// <summary>The group the new requirement is placed in, or <see langword="null"/> for none (`WP 17.9.3`: the selected group, when one is selected).</summary>
    public Guid? GroupId { get; }

    /// <summary>Gets the new requirement's own business identifier, or <see langword="null"/> for the next in sequence.</summary>
    public string? Identifier { get; }

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
    private readonly Func<Guid, CancellationToken, Task<string?>>? _projectIdentifierOf;

    /// <param name="requirementsService">Where the requirement is created.</param>
    /// <param name="dispatcher">
    /// Dispatches this create's own compensation (`WP 21.6A`, mirrors
    /// <c>EngineeringDomain</c>'s own <c>Create*ObjectCommandHandler</c>
    /// shape) — optional; <see langword="null"/> means no
    /// <see cref="CommandResult.Compensation"/> is attached.
    /// </param>
    /// <param name="projectIdentifierOf">Reads an open project's identifier (<c>TESTY-TRIALX</c>), so a generated identifier is numbered per project; <see langword="null"/> numbers every generated identifier as <c>REQ-nnn</c>.</param>
    public CreateRequirementCommandHandler(
        IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null,
        Func<Guid, CancellationToken, Task<string?>>? projectIdentifierOf = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
        _projectIdentifierOf = projectIdentifierOf;
    }

    /// <summary>
    /// The next identifier in sequence (`v0.25.3` independent test E2):
    /// <c>&lt;project&gt;-REQ-001</c> in a project numbered by customer code,
    /// otherwise <c>REQ-001</c>; one past the highest already used under that
    /// prefix, skipping any taken, so a number is never reissued.
    /// </summary>
    internal async Task<string> NextIdentifierAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        var projectIdentifier = projectId is { } id && _projectIdentifierOf is not null
            ? await _projectIdentifierOf(id, cancellationToken).ConfigureAwait(false)
            : null;
        var prefix = ProjectNumbering.TryGetDocumentPrefix(projectIdentifier, ProjectNumbering.Requirement, out var projectPrefix)
            ? projectPrefix
            : $"{ProjectNumbering.Requirement}-";

        var existing = await _requirementsService.ListAsync(cancellationToken).ConfigureAwait(false);
        var candidate = ProjectNumbering.NextNumber(prefix, existing.Select(r => r.Identifier));
        while (await _requirementsService.FindByIdentifierAsync(candidate, cancellationToken).ConfigureAwait(false) is not null)
            candidate = ProjectNumbering.NextNumber(prefix, [candidate]);

        return candidate;
    }

    public async Task<CommandResult> HandleAsync(CreateRequirementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var identifier = command.Identifier ?? await NextIdentifierAsync(command.ProjectId, cancellationToken).ConfigureAwait(false);
            var created = await _requirementsService.CreateAsync(identifier, command.Statement, command.Category, cancellationToken)
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
