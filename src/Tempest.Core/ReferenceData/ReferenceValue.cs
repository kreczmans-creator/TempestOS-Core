using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// One dimensioned engineering value together with where it came from and
/// the conditions it holds under.
/// </summary>
/// <remarks>
/// The shared shape every Group A library records a sourced engineering
/// value in. A value can never be recorded without also recording its own
/// origin, by construction rather than by convention. Strongly typed on
/// <typeparamref name="TDimension"/> rather than boxed, so no codec is
/// needed to round-trip it (`ADR-0124`).
/// </remarks>
/// <typeparam name="TDimension">The physical dimension of <paramref name="Value"/>.</typeparam>
/// <param name="Value">The value, in the unit the source itself quoted it in — never silently converted, so the record stays a faithful transcription.</param>
/// <param name="Origin">Where the value came from.</param>
/// <param name="Conditions">The conditions the value holds under, as the source states them (a temperature, a lubrication regime, a load case, a test method). Free text — no fixed vocabulary of conditions exists. <see langword="null"/> if the source gave none.</param>
/// <param name="SourceDesignation">The source's own symbol or label for this value (e.g. a catalogue column heading). <see langword="null"/> if none was given.</param>
public sealed record ReferenceValue<TDimension>(
    Quantity<TDimension> Value,
    ReferenceValueOrigin Origin,
    string? Conditions = null,
    string? SourceDesignation = null)
    where TDimension : IDimension
{
    /// <summary>
    /// <see cref="Value"/> expressed in <typeparamref name="TDimension"/>'s
    /// own base unit, for order-comparing values two sources quoted in
    /// different units. The record itself is unchanged.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public double CanonicalValue => Value.BaseValue;

    /// <summary>Whether this value was computed by TempestOS rather than taken from a source.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDerived => Origin == ReferenceValueOrigin.DerivedByTempestOS;
}
