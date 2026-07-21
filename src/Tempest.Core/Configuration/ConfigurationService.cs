namespace Tempest.Core.Configuration;

/// <summary>
/// Provides access to the application configuration.
/// </summary>
public class ConfigurationService
{
    public ApplicationConfiguration Load()
    {
        return new ApplicationConfiguration();
    }
}