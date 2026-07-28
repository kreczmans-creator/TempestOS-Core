# Test Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Test Register |
| **Purpose** | The index of the test suite by subsystem — file counts, test-method counts, and which platform capability each directory covers. |
| **Scope** | Every `.cs` file under `tests/Tempest.Core.Tests/`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `tests/Tempest.Core.Tests/` (direct inspection); `dotnet test` output. |
| **Review Frequency** | Updated whenever the test suite's total count changes materially (in practice, every Work Package). |
| **Last Reviewed** | 2026-07-28 (WP 5.0S) — counts re-verified directly via a fresh `dotnet test` run; 448/448, 2 new regression tests added to `Plugins/` (plugin manifest `AssemblyFileName` path-containment). |
| **Related Documents** | `docs/releases/v0.4.0/Testing.md`; `Validation Register.md`; `Repository Metrics Register.md`. |
| **Related ADRs** | None directly. |
| **Related Academy Articles** | `docs/academy/06 Engineering Standards/02-testing-strategy.md`. |
| **Coverage Status** | Complete. |

---

## Entries

| Directory | Files | `[Fact]`/`[Theory]` Attributes | Covers |
|---|---|---|---|
| `BackgroundServices/` | 4 | 37 | Hosted service discovery, manager, fixtures (WP 4.5) |
| `Commands/` | 1 | 2 | `ICommand` contract (WP 4.0) |
| `Configuration/` | 4 | 31 | Configuration Framework (WP 2.5) |
| `DependencyInjection/` | 3 | 22 | Custom DI container (WP 2.4) |
| `Events/` | 4 | 27 | Event Bus (WP 4.0 contracts, WP 4.4D bus) |
| `Logging/` | 9 | 42 | Logging & Diagnostics Framework (WP 2.6) |
| `Modules/` | 13 | 72 | Discovery, Registration, Lifecycle, Module SDK, `ModuleMetadataAttribute` (WP 2.1–2.3, WP 4.1, WP 4.4A/B) |
| `Navigation/` | 2 | 31 | `NavigationItem`, `NavigationService` — registration, ordering, hierarchy, visibility, events, DI (WP 5.0B) |
| `Plugins/` | 6 | 21 | Plugin manifest, discovery, loading (WP 4.2); extended `WP 5.0B` with a Navigation-registering dynamic plugin assembly builder; extended `WP 5.0S` with 2 `AssemblyFileName` path-containment regression tests |
| `Runtime/` | 5 | 50 | `TempestHost`, `TempestHostBuilder`, plugin/hosted-service Host integration; extended `WP 5.0D` with `ITempestHost.Services` availability, resolution, and Discovery/Registration/Lifecycle non-exposure tests |
| `Samples/` | 5 | 39 | `ClockModule`/`ClockLifecycleObserverModule` pipeline and event integration (WP 4.3, WP 4.4E); `NavigationSampleModule` and companions, module/host/plugin integration (WP 5.0B) |
| `Shell/` | 2 | 31 | `TempestShell`, `PlaceholderPage` — composition, Navigation/Content rendering, page selection, unknown-page placeholder, real Host/sample-module integration, full interactive sessions (WP 5.0D) |
| `Versioning/` | 2 | 17 | Platform Version infrastructure (WP 4.2A) |

**Total: 60 test files, 422 `[Fact]`/`[Theory]` attribute occurrences.**

## Reconciling Attribute Count Against Executed Test Count

`dotnet test` reports **448** executed tests, 26 more than the 422 raw
`[Fact]`/`[Theory]` attribute occurrences above (both new `WP 5.0S`
regression tests are `[Fact]` methods, one occurrence each, so the gap
is unchanged from the `WP 5.0D` baseline). This difference is
**Verified** to be `[Theory]` methods with multiple `[InlineData]` rows
executing as multiple tests at runtime from a single attribute occurrence
— for example, the Plugin Manifest test suite's missing-required-field
theory (one `[Theory]` attribute, five `InlineData` rows, one per
required field), `NavigationItemTests`' own two `[Theory]` methods
(invalid `Id`/`Title`, three `InlineData` rows each), and, as of
`WP 5.0D`, `PlaceholderPageTests`' own two `[Theory]` methods (invalid
title/message, three `InlineData` rows each) and
`TempestShellTests.HandleInputAsync_InvalidSelection_ReportsInvalid_AndReturnsTrue`
(one `[Theory]` attribute, four `InlineData` rows). No discrepancy or
missing test was found; the two counts measure different things (source
attributes vs. runtime-executed cases) and both are reported here to
avoid the false impression that they should match.

## Historical Test Count Progression (Verified from CHANGELOG.md / Testing.md / Retrospectives)

| Milestone | Test Count |
|---|---|
| v0.3.0 (Runtime Foundation baseline) | 164 |
| WP 4.1 (Module SDK) | Unknown exact figure at this point — not stated as a standalone total in any reviewed document |
| WP 4.2 (Plugin Manifest implementation) | 242 (215 pre-existing + 27 new) |
| WP 4.3 (Sample Module implementation) | Unknown exact running total — 18 new tests added, base not restated in that retrospective |
| WP 4.4D (Event Bus implementation) | 302 (278 pre-existing + 24 new) |
| WP 4.5 (Background Services implementation) | 355 (313 pre-existing + 42 new) |
| WP 4.5A (Governance Register Baseline) | 355 — re-verified directly, 0 failures, no code change |
| WP 5.0B (Navigation Framework implementation) | 400 (355 pre-existing + 45 new) |
| WP 5.0D (Shell & Composition Framework implementation) | 446 (400 pre-existing + 46 new) |
| WP 5.0S (Platform Security Baseline Audit) | 448 (446 pre-existing + 2 new) |
| **Current (WP 5.0S)** | **448** — re-verified directly, 0 failures |

Gaps in this progression are recorded as **Unknown**, not interpolated —
several retrospectives report only the tests *they* added, not a running
total, and reconstructing every intermediate total would require
re-running `dotnet test` against 43 individual historical commits, out of
this Work Package's own scope.

## Cross-Reference Check

The 448 figure above is cross-checked directly against
`Validation Register.md`'s own Test Gate row (also 448, from the same
`dotnet test` run performed as part of this Work Package) — consistent,
no discrepancy.
