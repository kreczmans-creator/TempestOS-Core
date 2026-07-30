namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// Thrown when an operation combines two <see cref="Quantity{TDimension}"/>
/// or <see cref="Unit{TDimension}"/> values that cannot be safely combined
/// without an explicit conversion.
/// </summary>
/// <remarks>
/// The generic constraint shared by both operands of every arithmetic and
/// comparison operator on <see cref="Quantity{TDimension}"/> already
/// prevents cross-dimension combination at compile time (a
/// <see cref="Quantity{TDimension}"/> of <c>TDimension = Length</c> can
/// never be added to one of <c>TDimension = Mass</c>). This exception
/// exists for the residual runtime case this compile-time guarantee does
/// not cover: two quantities of the <em>same</em> dimension but
/// <em>different</em> <see cref="Unit{TDimension}"/> values (5 m vs. 500
/// cm) — combining these without an explicit <see cref="Quantity{TDimension}.ConvertTo"/>
/// first would be an implicit unit conversion, which this framework's own
/// Design Principles forbid.
/// </remarks>
public sealed class IncompatibleUnitsException : Exception
{
    /// <summary>
    /// Initialises a new instance of the <see cref="IncompatibleUnitsException"/> class.
    /// </summary>
    /// <param name="message">A message describing the failure.</param>
    public IncompatibleUnitsException(string message)
        : base(message)
    {
    }
}
