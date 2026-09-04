# Repository Metrics Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Repository Metrics Register |
| **Purpose** | A single, dated snapshot of the repository's own size and shape — file counts, line counts, commit counts — so future baselines can measure growth against a known point rather than re-deriving it from scratch. |
| **Scope** | `src/`, `tests/`, `docs/`, and git history, as they stood at time of review. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Direct repository inspection (`find`, `wc`, `git log`) performed as part of this Work Package. |
| **Review Frequency** | Re-measured at each Governance Baseline review, or on request. |
| **Last Reviewed** | 2026-09-04 (WP 16.2A, Register and Status Currency) — new snapshot added, first since `WP 5.4` (eleven releases stale). Every count independently re-derived directly from the file system and git history at this Work Package's own base commit; see the new "Snapshot: 2026-09-04" section below. See `docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md`. Previously reviewed 2026-07-28 (WP 5.4, v0.5.0 Release Candidate & Engineering Sign-Off) — every count below independently re-derived from the file system directly, not carried forward from the previous snapshot's own arithmetic; two genuine, silent undercounts found and corrected (Exception Register total; Academy article/Work-Package-retrospective counts). |
| **Related Documents** | `Test Register.md`; `Namespace Register.md`; `Engineering Evolution Register.md`. |
| **Related ADRs** | None directly. |
| **Related Academy Articles** | None directly — this is a raw metrics snapshot, not a teaching document. |
| **Coverage Status** | Complete (as a point-in-time snapshot; by nature, it goes stale the moment the repository changes further — see Review Frequency). |

---

## Snapshot: 2026-07-25 (WP 4.5A)

| Metric | Value |
|---|---|
| Total commits | 48 |
| Claude-authored commits (carry `Co-Authored-By: Claude` trailer) | 43 |
| Pre-Claude commits | 5 |
| First Claude-authored commit | `7514b9d`, 2026-07-21 |
| Most recent commit (at time of review) | `c460aaf`, 2026-07-25 |
| `src/` `.cs` files (excluding `obj`/`bin`) | 106 |
| `src/` `.cs` lines (excluding `obj`/`bin`) | 6,603 |
| `tests/` `.cs` files (excluding `obj`/`bin`) | 55 |
| `tests/` `.cs` lines (excluding `obj`/`bin`) | 7,310 |
| `docs/` `.md` files | 134 |
| `docs/` `.md` lines | 24,215 |
| Projects in solution | 4 (`Tempest.Core`, `Tempest.App`, `Tempest.Samples`, `Tempest.Core.Tests`) |
| Namespaces under `src/` | 14 declared + 1 global (see `Namespace Register.md`) |
| ADRs | 30 (`ADR-0001`–`ADR-0030`), all Accepted |
| Rejected Designs entries | 29 (`RD-0001`–`RD-0029`) |
| Architecture documents (`docs/architecture/`) | 16 |
| Academy articles (`docs/academy/`, all subfolders) | 61 |
| Public interfaces (`src/Tempest.Core/`) | 26 |
| Custom exception types | 22 |
| Production modules | 2 (`ClockModule`, `ClockLifecycleObserverModule`) |
| Production event types | 1 (`ClockModuleLifecycleEvent`) |
| Production hosted services | 0 (infrastructure only — see `Hosted Services Register.md`) |
| Production plugins | 0 (`src/Plugins/` empty — see `Plugin Register.md`) |
| Test files | 55 |
| `[Fact]`/`[Theory]` attribute occurrences | 340 |
| Executed tests (`dotnet test`) | 355, 0 failures |
| Build warnings | 0 |
| Build errors | 0 |
| Current `VERSION` | 0.3.0 (v0.4.0 not yet tagged) |
| Current branch | `feature/v0.4.0-platform-services` |

## Snapshot: 2026-07-25 (WP 4.5B — Platform Foundation Closeout)

Documentation and governance only — no production or test code changed
since the WP 4.5A snapshot above; every `src/`/`tests/` figure is
unchanged. Shown here are only the metrics this Work Package's own
additions moved.

| Metric | WP 4.5A | WP 4.5B (current) |
|---|---|---|
| Root `.md` files | 2 (`README.md`, `LICENSE.md`) | 3 (adds `PROJECT_STATUS.md`) |
| `docs/` `.md` files | 134 | 152 |
| Academy articles (`docs/academy/`, all subfolders) | 61 | 63 (adds `Contributor Learning Path.md`, `06 Engineering Standards/Engineering Lifecycle.md`) |
| Governance documents (`docs/governance/`, all subfolders) | 31 | 32 (adds `Future Work Package Guidelines.md`) |
| `docs/releases/` documents | 6 | 7 (adds `Platform Foundation Completion Report.md`) |
| Engineering Standards documents | 4 | 5 (adds `Engineering Lifecycle.md`) |
| Executed tests (`dotnet test`) | 355, 0 failures | 355, 0 failures (unchanged) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-27 (v0.4.0 Release Engineering — "Platform Foundation")

Release-preparation changes only — `VERSION`, `CHANGELOG.md`, release
documentation, and governance-register updates; no production or test
code changed since the WP 4.5B snapshot above.

| Metric | WP 4.5B | v0.4.0 (current) |
|---|---|---|
| `docs/releases/` `.md` files | 6 | 13 (adds `v0.4.0.md`, `v0.4.0/Release Notes.md`) |
| `docs/` `.md` files | 152 | 154 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — this release updates existing registers rather than adding new ones) |
| Root `VERSION` | `0.3.0` | **`0.4.0`** |
| Total commits | 48 | 50 (45 Claude-authored, 5 pre-Claude) |
| Commits since `v0.3.0` tag | — | 23 |
| Executed tests (`dotnet test`) | 355, 0 failures | 355, 0 failures (unchanged) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-27 (WP 5.0A — Navigation Framework Architecture)

Architecture-only Work Package — no production or test code changed
since the v0.4.0 Release Engineering snapshot above; every `src/`/
`tests/` figure is unchanged. Shown here are only the metrics this Work
Package's own additions moved.

| Metric | v0.4.0 Release Engineering | WP 5.0A (current) |
|---|---|---|
| `src/` `.cs` files / lines | 106 / 6,603 | 106 / 6,603 (unchanged) |
| `tests/` `.cs` files / lines | 55 / 7,310 | 55 / 7,310 (unchanged) |
| ADRs | 30 (`ADR-0001`–`ADR-0030`) | 32 (`ADR-0001`–`ADR-0032`, adds `ADR-0031`, `ADR-0032`) |
| Rejected Designs entries | 29 (`RD-0001`–`RD-0029`) | 33 (`RD-0001`–`RD-0033`, adds `RD-0030`–`RD-0033`) |
| Architecture documents (`docs/architecture/`) | 16 | 17 (adds `Navigation Framework Architecture.md`) |
| Academy articles (`docs/academy/`, all subfolders) | 63 | 65 (adds `09-navigation-architecture.md`, `WP5.0A-navigation-framework-architecture.md`) |
| `docs/releases/` `.md` files | 13 | 15 (adds `docs/releases/v0.5.0/ReleasePlan.md`, `WorkPackages.md`) |
| `docs/` `.md` files | 154 | 161 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — this Work Package updates existing registers rather than adding new ones) |
| Root `VERSION` | `0.4.0` | `0.4.0` (unchanged — v0.5.0 not yet tagged) |
| Current branch | `main` (post-merge) | `feature/v0.5.0-developer-experience` |
| Total commits | 50 | 52 (47 Claude-authored, 5 pre-Claude) |
| Commits since `v0.4.0` tag | — | 0 (this Work Package not yet committed at time of this snapshot) |
| Executed tests (`dotnet test`) | 355, 0 failures | 355, 0 failures (unchanged — architecture-only, by design) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-27 (WP 5.0B — Navigation Framework Implementation)

The first Work Package since `v0.4.0` Release Engineering to change
`src/`/`tests/` — implements `Tempest.Core.Navigation`, three new
`Tempest.Samples` reference modules, and 45 new tests.

| Metric | WP 5.0A | WP 5.0B (current) |
|---|---|---|
| `src/` `.cs` files / lines | 106 / 6,603 | 116 / 7,231 (adds 7 `Tempest.Core.Navigation` files, 3 `Tempest.Samples` files) |
| `tests/` `.cs` files / lines | 55 / 7,310 | 58 / 8,163 (adds 2 `Navigation/` test files, 1 `Samples/` test file; extends `DynamicPluginAssemblyBuilder.cs`) |
| Executed tests (`dotnet test`) | 355, 0 failures | **400, 0 failures** (45 new) |
| Namespaces under `src/` | 15 declared + 1 global | 16 declared + 1 global (adds `Tempest.Core.Navigation`) |
| Public interfaces (`src/Tempest.Core/`) | 26 | 27 (adds `INavigationProvider`) |
| Custom exception types | 22 | 25 (adds `NavigationException` and 2 subtypes) |
| Production modules | 2 (`ClockModule`, `ClockLifecycleObserverModule`) | 5 (adds `NavigationSampleModule`, `SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`) |
| Production event types | 1 (`ClockModuleLifecycleEvent`) | 2 (adds `NavigationRequestedEvent`) |
| Platform services (Register total) | 16 catalogued, 11 Implemented | 16 catalogued, **12 Implemented** (Navigation moves from Designed to Implemented) |
| Architecture documents (`docs/architecture/`) | 17 | 17 (unchanged — no new document; `Navigation Framework Architecture.md`'s own status field updated in place) |
| Academy articles (`docs/academy/`, all subfolders) | 65 | 66 (adds `WP5.0B-navigation-framework-implementation.md`) |
| `docs/` `.md` files | 161 | 162 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — updates existing registers rather than adding new ones) |
| Total commits | 52 | 53 (before this Work Package's own commit) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-27 (WP 5.0C — Shell & Composition Framework Architecture)

Architecture-only Work Package — no production or test code changed
since the WP 5.0B snapshot above; every `src/`/`tests/` figure is
unchanged. Shown here are only the metrics this Work Package's own
additions moved.

| Metric | WP 5.0B | WP 5.0C (current) |
|---|---|---|
| `src/` `.cs` files / lines | 116 / 7,231 | 116 / 7,231 (unchanged) |
| `tests/` `.cs` files / lines | 58 / 8,163 | 58 / 8,163 (unchanged) |
| ADRs | 32 (`ADR-0001`–`ADR-0032`) | 35 (`ADR-0001`–`ADR-0035`, adds `ADR-0033`–`ADR-0035`) |
| Rejected Designs entries | 33 (`RD-0001`–`RD-0033`) | 37 (`RD-0001`–`RD-0037`, adds `RD-0034`–`RD-0037`) |
| Architecture documents (`docs/architecture/`) | 17 | 18 (adds `Shell & Composition Framework Architecture.md`) |
| Academy articles (`docs/academy/`, all subfolders) | 66 | 68 (adds `10-shell-and-application-composition.md`, `WP5.0C-shell-and-composition-framework-architecture.md`) |
| `docs/` `.md` files | 162 | 168 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — this Work Package updates existing registers rather than adding new ones) |
| Root `VERSION` | `0.4.0` | `0.4.0` (unchanged — v0.5.0 not yet tagged) |
| Current branch | `feature/v0.5.0-developer-experience` | `feature/v0.5.0-developer-experience` (unchanged) |
| Total commits | 53 | 54 (before this Work Package's own commit) |
| Executed tests (`dotnet test`) | 400, 0 failures | 400, 0 failures (unchanged — architecture-only, by design) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-27 (WP 5.0D — Shell & Composition Framework Implementation)

The first implementation Work Package since `WP 5.0B` to change
`src/`/`tests/` — implements `ITempestHost.Services` and
`Tempest.App.Shell` (`TempestShell`, `IPage`, `PlaceholderPage`), and
rewrites `Program.cs` as the real entry point.

| Metric | WP 5.0C | WP 5.0D (current) |
|---|---|---|
| `src/` `.cs` files / lines | 116 / 7,231 | 119 / 7,564 (adds 3 `Tempest.App.Shell` files; `Program.cs` rewritten in place) |
| `tests/` `.cs` files / lines | 58 / 8,163 | 60 / 8,384 (adds `Shell/TempestShellTests.cs`, `PlaceholderPageTests.cs`; extends `Runtime/TempestHostTests.cs`) |
| Executed tests (`dotnet test`) | 400, 0 failures | **446, 0 failures** (46 new) |
| Namespaces under `src/` | 16 declared + 1 global | 17 declared + 1 global (adds `Tempest.App.Shell`) |
| Project references | 4 projects, 4 edges | 4 projects, 6 edges (adds `Tempest.App` → `Tempest.Samples`; `Tempest.Core.Tests` → `Tempest.App`) |
| Architecture documents (`docs/architecture/`) | 18 | 18 (unchanged — `Shell & Composition Framework Architecture.md`'s own status field updated in place, no new document) |
| Academy articles (`docs/academy/`, all subfolders) | 68 | 69 (adds `WP5.0D-shell-and-composition-framework-implementation.md`) |
| `docs/` `.md` files | 168 | 169 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — updates existing registers rather than adding new ones) |
| Total commits | 54 | 55 (before this Work Package's own commit) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

**A genuine application behaviour change, not merely a code change:**
running the built application (`dotnet run` against `Tempest.App`) now
starts a real `TempestHost`, discovers all five `Tempest.Samples`
modules, and presents a real, interactive Navigation/Content region —
confirmed by direct execution, not merely by the test suite.

## Snapshot: 2026-07-28 (WP 5.0S — Platform Security Baseline Audit)

The first dedicated security audit Work Package — no architecture
redesigned, no new feature built. One isolated, non-breaking fix applied
(`PluginManifestDiscoveryService`'s `AssemblyFileName` path-containment
check) with two regression tests added; four new standing documents
created under a new top-level `docs/security/` tree.

| Metric | WP 5.0D | WP 5.0S (current) |
|---|---|---|
| `src/` `.cs` files / lines | 119 / 7,564 | 119 / 7,589 (unchanged file count; `PluginManifestDiscoveryService.cs` extended in place with the path-containment check) |
| `tests/` `.cs` files / lines | 60 / 8,384 | 60 / 8,867 (unchanged file count; `PluginManifestDiscoveryServiceTests.cs` extended in place with 2 new regression tests) |
| Executed tests (`dotnet test`) | 446, 0 failures | **448, 0 failures** (2 new) |
| ADRs | 35 (`ADR-0001`–`ADR-0035`) | 35 (unchanged — no architecture redesigned, per this Work Package's own brief) |
| Rejected Designs entries | 37 (`RD-0001`–`RD-0037`) | 37 (unchanged) |
| Architecture documents (`docs/architecture/`) | 18 | 18 (unchanged — this Work Package's new documents live under `docs/security/`, a new top-level tree, not `docs/architecture/`) |
| `docs/security/` documents (new top-level tree) | 0 | 4 (`Threat Model.md`, `Security Principles.md`, `Platform Security Review v0.5.0.md`, `Security Roadmap.md`) |
| Academy articles (`docs/academy/`, all subfolders) | 69 | 70 (adds `WP5.0S-platform-security-baseline-audit.md`) |
| `docs/` `.md` files | 169 | 174 (adds 4 `docs/security/` documents, 1 Academy retrospective) |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — updates existing registers rather than adding new ones) |
| Technical Debt Register open items | 5 Open, 1 Partially resolved, 2 Resolved (8 total) | 7 Open, 1 Partially resolved, 2 Resolved (10 total — adds `TD-09`, `TD-10`) |
| Decision Register entries | 16 | 17 (adds `D-017`) |
| Total commits | 56 | 57 (before this Work Package's own commit) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

## Snapshot: 2026-07-28 (WP 5.1A — Command Framework Architecture)

Architecture-only Work Package — no production or test code changed
since the `WP 5.0S` snapshot above; every `src/`/`tests/` figure is
unchanged. Shown here are only the metrics this Work Package's own
additions moved.

| Metric | WP 5.0S | WP 5.1A (current) |
|---|---|---|
| `src/` `.cs` files / lines | 119 / 7,589 | 119 / 7,589 (unchanged — architecture only) |
| `tests/` `.cs` files / lines | 60 / 8,867 | 60 / 8,867 (unchanged — no tests added) |
| Executed tests (`dotnet test`) | 448, 0 failures | 448, 0 failures (unchanged) |
| ADRs | 35 (`ADR-0001`–`ADR-0035`) | 38 (`ADR-0001`–`ADR-0038`, adds `ADR-0036`–`ADR-0038`) |
| Rejected Designs entries | 37 (`RD-0001`–`RD-0037`) | 41 (`RD-0001`–`RD-0041`, adds `RD-0038`–`RD-0041`) |
| Architecture documents (`docs/architecture/`) | 18 | 19 (adds `Command Framework Architecture.md`) |
| `docs/security/` documents | 4 | 4 (unchanged) |
| Academy articles (`docs/academy/`, all subfolders) | 70 | 72 (adds `11-command-framework.md`, `WP5.1A-command-framework-architecture.md`) |
| `docs/` `.md` files | 174 | 180 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — updates existing registers rather than adding new ones) |
| Technical Debt Register open items | 7 Open, 1 Partially resolved, 2 Resolved (10 total) | 8 Open, 1 Partially resolved, 2 Resolved (11 total — adds `TD-11`; `TD-09`'s own scope widened, not re-counted) |
| Decision Register entries | 17 | 18 (adds `D-018`) |
| Risk Register entries | 10 risks, 4 Retired | 10 risks, 5 Retired (R3 retired — see `Risks.md`) |
| Total commits | 57 | 58 (before this Work Package's own commit) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

**No production behaviour change.** `dotnet build`/`dotnet test` were
re-run to confirm this directly: identical 448/448 result to the
`WP 5.0S` snapshot, since no `src/`/`tests/` file was touched.

## Snapshot: 2026-07-28 (WP 5.1B — Command Framework Implementation)

The first implementation Work Package since `WP 5.0D` to change
`src/`/`tests/` — implements `ICommandDispatcher`/`CommandDispatcher`,
`ICommandRegistry`/`CommandRegistry`, `CommandDescriptor`, `CommandResult`,
`CommandHandlerTable`, and the `CommandException` hierarchy
(`Tempest.Core.Commands`), and `CommandSampleModule` plus two reference
commands (`Tempest.Samples`).

| Metric | WP 5.1A | WP 5.1B (current) |
|---|---|---|
| `src/` `.cs` files / lines | 119 / 7,589 | 137 / 8,554 (adds 13 `Tempest.Core.Commands` files, 5 `Tempest.Samples` files) |
| `tests/` `.cs` files / lines | 60 / 8,867 | 65 / 10,078 (adds `Commands/CommandFixtures.cs`, `CommandDispatcherTests.cs`, `CommandRegistryTests.cs`, `CommandDescriptorAndResultTests.cs`; `Samples/CommandSampleModuleIntegrationTests.cs`; extends `DynamicPluginAssemblyBuilder.cs`) |
| Executed tests (`dotnet test`) | 448, 0 failures | **514, 0 failures** (66 new) |
| Namespaces under `src/` | 17 declared + 1 global | 17 declared + 1 global (unchanged — extends the existing `Tempest.Core.Commands` namespace, introduced `WP 4.0`) |
| ADRs | 38 (`ADR-0001`–`ADR-0038`) | 38 (unchanged — implementation realises `WP 5.1A`'s already-Accepted ADRs, no new ADR) |
| Rejected Designs entries | 41 (`RD-0001`–`RD-0041`) | 41 (unchanged) |
| Architecture documents (`docs/architecture/`) | 19 | 19 (unchanged — `Command Framework Architecture.md`'s own status updated in place, plus a new Implementation Note and Security Review Update section, no new document) |
| Platform services (Register total) | 16 catalogued, 12 Implemented + 1 Architected | 16 catalogued, **13 Implemented** (Command Framework moves from Architected to Implemented) |
| Public interfaces (`src/Tempest.Core/`) | 27 | 30 (adds `ICommandDispatcher`, `ICommandHandler<T>`, `ICommandRegistry`) |
| Custom exception types | 25 | 30 (adds `CommandException` and four subtypes) |
| Production modules | 5 | 6 (adds `CommandSampleModule`) |
| Academy articles (`docs/academy/`, all subfolders) | 72 | 73 (adds `WP5.1B-command-framework-implementation.md`) |
| `docs/` `.md` files | 180 | 181 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — updates existing registers rather than adding new ones) |
| Technical Debt Register open items | 8 Open, 1 Partially resolved, 2 Resolved (11 total) | 8 Open, 1 Partially resolved, 2 Resolved (11 total — unchanged; `TD-09`/`TD-11` confirmed present in the implementation, not newly introduced) |
| Total commits | 58 | 59 (before this Work Package's own commit) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

**A genuine application behaviour change, not merely a code change:**
running the built application confirms `CommandSampleModule` discovered,
registered, initialised, and disposed cleanly through the real
`TempestHost`, and `ICommandDispatcher`/`ICommandRegistry` registered
during the existing Platform Services Registered phase — confirmed by
direct execution, not merely by the test suite.

## Snapshot: 2026-07-28 (WP 5.2 — Diagnostics Improvements)

The first implementation Work Package since `WP 5.1B` to change
`src/`/`tests/` — implements `CompositeLogSink` (`Tempest.Core.Logging`),
`IDiagnosticsProvider`/`DiagnosticsProvider` (the new
`Tempest.Core.Diagnostics` namespace), and `DiagnosticsSampleModule` plus
one reference command (`Tempest.Samples`). Also resolves `TD-02` and
reassesses/re-scopes `TD-01` forward again.

| Metric | WP 5.1B | WP 5.2 (current) |
|---|---|---|
| `src/` `.cs` files / lines | 137 / 8,554 | 143 / 8,986 (adds 3 `Tempest.Core.Diagnostics`/`Logging` files, 3 `Tempest.Samples` files) |
| `tests/` `.cs` files / lines | 65 / 10,078 | 68 / 10,628 (adds `Logging/CompositeLogSinkTests.cs`, `Diagnostics/DiagnosticsProviderTests.cs`, `Samples/DiagnosticsSampleModuleIntegrationTests.cs`) |
| Executed tests (`dotnet test`) | 514, 0 failures | **542, 0 failures** (28 new) |
| Namespaces under `src/` | 17 declared + 1 global | 18 declared + 1 global (adds `Tempest.Core.Diagnostics`) |
| ADRs | 38 (`ADR-0001`–`ADR-0038`) | 39 (`ADR-0001`–`ADR-0039`, adds `ADR-0039`) |
| Rejected Designs entries | 41 (`RD-0001`–`RD-0041`) | 44 (`RD-0001`–`RD-0044`, adds `RD-0042`–`RD-0044`) |
| Architecture documents (`docs/architecture/`) | 19 | 20 (adds `Diagnostics Architecture.md`; `Command Framework Architecture.md`'s own stale "implementation pending" marker also corrected in place, see this Work Package's own repository review) |
| Platform services (Register total) | 16 catalogued, 13 Implemented | 17 catalogued, **14 Implemented** (Diagnostics moves from not-catalogued to Implemented) |
| Public interfaces (`src/Tempest.Core/`) | 30 | 31 (adds `IDiagnosticsProvider`) |
| Custom exception types | 30 | 30 (unchanged — no new exception type introduced; see `Exception Register.md`'s own "A Note on Diagnostics") |
| Production modules | 6 | 7 (adds `DiagnosticsSampleModule`) |
| Academy articles (`docs/academy/`, all subfolders) | 73 | 75 (adds `12-diagnostics-and-composite-logging.md`, `WP5.2-diagnostics-improvements.md`) |
| `docs/` `.md` files | 181 | 185 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — updates existing registers rather than adding new ones) |
| Technical Debt Register open items | 8 Open, 1 Partially resolved, 2 Resolved (11 total) | 7 Open, 1 Partially resolved, **3 Resolved** (11 total — `TD-02` resolved; `TD-01` reassessed, remains Open) |
| Decision Register entries | 18 | 20 (adds `D-019`, `D-020`) |
| Total commits | 59 | 60 (before this Work Package's own commit) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

**A genuine application behaviour change, not merely a code change:**
running the built application confirms `DiagnosticsSampleModule`
discovered, registered, and initialised through the real `TempestHost`,
`IDiagnosticsProvider` registered during the existing Platform Services
Registered phase, and `GetDiagnosticsSummaryCommand` registered against
the real `ICommandDispatcher`/`ICommandRegistry` — confirmed by direct
execution, not merely by the test suite.

## Snapshot: 2026-07-28 (WP 5.3 — Developer Experience Improvements)

The Developer Experience release's own final Work Package — no new
platform service, no new namespace under `src/Tempest.Core/`. Adds one
`dotnet new` module template (`src/Templates/`, content only — not part
of `TempestOS.slnx`) and one clarity fix to
`ReflectionFrameworkDiscoveryService`'s own existing failure message.

| Metric | WP 5.2 | WP 5.3 (current) |
|---|---|---|
| `src/` `.cs` files / lines | 143 / 8,986 | 144 / 9,043 (adds 1 template content file, `src/Templates/Tempest.Templates.Module/TempestSampleModule.cs` — deliberately not part of `TempestOS.slnx`; zero new *compiled* production files) |
| `tests/` `.cs` files / lines | 68 / 10,628 | 71 / 10,872 (adds `Templates/RepositoryPaths.cs`, `ModuleTemplateManifestTests.cs`, `ModuleTemplateContentTests.cs`) |
| Executed tests (`dotnet test`) | 542, 0 failures | **552, 0 failures** (10 new) |
| Namespaces under `src/` | 18 declared + 1 global | 18 declared + 1 global (unchanged — `src/Templates/` is template content, not a compiled namespace; out of this register's own counted scope, matching `Namespace Register.md`'s own stated scope) |
| ADRs | 39 (`ADR-0001`–`ADR-0039`) | 39 (unchanged — no new ADR; this Work Package's own template-packaging decision met the Rejected Designs bar, not the ADR bar) |
| Rejected Designs entries | 44 (`RD-0001`–`RD-0044`) | 45 (`RD-0001`–`RD-0045`, adds `RD-0045`; also backfills `RD-0042`–`RD-0044`'s own full entries into the source log, missing since `WP 5.2` — see Repository Review) |
| Architecture documents (`docs/architecture/`) | 20 | 20 (unchanged — `Sample Module Architecture.md`'s own status line updated in place, no new document) |
| Academy articles (`docs/academy/`, all subfolders) | 75 | 76 (adds `WP5.3-developer-experience-improvements.md`; `03-building-a-module.md` updated in place, no new file) |
| `docs/` `.md` files | 185 | 186 |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — updates existing registers rather than adding new ones) |
| Feature Register | 26 Implemented/Complete, 1 Not Started | **27 Implemented/Complete, 0 Not Started** — every feature in the Developer Experience phase (and the release plan as a whole) is now complete |
| Traceability Matrix | 17 fully-traced capabilities | 18 fully-traced capabilities (adds Developer Experience Improvements; "Not Yet Applicable" category now empty) |
| Total commits | 60 | 61 (before this Work Package's own commit) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

**A genuine, manually-verified capability, not merely a code change:**
the real `dotnet new` CLI was used, once, to install the template,
generate a module at its documented location, and build it successfully
(0 warnings, 0 errors) — then the template was uninstalled and the
generated files removed, leaving no trace, exactly as a real
contributor's own first use would work. The automated test suite proves
the same claim on every future `dotnet test` run without touching the
shared, global template cache (see this Work Package's own retrospective
for why).

## Snapshot: 2026-07-28 (WP 5.4 — v0.5.0 Release Candidate & Engineering Sign-Off)

Release verification and closeout — no feature added, no architecture
redesigned. Every count below was independently re-derived directly from
the file system (`find`, `grep -c`) rather than incremented from the
previous snapshot's own stated number — the specific discipline this
Work Package's own retrospective recommends as a standing practice going
forward (see "Repository Maturity," `WP5.4-v0.5.0-release-candidate-and-
engineering-sign-off.md`).

| Metric | WP 5.3 | WP 5.4 (current) |
|---|---|---|
| `src/` `.cs` files / lines | 144 / 9,043 | 144 / 9,043 (unchanged — no production code changed) |
| `tests/` `.cs` files / lines | 71 / 10,872 | 71 / 10,872 (unchanged — no test code changed) |
| Executed tests (`dotnet test`) | 552, 0 failures | 552, 0 failures (unchanged — re-verified directly) |
| ADRs | 39 (`ADR-0001`–`ADR-0039`) | 39 (unchanged — no new ADR; re-verified by direct file count) |
| Rejected Designs entries | 45 (`RD-0001`–`RD-0045`) | 45 (unchanged — re-verified by direct `grep -c` against the source log) |
| Decision Register entries | 20 | 20 (re-verified by direct `grep -c`) |
| Academy articles (`docs/academy/`, all subfolders) | 76 *(stated — see correction)* | **77** (adds `WP5.4-v0.5.0-release-candidate-and-engineering-sign-off.md`; **corrects a silent undercount** — the "76" this row inherited from `WP 5.3`'s own snapshot had itself inherited an undercount from `WP 5.2`, neither independently re-derived from the file system; direct `find` count per subfolder now backs every figure) |
| `docs/academy/03 Work Packages/` retrospectives | 34 *(stated — see correction)* | **35** (re-verified by direct `find`/`grep -c` against the actual folder — the stated count had undercounted its own table by one since at least `WP 5.2`) |
| `docs/` `.md` files | 186 | 191 (adds `docs/releases/v0.5.0/CHANGELOG.md`, `Release Notes.md`, `ReleaseChecklist.md`, `docs/releases/v0.5.0.md`, and the `WP 5.4` retrospective) |
| Governance documents (`docs/governance/`, all subfolders) | 32 | 32 (unchanged — updates existing registers rather than adding new ones) |
| Custom exception types | 30 *(stated — see correction)* | **31** (**corrects a second silent undercount**, present since `WP 5.1B` first introduced it — `Exception Register.md`'s own Entries/Distribution tables had always summed to 31; direct `grep -rlP` against `src/Tempest.Core/` now confirms 31 files, matching) |
| Feature Register | 27 Implemented/Complete, 0 Not Started | 27 Implemented/Complete, 0 Not Started (unchanged — the release plan was already fully complete as of `WP 5.3`) |
| Traceability Matrix | 18 fully-traced capabilities | 18 fully-traced capabilities (unchanged — `WP 5.4` is a verification pass, not a new capability) |
| `docs/releases/v0.4.0/Risks.md` | 10 risks, 6 Retired | 10 risks, **all 10 Retired** (`R5`, `R7`, `R8`, `R9` retired in full — each risk's own residual exposure, carried forward since `v0.4.0`, is now confirmed resolved by a specific, named `v0.5.0` Work Package) |
| Total commits | 61 (before `WP 5.3`'s own commit) | 62 (before this Work Package's own commit; includes `WP 5.3`'s own small follow-up commit) |
| Build warnings/errors | 0/0 | 0/0 (unchanged) |

**Two genuine, silent arithmetic undercounts found and corrected, both of
the identical shape**: a register's own summary line had been updated by
incrementing the *previous* summary line's own stated number, rather than
by re-counting the table or file system beneath it each time. Neither
reflects an actual change in what exists — both are corrected
documentation, not corrected reality. See this Work Package's own
retrospective for the full discussion and the standing-practice
recommendation this finding produced.

## Snapshot: 2026-09-04 (WP 16.2A — Register and Status Currency)

This register had not been re-measured since `WP 5.4` (`v0.5.0`,
2026-07-28) — eleven releases stale. Every figure below is re-derived
directly from the file system and git history at this Work Package's
own base commit (`8b4c3948`), not incremented from `WP 5.4`'s own
figures.

| Metric | WP 5.4 (`v0.5.0`) | WP 16.2A (current, `v0.16.0` in progress) |
|---|---|---|
| `VERSION` | `0.4.0` (file not yet bumped at `WP 5.4`'s own review point) | **`0.15.0`** — `cat VERSION` |
| Total commits | 62 | **267** — `git log --oneline \| wc -l` |
| `src/` `.cs` files / lines | 144 / 9,043 | **802 / 75,955** — `find src -name "*.cs" \| wc -l`; `find src -name "*.cs" -exec cat {} + \| wc -l` |
| `tests/` `.cs` files / lines | 71 / 10,872 | **348 / 78,161** — same method, `tests/` |
| `[Fact]`/`[Theory]` attributes under `tests/` | Not tracked at `WP 5.4` | **2,725** — `grep -rE '\[Fact\]\|\[Theory\]' tests --include=*.cs \| wc -l` |
| Last real, CI-verified executed-test totals | 552/552 (`dotnet test`, `WP 5.3`) | **3,088/3,088 Core, 408/408 Desktop** at the `v0.15.0` tag (412/412 Desktop after `WP 15.2A`) — cited from `docs/releases/v0.15.0/Release Notes.md`; `dotnet test` not re-run this Work Package (documentation-only, attribute-counting sufficient per controlling instruction) |
| ADRs | 39 (`ADR-0001`–`ADR-0039`) | **120** (`ADR-0001`–`ADR-0120`) — `ls docs/adr/ \| grep -c "^ADR-"` |
| Rejected Designs entries | 45 (`RD-0001`–`RD-0045`) | **65** (`RD-0001`–`RD-0065`) — `grep -cE "^## RD-[0-9]{4}" docs/architecture/"Rejected Designs.md"`. **Disclosed, not fixed**: `Rejected Designs Register.md` (the governance index of this same log) itself still reads 45 — stale since `v0.5.0`, outside this Work Package's own itemized scope; this figure is drawn from the actual source log, `docs/architecture/Rejected Designs.md`, its own declared Source of Truth |
| Decision Register entries | 20 | **20** (unchanged) — `grep -oE "D-0[0-9]{2}" "Decision Register.md" \| sort -u \| wc -l`. `D-021`–`D-026` (`WP 16.0A`) are drafted and Proposed in `docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` but **not yet entered in the register itself**, ratification pending PR #6 — correctly excluded from this count |
| Academy articles (`docs/academy/`, all subfolders) | 77 | **240** — `find docs/academy -name "*.md" \| wc -l` |
| `docs/academy/03 Work Packages/` retrospectives | 35 | **165** — `find "docs/academy/03 Work Packages" -name "*.md" \| wc -l` |
| `docs/` `.md` files (total) | 191 | **923** — `find docs -name "*.md" \| wc -l` |
| Governance documents (`docs/governance/`, all subfolders) | 32 | **38** — `find docs/governance -name "*.md" \| wc -l` |
| Architecture documents (`docs/architecture/`) | Not tracked at `WP 5.4` | **30** — `find docs/architecture -name "*.md" \| wc -l` |
| Custom exception types | 31 | **84** — `grep -rE '^public (sealed \|abstract )?class \w+Exception\b' src/Tempest.Core --include=*.cs \| wc -l`; see `Exception Register.md`'s own `WP 16.2A` re-derivation |
| Public interfaces under `src/Tempest.Core/` | Not tracked at `WP 5.4` | **188** — `grep -rEn '^\s*public interface \w+' src/Tempest.Core --include=*.cs \| wc -l`; see `Interface Register.md`'s own `WP 16.2A` re-derivation |
| Technical Debt Register rows | Not tracked at `WP 5.4` | **122** (`TD-001`–`TD-122`) — `grep -c "^\| TD-\|^\| \`TD-" "Technical Debt Register.md"`; see that register's own `WP 16.2A` closure of `TD-57` and pointer update to `TD-45` |
| Namespaces / files in declared scope (`Tempest.Core`, `Tempest.App`, `Tempest.Samples`, `Tempest.Validation`) | Not tracked at `WP 5.4` | **46 / 713** — see `Namespace Register.md`'s own `WP 16.2A` full re-derivation; `src/`-wide (including `Tempest.Desktop`) is 61 namespaces / 802 files |
| Build warnings/errors | 0/0 | Not re-run this Work Package (documentation-only; `WP 16.1A`'s own `ci.yml`/`Governance Health Check` gate the real thing on every push) |

**Method note.** Every figure above was independently re-derived by the
command shown, not carried forward from any prior register's own
stated number, per this register's own `WP 5.4`-established standing
practice. Full command output is in
`docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md`'s
own Derivations appendix.

## Governance Suite Size (Introduced by This Work Package, WP 4.5A)

| Metric | Value |
|---|---|
| Governance registers created | 27 |
| Governance top-level documents created (Index, Philosophy, Audit Report, Maturity Report) | 4 |
| Total governance documents | 31 |

## Methodology Note

Line counts are raw `wc -l` totals, including blank lines, comments, and
XML documentation — not a "logical lines of code" metric. File counts
explicitly exclude `obj`/`bin` build-artifact directories. These figures
are a snapshot, not a trend — no historical size-over-time series is
claimed or fabricated; where a prior snapshot does not exist (this is the
first Repository Metrics Register TempestOS has produced), no comparison
is offered.

## Cross-Reference Check

The ADR count (39), Rejected Designs count (45), Academy article count
(77), test count (552), and build status (0/0) above are each
cross-checked directly against `ADR Register.md`, `Rejected Designs
Register.md`, `Academy Register.md`, `Test Register.md`, and `Validation
Register.md` respectively — all consistent, no discrepancy found, as of
`WP 5.4`'s own direct re-verification.
