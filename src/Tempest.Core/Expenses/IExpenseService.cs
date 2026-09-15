using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.Expenses;

/// <summary>
/// The acts a <see cref="ProjectExpense"/> supports: record, amend,
/// delete, mark invoiced, and the two list queries the Timesheets view's
/// own Record expense flow and the unbilled-work read model need (`WP
/// 21.3B`). Every act is one transaction with an audit row; whether an act
/// is <em>permitted</em> is decided here, before <see cref="ProjectExpense"/>'s
/// own mutator ever runs, and reported back as a refusal result rather
/// than an exception, mirroring <c>Tempest.Core.Timesheets.ITimesheetService</c>.
/// </summary>
public interface IExpenseService
{
    /// <summary>Records an expense of <paramref name="netAmount"/> plus <paramref name="vatAmount"/> against <paramref name="projectId"/> on <paramref name="date"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="projectId"/> does not identify a live project.</exception>
    /// <remarks>Refused, as a result, when <paramref name="netAmount"/> and <paramref name="vatAmount"/> are not in the same currency, or either is below zero.</remarks>
    Task<ExpenseResult> RecordAsync(
        Guid projectId, DateOnly date, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable,
        CancellationToken cancellationToken = default);

    /// <summary>Amends <paramref name="expenseId"/>'s own description, category, amounts and billable flag. Refused, as a result, once the expense carries an <see cref="ProjectExpense.InvoicedBy"/> link.</summary>
    Task<ExpenseResult> AmendAsync(
        Guid expenseId, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes <paramref name="expenseId"/>'s own expense. Refused, as a result, once the expense carries an <see cref="ProjectExpense.InvoicedBy"/> link.</summary>
    Task<ExpenseResult> DeleteAsync(Guid expenseId, CancellationToken cancellationToken = default);

    /// <summary>Sets <paramref name="expenseId"/>'s own <see cref="ProjectExpense.InvoicedBy"/> link to <paramref name="requestId"/>. Refused, as a result, if the expense already carries one.</summary>
    Task<ExpenseResult> MarkInvoicedAsync(Guid expenseId, Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>Every live expense for <paramref name="projectId"/>, read as one coherent list, most recent first.</summary>
    Task<IReadOnlyList<ProjectExpense>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Every live, billable expense for <paramref name="projectId"/> carrying no <see cref="ProjectExpense.InvoicedBy"/> link, read as one coherent list — the unbilled-work read model, mirroring <c>ITimesheetService.ListUnbilledForProjectAsync</c>.</summary>
    Task<IReadOnlyList<ProjectExpense>> ListUnbilledForProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
