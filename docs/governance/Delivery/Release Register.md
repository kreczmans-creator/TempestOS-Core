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
| **Last Reviewed** | 2026-07-30 (`v0.6.0` Release Engineering) — `v0.5.0` corrected from "in progress" to Released (had gone unreviewed since `WP 5.0D`, missing its own final three Work Packages and tag); `v0.6.0` added as Released; `v0.7.0` added as not yet scoped. |
| **Related Documents** | `docs/releases/FOUNDATION.md`; `docs/releases/v0.5.0/ReleasePlan.md`, `WorkPackages.md`; `docs/releases/v0.6.0/WorkPackages.md`, `WP6.8 Platform Certification Report.md`; `docs/releases/v0.7.0/WorkPackages.md`; `docs/releases/v0.4.0/WorkPackages.md`, `CHANGELOG.md`, `Risks.md`, `ReleaseChecklist.md`; `Feature Register.md`; `Validation Register.md`. |
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
| v0.5.0 | **Released** | `docs/releases/v0.5.0/Release Notes.md`; tag `v0.5.0` | **"Developer Experience"** — renumbers `v0.4.0`'s own deferred `WP 4.6A`–`WP 4.9` as `WP 5.0A`–`WP 5.3` (see `ReleasePlan.md`'s "A Note on Renumbering"), and grew a new `WP 5.0C`/`WP 5.0D` pair beyond that original scope once `WP 5.0C`'s own Repository Investigation confirmed `Tempest.App` still had no composition root, plus `WP 5.0S` (Security Baseline Audit, not originally scoped) and `WP 5.4` (Release Candidate & Engineering Sign-Off). All ten Work Packages (`WP 5.0A`–`WP 5.4`) complete — Navigation, the Shell, the Command Framework, Diagnostics improvements, and Developer Experience tooling all shipped. |
| v0.6.0 | **Released, 2026-07-30** | `docs/releases/v0.6.0/ReleaseNotes.md`; `docs/releases/v0.6.0/WP6.8 Platform Certification Report.md`; tag `v0.6.0` (`99ed285`) | **"Platform Services"** — `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`. Nine Work Packages (`WP 6.0`–`WP 6.8`): Reporting, Permissions & Identity, Notifications, REST API, Settings, Audit, Licensing, Export/Import, and the closing `WP 6.8` Integration Review & Release Certification. Merged into `main` non-fast-forward; six of eight feature Work Packages implemented ahead of their own nominal numeric order, per `Platform Service Implementation Order.md`'s own recommendation. 1016 tests, 0 failures; 52 ADRs, no gaps; zero Release Blocking technical debt (16 tracked items, 13 disclosed trade-offs, all classified Resolved/Accepted/Deferred). |
| v0.7.0 | **Not started, not yet scoped** | `docs/releases/v0.7.0/WorkPackages.md` (candidate items only); `feature/v0.7.0-engineering-foundation` cut from `main` at the `v0.6.0` tag | **"Engineering Foundation"** (working name, from the branch itself — not yet a Product-approved scope). Candidate items drawn from `WP 6.8`'s own disclosed recommendations: the `Runtime`↔`Diagnostics` namespace-reference resolution, a governance-register health check, and revisiting `TD-09`/`TD-10`/`TD-11`/`TD-13`/`TD-14` once a concrete triggering need arises. Requires its own Architecture/Planning/Contract Review phase before any Work Package begins, mirroring `v0.6.0`'s own precedent. |

**Total: 7 versions referenced — 5 confirmed released (v0.1.0, v0.3.0,
v0.4.0, v0.5.0, v0.6.0), 1 Unknown status (v0.2.0), 1 not yet scoped
(v0.7.0).**

## Cross-Reference Check

v0.4.0's shipped-vs-deferred scope above is cross-checked directly
against `Feature Register.md`'s own "Complete" vs. "Deferred to next
milestone" rows — consistent, no discrepancy. v0.5.0's own scope is
cross-checked against `docs/releases/v0.5.0/WorkPackages.md` directly —
consistent. The v0.2.0 gap is cross-checked against `Documentation
Register.md`'s identical finding — both registers disclose the same
Unknown independently, from different angles, and agree.
