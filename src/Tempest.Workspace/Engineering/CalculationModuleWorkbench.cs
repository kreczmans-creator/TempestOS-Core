using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Workspace.Engineering;

/// <summary>One category of the calculator catalogue and the modules under it.</summary>
public sealed record CalculationModuleGroup(string Category, IReadOnlyList<CalculationModuleDescriptor> Modules);

/// <summary>A released material record, as the calculator's material picker offers it.</summary>
/// <param name="RecordId">The record's registered id.</param>
/// <param name="Label">What the picker shows: designation and name.</param>
/// <param name="Pin">The exact record and revision a calculation built on it will cite.</param>
public sealed record ReleasedMaterialOption(string RecordId, string Label, ReferencePin Pin)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>What the engineer typed or chose for one input, by the input's own name.</summary>
/// <param name="Name">The input record's property name, as the descriptor states it.</param>
/// <param name="Text">The number or text typed, for a quantity, number or text input.</param>
/// <param name="UnitSymbol">The unit chosen, for a quantity.</param>
/// <param name="Choice">The member chosen, for a choice.</param>
/// <param name="Flag">The state, for a yes-or-no input.</param>
/// <param name="Rows">The rows typed, one per line, for a list.</param>
public sealed record CalculationFormField(
    string Name,
    string? Text = null,
    string? UnitSymbol = null,
    string? Choice = null,
    bool Flag = false,
    IReadOnlyList<string>? Rows = null);

/// <summary>One thing wrong with what was entered, named by input so a form can point at it.</summary>
public sealed record CalculationFormProblem(string InputName, string Label, string Problem)
{
    /// <inheritdoc />
    public override string ToString() => $"{Label}: {Problem}";
}

/// <summary>The fields a picked material fills, and any property it could not supply.</summary>
public sealed record MaterialFill(ReleasedMaterialOption Material, IReadOnlyList<CalculationFormField> Fields, IReadOnlyList<CalculationFormProblem> Problems);

/// <summary>The input record built from a form, or the problems that stopped it.</summary>
public sealed record CalculationInputBuild(object? Input, IReadOnlyList<CalculationFormProblem> Problems)
{
    /// <summary>Whether an input was built.</summary>
    public bool Succeeded => Input is not null && Problems.Count == 0;
}

/// <summary>One line of a result or of the working: a label and its display text.</summary>
public sealed record CalculationRunRow(string Label, string Display);

/// <summary>One constraint the definition checked, and whether it held.</summary>
public sealed record CalculationCheckRow(string Description, bool IsSatisfied, string? Detail);

/// <summary>One execution of a module, presented for a surface to render.</summary>
/// <param name="Module">The module that ran.</param>
/// <param name="RecordId">The durable record the engine wrote.</param>
/// <param name="ExecutedAt">When.</param>
/// <param name="OutcomeSummary">The engineering finding in words: meets its criteria, does not, outside the method, or simply computed.</param>
/// <param name="IsRefused">Whether the method refused the input (see <see cref="EngineeringCheckOutcome.OutsideMethodLimits"/>).</param>
/// <param name="RefusalReason">Why, when refused.</param>
/// <param name="Results">Every result figure, in the result record's own order.</param>
/// <param name="Working">Every intermediate the definition recorded, in order.</param>
/// <param name="Checks">Every constraint check the definition recorded, in order.</param>
/// <param name="ValidationOutcome">The engine's own validation outcome for the record.</param>
/// <param name="ReferencedMaterialIds">The material records the calculation cited.</param>
public sealed record CalculationModuleRun(
    CalculationModuleDescriptor Module,
    Guid RecordId,
    DateTimeOffset ExecutedAt,
    string OutcomeSummary,
    bool IsRefused,
    string? RefusalReason,
    IReadOnlyList<CalculationRunRow> Results,
    IReadOnlyList<CalculationRunRow> Working,
    IReadOnlyList<CalculationCheckRow> Checks,
    string ValidationOutcome,
    IReadOnlyList<string> ReferencedMaterialIds);

/// <summary>What pressing Calculate produced: a run, form problems, or the definition's own rejection of the input.</summary>
public sealed record CalculationAttempt(CalculationModuleRun? Run, IReadOnlyList<CalculationFormProblem> Problems, string? Rejection)
{
    /// <summary>Whether a run was recorded.</summary>
    public bool Succeeded => Run is not null;
}

/// <summary>
/// The Engineering Calculators' own read-and-run model (`WP 21.7B`): the
/// catalogue by category, the released materials a calculation may stand
/// on, the input record built from what a generated form collected, the
/// execution through the engine, and the result presented as rows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Generic on purpose.</b> Nothing here knows any one module. The
/// catalogue, the form and the result presentation all come from
/// <see cref="CalculationModuleDescriptors"/> and from the input and
/// result records themselves, read by reflection, so a module registered
/// tomorrow is calculable from this surface with no code. That is the
/// Product Owner's ask: pick a calculation, fill a form, read the result
/// and its working, never touch code.
/// </para>
/// <para>
/// <b>It decides nothing of engineering.</b> Limits, refusals and
/// criteria are the definition's; materials come from released records
/// through <see cref="MaterialPropertyReader"/> and are never typed; the
/// record the engine writes is the record a reviewer sees. This class
/// parses what was typed, names what is wrong with it, and renders what
/// came back.
/// </para>
/// </remarks>
public sealed class CalculationModuleWorkbench
{
    private readonly IMaterialCatalog _materials;
    private readonly ICalculationEngine _engine;

    /// <summary>Initialises a new instance of the <see cref="CalculationModuleWorkbench"/> class.</summary>
    public CalculationModuleWorkbench(IMaterialCatalog materials, ICalculationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(engine);

        _materials = materials;
        _engine = engine;
    }

    /// <summary>Every product calculation, grouped by category in first-seen order, each group in registration order.</summary>
    public static IReadOnlyList<CalculationModuleGroup> Catalogue() =>
        CalculationModuleDescriptors.All
            .GroupBy(d => d.Category, StringComparer.Ordinal)
            .Select(g => new CalculationModuleGroup(g.Key, g.ToList()))
            .ToList();

    /// <summary>The released material records, by designation, as the picker offers them. Draft and superseded records are not offered.</summary>
    public async Task<IReadOnlyList<ReleasedMaterialOption>> ListReleasedMaterialsAsync(CancellationToken cancellationToken = default)
    {
        var records = await _materials.ListAsync(cancellationToken).ConfigureAwait(false);

        return records
            .Where(r => r.ValidationState == ReferenceValidationState.Released)
            .OrderBy(r => r.Definition.Designation ?? r.Definition.Name, StringComparer.Ordinal)
            .Select(r => new ReleasedMaterialOption(
                r.Id,
                r.Definition.Designation is { } designation && !string.Equals(designation, r.Definition.Name, StringComparison.Ordinal)
                    ? $"{designation} — {r.Definition.Name}"
                    : r.Definition.Name,
                ReferencePin.For(_materials.LibraryName, r)))
            .ToList();
    }

    /// <summary>
    /// Reads every material-sourced input of <paramref name="module"/> from
    /// the released record <paramref name="recordId"/>, each expressed in
    /// the input's own default unit, and names any property the record
    /// cannot supply.
    /// </summary>
    public async Task<MaterialFill> FillFromMaterialAsync(CalculationModuleDescriptor module, string recordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var fields = new List<CalculationFormField>();
        var problems = new List<CalculationFormProblem>();
        ReleasedMaterialOption? option = null;

        foreach (var input in module.Inputs.Where(i => i.MaterialPropertyName is not null))
        {
            var reading = await ReadPropertyAsync(input.DimensionName!, recordId, input.MaterialPropertyName!, cancellationToken).ConfigureAwait(false);

            if (reading.Pin is { } pin && option is null)
                option = new ReleasedMaterialOption(recordId, recordId, pin);

            if (reading.Value is null)
            {
                problems.Add(new CalculationFormProblem(input.Name, input.Label, reading.Reason ?? "could not be read from the record"));
                continue;
            }

            var value = CalculationInputUnits.ValueIn(input.DimensionName!, reading.Value, input.DefaultUnitSymbol!);
            fields.Add(new CalculationFormField(input.Name, value.ToString("R", CultureInfo.InvariantCulture), input.DefaultUnitSymbol));
        }

        if (option is null)
        {
            var record = await _materials.FindAsync(recordId, cancellationToken).ConfigureAwait(false);
            option = record is null
                ? throw new ArgumentException($"No material '{recordId}' is registered.", nameof(recordId))
                : new ReleasedMaterialOption(recordId, record.Definition.Designation ?? record.Definition.Name, ReferencePin.For(_materials.LibraryName, record));
        }

        return new MaterialFill(option, fields, problems);
    }

    /// <summary>
    /// Builds <paramref name="module"/>'s input record from what the form
    /// collected, or names every input that stopped it. Reference inputs
    /// take <paramref name="material"/>'s pin; a required one with no
    /// material picked is a problem, an optional one is left empty.
    /// </summary>
    public static CalculationInputBuild BuildInput(CalculationModuleDescriptor module, IReadOnlyList<CalculationFormField> fields, ReleasedMaterialOption? material)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(fields);

        var byName = fields.ToDictionary(f => f.Name, StringComparer.Ordinal);
        var constructor = module.InputType.GetConstructors().Single();
        var parameters = constructor.GetParameters();
        var arguments = new object?[parameters.Length];
        var problems = new List<CalculationFormProblem>();

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var descriptor = module.Inputs.FirstOrDefault(d => string.Equals(d.Name, parameter.Name, StringComparison.Ordinal));

            if (descriptor is null)
            {
                problems.Add(new CalculationFormProblem(parameter.Name!, parameter.Name!, "the descriptor does not describe this input"));
                continue;
            }

            byName.TryGetValue(descriptor.Name, out var field);
            arguments[i] = Convert(descriptor, parameter, field, material, problems);
        }

        if (problems.Count > 0)
            return new CalculationInputBuild(null, problems);

        return new CalculationInputBuild(constructor.Invoke(arguments), []);
    }

    /// <summary>Builds the input from the form and runs it: one call for a Calculate button.</summary>
    public async Task<CalculationAttempt> CalculateAsync(
        CalculationModuleDescriptor module, IReadOnlyList<CalculationFormField> fields, ReleasedMaterialOption? material, CancellationToken cancellationToken = default)
    {
        var build = BuildInput(module, fields, material);

        if (!build.Succeeded)
            return new CalculationAttempt(null, build.Problems, null);

        try
        {
            return new CalculationAttempt(await RunAsync(module, build.Input!, cancellationToken).ConfigureAwait(false), [], null);
        }
        catch (CalculationInputInvalidException rejected)
        {
            // The definition's own refusal of a malformed input, in its words.
            return new CalculationAttempt(null, [], rejected.Message);
        }
    }

    /// <summary>Executes <paramref name="input"/> through the engine as <paramref name="module"/> and presents the record.</summary>
    /// <exception cref="CalculationInputInvalidException">The definition rejected the input.</exception>
    public async Task<CalculationModuleRun> RunAsync(CalculationModuleDescriptor module, object input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(input);

        if (!module.InputType.IsInstanceOfType(input))
            throw new ArgumentException($"The input is a {input.GetType().Name}; {module.Id} takes a {module.InputType.Name}.", nameof(input));

        var run = typeof(CalculationModuleWorkbench)
            .GetMethod(nameof(RunTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(module.InputType, module.ResultType);

        try
        {
            return await ((Task<CalculationModuleRun>)run.Invoke(this, [module, input, cancellationToken])!).ConfigureAwait(false);
        }
        catch (TargetInvocationException wrapped) when (wrapped.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(wrapped.InnerException).Throw();
            throw;
        }
    }

    private async Task<CalculationModuleRun> RunTypedAsync<TInput, TResult>(CalculationModuleDescriptor module, TInput input, CancellationToken cancellationToken)
    {
        var record = await _engine.ExecuteAsync<TInput, TResult>(module.Id, input, cancellationToken).ConfigureAwait(false);

        var results = new List<CalculationRunRow>();
        string? refusal = null;
        EngineeringCheckOutcome? outcome = null;

        foreach (var property in typeof(TResult).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            var value = property.GetValue(record.Result);

            if (value is EngineeringCheckOutcome checkOutcome && property.Name == "Outcome")
            {
                outcome = checkOutcome;
                continue;
            }

            if (property.Name == "RefusalReason" && value is string reason)
            {
                refusal = reason;
                continue;
            }

            if (property.Name == "RefusalReason")
                continue;

            results.Add(new CalculationRunRow(Humanise(property.Name), Format(value)));
        }

        var summary = outcome switch
        {
            EngineeringCheckOutcome.MeetsCriteria => "Meets its criteria",
            EngineeringCheckOutcome.DoesNotMeetCriteria => "Does not meet its criteria",
            EngineeringCheckOutcome.OutsideMethodLimits => "Outside the method's limits — refused, nothing computed",
            _ => record.Validation.Outcome == CalculationValidationOutcome.Valid ? "Computed" : "Computed, with a constraint not satisfied",
        };

        return new CalculationModuleRun(
            module,
            record.Id,
            record.ExecutedAt,
            summary,
            outcome == EngineeringCheckOutcome.OutsideMethodLimits,
            refusal,
            results,
            record.IntermediateResults.Select(i => new CalculationRunRow(i.Name, Format(i.Value))).ToList(),
            record.Validation.ConstraintChecks.Select(c => new CalculationCheckRow(c.Description, c.IsSatisfied, c.Detail)).ToList(),
            record.Validation.Outcome.ToString(),
            record.ReferencedMaterialIds);
    }

    // ---- Form values to constructor arguments ----

    private static object? Convert(CalculationInputDescriptor descriptor, ParameterInfo parameter, CalculationFormField? field, ReleasedMaterialOption? material, List<CalculationFormProblem> problems)
    {
        var underlying = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

        void Problem(string text) => problems.Add(new CalculationFormProblem(descriptor.Name, descriptor.Label, text));

        switch (descriptor.Kind)
        {
            case CalculationInputKind.Quantity:
                if (string.IsNullOrWhiteSpace(field?.Text))
                {
                    if (descriptor.IsOptional)
                        return null;

                    Problem("no value was entered");
                    return null;
                }

                var quantity = CalculationInputUnits.TryParse(descriptor.DimensionName!, field.Text, field.UnitSymbol ?? descriptor.DefaultUnitSymbol, out var problem);
                if (quantity is null)
                    Problem(problem!);
                return quantity;

            case CalculationInputKind.Number:
                if (string.IsNullOrWhiteSpace(field?.Text))
                {
                    Problem("no value was entered");
                    return null;
                }

                if (underlying == typeof(int))
                {
                    if (!int.TryParse(field.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
                    {
                        Problem($"'{field.Text.Trim()}' is not a whole number");
                        return null;
                    }

                    return count;
                }

                if (!double.TryParse(field.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                {
                    Problem($"'{field.Text.Trim()}' is not a number");
                    return null;
                }

                return number;

            case CalculationInputKind.Text:
                if (string.IsNullOrWhiteSpace(field?.Text))
                {
                    if (descriptor.IsOptional)
                        return null;

                    Problem("nothing was entered");
                    return null;
                }

                return field.Text.Trim();

            case CalculationInputKind.Choice:
                var choice = field?.Choice ?? descriptor.Choices?.FirstOrDefault();
                if (choice is null || !Enum.TryParse(underlying, choice, ignoreCase: false, out var member))
                {
                    Problem($"'{choice}' is not one of {string.Join(", ", descriptor.Choices ?? [])}");
                    return null;
                }

                return member;

            case CalculationInputKind.Boolean:
                return field?.Flag ?? false;

            case CalculationInputKind.List:
                return ConvertRows(descriptor, underlying, field?.Rows ?? [], Problem);

            case CalculationInputKind.Reference:
                if (material is null)
                {
                    if (descriptor.IsOptional)
                        return null;

                    Problem("pick a released material record");
                    return null;
                }

                return material.Pin;

            default:
                Problem($"unknown input kind {descriptor.Kind}");
                return null;
        }
    }

    private static object? ConvertRows(CalculationInputDescriptor descriptor, Type listType, IReadOnlyList<string> rows, Action<string> problem)
    {
        var elementType = listType.IsGenericType ? listType.GetGenericArguments()[0] : null;
        if (elementType is null)
        {
            problem("the input is not a list of rows");
            return null;
        }

        var constructor = elementType.GetConstructors().Single();
        var cells = constructor.GetParameters();
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        var meaningful = rows.Select(r => r.Trim()).Where(r => r.Length > 0).ToList();

        if (meaningful.Count == 0)
        {
            problem("no rows were entered");
            return null;
        }

        for (var r = 0; r < meaningful.Count; r++)
        {
            var parts = meaningful[r].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != cells.Length)
            {
                problem($"row {r + 1} has {parts.Length} value(s); expected {cells.Length} ({string.Join(", ", descriptor.Choices ?? cells.Select(c => c.Name!))})");
                return null;
            }

            var arguments = new object?[cells.Length];
            for (var c = 0; c < cells.Length; c++)
            {
                var cellType = cells[c].ParameterType;

                if (cellType.IsGenericType && cellType.GetGenericTypeDefinition() == typeof(Quantity<>))
                {
                    var value = CalculationInputUnits.TryParseWithUnit(cellType.GetGenericArguments()[0].Name, parts[c], out var cellProblem);
                    if (value is null)
                    {
                        problem($"row {r + 1}, {cells[c].Name}: {cellProblem}");
                        return null;
                    }

                    arguments[c] = value;
                }
                else if (cellType == typeof(double))
                {
                    if (!double.TryParse(parts[c], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                    {
                        problem($"row {r + 1}, {cells[c].Name}: '{parts[c]}' is not a number");
                        return null;
                    }

                    arguments[c] = number;
                }
                else
                {
                    problem($"row {r + 1}, {cells[c].Name}: a {cellType.Name} cannot be typed as a row value");
                    return null;
                }
            }

            list.Add(constructor.Invoke(arguments));
        }

        return list;
    }

    // ---- Materials ----

    private sealed record PropertyReading(object? Value, ReferencePin? Pin, string? Reason);

    private async Task<PropertyReading> ReadPropertyAsync(string dimensionName, string recordId, string propertyName, CancellationToken cancellationToken)
    {
        var read = typeof(CalculationModuleWorkbench)
            .GetMethod(nameof(ReadTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(CalculationInputUnits.DimensionTypeOf(dimensionName));

        return await ((Task<PropertyReading>)read.Invoke(this, [recordId, propertyName, cancellationToken])!).ConfigureAwait(false);
    }

    private async Task<PropertyReading> ReadTypedAsync<TDimension>(string recordId, string propertyName, CancellationToken cancellationToken)
        where TDimension : IDimension
    {
        var reading = await MaterialPropertyReader.ReadAsync<TDimension>(_materials, recordId, propertyName, cancellationToken).ConfigureAwait(false);
        return new PropertyReading(reading.Succeeded ? reading.Value : null, reading.Pin, reading.Reason);
    }

    // ---- Presentation ----

    /// <summary>A property name as a label: "MaximumBendingStress" reads "Maximum bending stress".</summary>
    public static string Humanise(string propertyName)
    {
        var text = new StringBuilder();

        for (var i = 0; i < propertyName.Length; i++)
        {
            var c = propertyName[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(propertyName[i - 1]) || (i + 1 < propertyName.Length && char.IsLower(propertyName[i + 1]))))
            {
                text.Append(' ');
                text.Append(char.ToLowerInvariant(c));
            }
            else
            {
                text.Append(c);
            }
        }

        return text.ToString();
    }

    /// <summary>A value as the surface shows it: a quantity with its unit, a number to six figures, yes or no, a list joined, nothing as a dash.</summary>
    public static string Format(object? value)
    {
        switch (value)
        {
            case null:
                return "—";
            case bool flag:
                return flag ? "Yes" : "No";
            case double number:
                return number.ToString("G6", CultureInfo.InvariantCulture);
            case int count:
                return count.ToString(CultureInfo.InvariantCulture);
            case string text:
                return text;
            case Enum member:
                return Humanise(member.ToString());
            case ReferencePin pin:
                return pin.ToString();
        }

        var type = value.GetType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Quantity<>))
        {
            var magnitude = (double)type.GetProperty("Value")!.GetValue(value)!;
            var unit = type.GetProperty("Unit")!.GetValue(value)!;
            var symbol = (string)unit.GetType().GetProperty("Symbol")!.GetValue(unit)!;
            return $"{magnitude.ToString("G6", CultureInfo.InvariantCulture)} {symbol}";
        }

        if (value is IEnumerable items)
        {
            var parts = items.Cast<object?>().Select(Format).ToList();
            return parts.Count <= 24 ? string.Join("; ", parts) : string.Join("; ", parts.Take(24)) + $"; … ({parts.Count} in all)";
        }

        return value.ToString() ?? "—";
    }
}
