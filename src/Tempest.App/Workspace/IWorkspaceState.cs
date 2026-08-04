namespace Tempest.App.Workspace;

/// <summary>
/// The complete, persistable session snapshot — layout, open tabs, and last
/// selection — backed by the existing
/// <see cref="Tempest.Core.Settings.ISettingsProvider"/> (`ADR-0064`).
/// </summary>
public interface IWorkspaceState
{
    /// <summary>Gets the current docking layout.</summary>
    IWorkspaceLayout Layout { get; }

    /// <summary>Gets every currently open view's own Id, in tab order.</summary>
    IReadOnlyList<Guid> OpenViewIds { get; }

    /// <summary>Gets the selection in effect when this state was last saved.</summary>
    WorkspaceSelection? LastSelection { get; }

    /// <summary>Writes current state via <see cref="Tempest.Core.Settings.ISettingsProvider.SetValueAsync"/>.</summary>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads persisted state via
    /// <see cref="Tempest.Core.Settings.ISettingsProvider.GetValueAsync"/>. A
    /// missing/first-run value yields <see cref="IWorkspaceLayout.ResetToDefault"/>
    /// and no open tabs — never an exception.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
