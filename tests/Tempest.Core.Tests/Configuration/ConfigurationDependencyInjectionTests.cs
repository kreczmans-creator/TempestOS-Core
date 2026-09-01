using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;

namespace Tempest.Core.Tests.Configuration;

/// <summary>
/// A stand-in for a runtime service consuming configuration via ordinary constructor
/// injection, proving requirement #6 ("configuration shall be available to every
/// runtime service") rather than only proving that <see cref="IConfigurationProvider"/>
/// itself can be resolved directly.
/// </summary>
internal sealed class ConfigurationConsumingService
{
    public ConfigurationConsumingService(IConfigurationProvider configuration)
    {
        RuntimeName = configuration.Get("Runtime:Name");
    }

    public string RuntimeName { get; }
}

public class ConfigurationDependencyInjectionTests
{
    private static IConfigurationProvider BuildConfiguration(params (string Key, string Value)[] entries)
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(new MemoryConfigurationSource(
            entries.Select(entry => new KeyValuePair<string, string>(entry.Key, entry.Value))));

        return builder.Build();
    }

    [Fact]
    public void GetService_ResolvesConfigurationProvider_RegisteredAsInstance()
    {
        var configuration = BuildConfiguration(("Runtime:Name", "TempestOS"));

        var services = new ServiceCollection();
        services.AddInstance(configuration);

        var provider = new TempestServiceProvider(services);

        var resolved = (IConfigurationProvider)provider.GetService(typeof(IConfigurationProvider));

        Assert.Same(configuration, resolved);
    }

    [Fact]
    public void GetService_RuntimeServiceDependingOnConfiguration_ReceivesItViaConstructorInjection()
    {
        var configuration = BuildConfiguration(("Runtime:Name", "TempestOS"));

        var services = new ServiceCollection();
        services.AddInstance(configuration);
        services.Transient<ConfigurationConsumingService>();

        var provider = new TempestServiceProvider(services);

        var consumingService = (ConfigurationConsumingService)provider.GetService(typeof(ConfigurationConsumingService));

        Assert.Equal("TempestOS", consumingService.RuntimeName);
    }

    [Fact]
    public void GetService_ConfigurationProvider_IsTheSameInstanceAcrossMultipleConsumers()
    {
        var configuration = BuildConfiguration(("Runtime:Name", "TempestOS"));

        var services = new ServiceCollection();
        services.AddInstance(configuration);
        services.Transient<ConfigurationConsumingService>();

        var provider = new TempestServiceProvider(services);

        var directlyResolved = (IConfigurationProvider)provider.GetService(typeof(IConfigurationProvider));
        var consumingService = (ConfigurationConsumingService)provider.GetService(typeof(ConfigurationConsumingService));

        Assert.Same(configuration, directlyResolved);
        Assert.Equal("TempestOS", consumingService.RuntimeName);
    }
}
