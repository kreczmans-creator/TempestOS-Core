using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.Projects;

namespace Tempest.Workspace.Projects;

/// <summary>Sets, or clears, the project's own client (`WP 19.0A`, `ADR-0150`).</summary>
public sealed class SetProjectClientCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetProjectClientCommand"/> class.</summary>
    public SetProjectClientCommand(Guid targetObjectId, string targetKind, string? organisationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        OrganisationId = organisationId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the client's own record id in the Organisation catalogue. <see langword="null"/> clears it.</summary>
    public string? OrganisationId { get; }
}

/// <summary>Handles <see cref="SetProjectClientCommand"/>.</summary>
public sealed class SetProjectClientCommandHandler : ICommandHandler<SetProjectClientCommand>
{
    private readonly IProjectCommercialService _service;

    /// <summary>Initialises a new instance of the <see cref="SetProjectClientCommandHandler"/> class.</summary>
    public SetProjectClientCommandHandler(IProjectCommercialService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetProjectClientCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.SetClientAsync(command.TargetObjectId, command.OrganisationId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success(
                command.OrganisationId is { } id ? $"Client set to organisation '{id}'." : "Client cleared.",
                command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The client could not be set.");
    }
}

/// <summary>Sets, or clears, the project's own client purchase-order reference (`WP 19.0A`, `ADR-0150`).</summary>
public sealed class SetProjectPurchaseOrderCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetProjectPurchaseOrderCommand"/> class.</summary>
    public SetProjectPurchaseOrderCommand(Guid targetObjectId, string targetKind, string? purchaseOrderReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        PurchaseOrderReference = purchaseOrderReference;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the client's own purchase-order reference. <see langword="null"/> clears it.</summary>
    public string? PurchaseOrderReference { get; }
}

/// <summary>Handles <see cref="SetProjectPurchaseOrderCommand"/>.</summary>
public sealed class SetProjectPurchaseOrderCommandHandler : ICommandHandler<SetProjectPurchaseOrderCommand>
{
    private readonly IProjectCommercialService _service;

    /// <summary>Initialises a new instance of the <see cref="SetProjectPurchaseOrderCommandHandler"/> class.</summary>
    public SetProjectPurchaseOrderCommandHandler(IProjectCommercialService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetProjectPurchaseOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.SetPurchaseOrderAsync(command.TargetObjectId, command.PurchaseOrderReference, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success(
                command.PurchaseOrderReference is { } reference ? $"Purchase order set to '{reference}'." : "Purchase order cleared.",
                command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The purchase order could not be set.");
    }
}

/// <summary>Sets, or clears, the project's own budget, given as <c>"&lt;amount&gt; &lt;currency&gt;"</c> text (`WP 19.0A`, `ADR-0150`).</summary>
public sealed class SetProjectBudgetCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetProjectBudgetCommand"/> class.</summary>
    public SetProjectBudgetCommand(Guid targetObjectId, string targetKind, Money? budget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Budget = budget;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the project's own budget. <see langword="null"/> clears it.</summary>
    public Money? Budget { get; }
}

/// <summary>Handles <see cref="SetProjectBudgetCommand"/>.</summary>
public sealed class SetProjectBudgetCommandHandler : ICommandHandler<SetProjectBudgetCommand>
{
    private readonly IProjectCommercialService _service;

    /// <summary>Initialises a new instance of the <see cref="SetProjectBudgetCommandHandler"/> class.</summary>
    public SetProjectBudgetCommandHandler(IProjectCommercialService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetProjectBudgetCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.SetBudgetAsync(command.TargetObjectId, command.Budget, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success(command.Budget is { } amount ? $"Budget set to {amount}." : "Budget cleared.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The budget could not be set.");
    }
}

/// <summary>Pins the Released rate card a project bills against (`WP 19.0A`, `ADR-0150`). Refused, as a result, when the card is not registered or has not reached Released — the Evidence citation way.</summary>
public sealed class PinProjectRateCardCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="PinProjectRateCardCommand"/> class.</summary>
    public PinProjectRateCardCommand(Guid targetObjectId, string targetKind, string rateCardId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(rateCardId);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        RateCardId = rateCardId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the rate card's own record id.</summary>
    public string RateCardId { get; }
}

/// <summary>Handles <see cref="PinProjectRateCardCommand"/>.</summary>
public sealed class PinProjectRateCardCommandHandler : ICommandHandler<PinProjectRateCardCommand>
{
    private readonly IProjectCommercialService _service;

    /// <summary>Initialises a new instance of the <see cref="PinProjectRateCardCommandHandler"/> class.</summary>
    public PinProjectRateCardCommandHandler(IProjectCommercialService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(PinProjectRateCardCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.PinRateCardAsync(command.TargetObjectId, command.RateCardId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success($"Rate card '{command.RateCardId}' pinned.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The rate card could not be pinned.");
    }
}

/// <summary>Sets the project's own start and target dates (`WP 19.0A`, `ADR-0150`).</summary>
public sealed class SetProjectDatesCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetProjectDatesCommand"/> class.</summary>
    public SetProjectDatesCommand(Guid targetObjectId, string targetKind, DateOnly? startDate, DateOnly? targetDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        StartDate = startDate;
        TargetDate = targetDate;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets when the project starts. <see langword="null"/> clears it.</summary>
    public DateOnly? StartDate { get; }

    /// <summary>Gets when the project is targeted to complete. <see langword="null"/> clears it.</summary>
    public DateOnly? TargetDate { get; }
}

/// <summary>Handles <see cref="SetProjectDatesCommand"/>.</summary>
public sealed class SetProjectDatesCommandHandler : ICommandHandler<SetProjectDatesCommand>
{
    private readonly IProjectCommercialService _service;

    /// <summary>Initialises a new instance of the <see cref="SetProjectDatesCommandHandler"/> class.</summary>
    public SetProjectDatesCommandHandler(IProjectCommercialService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetProjectDatesCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.SetDatesAsync(command.TargetObjectId, command.StartDate, command.TargetDate, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success("Dates set.", command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The dates could not be set.");
    }
}

/// <summary>Sets, or clears, the principal managing a project (`WP 19.0A`, `ADR-0150`).</summary>
public sealed class SetProjectManagerCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="SetProjectManagerCommand"/> class.</summary>
    public SetProjectManagerCommand(Guid targetObjectId, string targetKind, string? projectManagerIdentityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        ProjectManagerIdentityId = projectManagerIdentityId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the identity id of the principal managing the project. <see langword="null"/> clears it.</summary>
    public string? ProjectManagerIdentityId { get; }
}

/// <summary>Handles <see cref="SetProjectManagerCommand"/>.</summary>
public sealed class SetProjectManagerCommandHandler : ICommandHandler<SetProjectManagerCommand>
{
    private readonly IProjectCommercialService _service;

    /// <summary>Initialises a new instance of the <see cref="SetProjectManagerCommandHandler"/> class.</summary>
    public SetProjectManagerCommandHandler(IProjectCommercialService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetProjectManagerCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.SetProjectManagerAsync(command.TargetObjectId, command.ProjectManagerIdentityId, cancellationToken).ConfigureAwait(false);

        return result.Succeeded
            ? CommandResult.Success(
                command.ProjectManagerIdentityId is { } pm ? $"Project manager set to '{pm}'." : "Project manager cleared.",
                command.TargetObjectId, command.TargetKind)
            : CommandResult.Failure(result.Reason ?? "The project manager could not be set.");
    }
}
