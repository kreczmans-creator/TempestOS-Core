using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Fasteners;

/// <summary>
/// A tightening torque a source published for a fastener, under the
/// conditions the source stated.
/// </summary>
/// <remarks>
/// <para>
/// <b>A published figure, transcribed — never a torque TempestOS worked
/// out.</b> Tightening torque depends on friction at the thread and under
/// the head, on lubrication, plating, reuse, joint stiffness and the
/// preload actually wanted, none of which A3 knows. A3 records that a
/// source published a figure and under exactly what conditions; deciding a
/// tightening torque for a real joint is a calculation and a judgement,
/// and belongs to a future calculation capability that will consume this
/// as evidence.
/// </para>
/// <para>
/// <see cref="Conditions"/> is therefore not decoration. A torque figure
/// separated from the friction condition it was published for is not
/// reference data, it is a number — which is why a figure recorded without
/// conditions is flagged by validation.
/// </para>
/// </remarks>
/// <param name="Torque">The published torque figure.</param>
/// <param name="Origin">Where the figure came from.</param>
/// <param name="Conditions">The conditions the source published it for — lubrication, coefficient of friction, plating, reuse, preload target. <see langword="null"/> if the source gave none, which validation reports.</param>
/// <param name="PropertyClass">The property class the figure applies to, where the source states one. <see langword="null"/> otherwise.</param>
/// <param name="SourceDesignation">The source's own label for the figure (a table heading, a column). <see langword="null"/> if none was given.</param>
public sealed record FastenerTorqueReference(
    Quantity<Torque> Torque,
    ReferenceValueOrigin Origin,
    string? Conditions = null,
    string? PropertyClass = null,
    string? SourceDesignation = null)
{
    /// <summary>The torque in newton metres, for order-comparing figures published in different units.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double CanonicalValue => Torque.BaseValue;

    /// <summary>Whether TempestOS computed the figure rather than taking it from a source.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDerived => Origin == ReferenceValueOrigin.DerivedByTempestOS;

    /// <summary>Whether the source's own conditions are recorded alongside the figure.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool StatesConditions => !string.IsNullOrWhiteSpace(Conditions);
}
