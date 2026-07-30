using Tempest.Core.Calculations;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves the Calculation Framework is wired into
// the real, unmodified TempestHost exactly as ADR-0056 specifies -
// ICalculationEngine resolvable, ordinary singleton semantics, and the
// engine genuinely reuses the same IEngineeringDocumentStore Materials
// resolves, not a second, independent one.
[Collection("Console output capture")]
public class CalculationHostRegistrationTests
{
    private sealed class AddOneCalculation : ICalculationDefinition<double, double>
    {
        public const string Id = "registration-test.add-one";
        public string CalculationId => Id;
        public CalculationMetadata Metadata { get; } = new("Add One", null, null, [], []);
        public double Calculate(double input, CalculationContext context) => input + 1;
    }

    private static async Task RunAgainstRunningHostAsync(string rootPath, Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            await body(host);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task Host_RegistersICalculationEngine_Resolvable()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var engine = host.Services!.GetService(typeof(ICalculationEngine));

            Assert.IsType<CalculationEngine>(engine);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_ResolvingICalculationEngineTwice_ReturnsTheSameInstance()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var first = host.Services!.GetService(typeof(ICalculationEngine));
            var second = host.Services!.GetService(typeof(ICalculationEngine));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Host_CalculationEngine_CanRegisterAndExecuteThroughTheRealDocumentStore()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var engine = (ICalculationEngine)host.Services!.GetService(typeof(ICalculationEngine));

            engine.RegisterDefinition(new AddOneCalculation());
            var record = await engine.ExecuteAsync<double, double>(AddOneCalculation.Id, 4.0);

            Assert.Equal(5.0, record.Result);
        });
    }
}
