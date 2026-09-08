# WP 4.3 — Sample Module Implementation

## 1. Introduction

WP 4.3 implements `ClockModule` exactly as `Sample Module Architecture.md`
designed it: a small, self-contained, SDK-built module that becomes the
living reference `WP 4.4` through `WP 4.7` extend and validate against.
Unlike the architecture phase immediately before it, this work package
produces real, tested production code — `Tempest.Samples` — with zero
change to any existing platform file.

## 2. Purpose

To prove, concretely rather than by design-time reasoning alone, that a
normal module travels through the complete Platform Services pipeline —
Discovery, Registration, Dependency Injection, Lifecycle — without
requiring any platform change, and to demonstrate that the Module SDK's
own documentation (*Building a Module*) is sufficient, on its own, for a
third-party-style author to build a working module from.

## 3. Background

Every architectural question had already been settled by the design
phase: what the module models (a clock), where it lives (`src/Samples/`,
a new project, not inside `Tempest.Core` or `src/Plugins/`), what it can
and cannot consume (no platform service, per the documented
parameterless-constructor constraint), and what is deliberately deferred
(a companion module, plugin packaging, any constructor dependency). This
work package's own brief was explicit that the one architectural question
the design phase surfaced — the parameterless-constructor/DI-access
tension — belongs to `WP 4.4` and must not be solved here.

## 4. The Problem

1. **Implement `ClockModule` exactly as specified**, without smuggling in
   any capability the design deferred.
2. **Prove the pipeline requires no special-casing**, not merely assert it
   — with real Discovery, real Registration, and real
   `ModuleLifecycleManager` orchestration, not a mock standing in for any
   of them.
3. **Find a way to observe a specific module's own instance state after a
   full pipeline run**, given `ITempestHost` deliberately exposes nothing
   module-specific (ADR-0017) — a question the design document's own
   Testing Strategy had not fully resolved (see Section 6).
4. **Avoid the exact full-`AppDomain`-scan test hazard** the design phase
   already flagged, without introducing a new one.

## 5. The Design

See `Tempest.Samples/ClockModule.cs` in full, and `Sample Module
Architecture.md`'s own public surface listing — implemented without
deviation: a public, zero-argument constructor calling
`ModuleLifecycleBase`'s own constructor with literal `Id`/`Name`/`Version`
values; `InitialisedAt`/`StartedAt`/`StoppedAt`/`IsRunning`/`Uptime`
computed and recorded for real inside `InitialiseAsync`/`StartAsync`/
`StopAsync`; `DisposeAsync` not overridden, inheriting the SDK's no-op
default, since there is nothing to release.

`src/Samples/Tempest.Samples/Tempest.Samples.csproj` references
`Tempest.Core` only — no `Tempest.Core.Tests` reference, no test framework
dependency, proving the module's own production code has no test-only
coupling. The test project gained a `ProjectReference` to
`Tempest.Samples`, the only wiring change needed to exercise a real,
compiled, non-test assembly from within the existing test suite.

## 6. Alternatives Considered

**Asserting `ClockModule`'s own timestamps through `TempestHostBuilder`
directly**, as the design document originally proposed. Investigated
first, since it is what the design called for. Found not to work:
`ITempestHost`'s public surface exposes only `State`/`RunAsync`/
`StopAsync` — no way to reach a specific module's resolved instance,
deliberately, per ADR-0017 (Discovery/Registration/Lifecycle are
Host-owned, and the Host does not re-expose what it deliberately keeps
private). Resolved by composing the same pipeline pieces `TempestHost`
itself composes internally — `RuntimeModuleManager`, `ServiceCollection`,
`TempestServiceProvider`, `ModuleLifecycleManager` — directly in the test,
exactly mirroring `ModuleLifecycleManagerTests`' own established
composition-root pattern, then resolving `ClockModule` a second time from
the same provider (a singleton) to inspect the identical instance
`ModuleLifecycleManager` drove. This is not a workaround — it is a
stronger proof of the same claim, since it exercises the real, public
pipeline pieces directly rather than a wrapper that was never designed to
re-expose them. The design document is corrected accordingly, not left to
describe a test that could not be written as stated.

**Continuing to describe the full-`AppDomain`-scan test hazard as caused
partly by `internal`-visibility fixtures being unconstructible without
`InternalsVisibleTo`.** Investigated while writing the scoped-Discovery
tests, since the claim needed to be relied upon precisely. Found to be
factually incorrect: every existing `TempestHostTests` test already
constructs `internal` fixtures (`HealthyHostTestModuleAlpha`, and others)
via `Activator.CreateInstance` from `Tempest.Core`, successfully, across
the assembly boundary, with no `InternalsVisibleTo` needed for that
purpose — `Activator.CreateInstance` requires the *constructor* to be
public, not the *type*. The sole confirmed hazard is `InvalidIdModule`'s
deliberately empty `Id`, which throws a genuine `ModuleDiscoveryException`
regardless of visibility. The design document is corrected to state this
precisely; the scoping strategy it recommends was already correct and is
unchanged.

## 7. Why This Solution Was Chosen

Every implementation decision traces directly back to the approved
design; the two corrections above are not new decisions but corrections
to how two of the design's own claims were stated, found while proving
them rather than merely restating them. Where the design's own words and
the platform's actual, real behaviour disagreed, the real behaviour
governed — consistent with this project's own established discipline of
documenting a found contradiction honestly rather than silently patching
around it (`FOUNDATION.md`, "What Future Contributors Must Preserve").

## 8. Architectural Principles

- **Reuse Before Invention** — the composition-root pattern used to prove
  the full pipeline is not new; it is `ModuleLifecycleManagerTests`' own
  existing pattern, applied to a real production module instead of a test
  fixture for the first time.
- **Fail Fast, precisely** — the scoped-Discovery tests prove exactly what
  they claim (a specific assembly's own content), rather than a broader,
  noisier claim (a full `AppDomain` scan) that would fail for unrelated
  reasons.
- **Downward Dependency Direction** (ADR-0023) — `Tempest.Samples`
  references `Tempest.Core` only; verified directly in its own `.csproj`,
  not merely asserted.
- **Minimal Host Complexity** — zero lines of `TempestHost.cs`,
  `ReflectionFrameworkDiscoveryService.cs`, `RuntimeModuleManager.cs`, or
  `ModuleLifecycleManager.cs` were touched; `git diff` against each is
  empty.

## 9. Benefits

- **The pipeline's "no special-casing" claim is now proven by a real,
  production module**, not merely by test fixtures that were always
  understood to be disposable stand-ins.
- **Two small, genuine inaccuracies in the approved design were found and
  corrected** before they could mislead a future reader — a design
  document is not weakened by a found correction stated plainly; it is
  strengthened, exactly as this project's own Rejected Designs and ADR
  practice already treats a found contradiction as valuable information,
  not an embarrassment.
- **`WP 4.4` now has a real, compiled, referenceable module** to extend,
  rather than a design description alone.

## 10. Trade-offs

- `ClockModule` remains, honestly, less capable than a real TempestOS
  module will eventually need to be — no configuration, no logging, no
  platform-service access at all, an accurate reflection of the pipeline's
  current limits, not a shortfall specific to this implementation.
- Proving `ClockModule`'s own instance state required composing the
  pipeline manually in tests rather than through `TempestHostBuilder`
  alone — a slightly larger test surface than the design anticipated, for
  a legitimate, now-documented reason (ADR-0017's own deliberate
  non-exposure).

## 11. Common Mistakes

The mistake most worth naming here is one avoided: when the design's
own proposed `TempestHostBuilder`-based assertion turned out not to be
achievable, the temptation was to weaken `ITempestHost`'s contract (add a
way to reach a module's instance) to make the originally-planned test
possible. This was not done — ADR-0017 deliberately keeps this
information out of the Host's public surface, for good reason (a module
resolving another module, or an external caller reaching into the
pipeline's own internals, is exactly the boundary violation that ADR
exists to prevent). Composing the same real pieces directly in the test,
instead, proved the same claim without touching that boundary at all.

## 12. Future Evolution

- **`WP 4.4`** should resolve the parameterless-constructor/DI-access
  tension via its own ADR before extending `ClockModule` to publish an
  event, exactly as the design phase recommended.
- **A companion module** remains `WP 4.4`'s own responsibility to add, per
  its Deliverables.
- **`Sample Module Architecture.md`'s two corrected claims** should be
  read as the current, authoritative statement — this retrospective
  documents why they changed, not a discrepancy to reconcile further.

## 13. Key Takeaways

1. A design document's own untested claims should be verified, not merely
   transcribed into code — this work package found two, both small, both
   worth correcting rather than working around silently.
2. Proving "no special-casing" convincingly sometimes requires composing
   a pipeline's real pieces directly, not only driving it through its own
   highest-level entry point — `ModuleLifecycleManagerTests`' own
   precedent existed for exactly this reason and was reused, not
   reinvented.
3. A boundary a prior ADR deliberately drew (ADR-0017: the Host does not
   re-expose module instances) is a reason to change the test, not the
   boundary — recognising which side of that choice to take is itself a
   small but real architectural judgment call this work package made
   correctly.

---

## Architectural Debt Assessment

**No new debt introduced.** `ClockModule`'s own inability to consume any
platform service is pre-existing, already-documented debt (`WP 4.1`),
restated here as exercised rather than newly found. No other debt item on
record from the Runtime Foundation, WP 4.0–4.2, or WP 4.2D changes as a
result of this work package.

## Observations

- **Files added**: `src/Samples/Tempest.Samples/Tempest.Samples.csproj`;
  `src/Samples/Tempest.Samples/ClockModule.cs`;
  `tests/Tempest.Core.Tests/Samples/ClockModuleTests.cs`;
  `tests/Tempest.Core.Tests/Samples/ClockModuleDiscoveryTests.cs`;
  `tests/Tempest.Core.Tests/Samples/ClockModulePipelineTests.cs`.
- **Files modified**: `src/TempestOS.slnx` (new project added);
  `tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj` (new
  `ProjectReference`). **Zero existing production source file was
  modified** — confirmed directly: `git diff --stat` against every
  existing file under `src/Tempest.Core/` and every pre-existing test
  file is empty.
- **Tests added**: 18 — module metadata correctness (2); timestamp
  recording per lifecycle method (3); lifecycle ordering (1); `Uptime`
  behaviour before/during/after running (3); scoped Discovery success and
  metadata correctness (1); repeatable discovery, both same-instance and
  fresh-instance (2); isolation from an unrelated real module, both
  positively (found alongside) and structurally (assembly-scoped result
  contains only the sample assembly's own types) (2); successful
  registration from real Discovery output (1); the full, really-composed
  Discovery → Registration → DI → Lifecycle pipeline, asserting ordering
  and timestamps against the actual driven instance (1); Host-level
  black-box proof, alone and alongside another module (2).
- **Test results**: 260 of 260 passing (242 pre-existing + 18 new), 0
  failures, verified stable across 5 consecutive full-suite runs.
- **Build results**: 0 warnings, 0 errors.
- **Platform changes**: none. `Tempest.Core.Modules.ReflectionFrameworkDiscoveryService`,
  `RuntimeModuleManager`, `ModuleLifecycleManager`, `TempestHost`, and
  `TempestHostBuilder` are byte-for-byte unchanged from before this work
  package.
- **Corrections made to already-approved design documentation**: two, both
  found during test-writing, both documented in Section 6 above and
  reflected in `Sample Module Architecture.md`'s own Testing Strategy
  section — neither required revisiting any architectural decision, only
  the wording of how two claims were proven.
- **Readiness assessment**: WP 4.3 is complete. The sample module travels
  through the complete, real, unmodified Platform Services pipeline with
  no special-casing, proven at three levels (unit, scoped-pipeline, and
  Host black-box). No architectural blocker exists for `WP 4.4`, beyond
  the one already-identified ADR that work package must resolve as its
  own first step before extending `ClockModule` with event publishing.
