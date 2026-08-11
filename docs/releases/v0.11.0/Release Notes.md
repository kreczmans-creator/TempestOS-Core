# TempestOS v0.11.0 — "Release Engineering & Architecture Governance"

## 1. Executive Summary

`v0.11.0` is TempestOS's first **Release Engineering & Architecture
Governance** release. It adds no new Engineering Discipline and no
product feature — the first release since `v0.4.0` to add zero new
production `.cs` files. Its entire scope is making the platform's own
engineering process trustworthy at the point six real Engineering
Disciplines and a full graphical Desktop application already exist:
TempestOS's first CI pipeline (`WP 11.1A`), its first complete,
documented engineering workflow — branching, PR, release-candidate,
hotfix, rollback (`WP 11.1B`) — its first automated governance
health-check tool (`WP 11.2A`, delivering `FCR-0005` after eight
recurrences of the drift pattern it exists to prevent), and a fully
reviewed and implemented presentation-strategy decision that finally
resolves `WP11.0A` finding `A-2` (`WP 11.3A`/`WP 11.3B`) —
`Tempest.Desktop` is confirmed the shipped application, `Tempest.App`/
`WorkspaceShell` is formally ratified (`ADR-0101`) as TempestOS's
Internal Engineering Harness, and `TempestShell` — provably dead since
`ADR-0068`/`WP 8.1A` (`v0.8.0`), three releases — is retired.

Eight Work Packages (`WP 11.0A` through `WP 11.9.0`) delivered this
release. A twice-disclosed divergence from `WP11.0B Architecture
Roadmap.md`'s own predicted sequencing is recorded in full in
`WorkPackages.md`'s own Status section — not a defect, the same
"recommendation, not approval" distinction that roadmap named for
itself, exercised twice.

**`WP 11.9.0` itself** — this release's own closing sign-off — was
executed as a six-discipline Engineering Programme (Chief Architect,
Principal Engineer, Workflow Engineer, QA Lead, Technical Author,
Product Manager), each performing an independent review, reconciled
into one Programme Review. Full detail, all six reports, and the
reconciled Definition of Done: `WP11.9.0 Engineering Release Report.md`.

## 2. Major Capabilities Added, by Work Package

- **`WP 11.0A` — Platform Architecture & Code Quality Review.**
  Independent review of the complete `v0.10.0` codebase; ten findings
  (`A-1`–`A-9`, `R-1`). No code modified.
- **`WP 11.0B` — v1.0 Architecture Roadmap & Release Planning.** Turned
  `WP 11.0A`'s findings into a scoped, estimated Work Package sequence
  from `v0.11.0` through `v1.0.0`.
- **`WP 11.1A` — Continuous Integration & Build Verification.**
  `.github/workflows/ci.yml`, closing `WP11.0A` finding `R-1`. Builds
  Debug and Release (warnings promoted to errors at the CI step only),
  runs the full test suite against both, on every push/PR/manual
  dispatch.
- **`WP 11.1B` — Branch Protection & Engineering Workflow Hardening.**
  The complete engineering workflow surrounding `WP 11.1A`'s pipeline:
  branching strategy, PR expectations (`CODEOWNERS`, PR template),
  merge/release requirements, required CI status checks (documented,
  not yet configured in GitHub), a new `.github/workflows/release.yml`,
  TempestOS's first hotfix/rollback procedure, and a release-candidate
  process. Two genuine pre-existing defects found and fixed in
  `scripts/new-release.ps1` (stale release-notes path; missing
  `TreatWarningsAsErrors` parity with CI). One genuine governance
  deviation found and disclosed, not silently fixed: the `v0.10.0` tag
  points to the feature branch's pre-merge commit, not `main`.
- **`WP 11.2A` — Governance Health-Check Automation.**
  `scripts/governance-healthcheck.ps1` delivers `FCR-0005` — eight
  automated checks, Pass/Warn/Fail, wired into CI as a non-gating job.
  Three genuine tool defects found and fixed during development. Its
  first live run found two genuine, previously-undisclosed governance
  findings (Academy Index's own "Work Package Walkthroughs" section
  stopping at `WP 7.3A`; four documented-but-untracked-by-git
  directories) — both disclosed, neither fixed within that Work
  Package's own scope, both still open at this release's close.
- **`WP 11.3A` — Presentation Strategy Review & Platform Consolidation.**
  Resolved `WP11.0A` finding `A-2` after being predicted for two Work
  Package slots without landing in either. Found the real duplication
  confined to `TempestShell` (provably dead three releases), not the
  shared Workspace domain layer `Tempest.Desktop` depends on. Five-stage
  minimum-disruption roadmap recommended; Stages 1–4 approved, Stage 5
  explicitly deferred.
- **`WP 11.3B` — Presentation Strategy Implementation.** Executed
  Stages 1–4: `TempestShell`/`IPage`/`PlaceholderPage` retired (`git
  rm`, history preserved); `ADR-0101` authored; `README.md`,
  `Contributor Learning Path.md`, `Platform Service Map.md`, `Shell &
  Composition Framework Architecture.md` corrected; three governance
  registers narrowly corrected; `ci.yml`/`release.yml` now package
  `Tempest.Desktop` and `Tempest.App` as two separate, clearly-labelled
  artifacts.
- **`WP 11.9.0` — `v0.11.0` Release Preparation & Engineering
  Sign-Off.** This release's own closing verification pass, run as a
  six-discipline Engineering Programme. Independently reconfirmed every
  hard gate from a clean build (not carried forward from any prior
  claim); found and corrected a factual error in `Future Capability
  Register.md` (`FCR-0005` still read "Identified, not started" despite
  having shipped); found and registered four genuine new technical debt
  items (`TD-42`–`TD-45`), two of them previously-undisclosed defects in
  this release's own new release-tooling scripts; found five governance
  registers drifted stale relative to this release's own seven prior
  Work Packages and corrected each. Full detail: `WP11.9.0 Engineering
  Release Report.md`.

## 3. Testing Summary

**Independently re-verified from a fully clean build (`bin`/`obj`
wiped, not merely `dotnet clean`), both configurations, by `WP 11.9.0`
itself:**

| Configuration | `Tempest.Core.Tests` | `Tempest.Desktop.Tests` | Combined | Failed | Skipped |
|---|---|---|---|---|---|
| Debug | 2026/2026 | 202/202 | **2228/2228** | 0 | 0 |
| Release | 2026/2026 | 202/202 | **2228/2228** | 0 | 0 |

Both builds: **0 Warnings / 0 Errors**, both configurations. The
2266→2228 drop from `v0.10.0` (−38) is fully and exactly reconciled by
the QA Lead's own independent re-derivation against the two deleted
test files' own raw source (`TempestShellTests.cs` = 29 cases,
`PlaceholderPageTests.cs` = 9 cases, 29 + 9 = 38) — not a regression.
Zero executable-code lines changed anywhere in the release outside that
deletion, independently confirmed twice (Chief Architect and QA Lead,
separately).

**CI pipeline status**: `feature/v0.11.0-v1-architecture` has never been
pushed (`git ls-remote --heads origin` returns only `main`) — every
claim this release makes about CI execution remains `Inferred` from
local reproduction, never `Verified` against real GitHub-hosted
infrastructure, for any of this release's own eight Work Packages. This
is the single highest-leverage open item carried into `v0.12.0` (see
`TD-44`).

## 4. Known Technical Debt

Full detail: `Technical Debt Register.md` (45 tracked items, 38 Open,
6 Resolved, 1 Partially resolved). Four items added this release:

- **`TD-42`** — `scripts/new-release.ps1`'s `git tag`/`git push` steps
  have no exit-code verification; a failure is silently swallowed and
  still reports `RELEASE SUCCESSFUL`. Found by independent QA
  re-verification, empirically reproduced. This script has never been
  used for a real release.
- **`TD-43`** — `scripts/governance-healthcheck.ps1`'s generic exception
  handler loses the failing check's own identity on an empty-register
  or link-less-index input. Fails safely today (correct non-zero exit);
  diagnostically weak.
- **`TD-44`** — CI has never executed on real GitHub-hosted
  infrastructure for this entire release.
- **`TD-45`** — Branch protection is documented, not configured in
  GitHub; the PR/CI/CODEOWNERS apparatus this release designs is
  currently voluntary, not mechanically enforced.

Every item carried forward from `v0.10.0` (`TD-41`, `TD-38`, `TD-31`,
`TD-34`, and 37 others) is unchanged — this release touched no
Engineering Domain or Discipline code.

## 5. Deferred / Open Findings

- **Academy retrospective coverage**: zero of this release's own eight
  Work Packages has a `03 Work Packages` retrospective article. Three
  (`WP 11.1A`/`WP 11.1B`/`WP 11.2A`) have substantive equivalent
  coverage via new `06 Engineering Standards` articles; four (`WP
  11.0A`/`WP 11.0B`/`WP 11.3A`/`WP 11.3B`) have none. Assessed
  non-blocking, per this project's own `v0.10.0`-era precedent for a
  comparable finding; recommended as an immediate `v0.12.0` fast-follow.
- **Academy Index gap** (`WP 11.2A` finding, still open): "Work Package
  Walkthroughs" stops at `WP 7.3A`, ~50 real retrospectives unlinked.
- **Four documented-but-untracked-by-git directories** (`WP 11.2A`
  finding, still open).
- **`WorkspaceShell` Stage 5** (further trimming, `WP 11.3A`) —
  deliberately deferred, pending demonstrated need.
- **The `v0.10.0` git tag points to a pre-merge feature-branch commit,
  not `main`** — permanently uncorrectable per this project's own
  "a pushed tag is never moved" rule; disclosed for the historical
  record.

## 6. Statistics

- **49 files changed** across the release's own seven implementation/
  review Work Packages: 5,194 insertions(+), 951 deletions(-).
  `WP 11.9.0` itself additionally touches five governance-register
  files and adds this document plus its own Engineering Release Report.
- **Zero new production `.cs` files** — net `src/`/`tests/` line count
  *shrank* from `TempestShell`'s removal.
- **ADR count: 99 → 100** (`ADR-0101`).
- **Test count: 2266/2266 (`v0.10.0`) → 2228/2228** — reconciled exactly,
  see §3.
- **Technical Debt Register: 41 → 45 tracked items** (`TD-42`–`TD-45`
  added this release); **34 → 38 Open**.
- **Eight Work Packages** completed (`WP 11.0A`–`WP 11.9.0`).

## 7. Final Engineering Assessment

Every hard engineering gate — clean build (both configurations, 0
Warnings/0 Errors), full test suite (both configurations, 2228/2228,
independently re-run from source, not carried forward), architecture
compliance (`ADR-0101` honoured, dependency direction unchanged, all
three named public contracts byte-for-byte unmodified), and process
hygiene (zero `TODO`/skipped tests/suppressed warnings/untracked
cruft) — passed on independent, from-source re-verification, not on any
prior claim. `WorkspaceShell` and `Tempest.Desktop` both confirmed to
launch and run cleanly (console piped-stdin start-to-shutdown cycle,
exit 0; process launch with full module/service registration and
default navigation observed, no exception) — full interactive,
mouse-driven UI click-through (the depth `WP 10.9A` performed for
`v0.10.0`) was **not** performed this Work Package; no mouse-automation
tool was available in this session, and this gap is disclosed here
explicitly rather than silently narrowed to match the prior release's
own reported depth. Given this release changes no UI-rendering code
whatsoever (§3 — every diff line outside the `TempestShell` deletion is
a comment), the residual risk this leaves is assessed low, but it is a
real, named gap against precedent, not an oversight to leave
undisclosed.

Five governance registers were found drifted stale relative to this
release's own prior seven Work Packages and corrected within this
Work Package itself, including one outright factual error
(`FCR-0005`). Two genuine, previously-undisclosed defects were found in
this release's own new release-engineering tooling by independent QA
re-verification — both now formally tracked (`TD-42`, `TD-43`), neither
release-blocking today since neither script/tool is yet load-bearing
for a real release action this Work Package itself takes.

**See `WP11.9.0 Engineering Release Report.md` for the complete
Programme Review, the six independent discipline reports it reconciles,
and the final release recommendation.**

## Related Documents

`docs/releases/v0.11.0/WorkPackages.md`; every `WP11.0A`–`WP11.9.0`
document under `docs/releases/v0.11.0/`; `docs/releases/v0.11.0/WP11.0B
Architecture Roadmap.md`; `Technical Debt Register.md`; `Future
Capability Register.md`; `Release Register.md`; `PROJECT_STATUS.md`.
