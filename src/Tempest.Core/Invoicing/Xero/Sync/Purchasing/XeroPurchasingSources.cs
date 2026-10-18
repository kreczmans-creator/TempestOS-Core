using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Core.PurchaseOrders;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

// ============================================================================
// `v0.24.0` task X5 (D5) — what a TempestOS purchase order and a project
// expense look like to the Xero sync (`docs/releases/v0.24.0/Xero Technical
// Design.md` §3, §4.3, §4.4). The planners and push handlers read them only
// through `IXeroPurchaseOrderSource` / `IXeroExpenseSource`, as plain-data
// snapshots — so they never touch the domain model's mutators, and tests
// can drive them without a workspace.
// ============================================================================

/// <summary>One line of a purchase order, as the Xero sync reads it.</summary>
/// <param name="Description">What the line is.</param>
/// <param name="Quantity">How many units.</param>
/// <param name="UnitPrice">The net price of one unit.</param>
/// <param name="VatRate">The line's VAT treatment (mapped to an input tax type, X1).</param>
public sealed record XeroPurchaseOrderLine(string Description, decimal Quantity, decimal UnitPrice, VatRate VatRate);

/// <summary>A TempestOS purchase order, as the Xero sync reads it.</summary>
/// <param name="Id">The order's id (its <see cref="XeroDocumentRef"/> key).</param>
/// <param name="Reference">The order's reference — Xero's <c>PurchaseOrderNumber</c>.</param>
/// <param name="Status">The order's status.</param>
/// <param name="ProjectCode">The project's code — Xero's <c>Reference</c>; <see langword="null"/> when the project is not found.</param>
/// <param name="SupplierOrganisationReference">The supplier's <c>Organisation.Reference</c> (resolved from the order's catalogue id); <see langword="null"/> when the order names none, or a supplier TempestOS does not know.</param>
/// <param name="IssuedDate">When it was issued; <see langword="null"/> while Draft, or for an order cancelled before issue.</param>
/// <param name="ExpectedDelivery">When the goods are expected; <see langword="null"/> when not stated.</param>
/// <param name="CurrencyCode">The order's currency (ISO 4217).</param>
/// <param name="Lines">The order's lines, in order.</param>
public sealed record XeroPurchaseOrderSnapshot(
    Guid Id,
    string Reference,
    PurchaseOrderStatus Status,
    string? ProjectCode,
    string? SupplierOrganisationReference,
    DateOnly? IssuedDate,
    DateOnly? ExpectedDelivery,
    string CurrencyCode,
    IReadOnlyList<XeroPurchaseOrderLine> Lines)
{
    /// <summary>Whether the order has been issued to its supplier at some point — Issued, Received or Closed, or Cancelled after issue. Only an issued order goes to Xero (§4.3).</summary>
    public bool WasIssued => IssuedDate is not null && Status != PurchaseOrderStatus.Draft;
}

/// <summary>A TempestOS project expense, as the Xero sync reads it.</summary>
/// <param name="Id">The expense's id (its <see cref="XeroDocumentRef"/> key).</param>
/// <param name="ProjectCode">The project's code, for the bill line's description; <see langword="null"/> when the project is not found.</param>
/// <param name="Date">The day the expense was incurred — the bill date.</param>
/// <param name="Description">What the expense was.</param>
/// <param name="Category">What it was for — which Xero account it posts to (X1).</param>
/// <param name="NetAmount">The net amount, as the receipt states it.</param>
/// <param name="VatAmount">The VAT amount, as the receipt states it — sent as the line's <c>TaxAmount</c>, never recomputed.</param>
/// <param name="CurrencyCode">The expense's currency (ISO 4217).</param>
/// <param name="SupplierOrganisationReference">The supplier's <c>Organisation.Reference</c>; <see langword="null"/> when the expense names none (the "General expenses" contact is then used, Q3).</param>
/// <param name="SupplierOrganisationIdUnresolved">The expense's supplier catalogue id when it names one TempestOS cannot find; <see langword="null"/> otherwise.</param>
/// <param name="SupplierInvoiceNumber">The supplier's own invoice number; <see langword="null"/> when none (the bill is then numbered <c>EXP-{id}</c>, Q4).</param>
/// <param name="SourcePurchaseOrderId">The purchase order whose received lines the expense was recorded from (Q6); <see langword="null"/> for an expense entered by hand.</param>
/// <param name="IsDeleted">Whether the expense was deleted in TempestOS.</param>
/// <param name="RecordedAtUtc">When the expense was recorded in TempestOS; <see langword="null"/> when not known (treated as recorded before Xero sync began).</param>
public sealed record XeroExpenseSnapshot(
    Guid Id,
    string? ProjectCode,
    DateOnly Date,
    string Description,
    ExpenseCategory Category,
    decimal NetAmount,
    decimal VatAmount,
    string CurrencyCode,
    string? SupplierOrganisationReference,
    string? SupplierOrganisationIdUnresolved,
    string? SupplierInvoiceNumber,
    Guid? SourcePurchaseOrderId,
    bool IsDeleted,
    DateTimeOffset? RecordedAtUtc = null);

/// <summary>Reads purchase orders for the Xero sync. No network.</summary>
public interface IXeroPurchaseOrderSource
{
    /// <summary>The purchase order <paramref name="purchaseOrderId"/>, or <see langword="null"/> when there is none.</summary>
    Task<XeroPurchaseOrderSnapshot?> FindAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default);

    /// <summary>Every purchase order's id, for the start-up and Refresh scan (§6.2).</summary>
    Task<IReadOnlyList<Guid>> ListIdsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Reads project expenses for the Xero sync. No network.</summary>
public interface IXeroExpenseSource
{
    /// <summary>The expense <paramref name="expenseId"/> (deleted ones included, flagged), or <see langword="null"/> when there is none.</summary>
    Task<XeroExpenseSnapshot?> FindAsync(Guid expenseId, CancellationToken cancellationToken = default);

    /// <summary>Every expense's id, deleted ones included, for the start-up and Refresh scan (§6.2).</summary>
    Task<IReadOnlyList<Guid>> ListIdsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="IXeroPurchaseOrderSource"/> over the engineering domain's
/// repository: <see cref="PurchaseOrder"/>s, their project's code, and the
/// supplier's <c>Organisation.Reference</c> from the customers and
/// suppliers catalogue.
/// </summary>
public sealed class DomainXeroPurchaseOrderSource : IXeroPurchaseOrderSource
{
    private readonly EngineeringDomainContext _domain;
    private readonly IOrganisationCatalog _organisations;

    /// <summary>Initialises a new instance of the <see cref="DomainXeroPurchaseOrderSource"/> class.</summary>
    /// <param name="domain">The engineering domain (its repository holds purchase orders and projects).</param>
    /// <param name="organisations">The customers and suppliers.</param>
    public DomainXeroPurchaseOrderSource(EngineeringDomainContext domain, IOrganisationCatalog organisations)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(organisations);

        _domain = domain;
        _organisations = organisations;
    }

    /// <inheritdoc />
    public async Task<XeroPurchaseOrderSnapshot?> FindAsync(Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        if (await _domain.Repository.FindAsync(purchaseOrderId, cancellationToken).ConfigureAwait(false) is not PurchaseOrder order)
            return null;

        var projectCode = await XeroPurchasingDomain.ProjectCodeAsync(_domain, order.ParentId, cancellationToken).ConfigureAwait(false);
        var (supplier, _) = await XeroPurchasingDomain.SupplierReferenceAsync(_organisations, order.SupplierOrganisationId, cancellationToken).ConfigureAwait(false);

        return new XeroPurchaseOrderSnapshot(
            order.Id,
            order.Reference,
            order.Status,
            projectCode,
            supplier,
            order.IssuedDate,
            order.ExpectedDelivery,
            XeroPurchasingDomain.Currency(order.Currency),
            [.. order.Lines.Select(l => new XeroPurchaseOrderLine(l.Description, l.Quantity, l.UnitPrice.Amount, l.VatRate))]);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListIdsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _domain.Repository.ListByKindAsync(PurchaseOrder.CanonicalKind, cancellationToken).ConfigureAwait(false);
        return [.. entries.Select(e => e.Id)];
    }
}

/// <summary>
/// <see cref="IXeroExpenseSource"/> over the engineering domain's
/// repository: <see cref="ProjectExpense"/>s (deleted ones flagged), their
/// project's code, and the supplier's <c>Organisation.Reference</c>.
/// </summary>
public sealed class DomainXeroExpenseSource : IXeroExpenseSource
{
    private readonly EngineeringDomainContext _domain;
    private readonly IOrganisationCatalog _organisations;

    /// <summary>Initialises a new instance of the <see cref="DomainXeroExpenseSource"/> class.</summary>
    /// <param name="domain">The engineering domain (its repository holds expenses and projects).</param>
    /// <param name="organisations">The customers and suppliers.</param>
    public DomainXeroExpenseSource(EngineeringDomainContext domain, IOrganisationCatalog organisations)
    {
        ArgumentNullException.ThrowIfNull(domain);
        ArgumentNullException.ThrowIfNull(organisations);

        _domain = domain;
        _organisations = organisations;
    }

    /// <inheritdoc />
    public async Task<XeroExpenseSnapshot?> FindAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        if (await _domain.Repository.FindAsync(expenseId, cancellationToken).ConfigureAwait(false) is not ProjectExpense expense)
            return null;

        var projectCode = await XeroPurchasingDomain.ProjectCodeAsync(_domain, expense.ProjectId, cancellationToken).ConfigureAwait(false);
        var (supplier, unresolved) = await XeroPurchasingDomain.SupplierReferenceAsync(_organisations, expense.SupplierOrganisationId, cancellationToken).ConfigureAwait(false);

        return new XeroExpenseSnapshot(
            expense.Id,
            projectCode,
            expense.Date,
            expense.Description,
            expense.Category,
            expense.NetAmount.Amount,
            expense.VatAmount.Amount,
            XeroPurchasingDomain.Currency(expense.NetAmount.Currency),
            supplier,
            unresolved,
            expense.SupplierInvoiceNumber,
            expense.SourcePurchaseOrderId,
            expense is IDeletable { IsDeleted: true },
            expense.CreatedAt);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListIdsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _domain.Repository.ListByKindAsync(ProjectExpense.CanonicalKind, cancellationToken).ConfigureAwait(false);
        return [.. entries.Select(e => e.Id)];
    }
}

/// <summary>Domain look-ups the two sources share.</summary>
internal static class XeroPurchasingDomain
{
    /// <summary>The ISO 4217 code Xero is sent for <paramref name="currency"/>; GBP when unspecified (the platform's own default).</summary>
    public static string Currency(CurrencyCode currency) =>
        currency.IsSpecified ? currency.ToString().ToUpperInvariant() : CurrencyCode.Gbp.ToString();

    /// <summary>The code of the project <paramref name="projectId"/> (its identifier, else its name); <see langword="null"/> when none.</summary>
    public static async Task<string?> ProjectCodeAsync(EngineeringDomainContext domain, Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is not { } id || await domain.Repository.FindAsync(id, cancellationToken).ConfigureAwait(false) is not Project project)
            return null;

        return string.IsNullOrWhiteSpace(project.Identifier) ? project.DisplayName : project.Identifier.Trim();
    }

    /// <summary>The <c>Organisation.Reference</c> of the catalogue record <paramref name="organisationId"/>, or — when it names one the catalogue does not hold — the id itself as unresolved.</summary>
    public static async Task<(string? Reference, string? Unresolved)> SupplierReferenceAsync(
        IOrganisationCatalog organisations, string? organisationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organisationId))
            return (null, null);

        var record = await organisations.FindAsync(organisationId.Trim(), cancellationToken).ConfigureAwait(false)
                     ?? await organisations.FindByReferenceAsync(organisationId.Trim(), cancellationToken).ConfigureAwait(false);
        return record is null ? (null, organisationId.Trim()) : (record.Definition.Reference, null);
    }
}
