using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.Core.Calculations;

/// <summary>
/// A named intermediate value a calculation definition recorded while
/// computing its own final result — makes an evidentiary record
/// inspectable step-by-step, not only as a final answer.
/// </summary>
/// <param name="Name">A short, human-readable name for this intermediate value.</param>
/// <param name="Value">The value itself — typically a boxed <c>Quantity&lt;TDimension&gt;</c> where dimensioned, but not constrained to one.</param>
/// <param name="ValueTypeName">
/// The full name of <paramref name="Value"/>'s own declared CLR type —
/// what <see cref="As{TValue}"/> checks a requested type against
/// (`TD-22`). Nullable, because a result recorded before this field
/// existed does not carry it; <see cref="As{TValue}"/> still honours those
/// by falling back to a direct type check against whatever <see cref="Value"/>
/// itself holds.
/// </param>
[method: JsonConstructor]
public sealed record CalculationIntermediateResult(string Name, object Value, string? ValueTypeName)
{
    /// <summary>
    /// Records <paramref name="Value"/> together with its own runtime
    /// type's full name, so a later <see cref="As{TValue}"/> can refuse a
    /// mismatched read-back by name rather than by an unchecked cast.
    /// </summary>
    /// <remarks>
    /// Two public constructors exist only for callers; System.Text.Json
    /// always deserializes through the three-parameter one above
    /// (<c>[JsonConstructor]</c>) — the same disambiguation
    /// <c>Quantity&lt;TDimension&gt;</c>'s own constructor already needs and
    /// applies for exactly the same reason.
    /// </remarks>
    public CalculationIntermediateResult(string Name, object Value)
        : this(Name, Value, Value?.GetType().FullName)
    {
    }

    /// <summary>
    /// Reads <see cref="Value"/> back as <typeparamref name="TValue"/> — the
    /// type it was declared with — never a blind cast.
    /// </summary>
    /// <remarks>
    /// Handles both shapes <see cref="Value"/> can actually hold: the real
    /// CLR object a definition recorded within the same process (an
    /// in-memory <see cref="CalculationRecord{TResult}"/>, just returned by
    /// <see cref="ICalculationEngine.ExecuteAsync{TInput, TResult}"/>), and a
    /// boxed <see cref="JsonElement"/> after a round trip through
    /// persistence (<see cref="ICalculationEngine.FindRecordAsync{TResult}"/>).
    /// Either way, a declared-type mismatch or an unreadable value throws
    /// <see cref="CalculationReadbackException"/> rather than letting an
    /// <see cref="InvalidCastException"/> or a silently-wrong default escape.
    /// </remarks>
    /// <exception cref="CalculationReadbackException">
    /// <typeparamref name="TValue"/> does not match <see cref="ValueTypeName"/>
    /// (when known), or <see cref="Value"/> cannot be read back as
    /// <typeparamref name="TValue"/>.
    /// </exception>
    public TValue As<TValue>()
    {
        var requestedTypeName = typeof(TValue).FullName ?? typeof(TValue).Name;

        if (ValueTypeName is { } declared && !string.Equals(declared, requestedTypeName, StringComparison.Ordinal))
            throw new CalculationReadbackException(Name, typeof(TValue), declared);

        if (Value is TValue typed)
            return typed;

        if (Value is JsonElement element)
        {
            try
            {
                if (element.Deserialize<TValue>() is { } deserialized)
                    return deserialized;
            }
            catch (JsonException)
            {
                // Falls through to the shared refusal below.
            }
        }

        throw new CalculationReadbackException(Name, typeof(TValue), ValueTypeName ?? Value.GetType().FullName ?? Value.GetType().Name);
    }
}
