using System.Globalization;
using System.Text.Json;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Core.Calculations;

namespace Tempest.Calc.Library;

/// <summary>One migration pass: what was written, what was already there, what could not be read.</summary>
public sealed record LegacyMigrationReport(int Migrated, int AlreadyMigrated, IReadOnlyList<(Guid RecordId, string Reason)> Failed)
{
    public int Total => Migrated + AlreadyMigrated + Failed.Count;
}

/// <summary>
/// Copies every stored legacy calculation record (<c>Tempest.Core.Calculations</c>,
/// v0.16.0–v0.26.0) into a <see cref="CalcInstance"/> archive snapshot so the
/// run stays readable after the legacy modules are retired (ADR-0167).
/// Nothing is deleted: the record keeps its document; the instance takes the
/// record's own Id, so a second pass writes nothing. The snapshot is a Draft
/// with Manual provenance under the reference <c>legacy/&lt;calculation id&gt;</c>;
/// it has no calculator and is rendered by <see cref="LegacyArchiveSheet"/>.
/// </summary>
public sealed class LegacyCalculationMigration
{
    /// <summary>Records larger than this are reported, not migrated (a 4 MB instance document is the store's own cap).</summary>
    public const int MaxRecordJsonLength = 2_000_000;

    private readonly ICalculationEngine _engine;
    private readonly CalcInstanceStore _store;
    private readonly TimeProvider _time;

    public LegacyCalculationMigration(ICalculationEngine engine, CalcInstanceStore store, TimeProvider? time = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _time = time ?? TimeProvider.System;
    }

    public async Task<LegacyMigrationReport> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var summaries = await _engine.ListRecordsAsync(cancellationToken).ConfigureAwait(false);
        var migrated = 0;
        var already = 0;
        var failed = new List<(Guid, string)>();

        foreach (var summary in summaries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await _store.FindInstanceAsync(summary.Id, cancellationToken).ConfigureAwait(false) is not null)
            {
                already++;
                continue;
            }

            try
            {
                var document = await _engine.FindRecordDocumentAsync(summary.Id, cancellationToken).ConfigureAwait(false);

                if (document is null)
                {
                    failed.Add((summary.Id, "the record document no longer exists"));
                    continue;
                }

                var instance = ToInstance(document, _time.GetUtcNow());
                await _store.SaveInstanceAsync(instance, cancellationToken).ConfigureAwait(false);
                migrated++;
            }
            catch (CalculationException ex)
            {
                failed.Add((summary.Id, ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                failed.Add((summary.Id, ex.Message));
            }
        }

        return new LegacyMigrationReport(migrated, already, failed);
    }

    /// <summary>Maps one record to its archive snapshot; pure, so a test can check the mapping without a store.</summary>
    public static CalcInstance ToInstance(CalculationRecordDocument record, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);

        var inputs = new List<CalcInputValue>();

        if (record.Input is { ValueKind: JsonValueKind.Object } input)
        {
            foreach (var property in input.EnumerateObject())
                inputs.Add(ToInput(property.Name, property.Value));
        }

        var outputs = new List<CalcOutputSnapshot>();

        foreach (var intermediate in record.IntermediateResults)
            outputs.Add(ToOutput(intermediate.Name, intermediate.Value is JsonElement e ? e : JsonSerializer.SerializeToElement(intermediate.Value), "recorded intermediate"));

        if (record.Result.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in record.Result.EnumerateObject())
                outputs.Add(ToOutput(property.Name, property.Value, "result"));
        }

        var checks = record.Validation.ConstraintChecks
            .Select(c => new CalcCheckSnapshot(c.Description, c.Detail ?? string.Empty, string.Empty, c.IsSatisfied ? 0.0 : 1.0, c.IsSatisfied))
            .ToList();

        var notes = new List<string>
        {
            $"Migrated from calculation record {record.Id:N} ('{record.CalculationId}') executed {record.ExecutedAt:yyyy-MM-dd HH:mm} UTC by {record.ExecutedByPrincipalId}, document revision {record.RevisionNumber}; validation {record.Validation.Outcome}.",
        };
        notes.AddRange(record.Assumptions.Select(a => a.Justification is { Length: > 0 } j ? $"Assumption: {a.Description} ({j})" : $"Assumption: {a.Description}"));

        if (record.PredecessorRecordId is { } predecessor)
            notes.Add($"Re-run of record {predecessor:N}.");

        if (record.ReferencedMaterialIds.Count > 0)
            notes.Add("Referenced materials: " + string.Join(", ", record.ReferencedMaterialIds) + ".");

        var name = CalcSheetTextRenderer.Clean($"{record.CalculationId} {record.ExecutedAt:yyyy-MM-dd HH:mm}");

        return new CalcInstance(
            record.Id, CalcInstance.LegacyReferencePrefix + record.CalculationId + "@" + record.RevisionNumber.ToString(CultureInfo.InvariantCulture), "legacy",
            null, name, "A", inputs, outputs, checks, [], [], CalcInstanceStatus.Draft, null, null,
            record.ExecutedByPrincipalId, record.ExecutedAt, now, Notes: notes);
    }

    private static CalcInputValue ToInput(string name, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return CalcInputValue.Of(name, value.GetDouble(), CalcProvenance.Manual);
            case JsonValueKind.True:
            case JsonValueKind.False:
                return CalcInputValue.Switch(name, value.GetBoolean(), CalcProvenance.Manual);
            case JsonValueKind.String:
                return new CalcInputValue(name, Text: Trim(value.GetString() ?? string.Empty), Provenance: CalcProvenance.Manual);
            case JsonValueKind.Object when TryQuantity(value, out var number, out var unit):
                return unit.Length > 0 ? CalcInputValue.Quantity(name, number, unit, CalcProvenance.Manual) : CalcInputValue.Of(name, number, CalcProvenance.Manual);
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return new CalcInputValue(name, Text: string.Empty, Provenance: CalcProvenance.Manual);
            default:
                return new CalcInputValue(name, Text: Trim(value.GetRawText()), Provenance: CalcProvenance.Manual);
        }
    }

    private static CalcOutputSnapshot ToOutput(string name, JsonElement value, string description)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
            {
                var n = value.GetDouble();
                return new CalcOutputSnapshot(name, description, CalcValue.FormatNumber(n), n, null);
            }

            case JsonValueKind.Object when TryQuantity(value, out var number, out var unit):
                return new CalcOutputSnapshot(name, description, unit.Length > 0 ? $"{CalcValue.FormatNumber(number)} {unit}" : CalcValue.FormatNumber(number), number, unit.Length > 0 ? unit : null);
            case JsonValueKind.True:
            case JsonValueKind.False:
                return new CalcOutputSnapshot(name, description, value.GetBoolean() ? "yes" : "no", null, null);
            case JsonValueKind.String:
                return new CalcOutputSnapshot(name, description, Trim(value.GetString() ?? string.Empty), null, null);
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return new CalcOutputSnapshot(name, description, "—", null, null);
            default:
                return new CalcOutputSnapshot(name, description, Trim(value.GetRawText()), null, null);
        }
    }

    /// <summary>A Core <c>Quantity&lt;T&gt;</c> serialises as <c>{"Value": n, "Unit": {"Symbol": "kN", …}}</c>; the unit symbol is kept as text even when the worksheet engine spells it differently.</summary>
    private static bool TryQuantity(JsonElement value, out double number, out string unit)
    {
        number = 0;
        unit = string.Empty;

        if (!value.TryGetProperty("Value", out var v) || v.ValueKind != JsonValueKind.Number)
            return false;

        number = v.GetDouble();

        if (value.TryGetProperty("Unit", out var u))
        {
            if (u.ValueKind == JsonValueKind.String)
                unit = Trim(u.GetString() ?? string.Empty);
            else if (u.ValueKind == JsonValueKind.Object && u.TryGetProperty("Symbol", out var s) && s.ValueKind == JsonValueKind.String)
                unit = Trim(s.GetString() ?? string.Empty);
        }

        return true;
    }

    private static string Trim(string s)
    {
        var clean = CalcSheetTextRenderer.Clean(s);
        return clean.Length > CalcInputValidator.MaxTextLength ? clean[..CalcInputValidator.MaxTextLength] : clean;
    }
}
