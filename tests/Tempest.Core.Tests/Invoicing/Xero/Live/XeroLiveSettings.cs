namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>
/// What the operator asked the live Xero smoke test for, read from
/// environment variables (`v0.24.0` task X8, design §10.2).
/// <c>scripts/xero-demo-smoke.ps1</c> sets them from its parameters; nothing
/// here is ever committed or read from the repository.
/// </summary>
/// <remarks>
/// <para>
/// <b>Credentials, two ways.</b> By default the test uses the tokens
/// TempestOS itself stored when the Product Owner connected Xero in
/// Settings (<c>ISecretStore</c>, the same secrets folder as the app, under
/// <see cref="DataFolderVariable"/> or the app's default data folder), so the
/// run exercises the real grant, refresh and granted-scope record. Or a
/// secret store of the operator's choosing (a CI secret, a password
/// manager) supplies <see cref="AccessTokenVariable"/> and
/// <see cref="TenantIdVariable"/> directly; that token is used as it is and
/// never refreshed.
/// </para>
/// <para>
/// No value read here is ever written to a log, a report or the test output.
/// </para>
/// </remarks>
/// <param name="Read">Reads one variable; <see langword="null"/> when unset.</param>
public sealed record XeroLiveSettings(Func<string, string?> Read)
{
    /// <summary><c>1</c> runs the live tests; anything else (or unset) skips them.</summary>
    public const string LiveVariable = "TEMPEST_XERO_LIVE";

    /// <summary>The TempestOS data folder whose secrets hold the stored Xero tokens; unset for the app's default data folder.</summary>
    public const string DataFolderVariable = "TEMPEST_XERO_DATA_FOLDER";

    /// <summary>An access token supplied directly (with <see cref="TenantIdVariable"/>) instead of the stored tokens.</summary>
    public const string AccessTokenVariable = "TEMPEST_XERO_ACCESS_TOKEN";

    /// <summary>The Xero tenant id (the Demo Company's) that goes with <see cref="AccessTokenVariable"/>.</summary>
    public const string TenantIdVariable = "TEMPEST_XERO_TENANT_ID";

    /// <summary>The Xero app's client id, when it is not already in TempestOS's secret store (needed to refresh or to connect).</summary>
    public const string ClientIdVariable = "TEMPEST_XERO_CLIENT_ID";

    /// <summary>The Xero app's client secret, when the app has one and it is not already in TempestOS's secret store.</summary>
    public const string ClientSecretVariable = "TEMPEST_XERO_CLIENT_SECRET";

    /// <summary><c>1</c> signs in first (opens the browser, as Settings → Connect does), storing the new tokens in the data folder's secrets.</summary>
    public const string ConnectVariable = "TEMPEST_XERO_CONNECT";

    /// <summary><c>1</c> keeps the smoke drafts in the Demo Company instead of deleting them at the end.</summary>
    public const string KeepVariable = "TEMPEST_XERO_KEEP";

    /// <summary><c>1</c> also runs the idempotency-key retention probe (about seven minutes of waiting).</summary>
    public const string KeyWindowVariable = "TEMPEST_XERO_KEY_WINDOW";

    /// <summary>Where the run's Markdown report is written; unset for a file in the temp folder (its path is printed).</summary>
    public const string ReportVariable = "TEMPEST_XERO_REPORT";

    /// <summary>The settings of this process's environment.</summary>
    public static XeroLiveSettings FromEnvironment() => new(Environment.GetEnvironmentVariable);

    /// <summary>Whether the live run was asked for.</summary>
    public bool Enabled => IsOn(Read(LiveVariable));

    /// <summary>Whether a browser sign-in should run first.</summary>
    public bool Connect => IsOn(Read(ConnectVariable));

    /// <summary>Whether the smoke drafts are kept.</summary>
    public bool Keep => IsOn(Read(KeepVariable));

    /// <summary>Whether the token is supplied directly rather than read from TempestOS's secret store.</summary>
    public bool UsesSuppliedToken => !string.IsNullOrWhiteSpace(Read(AccessTokenVariable));

    /// <summary>The data folder, or <see langword="null"/> for the app's default.</summary>
    public string? DataFolder => Trimmed(DataFolderVariable);

    /// <summary>The report path, or <see langword="null"/> for the default.</summary>
    public string? ReportPath => Trimmed(ReportVariable);

    /// <summary>Whether <paramref name="value"/> switches something on (<c>1</c> or <c>true</c>).</summary>
    /// <param name="value">The variable's value.</param>
    public static bool IsOn(string? value) =>
        value is not null && (value.Trim() == "1" || string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase));

    /// <summary>The trimmed value of <paramref name="name"/>, or <see langword="null"/> when unset or blank.</summary>
    /// <param name="name">The variable.</param>
    public string? Trimmed(string name) => Read(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
