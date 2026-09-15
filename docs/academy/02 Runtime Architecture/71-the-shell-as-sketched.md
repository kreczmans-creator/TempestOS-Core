# The Shell as Sketched: Five Areas, Three Trees, Four Dashboards

**Release:** `v0.19.1` release candidate (`release/v0.19.1`, head
`94998b9`); the `v0.20.0` release candidate (`release/v0.20.0`, head
`3ce8f20`) adds `WP 20.10A`'s Details tab — neither released ·
**Work Package(s):** `WP 19.7A`, `WP 19.7B`, `WP 19.10O`, `WP 19.10Q`,
`WP 19.10C`, `WP 19.10D`; `WP 20.10A` · **Debt:** `TD-81` (a global
Tasks area, revived), `TD-177` (closed) · **Code:** `Tempest.Workspace.Shell`
(`ShellArea`, `ProjectArea`, `ProjectAreas`, `NavigationAvailability`),
`Tempest.Desktop.Views` (`GlobalNavigationRail`, `ShellHeaderView`, the
three area/tree views, `TasksAreaView`, `CollapsibleColumn`,
`ProjectBrowserView`, `Views.Dashboards.*`)

**In plain terms.** Everything a user sees on opening TempestOS — the
buttons down the left edge, the search box at the top, what "Projects" or
"Business" actually shows — used to be arranged the way the engineering
platform happened to organise itself, not the way a consultancy thinks
about its own work. The Product Owner drew, on paper, exactly what should
be there instead: five buttons, a header, three lists that unfold, four
summary screens. This chapter turns nine hand-drawn sheets into the real
navigation of the product, without breaking the suites proving the rest
of it still works.

## What "information architecture" is, and why paper is the right source

**Information architecture** is the plan for what goes where in a piece
of software, decided before anyone draws a button: which questions a
user actually asks, which belong together, and what each place is
called — in the user's own words, not the system's. A wireframing tool
tempts a designer toward a widget before the questions are settled; paper
cannot render a `TreeView`, so the page holds only the questions and
their groupings, transcribed in full as comment 6 of `Product Owner
Comments.md`. That is exactly the gap the design-freeze review had
already diagnosed: the shell existed, but had been **"composed from
platform primitives rather than from the user's questions"** (`05 Case
Studies/07-the-design-freeze-review.md`). A Kind-keyed explorer is a
correct, general building block; "where do I go to chase an overdue
invoice" is a specific question no general block answers on its own.
`WP 19.7A`/`WP 19.7B` are that finding closed.

## Nine sketches, five areas

The header (mark, centred global search, notifications, signed-in
principal) sits above a rail reading exactly **Home, Projects, Tasks,
Engineering, Business**. Three open a tree with a right pane over the
selected node — Projects (Dashboard + Reports, Open, Closed under 90
days, Archive), Engineering (Dashboard + Reports, Tasks, Modules,
Reference data), Business (Dashboard & Reports, Quotes, Invoices,
Timesheets, Subscriptions); Home is a dashboard with a right rail, and
Tasks a flat set of buckets. The comment's own "deltas" note the cost:
Tasks returns after `WP 19.2B` had removed it as declared-but-empty;
Business is new scope sitting, in the Product Owner's own words, "at the
edge of the 'not ERP' guard" (`D-028`); Evidence and Reports leave the
rail for a project tab and the Engineering tree; Timesheets moves under
Business — `Execution Plan.md`'s own **decision 6**.

`WP 19.2B` had already set the rule this release inherits rather than
relaxes (**decision 2**): a rail entry that exists says so, and a
capability that does not is never dimmed — it is not declared.
Electrical and Structural are the clearest case: the Engineering tree's
Modules node reads "Mechanical; Electrical and Structural are future",
and `EngineeringAreaView`'s remarks say the two "are deliberately not
shown" — no greyed-out entry, no entry. The full mechanics of this rule
and the Structure tab it also governs are
`69-the-honest-rail-and-the-structure-tab.md`.

## Sixteen commits to move three trees without breaking what worked

`WP 19.7A` built the rail, header and three trees. Its first commit,
`4c2bf5d`, is structurally the important one: `ShellArea` gains
`Business` and `EngineeringDepartment`, `ProjectArea` gains `Evidence`
and `SignOff` — every one **appended, never inserted**, because
`ShellLocation` is persisted by ordinal position, and inserting would
silently relocate every session saved before the change. `ShellArea.Business`'s
own remark states the rule: *"A brand-new member, never `Commercial`'s
own ordinal … Appended, not inserted."* `WP 20.10A`'s later
`ProjectArea.Details` (below) follows the identical, by-now habitual,
pattern.

Ten more commits (`7a0466d`–`af601cc`) build `TasksAreaView`,
`ProjectsAreaView`, `EngineeringAreaView`, `BusinessAreaView` and
`SubscriptionsView`, wire them into `MainWindow`, and re-point every
suite that had assumed the old ten-entry rail — the Release Notes count
**nineteen suites re-navigated**. Re-pointing at that scale surfaces real
defects, and this Work Package found five:

- **A double-parenting crash.** `EngineeringAreaView` wrapped the *same*
  child controls in a *new* panel on every re-render; Avalonia refuses to
  re-parent a control that already has one, so a second visit hung the
  test suite's own polling loop forever (`3e1dfa8`).
- **A sticky selection loop.** Selecting Modules → Mechanical left the
  tree's selection pointing at that node, so the next entry re-navigated
  straight back to it (`6919201`).
- **Stale group content.** Re-entering Projects with a group already
  selected skipped re-reading it; `EngineeringAreaView` had the mirror
  bug on its Dashboard node (`48b4486`).
- **A closed popup's false bounds.** The notifications flyout reported
  non-zero layout bounds while closed, tripping the layout walk's overlap
  check on every area, since the header sits everywhere (`3e1dfa8`).
- **Disposed-host handlers.** Three new area views posted their
  change-feed refresh with no `try`/`catch`; an unvisited test's own
  subscription outlived its disposal and later threw against an
  already-disposed store, taking the run down (`7c631ee`).

The Work Package's release-notes row (`fc67feb`) then discloses four
limits rather than shipping around them quietly: the header search opened the Palette without its typed text (no
seed-query entry point yet); Engineering → Tasks showed Reviews and
Approvals only, the read model having no Calculations bucket yet (closed
later, `WP 20.1B`/`TD-181`); a project-context race in the New Project
journey was fixed at the test, not its source, and carried to the
backlog as such; and the Cockpit's own action buttons still carried no
automation name, a pre-existing `WP 18.1B` gap `WP 19.7B` was about to
make irrelevant.

## Four dashboards, one chart helper

`WP 19.7B` gave Home, Projects, Engineering and Business the dashboard
each sketch called for, over the read models
`67-read-models-kpis-status-tasks-and-accounts.md` describes in full.
Home shows five task tiles, a commercial snapshot, project-status totals
as a chart, milestones, a task list and a right rail; Projects shows
status tiles, three reason-carrying lists and a Gantt from the quote's
own hours; Engineering shows open tasks and reviews; Business shows four
ageing tiles, receivable/payable panels, a quotes-to-chase list and a
twelve-week cash-flow line — every one drawn through one shared helper,
`DashboardChart`: *"each built from plain Avalonia shapes bound to
theme-token colours … Every figure a chart draws is also rendered as
plain text alongside it … the automation/layout walk, and a screen
reader, need the same numbers a sighted user reads off the drawing."*

`DashboardsTests` proves all four against a six-project fixture with
hand-computed values. Building it found two real bugs: `HomeDashboardView`'s
open-right-up handler called into a path that only ever worked for one
already-embedded view, so a row opened from Home did nothing visible
(`c9725db`); and `HomeDashboardView` had never subscribed to the change
feed at all, only re-reading on an explicit rail click (`c026902`). That
fix disclosed a broader pattern — every rail-area view loses live
reactivity on leaving and returning, since none restore the subscription
on reattach — which `08-the-view-that-stopped-listening.md` fixes as
`WP 19.7C`, not retold here.

Home taking its own dashboard meant `CockpitView` had to stop claiming
the rail: *"no longer the rail's own Home screen … stays exactly where
it always was — the engineering surface's own permanent tab … never from
the rail's Home button any more"* (`171abf8`). Nothing was deleted; a
surface simply stopped being asked to be something it was never designed
to be.

## Making room: columns that collapse on demand

`WP 19.10O` answered a plain complaint, not a defect: the rail and the
three tree columns help find things, then sit in the way of doing them.
`CollapsibleColumn`'s own remarks keep the Product Owner's words: *"when
I'm not using those menus I get the maximum real estate on the screens
for working in."* One shared control (`373c115`) gives all three tree
areas an identical chevron folding the tree to a 28px captioned strip,
independent of the existing responsive fold that still wins below the
compact-width threshold. Both states persist and restore before first
render (`1fc79d0`), reachable by mouse, the Command Palette, or `Ctrl+B`.
The last needed care: a standing guard asserts the keyboard ships with
zero default bindings (`AT-23`); rather than loosen it, `Ctrl+B` was
added as one named, disclosed exception on an allow-list a second test
keeps honest against silent widening (`d4c9279`) — name the exception,
do not weaken the rule, the same discipline chapter 41 uses for a
canonical lifecycle table.

## Three loose ends closed at review

**`WP 19.10Q`** fixed New Project from Projects → Open failing to open
what it had just created: `ProjectBrowserView.CreateAsync` searched its
own filtered list of visible projects — a snapshot taken *before* the new
project existed — rather than the project directory itself. The fix
(`b21ab98`) asks the directory directly and raises a `ProjectCreated`
event the tree extends its Open group with synchronously; `PHYSICAL_REVIEW.md`
§7c's own D2 note records this was found by file-based tracing, not by
clicking and hoping to notice.

**`WP 19.10C`** closed `TD-177`: the header search opened the Command
Palette but never carried its typed text across, so a query was typed
twice. `CommandPaletteOverlay.Open(string?)` now takes the seed text
(`931cf05`), wired `SearchRequested += query => CommandPalette.Open(query)`
— the entry point `WP 19.7A` had disclosed it lacked.

**`WP 19.10D`** regrouped Business → Invoices into the five buckets
sketched — New, Available to invoice, Sent, Outstanding/Overdue, Closed —
so every status this platform can persist on an invoice request lands in
exactly one, per `InvoicesGroupingTests`.

## Postscript: the Details tab lands first (`v0.20.0`)

`WP 20.10A` gives each project a **Details** tab holding its own
commercial identity — client, rate card, purchase order — reachable
directly for the first time. `ProjectArea.Details` is appended to the
enum exactly as `Business`, `Evidence` and `SignOff` were before it; but
`ProjectAreas`' own declaration list, not the enum's numbering, decides
tab order, and that list places `Details` first, reasoned inline: *"a
project with no client and no pinned rate card cannot record time
against it at all … needs to be set, and reachable again afterwards,
before anything else about the project is worth looking at."* The full
quotation and lifecycle story this tab belongs to is
`70-quotations-change-orders-and-the-project-lifecycle.md`; the lesson
here is narrower — an append-only enum and a user-facing order are two
different decisions, and conflating them would put identity wherever the
enum happened to grow next.

## What was deliberately not built

Electrical and Structural stay unbuilt and undeclared, as sketched. The
header's palette-seeding gap, the Cockpit's missing automation names and
the rail-area reattach defect were each disclosed rather than folded
silently into scope — closed by name above or in `WP 19.7C`. No charting
library was pulled in for four dashboards that needed, in the end, only
rectangles, a polyline and some text.

## What to take away

- **A paper sketch is not a lesser design artefact than a wireframe for
  information architecture — it is often the better one**, because it
  cannot render a widget before the questions are sorted.
- **Append, never insert, once a location is something a session can be
  restored into.** `ShellArea` and `ProjectArea` learned this once and
  now apply it by habit, most recently for a tab that had to display
  first despite arriving last.
- **A rule can survive one genuine exception without being weakened for
  everyone** — `AT-23`'s zero-default-bindings guard kept `Ctrl+B` on a
  named, tested allow-list rather than loosening the rule the rest of the
  keyboard still depends on.
