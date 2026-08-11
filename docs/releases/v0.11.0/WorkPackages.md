# TempestOS v0.11.0 — Work Packages

## Status

**In progress.** `feature/v0.11.0-v1-architecture` was cut from `main` at
the `v0.10.0` tag. `WP 11.0A` (Platform Architecture & Code Quality
Review), `WP 11.0B` (v1.0 Architecture Roadmap & Release Planning),
`WP 11.1A` (Continuous Integration & Build Verification), and `WP 11.1B`
(Branch Protection & Engineering Workflow Hardening) are this release's
own first four Work Packages, each following this project's own standing
discipline (`FOUNDATION.md` §1: architecture and planning precede
implementation for anything non-trivial) — `WP 11.0A`/`WP 11.0B`
review-and-planning only; `WP 11.1A`/`WP 11.1B` implementation, scoped
entirely to CI/release-engineering infrastructure, with no production
code, ADR, or architecture change in either. This release's own scope
and sequencing are fully described by `WP11.0B Architecture Roadmap.md`;
this document tracks status only, per this project's established
`WorkPackages.md` convention. TempestOS's first CI pipeline
(`.github/workflows/ci.yml`, `WP 11.1A`) and its complete surrounding
engineering workflow (`WP 11.1B`) both now exist — every Work Package
from here forward can cite a real CI run and a defined merge/release
process for its own Build/Test Gate and Merge/Release Gate evidence.

**Disclosed divergence from `WP11.0B Architecture Roadmap.md`'s own
prediction:** that roadmap scoped `WP 11.1B` as "Governance Currency
Pass & Health-Check Tooling." The Product Owner instead commissioned the
real `WP 11.1B` as Branch Protection & Engineering Workflow Hardening —
the roadmap's own §7 named itself "a recommendation, not an approval";
this is that distinction exercised, not a defect. The originally-named
Governance Currency Pass / `FCR-0005` scope remains genuinely not
started, carried forward unscoped to a specific future Work Package
number — see `PROJECT_STATUS.md`, Next Planned Work Package.

## Work Packages

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP 11.0A` | Platform Architecture & Code Quality Review — independent architecture, engineering, and release-readiness review of the complete codebase as of `v0.10.0`. No code modified. | Review only | **Complete** |
| `WP 11.0B` | v1.0 Architecture Roadmap & Release Planning — categorises `WP 11.0A`'s findings, scopes the Work Packages and release sequence from `v0.11.0` through `v1.0.0`. No code, ADR, or architecture modified. | Planning only | **Complete** |
| `WP 11.1A` | Continuous Integration & Build Verification — `.github/workflows/ci.yml`, TempestOS's first CI pipeline. Closes `WP11.0A` finding `R-1`. Builds Debug and Release (warnings promoted to errors at the CI step only) and runs the complete test suite against both, on every push/pull request/manual dispatch. Verified locally: 0 Warnings/0 Errors both configurations, 2,266/2,266 tests passing. GitHub-hosted execution disclosed unverified pending the first real push. No production code, ADR, or architecture modified. See `WP11.1A Implementation Report.md`. | Implementation | **Complete** |
| `WP 11.1B` | Branch Protection & Engineering Workflow Hardening — the complete engineering workflow surrounding `WP 11.1A`'s CI pipeline: branching strategy, pull request expectations (`.github/pull_request_template.md`, `.github/CODEOWNERS`), merge/release requirements, required CI status checks (documented for branch protection, not configured), the version-tagging workflow (new `.github/workflows/release.yml` — independent re-verification + durable GitHub Release publishing), a first-ever hotfix workflow and rollback procedure, and a release-candidate process. Two genuine pre-existing defects found and fixed in `scripts/new-release.ps1` (stale release-notes path; missing `TreatWarningsAsErrors` parity with CI). One genuine governance deviation found and disclosed, not silently fixed: the `v0.10.0` tag points to the feature branch's pre-merge commit, not to `main`. No production code, ADR, or architecture modified; no GitHub repository setting configured. See `WP11.1B Engineering Workflow.md`. | Implementation | **Complete** |
| `WP 11.2A` | Desktop & Console Presentation Strategy Decision — resolves `WP11.0A` finding `A-2` (two independently maintained presentation stacks with no documented product decision). Sets the scope of `WP 12.2A` and whether the conditional `v0.13.0` release is needed. | Architecture / Decision | Not started |
| — | Governance Currency Pass & Health-Check Tooling (`FCR-0005`) — the roadmap's own originally-scoped `WP 11.1B`, superseded by the Work Package actually commissioned above. Genuinely not started; unscoped to a specific future Work Package number. | Implementation | Not started |
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
`.github/workflows/ci.yml`; `.github/workflows/release.yml`;
`docs/academy/06 Engineering Standards/04-continuous-integration.md`;
`docs/academy/06 Engineering Standards/05-release-engineering.md`;
`docs/releases/v0.10.0/Release Notes.md` (the immediately preceding
release); `PROJECT_STATUS.md`; `docs/governance/Future Capability
Register.md`.
