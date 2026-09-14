using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.Quotations;

namespace Tempest.Workspace.Quotations;

/// <summary>
/// Opens a new, empty <see cref="Quotation"/> with the shell's own open
/// project (`WP 19.5A`, Product Owner comment item 4: "the quote should be
/// opened with the project") — the target is the shell's ambient
/// <c>CommandContext.ProjectId</c>, never a selected object, so Create
/// Quotation is reachable from anywhere a project is open.
/// </summary>
public sealed class CreateQuotationCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="CreateQuotationCommand"/> class.</summary>
    public CreateQuotationCommand(Guid projectId, string? reference)
    {
        ProjectId = projectId;
        Reference = reference;
    }

    /// <summary>The project this quotation is opened with — the shell's own open project, or <see cref="Guid.Empty"/> when none was, which <see cref="IQuotationService.CreateAsync"/> refuses as <see cref="QuotationRefusal.ProjectNotFound"/>.</summary>
    public Guid ProjectId { get; }

    /// <summary>A reference to use verbatim, or <see langword="null"/> to generate one.</summary>
    public string? Reference { get; }
}

/// <summary>Handles <see cref="CreateQuotationCommand"/>.</summary>
public sealed class CreateQuotationCommandHandler : ICommandHandler<CreateQuotationCommand>
{
    private readonly IQuotationService _service;

    /// <summary>Initialises a new instance of the <see cref="CreateQuotationCommandHandler"/> class.</summary>
    public CreateQuotationCommandHandler(IQuotationService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CreateQuotationCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(command.ProjectId, command.Reference, clientOrganisationId: null, cancellationToken).ConfigureAwait(false);

        // The shell reveals and opens whatever a create command names here
        // (Product Owner guard, `WP 17.9.4`).
        return result.Succeeded
            ? CommandResult.Success($"Quotation '{result.Quotation!.Reference}' opened.", result.Quotation.Id, Quotation.CanonicalKind)
            : CommandResult.Failure(result.Reason ?? "The quotation was refused.");
    }
}

/// <summary>Adds a line to the selected <see cref="Quotation"/> (<see cref="IQuotationService.AddLineAsync"/>).</summary>
public sealed class AddQuotationLineCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="AddQuotationLineCommand"/> class.</summary>
    public AddQuotationLineCommand(Guid targetObjectId, string targetKind, string description, decimal? hours, Money? rate, Money? fixedPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Description = description;
        Hours = hours;
        Rate = rate;
        FixedPrice = fixedPrice;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>What the line is.</summary>
    public string Description { get; }

    /// <summary>Billable hours, for an hourly line. <see langword="null"/> for a fixed-price line.</summary>
    public decimal? Hours { get; }

    /// <summary>The rate one hour bills at, for an hourly line. <see langword="null"/> for a fixed-price line.</summary>
    public Money? Rate { get; }

    /// <summary>The line's own fixed price. <see langword="null"/> for an hourly line.</summary>
    public Money? FixedPrice { get; }
}

/// <summary>Handles <see cref="AddQuotationLineCommand"/>.</summary>
public sealed class AddQuotationLineCommandHandler : ICommandHandler<AddQuotationLineCommand>
{
    private readonly IQuotationService _service;

    /// <summary>Initialises a new instance of the <see cref="AddQuotationLineCommandHandler"/> class.</summary>
    public AddQuotationLineCommandHandler(IQuotationService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(AddQuotationLineCommand command, CancellationToken cancellationToken)
    {
        var result = await _service
            .AddLineAsync(command.TargetObjectId, command.Description, command.Hours, command.Rate, command.FixedPrice, cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Line added — total now {result.Quotation!.Total}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The line was refused.");
    }
}

/// <summary>Sends the selected, Draft <see cref="Quotation"/> (<see cref="IQuotationService.SendAsync"/>).</summary>
public sealed class SendQuotationCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SendQuotationCommand"/> class.</summary>
    public SendQuotationCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="SendQuotationCommand"/>.</summary>
public sealed class SendQuotationCommandHandler : ICommandHandler<SendQuotationCommand>
{
    private readonly IQuotationService _service;

    /// <summary>Initialises a new instance of the <see cref="SendQuotationCommandHandler"/> class.</summary>
    public SendQuotationCommandHandler(IQuotationService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SendQuotationCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.SendAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Sent on {result.Quotation!.SentOn:O}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The quotation could not be sent.");
    }
}

/// <summary>
/// Accepts the selected, Sent <see cref="Quotation"/> (<see cref="IQuotationService.AcceptAsync"/>)
/// — creates one Deliverable and one Requirement per line.
/// </summary>
public sealed class AcceptQuotationCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="AcceptQuotationCommand"/> class.</summary>
    public AcceptQuotationCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="AcceptQuotationCommand"/>.</summary>
public sealed class AcceptQuotationCommandHandler : ICommandHandler<AcceptQuotationCommand>
{
    private readonly IQuotationService _service;

    /// <summary>Initialises a new instance of the <see cref="AcceptQuotationCommandHandler"/> class.</summary>
    public AcceptQuotationCommandHandler(IQuotationService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(AcceptQuotationCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.AcceptAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success(
                $"Accepted — {result.Quotation!.Lines.Count} deliverable(s) and requirement(s) created.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The quotation could not be accepted.");
    }
}

/// <summary>Declines the selected, Sent <see cref="Quotation"/> (<see cref="IQuotationService.DeclineAsync"/>). Creates nothing.</summary>
public sealed class DeclineQuotationCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="DeclineQuotationCommand"/> class.</summary>
    public DeclineQuotationCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="DeclineQuotationCommand"/>.</summary>
public sealed class DeclineQuotationCommandHandler : ICommandHandler<DeclineQuotationCommand>
{
    private readonly IQuotationService _service;

    /// <summary>Initialises a new instance of the <see cref="DeclineQuotationCommandHandler"/> class.</summary>
    public DeclineQuotationCommandHandler(IQuotationService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(DeclineQuotationCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.DeclineAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Declined on {result.Quotation!.DecidedOn:O}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The quotation could not be declined.");
    }
}
