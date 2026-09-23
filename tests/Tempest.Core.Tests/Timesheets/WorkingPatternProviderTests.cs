using Tempest.Core.EngineeringData;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.Settings;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Timesheets;

/// <summary>`WP 19.0A`: <see cref="WorkingPatternProvider"/> — the per-principal working-pattern setting, registered lazily.</summary>
public sealed class WorkingPatternProviderTests
{
    [Fact]
    public async Task ANeverRegisteredPrincipal_DefaultsTo37Point5HoursAWeek()
    {
        var provider = new WorkingPatternProvider(BuildSettingsProvider());

        var hours = await provider.AvailableHoursAsync("never-seen-principal", new DateOnly(2026, 3, 2));

        Assert.Equal(37.5m, hours);
    }

    [Fact]
    public async Task EnsureRegisteredAsync_IsIdempotent_AcrossRepeatedCalls()
    {
        var settings = BuildSettingsProvider();
        var provider = new WorkingPatternProvider(settings);

        await provider.EnsureRegisteredAsync("idempotent-principal");
        await provider.EnsureRegisteredAsync("idempotent-principal");
        await provider.EnsureRegisteredAsync("idempotent-principal");

        // No DuplicateSettingDefinitionException surfaced, and the value is
        // still readable at the default.
        var hours = await provider.AvailableHoursAsync("idempotent-principal", new DateOnly(2026, 3, 2));
        Assert.Equal(37.5m, hours);
    }

    [Fact]
    public async Task AnOverriddenPattern_ReadsBackTheOverride_NotTheDefault()
    {
        var settings = BuildSettingsProvider();
        var provider = new WorkingPatternProvider(settings);

        await provider.EnsureRegisteredAsync("part-time-principal");
        await settings.SetValueAsync(WorkingPatternProvider.SettingKeyFor("part-time-principal"), "22.5");

        var hours = await provider.AvailableHoursAsync("part-time-principal", new DateOnly(2026, 3, 2));

        Assert.Equal(22.5m, hours);
    }

    [Fact]
    public async Task TwoDifferentPrincipals_KeepTheirOwnIndependentPatterns()
    {
        var settings = BuildSettingsProvider();
        var provider = new WorkingPatternProvider(settings);

        await provider.EnsureRegisteredAsync("principal-a");
        await settings.SetValueAsync(WorkingPatternProvider.SettingKeyFor("principal-a"), "40");

        var principalAHours = await provider.AvailableHoursAsync("principal-a", new DateOnly(2026, 3, 2));
        var principalBHours = await provider.AvailableHoursAsync("principal-b", new DateOnly(2026, 3, 2));

        Assert.Equal(40m, principalAHours);
        Assert.Equal(37.5m, principalBHours);
    }

    private static ISettingsProvider BuildSettingsProvider()
    {
        var persistenceStore = new InMemoryPersistenceStore();
        return new SettingsProvider(persistenceStore, new EventBus(), (ILogger?)null);
    }
}
