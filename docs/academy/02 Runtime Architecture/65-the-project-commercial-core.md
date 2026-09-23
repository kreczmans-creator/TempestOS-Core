# The Project Commercial Core: Client, Rate Card, Time and Deliverables

**Release:** `v0.19.0` release candidate on `release/v0.19.0` (unreleased
— superseded for testing by `v0.19.1`), extended on the `v0.20.0` release
candidate on `release/v0.20.0` (unreleased — under the Product Owner's
manual test) · **Work Package(s):** `WP 19.0A`, `WP 20.1B`, `WP 20.10A` ·
**Debt:** `TD-180`, `TD-181` · **Decision:** `ADR-0150` · **Code:**
`Tempest.Core.Projects.ProjectCommercialService`,
`Tempest.Core.Timesheets`, `Tempest.Core.Deliverables`,
`Tempest.Core.BusinessGovernance.Pricing.RateCard`

**In plain terms.** A consultancy runs on a few numbers: which client a
project is for, what was agreed to pay for it, what an hour of an
engineer's time bills and costs, and whether a promised piece of work has
been delivered. Before this work a TempestOS project had none of that —
it could not even say who it was for. This gives a project a client, a
purchase-order reference, a budget and a price list (a *rate card*)
pinned to it; lets an engineer log a week's hours against it; and lets
someone mark a deliverable done, once. Two rules protect the numbers: the
price an hour billed and cost at is locked in the moment it is recorded,
so a rate rise next year can never quietly rewrite what last month
earned; and a deliverable can be marked delivered only once, so nobody is
billed twice by accident.

## Two sentences of a five-sentence product

"What v1.0.0 is" (`docs/releases/v1.0.0/WorkPackages.md`) names TempestOS
in five sentences. This chapter covers the first — a project for a
client, with a PO reference, a budget and a pinned rate card — and the
recording half of the fourth, time and deliverable completion. Invoicing
(the rest of sentence four) is
`66-outbound-invoicing-and-the-connector-seam.md`; the cockpit (sentence
five) is `67-read-models-kpis-status-tasks-and-accounts.md`. None of this
is released: `WP 19.0A` landed on the `v0.19.0` candidate, superseded for
testing by `v0.19.1`; `WP 20.1B`/`WP 20.10A` land on the `v0.20.0`
candidate, still under manual test.

## What a rate card actually is

A **rate card** is a price list: for each *grade* it states two figures.
The **billing rate** is what the client is charged for an hour. The
**cost rate** is what that hour actually costs the consultancy — salary,
overhead, the loaded figure an accountant would recognise. A card with
only the billing rate tells you revenue; with both, it tells you margin
— why `ADR-0150` adds `CostRate` to `RateCardEntry` beside the `Rate`
already there, nullable so a pre-existing card still reads back with no
cost figure to compute margin from. No rate ships with TempestOS: what an
organisation charges, and what delivery costs it, is its own commercial
position.

A project may pin only a **Released** card. `ProjectCommercialService.PinRateCardAsync`
checks the card's governance state before `Project`'s mutator runs,
refusing a Draft card as a *result*, not an exception:

```csharp
if (record.ValidationState != ReferenceValidationState.Released)
    return new ProjectCommercialResult(ProjectCommercialRefusal.RateCardNotReleased,
        $"Rate card '{rateCardId}' is {record.ValidationState}, not Released. " +
        "A project may not pin a card nobody has verified.", project);
```

This mirrors `IEvidenceService.CiteAsync`, which refuses an unreleased
citation for the same reason: a Draft card is an unfinished proposal, and
pinning it would let a project bill against prices nobody checked.

## The one lookup a timesheet entry freezes

A `TimesheetEntry`'s `BillingRate` and `CostRate` are resolved exactly
once, at record time, from `RateCard.ResolveRates(grade)`, and neither
field has a mutator afterwards. This is `ADR-0150`'s load-bearing
decision: **a timesheet entry's rate is a fact about the day the work was
done, not a number that moves under a finished period's report.** A later
card revision, even a supersession, never reaches an already-recorded
entry, because `RateCardCatalog.GetRevisionAsync` reads one immutable
historical revision, never the card's current state.
`ProjectCommercialJourneyTests` proves the sharper case: after a card is
superseded, recording a *new* entry against the project's own
(now-superseded) pin still resolves the original, frozen rate — the pin
names a revision, not "whatever this card says today."

Without freezing, raising rates in January could silently rewrite what
November's margin was. A timesheet is evidence of what happened; evidence
that can retroactively change is not evidence. `InvoicedBy` follows the
same once-only shape: set exactly once, and `AmendAsync`/`DeleteAsync`
refuse an entry once it is set, naming the invoice request that holds it.

## Once, and never twice: `DeliverableCompletion`

`DeliverableCompletion` records that a project's `Deliverable` — the
existing, milestone-parented engineering object — was actually delivered:
when, by whom, against which Issued evidence, and at what fixed price if
billed that way rather than by time. Execution Plan decision 5 makes it a
**new Kind, distinct from `Deliverable`**, which stays as it was: a
deliverable is a promise, a completion is the record the promise was
kept, and conflating them would re-purpose an engineering object's
lifecycle for a commercial fact.

`DeliverableService.CompleteAsync` refuses a second completion, returning
the first rather than creating a duplicate:

```csharp
var existing = await FindExistingCompletionAsync(deliverableId, cancellationToken);
if (existing is not null)
    return new DeliverableCompletionResult(DeliverableCompletionRefusal.AlreadyCompleted,
        $"Deliverable '{deliverableId}' was already completed on {existing.CompletedOn:O} " +
        $"(completion '{existing.Id}'). A deliverable can be completed once.", existing);
```

A deliverable is usually what an invoice line is built from; a second
completion could raise a second invoice for the same work. "Complete" is
deliberately a one-way door. Completing one also runs a best-effort hook
raising an invoice request — `Tempest.Core.Invoicing`'s own concern
(`66-outbound-invoicing-and-the-connector-seam.md`); the completion
itself "raises no invoice request of its own," only carrying the
`InvoicedBy` link that service writes.

## The same shape as `Evidence`, and a composite settings key

`TimesheetEntry` and `DeliverableCompletion` both follow the shape
`ADR-0148` set for `Evidence`: an `EngineeringObjectBase` subtype with its
own `CaptureTypeState`/`ApplyTypeState`/`Rehydrate`, parented to the
project (a completion resolves its project from the deliverable's own
ancestry), with a service deciding permission before the Kind's `internal`
mutators run. `ADR-0150` calls them "the nineteenth and twentieth Kinds
with a production rehydrator, as Evidence was the eighteenth" —
registered directly, like Evidence, rather than folded into
`CanonicalObjectKinds`, whose documented purpose is Kinds with *no*
discipline workspace. Named cost: `CanonicalObjectKinds.All` stays at
twenty-one, so a reader expecting every production Kind listed there will
not find these two.

Each principal's **working pattern** — hours available per week, default
37.5, what utilisation later divides by — is stored as an ordinary
setting, but keyed per identity: `Timesheet.WorkingPattern:{identityId}`.
Execution Plan decision 6 gives the reason: *"the settings substrate has
no enumeration"* — `ISettingsProvider` expects a fixed, known-in-advance
set of settings, and a working pattern needs one row per person whose
roster is not known at startup. `EnsureRegisteredAsync` registers a
principal's row lazily, the first time anything asks, sidestepping a
change to the substrate for one Work Package.

`TimesheetWeek.WeekOf` is the one calendar rule this Work Package owns —
which Monday, ISO 8601, a date belongs to:

```csharp
public static DateOnly WeekOf(DateOnly date)
{
    var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
    return date.AddDays(-daysSinceMonday);
}
```

## The mistake the release gate caught

`TimesheetsWorkspaceRegistration` was first written with no delete
factory registered for `TimesheetEntry`. The shell's generic delete path
routes every deletion through whichever factory a Kind registers; with
none, an invoiced entry's own refusal would never be reached by that
path — the shell would happily delete an entry the domain says is
frozen. The gate run at `WP 19.0A (13/n)` caught it, alongside a missing
entry in `SurfaceCommandPolicy.DeleteCommandIds`. The fix (`ccd4c46`)
registers the Kind's own delete command as its factory, three lines. **A
domain rule enforced inside a service is not enforced until every path a
user can take reaches that service.**

## Extended on `v0.20.0`: payment terms and a calculation as a task

`WP 20.1B` (`TD-180`, `TD-181`), from the Product Owner's decisions of
2026-09-15, extends the core, frozen the same way rates are. **Payment
terms** are now a closed vocabulary on the client — Up front, 30 days or
60 days, defaulting to Up front. An `InvoiceRequest` copies its client's
terms at raise, and freezes them; `DueOn` is computed once, at **Send**,
not at raise — nothing is due before an invoice has gone out — and a
request sent before `WP 20.1B` backfills as Up front, due the day it was
sent. **Warning:** terms default to *Up front* silently for a client with
none recorded, not by prompt.

**A calculation is now a task from the moment it exists.** The Product
Owner's reasoning: a calculation identified as a deliverable, or as
evidence in a review package, needs doing for the project to progress,
and the same person can complete it. `Calculation` gains a minimal
`Completed`/`CompletedOn` flag and `calculations.complete`; the tasks
read model lists every live calculation under a project from creation,
leaving the bucket on completion or the moment Issued evidence cites it.
**Warning:** a calculation with no project ancestor is not a task,
confirmed with the Product Owner rather than assumed — this holds only
*within* a project.

## Extended again: a project's commercial identity starts at creation

`WP 20.10A` closed four of the Product Owner's own `v0.20.0` findings
(`D1`, `D2`, `D12`, `T1`): a project could be *given* a client and rate
card once it existed, but nothing let anyone set them at creation, or
find them afterwards. **New Project** now asks for a **Client**, a **Rate
card** (Released only; "None" allowed, with its consequence stated inline
— *"Time cannot be recorded against this project until a rate card is
pinned"*) and an optional **PO reference**, written through the identical
commands the generic editor's Commercial section dispatches.

The project workspace gains a **Details** tab, first in its strip — the
one place these fields are reachable directly, closing a finding that
read, verbatim, *"Commercial … doesnt exist at all … cannot navigate to
it anywhere."* Building it surfaced the Work Package's own **kill
switch**: hosting the existing `ObjectEditorView` inside the tab was
rejected — that control is built for one fixed object id and would be
rebuilt whole on every project switch. Instead `ProjectDetailsView` is a
dedicated, small view over the identical `ProjectCommercialEditorSupport`
collaborator and `project.*` commands — the same write, the same
transaction, a different surface. **A dedicated view built for a changing
subject beats forcing a single-subject control to follow one.**
Timesheets' Record dialog, found not listing projects at all (`D12`), now
lists every open project; one with no rate card pinned disables Grade and
Hours and says why, with an Open Details button.

## What was deliberately not built

Not ERP, not PLM (`D-028`): no occurrence model, no procurement, no
workflow engine, no stock or supplier field. Every commercial attribute
on `Project` earns its place because a calc sheet cites it, a drawing
shows it, or the invoice seam needs it. `TimesheetEntry` and
`DeliverableCompletion` raise no invoice and compute no total — that is
`Tempest.Core.Invoicing`'s own job, reading these links, never writing
them a second way. The five KPI equations were defined in `ADR-0150`
before any card was built; the cards themselves are
`67-read-models-kpis-status-tasks-and-accounts.md`'s own chapter.

## What to take away

- **A number that can change after the fact is not a record of what
  happened; freeze it the moment it becomes true** — a timesheet's rate,
  an invoice's payment terms.
- **A one-way action deserves a service that says so**, returning the
  first result rather than creating a second — a deliverable completed
  once, an entry invoiced once.
- **A domain rule is only as strong as every path that can reach it**:
  the timesheet-delete defect existed because one of two delete paths
  never learned the rule the other one enforced.
