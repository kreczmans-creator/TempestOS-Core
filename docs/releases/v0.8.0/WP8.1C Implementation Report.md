# WP 8.1C — Engineering Cockpit — Implementation Report

## Status

Complete. Implements the Engineering Cockpit as the Workspace's own
default landing experience (`ADR-0069`), consuming existing Workspace
services only — no Requirements, Calculations, Verification, or
Digital Thread traversal logic. Every card the controlling instruction
names is implemented; every card with no real backing service today
shows fixed, disclosed, representative placeholder content rather than
being omitted, so the dashboard "feels complete" even before any real
engineering discipline is wired to it.

## A Disclosed Scope Note

This Work Package's own controlling instruction arrived in two parts —
an initial, narrower brief, followed directly by a substantially
expanded one ("Engineering Cockpit (Product Experience)") naming many
more cards than `WP8.0C Engineering Cockpit Specification.md`'s own
seven layout regions: Continue Where I Left Off, Recent Projects,
Favourite Projects, a Project Health Dashboard with five named
per-discipline statuses (Requirements/Verification/Calculations/
Documentation/Review), Risk Summary, Open Decisions, Blocked Items,
Overdue Actions (distinct from Open Actions), and Quick Actions. This
report and the shipped implementation follow the expanded brief in
full — the second, superseding instruction — while noting explicitly
that several named cards go beyond what `WP 8.0C` itself formally
specified. This is not treated as an architectural boundary decision
(nothing here introduces a new Platform Service, persistence
mechanism, or crosses `ADR-0062`'s own Host/Workspace boundary) — it is
a product-scope expansion, disclosed here rather than silently folded
into "as originally specified."

## What Was Implemented

Five new files under `src/Tempest.App/Workspace/`:
`EngineeringHealthStatus.cs` (the closed, four-value status vocabulary
— `Healthy`/`Attention`/`Blocked`/`Unknown` — `WP8.0C UX
Specification.md` §3), `CockpitAttentionItem.cs`, `CockpitActionItem.cs`,
`CockpitKpiCard.cs` (supporting record types), and
`EngineeringCockpit.cs` (the Cockpit itself). Three modified files:
`WorkspaceManager.cs` (resolves the existing `ICommandRegistry`
Platform Service, constructs the Cockpit), `Workspace.cs` (a new
internal `Cockpit` accessor, mirroring `ProjectExplorerConcrete`'s own
`WP 8.1B` precedent), and `WorkspaceShell.cs` (a new default screen,
toggled by a private `_onCockpit` flag, plus four new interaction
verbs).

| Named Card | Implementation | Real or Placeholder |
|---|---|---|
| Engineering Cockpit (the screen itself) | `WorkspaceShell`'s own default render mode | — |
| What Needs Attention | `EngineeringCockpit.AttentionItems` | Placeholder |
| Continue Where I Left Off | `EngineeringCockpit.ContinueWhereILeftOff`/`ContinueAsync` | **Real** — `NavigationService.RecentItems[0]` |
| Recent Projects | `EngineeringCockpit.RecentProjects` | Placeholder |
| Favourite Projects | `EngineeringCockpit.FavouriteProjects` | Placeholder (honestly empty — favouriting does not exist) |
| Recent Engineering Activity | `EngineeringCockpit.RecentActivity`/`OpenRecentAsync` | **Real** — `NavigationService.RecentItems` |
| Project Health Dashboard | `Health`, `HealthScoreDisplay`, `RequirementsStatus`, `VerificationStatus`, `CalculationStatus`, `DocumentationStatus`, `ReviewStatus` | Placeholder |
| Engineering Health Score | `HealthScoreDisplay` | Placeholder |
| Requirements/Verification/Calculation Status | `*Status` properties | Placeholder |
| Documentation Status | `DocumentationStatus` | Placeholder |
| Review Status | `ReviewStatus` | Placeholder |
| Risk Summary | `RiskSummary` | Placeholder |
| Open Decisions | `OpenDecisions` | Placeholder |
| Blocked Items | `BlockedItems` | Placeholder (honestly empty) |
| Overdue Actions | `OverdueActions` | Placeholder (honestly empty) |
| Upcoming Milestones | `UpcomingMilestones` | Placeholder (honestly empty) |
| Digital Thread Summary | `DigitalThreadSummary` | Placeholder — a fixed count string, **never a traversal**, per this Work Package's own explicit constraint |
| Workspace Status | `AreaCount`, `OpenDocumentCount` | **Real** |
| Quick Actions | `QuickActions` | **Real** — computed from live state (`ContinueWhereILeftOff`, `AreaCount`, `AvailableCommands.Count`), never fixed text |
| Navigation Shortcuts | The Areas list, reused from `WP 8.1A`/`WP 8.1B` | **Real** |
| Global Command Palette integration | `AvailableCommands`/`InvokeCommandAsync` | **Real** — live `ICommandRegistry.Items`, filtered by `CanExecute` |
| Placeholder KPI Cards | `KpiCards` (now six: Requirements/Verification/Calculations/Documentation/Review/Risks) | Placeholder, each marked `IsPlaceholder: true` |

## Interaction Model

The Cockpit is `WorkspaceShell`'s own default screen (`_onCockpit =
true` from construction). From it: a bare number switches to that Area
(leaving the Cockpit, unchanged mechanism from `WP 8.1A`); `run <N>`
invokes an available global command (`WP 8.1C`'s own Command Palette
integration); `continue` re-opens `ContinueWhereILeftOff`; `recent <N>`
re-opens a specific Recent Activity entry. From an Area, `cockpit`
returns to the Cockpit screen (added to `HandleAreaInputAsync`,
`WP 8.1B`'s own vocabulary). This is the same disclosed,
terminal-appropriate realisation of `WP8.0C Interaction
Specification.md`'s own richer model that `WP 8.1B` already
established — "every card shall support Selection, Navigation" is
honoured for every card with a real backing object (Continue, Recent
Activity, Areas, Global Commands); cards with no real backing object
(the Project Health Dashboard's own statuses, Risk Summary, Open
Decisions, Blocked Items, Overdue Actions, Upcoming Milestones, Digital
Thread Summary, Recent/Favourite Projects) are display-only — inventing
selectable targets for them would require exactly the Requirements/
Calculations/Verification/Digital-Thread logic this Work Package's own
constraints explicitly forbid.

## Visual Principles, Realised

- **Information density over decoration**: every region is a plain
  labelled list — no boxes, no padding, no visual flourish beyond
  section headers.
- **Attention naturally drawn to Critical/Warning/Healthy**: a small,
  closed set of bracketed markers (`[BLOCKED]`, `[ATTENTION]`,
  `[HEALTHY]`, `[UNKNOWN]`) — `WorkspaceShell.FormatStatus` — applied
  uniformly to every `EngineeringHealthStatus` value shown on the
  Cockpit. Deliberately **not** ANSI colour: a real colour scheme is a
  disclosed, separate decision this Work Package does not make (see
  Trade-offs, below).
- **Consistent hierarchy**: the same four-question order (where am I /
  what needs attention / is it healthy / what next) organises both this
  report's own table, above, and `RenderCockpit`'s own section order.

## Testing

40 new tests (1552 → 1592), zero regressions, confirmed across three
clean-rebuild runs (two Debug, one Release). `EngineeringCockpitTests.cs`
(31 tests) proves every placeholder card's own fixed shape, every real
card's own live delegation to `NavigationService`/`ICommandRegistry`
(including a real `CanExecute: () => false` descriptor, registered
directly against the real `ICommandRegistry` resolved from the test's
own `ITempestHost`, proving the Cockpit's own filtering), and both new
navigation gestures (`ContinueAsync`, `OpenRecentAsync`, including their
own out-of-range/nothing-yet failure paths). `WorkspaceShellTests.cs`
gained coverage for the Cockpit as the default screen, every new
section heading, the closed status-marker vocabulary, and the `continue`/
`recent <N>` commands end to end via the real sample content
(`Tempest.App.Workspace.Samples`, `WP 8.1B`). Three existing tests that
asserted on the previously-default Area layout (`WP 8.1A`/`WP 8.1B`)
were updated to switch to an Area first, since the Cockpit is now what
`StartAsync` alone renders — a deliberate, disclosed, required
behavioural change, not a regression.

## Platform Integration

Zero new Platform Service, zero new persistence mechanism. The
Cockpit's own only new Platform Service consumption is `ICommandRegistry`
(already shipped, `ADR-0036`/`ADR-0037`), resolved through the identical
`ITempestHost.Services` path every other Workspace service already
uses (`WorkspaceManager.StartAsync`). No existing Platform Service or
Engineering Core contract was modified.

## New ADRs

**None.** `ADR-0069` already decided the Cockpit is the Workspace's own
default landing screen; `ADR-0070` already decided the Command Palette
is a first-class global entry point reachable from any screen. This
Work Package implements both decisions — it partially, disclosedly
realises `ADR-0070` (available commands, filtered and invokable, scoped
to the Cockpit's own dashboard region, not a full floating overlay
reachable from every screen) rather than deciding anything new; neither
decision required revisiting.

## Technical Debt Assessment

**No new Technical Debt item is raised by this Work Package.** Every
placeholder region is a direct, expected consequence of this Work
Package's own explicit "no Requirements/Calculations/Verification/
Digital-Thread-traversal logic" constraint, not a corner cut under
implementation pressure. Two disclosed, deliberate scoping decisions,
neither rising to Technical Debt:

- **No colour rendering.** The Visual Principles ask for attention to
  be "naturally drawn" to status; this Work Package realises that
  through a closed textual marker vocabulary (`[BLOCKED]` etc.) rather
  than ANSI colour codes — introducing real terminal colour is a
  separate, disclosed decision (does every terminal the Shell might run
  in support it? does `ADR-0066`'s own "hand-rolled renderer" choice
  still hold once colour is involved?) this Work Package was not scoped
  to make.
- **Command Palette integration is Cockpit-scoped, not global.**
  `ADR-0070` names the Palette as reachable "from any screen"; this
  Work Package's own Global Commands region is reachable only from the
  Cockpit. Reaching it from an Area screen too is a natural, small
  follow-on, not designed here.

## Repository Metrics

- Production files: 5 new (`EngineeringHealthStatus.cs`,
  `CockpitAttentionItem.cs`, `CockpitActionItem.cs`, `CockpitKpiCard.cs`,
  `EngineeringCockpit.cs`), 3 modified (`Workspace.cs`,
  `WorkspaceManager.cs`, `WorkspaceShell.cs`).
- Tests: 40 new (1552 → 1592), 0 failures, both configurations, clean
  rebuild, stable across three consecutive runs.
- ADRs: 0 new.
- Zero new Technical Debt items, zero new Future Capability Register
  entries.

## Related Documents

`WP8.0A Workspace Architecture Document.md`; `WP8.0B Workspace
Contracts.md`; `WP8.0C UX Specification.md`; `WP8.0C Engineering
Cockpit Specification.md`; `WP8.1A Implementation Report.md`;
`WP8.1B Implementation Report.md`; `ADR-0062`, `ADR-0069`, `ADR-0070`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`;
`docs/academy/03 Work Packages/WP8.1C-engineering-cockpit-implementation.md`.
