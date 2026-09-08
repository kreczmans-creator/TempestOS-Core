using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Creates a new Verification Activity under an explicit, possibly
/// different, target parent — reuses
/// <see cref="VerificationActivityFactoryRegistry"/>'s own existing
/// Create machinery, mirroring
/// <see cref="Calculations.CopyCalculationObjectCommand"/>/
/// <see cref="Documents.CopyDocumentObjectCommand"/>'s own identical
/// shape; no new Domain capability is introduced. The source's own
/// <see cref="IVerificationActivity.SubjectId"/>/<see cref="IVerificationActivity.Method"/>
/// are preserved on the copy — a copy of an Inspection activity stays an
/// Inspection activity verifying the same subject.
/// </summary>
public sealed class CopyVerificationActivityCommand : IWorkspaceCommand
{
    public CopyVerificationActivityCommand(Guid targetObjectId, string targetKind, Guid? newParentId, string? newDisplayName = null)
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

/// <summary>Handles <see cref="CopyVerificationActivityCommand"/>.</summary>
public sealed class CopyVerificationActivityCommandHandler : ICommandHandler<CopyVerificationActivityCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly VerificationActivityFactoryRegistry _registry;

    public CopyVerificationActivityCommandHandler(EngineeringDomainContext context, VerificationActivityFactoryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);

        _context = context;
        _registry = registry;
    }

    public async Task<CommandResult> HandleAsync(CopyVerificationActivityCommand command, CancellationToken cancellationToken)
    {
        var source = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (source is not IVerificationActivity sourceActivity)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or is not a VerificationActivity.");

        var sourceDisplayName = (source as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();
        var content = source is IHasRevisions revisable ? revisable.Content : $"Copy of {sourceDisplayName}.";
        var displayName = command.NewDisplayName ?? $"{sourceDisplayName} (Copy)";

        IEngineeringObject copy;

        try
        {
            copy = await _registry.CreateAsync(
                displayName, content, sourceActivity.SubjectId, sourceActivity.Method, command.NewParentId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Copied '{command.TargetObjectId}' to new VerificationActivity '{copy.Id}'.");
    }
}
