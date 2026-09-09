namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// Marker for a physical dimension (length, mass, force, and so on). A
/// distinct type per dimension prevents a length-typed quantity from
/// being mistaken for a mass-typed one at compile time.
/// </summary>
/// <remarks>
/// Every implementation is expected to be a non-instantiable, <see langword="sealed"/>
/// marker class with a private constructor (see <see cref="Length"/>,
/// <see cref="Mass"/>, and so on) — <see cref="IDimension"/> itself carries
/// no instance members and exists purely as a compile-time phantom type.
/// </remarks>
/// <remarks>
/// `ADR-0147`. <see cref="Vector"/> is the one runtime fact every marker
/// type must declare about itself: which <see cref="UnitsAndQuantities.Dimension"/>
/// it stands for. This is what lets <see cref="Quantity{TDimension}.ToQuantity"/>
/// and <see cref="Quantity{TDimension}.FromQuantity"/> cross the boundary
/// between the compile-time-checked generic facade and the runtime-checked
/// non-generic <see cref="Quantity"/> — the facade already prevented a
/// length from being added to a mass at compile time; <see cref="Vector"/>
/// is what lets that same guarantee be checked at run time on the far side
/// of that boundary, where the compile-time type has already been erased.
/// </remarks>
public interface IDimension
{
    /// <summary>The runtime <see cref="UnitsAndQuantities.Dimension"/> vector this marker type stands for.</summary>
    static abstract Dimension Vector { get; }
}
