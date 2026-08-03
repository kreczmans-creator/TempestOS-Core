namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// Converts quantities between units, for callers that do not hold a
/// strongly-typed <see cref="Quantity{TDimension}"/> directly (e.g., a
/// value read from configuration or a REST request).
/// </summary>
/// <remarks>
/// A thin, stateless convenience wrapper over <see cref="Quantity{TDimension}.ConvertTo"/>
/// (`ADR-0054`) — not a DI-registered service. Every implementation must
/// be constructible with no dependencies and safe to share as a single,
/// process-wide instance, though nothing in this platform requires it be
/// shared; a caller may equally construct a new instance per use.
/// </remarks>
public interface IUnitConverter
{
    /// <summary>Returns <paramref name="source"/> re-expressed in <paramref name="targetUnit"/>.</summary>
    Quantity<TDimension> Convert<TDimension>(Quantity<TDimension> source, Unit<TDimension> targetUnit) where TDimension : IDimension;
}
