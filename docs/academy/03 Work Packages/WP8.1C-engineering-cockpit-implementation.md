# WP 8.1C — Engineering Cockpit — Implementation

## 1. Introduction

`WP 8.1C` is `v0.8.0`'s own sixth Work Package, and its third
implementation — following `WP 8.1B` (Navigation & Project Explorer)
directly. It implements the Engineering Cockpit as the Workspace's own
default landing experience (`ADR-0069`): the screen every launch of
TempestOS now presents first, answering four questions on every visit
— where am I, what needs attention, is the project healthy, what
should I do next — consuming only existing Workspace services, with no
Requirements, Calculations, Verification, or Digital Thread traversal
logic of its own.

## 2. Purpose

To give TempestOS a real, coherent front door — replacing "launch into
an empty Project Explorer" with a dashboard that feels complete on day
one, even though no real engineering discipline is wired to it yet,
and to prove the Command Palette's own integration point
(`ADR-0070`) against a real Platform Service (`ICommandRegistry`) for
the first time.

## 3. Background

`WP 8.1A` shipped a Workspace shell that started on an empty Project
Explorer, with no landing screen of its own. `WP 8.0C` had already
specified, in full, what the Cockpit should be (`WP8.0C Engineering
Cockpit Specification.md`'s own seven layout regions) and why
(`ADR-0069`), but nothing had built it. `WP 8.1B` closed the Navigation/
Project Explorer gap; the Cockpit remained the one specified-but-
unbuilt piece of the target experience `WP8.0C UX Specification.md` §0
named explicitly. This Work Package's own controlling instruction
arrived in two parts, the second substantially expanding the named card
set beyond `WP 8.0C`'s own seven regions — followed in full, as the
superseding version, and disclosed as an expansion rather than treated
as if it had always been the specification (`WP8.1C Implementation
Report.md`'s own "A Disclosed Scope Note").

## 4. The Problem

1. **What does "feels complete before backend services are connected"
   actually mean in code**, given this Work Package explicitly forbids
   the Requirements/Calculations/Verification/Digital-Thread logic that
   would make most of the named cards' own data real?
2. **Where does the Cockpit itself live**, given it is not one of the
   twelve `WP 8.0B` contracts and (per `WP 8.1B`'s own precedent) should
   not become a thirteenth without a Contract Review?
3. **How does the Command Palette's own integration actually reach
   `ICommandRegistry`**, given no Workspace component had resolved that
   Platform Service before this Work Package?
4. **Which cards can be made genuinely real** (not placeholder) using
   only what `NavigationService`/`ICommandRegistry` already expose, and
   which honestly cannot without violating this Work Package's own
   scope boundary?

## 5. The Design

`EngineeringCockpit` — internal, reached only through the new
`Workspace.Cockpit` accessor (mirroring `ProjectExplorerConcrete`'s own
`WP 8.1B` precedent) — composes `NavigationService` (already existed)
and `ICommandRegistry` (newly resolved by `WorkspaceManager.StartAsync`,
the Cockpit's own only new Platform Service dependency). Every card
with a real Workspace-service backing (`ContinueWhereILeftOff`,
`RecentActivity`, `AreaCount`, `OpenDocumentCount`, `AvailableCommands`)
is a live read; every other named card (the Project Health Dashboard's
own five statuses, Risk Summary, Open Decisions, Blocked Items,
Overdue Actions, Upcoming Milestones, Digital Thread Summary, Recent/
Favourite Projects) is fixed, representative, and explicitly disclosed
as placeholder — never fabricated to look real.
`WorkspaceShell` gained a second screen (`_onCockpit`, defaulting to
`true`) and four new interaction verbs (`run`, `continue`, `recent`,
`cockpit`). See `WP8.1C Implementation Report.md` for the complete
card-by-card, real-vs-placeholder account.

## 6. Alternatives Considered

**Deferring the Cockpit until a Contract Review formalises a
thirteenth `IEngineeringCockpit` interface** — considered and rejected.
`WP 8.1B` already established the precedent (same-assembly-only
concrete-class extensions for real capabilities the twelve contracts
never named) that makes a Contract Review unnecessary for an internal
addition; the Cockpit follows the identical shape.

**Fabricating fake Requirements/Risk/Decision data so every card would
have a "real" backing object** — considered and rejected; directly
violates this Work Package's own explicit scope boundary and would
mislead a user about what data actually exists.

**Colouring status indicators via ANSI escape codes** — considered and
rejected for this Work Package specifically; a closed, bracketed
textual vocabulary (`[BLOCKED]` etc.) satisfies the "attention
naturally drawn" requirement without deciding a real terminal-colour
question this Work Package was not scoped to answer.

## 7. Why This Solution Was Chosen

It answers every question the controlling instruction's own Definition
of Done names, with zero new Platform Service beyond one already-shipped
one (`ICommandRegistry`), zero new persistence mechanism, and zero
fabricated data — the same "reuse what exists, disclose what doesn't"
discipline `WP 8.1A`/`WP 8.1B` already established, now applied to a
dashboard rather than a tree.

## 8. Architectural Principles

- **Composition Over Inheritance** — `EngineeringCockpit` composes two
  existing services; it introduces no query, cache, or index of its
  own.
- **Single Responsibility Principle** — every placeholder property is a
  simple, independent read; `QuickActions` is the one property computed
  from other state, and it composes rather than duplicates that state.
- **Honesty over completeness-theatre** — a principle this Work Package
  states explicitly for the first time: showing a fixed, disclosed
  placeholder is preferred over hiding an empty region or fabricating
  fake data to make a screen look more finished than it is.

## 9. Files Added

5 new production files (`EngineeringHealthStatus.cs`,
`CockpitAttentionItem.cs`, `CockpitActionItem.cs`, `CockpitKpiCard.cs`,
`EngineeringCockpit.cs`), 3 modified (`Workspace.cs`,
`WorkspaceManager.cs`, `WorkspaceShell.cs`); 1 new test file
(`EngineeringCockpitTests.cs`), 1 modified
(`WorkspaceShellTests.cs`). Full list: `WP8.1C Implementation
Report.md`.

## 10. Trade-offs

- The Command Palette integration is reachable only from the Cockpit,
  not from every screen as `ADR-0070` names — a disclosed, partial
  realisation, not the full decision.
- No colour rendering — status emphasis is textual only
  (`[BLOCKED]`/`[ATTENTION]`/`[HEALTHY]`/`[UNKNOWN]`), a real,
  undecided question for a future Work Package.
- Most named cards remain placeholder, since Requirements/Calculations/
  Verification/Digital-Thread/Project/Risk/Decision services do not
  exist anywhere in this platform yet — expected, not a defect, given
  this Work Package's own explicit constraint.

## 11. Common Mistakes

The mistake most worth naming: treating "placeholder" as license to
invent fake-looking real data. Every placeholder card in this Work
Package's own implementation says so explicitly, in its own rendered
text (`(placeholder)`, "favouriting is not yet implemented," "no
traversal is performed") — the discipline is that a placeholder should
read as obviously provisional to the person looking at it, not as a
value a future contributor might mistake for a real signal.

## 12. Future Evolution

- **The Command Palette's own full, screen-independent realisation**
  — reachable from every screen, not only the Cockpit.
- **The Project Dashboard and Properties/Inspector split** — the next
  named gaps in `WP 8.0C`'s own target experience.
- **Real per-discipline status once Requirements/Verification/
  Calculations are wired to the Workspace** — the natural trigger for
  retiring each of the Project Health Dashboard's own placeholder
  statuses individually, not all at once.

## 13. Key Takeaways

1. A dashboard can "feel complete" honestly — fixed, clearly-disclosed
   placeholder content plus a small set of genuinely live reads, never
   fabricated data pretending to be real.
2. A controlling instruction expanding mid-Work-Package is followed in
   full, as the superseding version, with the expansion disclosed
   explicitly against whatever was specified before it — not silently
   absorbed as if it had always been the plan.
3. The same-assembly-only concrete-class extension pattern `WP 8.1B`
   established for Navigation/Explorer capabilities scales cleanly to
   an entirely new dashboard concept — no new pattern was needed, only
   its second application.

## Architectural Debt Assessment

**None.** Every placeholder region is a direct, expected consequence of
this Work Package's own explicit scope boundary, not a corner cut under
implementation pressure. See `WP8.1C Implementation Report.md`'s own
Technical Debt Assessment for the two disclosed, deliberate scoping
decisions (no colour rendering; Cockpit-scoped, not global, Command
Palette).

## Observations

This is the third implementation Work Package of `v0.8.0` — validated
by the same discipline every implementation Work Package before it has
used (clean Debug/Release builds, 1592/1592 tests, both configurations,
clean rebuild, stable across three runs, up from a 1552 baseline).
Zero new ADRs — the first implementation Work Package in this release
to genuinely need none, since both `ADR-0069` and `ADR-0070` had
already made the decisions this Work Package only needed to build.

## Related Documents

`docs/releases/v0.8.0/WP8.1C Implementation Report.md`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`;
`docs/releases/v0.8.0/WP8.0C Engineering Cockpit Specification.md`;
`docs/releases/v0.8.0/WP8.1B Implementation Report.md`; `ADR-0069`;
`ADR-0070`.
