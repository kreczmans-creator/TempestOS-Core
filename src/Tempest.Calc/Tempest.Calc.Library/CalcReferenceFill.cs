using System.Globalization;
using System.Reflection;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Sheets;
using Tempest.Core.Bearings;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Fasteners;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Calc.Library;

/// <summary>One released reference record a worksheet input can be filled from.</summary>
public sealed record CalcReferenceOption(string Library, string RecordId, string Label, string Pin);

/// <summary>What one fill produced: the input values with their pinned provenance, and the inputs the record could not supply.</summary>
public sealed record CalcReferenceFillResult(IReadOnlyList<CalcInputValue> Values, IReadOnlyList<CalcNotice> Problems, string? Pin, string? Label)
{
    public bool Succeeded => Values.Count > 0 && Problems.Count == 0;
}

/// <summary>
/// Fills a template's reference-declared inputs (<see cref="CalcInputSpec.ReferenceLibrary"/>,
/// <see cref="CalcInputSpec.ReferenceProperty"/>) from a released material,
/// fastener or bearing record, with <see cref="CalcProvenance.FromReference"/>
/// carrying the record's pin (ADR-0167 §3; the legacy descriptor's
/// <c>Library</c>/<c>SourceProperty</c> pattern carried over). Only released
/// records are offered or read; a Checked or Draft record refuses, as the
/// legacy readers did.
/// </summary>
public sealed class CalcReferenceFill
{
    public const string Materials = "Materials";
    public const string Fasteners = "Fasteners";
    public const string Bearings = "Bearings";

    public static IReadOnlyList<string> Libraries { get; } = [Materials, Fasteners, Bearings];

    private readonly IMaterialCatalog _materials;
    private readonly IFastenerCatalog _fasteners;
    private readonly IBearingCatalog _bearings;

    public CalcReferenceFill(IMaterialCatalog materials, IFastenerCatalog fasteners, IBearingCatalog bearings)
    {
        _materials = materials ?? throw new ArgumentNullException(nameof(materials));
        _fasteners = fasteners ?? throw new ArgumentNullException(nameof(fasteners));
        _bearings = bearings ?? throw new ArgumentNullException(nameof(bearings));
    }

    /// <summary>The libraries a template draws on, in the order its inputs name them.</summary>
    public static IReadOnlyList<string> LibrariesOf(CalcTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return template.Inputs.Where(i => i.IsReferenceFillable).Select(i => i.ReferenceLibrary!).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>"Materials:hub" names the Materials library for the hub slot: the catalogue before the colon, the slot after it.</summary>
    public static string CatalogueOf(string library) => (library ?? string.Empty).Split(':')[0];

    /// <summary>The label a picker shows for a library or a slot: "Material record", "Hub material record".</summary>
    public static string PickerLabel(string library)
    {
        var parts = (library ?? string.Empty).Split(':');
        var noun = parts[0] switch { Materials => "material", Fasteners => "fastener", Bearings => "bearing", var other => other.ToLowerInvariant() };
        return parts.Length > 1 && parts[1].Length > 0 ? $"{char.ToUpperInvariant(parts[1][0])}{parts[1][1..]} {noun} record" : $"{char.ToUpperInvariant(noun[0])}{noun[1..]} record";
    }

    public async Task<IReadOnlyList<CalcReferenceOption>> ListReleasedAsync(string library, CancellationToken cancellationToken = default)
    {
        switch (CatalogueOf(library))
        {
            case Materials:
            {
                var records = await _materials.ListAsync(cancellationToken).ConfigureAwait(false);
                return records.Where(r => r.ValidationState == ReferenceValidationState.Released)
                    .Select(r => new CalcReferenceOption(library, r.Id, Label(r.Definition.Designation, r.Definition.Name), ReferencePin.For(_materials.LibraryName, r).ToString()))
                    .OrderBy(o => o.Label, StringComparer.Ordinal).ToList();
            }

            case Fasteners:
            {
                var records = await _fasteners.ListAsync(cancellationToken).ConfigureAwait(false);
                return records.Where(r => r.ValidationState == ReferenceValidationState.Released)
                    .Select(r => new CalcReferenceOption(library, r.Id, Label(r.Definition.Designation, r.Definition.Mechanical?.PropertyClass ?? r.Definition.Family.ToString()), ReferencePin.For(_fasteners.LibraryName, r).ToString()))
                    .OrderBy(o => o.Label, StringComparer.Ordinal).ToList();
            }

            case Bearings:
            {
                var records = await _bearings.ListAsync(cancellationToken).ConfigureAwait(false);
                return records.Where(r => r.ValidationState == ReferenceValidationState.Released)
                    .Select(r => new CalcReferenceOption(library, r.Id, Label(r.Definition.Identity.Designation, r.Definition.Family.ToString()), ReferencePin.For(_bearings.LibraryName, r).ToString()))
                    .OrderBy(o => o.Label, StringComparer.Ordinal).ToList();
            }

            default:
                throw new ArgumentException($"Unknown reference library '{library}'.", nameof(library));
        }
    }

    /// <summary>Reads every input of <paramref name="template"/> declared on <paramref name="library"/> from <paramref name="recordId"/>; never throws for a missing, unreleased or incomplete record.</summary>
    public async Task<CalcReferenceFillResult> FillAsync(CalcTemplate template, string library, string recordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var values = new List<CalcInputValue>();
        var problems = new List<CalcNotice>();
        string? pin = null;

        foreach (var spec in template.Inputs.Where(i => i.IsReferenceFillable && string.Equals(i.ReferenceLibrary, library, StringComparison.Ordinal)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (spec.Kind is CalcInputKind.Quantity or CalcInputKind.Number)
            {
                var dimension = spec.ReferenceDimension ?? (spec.Kind == CalcInputKind.Number ? "Dimensionless" : string.Empty);

                if (!CalculationInputUnits.IsKnown(dimension))
                {
                    problems.Add(new CalcNotice("reference." + spec.Symbol, CalcNoticeSeverity.Block, $"{spec.Symbol}: the template names an unknown dimension '{dimension}' for its reference property."));
                    continue;
                }

                var read = typeof(CalcReferenceFill).GetMethod(nameof(ReadQuantityAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(CalculationInputUnits.DimensionTypeOf(dimension));
                var reading = await ((Task<(string? Pin, double? Si, string? Reason)>)read.Invoke(this, [CatalogueOf(library), recordId, spec.ReferenceProperty!, cancellationToken])!).ConfigureAwait(false);
                pin ??= reading.Pin;

                if (reading.Si is not { } si)
                {
                    problems.Add(new CalcNotice("reference." + spec.Symbol, CalcNoticeSeverity.Block, $"{spec.Symbol}: {reading.Reason ?? "the record does not supply it"}"));
                    continue;
                }

                if (spec.Kind == CalcInputKind.Number || spec.DefaultUnit.Length == 0)
                {
                    values.Add(CalcInputValue.Of(spec.Symbol, si, CalcProvenance.FromReference(reading.Pin!)));
                    continue;
                }

                try
                {
                    var unit = UnitCatalog.Parse(spec.DefaultUnit);
                    values.Add(CalcInputValue.Quantity(spec.Symbol, unit.FromSi(si), spec.DefaultUnit, CalcProvenance.FromReference(reading.Pin!)));
                }
                catch (CalcUnitException ex)
                {
                    problems.Add(new CalcNotice("reference." + spec.Symbol, CalcNoticeSeverity.Block, $"{spec.Symbol}: {ex.Message}"));
                }
            }
            else if (spec.Kind is CalcInputKind.Text or CalcInputKind.Choice)
            {
                var reading = await ReadTextAsync(CatalogueOf(library), recordId, spec.ReferenceProperty!, cancellationToken).ConfigureAwait(false);
                pin ??= reading.Pin?.ToString();

                if (!reading.Succeeded)
                {
                    problems.Add(new CalcNotice("reference." + spec.Symbol, CalcNoticeSeverity.Block, $"{spec.Symbol}: {reading.Reason ?? "the record does not supply it"}"));
                    continue;
                }

                var text = CalcSheetTextRenderer.Clean(reading.Value!);

                if (spec.Kind == CalcInputKind.Choice && spec.Choices is { } choices && !choices.Contains(text, StringComparer.Ordinal))
                {
                    problems.Add(new CalcNotice("reference." + spec.Symbol, CalcNoticeSeverity.Block, $"{spec.Symbol}: the record says '{text}', which is not one of {string.Join(", ", choices)}."));
                    continue;
                }

                values.Add(new CalcInputValue(spec.Symbol, Text: text, Provenance: CalcProvenance.FromReference(reading.Pin!.ToString())));
            }
        }

        var label = (await ListReleasedAsync(library, cancellationToken).ConfigureAwait(false)).FirstOrDefault(o => string.Equals(o.RecordId, recordId, StringComparison.Ordinal))?.Label;
        return new CalcReferenceFillResult(values, problems, pin, label);
    }

    private async Task<(string? Pin, double? Si, string? Reason)> ReadQuantityAsync<TDimension>(string library, string recordId, string property, CancellationToken cancellationToken)
        where TDimension : IDimension
    {
        var reading = library switch
        {
            Materials => await MaterialPropertyReader.ReadAsync<TDimension>(_materials, recordId, property, cancellationToken).ConfigureAwait(false),
            Fasteners => await FastenerPropertyReader.ReadAsync<TDimension>(_fasteners, recordId, property, cancellationToken).ConfigureAwait(false),
            Bearings => await BearingPropertyReader.ReadAsync<TDimension>(_bearings, recordId, property, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentException($"Unknown reference library '{library}'.", nameof(library)),
        };

        return (reading.Pin?.ToString(), reading.Succeeded ? reading.Value!.Value.BaseValue : null, reading.Reason);
    }

    private Task<ReferenceTextReading> ReadTextAsync(string library, string recordId, string property, CancellationToken cancellationToken) =>
        library switch
        {
            Fasteners => FastenerPropertyReader.ReadTextAsync(_fasteners, recordId, property, cancellationToken),
            Bearings => BearingPropertyReader.ReadTextAsync(_bearings, recordId, property, cancellationToken),
            Materials => Task.FromResult(new ReferenceTextReading(ReferencePropertyRefusal.RequiredPropertyMissing, $"A material record supplies no text property '{property}'.", null, null)),
            _ => throw new ArgumentException($"Unknown reference library '{library}'.", nameof(library)),
        };

    private static string Label(string? designation, string name) =>
        designation is { Length: > 0 } d && !string.Equals(d, name, StringComparison.Ordinal) ? $"{d} — {name}" : (designation is { Length: > 0 } ? designation : name);
}
