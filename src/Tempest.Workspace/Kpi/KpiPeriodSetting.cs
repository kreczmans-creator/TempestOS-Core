using Tempest.Core.Settings;

namespace Tempest.Workspace.Kpi;

/// <summary>
/// Persists the Home cockpit's own selected <see cref="KpiPeriod"/>
/// through <see cref="ISettingsProvider"/> (`WP 19.1B`), under
/// <see cref="Key"/> — generalising <c>EvidenceService</c>'s own
/// construction-time <c>Evidence.IndependentCheck</c> registration to a
/// single, fixed key (there is exactly one Home cockpit, unlike the
/// per-principal working-pattern roster <c>WorkingPatternProvider</c>
/// registers).
/// </summary>
public static class KpiPeriodSetting
{
    /// <summary>The <see cref="ISettingsProvider"/> key the selected period is stored under.</summary>
    public const string Key = "Cockpit.KpiPeriod";

    /// <summary>
    /// Registers <see cref="Key"/>'s own definition, defaulting to
    /// <see cref="KpiPeriodPreset.ThisWeek"/>, if nothing has registered
    /// it yet. Idempotent — a second Cockpit construction against the same
    /// <see cref="ISettingsProvider"/> (a test harness restarting a
    /// <c>WorkspaceManager</c> over the same store, say) finds the
    /// definition already there and does nothing further.
    /// </summary>
    public static void EnsureRegistered(ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            settings.RegisterDefinition(new SettingDefinition(Key, "Home cockpit — selected KPI period", KpiPeriodPreset.ThisWeek.ToString()));
        }
        catch (DuplicateSettingDefinitionException)
        {
            // Registered already — by an earlier instance against the same
            // durable store. The definition existing is what matters, not
            // who won the race to register it.
        }
    }

    /// <summary>Reads the persisted period, anchored on <paramref name="today"/>. <see cref="EnsureRegistered"/> must have run first.</summary>
    public static async Task<KpiPeriod> LoadAsync(ISettingsProvider settings, DateOnly today, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var raw = await settings.GetValueAsync(Key, cancellationToken).ConfigureAwait(false);
        return KpiPeriod.Parse(raw, today);
    }

    /// <summary>Persists <paramref name="period"/> as the selected period. <see cref="EnsureRegistered"/> must have run first.</summary>
    public static Task SaveAsync(ISettingsProvider settings, KpiPeriod period, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(period);

        return settings.SetValueAsync(Key, period.Serialize(), cancellationToken);
    }
}
