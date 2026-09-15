using Tempest.Core.Calculations;
using Tempest.Core.Commands;

namespace Tempest.Workspace.Calculations;

/// <summary>
/// Compares the target Calculation Domain object's own two most recent
/// records — which input and result fields changed, old and new, with
/// units (`TD-29`) — the Calculation Management scope's own "Compare with
/// previous" capability. Never mutates: a read, not a new execution.
/// </summary>
public sealed class CompareCalculationWithPreviousCommand : IWorkspaceCommand
{
    public CompareCalculationWithPreviousCommand(Guid targetObjectId, string targetKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
    }

    /// <inheritdoc />
    /// <remarks>The Domain object whose own two most recent <c>CalculationRecord</c>s are compared — typically a <c>"Calculation"</c>.</remarks>
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }
}

/// <summary>Handles <see cref="CompareCalculationWithPreviousCommand"/>.</summary>
public sealed class CompareCalculationWithPreviousCommandHandler : ICommandHandler<CompareCalculationWithPreviousCommand>
{
    private readonly CalculationTemplateRegistry _templateRegistry;

    public CompareCalculationWithPreviousCommandHandler(CalculationTemplateRegistry templateRegistry)
    {
        ArgumentNullException.ThrowIfNull(templateRegistry);

        _templateRegistry = templateRegistry;
    }

    public async Task<CommandResult> HandleAsync(CompareCalculationWithPreviousCommand command, CancellationToken cancellationToken)
    {
        CalculationComparison comparison;

        try
        {
            comparison = await _templateRegistry.CompareWithPreviousAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);
        }
        catch (CalculationException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success(Format(comparison));
    }

    /// <summary>Formats a <see cref="CalculationComparison"/> as one status-bar/history line.</summary>
    private static string Format(CalculationComparison comparison)
    {
        var segments = new List<string>(3);

        if (comparison.InputComparisonNote is { } note)
            segments.Add(note);
        else if (comparison.InputChanges.Count > 0)
            segments.Add("input changed: " + string.Join("; ", comparison.InputChanges.Select(FormatDiff)));
        else
            segments.Add("input unchanged");

        segments.Add(comparison.ResultChanges.Count > 0
            ? "result changed: " + string.Join("; ", comparison.ResultChanges.Select(FormatDiff))
            : "result unchanged");

        return $"Compared record '{comparison.RecordIdA}' with '{comparison.RecordIdB}': {string.Join(" ", segments)}";
    }

    private static string FormatDiff(CalculationFieldDiff diff) =>
        $"{diff.FieldName} {diff.OldDisplay ?? "(none)"} -> {diff.NewDisplay ?? "(none)"}";
}
