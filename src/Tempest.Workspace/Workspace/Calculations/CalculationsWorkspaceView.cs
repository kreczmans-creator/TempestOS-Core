using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Presents one real <c>"Calculation"</c>, <c>"CalculationSet"</c>, or
/// (read-only) <c>"CalculationTemplate"</c> — one instance serves whichever
/// of the three Kinds <see cref="ObjectKind"/> names, mirroring
/// <see cref="Requirements.RequirementsWorkspaceView"/>'s own identical
/// multi-Kind shape (a Template has no Domain identity, so it is read
/// through <see cref="CalculationTemplateRegistry"/> rather than
/// <see cref="EngineeringDomainContext.Repository"/>).
/// </summary>
public sealed class CalculationsWorkspaceView : IWorkspaceView
{
    private readonly EngineeringDomainContext _context;
    private readonly CalculationTemplateRegistry _templateRegistry;
    private string _title;

    /// <summary>Initialises a new instance of the <see cref="CalculationsWorkspaceView"/> class.</summary>
    public CalculationsWorkspaceView(
        Guid objectId, string objectKind, string initialTitle, EngineeringDomainContext context, CalculationTemplateRegistry templateRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialTitle);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(templateRegistry);

        ObjectId = objectId;
        ObjectKind = objectKind;
        _title = initialTitle;
        _context = context;
        _templateRegistry = templateRegistry;
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
    /// <remarks>
    /// A real read for <c>"Calculation"</c>/<c>"CalculationSet"</c> — re-fetches
    /// <see cref="ObjectId"/> and refreshes <see cref="Title"/>, picking up
    /// any rename/revision since this view was created. A
    /// <c>"CalculationTemplate"</c> re-reads its own
    /// <see cref="CalculationTemplateRegistry"/> entry instead — Templates
    /// are registered-once, in-memory content, never revised, but this
    /// still stays a real read rather than a permanently-fixed <see cref="Title"/>.
    /// </remarks>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (string.Equals(ObjectKind, "CalculationTemplate", StringComparison.Ordinal))
        {
            if (_templateRegistry.FindByNodeId(ObjectId) is { } template)
                _title = template.Metadata.Name;

            return;
        }

        var current = await _context.Repository.FindAsync(ObjectId, cancellationToken).ConfigureAwait(false);

        if (current is IHasBusinessIdentifier identity)
            _title = identity.DisplayName;
    }

    /// <inheritdoc />
    /// <remarks>Always returns <see langword="true"/> — <see cref="IsDirty"/> is always <see langword="false"/>, so no unsaved-edit prompt is ever needed.</remarks>
    public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
