using Tempest.Core.Commands;
using Tempest.Core.Evidence;

namespace Tempest.Workspace.Evidence;

/// <summary>Declares a named, typed figure against a piece of evidence (<see cref="IEvidenceService.DeclareFigureAsync"/>).</summary>
public sealed class DeclareEvidenceFigureCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="DeclareEvidenceFigureCommand"/> class.</summary>
    public DeclareEvidenceFigureCommand(Guid targetObjectId, string targetKind, string name, DeclaredFigureRole role, string quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(quantity);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Name = name;
        Role = role;
        Quantity = quantity;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the figure's own name.</summary>
    public string Name { get; }

    /// <summary>Gets whether this figure was fed into the work, or came out of it.</summary>
    public DeclaredFigureRole Role { get; }

    /// <summary>Gets the value, as <c>"&lt;value&gt; &lt;unit symbol&gt;"</c> text.</summary>
    public string Quantity { get; }
}

/// <summary>Handles <see cref="DeclareEvidenceFigureCommand"/>.</summary>
public sealed class DeclareEvidenceFigureCommandHandler : ICommandHandler<DeclareEvidenceFigureCommand>
{
    private readonly IEvidenceService _service;

    /// <summary>Initialises a new instance of the <see cref="DeclareEvidenceFigureCommandHandler"/> class.</summary>
    public DeclareEvidenceFigureCommandHandler(IEvidenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);

        _service = service;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(DeclareEvidenceFigureCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var evidence = await _service.DeclareFigureAsync(
                command.TargetObjectId, command.Name, command.Role, command.Quantity, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Declared '{command.Name}'.", evidence.Id, command.TargetKind);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
