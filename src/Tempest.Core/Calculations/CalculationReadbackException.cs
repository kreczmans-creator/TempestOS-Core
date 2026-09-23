namespace Tempest.Core.Calculations;

/// <summary>
/// Thrown when a typed read-back of a value the Calculation Framework is
/// holding untyped (an intermediate result, a retained input) is asked for
/// a type that does not match the type it was actually recorded with
/// (`TD-22`, `TD-29`) — the honest alternative to letting a mismatched cast
/// throw <see cref="InvalidCastException"/>, or worse, silently return a
/// well-formed value full of defaults.
/// </summary>
public sealed class CalculationReadbackException : CalculationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationReadbackException"/> class.
    /// </summary>
    /// <param name="key">The name of the value that could not be read back — an intermediate result's own <c>Name</c>, or an input's own key.</param>
    /// <param name="requestedType">The type the caller asked for.</param>
    /// <param name="actualTypeName">The full name of the type the value was actually recorded with.</param>
    public CalculationReadbackException(string key, Type requestedType, string actualTypeName)
        : base(BuildMessage(key, requestedType, actualTypeName))
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(requestedType);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualTypeName);

        Key = key;
        RequestedTypeName = requestedType.FullName ?? requestedType.Name;
        ActualTypeName = actualTypeName;
    }

    /// <summary>Gets the name of the value that could not be read back.</summary>
    public string Key { get; }

    /// <summary>Gets the full name of the type the caller asked for.</summary>
    public string RequestedTypeName { get; }

    /// <summary>Gets the full name of the type the value was actually recorded with.</summary>
    public string ActualTypeName { get; }

    private static string BuildMessage(string key, Type requestedType, string actualTypeName) =>
        $"'{key}' was recorded as '{actualTypeName}' and cannot be read back as '{requestedType.FullName ?? requestedType.Name}'.";
}
