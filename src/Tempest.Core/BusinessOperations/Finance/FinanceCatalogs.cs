using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations.Finance;

// WP 18.0C (D-028): IFinancialEntryCatalog/FinancialEntryCatalog,
// FinanceValidationRules, IBudgetValidationService/BudgetValidationService
// and IBudgetPositionService/BudgetPositionService moved to
// src/Frozen/Tempest.Core.BusinessOperations/Finance/FinanceCatalogs.cs the
// same day; only Budget.cs and BudgetCatalog were named to keep, and the
// validation/position services over FinancialEntry cannot function once
// it is gone.

/// <summary>The budget library.</summary>
public interface IBudgetCatalog : IReferenceDataCatalog<Budget>
{
    /// <summary>Returns the budget registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<Budget>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>The budgets in force on <paramref name="asAt"/>. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IReferenceRecord<Budget>>> FindCurrentAsync(DateOnly asAt, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IBudgetCatalog"/> implementation.</summary>
public sealed class BudgetCatalog : ReferenceDataCatalog<Budget>, IBudgetCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every budget's own backing document carries.</summary>
    public const string BudgetDocumentKind = "BusinessBudget";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string BudgetLibraryName = "BusinessBudgets";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>budgetId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessBudgets.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each reference to the <c>budgetId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessBudgets.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="BudgetCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public BudgetCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => BudgetLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => BudgetDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<Budget>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(Budget.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<Budget>>> FindCurrentAsync(DateOnly asAt, CancellationToken cancellationToken = default) =>
        FilterAsync(record => record.Definition.IsCurrentAt(asAt), cancellationToken);

    /// <inheritdoc />
    protected override string? GetSecondaryKey(Budget definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(Budget definition) => $"Budget reference '{definition.Reference}'";
}
