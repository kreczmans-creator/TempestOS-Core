# WP 4.5 — Background Services Implementation

## 1. Introduction

WP 4.5 implements the Background Services subsystem `Background Services
Architecture.md` designed and ADR-0029/ADR-0030 decided. Unlike the
architecture-only phase immediately before it, this work package produces
real, tested production code: `Tempest.Core.BackgroundServices`, wired
into `TempestHost` exactly where ADR-0030 places it.

## 2. Purpose

To build `IHostedServiceDiscoveryService`/`HostedServiceDiscoveryService`,
`IHostedServiceManager`/`HostedServiceManager`, `HostedServiceState`,
`HostedServiceStatus`, and `HostedServiceCollectionExtensions.AddDiscoveredHostedServices`,
and wire the manager into `TempestHost.RunAsync` as Hosted Services Started
(Phase 8.1, after Module Initialisation) and Hosted Services Stopped
(Phase 10.1, before Module Disposal) — closing the extensibility seam
`Runtime Host Architecture.md` named since WP 2.7A, without altering
Module Discovery, `RuntimeModuleManager`, `ModuleLifecycleManager`,
`EventBus`, Plugin infrastructure, constructor injection, or Runtime
Versioning in any way.

## 3. Background

By the time this work package began, every architectural question had
already been settled: what a hosted service is (a fourth, Host-owned
category — ADR-0029), how its failures are classified (ADR-0021), and
exactly where its two new phases sit in `Host Lifecycle.md`'s table
(ADR-0030). This work package's own brief was explicit that it must not
revisit any of those decisions unless a genuine defect was discovered —
none was. It was equally explicit about scope: implement the Host
infrastructure only; do not build feature-rich Background Services; any
sample service exists solely to validate the infrastructure.

## 4. The Problem

1. **Realise the design exactly**, without smuggling in scheduling,
   monitoring, or restart-policy capability the design deliberately
   excluded.
2. **Discover without ever instantiating a candidate** — unlike Module
   Discovery's metadata probe, a hosted service carries no metadata to
   read, so discovery must prove it never constructs one.
3. **Implement ADR-0021's isolated/critical failure model faithfully** —
   for both `StartAsync` and `StopAsync`, without weakening a critical
   service's opt-in during shutdown "because the platform is stopping
   anyway" (the exact mistake ADR-0029's own retrospective names as worth
   avoiding).
4. **Test comprehensively without mocks**, per the brief's own explicit
   instruction and this project's established testing philosophy — using
   the real `TempestHost` wherever practical, reserving a test double
   only for observing log output.

## 5. The Design

See `Tempest.Core.BackgroundServices` in full. In summary:

- **`HostedServiceDiscoveryService`**: mirrors
  `ReflectionFrameworkDiscoveryService`'s own pattern exactly — a public
  parameterless constructor scanning `AppDomain.CurrentDomain.GetAssemblies()`,
  a public constructor accepting explicit assemblies, and an `internal`
  `DiscoverHostedServiceTypes(IEnumerable<Type>)` seam for tests. A
  candidate is valid if it implements `IHostedService` and is a concrete,
  non-generic-definition class. **Never instantiates a candidate** — the
  one deliberate divergence from Module Discovery, since `IHostedService`
  carries no `Id`/`Name`/`Version` to read. Results are ordered ascending,
  ordinal, by `Type.FullName` — the same determinism guarantee Module and
  Plugin Discovery already give.
- **`HostedServiceManager`**: constructed with the discovered types, the
  real `ITempestServiceProvider`, and an optional `ILogger`. Resolves each
  service instance through the service provider (proving constructor
  injection works with no attribute or metadata prerequisite, unlike a
  discovered module). `StartAllAsync`/`StopAllAsync` share a private
  `RunBatchAsync` helper mirroring `ModuleLifecycleManager.RunBatchAsync`'s
  own sequential, per-item-isolated batch shape exactly, with one addition
  `ModuleLifecycleManager` has no equivalent for: a service implementing
  `ICriticalBackgroundService` has its exception rethrown uncaught instead
  of isolated. `StopAllAsync` iterates the reverse of `StartAllAsync`'s own
  order. `Services` exposes a locked, immutable snapshot of each service's
  `HostedServiceState`/`FailureReason` as `HostedServiceStatus`.
- **`HostedServiceCollectionExtensions.AddDiscoveredHostedServices`**:
  registers each discovered type as an ordinary, self-referential
  singleton (`services.Singleton(type, type)`), the identical Type-based
  overload `AddDiscoveredModules` already uses — no new `IServiceCollection`
  capability.
- **`TempestHost.ExecuteStartupPhasesAsync`**: discovers hosted service
  types immediately before `AddDiscoveredHostedServices`, alongside the
  existing module discovery/registration calls; after
  `lifecycleManager.StartAllAsync` completes (Module Initialisation),
  constructs `HostedServiceManager` and awaits `StartAllAsync` (Phase 8.1).
  `StopInternalAsync` (renamed to return `Exception?`) calls
  `StopAllAsync` (Phase 10.1) before its existing module-disposal block; a
  critical stop failure is captured and, if module/service disposal still
  completes, faults the Host (`EnterFaulted`) after disposal rather than
  before — preserving the existing cleanup-always-attempted guarantee.
  `DisposeAsync`'s post-fault teardown path also calls `StopAllAsync`
  (best-effort, exceptions logged and swallowed, never rethrown, so a
  second failure during post-fault teardown can never prevent `Disposed`
  from being reached). Both `TempestHost` and `TempestHostBuilder` gained
  a 3-argument internal constructor accepting a `hostedServiceCandidateTypesOverride`,
  mirroring the existing `discoveryCandidateTypesOverride`/
  `pluginsRootPathOverride` test seams exactly.

## 6. Alternatives Considered

None new — every mechanical question this work package might otherwise
have had to re-litigate was already decided by ADR-0029/ADR-0030, whose
own Alternatives Considered sections record RD-0023 through RD-0029
(DI multi-registration, a dedicated descriptor type, extending Module
Discovery, active monitoring, a new discovery phase, concurrent start,
automatic restart — all rejected). This work package's only genuinely new
judgment call was cosmetic: the design phase's working name,
`ReflectionHostedServiceDiscoveryService`, is implemented as
`HostedServiceDiscoveryService` — shorter, consistent with this work
package's own brief, and confirmed not to be a behavioural change before
proceeding (the pre-implementation validation step this brief itself
required).

## 7. Why This Solution Was Chosen

Every implementation decision traces back to ADR-0029 or ADR-0030; none
required a new architectural judgment call. Where a genuine engineering
question did arise — how to prove "never instantiates a candidate"
convincingly — the solution stayed closest to proving the claim directly:
a fixture (`ConstructorInjectedHostedService`) whose constructor requires
two platform services neither supplied to discovery would throw if
discovery ever attempted construction; discovery finding it without
throwing is direct evidence, not an inference from code inspection.

## 8. Architectural Principles

- **Reuse Before Invention** — discovery mirrors
  `ReflectionFrameworkDiscoveryService`'s pattern; orchestration mirrors
  `ModuleLifecycleManager.RunBatchAsync`'s batch shape; registration
  reuses `AddDiscoveredModules`'s own Type-based overload. Only two
  genuinely new types are introduced.
- **Host-Owned Collaborators, Never DI-Public** (ADR-0017) — extended to
  `HostedServiceDiscoveryService`/`HostedServiceManager` exactly as it
  already governs Discovery/Registration/Lifecycle: both are constructed
  directly by `TempestHost`, neither is ever registered into the
  `ServiceCollection`.
- **The Atomic Phase Principle** — cancellation is checked between
  services, via `ThrowIfCancellationRequested()` at the top of each batch
  iteration, never mid-`StartAsync`/`StopAsync` call.
- **Permissive Disposal / Cleanup Always Attempted** (ADR-0004, ADR-0019)
  — a critical hosted service's stop failure is captured, not thrown
  immediately; module and service disposal still complete before the Host
  transitions to `Faulted`, and `DisposeAsync`'s own post-fault path
  swallows (logs, never rethrows) a second failure during teardown.

## 9. Benefits

- **Every ADR-0021/ADR-0029/ADR-0030 rule is now proven, not merely
  designed** — 42 new tests exercise discovery, the manager directly, and
  the real, unmodified `TempestHost` end-to-end.
- **Zero code changes to Module Discovery, `RuntimeModuleManager`,
  `ModuleLifecycleManager`, `EventBus`, or Plugin infrastructure** — not
  merely claimed, verified: every pre-existing test in the pre-WP-4.5
  313-test suite passed unmodified once two genuine pre-existing
  test-isolation gaps (Section 10) were fixed — gaps in test seams, not in
  the systems themselves.
- **A hosted service is constructor-injectable from its first
  implementation**, confirmed directly:
  `ConstructorInjectedHostedService` resolves two genuine, already-
  registered platform services (`ILogger`, `IEventBus`) with no attribute
  or metadata prerequisite of any kind.
- **355 of 355 tests passing**, verified stable across 10 consecutive
  full-suite runs.

## 10. Trade-offs

- **Two genuine pre-existing test-isolation gaps were found and fixed
  during validation, neither a change to the Host's own design:**
  - `TempestHostBuilder`'s 1-argument and 2-argument internal test-seam
    constructors (used by `TempestHostBuilderTests`, `TempestHostTests`,
    `TempestHostPluginLifecycleTests`, `ClockModuleEventIntegrationTests`,
    `ClockModulePipelineTests`, and `ModuleMetadataAttributePipelineTests`)
    previously left hosted service discovery at its default — a full
    `AppDomain` scan. Once hosted service discovery existed, every one of
    those pre-existing tests began unintentionally discovering and
    running this work package's own `IHostedService` test fixtures,
    including a deliberately critical-failing one — this surfaced as a
    single, previously-passing test (`TempestHostTests.RunAsync_LogsEveryLifecyclePhase`)
    failing, and the full unfiltered suite taking tens of minutes instead
    of under a second once enough hosts were faulting and logging under
    full parallel load. Fixed by defaulting both constructors' hosted
    service candidate list to `Type.EmptyTypes`, mirroring the isolation
    those same constructors already gave module discovery.
  - `ClockModuleEventIntegrationTests` redirects `Console.Out` for the
    duration of a real Host run but was missing the shared
    `[Collection("Console output capture")]` attribute every other such
    test class already carries — a latent gap, present before this work
    package, that only manifested once this work package's own
    console-redirecting tests increased the odds of the race actually
    firing. Fixed by adding the attribute, consistent with the established
    precedent.
- **No sample hosted service was added to the `Tempest.Samples` set**, by
  deliberate scope decision stated in this work package's own brief ("do
  not yet build feature-rich Background Services"); test-only fixtures
  demonstrate the isolated/critical failure model instead. `WorkPackages.md`
  records this as a scope decision, not a gap.
- No ongoing supervision, monitoring, or restart policy exists for a
  hosted service once `Running` is reached — ADR-0029's own disclosed,
  accepted gap (RD-0026/RD-0029), unchanged by this implementation.

## 11. Common Mistakes

The mistake most worth naming here is one caught during validation, not
avoided by design foresight alone: assuming that adding hosted service
discovery to `TempestHost` was purely additive because "the default case
(zero hosted services) behaves identically." That was true for a *fresh*
`TempestHostBuilder()` in production, but false for every pre-existing
*test* that used the narrower, module-scoped constructor overloads —
those tests never asked for zero hosted services explicitly, and the new
discovery step's own default (scan everything) silently filled that gap
with whatever `IHostedService` fixtures happened to exist elsewhere in the
same test assembly. Running the full, unfiltered suite — not just the new
tests in isolation — is what caught this; a filtered run of only the new
Background Services tests passed cleanly every time and would never have
revealed it.

## 12. Future Evolution

- **A real, feature-rich hosted service** — deliberately out of this work
  package's own scope — is now unblocked for whichever future work
  package wants to build one against fully validated infrastructure.
- **Active monitoring and automatic restart/backoff**, both explicitly
  deferred by ADR-0029 (RD-0026, RD-0029), remain available, purely
  additively, if a real need for either emerges.
- **`WP 4.6A` (Navigation Architecture)** has no dependency on this work
  package; this was the last remaining prerequisite named for `WP 4.5`
  itself — none remain.

## 13. Key Takeaways

1. An implementation work package that follows a fully-resolved
   architecture faithfully can still surface a genuine defect — not in
   the new subsystem itself, but in how pre-existing tests' own isolation
   assumptions quietly depended on a capability (hosted service discovery)
   not yet existing. The fix belonged in the test seam, not the design.
2. "Filtered tests pass" and "the full suite passes" are different
   claims — a new subsystem's own tests can be perfectly green while
   silently breaking a dozen pre-existing tests elsewhere, and only
   running the complete, unfiltered suite surfaces that.
3. A critical hosted service's stop failure deserves exactly the same
   weight as its start failure — both are Host-fatal, and both still
   permit disposal to complete, because `Faulted → Disposed` remains
   always legal regardless of which phase produced the fault.

---

## Architectural Debt Assessment

**No new debt introduced.** The two disclosed gaps (no ongoing
supervision; no automatic restart/backoff) are ADR-0029's own accepted
trade-offs, disclosed at design time, not new debt discovered here. Every
other debt item on record from the Runtime Foundation, WP 4.0–4.4F, and
WP 4.5's own architecture phase remains exactly as previously described.

## Observations

- **Files added** (`src/Tempest.Core/BackgroundServices/`):
  `IHostedServiceDiscoveryService.cs`, `HostedServiceDiscoveryService.cs`,
  `HostedServiceState.cs`, `HostedServiceStatus.cs`,
  `IHostedServiceManager.cs`, `HostedServiceManager.cs`,
  `HostedServiceCollectionExtensions.cs` (7 new production files).
- **Files modified**: `TempestHost.cs` (hosted service discovery,
  registration, Phases 8.1/10.1, `StopInternalAsync`'s `Exception?`
  return, `DisposeAsync`'s post-fault teardown); `TempestHostBuilder.cs`
  (new 3-argument internal constructor; 1-/2-argument constructors now
  default hosted service discovery to `Type.EmptyTypes`, the test-isolation
  fix described in Section 10).
- **Test files added**:
  `tests/Tempest.Core.Tests/BackgroundServices/HostedServiceFixtures.cs`,
  `HostedServiceDiscoveryServiceTests.cs`, `HostedServiceManagerTests.cs`,
  `tests/Tempest.Core.Tests/Runtime/TempestHostHostedServiceTests.cs`.
- **Test files modified**:
  `tests/Tempest.Core.Tests/Samples/ClockModuleEventIntegrationTests.cs`
  (added `[Collection("Console output capture")]` — the second
  test-isolation fix described in Section 10).
- **Tests added**: 42 — discovery (deterministic ordering, never
  instantiating a candidate, exclusion of interfaces/abstract
  classes/open generic definitions/non-implementing types, critical-marker
  discovery, repeatability with the same and a fresh discovery instance,
  assembly-scoped discovery); the manager directly (constructor-injected
  resolution, null-argument guards, ascending-`FullName` start ordering,
  sequential dispatch, reverse stop ordering, repeated start/stop, status
  snapshot, isolated start/stop failure, critical start/stop failure,
  cancellation between services); the real, unmodified `TempestHost`
  end-to-end (reaching `Running`/`Stopped`, phase-completion logging,
  start-after-Module-Initialisation/stop-before-Module-Disposal ordering
  via captured console output, multiple services in deterministic order,
  isolated failure still reaching `Running`, critical start failure
  faulting the Host and not preventing disposal, critical stop failure
  faulting the Host while disposal still completes, repeated execution
  across fresh hosts, zero-hosted-service regression safety).
- **Test results**: 355 of 355 passing (313 pre-existing + 42 new), 0
  failures, verified stable across 10 consecutive full-suite runs.
- **Build results**: 0 warnings, 0 errors.
- **Regressions found and fixed during this work package** (both
  test-seam fixes, no production behaviour affected beyond the two
  `TempestHostBuilder` constructor defaults described above): hosted
  service discovery's full-`AppDomain`-scan default unintentionally
  reaching pre-existing tests' own `IHostedService` fixtures; a
  pre-existing `Console.Out`-redirection race, the same class of hazard
  already found and fixed twice before (WP 4.1→4.2, WP 4.2), recurring a
  third time in a test class that had never joined the shared collection.
- **Readiness assessment**: WP 4.5 is complete. Every prerequisite named
  by `Background Services Architecture.md` (ADR-0021, ADR-0029, ADR-0030)
  is resolved and now realised in working code. No architectural blocker,
  and no known implementation gap, remains for this feature.
