# User Experience & Desktop Application

## 1. Introduction

This is the Academy's own concept guide for `v0.10.0`'s opening
architecture: the Engineering Workspace's presentation moves from a
terminal interface to a graphical desktop application. Written at the
architecture stage (`WP 10.0A`), mirroring `17-engineering-workspace.md`'s
own precedent of being written before implementation exists to
describe, to be updated once real rendering code lands.

## 2. Purpose

To explain *why* TempestOS's presentation layer changes paradigm for
the second time in its history (console → terminal-rich Workspace,
`v0.8.0`; terminal → graphical desktop, `v0.10.0`), and what stays
exactly the same underneath both changes — the Workspace contract
layer itself, untouched since `WP 8.0B`.

## 3. Background

`ADR-0066` (`WP 8.0B`, `v0.8.0`) chose a Terminal User Interface over a
graphical desktop framework, reasoning that no real, demonstrated need
for a graphical experience existed yet, and naming its own reversal
condition explicitly: such a need, demonstrated later. Six real
Engineering Disciplines were then built against that terminal-era
Workspace contract layer (`v0.9.0`, `WP 9.0A`–`WP 9.5A`) — Mechanical,
Requirements, Calculations, Verification, Documents, Manufacturing —
each proving the contract layer (`IWorkspaceView`, `IWorkspacePanel`,
`IProjectExplorerNodeProvider`, `IPropertyFacetProvider`) sound and
stable under real, varied use.

## 4. The Problem

The Product Owner then commissioned Programme 10, "User Experience &
Desktop Application" — naming multi-monitor behaviour, theme
architecture, iconography, and pixel-precise docking as required
topics. None of these is achievable inside a terminal at any fidelity,
regardless of implementation skill: a terminal has no monitor-boundary
concept, no true iconography beyond glyphs, and no pixel-level
geometry. The reversal condition `ADR-0066` itself named — a real,
demonstrated need — was now met, for the first time since that ADR was
written.

## 5. The Design

Two ADRs resolve this, the first supersessions in this project's
history: `ADR-0092` moves the presentation paradigm itself, from
terminal to graphical desktop, explicitly building on — not discarding
— `ADR-0062`'s own original "graphical, multi-panel, additive to
console Shell" framing, realised literally for the first time rather
than reinterpreted narrowly. `ADR-0093` separately moves the Digital
Thread/Object Relationship presentation from a flat list to a
progressively-expandable node-link graph, while explicitly carrying
forward `ADR-0065`'s own core finding: no new platform traversal
capability is needed, only a richer rendering of the same one-hop
reads. Both decisions are architecture-only — zero Workspace contract
changes, confirmed by direct re-read of `IWorkspaceLayout`,
`WorkspacePanelPlacement`, and `WorkspaceDockPosition`, all already
rendering-agnostic since `WP 8.0B`. Two genuine gaps are named and
reserved, not designed speculatively: the concrete UI framework
(`ADR-0094`) and a contract extension for floating/multi-monitor panel
placement (`ADR-0095`).

## 6. Alternatives Considered

Staying within the terminal paradigm and stretching its own vocabulary
to nominally answer the Programme's topic list was considered and
rejected — several named topics are not expressible in a terminal at
all, making that path incomplete by construction, not merely a lesser
design. A browser-based presentation was reconsidered from `ADR-0066`'s
own prior rejection and rejected again, for the identical reason: it
sits awkwardly beside this platform's own "not a general-purpose
application platform" self-description, with no advantage over a
native desktop framework for the same journeys.

## 7. Why This Solution Was Chosen

Because the existing Workspace contract layer already anticipated this
exact moment. `WorkspacePanelPlacement.Size`'s own documentation reads
"deliberately unitless... for example, a column count in a terminal" —
already written to be reinterpreted as pixels the day a graphical
renderer arrived, without changing a single signature. This is the
strongest evidence available that `WP 8.0B`'s own rendering-agnostic
contract design was correct: six disciplines' worth of real Workspace
implementation and a full paradigm change both survive on the identical
contract layer, unmodified.

## 8. Architectural Principles

- **A superseded decision is marked, never erased** (`Engineering
  Governance.md` §5) — both `ADR-0065` and `ADR-0066` remain fully
  readable, their own historical reasoning intact, with a status note
  pointing to what superseded them.
- **A reversal condition, named at decision time, is honoured
  literally when later met** — `ADR-0066` named its own reversal
  condition in 2026-07-30; `ADR-0092` supersedes it only once that
  exact condition (a real, demonstrated Product Owner need) is
  satisfied, not on a whim.
- **Contracts designed rendering-agnostic pay off exactly when a
  rendering paradigm changes** — the entire point of `WP 8.0B`'s own
  design discipline, proven true for the first time by this Work
  Package.

## 9. Benefits

Six real disciplines require zero rework. Every capability the
Programme names becomes achievable for the first time. The governance
mechanism for superseding a decision is exercised successfully, on the
first attempt, establishing a real precedent future Work Packages can
follow with confidence rather than inventing the mechanism under
pressure.

## 10. Trade-offs

This platform's first-ever GUI dependency commitment, deferred but now
real (`ADR-0094`). `TempestShell`'s own console-based test
infrastructure does not carry forward to graphical Workspace testing —
a new test strategy is a genuine open question for the first
implementation Work Package. Multi-monitor's own physical-exposure
consideration (a panel visible on a less-trusted secondary display) is
a genuinely new class of risk this platform has not had to reason about
before.

## 11. Common Mistakes

Assuming "graphical" always meant pixel-based rendering in this
project's own history — it did not; `ADR-0062`'s own use of the word
was deliberately reinterpreted narrowly by `ADR-0066` for over a
year of this project's own timeline, and only this Work Package
resolves it back to its literal meaning. Assuming a supersession
means the original ADR was *wrong* — it was not; `ADR-0066`'s own
reasoning was correct for the evidence available in 2026-07-30, and
this concept guide's own §3-4 exist specifically to make that
distinction clear to a future reader.

## 12. Future Evolution

`ADR-0094` (concrete desktop UI framework) and `ADR-0095` (floating/
multi-monitor panel contract extension) are the two most immediate
next architectural decisions this Programme will need. A future
implementation Work Package's own dedicated test-strategy design for a
graphical presentation layer is named as a real, open question, not
answered here.

## 13. Key Takeaways

A rendering-agnostic contract layer is not a theoretical nicety — it is
what let this platform change its entire presentation paradigm twice
without touching a single existing Workspace implementation. A
reversal condition named honestly at decision time, and honoured
literally when later met, is how a project changes its mind without
looking like it never had one in the first place.

## Related Documents

`ADR-0092`; `ADR-0093`; `ADR-0066`; `ADR-0065`; `ADR-0062`;
`17-engineering-workspace.md`; `WP10.0A UX Architecture Document.md`;
`docs/academy/03 Work Packages/WP10.0A-user-experience-architecture.md`.
