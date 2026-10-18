using System.Runtime.CompilerServices;
using Tempest.Core.Configuration;
using Tempest.Desktop.Documents.Timesheets;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Switches the default timesheet export folder off for the whole Desktop
/// test run (runbook G2), exactly as <see cref="TestProjectFolders"/> does
/// for project folders: on a Windows machine with a D: drive, Export week
/// would otherwise create and start in the real
/// <see cref="TimesheetExportFolder.DefaultWindowsFolder"/>. A test that
/// wants an export folder saves its own temp folder through Settings,
/// which takes precedence over this configuration value.
/// </summary>
internal static class TestTimesheetExportFolder
{
    [ModuleInitializer]
    internal static void SwitchOff() =>
        Environment.SetEnvironmentVariable(
            MicrosoftExtensionsConfigurationSource.EnvironmentVariablePrefix + TimesheetExportFolder.ConfigurationKey.Replace(":", "__", StringComparison.Ordinal),
            TimesheetExportFolder.OffValue);
}
