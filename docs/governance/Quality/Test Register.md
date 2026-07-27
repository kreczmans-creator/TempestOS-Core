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
| **Last Reviewed** | 2026-07-27 (WP 5.0B) — counts re-verified directly via a fresh `dotnet test` run; 400/400, 45 new tests added (`Navigation/`, plus one new file in `Samples/`). |
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
| `Plugins/` | 6 | 19 | Plugin manifest, discovery, loading (WP 4.2); extended `WP 5.0B` with a Navigation-registering dynamic plugin assembly builder |
| `Runtime/` | 5 | 42 | `TempestHost`, `TempestHostBuilder`, plugin/hosted-service Host integration |
| `Samples/` | 5 | 39 | `ClockModule`/`ClockLifecycleObserverModule` pipeline and event integration (WP 4.3, WP 4.4E); `NavigationSampleModule` and companions, module/host/plugin integration (WP 5.0B) |
| `Versioning/` | 2 | 17 | Platform Version infrastructure (WP 4.2A) |

**Total: 58 test files, 381 `[Fact]`/`[Theory]` attribute occurrences.**

## Reconciling Attribute Count Against Executed Test Count

`dotnet test` reports **400** executed tests, 19 more than the 381 raw
`[Fact]`/`[Theory]` attribute occurrences above. This difference is
**Verified** to be `[Theory]` methods with multiple `[InlineData]` rows
executing as multiple tests at runtime from a single attribute occurrence
— for example, the Plugin Manifest test suite's missing-required-field
theory (one `[Theory]` attribute, five `InlineData` rows, one per
required field), and, as of `WP 5.0B`, `NavigationItemTests`' own two
`[Theory]` methods (invalid `Id`/`Title`, three `InlineData` rows each).
No discrepancy or missing test was found; the two counts measure
different things (source attributes vs. runtime-executed cases) and both
are reported here to avoid the false impression that they should match.

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
| **Current (WP 5.0B)** | **400** — re-verified directly, 0 failures |

Gaps in this progression are recorded as **Unknown**, not interpolated —
several retrospectives report only the tests *they* added, not a running
total, and reconstructing every intermediate total would require
re-running `dotnet test` against 43 individual historical commits, out of
this Work Package's own scope.

## Cross-Reference Check

The 400 figure above is cross-checked directly against
`Validation Register.md`'s own Test Gate row (also 400, from the same
`dotnet test` run performed as part of this Work Package) — consistent,
no discrepancy.
