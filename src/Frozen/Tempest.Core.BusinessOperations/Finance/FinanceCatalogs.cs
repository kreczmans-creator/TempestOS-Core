using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations.Finance;

// WP 18.0C (D-028): split out of the live
// src/Tempest.Core/BusinessOperations/Finance/FinanceCatalogs.cs the same day
// BudgetCatalog stayed live.

/// <summary>The commitments, accruals and actuals recorded against those budgets.</summary>
public interface IFinancialEntryCatalog : IReferenceDataCatalog<FinancialEntry>
{
    /// <summary>Returns the entry registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<FinancialEntry>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every entry drawing on <paramref name="budgetReference"/>. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="budgetReference"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<IReferenceRecord<FinancialEntry>>> FindForBudgetAsync(string budgetReference, CancellationToken cancellationToken = default);

    /// <summary>Every entry that names no budget. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IReferenceRecord<FinancialEntry>>> FindUnallocatedAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IFinancialEntryCatalog"/> implementation.</summary>
public sealed class FinancialEntryCatalog : ReferenceDataCatalog<FinancialEntry>, IFinancialEntryCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every entry's own backing document carries.</summary>
    public const string FinancialEntryDocumentKind = "BusinessFinancialEntry";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string FinancialEntryLibraryName = "BusinessFinancialEntries";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>entryId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessFinancialEntries.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each reference to the <c>entryId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessFinancialEntries.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="FinancialEntryCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public FinancialEntryCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => FinancialEntryLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => FinancialEntryDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<FinancialEntry>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(FinancialEntry.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<FinancialEntry>>> FindForBudgetAsync(
        string budgetReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(budgetReference);

        var budget = budgetReference.Trim();

        return FilterAsync(
            record => string.Equals(record.Definition.BudgetReference, budget, StringComparison.OrdinalIgnoreCase),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<FinancialEntry>>> FindUnallocatedAsync(CancellationToken cancellationToken = default) =>
        FilterAsync(record => !record.Definition.IsAllocated, cancellationToken);

    /// <inheritdoc />
    protected override string? GetSecondaryKey(FinancialEntry definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(FinancialEntry definition) => $"Financial entry reference '{definition.Reference}'";
}

/// <summary>The diagnostic codes WP04.3's validation reports.</summary>
public static class FinanceValidationRules
{
    /// <summary>The budget has no lines, so it sets nothing aside.</summary>
    public const string BudgetHasNoLines = "TEMPEST-BOF-001";

    /// <summary>A line is stated in a currency other than the budget's.</summary>
    public const string LineCurrencyMismatch = "TEMPEST-BOF-002";

    /// <summary>Two lines share one reference.</summary>
    public const string DuplicateLineReference = "TEMPEST-BOF-003";

    /// <summary>The budget names nobody who set it.</summary>
    /// <remarks>
    /// An error. Setting a budget commits the business to spending, and
    /// one nobody is recorded as having set is a commitment nobody is
    /// accountable for.
    /// </remarks>
    public const string BudgetHasNoAuthority = "TEMPEST-BOF-004";

    /// <summary>The budget covers no period, so nobody can say when it applies.</summary>
    public const string BudgetHasNoPeriod = "TEMPEST-BOF-005";

    /// <summary>An entry names a budget the library does not hold.</summary>
    public const string BudgetMustResolve = "TEMPEST-BOF-006";

    /// <summary>An entry names a budget line the budget does not hold.</summary>
    public const string BudgetLineMustResolve = "TEMPEST-BOF-007";

    /// <summary>An entry is stated in a currency the budget is not.</summary>
    public const string EntryCurrencyMismatch = "TEMPEST-BOF-008";

    /// <summary>An entry names no budget, so nothing measures it.</summary>
    public const string EntryIsUnallocated = "TEMPEST-BOF-009";

    /// <summary>An entry recording money that has moved carries no date.</summary>
    public const string ActualHasNoDate = "TEMPEST-BOF-010";

    /// <summary>More has been promised against the budget than it holds.</summary>
    public const string BudgetIsOvercommitted = "TEMPEST-BOF-011";

    /// <summary>More has been paid against the budget than it holds.</summary>
    public const string BudgetIsOverspent = "TEMPEST-BOF-012";

    /// <summary>An entry names a party that resolves to no governed record.</summary>
    public const string PartyIsUnresolved = "TEMPEST-BOF-013";
}

/// <summary>Governance of the budget library.</summary>
public interface IBudgetValidationService : IReferenceValidationService<Budget>
{
}

/// <summary>The concrete <see cref="IBudgetValidationService"/> implementation.</summary>
public sealed class BudgetValidationService : ReferenceValidationService<Budget>, IBudgetValidationService
{
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="BudgetValidationService"/> class.</summary>
    /// <param name="catalog">The budget library whose records this service validates.</param>
    /// <param name="timeProvider">The clock date checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public BudgetValidationService(IBudgetCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        Budget definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Budget '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.Lines.Count == 0)
            errors.Add(OperationalValidation.Diagnostic(
                FinanceValidationRules.BudgetHasNoLines,
                $"{subject} has no lines, so it sets nothing aside."));

        OperationalValidation.EvaluateDuplicateReferences(
            definition.Lines.Select(l => l.Reference),
            $"{subject} has two lines sharing the reference",
            errors);

        foreach (var line in definition.Lines.Where(l => l.Amount.Currency != definition.Currency))
            errors.Add(OperationalValidation.Diagnostic(
                FinanceValidationRules.LineCurrencyMismatch,
                $"{subject} line '{line.Reference}' is stated in {line.Amount.Currency} but the budget is in "
                + $"{definition.Currency}. TempestOS does not convert currencies."));

        if (!definition.IsAuthorised)
            errors.Add(OperationalValidation.Diagnostic(
                FinanceValidationRules.BudgetHasNoAuthority,
                $"{subject} names nobody who set it. Setting a budget commits the business to spending, and one nobody "
                + "is recorded as having set is a commitment nobody is accountable for."));

        if (definition.Period is null)
            warnings.Add(OperationalValidation.Diagnostic(
                FinanceValidationRules.BudgetHasNoPeriod,
                $"{subject} covers no period, so nobody can say when it applies or when it lapses."));

        OperationalValidation.Evaluate(definition.Facts, subject, today, errors, warnings, requireDueDate: false);

        return Task.CompletedTask;
    }
}

/// <summary>Answers how a budget stands.</summary>
public interface IBudgetPositionService
{
    /// <summary>
    /// What was allowed, what is promised, what has gone, and what is
    /// left.
    /// </summary>
    /// <remarks>
    /// Read-only and deterministic. It totals what the entries say and
    /// changes nothing — in particular it never reallocates an entry or
    /// closes a budget.
    /// </remarks>
    /// <param name="budgetReference">The budget to position.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The position, or <see langword="null"/> where no such budget is registered.</returns>
    /// <exception cref="ArgumentException"><paramref name="budgetReference"/> is null, empty, or whitespace.</exception>
    Task<BudgetPosition?> PositionAsync(string budgetReference, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IBudgetPositionService"/> implementation.</summary>
/// <remarks>
/// Entries stated in a currency other than the budget's are **excluded
/// and counted**, never converted. A position that silently dropped them
/// would understate the spend; one that converted them would invent a
/// rate. The count surfaces through validation instead.
/// </remarks>
public sealed class BudgetPositionService : IBudgetPositionService
{
    private readonly IBudgetCatalog _budgets;
    private readonly IFinancialEntryCatalog _entries;

    /// <summary>Initialises a new instance of the <see cref="BudgetPositionService"/> class.</summary>
    /// <param name="budgets">The budget library.</param>
    /// <param name="entries">The entry library.</param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public BudgetPositionService(IBudgetCatalog budgets, IFinancialEntryCatalog entries)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(entries);

        _budgets = budgets;
        _entries = entries;
    }

    /// <inheritdoc />
    public async Task<BudgetPosition?> PositionAsync(string budgetReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(budgetReference);

        var record = await _budgets.FindByReferenceAsync(budgetReference, cancellationToken).ConfigureAwait(false);

        if (record is null)
            return null;

        var budget = record.Definition;
        var entries = await _entries.FindForBudgetAsync(budget.Reference, cancellationToken).ConfigureAwait(false);

        var comparable = entries
            .Select(e => e.Definition)
            .Where(e => e.Amount.Currency == budget.Currency && e.Direction == CashDirection.Outgoing)
            .ToList();

        var zero = new Money(0m, budget.Currency);

        // Committed includes everything promised away, whatever stage it
        // has reached: an accrual and a payment were both commitments
        // first, and counting only entries still marked Committed would
        // show the budget refilling as invoices arrive.
        var committed = comparable
            .Where(e => e.Posture is FinancialPosture.Committed or FinancialPosture.Accrued or FinancialPosture.Actual)
            .Aggregate(zero, (running, e) => running + e.Amount);

        var actual = comparable
            .Where(e => e.Posture == FinancialPosture.Actual)
            .Aggregate(zero, (running, e) => running + e.Amount);

        return new BudgetPosition(
            budget.Reference,
            budget.Currency,
            budget.TotalOutgoing,
            committed,
            actual,
            entries.Count(e => e.Definition.BudgetLineReference is null));
    }
}
