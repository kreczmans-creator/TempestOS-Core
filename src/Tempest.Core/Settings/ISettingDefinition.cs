namespace Tempest.Core.Settings;

/// <summary>Describes one setting — identity, default value, and type. Immutable.</summary>
public interface ISettingDefinition
{
    /// <summary>Gets the setting's stable, unique key.</summary>
    string Key { get; }

    /// <summary>Gets a human-readable display name.</summary>
    string DisplayName { get; }

    /// <summary>Gets the value used when nothing has been persisted yet.</summary>
    string DefaultValue { get; }
}
