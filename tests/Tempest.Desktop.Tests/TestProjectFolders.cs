using System.Runtime.CompilerServices;
using Tempest.Core.Configuration;
using Tempest.Core.Projects;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Switches project folder generation off for the whole Desktop test run
/// (PO decision 2026-10-01): on a Windows machine with a D: drive the
/// shell would otherwise file every project a test opens under the real
/// <see cref="ProjectFolderOptions.DefaultWindowsRoot"/>. Set through the
/// platform's own environment-variable configuration source
/// (<see cref="MicrosoftExtensionsConfigurationSource.EnvironmentVariablePrefix"/>,
/// <c>__</c> as the section separator), so every <see cref="WorkspaceHost"/>
/// a test builds reads it exactly as a real launch would; a test that
/// wants folders supplies its own <see cref="Tempest.Workspace.Projects.ProjectFolderLocator"/>
/// over a temp root.
/// </summary>
internal static class TestProjectFolders
{
    [ModuleInitializer]
    internal static void SwitchOff() =>
        Environment.SetEnvironmentVariable(
            MicrosoftExtensionsConfigurationSource.EnvironmentVariablePrefix + ProjectFolderOptions.FolderRootKey.Replace(":", "__", StringComparison.Ordinal),
            ProjectFolderOptions.OffValue);
}
