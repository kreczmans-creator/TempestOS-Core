using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Creates a new object of the same Kind as <see cref="IWorkspaceCommand.TargetObjectId"/>
/// under an explicit, possibly different, target parent — reuses
/// <see cref="MechanicalObjectFactoryRegistry"/>'s own existing Create
/// machinery; no new Domain capability is introduced.
/// </summary>
public sealed class CopyMechanicalObjectCommand : IWorkspaceCommand
{
    public CopyMechanicalObjectCommand(Guid targetObjectId, string targetKind, Guid? newParentId, string? newIdentifier = null, string? newDisplayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewParentId = newParentId;
        NewIdentifier = newIdentifier;
        NewDisplayName = newDisplayName;
    }

    /// <inheritdoc />
    /// <remarks>The object being copied <em>from</em> — the source, not the newly-created copy.</remarks>
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the copy's own new parent, or <see langword="null"/> for a top-level copy.</summary>
    public Guid? NewParentId { get; }

    /// <summary>Gets the copy's own new business identifier, or <see langword="null"/> to leave it unset.</summary>
    public string? NewIdentifier { get; }

    /// <summary>Gets the copy's own new display name, or <see langword="null"/> to default to "&lt;source name&gt; (Copy)".</summary>
    public string? NewDisplayName { get; }
}

/// <summary>Handles <see cref="CopyMechanicalObjectCommand"/>.</summary>
public sealed class CopyMechanicalObjectCommandHandler : ICommandHandler<CopyMechanicalObjectCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly MechanicalObjectFactoryRegistry _registry;

    public CopyMechanicalObjectCommandHandler(EngineeringDomainContext context, MechanicalObjectFactoryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);

        _context = context;
        _registry = registry;
    }

    public async Task<CommandResult> HandleAsync(CopyMechanicalObjectCommand command, CancellationToken cancellationToken)
    {
        var source = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (source is null)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found.");

        var sourceDisplayName = (source as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();
        var content = source is IHasRevisions revisable ? revisable.Content : $"Copy of {sourceDisplayName}.";
        var displayName = command.NewDisplayName ?? $"{sourceDisplayName} (Copy)";

        IEngineeringObject copy;

        try
        {
            copy = await _registry.CreateAsync(
                source.Kind, command.NewIdentifier, displayName, content, command.NewParentId,
                memberRevisions: source is IConfiguration configuration ? configuration.MemberRevisions : null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Copied '{command.TargetObjectId}' to new {source.Kind} '{copy.Id}'.");
    }
}
