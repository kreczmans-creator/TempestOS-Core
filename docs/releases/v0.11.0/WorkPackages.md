# TempestOS v0.11.0 — Work Packages

## Status

**In progress.** `feature/v0.11.0-v1-architecture` was cut from `main` at
the `v0.10.0` tag. `WP 11.0A` (Platform Architecture & Code Quality
Review) and `WP 11.0B` (v1.0 Architecture Roadmap & Release Planning)
are this release's own first two Work Packages — a review-and-planning
phase only, no implementation — following this project's own standing
discipline (`FOUNDATION.md` §1: architecture and planning precede
implementation for anything non-trivial). This release's own scope and
sequencing are fully described by `WP11.0B Architecture Roadmap.md`;
this document tracks status only, per this project's established
`WorkPackages.md` convention.

## Work Packages

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP 11.0A` | Platform Architecture & Code Quality Review — independent architecture, engineering, and release-readiness review of the complete codebase as of `v0.10.0`. No code modified. | Review only | **Complete** |
| `WP 11.0B` | v1.0 Architecture Roadmap & Release Planning — categorises `WP 11.0A`'s findings, scopes the Work Packages and release sequence from `v0.11.0` through `v1.0.0`. No code, ADR, or architecture modified. | Planning only | **Complete** |
| `WP 11.1A` | CI/CD Pipeline Standup — closes `WP11.0A` finding `R-1` (no build/test automation exists). | Implementation | Not started |
| `WP 11.1B` | Governance Currency Pass & Health-Check Tooling — closes `WP11.0A` finding `A-5` and reprioritises `FCR-0005` (Governance Register Health-Check Tooling, deferred since `v0.7.0`, now independently found a seventh time). | Implementation | Not started |
| `WP 11.2A` | Desktop & Console Presentation Strategy Decision — resolves `WP11.0A` finding `A-2` (two independently maintained presentation stacks with no documented product decision). Sets the scope of `WP 12.2A` and whether the conditional `v0.13.0` release is needed. | Architecture / Decision | Not started |
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
`docs/releases/v0.10.0/Release Notes.md` (the immediately preceding
release); `PROJECT_STATUS.md`; `docs/governance/Future Capability
Register.md`.
