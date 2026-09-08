using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Presents one real Requirement, Requirement Collection, or Requirement
/// Group — the second <see cref="IWorkspaceView"/> backed by a real
/// Engineering discipline, after Mechanical's own (`ADR-0067`,
/// `WP 9.0A`/`WP 9.1A`). One instance serves whichever of the three Kinds
/// <see cref="ObjectKind"/> names — the Requirements Framework's own
/// genuinely different, non-<c>IEngineeringObject</c> shape means a single
/// generic view type reads through <see cref="IRequirementsService"/>
/// directly, rather than <c>EngineeringDomainContext.Repository</c>.
/// </summary>
public sealed class RequirementsWorkspaceView : IWorkspaceView
{
    private readonly IRequirementsService _requirementsService;
    private string _title;

    /// <summary>Initialises a new instance of the <see cref="RequirementsWorkspaceView"/> class.</summary>
    public RequirementsWorkspaceView(Guid objectId, string objectKind, string initialTitle, IRequirementsService requirementsService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialTitle);
        ArgumentNullException.ThrowIfNull(requirementsService);

        ObjectId = objectId;
        ObjectKind = objectKind;
        _title = initialTitle;
        _requirementsService = requirementsService;
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
    /// <remarks>A real read — re-fetches <see cref="ObjectId"/> from <see cref="IRequirementsService"/> and refreshes <see cref="Title"/>, picking up any revision/rename since this view was created. A <see langword="null"/> read (the object was deleted between selection and refresh — soft-delete never removes it, so this can only mean it never existed) leaves <see cref="Title"/> unchanged.</remarks>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _title = ObjectKind switch
        {
            RequirementsService.RequirementDocumentKind =>
                await _requirementsService.FindAsync(ObjectId, cancellationToken).ConfigureAwait(false) is { } requirement
                    ? $"{requirement.Identifier} — {requirement.Statement}" : _title,

            RequirementsService.RequirementCollectionDocumentKind =>
                await _requirementsService.FindCollectionAsync(ObjectId, cancellationToken).ConfigureAwait(false) is { } collection
                    ? collection.Name : _title,

            RequirementsService.RequirementGroupDocumentKind =>
                await _requirementsService.FindGroupAsync(ObjectId, cancellationToken).ConfigureAwait(false) is { } group
                    ? group.Name : _title,

            _ => _title,
        };
    }

    /// <inheritdoc />
    /// <remarks>Always returns <see langword="true"/> — <see cref="IsDirty"/> is always <see langword="false"/>, so no unsaved-edit prompt is ever needed.</remarks>
    public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
