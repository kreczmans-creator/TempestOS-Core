using System.Globalization;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Timesheets;
using Tempest.Core.Identity;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Timesheets;

/// <summary>
/// The single composition-root entry point wiring the Timesheets
/// discipline into a running Workspace (`WP 19.0A`, `ADR-0150`) — mirrors
/// <c>Evidence.EvidenceWorkspaceRegistration.Register</c>'s own shape.
/// </summary>
/// <remarks>
/// No Rename/Delete factory is registered against the Explorer's own
/// generic right-click affordance: a timesheet entry's display name is
/// derived, not user-set, and its delete must refuse an invoiced entry —
/// a rule the generic <c>DeleteMechanicalObjectCommand</c> does not know.
/// <see cref="Timesheets.DeleteTimesheetCommand"/> (`timesheet.delete`) is
/// the one way to delete an entry, and it is the one that enforces it.
/// </remarks>
public static class TimesheetsWorkspaceRegistration
{
    /// <summary>The Project Explorer area this registration populates.</summary>
    public const string ExplorerAreaId = "timesheets";

    /// <summary>The Kinds a new timesheet entry lands under when nothing container-shaped is selected — just the project.</summary>
    public static readonly IReadOnlyList<string> ContainerKinds = [MechanicalObjectFactoryRegistry.Project];

    private static readonly IReadOnlyList<string> BoundKinds = [TimesheetEntry.CanonicalKind];

    /// <summary>Registers every Timesheets Workspace extension point this Work Package owns.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, ITimesheetService timesheetService,
        IPrincipalDirectory principalDirectory, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(timesheetService);
        ArgumentNullException.ThrowIfNull(principalDirectory);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(ExplorerAreaId, new TimesheetEntryNodeProvider(ExplorerAreaId, domainContext));
        manager.RegisterFacetProvider(TimesheetEntry.CanonicalKind, new TimesheetEntryPropertyFacetProvider(TimesheetEntry.CanonicalKind, domainContext, principalDirectory));
        manager.RegisterView(TimesheetEntry.CanonicalKind, new TimesheetEntryObjectViewFactory(domainContext));

        commandDispatcher.RegisterHandler<RecordTimesheetCommand>(new RecordTimesheetCommandHandler(timesheetService));
        commandDispatcher.RegisterHandler<AmendTimesheetCommand>(new AmendTimesheetCommandHandler(timesheetService));
        commandDispatcher.RegisterHandler<DeleteTimesheetCommand>(new DeleteTimesheetCommandHandler(timesheetService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "timesheet.record", displayName: "Record Time", category: "Timesheets",
            description: "Records time against the open project, priced from its pinned rate card and the grade given, frozen from this moment on.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (context, values) => new RecordTimesheetCommand(
                    CreationPlacement.ParentFor(context, ContainerKinds),
                    ParseDate(values["date"]), WorkspaceCommandBindings.ParseDecimal(values["hours"]) ?? 0m,
                    bool.Parse(values["billable"]), values["grade"], values["task"]),
                [
                    new CommandParameter("date", "Date (yyyy-MM-dd)", DefaultValue: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Validate: ValidateRequiredDate),
                    WorkspaceCommandBindings.Decimal("hours", "Hours", defaultValue: "1"),
                    WorkspaceCommandBindings.Choice("billable", "Billable", ["True", "False"], "True"),
                    WorkspaceCommandBindings.Required("grade", "Grade"),
                    WorkspaceCommandBindings.Required("task", "Task"),
                ]),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "timesheet.amend", displayName: "Amend Time", category: "Timesheets",
            description: "Amends the selected entry's own hours, task and billable flag. Refused once the entry is invoiced.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new AmendTimesheetCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    WorkspaceCommandBindings.ParseDecimal(values["hours"]) ?? 0m, values["task"], bool.Parse(values["billable"])),
                [
                    WorkspaceCommandBindings.Decimal("hours", "Hours"),
                    WorkspaceCommandBindings.Required("task", "Task"),
                    WorkspaceCommandBindings.Choice("billable", "Billable", ["True", "False"], "True"),
                ],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "timesheet.delete", displayName: "Delete Time Entry", category: "Timesheets",
            description: "Soft-deletes the selected timesheet entry. Refused once the entry is invoiced.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteTimesheetCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: BoundKinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Timesheet Entry")),
        });
    }

    private static string? ValidateRequiredDate(string value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            ? null
            : "must be a valid date (yyyy-MM-dd).";

    private static DateOnly ParseDate(string value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : DateOnly.FromDateTime(DateTime.UtcNow);
}
