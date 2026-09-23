namespace Tempest.Core.PurchaseOrders;

/// <summary>
/// A <see cref="PurchaseOrder"/>'s own lifecycle position (`WP 21.3B`) —
/// the Business → Purchase orders tree groups by this exactly as
/// Business → Invoices groups by <c>Tempest.Core.Invoicing.InvoiceRequestStatus</c>.
/// </summary>
public enum PurchaseOrderStatus
{
    /// <summary>Built, not yet issued to the supplier. Lines may still be added, changed or removed.</summary>
    Draft,

    /// <summary>Issued to the supplier. Lines are fixed.</summary>
    Issued,

    /// <summary>The goods or service arrived.</summary>
    Received,

    /// <summary>Complete — nothing further to do. Terminal.</summary>
    Closed,

    /// <summary>Cancelled before receipt. Terminal.</summary>
    Cancelled,
}

/// <summary>
/// The permitted <see cref="PurchaseOrderStatus"/> transition table — the
/// sole enforcement point <see cref="PurchaseOrderService"/>'s own
/// status-moving acts check against, mirroring
/// <c>Tempest.Core.Quotations.QuotationStatusTransitions</c>'s own shape
/// (`WP 21.3B`).
/// </summary>
internal static class PurchaseOrderStatusTransitions
{
    private static readonly IReadOnlyDictionary<PurchaseOrderStatus, IReadOnlySet<PurchaseOrderStatus>> Permitted =
        new Dictionary<PurchaseOrderStatus, IReadOnlySet<PurchaseOrderStatus>>
        {
            [PurchaseOrderStatus.Draft] = new HashSet<PurchaseOrderStatus> { PurchaseOrderStatus.Issued, PurchaseOrderStatus.Cancelled },
            [PurchaseOrderStatus.Issued] = new HashSet<PurchaseOrderStatus> { PurchaseOrderStatus.Received, PurchaseOrderStatus.Cancelled },
            [PurchaseOrderStatus.Received] = new HashSet<PurchaseOrderStatus> { PurchaseOrderStatus.Closed },

            // Terminal.
            [PurchaseOrderStatus.Closed] = new HashSet<PurchaseOrderStatus>(),
            [PurchaseOrderStatus.Cancelled] = new HashSet<PurchaseOrderStatus>(),
        };

    /// <summary>Whether transitioning from <paramref name="from"/> to <paramref name="to"/> is permitted.</summary>
    public static bool IsPermitted(PurchaseOrderStatus from, PurchaseOrderStatus to) => Permitted[from].Contains(to);

    /// <summary>Every <see cref="PurchaseOrderStatus"/> value, for a test to walk exhaustively, and for the explorer's own status groups.</summary>
    public static IReadOnlyList<PurchaseOrderStatus> AllStatuses { get; } =
        [PurchaseOrderStatus.Draft, PurchaseOrderStatus.Issued, PurchaseOrderStatus.Received, PurchaseOrderStatus.Closed, PurchaseOrderStatus.Cancelled];
}
