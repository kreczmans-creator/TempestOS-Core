using System.Diagnostics;
using System.Reflection;
using Tempest.Core.Modules;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Templates;

// Proves the real, shipped template content - not a copy, not a mock of
// it - produces a genuinely working, discoverable module once
// substituted, built, and loaded, exactly as WP 5.3's own Acceptance
// Criteria requires ("a new contributor can scaffold a working module
// using a template alone"). Substitution is performed by hand here,
// mirroring exactly what dotnet new's sourceName/symbol replacement
// mechanism does, rather than by invoking `dotnet new install` itself -
// which would mutate the shared, global template cache during an
// automated test run. A one-off manual verification using the real
// `dotnet new` CLI was performed separately (see this Work Package's own
// retrospective) to prove the template.json manifest itself is accepted
// by the real templating engine; this test proves the template's own
// file content compiles and is discoverable, using the real compiler and
// the real, unmodified ReflectionFrameworkDiscoveryService.
[Collection("Console output capture")]
public class ModuleTemplateContentTests
{
    private const string SourceNameToken = "TempestSampleModule";

    private static string Substitute(string content, string moduleName, string moduleId, string displayName, string version) =>
        content
            .Replace(SourceNameToken, moduleName, StringComparison.Ordinal)
            .Replace("TEMPEST_MODULE_ID", moduleId, StringComparison.Ordinal)
            .Replace("TEMPEST_MODULE_DISPLAY_NAME", displayName, StringComparison.Ordinal)
            .Replace("TEMPEST_MODULE_VERSION", version, StringComparison.Ordinal);

    private static async Task<(int ExitCode, string Output)> RunDotNetAsync(string arguments, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)!;
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await stdOutTask + await stdErrTask;
        return (process.ExitCode, output);
    }

    [Fact]
    public async Task GeneratedModule_OnceSubstitutedAndBuilt_CompilesAndIsDiscoverable()
    {
        const string moduleName = "ContentTestModule";
        const string moduleId = "tempest.tests.content-test-module";
        const string displayName = "Content Test Module";
        const string version = "2.0.0";

        using var temp = new TempDirectory();

        var templateDir = RepositoryPaths.ModuleTemplateDirectory;
        var csprojSource = Substitute(
            File.ReadAllText(Path.Combine(templateDir, "TempestSampleModule.csproj")),
            moduleName, moduleId, displayName, version);
        var csSource = Substitute(
            File.ReadAllText(Path.Combine(templateDir, "TempestSampleModule.cs")),
            moduleName, moduleId, displayName, version);

        // The template's own ProjectReference is a relative path assuming
        // the generated project lives as a sibling of Tempest.Samples
        // (src/Samples/<Name>/) - rewritten here to an absolute path so
        // this test can build the substituted content from any temp
        // directory, without relying on matching that exact layout.
        var tempestCoreCsproj = Path.Combine(RepositoryPaths.RepositoryRoot, "src", "Tempest.Core", "Tempest.Core.csproj");
        csprojSource = csprojSource.Replace(
            @"..\..\Tempest.Core\Tempest.Core.csproj", tempestCoreCsproj, StringComparison.Ordinal);

        var csprojPath = Path.Combine(temp.Path, $"{moduleName}.csproj");
        File.WriteAllText(csprojPath, csprojSource);
        File.WriteAllText(Path.Combine(temp.Path, $"{moduleName}.cs"), csSource);

        var (exitCode, output) = await RunDotNetAsync($"build \"{csprojPath}\" -c Debug", temp.Path);

        Assert.True(exitCode == 0, $"Generated module failed to build:{Environment.NewLine}{output}");

        var builtAssemblyPath = Path.Combine(temp.Path, "bin", "Debug", "net10.0", $"{moduleName}.dll");
        Assert.True(File.Exists(builtAssemblyPath), $"Expected build output not found at '{builtAssemblyPath}'.");

        var assembly = Assembly.LoadFrom(builtAssemblyPath);
        var discoveryService = new ReflectionFrameworkDiscoveryService([assembly]);
        var descriptor = Assert.Single(discoveryService.DiscoverModules());

        Assert.Equal(moduleId, descriptor.Id);
        Assert.Equal(displayName, descriptor.Name);
        Assert.Equal(version, descriptor.Version);
        Assert.Equal(moduleName, descriptor.ModuleType.Name);
    }
}
