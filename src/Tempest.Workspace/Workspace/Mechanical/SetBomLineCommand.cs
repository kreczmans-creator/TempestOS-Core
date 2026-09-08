using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>Sets one Mechanical Product Structure object's own Bill of Materials line (<see cref="IHasBomLine.SetBomLineAsync"/>).</summary>
public sealed class SetBomLineCommand : IWorkspaceCommand
{
    public SetBomLineCommand(
        Guid targetObjectId, string targetKind, decimal quantity, string? unitOfMeasure = null,
        string? findNumber = null, string? itemNumber = null, string? referenceDesignator = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Quantity = quantity;
        UnitOfMeasure = unitOfMeasure;
        FindNumber = findNumber;
        ItemNumber = itemNumber;
        ReferenceDesignator = referenceDesignator;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets how many of the target are used under its current parent.</summary>
    public decimal Quantity { get; }

    /// <summary>Gets the unit <see cref="Quantity"/> is expressed in, or <see langword="null"/> to leave it unset.</summary>
    public string? UnitOfMeasure { get; }

    /// <summary>Gets the drawing-callout find number, or <see langword="null"/> to leave it unset.</summary>
    public string? FindNumber { get; }

    /// <summary>Gets the BOM line sequence number, or <see langword="null"/> to leave it unset.</summary>
    public string? ItemNumber { get; }

    /// <summary>Gets the reference designator(s), or <see langword="null"/> to leave it unset.</summary>
    public string? ReferenceDesignator { get; }
}

/// <summary>Handles <see cref="SetBomLineCommand"/>.</summary>
public sealed class SetBomLineCommandHandler : ICommandHandler<SetBomLineCommand>
{
    private readonly EngineeringDomainContext _context;

    public SetBomLineCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(SetBomLineCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasBomLine bomLine)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind carries no Bill of Materials line.");

        try
        {
            await bomLine.SetBomLineAsync(
                command.Quantity, command.UnitOfMeasure, command.FindNumber, command.ItemNumber, command.ReferenceDesignator, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Set BOM line for '{command.TargetObjectId}': ×{command.Quantity} {command.UnitOfMeasure}.");
    }
}
