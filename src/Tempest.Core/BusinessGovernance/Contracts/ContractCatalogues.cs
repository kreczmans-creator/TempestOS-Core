using Tempest.Core.EngineeringData;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Contracts;

/// <summary>A deterministic filter over the contract-template library.</summary>
public sealed record ContractTemplateQuery
{
    /// <summary>Matches any template whose code contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? CodeContains { get; init; }

    /// <summary>Matches any template whose name or purpose contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches templates containing a clause in every one of these categories. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ClauseCategory> MustCover { get; init; } = [];

    /// <summary>Matches templates in any of these legal-review states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<DeterminationState> LegalReviewStates { get; init; } = [];

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of controlled contract templates.</summary>
public interface IContractTemplateCatalog : IReferenceDataCatalog<ContractTemplate>
{
    /// <summary>Returns the template registered under <paramref name="code"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="code"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<ContractTemplate>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Every registered template matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<ContractTemplate>>> SearchAsync(
        ContractTemplateQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IContractTemplateCatalog"/> implementation.</summary>
public sealed class ContractTemplateCatalog : ReferenceDataCatalog<ContractTemplate>, IContractTemplateCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every contract-template record's own backing document carries.</summary>
    public const string ContractTemplateDocumentKind = "BusinessContractTemplate";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>templateId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessContractTemplates.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each template code to the <c>templateId</c> holding it.</summary>
    public const string CodeIndexCollection = "BusinessContractTemplates.CodeIndex";

    /// <summary>Initialises a new instance of the <see cref="ContractTemplateCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own template records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public ContractTemplateCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessContractTemplates";

    /// <inheritdoc />
    public override string DocumentKind => ContractTemplateDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => CodeIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<ContractTemplate>?> FindByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(ContractTemplate.CodeKeyFor(code), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<ContractTemplate>>> SearchAsync(
        ContractTemplateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(ContractTemplate definition) => definition.CodeKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(ContractTemplate definition) => $"Contract template code '{definition.Code}'";

    private static bool Matches(IReferenceRecord<ContractTemplate> record, ContractTemplateQuery query)
    {
        var template = record.Definition;

        if (query.CodeContains is not null && !template.Code.Contains(query.CodeContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.TextContains is { } text
            && !template.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !template.Purpose.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.MustCover.Count > 0 && !query.MustCover.All(template.Covers))
            return false;

        if (query.LegalReviewStates.Count > 0 && !query.LegalReviewStates.Contains(template.LegalReviewState))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>A deterministic filter over the issued-contract library.</summary>
public sealed record IssuedContractQuery
{
    /// <summary>Matches any contract whose reference contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? ReferenceContains { get; init; }

    /// <summary>Matches any contract naming a party whose legal name contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? PartyNameContains { get; init; }

    /// <summary>Matches any of these commercial statuses. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ContractStatus> Statuses { get; init; } = [];

    /// <summary>Matches contracts drawn from the template with this code. <see langword="null"/> to match any.</summary>
    public string? TemplateCode { get; init; }

    /// <summary>Matches contracts in force on this date. <see langword="null"/> to match any.</summary>
    public DateOnly? InForceOn { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of contracts the organisation has issued or entered into.</summary>
public interface IIssuedContractCatalog : IReferenceDataCatalog<IssuedContract>
{
    /// <summary>Returns the contract registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<IssuedContract>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered contract matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<IssuedContract>>> SearchAsync(
        IssuedContractQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IIssuedContractCatalog"/> implementation.</summary>
/// <remarks>
/// An issued contract is a governed record for the same reasons a template
/// is — it is authored, evidenced, approved, revisioned and superseded —
/// so it uses the shared lifecycle rather than a store of its own. Its
/// commercial position lives in <see cref="IssuedContract.Status"/>, which
/// is a separate axis from the record's own validation state.
/// </remarks>
public sealed class IssuedContractCatalog : ReferenceDataCatalog<IssuedContract>, IIssuedContractCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every issued-contract record's own backing document carries.</summary>
    public const string IssuedContractDocumentKind = "BusinessContract";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>contractId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessContracts.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each contract reference to the <c>contractId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessContracts.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="IssuedContractCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own contract records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public IssuedContractCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessContracts";

    /// <inheritdoc />
    public override string DocumentKind => IssuedContractDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<IssuedContract>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(IssuedContract.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<IssuedContract>>> SearchAsync(
        IssuedContractQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(IssuedContract definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(IssuedContract definition) => $"Contract reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<IssuedContract> record, IssuedContractQuery query)
    {
        var contract = record.Definition;

        if (query.ReferenceContains is not null
            && !contract.Reference.Contains(query.ReferenceContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.PartyNameContains is { } party
            && !contract.Parties.Any(p => p.LegalName.Contains(party, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.Statuses.Count > 0 && !query.Statuses.Contains(contract.Status))
            return false;

        if (query.TemplateCode is { } templateCode
            && !string.Equals(contract.TemplatePin?.RecordId, templateCode, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.InForceOn is { } date && !(contract.Term?.Contains(date) ?? false))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}
