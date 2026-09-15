using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.PurchaseOrders;

/// <summary>
/// The acts a <see cref="PurchaseOrder"/> supports: create, add/update/
/// remove a line, issue, receive, close, cancel, and record a received
/// order's own lines as project expenses — the seam that reaches project
/// cost without a ledger (`WP 21.3B`). Every act is one transaction with
/// an audit row; whether an act is <em>permitted</em> is decided here,
/// before <see cref="PurchaseOrder"/>'s own mutator ever runs, and
/// reported back as a refusal result rather than an exception, mirroring
/// <c>Tempest.Core.Quotations.IQuotationService</c>.
/// </summary>
public interface IPurchaseOrderService
{
    /// <summary>Creates a new, empty Draft purchase order under <paramref name="projectId"/>. <paramref name="reference"/> blank generates <c>PO-&lt;yyyy&gt;-&lt;nnn&gt;</c>.</summary>
    Task<PurchaseOrderResult> CreateAsync(
        Guid projectId, string? reference = null, string? supplierOrganisationId = null, string? notes = null,
        DateOnly? expectedDelivery = null, CancellationToken cancellationToken = default);

    /// <summary>Adds a line to the selected, Draft order. Refused, as a result, when <paramref name="quantity"/> or <paramref name="unitPrice"/> is zero or below, or <paramref name="unitPrice"/> is not in the order's own currency.</summary>
    Task<PurchaseOrderResult> AddLineAsync(
        Guid orderId, string description, decimal quantity, Money unitPrice, VatRate vatRate = VatRate.OutOfScope,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces the line named <paramref name="lineId"/> on the selected, Draft order.</summary>
    Task<PurchaseOrderResult> UpdateLineAsync(
        Guid orderId, Guid lineId, string description, decimal quantity, Money unitPrice, VatRate vatRate,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the line named <paramref name="lineId"/> from the selected, Draft order.</summary>
    Task<PurchaseOrderResult> RemoveLineAsync(Guid orderId, Guid lineId, CancellationToken cancellationToken = default);

    /// <summary>Issues the selected, Draft order to its supplier. Refused, as a result, if it carries no lines.</summary>
    Task<PurchaseOrderResult> IssueAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Marks the selected, Issued order Received.</summary>
    Task<PurchaseOrderResult> ReceiveAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Closes the selected, Received order.</summary>
    Task<PurchaseOrderResult> CloseAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Cancels the selected Draft or Issued order.</summary>
    Task<PurchaseOrderResult> CancelAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records every line on the selected, Received order as a billable
    /// <c>Tempest.Core.Expenses.ProjectExpense</c> against the same
    /// project, one expense per line — the act that lets a purchase
    /// order's own cost reach the invoice seam with no ledger of its own.
    /// Refused, as a result, when the order is not Received or has
    /// already had its lines recorded once.
    /// </summary>
    Task<PurchaseOrderResult> RecordLinesAsExpensesAsync(Guid orderId, CancellationToken cancellationToken = default);
}
