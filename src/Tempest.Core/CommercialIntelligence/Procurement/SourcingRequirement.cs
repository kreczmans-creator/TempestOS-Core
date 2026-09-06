using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.CommercialIntelligence.Procurement;

/// <summary>What a sourcing criterion is measuring.</summary>
public enum SourcingCriterionKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>What it costs.</summary>
    Cost,

    /// <summary>How long it takes.</summary>
    LeadTime,

    /// <summary>Whether the supplier can do the work at all.</summary>
    Capability,

    /// <summary>Approvals, certifications and the quality system behind them.</summary>
    Quality,

    /// <summary>Whether the supplier delivers what it promised, historically.</summary>
    Reliability,

    /// <summary>Where the supplier is, and what that costs in carriage, duty and difficulty.</summary>
    Geography,

    /// <summary>Concentration, single-sourcing, financial standing, anything else that could go wrong.</summary>
    Risk,

    /// <summary>Something the organisation cares about that none of the above covers.</summary>
    Other
}

/// <summary>Whether a criterion eliminates a candidate or merely ranks it.</summary>
/// <remarks>
/// The distinction that keeps a weighted score honest. A mandatory
/// criterion cannot be traded away by scoring well elsewhere: a supplier
/// without the approval the customer requires is not a cheap option, it
/// is not an option. Weighted criteria discriminate between the
/// candidates that remain.
/// </remarks>
public enum SourcingCriterionRole
{
    /// <summary>Must be met. Failing it removes the candidate from consideration.</summary>
    Mandatory,

    /// <summary>Ranks the candidates that meet every mandatory criterion.</summary>
    Weighted,

    /// <summary>Recorded and reported, but neither eliminates nor scores.</summary>
    Informational
}

/// <summary>One thing the organisation is judging candidates on.</summary>
/// <param name="Code">The criterion's own identifier within the requirement. Required.</param>
/// <param name="Kind">What it measures.</param>
/// <param name="Statement">What is being required or preferred, in plain words. Required.</param>
/// <param name="Role">Whether it eliminates or ranks.</param>
/// <param name="Weight">Its share of the weighted score, where <paramref name="Role"/> is <see cref="SourcingCriterionRole.Weighted"/>.</param>
/// <param name="Rationale">Why the organisation cares. <see langword="null"/> where nobody said.</param>
public sealed record SourcingCriterion(
    string Code,
    SourcingCriterionKind Kind,
    string Statement,
    SourcingCriterionRole Role = SourcingCriterionRole.Weighted,
    decimal Weight = 0m,
    string? Rationale = null)
{
    /// <summary>The criterion's own identifier within the requirement.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A sourcing criterion must carry its own code.", nameof(Code))
        : Code.Trim();

    /// <summary>What is being required or preferred.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A sourcing criterion must say what is being required or preferred.", nameof(Statement))
        : Statement.Trim();

    /// <summary>Its share of the weighted score.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Weight"/> is negative.</exception>
    public decimal Weight { get; } = Weight < 0m
        ? throw new ArgumentOutOfRangeException(nameof(Weight), Weight, "A criterion's weight cannot be negative.")
        : Weight;

    /// <summary>Whether failing this criterion removes a candidate from consideration.</summary>
    public bool IsEliminating => Role == SourcingCriterionRole.Mandatory;

    /// <summary>Whether it contributes to the weighted score.</summary>
    public bool IsScoring => Role == SourcingCriterionRole.Weighted && Weight > 0m;
}

/// <summary>
/// What the organisation needs sourced, and how it intends to judge the
/// candidates.
/// </summary>
/// <remarks>
/// <para>
/// Written before the candidates are assessed, and governed as a record
/// in its own right, so that the criteria cannot be quietly reshaped
/// around the answer somebody wanted. A requirement whose weights changed
/// after the assessments were scored is a requirement whose history says
/// so.
/// </para>
/// <para>
/// The requirement states what matters. It does not select anybody:
/// selection is `D5`'s recommendation, and awarding the work is a
/// procurement act TempestOS does not perform (`ADR-0135`).
/// </para>
/// </remarks>
public sealed record SourcingRequirement
{
    /// <summary>The reference the requirement is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What is to be sourced. Required.</summary>
    public required string Subject { get; init; }

    /// <summary>The criteria candidates are judged on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<SourcingCriterion> Criteria { get; init; } = [];

    /// <summary>The quantity sought. <see langword="null"/> where it is open.</summary>
    public QuantityBand? Quantity { get; init; }

    /// <summary>The currency figures are to be compared in. <see langword="null"/> where nobody said.</summary>
    /// <remarks>
    /// Stated because candidates quoting in different currencies cannot be
    /// ranked on cost, and TempestOS will not convert to make them
    /// look comparable.
    /// </remarks>
    public CurrencyCode? ComparisonCurrency { get; init; }

    /// <summary>The date by which delivery is needed. <see langword="null"/> where none is fixed.</summary>
    public DateOnly? RequiredBy { get; init; }

    /// <summary>Who raised the requirement. <see langword="null"/> where unrecorded.</summary>
    public string? RaisedByPrincipalId { get; init; }

    /// <summary>When it was raised. <see langword="null"/> where unrecorded.</summary>
    public DateOnly? RaisedOn { get; init; }

    /// <summary>Anything the requirement rules out from the start, and why. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Constraints { get; init; } = [];

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The criteria that eliminate.</summary>
    public IEnumerable<SourcingCriterion> MandatoryCriteria => Criteria.Where(c => c.IsEliminating);

    /// <summary>The criteria that rank.</summary>
    public IEnumerable<SourcingCriterion> WeightedCriteria => Criteria.Where(c => c.IsScoring);

    /// <summary>The weights added up, for checking they mean what the reader thinks.</summary>
    public decimal TotalWeight => WeightedCriteria.Sum(c => c.Weight);

    /// <summary>The criterion carrying <paramref name="code"/>, or <see langword="null"/> where none does.</summary>
    public SourcingCriterion? FindCriterion(string code) =>
        Criteria.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}
