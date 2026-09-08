# WP 4.1 — Module SDK

## 1. Introduction

WP 4.1 is the first developer-facing work package of v0.4.0: two small
abstract classes, `ModuleBase` and `ModuleLifecycleBase`, that reduce a
module author's repeated boilerplate without changing a single byte of
runtime behaviour. It follows directly from WP 4.0's platform contracts and
precedes every later work package that will build modules against it (most
immediately WP 4.3, the Sample Module).

## 2. Purpose

To make writing a TempestOS module simpler, more readable, and more
consistent — and, in doing so, to identify precisely how much of that
simplification is actually justified by real, observed repetition, rather
than by what an SDK "could" plausibly offer.

## 3. Background

WP 4.0 defined `IModule` (re-affirmed) and five new contracts, deliberately
leaving anything without settled design (`INavigationProvider`,
`IDiagnosticsProvider`, a command handler/result shape) undefined. WP 4.1
continues that same discipline in a different direction: rather than adding
new contracts, it asks what of the *existing* module-authoring experience
is genuinely repetitive, and addresses only that.

## 4. The Problem

1. **What does a module author actually repeat today?** Reviewing every
   existing module fixture in the test suite (`SampleModuleA/B/C`,
   `RecordingLifecycleModuleAlpha/Beta/Gamma`, the Runtime Host's own
   `HealthyHostTestModuleAlpha/Beta`, and others) shows the same two
   patterns every time: three near-identical property getters for
   `Id`/`Name`/`Version`, and — for anything implementing
   `IModuleLifecycle` — trivial `=> Task.CompletedTask;` overrides for
   whichever lifecycle phases a given module doesn't actually use.
2. **Does any existing interface already satisfy what an SDK would
   provide?** No — `IModule`/`IModuleLifecycle` are the contracts; nothing
   already implements them generically.
3. **Does introducing convenience base classes require touching Discovery,
   Registration, or Lifecycle?** No, and it must not — the brief is
   explicit that all three must continue to function completely
   unchanged.
4. **Is there a real, current limitation module authors should know about,
   independent of anything the SDK can fix?** Yes — see Section 5.

## 5. The Design

Two abstract classes, both in `Tempest.Core.Modules` (the same namespace as
`IModule`/`IModuleLifecycle` themselves — no new project, no new
namespace):

- **`ModuleBase : IModule`** — a constructor taking `(id, name, version)`,
  validated exactly like `RuntimeModuleManager.Register`'s existing
  null/empty/whitespace checks, exposing them as the three required
  properties. For modules with no lifecycle behaviour.
- **`ModuleLifecycleBase : ModuleBase, IModuleLifecycle`** — adds four
  `virtual` lifecycle methods, each defaulting to `Task.CompletedTask`. A
  module overrides only the phase(s) it needs.

**A discovered, not invented, constraint.** Reviewing Discovery's and
`TempestServiceProvider`'s construction paths together surfaced a real,
pre-existing limitation: Discovery's metadata probe calls
`Activator.CreateInstance(type)` — requiring a public *parameterless*
constructor — while `TempestServiceProvider.Construct` requires *exactly
one* public constructor, whichever shape it has. Both requirements hold
simultaneously only when a module's sole constructor takes zero arguments.
In practice, this means **a normally-discovered module cannot currently
receive constructor-injected platform-service dependencies** — a
significant, previously-undocumented fact about the module pipeline as it
exists today. This work package does not fix it (fixing it would mean
changing Discovery, explicitly out of scope — "existing discovery must
continue to function unchanged"); it documents it plainly, in the Platform
Service Map's new Module SDK entry and in *Building a Module*, so a future
module author does not discover it by trial and error.

## 6. Alternatives Considered

**A dedicated `Tempest.SDK` project.** Considered, since WP 4.0's own
planning left this open. Rejected for now — two small classes do not
justify a new project's build/packaging overhead; `Tempest.Core.Modules`
already holds `IModule`/`IModuleLifecycle`, and keeping the convenience
implementations alongside their contracts matches how every other
capability in the platform is organised (`Tempest.Core.Logging` holds both
`ILogger` and `Logger`, for example).

**Registration helpers / a module builder pattern.** Both named as
possible SDK facilities in the brief. Rejected outright — registration is
already fully automatic (the Host loops over discovered descriptors and
calls `Register` itself); there is no per-module registration boilerplate
for a helper to remove, and no evidence a builder pattern would simplify
anything a plain constructor call does not already handle.

**A `ToString()` override, or other metadata convenience, on `ModuleBase`.**
Considered — several existing log call sites already format modules as
`"{Name} v{Version} ({Id})"`. Rejected: adding it would have no current
consumer (changing those existing call sites to use it would be exactly
the "unrelated refactoring" this work package was told to avoid), and "every
public API must have a real consumer today" ruled it out cleanly.

**Attempting to lift the parameterless-constructor constraint** (for
example, via a service-locator-style pattern letting a module resolve its
own dependencies post-construction). Rejected immediately — this is
precisely the kind of "hidden reflection" and "runtime surprise" the brief
explicitly forbids, and fixing the underlying constraint would mean
changing Discovery, out of scope for this work package regardless.

## 7. Why This Solution Was Chosen

Both classes exist because the repetition they remove is directly
observable in the existing test suite, not hypothesised. Every rejected
alternative was rejected for the same reason: no demonstrated repetition,
no real consumer today, or a fix that belonged to a different work
package's scope (or no work package's scope at all, in the case of hidden
reflection).

## 8. Architectural Principles

- **Reuse Before Invention** — `ModuleLifecycleBase`'s identity/behaviour
  split reuses `IModule`/`IModuleLifecycle`'s own existing split, exactly
  as WP 4.0's `ICommand` design reused it a first time.
- **Avoid Speculative Design** — every considered-and-rejected addition
  above was rejected on the same test: does this have a real consumer
  today, or does it exist because it might be convenient someday.
- **Zero Unnecessary Abstractions** — two classes, no new project, no new
  namespace, no attributes, no source generation.
- **Fail Fast** — `ModuleBase`'s constructor validates its three arguments
  immediately, at module-construction time, using the same
  null/empty/whitespace check already established in
  `RuntimeModuleManager.Register`.

## 9. Benefits

- A module with no lifecycle collapses from three property getters to one
  base-constructor call.
- A module using only one or two lifecycle phases no longer writes
  trivial no-op overrides for the phases it doesn't use.
- A real, previously-undocumented constraint on module authoring
  (parameterless-constructor-only, in practice) is now written down where
  a module author will actually find it, rather than discovered by a
  failed `Activator.CreateInstance` call at runtime.

## 10. Trade-offs

- The parameterless-constructor constraint remains exactly as limiting as
  it was before this work package — the SDK works within it, not around
  it. A future work package could revisit Discovery's own construction
  path if this becomes a recurring pain point; this one does not.
- Two new public base classes are now part of the platform's permanent
  public surface, each requiring the same backwards-compatibility
  discipline as `IModule`/`IModuleLifecycle` themselves going forward.

## 11. Common Mistakes

The mistake most worth naming: treating the brief's example list ("base
module abstraction, module metadata helpers, module builder patterns,
registration helpers, lifecycle convenience APIs, common validation
helpers") as a checklist to fulfil, rather than as illustrative
possibilities each requiring its own justification. Four of those six
named possibilities were not built, deliberately, because none had a real,
demonstrated consumer — building all six "because they were listed" would
have been exactly the kind of speculative abstraction this release's own
governing philosophy (established in WP 4.0) exists to prevent.

## 12. Future Evolution

- **WP 4.3 (Sample Module)** is this SDK's first real, non-test consumer —
  any gap it finds should be fed back into `ModuleBase`/`ModuleLifecycleBase`
  or into *Building a Module*, not silently worked around.
- **The parameterless-constructor constraint** should be revisited if a
  future work package finds a genuine need for constructor-injected
  module dependencies — that would be a Discovery-level architectural
  decision, not an SDK one, and this retrospective is the citable record
  of why it wasn't addressed here.
- **A dedicated `Tempest.SDK` project** remains an option for a future
  release if the SDK's surface grows enough to justify its own packaging —
  not needed for two classes.

## 13. Key Takeaways

1. An SDK's value is in what it removes, not in how much surface it adds —
   two classes, addressing two directly observed repetitions, is a
   complete answer when that is all the evidence supports.
2. Reviewing how existing services actually construct instances (Discovery's
   `Activator.CreateInstance` vs. `TempestServiceProvider`'s DI resolution)
   surfaced a real architectural constraint no prior document had stated
   explicitly — proof that a "just write the SDK" work package can still
   produce a genuine architectural finding if the design review is done
   properly first.
3. "Every public API must have a real consumer today" is a concrete,
   applicable filter, not a slogan — it directly eliminated four of six
   brief-suggested facilities in this work package alone.

---

## Architectural Debt Assessment

**No new debt introduced.** The parameterless-constructor-only constraint
is not new debt created by this work package — it is a pre-existing
property of Discovery and `TempestServiceProvider`'s construction paths,
newly documented rather than newly introduced. Every other debt item on
record from the Runtime Foundation and WP 4.0 remains unchanged.

## Observations

- **Public APIs introduced**: `ModuleBase` (`Tempest.Core.Modules`),
  `ModuleLifecycleBase` (`Tempest.Core.Modules`).
- **Files changed**: 2 new production files; 4 new test files (21 new
  tests); Platform Service Map (Discovery, Lifecycle, and a new Module SDK
  entry); a new Academy document, *Building a Module*; this retrospective.
  Zero existing production files modified.
- **Tests added**: 21 (construction validation, contract satisfaction,
  default no-op behaviour, selective and full lifecycle overriding,
  discovery compatibility, full-pipeline integration through
  `ModuleLifecycleManager`).
- **Test results**: 198 of 198 passing (177 pre-existing + 21 new), 0
  failures, verified stable across four consecutive full-suite runs.
- **Build results**: 0 warnings, 0 errors.
- **ADRs added or modified**: none. This work package's decisions
  (namespace placement, which facilities to build) did not meet
  Engineering Governance §5's criteria for a new ADR — each was a direct,
  low-risk application of an already-established convention, not a novel
  or reversible-at-cost decision.
- **Risks discovered**: the parameterless-constructor constraint (Section
  5) — documented, not a defect introduced here.
- **Readiness assessment**: WP 4.1 is complete and ready to merge into
  `feature/v0.4.0-platform-services`'s own history. `WP 4.2` (Plugin
  Manifest) and `WP 4.3` (Sample Module) may now build against a settled,
  documented Module SDK.
