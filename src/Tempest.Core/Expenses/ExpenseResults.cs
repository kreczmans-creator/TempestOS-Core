namespace Tempest.Core.Expenses;

/// <summary>Why an <see cref="IExpenseService"/> act was refused, or <see cref="None"/> if it was not.</summary>
public enum ExpenseRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No expense is registered under the requested id.</summary>
    ExpenseNotFound,

    /// <summary>This expense already carries an <see cref="ProjectExpense.InvoicedBy"/> link — its amounts, description, category and billable flag are frozen.</summary>
    ExpenseInvoiced,

    /// <summary>The net and VAT amounts are not in the same currency — <see cref="ProjectExpense.GrossAmount"/> has no honest answer without one.</summary>
    CurrencyMismatch,

    /// <summary>The net or VAT amount is below zero.</summary>
    InvalidAmount,

    /// <summary>The project is Archive — closed 90 days or more ago — and read-only (`WP 19.5C`, Product Owner comment item 6).</summary>
    ProjectArchived,
}

/// <summary>The outcome of an <see cref="IExpenseService"/> act: either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="ExpenseRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Expense">The expense acted on, when it could be resolved.</param>
public sealed record ExpenseResult(ExpenseRefusal Refusal, string? Reason, ProjectExpense? Expense)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == ExpenseRefusal.None;
}
