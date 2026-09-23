using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.Expenses;

namespace Tempest.Workspace.Expenses;

/// <summary>
/// Records a new <see cref="ProjectExpense"/>. Plain <see cref="ICommand"/>,
/// not <see cref="IWorkspaceCommand"/> — there is no pre-existing target,
/// only a new object to be revealed and opened right up once created,
/// mirroring <c>Timesheets.RecordTimesheetCommand</c>'s own identical
/// reasoning (`WP 21.3B`).
/// </summary>
public sealed class RecordExpenseCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="RecordExpenseCommand"/> class.</summary>
    public RecordExpenseCommand(
        Guid? projectId, DateOnly date, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        ProjectId = projectId;
        Date = date;
        Description = description;
        Category = category;
        NetAmount = netAmount;
        VatAmount = vatAmount;
        Billable = billable;
    }

    /// <summary>Gets where the new expense goes — the open project, or a chosen container (<c>CreationPlacement</c>). <see langword="null"/> when neither resolves.</summary>
    public Guid? ProjectId { get; }

    /// <summary>Gets the day the expense was incurred.</summary>
    public DateOnly Date { get; }

    /// <summary>Gets what the expense was.</summary>
    public string Description { get; }

    /// <summary>Gets what the expense was for.</summary>
    public ExpenseCategory Category { get; }

    /// <summary>Gets the amount before VAT.</summary>
    public Money NetAmount { get; }

    /// <summary>Gets the VAT amount.</summary>
    public Money VatAmount { get; }

    /// <summary>Gets whether the expense is billable to the client.</summary>
    public bool Billable { get; }
}

/// <summary>Handles <see cref="RecordExpenseCommand"/>.</summary>
public sealed class RecordExpenseCommandHandler : ICommandHandler<RecordExpenseCommand>
{
    private readonly IExpenseService _service;

    /// <summary>Initialises a new instance of the <see cref="RecordExpenseCommandHandler"/> class.</summary>
    public RecordExpenseCommandHandler(IExpenseService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RecordExpenseCommand command, CancellationToken cancellationToken)
    {
        if (command.ProjectId is not { } projectId)
            return CommandResult.Failure("Recording an expense needs an open project.");

        ExpenseResult result;

        try
        {
            result = await _service
                .RecordAsync(projectId, command.Date, command.Description, command.Category, command.NetAmount, command.VatAmount, command.Billable, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return result.Succeeded
            ? CommandResult.Success($"Recorded — '{result.Expense!.Description}', {result.Expense.GrossAmount} gross.", result.Expense.Id, ProjectExpense.CanonicalKind)
            : CommandResult.Failure(result.Reason ?? "The expense was refused.");
    }
}

/// <summary>Amends an expense's own description, category, amounts and billable flag (`WP 21.3B`). Refused, as a result, once the expense is invoiced.</summary>
public sealed class AmendExpenseCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="AmendExpenseCommand"/> class.</summary>
    public AmendExpenseCommand(
        Guid targetObjectId, string targetKind, string description, ExpenseCategory category, Money netAmount, Money vatAmount, bool billable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Description = description;
        Category = category;
        NetAmount = netAmount;
        VatAmount = vatAmount;
        Billable = billable;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the amended description.</summary>
    public string Description { get; }

    /// <summary>Gets the amended category.</summary>
    public ExpenseCategory Category { get; }

    /// <summary>Gets the amended net amount.</summary>
    public Money NetAmount { get; }

    /// <summary>Gets the amended VAT amount.</summary>
    public Money VatAmount { get; }

    /// <summary>Gets the amended billable flag.</summary>
    public bool Billable { get; }
}

/// <summary>Handles <see cref="AmendExpenseCommand"/>.</summary>
public sealed class AmendExpenseCommandHandler : ICommandHandler<AmendExpenseCommand>
{
    private readonly IExpenseService _service;

    /// <summary>Initialises a new instance of the <see cref="AmendExpenseCommandHandler"/> class.</summary>
    public AmendExpenseCommandHandler(IExpenseService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(AmendExpenseCommand command, CancellationToken cancellationToken)
    {
        var result = await _service
            .AmendAsync(command.TargetObjectId, command.Description, command.Category, command.NetAmount, command.VatAmount, command.Billable, cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success("Expense amended.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The expense could not be amended.");
    }
}

/// <summary>Soft-deletes an expense (`WP 21.3B`). Refused, as a result, once the expense is invoiced.</summary>
public sealed class DeleteExpenseCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="DeleteExpenseCommand"/> class.</summary>
    public DeleteExpenseCommand(Guid targetObjectId, string targetKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }
}

/// <summary>Handles <see cref="DeleteExpenseCommand"/>.</summary>
public sealed class DeleteExpenseCommandHandler : ICommandHandler<DeleteExpenseCommand>
{
    private readonly IExpenseService _service;

    /// <summary>Initialises a new instance of the <see cref="DeleteExpenseCommandHandler"/> class.</summary>
    public DeleteExpenseCommandHandler(IExpenseService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(DeleteExpenseCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success("Expense deleted.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The expense could not be deleted.");
    }
}
