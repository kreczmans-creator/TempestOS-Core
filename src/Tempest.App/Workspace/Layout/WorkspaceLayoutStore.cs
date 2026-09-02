using Tempest.Core.Settings;

namespace Tempest.App.Workspace.Layout;

/// <summary>The concrete <see cref="IWorkspaceLayoutStore"/>, over <see cref="ISettingsProvider"/> (`ADR-0064`).</summary>
public sealed class WorkspaceLayoutStore : IWorkspaceLayoutStore
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> the arrangement is stored under.</summary>
    public const string SettingKey = "Workspace.Layout.Tree";

    private readonly ISettingsProvider _settingsProvider;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceLayoutStore"/> class.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="settingsProvider"/> is <see langword="null"/>.</exception>
    public WorkspaceLayoutStore(ISettingsProvider settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _settingsProvider = settingsProvider;

        try
        {
            _settingsProvider.RegisterDefinition(new SettingDefinition(SettingKey, "Workspace Layout", string.Empty));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Idempotent across restarts, exactly as every other Desktop
            // state holder's own registration is.
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(WorkspaceLayoutTree tree, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return _settingsProvider.SetValueAsync(SettingKey, WorkspaceLayoutSerializer.Serialise(tree), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkspaceLayoutTree?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _settingsProvider.GetValueAsync(SettingKey, cancellationToken).ConfigureAwait(false);

        return WorkspaceLayoutSerializer.Deserialise(json);
    }
}
