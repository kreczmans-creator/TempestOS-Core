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
/// no members and exists purely as a compile-time phantom type.
/// </remarks>
public interface IDimension
{
}
