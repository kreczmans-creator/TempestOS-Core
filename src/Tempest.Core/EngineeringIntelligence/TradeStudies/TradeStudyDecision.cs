namespace Tempest.Core.EngineeringIntelligence.TradeStudies;

/// <summary>
/// An eliminating consideration that the decision-maker knowingly went
/// against.
/// </summary>
/// <remarks>
/// Overriding a constraint is a legitimate and common engineering act —
/// the constraint was wrong, or the requirement was relaxed, or the risk
/// is acceptable. What is not legitimate is doing it silently. An override
/// names the consideration, the person, and the reason, and it stays in
/// the record.
/// </remarks>
/// <param name="ConsiderationCode">The consideration being overridden. Required.</param>
/// <param name="Reason">Why the decision proceeds despite it. Required.</param>
/// <param name="AuthorisedByPrincipalId">Who authorised the override. Required.</param>
public sealed record ConsiderationOverride(
    string ConsiderationCode,
    string Reason,
    string AuthorisedByPrincipalId)
{
    /// <summary>The consideration being overridden.</summary>
    public string ConsiderationCode { get; } = string.IsNullOrWhiteSpace(ConsiderationCode)
        ? throw new ArgumentException("An override must name the consideration it overrides.", nameof(ConsiderationCode))
        : ConsiderationCode.Trim();

    /// <summary>Why the decision proceeds despite it.</summary>
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException("An override must say why the decision proceeds despite the consideration.", nameof(Reason))
        : Reason.Trim();

    /// <summary>Who authorised the override.</summary>
    public string AuthorisedByPrincipalId { get; } = string.IsNullOrWhiteSpace(AuthorisedByPrincipalId)
        ? throw new ArgumentException("An override must name the person who authorised it.", nameof(AuthorisedByPrincipalId))
        : AuthorisedByPrincipalId.Trim();
}

/// <summary>
/// The engineering decision a trade study led to.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing in TempestOS produces one of these.</b> There is no method
/// anywhere in `P02` that reads a set of judgements and returns a
/// decision, and that absence is the design. The framework narrows,
/// evidences and records; a person chooses and signs.
/// </para>
/// <para>
/// Every field that makes the decision attributable is required:
/// <see cref="SelectedOptionCode"/>, <see cref="Rationale"/> and
/// <see cref="DecidedByPrincipalId"/>. A decision without a stated reason
/// is not recordable here, because a trade study whose reasoning was not
/// written down has not been done — it has been concluded.
/// </para>
/// </remarks>
/// <param name="SelectedOptionCode">The option chosen. Required.</param>
/// <param name="Rationale">Why this option, in the decision-maker's own words. Required.</param>
/// <param name="DecidedByPrincipalId">Who decided. Required.</param>
/// <param name="DecidedAt">When they decided.</param>
/// <param name="RejectedOptionReasons">Why each option not chosen was not chosen, keyed by option code. Never <see langword="null"/>.</param>
/// <param name="Overrides">Eliminating considerations the decision knowingly went against. Never <see langword="null"/>.</param>
/// <param name="AcceptedRiskCodes">Risks the decision-maker is knowingly carrying. Never <see langword="null"/>.</param>
/// <param name="OutstandingWork">What still has to be done before the decision can be relied upon. <see langword="null"/> if nothing.</param>
/// <param name="Dissent">Any recorded disagreement with the decision. <see langword="null"/> if none was raised.</param>
public sealed record TradeStudyDecision(
    string SelectedOptionCode,
    string Rationale,
    string DecidedByPrincipalId,
    DateTimeOffset DecidedAt,
    IReadOnlyDictionary<string, string>? RejectedOptionReasons = null,
    IReadOnlyList<ConsiderationOverride>? Overrides = null,
    IReadOnlyList<string>? AcceptedRiskCodes = null,
    string? OutstandingWork = null,
    string? Dissent = null)
{
    /// <summary>The option chosen.</summary>
    public string SelectedOptionCode { get; } = string.IsNullOrWhiteSpace(SelectedOptionCode)
        ? throw new ArgumentException("A decision must name the option chosen.", nameof(SelectedOptionCode))
        : SelectedOptionCode.Trim();

    /// <summary>Why this option, in the decision-maker's own words.</summary>
    public string Rationale { get; } = string.IsNullOrWhiteSpace(Rationale)
        ? throw new ArgumentException(
            "A trade-study decision must record why the option was chosen. A decision without a stated reason cannot be reviewed, "
            + "reproduced, or revisited, and is not recordable.",
            nameof(Rationale))
        : Rationale.Trim();

    /// <summary>Who decided.</summary>
    public string DecidedByPrincipalId { get; } = string.IsNullOrWhiteSpace(DecidedByPrincipalId)
        ? throw new ArgumentException(
            "A trade-study decision must name the person who took it. TempestOS does not take engineering decisions.",
            nameof(DecidedByPrincipalId))
        : DecidedByPrincipalId.Trim();

    /// <summary>Why each option not chosen was not chosen, keyed by option code.</summary>
    public IReadOnlyDictionary<string, string> RejectedOptionReasons { get; init; } =
        RejectedOptionReasons ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Eliminating considerations the decision knowingly went against.</summary>
    public IReadOnlyList<ConsiderationOverride> Overrides { get; init; } = Overrides ?? [];

    /// <summary>Risks the decision-maker is knowingly carrying.</summary>
    public IReadOnlyList<string> AcceptedRiskCodes { get; init; } = AcceptedRiskCodes ?? [];

    /// <summary>Whether the decision-maker went against an eliminating consideration.</summary>
    public bool HasOverrides => Overrides.Count > 0;

    /// <summary>Returns the override against <paramref name="considerationCode"/>, or <see langword="null"/> if the decision did not override it.</summary>
    public ConsiderationOverride? FindOverride(string considerationCode) =>
        Overrides.FirstOrDefault(o => string.Equals(o.ConsiderationCode, considerationCode, StringComparison.OrdinalIgnoreCase));
}
