using Tempest.Core.Logging;
using System.Text.Json;
using Tempest.Core.Settings;

namespace Tempest.App.Workspace;

/// <summary>
/// The concrete <see cref="IWorkspaceState"/> implementation — persists via
/// the existing <see cref="ISettingsProvider"/> (`ADR-0064`), introducing no
/// new persistence mechanism.
/// </summary>
/// <remarks>
/// <b>Disclosed implementation-phase finding:</b> `WP8.0B Workspace
/// Contracts.md` proposed <c>ISettingsProvider.GetValueAsync&lt;T&gt;</c>/
/// <c>SetValueAsync&lt;T&gt;</c> generically; the real, shipped
/// <see cref="ISettingsProvider"/> (`WP 6.4`) operates on <see cref="string"/>
/// only. This class serializes its own <see cref="WorkspaceStateDto"/> to
/// JSON via <see cref="JsonSerializer"/> and stores that string directly —
/// the identical pattern <see cref="Tempest.Core.Requirements.RequirementDto"/>
/// already establishes for <see cref="Tempest.Core.EngineeringData.IDocumentRevision.Content"/>,
/// applied here to Settings instead. A minor, disclosed deviation from the
/// contract document's own proposed generic signature, not from its own
/// governing decision (`ADR-0064`) — see `WP8.1A Implementation Report.md`.
/// </remarks>
internal sealed class WorkspaceState : IWorkspaceState
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> this Workspace's own session state is stored under.</summary>
    public const string SettingKey = "Workspace.State";

    private readonly ISettingsProvider _settingsProvider;
    private readonly SettingsDocument<WorkspaceStateDto> _document;
    private readonly IReadOnlyList<WorkspacePanelPlacement> _defaultPlacements;
    private List<Guid> _openViewIds = [];

    /// <summary>Initialises a new instance of the <see cref="WorkspaceState"/> class.</summary>
    /// <param name="settingsProvider">The Settings service this state persists through.</param>
    /// <param name="defaultPlacements">The default panel placements a first-run/missing session yields.</param>
    public WorkspaceState(ISettingsProvider settingsProvider, IReadOnlyList<WorkspacePanelPlacement> defaultPlacements, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        ArgumentNullException.ThrowIfNull(defaultPlacements);

        _settingsProvider = settingsProvider;
        _defaultPlacements = defaultPlacements;
        Layout = new WorkspaceLayout(defaultPlacements);

        _document = new SettingsDocument<WorkspaceStateDto>(settingsProvider, SettingKey, "Workspace Layout", logger);
    }

    /// <inheritdoc />
    public IWorkspaceLayout Layout { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<Guid> OpenViewIds => _openViewIds;

    /// <inheritdoc />
    public WorkspaceSelection? LastSelection { get; private set; }

    /// <summary>Records the currently open view Ids, to be included in the next <see cref="SaveAsync"/>.</summary>
    internal void SetOpenViewIds(IReadOnlyList<Guid> openViewIds)
    {
        ArgumentNullException.ThrowIfNull(openViewIds);
        _openViewIds = [.. openViewIds];
    }

    /// <summary>Records the current selection, to be included in the next <see cref="SaveAsync"/>.</summary>
    internal void SetLastSelection(WorkspaceSelection? selection) => LastSelection = selection;

    /// <inheritdoc />
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var dto = new WorkspaceStateDto(Layout.PanelPlacements, _openViewIds, LastSelection);
        await _document.SaveAsync(dto, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var dto = await _document.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            Layout = new WorkspaceLayout(_defaultPlacements);
            _openViewIds = [];
            LastSelection = null;
            return;
        }

        Layout = new WorkspaceLayout(dto.PanelPlacements);
        _openViewIds = [.. dto.OpenViewIds];
        LastSelection = dto.LastSelection;
    }
}
