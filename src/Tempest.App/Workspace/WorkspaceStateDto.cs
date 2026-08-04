namespace Tempest.App.Workspace;

/// <summary>The plain, JSON-serializable shape <see cref="WorkspaceState"/> persists via <see cref="Tempest.Core.Settings.ISettingsProvider"/>.</summary>
internal sealed record WorkspaceStateDto(
    IReadOnlyList<WorkspacePanelPlacement> PanelPlacements,
    IReadOnlyList<Guid> OpenViewIds,
    WorkspaceSelection? LastSelection);
