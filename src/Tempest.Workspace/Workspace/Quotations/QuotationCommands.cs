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
        var result = await _service
            .CreateAsync(command.ProjectId, command.Reference, clientOrganisationId: null, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

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
    public AddQuotationLineCommand(
        Guid targetObjectId, string targetKind, string description, decimal? hours, Money? rate, Money? fixedPrice, string? rateCardServiceCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Description = description;
        Hours = hours;
        Rate = rate;
        FixedPrice = fixedPrice;
        RateCardServiceCode = rateCardServiceCode;
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

    /// <summary>Runbook C3: the pinned rate card's own hourly entry this line's rate is taken from, or <see langword="null"/> for a fixed-price line or a rate given directly.</summary>
    public string? RateCardServiceCode { get; }
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
            .AddLineAsync(
                command.TargetObjectId, command.Description, command.Hours, command.Rate, command.FixedPrice,
                rateCardServiceCode: command.RateCardServiceCode, cancellationToken: cancellationToken)
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

/// <summary>Replaces a line on the selected, Draft <see cref="Quotation"/> (<see cref="IQuotationService.UpdateLineAsync"/>).</summary>
/// <remarks>
/// `WP 19.5B`: the Quote tab's own editable lines table (brief scope item
/// 2, "add, edit, remove rows") needs a real command for the "edit"
/// half — <c>Tempest.Core.Quotations.IQuotationService.UpdateLineAsync</c>
/// already existed (`WP 19.5A`), but no Workspace command wrapped it yet,
/// mirroring <see cref="AddQuotationLineCommand"/>'s own identical shape.
/// A disclosed extension of this Work Package's own "files you own" list
/// (see this Work Package's report): mutating only through a Command,
/// never a domain service called directly from a Desktop view, is this
/// platform's one rule for every write (`ADR-0063`).
/// </remarks>
public sealed class UpdateQuotationLineCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="UpdateQuotationLineCommand"/> class.</summary>
    public UpdateQuotationLineCommand(
        Guid targetObjectId, string targetKind, Guid lineId, string description, decimal? hours, Money? rate, Money? fixedPrice,
        string? rateCardServiceCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        LineId = lineId;
        Description = description;
        Hours = hours;
        Rate = rate;
        FixedPrice = fixedPrice;
        RateCardServiceCode = rateCardServiceCode;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The line to replace.</summary>
    public Guid LineId { get; }

    /// <summary>What the line is.</summary>
    public string Description { get; }

    /// <summary>Billable hours, for an hourly line. <see langword="null"/> for a fixed-price line.</summary>
    public decimal? Hours { get; }

    /// <summary>The rate one hour bills at, for an hourly line. <see langword="null"/> for a fixed-price line.</summary>
    public Money? Rate { get; }

    /// <summary>The line's own fixed price. <see langword="null"/> for an hourly line.</summary>
    public Money? FixedPrice { get; }

    /// <summary>Runbook C3: the pinned rate card's own hourly entry this line's rate is taken from, or <see langword="null"/> for a fixed-price line or a rate given directly.</summary>
    public string? RateCardServiceCode { get; }
}

/// <summary>Handles <see cref="UpdateQuotationLineCommand"/>.</summary>
public sealed class UpdateQuotationLineCommandHandler : ICommandHandler<UpdateQuotationLineCommand>
{
    private readonly IQuotationService _service;

    /// <summary>Initialises a new instance of the <see cref="UpdateQuotationLineCommandHandler"/> class.</summary>
    public UpdateQuotationLineCommandHandler(IQuotationService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(UpdateQuotationLineCommand command, CancellationToken cancellationToken)
    {
        var result = await _service
            .UpdateLineAsync(
                command.TargetObjectId, command.LineId, command.Description, command.Hours, command.Rate, command.FixedPrice,
                rateCardServiceCode: command.RateCardServiceCode, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Line updated — total now {result.Quotation!.Total}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The line could not be updated.");
    }
}

/// <summary>Removes a line from the selected, Draft <see cref="Quotation"/> (<see cref="IQuotationService.RemoveLineAsync"/>).</summary>
/// <remarks>`WP 19.5B`: see <see cref="UpdateQuotationLineCommand"/>'s own remarks — the "remove" half of the same disclosed extension.</remarks>
public sealed class RemoveQuotationLineCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="RemoveQuotationLineCommand"/> class.</summary>
    public RemoveQuotationLineCommand(Guid targetObjectId, string targetKind, Guid lineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        LineId = lineId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The line to remove.</summary>
    public Guid LineId { get; }
}

/// <summary>Handles <see cref="RemoveQuotationLineCommand"/>.</summary>
public sealed class RemoveQuotationLineCommandHandler : ICommandHandler<RemoveQuotationLineCommand>
{
    private readonly IQuotationService _service;

    /// <summary>Initialises a new instance of the <see cref="RemoveQuotationLineCommandHandler"/> class.</summary>
    public RemoveQuotationLineCommandHandler(IQuotationService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RemoveQuotationLineCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.RemoveLineAsync(command.TargetObjectId, command.LineId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Line removed — total now {result.Quotation!.Total}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The line could not be removed.");
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


/// <summary>
/// The four draft-and-review acts on a <see cref="Quotation"/> (runbook C3,
/// PO: "save the quote without exporting or sending, save it as a draft
/// and then review by second person, then export becomes R1").
/// </summary>
public enum QuotationReviewAct
{
    /// <summary>Saves the draft explicitly (<see cref="IQuotationService.SaveDraftAsync"/>).</summary>
    SaveDraft,

    /// <summary>Draft → In review (<see cref="IQuotationService.SubmitForReviewAsync"/>).</summary>
    SubmitForReview,

    /// <summary>In review → Approved, issuing the next revision (<see cref="IQuotationService.ApproveAsync"/>).</summary>
    Approve,

    /// <summary>In review → Draft, with a comment (<see cref="IQuotationService.ReturnToDraftAsync"/>).</summary>
    ReturnToDraft,
}

/// <summary>Runs one <see cref="QuotationReviewAct"/> on the selected <see cref="Quotation"/> (runbook C3).</summary>
public sealed class QuotationReviewCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="QuotationReviewCommand"/> class.</summary>
    /// <param name="targetObjectId">The quotation.</param>
    /// <param name="targetKind">Its Kind.</param>
    /// <param name="act">Which act to run.</param>
    /// <param name="comment">The reviewer's comment — required by <see cref="QuotationReviewAct.ReturnToDraft"/>, ignored otherwise.</param>
    public QuotationReviewCommand(Guid targetObjectId, string targetKind, QuotationReviewAct act, string? comment = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Act = act;
        Comment = comment;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Which act to run.</summary>
    public QuotationReviewAct Act { get; }

    /// <summary>The reviewer's comment, for <see cref="QuotationReviewAct.ReturnToDraft"/>.</summary>
    public string? Comment { get; }
}

/// <summary>Handles <see cref="QuotationReviewCommand"/>.</summary>
public sealed class QuotationReviewCommandHandler : ICommandHandler<QuotationReviewCommand>
{
    private readonly IQuotationService _service;

    /// <summary>Initialises a new instance of the <see cref="QuotationReviewCommandHandler"/> class.</summary>
    public QuotationReviewCommandHandler(IQuotationService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(QuotationReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = command.Act switch
        {
            QuotationReviewAct.SaveDraft => await _service.SaveDraftAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false),
            QuotationReviewAct.SubmitForReview => await _service.SubmitForReviewAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false),
            QuotationReviewAct.Approve => await _service.ApproveAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false),
            QuotationReviewAct.ReturnToDraft => await _service.ReturnToDraftAsync(command.TargetObjectId, command.Comment ?? string.Empty, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.Act, "Unknown quotation review act."),
        };

        if (!result.Succeeded)
            return CommandResult.Failure(result.Reason ?? "The quotation review act was refused.");

        var quote = result.Quotation!;
        var message = command.Act switch
        {
            QuotationReviewAct.SaveDraft => $"Draft saved — {quote.Lines.Count} line(s), total {quote.Total}.",
            QuotationReviewAct.SubmitForReview => $"'{quote.Reference}' submitted for review — a second person approves it.",
            QuotationReviewAct.Approve => $"'{quote.Reference}' approved as {quote.RevisionLabel} — ready to export and send.",
            _ => $"'{quote.Reference}' returned to draft.",
        };

        return CommandResult.Success(message, command.TargetObjectId, command.TargetKind);
    }
}
