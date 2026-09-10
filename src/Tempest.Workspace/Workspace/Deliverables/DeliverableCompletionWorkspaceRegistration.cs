using System.Globalization;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

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

        commandDispatcher.RegisterHandler<CompleteDeliverableCommand>(new CompleteDeliverableCommandHandler(domainContext, deliverableService));

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
                BoundKinds),
        });
    }

    private static string? ValidateRequiredDate(string value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : "must be a valid date (yyyy-MM-dd).";

    private static DateOnly ParseDate(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : DateOnly.FromDateTime(DateTime.UtcNow);
}
