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
