using Tempest.Core.Configuration;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// The Dashboard Export capability's own two configuration values — where
/// <see cref="EngineeringStatusExportAdapter"/>/<see cref="ProgrammeHierarchyExportAdapter"/>'s
/// two JSON files are written, and how often
/// <see cref="DashboardExportHostedService"/> re-exports them. Read from
/// <see cref="IConfigurationProvider"/> with a sensible default when unset —
/// mirrors <c>Tempest.Core.Evidence.EvidenceService</c>'s own
/// "<see cref="IConfigurationProvider"/> first, with a defaulted fallback"
/// convention (this capability has no <c>ISettingsProvider</c>-backed
/// runtime override; a directory path and a re-export interval are
/// deployment-time facts, not something a user changes from a Settings
/// screen mid-session).
/// </summary>
/// <param name="ExportDirectory">The directory the two dashboard export files are written to.</param>
/// <param name="IntervalSeconds">How often, in seconds, a fresh pair of files is written.</param>
public sealed record DashboardExportOptions(string ExportDirectory, int IntervalSeconds)
{
    /// <summary>The <see cref="IConfigurationProvider"/> key naming the export directory.</summary>
    public const string ExportDirectoryConfigurationKey = "DashboardExport:Directory";

    /// <summary>The <see cref="IConfigurationProvider"/> key naming the re-export interval, in seconds.</summary>
    public const string IntervalSecondsConfigurationKey = "DashboardExport:IntervalSeconds";

    /// <summary>
    /// The default re-export interval — five minutes, matching
    /// Tempest-Dashboard's own <c>connectors.projects</c>/<c>engineering</c>
    /// <c>pollSeconds: 300</c> (<c>config/default.json</c>), so a fresh
    /// export is never older than what the Dashboard would poll for anyway.
    /// </summary>
    public const int DefaultIntervalSeconds = 300;

    /// <summary>Reads both values from <paramref name="configuration"/>, falling back to a sensible default for either that is absent, blank, or not a positive integer.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
    public static DashboardExportOptions FromConfiguration(IConfigurationProvider configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var directory =
            configuration.TryGetValue(ExportDirectoryConfigurationKey, out var configuredDirectory) && !string.IsNullOrWhiteSpace(configuredDirectory)
                ? configuredDirectory
                : DefaultExportDirectory();

        var interval =
            configuration.TryGetValue(IntervalSecondsConfigurationKey, out var configuredInterval)
            && int.TryParse(configuredInterval, out var parsedInterval) && parsedInterval > 0
                ? parsedInterval
                : DefaultIntervalSeconds;

        return new DashboardExportOptions(directory, interval);
    }

    /// <summary>
    /// Core's own desktop host platform is Windows (`ADR-0094`'s own
    /// Avalonia.Desktop target): <c>%ProgramData%\Tempest\dashboard-export</c>
    /// matches where the existing Dashboard telemetry agent already writes
    /// its own state (Tempest-Dashboard <c>agents/README.md</c>:
    /// <c>%ProgramData%\Tempest\</c>). A non-Windows composition root (the
    /// console harness, CI) falls back to a conventional Unix state
    /// directory instead of an environment-variable expansion that would
    /// never resolve there.
    /// </summary>
    private static string DefaultExportDirectory() =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Tempest", "dashboard-export")
            : "/var/lib/tempest/dashboard-export";
}
