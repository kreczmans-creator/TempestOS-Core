namespace Tempest.App.Workspace;

/// <summary>The concrete <see cref="IWorkspaceLayout"/> implementation — an in-memory placement table, no persistence of its own (persistence is <see cref="IWorkspaceState"/>'s own responsibility, `ADR-0064`).</summary>
internal sealed class WorkspaceLayout : IWorkspaceLayout
{
    private readonly IReadOnlyList<WorkspacePanelPlacement> _defaults;
    private readonly Dictionary<Guid, WorkspacePanelPlacement> _placements;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceLayout"/> class.</summary>
    /// <param name="defaults">The default placement for every known panel — this Workspace's own documented default arrangement.</param>
    public WorkspaceLayout(IReadOnlyList<WorkspacePanelPlacement> defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        _defaults = defaults;
        _placements = defaults.ToDictionary(p => p.PanelId);
    }

    /// <inheritdoc />
    public IReadOnlyList<WorkspacePanelPlacement> PanelPlacements => _placements.Values.ToList();

    /// <inheritdoc />
    public WorkspacePanelPlacement GetPlacement(Guid panelId) =>
        _placements.TryGetValue(panelId, out var placement)
            ? placement
            : throw new ArgumentException($"'{panelId}' is not a known panel.", nameof(panelId));

    /// <inheritdoc />
    public void SetPlacement(Guid panelId, WorkspacePanelPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (placement.PanelId != panelId)
            throw new ArgumentException("placement's own PanelId must match panelId.", nameof(placement));

        if (!_placements.ContainsKey(panelId))
            throw new ArgumentException($"'{panelId}' is not a known panel.", nameof(panelId));

        _placements[panelId] = placement;
    }

    /// <inheritdoc />
    public IWorkspaceLayout ResetToDefault() => new WorkspaceLayout(_defaults);
}
