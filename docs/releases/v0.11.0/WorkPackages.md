# TempestOS v0.11.0 — Work Packages

## Status

**In progress.** `feature/v0.11.0-v1-architecture` was cut from `main` at
the `v0.10.0` tag. `WP 11.0A` (Platform Architecture & Code Quality
Review), `WP 11.0B` (v1.0 Architecture Roadmap & Release Planning),
`WP 11.1A` (Continuous Integration & Build Verification), `WP 11.1B`
(Branch Protection & Engineering Workflow Hardening), `WP 11.2A`
(Governance Health-Check Automation), and `WP 11.3A` (Presentation
Strategy Review & Platform Consolidation) are this release's own first
six Work Packages, each following this project's own standing discipline
(`FOUNDATION.md` §1: architecture and planning precede implementation
for anything non-trivial) — `WP 11.0A`/`WP 11.0B`/`WP 11.3A` review-and-
planning only; `WP 11.1A`/`WP 11.1B`/`WP 11.2A` implementation, scoped
entirely to CI/release-engineering/governance-tooling infrastructure,
with no production code, ADR, or architecture change in any of the
three. This release's own scope and sequencing are fully described by
`WP11.0B Architecture Roadmap.md`; this document tracks status only, per
this project's established `WorkPackages.md` convention. TempestOS's
first CI pipeline (`WP 11.1A`), its complete surrounding engineering
workflow (`WP 11.1B`), its first automated governance health check
(`WP 11.2A`), and a complete, evidence-based presentation-layer review
(`WP 11.3A`) all now exist.

**Disclosed divergence from `WP11.0B Architecture Roadmap.md`'s own
prediction — twice, in the same release:** that roadmap scoped
`WP 11.1B` as "Governance Currency Pass & Health-Check Tooling" and
`WP 11.2A` as "Desktop & Console Presentation Strategy Decision." The
Product Owner instead commissioned `WP 11.1B` as Branch Protection &
Engineering Workflow Hardening and `WP 11.2A` as Governance Health-Check
Automation — the latter is, in substance, the roadmap's own originally-
predicted `WP 11.1B` scope, delivered one Work Package later under a
different number. The roadmap's own §7 named itself "a recommendation,
not an approval"; this is that distinction exercised a second time, not
a defect. The Desktop & Console Presentation Strategy Decision (finding
`A-2`) was predicted for two Work Package slots without being
commissioned under either, before finally being commissioned directly as
`WP 11.3A` — see that Work Package's own row, below.

## Work Packages

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP 11.0A` | Platform Architecture & Code Quality Review — independent architecture, engineering, and release-readiness review of the complete codebase as of `v0.10.0`. No code modified. | Review only | **Complete** |
| `WP 11.0B` | v1.0 Architecture Roadmap & Release Planning — categorises `WP 11.0A`'s findings, scopes the Work Packages and release sequence from `v0.11.0` through `v1.0.0`. No code, ADR, or architecture modified. | Planning only | **Complete** |
| `WP 11.1A` | Continuous Integration & Build Verification — `.github/workflows/ci.yml`, TempestOS's first CI pipeline. Closes `WP11.0A` finding `R-1`. Builds Debug and Release (warnings promoted to errors at the CI step only) and runs the complete test suite against both, on every push/pull request/manual dispatch. Verified locally: 0 Warnings/0 Errors both configurations, 2,266/2,266 tests passing. GitHub-hosted execution disclosed unverified pending the first real push. No production code, ADR, or architecture modified. See `WP11.1A Implementation Report.md`. | Implementation | **Complete** |
| `WP 11.1B` | Branch Protection & Engineering Workflow Hardening — the complete engineering workflow surrounding `WP 11.1A`'s CI pipeline: branching strategy, pull request expectations (`.github/pull_request_template.md`, `.github/CODEOWNERS`), merge/release requirements, required CI status checks (documented for branch protection, not configured), the version-tagging workflow (new `.github/workflows/release.yml` — independent re-verification + durable GitHub Release publishing), a first-ever hotfix workflow and rollback procedure, and a release-candidate process. Two genuine pre-existing defects found and fixed in `scripts/new-release.ps1` (stale release-notes path; missing `TreatWarningsAsErrors` parity with CI). One genuine governance deviation found and disclosed, not silently fixed: the `v0.10.0` tag points to the feature branch's pre-merge commit, not to `main`. No production code, ADR, or architecture modified; no GitHub repository setting configured. See `WP11.1B Engineering Workflow.md`. | Implementation | **Complete** |
| `WP 11.2A` | Governance Health-Check Automation — `scripts/governance-healthcheck.ps1`, delivering `FCR-0005`. A standalone, read-only PowerShell script with no production-code dependency; eight checks (ADR Register, Academy Index, Release Register, Documentation Register, `PROJECT_STATUS.md`, `VERSION`, release-folder mandatory docs, Governance Index orphans), Pass/Warn/Fail per check, non-zero exit on any Fail. Three genuine tool defects found and fixed during development (multi-line path-extraction crash, git-unavailable crash, PowerShell single-item-count sharp edge). The fixed tool's first live run found two genuine, real, previously-undisclosed governance findings (Academy Index "Work Package Walkthroughs" stops at `WP 7.3A`, missing ~50 retrospectives; four references to directories git cannot track because they are empty) — both disclosed, neither fixed within this Work Package's own scope. Wired into `.github/workflows/ci.yml` as a new job; deliberately not yet a required `CI Gate` check. Failure-detection verified against an isolated, temporary fixture, never the real repository. No production code, ADR, or architecture modified; no GitHub repository setting configured. See `WP11.2A Governance Health-Check Tool.md`. | Implementation | **Complete** |
| `WP 11.3A` | Presentation Strategy Review & Platform Consolidation — the Desktop & Console Presentation Strategy Decision (`WP11.0A` finding `A-2`), finally commissioned directly. Complete source review of `Tempest.App`/`Tempest.Desktop`, startup flow, dependency graph, duplication, documentation assumptions, release-process implications. Finds the real duplication confined to the console-*rendering* slice of `Tempest.App` (`WorkspaceShell`, `TempestShell`), not the shared Workspace domain layer `Tempest.Desktop` depends on. `TempestShell` found provably dead (unreachable since `ADR-0068`, `WP 8.1A`, `v0.8.0` — three releases). A `Program.cs` comment's claim that `WP 10.0B` formally decided "Desktop primary, console diagnostic" could not be found in any `WP 10.0B` document — reported Unverified. `README.md`/`Contributor Learning Path.md` found describing an architecture ~6 releases stale, omitting `Tempest.Desktop` entirely; `ci.yml`/`release.yml` found packaging both presentation layers as symmetric release artifacts. Recommends (5-stage, minimum-disruption roadmap, none executed here): retire `TempestShell`; ratify `Tempest.App`/`WorkspaceShell` as an Internal Engineering Harness via a new ADR; correct documentation and release packaging. Review only — no code, ADR, or architecture modified; no repository restructuring. See `WP11.3A Presentation Strategy Review.md`. | Review only | **Complete** |
| — | Execution of `WP11.3A`'s own five-stage roadmap — genuinely not started, pending Product Owner acceptance of its recommendation; unscoped to a specific future Work Package number. | Implementation | Not started |
| — | Governance Currency Pass & Health-Check Tooling (`FCR-0005`) — the roadmap's own originally-scoped `WP 11.1B`; delivered instead, in substance, by `WP 11.2A` above. No longer outstanding. | Implementation | Delivered by `WP 11.2A` |
| `WP 11.9.0` | `v0.11.0` Release Preparation & Engineering Sign-Off | Verification only | Not started |

Further Work Packages (`v0.12.0`, conditionally `v0.13.0`, and `v1.0.0`
itself) are scoped by `WP11.0B Architecture Roadmap.md` §3 but are not
yet approved to begin — each release's own `WorkPackages.md` is created
when that release's branch is cut, per this project's own established
convention (see `docs/releases/v0.8.0/WorkPackages.md`).

## Related Documents

`docs/releases/v0.11.0/WP11.0A Platform Architecture Review.md`;
`docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md` (this release's
own full scope, estimates, dependencies, and release sequence);
`docs/releases/v0.11.0/WP11.1A Implementation Report.md`;
`docs/releases/v0.11.0/WP11.1B Engineering Workflow.md`;
`docs/releases/v0.11.0/WP11.2A Governance Health-Check Tool.md`;
`docs/releases/v0.11.0/WP11.3A Presentation Strategy Review.md`;
`.github/workflows/ci.yml`; `.github/workflows/release.yml`;
`scripts/governance-healthcheck.ps1`;
`docs/academy/06 Engineering Standards/04-continuous-integration.md`;
`docs/academy/06 Engineering Standards/05-release-engineering.md`;
`docs/academy/06 Engineering Standards/06-governance-automation.md`;
`ADR-0068`; `ADR-0092`; `README.md`; `docs/academy/Contributor Learning
Path.md`; `docs/releases/v0.10.0/Release Notes.md` (the immediately
preceding release); `PROJECT_STATUS.md`; `docs/governance/Future
Capability Register.md`.
