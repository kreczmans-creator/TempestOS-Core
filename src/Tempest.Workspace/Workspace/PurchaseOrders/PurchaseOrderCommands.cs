using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.PurchaseOrders;

namespace Tempest.Workspace.PurchaseOrders;

/// <summary>
/// Opens a new, empty <see cref="PurchaseOrder"/> with the shell's own
/// open project (`WP 21.3B`) — the target is the shell's ambient
/// <c>CommandContext.ProjectId</c>, never a selected object, mirroring
/// <c>Quotations.CreateQuotationCommand</c>'s own identical reasoning.
/// </summary>
public sealed class CreatePurchaseOrderCommand : ICommand
{
    /// <summary>Initialises a new instance of the <see cref="CreatePurchaseOrderCommand"/> class.</summary>
    public CreatePurchaseOrderCommand(Guid projectId, string? reference, string? supplierOrganisationId, DateOnly? expectedDelivery)
    {
        ProjectId = projectId;
        Reference = reference;
        SupplierOrganisationId = supplierOrganisationId;
        ExpectedDelivery = expectedDelivery;
    }

    /// <summary>The project this order is raised against — the shell's own open project, or <see cref="Guid.Empty"/> when none was, which <see cref="IPurchaseOrderService.CreateAsync"/> refuses as <see cref="PurchaseOrderRefusal.ProjectNotFound"/>.</summary>
    public Guid ProjectId { get; }

    /// <summary>A reference to use verbatim, or <see langword="null"/> to generate one.</summary>
    public string? Reference { get; }

    /// <summary>The supplier to raise against, or <see langword="null"/>.</summary>
    public string? SupplierOrganisationId { get; }

    /// <summary>When the goods or service are expected, or <see langword="null"/>.</summary>
    public DateOnly? ExpectedDelivery { get; }
}

/// <summary>Handles <see cref="CreatePurchaseOrderCommand"/>.</summary>
public sealed class CreatePurchaseOrderCommandHandler : ICommandHandler<CreatePurchaseOrderCommand>
{
    private readonly IPurchaseOrderService _service;

    /// <summary>Initialises a new instance of the <see cref="CreatePurchaseOrderCommandHandler"/> class.</summary>
    public CreatePurchaseOrderCommandHandler(IPurchaseOrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CreatePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await _service
            .CreateAsync(command.ProjectId, command.Reference, command.SupplierOrganisationId, notes: null, command.ExpectedDelivery, cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Purchase order '{result.Order!.Reference}' raised.", result.Order.Id, PurchaseOrder.CanonicalKind)
            : CommandResult.Failure(result.Reason ?? "The purchase order was refused.");
    }
}

/// <summary>Adds a line to the selected <see cref="PurchaseOrder"/> (<see cref="IPurchaseOrderService.AddLineAsync"/>).</summary>
public sealed class AddPurchaseOrderLineCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="AddPurchaseOrderLineCommand"/> class.</summary>
    public AddPurchaseOrderLineCommand(Guid targetObjectId, string targetKind, string description, decimal quantity, Money unitPrice, VatRate vatRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatRate = vatRate;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>What the line is.</summary>
    public string Description { get; }

    /// <summary>How many units.</summary>
    public decimal Quantity { get; }

    /// <summary>The price of one unit.</summary>
    public Money UnitPrice { get; }

    /// <summary>This line's own VAT treatment.</summary>
    public VatRate VatRate { get; }
}

/// <summary>Handles <see cref="AddPurchaseOrderLineCommand"/>.</summary>
public sealed class AddPurchaseOrderLineCommandHandler : ICommandHandler<AddPurchaseOrderLineCommand>
{
    private readonly IPurchaseOrderService _service;

    /// <summary>Initialises a new instance of the <see cref="AddPurchaseOrderLineCommandHandler"/> class.</summary>
    public AddPurchaseOrderLineCommandHandler(IPurchaseOrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(AddPurchaseOrderLineCommand command, CancellationToken cancellationToken)
    {
        var result = await _service
            .AddLineAsync(command.TargetObjectId, command.Description, command.Quantity, command.UnitPrice, command.VatRate, cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Line added — total now {result.Order!.GrossTotal}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The line was refused.");
    }
}

/// <summary>Replaces a line on the selected, Draft <see cref="PurchaseOrder"/> (<see cref="IPurchaseOrderService.UpdateLineAsync"/>).</summary>
public sealed class UpdatePurchaseOrderLineCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="UpdatePurchaseOrderLineCommand"/> class.</summary>
    public UpdatePurchaseOrderLineCommand(
        Guid targetObjectId, string targetKind, Guid lineId, string description, decimal quantity, Money unitPrice, VatRate vatRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        LineId = lineId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        VatRate = vatRate;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>The line to replace.</summary>
    public Guid LineId { get; }

    /// <summary>What the line is.</summary>
    public string Description { get; }

    /// <summary>How many units.</summary>
    public decimal Quantity { get; }

    /// <summary>The price of one unit.</summary>
    public Money UnitPrice { get; }

    /// <summary>This line's own VAT treatment.</summary>
    public VatRate VatRate { get; }
}

/// <summary>Handles <see cref="UpdatePurchaseOrderLineCommand"/>.</summary>
public sealed class UpdatePurchaseOrderLineCommandHandler : ICommandHandler<UpdatePurchaseOrderLineCommand>
{
    private readonly IPurchaseOrderService _service;

    /// <summary>Initialises a new instance of the <see cref="UpdatePurchaseOrderLineCommandHandler"/> class.</summary>
    public UpdatePurchaseOrderLineCommandHandler(IPurchaseOrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(UpdatePurchaseOrderLineCommand command, CancellationToken cancellationToken)
    {
        var result = await _service
            .UpdateLineAsync(command.TargetObjectId, command.LineId, command.Description, command.Quantity, command.UnitPrice, command.VatRate, cancellationToken)
            .ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Line updated — total now {result.Order!.GrossTotal}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The line could not be updated.");
    }
}

/// <summary>Removes a line from the selected, Draft <see cref="PurchaseOrder"/> (<see cref="IPurchaseOrderService.RemoveLineAsync"/>).</summary>
public sealed class RemovePurchaseOrderLineCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="RemovePurchaseOrderLineCommand"/> class.</summary>
    public RemovePurchaseOrderLineCommand(Guid targetObjectId, string targetKind, Guid lineId)
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

/// <summary>Handles <see cref="RemovePurchaseOrderLineCommand"/>.</summary>
public sealed class RemovePurchaseOrderLineCommandHandler : ICommandHandler<RemovePurchaseOrderLineCommand>
{
    private readonly IPurchaseOrderService _service;

    /// <summary>Initialises a new instance of the <see cref="RemovePurchaseOrderLineCommandHandler"/> class.</summary>
    public RemovePurchaseOrderLineCommandHandler(IPurchaseOrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RemovePurchaseOrderLineCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.RemoveLineAsync(command.TargetObjectId, command.LineId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Line removed — total now {result.Order!.GrossTotal}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The line could not be removed.");
    }
}

/// <summary>A purchase order status-moving command with no extra data — issue, receive, close, cancel or record-as-expenses all share this shape (`WP 21.3B`).</summary>
public abstract class PurchaseOrderActionCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="PurchaseOrderActionCommand"/> class.</summary>
    protected PurchaseOrderActionCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Issues the selected, Draft <see cref="PurchaseOrder"/> (<see cref="IPurchaseOrderService.IssueAsync"/>).</summary>
public sealed class IssuePurchaseOrderCommand : PurchaseOrderActionCommand
{
    /// <summary>Initialises a new instance of the <see cref="IssuePurchaseOrderCommand"/> class.</summary>
    public IssuePurchaseOrderCommand(Guid targetObjectId, string targetKind) : base(targetObjectId, targetKind)
    {
    }
}

/// <summary>Handles <see cref="IssuePurchaseOrderCommand"/>.</summary>
public sealed class IssuePurchaseOrderCommandHandler : ICommandHandler<IssuePurchaseOrderCommand>
{
    private readonly IPurchaseOrderService _service;

    /// <summary>Initialises a new instance of the <see cref="IssuePurchaseOrderCommandHandler"/> class.</summary>
    public IssuePurchaseOrderCommandHandler(IPurchaseOrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(IssuePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.IssueAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Issued on {result.Order!.IssuedDate:O}.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The purchase order could not be issued.");
    }
}

/// <summary>Marks the selected, Issued <see cref="PurchaseOrder"/> Received (<see cref="IPurchaseOrderService.ReceiveAsync"/>).</summary>
public sealed class ReceivePurchaseOrderCommand : PurchaseOrderActionCommand
{
    /// <summary>Initialises a new instance of the <see cref="ReceivePurchaseOrderCommand"/> class.</summary>
    public ReceivePurchaseOrderCommand(Guid targetObjectId, string targetKind) : base(targetObjectId, targetKind)
    {
    }
}

/// <summary>Handles <see cref="ReceivePurchaseOrderCommand"/>.</summary>
public sealed class ReceivePurchaseOrderCommandHandler : ICommandHandler<ReceivePurchaseOrderCommand>
{
    private readonly IPurchaseOrderService _service;

    /// <summary>Initialises a new instance of the <see cref="ReceivePurchaseOrderCommandHandler"/> class.</summary>
    public ReceivePurchaseOrderCommandHandler(IPurchaseOrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ReceivePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.ReceiveAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success("Received.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The purchase order could not be marked received.");
    }
}

/// <summary>Closes the selected, Received <see cref="PurchaseOrder"/> (<see cref="IPurchaseOrderService.CloseAsync"/>).</summary>
public sealed class ClosePurchaseOrderCommand : PurchaseOrderActionCommand
{
    /// <summary>Initialises a new instance of the <see cref="ClosePurchaseOrderCommand"/> class.</summary>
    public ClosePurchaseOrderCommand(Guid targetObjectId, string targetKind) : base(targetObjectId, targetKind)
    {
    }
}

/// <summary>Handles <see cref="ClosePurchaseOrderCommand"/>.</summary>
public sealed class ClosePurchaseOrderCommandHandler : ICommandHandler<ClosePurchaseOrderCommand>
{
    private readonly IPurchaseOrderService _service;

    /// <summary>Initialises a new instance of the <see cref="ClosePurchaseOrderCommandHandler"/> class.</summary>
    public ClosePurchaseOrderCommandHandler(IPurchaseOrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ClosePurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.CloseAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success("Closed.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The purchase order could not be closed.");
    }
}

/// <summary>Cancels the selected Draft or Issued <see cref="PurchaseOrder"/> (<see cref="IPurchaseOrderService.CancelAsync"/>).</summary>
public sealed class CancelPurchaseOrderCommand : PurchaseOrderActionCommand
{
    /// <summary>Initialises a new instance of the <see cref="CancelPurchaseOrderCommand"/> class.</summary>
    public CancelPurchaseOrderCommand(Guid targetObjectId, string targetKind) : base(targetObjectId, targetKind)
    {
    }
}

/// <summary>Handles <see cref="CancelPurchaseOrderCommand"/>.</summary>
public sealed class CancelPurchaseOrderCommandHandler : ICommandHandler<CancelPurchaseOrderCommand>
{
    private readonly IPurchaseOrderService _service;

    /// <summary>Initialises a new instance of the <see cref="CancelPurchaseOrderCommandHandler"/> class.</summary>
    public CancelPurchaseOrderCommandHandler(IPurchaseOrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(CancelPurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.CancelAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success("Cancelled.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The purchase order could not be cancelled.");
    }
}

/// <summary>
/// Records the selected, Received <see cref="PurchaseOrder"/>'s own lines
/// as project expenses, one per line (<see cref="IPurchaseOrderService.RecordLinesAsExpensesAsync"/>)
/// — the act that reaches the invoice seam with no ledger of its own.
/// </summary>
public sealed class RecordPurchaseOrderLinesAsExpensesCommand : PurchaseOrderActionCommand
{
    /// <summary>Initialises a new instance of the <see cref="RecordPurchaseOrderLinesAsExpensesCommand"/> class.</summary>
    public RecordPurchaseOrderLinesAsExpensesCommand(Guid targetObjectId, string targetKind) : base(targetObjectId, targetKind)
    {
    }
}

/// <summary>Handles <see cref="RecordPurchaseOrderLinesAsExpensesCommand"/>.</summary>
public sealed class RecordPurchaseOrderLinesAsExpensesCommandHandler : ICommandHandler<RecordPurchaseOrderLinesAsExpensesCommand>
{
    private readonly IPurchaseOrderService _service;

    /// <summary>Initialises a new instance of the <see cref="RecordPurchaseOrderLinesAsExpensesCommandHandler"/> class.</summary>
    public RecordPurchaseOrderLinesAsExpensesCommandHandler(IPurchaseOrderService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RecordPurchaseOrderLinesAsExpensesCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.RecordLinesAsExpensesAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"{result.Order!.Lines.Count} line(s) recorded as expenses.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The lines could not be recorded as expenses.");
    }
}
