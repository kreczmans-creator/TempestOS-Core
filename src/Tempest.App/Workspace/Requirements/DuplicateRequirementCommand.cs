using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Creates a new Requirement with the same Statement/Category/Priority/
/// Group as <see cref="IWorkspaceCommand.TargetObjectId"/>, under a new
/// business identifier — composition over <see cref="IRequirementsService.CreateAsync"/>
/// (plus <see cref="IRequirementsService.SetPriorityAsync"/>/<see cref="IRequirementsService.MoveToGroupAsync"/>
/// for the two facets <see cref="IRequirementsService.CreateAsync"/> itself
/// does not set), mirroring <c>DuplicateMechanicalObjectCommand</c>'s own
/// identical "never a second, independent implementation of create a copy"
/// reasoning. Deliberately does not copy Owner or Status: a duplicate
/// starts unowned and at Draft — ownership is a per-instance accountability
/// this Work Package's own model treats as something a duplicate must be
/// given explicitly, never silently inherited, and every requirement's own
/// lifecycle genuinely starts over on a duplicate.
/// </summary>
public sealed class DuplicateRequirementCommand : IWorkspaceCommand
{
    public DuplicateRequirementCommand(Guid targetObjectId, string newIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newIdentifier);

        TargetObjectId = targetObjectId;
        NewIdentifier = newIdentifier;
    }

    /// <inheritdoc />
    /// <remarks>The requirement being duplicated <em>from</em> — the source, not the newly-created duplicate.</remarks>
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the duplicate's own new business identifier.</summary>
    public string NewIdentifier { get; }
}

/// <summary>Handles <see cref="DuplicateRequirementCommand"/>.</summary>
public sealed class DuplicateRequirementCommandHandler : ICommandHandler<DuplicateRequirementCommand>
{
    private readonly IRequirementsService _requirementsService;

    public DuplicateRequirementCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(DuplicateRequirementCommand command, CancellationToken cancellationToken)
    {
        var source = await _requirementsService.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (source is null)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found.");

        IRequirement duplicate;
        try
        {
            duplicate = await _requirementsService.CreateAsync(command.NewIdentifier, source.Statement, source.Category, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DuplicateRequirementIdentifierException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        if (source.Priority is { } priority)
            await _requirementsService.SetPriorityAsync(duplicate.Id, priority, cancellationToken).ConfigureAwait(false);

        if (source.GroupId is { } groupId)
            await _requirementsService.MoveToGroupAsync(duplicate.Id, groupId, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Duplicated '{source.Identifier}' to new Requirement '{command.NewIdentifier}' ('{duplicate.Id}').");
    }
}
