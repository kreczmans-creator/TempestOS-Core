using System.Collections.Concurrent;
using System.Globalization;
using Tempest.Core.Settings;

namespace Tempest.Core.Timesheets;

/// <summary>
/// Each principal's own working pattern — hours available per week — the
/// denominator utilisation (`WP 19.1B`) reads. Registered lazily,
/// per-identity, through <see cref="ISettingsProvider"/> (`WP 19.0A`,
/// `ADR-0150`), exactly as <c>EvidenceService</c> registers
/// <c>Evidence.IndependentCheck</c> — except keyed per principal rather
/// than once, since there is no fixed roster of principals to register
/// up front.
/// </summary>
public interface IWorkingPatternProvider
{
    /// <summary>
    /// The hours <paramref name="identityId"/> has available in the week
    /// starting <paramref name="weekStart"/> — today, a flat weekly figure
    /// with no date variation of its own, so every week reads the same
    /// value; the parameter exists because utilisation reads this per
    /// period (`WP 19.1B`) and a future working-pattern change (a new
    /// starter's part-time ramp, say) would need it to vary by week.
    /// </summary>
    Task<decimal> AvailableHoursAsync(string identityId, DateOnly weekStart, CancellationToken cancellationToken = default);

    /// <summary>Registers <paramref name="identityId"/>'s own working-pattern setting, at the default, if nothing has registered it yet. Idempotent.</summary>
    Task EnsureRegisteredAsync(string identityId, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IWorkingPatternProvider"/> implementation.</summary>
public sealed class WorkingPatternProvider : IWorkingPatternProvider
{
    /// <summary>The hours per week a working pattern carries until a principal or an operator says otherwise.</summary>
    public const decimal DefaultHoursPerWeek = 37.5m;

    private readonly ISettingsProvider _settings;
    private readonly ConcurrentDictionary<string, byte> _attempted = new(StringComparer.Ordinal);

    /// <summary>Initialises a new instance of the <see cref="WorkingPatternProvider"/> class.</summary>
    public WorkingPatternProvider(ISettingsProvider settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <summary>The <see cref="ISettingDefinition.Key"/> <paramref name="identityId"/>'s own working pattern is stored under.</summary>
    public static string SettingKeyFor(string identityId) => $"Timesheet.WorkingPattern:{identityId}";

    /// <inheritdoc />
    public async Task<decimal> AvailableHoursAsync(string identityId, DateOnly weekStart, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);

        await EnsureRegisteredAsync(identityId, cancellationToken).ConfigureAwait(false);

        var raw = await _settings.GetValueAsync(SettingKeyFor(identityId), cancellationToken).ConfigureAwait(false);

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var hours) ? hours : DefaultHoursPerWeek;
    }

    /// <inheritdoc />
    public Task EnsureRegisteredAsync(string identityId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityId);

        if (_attempted.TryAdd(identityId, 0))
        {
            try
            {
                _settings.RegisterDefinition(new SettingDefinition(
                    SettingKeyFor(identityId),
                    $"Working pattern — hours per week ({identityId})",
                    DefaultHoursPerWeek.ToString(CultureInfo.InvariantCulture)));
            }
            catch (DuplicateSettingDefinitionException)
            {
                // Registered already — by an earlier call this process made,
                // or a concurrent one. The definition existing is what
                // matters; who won the race to register it does not.
            }
        }

        return Task.CompletedTask;
    }
}
