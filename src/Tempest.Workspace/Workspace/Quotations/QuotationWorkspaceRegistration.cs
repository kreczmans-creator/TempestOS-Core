using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Quotations;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Quotations;

/// <summary>The command ids <see cref="QuotationWorkspaceRegistration"/> registers.</summary>
public static class QuotationCommandIds
{
    /// <summary>Opens a new, empty quotation with the shell's own open project.</summary>
    public const string Create = "quotation.create";

    /// <summary>Adds a line to the selected quotation.</summary>
    public const string AddLine = "quotation.add-line";

    /// <summary>Replaces a line on the selected quotation (`WP 19.5B`).</summary>
    public const string UpdateLine = "quotation.update-line";

    /// <summary>Removes a line from the selected quotation (`WP 19.5B`).</summary>
    public const string RemoveLine = "quotation.remove-line";

    /// <summary>Sends the selected quotation.</summary>
    public const string Send = "quotation.send";

    /// <summary>Accepts the selected quotation — creates a Deliverable and a Requirement per line.</summary>
    public const string Accept = "quotation.accept";

    /// <summary>Declines the selected quotation. Creates nothing.</summary>
    public const string Decline = "quotation.decline";
}

/// <summary>
/// The single composition-root entry point wiring the Quotations discipline
/// into a running Workspace (`WP 19.5A`, `ADR-0152`, Product Owner comment
/// item 4) — mirrors <c>Invoicing.InvoicingWorkspaceRegistration</c>'s own
/// shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>Renders through the generic Object Editor, not a bespoke view.</b> A
/// <c>KindEditorDeclaration</c> (<c>KindEditorDeclarations.Quotation</c>)
/// names Identity, a <c>QuotationLines</c> section and Lifecycle — the
/// section itself renders no content yet (no <c>PopulateQuotationAsync</c>
/// exists in <c>ObjectEditorView</c>; that, and the Lines section's own
/// real rendering, are <c>WP 19.5B</c>'s), but the declaration alone does
/// not break the editor: an unrecognised section key still shows its own
/// title and field list, exactly as every other Kind's declaration does
/// before its own <c>Populate*</c> method exists.
/// </para>
/// <para>
/// <b>No delete factory, no rename.</b> A quotation's own display name is
/// derived from its reference, not user-set, and there is no generic
/// delete for a quotation that may already have been sent — declining is
/// the one way to end a Sent quotation that will not be accepted, exactly
/// as <c>InvoiceRequest</c>'s own "no delete, only Void" precedent.
/// </para>
/// </remarks>
public static class QuotationWorkspaceRegistration
{
    /// <summary>The Project Explorer area this registration populates.</summary>
    public const string ExplorerAreaId = "quotations";

    private static readonly IReadOnlyList<string> QuotationKind = [Quotation.CanonicalKind];
    private static readonly IReadOnlyList<string> ProjectKind = [MechanicalObjectFactoryRegistry.Project];

    /// <summary>Registers every Quotations Workspace extension point this Work Package owns.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, IQuotationService quotationService,
        ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(quotationService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(ExplorerAreaId, new QuotationNodeProvider(ExplorerAreaId, domainContext));
        manager.RegisterFacetProvider(Quotation.CanonicalKind, new QuotationPropertyFacetProvider(Quotation.CanonicalKind, domainContext));
        manager.RegisterView(Quotation.CanonicalKind, new QuotationObjectViewFactory(domainContext));

        commandDispatcher.RegisterHandler<CreateQuotationCommand>(new CreateQuotationCommandHandler(quotationService));
        commandDispatcher.RegisterHandler<AddQuotationLineCommand>(new AddQuotationLineCommandHandler(quotationService));
        commandDispatcher.RegisterHandler<UpdateQuotationLineCommand>(new UpdateQuotationLineCommandHandler(quotationService));
        commandDispatcher.RegisterHandler<RemoveQuotationLineCommand>(new RemoveQuotationLineCommandHandler(quotationService));
        commandDispatcher.RegisterHandler<SendQuotationCommand>(new SendQuotationCommandHandler(quotationService));
        commandDispatcher.RegisterHandler<AcceptQuotationCommand>(new AcceptQuotationCommandHandler(quotationService));
        commandDispatcher.RegisterHandler<DeclineQuotationCommand>(new DeclineQuotationCommandHandler(quotationService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: QuotationCommandIds.Create, displayName: "Create Quotation", category: "Quotations",
            description: "Opens a new, empty quotation with the currently open project — its client defaults from the project's own client.")
        {
            // `CommandContextRequirement.None`: the target is the shell's
            // own ambient `CommandContext.ProjectId` (`CreationPlacement`'s
            // own precedent), never a selected object, so Create Quotation
            // is reachable from anywhere a project is open, matching the
            // Product Owner's own words verbatim ("the quote should be
            // opened with the project"). No project open resolves to
            // `Guid.Empty`, which `IQuotationService.CreateAsync` refuses
            // as `QuotationRefusal.ProjectNotFound` rather than throwing.
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (context, values) => new CreateQuotationCommand(context.ProjectId ?? Guid.Empty, WorkspaceCommandBindings.OrNull(values["reference"])),
                [new CommandParameter("reference", "Reference (blank to generate Q-<year>-<nnn>)", DefaultValue: string.Empty)]),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: QuotationCommandIds.AddLine, displayName: "Add Quotation Line", category: "Quotations",
            description: "Adds a line to the selected, Draft quotation — either hours and a rate, or a fixed price, never both.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new AddQuotationLineCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    values["description"], ParseDecimalOrNull(values["hours"]), ParseMoneyOrNull(values["rate"]), ParseMoneyOrNull(values["fixedPrice"])),
                [
                    WorkspaceCommandBindings.Required("description", "Description"),
                    new CommandParameter("hours", "Hours (hourly lines only)", DefaultValue: string.Empty, Validate: ValidateOptionalDecimal),
                    new CommandParameter("rate", "Rate (\"amount currency\", hourly lines only)", DefaultValue: string.Empty, Validate: ValidateOptionalMoney),
                    new CommandParameter("fixedPrice", "Fixed price (\"amount currency\", fixed-price lines only)", DefaultValue: string.Empty, Validate: ValidateOptionalMoney),
                ],
                appliesToKinds: QuotationKind),
        });

        // `WP 19.5B`: the Quote tab's own editable lines table (brief
        // scope item 2) needs a real command for "edit" and "remove", not
        // only "add" — see `QuotationCommands.UpdateQuotationLineCommand`'s
        // own remarks for why this file is touched beyond this Work
        // Package's own brief.
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: QuotationCommandIds.UpdateLine, displayName: "Update Quotation Line", category: "Quotations",
            description: "Replaces a line on the selected, Draft quotation — either hours and a rate, or a fixed price, never both.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new UpdateQuotationLineCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    ParseGuidOrEmpty(values["lineId"]), values["description"], ParseDecimalOrNull(values["hours"]),
                    ParseMoneyOrNull(values["rate"]), ParseMoneyOrNull(values["fixedPrice"])),
                [
                    new CommandParameter("lineId", "Line id", Validate: ValidateGuid),
                    WorkspaceCommandBindings.Required("description", "Description"),
                    new CommandParameter("hours", "Hours (hourly lines only)", DefaultValue: string.Empty, Validate: ValidateOptionalDecimal),
                    new CommandParameter("rate", "Rate (\"amount currency\", hourly lines only)", DefaultValue: string.Empty, Validate: ValidateOptionalMoney),
                    new CommandParameter("fixedPrice", "Fixed price (\"amount currency\", fixed-price lines only)", DefaultValue: string.Empty, Validate: ValidateOptionalMoney),
                ],
                appliesToKinds: QuotationKind),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: QuotationCommandIds.RemoveLine, displayName: "Remove Quotation Line", category: "Quotations",
            description: "Removes a line from the selected, Draft quotation.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RemoveQuotationLineCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, ParseGuidOrEmpty(values["lineId"])),
                [new CommandParameter("lineId", "Line id", Validate: ValidateGuid)],
                appliesToKinds: QuotationKind,
                confirmationMessage: "Remove this line from the quotation?"),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: QuotationCommandIds.Send, displayName: "Send Quotation", category: "Quotations",
            description: "Sends the selected, Draft quotation to its client — refused if it carries no lines.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new SendQuotationCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: QuotationKind,
                confirmationMessage: "Send the selected quotation?"),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: QuotationCommandIds.Accept, displayName: "Accept Quotation", category: "Quotations",
            description: "Accepts the selected, Sent quotation — creates one Deliverable and one Requirement per line, under a milestone named after the quotation's own reference.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new AcceptQuotationCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: QuotationKind,
                confirmationMessage: "Accept the selected quotation? This creates a Deliverable and a Requirement for every line."),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: QuotationCommandIds.Decline, displayName: "Decline Quotation", category: "Quotations",
            description: "Declines the selected, Sent quotation. Creates nothing.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeclineQuotationCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: QuotationKind,
                confirmationMessage: "Decline the selected quotation? This cannot be undone."),
        });
    }

    private static string? ValidateGuid(string value) =>
        Guid.TryParse(value, out _) ? null : "must be a valid line id.";

    private static Guid ParseGuidOrEmpty(string value) =>
        Guid.TryParse(value, out var id) ? id : Guid.Empty;

    private static string? ValidateOptionalDecimal(string value) =>
        string.IsNullOrWhiteSpace(value) || WorkspaceCommandBindings.ParseDecimal(value) is not null
            ? null
            : "must be a number, or blank.";

    private static decimal? ParseDecimalOrNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : WorkspaceCommandBindings.ParseDecimal(value);

    private static string? ValidateOptionalMoney(string value) =>
        string.IsNullOrWhiteSpace(value) || TryParseMoney(value, out _)
            ? null
            : "must be \"<amount> <currency>\" (e.g. \"150 GBP\"), or blank.";

    /// <summary>Parses <c>"&lt;amount&gt; &lt;currency&gt;"</c> — the identical text shape <c>Projects.ProjectCommercialWorkspaceRegistration</c>'s own <c>project.set-budget</c> command already asks for.</summary>
    private static Money? ParseMoneyOrNull(string value) =>
        !string.IsNullOrWhiteSpace(value) && TryParseMoney(value, out var money) ? money : null;

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
