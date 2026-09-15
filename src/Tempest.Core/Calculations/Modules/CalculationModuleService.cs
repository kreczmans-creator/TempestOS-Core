using System.Globalization;
using System.Reflection;
using Tempest.Core.Bearings;
using Tempest.Core.Fasteners;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>Why a governed module run did not happen.</summary>
public enum CalculationModuleRefusal
{
    /// <summary>The run happened.</summary>
    None,

    /// <summary>No product calculation has the requested id.</summary>
    ModuleNotFound,

    /// <summary>A picked reference record does not exist.</summary>
    RecordNotFound,

    /// <summary>A picked reference record is not Released.</summary>
    RecordNotReleased,

    /// <summary>A picked record lacks a property the calculation takes from it, or carries it in the wrong dimension.</summary>
    RecordIncomplete,

    /// <summary>What was entered could not be read into the input record; the problems name each input.</summary>
    InputIncomplete,

    /// <summary>The definition rejected the input as malformed, in its own words.</summary>
    InputInvalid,
}

/// <summary>A request to run one module with pinned inputs: the module id and every field, references by record id.</summary>
public sealed record CalculationModuleRequest(string CalculationId, IReadOnlyList<CalculationFormField> Fields);

/// <summary>What a governed module run produced: the run, or the refusal and its reason.</summary>
public sealed record CalculationModuleOutcome(
    CalculationModuleRefusal Refusal,
    string? Reason,
    IReadOnlyList<CalculationFormProblem> Problems,
    IReadOnlyList<ReferenceFill> Fills,
    CalculationModuleRun? Run)
{
    /// <summary>Whether a record was written.</summary>
    public bool WasPerformed => Refusal == CalculationModuleRefusal.None && Run is not null;
}

/// <summary>
/// The governed entry point of every calculation module (`WP 21.7C`):
/// give it a module id and the inputs, references by released-record id,
/// and it resolves each record, fills what the calculation takes from it,
/// builds the input record, runs it through the engine and hands back the
/// identical record a form-driven run would have written.
/// </summary>
/// <remarks>
/// <para>
/// <b>The same shape as <see cref="GovernedBracketCheckService"/>.</b> A
/// caller without a form (a test, a report, a future batch) does not touch
/// a definition or the engine; it states what it wants calculated and on
/// which released records, and gets a <see cref="CalculationModuleOutcome"/>
/// whose refusals are the same four the bracket service has, plus the two
/// a typed input can earn: incomplete and invalid. The Desktop surface's
/// own workbench stands on this class, so a surface run and a service run
/// are one code path.
/// </para>
/// <para>
/// <b>Materials, fasteners and bearings are never typed.</b> A reference
/// input names its library; an input sourced from that record is filled
/// by the library's reader (<see cref="MaterialPropertyReader"/>,
/// <see cref="FastenerPropertyReader"/>, <see cref="BearingPropertyReader"/>)
/// and the record's pin goes onto the input. A value typed for a sourced
/// input is ignored in favour of the record's: the record is the source.
/// </para>
/// </remarks>
public sealed class CalculationModuleService
{
    private readonly IMaterialCatalog _materials;
    private readonly IFastenerCatalog _fasteners;
    private readonly IBearingCatalog _bearings;
    private readonly ICalculationEngine _engine;

    /// <summary>Initialises a new instance of the <see cref="CalculationModuleService"/> class.</summary>
    public CalculationModuleService(IMaterialCatalog materials, IFastenerCatalog fasteners, IBearingCatalog bearings, ICalculationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(fasteners);
        ArgumentNullException.ThrowIfNull(bearings);
        ArgumentNullException.ThrowIfNull(engine);

        _materials = materials;
        _fasteners = fasteners;
        _bearings = bearings;
        _engine = engine;

        try
        {
            ProductCalculationCatalogue.RegisterAll(engine);
        }
        catch (DuplicateCalculationException)
        {
            // Already registered, by the host or an earlier instance: the
            // engine holds the same immutable types this would have added.
        }
    }

    /// <summary>The released records of <paramref name="library"/>, by designation, as a picker offers them.</summary>
    public async Task<IReadOnlyList<ReleasedRecordOption>> ListReleasedAsync(ReferenceLibrary library, CancellationToken cancellationToken = default)
    {
        switch (library)
        {
            case ReferenceLibrary.Materials:
            {
                var records = await _materials.ListAsync(cancellationToken).ConfigureAwait(false);
                return records
                    .Where(r => r.ValidationState == ReferenceValidationState.Released)
                    .OrderBy(r => r.Definition.Designation ?? r.Definition.Name, StringComparer.Ordinal)
                    .Select(r => new ReleasedRecordOption(library, r.Id, Label(r.Definition.Designation, r.Definition.Name), ReferencePin.For(_materials.LibraryName, r)))
                    .ToList();
            }

            case ReferenceLibrary.Fasteners:
            {
                var records = await _fasteners.ListAsync(cancellationToken).ConfigureAwait(false);
                return records
                    .Where(r => r.ValidationState == ReferenceValidationState.Released)
                    .OrderBy(r => r.Definition.Designation, StringComparer.Ordinal)
                    .Select(r => new ReleasedRecordOption(
                        library, r.Id,
                        r.Definition.Mechanical.PropertyClass is { } propertyClass ? $"{r.Definition.Designation} — class {propertyClass}" : $"{r.Definition.Designation} — geometry only",
                        ReferencePin.For(_fasteners.LibraryName, r)))
                    .ToList();
            }

            case ReferenceLibrary.Bearings:
            {
                var records = await _bearings.ListAsync(cancellationToken).ConfigureAwait(false);
                return records
                    .Where(r => r.ValidationState == ReferenceValidationState.Released)
                    .OrderBy(r => r.Definition.Identity.ManufacturerPartNumber, StringComparer.Ordinal)
                    .Select(r => new ReleasedRecordOption(
                        library, r.Id,
                        $"{r.Definition.Identity.Manufacturer} {r.Definition.Identity.ManufacturerPartNumber} — {r.Definition.Identity.FamilyDesignation ?? r.Definition.Family.ToString()}",
                        ReferencePin.For(_bearings.LibraryName, r)))
                    .ToList();
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(library), library, "Unknown reference library.");
        }
    }

    /// <summary>
    /// Reads every input of <paramref name="module"/> sourced from the
    /// reference input <paramref name="referenceInputName"/> out of the
    /// released record <paramref name="recordId"/>, each expressed in the
    /// input's own default unit, and names any the record cannot supply.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="referenceInputName"/> is not a reference input of the module.</exception>
    public async Task<ReferenceFill> FillFromRecordAsync(CalculationModuleDescriptor module, string referenceInputName, string recordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceInputName);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var reference = module.Inputs.FirstOrDefault(i => i.Kind == CalculationInputKind.Reference && i.Name == referenceInputName)
            ?? throw new ArgumentException($"{module.Id} has no reference input '{referenceInputName}'.", nameof(referenceInputName));
        var library = reference.Library ?? throw new ArgumentException($"{module.Id}.{referenceInputName} names no library.", nameof(referenceInputName));

        var fields = new List<CalculationFormField>();
        var problems = new List<CalculationFormProblem>();
        ReferencePin? pin = null;
        string? firstRefusal = null;

        foreach (var input in module.Inputs.Where(i => i.SourceInputName == referenceInputName))
        {
            switch (input.Kind)
            {
                case CalculationInputKind.Quantity:
                {
                    var reading = await ReadQuantityAsync(library, input.DimensionName!, recordId, input.SourcePropertyName!, cancellationToken).ConfigureAwait(false);
                    pin ??= reading.Pin;
                    if (reading.Value is null)
                    {
                        problems.Add(new CalculationFormProblem(input.Name, input.Label, reading.Reason ?? "could not be read from the record"));
                        firstRefusal ??= reading.Refusal.ToString();
                        continue;
                    }

                    var value = CalculationInputUnits.ValueIn(input.DimensionName!, reading.Value, input.DefaultUnitSymbol!);
                    fields.Add(new CalculationFormField(input.Name, value.ToString("R", CultureInfo.InvariantCulture), input.DefaultUnitSymbol));
                    break;
                }

                case CalculationInputKind.Text:
                case CalculationInputKind.Choice:
                {
                    var reading = await ReadTextAsync(library, recordId, input.SourcePropertyName!, cancellationToken).ConfigureAwait(false);
                    pin ??= reading.Pin;
                    if (reading.Value is null)
                    {
                        problems.Add(new CalculationFormProblem(input.Name, input.Label, reading.Reason ?? "could not be read from the record"));
                        firstRefusal ??= reading.Refusal.ToString();
                        continue;
                    }

                    fields.Add(input.Kind == CalculationInputKind.Text
                        ? new CalculationFormField(input.Name, reading.Value)
                        : new CalculationFormField(input.Name, Choice: reading.Value));
                    break;
                }
            }
        }

        var option = await DescribeAsync(library, recordId, cancellationToken).ConfigureAwait(false);
        return new ReferenceFill(referenceInputName, option, fields, problems);
    }

    /// <summary>
    /// Runs <paramref name="request"/>: every reference resolved to a released
    /// record, every sourced input filled from it, the input built, the
    /// module executed, the record presented. Refuses, with the reason, at
    /// the first thing that stops it.
    /// </summary>
    public async Task<CalculationModuleOutcome> RunAsync(CalculationModuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var module = CalculationModuleDescriptors.For(request.CalculationId);
        if (module is null)
            return new CalculationModuleOutcome(CalculationModuleRefusal.ModuleNotFound, $"No product calculation has the id '{request.CalculationId}'.", [], [], null);

        var fields = request.Fields.ToDictionary(f => f.Name, StringComparer.Ordinal);
        var pins = new Dictionary<string, ReferencePin>(StringComparer.Ordinal);
        var fills = new List<ReferenceFill>();

        foreach (var reference in module.Inputs.Where(i => i.Kind == CalculationInputKind.Reference))
        {
            var recordId = fields.TryGetValue(reference.Name, out var field) ? field.RecordId : null;

            if (string.IsNullOrWhiteSpace(recordId))
                continue; // Absent: BuildInput refuses a required one, leaves an optional one empty.

            var found = await FindAsync(reference.Library!.Value, recordId, cancellationToken).ConfigureAwait(false);
            if (found.Refusal != ReferencePropertyRefusal.None)
            {
                var refusal = found.Refusal == ReferencePropertyRefusal.RecordNotFound ? CalculationModuleRefusal.RecordNotFound : CalculationModuleRefusal.RecordNotReleased;
                return new CalculationModuleOutcome(refusal, found.Reason, [], fills, null);
            }

            pins[reference.Name] = found.Pin!;

            var fill = await FillFromRecordAsync(module, reference.Name, recordId, cancellationToken).ConfigureAwait(false);
            fills.Add(fill);

            if (fill.Problems.Count > 0)
            {
                return new CalculationModuleOutcome(
                    CalculationModuleRefusal.RecordIncomplete,
                    $"The record picked for {reference.Label} cannot supply every input read from it: {string.Join(" ", fill.Problems.Select(p => $"{p.Label}: {p.Problem}."))}",
                    fill.Problems, fills, null);
            }

            // The record is the source: its values replace anything typed for a sourced input.
            foreach (var filled in fill.Fields)
                fields[filled.Name] = filled;
        }

        var build = CalculationModuleForm.BuildInput(module, fields.Values.ToList(), pins);
        if (!build.Succeeded)
            return new CalculationModuleOutcome(CalculationModuleRefusal.InputIncomplete, $"Some inputs could not be read: {string.Join(" ", build.Problems.Select(p => $"{p.Label}: {p.Problem}."))}", build.Problems, fills, null);

        try
        {
            var run = await ExecuteAsync(module, build.Input!, cancellationToken).ConfigureAwait(false);
            return new CalculationModuleOutcome(CalculationModuleRefusal.None, null, [], fills, run);
        }
        catch (CalculationInputInvalidException rejected)
        {
            return new CalculationModuleOutcome(CalculationModuleRefusal.InputInvalid, rejected.Message, [], fills, null);
        }
    }

    /// <summary>Executes an already-built input of <paramref name="module"/> through the engine and presents the record.</summary>
    /// <exception cref="CalculationInputInvalidException">The definition rejected the input.</exception>
    public async Task<CalculationModuleRun> ExecuteAsync(CalculationModuleDescriptor module, object input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(input);

        if (!module.InputType.IsInstanceOfType(input))
            throw new ArgumentException($"The input is a {input.GetType().Name}; {module.Id} takes a {module.InputType.Name}.", nameof(input));

        return await InvokeTypedAsync<CalculationModuleRun>(nameof(ExecuteTypedAsync), module, [module, input, cancellationToken]).ConfigureAwait(false);
    }

    /// <summary>Presents the record <paramref name="recordId"/> of <paramref name="module"/>, or <see langword="null"/> where no such record exists.</summary>
    /// <exception cref="CalculationReadbackException">The record is not of the module's result type.</exception>
    public Task<CalculationModuleRun?> PresentAsync(CalculationModuleDescriptor module, Guid recordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        return InvokeTypedAsync<CalculationModuleRun?>(nameof(PresentTypedAsync), module, [module, recordId, cancellationToken]);
    }

    private async Task<CalculationModuleRun> ExecuteTypedAsync<TInput, TResult>(CalculationModuleDescriptor module, TInput input, CancellationToken cancellationToken)
    {
        var record = await _engine.ExecuteAsync<TInput, TResult>(module.Id, input, cancellationToken).ConfigureAwait(false);
        return CalculationModuleForm.Present(module, record);
    }

    private async Task<CalculationModuleRun?> PresentTypedAsync<TInput, TResult>(CalculationModuleDescriptor module, Guid recordId, CancellationToken cancellationToken)
    {
        var record = await _engine.FindRecordAsync<TResult>(recordId, cancellationToken).ConfigureAwait(false);
        return record is null ? null : CalculationModuleForm.Present(module, record);
    }

    private async Task<T> InvokeTypedAsync<T>(string method, CalculationModuleDescriptor module, object?[] arguments)
    {
        var generic = typeof(CalculationModuleService)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(module.InputType, module.ResultType);

        try
        {
            return await ((Task<T>)generic.Invoke(this, arguments)!).ConfigureAwait(false);
        }
        catch (TargetInvocationException wrapped) when (wrapped.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
            throw;
        }
    }

    // ---- Reference records ----

    private async Task<ReferenceTextReading> FindAsync(ReferenceLibrary library, string recordId, CancellationToken cancellationToken)
    {
        // Existence and release only: the designation read is the cheapest fact every library has.
        var reading = library switch
        {
            ReferenceLibrary.Materials => await MaterialExistsAsync(recordId, cancellationToken).ConfigureAwait(false),
            ReferenceLibrary.Fasteners => await FastenerPropertyReader.ReadTextAsync(_fasteners, recordId, nameof(FastenerDefinition.Designation), cancellationToken).ConfigureAwait(false),
            ReferenceLibrary.Bearings => await BearingPropertyReader.ReadTextAsync(_bearings, recordId, nameof(BearingIdentity.Designation), cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(library)),
        };

        return reading;
    }

    private async Task<ReferenceTextReading> MaterialExistsAsync(string recordId, CancellationToken cancellationToken)
    {
        var record = await _materials.FindAsync(recordId, cancellationToken).ConfigureAwait(false);
        if (record is null)
            return new ReferenceTextReading(ReferencePropertyRefusal.RecordNotFound, $"No record '{recordId}' is registered in {_materials.LibraryName}.", null, null);

        var pin = ReferencePin.For(_materials.LibraryName, record);
        return record.ValidationState == ReferenceValidationState.Released
            ? new ReferenceTextReading(ReferencePropertyRefusal.None, null, pin, record.Definition.Name)
            : new ReferenceTextReading(
                ReferencePropertyRefusal.RecordNotReleased,
                $"{_materials.LibraryName} record '{record.Id}' is {record.ValidationState}, not Released. Engineering work may not rely on reference data nobody has verified against its source.",
                pin, null);
    }

    private async Task<ReferencePropertyReading<Dimensionless>> ReadQuantityRawAsync(ReferenceLibrary library, string dimensionName, string recordId, string propertyName, CancellationToken cancellationToken)
    {
        var read = typeof(CalculationModuleService)
            .GetMethod(nameof(ReadQuantityTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(CalculationInputUnits.DimensionTypeOf(dimensionName));

        return await ((Task<ReferencePropertyReading<Dimensionless>>)read.Invoke(this, [library, recordId, propertyName, cancellationToken])!).ConfigureAwait(false);
    }

    private sealed record QuantityReading(ReferencePropertyRefusal Refusal, string? Reason, ReferencePin? Pin, object? Value);

    private async Task<QuantityReading> ReadQuantityAsync(ReferenceLibrary library, string dimensionName, string recordId, string propertyName, CancellationToken cancellationToken)
    {
        var read = typeof(CalculationModuleService)
            .GetMethod(nameof(ReadQuantityTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(CalculationInputUnits.DimensionTypeOf(dimensionName));

        return await ((Task<QuantityReading>)read.Invoke(this, [library, recordId, propertyName, cancellationToken])!).ConfigureAwait(false);
    }

    private async Task<QuantityReading> ReadQuantityTypedAsync<TDimension>(ReferenceLibrary library, string recordId, string propertyName, CancellationToken cancellationToken)
        where TDimension : IDimension
    {
        var reading = library switch
        {
            ReferenceLibrary.Materials => await MaterialPropertyReader.ReadAsync<TDimension>(_materials, recordId, propertyName, cancellationToken).ConfigureAwait(false),
            ReferenceLibrary.Fasteners => await FastenerPropertyReader.ReadAsync<TDimension>(_fasteners, recordId, propertyName, cancellationToken).ConfigureAwait(false),
            ReferenceLibrary.Bearings => await BearingPropertyReader.ReadAsync<TDimension>(_bearings, recordId, propertyName, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(library)),
        };

        return new QuantityReading(reading.Refusal, reading.Reason, reading.Pin, reading.Succeeded ? reading.Value : null);
    }

    private Task<ReferenceTextReading> ReadTextAsync(ReferenceLibrary library, string recordId, string propertyName, CancellationToken cancellationToken) =>
        library switch
        {
            ReferenceLibrary.Fasteners => FastenerPropertyReader.ReadTextAsync(_fasteners, recordId, propertyName, cancellationToken),
            ReferenceLibrary.Bearings => BearingPropertyReader.ReadTextAsync(_bearings, recordId, propertyName, cancellationToken),
            ReferenceLibrary.Materials => Task.FromResult(new ReferenceTextReading(ReferencePropertyRefusal.RequiredPropertyMissing, $"A material record supplies no text property '{propertyName}'.", null, null)),
            _ => throw new ArgumentOutOfRangeException(nameof(library)),
        };

    private async Task<ReleasedRecordOption> DescribeAsync(ReferenceLibrary library, string recordId, CancellationToken cancellationToken)
    {
        var offered = await ListReleasedAsync(library, cancellationToken).ConfigureAwait(false);
        return offered.FirstOrDefault(o => string.Equals(o.RecordId, recordId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No released {library} record '{recordId}' is registered.", nameof(recordId));
    }

    private static string Label(string? designation, string name) =>
        designation is { } d && !string.Equals(d, name, StringComparison.Ordinal) ? $"{d} — {name}" : name;
}
