using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.Invoicing;

/// <summary>
/// One bill (a payable, not a Tempest-raised invoice) as read from the
/// accounting package — never entered in Tempest, only ever read
/// (`WP 19.8B`, po-comments.md item 8).
/// </summary>
/// <param name="Supplier">The supplier's own name, as the accounting package records it.</param>
/// <param name="Reference">The supplier's own bill/reference number, where the package states one. Falls back to the package's own internal document number when no separate supplier reference exists.</param>
/// <param name="Issued">When the bill was raised, as the accounting package records it.</param>
/// <param name="Due">When the bill is due, as the accounting package records it.</param>
/// <param name="Amount">The bill's own total, in the currency the accounting package stated it in.</param>
/// <param name="Status">The accounting package's own status word, verbatim (for example <c>"AUTHORISED"</c>, <c>"PAID"</c>) — never interpreted by the connector itself, exactly as <see cref="InvoiceStatusReading.ExternalStatus"/> is not.</param>
public sealed record BillDue(string Supplier, string Reference, DateOnly Issued, DateOnly Due, Money Amount, string Status);

/// <summary>
/// One repeating bill — a subscription — as read from the accounting
/// package (`WP 19.8B`, po-comments.md item 8).
/// </summary>
/// <param name="Supplier">The supplier's own name, as the accounting package records it.</param>
/// <param name="Description">A human-readable description of what the subscription is for.</param>
/// <param name="Amount">The amount charged each occurrence.</param>
/// <param name="Frequency">The accounting package's own schedule word, verbatim (for example <c>"MONTHLY"</c>) — never interpreted by the connector itself.</param>
/// <param name="NextDue">When the next occurrence is due.</param>
/// <param name="AccountName">The package's own account or tracking-category name this subscription is coded to, where one exists — what <see cref="AccountsCategoriser"/> categorises into Hardware/Software/Premises/Other. <see langword="null"/> when the package states none.</param>
public sealed record RepeatingBill(string Supplier, string Description, Money Amount, string Frequency, DateOnly NextDue, string? AccountName);

/// <summary>One bank account's own balance, as read from the accounting package (`WP 19.8B`, po-comments.md item 8).</summary>
/// <param name="Name">The account's own name, as the accounting package records it.</param>
/// <param name="Balance">The account's own balance, in the currency the accounting package stated it in.</param>
/// <param name="AsOf">When this balance was true, as the accounting package records it (or the moment this connector read it, when the package states no date of its own).</param>
public sealed record CashAccountBalance(string Name, Money Balance, DateOnly AsOf);

/// <summary>
/// A read-only draw from the accounting package — bills due, repeating
/// bills (subscriptions) and the cash position — never a write
/// (`WP 19.8B`, po-comments.md item 8). Sibling to
/// <see cref="IInvoicingConnector"/>, same result shape: every member
/// returns a <see cref="ConnectorResult{T}"/> and never throws for an
/// ordinary network or authorisation failure.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a sibling interface, not new members on <see cref="IInvoicingConnector"/>.</b>
/// Outbound invoicing and this read are different relationships with the
/// same accounting package — one writes (a draft invoice), this only
/// ever reads. Keeping them separate lets a package that can send
/// invoices but exposes no accounts data (or the reverse) implement only
/// the interface it actually supports, and keeps
/// <see cref="IInvoicingConnector"/>'s own contract exactly as `WP 19.1A`
/// left it. Every shipped implementation of
/// <see cref="IInvoicingConnector"/> (<see cref="FakeInvoicingConnector"/>,
/// <c>XeroConnector</c>, <c>QuickBooksOnlineConnector</c>) also implements
/// this interface, over the same <c>HttpClient</c>/<c>OAuthAuthoriser</c>.
/// </para>
/// <para>
/// <b>Unavailable when not authorised — deliberately, not
/// <see cref="ConnectorOutcome.Reauthorise"/>.</b> Unlike
/// <see cref="IInvoicingConnector"/>'s own gate (which distinguishes
/// "never authorised" from "token expired" because sending an invoice is
/// a deliberate act an operator needs to unblock), this read is a
/// background, best-effort draw a dashboard tile quietly goes without —
/// every member answers <see cref="ConnectorOutcome.Unavailable"/> when
/// there is no usable access, one honest "cannot read this right now"
/// rather than a second workflow.
/// </para>
/// <para>
/// <b>Nothing here is ever written back.</b> There is no create, update
/// or void member on this interface, and nothing in TempestOS computes a
/// figure this interface reports — it is read, cached with its own
/// timestamp (<see cref="AccountsReading"/>), and shown, exactly as
/// <see cref="InvoiceStatusReading.PaidDate"/> is read from
/// <see cref="IInvoicingConnector"/> and never set by anything else in
/// this platform.
/// </para>
/// </remarks>
public interface IAccountsConnector
{
    /// <summary>This connector's own name — the identical property every shipped implementation already carries as <see cref="IInvoicingConnector.Name"/>; declared again here so <see cref="AccountsReading.Connector"/> can be recorded without this interface's own caller needing to know about <see cref="IInvoicingConnector"/> at all.</summary>
    string Name { get; }

    /// <summary>
    /// Every bill due on or before <paramref name="asOf"/> plus
    /// <paramref name="horizonDays"/> — including any already overdue.
    /// </summary>
    Task<ConnectorResult<IReadOnlyList<BillDue>>> ListBillsDueAsync(DateOnly asOf, int horizonDays, CancellationToken cancellationToken = default);

    /// <summary>Every repeating bill (subscription) the accounting package currently has scheduled.</summary>
    Task<ConnectorResult<IReadOnlyList<RepeatingBill>>> ListRepeatingBillsAsync(CancellationToken cancellationToken = default);

    /// <summary>Every bank account's own current balance.</summary>
    Task<ConnectorResult<IReadOnlyList<CashAccountBalance>>> ReadCashPositionAsync(CancellationToken cancellationToken = default);
}
