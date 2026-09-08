using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Manufacturing;

/// <summary>
/// Creates a new object of the same Kind as
/// <see cref="IWorkspaceCommand.TargetObjectId"/> under an explicit,
/// possibly different, target parent — reuses
/// <see cref="ManufacturingObjectFactoryRegistry"/>'s own existing Create
/// machinery, mirroring
/// <see cref="Documents.CopyDocumentObjectCommand"/>/
/// <see cref="Verification.CopyVerificationActivityCommand"/>'s own
/// identical shape; no new Domain capability is introduced. The source's
/// own <see cref="IManufacturingOperation.PartId"/>/
/// <see cref="IWorkInstruction.ManufacturingOperationId"/>/
/// <see cref="IVerificationActivity.SubjectId"/>/
/// <see cref="IVerificationActivity.Method"/> — whichever the source's
/// own Kind carries — is preserved on the copy.
/// </summary>
public sealed class CopyManufacturingObjectCommand : IWorkspaceCommand
{
    public CopyManufacturingObjectCommand(Guid targetObjectId, string targetKind, Guid? newParentId, string? newDisplayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewParentId = newParentId;
        NewDisplayName = newDisplayName;
    }

    /// <inheritdoc />
    /// <remarks>The object being copied <em>from</em> — the source, not the newly-created copy.</remarks>
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the copy's own new parent, or <see langword="null"/> for a top-level copy.</summary>
    public Guid? NewParentId { get; }

    /// <summary>Gets the copy's own new display name, or <see langword="null"/> to default to "&lt;source name&gt; (Copy)".</summary>
    public string? NewDisplayName { get; }
}

/// <summary>Handles <see cref="CopyManufacturingObjectCommand"/>.</summary>
public sealed class CopyManufacturingObjectCommandHandler : ICommandHandler<CopyManufacturingObjectCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly ManufacturingObjectFactoryRegistry _registry;

    public CopyManufacturingObjectCommandHandler(EngineeringDomainContext context, ManufacturingObjectFactoryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);

        _context = context;
        _registry = registry;
    }

    public async Task<CommandResult> HandleAsync(CopyManufacturingObjectCommand command, CancellationToken cancellationToken)
    {
        var source = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (source is null)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found.");

        var sourceDisplayName = (source as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();
        var content = source is IHasRevisions revisable ? revisable.Content : $"Copy of {sourceDisplayName}.";
        var displayName = command.NewDisplayName ?? $"{sourceDisplayName} (Copy)";
        var identifier = (source as IHasBusinessIdentifier)?.Identifier;
        var classification = (source as IHasMetadata)?.Classification;

        IEngineeringObject copy;

        try
        {
            copy = source switch
            {
                IManufacturingOperation operation => await _registry.CreateOperationAsync(
                    identifier, displayName, content, operation.PartId, classification, command.NewParentId, cancellationToken).ConfigureAwait(false),

                IWorkInstruction workInstruction => await _registry.CreateWorkInstructionAsync(
                    identifier, displayName, content, workInstruction.ManufacturingOperationId, command.NewParentId, cancellationToken).ConfigureAwait(false),

                IVerificationActivity inspection when string.Equals(source.Kind, "Inspection", StringComparison.Ordinal) => await _registry.CreateInspectionAsync(
                    displayName, content, inspection.SubjectId, inspection.Method, command.NewParentId, cancellationToken).ConfigureAwait(false),

                _ => throw new ArgumentException($"'{command.TargetObjectId}' is not a known Manufacturing object.", nameof(command)),
            };
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Copied '{command.TargetObjectId}' to new {source.Kind} '{copy.Id}'.");
    }
}
