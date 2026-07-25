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

**Update, WP 4.4A.** The underlying constraint is now addressed —
ADR-0027, *A Declarative `ModuleMetadataAttribute` Decouples Discovery
From Construction* — exactly via the path this entry's own revisit
trigger named: a Discovery-level decision, with its own ADR, not a second
SDK-level workaround. This entry's own rejection is **not superseded**: a
service-locator or post-construction resolution pattern remains exactly
as rejected as it was when this entry was written; ADR-0027 solves the
same underlying problem through ordinary constructor injection instead.

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

---

## RD-0016 — Deferring Module Metadata Reading Until After Dependency Injection Is Built

**Considered during:** WP 4.4A (ADR-0027, Dependency Injection for
Discovered Modules).

**Rejected because:** resolving every module's real instance first, then
reading `Id`/`Name`/`Version` from it for both registration and lifecycle
purposes, would eliminate Discovery's own throwaway instance entirely —
but `RuntimeModuleManager.Register`'s duplicate-`Id` detection and
`ServiceCollection.AddDiscoveredModules`'s own registration both need
every module's `Id` and concrete type *before* the container is built.
Deferring metadata reading until after Dependency Injection Built would
require Discovery and Registration to follow, not precede, the container's
own construction — directly inverting ADR-0011's already-decided ordering,
itself a consequence of ADR-0008's independent-of-DI Discovery. Rejected
as contradicting two already-settled decisions, not merely as a worse
option among open ones.

**Reversibility.** N/A — rejected as incompatible with existing, decided
architecture, not deferred pending more information.

**Revisit trigger.** Only if ADR-0011's own ordering is itself revisited
from first principles — not expected, and not a reason this entry's own
rejection should be read as provisional.

**Source.** ADR-0027, Alternatives Considered.

---

## RD-0017 — A Second, Always-Parameterless "Descriptor" Type Per Module

**Considered during:** WP 4.4A (ADR-0027, Dependency Injection for
Discovered Modules).

**Rejected because:** Discovery could instantiate a lightweight,
always-parameterless descriptor type that names the module's real
implementation type for DI to construct later — mirroring the Plugin
Manifest/plugin-assembly split `WP 4.2` already established at the
process-boundary level. At the single-assembly, single-module level this
proposal operates at, it would require every module wanting DI access to
author two classes instead of one, reintroducing exactly the per-module
boilerplate `WP 4.1`'s SDK exists to eliminate — a materially heavier cost
than one optional attribute, for the identical result.

**Reversibility.** Cheap to avoid now; expensive to introduce later only
in the sense that any module already migrated to the chosen
(`ModuleMetadataAttribute`) approach would need rewriting to adopt this
one instead — not expected to be worth that cost given the attribute
already solves the same problem more cheaply.

**Revisit trigger.** If a genuine need arises for Discovery to know more
about a module than three strings and a type — a scenario broad enough to
justify a real, separate descriptor type — not for this problem alone.

**Source.** ADR-0027, Alternatives Considered.

---

## RD-0018 — Static Abstract Interface Members on `IModule` for Metadata

**Considered during:** WP 4.4A (ADR-0027, Dependency Injection for
Discovered Modules).

**Rejected because:** C# 11's static abstract interface members
(`static abstract string Id { get; }`) would let Discovery read metadata
via reflection on the `Type` itself, with no instantiation and no
attribute, at all. Rejected because it would require changing `IModule`'s
own, long-settled instance-property contract for every module ever
written — `ModuleBase`, `ClockModule`, every test fixture across the
codebase — a breaking change of a scale this ADR's own problem does not
justify, when an additive, opt-in attribute solves the identical problem
for exactly the one category of module that actually needs it, without
touching any existing module at all.

**Reversibility.** Expensive to introduce later in the sense that matters:
changing `IModule`'s own contract after every existing module already
depends on its current, instance-property shape would be a breaking
change regardless of when it happens — this is not a cost unique to
rejecting it now.

**Revisit trigger.** Not expected to be revisited. Would require a much
larger, platform-wide justification than "modules should be able to use
constructor injection," which the chosen, additive design already
satisfies without it.

**Source.** ADR-0027, Alternatives Considered.

---

## RD-0019 — DI-Auto-Discovered Event Handlers

**Considered during:** WP 4.4 (ADR-0028, Event Bus Dispatch, Subscription,
and Failure Model).

**Rejected because:** letting the Event Bus discover every registered
`IEventHandler<T>` implementation automatically, rather than requiring an
explicit `Subscribe` call, would require `TempestServiceProvider` to
resolve *every* registration for a given service type
(`IEnumerable<IEventHandler<TEvent>>`) — a genuine new container
capability. `ADR-0005`'s deliberately minimal container has never needed
multi-registration resolution and does not have it; adding it would be a
real Dependency Injection platform-service change, explicitly out of this
work package's own scope, for a convenience no current consumer has asked
for. Imperative `Subscribe`/`Unsubscribe` achieves the identical outcome
with zero container changes.

**Reversibility.** Cheap to add later, purely additively, alongside
imperative subscription rather than replacing it, if a real need for
auto-discovery ever emerges (for example, a future work package with many
handlers where explicit `Subscribe` calls become genuinely repetitive
boilerplate).

**Revisit trigger.** If a real, demonstrated repetition problem emerges
from imperative subscription across many modules — not speculatively now,
with zero real subscribers yet built.

**Source.** ADR-0028, Decision (Subscription model).

---

## RD-0020 — Deferred, Queued Re-Entrant Publishing

**Considered during:** WP 4.4 (ADR-0028, Event Bus Dispatch, Subscription,
and Failure Model).

**Rejected because:** queueing a `PublishAsync` call made from within a
handler's own `HandleAsync`, processing it only after the current dispatch
completes, is a well-established pattern in other event-aggregator designs
— but it requires real, new infrastructure (an internal queue, a
"dispatch in progress" flag, and a draining loop) to solve a problem the
chosen design does not actually have: because each `PublishAsync` call
snapshots its own subscriber list independently, a nested call is already
safe and well-defined as an ordinary nested method call, with no risk of
mutating the outer dispatch's own iteration state. Building the queue
machinery anyway would be speculative complexity for a correctness
property the simpler design already provides.

**Reversibility.** Moderate cost to introduce later — would change nested
publishes from "resolved immediately, in-line" to "resolved after the
current dispatch completes," a real behavioural difference any existing
consumer would need to account for, not a purely additive change.

**Revisit trigger.** If a genuine need arises for breadth-first,
wave-by-wave event ordering across nested publishes (rather than
depth-first, immediate resolution) — not speculatively, since no current
consumer publishes re-entrantly at all yet.

**Source.** ADR-0028, Decision (Dispatch: sequential, awaited,
snapshot-based).

---

## RD-0021 — Polymorphic Event Dispatch

**Considered during:** WP 4.4 (ADR-0028, Event Bus Dispatch, Subscription,
and Failure Model).

**Rejected because:** letting a subscriber of a base event type or shared
interface also receive publications of a derived event type is a real,
plausible future capability — but no current event has, or needs, a type
hierarchy, and deciding a dispatch rule (does a subscriber of the base
type receive the derived publication before or after the base type's own
subscribers; does it receive it at all by default or only by explicit
opt-in) for a hierarchy that does not exist would be guessing, not
designing.

**Reversibility.** Cheap — exact-type dispatch can be extended to also
walk a type's own interface/base-class chain later, purely additively,
without changing any existing subscriber's own behaviour (an exact-type
subscription would continue to work exactly as it does today).

**Revisit trigger.** Once a real event type hierarchy is actually
proposed by a future work package — not before.

**Source.** ADR-0028, Decision (Dispatch is by exact event type only).

---

## RD-0022 — A Per-Subscriber Critical Opt-In, Mirroring `ICriticalBackgroundService`

**Considered during:** WP 4.4 (ADR-0028, Event Bus Dispatch, Subscription,
and Failure Model).

**Rejected because:** `ICriticalBackgroundService` (ADR-0021) is a
meaningful opt-in specifically because a background service is a live,
independently-running component capable of making its own self-assessment
about how load-bearing it is. An event subscriber is invoked synchronously,
by something that already exists and is already running, reacting to
something that already happened — a different enough shape that the same
opt-in pattern does not obviously transfer, echoing exactly the same
reasoning RD-0011 already applied to reject an analogous opt-in for
plugins. No current or anticipated subscriber has a demonstrated need to
be load-bearing enough that its own failure should abort the entire
platform.

**Reversibility.** Cheap — purely additive to `IEventHandler<T>` or a
future marker interface, if a real, demonstrated need for it ever
emerges; no existing subscriber's behaviour would need to change.

**Revisit trigger.** If a genuine, demonstrated need arises for some
subscribers to be load-bearing enough that their failure should abort
startup or fault the Host — not speculatively, and not merely because
`ICriticalBackgroundService` happens to look like a reusable template
(the same caution RD-0011 already named).

**Source.** ADR-0028, Decision (Failure model).

---

## RD-0023 — DI Container Multi-Registration Resolution for Auto-Discovering Hosted Services

**Considered during:** WP 4.5 (ADR-0029, Background Service Discovery,
Ownership, and Orchestration).

**Rejected because:** having `TempestServiceProvider` resolve "every
registered `IHostedService`" (`IEnumerable<IHostedService>`) would let the
Host skip its own reflection-based discovery step and simply ask the
container for whatever was registered — but this requires a genuine new
container capability (multi-registration resolution) `ADR-0005`'s
deliberately minimal container has never needed and does not have,
identically to RD-0019's own finding for the Event Bus. Reflection-based
discovery, reusing the already-proven Module/Plugin Discovery pattern,
achieves the same outcome with zero container changes.

**Reversibility.** Cheap to add later, purely additively, if a real,
demonstrated need for container-native multi-registration resolution ever
emerges from an unrelated capability — not deferred specifically for this
one.

**Revisit trigger.** If a future capability (unrelated to background
services specifically) demonstrates a real need for `IEnumerable<TService>`
resolution — not speculatively now, with reflection-based discovery
already solving this problem completely.

**Source.** ADR-0029, Context and Decision (Discovery).

---

## RD-0024 — A Dedicated `HostedServiceDescriptor` Type

**Considered during:** WP 4.5 (ADR-0029).

**Rejected because:** `ModuleDescriptor` and `PluginManifest` both exist
because their subjects carry real metadata (`Id`/`Name`/`Version`,
`MinimumPlatformVersion`, and so on) a later pipeline stage needs.
`IHostedService` carries none at all — there is no `Id` to catalogue, no
version to compare, nothing beyond the discovered `Type` itself. A
descriptor wrapping a bare `Type` would be ceremony mirroring an existing
pattern's *shape* without the information that pattern exists to carry.

**Reversibility.** Cheap — a descriptor type could be introduced later,
purely additively, if a hosted service ever gains real metadata worth
describing before construction.

**Revisit trigger.** If a future need arises for a hosted service to
declare metadata readable without constructing it (mirroring why
`ModuleMetadataAttribute` exists for modules) — not before, since no
current hosted service contract has any metadata to declare.

**Source.** ADR-0029, Decision (Discovery).

---

## RD-0025 — Extending `ReflectionFrameworkDiscoveryService` to Also Discover Hosted Services

**Considered during:** WP 4.5 (ADR-0029).

**Rejected because:** having Module Discovery's own, already-frozen
service scan for a second, unrelated candidate shape (`IHostedService`
alongside `IModule`) would blur its single, established responsibility —
"finds `IModule` implementations," unchanged since `WP 2.1` — and would
require modifying a component every other work package since has
deliberately left untouched. This mirrors exactly why Plugin Discovery
(`WP 4.2`) received its own dedicated service rather than extending Module
Discovery for a structurally similar reason.

**Reversibility.** N/A — rejected as the wrong shape of solution entirely,
not deferred pending more information.

**Revisit trigger.** Not expected to be revisited. A future, genuinely
shared discovery need (if one ever emerges) should be solved by a common,
reusable discovery *algorithm* both services call — mirroring the pattern
`docs/academy/04 Design Patterns/04-reflection-based-discovery.md` already
names as reusable — not by one service scanning for two unrelated
candidate interfaces.

**Source.** ADR-0029, Decision (Discovery).

---

## RD-0026 — Active Host-Level Monitoring of a Hosted Service's Own Background Work

**Considered during:** WP 4.5 (ADR-0029).

**Rejected because:** once `StartAsync` returns, a hosted service's own
ongoing work (a timer, an internal loop) runs independently, and nothing
in `IHostedService`'s own contract (`WP 4.0`, not revisited here) exposes a
`Task` handle, a health check, or any other surface the Host could
actively monitor. Building a monitoring capability now would mean
inventing a new contract member or a parallel tracking mechanism for a
need no current or anticipated hosted service has demonstrated — exactly
the kind of speculative design this release's own governing discipline
(established `WP 4.0` onward) exists to prevent.

**Reversibility.** Moderate — adding a monitoring surface later would
likely require a new, additive member on `IHostedService` (or a separate,
optional interface a service could implement), which every existing
hosted service implementation would need to consider, though not
necessarily adopt.

**Revisit trigger.** If a genuine, demonstrated need arises for the Host
(or a diagnostics capability, `WP 4.8`) to observe a hosted service's own
later failure — the interim, already-available answer is for the service
itself to publish an event via `IEventBus` from within its own defensive
exception handling, reusing existing infrastructure rather than adding a
new one.

**Source.** ADR-0029, Decision (Interaction with existing services) and
Future Considerations.

---

## RD-0027 — A New, Dedicated Host Lifecycle Phase for Hosted Service Discovery/Registration

**Considered during:** WP 4.5 (ADR-0029, ADR-0030).

**Rejected because:** discovering hosted service types and registering
them into the `ServiceCollection` is cheap and side-effect-free relative
to the container — exactly the same character as the existing
`AddDiscoveredModules` call and the Event Bus's own `WP 4.4D` registration,
both of which already fold into the existing Platform Services Registered
phase (Phase 6) without redefining its meaning. A dedicated new phase for
this step alone would proliferate the phase table for a step that changes
nothing about when the DI container itself is built, unlike the two new
phases this design *does* introduce (Hosted Services Started/Stopped),
which have genuine new side effects and failure semantics.

**Reversibility.** Cheap to introduce later if the registration step ever
grows real complexity of its own worth naming as a separate phase — not
expected, since registration here is, and is likely to remain, a single
loop over already-discovered types.

**Revisit trigger.** If discovering or registering hosted services ever
grows a genuine, separate failure mode or ordering constraint relative to
module registration — not speculatively now, since none exists.

**Source.** ADR-0029, Decision (Registration); ADR-0030, Alternatives
Considered.

---

## RD-0028 — Concurrent (Parallel) Start of Independent Hosted Services

**Considered during:** WP 4.5 (ADR-0029).

**Rejected because:** background services are, by nature, independent of
each other once running — but starting them (the act of calling
`StartAsync` itself, which is expected to be a bounded, quick operation)
does not need to happen concurrently to achieve that independence: each
service's own ongoing work becomes concurrent the moment its own
`StartAsync` returns, regardless of whether the *calls* to `StartAsync`
were made one at a time or all at once. Sequential, deterministic starting
— mirroring `ModuleLifecycleManager.RunBatchAsync`'s own established
shape — is easier to reason about, test, and diagnose than concurrent
starting, for a benefit (faster aggregate startup) no current or
anticipated hosted service count actually demonstrates a need for.

**Reversibility.** Moderate cost to introduce later — would change
`StartAllAsync`'s own observable behaviour (a failing service's log
message and any subsequent service's own start could now interleave
unpredictably), a real behavioural difference any existing test or
consumer relying on today's deterministic ordering would need to account
for.

**Revisit trigger.** If a genuine, demonstrated need arises for faster
aggregate startup with a large number of independent hosted services —
not speculatively now, with zero hosted services yet built.

**Source.** ADR-0029, Decision (Ordering and concurrency).

---

## RD-0029 — Automatic Restart/Backoff for Isolated Hosted Service Failures

**Considered during:** WP 4.5 (ADR-0029), revisiting the question
ADR-0021 explicitly left open.

**Rejected because:** ADR-0021 already decided only that an isolated
hosted service's failure does not stop the Host — it explicitly did not
decide whether a failed service should be restarted, retried with
backoff, or left failed permanently. Designing a restart/backoff policy
now — how many attempts, what backoff curve, whether a repeatedly-failing
service eventually escalates to critical — would be guessing at
operational requirements with no real hosted service yet built to test
any policy against.

**Reversibility.** Cheap — purely additive to `HostedServiceManager`'s own
internal behaviour if a real need for it emerges; no existing consumer's
behaviour would need to change, since "no restart" is itself a valid,
observable default a future opt-in policy would only extend, not replace.

**Revisit trigger.** Once a real, demonstrated operational need for
automatic recovery exists — not speculatively now, echoing ADR-0021's own
Future Considerations, which named this exact question and left it
explicitly open rather than silently implied.

**Source.** ADR-0021, Future Considerations; ADR-0029, Decision
(Failure model).
