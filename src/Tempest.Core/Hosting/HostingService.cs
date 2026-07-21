using Tempest.Core.Configuration;

namespace Tempest.Core.Hosting;

public class HostingService
{
    public void Initialise(ApplicationConfiguration configuration)
    {
        Directory.CreateDirectory(configuration.WorkspaceRoot);

        Directory.CreateDirectory(configuration.ProjectsPath);
        Directory.CreateDirectory(configuration.LogsPath);
        Directory.CreateDirectory(configuration.ConfigurationPath);
        Directory.CreateDirectory(configuration.TemplatesPath);
        Directory.CreateDirectory(configuration.LibrariesPath);
        Directory.CreateDirectory(configuration.PluginsPath);
    }
}