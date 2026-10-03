using Tempest.Core.Commands;
using Tempest.Core.Invoicing;

namespace Tempest.Workspace.Invoicing;

/// <summary>
/// Raises a new <see cref="InvoiceRequest"/> from a completed deliverable
/// (<see cref="IInvoicingService.RaiseFromCompletionAsync"/>) or, `WP
/// 21.3B`, from a billable expense with no completion available at all
/// (<see cref="IInvoicingService.RaiseFromExpenseAsync"/>) — the target is
/// the selected <c>DeliverableCompletion</c> or <c>ProjectExpense</c>,
/// <see cref="RaiseInvoiceCommandHandler"/> reading
/// <see cref="TargetKind"/> to know which; the subject a create command
/// reveals is the new request either way (`WP 19.1A`, `ADR-0151`).
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
        // `WP 21.3B`: the identical command, over whichever Kind the
        // caller selected — a project whose only unbilled work is an
        // expense has no completion to raise from at all.
        var result = string.Equals(command.TargetKind, Tempest.Core.Expenses.ProjectExpense.CanonicalKind, StringComparison.Ordinal)
            ? await _service.RaiseFromExpenseAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false)
            : await _service.RaiseFromCompletionAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

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

/// <summary>
/// Edits one line of the selected <see cref="InvoiceRequest"/>
/// (<see cref="IInvoicingService.ReviseLinesAsync"/>, `v0.24.0` review-board
/// fix M4): a Draft or Rejected request locally; a Sent one only while the
/// accounting system (Xero) still holds its invoice as a draft, which is
/// updated to match. A blank value keeps the line's current one.
/// </summary>
public sealed class ReviseInvoiceLinesCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="ReviseInvoiceLinesCommand"/> class.</summary>
    /// <param name="targetObjectId">The invoice request.</param>
    /// <param name="targetKind">Its Kind.</param>
    /// <param name="lineNumber">The line to edit, counting from 1 in the request's own order.</param>
    /// <param name="description">The new description; blank keeps the current one.</param>
    /// <param name="quantity">The new quantity (invariant culture); blank keeps the current one.</param>
    /// <param name="unitRate">The new unit rate in the request's own currency (invariant culture); blank keeps the current one.</param>
    /// <param name="vatRate">The new VAT rate (a <see cref="Tempest.Core.BusinessGovernance.VatRate"/> name); blank keeps the current one.</param>
    public ReviseInvoiceLinesCommand(
        Guid targetObjectId, string targetKind, string lineNumber, string? description = null, string? quantity = null, string? unitRate = null, string? vatRate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(lineNumber);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        LineNumber = lineNumber.Trim();
        Description = description;
        Quantity = quantity;
        UnitRate = unitRate;
        VatRate = vatRate;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The line to edit, counting from 1.</summary>
    public string LineNumber { get; }

    /// <summary>The new description; blank keeps the current one.</summary>
    public string? Description { get; }

    /// <summary>The new quantity; blank keeps the current one.</summary>
    public string? Quantity { get; }

    /// <summary>The new unit rate; blank keeps the current one.</summary>
    public string? UnitRate { get; }

    /// <summary>The new VAT rate's name; blank keeps the current one.</summary>
    public string? VatRate { get; }
}

/// <summary>Handles <see cref="ReviseInvoiceLinesCommand"/>.</summary>
public sealed class ReviseInvoiceLinesCommandHandler : ICommandHandler<ReviseInvoiceLinesCommand>
{
    private readonly IInvoicingService _service;
    private readonly Func<Guid, CancellationToken, Task<InvoiceRequest?>> _findRequest;

    /// <summary>Initialises a new instance of the <see cref="ReviseInvoiceLinesCommandHandler"/> class.</summary>
    /// <param name="service">The invoicing service.</param>
    /// <param name="findRequest">Reads the request, for the line's current values.</param>
    public ReviseInvoiceLinesCommandHandler(IInvoicingService service, Func<Guid, CancellationToken, Task<InvoiceRequest?>> findRequest)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(findRequest);
        _service = service;
        _findRequest = findRequest;
    }

    /// <summary>Validates a line number: a whole number of 1 or more.</summary>
    /// <param name="value">The value entered.</param>
    public static string? CheckLineNumber(string value) =>
        int.TryParse(value?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 1
            ? null
            : "Enter the line's number, counting from 1.";

    /// <summary>Validates an optional decimal: blank, or a number of 0 or more.</summary>
    /// <param name="value">The value entered.</param>
    public static string? CheckOptionalAmount(string value) =>
        string.IsNullOrWhiteSpace(value)
        || (decimal.TryParse(value.Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var d) && d >= 0m)
            ? null
            : "Enter a number (for example 2.5), or leave it blank to keep the current one.";

    /// <summary>Validates an optional VAT rate: blank, or one of <see cref="Tempest.Core.BusinessGovernance.VatRate"/>'s names.</summary>
    /// <param name="value">The value entered.</param>
    public static string? CheckOptionalVatRate(string value) =>
        string.IsNullOrWhiteSpace(value) || Enum.TryParse<Tempest.Core.BusinessGovernance.VatRate>(value.Trim(), ignoreCase: true, out _)
            ? null
            : $"Enter one of: {string.Join(", ", Enum.GetNames<Tempest.Core.BusinessGovernance.VatRate>())}; or leave it blank to keep the current one.";

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ReviseInvoiceLinesCommand command, CancellationToken cancellationToken)
    {
        if (CheckLineNumber(command.LineNumber) is { } badLine)
            return CommandResult.Failure(badLine);

        if (await _findRequest(command.TargetObjectId, cancellationToken).ConfigureAwait(false) is not { } request)
            return CommandResult.Failure("The invoice request could not be found.");

        var index = int.Parse(command.LineNumber, System.Globalization.CultureInfo.InvariantCulture) - 1;
        if (index >= request.Lines.Count)
            return CommandResult.Failure($"The request has {request.Lines.Count} line(s); there is no line {command.LineNumber}.");

        foreach (var problem in new[] { CheckOptionalAmount(command.Quantity ?? string.Empty), CheckOptionalAmount(command.UnitRate ?? string.Empty), CheckOptionalVatRate(command.VatRate ?? string.Empty) })
        {
            if (problem is not null)
                return CommandResult.Failure(problem);
        }

        var line = request.Lines[index];
        var revision = new InvoiceRequestLineRevision(
            line.SourceId,
            string.IsNullOrWhiteSpace(command.Description) ? line.Description : command.Description.Trim(),
            string.IsNullOrWhiteSpace(command.Quantity) ? line.Quantity : decimal.Parse(command.Quantity.Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(command.UnitRate)
                ? line.UnitRate
                : new Tempest.Core.BusinessGovernance.Money(decimal.Parse(command.UnitRate.Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture), request.Currency),
            string.IsNullOrWhiteSpace(command.VatRate) ? line.VatRate : Enum.Parse<Tempest.Core.BusinessGovernance.VatRate>(command.VatRate.Trim(), ignoreCase: true));

        var result = await _service.ReviseLinesAsync(command.TargetObjectId, [revision], cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? CommandResult.Success($"Line {command.LineNumber} revised — total now {result.Request!.Total}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The lines could not be revised.");
    }
}
