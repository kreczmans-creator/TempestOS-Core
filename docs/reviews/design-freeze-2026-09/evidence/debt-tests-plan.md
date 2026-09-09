# Design-Freeze Evidence: Debt Register, Test Suite, and Remediation Plan

Audit date: 2026-09-08. Repository: `D:\Tempest\01 Projects\TempestOS`. Read-only audit; no build/test run performed. All paths repo-root-relative unless noted.

---

## 1. Debt register digest

Source: `BACKLOG.md` (223 lines), read in full.

### 1.1 Counts by owner/bucket

`BACKLOG.md` is organized into four buckets, not one flat list. Counts below are rows actually present in each bucket's tables (not the narrative totals quoted in the doc's own prose, which are called out separately).

| Bucket | Row count | Notes |
|---|---|---|
| Live Backlog (unowned) | 15 | TD-05, 24, 41, 42, 63, 78, 84, 91, 92, 93, 99, 101, 131, 134, 154 |
| Live Backlog (owned by a WP, kept live because judgement/partial) | 12 | TD-17,25,27,28,33,38,76,98,150,155,157,163 (owners: 18.0B x4, 18.2B x2, 17.1B x1 "judgement", 18.1A x1 "judgement", 19.1B x1, 19.0A x1, 18.3A x1 "partial", 17.0C x1) |
| **Live Backlog total** | **27** | Doc header says "28 of 30 cap" — the 28th (TD-147) is called out in prose as already closed by `WP 17.1B` and "no longer appears below" (`BACKLOG.md:30-40`), so 27 rows are actually tabulated today. |
| Closed by WP 17.1B (own table) | 6 | TD-142,144,145,146,147,148 (`BACKLOG.md:98-106`) |
| Owned by Programme (open, mapped to a future WP's "Closes" column) | 43 | `BACKLOG.md:119-163`; by owner: WP 17.1A=9, WP 17.1B=8 (+TD-29 dual with 18.0A), WP 17.2A=3, WP 18.0A=5 (incl. TD-29 dual), WP 17.3A=1 (TD-29 dual), WP 18.2B=1, WP 18.1A=4, WP 18.2A=3, WP 19.2A=2, WP 19.2B=8 |
| Closed (own table) | 1 | TD-01, plus TD-02 called out in prose only (no table row) (`BACKLOG.md:170-177`) |
| Archived with the Layer | 16 | TD-161,162,82,13,14,15,16,49,50,53,54,55,56,61,64,129 (`BACKLOG.md:196-213`); doc's own prose says "sixteen rows land here in the currently-open subset" (`BACKLOG.md:215`) |

Register provenance (`BACKLOG.md:3-27`): this file replaces a 468 KB `Technical Debt Register.md`, archived at `archive/docs-2026-09/governance/Quality/Technical Debt Register.md`. Triage basis is `WP 17.0B` (2026-09-08): 101 of 170 total register rows were `Open`/`Partially resolved`/`Deferred` (93 of the 101 `Open` alone — the figure `docs/releases/v1.0.0/WorkPackages.md` cites). Nine rows (TD-45, TD-123–125, TD-127, TD-151–153, TD-164, TD-43) close by `WP 17.0B` itself and appear in none of the tables above.

### 1.2 Every Live Backlog row (id, title, owner, classification)

Classification legend — **Substrate** (domain/persistence/composition-root correctness), **Surface** (Desktop UI/UX), **Governance** (process/scripts/registers), **Test-only** (the debt is itself about test coverage/visibility rather than a behavioural defect). Judgement calls, made for this audit — not present in `BACKLOG.md`.

| ID | Title | Owner | Classification |
|---|---|---|---|
| TD-05 | Module discovery still requires a parameterless constructor outside the `[ModuleMetadata]` lift | unowned | Substrate |
| TD-17 | Document revision content is an opaque string with no structured payload support | WP 18.0B | Substrate |
| TD-24 | `VerificationContext` has no bound on criteria, evidence or links recorded | unowned | Substrate |
| TD-25 | `RequirementsService` has no compare-and-swap; concurrent edits can silently clobber | WP 18.2B | Substrate |
| TD-27 | `InMemoryEngineeringObjectRepository` iteration order is unguaranteed | WP 17.1B (judgement) | Substrate |
| TD-28 | Bulk requirement commands don't auto-refresh an already-open view | WP 18.1A (judgement) | Surface |
| TD-33 | `EngineeringCockpit.FormatCoverage` returns a hardcoded, wrong-discipline empty-state string | WP 19.1B | Surface |
| TD-38 | `EngineeringObjectFactory` enforces no business-identifier uniqueness | WP 18.2B | Substrate |
| TD-41 | `ObjectEditorView` never resolves a real Requirement; always falls back to the generic body | unowned | Surface |
| TD-42 | `new-release.ps1`'s `git tag`/`git push` calls never check `$LASTEXITCODE` | unowned | Governance |
| TD-63 | `TD-40`'s dirty-tab-close fix is not pinned on its production path | unowned | Test-only |
| TD-76 | No project context anywhere in the running application | WP 19.0A | Surface |
| TD-78 | Brand design system (colours, fonts) is absent from the Desktop | unowned | Surface |
| TD-84 | Grouping row: `TD-74`/`76`/`79`/`81` are one Product Spine deficiency, not four | unowned | Governance |
| TD-91 | `IWorkspaceLayout` cannot express a tabbed or floating panel | unowned | Surface |
| TD-92 | Drag-to-dock has no live preview adorner | unowned | Surface |
| TD-93 | `Tempest.Samples` redeclares 13 canonical vocabulary strings; can't reference the owner | unowned | Substrate |
| TD-98 | Document viewer has no markup, annotation or rotation | WP 18.3A (partial) | Surface |
| TD-99 | DWG and SVG attachments report `Unsupported` in the viewer | unowned | Surface |
| TD-101 | A page rasterises at full size even when only part of it is visible | unowned | Surface |
| TD-131 | Focus-ring contrast test can't see any `Flat`-treatment state | unowned | Test-only |
| TD-134 | `SettingsDocument<TDocument>` has no per-consumer notion of "current version" | unowned | Substrate |
| TD-150 | `PersistenceStore`'s post-commit failure window: 0 of 3,330 tests would notice a revert | WP 17.0C | Substrate |
| TD-154 | CI's `linux-launch-smoke` marker now fires before the composition root runs | unowned | Test-only |
| TD-155 | Materials library reuses an incompatible payload shape under the old document Kind | WP 18.0B | Substrate |
| TD-157 | "Pinned source superseded" warning can never fire; the resolver is never wired up | WP 18.0B | Substrate |
| TD-163 | 79 seeded reference records never reach the shipped product | WP 18.0B | Substrate |

### 1.3 Rows judged Release-blocking for v1.0

Judgement calls made for this audit; `BACKLOG.md` itself marks no currently-open row "Release Blocking" (the one row it did mark that way, TD-147, is reported closed by `WP 17.1B`, `BACKLOG.md:32-40`).

| ID | Reason |
|---|---|
| TD-76 | v1.0's stated first capability is project-scoped work (`PROJECT_STATUS.md:12-13`); with no project context anywhere in the app, every project-centric command in the plan is unimplementable groundwork, not a UI gap. |
| TD-25 | No compare-and-swap on `RequirementsService` means concurrent edits silently clobber each other — a correctness/data-loss defect, not a missing feature. |
| TD-38 | No business-identifier uniqueness enforced on object creation risks duplicate/ambiguous identifiers in engineering records that other objects and documents reference. |
| TD-150 | Same defect class as the already-blocking, now-fixed TD-147 (a commit that reports failure while state already changed) — 0 of 3,330 tests would catch a revert here. |
| TD-157 | The "pinned source superseded" warning can never fire, so a user can act on superseded reference data with no signal — a correctness risk specific to an engineering-calculation governance product. |
| TD-63 | A known dirty-tab-close data-loss fix (`TD-40`) is not confirmed to run on the actual production code path. |

### 1.4 PROJECT_STATUS.md and PHYSICAL_REVIEW.md §1-2: declared gate figures

`PROJECT_STATUS.md` (56 lines, read in full):
- Branch `release/v0.17.0`; VERSION `0.17.0`, not yet tagged/published, awaiting Windows verification and Product Approval (line 3-4).
- "What a user can do today": all five v1.0 capabilities are `○` (none landed) — project setup targets `v0.19.0` (WP 19.0A); calculation-sheet authoring targets `v0.18.0` (WP 18.0A/18.2A); independent check + PDF issue targets `v0.18.0` (WP 18.2B/18.3A); time/invoice targets `v0.19.0` (WP 19.0A/19.1A); Home cockpit utilisation/margin/WIP/DSO targets `v0.19.0` (WP 19.1B) (lines 8-25).
- Work in flight: none; every v0.17.0 WP (17.0A, 17.0B, 17.0C, 17.1A, 17.1B, 17.2A parts 1-2, 17.2B, 17.3A) merged to `release/v0.17.0` (lines 29-31).
- **Gate figures** (lines 35-42): Core tests 4,931 passed/0 failed/0 skipped; Desktop tests 498 passed/0 failed/0 skipped; Build 0 warnings/0 errors both configs under `TreatWarningsAsErrors`; `DependencyDirectionTests` 5/5 green; governance health check 5/5 passed (Windows PowerShell 5.1 and PowerShell 7); CI not yet run (nothing pushed pending Windows verification).

`PHYSICAL_REVIEW.md` §1-2 (lines 14-93):
- .NET SDK pinned via `global.json` to `10.0.302`, `rollForward: latestFeature`.
- Windows is the CI-verified build/test platform (`windows-2022`, both configurations, every push); no CI step launches the real windowed app on Windows (headless Avalonia suite). macOS "expected to work," untested. Linux launches the desktop app as of `WP 16.5B` but on weaker evidence: one local `xvfb-run` launch plus an advisory (non-required) `linux-launch-smoke` CI job (line 19).
- PowerShell only needed for the governance health check; CI uses PowerShell 7 (`pwsh`); the WP 17.0B-reduced script was separately verified under Windows PowerShell 5.1 on 2026-09-08, 5 passed (line 20).
- Network needed once, for `dotnet restore` (nuget.org only, no private feed) (line 21).
- Core tests: 4,931 tests, ~20-50s — re-derived at v0.17.0 after WP 17.9.1; the v0.16.0 tree had 5,153 before WP 17.0C removed scaffolding and WP 17.2A froze the plugin/REST/licensing suites (line 58).
- Desktop tests: 498 tests, ~2.5-3 min — re-derived at v0.17.0 after WP 17.9.1; `TEMPEST_TEST_TIMEOUT_FACTOR=3` recommended on a busy machine (line 64).
- Governance health check (`scripts/governance-healthcheck.ps1`): read-only, expect 5 passed/0 warned/0 failed; WP 17.0B reduced the check set to five checks that derive from source and git (lines 80-89).

---

## 2. Test suite shape

### 2.1 Test files per folder

`tests/Tempest.Core.Tests` — 369 `.cs` test files (excludes `bin/`/`obj/`), across 54 folders/subfolders. Top folders by file count (full folder list captured; only non-trivial folders shown):

| Folder | Files | Folder | Files |
|---|---|---|---|
| Workspace (incl. Layout/Samples/Viewing subfolders) | 52 | Requirements | 7 |
| Runtime | 25 | Identity | 7 |
| EngineeringDomain (incl. SchemaVersioning) | 28 | ExportImport | 7 |
| Samples | 19 | Audit | 7 |
| Modules | 15 | EngineeringData | 6 |
| UnitsAndQuantities | 13 | Reporting | 5 |
| Logging | 12 | ReferenceData | 5 |
| Plugins | 10 | Population | 5 |
| Persistence | 10 | Notifications | 5 |
| Commands | 10 | Materials | 5 |
| Calculations | 10 | Integration | 5 |
| Bearings | 10 | Configuration | 5 |
| Settings | 9 | Verification, Shell, Projects, Events, CommercialIntelligence, BackgroundServices | 4 each |
| EngineeringIntelligence | 8 | Templates, Macros, EngineeringAssets, DependencyInjection | 3 each |
| BusinessGovernance | 8 | Versioning, Standards, Navigation, Manufacturing, Knowledge, Input, Fasteners, Constants, Components, BusinessOperations, root | 2 each |
| | | Diagnostics, Concurrency, Architecture | 1 each |

`tests/Tempest.Desktop.Tests` — 72 `.cs` test files, all in one flat folder (no subfolders). `tests/Frozen/Tempest.Core.Tests` also exists (frozen REST/Plugin/Licensing suites, `ADR-0146`; excluded from the two counts above since the task scopes only the two live projects).

### 2.2 Sample classification (40 files)

Chosen one file per Core.Tests top-level folder (30 folders) plus 10 files spanning Desktop.Tests, read for class doc-comments and `[Fact]`/`[Theory]` method names (some fully read where the name was ambiguous). Legend: **Behavioural** = asserts a user-visible/domain outcome; **Structural** = asserts code shape, reflection, DI wiring, dependency direction, or scans source text; **Self-referential** = asserts a constant/message against a duplicate of itself.

| File | Class | Classification | Reason |
|---|---|---|---|
| `Workspace/EngineeringCockpitTests.cs` | `EngineeringCockpitTests` | Behavioural | Asserts real computed cockpit values (e.g. `"33% (1/3)"` coverage) and honest empty-states, not code shape. |
| `Runtime/AuditHostRegistrationTests.cs` | `AuditHostRegistrationTests` | Structural | Asserts DI resolvability and singleton identity (`Host_ResolvingIAuditRecorderTwice_ReturnsTheSameInstance`) — composition-root wiring, not a domain outcome. |
| `EngineeringDomain/GovernanceRestartTests.cs` | `GovernanceRestartTests` | Behavioural | Verifies a Risk/Issue/Decision/Hazard's real fields survive an actual host restart. |
| `Samples/ClockModuleTests.cs` | `ClockModuleTests` | Structural | Exercises a demo/reference `ClockModule`'s lifecycle timestamps to test the module-lifecycle mechanism, not a shipped feature. |
| `Modules/ModuleMetadataAttributeDiscoveryTests.cs` | `ModuleMetadataAttributeDiscoveryTests` | Structural | Reflection-based discovery of `[ModuleMetadata]`-carrying types and constructor shapes. |
| `UnitsAndQuantities/UnitConverterTests.cs` | `UnitConverterTests` | Behavioural | Asserts a real numeric outcome (1 m converts to 100 cm), despite the method name "Delegates". |
| `Logging/LoggerTests.cs` | `LoggerTests` | Behavioural | Asserts real level-filtering and entry-population behaviour against a `RecordingLogSink`. |
| `Plugins/PluginManifestDiscoveryServiceTests.cs` | `PluginManifestDiscoveryServiceTests` | Behavioural | Asserts real manifest parsing/validation/security outcomes (malformed JSON, path escape). |
| `Persistence/PersistenceStoreTests.cs` | `PersistenceStoreTests<TBackend>` (+2 sealed subclasses) | Behavioural | Generic contract test run against both the file and SQLite backends; real write/read/delete outcomes. |
| `Commands/CommandDispatcherTests.cs` | `CommandDispatcherTests` | Behavioural | Real dispatch/handler-invocation and duplicate-registration outcomes. |
| `Calculations/CalculationEngineTests.cs` | `CalculationEngineTests` | Behavioural | Real registration/execution outcomes, incl. specific exception types. |
| `Bearings/BearingSearchTests.cs` | `BearingSearchTests` | Behavioural | Real search-matching outcomes over a bearing catalogue. |
| `Settings/SettingsProviderTests.cs` | `SettingsProviderTests` | Behavioural | Real get/set round-trip and duplicate-key outcomes. |
| `EngineeringIntelligence/RuleEngineTests.cs` | `RuleEngineTests` | Behavioural | Real threshold/pass-fail rule evaluation, incl. boundary and "indeterminate" cases. |
| `BusinessGovernance/PricingTests.cs` | `PricingTests` | Behavioural | Real quotation pricing outcomes, incl. exact (non-floating-point) arithmetic assertion. |
| `Requirements/RequirementsServiceTests.cs` | `RequirementsServiceTests` | Behavioural | Real requirement lifecycle/status outcomes (912-line file; largest in the sample). |
| `Identity/PermissionEvaluatorTests.cs` | `PermissionEvaluatorTests` | Behavioural | Real permission-grant/deny outcomes. |
| `ExportImport/ExportServiceTests.cs` | `ExportServiceTests` | Behavioural | Real artefact-writing and exception-propagation outcomes. |
| `Audit/AuditRecorderTests.cs` | `AuditRecorderTests` | Behavioural | Real actor-id/timestamp recording, incl. a concurrency outcome (`ConcurrentRecordAsyncCalls_NeverLoseARecord`). |
| `EngineeringData/EngineeringDocumentStoreTests.cs` | `EngineeringDocumentStoreTests` | Behavioural | Real document creation/revision outcomes. |
| `Reporting/ReportingServiceTests.cs` | `ReportingServiceTests` | Behavioural | Real render-dispatch outcome to a registered renderer. |
| `ReferenceData/ReferenceDataCatalogTests.cs` | `ReferenceDataCatalogTests` | Behavioural | Real record registration, provenance and duplicate-key outcomes. |
| `Population/SeedDatasetTests.cs` | `SeedDatasetTests` | Behavioural | Real seeding idempotency and "never overwrites a user edit" outcomes. |
| `Notifications/NotificationDispatcherTests.cs` | `NotificationDispatcherTests` | Behavioural | Real subscribe/publish/unsubscribe delivery-order outcomes. |
| `Materials/MaterialLibraryTests.cs` | `MaterialLibraryTests` | Behavioural | Real domain-model defaulting and family-trait outcomes. |
| `Integration/BracketScenarioTests.cs` | `BracketScenarioTests` | Behavioural | Full end-to-end scenario over the real material corpus (selection, design rules, asset chain). |
| `Configuration/ConfigurationBuilderTests.cs` | `ConfigurationBuilderTests` | Behavioural | Real multi-source merge/override/case-insensitivity outcomes. |
| `Verification/VerificationServiceTests.cs` | `VerificationServiceTests` | Behavioural | Real verification-record outcomes. |
| `Architecture/DependencyDirectionTests.cs` | `DependencyDirectionTests` | Structural | Explicit dependency-direction/project-reference and built-assembly-reference checks (`Core_DependsOnNothingInThisRepository`, `NoAvaloniaPackage_ReachesCoreOrWorkspaceOrHarness`). |
| `Concurrency/AsyncKeyedLockTests.cs` | `AsyncKeyedLockTests` | Behavioural | Real serialization/entry-lifecycle outcomes under concurrent access. |
| `Tempest.Desktop.Tests/AccessibilityAutomationTests.cs` | `AccessibilityAutomationTests` | Behavioural | Real `AutomationProperties` values on real rendered controls (assistive-technology-visible outcome). |
| `Tempest.Desktop.Tests/CockpitViewHonestyTests.cs` | `CockpitViewHonestyTests` | Behavioural | Explicitly checks real seeded data renders, "never the old fixed placeholder." |
| `Tempest.Desktop.Tests/BracketCalculationJourneyTests.cs` | `BracketCalculationJourneyTests` | Behavioural | Full engineer user-journey through review/release/calculate/retrieve. |
| `Tempest.Desktop.Tests/FeatureCompletionTests.cs` | `FeatureCompletionTests` | Behavioural | "Actually chains the real status transitions" / "actually creates a real object" — explicit real-outcome framing (764-line file). |
| `Tempest.Desktop.Tests/ProductGapReconciliationAuditTests.cs` | `ProductGapReconciliationAuditTests` | Behavioural | Renders real screens and asserts on real visible surface content, incl. a known gap (`FictionalSampleContent_IsStillVisibleToEndUsers_InARealLaunch`). |
| `Tempest.Desktop.Tests/SurfaceCommandPolicyCompletenessTests.cs` | `SurfaceCommandPolicyCompletenessTests` | Structural | Cross-references a delete-confirmation policy list against registered command descriptors — a completeness/consistency check over registrations, not a runtime scenario. |
| `Tempest.Desktop.Tests/WorkspaceLayoutControllerTests.cs` | `WorkspaceLayoutControllerTests` | Behavioural | Real drag/dock/undock/save-restore outcomes. |
| `Tempest.Desktop.Tests/MainWindowCompositionTests.cs` | `MainWindowCompositionTests` | Behavioural | Real menu/toolbar/dispatcher-driven UI outcomes. |
| `Tempest.Desktop.Tests/DormantKeyboardBindingTests.cs` | `DormantKeyboardBindingTests` | Structural | Literally scans every `.cs` file under `src/` (`Directory.EnumerateFiles`) for a gesture-to-command binding call — a source-text scan, not a runtime assertion. Its own doc-comment explains why: "a behavioural test would not catch it." |
| `Tempest.Desktop.Tests/VisualPolishTests.cs` | `VisualPolishTests` | Behavioural | Real resolved-brush, toast-lifecycle and dialog-resolution outcomes. |

**Sample result: 34/40 (85%) Behavioural, 6/40 (15%) Structural, 0/40 Self-referential.**

A repo-wide automated scan for the sharpest form of self-referential assertion — `Assert.Equal(X, X)` / `Assert.Same(X, X)` with the identical expression on both sides — found **zero matches** across both projects. The closest pattern found, not counted as self-referential above because it is a genuine (if thin) round-trip check rather than a tautology: nine `ExceptionTests.cs` files (one per domain folder, e.g. `Calculations/ExceptionTests.cs`) that construct an exception with a literal string and assert `.Message` equals that same literal — testing constructor pass-through, not a duplicated production constant. Extrapolating the 34/6/0 sample ratio across the full 441 (369+72) test files suggests roughly 85% Behavioural / 15% Structural by file count; the codebase's own naming convention (`RealData_…NeverTheOldFixedPlaceholder`, `Actually…`, `NotAPlaceholder`) is itself evidence of a deliberate, stated policy against placeholder/tautological assertions (see `DormantKeyboardBindingTests`'s doc-comment, which explains in prose why a behavioural test could not have caught the defect it guards).

### 2.3 Test helper infrastructure

| Helper | Path | Lines | Purpose |
|---|---|---|---|
| `RunningHostFixture` | `tests/Tempest.Core.Tests/Runtime/RunningHostFixture.cs` | 91 | `WaitUntilRunningAsync(ITempestHost, TimeSpan?)` — replaced ~90 copy-pasted `while (host.State is Created or Starting) await Task.Delay(5)` polling loops (`WP 17.0C`) with one bounded (30 s default), timeout-throwing implementation. |
| `RecordingLogSink` | `tests/Tempest.Core.Tests/Logging/RecordingLogSink.cs` | 18 | Test-only `ILogSink` backed by a `ConcurrentQueue<LogEntry>`; records every entry for assertion, thread-safe for concurrency tests. |
| `IsolatedPersistenceRoot` | `tests/Tempest.Core.Tests/IsolatedPersistenceRoot.cs` | 88 | Extension method `WithIsolatedPersistenceRoot()` on `ITempestHostBuilder`; gives every host-building test its own persistence root under a per-process temp directory (cleaned on `ProcessExit`) so no test writes into, or is refused by the lock on, the real default `persistence-data` root (`WP 17.0A`/`17.1A`). |
| `DesktopTestHelpers` | `tests/Tempest.Desktop.Tests/DesktopTestHelpers.cs` | 224 | Shared Desktop-suite utilities: `TimeoutFactor`/`Deadline` (scales render deadlines via `TEMPEST_TEST_TIMEOUT_FACTOR`, `WP 17.0A`), `AssertPlaced`/`AssertNoSiblingOverlap` (catches controls rendered with zero size or drawn on top of a visible sibling — the exact `v0.16.0` calculation-workspace overlap defect), and a tab-scoped `FindButton` (replacing a duplicated, ribbon-wide version that silently matched the wrong same-named command). |

Each helper's own doc-comment states the specific defect or duplication it was built to stop, with the originating Work Package.

### 2.4 Stryker mutation testing configuration

`stryker-config.json` (repo root, 24 lines, read in full):

```json
{
  "stryker-config": {
    "project": "Tempest.Core.csproj",
    "solution": "src/TempestOS.slnx",
    "test-projects": ["tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj"],
    "mutate": ["Calculations/**/*.cs", "UnitsAndQuantities/**/*.cs"],
    "reporters": ["html", "json", "progress"],
    "thresholds": {"high": 85, "low": 70, "break": 70},
    "concurrency": 4
  }
}
```

Targets only two folders of `Tempest.Core` — `Calculations/**` and `UnitsAndQuantities/**` — i.e. mutation testing is scoped to the calculation-engine and units/quantities code, not the whole Core project, and not Desktop at all. Break threshold 70%, high-confidence bar 85%. No other Stryker config file exists in the live tree (a stale copy exists only under `.claude/worktrees/agent-a35c1929ee24086e9/`, an agent worktree artifact, not part of the reviewed source).

---

## 3. Test fragility evidence

`git log --format='%h %s' -300` (528 commits total on `HEAD`; the 300-commit window covers back to 2026-08-11) searched case-insensitively, word-bounded, for: `intermittent`, `flaky`/`flakiness`, `deadline(s)`, `timeout(s)`, `race`/`races`/`racy`, `ObjectDisposed`, `retry`/`retries`, `"stale binaries"`, `drain`/`draining`. (An unbounded substring pass first returned false positives — `traceability` contains "race", `engineering change` contains "hang" — discarded once word-bounded.) No commit subject in the window contains `flaky`, `flakiness`, `deadline`, `timeout`, `ObjectDisposed`, `retry`, `"stale binaries"`, or `drain` literally; four subjects contain `intermittent` or `race`:

| Hash | Date | Subject |
|---|---|---|
| `d7c641e` | 2026-09-08 | WP 17.9.0: gate figures into the status, guide and Release Notes; explain the pool-clear **intermittent** |
| `c486f27` | 2026-09-05 | Merge WP 16.4B-R2 — attachment sweep write-intent marker and sweep read ordering (data-loss **race**) |
| `aa0daaf` | 2026-09-05 | WP 16.4B-R2: close the attachment sweep data-loss **race** with a write-intent marker |
| `7c4554d` | 2026-09-05 | Test quality remediation: a Console **race** reopened, and a corpus test that could not fail |

### 3.1 What each fixed

**`aa0daaf`/`c486f27` (2026-09-05) — attachment sweep data-loss race.** `AttachmentContentReconciliationService` could permanently delete a live, in-flight attachment's bytes when a sweep ran between `AttachContentAsync`'s content write and its state write. Fixed with a durable write-intent marker (`IAttachmentWriteIntentStore`) bracketing the existing two writes. Scope widened mid-fix when a second reviewer reproduced the identical write-then-index race in `MaterialCatalogReconciliationService` and `RequirementsReconciliationService`; both now scan the derived side before the authoritative side, closing the race by construction. Six new deterministic `TaskCompletionSource`-driven interleaving tests added (no timing dependence).

**`7c4554d` (2026-09-05) — Console race reopened.** `RestartProofTests` redirected the process-wide `Console.Out` without joining the `[Collection("Console output capture")]` that exists to serialise the ~52 classes doing this (the collection itself is the fix for `TD-34`, closed the same release by `WP 16.4A`); this file landed on a parallel branch ~11 minutes after that convention merged, so its author never saw it. The reviewer could not reproduce the race in five targeted runs — "real, load-dependent, and rare enough that local runs do not settle it," matching `TD-34`'s own incident history. Same commit also strengthened a golden-corpus migration test that previously asserted only `Id`/`SchemaVersion` survived round-trip (would have silently passed a migration that dropped every other field); mutation-proven by temporarily making the read path lossy.

**`d7c641e` (2026-09-08) — the pool-clear intermittent, explained.** Release Notes state: `ReconciliationHostRegistrationTests` had failed in 2 of 6 worktree runs during `WP 17.1B`, attributed at the time to "SQLite lock contention." It was not — three companion commits earlier the same day fixed the real causes, and this commit documents the explanation in `PROJECT_STATUS.md`/`PHYSICAL_REVIEW.md`/Release Notes (`docs/releases/v0.17.0/Release Notes.md:157-162`):
- `1dcf8df` "Clear only this store's SQLite connection pool on dispose" — `SqliteConnection.ClearAllPools` is process-wide; disposing one store's connection pool disposed the native handle under every *other* live store in the process, surfacing as `ObjectDisposedException (SQLitePCL.sqlite3)` in roughly 1 of 4 parallel Core runs. Fixed by calling `ClearPool` on only this store's own connection string.
- `94bf990` "Bind the native SQLite provider eagerly, before any connection opens" — a parallel test run reproduced a first-use initialisation race in the SQLitePCL provider once; fixed by calling the idempotent `Batteries_V2.Init` from the store's static constructor.
- `88f36de`/`a7c33cf` "Let in-flight commands land before the platform is disposed under them" / "Track a command invocation from the moment it is requested" — root cause of a separate intermittent *Desktop* failure (`FeatureCompletionTests.VerificationCreate_UsesTheCurrentSelectionAsSubject`): a ribbon command's cockpit-refresh continuation ran after test teardown had already disposed the (now-disposable-since-`WP 17.1A`) SQLite store, throwing `ObjectDisposedException`. Fixed with `ICommandRegistry.WhenIdleAsync(timeout)`, which `WorkspaceManager.DisposeAsync` awaits (bounded, 10 s) before disposing the host; the invocation counter itself was fixed to start at request-time (prompt phase included), not at dispatch, since a caller awaiting idleness during an open prompt previously saw a false "zero in flight."

### 3.2 Related determinism work outside the literal keyword match (same window, cited for completeness)

- `1dbb8ab` (2026-09-04, `WP 16.4A`) "Test determinism — TD-34, TD-119, TD-83, ..." — closes `TD-34` (the `Console.Error`/`Console.Out` global-stream race, via an injectable `errorWriter` on `CompositeLogSink` plus the `[Collection("Console output capture")]` convention `7c4554d` above later found one file outside of), `TD-119` (removes the last fixed `Task.Delay(50)`, replaced by a condition-based wait after 30/30 clean runs), a Core-side persistence-root leak across four Project* test classes (65 call sites), and an environment dependency in `SampleSeparationTests` (`.csproj` enumeration walked into `.claude/worktrees/**`).
- `08915c2` (2026-09-05, `WP 16.9.0`) "CI determinism matrix obtained" — ran 5/5 clean CI matrix runs (both configurations + governance + CI Gate + advisory Linux smoke, 25/25 jobs) on one frozen commit before accepting `WP 16.4A`, after an independent reviewer refused local repetition as a substitute, citing `WP 13.12.9` (2026-08-18, outside this 300-commit window) — a case where a flake reproduced zero times in 25 local attempts yet had failed a real tag-triggered `release.yml` run.
- `62e753f` (`WP 17.0C`) "RecordingLogSink replaces console capture for real log assertions" — removes console-capture scaffolding from 83 test classes (per Release Notes line 99, "so they run in parallel"), the same class of global-stream sharing `TD-34`/`7c4554d` guarded against.

### 3.3 Summary: classes of flakiness fixed, and the fix pattern

| Class | Example(s) | Fix pattern |
|---|---|---|
| Global mutable stream shared across parallel tests (`Console.Out`/`Console.Error`) | `TD-34`, `7c4554d`, `62e753f` | Inject a `TextWriter` instead of touching the process-global stream; where legacy code still must, serialise via an xUnit `[Collection]`; eventually remove the pattern (`RecordingLogSink`) rather than police it. |
| Fixed/arbitrary sleep racing a real async completion | `TD-119`, `WP 13.12.9`, `RunningHostFixture`'s ~90-site predecessor | Replace with a bounded condition-based poll (interval + deadline) that fails only if the awaited condition genuinely never occurs. |
| Process-wide native resource cleanup affecting sibling instances | `1dcf8df` (`SqliteConnection.ClearAllPools`) | Scope the cleanup call to the one connection string owned by the disposing instance. |
| One-time native/static initialisation raced by concurrent first use | `94bf990` (SQLitePCL `Batteries_V2.Init`) | Force the idempotent init eagerly and deterministically (static constructor) rather than on first real use. |
| Disposal racing an in-flight async continuation | `88f36de`/`a7c33cf` (`ObjectDisposedException` on a post-teardown UI continuation) | Track in-flight work explicitly (`InFlightInvocations`/`WhenIdleAsync`) and have the disposing owner wait for idle, bounded by a timeout. |
| Write/derived-index ordering race under concurrent sweep and mutation | `aa0daaf`/`c486f27` (attachment/material/requirements reconciliation sweeps) | Either a durable write-intent marker bracketing the two writes, or reordering reads to scan the derived side before the authoritative side so the invariant holds by construction. |
| Test assertion too weak to fail on the regression it exists to catch | `7c4554d`'s golden-corpus test, `TD-119`'s disposition | Strengthen the assertion and mutation-prove it (temporarily break the code path, confirm the test now fails, then revert). |

No commit subject in the 300-commit window contains the literal phrase "stale binaries"; none was found searching the full history either.

---

## 4. CI

Two workflow files under `.github/workflows/`: `ci.yml` (576 lines) and `release.yml` (181 lines). No other workflow files exist.

### 4.1 `ci.yml` jobs

| Job | Runs on | Timeout | Triggers | What it does |
|---|---|---|---|---|
| `build-and-test` | `windows-2022`, matrix `[Debug, Release]` | 30 min | push, PR, `workflow_dispatch` | Restores via `global.json`; builds with `-p:TreatWarningsAsErrors=true` (applied only at the CI/release command line, never in `Directory.Build.props`); runs `dotnet test` on the whole solution with `--blame-hang --blame-hang-timeout 5m` and `--collect:"XPlat Code Coverage"`; publishes a diagnostic build/test/coverage summary (`continue-on-error: true`, explicitly never a gate — added after run 33659184434 turned a green test run red on an unparsable `.trx`); uploads build logs, `.trx` results, and (Release only) the `Tempest.Desktop` and `Tempest.Harness` build outputs as two separate artifacts (`continue-on-error: true` after run 33240113523 hit an artifact-storage quota). |
| `governance-health-check` | `windows-2022` | 10 min | after `build-and-test` (`needs`, `if: always()` — runs even on a red build) | Checks out by SHA with `fetch-tags: true`, `fetch-depth: 0`; runs `scripts/governance-healthcheck.ps1`, posts its summary to the step summary. |
| `linux-launch-smoke` | `ubuntu-latest` | 15 min | — (not gated by anything, and not in `gate`'s `needs`) | Builds `Tempest.Desktop` Release, launches it under `xvfb-run` with a 25 s `timeout --kill-after=10`; success requires exit 124/137 (still running at timeout) **and** the log containing `Host -> Running.` (added `WP 16.1A-R1` after a still-running process that had actually deadlocked during startup was passing identically to a healthy launch); a written crash log fails the job regardless of exit code. Explicitly advisory — comment block states Linux is "NOT CI-verified in the same sense as Windows" (`D-025`, ratified 2026-09-05). |
| `mutation` | `windows-2022` | **180 min** | `workflow_dispatch` **or** `schedule` only (never push/PR) | `dotnet tool restore` then `dotnet stryker` (reads `stryker-config.json` by default-filename convention); uploads the HTML/JSON report; break threshold (70, from `stryker-config.json`) is informational only — not wired into `gate`. |
| `gate` | `windows-2022` | (none set — inherits the default 360 min) | `needs: [build-and-test, governance-health-check]`, `if: always()` | The single named required status check: fails if either dependency's `result` is not `success`. `linux-launch-smoke` and `mutation` are deliberately excluded from `needs`. |

**Schedule**: `cron: "0 6 * * 1"` — Monday 06:00 UTC, ahead of the working week — is the only scheduled trigger, and it exists solely to fire the `mutation` job (per the comment at `ci.yml:37-45`, chosen to be "clear of any push traffic").

**Concurrency**: `group: ci-${{ github.workflow }}-${{ github.ref }}` with `cancel-in-progress: true` — a later push to the same ref cancels an in-flight run on that ref (documented in commit `08915c2`'s body as the reason a frozen release-candidate ref, not the working branch, was used for the WP 16.4A determinism matrix).

**CI Gate check name**: `CI Gate` (the `gate` job's `name:`), required via branch protection per `CONTRIBUTING.md:52-53` — "strict" (branch must be up to date before merging).

### 4.2 `release.yml`

Triggered only by pushing a tag matching `v*.*.*` (covers final, hotfix-patch and `-rc.N` release-candidate tags with one pattern). Single job `publish` (`windows-2022`, 30 min): re-restores/re-builds/re-tests Release at the tagged commit (independent of whatever CI run the branch produced earlier), then **also runs the Governance Health Check as a hard gate** (added per `WP 16.1A-R1` finding that the release path was weaker than the merge path — a tag could previously publish with governance drift the merge gate would have rejected), packages `Tempest.Desktop` and `Tempest.Harness` into two separate zip archives (never bundled — `ADR-0101`/`WP 11.3B`), resolves `docs/releases/vX.Y.Z/Release Notes.md` (falling back to a generated placeholder for an RC tag with no notes file yet), and publishes a GitHub Release via `gh release create` (marked `--prerelease` for an `-rc.` tag).

### 4.3 Coverage publishing

Coverage is collected (`--collect:"XPlat Code Coverage"`, `coverlet.collector`) in every `build-and-test` matrix leg and surfaced only as a **diagnostic** line-coverage percentage per test project in the job's step summary (`ci.yml:236-260`) — parsed from `coverage.cobertura.xml`, never enforced as a threshold, and not uploaded to any external coverage service (no Codecov/Coveralls step in either workflow).

### 4.4 Branch protection (per `CONTRIBUTING.md:47-62`)

- Required status check: `CI Gate`, strict (must be up to date before merge).
- Pull request required before merging; **0 required approvals** (stated reason: solo-owner repository, owner cannot approve their own PR — the `CI Gate` check plus one-round review discipline substitute).
- No force pushes, no deletion of `main`.
- Conversation resolution required before merging.
- `enforce_admins` is off (admins not blocked by the review rule), so the solo owner can merge a green PR without a second account; `CI Gate` still applies to everyone.

Related CONTRIBUTING.md provisions: a Work Package is one branch + one PR, no separate retrospective document (`CONTRIBUTING.md:3-8`); one review, one round — findings are fixed in-PR or filed to `BACKLOG.md`, never silently dropped, never reopening a second round (lines 10-15); **Release Blocking** is defined narrowly as "data loss a user can reproduce from the running UI" — everything else goes to `BACKLOG.md` (lines 17-23); a Markdown-lines-added-vs-code-lines-added budget enforced by the health check (lines 25-33); Definition of Done requires 0 warnings/0 errors both configs, all tests passing both configs, `DependencyDirectionTests` green, an ADR for constraining decisions, a `PHYSICAL_REVIEW.md` §7 row for new user-facing surface, and one Release Notes line (lines 35-45).

---

## 5. The v1.0.0 plan

Source: `docs/releases/v1.0.0/WorkPackages.md` (251 lines), read in full. Status line (lines 3-20): "Proposed, 2026-09-08; amended the same day after Product Owner review" — supersedes a 2026-09-04 proposal built on the assumption that the `v0.15.0` code shape was what would ship; the full tree review at `f19e231` found "three substrates are the wrong shape for the stated business, that four layers are speculative, and that the governance process now costs more than the product." As of this document, "Nothing below has begun. `VERSION` is `0.16.0`" (line 22) — stated inside the plan itself, now stale against the actual repository state (see §5.3: the `v0.17.0` release this plan's own table describes is engineering-complete).

### 5.1 Every Work Package

Effort is developer-days "for one developer working with agents" (line 70). Dependencies column states only what `WorkPackages.md` itself specifies (the named critical-path chain, line 177, and the prose "Sequencing rule," lines 63-71) — no dependency is inferred beyond that.

| WP | Title | Type | Effort | Dependencies (as stated) | Closes |
|---|---|---|---|---|---|
| `WP 17.0A` | Immediate defects | Fix | 3 | none stated (first in sequence) | `TD-137`, `TD-166` residual, `TD-119` recurrence, unregistered `HostedServiceManager` defect |
| `WP 17.0B` | Governance reset | Governance | 4 | none stated | `TD-45`, `TD-123`–`125`, `TD-127`, `TD-151`–`153`, `TD-164` |
| `WP 17.0C` | Test-suite consolidation | Tests | 5 | none stated | `TD-34`, `TD-114`, `TD-119`, `TD-150` |
| `WP 17.1A` | SQLite persistence (ADR-0144) | Substrate | 6 | **On critical path** (first link) | `TD-12`,`18`,`20`,`36`,`59`,`67`,`88`,`137`,`149`,`156` |
| `WP 17.1B` | Transactional engineering object store (ADR-0145) | Substrate | 8 | **Critical path**, after `17.1A` | `TD-23`,`32`,`68`,`86`,`95`–`97`,`135`,`136`,`138`–`148`,`158`,`170` |
| `WP 17.2A` | Platform trim (ADR-0146) | Substrate | 6 | none stated | `TD-01`–`04`,`06`,`13`–`16`,`49`–`56`,`61`,`62`,`64`,`69`,`94`,`103`,`129`,`130` |
| `WP 17.2B` | Repackage the workspace layer | Refactor | 2 | none stated | `TD-75` residual, `TD-162` |
| `WP 17.3A` | Units as a dimension vector (ADR-0147) | Substrate | 5 | none stated ("Sequencing rule" implies before `18.0A`) | `TD-19` residual, `TD-29` precondition |
| `WP 17.9.0` | `v0.17.0` release | Release | 2 | after all `v0.17.0` WPs | — |
| `WP 18.0A` | Calc-sheet model and expression engine (ADR-0148) | Substrate | 12 | **Critical path**, after `17.1B`; "Sequencing rule" also names `17.1A`/`17.3A` as substrate it sits on | `TD-21`,`22`,`29`,`30`,`159`,`167`–`169` |
| `WP 18.0B` | Reference tables with structured citation (ADR-0149) | Substrate | 6 | none stated | `TD-17`,`155`,`157`,`163` |
| `WP 18.1A` | Async workspace read surface and change notification | Refactor | 6 | none stated | `TD-58`,`66`,`90`,`108`,`111`,`117`,`118`,`121` |
| `WP 18.2A` | Calc-sheet editor | Surface | 8 | **Critical path**, after `18.0A`; deletes `BracketCalculationWorkbench` etc. once it reproduces the bracket journey | `TD-79`(calc),`160`(calc),`165` |
| `WP 18.2B` | Independent check and issue | Surface | 4 | **Critical path**, after `18.2A` | `TD-25`,`31`,`38`(for sheets) |
| `WP 18.3A` | Calc-sheet PDF report | Surface | 4 | **Critical path**, after `18.2B` | `TD-98` partial, `TD-160`(documents) |
| `WP 18.9.0` | `v0.18.0` release | Release | 2 | after all `v0.18.0` WPs | — |
| `WP 19.0A` | Project commercial core (ADR-0150) | Model + Surface | 6 | **Critical path**, after `18.3A`; "Sequencing rule": business seam sits on the sheet model because an invoice line cites an issued sheet | `TD-76` residual, `TD-81`(Commercial) |
| `WP 19.0B` | Archive P02–P07 | Refactor | 2 | none stated | `TD-161`,`162` |
| `WP 19.1A` | Outbound invoicing connector (ADR-0151) | Integration | 9 | **Critical path**, after `19.0A` | `FCR-0018` (outbound direction) |
| `WP 19.1B` | KPI read models and Home cockpit | Surface | 4 | none stated (reads `19.0A`/`19.1A`/`Project` data per its own equations) | `TD-33`, `TD-118` residual |
| `WP 19.2A` | Desktop composition | Refactor | 4 | none stated ("Sequencing rule": Desktop rework waits for the async read surface, `18.1A`) | `TD-105`–`107`,`109`,`112`,`113`,`115` |
| `WP 19.2B` | An honest rail and the remaining surfaces | Surface | 6 | none stated | `TD-65` residual,`73`,`74`,`77` residual,`81`,`128`,`132`,`133` |
| `WP 19.3A` | Layout verification in CI | Tests | 3 | none stated | `TD-83`, the `WP 17.0A` overlap class |
| `WP 19.9.0` | `v0.19.0` release | Release | 2 | after all `v0.19.0` WPs | — |
| `WP RC.0A` | Installer and upgrade | Release engineering | 4 | **Critical path**, after `19.1A` | `TD-116` disposition, `D-025` |
| `WP RC.0B` | Golden-example coverage | Tests | 3 | none stated | `WP 17.0C` residual |
| `WP RC.0C` | Security posture statement | Documentation | 2 | none stated | `FCR-0003`/`FCR-0004` disposition for v1.0 |
| `WP RC.0D` | Determinism and load | Tests | 2 | none stated | `TD-119` class |
| `WP RC.0E` | Physical review on a clean machine, recorded | Verification | 2 | **Critical path** (final link), after `RC.0A` | — |
| `WP RC.0F` | Release (`VERSION` → `1.0.0`) | Release | 1 | after `RC.0E` | — |

### 5.2 Stated totals, parallelism, and critical path

| Release | Dev-days | Calendar (stated) |
|---|---|---|
| `v0.17.0` Reset and Substrates | 41 | weeks 1–6 |
| `v0.18.0` Calculation as Document | 42 | weeks 7–13 |
| `v0.19.0` Consultancy Seam and Desktop | 36 | weeks 14–19 |
| `v1.0.0` Release Candidate | 14 | weeks 20–22 |
| **Programme total** | **133** | **22 weeks** |

(Each release's own effort column sums to the stated release total exactly: 3+4+5+6+8+6+2+5+2=41; 12+6+6+8+4+4+2=42; 6+2+9+4+4+6+3+2=36; 4+3+2+2+2+1=14.)

**Parallelism assumption** (lines 169-173): "133 developer-days in 22 calendar weeks (110 working days) assumes roughly 1.2 parallel streams from agents working independent Work Packages within a release; executed strictly serially it is 27 weeks." (133/110 ≈ 1.21, consistent with the stated ~1.2.) Contingency is explicitly not added as a line; a slipped release's surfaces slip with it, but the next release's substrate work still starts on time.

**Critical path** (line 177, stated verbatim): `WP 17.1A → 17.1B → 18.0A → 18.2A → 18.2B → 18.3A → 19.0A → 19.1A → RC.0A → RC.0E`. Summed effort along this chain: 6+8+12+8+4+4+6+9+4+2 = **63 developer-days** — under half the 133-day programme total, meaning roughly 70 developer-days of the plan (governance, test infrastructure, platform trim, units, reference tables, async read surface, archival, KPIs, Desktop composition, rail work, RC documentation/testing) is stated to be off the critical path and schedulable in parallel by the ~1.2-stream assumption. The document states a slip on this chain "is a slip on the programme," reported in that release's Release Notes.

### 5.3 Revised view: what is already done on `release/v0.17.0`

Source: `docs/releases/v0.17.0/Release Notes.md`, "What shipped, by Work Package" (lines 93-107), cross-checked against `PROJECT_STATUS.md` line 29-31 ("Every `v0.17.0` Work Package … is merged to `release/v0.17.0`").

| WP (plan) | Status | Evidence |
|---|---|---|
| `WP 17.0A` | **Done** | Release Notes row 97: layout fix, overlap-aware assertions, write-through fsync, Host-fatal critical-service constructor, DI fallback restriction, `TEMPEST_TEST_TIMEOUT_FACTOR`, persistence-root test isolation, stray-artefact cleanup — all items from the plan's own scope text are named as delivered. |
| `WP 17.0B` | **Done** | Row 98: 807 governance files archived, `PROJECT_STATUS.md`/`CONTRIBUTING.md`/`BACKLOG.md` written (101 debt rows triaged: 27 live), health check reduced 16→5+1 checks, branch protection configured. |
| `WP 17.0C` | **Done** | Row 99: `RunningHostFixture` (93 loops replaced — plan said "sixty"), console capture removed from 83 classes, fixture pins converted, six defect-characterisation facts deleted, Stryker configured, coverage/duration published. |
| `WP 17.1A` | **Done** | Row 100: `SqlitePersistenceStore` (WAL, `synchronous=FULL`), `IQueryablePersistenceStore`, instance lock, `Persistence:Backend` switch, performance figures given. ADR-0144. |
| `WP 17.1B` | **Done** | Row 101: one authoritative store, single transaction under one domain-wide lock, rollback machinery deleted, `EngineeringObjectBase` reduced 1,819→821+203 lines, `TD-147` (the Release Blocking row) closed with `TD-142/144/145/146/148`. ADR-0145. |
| `WP 17.2A` | **Done** | Row 102: plugin trust/REST/Licensing frozen to `src/Frozen/` (41+37 files), Microsoft.Extensions.Configuration/Logging adopted, `ISessionPrincipal`, audit rows added. ADR-0146. |
| `WP 17.2B` | **Done** | Row 103: `Tempest.App` → `Tempest.Workspace`/`Tempest.Harness`, `InternalsVisibleTo` removed, dependency-direction tests rewritten. ADR-0101 amended. |
| `WP 17.3A` | **Done** | Row 104: seven-exponent runtime dimension vector, automatic same-dimension conversion, temperature deltas, eight new dimensions, 37 CsCheck property tests. ADR-0147. |
| `WP 17.9.0` | **Done (engineering); release itself not yet published** | Row 106 heading confirms gate figures produced; but `PROJECT_STATUS.md` line 3-4 and Release Notes line 3-5: "awaiting Windows verification by the Product Owner, then merge to `main`, tag and publish" — the tag/publish/merge-to-`main` steps `WP 17.9.0`'s own scope calls for are outstanding. |

**Not started**: every `v0.18.0`, `v0.19.0` and `v1.0.0` WP (`18.0A` onward) — consistent with `PROJECT_STATUS.md`'s "What a user can do today" table, where all five v1.0.0 capabilities are still `○`.

**Two hotfix WPs outside the original plan, plus unplanned work, both already shipped on this branch:**

- **`WP 17.9.1`** (Release Notes row 106) — first-Windows-review hotfixes: `WorkspaceDockingComposer.EnsureCorePanelsPresent` (Project Explorer/Properties panels always present on entering Engineering); `IPrincipalDirectory` resolves identity ids to names; Object Editor shows the Bill of Materials section only on the five mechanical Kinds and retires the "Execute"/"Input (JSON)" section; `SampleSeparationTests` no longer counts project files inside a nested clone. **Not fixed, recorded as `TD-171` for `WP 18.1A`**: "an object created from the palette while another discipline tab is active is not shown where the user is looking" (Release Notes line 106).
- **`WP 17.9.2`** (row 107) — second round: `CommandContext.ProjectId` threading (Ribbon/Palette/input bindings all supply it — directly overlaps `WP 19.0A`'s "project commercial core" scope and the plan's own `TD-76` disposition, "no project context anywhere in the running application," owned by `WP 19.0A`); `MechanicalCreateParentPolicy` (created objects land under the selected container or the open project; a "Not in any project" Explorer node lists orphans); `BracketCalculationWorkbench.AddMaterialAsync` (an engineer's own material record, Draft-then-release) — this pre-empts scope `WP 18.0B` ("Reference tables with structured citation") will need to formalise for every reference type, not just materials. **Not fixed here either: `TD-171`.**
- **"Unplanned"** (row 105) — the command-invocation-draining and SQLite-provider fixes analysed in §3.1 above (`88f36de`, `a7c33cf`, `94bf990`, `1dcf8df`).

**Finding: `TD-171` is used for two different defects across governance documents, an ID collision.** `BACKLOG.md:146` defines `TD-171` as "Three verification models remain (`Core/Verification`, `EngineeringDomain/RequirementsVerification`, `EngineeringAssets/Verification`); collapse deferred to `WP 18.2B`," owner `WP 18.2B` — consistent with `WorkPackages.md:153` ("Collapsing them was deferred from WP 17.1B to WP 18.2B as `TD-171`"). But `docs/releases/v0.17.0/Release Notes.md:106-107` uses the identical id `TD-171` for a distinct, unrelated defect — an object created from the palette while another discipline tab is active is not shown where the user is looking — and assigns it to `WP 18.1A`, not `18.2A`/`18.2B`. This second usage is corroborated as a real, broader defect (not a typo for a different TD number) by this same review's own `docs/reviews/design-freeze-2026-09/evidence/surface-audit.md:168-171`, which documents a "TD-171-class" placement defect independently reproduced in Requirements (there, worse: the created object becomes fully unreachable in its own module's Explorer tree) and calls the identical number "TD-171" throughout its own analysis. Neither `BACKLOG.md` nor `WorkPackages.md` carries a row for the palette/discipline-tab defect under any other id — it exists only in the Release Notes and the other evidence file's prose, on a number the debt register itself has already assigned to something else.

**Search results for the requested cross-reference terms**, run against `docs/releases/v1.0.0/WorkPackages.md` specifically:
- `"discipline-centric"` — **zero matches** in `WorkPackages.md` (or anywhere under live `docs/`); the phrase exists only in archived/worktree copies of `docs/governance/Quality/Product Compliance Audit (2026-08-28).md`, outside this plan.
- `"18.1A"` — appears at lines 10, 109 (its own row) and 211 (the "Closed by a surface" bucket definition).
- `"18.2A"` — appears at lines 107 (referenced from within `WP 18.0A`'s own scope text — "once `WP 18.2A` reproduces the bracket journey"), 110 (its own row), 177 (critical path) and 211.
- `"19.2B"` — appears at lines 11, 132 (its own row) and 211.

---

## 6. Remaining remediation workload

Scope: every WP not yet started per §5.3 — all of `v0.18.0`, `v0.19.0` and `v1.0.0` RC. Risk ratings are this audit's judgement (not stated in `WorkPackages.md`), based on: external-dependency surface, size of the code path touched, whether the WP is on the stated critical path, and whether it is a first-time code path (e.g. an installer/upgrade path with no prior version to compare against).

### `v0.18.0` — Calculation as Document (42 dev-days)

| WP | Effort | What a user gets | Risk | Reason |
|---|---|---|---|---|
| `WP 18.0A` | 12 | Nothing directly yet — the calc-sheet substrate itself (cells, expressions, citations, re-run) | **High** | Largest single WP in the whole programme (12 of 133 days); a frozen expression grammar (`ADR-0148`) written before the parser exists; migrates all six existing calculation definitions to built-in functions; on the critical path — everything from `18.2A` onward depends on this shape being right the first time. |
| `WP 18.0B` | 6 | Materials/fasteners/bearings/standards with a real citation and interpolation, not prose | **Medium** | Migrates four existing seed shapes (`MaterialSeed`, `FastenerSeed`, `BearingSeed`, `ConstantSeed`); citation/pin integrity must hold across a table supersession (stated as "an invariant, not an intention") — a correctness property, not just a data migration. |
| `WP 18.1A` | 6 | No new visible feature; fixes UI staleness/blocking (the mechanism behind `TD-58`, `TD-66`, `TD-90`, `TD-108`, `TD-118`) | **Medium-High** | Deletes 16 cockpit-refresh and 17 explorer-reload call sites across the Desktop read path — a sprawling, cross-cutting refactor of "no blocking UI-thread access," not a localised change; regression surface is every existing view. |
| `WP 18.2A` | 8 | A working calc-sheet editor (cell grid, run history, cell-by-cell diff) replacing the current calculation workspace | **Medium** | On the critical path directly after `18.0A`; deletes the old bracket workbench only once the new surface reproduces the existing journey test, so the risk is bounded by that acceptance check. |
| `WP 18.2B` | 4 | A second principal can independently check and issue a calc sheet | **Medium** | Closes `TD-25` (no compare-and-swap on `RequirementsService`, judged release-blocking in §1.3) and `TD-38`; the document itself flags that "independent" is a workflow constraint, not an authentication guarantee (`WP RC.0C` restates the caveat) — a claim/evidence gap by design, not an accident. |
| `WP 18.3A` | 4 | An actual PDF calc-sheet deliverable, attachable to a project | **Low** | Templated rendering (QuestPDF) over a data model `18.0A`/`18.0B` already defines; the report is regenerated from the run, never edited — a well-bounded, additive task. |
| `WP 18.9.0` | 2 | The `v0.18.0` release itself | **Low** | Process step (physical review, CI runs, tag, publish) once the WPs above are done. |

### `v0.19.0` — Consultancy Seam and Desktop (36 dev-days)

| WP | Effort | What a user gets | Risk | Reason |
|---|---|---|---|---|
| `WP 19.0A` | 6 | Projects carry a client, PO reference, budget and rate card; time and deliverable completion are tracked | **Medium** | Closes `TD-76` (judged release-blocking in §1.3, "no project context anywhere in the running application"); foundational data every downstream `v0.19.0` WP (`19.1A`, `19.1B`) reads. |
| `WP 19.0B` | 2 | No visible change (six speculative disciplines moved to `src/Frozen/`) | **Low** | Pure archival, already proven once at this scale by `WP 17.2A`. |
| `WP 19.1A` | 9 | Marking a deliverable complete can send a real invoice request to Xero or QuickBooks | **High** | Second-largest WP in the programme; two independent external OAuth 2.0 integrations, token custody (DPAPI/keychain), an explicit idempotency-key/retry-state model, and a background polling service — the single largest external-dependency and failure-mode surface in the whole plan, and on the critical path. |
| `WP 19.1B` | 4 | Real utilisation, margin, WIP and DSO figures on the Home cockpit, replacing placeholder KPI cards | **Medium** | Five distinct equations, each needing its own hand-computed fixture proof; correctness depends on `19.0A`/`19.1A` data already being right, so defects upstream surface here as wrong numbers rather than a crash. |
| `WP 19.2A` | 4 | No new visible feature; the 1,577-line `MainWindow` god object (`TD-109`) is decomposed | **Medium** | Internal refactor of the composition root every screen is wired through — low conceptual risk, broad regression surface given `MainWindow`'s centrality. |
| `WP 19.2B` | 6 | A left rail that shows only what actually works; Commercial/Resources/Knowledge/Administration removed rather than shown dimmed | **Medium** | States its own strict six-point behavioural acceptance bar per rail surface ("a surface that fails any of the six is removed from the rail rather than shipped dimmed") — the bar itself is evidence the current surfaces are not confidently expected to clear it first time. |
| `WP 19.3A` | 3 | No visible change; a CI job screenshots every rail entry/tab and fails on overlap | **Low** | Additive tooling layered on an already-proven pattern (`WP 17.0A`'s overlap-aware assertions). |
| `WP 19.9.0` | 2 | The `v0.19.0` release itself | **Low** | Process step. |

### `v1.0.0` Release Candidate (14 dev-days)

| WP | Effort | What a user gets | Risk | Reason |
|---|---|---|---|---|
| `WP RC.0A` | 4 | A real Windows installer with in-place update | **Medium-High** | First time this product handles upgrade-in-place at all: a schema migration on first launch plus a pre-migration backup is a new, unexercised code path with real data-loss potential if either step is wrong; on the critical path. |
| `WP RC.0B` | 3 | No visible change; confidence every built-in calculation matches a published worked example | **Low** | Additive test coverage against already-built functionality. |
| `WP RC.0C` | 2 | A stated security posture document | **Low** | Documentation task; the claims it states (no listener, DPAPI tokens, outbound-HTTPS-only) describe what earlier WPs already build. |
| `WP RC.0D` | 2 | No visible change; five clean CI runs under concurrent load | **Low-Medium** | Given the fragility history in §3 (multiple real intermittent-failure classes found and fixed as late as the day this plan was written), a fresh determinism pass at this scale has a real chance of surfacing another one — which is the point of the WP, not a flaw in it. |
| `WP RC.0E` | 2 | No new feature; a recorded clean-machine walkthrough of all five "What v1.0.0 is" capabilities | **Medium** | Final critical-path gate — the first point every substrate and surface WP's assumptions are checked together, by someone who did not build any of it. |
| `WP RC.0F` | 1 | The `v1.0.0` release itself | **Low** | Process step. |

### Sum

| Release | Remaining dev-days |
|---|---|
| `v0.18.0` | 42 |
| `v0.19.0` | 36 |
| `v1.0.0` RC | 14 |
| **Total remaining** | **92** |

(133 programme total − 41 already done on `v0.17.0` = 92, matching the sum of the three release totals above exactly.)

---

## 7. Governance weight

### 7.1 `archive/docs-2026-09/` — files by type

| Type | Count |
|---|---|
| `.md` | 809 |
| **Total** | **809** |

All 809 files are Markdown; no other file type present. By top-level subfolder: `academy/` 237, `architecture/` 27, `governance/` 39, `releases/` 505 (237+27+39+505=808; the remaining 1 is a file directly at `archive/docs-2026-09/` root, e.g. the "one-paragraph README explaining what they were" `WP 17.0B`'s own scope text calls for). `releases/` dominates the archive — consistent with `WP 17.0B`'s scope of moving "every file under `docs/releases/v0.*/` except each release's `Release Notes.md`" (`WorkPackages.md:85`). This is the 807-file "Live documentation files" reduction the Release Notes quantify (`docs/releases/v0.17.0/Release Notes.md:120`, "1,062 → 257"); the archive itself is the difference, moved with git history intact.

### 7.2 `docs/` (live) — file count

| Type | Count |
|---|---|
| `.md` | 265 |
| `.html` | 1 (`docs/releases/v1.0.0/v1.0.0 Road Map Diagram.html`) |
| **Total** | **266** |

By top-level subfolder: `docs/adr/` 147, `docs/academy/` 61, `docs/releases/` 22, `docs/architecture/` 17, `docs/security/` 5, `docs/governance/` 4, `docs/reviews/` 8 (includes this review's own in-progress evidence set — 1 top-level review document plus 7 files under `evidence/`, this file being one), `docs/design/` 1, `docs/engineering/` 1.

### 7.3 `scripts/governance-healthcheck.ps1` — the five checks

Read in full (501 lines). Per its own header, `WP 17.0B` reduced this script from a larger set (formerly cross-checking Academy Index, Release Register, Documentation Register, `PROJECT_STATUS.md`'s own path/version references, and Interface/Exception/Namespace/Future-Capability/Academy-coverage registers — all now archived) to five checks, all "derived from source and git" rather than from a hand-maintained register:

1. **ADR Register matches `docs/adr/`** (`Test-AdrRegisterMatchesFiles`) — every `ADR-####` id present as a file in `docs/adr/` must have a row in `docs/governance/Architecture/ADR Register.md`, and vice versa; fails on either direction of mismatch.
2. **`VERSION` matches a planned release folder** (`Test-VersionMatchesPlannedRelease`) — the repo-root `VERSION` file's value (with any `-rc.N` suffix stripped) must have a corresponding `docs/releases/v<version>/` directory.
3. **Release folders contain a Release Notes file** (`Test-ReleaseFoldersHaveMandatoryDocs`) — for every `docs/releases/vX.Y.Z/` directory that also has a matching real git tag (`git tag -l "v*"`), a `Release Notes.md` or `ReleaseNotes.md` must exist; an untagged (in-progress) release folder is exempt by design, not flagged.
4. **`docs/adr/`'s file count matches the ADR Register's row count** (`Test-AdrCountMatchesRegister`) — a raw count comparison, independent of check 1's per-id matching.
5. **Markdown lines added must not exceed code lines added** (`Test-MarkdownLinesDoNotExceedCodeLines`) — new at `WP 17.0B`, enforcing `CONTRIBUTING.md`'s PR budget; runs `git diff --numstat` between `HEAD` and its merge-base with `origin/main`/`main`, sums added/deleted lines split by `.md` vs. everything else, and fails only if Markdown-added exceeds code-added. Skips (Warn, not Fail) when not in a git repo, on `main` itself, when no `main` ref resolves, or when there is no diff — each an explicit, named skip reason rather than a silent pass.

Every check is read-only (script never writes inside `-RepoRoot`, enforced by a path-containment guard on `-SummaryPath` at lines 69-92); a check that throws is itself recorded as a `Fail` naming the exception type and message (lines 421-441, the generic-exception-handling behaviour `BACKLOG.md`'s `TD-43` narrative — §1.1 above — says is carried forward unchanged). Exit code is 1 if any check fails, 0 otherwise (Warn does not fail the run).

### 7.4 `CONTRIBUTING.md` length

70 lines (confirmed by direct line count), read in full for §1.4 and §4.4 above. Sections: "The unit of work is a Work Package," "Review," "Release Blocking," "Markdown budget," "Definition of Done," "Branch protection on `main`," "Out of scope for a Work Package PR."

