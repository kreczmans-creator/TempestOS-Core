using Tempest.Core.Commands;
using Tempest.Core.Tasks;

namespace Tempest.Workspace.Tasks;

/// <summary>Creates a new, open <see cref="ManualTask"/> (<see cref="ITaskService.CreateAsync"/>).</summary>
public sealed class CreateTaskCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="CreateTaskCommand"/> class.</summary>
    public CreateTaskCommand(string title, Guid? projectId, DateOnly? dueDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title;
        ProjectId = projectId;
        DueDate = dueDate;
    }

    /// <summary>The task's own title.</summary>
    public string Title { get; }

    /// <summary>The project this task belongs to — the shell's own open project, or <see langword="null"/> when none is open, or when this task belongs to no project.</summary>
    public Guid? ProjectId { get; }

    /// <summary>When this task is due, or <see langword="null"/> for none.</summary>
    public DateOnly? DueDate { get; }
}

/// <summary>Handles <see cref="CreateTaskCommand"/>.</summary>
public sealed class CreateTaskCommandHandler : ICommandHandler<CreateTaskCommand>
{
    private readonly ITaskService _service;

    /// <summary>Initialises a new instance of the <see cref="CreateTaskCommandHandler"/> class.</summary>
    public CreateTaskCommandHandler(ITaskService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(command.Title, command.ProjectId, command.DueDate, cancellationToken).ConfigureAwait(false);

        // The shell reveals and opens whatever a create command names here
        // (Product Owner guard, `WP 17.9.4`).
        return result.Succeeded
            ? CommandResult.Success($"Task '{result.Task!.DisplayName}' created.", result.Task.Id, ManualTask.CanonicalKind)
            : CommandResult.Failure(result.Reason ?? "The task was refused.");
    }
}

/// <summary>Marks the selected <see cref="ManualTask"/> done (<see cref="ITaskService.CompleteAsync"/>).</summary>
public sealed class CompleteTaskCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="CompleteTaskCommand"/> class.</summary>
    public CompleteTaskCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="CompleteTaskCommand"/>.</summary>
public sealed class CompleteTaskCommandHandler : ICommandHandler<CompleteTaskCommand>
{
    private readonly ITaskService _service;

    /// <summary>Initialises a new instance of the <see cref="CompleteTaskCommandHandler"/> class.</summary>
    public CompleteTaskCommandHandler(ITaskService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CompleteTaskCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.CompleteAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Task '{result.Task!.DisplayName}' marked done.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The task could not be marked done.");
    }
}
