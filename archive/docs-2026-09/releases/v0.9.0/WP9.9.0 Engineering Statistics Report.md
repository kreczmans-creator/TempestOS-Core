# WP 9.9.0 — Release Preparation & Product Baseline — Engineering Statistics Report

## Purpose

The complete, independently re-derived engineering statistics baseline
for `v0.9.0`, every figure verified directly against the repository
(`grep`, `find`, `dotnet build`/`dotnet test`), not carried forward from
any prior register's own claim.

## Headline Metrics

| Metric | v0.8.0 (baseline) | v0.9.0 | Change | Verification Method |
|---|---|---|---|---|
| Automated tests | 1631 | 2026 | +395 | `dotnet test`, both configurations, four clean-rebuild runs |
| Test failures | 0 | 0 | — | Same |
| Build warnings | 0 | 0 | — | `dotnet build`, both configurations, clean rebuild |
| Build errors | 0 | 0 | — | Same |
| ADRs | 79 | 91 | +12 | `find docs/adr -iname "*.md"` unique `ADR-\d{4}` count; zero gaps, zero non-Accepted status lines |
| Rejected Designs | 45 | 45 | — | `grep -oE "RD-\d{4}"` unique count, `docs/architecture/Rejected Designs.md` |
| Academy articles | 116 | 124 | +8 | `find docs/academy -iname "*.md"` |
| Governance registers | 27 | 27 | — | Unchanged this release (register *count*; underlying `docs/governance/` file count remains a disclosed 35, not 27/32 — see Release Readiness Report Finding 2) |
| Architecture documents | 20 | 20 | — | `find docs/architecture -maxdepth 1 -type f` |
| Platform services catalogued | 27 | 27 | — | `Platform Services Register.md` entry count — Workspace/Engineering Domain/every real Discipline are correctly not Platform Services by this platform's own taxonomy |
| Modules (production) | 22 | 34 | +12 | `ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule` (part of the 2026/2026 passing suite) |
| Hosted services (production) | 2 | 2 | — | Unchanged this release |
| Plugins (production) | 0 | 0 | — | Deliberate scope decision, unchanged |
| Public interfaces (`src/Tempest.Core/`) | 163 | 168 | +5 | `grep -rhoP "^public interface \w+(<[^>]+>)?"` |
| DI registrations (named) | 41 | 42 | +1 | `TempestHost.cs` Phase 6, direct inspection (`WP 9.1A`'s own `IRequirementValidationService`; zero further additions across `WP 9.0B`/`WP 9.2A`/`WP 9.4A`/`WP 9.3A`/`WP 9.5A` — every Workspace-layer type is composition-root-constructed, never DI-registered) |
| DI raw call sites | 43 | 44 | +1 | Same |
| Custom exception types | 69 | 72 | +3 | `grep -rhoP "^public (sealed )?class \w+Exception"` |
| Technical Debt Register items | 25 | 33 | +8 | Register Entries table, direct count — `TD-26` through `TD-33`, one per Work Package |
| Disclosed trade-offs (AT-) | 17 | 17 | — | Register Entries table, direct count |
| Future Capability Register entries | 38 | 62 | +24 | Register section headings, direct count — `FCR-0039` through `FCR-0062` |

## Engineering Discipline — Dedicated Breakdown

Not applicable to any prior release; new this cycle. `v0.9.0` is the
first release to wire real Engineering Disciplines into the Engineering
Workspace, built entirely atop the already-real Engineering Domain
(`WP 8.2C`) and Engineering Core frameworks (`v0.7.0`).

| Metric | Value | Verification Method |
|---|---|---|
| Real Engineering Disciplines wired into the Workspace | 6 (Mechanical, Requirements, Calculations, Documents, Verification, Manufacturing) | Direct count of `*WorkspaceRegistration.Register` calls in `Program.cs` |
| Work Packages requiring zero Domain-layer (`Tempest.Core.EngineeringDomain`) changes | 4 of 7 (`WP 9.2A`, `WP 9.4A`, `WP 9.3A`, `WP 9.5A`) | Each Work Package's own disclosed finding, reconfirmed by `git diff 28e41e8 -- src/Tempest.Core/EngineeringDomain` |
| Work Packages requiring zero `Tempest.Core.Verification`/`Tempest.Core.Calculations` Framework changes | 2 (`WP 9.3A`, and `WP 9.5A` reusing `WP 9.3A`'s own command unmodified) | Direct inspection |
| New ADRs this programme | 12 (`ADR-0080`–`ADR-0091`) | Direct file count |
| Kind-keyed Workspace providers registered (`IProjectExplorerNodeProvider`/`IWorkspaceViewFactory`/`IPropertyFacetProvider` triples) | 6 native + 2 disclosed cross-Work-Package reuses (`"WorkInstruction"`→Documents', `"Inspection"`→Verification's own provider instances) | Direct inspection of all six `*WorkspaceRegistration.Register` methods |
| Real Domain Kinds given Workspace presence this release | 11 (`Assembly`/`SubAssembly`/`Part`/`Component`/`Configuration`/`Baseline`/`Release` — Mechanical family, `WP 9.0A`/`WP 9.0B`; `Requirement`/`RequirementGroup`/`RequirementCollection` — `WP 9.1A`; `Calculation`/`CalculationSet` — `WP 9.2A`; `Document`/`Drawing`/`CadModel` — `WP 9.4A`; `VerificationActivity` — `WP 9.3A`; `ManufacturingOperation`/`WorkInstruction`/`Inspection` — `WP 9.5A`) | Direct count against each Work Package's own Implementation Report |
| Relationship kinds exercised this release | 7 (`"references"`, `"verifiedBy"`, `"calculatedBy"`, `"basedOnCalculation"`, `"manufacturedBy"`, `"documentedBy"`, `"groupedUnder"`) | All pre-existing in `RelationshipKindCategoryMap` since `WP 8.2A`/`WP 8.2B` — **zero new relationship kinds introduced across all seven Work Packages** |
| Engineering Cockpit KPI card sets added | 5 dedicated (`RequirementsKpiCards`/`CalculationsKpiCards`/`DocumentsKpiCards`/`VerificationKpiCards`/`ManufacturingKpiCards`) + Mechanical's own `WP 8.1C`-era generic reads | Direct source inspection |

## Per-Work-Package Breakdown (`v0.9.0`)

| Work Package | Type | New Production Files | New Tests | ADR(s) |
|---|---|---|---|---|
| `WP 9.0A` — Mechanical Product Structure | Implementation | 16 | 64 | `ADR-0080`–`ADR-0082` |
| `WP 9.0B` — Product Configuration & BOM Management | Implementation | 5 | 43 | `ADR-0083` |
| `WP 9.1A` — Requirements Management Workspace | Implementation | 29 | 70 | `ADR-0084`, `ADR-0085` |
| `WP 9.2A` — Engineering Calculations Workspace | Implementation | 21 | 57 | `ADR-0086`, `ADR-0087` |
| `WP 9.4A` — Engineering Documents Workspace | Implementation | 17 | 57 | `ADR-0088` |
| `WP 9.3A` — Verification Management Workspace | Implementation | 17 | 50 | `ADR-0089`, `ADR-0090` |
| `WP 9.5A` — Manufacturing Workspace | Implementation | 16 | 54 | `ADR-0091` |

**Total new production files this release: 121.** All seven Work
Packages were implementation Work Packages — unlike `v0.8.0` (which
included three architecture/contract/UX-only Work Packages contributing
zero code), `v0.9.0`'s own controlling instructions named a real
Engineering Discipline for every single Work Package. **Total new
tests: 395** (64+43+70+57+57+50+54), exactly matching the headline
1631 → 2026 change.

## Commits This Release (`v0.8.0` → `v0.9.0`, so far)

3 commits on `main` (no feature branch exists — see Release Readiness
Report Finding, Repository Verification §1): `71b49ea` (`WP 9.0A`–`WP 9.1A`
Mechanical Foundation consolidation), `7d6b493` (`WP 9.1B` Development
Baseline Consolidation), `447c368` (`WP 9.1B` follow-up fix). `WP 9.2A`,
`WP 9.4A`, `WP 9.3A`, `WP 9.5A`, and this Work Package's own work remain
uncommitted, pending explicit Product Owner instruction to commit — per
this Work Package's own "Do NOT merge… Do NOT push" constraint, no
commit action is taken by this review either. Total diff against the
`v0.8.0` merge commit (`28e41e8`): 143 files changed, +14,055/−268 lines.

## Build Verification

4/4 projects (`Tempest.Core`, `Tempest.App`, `Tempest.Samples`,
`Tempest.Core.Tests`), both Debug and Release, each from a fully-removed
`bin`/`obj` tree. 0 warnings, 0 errors, every configuration, plus
per-project Release builds of `Tempest.App`/`Tempest.Samples`
(`--no-incremental`).

| Build | Configuration | Time Elapsed |
|---|---|---|
| Full chain (`src/TempestOS.slnx`) | Debug | ~19s |
| Full chain (`src/TempestOS.slnx`) | Release | ~12s |
| `Tempest.App` (per-project) | Release | ~5s |
| `Tempest.Samples` (per-project) | Release | ~3s |

## Test Verification

4 full-suite runs (2 Debug, 2 Release — the second Release run
reproducing `scripts/new-release.ps1`'s own exact invocation), each
2026/2026 passing, 0 failures, 0 skipped:

| Run | Configuration | Duration (`dotnet test` reported) |
|---|---|---|
| 1 | Debug (clean rebuild) | 2m 27s |
| 2 | Debug | 2m 22s |
| 3 | Release (clean rebuild) | 2m 19s |
| 4 | Release (`scripts/new-release.ps1` invocation) | 2m 19s |

One dedicated scoped run against this release's own six new/extended
Workspace namespaces plus `EngineeringCockpit`: 516/516 passing. One
additional targeted run against `ConsoleLogSinkTests`, the specific test
class carrying this project's own previously-disclosed `Console.Out`-
capture flake: 6/6 passing — no flake observed this pass.

**Zero flaky tests identified across all six total runs this Work
Package performed.**

## Security Reviews Performed

**Seven dedicated Security Reviews this release — one per Work Package,
a full recovery from `v0.8.0`'s own disclosed "zero dedicated Security
Reviews" gap.** `WP9.0A`–`WP9.5A Security Review Report.md`, each
independently confirming zero Release Blocking findings; re-verified at
the release level by this Work Package's own Release Readiness Report
§18.

## Contributors

1 (repository owner; all commits co-authored by Claude), unchanged from
every prior release.

## Related Documents

`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report.md`;
`docs/releases/v0.9.0/ReleaseNotes.md`; `PROJECT_STATUS.md` (Repository
Metrics table, the same source this report's own figures were
cross-checked against).
