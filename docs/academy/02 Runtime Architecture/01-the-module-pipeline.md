# The Module Pipeline: Discovery → Registration → Lifecycle → Dependency Injection

## 1. Introduction

Four work packages, delivered sequentially, built one continuous pipeline:
Framework Discovery (WP 2.1) finds modules; the Runtime Module Manager (WP 2.2)
registers them; the Lifecycle Manager (WP 2.3) orchestrates their initialisation,
startup, shutdown, and disposal; the Service Provider (WP 2.4) constructs the
actual instances the lifecycle manager drives. This document steps back from any
single work package to describe the pipeline as a whole — how the stages
connect, what crosses each boundary, and why the boundaries sit exactly where
they do.

## 2. Purpose

To give a reader who already understands each individual stage (from the Work
Packages section) a single, unified picture of how they compose into one
runtime, and to make explicit a design property that is easy to miss when
reading any one work package in isolation: every stage depends only on the
*interface* of the stage before it, never on its concrete implementation.

## 3. Background

The pipeline's shape — Discovery → Registration → Lifecycle → Dependency
Injection, with Health and Diagnostics anticipated as future stages — was
articulated, in outline, before any of the four work packages existed, and was
restated, verbatim, in each subsequent work package's own brief. This is unusual:
most systems arrive at their architecture through a sequence of locally
reasonable decisions that only retrospectively resemble a plan. TempestOS's
module pipeline was closer to the reverse — the plan came first, and each work
package was scoped specifically to implement one stage of it, with explicit
instructions in every brief not to redesign the stages that came before.

## 4. The Problem

A module system needs to answer four genuinely different questions, and a
design that answers all four with one component inevitably produces a class that
violates the Single Responsibility Principle: what modules exist; which of them
does this running instance actually know about; what order should they start and
stop in, and what happens on failure; and where do the actual, runnable
instances come from. TempestOS's core architectural bet was that these four
questions are independent enough to deserve four independent answers, connected
only by narrow, stable contracts.

## 5. The Design

```
ModuleDescriptor[]                    (WP 2.1 output)
        │
        ▼
IFrameworkDiscoveryService.DiscoverModules()
        │  produces ModuleDescriptor, in ascending Id order
        ▼
IRuntimeModuleManager.Register(descriptor)
        │  produces RuntimeModule, in registration order
        ▼
IModuleLifecycleManager(runtimeModuleManager, serviceProvider)
        │  drives IModuleLifecycle via ITempestServiceProvider
        ▼
ITempestServiceProvider.GetService(descriptor.ModuleType)
        │  constructs the actual, persistent module instance
        ▼
     (Application Runtime)
```

Each arrow in this diagram is a dependency on an *interface*, never a concrete
type: `ModuleLifecycleManager` depends on `IRuntimeModuleManager` and
`ITempestServiceProvider`, not on `RuntimeModuleManager` or
`TempestServiceProvider` specifically. Nothing downstream of discovery depends on
reflection; nothing downstream of registration depends on how the catalogue is
stored; nothing downstream of lifecycle depends on how modules are actually
constructed.

Two types cross every boundary and deserve calling out specifically:
`ModuleDescriptor` (discovery's output, consumed by registration, and again by
the service provider via `descriptor.ModuleType`) and `ModuleState` (extended
additively across WP 2.2 and WP 2.3, never redefined).

## 6. Alternatives Considered

**A single, unified "Module Manager" doing all four jobs.** The path of least
resistance, and the one most systems drift toward under time pressure — one
class that discovers, registers, orchestrates, and constructs, because it's
faster to write initially and there's only one file to open. Rejected from the
outset (never actually attempted in TempestOS) precisely because every one of
the four work packages' briefs stated the fixed responsibility list explicitly,
before the corresponding code existed, specifically to prevent this drift.

**Passing concrete types between stages instead of interfaces.** Would have
worked functionally — `ModuleLifecycleManager` could have taken a concrete
`RuntimeModuleManager` and a concrete `TempestServiceProvider` directly. Rejected
in favour of interface dependencies throughout, since concrete dependencies
would have made every stage's tests depend on every other stage's real
implementation, rather than on a substitutable contract — directly undermining
the testability each work package's own test suite depends on.

## 7. Why This Solution Was Chosen

The pipeline shape was chosen because each stage answers a question with a
genuinely different rate and reason for change — discovery changes if *how
modules are found* changes; registration changes if *how the catalogue is kept*
changes; lifecycle changes if *orchestration policy* changes; the service
provider changes if *construction mechanics* change — and coupling any two of
them together would mean a change motivated by one concern risking an
unintended, untested effect on an unrelated one.

## 8. Architectural Principles

- **Separation of Concerns** and **Single Responsibility** — the pipeline's
  entire reason for existing; see both Engineering Principle documents.
- **Dependency Inversion** (from SOLID) — every stage depends on an abstraction
  of the stage before it, never a concrete implementation.
- **Immutability** — the two types that cross every boundary
  (`ModuleDescriptor`, and `RuntimeModule` within registration/lifecycle) are
  both immutable, so no stage can corrupt data another stage is relying on.
- **Deterministic Systems** — each stage that produces an ordered output
  (discovery's alphabetical order; lifecycle's ascending/descending order)
  imposes that order explicitly, rather than inheriting whatever incidental
  order the platform happened to produce.

## 9. Benefits

- Four work packages were delivered sequentially, each depending on the
  previous stage's *interface* only, and not one of them required reopening and
  modifying a previous stage's concrete implementation to integrate — the
  clearest possible evidence the boundaries were drawn correctly. WP 2.4's
  entire production-code footprint on prior work was one call-site change inside
  `ModuleLifecycleManager`.
- Each stage is independently testable: WP 2.1's tests need no registration,
  lifecycle, or DI machinery; WP 2.2's tests need no discovery or lifecycle
  machinery; and so on.
- The pipeline is extensible at each stage independently — a different discovery
  strategy, a persistent registration store, a different orchestration policy,
  or a different construction mechanism could each, in principle, replace one
  stage without requiring changes to the others, as long as the interface
  contract is honoured.

## 10. Trade-offs

- Four separate classes, four separate namespaces of concern, and four separate
  sets of tests are objectively more to navigate than one unified manager would
  have been, for a reader encountering the system for the first time — the
  Runtime Architecture section of this Academy exists specifically to offset
  that cost by providing the unified view a single class would have given "for
  free," but which four independent classes cannot.
- Wiring the four stages together (as `ModuleLifecycleManagerTests.BuildLifecycleManager`
  and, eventually, a real composition root must do) is manual and repeated at
  every call site today — see WP 2.4's Future Evolution note on a
  composition-root helper.

## 11. Common Mistakes

The mistake this architecture is specifically designed to prevent — and has, so
far, successfully prevented across four work packages — is *scope creep across
stage boundaries*: a developer implementing lifecycle orchestration reaching
into registration's internals because it's the fastest way to get a specific
piece of information, rather than adding a method to registration's own
interface if that information is genuinely needed. Every one of WP 2.2, WP 2.3,
and WP 2.4's briefs included an explicit "do not redesign" clause aimed at
exactly this temptation, and none of the four work packages' implementations
violated it. The discipline required to maintain this is external (an explicit
instruction in each brief) as much as it is internal (the actual code
structure) — worth remembering if this pipeline is ever extended without an
equally explicit brief constraining the boundary.

## 12. Future Evolution

The pipeline diagram, referenced identically across WP 2.1 through WP 2.4's
briefs, explicitly anticipates two further stages: **Health** and
**Diagnostics**, following Dependency Injection. Whatever those stages turn out
to need, the same discipline that made the first four work packages compose
cleanly should apply: a fixed, narrow responsibility, a dependency on the
*interfaces* already established (`IModuleLifecycleManager`, most likely, for
Health — health probably needs to know what state modules are in, not how they
got there), and an explicit non-goal list preventing the new stage from reaching
into and modifying the stages before it.

## 13. Key Takeaways

1. A pipeline architecture's value is proven not by its diagram but by whether
   later stages can be added without modifying earlier ones — TempestOS's module
   pipeline has now been tested against this four separate times, successfully.
2. Interfaces at every stage boundary, not concrete types, are what make that
   proof possible — a concrete dependency would have made "add a stage without
   touching the others" false the first time an internal detail of an earlier
   stage needed to differ from what a later stage assumed.
3. An explicit, restated-every-time non-goal list ("do not redesign discovery,"
   "do not redesign registration," "do not redesign lifecycle") is not
   bureaucratic caution — it is the actual mechanism that kept four sequential
   work packages from drifting back toward one unified, tangled module manager,
   which is the default outcome absent that discipline.
