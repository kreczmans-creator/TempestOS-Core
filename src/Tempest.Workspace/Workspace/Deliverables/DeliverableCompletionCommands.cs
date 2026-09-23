using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Deliverables;

/// <summary>
/// Completes a selected <c>Deliverable</c> (`WP 19.0A`, `ADR-0150`). Acts
/// on the existing, milestone-parented <c>Deliverable</c> object — the
/// project it belongs to is resolved from its own ancestry (deliverable →
/// milestone → project), never asked of the caller. Refused, as a result,
/// when this deliverable already carries a completion (the first is
/// shown), or when a cited evidence id is not Issued.
/// </summary>
public sealed class CompleteDeliverableCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="CompleteDeliverableCommand"/> class.</summary>
    public CompleteDeliverableCommand(
        Guid targetObjectId, string targetKind, DateOnly completedOn,
        IReadOnlyList<Guid>? issuedEvidenceIds = null, IReadOnlyList<Guid>? documentIds = null, Money? fixedPriceValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        CompletedOn = completedOn;
        IssuedEvidenceIds = issuedEvidenceIds ?? [];
        DocumentIds = documentIds ?? [];
        FixedPriceValue = fixedPriceValue;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the day the deliverable was completed.</summary>
    public DateOnly CompletedOn { get; }

    /// <summary>Gets the Evidence records, each Issued, this completion is supported by.</summary>
    public IReadOnlyList<Guid> IssuedEvidenceIds { get; }

    /// <summary>Gets the documents this completion is supported by.</summary>
    public IReadOnlyList<Guid> DocumentIds { get; }

    /// <summary>Gets the fixed price this deliverable is billed at, when it is. <see langword="null"/> otherwise.</summary>
    public Money? FixedPriceValue { get; }
}

/// <summary>Handles <see cref="CompleteDeliverableCommand"/>.</summary>
public sealed class CompleteDeliverableCommandHandler : ICommandHandler<CompleteDeliverableCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly IDeliverableService _service;

    /// <summary>Initialises a new instance of the <see cref="CompleteDeliverableCommandHandler"/> class.</summary>
    public CompleteDeliverableCommandHandler(EngineeringDomainContext context, IDeliverableService service)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(service);

        _context = context;
        _service = service;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Resolves the deliverable's own project by walking its ancestry
    /// (deliverable → milestone → project, <c>ProjectMilestoneService</c>'s
    /// own parenting) before ever calling
    /// <see cref="IDeliverableService.CompleteAsync"/> — so a deliverable
    /// selected from a malformed or incomplete hierarchy (including the
    /// non-existent id a generic contract test dispatches against) is
    /// refused as an outcome here, never a throw out of the service.
    /// </remarks>
    public async Task<CommandResult> HandleAsync(CompleteDeliverableCommand command, CancellationToken cancellationToken)
    {
        if (await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false) is not Deliverable deliverable)
            return CommandResult.Failure($"'{command.TargetObjectId}' is not a known deliverable.");

        if (deliverable.ParentId is not { } milestoneId
            || await _context.Repository.FindAsync(milestoneId, cancellationToken).ConfigureAwait(false) is not IHasParent milestone
            || milestone.ParentId is not { } projectId)
        {
            return CommandResult.Failure($"Deliverable '{command.TargetObjectId}' is not parented to a milestone in a project.");
        }

        DeliverableCompletionResult result;

        try
        {
            result = await _service
                .CompleteAsync(
                    command.TargetObjectId, projectId, command.CompletedOn,
                    command.IssuedEvidenceIds, command.DocumentIds, command.FixedPriceValue, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return result.Succeeded
            ? CommandResult.Success(
                $"Deliverable completed on {result.Completion!.CompletedOn:yyyy-MM-dd}.", result.Completion.Id, DeliverableCompletion.CanonicalKind)
            : CommandResult.Failure(result.Reason ?? "The completion was refused.");
    }
}

/// <summary>
/// Adds a deliverable directly to the shell's own open project, with no
/// quotation involved (<see cref="IDeliverableService.AddDeliverableAsync"/>,
/// `WP 19.5A`/`WP 19.5B`, `ADR-0152` §7, Product Owner comment item 4's
/// second half: "no way to add a deliverable").
/// </summary>
/// <remarks>
/// `WP 19.5B`: the Deliverables tab's own Add action, and the ribbon's
/// Deliverables category gaining the same command, both need a real
/// Command — mutating only through one, never a domain service called
/// directly from a Desktop view (`ADR-0063`). This Work Package's own
/// "files you own" list did not name this file; a disclosed, minimal
/// extension, exactly as `ADR-0152` §9 discloses its own single deviation
/// (see this Work Package's report).
/// </remarks>
public sealed class AddDeliverableCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="AddDeliverableCommand"/> class.</summary>
    public AddDeliverableCommand(Guid projectId, string title, DateOnly? targetDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        ProjectId = projectId;
        Title = title;
        TargetDate = targetDate;
    }

    /// <summary>The project this deliverable is added to — the shell's own open project, or <see cref="Guid.Empty"/> when none was, refused as an outcome rather than thrown.</summary>
    public Guid ProjectId { get; }

    /// <summary>The deliverable's own title.</summary>
    public string Title { get; }

    /// <summary>The default "Unquoted" milestone's own target date, used only the first time it is created for this project. <see langword="null"/> defaults to ninety days out.</summary>
    public DateOnly? TargetDate { get; }
}

/// <summary>Handles <see cref="AddDeliverableCommand"/>.</summary>
public sealed class AddDeliverableCommandHandler : ICommandHandler<AddDeliverableCommand>
{
    private readonly IDeliverableService _service;

    /// <summary>Initialises a new instance of the <see cref="AddDeliverableCommandHandler"/> class.</summary>
    public AddDeliverableCommandHandler(IDeliverableService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(AddDeliverableCommand command, CancellationToken cancellationToken)
    {
        if (command.ProjectId == Guid.Empty)
            return CommandResult.Failure("Open a project first — a deliverable is added to the project currently open.");

        try
        {
            var deliverable = await _service
                .AddDeliverableAsync(command.ProjectId, command.Title, command.TargetDate, cancellationToken)
                .ConfigureAwait(false);

            return CommandResult.Success($"Deliverable '{deliverable.DisplayName}' added.", deliverable.Id, CanonicalObjectKinds.Deliverable);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
