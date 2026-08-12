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
| **Last Reviewed** | 2026-08-12 (WP 12.3B, Fault-Injection Validation Framework Implementation) — counts re-verified directly via a fresh `dotnet test` run against `tests/Tempest.Core.Tests/`: 2031/2031 passing (Debug), +5 net new since this field's own last recorded total (2026): 2 new (`FaultInjectionModuleDiscoveryTests.cs`, new file, `Modules/`) + 3 new (`ReflectionFrameworkDiscoveryServiceTests.cs`, extended in place, `Modules/`) proving the `IFaultInjectionModule` default-exclusion filter (ADR-0102); zero test methods removed — `NavigationSampleModuleIntegrationTests.cs`/`ClockModuleDiscoveryTests.cs` were edited in place (updated assertions/counts for the moved module), not shrunk. This register's own per-directory breakdown table's broader staleness (disclosed below, unchanged since `WP 5.3`) is out of this Work Package's own scope, per the same precedent. Previously reviewed 2026-08-11 (WP 11.3B, Presentation Strategy Implementation) — **narrow correction only, not a full re-derivation**: the `Shell/` row in the already-disclosed-stale (since `WP 5.3`) per-directory breakdown table below marked retired, reflecting `TempestShell`/`PlaceholderPage`'s removal (`ADR-0101`). The table's own broader staleness (frozen at a `WP 5.3`-era snapshot while real executed-test counts have since reached 2,228 — confirmed by a real `dotnet test` run this Work Package) is unchanged and remains out of this Work Package's own scope, per the prior disclosure below. Previously reviewed 2026-08-05 (WP 9.1B, Development Baseline Consolidation) — counts re-verified directly via a fresh `dotnet test` run against the consolidated working tree; 1808/1808. **Disclosed correction**: this field was not updated by either `WP 9.0B` or `WP 9.1A` at the time — a genuine omission during those two Work Packages, found and corrected here as part of this Work Package's own governance-consistency verification, not a new staleness introduced by `WP 9.1B` itself. `WP 9.0B` added 43 tests (1695 → 1738): 16 `BillOfMaterialsTests.cs`, 4 more `StructuralMutationTests.cs` (`ReviseAsync` regression), 10 for three new commands, 7 for extended node/facet providers, 6 more `MechanicalWorkspaceIntegrationTests.cs`. `WP 9.1A` added 70 tests (1738 → 1808): 32 Domain (`RequirementsLifecycleExtensionsTests.cs`, `RequirementValidationServiceTests.cs`, both new), 12 `SelectionServiceTests.cs` (multi-selection), 10 `EngineeringCockpitTests.cs` (Requirements KPIs), 16 `RequirementsWorkspaceIntegrationTests.cs` (new). **Disclosed, not fixed**: this register's own per-directory breakdown table below has not been re-derived since `WP 5.3` (552 tests) — a large, genuine staleness spanning `v0.6.0`/`v0.7.0`/`v0.8.0`/`v0.9.0` in their entirety, out of this Work Package's own scope to backfill; only this field's own top-line total is corrected here. Recommended as a future governance health-check candidate (`FCR-0005`). Previously reviewed 2026-08-05 (WP 9.0A, Mechanical Product Structure) — counts re-verified directly via four fresh `dotnet test` runs (two Debug, two Release, both via `src/TempestOS.slnx`); 1695/1695, 64 new tests: `EngineeringDomain/` extended with `StructuralMutationTests.cs` (13); `Workspace/` extended with `MechanicalNodeProviderAndFacetsTests.cs` (17), `MechanicalCommandsTests.cs` (21), `MechanicalWorkspaceIntegrationTests.cs` (7), and 6 new `WorkspaceManagerTests.cs` cases (`RegisterFacetProvider`); `Samples/` unaffected in count (`ClockModuleDiscoveryTests.cs` updated in place, module count 22→24, no new test method). |
| **Related Documents** | `docs/releases/v0.4.0/Testing.md`; `Validation Register.md`; `Repository Metrics Register.md`. |
| **Related ADRs** | None directly. |
| **Related Academy Articles** | `docs/academy/06 Engineering Standards/02-testing-strategy.md`. |
| **Coverage Status** | Complete. |

---

## Entries

| Directory | Files | `[Fact]`/`[Theory]` Attributes | Covers |
|---|---|---|---|
| `BackgroundServices/` | 4 | 37 | Hosted service discovery, manager, fixtures (WP 4.5) |
| `Commands/` | 5 | 54 | `ICommand` contract (WP 4.0); `CommandDispatcher`/`CommandRegistry`/`CommandHandlerTable` — registration, duplicate rejection, dispatch, failure propagation, cancellation, logging, thread safety, DI lifetime, shared-state sharing (WP 5.1B) |
| `Configuration/` | 4 | 31 | Configuration Framework (WP 2.5) |
| `DependencyInjection/` | 3 | 22 | Custom DI container (WP 2.4) |
| `Diagnostics/` | 1 | 9 | `IDiagnosticsProvider`/`DiagnosticsProvider` — live accessor projection, empty-before-attached/populated-after-attached, construction validation (WP 5.2) |
| `Events/` | 4 | 27 | Event Bus (WP 4.0 contracts, WP 4.4D bus) |
| `Logging/` | 10 | 53 | Logging & Diagnostics Framework (WP 2.6); extended `WP 5.2` with `CompositeLogSinkTests.cs` — fan-out, per-child failure isolation, `Logger` integration |
| `Modules/` | 13 | 74 | Discovery, Registration, Lifecycle, Module SDK, `ModuleMetadataAttribute` (WP 2.1–2.3, WP 4.1, WP 4.4A/B); extended `WP 5.3` with 2 tests proving a module with no `[ModuleMetadata]` and no parameterless constructor now fails with a clear `ModuleDiscoveryException`, not a raw `MissingMethodException` |
| `Navigation/` | 2 | 31 | `NavigationItem`, `NavigationService` — registration, ordering, hierarchy, visibility, events, DI (WP 5.0B) |
| `Plugins/` | 6 | 21 | Plugin manifest, discovery, loading (WP 4.2); extended `WP 5.0B` with a Navigation-registering dynamic plugin assembly builder; extended `WP 5.0S` with 2 `AssemblyFileName` path-containment regression tests |
| `Runtime/` | 5 | 50 | `TempestHost`, `TempestHostBuilder`, plugin/hosted-service Host integration; extended `WP 5.0D` with `ITempestHost.Services` availability, resolution, and Discovery/Registration/Lifecycle non-exposure tests |
| `Samples/` | 7 | 55 | `ClockModule`/`ClockLifecycleObserverModule` pipeline and event integration (WP 4.3, WP 4.4E); `NavigationSampleModule` and companions, module/host/plugin integration (WP 5.0B); `CommandSampleModule`, module/host/plugin integration and Navigation-integration proof (WP 5.1B); `DiagnosticsSampleModule`, module/host/plugin integration and the disclosed "zero hosted services during Initialise" finding (WP 5.2) |
| `Shell/` | 0 — **retired, `WP 11.3B`** | 0 | Formerly `TempestShell`, `PlaceholderPage` — composition, Navigation/Content rendering, page selection, unknown-page placeholder, real Host/sample-module integration, full interactive sessions (WP 5.0D). Both the production classes and this entire test directory were retired `WP 11.3B` (`ADR-0101`) — `TempestShell` had been unreachable from any running entry point since `ADR-0068` (`WP 8.1A`, `v0.8.0`), confirmed dead before removal. |
| `Templates/` | 3 | 6 | The `dotnet new tempest-module` template (`WP 5.3`) — `template.json` manifest validity; the template's own file content, substituted, built with the real compiler, and proven discoverable by the real, unmodified `ReflectionFrameworkDiscoveryService` |
| `Versioning/` | 2 | 17 | Platform Version infrastructure (WP 4.2A) |

**Total: 71 test files, 518 `[Fact]`/`[Theory]` attribute occurrences.**

## Reconciling Attribute Count Against Executed Test Count

`dotnet test` reports **552** executed tests, 34 more than the 518 raw
`[Fact]`/`[Theory]` attribute occurrences above. This difference is
**Verified** to be `[Theory]` methods with multiple `[InlineData]` rows
executing as multiple tests at runtime from a single attribute occurrence
— for example, the Plugin Manifest test suite's missing-required-field
theory (one `[Theory]` attribute, five `InlineData` rows, one per
required field), `NavigationItemTests`' own two `[Theory]` methods
(invalid `Id`/`Title`, three `InlineData` rows each), `PlaceholderPageTests`'
own two `[Theory]` methods (invalid title/message, three `InlineData`
rows each), `TempestShellTests.HandleInputAsync_InvalidSelection_ReportsInvalid_AndReturnsTrue`
(one `[Theory]` attribute, four `InlineData` rows), `CommandDescriptorAndResultTests`'
own three `[Theory]` methods (invalid `Id`/`DisplayName`/failure message,
three `InlineData` rows each, `WP 5.1B`), and, as of `WP 5.3`,
`ModuleTemplateManifestTests.TemplateManifest_DeclaresEachDocumentedSymbol_WithItsDefaultValue`
(one `[Theory]` attribute, three `InlineData` rows, one per template
symbol — a net new gap of 2). No discrepancy or missing test was found;
the two counts measure different things (source attributes vs.
runtime-executed cases) and both are reported here to avoid the false
impression that they should match.

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
| WP 5.1B (Command Framework Implementation) | 514 (448 pre-existing + 66 new) |
| WP 5.2 (Diagnostics Improvements) | 542 (514 pre-existing + 28 new) |
| WP 5.3 (Developer Experience Improvements) | 552 (542 pre-existing + 10 new) |
| **Current (WP 5.3)** | **552** — re-verified directly, 0 failures |

Gaps in this progression are recorded as **Unknown**, not interpolated —
several retrospectives report only the tests *they* added, not a running
total, and reconstructing every intermediate total would require
re-running `dotnet test` against 43 individual historical commits, out of
this Work Package's own scope.

## Cross-Reference Check

The 552 figure above is cross-checked directly against
`Validation Register.md`'s own Test Gate row (also 552, from the same
`dotnet test` run performed as part of this Work Package) — consistent,
no discrepancy.
