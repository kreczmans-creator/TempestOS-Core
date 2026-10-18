using Tempest.Core.Configuration;

namespace Tempest.Desktop.Documents.Timesheets;

/// <summary>The folder Export week's own save picker opens in, or why it opens in its normal start instead.</summary>
/// <param name="Folder">The folder to start the picker in, created if it was missing. <see langword="null"/> leaves the picker's own default.</param>
/// <param name="Note">Why <paramref name="Folder"/> is <see langword="null"/> although a folder was configured — said in the status bar. <see langword="null"/> when nothing needs saying.</param>
public sealed record TimesheetExportStart(string? Folder, string? Note);

/// <summary>
/// Where Business → Timesheets → Export week saves by default (runbook G2,
/// Product Owner: "give me a place in settings to set where this goes. By
/// default make it D:\11 Business Admin\02 Timesheets").
/// </summary>
/// <remarks>
/// <para>
/// Resolved in this order: the folder the user chose in Settings
/// (<see cref="UserSettings.TimesheetExportFolder"/>); otherwise
/// <see cref="ConfigurationKey"/> from the platform's own configuration
/// (`ADR-0146`) — set to an empty value or <see cref="OffValue"/> to have no
/// default at all, which is what the Desktop test suite sets so a test run
/// never writes to the real drive; otherwise <see cref="DefaultWindowsFolder"/>
/// on Windows and none anywhere else — the identical shape
/// <c>Tempest.Core.Projects.ProjectFolderOptions</c> already uses for
/// <c>Projects:FolderRoot</c>.
/// </para>
/// </remarks>
public static class TimesheetExportFolder
{
    /// <summary>The configuration key naming the default timesheet export folder.</summary>
    public const string ConfigurationKey = "Timesheets:ExportFolder";

    /// <summary>The <see cref="ConfigurationKey"/> value that switches the default off.</summary>
    public const string OffValue = "off";

    /// <summary>The default on Windows (runbook G2).</summary>
    public const string DefaultWindowsFolder = @"D:\11 Business Admin\02 Timesheets";

    /// <summary>The folder used when the user has chosen none in Settings: <see cref="ConfigurationKey"/>, else <see cref="DefaultWindowsFolder"/> on Windows. <see langword="null"/> for none.</summary>
    /// <param name="configuration">The platform's configuration, or <see langword="null"/> for the defaults alone.</param>
    /// <param name="isWindows">Whether this is a Windows desktop; <see langword="null"/> asks the running OS.</param>
    public static string? Fallback(IConfigurationProvider? configuration, bool? isWindows = null)
    {
        if (configuration is not null && configuration.TryGetValue(ConfigurationKey, out var configured))
        {
            return string.IsNullOrWhiteSpace(configured) || string.Equals(configured.Trim(), OffValue, StringComparison.OrdinalIgnoreCase)
                ? null
                : configured.Trim();
        }

        return (isWindows ?? OperatingSystem.IsWindows()) ? DefaultWindowsFolder : null;
    }

    /// <summary>The effective export folder: <paramref name="userSetting"/> when set, else <see cref="Fallback"/>.</summary>
    public static string? Resolve(string? userSetting, IConfigurationProvider? configuration, bool? isWindows = null) =>
        string.IsNullOrWhiteSpace(userSetting) ? Fallback(configuration, isWindows) : userSetting.Trim();

    /// <summary>
    /// Makes <paramref name="folder"/> ready to start the save picker in —
    /// creating it when missing. A folder on a drive that does not exist, a
    /// relative path, or one that cannot be created falls back to the
    /// picker's own normal start, with the reason in <see cref="TimesheetExportStart.Note"/>.
    /// Never throws.
    /// </summary>
    public static TimesheetExportStart Prepare(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return new TimesheetExportStart(null, null);

        if (!Path.IsPathFullyQualified(folder))
            return new TimesheetExportStart(null, $"Timesheet export folder '{folder}' is not a full path; choose where to save.");

        var root = Path.GetPathRoot(folder);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return new TimesheetExportStart(null, $"Timesheet export folder '{folder}' is unavailable — drive '{root}' does not exist; choose where to save.");

        try
        {
            Directory.CreateDirectory(folder);
            return new TimesheetExportStart(folder, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return new TimesheetExportStart(null, $"Timesheet export folder '{folder}' could not be created ({ex.Message}); choose where to save.");
        }
    }
}
