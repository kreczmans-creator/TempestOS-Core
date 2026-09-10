# ADR-0150: Time, Deliverable Completion and Rate Resolution Are Engineering Objects on the Project; Billing and Cost Rates Freeze at Entry; TempestOS Holds No Ledger

## Status

Accepted — `WP 19.0A` (Project commercial core), 2026-09-10.

## Context

`v0.19.0` closes the consultancy seam: a project needs a client, a
purchase order, a budget and a rate card before it can be billed at all;
an engineer needs to record time and complete deliverables against those
rates; `WP 19.1A`/`WP 19.1B` need frozen figures to raise an invoice
request and to compute utilisation and margin later. None of that exists
yet — `Project` carries only `ProgrammeId` (`TD-76`), and no Kind records
time or delivery.

`Evidence` (`ADR-0148`) already set the shape every governed Kind since
has followed: an `EngineeringObjectBase` subtype carrying its own state
through `CaptureTypeState`/`ApplyTypeState`/`IRehydratable<T>.Rehydrate`,
a service deciding everything permission depends on before the Kind's
own `internal` mutators run, reporting a refusal result rather than an
exception. This Work Package repeats that shape twice more, and extends
`Project` the same way.

Not ERP, not PLM (`D-028`): no occurrence model, no procurement, no
workflow engine, no stock or supplier field. An attribute earns its
place only because a calc sheet cites it, a drawing shows it, or the
invoice seam needs it — every field below traces to one.

## Decision

**1. `Project` gains its commercial core**, six optional fields, each
written by its own `ProjectCommercialService` act (one transaction, one
audit row, via `Project`'s own `internal` mutators): `ClientOrganisationId`
(`string?`, an Organisation-catalogue id — a tag, never validated,
exactly `Evidence.SubjectId`'s own rule), `PurchaseOrderReference`
(`string?`), `Budget` (`Money?`), `RateCardPin` (`ReferencePin?`, minted
with `ReferencePin.For` from the record actually read; pinning an
unreleased card is refused as a result, exactly as
`IEvidenceService.CiteAsync` refuses an unreleased citation),
`StartDate`/`TargetDate` (`DateOnly?`), `ProjectManagerIdentityId`
(`string?`). `IProject` exposes all six read-only.

**2. `RateCardEntry` gains `Money? CostRate` beside `Rate`** — the loaded
cost of an hour of that grade; `null` on a record predating this Work
Package, so the JSON round trip keeps every existing card readable.
`RateCard.ResolveRates(grade)` returns a `RateResolution` (billing and
cost rate, or a named refusal when the grade is not on the card) — the
one lookup a timesheet entry freezes.

**3. `TimesheetEntry : EngineeringObjectBase, IRehydratable<TimesheetEntry>`**
(`src/Tempest.Core/Timesheets/`), parented to the project. State:
`PrincipalIdentityId`, `ProjectId`, `TaskDescription`, `Date`, `Hours`
(0 exclusive to 24 inclusive, quarter-hour granularity, validated as
`RateCardEntry.Rate` validates non-negative), `Billable`, `Grade`,
`BillingRate` (`Money`) and `CostRate` (`Money?`) **resolved once, from
the project's own pinned card and this grade, at `RecordAsync` time, and
frozen thereafter** — neither has a mutator; a later supersession of
that card, or even a later recording against the same, now-superseded
pin, never reaches an already-recorded entry, because
`RateCardCatalog.GetRevisionAsync` reads one immutable historical
revision regardless of the record's own current validation state.
`InvoicedBy` (`Guid?`) is set once (`MarkInvoicedAsync`) and never
cleared; a second set is refused. `RecordAsync` refuses, as a result, a
project with no Released pin or a grade the pinned card does not price;
`AmendAsync`/`DeleteAsync` refuse once `InvoicedBy` is set. The two list
queries read through `EngineeringDomainContext.Repository`, the same
in-memory, committed-state-only index production listings already use —
a Core-layer domain read, not a `WorkspaceSnapshotKind`. `TimesheetWeek.WeekOf`
is the one calendar rule owned here: the Monday on or before a date, ISO
8601, correct across a year boundary. Each principal's own working
pattern (hours/week, default 37.5) is a `Timesheet.WorkingPattern:{identityId}`
setting registered lazily per identity through `ISettingsProvider`,
generalising `EvidenceService`'s own construction-time registration of
`Evidence.IndependentCheck` to a roster with no fixed membership.

**4. `DeliverableCompletion : EngineeringObjectBase, IRehydratable<DeliverableCompletion>`**
(`src/Tempest.Core/Deliverables/`), parented to the project, distinct
from the existing milestone-parented `Deliverable`. State: `DeliverableId`,
`CompletedOn`, `PrincipalIdentityId`, `IssuedEvidenceIds` (each verified
Issued at completion time, else refused), `DocumentIds`, `FixedPriceValue`
(`Money?`), `InvoicedBy` (`TimesheetEntry`'s own once-only rule). **A
deliverable can be completed once**: `CompleteAsync` refuses a second
completion for the same `DeliverableId`, returning the first. It raises
no invoice request of its own — `WP 19.1A` reads `InvoicedBy`, never
writes it a second way.

**5. Both new Kinds follow `Evidence`'s own registration path, registered
directly rather than folded into `CanonicalObjectKinds`** — that class's
documented purpose is Kinds with *no* discipline workspace, and these two
have one each: an explorer area (project → week → entry; project →
deliverable → completion), a facet provider naming the principal
(`IPrincipalDirectory.Describe`) and the project or deliverable, not a
bare id, a plain-data `IWorkspaceView` so a created object opens right
up, and command descriptors — the nineteenth and twentieth Kinds with a
production rehydrator, as Evidence was the eighteenth. `deliverable.complete`
resolves its own project from the selected `Deliverable`'s own ancestry
(deliverable → milestone → project) rather than asking the caller. The
six project-commercial commands need no new node or facet provider —
`Project`'s own already exist.

**6. The five KPI equations, defined here before any card is built**
(`WP 19.1B` row, verbatim): **Utilisation** = Σ billable hours ÷ Σ
available hours, per principal, over the selected period; available
hours come from the principal's working pattern (`WP 19.0A`), not
calendar days. **Margin per project** = (Σ billable hours × frozen
billing rate + Σ fixed-price deliverable value) − Σ all hours × frozen
cost rate, over entries dated in the period, shown as a currency amount
and a percentage of billable value; both rates come from the timesheet
entry, never re-resolved. **Work in progress** = Σ billable value of
entries with no `InvoicedBy` link, by project, with age from the oldest
entry. **Days sales outstanding** = Σ over invoices in `Sent` or later of
(paid date or today − invoice date) ÷ count, using dates read from the
connector; where no connector is authorised the card reads "unavailable"
and never zero. **Calc throughput** = sheets reaching `Issued` in the
period.

## Consequences

**Positive:** a timesheet entry's own rate is a fact about the day the
work was done, never a number that moves underneath a finished period's
report; a deliverable cannot be billed twice by accident; `WP 19.1B`'s
five cards have equations to implement, not to invent; `WP 19.1A`'s
invoice request has exactly one place to read what is billable and what
it cost.

**Negative:** `CanonicalObjectKinds.All` stays at twenty-one — a reader
expecting every new production Kind there will not find these two; the
rationale is recorded at both call sites. The Settings dialog's working-
pattern field is not wired into `MainWindow.cs`'s construction call this
Work Package: that file belongs, in parallel, to `WP 19.2A`'s composition
refactor; the dialog is built and tested, one argument short of live.

## Related Documents

`D-028`; `ADR-0148` (Evidence, the shape repeated twice here); `ADR-0145`
(one transaction, one audit row); `IEvidenceService.CiteAsync` (refusal-
as-result precedent); `WorkPackages.md` (`WP 19.0A`/`19.1A`/`19.1B` rows)
— `ADR-0151` reads `InvoicedBy` and the frozen rates defined here.
