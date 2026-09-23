using System.Reflection;
using System.Text;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations;

/// <summary>
/// Produces a <see cref="CalculationComparison"/> between two
/// <see cref="CalculationRecord{TResult}"/>s (`TD-29`) — pure, synchronous,
/// and reflection-based, since <c>TInput</c>/<c>TResult</c> are ordinary
/// records/classes whose own public properties are the "keys" a diff
/// reports by name.
/// </summary>
public static class CalculationComparer
{
    /// <summary>
    /// Compares <paramref name="recordA"/> against <paramref name="recordB"/>
    /// — which input and result fields changed, old and new, with units.
    /// </summary>
    /// <exception cref="CalculationReadbackException">A record's own retained input does not match <typeparamref name="TInput"/>.</exception>
    public static CalculationComparison Compare<TInput, TResult>(CalculationRecord<TResult> recordA, CalculationRecord<TResult> recordB)
    {
        ArgumentNullException.ThrowIfNull(recordA);
        ArgumentNullException.ThrowIfNull(recordB);

        string? inputNote = null;
        IReadOnlyList<CalculationFieldDiff> inputChanges = [];

        if (recordA.Input is null || recordB.Input is null)
        {
            var withoutInput = new List<string>();
            if (recordA.Input is null)
                withoutInput.Add(recordA.Id.ToString());
            if (recordB.Input is null)
                withoutInput.Add(recordB.Id.ToString());

            inputNote =
                $"No input comparison: record(s) {string.Join(", ", withoutInput)} ran before input retention "
                + "(TD-29) and carry no input.";
        }
        else
        {
            var inputA = CalculationTypedReadback.Read<TInput>(recordA.Input, recordA.InputTypeName, $"Input (record {recordA.Id})");
            var inputB = CalculationTypedReadback.Read<TInput>(recordB.Input, recordB.InputTypeName, $"Input (record {recordB.Id})");
            inputChanges = CompareFields(typeof(TInput), inputA, inputB);
        }

        var resultChanges = CompareFields(typeof(TResult), recordA.Result, recordB.Result);

        return new CalculationComparison(recordA.Id, recordB.Id, inputNote, inputChanges, resultChanges);
    }

    private static IReadOnlyList<CalculationFieldDiff> CompareFields(Type type, object? a, object? b)
    {
        if (a is null && b is null)
            return [];

        var diffs = new List<CalculationFieldDiff>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;

            var oldValue = a is null ? null : property.GetValue(a);
            var newValue = b is null ? null : property.GetValue(b);

            if (Equals(oldValue, newValue))
                continue;

            diffs.Add(new CalculationFieldDiff(property.Name, FormatFieldValue(oldValue), FormatFieldValue(newValue)));
        }

        return diffs;
    }

    /// <summary>
    /// Formats one field's value for display — a boxed <c>Quantity&lt;TDimension&gt;</c>
    /// becomes "value unit"; a plain number reads to six significant figures
    /// with trailing zeros trimmed; an enum reads as spaced words ("Meets
    /// criteria"); everything else is its own <see cref="object.ToString"/>.
    /// What changed is decided by <see cref="object.Equals(object?)"/> on the
    /// values themselves, never on these strings.
    /// </summary>
    private static string? FormatFieldValue(object? value)
    {
        if (value is null)
            return null;

        var type = value.GetType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Quantity<>))
        {
            var quantityValue = type.GetProperty(nameof(Quantity<Length>.Value))?.GetValue(value);
            var unit = type.GetProperty(nameof(Quantity<Length>.Unit))?.GetValue(value);
            var symbol = unit?.GetType().GetProperty(nameof(Unit<Length>.Symbol))?.GetValue(unit);

            return symbol is not null ? $"{quantityValue} {symbol}" : quantityValue?.ToString() ?? value.ToString();
        }

        return value switch
        {
            double number => EngineeringNumber.Format(number),
            float number => EngineeringNumber.Format(number),
            decimal number => EngineeringNumber.Format((double)number),
            Enum member => SpaceWords(member.ToString()),
            _ => value.ToString(),
        };
    }

    /// <summary>"DoesNotMeetCriteria" as "Does not meet criteria": a word break before each capital that follows a lower-case letter.</summary>
    private static string SpaceWords(string name)
    {
        var text = new StringBuilder(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]))
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
}
