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
| **Last Reviewed** | 2026-07-27 (WP 5.0A, Navigation Framework Architecture). |
| **Related Documents** | `docs/releases/FOUNDATION.md`; `docs/releases/v0.5.0/ReleasePlan.md`, `WorkPackages.md`; `docs/releases/v0.4.0/WorkPackages.md`, `CHANGELOG.md`, `Risks.md`, `ReleaseChecklist.md`; `Feature Register.md`; `Validation Register.md`. |
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
| v0.4.0 | **Released, 2026-07-27** | `docs/releases/v0.4.0.md`; `docs/releases/v0.4.0/Release Notes.md` | **"Platform Foundation"** — rescoped, during Release Engineering, from its own original, broader plan down to exactly the Foundation-phase scope: Platform Contracts, Module SDK, Plugin Manifest, Sample Module, Event Bus, Background Services, the Governance Register Baseline, and the Foundation phase closeout (`WP 4.5B`). Navigation, Command Framework, Diagnostics, and Developer Experience were part of this release's original scope list but are formally deferred to the next milestone, not shipped here (see `docs/releases/v0.4.0/ReleasePlan.md`'s "Scope" section and `Feature Register.md`). 355 tests, 0 failures. |
| v0.5.0 | **In progress, not yet tagged** | `docs/releases/v0.5.0/` (ReleasePlan, WorkPackages); root `VERSION` file still reads `0.4.0` | **"Developer Experience"** — renumbers `v0.4.0`'s own deferred `WP 4.6A`–`WP 4.9` as `WP 5.0A`–`WP 5.3` (see `ReleasePlan.md`'s "A Note on Renumbering"). `WP 5.0A` (Navigation Framework Architecture) complete — `ADR-0031`, `ADR-0032`, `Navigation Framework Architecture.md`; `WP 5.0B` onward not started. |

**Total: 5 versions referenced — 3 confirmed released (v0.1.0, v0.3.0,
v0.4.0), 1 Unknown status (v0.2.0), 1 in progress (v0.5.0).**

## Cross-Reference Check

v0.4.0's shipped-vs-deferred scope above is cross-checked directly
against `Feature Register.md`'s own "Complete" vs. "Deferred to next
milestone" rows — consistent, no discrepancy. v0.5.0's own scope is
cross-checked against `docs/releases/v0.5.0/WorkPackages.md` directly —
consistent. The v0.2.0 gap is cross-checked against `Documentation
Register.md`'s identical finding — both registers disclose the same
Unknown independently, from different angles, and agree.
