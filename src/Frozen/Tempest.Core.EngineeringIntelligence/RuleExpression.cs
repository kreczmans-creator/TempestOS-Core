using System.Text.Json.Serialization;

namespace Tempest.Core.EngineeringIntelligence;

/// <summary>
/// The condition a rule tests, as data rather than as code.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why an expression tree and not a delegate or a script.</b> A rule
/// whose condition is compiled code cannot be stored, revisioned,
/// superseded, diffed between revisions, or explained back to an engineer
/// — and a rule whose condition is a script is neither deterministic nor
/// inspectable without running it. A small closed algebra is all three:
/// every rule is serialisable into its own catalogue record, every
/// evaluation reports which sub-condition decided the result, and two
/// revisions of a rule differ visibly.
/// </para>
/// <para>
/// <b>Deliberately small.</b> Six forms: compare a quantity, match a text
/// attribute, require a property to be recorded at all, require evidence a
/// person must supply, combine conditions, and negate one. There is no
/// arithmetic, no loop and no user-defined function, because a rule that
/// needs any of those is a calculation, and calculations belong to
/// <see cref="Calculations.ICalculationEngine"/>. That boundary is what
/// keeps P02 from quietly becoming a second calculation engine.
/// </para>
/// <para>
/// Every form evaluates to an <see cref="AssessmentOutcome"/>, not to a
/// boolean — a condition over data that is missing has not failed, and
/// flattening that into <c>false</c> is the mistake the whole outcome
/// model exists to prevent.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "form")]
[JsonDerivedType(typeof(QuantityComparisonExpression), "quantityComparison")]
[JsonDerivedType(typeof(TextMatchExpression), "textMatch")]
[JsonDerivedType(typeof(PropertyRecordedExpression), "propertyRecorded")]
[JsonDerivedType(typeof(EvidenceRequiredExpression), "evidenceRequired")]
[JsonDerivedType(typeof(AllOfExpression), "allOf")]
[JsonDerivedType(typeof(AnyOfExpression), "anyOf")]
[JsonDerivedType(typeof(NotExpression), "not")]
[JsonDerivedType(typeof(StatedExpression), "stated")]
public abstract record RuleExpression
{
    /// <summary>A short, human-readable rendering of the condition, for explanations and for diffing two rule revisions.</summary>
    public abstract string Describe();

    /// <summary>Every sub-expression directly beneath this one. Empty for a leaf.</summary>
    [JsonIgnore]
    public virtual IReadOnlyList<RuleExpression> Children => [];

    /// <summary>Every `A6` constant symbol this expression, or anything beneath it, needs resolved before it can be evaluated.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> RequiredConstantSymbols =>
        Flatten()
            .OfType<QuantityComparisonExpression>()
            .Select(e => e.Threshold.ConstantSymbol)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Every property name this expression, or anything beneath it, reads from the subject.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> ReferencedProperties =>
        Flatten()
            .Select(e => e switch
            {
                QuantityComparisonExpression q => q.PropertyName,
                TextMatchExpression t => t.AttributeName,
                PropertyRecordedExpression p => p.PropertyName,
                _ => null,
            })
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>This expression and every expression beneath it, depth-first.</summary>
    public IReadOnlyList<RuleExpression> Flatten()
    {
        var all = new List<RuleExpression>();
        Collect(this, all);
        return all;

        static void Collect(RuleExpression expression, List<RuleExpression> into)
        {
            into.Add(expression);
            foreach (var child in expression.Children)
                Collect(child, into);
        }
    }
}

/// <summary>Compares a dimensioned property of the subject against a threshold.</summary>
/// <param name="PropertyName">The property to read from the subject. Required.</param>
/// <param name="Comparator">How the value is compared against the threshold.</param>
/// <param name="Threshold">What it is compared against. Required.</param>
public sealed record QuantityComparisonExpression(
    string PropertyName,
    QuantityComparator Comparator,
    RuleThreshold Threshold) : RuleExpression
{
    /// <summary>The property to read from the subject.</summary>
    public string PropertyName { get; } = string.IsNullOrWhiteSpace(PropertyName)
        ? throw new ArgumentException("A quantity comparison must name the property it reads.", nameof(PropertyName))
        : PropertyName.Trim();

    /// <summary>What it is compared against.</summary>
    public RuleThreshold Threshold { get; } = Threshold ?? throw new ArgumentNullException(nameof(Threshold));

    /// <inheritdoc />
    public override string Describe() => $"{PropertyName} {Describe(Comparator)} {Threshold}";

    private static string Describe(QuantityComparator comparator) => comparator switch
    {
        QuantityComparator.AtLeast => "is at least",
        QuantityComparator.AtMost => "is at most",
        QuantityComparator.GreaterThan => "is greater than",
        QuantityComparator.LessThan => "is less than",
        QuantityComparator.EqualTo => "equals",
        QuantityComparator.NotEqualTo => "does not equal",
        _ => "compares to",
    };
}

/// <summary>Matches a text or classification attribute of the subject against an accepted set.</summary>
/// <remarks>
/// Case-insensitive and whitespace-trimmed, because the attributes this
/// tests are the source's own wording rather than identifiers. An empty
/// accepted set is refused at construction: a match against nothing is not
/// a condition.
/// </remarks>
/// <param name="AttributeName">The attribute to read from the subject. Required.</param>
/// <param name="AcceptedValues">The values that satisfy the condition. Required, and never empty.</param>
/// <param name="Negated">Whether the condition is satisfied by <em>not</em> being in the set.</param>
public sealed record TextMatchExpression(
    string AttributeName,
    IReadOnlyList<string> AcceptedValues,
    bool Negated = false) : RuleExpression
{
    /// <summary>The attribute to read from the subject.</summary>
    public string AttributeName { get; } = string.IsNullOrWhiteSpace(AttributeName)
        ? throw new ArgumentException("A text match must name the attribute it reads.", nameof(AttributeName))
        : AttributeName.Trim();

    /// <summary>The values that satisfy the condition.</summary>
    public IReadOnlyList<string> AcceptedValues { get; } =
        AcceptedValues is { Count: > 0 } && AcceptedValues.All(v => !string.IsNullOrWhiteSpace(v))
            ? AcceptedValues.Select(v => v.Trim()).ToList()
            : throw new ArgumentException("A text match must accept at least one non-blank value.", nameof(AcceptedValues));

    /// <inheritdoc />
    public override string Describe() =>
        $"{AttributeName} is {(Negated ? "not " : string.Empty)}one of [{string.Join(", ", AcceptedValues)}]";
}

/// <summary>Satisfied when the subject records the named property at all — a completeness check, not a value check.</summary>
/// <param name="PropertyName">The property that must be recorded. Required.</param>
public sealed record PropertyRecordedExpression(string PropertyName) : RuleExpression
{
    /// <summary>The property that must be recorded.</summary>
    public string PropertyName { get; } = string.IsNullOrWhiteSpace(PropertyName)
        ? throw new ArgumentException("A completeness check must name the property it requires.", nameof(PropertyName))
        : PropertyName.Trim();

    /// <inheritdoc />
    public override string Describe() => $"{PropertyName} is recorded";
}

/// <summary>
/// A condition that recorded data cannot decide, and that a person must
/// close with evidence.
/// </summary>
/// <remarks>
/// Always evaluates to <see cref="AssessmentOutcome.EvidenceRequired"/>.
/// This is not an escape hatch for a condition somebody could not be
/// bothered to model: it is the honest form for a real engineering
/// question — "confirm compatibility with the service fluid", "confirm the
/// supplier's process is qualified" — that no property in any reference
/// library answers. Modelling it explicitly is what stops such a question
/// disappearing from an assessment altogether.
/// </remarks>
/// <param name="Requirement">What must be evidenced, in plain engineering language. Required.</param>
public sealed record EvidenceRequiredExpression(string Requirement) : RuleExpression
{
    /// <summary>What must be evidenced.</summary>
    public string Requirement { get; } = string.IsNullOrWhiteSpace(Requirement)
        ? throw new ArgumentException("An evidence-required condition must say what must be evidenced.", nameof(Requirement))
        : Requirement.Trim();

    /// <inheritdoc />
    public override string Describe() => $"evidence required: {Requirement}";
}

/// <summary>
/// A condition stated as holding or not holding, independent of any
/// subject property.
/// </summary>
/// <remarks>
/// The form a rule takes when its entire content is its applicability —
/// "for this family, this practice is prohibited". The condition is
/// <see langword="false"/> and the applicability does the work, which
/// keeps the rule inspectable rather than encoding the family test inside
/// a condition that would then be invisible to applicability filtering.
/// </remarks>
/// <param name="Holds">Whether the condition is satisfied.</param>
/// <param name="Statement">What is being stated. Required.</param>
public sealed record StatedExpression(bool Holds, string Statement) : RuleExpression
{
    /// <summary>What is being stated.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A stated condition must say what it states.", nameof(Statement))
        : Statement.Trim();

    /// <inheritdoc />
    public override string Describe() => Statement;
}

/// <summary>Satisfied when every sub-condition is satisfied.</summary>
/// <remarks>
/// Evaluates <b>every</b> operand rather than short-circuiting. A rule
/// that stopped at the first failure would report one reason where there
/// were three, and an engineer reading the result would fix one problem
/// and re-run to discover the next. Determinism is unaffected; only the
/// completeness of the explanation changes, and completeness is the point.
/// </remarks>
/// <param name="Operands">The sub-conditions. Required, and never empty.</param>
public sealed record AllOfExpression(IReadOnlyList<RuleExpression> Operands) : RuleExpression
{
    /// <summary>The sub-conditions.</summary>
    public IReadOnlyList<RuleExpression> Operands { get; } =
        Operands is { Count: > 0 } && Operands.All(o => o is not null)
            ? Operands
            : throw new ArgumentException("An all-of condition must combine at least one sub-condition.", nameof(Operands));

    /// <inheritdoc />
    public override IReadOnlyList<RuleExpression> Children => Operands;

    /// <inheritdoc />
    public override string Describe() => $"all of ({string.Join("; ", Operands.Select(o => o.Describe()))})";
}

/// <summary>Satisfied when at least one sub-condition is satisfied.</summary>
/// <remarks>
/// Evaluates every operand, for the same reason
/// <see cref="AllOfExpression"/> does. Where no operand passes but some
/// could not be evaluated, the result is the gap rather than a failure:
/// an alternative nobody could assess is not an alternative that was
/// ruled out.
/// </remarks>
/// <param name="Operands">The sub-conditions. Required, and never empty.</param>
public sealed record AnyOfExpression(IReadOnlyList<RuleExpression> Operands) : RuleExpression
{
    /// <summary>The sub-conditions.</summary>
    public IReadOnlyList<RuleExpression> Operands { get; } =
        Operands is { Count: > 0 } && Operands.All(o => o is not null)
            ? Operands
            : throw new ArgumentException("An any-of condition must combine at least one sub-condition.", nameof(Operands));

    /// <inheritdoc />
    public override IReadOnlyList<RuleExpression> Children => Operands;

    /// <inheritdoc />
    public override string Describe() => $"any of ({string.Join("; ", Operands.Select(o => o.Describe()))})";
}

/// <summary>Satisfied when the sub-condition is not satisfied.</summary>
/// <remarks>
/// <b>Negation does not turn a gap into a pass.</b> Negating a condition
/// that could not be evaluated leaves it unevaluated: not knowing whether
/// something holds is not the same as knowing it does not.
/// </remarks>
/// <param name="Operand">The sub-condition. Required.</param>
public sealed record NotExpression(RuleExpression Operand) : RuleExpression
{
    /// <summary>The sub-condition.</summary>
    public RuleExpression Operand { get; } = Operand ?? throw new ArgumentNullException(nameof(Operand));

    /// <inheritdoc />
    public override IReadOnlyList<RuleExpression> Children => [Operand];

    /// <inheritdoc />
    public override string Describe() => $"not ({Operand.Describe()})";
}
