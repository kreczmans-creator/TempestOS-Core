# ADR-0102: Fault-Injection Modules Are Isolated By Project Reference *and* a Default-Excluded Discovery Marker

## Status

Accepted — `v0.12.0`, `WP 12.3A`/`WP 12.3B`, 2026-08-12.

## Context

`Tempest.Samples` shipped one module, `DuplicateNavigationSampleModule`
(`WP 5.0B`), whose entire purpose is to always fail: its
`InitialiseAsync` deliberately registers a duplicate
`NavigationItem.Id`, throwing `DuplicateNavigationItemException`, so
that `ModuleLifecycleManager`'s existing, unmodified per-module
isolation (ADR-0013) has something real to demonstrate.

Because `Tempest.App`/`Tempest.Desktop` both reference `Tempest.Samples`
and both compose their real `TempestHostBuilder` unrestricted (a genuine
full-`AppDomain` scan, via `EngineeringWorkspaceComposer.Build()` →
`WorkspaceHost`), this module was discovered and initialised on **every
real run** of the shipped application — confirmed directly:
`tests/Tempest.Desktop.Tests/ModuleLifecycleStabilityTests.cs` had to
special-case-exclude its module Id from every "no module failed"
assertion just to pass. `IDiagnosticsProvider.Modules` therefore never
reported a genuinely healthy platform in real use — a permanent false
positive baked into the runtime health signal ADR-0039 exists to make
trustworthy.

**The question this ADR answers**: how does a deliberately-failing
module stay real, tested, and available for validation runs, without
ordinary application startup ever discovering it?

Two candidate mechanisms were evaluated, both alone found insufficient:

1. **Project isolation alone** (move it to a project `Tempest.App`/
   `Tempest.Desktop` never reference). Sufficient for the two real,
   shipped processes — an assembly never loaded cannot appear in that
   process's `AppDomain.GetAssemblies()`. **Not** sufficient in general:
   `ReflectionFrameworkDiscoveryService`'s unrestricted overload scans
   the *whole process's* `AppDomain`, not just directly-referenced
   assemblies. Any future process that happens to load both the new
   project (for its own legitimate reason — the test suite already
   does) and constructs an unrestricted `TempestHostBuilder()` would
   silently resurrect the identical false-positive hazard, with no
   compile-time or code-review signal that it had.

2. **A default-excluded discovery-time marker alone** (no project move,
   filter by type). Sufficient to prevent discovery in any process,
   regardless of what is loaded — but leaves the module physically
   inside `Tempest.Samples`, contradicting its own register/namespace
   documentation (a "sample module" that cannot actually be sampled)
   and offering no natural place to grow a second, third fault-injection
   module without further cluttering a project whose entire purpose,
   documented since `WP 4.3` (`Sample Module Architecture.md`), is
   genuine reference modules a third-party author could plausibly have
   written.

## Decision

**Both, together — neither replaces the other:**

1. **Project isolation.** A new project, `Tempest.Validation`
   (`src/Validation/Tempest.Validation/`), namespace
   `Tempest.Validation.FaultInjection` for this category. Referenced by
   `Tempest.Core.Tests` only (and, when a fault-injection module needs
   to collide with a specific `Tempest.Samples` module's own
   registration, by `Tempest.Samples` too — downward only, never the
   reverse). **Never referenced by `Tempest.App` or `Tempest.Desktop`.**
   `DuplicateNavigationSampleModule` moves here, renamed
   `DuplicateNavigationModule` (namespace already says what it is; the
   name no longer needs to). Only the one category that exists today,
   `FaultInjection`, is built — `Lifecycle`/`Performance`/`Compatibility`
   are named as a deliberately deferred future extension point in
   `Fault Injection & Validation Architecture.md`, never built as empty
   scaffolding ahead of a real need.

2. **`IFaultInjectionModule : IModule`** (`Tempest.Core.Modules`) — a
   marker interface, no members. `ReflectionFrameworkDiscoveryService`
   gains a defaulted `includeFaultInjectionModules: bool = false`
   constructor parameter on both existing constructors; a candidate
   implementing this interface is excluded exactly like an interface,
   abstract class, or open generic type definition, unless the flag is
   `true`. Applies identically to the full-`AppDomain` overload and the
   explicit-candidate-type overload, so an explicit list naming a
   fault-injection module by type still requires the flag.
   `ITempestHostBuilder`/`TempestHostBuilder` gain one new fluent,
   public method, `EnableFaultInjectionModules()`, the explicit
   "enabled for validation runs" surface this Work Package's brief
   asked for.

Neither piece alone gives the guarantee both give together: project
isolation is what keeps the assembly out of the two real shipped
processes at all; the marker is what keeps discovery honest even inside
a process where the assembly legitimately is loaded (the test suite,
today; any future validation-run tool, later).

## Consequences

**Positive:**

- The false positive is gone, verified directly: a real
  `EngineeringWorkspaceComposer.Build()`/`WorkspaceHost` run now reaches
  `Running` with **zero** modules in `ModuleState.Failed` —
  `ModuleLifecycleStabilityTests.cs`'s own special-case exclusion is
  deleted, not merely updated, because it is genuinely no longer needed.
- **Zero behavioural change for any existing caller.** Both new
  constructor parameters default to their current, unchanged behaviour;
  `ITempestHostBuilder`'s one new method is additive; no existing
  Discovery/Registration/Lifecycle code path, `HostState`, or failure
  category changes. `DuplicateNavigationModule`'s own failure remains an
  ordinary, already-covered isolated module failure (ADR-0013) —
  exactly as it already was.
- **A real, working validation-run path**, proven end-to-end by
  `FaultInjectionModuleDiscoveryTests.cs`: `new TempestHostBuilder([…])
  .EnableFaultInjectionModules().Build()` discovers and initialises the
  real `DuplicateNavigationModule` through the real, unmodified Host,
  isolated exactly as before.
- **Extensible without further Host or Discovery change.** A second
  fault-injection module — a startup failure, an event-handler failure,
  a plugin-load failure, named as future possibilities in `Fault
  Injection & Validation Architecture.md` — needs only to implement
  `IFaultInjectionModule` and live under `Tempest.Validation`; no
  further change to `ReflectionFrameworkDiscoveryService`,
  `TempestHostBuilder`, or `TempestHost` is required.

**Negative:**

- **A second, small opt-in surface now exists on `ITempestHostBuilder`**
  that every future implementer of the interface (today, only
  `TempestHostBuilder` itself) must honour. Judged acceptable: the
  interface already has exactly this "collect inputs, mutate before
  `Build()`" shape for `AddConfigurationSource`; this is the same shape
  applied a second time, not a new one.
- **A module author who forgets `IFaultInjectionModule`** on a genuinely
  deliberately-failing module gets no compile-time error — it is simply
  discovered like any ordinary module, in every process, exactly as
  `DuplicateNavigationSampleModule` was before this ADR. Mitigated by
  project placement being the natural, visible first signal (a
  deliberately-failing module has no reason to live anywhere but
  `Tempest.Validation`), and by this ADR's own documentation making the
  convention explicit for the next contributor.

## Alternatives Considered

**Assembly-name string matching inside `Tempest.Core`** (Discovery
checks `type.Assembly.GetName().Name == "Tempest.Validation"` instead of
a marker interface). Rejected: this would require `Tempest.Core` — the
platform's own lowest layer — to hardcode awareness of a specific,
named upstream project, a direct violation of ADR-0023's
downward-dependency-only layering. A marker interface *defined* in
`Tempest.Core` and *implemented* by an upstream project is the
established, correct direction (the same shape `IModule` itself already
uses); a hardcoded assembly name would invert it.

**A second `ModuleMetadataAttribute`-style attribute**
(`[FaultInjectionModule]`) instead of a marker interface. Considered,
since `ModuleMetadataAttribute` (ADR-0027) is the closest existing
precedent for a discovery-time classification. Rejected: `ModuleMetadataAttribute`
exists specifically to let Discovery read `Id`/`Name`/`Version` *without
instantiating* a candidate — a real, load-bearing problem for
constructor-injected modules. This decision asks a simpler question
("is this type a fault-injection module, yes or no") that a plain
`is`/`IsAssignableFrom` type test answers with no reflection-attribute
lookup and no risk of the attribute/reality drift ADR-0027 itself
already discloses as an accepted risk for its own attribute. A marker
interface is the lighter-weight, more direct tool for an "is-a"
question.

**Not referencing `Tempest.Samples` from `Tempest.Validation`, using a
hardcoded string literal for the colliding `NavigationItem.Id` instead
of `NavigationSampleModule.NavigationItemId`.** Considered, to keep
`Tempest.Validation`'s own dependency graph to `Tempest.Core` alone.
Rejected: a second, independent string literal duplicating
`NavigationSampleModule`'s own already-public `NavigationItemId`
constant would silently drift out of sync if that constant ever changed
— a real, avoidable fragility for zero benefit, when the constant is
already public and the dependency direction (`Tempest.Validation` →
`Tempest.Samples`, both downward, no cycle) introduces nothing ADR-0023
forbids.

**Leaving `DuplicateNavigationSampleModule` in `Tempest.Samples` and
relying on project isolation alone**, betting that no future process
ever loads both a fault-injection assembly and constructs an
unrestricted `TempestHostBuilder()`. Rejected as the sole mechanism —
see Context, above: the test suite already does exactly this today, and
the whole point of a "genuine guarantee" (the Work Package's own brief)
is that it must not depend on every future caller remembering a
convention.

## Future Considerations

**Further fault-injection categories.** `Fault Injection & Validation
Architecture.md` names `Lifecycle`, `Performance`, and `Compatibility`
as plausible future sibling namespaces under `Tempest.Validation`, none
built here — each would follow this ADR's own two-piece pattern
(`IFaultInjectionModule` plus project placement) without requiring a
further ADR, unless a genuinely new architectural question surfaces
when one is actually built.

**A validation-run CLI or hosted tool.** `EnableFaultInjectionModules()`
is deliberately a plain builder method, not yet wired to any
command-line switch or standalone executable — no real consumer needs
one yet beyond the test suite. If one is built, it constructs
`TempestHostBuilder` and calls this method exactly as
`FaultInjectionModuleDiscoveryTests.cs` already proves works, with zero
further platform change required.

## Related Documents

`ADR-0013` (module isolation, unmodified/reaffirmed); `ADR-0023`
(platform layering); `ADR-0027` (closest existing precedent for a
discovery-time classification mechanism); `ADR-0032` (Navigation's own
imperative registration, cited by the moved module itself); `Fault
Injection & Validation Architecture.md`; `Sample Module Architecture.md`;
`Diagnostics Architecture.md`; `docs/releases/v0.12.0/WorkPackages.md`
(`WP 12.3A`/`WP 12.3B`).
