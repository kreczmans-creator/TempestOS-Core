# TempestOS v0.4.0 — Testing Strategy

## Baseline

This release extends, and does not replace, the existing Engineering
Standard (`docs/academy/06 Engineering Standards/02-testing-strategy.md`).
Every convention established across WP 2.1 through WP 2.7B applies
unchanged: the internal-test-seam pattern for ambient/broad contracts,
minimal and clearly-separated test fixtures, explicit test-category
coverage matched to each work package's own brief, regression tests named
for the scenario they reproduce, and a full build/test run from a clean,
committed tree before any work package is reported done.

Starting point: **164 tests passing, 0 warnings, 0 errors** (the v0.3.0
baseline). Every work package in this release must leave that number
undiminished and unbroken before its own new tests are even considered.

## Per-Work-Package Test Categories

Each work package's own entry in `WorkPackages.md` implies the test
categories below. This table exists so a reviewer can check, for any given
work package, whether its actual test suite matches what was expected
before implementation began — the same discipline
`02-testing-strategy.md` already describes for prior work packages.

| Work Package | Expected Test Categories |
|---|---|
| WP 4.0 Platform Contracts | Contract compilation/documentation examples only — no runtime behaviour yet to test. Scope is deliberately narrow: `INavigationProvider` and `IDiagnosticsProvider` are not defined here at all, so there is nothing to test for either. |
| WP 4.1 Module SDK | Contract stability (no accidental breaking change to `IModule`/`IModuleLifecycle`); documentation examples actually compile and run. |
| WP 4.2 Plugin Manifest | Manifest parsing (valid/invalid/malformed), manifest-to-descriptor mapping, ordering relative to Module Discovery. |
| WP 4.3 Sample Module | End-to-end: discovered, registered, initialised, started, stopped, disposed, exactly like any other module, with no special-casing. |
| WP 4.4 Event Bus | Publish/subscribe basic behaviour, multiple subscribers, subscriber failure isolation, ordering/re-entrancy behaviour, proven against the WP 4.3 sample module and its companion. |
| WP 4.5 Background Services | Start/stop ordering relative to Module Initialisation and Module Disposal, cancellation observed only between atomic operations, isolated-by-default failure (ADR-0021), critical-service opt-in Host-fatal path. |
| WP 4.6A Navigation Architecture | None — architecture-only; no implementation to test. |
| WP 4.6B Navigation Implementation | Defined entirely by WP 4.6A's own architecture document once it exists. |
| WP 4.7 Command Framework | Command registration, dispatch, typed-parameter handling, the command/event distinction (a command with zero handlers, a command with more than one candidate handler), proven against the sample module set. |
| WP 4.8 Diagnostics Improvements | Composite sink fan-out (all sinks receive an entry; one sink's failure does not affect another — extending the existing Logger sink-isolation tests), health/status read-only projection accuracy. |
| WP 4.9 Developer Experience | Template scaffolding produces a buildable, discoverable module without manual correction. |

## New Testing Concerns This Release Introduces

- **Cross-module interaction tests** (Event Bus, Command Framework) are new
  in kind — every prior work package's tests exercised one component at a
  time (with `ModuleLifecycleManager` as the closest prior example of
  multi-module batch behaviour). Tests here should follow that same
  precedent: deterministic coordination (`TaskCompletionSource` gates, as
  `TempestHostTests` already established for cancellation/shutdown timing),
  never fixed sleeps or timing windows.
- **Host sequence extension tests** (Background Services, Plugin Manifest)
  must prove the new phase(s) fit into the existing, frozen sequence
  without disturbing it — a regression suite against the *existing*
  `TempestHostTests`/`Host Lifecycle.md` expectations is as important as
  the new phase's own tests.

## What Does Not Change

- No test parallelism assumptions change — tests within a class remain
  sequential by xUnit's default per-class collection behaviour, exactly as
  `TempestHostTests` and `ModuleLifecycleManagerTests` already rely on.
- No new test framework, mocking library, or assertion library is
  introduced. The existing xUnit + hand-written test doubles convention
  (`RecordingLogSink`, `ThrowingLogSink`, `RecordingLifecycleModuleAlpha`,
  and this release's own new fixtures following the same shape) continues.

## Exit Criteria for the Release as a Whole

- Every work package's own Acceptance Criteria (`WorkPackages.md`) is met.
- Full solution build: 0 warnings, 0 errors.
- Full test run: 100% pass, from a clean, fully-committed tree, on `main`
  after merge — exactly as `scripts/New-Release.ps1` already validates for
  a release.
