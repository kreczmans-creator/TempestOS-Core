namespace Tempest.Core.EngineeringIntelligence.TradeStudies;

/// <summary>
/// What a stated consideration <i>is</i>, and therefore what it can do to
/// an option.
/// </summary>
/// <remarks>
/// <para>
/// These are not weights on a common scale. They are different kinds of
/// statement with different consequences, and the framework keeps them
/// apart deliberately.
/// </para>
/// <para>
/// A requirement comes from outside the study and the study may not
/// negotiate it. A constraint eliminates. A criterion discriminates. A
/// preference colours a judgement without deciding it. Flattening the
/// four into one weighted score is precisely the failure this framework
/// exists to prevent: it produces a number that looks like an answer and
/// hides the fact that one option is not actually allowed.
/// </para>
/// </remarks>
public enum ConsiderationKind
{
    /// <summary>
    /// Not stated. A consideration that has not said what it is cannot be
    /// applied, and the framework reports it rather than guessing.
    /// </summary>
    Unspecified,

    /// <summary>
    /// Something the design must do, imposed from outside the study —
    /// a specification, a contract, a regulation, a customer statement.
    /// The study does not get to trade it away; it may only record that
    /// an option does not meet it.
    /// </summary>
    Requirement,

    /// <summary>
    /// A hard limit on the solution space — an envelope, a mass budget, a
    /// temperature, an available process. An option that violates a
    /// constraint is not a worse option, it is not an option.
    /// </summary>
    Constraint,

    /// <summary>
    /// A basis on which admissible options genuinely differ and are
    /// compared. Criteria discriminate between options that are all
    /// allowed. They never eliminate.
    /// </summary>
    Criterion,

    /// <summary>
    /// Something desirable that is not a basis for rejection and carries
    /// less force than a criterion — a house style, a familiar supplier,
    /// a preferred material family. Recorded so the reasoning is honest
    /// about what inclined it.
    /// </summary>
    Preference
}

/// <summary>
/// How confident the study is in a statement that is not itself a
/// measured or referenced fact.
/// </summary>
/// <remarks>
/// An assumption is not evidence. Keeping the two apart is what lets a
/// later reader see which parts of a decision would need revisiting if
/// the world turned out differently.
/// </remarks>
public enum AssumptionConfidence
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Believed sound, and the study would not change if it were checked.</summary>
    Sound,

    /// <summary>Reasonable, but the study would want it confirmed before release.</summary>
    ToBeConfirmed,

    /// <summary>
    /// Load-bearing and unverified. The decision rests on it, and if it is
    /// wrong the decision is wrong.
    /// </summary>
    Critical
}

/// <summary>
/// How a risk stands at the time the study was recorded.
/// </summary>
public enum RiskStanding
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Identified, with nothing yet done about it.</summary>
    Open,

    /// <summary>Something is being done about it, and the study says what.</summary>
    Mitigated,

    /// <summary>
    /// Knowingly carried. An accepted risk needs a person's name against
    /// it: acceptance is an act of engineering authority, not a status.
    /// </summary>
    Accepted,

    /// <summary>Shown not to apply, and the study says why.</summary>
    Retired
}
