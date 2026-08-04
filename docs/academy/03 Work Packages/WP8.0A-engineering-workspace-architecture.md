# WP 8.0A — Engineering Workspace Architecture

## What This Document Is

An architecture-only milestone Work Package, mirroring `WP7.2B
Requirements & Verification Platform Architecture`'s own format — no
production code written, no implementation performed. This document
follows the same whole-review shape (What Was Achieved, Architectural
Lessons, Implementation Lessons, Repository Maturity, Recommendations,
Key Takeaways) rather than the standard 13-section per-feature template,
since that template's own "Files Added"/"Trade-offs" sections apply to
an implementation Work Package, not a pure design phase.

## Introduction

`WP 8.0A` is `v0.8.0`'s own first Work Package — the platform's first
attempt to design what an engineer actually *sees* when using
TempestOS, following two full releases (`v0.6.0`, `v0.7.0`) that built
exclusively infrastructure and backend engineering capability with zero
user-facing product surface beyond a minimum-viable console Shell. It
designs the complete Engineering Workspace: a graphical, multi-panel
desktop presentation built entirely on Platform Services and
Engineering Core capability that already exist and are already
certified, introducing zero new platform capability of its own.

## What Was Achieved

A complete architecture across all twelve areas this Work Package's own
controlling instruction named — workspace philosophy, user journeys,
main window layout, navigation model, Project Explorer, engineering
object hierarchy, docking strategy, view architecture, digital thread
visualisation, workspace state management, extensibility model, and
interaction patterns — produced as five documents (`Workspace
Architecture Document`, `UI Architecture`, `Navigation Specification`,
`Object Relationship Diagrams`, `User Workflow Diagrams`) plus four new
ADRs (`ADR-0062`–`ADR-0065`), each a genuine, locked-in architectural
boundary decision rather than a reserved placeholder. Two further
genuine open questions were named and explicitly deferred, their ADR
numbers reserved (`ADR-0066` concrete UI rendering technology;
`ADR-0067` the object-view extensibility contract shape) rather than
answered speculatively.

## Architectural Lessons

**The reuse-of-existing-mechanism pattern, proven six times at the
service layer, generalises to the presentation layer without
modification.** Every Engineering Core framework (Materials,
Calculations, Verification, Requirements) reached "build on
`IEngineeringDocumentStore`, introduce nothing new" as its own central
decision. This Work Package found the identical pattern holds for a
category of concern none of those frameworks ever had to consider:
Workspace layout state reuses `ISettingsProvider` rather than inventing
a UI-specific persistence mechanism (`ADR-0064`); Digital Thread
visualisation reuses `GetEvidenceAsync` rather than inventing a
traversal platform service (`ADR-0065`). The discipline that produced
six clean backend frameworks produces the identical shape of decision
in a domain (user interface architecture) this project has never
designed for before.

**Separating "what shape is this, architecturally" from "what
technology implements it" let this Work Package reach real, locked-in
decisions without needing to evaluate rendering frameworks it has no
present basis to compare.** `ADR-0062` locks in that the Workspace is
graphical, multi-panel, and additive to the console Shell — a boundary
every other design decision in this Work Package depends on — while
explicitly reserving the concrete UI technology choice (`ADR-0066`) for
a future Work Package that can actually evaluate real trade-offs
(cross-platform reach, licensing, existing .NET 10 usage) empirically
rather than in the abstract.

## Implementation Lessons

Not applicable in the usual sense — no implementation was performed.
The closest analogue: this Work Package's own research phase found that
`TempestShell`'s own existing design (`ADR-0033`–`ADR-0035`) and
`NavigationItem`'s own documentation (`WP 5.0A`) had *already*
anticipated a future graphical shell explicitly, in writing, three
releases before this Work Package needed to design one — "any future UI
shell" appears in `NavigationItem`'s own XML documentation. This is
worth naming as a genuine finding: an architectural decision made three
releases ago, deliberately left open rather than foreclosed, made this
Work Package's own first decision (`ADR-0062`) easier to reach and
easier to justify, since it did not need to argue from first principles
that a graphical shell was even a legitimate architectural destination.

## Repository Maturity

**A pure architecture Work Package still requires real research against
the shipped repository, not only against prior documentation.** This
Work Package read `TempestShell.cs`, `NavigationItem.cs`,
`ICommandDispatcher.cs`/`ICommandRegistry.cs`, and `IRequirementsService.cs`
directly, rather than relying solely on `VISION.md` or prior Academy
articles' own description of these components — confirming, for
instance, that `NavigationItem` already supports the hierarchy
(`ParentId`) and grouping (`Group`) the Project Explorer's own tree
design depends on, without requiring any change to that existing type.
No governance register required correction as part of this review — the
registers touched by `WP 7.4.0` (`Documentation Register.md`,
`Governance Register.md`) remain current, confirmed by spot-check, not
re-audited in full (a full audit was `WP 7.4.0`'s own scope, not this
one's).

## Recommendations for the Next Work Package

1. **A Contract Review Work Package should follow**, defining concrete
   public contracts for whatever new types the Workspace's own
   implementation needs (View base contracts, a Command-dispatch
   convention for Workspace-originated commands) — mirroring the
   Requirements Engine's own `WP 7.2B` → `WP 7.2C` → `WP 7.3A` sequence
   exactly.
2. **Resolve `ADR-0066`** (concrete UI rendering technology) as part of,
   or immediately before, that Contract Review — every other design
   decision in this Work Package is independent of the choice, but
   implementation cannot begin without it.
3. **Resolve `ADR-0067`** (the object-view extensibility contract) once
   a second real consumer exists to validate the shape against — not
   before, per this project's own standing "do not design ahead of a
   concrete second consumer" discipline.
4. **Consider whether `TempestShell` itself needs any change** once the
   Workspace exists alongside it — this Work Package's own position
   (`ADR-0062`) is that it does not, but this should be revisited once
   real Workspace implementation experience exists to confirm or
   revise that judgment.

## Key Takeaways

1. Architecture-only Work Packages should still write real ADRs for
   genuine boundary decisions, not defer everything to a later
   implementation phase — the test is whether a decision is a shape the
   rest of the design depends on (write it now) or an empirical question
   implementation should actually answer (reserve it).
2. A pattern proven repeatedly at one layer (service-layer reuse
   discipline) is worth deliberately testing against a new kind of
   concern (presentation-layer state, UI traversal) rather than assumed
   to only apply where it was first observed.
3. A three-release-old architectural decision, left deliberately open in
   writing rather than silently foreclosed, can materially ease a much
   later Work Package's own first decision — further evidence for this
   project's own standing discipline of recording every non-obvious
   decision, and its own boundaries, at the time it is made.

## Related Documents

`docs/releases/v0.8.0/WP8.0A Workspace Architecture Document.md` and its
four companion deliverables; `ADR-0062`–`ADR-0065`; `docs/academy/
02 Runtime Architecture/17-engineering-workspace.md`; `VISION.md`;
`docs/academy/03 Work Packages/
WP7.2B-requirements-and-verification-platform-architecture.md` (the
format precedent this document follows).
