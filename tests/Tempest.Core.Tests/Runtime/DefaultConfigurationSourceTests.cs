using Tempest.Core.Configuration;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

/// <summary>
/// Proves <see cref="TempestHostBuilder"/>'s own default configuration
/// wiring (`WP 17.2A`, ADR-0146): the operator-reachable
/// <see cref="MicrosoftExtensionsConfigurationSource"/> is always applied
/// first, so an explicit <see cref="ITempestHostBuilder.AddConfigurationSource"/>
/// call — the shape every isolated test root and the Desktop's own
/// persistence-root override already use — always wins over an
/// environment variable, and <see cref="ITempestHostBuilder.AddCommandLineArgs"/>
/// reaches the built <see cref="IConfigurationProvider"/>.
/// </summary>
public class DefaultConfigurationSourceTests
{
    private const string EnvironmentVariableName = "TEMPEST_Runtime__Precedence__HostKey";

    private static async Task RunAgainstRunningHostAsync(ITempestHostBuilder builder, Func<ITempestHost, Task> body)
    {
        var host = builder.Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        await body(host);

        await host.StopAsync();
        await runTask;
    }

    [Fact]
    public async Task EnvironmentVariable_ReachesConfiguration_WhenNothingElseOverridesIt()
    {
        using var scoped = new ScopedEnvironmentVariable(EnvironmentVariableName, "from-environment");

        var builder = new TempestHostBuilder(Type.EmptyTypes).WithIsolatedPersistenceRoot();

        await RunAgainstRunningHostAsync(builder, host =>
        {
            var configuration = (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider));

            Assert.Equal("from-environment", configuration.Get("Runtime:Precedence:HostKey"));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ExplicitInMemorySource_OverridesTheEnvironmentVariable()
    {
        using var scoped = new ScopedEnvironmentVariable(EnvironmentVariableName, "from-environment");

        var builder = new TempestHostBuilder(Type.EmptyTypes)
            .WithIsolatedPersistenceRoot()
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>("Runtime:Precedence:HostKey", "from-in-memory-override"),
            ]));

        await RunAgainstRunningHostAsync(builder, host =>
        {
            var configuration = (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider));

            Assert.Equal("from-in-memory-override", configuration.Get("Runtime:Precedence:HostKey"));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task CommandLineArgs_ReachConfiguration_AndOverrideTheEnvironmentVariable()
    {
        using var scoped = new ScopedEnvironmentVariable(EnvironmentVariableName, "from-environment");

        var builder = new TempestHostBuilder(Type.EmptyTypes)
            .WithIsolatedPersistenceRoot()
            .AddCommandLineArgs(["--Runtime:Precedence:HostKey=from-command-line"]);

        await RunAgainstRunningHostAsync(builder, host =>
        {
            var configuration = (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider));

            Assert.Equal("from-command-line", configuration.Get("Runtime:Precedence:HostKey"));

            return Task.CompletedTask;
        });
    }

    /// <summary>Sets an environment variable for the life of this instance, restoring whatever it was before.</summary>
    private sealed class ScopedEnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public ScopedEnvironmentVariable(string name, string value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _originalValue);
    }
}
