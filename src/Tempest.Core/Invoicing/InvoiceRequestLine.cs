using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.Invoicing;

/// <summary>
/// One line of an <see cref="InvoiceRequest"/>: a <c>TimesheetEntry</c>'s
/// billable hours at its own frozen rate, or a <c>DeliverableCompletion</c>'s
/// own fixed price — never a figure this Kind computes itself (`WP 19.1A`,
/// `ADR-0151`).
/// </summary>
/// <param name="SourceKind">The Kind the line was built from — <c>Tempest.Core.Timesheets.TimesheetEntry.CanonicalKind</c> or <c>Tempest.Core.Deliverables.DeliverableCompletion.CanonicalKind</c>, never validated as a third value by this record itself.</param>
/// <param name="SourceId">That source object's own id — what <see cref="InvoicingService.SendAsync"/> marks <c>InvoicedBy</c> on once the request reaches <see cref="InvoiceRequestStatus.Sent"/>.</param>
/// <param name="Description">What the line is, read for a timesheet entry's own <c>TaskDescription</c> and for a deliverable completion's own deliverable name.</param>
/// <param name="Quantity">Billable hours, for a timesheet entry; <c>1</c>, for a fixed-price deliverable completion.</param>
/// <param name="UnitRate">The frozen rate one unit of <see cref="Quantity"/> bills at.</param>
/// <param name="Amount">
/// <see cref="Quantity"/> times <see cref="UnitRate"/>, carried alongside
/// rather than recomputed — the one figure this line states rather than
/// leaves a reader to multiply back out, and the one <see cref="InvoiceRequest.Total"/>
/// actually sums.
/// </param>
public sealed record InvoiceRequestLine(
    string SourceKind,
    Guid SourceId,
    string Description,
    decimal Quantity,
    Money UnitRate,
    Money Amount);

/// <summary>
/// The read-only projection of an <see cref="InvoiceRequest"/> an
/// <see cref="IInvoicingConnector"/> is actually handed — carries no
/// dependency on <c>Tempest.Core.EngineeringDomain.EngineeringObjectBase</c>
/// or any other engineering-domain plumbing, so a connector implementation
/// (`WP 19.1A` parts 2/3, over <c>HttpClient</c>) depends only on plain data
/// (`WP 19.1A`, `ADR-0151`).
/// </summary>
/// <param name="RequestId">The request's own id — <see cref="InvoicingService.SendAsync"/>'s own idempotency key, and what a real connector's implementation writes into the created invoice's own reference field.</param>
/// <param name="ClientOrganisationId">The client this invoice is raised against — <c>Tempest.Core.EngineeringDomain.Project.ClientOrganisationId</c>, an organisation-catalogue id.</param>
/// <param name="ClientName">
/// The client organisation's own name, read from the Organisation catalogue
/// (<c>Tempest.Core.BusinessOperations.Crm.IOrganisationCatalog</c>) by
/// <see cref="ClientOrganisationId"/> and filled in by <see cref="InvoicingService"/>
/// before a connector is ever called (`WP 19.1A-R1` disclosure #3) —
/// never resolved by a connector itself, keeping this projection's own
/// "plain data, no catalogue dependency" shape intact. <see langword="null"/>
/// when <see cref="ClientOrganisationId"/> does not resolve to any
/// registered organisation; every connector implementation rejects outright
/// rather than matching or creating a contact named after a raw, meaningless
/// id.
/// </param>
/// <param name="PurchaseOrderReference">The project's own purchase-order reference, where one is recorded. <see langword="null"/> otherwise.</param>
/// <param name="Currency">The currency every <see cref="InvoiceRequestLine.UnitRate"/>/<see cref="InvoiceRequestLine.Amount"/> and <see cref="Total"/> are stated in.</param>
/// <param name="Lines">The request's own lines, in the order the request carries them.</param>
/// <param name="Total">The sum of every line's own <see cref="InvoiceRequestLine.Amount"/>.</param>
public sealed record InvoiceRequestSnapshot(
    Guid RequestId,
    string ClientOrganisationId,
    string? ClientName,
    string? PurchaseOrderReference,
    CurrencyCode Currency,
    IReadOnlyList<InvoiceRequestLine> Lines,
    Money Total);
