namespace Tempest.Core.Invoicing.Xero;

/// <summary>
/// The exact OAuth 2.0 scope strings TempestOS asks Xero for (`v0.24.0`
/// X0, `ADR-0162`). One place, so <c>TempestHost</c>'s
/// <see cref="OAuth.OAuthProviderProfile"/>, the re-authorisation check and
/// the in-process Xero API simulator all agree on the same strings.
/// </summary>
/// <remarks>
/// <para>
/// <b>Granular scopes only.</b> An app created in the Xero developer portal
/// on or after 2 March 2026 cannot be granted the broad
/// <c>accounting.transactions</c> or <c>accounting.reports.read</c>
/// scopes; <c>accounting.invoices</c> replaces the former for invoices
/// (sales and bills), quotes, purchase orders, repeating invoices, credit
/// notes and items, and one scope per report replaces the latter. The
/// <c>accounting.contacts</c>, <c>accounting.settings</c> and
/// <c>accounting.attachments</c> scopes are unchanged by that move. Sources
/// and the endpoint-by-endpoint table are in
/// <c>docs/releases/v0.24.0/Xero Technical Design.md</c> §2.
/// </para>
/// <para>
/// <b>Minimal.</b> Every scope here is needed by at least one endpoint
/// TempestOS calls; <see cref="Required"/> is the set requested. Nothing
/// that pays, reconciles, posts journals or reads payroll is requested.
/// </para>
/// </remarks>
public static class XeroScopes
{
    /// <summary>Read and write sales invoices (<c>ACCREC</c>) and bills (<c>ACCPAY</c>), quotes, purchase orders; read repeating invoices.</summary>
    public const string Invoices = "accounting.invoices";

    /// <summary>Read, create and update contacts (X2: link or create a customer or supplier; fixes review item M21).</summary>
    public const string Contacts = "accounting.contacts";

    /// <summary>Read the organisation, tax rates and chart of accounts (X1, D6). Read-only: TempestOS never changes Xero's settings.</summary>
    public const string SettingsRead = "accounting.settings.read";

    /// <summary>Upload the TempestOS PDF to a quote, invoice, bill or purchase order (X3–X5).</summary>
    public const string Attachments = "accounting.attachments";

    /// <summary>Read the Bank Summary report for the Business dashboard's cash position (`WP 19.8B`, unchanged).</summary>
    public const string BankSummaryReportRead = "accounting.reports.banksummary.read";

    /// <summary>Receive a refresh token so the connection outlives the 30-minute access token.</summary>
    public const string OfflineAccess = "offline_access";

    /// <summary>
    /// Every scope TempestOS requests, in the order it is sent. A stored
    /// grant that lacks any one of these needs re-authorising before the
    /// operation that needs it can run (X0).
    /// </summary>
    public static IReadOnlyList<string> Required { get; } =
        [Invoices, Contacts, SettingsRead, Attachments, BankSummaryReportRead, OfflineAccess];
}
