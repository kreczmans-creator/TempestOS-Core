using System.Text;
using Tempest.Core.Tests.Invoicing.Xero.Live;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.ReleaseEngineering;

/// <summary>
/// `scripts/xero-demo-smoke.ps1` (`v0.24.0` task X8, ADR-0162 D7): the
/// script the Product Owner runs to smoke-test Xero against the Demo
/// Company. Checked as <see cref="InstallTestBuildScriptTests"/> checks its
/// script: the real PowerShell parser accepts it, it is pure ASCII (Windows
/// PowerShell 5.1 reads a BOM-less .ps1 as Windows-1252), and it runs the
/// live tests the way they are gated. Running it needs Demo Company
/// credentials and is done by hand (runbook steps XL-*).
/// </summary>
public sealed class XeroDemoSmokeScriptTests
{
    private static readonly string ScriptPath = Path.Combine(RepositoryPaths.RepositoryRoot, "scripts", "xero-demo-smoke.ps1");

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
    [InlineData("dotnet test")]
    [InlineData("tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj")]
    [InlineData("--filter \"Category=XeroLive\"")]
    [InlineData("IsDemoCompany")]
    [InlineData("exit $exitCode")]
    public void Script_RunsTheLiveTestsAsTheyAreGated(string expectedSubstring)
    {
        var content = File.ReadAllText(ScriptPath, Encoding.ASCII);

        Assert.Contains(expectedSubstring, content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(XeroLiveSettings.LiveVariable)]
    [InlineData(XeroLiveSettings.DataFolderVariable)]
    [InlineData(XeroLiveSettings.ReportVariable)]
    [InlineData(XeroLiveSettings.ConnectVariable)]
    [InlineData(XeroLiveSettings.KeepVariable)]
    [InlineData(XeroLiveSettings.KeyWindowVariable)]
    [InlineData(XeroLiveSettings.ClientIdVariable)]
    [InlineData(XeroLiveSettings.ClientSecretVariable)]
    [InlineData(XeroLiveSettings.AccessTokenVariable)]
    [InlineData(XeroLiveSettings.TenantIdVariable)]
    public void Script_SetsEveryVariableTheLiveTestsRead(string variable)
    {
        var content = File.ReadAllText(ScriptPath, Encoding.ASCII);

        Assert.Contains($"\"{variable}\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_NeverPrintsTheSuppliedToken()
    {
        var content = File.ReadAllText(ScriptPath, Encoding.ASCII);

        Assert.DoesNotContain("Write-Host $AccessToken", content, StringComparison.Ordinal);
        Assert.DoesNotContain("$AccessToken\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("$ClientSecret\"", content, StringComparison.Ordinal);
    }
}
