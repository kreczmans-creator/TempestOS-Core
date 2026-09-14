using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Tasks;

namespace Tempest.Workspace.Tasks;

/// <summary>The command ids <see cref="TaskWorkspaceRegistration"/> registers.</summary>
public static class TaskCommandIds
{
    /// <summary>Creates a new, open manual task, optionally against the shell's own open project.</summary>
    public const string Create = "task.create";

    /// <summary>Marks the selected task done.</summary>
    public const string Complete = "task.complete";
}

/// <summary>
/// The single composition-root entry point wiring the Tasks discipline
/// into a running Workspace (`WP 19.5C`, Product Owner comment item 6) —
/// mirrors <c>Quotations.QuotationWorkspaceRegistration</c>'s own shape.
/// </summary>
/// <remarks>
/// <b>Renders through the generic Object Editor, not a bespoke view.</b> A
/// <c>KindEditorDeclaration</c> (<c>KindEditorDeclarations.Task</c>) names
/// Identity and Lifecycle only — a manual task carries no field the
/// generic editor needs a bespoke section for.
/// </remarks>
public static class TaskWorkspaceRegistration
{
    /// <summary>The Project Explorer area this registration populates.</summary>
    public const string ExplorerAreaId = "tasks";

    private static readonly IReadOnlyList<string> TaskKind = [ManualTask.CanonicalKind];

    /// <summary>Registers every Tasks Workspace extension point this Work Package owns.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, ITaskService taskService,
        ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(taskService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(ExplorerAreaId, new TaskNodeProvider(ExplorerAreaId, domainContext));
        manager.RegisterFacetProvider(ManualTask.CanonicalKind, new TaskPropertyFacetProvider(ManualTask.CanonicalKind, domainContext));
        manager.RegisterView(ManualTask.CanonicalKind, new TaskObjectViewFactory(domainContext));

        commandDispatcher.RegisterHandler<CreateTaskCommand>(new CreateTaskCommandHandler(taskService));
        commandDispatcher.RegisterHandler<CompleteTaskCommand>(new CompleteTaskCommandHandler(taskService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: TaskCommandIds.Create, displayName: "Create Task", category: "Tasks",
            description: "Creates a new, open task — against the currently open project, if one is, or with no project at all.")
        {
            // `CommandContextRequirement.None`: the target is the shell's
            // own ambient `CommandContext.ProjectId` (`Quotations.QuotationWorkspaceRegistration`'s
            // own precedent) — Create Task is reachable from anywhere,
            // project open or not, since a manual task's own project is
            // optional.
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (context, values) => new CreateTaskCommand(values["title"], context.ProjectId, ParseDateOrNull(values["dueDate"])),
                [
                    WorkspaceCommandBindings.Required("title", "Title"),
                    new CommandParameter("dueDate", "Due date (yyyy-mm-dd, blank for none)", DefaultValue: string.Empty, Validate: ValidateOptionalDate),
                ]),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: TaskCommandIds.Complete, displayName: "Complete Task", category: "Tasks",
            description: "Marks the selected task done.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new CompleteTaskCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: TaskKind),
        });
    }

    private static string? ValidateOptionalDate(string value) =>
        string.IsNullOrWhiteSpace(value) || DateOnly.TryParse(value, out _)
            ? null
            : "must be a date (yyyy-mm-dd), or blank.";

    private static DateOnly? ParseDateOrNull(string value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, out var date) ? date : null;
}
