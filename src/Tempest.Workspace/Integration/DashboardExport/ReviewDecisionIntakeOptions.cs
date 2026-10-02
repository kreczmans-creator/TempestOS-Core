using Tempest.Core.Configuration;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// The review-decisions capability's own three configuration values —
/// whether it runs at all, where it reads intent files from, and how
/// often. `ADR-0162`: switched off by default, both ends, independently;
/// <see cref="Enabled"/> is this end's own half of that. Mirrors
/// <see cref="DashboardExportOptions"/>'s own "<see cref="IConfigurationProvider"/>
/// first, with a defaulted fallback" convention exactly — a deployment-time
/// fact, not something a user changes from a Settings screen mid-session.
/// </summary>
/// <param name="Enabled">Whether <see cref="ReviewDecisionIntakeHostedService"/> runs its loop at all. Default <see langword="false"/> — when off, the intake directory is never even read.</param>
/// <param name="IntakeDirectory">The directory <c>agents/tempest-core-agent.*</c> writes pulled intent files to, and this service reads and consumes them from.</param>
/// <param name="IntervalSeconds">How often, in seconds, the intake directory is polled.</param>
public sealed record ReviewDecisionIntakeOptions(bool Enabled, string IntakeDirectory, int IntervalSeconds)
{
    /// <summary>The <see cref="IConfigurationProvider"/> key naming whether this capability is switched on.</summary>
    public const string EnabledConfigurationKey = "ReviewDecisions:Enabled";

    /// <summary>The <see cref="IConfigurationProvider"/> key naming the intake directory.</summary>
    public const string IntakeDirectoryConfigurationKey = "ReviewDecisions:IntakeDirectory";

    /// <summary>The <see cref="IConfigurationProvider"/> key naming the poll interval, in seconds.</summary>
    public const string IntervalSecondsConfigurationKey = "ReviewDecisions:IntervalSeconds";

    /// <summary>
    /// The default poll interval — matches <see cref="DashboardExportOptions.DefaultIntervalSeconds"/>:
    /// a decision is never waiting on this service longer than the export
    /// cycle that will next report it gone from the queue.
    /// </summary>
    public const int DefaultIntervalSeconds = DashboardExportOptions.DefaultIntervalSeconds;

    /// <summary>Reads all three values from <paramref name="configuration"/>, falling back to a sensible default for any that is absent, blank, or (for the two non-boolean values) malformed.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    public static ReviewDecisionIntakeOptions FromConfiguration(IConfigurationProvider configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var enabled =
            configuration.TryGetValue(EnabledConfigurationKey, out var configuredEnabled)
            && bool.TryParse(configuredEnabled, out var parsedEnabled) && parsedEnabled;

        var directory =
            configuration.TryGetValue(IntakeDirectoryConfigurationKey, out var configuredDirectory) && !string.IsNullOrWhiteSpace(configuredDirectory)
                ? configuredDirectory
                : DefaultIntakeDirectory();

        var interval =
            configuration.TryGetValue(IntervalSecondsConfigurationKey, out var configuredInterval)
            && int.TryParse(configuredInterval, out var parsedInterval) && parsedInterval > 0
                ? parsedInterval
                : DefaultIntervalSeconds;

        return new ReviewDecisionIntakeOptions(enabled, directory, interval);
    }

    /// <summary>
    /// A sibling of <see cref="DashboardExportOptions"/>'s own default
    /// export directory, not inside it — the two are read/written by
    /// different processes on different schedules and must never share a
    /// directory listing or an atomic-rename collision.
    /// </summary>
    private static string DefaultIntakeDirectory() =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Tempest", "review-decisions")
            : "/var/lib/tempest/review-decisions";
}
