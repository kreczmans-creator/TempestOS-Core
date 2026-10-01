using System.Text;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.ReleaseEngineering;

/// <summary>
/// `scripts/install-test-build.ps1` (PO decision 2026-10-01: every build
/// handed over for testing ships with its own installer and a versioned
/// desktop shortcut). Checked the same way as <see cref="PackagingScriptTests"/>:
/// the real PowerShell parser accepts it, it is pure ASCII (Windows
/// PowerShell 5.1 reads a BOM-less .ps1 as Windows-1252), and it names the
/// steps it exists to perform. Running it (a full publish, install and
/// shortcut) is done by hand on the PO's machine, runbook step A0.
/// </summary>
public sealed class InstallTestBuildScriptTests
{
    private static readonly string ScriptPath = Path.Combine(RepositoryPaths.RepositoryRoot, "scripts", "install-test-build.ps1");

    [Fact]
    public void Script_Exists()
    {
        Assert.True(File.Exists(ScriptPath), $"Expected to find '{ScriptPath}'.");
    }

    [Fact]
    public void Script_IsPureAscii()
    {
        var bytes = File.ReadAllBytes(ScriptPath);

        Assert.DoesNotContain(bytes, b => b > 0x7F);
    }

    [Fact]
    public void Script_IsSyntacticallyValidPowerShell()
    {
        var (exitCode, output) = PackagingScriptTests.RunPowerShellSyntaxCheck(ScriptPath);

        Assert.True(exitCode == 0, $"PowerShell syntax check failed for '{ScriptPath}':{Environment.NewLine}{output}");
    }

    [Theory]
    [InlineData("package-installer.ps1")]
    [InlineData("TempestOS-$version-Setup.exe")]
    [InlineData("--silent")]
    [InlineData("first-run.json")]
    [InlineData("current\\Tempest.Desktop.exe")]
    [InlineData("--persistence-root")]
    [InlineData("TempestOS * (test).lnk")]
    [InlineData("TempestOS $version (test).lnk")]
    public void Script_NamesTheExpectedStepsAndAssets(string expectedSubstring)
    {
        var content = File.ReadAllText(ScriptPath, Encoding.ASCII);

        Assert.Contains(expectedSubstring, content, StringComparison.Ordinal);
    }
}
