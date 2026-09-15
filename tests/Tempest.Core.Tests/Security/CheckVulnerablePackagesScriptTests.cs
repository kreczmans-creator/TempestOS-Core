using System.Diagnostics;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Security;

/// <summary>
/// `WP 21.5E` / `WP RC.0C` ("Dependency vulnerability scan is a required
/// CI check"). <c>scripts/check-vulnerable-packages.ps1</c> is the parser
/// that turns <c>dotnet list package --vulnerable</c>'s own always-exits-0
/// report into an actual CI failure. These tests invoke the real script
/// file as a real <see cref="Process"/> — the same way
/// <c>ci.yml</c>'s own "Scan for vulnerable packages" and "Run governance
/// health check" steps do (<c>powershell -NoProfile -File ...</c>) — so a
/// pass here is evidence about the script CI actually runs, not a
/// reimplementation of its parsing logic that could drift from it.
/// </summary>
public sealed class CheckVulnerablePackagesScriptTests
{
    private static string ScriptPath { get; } =
        Path.Combine(RepositoryPaths.RepositoryRoot, "scripts", "check-vulnerable-packages.ps1");

    private static async Task<(int ExitCode, string Output)> RunAsync(string arguments)
    {
        var startInfo = new ProcessStartInfo("powershell", $"-NoProfile -File \"{ScriptPath}\" {arguments}")
        {
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
    public void TheScriptFile_ExistsAtTheKnownPath()
    {
        // A prerequisite for every test below, asserted on its own so a
        // moved or deleted script fails here with a clear reason rather
        // than as an opaque non-zero exit code from `powershell` itself.
        Assert.True(File.Exists(ScriptPath), $"Expected the dependency-scan parser at '{ScriptPath}'.");
    }

    [Fact]
    public async Task SelfTest_PassesItsOwnTwoFixtures()
    {
        // The script's own -SelfTest mode (a clean fixture and a synthetic
        // vulnerable-line fixture, asserted internally) — see the script's
        // own doc comment. Exercised here so a regression in either
        // fixture, or in the parser both share, fails the ordinary test
        // suite rather than only a future CI run of ci.yml itself.
        var (exitCode, output) = await RunAsync("-SelfTest");

        Assert.Equal(0, exitCode);
        Assert.Contains("All self-test assertions passed.", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACleanScan_ExitsZero()
    {
        using var temp = new TempDirectory();
        var inputPath = Path.Combine(temp.Path, "clean.log");

        // The exact shape `dotnet list package --vulnerable` prints for
        // this solution's own eight projects (WP 21.5E, 2026-09-15
        // baseline) when nothing is found.
        await File.WriteAllLinesAsync(inputPath,
        [
            "The following sources were used:",
            "   https://api.nuget.org/v3/index.json",
            "",
            "The given project `Tempest.Core` has no vulnerable packages given the current sources.",
            "The given project `Tempest.Desktop` has no vulnerable packages given the current sources.",
        ]);

        var (exitCode, output) = await RunAsync($"-InputPath \"{inputPath}\"");

        Assert.Equal(0, exitCode);
        Assert.Contains("no vulnerable packages", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ASyntheticVulnerableLine_ExitsNonZero()
    {
        using var temp = new TempDirectory();
        var inputPath = Path.Combine(temp.Path, "vulnerable.log");

        // A synthetic finding, in the exact shape `dotnet list package
        // --vulnerable` prints one — this is the failure the whole step
        // exists to catch; a real advisory ID is never referenced, so this
        // test can never itself go stale as one is patched.
        await File.WriteAllLinesAsync(inputPath,
        [
            "The following sources were used:",
            "   https://api.nuget.org/v3/index.json",
            "",
            "The given project `Tempest.Core` has no vulnerable packages given the current sources.",
            "Project `Tempest.Desktop` has the following vulnerable packages",
            "   [net10.0]:",
            "   Top-level Package      Requested   Resolved   Severity   Advisory URL",
            "   > Synthetic.TestOnly   1.0.0       1.0.0      High       https://example.invalid/GHSA-synthetic-test-0000",
        ]);

        var (exitCode, output) = await RunAsync($"-InputPath \"{inputPath}\"");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Vulnerable package(s) reported", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnparsableScan_ExitsNonZero_RatherThanSilentlyPassing()
    {
        // No recognisable "<project> has ..." line at all — a crashed or
        // wholly reshaped scan. Silence must never read as "0
        // vulnerabilities found"; see the script's own remarks.
        using var temp = new TempDirectory();
        var inputPath = Path.Combine(temp.Path, "unparsable.log");

        await File.WriteAllLinesAsync(inputPath, ["some unrelated restore noise", ""]);

        var (exitCode, output) = await RunAsync($"-InputPath \"{inputPath}\"");

        Assert.NotEqual(0, exitCode);
        Assert.Contains("could not confirm any project was actually scanned", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NeitherSelfTestNorInputPath_ExitsNonZero_WithAClearReason()
    {
        var (exitCode, output) = await RunAsync(string.Empty);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Either -SelfTest or -InputPath must be supplied", output, StringComparison.Ordinal);
    }
}
