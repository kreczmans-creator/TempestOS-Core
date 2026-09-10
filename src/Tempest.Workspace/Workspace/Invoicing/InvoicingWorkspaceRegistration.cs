using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;

namespace Tempest.Workspace.Invoicing;

/// <summary>The command ids <see cref="InvoicingWorkspaceRegistration"/> registers.</summary>
public static class InvoicingCommandIds
{
    /// <summary>Raises a new request from a completed deliverable.</summary>
    public const string Raise = "invoicing.raise";

    /// <summary>Sends a request to its connector.</summary>
    public const string Send = "invoicing.send";

    /// <summary>Reconciles a request against its connector.</summary>
    public const string Reconcile = "invoicing.reconcile";

    /// <summary>Voids a local-only request.</summary>
    public const string Void = "invoicing.void";
}

/// <summary>
/// The single composition-root entry point wiring the Invoicing discipline
/// into a running Workspace (`WP 19.1A`, `ADR-0151`) — mirrors
/// <c>Evidence.EvidenceWorkspaceRegistration.Register</c>'s own identical
/// shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>No view, no rail entry.</b> This Work Package's own scope is the
/// model, the connector seam and the substrate — `WP 19.1A` parts 2 and 3
/// build the real connectors and the Invoicing area itself. The Object
/// Editor already opens any Kind generically from its own
/// <see cref="IPropertyFacetProvider"/>, which this registration <em>does</em>
/// supply, so a raised request still opens right up (Product Owner guard,
/// `WP 17.9.4`) even with no rail entry pointing at it yet.
/// </para>
/// <para>
/// <b>No rename or delete factory.</b> An invoice request's own display
/// name is derived, not user-set, and there is no generic "delete" for a
/// request that may already have reached the provider —
/// <see cref="VoidInvoiceCommand"/> (<c>invoicing.void</c>) is the one way
/// to end a request that never sent, exactly as
/// <c>Timesheets.TimesheetsWorkspaceRegistration</c>'s own remarks explain
/// for <c>timesheet.delete</c>.
/// </para>
/// </remarks>
public static class InvoicingWorkspaceRegistration
{
    /// <summary>The Project Explorer area this registration populates.</summary>
    public const string ExplorerAreaId = "invoicing";

    private static readonly IReadOnlyList<string> InvoiceRequestKind = [InvoiceRequest.CanonicalKind];
    private static readonly IReadOnlyList<string> DeliverableCompletionKind = [DeliverableCompletion.CanonicalKind];

    /// <summary>Registers every Invoicing Workspace extension point this Work Package owns.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, IInvoicingService invoicingService,
        ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(invoicingService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(ExplorerAreaId, new InvoiceRequestNodeProvider(ExplorerAreaId, domainContext));
        manager.RegisterFacetProvider(InvoiceRequest.CanonicalKind, new InvoiceRequestPropertyFacetProvider(InvoiceRequest.CanonicalKind, domainContext));
        manager.RegisterView(InvoiceRequest.CanonicalKind, new InvoiceRequestObjectViewFactory(domainContext));

        commandDispatcher.RegisterHandler<RaiseInvoiceCommand>(new RaiseInvoiceCommandHandler(invoicingService));
        commandDispatcher.RegisterHandler<SendInvoiceCommand>(new SendInvoiceCommandHandler(invoicingService));
        commandDispatcher.RegisterHandler<ReconcileInvoiceCommand>(new ReconcileInvoiceCommandHandler(invoicingService));
        commandDispatcher.RegisterHandler<VoidInvoiceCommand>(new VoidInvoiceCommandHandler(invoicingService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: InvoicingCommandIds.Raise, displayName: "Raise Invoice Request", category: "Invoicing",
            description: "Raises a new invoice request from the selected, completed deliverable — its own fixed price, plus every unbilled timesheet entry of its project, at their frozen rates.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new RaiseInvoiceCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: DeliverableCompletionKind,
                confirmationMessage: "Raise an invoice request from the selected, completed deliverable?"),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: InvoicingCommandIds.Send, displayName: "Send Invoice Request", category: "Invoicing",
            description: "Sends the selected, Draft invoice request to its connector, with the request's own id as the idempotency key.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new SendInvoiceCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: InvoiceRequestKind,
                confirmationMessage: "Send the selected invoice request?"),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: InvoicingCommandIds.Reconcile, displayName: "Reconcile Invoice Request", category: "Invoicing",
            description: "Reconciles the selected invoice request against its connector — resolves a lost response by reference, or refreshes its status, invoice number, issue and paid dates.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new ReconcileInvoiceCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: InvoiceRequestKind,
                confirmationMessage: "Reconcile the selected invoice request against its connector now, rather than waiting for the next poll?"),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: InvoicingCommandIds.Void, displayName: "Void Invoice Request", category: "Invoicing",
            description: "Voids the selected invoice request. Only a Draft or Rejected request can be voided here — one that reached the provider is voided there, and read back.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new VoidInvoiceCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: InvoiceRequestKind,
                confirmationMessage: "Void the selected invoice request? This cannot be undone."),
        });
    }
}
