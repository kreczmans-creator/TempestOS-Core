# WP 4.4A — ADR: Dependency Injection for Discovered Modules

## 1. Introduction

WP 4.4A, like WP 4.2B/4.2C before it, produced no production code. Its
entire job was one Architecture Decision Record — ADR-0027, *A Declarative
`ModuleMetadataAttribute` Decouples Discovery From Construction* —
resolving the architectural limitation `WP 4.3`'s own design and
implementation phases identified and deliberately did not solve: a
discovered module cannot receive any constructor-injected, DI-public
platform service, and `WP 4.4` needs exactly that for its own
already-approved plan to extend the sample module with event publishing.

## 2. Purpose

To trace the module construction pipeline precisely enough to state, with
certainty, exactly where and why a parameterless constructor is required
today; to determine whether that requirement can be lifted, for the
modules that need it, without weakening anything the platform already
guarantees; and to decide this in writing, as its own dedicated work
package, before `WP 4.4` begins — exactly mirroring how `WP 4.2A`–`4.2C`
preceded `WP 4.2`'s own implementation.

## 3. Background

`WP 4.3`'s design phase found the problem and traced it to its exact
mechanical cause: `IFrameworkDiscoveryService`'s metadata probe calls
`Activator.CreateInstance(type)` unconditionally, requiring a public
parameterless constructor, for every candidate module — and a module
whose sole constructor takes parameters makes that call throw, uncaught,
before the module is ever registered. `WP 4.4`'s own already-approved
Deliverable — extending the sample module to publish an event through the
DI-public `IEventBus` (ADR-0020) — needs exactly the constructor injection
this forecloses. `WP 4.3`'s own brief was explicit that solving this
belonged to a later work package; this is that work package.

## 4. The Problem

1. **Where, exactly, is `Activator.CreateInstance` required, and why?**
   Traced precisely — see Section 5 and ADR-0027's own Context.
2. **What depends on parameterless construction, and what does not?**
   Investigated directly against every type in the pipeline, not assumed.
3. **Should Discovery instantiate modules at all — and can metadata be
   obtained without constructing an instance?** The two questions this
   work package's brief named as central.
4. **Does the real, lifecycle-driving construction (via
   `TempestServiceProvider`) already support constructor dependencies, or
   does it also need to change?** Verified directly, not assumed from
   `WP 4.1`'s own original documentation of the constraint.
5. **Can a solution be genuinely additive** — introducing zero risk to
   `ClockModule` or any other existing module — rather than requiring a
   migration?

## 5. The Design

See `docs/adr/ADR-0027-declarative-module-metadata-attribute.md` and
`docs/architecture/Module Dependency Injection Architecture.md` in full.
In summary: `TempestServiceProvider.Construct` already resolves
constructor dependencies recursively for any registered service,
including a discovered module's own concrete type — verified directly,
not merely asserted — so the *real* construction was never the problem.
The entire limitation is confined to Discovery's own, separate,
throwaway metadata probe. A new, optional, class-level attribute,
`ModuleMetadataAttribute`, lets a module declare `Id`/`Name`/`Version`
without being instantiated at all; Discovery reads it when present and
falls back to exactly today's behaviour — instantiate, read instance
properties, discard — when absent. Every existing module keeps the
fallback path, unchanged, forever.

## 6. Alternatives Considered

Recorded in full, with reasoning, in ADR-0027's own "Alternatives
Considered" section, and permanently indexed as RD-0016 (deferring
metadata reading until after the DI container is built — rejected as
directly inverting ADR-0011's already-decided ordering), RD-0017 (a
second, always-parameterless descriptor type per module — rejected as
reintroducing the per-module boilerplate `WP 4.1`'s SDK exists to remove),
and RD-0018 (static abstract interface members on `IModule` — rejected as
a breaking change to every existing module, for a problem an additive
attribute already solves). A service-locator workaround was not
re-evaluated from scratch — RD-0007, from `WP 4.1`, already rejected it
for this exact class of problem and named precisely this ADR's own
arrival as the correct resolution path; that entry is annotated, not
superseded, since its own specific rejection (service-locator) remains
fully valid.

## 7. Why This Solution Was Chosen

Every alternative considered either violated an already-decided ordering
(RD-0016), reintroduced boilerplate the platform has twice already worked
to remove (RD-0017, echoing `WP 4.1`'s own reasoning), or required a
breaking change disproportionate to the problem (RD-0018). The chosen
design is the only one of the four that is purely additive — every
existing module, including `ClockModule`, keeps working without any
change, while a new module gains a real, minimal, opt-in path to
constructor injection. This is the same test `WP 4.1`'s own SDK design
applied to every candidate facility it considered: does this have a real,
demonstrated need, and is the cost proportionate to it.

## 8. Architectural Principles

- **Reuse Before Invention** — the *real* construction path
  (`TempestServiceProvider.Construct`) is reused entirely unchanged; this
  design adds a metadata-reading branch, not a second construction
  mechanism.
- **Minimal Host Complexity** — zero change to `TempestHost.cs`, `Host
  Lifecycle.md`'s phase table, or `Runtime State Machine.md`.
- **One Responsibility Per Service** — Discovery still only discovers;
  `RuntimeModuleManager` still only registers; `ModuleLifecycleManager`
  still owns activation and lifecycle exclusively. No responsibility moves
  between components.
- **Avoid Speculative Design** — the attribute carries exactly the three
  fields `IModule` already requires, nothing more; no SDK convenience is
  built ahead of a second real consumer (see Future Considerations).
- **Constructor Injection Through Normal DI Patterns** — explicitly
  preserved; the design's entire purpose is to make ordinary constructor
  injection reachable for a module, not to introduce an alternative to it.

## 9. Benefits

- **`WP 4.4`'s own next deliverable is now unblocked** — extending the
  sample module (or its future companion) to publish through `IEventBus`
  requires only the attribute and a normal constructor, once this design
  is implemented.
- **Every existing module is provably unaffected** — the fallback path is
  `ReflectionFrameworkDiscoveryService`'s exact, unmodified existing
  behaviour; nothing about this design touches it.
- **A latent bad failure mode is corrected as an incidental consequence**:
  a construction problem for an attribute-based module becomes an
  isolated module failure (ADR-0013) rather than a Host-fatal crash —
  found while tracing the pipeline, not the design's original purpose.
- **`RD-0007`'s own named revisit path is fulfilled**, not reopened —
  direct continuity between what `WP 4.1` predicted and what this work
  package delivered.

## 10. Trade-offs

- This is documentation only — nothing here is enforced by a compiler,
  test, or running code yet, exactly as every architecture-only work
  package in this release has noted about itself.
- A module author opting into `[ModuleMetadata]` must keep the attribute
  and the module's own instance properties in agreement by hand — a named,
  accepted risk, structurally identical to `PluginManifest`'s own
  already-accepted `Version`-field duplication risk (`Plugin Manifest
  Architecture.md`).
- The Module SDK does not yet offer a convenience for the attribute-based
  path — deliberately deferred until a second real consumer exists beyond
  `WP 4.4`'s own anticipated one.

## 11. Common Mistakes

The mistake most worth naming here is one avoided: treating "modules need
DI access" as a reason to weaken or generalise `IFrameworkDiscoveryService`'s
own contract broadly (for example, giving Discovery a service provider of
its own, or resolving metadata through DI directly). Either would
reintroduce exactly the circularity ADR-0008 already ruled out (Discovery
resolving through a container that isn't populated until Discovery's own
output exists) or the ordering violation RD-0016 names. The chosen design
instead narrows the actual gap — Discovery's own throwaway instantiation —
to precisely the one thing that needed to change.

## 12. Future Evolution

- **`WP 4.4`'s own first implementation step** should be exactly this
  design: `ModuleMetadataAttribute` and
  `ReflectionFrameworkDiscoveryService`'s new, additive branch, proven
  against a small, dedicated test module — not `ClockModule`, which
  remains the unmodified, legacy-path living reference it already is —
  before extending the sample module (or its companion) with event
  publishing.
- **A Module SDK convenience** for the attribute-based path may be worth
  adding once a second real consumer exists beyond `WP 4.4`'s own — not
  before.
- **Attribute/instance-property agreement** may warrant a narrow,
  additive validation later, if divergence ever proves a real, recurring
  problem — not speculatively now.

## 13. Key Takeaways

1. Tracing a pipeline precisely, rather than trusting a prior work
   package's own summary of it, found that half of the presumed problem
   (the *real*, lifecycle-driving construction) was never actually broken
   — only the throwaway metadata probe was. A design that fixes exactly
   the broken half is smaller and safer than one that assumes the whole
   pipeline needs to change.
2. An additive, opt-in design that leaves every existing consumer
   completely unaffected is strictly preferable to a more elegant but
   breaking one (RD-0018), when the problem itself does not demand
   breaking anything.
3. A Rejected Design entry that names its own revisit path (RD-0007) is
   worth honouring precisely when the moment arrives — annotating it, not
   discarding or rewriting it, keeps the historical record showing that
   the platform did exactly what it said it would.

---

## Architectural Debt Assessment

**No new debt introduced.** This work package produced one ADR, one
architecture document, and three Rejected Designs entries; no code exists
for it to affect. Every debt item on record from the Runtime Foundation,
WP 4.0–4.3, and WP 4.2D remains exactly as previously described. The
attribute/instance-property agreement risk is newly named, not newly
created — it is a property of the design being proposed, disclosed at the
moment of proposal rather than discovered later.

## Observations

- **Files changed**: 1 new ADR (`ADR-0027-declarative-module-metadata-attribute.md`);
  1 new architecture document (`Module Dependency Injection Architecture.md`);
  3 new Rejected Designs entries (RD-0016, RD-0017, RD-0018); RD-0007
  annotated (not superseded); `Platform Service Map.md`, `Building a
  Module.md`, and `Sample Module Architecture.md` updated with forward
  cross-references; this retrospective. Zero production code files
  touched — none exist for this work package to touch.
- **ADRs required**: 1 (ADR-0027) — written in full, as this work
  package's entire deliverable.
- **Risks discovered**: the attribute/instance-property agreement risk
  (named, not resolved — deliberately, per this release's own
  "no speculative validation ahead of a real need" discipline).
- **Readiness assessment**: the design is complete and sound. No
  architectural blocker remains before `WP 4.4` begins. `WP 4.4`'s own
  first implementation step should be this design's own realisation
  (`ModuleMetadataAttribute` plus Discovery's new branch), proven against
  a dedicated test module, before extending the sample module with event
  publishing — **now complete, see the WP 4.4B implementation
  retrospective.**
