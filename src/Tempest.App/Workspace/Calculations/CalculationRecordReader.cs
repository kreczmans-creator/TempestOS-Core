using System.Text.Json;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Reads a Calculation Domain object's own execution history back —
/// every <c>"calculatedBy"</c>-linked <see cref="CalculationRecord{TResult}"/>
/// document — generically, without ever needing to know the executing
/// Template's own <c>TResult</c> type statically. The Workspace layer only
/// ever sees these records after the fact (Property Inspector, Cockpit
/// KPIs), so this reads the same JSON
/// <see cref="EngineeringData.IDocumentRevision.Content"/>
/// <see cref="CalculationEngine"/> itself already wrote via
/// <see cref="EngineeringData.IEngineeringDocumentStore.GetRevisionHistoryAsync"/>
/// — the same shared store, never a second one — and parses it with
/// <see cref="JsonDocument"/> rather than a typed contract, exactly the
/// same type-erasure boundary <see cref="CalculationTemplateRegistry"/>
/// already crosses for execution itself.
/// </summary>
public static class CalculationRecordReader
{
    /// <summary>Every <c>"calculatedBy"</c>-linked record for <paramref name="calculationObjectId"/>, oldest first.</summary>
    public static async Task<IReadOnlyList<CalculationRecordSnapshot>> GetResultHistoryAsync(
        EngineeringDomainContext context, Guid calculationObjectId, CancellationToken cancellationToken = default)
    {
        var relationships = await context.RelationshipRepository.GetOutgoingAsync(calculationObjectId, cancellationToken).ConfigureAwait(false);

        var recordLinks = relationships
            .Where(r => string.Equals(r.RelationshipKind, CalculationTemplateRegistry.CalculatedByRelationshipKind, StringComparison.Ordinal))
            .OrderBy(r => r.CreatedAt)
            .ToList();

        var snapshots = new List<CalculationRecordSnapshot>();
        foreach (var link in recordLinks)
        {
            if (await ReadAsync(context, link.TargetId, cancellationToken).ConfigureAwait(false) is { } snapshot)
                snapshots.Add(snapshot);
        }

        return snapshots;
    }

    /// <summary>The most recent <c>"calculatedBy"</c>-linked record for <paramref name="calculationObjectId"/>, or <see langword="null"/> if it has never been executed.</summary>
    public static async Task<CalculationRecordSnapshot?> GetLatestAsync(
        EngineeringDomainContext context, Guid calculationObjectId, CancellationToken cancellationToken = default)
    {
        var history = await GetResultHistoryAsync(context, calculationObjectId, cancellationToken).ConfigureAwait(false);
        return history.Count > 0 ? history[^1] : null;
    }

    private static async Task<CalculationRecordSnapshot?> ReadAsync(EngineeringDomainContext context, Guid recordId, CancellationToken cancellationToken)
    {
        var revisions = await context.Store.GetRevisionHistoryAsync(recordId, cancellationToken).ConfigureAwait(false);
        if (revisions.Count == 0)
            return null;

        using var document = JsonDocument.Parse(revisions[^1].Content);
        var root = document.RootElement;

        var calculationId = root.GetProperty("CalculationId").GetString() ?? string.Empty;
        var executedAt = root.GetProperty("ExecutedAt").GetDateTimeOffset();
        var executedBy = root.GetProperty("ExecutedByPrincipalId").GetString() ?? "unknown";
        var outcome = (CalculationValidationOutcome)root.GetProperty("Validation").GetProperty("Outcome").GetInt32();
        var resultDisplay = FormatValue(root.GetProperty("Result"));

        var intermediates = new List<(string Name, string Display)>();
        foreach (var intermediate in root.GetProperty("IntermediateResults").EnumerateArray())
            intermediates.Add((intermediate.GetProperty("Name").GetString() ?? string.Empty, FormatValue(intermediate.GetProperty("Value"))));

        var referencedMaterialIds = root.GetProperty("ReferencedMaterialIds").EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList();

        return new CalculationRecordSnapshot(recordId, calculationId, executedAt, executedBy, outcome, resultDisplay, intermediates, referencedMaterialIds);
    }

    /// <summary>
    /// Formats a generically-parsed JSON value for display — a
    /// <c>{"Value":..,"Unit":{"Symbol":".."}}</c> shape (a boxed
    /// <c>Quantity&lt;TDimension&gt;</c>) becomes <c>"&lt;value&gt; &lt;symbol&gt;"</c>;
    /// every other shape falls back to its own raw JSON text.
    /// </summary>
    private static string FormatValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("Value", out var valueProperty) &&
            element.TryGetProperty("Unit", out var unitProperty) &&
            unitProperty.TryGetProperty("Symbol", out var symbolProperty))
        {
            return $"{valueProperty.GetRawText()} {symbolProperty.GetString()}";
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            _ => element.GetRawText(),
        };
    }
}

/// <summary>
/// One generically-parsed <see cref="CalculationRecord{TResult}"/>, read
/// back for display without its own <c>TResult</c> type — see
/// <see cref="CalculationRecordReader"/>.
/// </summary>
/// <param name="RecordId">The record's own document Id.</param>
/// <param name="CalculationId">The Template that produced it.</param>
/// <param name="ExecutedAt">When it was executed.</param>
/// <param name="ExecutedByPrincipalId">Who executed it.</param>
/// <param name="Outcome">Its own validation outcome.</param>
/// <param name="ResultDisplay">Its own result, formatted for display.</param>
/// <param name="IntermediateResults">Every named intermediate result, formatted for display.</param>
/// <param name="ReferencedMaterialIds">Every material Id referenced during this execution.</param>
public sealed record CalculationRecordSnapshot(
    Guid RecordId,
    string CalculationId,
    DateTimeOffset ExecutedAt,
    string ExecutedByPrincipalId,
    CalculationValidationOutcome Outcome,
    string ResultDisplay,
    IReadOnlyList<(string Name, string Display)> IntermediateResults,
    IReadOnlyList<string> ReferencedMaterialIds);
