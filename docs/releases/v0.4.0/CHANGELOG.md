# TempestOS v0.4.0 — Changelog

## Status

**Unreleased.** Implementation is underway. Entries below are added as
each work package actually lands — not written in advance as predictions.
Compare against `WorkPackages.md` for what is planned but not yet
reflected here.

---

## [Unreleased]

### Added

- **Platform Contracts (WP 4.0)** — `IModule` (re-affirmed), `IHostedService`,
  `ICriticalBackgroundService`, `ICommand`, `IEvent`, `IEventHandler<T>`.
  `INavigationProvider`/`IDiagnosticsProvider` deliberately not defined yet.
- **Module SDK (WP 4.1)** — `ModuleBase`, `ModuleLifecycleBase`
  (`Tempest.Core.Modules`).
- **Plugin Manifest architecture (WP 4.2)** — design only; no code.
  `Plugin Manifest Architecture.md`.
- **Runtime Platform Version Infrastructure (WP 4.2A)** —
  `IPlatformVersionProvider`, `PlatformVersionProvider`, `PlatformVersion`
  (`Tempest.Core.Versioning`); `Directory.Build.props` now derives
  `<Version>` from the repository's own `VERSION` file.
- **Plugin Failure Classification (WP 4.2B)** — architecture only; no
  code. ADR-0025.
- **Plugin Discovery Lifecycle Placement (WP 4.2C)** — architecture only;
  no code. ADR-0026. `Host Lifecycle.md` gains two new phases (`3.1 Plugin
  Discovery`, `3.2 Plugin Loading`), inserted between Logging Built and
  Module Discovery without renumbering the existing thirteen phases. This
  was the last remaining prerequisite before `WP 4.2` implementation —
  none remain.
- **Plugin Manifest implementation (WP 4.2)** — `PluginManifest`,
  `PluginException` and five subtypes, `IPluginManifestDiscoveryService`/
  `PluginManifestDiscoveryService`, `IPluginAssemblyLoader`/
  `PluginAssemblyLoader` (`Tempest.Core.Plugins`). `TempestHost` now runs
  Plugin Discovery (Phase 3.1) and Plugin Loading (Phase 3.2) between
  Logging Built and Module Discovery, exactly per ADR-0026; Module
  Discovery, Registration, and Lifecycle are unchanged. 27 new tests.

_Still planned, per `WorkPackages.md`:_

- Sample Module (WP 4.3)
- Event Bus (WP 4.4)
- Background Services (WP 4.5)
- Navigation Architecture (WP 4.6A), then Navigation Implementation
  (WP 4.6B)
- Command Framework (WP 4.7)
- Diagnostics Improvements (WP 4.8)
- Developer Experience Improvements (WP 4.9)

### Changed

_Nothing yet._

### Fixed

- A flaky test in WP 4.1's own test suite (`SdkLifecycleLog` shared static
  state across two xUnit classes that could run concurrently) — found and
  fixed during routine validation before WP 4.2 began.
- Two test-only regressions found during WP 4.2's own implementation, both
  fixed without touching production code: a cross-test dynamic assembly
  identity collision (two test methods' dynamically-built plugin
  assemblies shared a simple name, so `Assembly.LoadFrom` silently
  resolved to whichever loaded first — fixed via GUID-suffixed assembly
  identities); and the same `Console.Out`-redirection race pattern
  already fixed once for `SdkLifecycleLog`, recurring between
  `TempestHostTests` and the new `TempestHostPluginLifecycleTests` — fixed
  via a shared `[Collection("Console output capture")]`.

### Architecture Decision Records

- **ADR-0020** — The Event Bus Is a DI-Public Platform Service. Decided
  during planning, before implementation (WP 4.0/4.4).
- **ADR-0021** — Background Service Failures Are Isolated by Default;
  Criticality Is Opt-In. Decided during planning, before implementation
  (WP 4.0/4.5).
- **ADR-0022** — Navigation and Commands Are Orthogonal Platform Services.
  Decided during planning, before implementation (WP 4.0/4.6A/4.7).
- **ADR-0023** — Platform Layering: Dependencies Flow Downward Only.
  Decided during planning; applies platform-wide, not only to this
  release (see `docs/releases/FOUNDATION.md`).
- **ADR-0024** — Platform Contracts Are Packaged by Capability, Not a
  Shared Contracts Namespace. Decided during WP 4.0.
- **ADR-0025** — Plugin Failure Classification. Decided during WP 4.2B —
  isolated for every plugin-loading failure category except a genuine
  Host-level defect in the loading orchestration itself.
- **ADR-0026** — Plugin Discovery Lifecycle Placement. Decided during
  WP 4.2C — two new decimal-numbered phases (`3.1 Plugin Discovery`,
  `3.2 Plugin Loading`) inserted between Logging Built and Module
  Discovery; `PlatformVersionProvider` construction moves earlier (its DI
  registration does not); candidate folders sorted ordinally by name for
  deterministic duplicate-identity resolution. This was the last
  architectural blocker before `WP 4.2` implementation.
- Expected, not yet written: Navigation's `Tempest.Core` placement
  (`WP 4.6A`) — see `Architecture.md`. No further ADR is expected before
  `WP 4.2` implementation.

---

## How This File Is Maintained

Each work package adds its own entries here as part of its own Definition
of Done (`ReleaseChecklist.md`), under the correct `Added`/`Changed`/
`Fixed` heading, referencing its work package number (e.g. "WP 4.4 — Event
Bus: added `IEventBus` with per-subscriber failure isolation."). This file
is not written retroactively at release time from memory — it is a running
record, exactly like `Risks.md`.
