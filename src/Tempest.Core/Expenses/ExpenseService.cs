using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;

namespace Tempest.Core.Expenses;

/// <summary>The concrete <see cref="IExpenseService"/> implementation (`WP 21.3B`).</summary>
public sealed class ExpenseService : IExpenseService
{
    private readonly EngineeringDomainContext _context;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ExpenseService"/> class.</summary>
    /// <param name="timeProvider">The clock the archived-project guard reads "now" from. <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>.</param>
    public ExpenseService(EngineeringDomainContext context, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ExpenseResult> RecordAsync(
        Guid projectId, DateOnly date, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
            throw new ArgumentException($"'{projectId}' does not identify a live project.", nameof(projectId));

        if (BuildAmounts(netAmount, vatAmount) is { } invalid)
            return invalid;

        if (ProjectArchival.IsArchived(project, _time.GetUtcNow()))
        {
            return new ExpenseResult(
                ExpenseRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); no new expense can be recorded against it.", null);
        }

        var created = await new EngineeringObjectFactory<ProjectExpense>(
            ProjectExpense.CanonicalKind,
            _context,
            (doc, rev) => new ProjectExpense(
                doc, rev, _context, identifier: null, $"{description} — {date:yyyy-MM-dd}", EngineeringObjectMetadata.Empty,
                projectId, date, description, category, netAmount, vatAmount, billable))
            .CreateAsync($"Expense recorded — {description}.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new ExpenseResult(ExpenseRefusal.None, null, (ProjectExpense)created);
    }

    /// <inheritdoc />
    public async Task<ExpenseResult> AmendAsync(
        Guid expenseId, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var expense = await FindExpenseAsync(expenseId, cancellationToken).ConfigureAwait(false);
        if (expense is null)
            return NotFound(expenseId);

        if (expense.InvoicedBy is not null)
            return Invoiced(expense);

        if (BuildAmounts(netAmount, vatAmount) is { } invalid)
            return invalid;

        if (await ArchivedAsync(expense, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await expense.AmendAsync(description, category, netAmount, vatAmount, billable, cancellationToken).ConfigureAwait(false);

        return new ExpenseResult(ExpenseRefusal.None, null, expense);
    }

    /// <inheritdoc />
    public async Task<ExpenseResult> DeleteAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await FindExpenseAsync(expenseId, cancellationToken).ConfigureAwait(false);
        if (expense is null)
            return NotFound(expenseId);

        if (expense.InvoicedBy is not null)
            return Invoiced(expense);

        if (await ArchivedAsync(expense, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await expense.DeleteAsync(cancellationToken).ConfigureAwait(false);

        return new ExpenseResult(ExpenseRefusal.None, null, expense);
    }

    /// <inheritdoc />
    public async Task<ExpenseResult> MarkInvoicedAsync(Guid expenseId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var expense = await FindExpenseAsync(expenseId, cancellationToken).ConfigureAwait(false);
        if (expense is null)
            return NotFound(expenseId);

        if (expense.InvoicedBy is not null)
            return Invoiced(expense);

        if (await ArchivedAsync(expense, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await expense.MarkInvoicedAsync(requestId, cancellationToken).ConfigureAwait(false);

        return new ExpenseResult(ExpenseRefusal.None, null, expense);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExpense>> ListForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var children = await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false);

        return children
            .OfType<ProjectExpense>()
            .Where(IsLive)
            .OrderByDescending(e => e.Date)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExpense>> ListUnbilledForProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var children = await _context.Repository.ListChildrenAsync(projectId, cancellationToken).ConfigureAwait(false);

        return children
            .OfType<ProjectExpense>()
            .Where(e => IsLive(e) && e.Billable && e.InvoicedBy is null)
            .OrderBy(e => e.Date)
            .ToList();
    }

    /// <summary>Validates that <paramref name="netAmount"/> and <paramref name="vatAmount"/> are in the same currency and neither is below zero — refused as a result, exactly as a currency mismatch anywhere else in this platform is (`ADR-0130`).</summary>
    private static ExpenseResult? BuildAmounts(Money netAmount, Money vatAmount)
    {
        if (netAmount.Currency != vatAmount.Currency)
        {
            return new ExpenseResult(
                ExpenseRefusal.CurrencyMismatch, $"The net amount is in {netAmount.Currency}; the VAT amount is in {vatAmount.Currency}.", null);
        }

        if (netAmount.IsNegative || vatAmount.IsNegative)
            return new ExpenseResult(ExpenseRefusal.InvalidAmount, "Neither the net nor the VAT amount may be below zero.", null);

        return null;
    }

    private async Task<ProjectExpense?> FindExpenseAsync(Guid expenseId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(expenseId, cancellationToken).ConfigureAwait(false) as ProjectExpense;

    /// <summary>The archived-project guard: every mutating command on an archived project's objects is refused, here, before its own mutator ever runs.</summary>
    private async Task<ExpenseResult?> ArchivedAsync(ProjectExpense expense, CancellationToken cancellationToken)
    {
        if (await _context.Repository.FindAsync(expense.ProjectId, cancellationToken).ConfigureAwait(false) is not Project project)
            return null;

        return ProjectArchival.IsArchived(project, _time.GetUtcNow())
            ? new ExpenseResult(ExpenseRefusal.ProjectArchived, $"Project '{expense.ProjectId}' is archived (closed {project.ClosedOn:O}); this expense is read-only.", expense)
            : null;
    }

    private static ExpenseResult NotFound(Guid expenseId) =>
        new(ExpenseRefusal.ExpenseNotFound, $"No expense '{expenseId}' is registered.", null);

    private static ExpenseResult Invoiced(ProjectExpense expense) =>
        new(ExpenseRefusal.ExpenseInvoiced, $"Expense '{expense.Id}' is already invoiced (request '{expense.InvoicedBy:N}'); it is frozen.", expense);

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
