namespace Tempest.Core.PurchaseOrders;

/// <summary>Why an <see cref="IPurchaseOrderService"/> act was refused, or <see cref="None"/> if it was not.</summary>
public enum PurchaseOrderRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No live project is registered under the requested id.</summary>
    ProjectNotFound,

    /// <summary>No purchase order is registered under the requested id.</summary>
    OrderNotFound,

    /// <summary>The order is not Draft — a line can only be added, changed or removed while Draft.</summary>
    OrderNotDraft,

    /// <summary>No line is registered under the requested id on this order.</summary>
    LineNotFound,

    /// <summary>The line's own quantity or unit price is not a usable value (zero or below).</summary>
    InvalidLine,

    /// <summary>A supplied unit price is not in the order's own currency.</summary>
    LineCurrencyMismatch,

    /// <summary>The requested status move is not permitted from the order's own current status.</summary>
    TransitionNotPermitted,

    /// <summary>The order carries no lines; there is nothing to issue.</summary>
    NothingToIssue,

    /// <summary>The order is not Received; its lines cannot be recorded as expenses yet.</summary>
    NotReceived,

    /// <summary><see cref="PurchaseOrderService.RecordLinesAsExpensesAsync"/> has already run for this order.</summary>
    ExpensesAlreadyRecorded,

    /// <summary>The project is Archive — closed 90 days or more ago — and read-only (`WP 19.5C`, Product Owner comment item 6).</summary>
    ProjectArchived,
}

/// <summary>The outcome of an <see cref="IPurchaseOrderService"/> act: either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="PurchaseOrderRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Order">The order acted on, when it could be resolved.</param>
public sealed record PurchaseOrderResult(PurchaseOrderRefusal Refusal, string? Reason, PurchaseOrder? Order)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == PurchaseOrderRefusal.None;
}
