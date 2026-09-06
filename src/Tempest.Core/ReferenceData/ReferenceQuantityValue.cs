using System.Text.Json.Serialization;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.ReferenceData;

/// <summary>
/// A dimensioned engineering value whose dimension is not known until run
/// time, together with where it came from and the conditions it holds
/// under.
/// </summary>
/// <remarks>
/// <para>
/// The boxed counterpart of <see cref="ReferenceValue{TDimension}"/>, for
/// the two Group A libraries whose value set is genuinely open: Materials,
/// whose property names are deliberately not a closed set (`ADR-0055`),
/// and Constants, whose whole purpose is to hold a value of whatever
/// dimension a constant happens to have. Everywhere else, a value is
/// declared at a statically-known dimension and needs none of this.
/// </para>
/// <para>
/// <b>Stored encoded, read decoded.</b> A boxed
/// <see cref="Quantity{TDimension}"/> cannot round-trip through
/// <c>System.Text.Json</c> — the deserialiser has no way to know which
/// closed generic to rebuild, and hands back a <c>JsonElement</c> instead.
/// So the stored shape is <see cref="EncodedQuantity"/>, and
/// <see cref="Value"/> decodes from it on first read and caches the
/// result. A caller constructs from the quantity it actually has and reads
/// back the quantity it actually wants; the encoding is an implementation
/// detail of durability, not something a consumer handles.
/// </para>
/// </remarks>
public sealed record ReferenceQuantityValue
{
    private object? _decoded;

    /// <summary>
    /// Initialises a new instance from a boxed <see cref="Quantity{TDimension}"/>.
    /// </summary>
    /// <param name="value">A boxed <see cref="Quantity{TDimension}"/> of any dimension <see cref="ReferenceQuantityCodec"/> supports.</param>
    /// <param name="origin">Where the value came from — never optional, so a derived value can never be mistaken for a sourced one.</param>
    /// <param name="conditions">The conditions the value holds under (a temperature, a test method, an orientation). <see langword="null"/> if the source gave none.</param>
    /// <param name="sourceDesignation">The source's own symbol or label for this value. <see langword="null"/> if none was given.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ReferenceDataException"><paramref name="value"/> is not a supported dimensioned quantity.</exception>
    public ReferenceQuantityValue(object value, ReferenceValueOrigin origin, string? conditions = null, string? sourceDesignation = null)
        : this(ReferenceQuantityCodec.Encode(value), origin, conditions, sourceDesignation)
    {
        _decoded = value;
    }

    /// <summary>Initialises a new instance from the stored, encoded form.</summary>
    /// <param name="encodedValue">The value's own plain, serialisable parts.</param>
    /// <param name="origin">Where the value came from.</param>
    /// <param name="conditions">The conditions the value holds under.</param>
    /// <param name="sourceDesignation">The source's own label for this value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="encodedValue"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public ReferenceQuantityValue(EncodedQuantity encodedValue, ReferenceValueOrigin origin, string? conditions = null, string? sourceDesignation = null)
    {
        ArgumentNullException.ThrowIfNull(encodedValue);

        EncodedValue = encodedValue;
        Origin = origin;
        Conditions = conditions;
        SourceDesignation = sourceDesignation;
    }

    /// <summary>The value's own plain, serialisable parts — the shape it is stored as.</summary>
    public EncodedQuantity EncodedValue { get; }

    /// <summary>Where <see cref="Value"/> came from.</summary>
    public ReferenceValueOrigin Origin { get; }

    /// <summary>The conditions <see cref="Value"/> holds under. <see langword="null"/> if the source gave none.</summary>
    public string? Conditions { get; }

    /// <summary>The source's own symbol or label for this value. <see langword="null"/> if none was given.</summary>
    public string? SourceDesignation { get; }

    /// <summary>
    /// The value as a boxed <see cref="Quantity{TDimension}"/> — cast it to
    /// the closed generic <see cref="DimensionName"/> names.
    /// </summary>
    /// <exception cref="ReferenceDataException">The stored dimension is one this build does not recognise.</exception>
    [JsonIgnore]
    public object Value => _decoded ??= ReferenceQuantityCodec.Decode(EncodedValue);

    /// <summary>The name of the dimension this value carries.</summary>
    [JsonIgnore]
    public string DimensionName => EncodedValue.DimensionKind;

    /// <summary>The value in its own dimension's base unit, for comparing values two sources quoted in different units.</summary>
    [JsonIgnore]
    public double CanonicalValue => (EncodedValue.Value * EncodedValue.UnitToBaseFactor) + EncodedValue.UnitToBaseOffset;

    /// <summary>Whether this value was computed by TempestOS rather than taken from a source.</summary>
    [JsonIgnore]
    public bool IsDerived => Origin == ReferenceValueOrigin.DerivedByTempestOS;
}
