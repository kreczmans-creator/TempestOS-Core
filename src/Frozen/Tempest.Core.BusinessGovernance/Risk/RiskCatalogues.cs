using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Risk;

/// <summary>A deterministic filter over the business risk register.</summary>
public sealed record BusinessRiskQuery
{
    /// <summary>Matches any risk whose reference contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? ReferenceContains { get; init; }

    /// <summary>Matches any risk whose title, cause or consequence contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these categories. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<BusinessRiskCategory> Categories { get; init; } = [];

    /// <summary>Matches any of these residual exposure bands. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<RiskExposure> ResidualExposures { get; init; } = [];

    /// <summary>Matches risks owned by this principal. <see langword="null"/> to match any.</summary>
    public string? OwnerPrincipalId { get; init; }

    /// <summary>Matches open risks, closed risks, or either. <see langword="null"/> to match any.</summary>
    public bool? IsClosed { get; init; }

    /// <summary>Matches accepted risks, unaccepted risks, or either. <see langword="null"/> to match any.</summary>
    public bool? IsAccepted { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The organisation's own risk register.</summary>
public interface IBusinessRiskCatalog : IReferenceDataCatalog<BusinessRisk>
{
    /// <summary>Returns the risk registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<BusinessRisk>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered risk matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<BusinessRisk>>> SearchAsync(BusinessRiskQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IBusinessRiskCatalog"/> implementation.</summary>
public sealed class BusinessRiskCatalog : ReferenceDataCatalog<BusinessRisk>, IBusinessRiskCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every business-risk record's own backing document carries.</summary>
    /// <remarks>
    /// Deliberately not <c>Risk</c>: that Kind belongs to the engineering
    /// domain's own project risk. This is the organisation's register, and
    /// one value keeps one meaning.
    /// </remarks>
    public const string BusinessRiskDocumentKind = "BusinessRisk";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>riskId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessRisks.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each risk reference to the <c>riskId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessRisks.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="BusinessRiskCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own risk records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public BusinessRiskCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessRisks";

    /// <inheritdoc />
    public override string DocumentKind => BusinessRiskDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<BusinessRisk>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(BusinessRisk.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<BusinessRisk>>> SearchAsync(
        BusinessRiskQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(BusinessRisk definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(BusinessRisk definition) => $"Risk reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<BusinessRisk> record, BusinessRiskQuery query)
    {
        var risk = record.Definition;

        if (query.ReferenceContains is not null && !risk.Reference.Contains(query.ReferenceContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.TextContains is { } text
            && !risk.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
            && (risk.Cause is null || !risk.Cause.Contains(text, StringComparison.OrdinalIgnoreCase))
            && (risk.Consequence is null || !risk.Consequence.Contains(text, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.Categories.Count > 0 && !query.Categories.Contains(risk.Category))
            return false;

        if (query.ResidualExposures.Count > 0 && !query.ResidualExposures.Contains(risk.ResidualExposure))
            return false;

        if (query.OwnerPrincipalId is { } owner
            && !string.Equals(risk.Governance.Ownership.OwnerPrincipalId, owner, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.IsClosed is { } closed && risk.IsClosed != closed)
            return false;

        if (query.IsAccepted is { } accepted && risk.IsAccepted != accepted)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>A deterministic filter over the insurance library.</summary>
public sealed record InsurancePolicyQuery
{
    /// <summary>Matches any policy whose reference or policy number contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? ReferenceContains { get; init; }

    /// <summary>Matches policies written by this insurer, ignoring case. <see langword="null"/> to match any.</summary>
    public string? Insurer { get; init; }

    /// <summary>Matches policies holding a section of any of these types. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<InsuranceCoverageType> CoverageTypes { get; init; } = [];

    /// <summary>Matches any of these statuses. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<PolicyStatus> Statuses { get; init; } = [];

    /// <summary>Matches policies on cover on this date, by their own recorded period and status. <see langword="null"/> to match any.</summary>
    public DateOnly? OnCoverOn { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of insurance policies the organisation holds.</summary>
public interface IInsurancePolicyCatalog : IReferenceDataCatalog<InsurancePolicy>
{
    /// <summary>Returns the policy registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<InsurancePolicy>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered policy matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<InsurancePolicy>>> SearchAsync(InsurancePolicyQuery query, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IInsurancePolicyCatalog"/> implementation.</summary>
public sealed class InsurancePolicyCatalog : ReferenceDataCatalog<InsurancePolicy>, IInsurancePolicyCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every insurance-policy record's own backing document carries.</summary>
    public const string InsurancePolicyDocumentKind = "BusinessInsurancePolicy";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>policyId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessInsurancePolicies.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each policy reference to the <c>policyId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessInsurancePolicies.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="InsurancePolicyCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own policy records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public InsurancePolicyCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessInsurancePolicies";

    /// <inheritdoc />
    public override string DocumentKind => InsurancePolicyDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<InsurancePolicy>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(InsurancePolicy.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<InsurancePolicy>>> SearchAsync(
        InsurancePolicyQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(InsurancePolicy definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(InsurancePolicy definition) => $"Policy reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<InsurancePolicy> record, InsurancePolicyQuery query)
    {
        var policy = record.Definition;

        if (query.ReferenceContains is { } text
            && !policy.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !policy.PolicyNumber.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Insurer is { } insurer && !string.Equals(policy.Insurer, insurer, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.CoverageTypes.Count > 0 && !query.CoverageTypes.Any(policy.Covers))
            return false;

        if (query.Statuses.Count > 0 && !query.Statuses.Contains(policy.Status))
            return false;

        if (query.OnCoverOn is { } date && !policy.IsOnCoverOn(date))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}
