using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Documents;

/// <summary>
/// Creates a new object of the same Kind as <see cref="IWorkspaceCommand.TargetObjectId"/>
/// under an explicit, possibly different, target parent — reuses
/// <see cref="DocumentObjectFactoryRegistry"/>'s own existing Create
/// machinery, mirroring <see cref="Calculations.CopyCalculationObjectCommand"/>'s
/// own identical shape; no new Domain capability is introduced. The source's
/// own <see cref="Tempest.Core.EngineeringDomain.EngineeringObjectMetadata.Classification"/>
/// is preserved on the copy — a Specification copied stays a Specification.
/// </summary>
public sealed class CopyDocumentObjectCommand : IWorkspaceCommand
{
    public CopyDocumentObjectCommand(Guid targetObjectId, string targetKind, Guid? newParentId, string? newIdentifier = null, string? newDisplayName = null)
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

/// <summary>Handles <see cref="CopyDocumentObjectCommand"/>.</summary>
public sealed class CopyDocumentObjectCommandHandler : ICommandHandler<CopyDocumentObjectCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly DocumentObjectFactoryRegistry _registry;
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="context">Where the source object is found.</param>
    /// <param name="registry">Creates the copy.</param>
    /// <param name="dispatcher">Dispatches this copy's own compensation (`WP 21.1A`) — optional; <see langword="null"/> means no <see cref="CommandResult.Compensation"/> is attached.</param>
    public CopyDocumentObjectCommandHandler(EngineeringDomainContext context, DocumentObjectFactoryRegistry registry, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);

        _context = context;
        _registry = registry;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(CopyDocumentObjectCommand command, CancellationToken cancellationToken)
    {
        var source = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (source is null)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found.");

        var sourceDisplayName = (source as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();
        var content = source is IHasRevisions revisable ? revisable.Content : $"Copy of {sourceDisplayName}.";
        var displayName = command.NewDisplayName ?? $"{sourceDisplayName} (Copy)";
        var classification = source is IHasMetadata metadata ? metadata.Classification : null;
        var drawingNumber = (source as IDrawing)?.DrawingNumber;
        var modelFormat = (source as ICadModel)?.ModelFormat;

        IEngineeringObject copy;

        try
        {
            copy = await _registry.CreateAsync(
                source.Kind, command.NewIdentifier, displayName, content, command.NewParentId,
                classification, drawingNumber, modelFormat, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var destinationPhrase = await WorkspaceCommandBindings.DestinationPhraseAsync(_context, command.NewParentId, cancellationToken).ConfigureAwait(false);
        var compensation = WorkspaceCommandBindings.CreationCompensation(
            _context, _dispatcher, copy.Id, source.Kind, $"Copy '{sourceDisplayName}'",
            buildDelete: () => new DeleteDocumentObjectCommand(copy.Id, source.Kind),
            buildUndelete: () => new UndeleteDocumentObjectCommand(copy.Id, source.Kind));

        return CommandResult.Success($"Copied '{sourceDisplayName}' as '{displayName}' {destinationPhrase}.", copy.Id, source.Kind, compensation);
    }
}
