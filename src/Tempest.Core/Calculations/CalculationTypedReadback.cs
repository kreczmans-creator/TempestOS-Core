using System.Text.Json;

namespace Tempest.Core.Calculations;

/// <summary>
/// The shared "read this untyped, possibly JSON-round-tripped value back as
/// a declared CLR type, or refuse honestly" logic behind every typed
/// read-back in the Calculation Framework (`TD-22`, `TD-29`): an
/// intermediate result's own <see cref="CalculationIntermediateResult.As{TValue}"/>,
/// a retained input <see cref="ICalculationEngine.ReRunAsync{TInput, TResult}(Guid, CancellationToken)"/>
/// reads back to re-run, and an input side of a
/// <see cref="CalculationComparer.Compare{TInput, TResult}"/> diff.
/// </summary>
/// <remarks>
/// Never resurrects a <see cref="Type"/> from a stored name — the same
/// discipline <see cref="CalculationEngine.FindRecordAsync{TResult}"/>
/// already applies to <c>ResultTypeName</c>: the caller always supplies
/// <typeparamref name="TValue"/> itself, and a declared type name
/// travelling with the data is only ever compared against it by string
/// equality, never resolved back into a live <see cref="Type"/>.
/// </remarks>
internal static class CalculationTypedReadback
{
    /// <summary>
    /// Reads <paramref name="rawValue"/> back as <typeparamref name="TValue"/>.
    /// </summary>
    /// <param name="rawValue">The untyped value — either the real CLR object (same-process), or a boxed <see cref="JsonElement"/> (after persistence).</param>
    /// <param name="declaredTypeName">The full name of the type this value was recorded with, or <see langword="null"/> if unknown (predates the declared-type field).</param>
    /// <param name="key">A name identifying this value, for the exception message.</param>
    /// <exception cref="CalculationReadbackException"><typeparamref name="TValue"/> does not match <paramref name="declaredTypeName"/>, or <paramref name="rawValue"/> cannot be read back as <typeparamref name="TValue"/>.</exception>
    public static TValue Read<TValue>(object rawValue, string? declaredTypeName, string key)
    {
        ArgumentNullException.ThrowIfNull(rawValue);

        var requestedTypeName = typeof(TValue).FullName ?? typeof(TValue).Name;

        if (declaredTypeName is not null && !string.Equals(declaredTypeName, requestedTypeName, StringComparison.Ordinal))
            throw new CalculationReadbackException(key, typeof(TValue), declaredTypeName);

        if (rawValue is TValue typed)
            return typed;

        if (rawValue is JsonElement element)
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

        throw new CalculationReadbackException(key, typeof(TValue), declaredTypeName ?? rawValue.GetType().FullName ?? rawValue.GetType().Name);
    }
}
