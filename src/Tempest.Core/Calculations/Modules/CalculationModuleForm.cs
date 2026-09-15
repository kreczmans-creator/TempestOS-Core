using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>A released reference record, as a calculator's picker offers it.</summary>
/// <param name="Library">Which library it is in.</param>
/// <param name="RecordId">The record's registered id.</param>
/// <param name="Label">What the picker shows: designation and name.</param>
/// <param name="Pin">The exact record and revision a calculation built on it will cite.</param>
public sealed record ReleasedRecordOption(ReferenceLibrary Library, string RecordId, string Label, ReferencePin Pin)
{
    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>What the engineer typed, chose or picked for one input, by the input's own name.</summary>
/// <param name="Name">The input record's property name, as the descriptor states it.</param>
/// <param name="Text">The number or text typed, for a quantity, number or text input.</param>
/// <param name="UnitSymbol">The unit chosen, for a quantity.</param>
/// <param name="Choice">The member chosen, for a choice.</param>
/// <param name="Flag">The state, for a yes-or-no input.</param>
/// <param name="Rows">The rows typed, one per line, for a list.</param>
/// <param name="RecordId">The released record picked, for a reference.</param>
public sealed record CalculationFormField(
    string Name,
    string? Text = null,
    string? UnitSymbol = null,
    string? Choice = null,
    bool Flag = false,
    IReadOnlyList<string>? Rows = null,
    string? RecordId = null);

/// <summary>One thing wrong with what was entered, named by input so a form can point at it.</summary>
public sealed record CalculationFormProblem(string InputName, string Label, string Problem)
{
    /// <inheritdoc />
    public override string ToString() => $"{Label}: {Problem}";
}

/// <summary>The fields a picked record fills for one reference input, and any property it could not supply.</summary>
public sealed record ReferenceFill(string ReferenceInputName, ReleasedRecordOption Record, IReadOnlyList<CalculationFormField> Fields, IReadOnlyList<CalculationFormProblem> Problems);

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
/// <param name="PredecessorRecordId">The record this one re-ran, where it was a re-run.</param>
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
    IReadOnlyList<string> ReferencedMaterialIds,
    Guid? PredecessorRecordId = null);

/// <summary>
/// Builds a module's input record from what a form collected, and presents
/// what came back: the generic half of every calculator, in the domain
/// (`WP 21.7B`, moved here from <c>Tempest.Workspace</c> by `WP 21.7C` so
/// the governed <see cref="CalculationModuleService"/> and the Desktop
/// surface share one implementation).
/// </summary>
/// <remarks>
/// Nothing here knows any one module. Names, kinds, dimensions and units
/// come from <see cref="CalculationModuleDescriptors"/>; the record shapes
/// come from the input and result records themselves, read by reflection.
/// </remarks>
public static class CalculationModuleForm
{
    /// <summary>
    /// Builds <paramref name="module"/>'s input record from the fields, or
    /// names every input that stopped it. A reference input takes the pin
    /// its field's record resolved to in <paramref name="pins"/>; a required
    /// one with no pin is a problem, an optional one is left empty.
    /// </summary>
    public static CalculationInputBuild BuildInput(
        CalculationModuleDescriptor module, IReadOnlyList<CalculationFormField> fields, IReadOnlyDictionary<string, ReferencePin> pins)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(pins);

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
            arguments[i] = Convert(descriptor, parameter, field, pins, problems);
        }

        if (problems.Count > 0)
            return new CalculationInputBuild(null, problems);

        return new CalculationInputBuild(constructor.Invoke(arguments), []);
    }

    /// <summary>Presents a record of <paramref name="module"/> as rows: the outcome, every result figure, the working and the checks.</summary>
    public static CalculationModuleRun Present<TResult>(CalculationModuleDescriptor module, CalculationRecord<TResult> record)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(record);

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

            if (property.Name == "RefusalReason")
            {
                refusal = value as string;
                continue;
            }

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
            record.IntermediateResults.Select(i => new CalculationRunRow(i.Name, Format(Materialise(i)))).ToList(),
            record.Validation.ConstraintChecks.Select(c => new CalculationCheckRow(c.Description, c.IsSatisfied, c.Detail)).ToList(),
            record.Validation.Outcome.ToString(),
            record.ReferencedMaterialIds,
            record.PredecessorRecordId);
    }

    /// <summary>
    /// An intermediate as its own type again. A record read back from the
    /// store (a re-run, a compare, a record opened later) carries each
    /// intermediate as JSON beside the type it was recorded as (`WP 21.3A`);
    /// the surface shows the value in that type's own terms, never the JSON.
    /// A type that cannot be resolved is read structurally instead.
    /// </summary>
    private static object? Materialise(CalculationIntermediateResult intermediate)
    {
        if (intermediate.Value is not System.Text.Json.JsonElement element)
            return intermediate.Value;

        if (intermediate.ValueTypeName is { } typeName && Type.GetType(typeName, throwOnError: false) is { } type)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize(element.GetRawText(), type);
            }
            catch (Exception failure) when (failure is System.Text.Json.JsonException or NotSupportedException or InvalidOperationException)
            {
                // Not readable as the declared type: read what the JSON itself says.
            }
        }

        return FromJson(element);
    }

    /// <summary>What a JSON element says, structurally: a serialised quantity as "value symbol", numbers, text, yes or no, arrays as lists.</summary>
    private static object? FromJson(System.Text.Json.JsonElement element) => element.ValueKind switch
    {
        System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined => null,
        System.Text.Json.JsonValueKind.True => true,
        System.Text.Json.JsonValueKind.False => false,
        System.Text.Json.JsonValueKind.Number => element.GetDouble(),
        System.Text.Json.JsonValueKind.String => element.GetString(),
        System.Text.Json.JsonValueKind.Array => element.EnumerateArray().Select(FromJson).ToList(),
        System.Text.Json.JsonValueKind.Object
            when element.TryGetProperty("Value", out var magnitude) && magnitude.ValueKind == System.Text.Json.JsonValueKind.Number
              && element.TryGetProperty("Unit", out var unit) && unit.ValueKind == System.Text.Json.JsonValueKind.Object
              && unit.TryGetProperty("Symbol", out var symbol) && symbol.ValueKind == System.Text.Json.JsonValueKind.String
            => $"{magnitude.GetDouble().ToString("G6", CultureInfo.InvariantCulture)} {symbol.GetString()}",
        _ => element.ToString(),
    };

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
            case System.Text.Json.JsonElement element:
                return Format(FromJson(element));
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

    // ---- Form values to constructor arguments ----

    private static object? Convert(CalculationInputDescriptor descriptor, ParameterInfo parameter, CalculationFormField? field, IReadOnlyDictionary<string, ReferencePin> pins, List<CalculationFormProblem> problems)
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

                    Problem(descriptor.SourceInputName is { } source ? $"no value was entered; it is read from the record picked for {source}" : "no value was entered");
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
                if (!pins.TryGetValue(descriptor.Name, out var pin))
                {
                    if (descriptor.IsOptional)
                        return null;

                    Problem($"pick a released {descriptor.Library?.ToString().ToLowerInvariant() ?? "reference"} record");
                    return null;
                }

                return pin;

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
}
