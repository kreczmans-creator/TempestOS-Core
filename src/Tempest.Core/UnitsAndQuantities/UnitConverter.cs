namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// The concrete <see cref="IUnitConverter"/> implementation.
/// </summary>
/// <remarks>
/// Stateless — holds no fields, requires no constructor arguments, and is
/// safe to construct with <see langword="new"/>() as freely as it is safe
/// to share as a singleton (`ADR-0054`). Not registered with the DI
/// container; this framework registers nothing.
/// </remarks>
public sealed class UnitConverter : IUnitConverter
{
    /// <inheritdoc />
    public Quantity<TDimension> Convert<TDimension>(Quantity<TDimension> source, Unit<TDimension> targetUnit) where TDimension : IDimension =>
        source.ConvertTo(targetUnit);
}
