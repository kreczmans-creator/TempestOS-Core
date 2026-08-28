using System.Text.Json;
using Tempest.Core.Settings;

namespace Tempest.Desktop.Docking;

/// <summary>
/// Persists Collapse/Auto-Hide state for the Project Explorer and Property
/// Inspector, plus the Output panel's own visibility/size/Collapse/Auto-Hide
/// state, and the name of the last predefined layout applied (`WP 10.2B`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not part of <see cref="Tempest.App.Workspace.IWorkspaceState"/>.</b>
/// Neither Collapse nor Auto-Hide nor the Output panel exists anywhere in
/// the frozen `WP8.0B` Workspace contracts (`IWorkspaceLayout`/
/// `WorkspacePanelPlacement` carry only Dock Position, Size, and
/// Visibility) — extending those contracts to carry three more concepts
/// this Work Package's own controlling instruction never asked for would
/// be exactly the "contract redesign" it explicitly excludes. This class
/// persists through the identical <see cref="ISettingsProvider"/>
/// substrate `WorkspaceState` already uses (`ADR-0064`), under a second,
/// sibling key — the same "own JSON blob under its own Settings key"
/// pattern, applied a second time to a second, independent concern, never
/// a second persistence mechanism.
/// </para>
/// <para>
/// <b>Independent of <see cref="Tempest.App.Workspace.IWorkspaceState.SaveAsync"/>'s own
/// save point.</b> <c>WorkspaceManager.ShutdownAsync</c> only ever knew to
/// save the Workspace's own state; this class is saved directly by
/// <c>MainWindow</c>'s own Closing handler instead, alongside it — two
/// independent writes to two independent Settings keys, not a change to
/// what the Workspace layer's own shutdown sequence does.
/// </para>
/// </remarks>
internal sealed class DesktopPanelUiState
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> this state is stored under.</summary>
    public const string SettingKey = "Workspace.Desktop.PanelUiState";

    private readonly ISettingsProvider _settingsProvider;

    /// <summary>Initialises a new instance of the <see cref="DesktopPanelUiState"/> class with every flag at its own documented default (nothing collapsed, everything pinned, Output hidden).</summary>
    public DesktopPanelUiState(ISettingsProvider settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _settingsProvider = settingsProvider;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(SettingKey, "Workspace Desktop Panel UI State", string.Empty));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Already registered by a prior IWorkspaceManager.StartAsync call
            // against the same ISettingsProvider instance (a restart) —
            // idempotent, not an error (identical precedent, WorkspaceState).
        }
    }

    /// <summary>Gets or sets whether the Project Explorer is currently Collapsed.</summary>
    public bool ExplorerCollapsed { get; set; }

    /// <summary>Gets or sets whether the Project Explorer is currently pinned (<see langword="false"/> = Auto-Hide).</summary>
    public bool ExplorerPinned { get; set; } = true;

    /// <summary>Gets or sets whether the Property Inspector is currently Collapsed.</summary>
    public bool InspectorCollapsed { get; set; }

    /// <summary>Gets or sets whether the Property Inspector is currently pinned (<see langword="false"/> = Auto-Hide).</summary>
    public bool InspectorPinned { get; set; } = true;

    /// <summary>Gets or sets whether the Output panel is currently shown at all — hidden by default (`WP8.0A UI Architecture.md` §1's own documented default arrangement never named a fourth default-visible panel).</summary>
    public bool OutputVisible { get; set; }

    /// <summary>Gets or sets the Output panel's own current height, in device-independent pixels.</summary>
    public double OutputHeight { get; set; } = 160;

    /// <summary>Gets or sets whether the Output panel is currently Collapsed.</summary>
    public bool OutputCollapsed { get; set; }

    /// <summary>Gets or sets whether the Output panel is currently pinned (<see langword="false"/> = Auto-Hide).</summary>
    public bool OutputPinned { get; set; } = true;

    /// <summary>
    /// Gets or sets the name of the last predefined layout applied
    /// (<see cref="PredefinedLayouts.WorkspaceLayoutPreset"/>), or
    /// <see langword="null"/> if none has been applied this session or the
    /// layout has since been manually changed — an honest label only,
    /// never re-derived from the current placements themselves.
    /// </summary>
    public string? LastAppliedPreset { get; set; }

    /// <summary>Gets or sets whether the Engineering Ribbon is minimised to its own tab strip (`TD-70`) — persisted so a user working on a laptop keeps the vertical space they reclaimed across restarts.</summary>
    public bool RibbonCollapsed { get; set; }

    /// <summary>Writes the current state via <see cref="ISettingsProvider.SetValueAsync"/>.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var dto = new DesktopPanelUiStateDto(ExplorerCollapsed, ExplorerPinned, InspectorCollapsed, InspectorPinned, OutputVisible, OutputHeight, OutputCollapsed, OutputPinned, LastAppliedPreset, RibbonCollapsed);
        var json = JsonSerializer.Serialize(dto);

        await _settingsProvider.SetValueAsync(SettingKey, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads persisted state via <see cref="ISettingsProvider.GetValueAsync"/>. A missing/first-run value leaves every property at its own documented default — never an exception.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingsProvider.GetValueAsync(SettingKey, cancellationToken).ConfigureAwait(false);

        DesktopPanelUiStateDto? dto;
        try
        {
            dto = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<DesktopPanelUiStateDto>(json);
        }
        catch (JsonException)
        {
            // A corrupted stored value (e.g. a torn write) degrades to
            // the documented first-run defaults — this method's own
            // "never an exception" contract (`TD-60`).
            dto = null;
        }

        if (dto is null)
            return;

        ExplorerCollapsed = dto.ExplorerCollapsed;
        ExplorerPinned = dto.ExplorerPinned;
        InspectorCollapsed = dto.InspectorCollapsed;
        InspectorPinned = dto.InspectorPinned;
        OutputVisible = dto.OutputVisible;
        OutputHeight = dto.OutputHeight;
        OutputCollapsed = dto.OutputCollapsed;
        OutputPinned = dto.OutputPinned;
        LastAppliedPreset = dto.LastAppliedPreset;
        RibbonCollapsed = dto.RibbonCollapsed;
    }

    /// <summary>The plain, JSON-serializable shape this class persists.</summary>
    private sealed record DesktopPanelUiStateDto(
        bool ExplorerCollapsed,
        bool ExplorerPinned,
        bool InspectorCollapsed,
        bool InspectorPinned,
        bool OutputVisible,
        double OutputHeight,
        bool OutputCollapsed,
        bool OutputPinned,
        string? LastAppliedPreset,
        bool RibbonCollapsed = false);
}
