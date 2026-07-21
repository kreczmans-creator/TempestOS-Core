using Tempest.Core.Configuration;
using Tempest.Core.Hosting;
using Tempest.Core.Logging;

namespace Tempest.Core.Bootstrap;

public class BootstrapService
{
    public ApplicationConfiguration Initialise()
    {
        var configurationService = new ConfigurationService();
        var configuration = configurationService.Load();

        var hostingService = new HostingService();
        hostingService.Initialise(configuration);

        var logger = new LoggingService(configuration.LogsPath);

        logger.Information("Workspace initialised.");
        logger.Information("Configuration loaded.");
        logger.Information("Bootstrap completed.");

        return configuration;
    }
}