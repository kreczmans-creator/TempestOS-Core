using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// The value a rule compares against — and where that value's authority
/// comes from.
/// </summary>
/// <remarks>
/// <para>
/// <b>A threshold is never a bare number.</b> A rule reading
/// <c>yieldStrength &gt; 250e6</c> hides three separate questions: what
/// unit, who says 250, and on what authority. This type answers all three
/// by construction, in exactly one of two ways:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="Literal"/> — a <see cref="ReferenceQuantityValue"/>, which
/// already carries its own unit, its own
/// <see cref="ReferenceValueOrigin"/> and the conditions it holds under.
/// A figure the rule's author transcribed from the rule's own source
/// belongs here: the origin says where it came from, and the rule's
/// provenance says which document.
/// </item>
/// <item>
/// <see cref="ConstantSymbol"/> — a symbol resolved against `A6`, for a
/// threshold that is a published engineering constant rather than
/// something this rule invented. The value is read at evaluation time
/// from the released constant, and the constant's own record and revision
/// are pinned into the result.
/// </item>
/// </list>
/// <para>
/// A threshold that is genuinely a project requirement rather than a
/// property of the rule does not belong in the rule at all — it belongs in
/// the criteria the caller supplies. That separation is what keeps a rule
/// library reusable across projects.
/// </para>
/// </remarks>
public sealed record RuleThreshold
{
    /// <summary>
    /// The single constructor, and the JSON round-trip constructor.
    /// Exactly one of the two forms must be present: a threshold with
    /// neither compares against nothing, and a threshold with both has two
    /// authorities and no way to choose between them.
    /// </summary>
    /// <exception cref="ArgumentException">Neither or both forms are present.</exception>
    [System.Text.Json.Serialization.JsonConstructor]
    public RuleThreshold(ReferenceQuantityValue? literal, string? constantSymbol)
    {
        if (literal is null == string.IsNullOrWhiteSpace(constantSymbol))
            throw new ArgumentException(
                "A rule threshold is either a transcribed value or a constant symbol, and must be exactly one of the two.",
                nameof(literal));

        Literal = literal;
        ConstantSymbol = constantSymbol?.Trim();
    }

    /// <summary>The threshold as a transcribed value carrying its own unit, origin and conditions. <see langword="null"/> where the threshold is a constant symbol.</summary>
    public ReferenceQuantityValue? Literal { get; }

    /// <summary>The `A6` symbol the threshold resolves to. <see langword="null"/> where the threshold is a transcribed value.</summary>
    public string? ConstantSymbol { get; }

    /// <summary>Whether this threshold must be resolved against the Engineering Constants Library before it can be compared.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequiresConstantResolution => ConstantSymbol is not null;

    /// <summary>A threshold transcribed into the rule, carrying its own unit, origin and conditions.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static RuleThreshold FromValue(ReferenceQuantityValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new RuleThreshold(value, null);
    }

    /// <summary>A threshold that is a published engineering constant, resolved from `A6` at evaluation time.</summary>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is null, empty, or whitespace.</exception>
    public static RuleThreshold FromConstant(string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        return new RuleThreshold(null, symbol.Trim());
    }

    /// <summary>A short description of the threshold, for explanations.</summary>
    public override string ToString() =>
        Literal is not null ? Literal.Value.ToString() ?? "(value)" : $"constant '{ConstantSymbol}'";
}

/// <summary>How a recorded value is compared against a <see cref="RuleThreshold"/>.</summary>
/// <remarks>
/// Comparison is always performed on the base-unit magnitude of both
/// sides, so a value a source quoted in millimetres and a threshold a rule
/// quoted in inches compare correctly. A comparison between two different
/// dimensions is never attempted — it is reported as
/// <see cref="AssessmentOutcome.Indeterminate"/>, because it is a
/// modelling error rather than a failed comparison.
/// </remarks>
public enum QuantityComparator
{
    /// <summary>The value must be greater than or equal to the threshold.</summary>
    AtLeast,

    /// <summary>The value must be less than or equal to the threshold.</summary>
    AtMost,

    /// <summary>The value must be strictly greater than the threshold.</summary>
    GreaterThan,

    /// <summary>The value must be strictly less than the threshold.</summary>
    LessThan,

    /// <summary>The value must equal the threshold, within the comparison tolerance.</summary>
    EqualTo,

    /// <summary>The value must differ from the threshold, by more than the comparison tolerance.</summary>
    NotEqualTo
}
