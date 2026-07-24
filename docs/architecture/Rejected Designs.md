# Rejected Designs

## Purpose

A permanent, indexed record of abstractions, patterns, and capabilities
that were seriously considered during a work package's design phase and
explicitly not built. This document exists so that "why don't we have
X" always has a citable answer, rather than an answer that only exists
inside whoever remembers the conversation — or worse, inside a retrospective's
prose, technically written down but never indexed anywhere a future
contributor would think to look.

**This is not a list of things nobody thought of.** Every entry below was a
real candidate, weighed against real criteria, and declined for a reason
someone can still check today. A rejected design is not a lesser cousin of
an ADR — it is the mirror image of one: an ADR records what was decided;
this document records what was deliberately *not* built, and why.

## How to Read an Entry

Each entry gives the design that was considered, why it was rejected, how
expensive it would be to introduce later (a design rejected because it
would be nearly free to add later, if ever needed, is a very different
kind of rejection from one ruled out as fundamentally the wrong shape), and
what — if anything — should prompt revisiting the decision. See Engineering
Governance §10 for when a new entry is required and how this log is
maintained.

**Entries are never deleted or renumbered.** A design that is later
reconsidered and built gets its entry marked **Superseded**, pointing to
whatever ADR or retrospective reversed it — the history stays whole,
exactly as Engineering Governance §5 already requires for ADRs.

---

## RD-0001 — `ICommand<TResult>` / `ICommandHandler<T>` Now

**Considered during:** WP 4.0 (Platform Contracts).

**Rejected because:**
- The release's own six-contract scope for WP 4.0 did not name a result
  type or handler contract — only `ICommand` itself.
- Designing a handler/result shape before Command Framework (`WP 4.7`) has
  actually reasoned about dispatch would be speculative design ahead of
  real understanding — exactly what WP 4.0's own governing philosophy
  ("only define a contract when there is enough understanding to make it
  stable") exists to prevent.

**Reversibility.** Cheap. `ICommand` is an empty marker interface; adding a
handler contract alongside it later cannot break anything already built
against `ICommand` itself.

**Revisit trigger.** `WP 4.7` (Command Framework) — this is not a
permanent rejection, it is a deferral with a named owner.

**Source.** WP 4.0 retrospective, Alternatives Considered.

---

## RD-0002 — `INavigationProvider` / `IDiagnosticsProvider` in WP 4.0

**Considered during:** WP 4.0 (Platform Contracts).

**Rejected because:**
- Neither Navigation's nor Diagnostics' architecture had been designed yet
  at the time WP 4.0 ran. Defining either contract — even marked
  provisional — would have been a guess wearing the appearance of a
  decision.
- Matches the precedent already set by ADR-0015's Future Considerations
  against speculative, ahead-of-need design.

**Reversibility.** N/A — these contracts simply do not exist yet; there is
nothing to unwind.

**Revisit trigger.** `WP 4.6A` (Navigation Architecture) defines
`INavigationProvider`; `WP 4.8` (Diagnostics Improvements) defines
`IDiagnosticsProvider`. Both are active, named owners, not open-ended
deferrals.

**Source.** WP 4.0 retrospective, Background and Alternatives Considered.

---

## RD-0003 — Module Builder Pattern

**Considered during:** WP 4.1 (Module SDK).

**Rejected because:**
- No second consumer — module construction today is `new MyModule()`, and
  no evidence surfaced during design review that constructing a module is
  complex enough to need a builder.
- A builder would add a layer of indirection over what a plain constructor
  call already does completely.

**Reversibility.** Can be introduced later without breaking any existing
API — a builder would be additive, sitting alongside `ModuleBase`/
`ModuleLifecycleBase`, not replacing them.

**Revisit trigger.** If a future module's construction genuinely becomes
complex enough to warrant one (for example, optional dependencies with
several valid combinations) — not expected from anything currently
planned, including the Sample Module (`WP 4.3`) or Plugin Manifest
(`WP 4.2`).

**Source.** WP 4.1 retrospective, Alternatives Considered.

---

## RD-0004 — Registration Helpers

**Considered during:** WP 4.1 (Module SDK).

**Rejected because:** registration is already fully automatic — the
Runtime Host loops over discovered descriptors and calls
`RuntimeModuleManager.Register` itself. There is no per-module
registration boilerplate today for a helper to remove.

**Reversibility.** N/A — nothing exists to reverse; this was rejected
outright, not deferred.

**Revisit trigger.** None currently foreseen. Would require registration to
stop being fully automatic, which would itself be a significant Runtime
Host architecture change, not a Module SDK one.

**Source.** WP 4.1 retrospective, Alternatives Considered.

---

## RD-0005 — Module Metadata / `ToString()` Convenience

**Considered during:** WP 4.1 (Module SDK).

**Rejected because:** several existing log call sites already format a
module as `"{Name} v{Version} ({Id})"`, which made a `ToString()` override
on `ModuleBase` tempting — but no current code would consume it without
also refactoring those existing, already-shipped call sites, which WP 4.1
was explicitly told not to do ("do not perform unrelated refactoring").
"Every public API must have a real consumer today" ruled it out cleanly.

**Reversibility.** Cheap — a `ToString()` override is purely additive and
can be added at any time without breaking anything.

**Revisit trigger.** If a future work package finds a genuine, current
consumer for a formatted module string (for example, a diagnostics or
health-report view, `WP 4.8`) — not before.

**Source.** WP 4.1 retrospective, Alternatives Considered.

---

## RD-0006 — A Dedicated `Tempest.SDK` Project

**Considered during:** WP 4.1 (Module SDK).

**Rejected because:** two small classes (`ModuleBase`, `ModuleLifecycleBase`)
do not justify a new project's build and packaging overhead.
`Tempest.Core.Modules` already holds `IModule`/`IModuleLifecycle`, and
keeping the convenience implementations alongside their own contracts
matches how every other capability in the platform is organised.

**Reversibility.** Moderate cost later — moving public types to a new
project/namespace after they have real consumers is a breaking change for
anyone already depending on their current location, unlike the other
entries in this log, which are all purely additive if reversed.

**Revisit trigger.** If the SDK's surface grows enough, across future work
packages, that bundling it inside `Tempest.Core` starts to feel like the
wrong packaging — no specific trigger named yet; this should be judged by
volume, not a fixed date or work package.

**Source.** WP 4.1 retrospective, Alternatives Considered.

---

## RD-0007 — Service-Locator Workaround for Module Constructor Dependencies

**Considered during:** WP 4.1 (Module SDK), while documenting the
parameterless-constructor constraint (see the Module SDK entry in
`Platform Service Map.md`).

**Rejected because:** a pattern letting a module resolve its own
dependencies post-construction (rather than via constructor injection) is
exactly the kind of hidden reflection and runtime surprise the Module SDK
was explicitly told to avoid. It would also only paper over the real
constraint (Discovery and `TempestServiceProvider`'s construction rules
only both hold for a zero-argument constructor), not fix it.

**Reversibility.** N/A — rejected as the wrong shape of solution entirely,
not deferred pending more information.

**Revisit trigger.** Not expected to be revisited as stated. If the
underlying constraint is ever lifted, it should be lifted at the Discovery/
`TempestServiceProvider` level (a Runtime Foundation-level architectural
decision, with its own ADR), not worked around at the SDK level a second
time.

**Source.** WP 4.1 retrospective, Alternatives Considered; Platform Service
Map, Module SDK entry.

---

## RD-0008 — `IPluginManifestSource` Abstraction

**Considered during:** WP 4.2 (Plugin Manifest architecture).

**Rejected because:** generalising *where* a plugin manifest comes from
(local filesystem today, something else hypothetically later) has no
second source in view, and no near-universal multi-source expectation the
way configuration does (files, environment variables, CLI arguments are
all common in real software; alternative plugin-manifest sources are not a
comparable, well-understood pattern). `IConfigurationSource` earned its
abstraction on that basis; this would not.

**Reversibility.** Cheap. A concrete manifest-discovery implementation can
be refactored behind a new interface later without breaking any consumer
that only ever depended on `IPluginManifestDiscoveryService`'s own output
(`PluginManifest` values), not on how they were produced.

**Revisit trigger.** If a second, genuinely different manifest source is
actually needed (for example, manifests embedded in a different packaging
format) — not before.

**Source.** `Plugin Manifest Architecture.md`, Candidate Public API.

---

## RD-0009 — Maximum / "Tested Up To" Platform Version in the Manifest

**Considered during:** WP 4.2 (Plugin Manifest architecture).

**Rejected because:** a version *ceiling* raises real policy questions —
warn, block, or allow-with-warning when the platform outgrows a plugin's
tested range — that have no real-world experience behind them yet, since
no plugin has ever existed. Designing that policy now would be guessing;
`MinimumPlatformVersion` alone answers every compatibility question this
release actually needs answered.

**Reversibility.** Cheap — purely additive to the `PluginManifest` shape;
no existing field or consumer would need to change to add it later.

**Revisit trigger.** Once real plugins and real version history exist to
design a ceiling policy against — not before, and not speculatively.

**Source.** `Plugin Manifest Architecture.md`, Versioning Strategy.

---

## RD-0010 — Host-Fatal Plugin Failures

**Considered during:** WP 4.2B (ADR-0025, Plugin Failure Classification).

**Rejected because:** Module Discovery's own existing
`DuplicateModuleIdException` is Host-fatal because it protects the
integrity of the platform's foundational, non-optional module catalogue.
A plugin is, by definition, optional add-on content — treating its
failure as equivalent to a foundational platform-service failure would
directly contradict this work package's own governing design principle,
"fail one plugin, not the platform."

**Reversibility.** Expensive to introduce later in the sense that matters
most: it would be a behavioural regression, not an addition — any plugin
author who had come to rely on "my plugin failing doesn't take down the
platform" would be surprised by a later change to Host-fatal. Cheap only
in the narrow sense that no code exists yet to migrate away from.

**Revisit trigger.** Not expected to be revisited. If a future need
arises for some plugins to be load-bearing, the correct mechanism is a
new, explicit declaration a plugin makes for itself (see RD-0011's own
revisit trigger) — not a change to this default.

**Source.** ADR-0025, Alternatives Considered.

---

## RD-0011 — Per-Plugin `IsCritical` Manifest Opt-In

**Considered during:** WP 4.2B (ADR-0025, Plugin Failure Classification).

**Rejected because:** `ICriticalBackgroundService` (ADR-0021) is a
meaningful opt-in specifically because a background service is a *live,
running component* capable of making that self-assessment. Every failure
category ADR-0025 governs happens *before* a plugin's module instance
ever exists — there is no live component available to declare anything.
A manifest-level `IsCritical` flag would also be exactly the kind of
speculative field `Plugin Manifest Architecture.md` already declined to
add (Author, Description, a version ceiling) without a real, demonstrated
need driving its shape.

**Reversibility.** Cheap — purely additive to the `PluginManifest` shape;
no existing field or consumer would need to change to add it later, if a
real need for it is ever demonstrated.

**Revisit trigger.** If a genuine, demonstrated need arises for some
plugins to be load-bearing enough that their failure should abort
startup — not speculatively, and not merely because
`ICriticalBackgroundService` happens to look like a reusable template.

**Source.** ADR-0025, Alternatives Considered.

---

## RD-0012 — A Single Combined Plugin Discovery/Loading Phase

**Considered during:** WP 4.2C (ADR-0026, Plugin Discovery Lifecycle
Placement).

**Rejected because:** folding manifest validation (side-effect-free) and
assembly loading (a real, harder-to-reverse side effect — no unloading
support exists) into one phase would blur exactly the distinction Module
Discovery/Module Registration's own existing two-phase split already
protects: finding and validating candidates is kept separate from
committing to something consequential. One phase would also make it
harder to state precise entry/exit criteria for each half independently.

**Reversibility.** Expensive to merge later in the sense that matters:
once implemented as two phases, with their own distinct entry/exit
criteria and failure semantics, collapsing them would be a real design
change, not a trivial one. Cheap only because no code exists yet either
way.

**Revisit trigger.** Not expected to be revisited — this mirrors an
already-established, working precedent (Module Discovery/Registration)
rather than proposing something new and unproven.

**Source.** ADR-0026, Alternatives Considered.

---

## RD-0013 — Renumbering All Thirteen Existing Host Lifecycle Phases

**Considered during:** WP 4.2C (ADR-0026, Plugin Discovery Lifecycle
Placement).

**Rejected because:** inserting two new phases before the existing Module
Discovery (Phase 4) would, under a strict sequential renumbering, shift
every subsequent phase number by two — touching every existing
cross-reference across `Host Lifecycle.md`, `Runtime State Machine.md`,
`Startup Sequence.md`, `Failure Behaviour.md`, prior ADRs, and prior
Academy retrospectives that cite a phase by number. That blast radius is
entirely disproportionate to what is, architecturally, a pure insertion —
decimal sub-numbering (`3.1`, `3.2`) says exactly the same thing without
touching anything that already works.

**Reversibility.** Cheap to *not* do (the option always remains open to
renumber later if decimal numbering ever proves genuinely confusing in
practice) — expensive to do preemptively, for a cost with no
corresponding benefit today.

**Revisit trigger.** If decimal phase numbers cause genuine, recurring
confusion once Plugin Manifest (and later, potentially, Background
Services — see ADR-0026's Future Considerations) actually implement
against them — not speculatively.

**Source.** ADR-0026, Alternatives Considered.

---

## RD-0014 — Plugin Discovery Reading Platform Version Metadata Independently

**Considered during:** WP 4.2C (ADR-0026, Plugin Discovery Lifecycle
Placement).

**Rejected because:** WP 4.2A's own stated goal was "a single
authoritative runtime platform version," queryable from exactly one
place. Giving Plugin Discovery its own, separate way to read the
executing assembly's version metadata would directly contradict that
goal and risk two independent readings of the same underlying metadata
silently diverging over time (for example, if one code path is updated
to account for a future informational-version suffix and the other is
not). Moving one already-existing constructor call earlier is cheaper,
and strictly more correct, than maintaining two.

**Reversibility.** N/A — rejected as the wrong shape of solution
entirely, not deferred pending more information.

**Revisit trigger.** Not expected to be revisited.

**Source.** ADR-0026, Alternatives Considered.

---

## RD-0015 — Packaging the WP 4.3 Sample Module Through the Plugin Manifest System

**Considered during:** WP 4.3 (Sample Module, architecture phase).

**Rejected because:** `WP 4.3`'s own approved scope explicitly names this
as optional "if ready" — and `WP 4.2` has since made it ready — but two
facts make it not worth doing now. First, `Tempest.App` does not run
`TempestHost` at all today (a pre-existing condition, not introduced by
this decision), so the packaging choice does not change whether a plugin
can be observed loading in a genuinely running process — that benefit is
unavailable either way until `Tempest.App` is separately wired to the
Host. Second, the remaining benefit — proving Plugin Discovery/Loading
against a real, non-synthetic assembly rather than a
`PersistedAssemblyBuilder`-built test double — is already substantially
covered by `WP 4.2`'s own test suite. The cost this option would add —
genuine build/publish tooling to stage a compiled module and a
hand-authored `plugin.manifest.json` into `Plugins/<name>/`, none of
which exists in any form yet — is disproportionate to that incremental
benefit for a work package estimated at **S** complexity.

**Reversibility.** Cheap. Nothing about building the sample module as an
ordinary, project-referenced module forecloses packaging it (or a copy of
it) as a plugin later — the two are not mutually exclusive, and a plugin
version could be added purely additively once the build tooling exists
and `Tempest.App` has a reason to load it that way.

**Revisit trigger.** Once `Tempest.App` is wired to `TempestHost` (making
a real running-process demonstration possible), or once `WP 4.9`
(Developer Experience) needs a real example plugin to scaffold a template
from — not speculatively before either exists.

**Source.** `Sample Module Architecture.md`, Alternatives Considered.
