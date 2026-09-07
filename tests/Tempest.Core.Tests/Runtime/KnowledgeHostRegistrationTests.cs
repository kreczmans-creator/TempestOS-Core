using Tempest.Core.Configuration;
using Tempest.Core.Knowledge.Academy;
using Tempest.Core.Knowledge.Challenges;
using Tempest.Core.Knowledge.Lessons;
using Tempest.Core.Knowledge.Prompts;
using Tempest.Core.Knowledge.WorkedExamples;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Knowledge;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Runtime;

// Registration validation for `Group F` (P06): proves the knowledge layer
// is wired into the real, unmodified TempestHost, that each library is
// one instance over one store, and — the point of the programme — that
// registering it introduced no executor, agent or model binding.
[Collection("Console output capture")]
public class KnowledgeHostRegistrationTests
{
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

    [Theory]
    [InlineData(typeof(IPromptCatalog), typeof(PromptCatalog))]
    [InlineData(typeof(IPromptValidationService), typeof(PromptValidationService))]
    [InlineData(typeof(IAcademyCatalog), typeof(AcademyCatalog))]
    [InlineData(typeof(IAcademyValidationService), typeof(AcademyValidationService))]
    [InlineData(typeof(IChallengeCatalog), typeof(ChallengeCatalog))]
    [InlineData(typeof(IChallengeValidationService), typeof(ChallengeValidationService))]
    [InlineData(typeof(ILessonCatalog), typeof(LessonCatalog))]
    [InlineData(typeof(ILessonValidationService), typeof(LessonValidationService))]
    [InlineData(typeof(IWorkedExampleCatalog), typeof(WorkedExampleCatalog))]
    [InlineData(typeof(IWorkedExampleValidationService), typeof(WorkedExampleValidationService))]
    public async Task Host_RegistersEveryKnowledgeLibraryAndService(Type serviceType, Type expected)
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            Assert.IsType(expected, host.Services!.GetService(serviceType));

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EveryKnowledgeLibrary_IsAnOrdinarySingleton()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            foreach (var serviceType in new[]
                     {
                         typeof(IPromptCatalog), typeof(IAcademyCatalog), typeof(IChallengeCatalog),
                         typeof(ILessonCatalog), typeof(IWorkedExampleCatalog),
                     })
            {
                Assert.Same(host.Services!.GetService(serviceType), host.Services!.GetService(serviceType));
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheAcademy_ResolvesAHierarchyThroughTheRealHost()
    {
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, async host =>
        {
            var academy = (IAcademyCatalog)host.Services!.GetService(typeof(IAcademyCatalog))!;

            await academy.RegisterAsync(
                "sub-probe",
                KnowledgeFixtures.Node("SUB-PROBE", AcademyNodeKind.Subject),
                KnowledgeFixtures.Verified());

            await academy.RegisterAsync(
                "les-probe",
                KnowledgeFixtures.Node("LES-PROBE", AcademyNodeKind.Lesson, "SUB-PROBE"),
                KnowledgeFixtures.Verified());

            var path = await academy.FindPathToAsync("LES-PROBE");

            Assert.Equal(["SUB-PROBE", "LES-PROBE"], path.Select(n => n.Reference));
        });
    }

    [Fact]
    public async Task RegisteringTheKnowledgeLayer_IntroducesNoExecutorOrAgent()
    {
        // The whole point of P06 being a knowledge layer: after the host
        // has composed it, nothing in the container runs a prompt.
        using var temp = new TempDirectory();

        await RunAgainstRunningHostAsync(temp.Path, host =>
        {
            var suspects = typeof(PromptRecord).Assembly
                .GetTypes()
                .Where(t => t.Namespace?.StartsWith("Tempest.Core.Knowledge", StringComparison.Ordinal) == true)
                .Where(t => t.Name.Contains("Executor", StringComparison.OrdinalIgnoreCase)
                            || t.Name.Contains("Agent", StringComparison.OrdinalIgnoreCase)
                            || t.Name.Contains("Runner", StringComparison.OrdinalIgnoreCase)
                            || t.Name.Contains("Completion", StringComparison.OrdinalIgnoreCase)
                            || t.Name.Contains("ModelClient", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(suspects);

            return Task.CompletedTask;
        });
    }
}
