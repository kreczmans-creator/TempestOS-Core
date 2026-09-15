# Read Models: KPIs, Project Status, Tasks and Accounts

**Release:** `v0.19.0`, `v0.19.1` and `v0.20.0` release candidates
(`release/v0.19.0`, `release/v0.19.1`, `release/v0.20.0` — none released) ·
**Work Package(s):** `WP 19.1B`; `WP 19.5C`, `WP 19.7B`, `WP 19.8B`;
`WP 20.1B` · **Decision:** `ADR-0150` · **Debt:** `TD-33`, `TD-180`, `TD-181` (closed) ·
**Code:** `src/Tempest.Workspace/Kpi/`,
`src/Tempest.Workspace/Projects/ProjectStatusReadModel.cs`,
`src/Tempest.Workspace/Tasks/`, `src/Tempest.Core/Invoicing/` (accounts
reads), `src/Tempest.Desktop/Views/Dashboards/`

**In plain terms.** This is the machinery behind every number the
consultancy's owner actually wants to see: how busy each engineer is,
which projects are making money, how much work is done but not yet
billed, and how long clients take to pay. None of those figures are
stored anywhere. Every time a screen needs one, TempestOS reads the
underlying records — hours logged, deliverables completed, invoices
sent — and works it out fresh. This chapter also covers the honest
boxes that state a project's true condition (On hold, Overdue, Ready to
invoice…), the task lists that say what needs doing, and the read-only
window onto the accounting package that shows what the business owes
without TempestOS ever becoming a bookkeeping system of its own.

## A figure the store cannot disagree with

A **read model** is a number or list computed from the stored records
at the moment it is asked for, and thrown away the instant the screen
moves on. Nothing is cached as truth. If a KPI card reads 62% one
morning and 71% the next, that is because the timesheet entries behind
it changed, never because two numbers were written down somewhere and
only one got updated.

`33-the-product-spine.md` drew this line once already, at a smaller
scale: the Project Workspace counts its own object graph rather than
storing a count, "precisely the mistake `ProjectModel`'s
`RequirementCount`/`CalculationCount` fields made." This chapter is the
same lesson at the scale of a whole consultancy's commercial picture —
five KPIs, six project statuses, eight task buckets, a cash position —
none of them a stored fact. A stored figure can go stale the moment the
thing it counted changes underneath it; a read model cannot, because
there is nothing to go stale.

## The equations came before the cards

`ADR-0150` states Utilisation, Margin per project, Work in progress,
Days sales outstanding and Calc throughput as five precise equations —
"verbatim," the ADR says, from the `WP 19.1B` row — before `WP 19.1B`
ever wrote a card to show one. Margin per project, for instance, is
fixed as "(Σ billable hours × frozen billing rate + Σ fixed-price
deliverable value) − Σ all hours × frozen cost rate," using rates each
timesheet entry froze on the day the work was done, never re-resolved
later. Writing the equation down first, and pinning it with
hand-authored fixture tests before the card exists, lets a reviewer
check the arithmetic without opening the desktop application. One
fixture from `tests/Tempest.Core.Tests/Kpi/KpiEquationsTests.cs` reads:

```csharp
// A: 4h billable @ £100/h billing, £60/h cost → revenue 400, cost 240.
Entry(project, new(2026, 3, 9), 4m, billable: true, billingRate: 100m, costRate: 60m),
// C: 3h billable @ £100/h billing, NO cost rate → revenue 300, cost 0, flags missing.
Entry(project, new(2026, 3, 11), 3m, billable: true, billingRate: 100m, costRate: null),
```

`KpiEquations` has no persistence dependency at all: every equation is a
pure function over plain fact records. `KpiPeriod` supplies six named
presets (This week, Last week, This month, Last month, This quarter,
This year) plus a `Custom` range, persisted by name so a saved "this
week" re-anchors to whatever week it now is, rather than freezing on
the range it happened to compute the day it was saved.
`KpiSnapshotService.GetSnapshotAsync` returns exactly one `KpiSnapshot`
per period — all five figures from one pass, never five separate reads
that could disagree about what "now" meant.

## One scan, filtered in memory

`v0.19.0`'s execution plan states its seventh engineering decision
plainly: "KPI read models scan the object-state collection inside one
read transaction and filter in memory; the query store has no field
filtering, and the volumes of a consultancy do not need one yet."
`WorkspaceSnapshotReader.ReadKpiAsync` does exactly that: one call to
`IQueryablePersistenceStore.ExecuteInReadTransactionAsync`, reading
every record in `EngineeringDomain.ObjectState` and picking out the
`TimesheetEntry`, `DeliverableCompletion`, `InvoiceRequest` and Evidence
rows it needs by a `switch` on each record's own Kind. A consultancy's
whole engineering estate is thousands of objects, not millions; scanning
all of them once is simpler than building an index for a query the
platform runs a handful of times a day. The shape became a pattern:
`ProjectStatusReadModel` and `TasksReadModelService` are each documented
as "a sibling reader over the identical durable
`EngineeringDomain.ObjectState` collection," neither touching
`SnapshotReader.cs`.

## Tearing out the placeholder

Before `WP 19.1B`, the cockpit's commercial card was "Engineering
Overview" — a cross-discipline aggregate with no project-specific
meaning, the same mislabelling later flagged elsewhere as "Project
health: Unknown — no Engineering data yet" on a card that could not know
about one project at all. Four commits removed it: `8f05843` wires the
five real cards in its place; `af5415a` adds the tests pinning the
equations; `e0a702d` proves it through the real desktop window with
hand-computed numbers ("margin per project 700.00 = 980 revenue - 280
cost… days sales outstanding and calc throughput both honestly empty")
and confirms "the removed 'Engineering Overview' placeholder card is
confirmed gone"; `6759f98` replaces the last test still checking the old
card's own text with one proving the new honest-empty text instead. A
card with nothing to show now says so in words —
`DaysSalesOutstandingKpiCards` reads "Unavailable — no invoice has
reached Sent with a known issued date yet" — never a placeholder and
never a zero standing in for "I don't know."

## One project, six statuses, one reason

`WP 19.5C`'s `ProjectStatusReadModel` answers a different question: not
"how is the business doing" but "how is *this* project doing, and why."
One coherent scan gives every open project exactly one of six statuses
— On hold, Blocked, Overdue, At risk, Ready to invoice, On track — with
the reason next to it. `RuleFor` evaluates them in a fixed priority
order (a project can be both on hold *and* overdue; the first match
wins, so the status shown is never ambiguous), each rule a plain
sentence built from real state: "Blocked: a quote was accepted but no
deliverable has been started." The same read carries the Gantt's own
schedule fields per project — dates, quoted hours from Accepted
quotations, recorded timesheet hours, every live milestone by date —
so the Projects dashboard's Gantt (`WP 19.7B`) draws from the identical
scan, never a second query that could disagree with it.

## Seven buckets, then an eighth

`ITasksReadModel` answers "what needs doing." `WP 19.5C` shipped seven
buckets over deliverables, milestones, manual tasks (a new `ManualTask`
Kind, deliberately not named `"Task"`, which `CanonicalObjectKinds.Task`
already claims), Evidence and invoice requests/quotations: Overdue, Due
today, Due this week, Later, Reviews (Evidence Draft with a subject and
a file — "genuinely ready to check, not an empty draft"), Approvals
(Evidence Checked), and Finance (unpaid invoices and stale quotes to
chase). `WP 20.1B` added an eighth, `TaskBucket.Calculations` (`TD-181`):
every live Calculation under a project, from creation, leaving on
completion or when issued evidence cites it — one with no project
ancestor at all is not shown, confirmed with the Product Owner rather
than guessed. The same Work Package closed `TD-180`: the Finance bucket
used to chase an unpaid invoice "thirty days since Sent," a flat guess
with no per-client meaning; `InvoiceRequest` now freezes each client's
own `PaymentTerms` at raise time and computes a real `DueOn`, and the
Finance bucket, the Invoicing area's grouping and the Business
dashboard's receivable split all read that date instead of the
constant, which was removed outright.

## What TempestOS reads, but never enters

`WP 19.8B` answers the Product Owner's eighth comment on the `v0.19.0`
candidate directly: subscriptions and bills are read from the
accounting package, never entered in Tempest. `IAccountsConnector` is a
read-only sibling to the outbound invoicing connector — bills due,
repeating bills and the cash position, never a write — implemented on
the Fake, Xero (primary) and QuickBooks Online connectors. A background
`AccountsRefreshService` polls it hourly, or on demand from Settings,
and caches the result at `<persistence root>/accounts/last-reading.json`;
a failed refresh keeps the last good reading and records only the
failure's own reason and time, so `AccountsReadModel` never has to lie
about it — its unavailable state reads "Accounts reading unavailable —
{reason} (since {time})," never a zero standing in for "the connector
didn't answer." `AccountsCategoriser` sorts each repeating bill into
Hardware, Software or Premises by matching its Xero account name against
configurable keyword lists, checked in a fixed order so a name matching
two lists still resolves to one category deterministically.
`AccountsSnapshot` folds that reading with Tempest's own live invoice
requests into the Business dashboard's tiles, a receivable/payable split
and a twelve-week cash-flow projection — deliberately "not a forecast,"
each week summing only whichever bill's own *next* due date falls
inside it. This keeps the "not ERP" guard (`D-028`) intact: TempestOS
holds no bookkeeping of its own, only a cached read of somebody else's.

## Dashboards as consumers, not sources

`WP 19.7B` built the four dashboards (Home, Projects, Engineering,
Business) as pure consumers of the read models above; no dashboard
computes a figure of its own. `DashboardChart` is the one shared
drawing helper every dashboard uses for bars, lines and Gantt rows,
built from plain Avalonia shapes rather than a charting library, so both
themes render correctly. Its own remarks state the rule: "every figure
a chart draws is also rendered as plain text alongside it… never a
figure only a screen can see." A bar chart with every value at zero
still draws every label and a literal "0"; a Gantt row with no dates
"draws no bar, only the label and trailing text — an honest 'no dates
recorded' rather than a fabricated span." `DashboardsTests` proves all
four together, over a fixture of six open projects (one per
`ProjectHealthStatus`, chosen to avoid the rule-priority conflicts a
smaller fixture would hit) and a Fake accounts reading, with
hand-computed values for every tile, list and chart.

## Honest limits

**Accounts reads are written to the API documents, not proven live**
(`v0.19.1` Release Notes, `WP 19.8B`): Xero's bank summary is parsed by
column title rather than position, and the connectors follow the
providers' own published shapes with no sandbox authorisation to test
against. The first live authorisation is the test.

**The five `ADR-0150` KPI cards did not move to Home.** `WP 19.7B` built
new dashboards with their own tiles — Overdue/Due today/Approvals,
project status counts, a commercial snapshot — but Utilisation, Margin
per project, Work in progress, Days sales outstanding and Calc
throughput stay exactly where `WP 19.1B` put them: inside the Structure
tab's engineering cockpit. `PROJECT_STATUS.md` records this precisely
against sentence five of "What v1.0.0 is": "the five KPI equations of
`WP 19.1B` stay in the cockpit inside the Structure tab." Two sets of
figures, in two places, answering two different questions.

**A discipline's own empty state used to speak for another's.** `TD-33`
(closed `WP 19.10E`, `f1d4d3c`) is a smaller case of the same honesty:
`CockpitFormatting.FormatCoverage` used one fixed string, "no
requirements yet," regardless of which discipline's zero-denominator
card called it. It now takes an `emptyStateNoun` each caller supplies,
so a card's empty state always names its own discipline — the same
discipline `TaskBucket.Calculations`'s own heading needs, once it, too,
has nothing to show.

**Deliberately not built:** a multi-currency roll-up across the accounts
snapshot (a live reading mixing currencies is refused with
`CurrencyMismatchException`, exactly as `Money` refuses it everywhere
else, never silently converted); forecasting beyond a bill's own next
due date; a field-level query index behind the KPI/status/tasks reads —
decision 7 accepted an in-memory scan for a consultancy's own volumes
rather than building one pre-emptively.

## What to take away

- **A number recomputed every time it is shown can never disagree with
  the records it comes from — a number that is stored can.**
- **Writing the equation down and pinning it with a hand-computed
  fixture before the card exists turns a maths review into reading a
  test, not running the application.**
- **An empty or unavailable state is not a gap to leave blank; it is a
  fact to state, in the exact words a person needs to hear.**
