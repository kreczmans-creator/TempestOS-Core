# WP 7.4.0 — Release Preparation & Product Baseline — Engineering Statistics Report

## Purpose

The complete, independently re-derived engineering statistics baseline
for `v0.7.0`, every figure verified directly against the repository
(`grep`, `find`, `dotnet test`), not carried forward from any prior
register's own claim.

## Headline Metrics

| Metric | v0.6.0 (baseline) | v0.7.0 | Change | Verification Method |
|---|---|---|---|---|
| Automated tests | 1016 | 1406 | +390 | `dotnet test`, both configurations, four clean-rebuild runs |
| Test failures | 0 | 0 | — | Same |
| Build warnings | 0 | 0 | — | `dotnet build`, both configurations |
| Build errors | 0 | 0 | — | Same |
| ADRs | 52 | 61 | +9 | `ls docs/adr/*.md \| wc -l` |
| Rejected Designs | 45 | 45 | — | `grep -oP "RD-\d{4}"` unique count |
| Academy articles | 86 | 104 | +18 | `find` per subfolder, summed |
| Governance registers | 27 | 27 | — | Unchanged this release |
| Architecture documents | 20 | 20 | — | `find docs/architecture -maxdepth 1 -type f` |
| Platform services catalogued | 26 | 27 | +1 | `Platform Services Register.md` entry count |
| Modules (production) | 15 | 20 | +5 | `ClockModuleDiscoveryTests` (7/7 passing) |
| Hosted services (production) | 2 | 2 | — | Unchanged this release |
| Plugins (production) | 0 | 0 | — | Deliberate scope decision, unchanged |
| Public interfaces (`src/Tempest.Core/`) | 64 | 80 | +16 | `grep -rhoP "^public interface \w+(<[^>]+>)?"` |
| DI registrations (named) | 26 | 31 | +5 | `TempestHost.cs` Phase 6, direct inspection |
| DI raw call sites | 28 | 33 | +5 | `grep -n "services\.\(Singleton\|AddInstance\)"` |
| Custom exception types | 52 | 66 | +14 | `grep -rhoP "^public (sealed \|abstract )?class \w+Exception"` |
| Technical Debt Register items | 24 | 25 | +1 | Register Entries table, direct count |
| Disclosed trade-offs (AT-) | 16 | 17 | +1 | Register Entries table, direct count |
| Future Capability Register entries | 33 | 38 | +5 | Register section headings, direct count |

## Per-Framework Breakdown (v0.7.0 New Capability)

| Framework | Namespace | Production Files | New Tests | ADR(s) |
|---|---|---|---|---|
| Engineering Data Model | `Tempest.Core.EngineeringData` | 13 | 36 | ADR-0053 |
| Units & Quantities | `Tempest.Core.UnitsAndQuantities` | 20 | 67 | ADR-0054 |
| Materials | `Tempest.Core.Materials` | 14 | 55 | ADR-0055 |
| Calculations | `Tempest.Core.Calculations` | 17 | 52 | ADR-0056 |
| Verification | `Tempest.Core.Verification` | 9 | 49 | ADR-0057 |
| Requirements | `Tempest.Core.Requirements` | 20 | 119 (+ 4 Host, + 8 sample integration = 131) | ADR-0058–ADR-0061 |

**Total new framework production files: 93** (13+20+14+17+9+20). **Total
new framework tests: 390** (36+67+55+52+49+131), exactly matching the
headline 1016 → 1406 change — confirming no test was added outside the
six frameworks' own scope this release (sample-module integration and
Host-registration tests are counted within each framework's own total
above).

## Commits This Release (`v0.6.0` → `v0.7.0`, so far)

16 commits: `v0.6.0` release-branch preparation (2: `18e61d5`,
`7709ccb`), merge from `main` (`ac181ce`), `WP 7.0A` (`6a11ae3`),
`WP 7.0B` (`2f8d1ef`), `WP 7.0C` (`36cbc88`), `WP 7.1A` (`4dee45d`),
`WP 7.1B` (`5769901`), `WP 7.1C` (`d9b1ff7`), `WP 7.1D` (`91b6714`),
`WP 7.1E` (`9d0a65c`), `WP 7.1F` (`59db844`), `WP 7.2A` (`31adcfd`),
`WP 7.2B` (`0e069e8`), `WP 7.2C` (`d532648`), `WP 7.3A` (`ab43ccd`).
`WP 7.4.0`'s own commit will be the 17th once this Work Package is
committed.

## Security Reviews Performed

Three dedicated Security Reviews this release (`WP 7.1D`, `WP 7.1E`,
`WP 7.3A`) — the first release in this project's history to include
more than one. Zero Release Blocking findings across all three. Nine
new Technical Debt items disclosed directly from these reviews
(`TD-17`–`TD-25`), plus one new accepted trade-off (`AT-17`).

## Contributors

1 (repository owner; all commits co-authored by Claude), unchanged from
every prior release.

## Related Documents

`docs/releases/v0.7.0/WP7.4.0 Release Readiness Report.md`;
`docs/releases/v0.7.0/ReleaseNotes.md`; `PROJECT_STATUS.md` (Repository
Metrics table, the same source this report's own figures were
cross-checked against).
