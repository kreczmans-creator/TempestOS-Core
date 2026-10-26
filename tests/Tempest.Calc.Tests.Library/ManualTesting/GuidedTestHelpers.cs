using System.Text.Json;
using Tempest.Calc.Library.ManualTesting;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>A small hand-written script for the service tests (the shipped one is exercised by the script tests), and the repository root.</summary>
internal static class GuidedTestHelpers
{
    /// <summary>Two examples of <c>t.a</c>, one of <c>t.b</c> and a manual step.</summary>
    public static GuidedTestScript SmallScript(string version = "9.9.9+aaaaaaaa") => GuidedTestScript.Parse(SmallScriptJson(version));

    public static string SmallScriptJson(string version = "9.9.9+aaaaaaaa", int exampleCount = 3)
    {
        var steps = new List<object>
        {
            new { id = "smoke-1", kind = "Smoke", section = "4", title = "Smoke test 1: start the app", instructions = new[] { "Start it." }, passWhen = "It starts." },
        };

        for (var i = 0; i < exampleCount; i++)
        {
            var template = i < 2 ? "t.a" : "t.b";
            steps.Add(new
            {
                id = $"example:{template}/e{i}",
                kind = "Example",
                section = "5",
                title = $"Example {i}",
                templateId = template,
                exampleId = $"e{i}",
                inputsJson = "[]",
                expected = new[] { new { symbol = "P", value = "3.93", unit = "kN" } },
            });
        }

        return JsonSerializer.Serialize(new { format = 1, scriptVersion = version, release = "9.9.9", steps });
    }

    public static string RepositoryRoot { get; } = FindRoot();

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VERSION")))
            dir = dir.Parent;

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
