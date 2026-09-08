using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Presents one real <c>"VerificationActivity"</c> — mirrors
/// <see cref="Mechanical.MechanicalWorkspaceView"/>/
/// <see cref="Documents.DocumentsWorkspaceView"/>'s own identical, simple
/// shape (a single real Kind, no synthetic, non-Domain Kind here).
/// </summary>
public sealed class VerificationActivityWorkspaceView : IWorkspaceView
{
    private readonly EngineeringDomainContext _context;
    private string _title;

    /// <summary>Initialises a new instance of the <see cref="VerificationActivityWorkspaceView"/> class.</summary>
    public VerificationActivityWorkspaceView(Guid objectId, string objectKind, string initialTitle, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialTitle);
        ArgumentNullException.ThrowIfNull(context);

        ObjectId = objectId;
        ObjectKind = objectKind;
        _title = initialTitle;
        _context = context;
    }

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Title => _title;

    /// <inheritdoc />
    public Guid ObjectId { get; }

    /// <inheritdoc />
    public string ObjectKind { get; }

    /// <inheritdoc />
    /// <remarks>Always <see langword="false"/> — every mutation dispatches through a Command and commits immediately (`ADR-0063`); this view never buffers a local, uncommitted edit.</remarks>
    public bool IsDirty => false;

    /// <inheritdoc />
    /// <remarks>A real read — re-fetches <see cref="ObjectId"/> from <see cref="EngineeringDomainContext.Repository"/> and refreshes <see cref="Title"/> from its current <c>DisplayName</c>, picking up any <see cref="IRenamable.RenameAsync"/> since this view was created.</remarks>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var current = await _context.Repository.FindAsync(ObjectId, cancellationToken).ConfigureAwait(false);

        if (current is IHasBusinessIdentifier identity)
            _title = identity.DisplayName;
    }

    /// <inheritdoc />
    /// <remarks>Always returns <see langword="true"/> — <see cref="IsDirty"/> is always <see langword="false"/>, so no unsaved-edit prompt is ever needed.</remarks>
    public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
