using Tempest.Core.Commands;
using Tempest.Core.Invoicing;

namespace Tempest.Workspace.Invoicing;

/// <summary>
/// Raises a new <see cref="InvoiceRequest"/> from a completed deliverable
/// (<see cref="IInvoicingService.RaiseFromCompletionAsync"/>) — the target
/// is the selected <c>DeliverableCompletion</c>, the subject a create
/// command reveals is the new request (`WP 19.1A`, `ADR-0151`).
/// </summary>
public sealed class RaiseInvoiceCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="RaiseInvoiceCommand"/> class.</summary>
    public RaiseInvoiceCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="RaiseInvoiceCommand"/>.</summary>
public sealed class RaiseInvoiceCommandHandler : ICommandHandler<RaiseInvoiceCommand>
{
    private readonly IInvoicingService _service;

    /// <summary>Initialises a new instance of the <see cref="RaiseInvoiceCommandHandler"/> class.</summary>
    public RaiseInvoiceCommandHandler(IInvoicingService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RaiseInvoiceCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.RaiseFromCompletionAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        // The shell reveals and opens whatever a create command names here
        // (Product Owner guard, `WP 17.9.4`).
        return result.Succeeded
            ? CommandResult.Success($"Invoice request raised — {result.Request!.Lines.Count} line(s), {result.Request.Total}.", result.Request.Id, InvoiceRequest.CanonicalKind)
            : CommandResult.Failure(result.Reason ?? "The invoice request was refused.");
    }
}

/// <summary>Sends the selected <see cref="InvoiceRequest"/> to its connector (<see cref="IInvoicingService.SendAsync"/>).</summary>
public sealed class SendInvoiceCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SendInvoiceCommand"/> class.</summary>
    public SendInvoiceCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="SendInvoiceCommand"/>.</summary>
public sealed class SendInvoiceCommandHandler : ICommandHandler<SendInvoiceCommand>
{
    private readonly IInvoicingService _service;

    /// <summary>Initialises a new instance of the <see cref="SendInvoiceCommandHandler"/> class.</summary>
    public SendInvoiceCommandHandler(IInvoicingService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SendInvoiceCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.SendAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
            return CommandResult.Failure(result.Reason ?? "The request could not be sent.");

        // Attempting the send always succeeds as an act; what actually
        // happened is the connector's own outcome, read off the request's
        // own status (IInvoicingService.SendAsync's own remarks).
        return CommandResult.Success(DescribeSendOutcome(result.Request!), command.TargetObjectId, command.TargetKind);
    }

    private static string DescribeSendOutcome(InvoiceRequest request) => request.Status switch
    {
        InvoiceRequestStatus.Sent => $"Sent — external id '{request.ExternalId}'.",
        InvoiceRequestStatus.Rejected => $"Rejected: {request.LastError}",
        InvoiceRequestStatus.Reauthorise => "The connector needs re-authorising; nothing was sent.",
        InvoiceRequestStatus.Draft => "The connector could not be reached; the request stays Draft and was not retried automatically.",
        InvoiceRequestStatus.Unknown => "The response was lost; reconcile once the connector is reachable.",
        _ => $"Send attempted — status now {request.Status}.",
    };
}

/// <summary>Reconciles the selected <see cref="InvoiceRequest"/> against its connector (<see cref="IInvoicingService.ReconcileAsync"/>).</summary>
public sealed class ReconcileInvoiceCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="ReconcileInvoiceCommand"/> class.</summary>
    public ReconcileInvoiceCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="ReconcileInvoiceCommand"/>.</summary>
public sealed class ReconcileInvoiceCommandHandler : ICommandHandler<ReconcileInvoiceCommand>
{
    private readonly IInvoicingService _service;

    /// <summary>Initialises a new instance of the <see cref="ReconcileInvoiceCommandHandler"/> class.</summary>
    public ReconcileInvoiceCommandHandler(IInvoicingService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ReconcileInvoiceCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.ReconcileAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Reconciled — status {result.Request!.Status}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The request could not be reconciled.");
    }
}

/// <summary>Voids the selected, local-only <see cref="InvoiceRequest"/> (<see cref="IInvoicingService.VoidAsync"/>).</summary>
public sealed class VoidInvoiceCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="VoidInvoiceCommand"/> class.</summary>
    public VoidInvoiceCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="VoidInvoiceCommand"/>.</summary>
public sealed class VoidInvoiceCommandHandler : ICommandHandler<VoidInvoiceCommand>
{
    private readonly IInvoicingService _service;

    /// <summary>Initialises a new instance of the <see cref="VoidInvoiceCommandHandler"/> class.</summary>
    public VoidInvoiceCommandHandler(IInvoicingService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(VoidInvoiceCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.VoidAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success("Voided.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The request could not be voided.");
    }
}
