# Release Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Release Register |
| **Purpose** | The index of every TempestOS release (tagged or in progress), its scope, and its documentation status. |
| **Scope** | Every version referenced under `docs/releases/` or the root `VERSION` file. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/releases/`; the root `VERSION` file; git commit history. |
| **Review Frequency** | Updated at every release tag, and whenever release-scoped planning documents change materially. |
| **Last Reviewed** | 2026-07-25 (WP 4.5A). |
| **Related Documents** | `docs/releases/FOUNDATION.md`; `docs/releases/v0.4.0/WorkPackages.md`, `CHANGELOG.md`, `Risks.md`, `ReleaseChecklist.md`; `Feature Register.md`; `Validation Register.md`. |
| **Related ADRs** | None directly — releases are delivery milestones, not architectural decisions. |
| **Related Academy Articles** | See `Academy Register.md` for the retrospectives each release's Work Packages produced. |
| **Coverage Status** | Partial. |

---

## Coverage Note

Marked **Partial**, not Complete, because two referenced versions
(`v0.1.0`, `v0.2.0`) have **Unknown** or only partially reconstructable
detail — see each entry below. This is disclosed explicitly rather than
worked around.

## Entries

| Version | Status | Evidence | Detail |
|---|---|---|---|
| v0.1.0 | Released (pre-Claude era) | Commit `e4fcd58`, "v0.1.0 Repository Stabilisation", 2026-07-21 | **Unknown** full scope — this commit is the last of the 5 pre-Claude commits; no `docs/releases/v0.1.0.md` exists to describe it in detail. |
| v0.2.0 | **Unknown** | `docs/releases/v0.2.0/` directory exists but is empty | No release notes, no commit message referencing "v0.2.0" was found in `git log`. **Unknown** whether this version was ever actually released, skipped, or reserved for a purpose not yet realised. See `Documentation Register.md`'s own note on this same gap. |
| v0.3.0 | Released | `docs/releases/v0.3.0.md`; commit `f2176d7`, "Prepare v0.3.0 release" | "Runtime Foundation Complete" — the six platform services (Configuration, Logging, Discovery, Registration, Dependency Injection, Lifecycle) plus the Runtime Host, Composition Root, and startup/shutdown orchestration. 164 tests at this baseline (per `docs/releases/v0.4.0/Testing.md`'s own stated starting point). |
| v0.4.0 | **In progress, not yet tagged** | `docs/releases/v0.4.0/` (ReleasePlan, Architecture, WorkPackages, CHANGELOG, Risks, Testing, ReleaseChecklist); root `VERSION` file still reads `0.3.0` | "Platform Services" milestone — Platform Contracts, Module SDK, Plugin Manifest, Sample Module, Event Bus, Background Services complete; Navigation, Command Framework, Diagnostics, Developer Experience remain (see `Feature Register.md`). |

**Total: 4 versions referenced — 2 confirmed released (v0.1.0, v0.3.0), 1
Unknown status (v0.2.0), 1 in progress (v0.4.0).**

## Cross-Reference Check

v0.4.0's remaining scope above is cross-checked directly against
`Feature Register.md`'s own "Not Started (planned)" rows — consistent, no
discrepancy. The v0.2.0 gap is cross-checked against
`Documentation Register.md`'s identical finding — both registers disclose
the same Unknown independently, from different angles, and agree.
