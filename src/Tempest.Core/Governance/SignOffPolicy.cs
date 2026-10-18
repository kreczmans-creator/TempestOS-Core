using System.Globalization;
using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.Settings;

namespace Tempest.Core.Governance;

/// <summary>
/// The concrete <see cref="ISignOffPolicy"/> — one runtime-mutable setting,
/// <see cref="SettingKey"/>, persisted through <see cref="ISettingsProvider"/>
/// so it survives a restart and reads the same from every module
/// (`ADR-0161`).
/// </summary>
/// <remarks>
/// The setting's default is <see langword="false"/> (a one-person
/// consultancy), unless configuration says otherwise under
/// <see cref="ConfigurationKey"/> — the identical "configured default,
/// persisted value wins" shape <c>EvidenceService</c> already gives
/// <c>Evidence:IndependentCheck</c>.
/// </remarks>
public sealed class SignOffPolicy : ISignOffPolicy
{
    /// <summary>The <see cref="ISettingsProvider"/> key the switch is stored under.</summary>
    public const string SettingKey = "Governance.SecondPersonSignOff";

    /// <summary>The configuration key that, when set to <c>true</c>, makes second-person sign-off the default before anybody has changed it.</summary>
    public const string ConfigurationKey = "Governance:SecondPersonSignOff";

    /// <summary>The <see cref="IAuditRecord.Action"/> recorded when the switch changes.</summary>
    public const string ChangedActionName = "governance.second-person-sign-off.changed";

    /// <summary>The display name the setting is registered under, and the label Settings shows.</summary>
    public const string DisplayName = "Second-person sign-off";

    /// <summary>The wording an approval or check record carries when it was a self-approval allowed because the switch is off.</summary>
    public const string SelfApprovalNote = "self-approval: second-person sign-off is off";

    private readonly ISettingsProvider _settings;
    private readonly IAuditRecorder? _audit;

    /// <summary>Initialises a new instance of the <see cref="SignOffPolicy"/> class.</summary>
    /// <param name="settings">Where the switch is stored.</param>
    /// <param name="configuration">Where an operator may set the default (<see cref="ConfigurationKey"/>).</param>
    /// <param name="audit">Where a change of the switch is recorded. <see langword="null"/> only in a host with no audit composed.</param>
    public SignOffPolicy(ISettingsProvider settings, IConfigurationProvider configuration, IAuditRecorder? audit = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(configuration);

        _settings = settings;
        _audit = audit;

        var configuredDefault =
            configuration.TryGetValue(ConfigurationKey, out var raw)
            && bool.TryParse(raw, out var parsed)
            && parsed;

        try
        {
            _settings.RegisterDefinition(new SettingDefinition(SettingKey, DisplayName, Format(configuredDefault)));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Registered already — a second policy over the same provider
            // (a test host restarted over the same in-memory provider).
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsSecondPersonRequiredAsync(CancellationToken cancellationToken = default)
    {
        var value = await _settings.GetValueAsync(SettingKey, cancellationToken).ConfigureAwait(false);
        return bool.TryParse(value, out var required) && required;
    }

    /// <inheritdoc />
    public async Task<bool> SetSecondPersonRequiredAsync(bool required, CancellationToken cancellationToken = default)
    {
        var previous = await IsSecondPersonRequiredAsync(cancellationToken).ConfigureAwait(false);
        if (previous == required)
            return false;

        await _settings.SetValueAsync(SettingKey, Format(required), cancellationToken).ConfigureAwait(false);

        if (_audit is not null)
        {
            await _audit.RecordAsync(
                ChangedActionName,
                new Dictionary<string, string>
                {
                    ["Subject"] = SettingKey,
                    ["OldValue"] = Describe(previous),
                    ["NewValue"] = Describe(required),
                },
                cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>"On" or "Off", as the audit row and Settings say it.</summary>
    public static string Describe(bool required) => required ? "On" : "Off";

    private static string Format(bool value) => value.ToString(CultureInfo.InvariantCulture);
}
