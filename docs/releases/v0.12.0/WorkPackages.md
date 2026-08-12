# TempestOS v0.12.0 — Work Packages

## Status

**In progress.** `v0.12.0` — "Desktop Composition & Domain Vocabulary
Hardening" — branch `feature/v0.12.0-desktop-composition-domain-vocabulary-hardening`,
cut from `main` at `b203aef` (the `v0.11.0` merge commit's own successor
on `main`). This document is created now, per this project's own
established convention ("each release's own `WorkPackages.md` is
created when that release's branch is cut," `WP11.4B Release Process
Correction Report.md` §10), seeded from `WP11.0B Architecture Roadmap.md`
§3's own predicted `v0.12.0` table.

**`WP 12.3A`/`WP 12.3B` are this release's own first completed Work
Packages** — a directly-commissioned pair, not named in the roadmap's own
predicted `12.0`–`12.2` slots, the identical pattern `WP 11.3A`/`WP
11.3B` and `WP 11.4A`/`WP 11.4B` each already established for
`v0.11.0`. Every roadmap-predicted Work Package below (`WP 12.0A`
through `WP 12.9.0`) remains **Not started** — this document's own
creation does not itself commission any of them.

## Work Packages

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP 12.3A` | Fault Injection & Validation Framework Architecture — traces `Tempest.App`'s/`Tempest.Desktop`'s real, unrestricted `TempestHostBuilder()` composition path end to end and finds a genuine, previously-undisclosed defect: `Tempest.Samples.DuplicateNavigationSampleModule`, a deliberately-always-failing module proving `ModuleLifecycleManager`'s per-module isolation (ADR-0013), was discovered and initialised on **every real launch** of either presentation layer, permanently leaving one module `ModuleState.Failed` — confirmed directly against `tests/Tempest.Desktop.Tests/ModuleLifecycleStabilityTests.cs`'s own pre-existing special-case exclusion. Designs `ADR-0102`: a new project, `Tempest.Validation` (namespace `Tempest.Validation.FaultInjection`), chosen over the brief's own suggested `Tempest.Diagnostics`/`Tempest.Samples.Diagnostics` naming specifically to avoid colliding with `IDiagnosticsProvider`/`DiagnosticsSampleModule`, plus a default-excluded discovery-time marker (`IFaultInjectionModule`) — project isolation alone assessed and found insufficient on its own, since `ReflectionFrameworkDiscoveryService`'s unrestricted overload scans the whole process's `AppDomain`, not only directly-referenced assemblies. Architecture only; no code. See `WP12.3A Fault Injection & Validation Framework Architecture.md` (Academy retrospective — this release currently has no dedicated `docs/releases/v0.12.0/` architecture-phase document beyond this `WorkPackages.md` row and the Academy retrospective; the full design lives in `docs/architecture/Fault Injection & Validation Architecture.md`, this platform's own established location for standing architecture documents). | Architecture | **Complete** |
| `WP 12.3B` | Fault Injection & Validation Framework Implementation — realises `WP 12.3A`'s design unchanged, with one disclosed addition found during implementation: `Tempest.Validation` also references `Tempest.Samples` (downward only, no cycle), so the moved module references `NavigationSampleModule.NavigationItemId` directly rather than duplicating it as a second string literal. `DuplicateNavigationSampleModule` moved out of `Tempest.Samples`, renamed `DuplicateNavigationModule`; new marker interface `IFaultInjectionModule` (`Tempest.Core.Modules`); `ReflectionFrameworkDiscoveryService` gains a defaulted `includeFaultInjectionModules` constructor parameter (default `false`, zero behavioural change for any existing caller); `ITempestHostBuilder`/`TempestHostBuilder` gain one new fluent method, `EnableFaultInjectionModules()`. **The actual defect closed, verified directly, not merely asserted**: `ModuleLifecycleStabilityTests.cs`'s special-case exclusion deleted, not updated — a real `WorkspaceHost`, composed through the identical production path `Tempest.App` itself uses, now genuinely reaches `Running` with zero modules `Failed`. New end-to-end proof (`FaultInjectionModuleDiscoveryTests.cs`): a real `TempestHostBuilder` discovers zero modules from `Tempest.Validation` without `.EnableFaultInjectionModules()`, and discovers + correctly isolates the fault-injection module with that one call added. Full regression re-verified directly, both configurations, immediately before this Work Package's own documentation phase and again as its closing gate: 2,233/2,233 passing (2,031 `Tempest.Core.Tests` + 202 `Tempest.Desktop.Tests`), 0 Warnings/0 Errors, both Debug and Release. No production code, ADR, or architecture change beyond what is disclosed above. See `WP12.3B Fault Injection & Validation Framework Implementation.md`, `docs/architecture/Fault Injection & Validation Architecture.md`, `ADR-0102`. | Implementation | **Complete** |
| `WP 12.0A` | Desktop Composition Root Decomposition Architecture (`A-1`) | Architecture | Not started |
| `WP 12.0B` | Desktop Composition Root Decomposition Implementation (`A-1`) | Implementation | Not started |
| `WP 12.1A` | Classification & Relationship Vocabulary Safety Net Architecture (`A-6`) | Architecture | Not started |
| `WP 12.1B` | Classification & Relationship Vocabulary Safety Net Implementation (`A-6`) | Implementation | Not started |
| `WP 12.2A` | Presentation Strategy Execution (realises `WP 11.2A`'s decision) | Implementation | Not started — scope set by `WP 11.2A` |
| `WP 12.9.0` | `v0.12.0` Release Preparation & Engineering Sign-Off | Verification only | Not started |

## Related Documents

`docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md` §3, §5, §6 (this
release's own originally-predicted scope, estimates, dependencies, and
sequence — `WP 12.3A`/`WP 12.3B` are additive to it, not a substitute for
it); `docs/academy/03 Work Packages/WP12.3A-fault-injection-validation-framework-architecture.md`;
`docs/academy/03 Work Packages/WP12.3B-fault-injection-validation-framework-implementation.md`;
`docs/architecture/Fault Injection & Validation Architecture.md`;
`ADR-0102`; `docs/releases/v0.11.0/WorkPackages.md` (the immediately
preceding release, and the direct precedent for inserting a
directly-commissioned Work Package pair outside a predicted roadmap
slot — `WP 11.3A`/`B`, `WP 11.4A`/`B`); `PROJECT_STATUS.md`.
