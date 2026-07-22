using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Logging;

namespace Tempest.Core.Tests.Logging;

public class LoggingServiceCollectionExtensionsTests
{
    private static IConfigurationProvider EmptyConfiguration() => new ConfigurationBuilder().Build();

    [Fact]
    public void AddLogging_RegistersResolvableLogSink()
    {
        var services = new ServiceCollection();
        services.AddLogging(EmptyConfiguration());

        var provider = new TempestServiceProvider(services);

        Assert.IsType<ConsoleLogSink>(provider.GetService<ILogSink>());
    }

    [Fact]
    public void AddLogging_RegistersResolvableLoggerFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging(EmptyConfiguration());

        var provider = new TempestServiceProvider(services);

        Assert.IsType<LoggerFactory>(provider.GetService<ILoggerFactory>());
    }

    [Fact]
    public void AddLogging_RegistersResolvableDefaultLogger()
    {
        var services = new ServiceCollection();
        services.AddLogging(EmptyConfiguration());

        var provider = new TempestServiceProvider(services);

        var logger = provider.GetService<ILogger>();

        Assert.NotNull(logger);
    }

    [Fact]
    public void AddLogging_RegisteredServicesAreSingletons()
    {
        var services = new ServiceCollection();
        services.AddLogging(EmptyConfiguration());

        var provider = new TempestServiceProvider(services);

        Assert.Same(provider.GetService<ILogSink>(), provider.GetService<ILogSink>());
        Assert.Same(provider.GetService<ILoggerFactory>(), provider.GetService<ILoggerFactory>());
        Assert.Same(provider.GetService<ILogger>(), provider.GetService<ILogger>());
    }

    [Fact]
    public void AddLogging_ConsumerDependingOnDefaultLogger_ReceivesItViaConstructorInjection()
    {
        var services = new ServiceCollection();
        services.AddLogging(EmptyConfiguration());
        services.Transient<LoggingConsumer>();

        var provider = new TempestServiceProvider(services);

        var consumer = provider.GetService<LoggingConsumer>();

        Assert.NotNull(consumer.Logger);
    }

    [Fact]
    public void AddLogging_HonoursConfiguredMinimumLevel()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(new MemoryConfigurationSource(new[]
        {
            new KeyValuePair<string, string>(LoggerFactory.MinimumLevelConfigurationKey, "Error"),
        }));

        var services = new ServiceCollection();
        services.AddLogging(builder.Build());

        var provider = new TempestServiceProvider(services);
        var loggerFactory = provider.GetService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Test");

        // The registered sink writes to the console; this test only confirms
        // construction succeeded end-to-end with a non-default configured level,
        // without throwing. Filtering behaviour itself is covered directly by
        // LoggerFactoryTests and LoggerTests.
        logger.Information("filtered");
        logger.Error("kept");
    }

    [Fact]
    public void AddLogging_ThrowsConfigurationException_WhenMinimumLevelIsInvalid()
    {
        var builder = new ConfigurationBuilder();
        builder.AddSource(new MemoryConfigurationSource(new[]
        {
            new KeyValuePair<string, string>(LoggerFactory.MinimumLevelConfigurationKey, "NotARealLevel"),
        }));

        var services = new ServiceCollection();

        Assert.Throws<ConfigurationException>(() => services.AddLogging(builder.Build()));
    }

    [Fact]
    public void AddLogging_ThrowsArgumentNullException_WhenConfigurationIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddLogging(null!));
    }

    private sealed class LoggingConsumer
    {
        public LoggingConsumer(ILogger logger)
        {
            Logger = logger;
        }

        public ILogger Logger { get; }
    }
}
