namespace Tempest.Core.Materials;

/// <summary>
/// A single engineering property of a material specification — a
/// dimensioned value paired with the provenance every engineering property
/// must carry.
/// </summary>
/// <remarks>
/// Resolves `ADR-0055`'s own reserved property-typing question: rather than
/// the approved contract's own proposed bare, un-provenanced
/// <c>IReadOnlyDictionary&lt;string, object&gt;</c>, every property is this
/// explicit record, so provenance can never be omitted by construction, not
/// merely by convention.
/// </remarks>
public sealed record MaterialProperty
{
    /// <summary>
    /// Initialises a new instance of the <see cref="MaterialProperty"/> class.
    /// </summary>
    /// <param name="value">A boxed <c>Quantity&lt;TDimension&gt;</c> — one of the seven dimensions <c>Tempest.Core.UnitsAndQuantities</c> defines (<see cref="MaterialPropertyValueCodec.SupportedDimensionNames"/>).</param>
    /// <param name="provenance">Where <paramref name="value"/> came from, and how much it can be trusted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="provenance"/> is <see langword="null"/>.</exception>
    /// <exception cref="MaterialsException"><paramref name="value"/> is not one of the seven supported dimensions.</exception>
    public MaterialProperty(object value, MaterialPropertyProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(provenance);
        if (!MaterialPropertyValueCodec.IsSupported(value))
            throw new MaterialsException(
                $"Material property values must be one of the Units & Quantities dimensions this framework supports ({MaterialPropertyValueCodec.SupportedDimensionNames}) — received '{value.GetType()}'.");

        Value = value;
        Provenance = provenance;
    }

    /// <summary>A boxed <c>Quantity&lt;TDimension&gt;</c> value.</summary>
    public object Value { get; }

    /// <summary>Where <see cref="Value"/> came from, and how much it can be trusted.</summary>
    public MaterialPropertyProvenance Provenance { get; }
}
