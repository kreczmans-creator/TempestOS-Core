namespace Tempest.Core.Configuration;

public class ApplicationConfiguration
{
    /// <summary>
    /// Root location for all Tempest workspace data.
    /// Example: C:\Tempest
    /// </summary>
    public string WorkspaceRoot { get; set; } = @"C:\Tempest";

    /// <summary>
    /// Relative folder names within the workspace.
    /// </summary>
    public string ProjectDirectory { get; set; } = "Projects";

    public string LogDirectory { get; set; } = "Logs";

    public string ConfigurationDirectory { get; set; } = "Configuration";

    public string TemplateDirectory { get; set; } = "Templates";

    public string LibraryDirectory { get; set; } = "Libraries";

    public string PluginDirectory { get; set; } = "Plugins";

    /// <summary>
    /// Convenience properties returning absolute paths.
    /// These keep path-building in one place.
    /// </summary>
    public string ProjectsPath =>
        Path.Combine(WorkspaceRoot, ProjectDirectory);

    public string LogsPath =>
        Path.Combine(WorkspaceRoot, LogDirectory);

    public string ConfigurationPath =>
        Path.Combine(WorkspaceRoot, ConfigurationDirectory);

    public string TemplatesPath =>
        Path.Combine(WorkspaceRoot, TemplateDirectory);

    public string LibrariesPath =>
        Path.Combine(WorkspaceRoot, LibraryDirectory);

    public string PluginsPath =>
        Path.Combine(WorkspaceRoot, PluginDirectory);
}