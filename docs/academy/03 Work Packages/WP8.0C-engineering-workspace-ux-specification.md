# WP 8.0C — Engineering Workspace UX Specification

## What This Document Is

A product-and-UX-design-only milestone Work Package, mirroring `WP8.0A
Engineering Workspace Architecture` and `WP8.0B Workspace Contracts`'s
own whole-review format — no production code written, no
implementation performed. This document follows the same whole-review
shape (What Was Achieved, Architectural Lessons, Implementation
Lessons, Repository Maturity, Recommendations, Key Takeaways) rather
than the standard 13-section per-feature template, since no code exists
for that template's own "Files Added"/"Trade-offs" sections to
describe.

## Introduction

`WP 8.0C` is `v0.8.0`'s own fourth Work Package, following `WP 8.0A`
(architecture) and `WP 8.0B` (contracts) directly, and running after
`WP 8.1A`'s own shell implementation rather than before it — a genuine
sequencing departure from the strict architecture-then-contracts-then-
implementation order `WP 7.2B`→`WP 7.2C`→`WP 7.3A` established, disclosed
rather than absorbed silently (§ Architectural Lessons, below). It
specifies the complete target user experience for the Engineering
Workspace: nine deliverables (a master UX Specification plus eight
companions) covering every one of the 28 named scope areas, from
application start-up through first-run experience.

## What Was Achieved

Nine documents produced: `WP8.0C UX Specification.md` (master — nine
Design Principles, five personas, Visual Language table, a Rendering
Feasibility Disclosure, and two new ADRs summarised); `Screen
Catalogue.md` (26 numbered screens/states, each answering the
governing "what am I looking at / what needs attention / what should I
do next" test and each disclosing "Today vs. Target"); `User Journey
Maps.md` (five persona journeys as mermaid sequence diagrams, each
proven achievable with zero new platform capability); `Interaction
Specification.md` (Command Palette behaviour, keyboard/mouse
interaction tables, context-sensitive action rule); `Navigation
Maps.md` (a global navigation flow diagram, breadcrumb/history/project-
switching behaviour); `Wireframe Sketches.md` (conceptual, technology-
neutral box diagrams of the primary screens); `Workspace Behaviour
Specification.md` (project open/switch, layout save/restore, and an
explicit elaboration of the multi-window/multi-monitor tension);
`Engineering Cockpit Specification.md` (the full landing-dashboard
definition, `ADR-0069`'s own subject). Two ADRs written in full
(`ADR-0069`, `ADR-0070`). No `src/` or `tests/` file was touched —
verified directly against `git status` before commit, satisfying this
Work Package's own explicit "No implementation. No code." constraint.

## Architectural Lessons

**A UX specification surfaces genuine architectural boundary
decisions, not only product preferences — and telling the two apart is
itself the discipline.** Of the many choices this Work Package made
(colour meanings, panel proportions, keyboard bindings, toolbar
population rules), exactly two rose to the level of an ADR: the default
landing screen (`ADR-0069`) and global command discoverability
(`ADR-0070`). Both constrain what a future implementation Work Package
must build against; the rest are product record this document itself
is the authoritative source for, not decisions `docs/adr/` needs to
duplicate. This mirrors `WP8.0B`'s own finding that not every named
"open question" deserves an ADR — the same discipline, applied here to
UX rather than contract design.

**Naming a tension precisely is more valuable than resolving it
prematurely.** The Rendering Feasibility Disclosure (`UX
Specification.md` §5, elaborated `Workspace Behaviour Specification.md`
§5-§6) states exactly which UX ambitions (true multi-window,
multi-monitor placement, a floating Command Palette overlay) stretch
`ADR-0066`'s terminal-based decision, and exactly why (a terminal has
no native floating-window or multi-monitor-placement primitive) —
without choosing a rendering technology, which remains explicitly out
of this Work Package's own scope. A vaguer treatment ("the UX wants
more than the terminal can do") would have given a future `ADR-0066`
revisit nothing concrete to reason from; naming the specific gap gives
it a precise starting point.

## Implementation Lessons

Not applicable in the usual sense — no implementation was performed.
The closest analogue: specifying the Properties/Inspector split
(`Screen Catalogue.md` §10) surfaced that the shipped `WP 8.0B`
contracts have no thirteenth interface for "what proves or relates to
this object" as distinct from `IPropertyInspector`'s own "what this
object is." Rather than inventing one mid-specification (out of scope
for a UX-only Work Package) or ignoring the gap, this document names a
plausible future interface (`IDigitalThreadInspector`) explicitly as
*not designed here* — a disclosed gap for a future Contract Review, the
same "name it, don't design it, don't hide it" discipline `WP8.0B`
itself used for `ADR-0066`/`ADR-0067` at the architecture stage.

## Repository Maturity

**A genuine, disclosed sequencing finding: this Work Package runs after
`WP 8.1A`'s own implementation, not before it.** Every prior two-stage
sequence in this project's history (architecture → contracts →
implementation) had the specification precede the code. Here, `WP
8.1A`'s minimal shell already exists and runs before this UX
specification does. `UX Specification.md` §0 names this directly rather
than presenting the specification as if it already matches what is
running, and every companion document's own "Today vs. Target"
disclosure (most load-bearing in `Screen Catalogue.md`) keeps that gap
visible screen by screen rather than only in one summary paragraph.
This is not treated as a defect in `WP 8.1A`'s own prior work — it
correctly implemented exactly what `WP 8.0B` had approved, with no UX
specification yet available to build toward — but it is a real,
disclosed departure from this project's own established sequencing
discipline, and is named as such so a future Work Package does not
need to rediscover it independently.

## Recommendations for the Next Work Package

1. **A Contract Review should reconcile `WP 8.0B`'s twelve contracts
   against this specification's own richer demands** — the Engineering
   Cockpit's own data needs, the Command Palette's reach into
   `ICommandRegistry`, and the plausible `IDigitalThreadInspector` —
   before a second implementation Work Package builds any of it,
   mirroring the identical two-stage discipline `WP 7.2C` already
   proved out for Requirements.
2. **The first real engineering-domain View should target
   Requirements**, unchanged from `WP8.0B`'s own recommendation — this
   Work Package's own Requirements Author journey (`User Journey
   Maps.md` §5) gives that future implementation a concrete, already-
   specified target to build toward rather than an abstract one.
3. **The Engineering Cockpit should be built before the Command
   Palette**, if the two cannot both ship in one Work Package — every
   persona's own journey begins at the Cockpit (`User Journey Maps.md`,
   "Cross-Journey Observations"), making it the higher-value target of
   the two newly-specified capabilities.
4. **`ADR-0066` should not be revisited speculatively** — only if a
   genuine, demonstrated need for multi-window or multi-monitor support
   actually emerges, unchanged from this project's own standing "do not
   build ahead of demonstrated need" discipline, now applied to the
   Rendering Feasibility Disclosure specifically.

## Key Takeaways

1. Not every "open question" a UX specification touches deserves an
   ADR — the discipline is distinguishing a genuine architectural
   boundary (what screen is default, how discoverability is guaranteed)
   from a product/UX decision this document itself is the authoritative
   record of.
2. A named, unresolved tension (Rendering Feasibility Disclosure) is
   more useful to a future Work Package than either silently ignoring
   the gap or prematurely resolving it by picking a rendering
   technology this Work Package was never scoped to choose.
3. When a specification runs after, not before, the implementation it
   is meant to guide, disclosing that sequencing gap explicitly — and
   holding every downstream document to the same "Today vs. Target"
   discipline — keeps the record honest, rather than letting a
   specification quietly imply the running product already matches it.

## Related Documents

`docs/releases/v0.8.0/WP8.0C UX Specification.md` and its seven
companion deliverables; `ADR-0069`; `ADR-0070`; `docs/academy/
02 Runtime Architecture/17-engineering-workspace.md`; `docs/academy/
03 Work Packages/WP8.0A-engineering-workspace-architecture.md`;
`docs/academy/03 Work Packages/WP8.0B-workspace-contracts.md`;
`docs/academy/03 Work Packages/WP8.1A-workspace-shell-implementation.md`.
