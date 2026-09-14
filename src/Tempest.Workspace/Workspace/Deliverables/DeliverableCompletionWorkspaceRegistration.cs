using System.Globalization;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Deliverables;

/// <summary>
/// The single composition-root entry point wiring the Deliverables
/// discipline into a running Workspace (`WP 19.0A`, `ADR-0150`) — mirrors
/// <c>Evidence.EvidenceWorkspaceRegistration.Register</c>'s own shape.
/// </summary>
public static class DeliverableCompletionWorkspaceRegistration
{
    /// <summary>The Project Explorer area this registration populates.</summary>
    public const string ExplorerAreaId = "deliverables";

    private static readonly IReadOnlyList<string> BoundKinds = [CanonicalObjectKinds.Deliverable];

    /// <summary>Registers every Deliverables Workspace extension point this Work Package owns.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, IDeliverableService deliverableService,
        IPrincipalDirectory principalDirectory, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(deliverableService);
        ArgumentNullException.ThrowIfNull(principalDirectory);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(ExplorerAreaId, new DeliverableCompletionNodeProvider(ExplorerAreaId, domainContext));
        manager.RegisterFacetProvider(
            DeliverableCompletion.CanonicalKind, new DeliverableCompletionPropertyFacetProvider(DeliverableCompletion.CanonicalKind, domainContext, principalDirectory));
        manager.RegisterView(DeliverableCompletion.CanonicalKind, new DeliverableCompletionObjectViewFactory(domainContext));

        // `WP 19.10P` (D6): no discipline ever registered a view for the
        // Deliverable itself (only for its own DeliverableCompletion,
        // above) — every "open this deliverable right up" caller
        // (`ProjectQuoteView`'s own "Created on acceptance" section,
        // `ProjectDeliverablesView`'s own Add Deliverable) threw
        // `WorkspaceViewFactoryNotFoundException("Deliverable")`, swallowed
        // by the fire-and-forget open delegate, so nothing visibly
        // happened. `MechanicalWorkspaceViewFactory` is Kind-agnostic — it
        // reads `IHasBusinessIdentifier.DisplayName` off whatever the
        // repository hands back — so it is reused here exactly as
        // `ManufacturingWorkspaceRegistration` already reuses
        // `DocumentsWorkspaceViewFactory`/`VerificationActivityWorkspaceViewFactory`
        // for its own "WorkInstruction"/"Inspection" Kinds, rather than a
        // new near-identical type.
        manager.RegisterView(CanonicalObjectKinds.Deliverable, new MechanicalWorkspaceViewFactory(CanonicalObjectKinds.Deliverable, domainContext));

        commandDispatcher.RegisterHandler<CompleteDeliverableCommand>(new CompleteDeliverableCommandHandler(domainContext, deliverableService));
        commandDispatcher.RegisterHandler<AddDeliverableCommand>(new AddDeliverableCommandHandler(deliverableService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "deliverable.complete", displayName: "Complete Deliverable", category: "Deliverables",
            description: "Completes the selected deliverable. Refused once it already carries a completion — a deliverable can be completed once.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new CompleteDeliverableCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    ParseDate(values["completedOn"])),
                [new CommandParameter(
                    "completedOn", "Completed on (yyyy-MM-dd)",
                    DefaultValue: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Validate: ValidateRequiredDate)],
                BoundKinds,
                mutates: true),
        });

        // `WP 19.5B` (`ADR-0152` §7, Product Owner comment item 4's second
        // half): a deliverable added directly, with no quotation — the
        // Deliverables tab's own Add action, and the ribbon's Deliverables
        // category gaining the same command. `CommandContextRequirement.None`:
        // the target is the shell's own ambient `CommandContext.ProjectId`,
        // mirroring `Quotations.QuotationCommandIds.Create`'s identical
        // "opened with whatever project is open" shape.
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "deliverable.add", displayName: "Add Deliverable", category: "Deliverables",
            description: "Adds a deliverable directly to the open project, with no quotation — grouped under a default 'Unquoted' milestone.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (context, values) => new AddDeliverableCommand(
                    context.ProjectId ?? Guid.Empty, values["title"], ParseOptionalDate(values["targetDate"])),
                [
                    new CommandParameter("title", "Title", Validate: ValidateRequiredTitle),
                    new CommandParameter("targetDate", "Target date (yyyy-MM-dd, blank for 90 days out)", DefaultValue: string.Empty, Validate: ValidateOptionalDate),
                ],
                mutates: true),
        });
    }

    private static string? ValidateRequiredTitle(string value) =>
        string.IsNullOrWhiteSpace(value) ? "'Title' is required." : null;

    private static string? ValidateOptionalDate(string value) =>
        string.IsNullOrWhiteSpace(value) || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : "must be a valid date (yyyy-MM-dd), or blank.";

    private static DateOnly? ParseOptionalDate(string value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static string? ValidateRequiredDate(string value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : "must be a valid date (yyyy-MM-dd).";

    private static DateOnly ParseDate(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : DateOnly.FromDateTime(DateTime.UtcNow);
}
