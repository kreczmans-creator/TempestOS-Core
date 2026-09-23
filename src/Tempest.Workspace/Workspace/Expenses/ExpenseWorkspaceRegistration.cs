using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Expenses;

/// <summary>The Expenses discipline's own command ids, the one place their strings live (`WP 21.3B`).</summary>
public static class ExpenseCommandIds
{
    /// <summary>Records a new expense against the open project.</summary>
    public const string Record = "expense.record";

    /// <summary>Amends the selected expense.</summary>
    public const string Amend = "expense.amend";

    /// <summary>Soft-deletes the selected expense.</summary>
    public const string Delete = "expense.delete";
}

/// <summary>
/// The single composition-root entry point wiring the Expenses discipline
/// into a running Workspace (`WP 21.3B`) — mirrors
/// <c>Timesheets.TimesheetsWorkspaceRegistration</c>'s own shape exactly:
/// an <c>EngineeringObjectBase</c> subtype with no discipline workspace of
/// its own before this Work Package, registered directly rather than
/// folded into <c>CanonicalObjectKinds</c> (`ADR-0150`'s own precedent,
/// which this Kind follows without a change of its own).
/// </summary>
/// <remarks>
/// No Rename/Delete factory is registered against the Explorer's own
/// generic right-click affordance: an expense's display name is derived,
/// not user-set, and its delete must refuse an invoiced expense — a rule
/// the generic delete command does not know.
/// <see cref="DeleteExpenseCommand"/> (<c>expense.delete</c>) is the one
/// way to delete one, and it is the one that enforces it.
/// </remarks>
public static class ExpenseWorkspaceRegistration
{
    /// <summary>The Project Explorer area this registration populates.</summary>
    public const string ExplorerAreaId = "expenses";

    /// <summary>The Kinds a new expense lands under when nothing container-shaped is selected — just the project.</summary>
    public static readonly IReadOnlyList<string> ContainerKinds = [MechanicalObjectFactoryRegistry.Project];

    private static readonly IReadOnlyList<string> BoundKinds = [ProjectExpense.CanonicalKind];

    /// <summary>Registers every Expenses Workspace extension point this Work Package owns.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, IExpenseService expenseService,
        ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(expenseService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(ExplorerAreaId, new ExpenseNodeProvider(ExplorerAreaId, domainContext));
        manager.RegisterFacetProvider(ProjectExpense.CanonicalKind, new ExpensePropertyFacetProvider(ProjectExpense.CanonicalKind, domainContext));
        manager.RegisterDeleteFactory(ProjectExpense.CanonicalKind, static (id, targetKind) => new DeleteExpenseCommand(id, targetKind));
        manager.RegisterView(ProjectExpense.CanonicalKind, new ExpenseObjectViewFactory(domainContext));

        commandDispatcher.RegisterHandler<RecordExpenseCommand>(new RecordExpenseCommandHandler(expenseService));
        commandDispatcher.RegisterHandler<AmendExpenseCommand>(new AmendExpenseCommandHandler(expenseService));
        commandDispatcher.RegisterHandler<DeleteExpenseCommand>(new DeleteExpenseCommandHandler(expenseService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: ExpenseCommandIds.Record, displayName: "Record Expense", category: "Expenses",
            description: "Records an out-of-pocket expense against the open project — a net and a VAT amount, exactly as a receipt states them.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (context, values) => new RecordExpenseCommand(
                    CreationPlacement.ParentFor(context, ContainerKinds),
                    ParseDate(values["date"]), values["description"], ParseCategory(values["category"]),
                    ParseMoney(values["net"]), ParseMoney(values["vat"]), bool.Parse(values["billable"])),
                [
                    new CommandParameter("date", "Date (yyyy-MM-dd)", DefaultValue: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Validate: ValidateRequiredDate),
                    WorkspaceCommandBindings.Required("description", "Description"),
                    WorkspaceCommandBindings.EnumChoice<ExpenseCategory>("category", "Category", nameof(ExpenseCategory.Other)),
                    new CommandParameter("net", "Net amount (\"amount currency\")", DefaultValue: string.Empty, Validate: ValidateMoney),
                    new CommandParameter("vat", "VAT amount (\"amount currency\")", DefaultValue: string.Empty, Validate: ValidateMoney),
                    WorkspaceCommandBindings.Choice("billable", "Billable", ["True", "False"], "True"),
                ]),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: ExpenseCommandIds.Amend, displayName: "Amend Expense", category: "Expenses",
            description: "Amends the selected expense's own description, category, amounts and billable flag. Refused once the expense is invoiced.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new AmendExpenseCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    values["description"], ParseCategory(values["category"]),
                    ParseMoney(values["net"]), ParseMoney(values["vat"]), bool.Parse(values["billable"])),
                [
                    WorkspaceCommandBindings.Required("description", "Description"),
                    WorkspaceCommandBindings.EnumChoice<ExpenseCategory>("category", "Category", nameof(ExpenseCategory.Other)),
                    new CommandParameter("net", "Net amount (\"amount currency\")", DefaultValue: string.Empty, Validate: ValidateMoney),
                    new CommandParameter("vat", "VAT amount (\"amount currency\")", DefaultValue: string.Empty, Validate: ValidateMoney),
                    WorkspaceCommandBindings.Choice("billable", "Billable", ["True", "False"], "True"),
                ],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: ExpenseCommandIds.Delete, displayName: "Delete Expense", category: "Expenses",
            description: "Soft-deletes the selected expense. Refused once the expense is invoiced.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteExpenseCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: BoundKinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Expense")),
        });
    }

    private static ExpenseCategory ParseCategory(string value) =>
        Enum.TryParse<ExpenseCategory>(value, ignoreCase: true, out var category) ? category : ExpenseCategory.Other;

    private static string? ValidateRequiredDate(string value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : "must be a valid date (yyyy-MM-dd).";

    private static DateOnly ParseDate(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : DateOnly.FromDateTime(DateTime.UtcNow);

    private static string? ValidateMoney(string value) =>
        TryParseMoney(value, out _) ? null : "must be \"<amount> <currency>\" (e.g. \"45.00 GBP\").";

    /// <summary>Parses <c>"&lt;amount&gt; &lt;currency&gt;"</c> — the identical text shape <c>Quotations.QuotationWorkspaceRegistration</c>'s own line commands already ask for.</summary>
    private static Money ParseMoney(string value) => TryParseMoney(value, out var money) ? money : default;

    private static bool TryParseMoney(string value, out Money money)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 && decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            try
            {
                money = new Money(amount, new CurrencyCode(parts[1]));
                return true;
            }
            catch (ArgumentException)
            {
                // Not three ASCII letters — falls through to the failure below.
            }
        }

        money = default;
        return false;
    }
}
