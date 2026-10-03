using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// <c>scripts/xero-demo-smoke.ps1 -Connect</c> opens the browser sign-in once
/// per run, not once per live test (F4 verifier round 1, defect 1): with
/// three live tests (full journey, production path, <c>-KeyWindow</c>)
/// each connecting, an ungated sign-in opened up to three consent pages and
/// waited out the sign-in timeout on each one the operator ignored.
/// </summary>
public sealed class XeroLiveSignInTests
{
    private static XeroLiveSettings Settings(params (string Name, string Value)[] values) =>
        new(name => values.FirstOrDefault(v => v.Name == name).Value);

    private static readonly XeroLiveSettings ConnectSettings = Settings(
        (XeroLiveSettings.LiveVariable, "1"), (XeroLiveSettings.ConnectVariable, "1"));

    [Fact]
    public async Task Connect_SignsInOnce_HoweverManyLiveTestsConnect()
    {
        var gate = new XeroLiveSignInGate();
        var signIns = 0;
        var log = new List<string>();

        Task<OAuthResult> Authorise(CancellationToken _)
        {
            signIns++;
            return Task.FromResult(new OAuthResult(OAuthOutcome.Ok));
        }

        // The full journey, the production path and the key-window probe.
        var first = await XeroLiveSmokeTests.SignInOnceIfAskedAsync(ConnectSettings, gate, Authorise, log.Add);
        var second = await XeroLiveSmokeTests.SignInOnceIfAskedAsync(ConnectSettings, gate, Authorise, log.Add);
        var third = await XeroLiveSmokeTests.SignInOnceIfAskedAsync(ConnectSettings, gate, Authorise, log.Add);

        Assert.Equal(1, signIns);
        Assert.True(first);
        Assert.False(second);
        Assert.False(third);
        Assert.Single(log, line => line.StartsWith("Connecting:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Connect_SignsInOnce_WhenTestsConnectAtTheSameTime_AndTheOthersWaitForIt()
    {
        var gate = new XeroLiveSignInGate();
        var signIns = 0;
        var release = new TaskCompletionSource<OAuthResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<OAuthResult> Authorise(CancellationToken _)
        {
            Interlocked.Increment(ref signIns);
            return release.Task;
        }

        var calls = Enumerable.Range(0, 3)
            .Select(_ => Task.Run(() => XeroLiveSmokeTests.SignInOnceIfAskedAsync(ConnectSettings, gate, Authorise, _ => { })))
            .ToArray();

        await Task.Delay(50);
        Assert.All(calls, call => Assert.False(call.IsCompleted));

        release.SetResult(new OAuthResult(OAuthOutcome.Ok));
        var results = await Task.WhenAll(calls);

        Assert.Equal(1, signIns);
        Assert.Single(results, ran => ran);
    }

    [Fact]
    public async Task Connect_ThatDidNotComplete_IsNotRepeatedByTheNextTest()
    {
        var gate = new XeroLiveSignInGate();
        var signIns = 0;

        Task<OAuthResult> Authorise(CancellationToken _)
        {
            signIns++;
            return Task.FromResult(new OAuthResult(OAuthOutcome.Failed, "timed out"));
        }

        await XeroLiveSmokeTests.SignInOnceIfAskedAsync(ConnectSettings, gate, Authorise, _ => { });
        await XeroLiveSmokeTests.SignInOnceIfAskedAsync(ConnectSettings, gate, Authorise, _ => { });

        Assert.Equal(1, signIns);
    }

    [Fact]
    public async Task NoSignIn_WithoutConnect_OrWithASuppliedToken()
    {
        var gate = new XeroLiveSignInGate();
        var signIns = 0;

        Task<OAuthResult> Authorise(CancellationToken _)
        {
            signIns++;
            return Task.FromResult(new OAuthResult(OAuthOutcome.Ok));
        }

        await XeroLiveSmokeTests.SignInOnceIfAskedAsync(Settings((XeroLiveSettings.LiveVariable, "1")), gate, Authorise, _ => { });
        await XeroLiveSmokeTests.SignInOnceIfAskedAsync(
            Settings((XeroLiveSettings.LiveVariable, "1"), (XeroLiveSettings.ConnectVariable, "1"), (XeroLiveSettings.AccessTokenVariable, "token")),
            gate, Authorise, _ => { });

        Assert.Equal(0, signIns);
    }

    [Fact]
    public void TheLiveTests_SignInOnlyThroughTheOncePerRunGate()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryPaths.RepositoryRoot, "tests", "Tempest.Core.Tests", "Invoicing", "Xero", "Live", "XeroLiveSmokeTests.cs"));

        // Never a direct sign-in per test: only the gate's call, handed the method group.
        Assert.DoesNotContain("Authoriser.AuthoriseAsync(", source, StringComparison.Ordinal);
        Assert.Contains("SignInOnceIfAskedAsync(settings, XeroLiveSignInGate.PerRun, connection.Authoriser.AuthoriseAsync", source, StringComparison.Ordinal);
    }
}
