namespace Tempest.Core.Calculations;

/// <summary>
/// The base exception thrown when a Calculation Framework operation
/// fails.
/// </summary>
/// <remarks>
/// Mirrors <see cref="Materials.MaterialsException"/>'s own
/// base-plus-subtype pattern — <c>public class</c>, not
/// <see langword="abstract"/>, matching this codebase's own universal
/// convention rather than `WP7.0C Engineering Foundation Contracts.md`'s
/// own literal proposal, the same disclosed deviation `WP 7.1A`/
/// `WP 7.1C` already established.
/// </remarks>
public class CalculationException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public CalculationException(string message)
        : base(message)
    {
    }
}
