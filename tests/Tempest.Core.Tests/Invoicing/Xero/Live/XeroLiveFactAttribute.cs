namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// A test that talks to the real Xero API (`v0.24.0` task X8, design §10.2,
/// D7). It runs only when the operator opts in with
/// <see cref="XeroLiveSettings.LiveVariable"/> = <c>1</c> (what
/// <c>scripts/xero-demo-smoke.ps1</c> sets); otherwise xUnit reports it
/// <b>skipped</b>, never failed, so CI and an ordinary <c>dotnet test</c>
/// never reach the network. Every such test also carries
/// <c>[Trait("Category", "XeroLive")]</c>, so the script selects exactly
/// these with <c>--filter "Category=XeroLive"</c>.
/// </summary>
/// <remarks>
/// xUnit 2 has no run-time skip, so the decision is made when the
/// attribute is constructed (test discovery): the environment variable must
/// be set in the process that runs <c>dotnet test</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class XeroLiveFactAttribute : FactAttribute
{
    private string? _alsoRequires;

    /// <summary>Initialises a new instance of the <see cref="XeroLiveFactAttribute"/> class, skipping the test unless the live run was asked for.</summary>
    public XeroLiveFactAttribute()
    {
        Skip = SkipReason(XeroLiveSettings.FromEnvironment());
    }

    /// <summary>
    /// An extra environment variable that must also be <c>1</c> for this
    /// test to run (for example the slow idempotency-key probe); <see langword="null"/>
    /// for none. Setting it re-evaluates <see cref="FactAttribute.Skip"/>.
    /// </summary>
    public string? AlsoRequires
    {
        get => _alsoRequires;
        set
        {
            _alsoRequires = value;
            Skip = SkipReason(XeroLiveSettings.FromEnvironment());
        }
    }

    /// <summary>Why the test is skipped under <paramref name="settings"/>, or <see langword="null"/> when it runs.</summary>
    /// <param name="settings">What the environment asked for.</param>
    internal string? SkipReason(XeroLiveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.Enabled)
        {
            return $"Live Xero smoke test: set {XeroLiveSettings.LiveVariable}=1 (scripts/xero-demo-smoke.ps1 does) with Demo Company credentials to run it. "
                + "Skipped, never failed, without them (CI).";
        }

        if (_alsoRequires is not null && !XeroLiveSettings.IsOn(settings.Read(_alsoRequires)))
            return $"Live Xero probe: also set {_alsoRequires}=1 to run it (it waits several minutes against Xero).";

        return null;
    }
}
