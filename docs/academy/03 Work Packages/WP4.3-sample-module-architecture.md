# WP 4.3 — Sample Module Architecture

## 1. Introduction

WP 4.3, like WP 2.7A and WP 4.2 before it, produced no production code. Its
job was to design the "living reference module" every later v0.4.0 work
package (`WP 4.4` through `WP 4.7`) is committed to extending, before any
of it is written — and, in the course of that design, this work package
found a real, near-term architectural collision that would otherwise have
surfaced mid-implementation of `WP 4.4` instead.

## 2. Purpose

To decide, in writing, before implementation begins: what the sample
module actually models; where it lives in the repository; how it is
packaged and discovered; what it can and cannot do given the module
pipeline's own existing constraints; and what, if anything, must be
decided by ADR before `WP 4.4` can complete its own already-approved
Deliverables against this same module.

## 3. Background

By the time `WP 4.3` began, the Platform Services milestone (`WP 4.0`–`4.2`)
had been formally reviewed and signed off (`WP 4.2D`) with no outstanding
architectural blocker. `WP 4.3`'s own brief was explicit that this
invocation is a design phase only — investigate the repository, determine
architecture, identify required ADRs, and stop short of implementation,
mirroring exactly the discipline `WP 4.2`'s own architecture-only phase
established immediately before it.

## 4. The Problem

1. **Does anything in the repository already satisfy part of this need?**
   Investigated directly — see Section 5.
2. **What should the sample module actually be**, realistic and non-trivial
   without being a "hello world" stub, small enough for this work package's
   own **S** estimate, and with an obvious, non-contrived extension story
   for every later work package already committed to extending it?
3. **Where does it live**, given it must be "discoverable exactly as a
   third-party module would be" — a claim a module living inside
   `Tempest.Core` itself could not honestly make?
4. **Can it consume any platform service** (configuration, logging,
   platform version), and if not, why not, and does that matter beyond this
   one module?
5. **Does `WP 4.3` need a companion module now**, or can that wait?

## 5. The Design

See `docs/architecture/Sample Module Architecture.md` in full. In summary:
a new project, `src/Samples/Tempest.Samples`, houses `ClockModule` — a
small, self-contained, SDK-built module (`ModuleLifecycleBase`) that
tracks its own initialise/start/stop timestamps and running state in
memory, with real logic in each lifecycle method rather than empty
overrides. It consumes no platform service, by necessity rather than
choice (see Section 6's most significant finding), and no companion
module is built alongside it.

The repository investigation directly preceding this design surfaced that
`Tempest.App` does not use `TempestHost` at all — still running its
original bootstrap path (`BootstrapService`/`HostingService`/
`ProjectService`) — a pre-existing, already-anticipated condition (the
Platform Service Map's own Host entry already lists `Tempest.App` as an
"anticipated," not actual, consumer), but one that directly shapes how
this design's own claims can be proven: via `TempestHostBuilder` in tests,
not by observing the real executable, exactly as every other Host-pipeline
behaviour has been proven throughout this release.

## 6. Alternatives Considered

Recorded in full, with reasoning, in `Sample Module Architecture.md`'s own
"Alternatives Considered" section: coupling the sample module to the
existing bootstrap-era Project code (rejected — out of this work package's
named dependencies, and would drag legacy file-I/O into every future work
package's own extension of the module); packaging it through `WP 4.2`'s
Plugin Manifest system now that it is ready (rejected for now, not
permanently — recorded as RD-0015, since `Tempest.App` cannot demonstrate
it running regardless of packaging choice, and the remaining benefit is
already substantially covered by `WP 4.2`'s own test suite); and building
a companion module now (rejected — the one named scenario requiring one,
`WP 4.4`'s publish/subscribe proof, has nothing to subscribe to yet, since
`IEventBus` does not exist).

**The most significant finding of this design phase is not itself an
"alternative considered" in the rejected-designs sense** — it is a real
constraint this design worked within rather than around: a normally-
discovered module's sole public constructor must take zero arguments, full
stop, because `IFrameworkDiscoveryService`'s metadata probe calls
`Activator.CreateInstance(type)` unconditionally and uncaught for every
candidate, and a non-parameterless constructor would make that call throw
before the module is ever registered — a Host-fatal crash, not an isolated
module failure. This was already documented by `WP 4.1` as a known
limitation; this design phase is the first to trace it to its exact
mechanical cause and connect it concretely to `WP 4.4`'s own already-
approved plan to extend the sample module with `IEventBus` publishing —
which needs precisely the constructor injection this constraint forecloses.

## 7. Why This Solution Was Chosen

Every decision in this design traces back to one of two things: an
already-established convention applied directly (the SDK's own documented
shape, ADR-0013's module-failure model, ADR-0023's layering), or a
constraint the existing codebase already enforces, worked within rather
than designed around (the parameterless-constructor rule, `RD-0007`'s own
prior rejection of a service-locator workaround for exactly this class of
problem). Nothing in this design invents a new mechanism; the one open
question it surfaces (how a discovered module could someday obtain a
DI-public service) is deliberately left open, named for `WP 4.4` to resolve
via its own ADR, not guessed at here.

## 8. Architectural Principles

- **One Responsibility Per Service** — `ClockModule` does exactly one
  thing (track its own lifecycle timestamps); it does not attempt to model
  the future Project Engine or anything beyond what proves the pipeline.
- **Downward Dependency Direction** (ADR-0023) — `Tempest.Samples`
  references `Tempest.Core` only; nothing in `Tempest.Core` knows the
  sample project exists.
- **Avoid Speculative Design** — no companion module, no plugin packaging,
  no constructor dependency: each was seriously considered and deferred
  specifically because building any of them now would anticipate work
  (`IEventBus`, plugin build tooling, a DI-access mechanism) that does not
  exist yet.
- **Reuse Before Invention** — the module is written exactly as *Building a
  Module* already documents; this design invents no new pattern for
  writing a module, only a new, real instance of the existing one.

## 9. Benefits

- `WP 4.4` through `WP 4.7` each now have a real, settled module to extend,
  named and justified before any of them begins, rather than inventing
  their own throwaway fixture.
- A genuine, previously only-abstractly-documented architectural
  constraint (the parameterless-constructor rule) is now connected to a
  concrete, near-term consequence (`WP 4.4`'s own plan) — finding this now,
  during a design phase, is exactly what an architecture-first pass is
  for, mirroring how `WP 4.2`'s own design phase found the platform-
  version-at-runtime gap before it could block implementation silently.
- A real, concrete testing hazard (`WP 4.2`'s already-once-avoided full-
  `AppDomain`-scan pollution problem) is pre-empted in the design itself,
  before a future implementer has to rediscover it via a flaky or failing
  test.

## 10. Trade-offs

- This is documentation only — nothing here is enforced by a compiler,
  test, or running code yet, exactly as every architecture-only work
  package in this release has noted about itself.
- The sample module, as designed, is deliberately less capable than a real
  TempestOS module will eventually need to be (no configuration, no
  logging, no platform-service access at all) — an honest reflection of
  the pipeline's own current limits, not a design shortfall specific to
  this module.

## 11. Common Mistakes

The mistake most worth naming here is one avoided: treating "the sample
module should be realistic" as license to reach for the one genuinely
realistic domain concept already sitting in the repository (the bootstrap-
era Project code) without first checking whether doing so was actually in
scope. It was investigated, found not to be named in `WP 4.3`'s own
Dependencies, and correctly set aside — realism was found instead in
choosing a domain concept (a clock) that needs no legacy coupling to be
genuine.

## 12. Future Evolution

- **`WP 4.4`** should treat resolving the parameterless-constructor/DI-
  access tension identified here as its own first order of business,
  mirroring exactly how `WP 4.2A`–`4.2C` preceded `WP 4.2`'s own
  implementation — not something to discover mid-implementation.
- **A companion module** is `WP 4.4`'s own responsibility to add "if it
  does not already exist," per its own Deliverables — not built here.
- **Plugin-manifest packaging** (RD-0015) remains available, purely
  additively, once `Tempest.App` is wired to `TempestHost` or `WP 4.9`
  needs a real example plugin.
- **`Tempest.App`'s own migration to `TempestHost`** is named here as a
  real, observed gap but is not this or any other v0.4.0 work package's
  named scope — a candidate for a future, explicitly-scoped work package,
  not an incidental side effect of one that happens to notice it.

## 13. Key Takeaways

1. A design phase's value is proportional to what it finds before
   implementation has to discover it the hard way — this work package's
   most significant output is not the module design itself but the
   precise, mechanical explanation of why `WP 4.4`'s own next step needs
   an ADR first.
2. "Realistic" and "reuses existing domain code" are not the same
   requirement — the right realism test is whether a genuine third-party
   author could plausibly have written it, not whether it happens to touch
   code already in the repository.
3. Deferring an alternative (plugin packaging) and rejecting one
   (constructor-injected dependencies) are different outcomes requiring
   different records — one earns a Rejected Designs entry with a named
   revisit trigger; the other is a pre-existing constraint this design
   worked within, documented in place, not re-litigated as if it were new.

---

## Architectural Debt Assessment

**No new debt introduced.** This work package produced documentation only.
One genuine, pre-existing constraint (the parameterless-constructor rule)
was traced to its exact cause and connected to a concrete near-term
consequence — this is a finding, not new debt; the constraint already
existed and was already named by `WP 4.1`. `Tempest.App`'s continued
non-use of `TempestHost` is likewise pre-existing, already anticipated in
the Platform Service Map since `WP 2.7B`, not newly introduced or worsened
by this work package.

## Observations

- **Files changed**: 1 new architecture document (`Sample Module
  Architecture.md`); 1 new Rejected Designs entry (`RD-0015`); this
  retrospective. Zero production code files touched — none exist for this
  work package to touch.
- **ADRs required**: 0 for `WP 4.3` itself. 1 identified as required before
  `WP 4.4` can complete its own already-approved Deliverables — not
  written here; see `Sample Module Architecture.md`'s "Required ADRs"
  section.
- **Risks discovered**: the parameterless-constructor/DI-access tension
  (identified, not resolved — named for `WP 4.4`); `Tempest.App`'s
  continued non-use of `TempestHost` (pre-existing, restated plainly so
  this design's own claims aren't read as stronger than they are).
- **Readiness assessment**: the design is complete and sound. `WP 4.3`
  itself had no blocking prerequisite and proceeded to implementation
  directly — **now complete, see the WP 4.3 implementation
  retrospective.** `WP 4.4`, when it begins, should treat the identified
  ADR as its own first step before attempting to extend the sample module
  with event publishing.
