using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.PurchaseOrders;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.PurchaseOrders;

/// <summary>The command ids <see cref="PurchaseOrderWorkspaceRegistration"/> registers.</summary>
public static class PurchaseOrderCommandIds
{
    /// <summary>Opens a new, empty purchase order with the shell's own open project.</summary>
    public const string Create = "purchaseorder.create";

    /// <summary>Adds a line to the selected purchase order.</summary>
    public const string AddLine = "purchaseorder.add-line";

    /// <summary>Replaces a line on the selected purchase order.</summary>
    public const string UpdateLine = "purchaseorder.update-line";

    /// <summary>Removes a line from the selected purchase order.</summary>
    public const string RemoveLine = "purchaseorder.remove-line";

    /// <summary>Issues the selected purchase order.</summary>
    public const string Issue = "purchaseorder.issue";

    /// <summary>Marks the selected purchase order Received.</summary>
    public const string Receive = "purchaseorder.receive";

    /// <summary>Closes the selected purchase order.</summary>
    public const string Close = "purchaseorder.close";

    /// <summary>Cancels the selected purchase order.</summary>
    public const string Cancel = "purchaseorder.cancel";

    /// <summary>Records the selected purchase order's own lines as project expenses.</summary>
    public const string RecordAsExpenses = "purchaseorder.record-as-expenses";
}

/// <summary>
/// The single composition-root entry point wiring the Purchase orders
/// discipline into a running Workspace (`WP 21.3B`) — mirrors
/// <c>Quotations.QuotationWorkspaceRegistration</c>'s own shape.
/// </summary>
/// <remarks>
/// <b>No delete factory, no rename.</b> A purchase order's own display
/// name is derived from its reference, not user-set, and there is no
/// generic delete — Cancel is the one way to end a Draft or Issued order
/// that will not proceed, exactly as <c>Quotation</c>'s own "no delete,
/// only decline/void" precedent.
/// </remarks>
public static class PurchaseOrderWorkspaceRegistration
{
    /// <summary>The Project Explorer area this registration populates.</summary>
    public const string ExplorerAreaId = "purchase-orders";

    private static readonly IReadOnlyList<string> PurchaseOrderKind = [PurchaseOrder.CanonicalKind];
    private static readonly IReadOnlyList<string> ProjectKind = [MechanicalObjectFactoryRegistry.Project];

    /// <summary>Registers every Purchase orders Workspace extension point this Work Package owns.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, IPurchaseOrderService purchaseOrderService,
        ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(purchaseOrderService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(ExplorerAreaId, new PurchaseOrderNodeProvider(ExplorerAreaId, domainContext));
        manager.RegisterFacetProvider(PurchaseOrder.CanonicalKind, new PurchaseOrderPropertyFacetProvider(PurchaseOrder.CanonicalKind, domainContext));
        manager.RegisterView(PurchaseOrder.CanonicalKind, new PurchaseOrderObjectViewFactory(domainContext));

        commandDispatcher.RegisterHandler<CreatePurchaseOrderCommand>(new CreatePurchaseOrderCommandHandler(purchaseOrderService));
        commandDispatcher.RegisterHandler<AddPurchaseOrderLineCommand>(new AddPurchaseOrderLineCommandHandler(purchaseOrderService));
        commandDispatcher.RegisterHandler<UpdatePurchaseOrderLineCommand>(new UpdatePurchaseOrderLineCommandHandler(purchaseOrderService));
        commandDispatcher.RegisterHandler<RemovePurchaseOrderLineCommand>(new RemovePurchaseOrderLineCommandHandler(purchaseOrderService));
        commandDispatcher.RegisterHandler<IssuePurchaseOrderCommand>(new IssuePurchaseOrderCommandHandler(purchaseOrderService));
        commandDispatcher.RegisterHandler<ReceivePurchaseOrderCommand>(new ReceivePurchaseOrderCommandHandler(purchaseOrderService));
        commandDispatcher.RegisterHandler<ClosePurchaseOrderCommand>(new ClosePurchaseOrderCommandHandler(purchaseOrderService));
        commandDispatcher.RegisterHandler<CancelPurchaseOrderCommand>(new CancelPurchaseOrderCommandHandler(purchaseOrderService));
        commandDispatcher.RegisterHandler<RecordPurchaseOrderLinesAsExpensesCommand>(new RecordPurchaseOrderLinesAsExpensesCommandHandler(purchaseOrderService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PurchaseOrderCommandIds.Create, displayName: "Create Purchase Order", category: "Purchase Orders",
            description: "Opens a new, empty purchase order with the currently open project.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (context, values) => new CreatePurchaseOrderCommand(
                    context.ProjectId ?? Guid.Empty, WorkspaceCommandBindings.OrNull(values["reference"]),
                    WorkspaceCommandBindings.OrNull(values["supplier"]), ParseOptionalDate(values["expectedDelivery"])),
                [
                    new CommandParameter("reference", "Reference (blank to generate PO-<year>-<nnn>)", DefaultValue: string.Empty),
                    new CommandParameter("supplier", "Supplier", DefaultValue: string.Empty),
                    new CommandParameter("expectedDelivery", "Expected delivery (yyyy-MM-dd, blank if unknown)", DefaultValue: string.Empty, Validate: ValidateOptionalDate),
                ],
                mutates: true),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PurchaseOrderCommandIds.AddLine, displayName: "Add Purchase Order Line", category: "Purchase Orders",
            description: "Adds a line to the selected, Draft purchase order.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new AddPurchaseOrderLineCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    values["description"], WorkspaceCommandBindings.ParseDecimal(values["quantity"]) ?? 0m, ParseMoney(values["unitPrice"]),
                    ParseVatRate(values["vatRate"])),
                [
                    WorkspaceCommandBindings.Required("description", "Description"),
                    WorkspaceCommandBindings.Decimal("quantity", "Quantity", defaultValue: "1"),
                    new CommandParameter("unitPrice", "Unit price (\"amount currency\")", DefaultValue: string.Empty, Validate: ValidateMoney),
                    WorkspaceCommandBindings.EnumChoice<VatRate>("vatRate", "VAT rate", nameof(VatRate.OutOfScope)),
                ],
                appliesToKinds: PurchaseOrderKind,
                mutates: true),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PurchaseOrderCommandIds.UpdateLine, displayName: "Update Purchase Order Line", category: "Purchase Orders",
            description: "Replaces a line on the selected, Draft purchase order.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new UpdatePurchaseOrderLineCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    ParseGuidOrEmpty(values["lineId"]), values["description"], WorkspaceCommandBindings.ParseDecimal(values["quantity"]) ?? 0m,
                    ParseMoney(values["unitPrice"]), ParseVatRate(values["vatRate"])),
                [
                    new CommandParameter("lineId", "Line id", DefaultValue: EmptyGuidText, Validate: ValidateGuid),
                    WorkspaceCommandBindings.Required("description", "Description"),
                    WorkspaceCommandBindings.Decimal("quantity", "Quantity", defaultValue: "1"),
                    new CommandParameter("unitPrice", "Unit price (\"amount currency\")", DefaultValue: string.Empty, Validate: ValidateMoney),
                    WorkspaceCommandBindings.EnumChoice<VatRate>("vatRate", "VAT rate", nameof(VatRate.OutOfScope)),
                ],
                appliesToKinds: PurchaseOrderKind,
                mutates: true),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PurchaseOrderCommandIds.RemoveLine, displayName: "Remove Purchase Order Line", category: "Purchase Orders",
            description: "Removes a line from the selected, Draft purchase order.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RemovePurchaseOrderLineCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, ParseGuidOrEmpty(values["lineId"])),
                [new CommandParameter("lineId", "Line id", DefaultValue: EmptyGuidText, Validate: ValidateGuid)],
                appliesToKinds: PurchaseOrderKind,
                confirmationMessage: "Remove this line from the purchase order?",
                mutates: true),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PurchaseOrderCommandIds.Issue, displayName: "Issue Purchase Order", category: "Purchase Orders",
            description: "Issues the selected, Draft purchase order to its supplier — refused if it carries no lines.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new IssuePurchaseOrderCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: PurchaseOrderKind,
                confirmationMessage: "Issue the selected purchase order?",
                mutates: true),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PurchaseOrderCommandIds.Receive, displayName: "Receive Purchase Order", category: "Purchase Orders",
            description: "Marks the selected, Issued purchase order Received.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new ReceivePurchaseOrderCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: PurchaseOrderKind,
                confirmationMessage: "Mark the selected purchase order received?",
                mutates: true),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PurchaseOrderCommandIds.Close, displayName: "Close Purchase Order", category: "Purchase Orders",
            description: "Closes the selected, Received purchase order.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new ClosePurchaseOrderCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: PurchaseOrderKind,
                confirmationMessage: "Close the selected purchase order?",
                mutates: true),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PurchaseOrderCommandIds.Cancel, displayName: "Cancel Purchase Order", category: "Purchase Orders",
            description: "Cancels the selected Draft or Issued purchase order.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new CancelPurchaseOrderCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: PurchaseOrderKind,
                confirmationMessage: "Cancel the selected purchase order? This cannot be undone.",
                mutates: true),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: PurchaseOrderCommandIds.RecordAsExpenses, displayName: "Record Lines As Expenses", category: "Purchase Orders",
            description: "Records every line on the selected, Received purchase order as a billable project expense — the seam that reaches invoicing with no ledger.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new RecordPurchaseOrderLinesAsExpensesCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: PurchaseOrderKind,
                confirmationMessage: "Record every line on this purchase order as a project expense?",
                mutates: true),
        });
    }

    /// <summary>A syntactically valid, semantically meaningless line id — every real caller supplies the actual line's own id directly, mirroring <c>Quotations.QuotationWorkspaceRegistration</c>'s own identical `lineId` remark.</summary>
    private static readonly string EmptyGuidText = Guid.Empty.ToString();

    private static string? ValidateGuid(string value) =>
        Guid.TryParse(value, out _) ? null : "must be a valid line id.";

    private static Guid ParseGuidOrEmpty(string value) =>
        Guid.TryParse(value, out var id) ? id : Guid.Empty;

    private static VatRate ParseVatRate(string value) =>
        Enum.TryParse<VatRate>(value, ignoreCase: true, out var rate) ? rate : VatRate.OutOfScope;

    private static string? ValidateOptionalDate(string value) =>
        string.IsNullOrWhiteSpace(value) || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : "must be a valid date (yyyy-MM-dd), or blank.";

    private static DateOnly? ParseOptionalDate(string value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static string? ValidateMoney(string value) =>
        TryParseMoney(value, out _) ? null : "must be \"<amount> <currency>\" (e.g. \"150 GBP\").";

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
