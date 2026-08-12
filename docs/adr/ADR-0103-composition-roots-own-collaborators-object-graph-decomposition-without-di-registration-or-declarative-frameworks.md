# ADR-0103: Composition Roots Own Collaborators — Object-Graph Decomposition Without DI Registration or Declarative Frameworks

## Status

Accepted — `v0.12.0`, `WP 12.0A` (Desktop Composition Root Decomposition
Architecture), 2026-08-12. Architecture only; no production code
accompanies this decision — `WP 12.0B` realises it.

## Context

`WP11.0A Platform Architecture Review.md` Finding `A-1` named two
"composition-root God Objects in the Desktop layer": `MainWindow.cs`
(1,556 lines; a single constructor spans lines 89–1082 — roughly 1,000
lines doing platform-service resolution, five separate desktop-local
state loads, all docking/panel construction and wiring, view
construction, ~450 lines of per-discipline Ribbon object-action wiring,
undo/redo, and window lifecycle) and `EngineeringCockpit.cs` (1,398
lines; a single read-model class carrying six Engineering Disciplines'
worth of computed KPI/status/attention-item properties). Both were
re-verified directly against source, unchanged, by `WP 12.0A`'s own
investigation.

`WP11.0B Architecture Roadmap.md` §5 predicted this Work Package
"likely warrants a new ADR (a genuine structural pattern decision,
Engineering Governance §5)." Investigation confirms it: this platform
already has one, precedented, undecided-in-general-form question —
*when an object graph inside a single project grows past one
responsibility, how is it decomposed?* — and three genuine, structurally
different answers were available. This ADR answers it once, generally,
rather than leaving each future Work Package to re-derive or
re-litigate the question.

**What already exists and is directly extended, not replaced.**
`ADR-0009` established that some services are built outside the DI
container's own construction graph, at the composition root, and named
this as a general principle rather than a single mechanism. `Shell &
Composition Framework Architecture.md` realised it once, at the
platform's own outermost boundary: `Program.cs`/`TempestHostBuilder`/
the Shell is *the* composition root that assembles a running
`ITempestHost` and hands it to a presentation layer. `EngineeringWorkspaceComposer`
(`WP 10.0B`) realised the identical principle a second time, one layer
down: a `public static class` with no state of its own, resolving
platform services once and handing them to per-discipline registration
methods — already the exact precedent `WP11.0A`'s own finding names
directly ("the project already has this pattern... precedent exists to
extend it or add a sibling for Desktop-specific wiring").

**What is genuinely new here.** Neither prior document answers the
question this ADR must: once a composition root's own object graph
grows large — not because the *platform* grew, but because *one
presentation surface's own* responsibilities did — what is the
sanctioned way to keep it from becoming a single, ever-growing class?
`MainWindow`/`EngineeringCockpit` are themselves nested composition
roots (each assembles a bounded object graph for one concern: "the
running desktop window," "the Cockpit's own read surface"), one layer
further down than `EngineeringWorkspaceComposer` — and nothing before
this ADR named the rules for *that* layer.

## Decision

**A composition root that has grown multiple responsibilities is
decomposed by extracting plain, `new`-constructed collaborator classes
it owns and delegates to — never by registering a collaborator in the
DI container, never by a declarative or reflection-based composition
mechanism, and never by a partial-class split.** This is a general
platform pattern, not a decision scoped to any one file. Every rule
below applies to any composition root that consumes an already-built
platform — resolving already-registered Platform Services, never
constructing or registering new ones into `TempestHost`'s own DI
container — at any layer above that boundary: `EngineeringWorkspaceComposer`
today, `MainWindow`/`EngineeringCockpit` as this ADR's own motivating
realisation, and any future one.

**Explicit boundary with `ADR-0009` — this ADR does not extend to, or
narrow, the Runtime Host's own composition root.** `Program.cs`/
`TempestHostBuilder`/`TempestHost` itself remain entirely `ADR-0009`'s
own territory, unmodified and unaffected: the Runtime Host's own
construction and DI-registration of Platform Services
(`IConfigurationProvider`, `ILogger`, and every other `AddInstance`/
`Singleton` call inside `TempestHost.ExecuteStartupPhasesAsync`) is not,
and was never intended to be, governed by this ADR. The two ADRs answer
genuinely different questions. `ADR-0009` governs a service that must be
constructed outside the DI container but still needs to be resolvable
*through* it, because some other, independently DI-constructed consumer
depends on it — `AddInstance` exists precisely to make that possible.
This ADR governs a collaborator with exactly one consumer, ever: the
specific composition root that constructs it directly with `new`. The
question `AddInstance` answers never arises for a collaborator, because
nothing external ever resolves it through any container. A component
genuinely meeting `ADR-0009`'s own description — built outside DI,
needed by some other, independently DI-resolved consumer — is not a
"collaborator" under this ADR at all, regardless of which layer
constructs it; it is answered by `ADR-0009`, unchanged.

### Responsibilities of a composition root

A composition root:

1. **Assembles one specific, bounded object graph for one concern** —
   never the platform's entire capability set (that remains
   `TempestHost`'s own job, unaffected by this ADR). "Bounded" means
   nameable in one sentence: "the running desktop window," "the
   Cockpit's own read surface," "the six Engineering Disciplines'
   Workspace registrations."
2. **Resolves whatever platform services it needs, once, at
   construction time** — via `ITempestHost.Services` or already-injected
   constructor parameters — never repeatedly, never re-resolved later
   from inside a collaborator.
3. **Constructs each collaborator exactly once, in dependency order**,
   handing each only the specific dependencies it needs — never the
   whole resolved-services bundle "in case it needs something later."
4. **Wires the genuinely cross-collaborator bridges that have no single
   natural owner** (an event one collaborator raises that another must
   react to) — this wiring is the composition root's own, irreducible
   job; it is not itself business logic, only connection.
5. **Owns its own lifecycle** (construction, and — where applicable —
   shutdown/disposal) and delegates each collaborator's own save/
   refresh/dispose responsibility to that collaborator directly, never
   inlining it.
6. **Contains no loop, branching business rule, or per-domain-discipline
   knowledge of its own.** If a composition root's own body needs a
   `switch` over Kinds, a per-discipline dictionary, or a multi-step
   validated workflow, that logic belongs to a collaborator, not the
   root.

### Responsibilities of a collaborator

A collaborator:

1. **Has exactly one reason to change**, nameable by its own class name
   (`FOUNDATION.md` non-negotiable #2, applied one layer below the
   platform services that non-negotiable already governs).
2. **Exposes the smallest public surface its own composition root (and,
   where genuinely needed, a sibling collaborator reached only through
   the root's own wiring) requires** — never a surface sized for a
   hypothetical future caller.
3. **Declares its own dependencies via ordinary constructor
   parameters** — never resolves anything itself from a container,
   never reaches back into its own composition root.
4. **Has no dependency on, or awareness of, its sibling
   collaborators.** Cross-collaborator coordination is the composition
   root's job, unconditionally — a collaborator that needs to react to
   another's own state does so through a delegate/event the composition
   root wires, never through a direct reference to the sibling itself.
5. **Takes whichever concrete shape its own responsibility actually
   needs** — a stateless static factory (pure construction, e.g. "build
   this menu") or a stateful instance (e.g. "own this undo/redo stack")
   — a composition root does not force every collaborator into one
   identical shape for its own convenience.

### Ownership and lifetime

A collaborator's lifetime is exactly its composition root's own
lifetime: constructed once, alongside the composition root, held for as
long as the composition root exists, released when the composition
root is (a window closes; a Cockpit read is discarded). A collaborator
is never a singleton spanning more than one composition root instance —
the moment a second, genuinely independent composition root needs the
identical collaborator, that is the trigger to ask whether it has
become a Platform Service (see below), not a reason to let its lifetime
quietly outlive any one root.

### Construction rules

- Every collaborator is constructed with `new`, directly, inside its
  own composition root — never resolved via reflection, a container, or
  a factory-of-factories.
- Construction order matches dependency order — a collaborator is never
  handed a reference to a sibling that does not yet exist. (C#'s own
  compiler already enforces this for a well-ordered constructor body;
  stated here so the *rule*, not merely the compiler's incidental
  enforcement of one instance of it, is on record.)
- A collaborator's constructor may do the same class of synchronous,
  already-precedented I/O this codebase already accepts at composition-root
  construction time (e.g. `SomeState.LoadAsync().GetAwaiter().GetResult()`,
  the pattern `MainWindow` already uses five times) — this ADR
  introduces no new precedent here, and does not relax `ADR-0003`
  (constructors are side-effect-free) for anything beyond what already,
  narrowly, applies at a composition root's own top level.
- **A collaborator never receives its own composition root as a
  constructor dependency.** No back-reference. This is what keeps the
  resulting object graph acyclic, and what makes a collaborator
  constructible and testable in isolation, against real inputs, without
  its own composition root existing at all.

### Dependency rules

- Downward only, exactly as `ADR-0023` already requires platform-wide:
  a collaborator may depend on `Tempest.Core` contracts and
  already-existing Workspace/Domain types; nothing below a collaborator
  ever depends on it.
- **A collaborator never depends on a sibling collaborator directly.**
  Any data or notification one collaborator needs from another flows
  through the composition root's own wiring — an event, a callback
  delegate, or a value passed once at construction — never a direct
  field or constructor reference between two collaborators.
- This ADR introduces no new dependency edge between projects.
  `Tempest.Desktop`, `Tempest.App.Workspace`, and `Tempest.Core` keep
  exactly the dependency graph they already have — this pattern shapes
  how one project's own *internal* object graph is cut, not which
  projects reference which.

### What must never be registered in DI

**A collaborator extracted under this ADR is never registered with
`ServiceCollection`/`AddInstance`/`Singleton`/`Transient`.** Doing so
would make it resolvable from anywhere in the same process for the rest
of that process's life — the opposite of why it was extracted (a
narrow, single-purpose object owned by exactly one composition root,
not a platform capability). The test to apply, reusing `ADR-0013`'s own
Future Considerations question verbatim: *does the rest of the
platform — another module, another composition root's own collaborator
set — genuinely need this to exist before it can function at all?* A
collaborator extracted under this ADR fails that test by construction;
if a real, demonstrated second consumer ever does need it independently
of its original composition root, *that* is a new architectural
question — whether it has become a genuine Platform Service — decided
on its own merits, with its own ADR, at the point a real second
consumer actually exists. It is never decided speculatively, in
advance, by registering "just in case."

### Why partial classes are not an acceptable decomposition mechanism

A `partial class` is still exactly one class: one instance, one shared
field/state bag, and a file-boundary-only separation the compiler
itself does not enforce as a real boundary — any method in any
partial-class file can silently reach into any field declared in any
other partial-class file belonging to the same type. `FOUNDATION.md`
non-negotiable #2 requires a component's boundary be "enforced
structurally, not by convention alone" — a partial-class split enforces
nothing a compiler, a code reviewer, or a future contributor can rely
on; it is a naming convention wearing the shape of a boundary. It also
solves nothing about the actual defect Finding `A-1` names: it does not
shrink a 1,000-line constructor (construction still happens in one
place, however many files the type's *methods* are spread across), and
it does not let a future test exercise "just the Undo/Redo behaviour"
without constructing the entire, still-single, still-large type.
Precedent: this is the identical structural-over-conventional reasoning
`ADR-0017` already applied to a comparable temptation (closing off a
module's own path back into Discovery/Registration/Lifecycle, rather
than trusting convention alone to keep it out).

### Why this pattern is preferred over service extraction (DI registration)

A collaborator extracted under this ADR exists, by construction, to
serve exactly one composition root's own object graph — it is not a
capability any module, plugin, or independent composition root could
plausibly need on its own, which is precisely `ADR-0023`'s own dividing
line between "Platform Service" and everything else. Registering it in
DI anyway would grant it a lifetime and a resolution path independent
of its own composition root, silently widening its blast radius and
reopening the "is this a Platform Service or something narrower"
classification question `ADR-0013`'s Future Considerations already
flags as needing an explicit, evidence-based answer — for something
that plainly fails that test today. It would also grow the Dependency
Injection Register's own scope with entries serving no genuine second
consumer, ever — real, avoidable governance debt for zero benefit,
which this project's own `Governance Philosophy.md` already treats as
worse than no register entry at all.

### Why this pattern is preferred over declarative/reflective composition

A declarative or reflection-based composition mechanism (attributes,
auto-registration, a "Desktop feature module" discovered the way
`IModule`/plugins are) solves a problem this platform does not have: an
open, third-party-extensible set of Desktop composition-root
collaborators. Today there is exactly one Desktop composition root
(`MainWindow`) and one Cockpit read-model composition root
(`EngineeringCockpit`), each with a fixed, known-at-compile-time
collaborator set — nothing like the genuinely open-ended, unbounded set
Module/Plugin Discovery exists to handle. This platform's own precedent
on exactly this question is explicit and has already been applied
twice: reflection-based discovery is introduced only where a real,
demonstrated extensibility need exists (Modules, Plugins, a simplified
form for Hosted Services) — and was explicitly **not** reused a fourth
time for Navigation (`ADR-0032`: "not every new platform capability
needs a new discovery mechanism just because three prior ones did"). A
declarative Desktop-composition framework would be a fifth attempt at
the identical mechanism, for a problem with no genuine trigger,
building exactly the class of speculative capability this project's own
discipline (already applied to defer Plugin packaging for the Sample
Module, and — within `WP 12.0A`'s own investigation — to reject
building this very framework for `MainWindow`) consistently argues
against. Ordinary `new` construction is also strictly more debuggable
(a stack trace shows real construction, not reflection indirection),
strictly more compile-time-verifiable (a missing dependency is a
compiler error, not a runtime discovery failure), and requires zero new
SDK, zero new discovery service, and zero new failure mode to design —
the entire mechanism is C# itself, exactly as `EngineeringWorkspaceComposer`
already proves works at the layer immediately above this one.

## Consequences

**Positive:**

- **One, general, citable answer** to "how do I decompose a composition
  root that has grown too large," applicable at any layer of this
  platform, not re-derived per Work Package. `WP 12.0B` (`MainWindow`/
  `EngineeringCockpit`) is the first realisation, not the last —
  `Desktop Composition Architecture.md` names this pattern's future
  applicability explicitly.
- **Zero new abstraction, zero new mechanism, zero new failure mode.**
  Every rule above is either already-proven precedent
  (`EngineeringWorkspaceComposer`, `ADR-0009`) applied one layer down,
  or a direct, structural application of `FOUNDATION.md`'s own existing
  non-negotiables. Nothing about `TempestHost`, Discovery, Registration,
  Lifecycle, or any Platform Service changes.
- **Testability improves as a direct, structural consequence**, not
  merely a hoped-for side effect: a collaborator with its own
  constructor and no back-reference to its composition root can be
  constructed and exercised in isolation, against real inputs, exactly
  as this project's own "prefer real implementations over mocks"
  testing convention already prefers.
- **The Dependency Injection Register's own scope stays honest** —
  nothing is added to it as a side effect of this ADR, since nothing
  extracted under it is ever DI-registered, by rule.

**Negative:**

- **More types, more files, for an equivalent amount of behaviour.**
  This is a deliberate, accepted cost — the alternative (one file
  keeps growing) is the defect this ADR exists to stop, not a neutral
  status quo.
- **A composition root's own wiring code (the cross-collaborator
  bridges named in Responsibility 4) can itself grow, if left
  unwatched, into the same kind of sprawl this ADR is meant to
  prevent.** Not solved by this ADR — a composition root that
  accumulates too much of *its own* wiring logic is itself a candidate
  for the identical question this ADR answers, applied recursively. No
  new rule is invented for that case; the same decomposition pattern
  applies again, at whatever depth it is next needed.
- **A future contributor must learn one more named vocabulary term**
  ("collaborator," alongside "Platform Service," "Module," "Platform
  API") — mitigated by this ADR's and `Desktop Composition
  Architecture.md`'s own complete definitions, and by the term
  describing something genuinely different from all three existing
  ones (never DI-public, never discovered, never Host-owned).

## Alternatives Considered

**Registering extracted collaborators as DI-public Platform Services.**
Rejected — see "Why this pattern is preferred over service extraction,"
above. Would misclassify Desktop-local presentation wiring as a
platform capability, violating `ADR-0023`'s own layering, and would
grow the Dependency Injection Register with entries no second consumer
will ever use.

**A generic, declarative "Desktop feature module" or "View-Model"
composition framework.** Rejected — see "Why this pattern is preferred
over declarative/reflective composition," above. Solves an
extensibility problem this platform does not have, at the cost of a
wholly new discovery/registration mechanism and failure mode, for a
fixed, small, compile-time-known collaborator set.

**Splitting `MainWindow`/`EngineeringCockpit` via `partial class`
rather than extracting genuinely separate types.** Rejected — see "Why
partial classes are not an acceptable decomposition mechanism," above.
Enforces no real boundary, does not shrink the actual defect (a single
oversized constructor / a single class needing six disciplines' worth
of injected services), and does not improve testability.

**Doing nothing — leaving `MainWindow`/`EngineeringCockpit` as-is.**
Rejected, per `WP11.0A Platform Architecture Review.md`'s own
disposition for Finding `A-1` ("Before v1.0" — "the file will only keep
growing at the current one-feature-per-Work-Package cadence" and "the
direct mechanism behind the recurring `TD-39`-class theming bugs").

## Future Considerations

**This ADR governs the *pattern*; it does not itself decompose
anything.** `WP 12.0B` realises it against `MainWindow`/
`EngineeringCockpit` specifically, and records its own actual,
final collaborator boundaries in its own Implementation Report and
Academy retrospective — those boundaries are an implementation-stage
judgement applying this ADR's rules, not a re-opening of this ADR
itself, exactly as `ADR-0027`'s own rules were realised, unmodified, by
`WP 4.4B`'s later implementation.

**A future composition root at any layer** (a further Desktop feature,
a future second presentation layer, a future Workspace-layer read model)
applies this ADR directly, without a further ADR, unless implementation
surfaces a genuine question this ADR's own rules do not already answer
— per `Future Work Package Guidelines.md` §9, that would stop and
report, not quietly redesign.

**If a collaborator ever gains a real, demonstrated second, independent
consumer**, the Platform Service classification question named under
"What must never be registered in DI," above, is decided then, on real
evidence, with its own ADR — never spun forward as an assumption this
ADR makes now.

## Related Documents

`ADR-0009` (Composition Root owns externally-created services — the
general principle this ADR shares, applied to a structurally distinct
case: a collaborator with exactly one consumer, never made resolvable
through DI, as opposed to `ADR-0009`'s own externally-constructed
services that still need to be DI-resolvable for other consumers — see
this ADR's own explicit boundary statement under Decision); `ADR-0013` (platform-
service/module failure boundary — the classification test this ADR
reuses); `ADR-0017` (Discovery/Registration/Lifecycle remain Host-owned
— the precedent for structural-over-conventional boundaries); `ADR-0023`
(four-layer platform model, downward-only dependencies); `ADR-0032`
(reflection-based discovery is not reused without a genuine
extensibility trigger — the direct precedent this ADR's declarative-
composition rejection extends a second time); `FOUNDATION.md`
non-negotiables 2 and 9; `Shell & Composition Framework Architecture.md`
(the platform's own outermost composition root); `Desktop Composition
Architecture.md` (this ADR's own realisation, motivated by `MainWindow`/
`EngineeringCockpit`); `docs/releases/v0.11.0/WP11.0A Platform
Architecture Review.md` (Finding `A-1`); `docs/releases/v0.11.0/WP11.0B
Architecture Roadmap.md` §3/§5; `docs/releases/v0.12.0/WorkPackages.md`
(`WP 12.0A`/`WP 12.0B`).
