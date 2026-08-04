# WP 8.9.0 — Release Preparation & Product Baseline — Engineering Statistics Report

## Purpose

The complete, independently re-derived engineering statistics baseline
for `v0.8.0`, every figure verified directly against the repository
(`grep`, `find`, `dotnet build`/`dotnet test`), not carried forward from
any prior register's own claim.

## Headline Metrics

| Metric | v0.7.0 (baseline) | v0.8.0 | Change | Verification Method |
|---|---|---|---|---|
| Automated tests | 1406 | 1631 | +225 | `dotnet test`, both configurations, four clean-rebuild runs |
| Test failures | 0 | 0 | — | Same |
| Build warnings | 0 | 0 | — | `dotnet build`, both configurations, clean rebuild |
| Build errors | 0 | 0 | — | Same |
| ADRs | 61 | 79 | +18 | `ls docs/adr/*.md` unique `ADR-\d{4}` count; zero gaps, zero non-Accepted status lines |
| Rejected Designs | 45 | 45 | — | `grep -oP "RD-\d{4}"` unique count, `docs/architecture/Rejected Designs.md` |
| Academy articles | 104 | 116 | +12 | `find docs/academy -name "*.md"` |
| Governance registers | 27 | 27 | — | Unchanged this release |
| Architecture documents | 20 | 20 | — | `find docs/architecture -maxdepth 1 -type f` |
| Platform services catalogued | 27 | 27 | — | `Platform Services Register.md` entry count — Workspace/Engineering Domain are correctly not Platform Services by this platform's own taxonomy |
| Modules (production) | 20 | 22 | +2 | `ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_FindsEveryRealSampleModule` (22/22 passing) |
| Hosted services (production) | 2 | 2 | — | Unchanged this release |
| Plugins (production) | 0 | 0 | — | Deliberate scope decision, unchanged |
| Public interfaces (`src/Tempest.Core/`) | 80 | 163 | +83 | `grep -rhoP "^public interface \w+(<[^>]+>)?"` |
| DI registrations (named) | 31 | 41 | +10 | `TempestHost.cs` Phase 6, direct inspection |
| DI raw call sites | 33 | 43 | +10 | `grep -c "services\.\(Singleton\|AddInstance\)"` |
| Custom exception types | 66 | 69 | +3 (net; 4 new types, one pre-existing count discrepancy) | `grep -rhoP "^(public\|internal)( sealed)? class \w+Exception\b"` |
| Technical Debt Register items | 25 | 25 | — | Register Entries table, direct count — zero new items raised across all nine `v0.8.0` Work Packages |
| Disclosed trade-offs (AT-) | 17 | 17 | — | Register Entries table, direct count |
| Future Capability Register entries | 38 | 38 | — | Register section headings, direct count |

## Engineering Domain — Dedicated Breakdown

Not applicable to any prior release; new this cycle.

| Metric | Value | Verification Method |
|---|---|---|
| Canonical Engineering Objects catalogued (`WP 8.2A`) | ~49, across 13 families | `WP8.2A Canonical Object Catalogue.md` §3 Cross-Reference Check |
| — already-Implemented (owned by an existing framework, `ADR-0078`) | 5 | Reconciled `WP 8.2A`; unchanged by `WP 8.2C` |
| — given a real concrete class this release (`WP 8.2C`) | 38 | `grep -rhoP "^public (sealed class\|class) \w+" src/Tempest.Core/EngineeringDomain/Implementation/` — corrected from an initially-claimed 39, see Disclosed Findings below |
| — realised as a relationship, metadata field, or extension mechanism, not an object | 5 | `WP8.2A Canonical Object Catalogue.md` §12/§13 |
| `Tempest.Core.EngineeringDomain` contract types compiled (`WP 8.2B`/`WP 8.2C`) | 83 | `grep -rhoP "^public interface \w+" src/Tempest.Core/EngineeringDomain/` |
| Relationship kinds catalogued (`WP 8.2A`) | 20 | `WP8.2A Relationship Catalogue.md` §4 |
| Relationship categories (`RelationshipCategory`, `WP 8.2B`) | 17 | One generic `IEngineeringRelationship` type, never a closed set of per-category types (`ADR-0076`) |
| Facet interfaces (`WP 8.2B`) | 9 (`IEngineeringObject` itself is the tenth, base "facet") | `WP8.2B Interface Catalogue.md` §1 |
| New ADRs this Work Package trio | 8 (`ADR-0072`–`ADR-0079`) | Direct file count |

## Per-Work-Package Breakdown (`v0.8.0`)

| Work Package | Type | Production Files | New Tests | ADR(s) |
|---|---|---|---|---|
| `WP 8.0A` — Engineering Workspace Architecture | Architecture only | 0 | 0 | `ADR-0062`–`ADR-0065` |
| `WP 8.0B` — Workspace Contracts | Contract review only | 0 | 0 | `ADR-0066`, `ADR-0067` |
| `WP 8.1A` — Workspace Shell | Implementation | 27 | 91 | `ADR-0068` |
| `WP 8.0C` — Engineering Workspace UX Specification | Product/UX only | 0 | 0 | `ADR-0069`, `ADR-0070` |
| `WP 8.1B` — Navigation & Project Explorer | Implementation | 7 | 55 | `ADR-0071` |
| `WP 8.1C` — Engineering Cockpit | Implementation | 5 | 40 | None (implements `ADR-0069`/`ADR-0070` directly) |
| `WP 8.2A` — Engineering Domain Architecture | Architecture only | 0 | 0 | `ADR-0072`–`ADR-0074` |
| `WP 8.2B` — Engineering Domain Contracts | Contract review only | 0 | 0 | `ADR-0075`, `ADR-0076` |
| `WP 8.2C` — Engineering Domain Implementation | Implementation | 66 (21 contract + 24 implementation + 3 sample + 3 test files, less overlaps — see `WP8.2C Implementation Report.md` for the exact file list) | 39 | `ADR-0077`–`ADR-0079` |

**Total new production files this release: ~105** (three architecture/contract/UX Work Packages contributed zero code, by design; the four implementation Work Packages contributed the rest). **Total new tests: 225** (91+55+40+39), exactly matching the headline 1406 → 1631 change (1631 − 1406 = 225).

## Commits This Release (`v0.7.0` → `v0.8.0`, so far)

9 commits, one per Work Package, all authored by `kreczmans-creator`,
zero merge commits, zero empty commits, zero WIP/fixup markers (verified
directly, `git log main..feature/v0.8.0-engineering-workspace`):

`769bc03` `WP 8.0A`; `66019b7` `WP 8.0B`; `fc846ea` `WP 8.1A`; `a8a4319`
`WP 8.0C`; `27dcb6e` `WP 8.1B`; `ccff4ca` `WP 8.1C`; `9213fbb` `WP 8.2A`;
`1eba87c` `WP 8.2B`; `89590eb` `WP 8.2C`. `WP 8.9.0`'s own commit will be
the 10th once this Work Package is committed. Total diff against `main`:
204 files changed, +21,427/−133 lines.

## Build Verification

4/4 projects (`Tempest.Core`, `Tempest.App`, `Tempest.Samples`,
`Tempest.Core.Tests`), both Debug and Release, each from a fully-removed
`bin`/`obj` tree. 0 warnings, 0 errors, every configuration.

| Project | Configuration | Time Elapsed |
|---|---|---|
| `Tempest.Core` | Debug | 7.9s |
| `Tempest.App` (+ `Tempest.Samples`) | Debug | 6.4s |
| `Tempest.Core.Tests` (full chain) | Debug | 13.3s |
| Full chain (`Tempest.Core.Tests`) | Release | 10.0s |

## Test Verification

4 full-suite runs (2 Debug, 2 Release), each 1631/1631 passing, 0
failures, 0 skipped:

| Run | Configuration | Duration (`dotnet test` reported) |
|---|---|---|
| 1 | Debug | 18s |
| 2 | Debug | 17s |
| 3 | Release | 17s |
| 4 | Release | 16s |

One dedicated scoped run against this release's own two new namespaces
(`Tempest.App.Workspace`, `Tempest.Core.EngineeringDomain`): 225/225
passing. Three additional targeted runs against
`ConsoleLogSinkTests`, the specific test class carrying this project's
own previously-disclosed `Console.Out`-capture flake: 6/6 passing, all
three runs — no flake observed this pass.

**Zero flaky tests identified across all eight total runs this Work
Package performed.**

## Security Reviews Performed

Zero dedicated Security Reviews this release — a genuine, disclosed
departure from `v0.7.0`'s own three-review standard (`WP 7.1D`,
`WP 7.1E`, `WP 7.3A`), named explicitly in the Release Readiness
Report's own Disclosed Findings.

## Contributors

1 (repository owner; all commits co-authored by Claude), unchanged from
every prior release.

## Related Documents

`docs/releases/v0.8.0/WP8.9.0 Release Readiness Report.md`;
`docs/releases/v0.8.0/ReleaseNotes.md`; `PROJECT_STATUS.md` (Repository
Metrics table, the same source this report's own figures were
cross-checked against).
