# Engineering Evolution Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Engineering Evolution Register |
| **Purpose** | The chronological timeline of TempestOS's own engineering discipline evolving — when major process shifts happened, not just when features shipped. Complements the Decision Register (what was decided) with when it happened, in sequence. |
| **Scope** | Git history from the earliest commit through the most recent, focused on process/discipline milestones rather than individual features. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `git log` (full history, both pre- and post-Claude). |
| **Review Frequency** | Updated at each major milestone (a new release, a new governance discipline adopted). |
| **Last Reviewed** | 2026-07-27 (WP 5.0A). |
| **Related Documents** | `Decision Register.md`; `Release Register.md`; `Repository Metrics Register.md`. |
| **Related ADRs** | None directly. |
| **Related Academy Articles** | `docs/academy/00 Introduction/00-welcome-to-the-academy.md` ("Where This History Begins"). |
| **Coverage Status** | Partial. |

---

## Coverage Note

Marked **Partial**: the pre-Claude era (2026-07-15 through 2026-07-21) is
represented only by its 5 commits' own messages — no retrospective,
design document, or first-hand account of that period's own engineering
process exists to draw on. Everything from `7514b9d` onward is
**Verified** directly from commit history and retrospectives.

## Timeline

| Date | Milestone | Evidence |
|---|---|---|
| 2026-07-15 | Earliest recorded commits ("Build 0008.1 - Core Platform Bootstrap", "Foundation Bootstrap complete - Build 0008.3" ×2) | Commits `1ca4939`, `74e3dcc`, `0eddbe6` |
| 2026-07-15 – 2026-07-21 | **Unknown** — a gap of 6 days between the Build 0008 commits and the next recorded commit; no evidence explains this period | Git history |
| 2026-07-21 | Python prototype archived; C# established as the canonical implementation | Commit `337c9cd` |
| 2026-07-21 | v0.1.0 Repository Stabilisation | Commit `e4fcd58` |
| 2026-07-21 | **First Claude-authored commit** — repository cleanup, `.gitignore`, wiring `Tempest.Core.Tests` into the solution | Commit `7514b9d` (first with a `Co-Authored-By: Claude` trailer) |
| 2026-07-21 – 2026-07-22 | Runtime Foundation Work Packages begin: WP 2.1 (Module Discovery) through WP 2.6 (Logging) | Commits `407e109` through `ca92ed2` |
| 2026-07-22 | Academy adopted as a maintained documentation asset | Commit `b45f544` |
| 2026-07-22 | Engineering Governance adopted as the project's constitution | Commit `c8f7175` |
| 2026-07-22 | Atomic Phase Principle named as a distinct Engineering Principle | Commits `a18edad`, `e834fea` |
| 2026-07-22 | Engineering Glossary introduced | Commit `2ea9c3a` |
| 2026-07-22 – 2026-07-23 | Runtime Host designed (WP 2.7) then implemented (WP 2.7B) | Commits `615d1ab` through `b6916b4` |
| 2026-07-23 | v0.3.0 released — "Runtime Foundation Complete" | Commit `f2176d7` |
| 2026-07-23 | v0.4.0 release plan and architecture baseline established | Commit `7974130` |
| 2026-07-23 | Rejected Designs Log adopted as a permanent engineering rule | Commit `466334c` |
| 2026-07-23 – 2026-07-25 | Platform Services milestone: WP 4.0 through WP 4.5 (Platform Contracts, Module SDK, Plugin Manifest, Sample Module, Dependency Injection for Discovered Modules, Event Bus, Background Services) | Commits `cf58c7e` through `c460aaf` |
| 2026-07-25 | Academy & Documentation Baseline Audit (`WP 4.4F`) — first formal, full-history documentation audit | Commit `c9aa96e` |
| 2026-07-25 | Governance Register Baseline (`WP 4.5A`), the first complete governance register suite | `256afc8` |
| 2026-07-25 | Platform Foundation Closeout (`WP 4.5B`): formally closes the Foundation phase, adds `PROJECT_STATUS.md`, `Platform Foundation Completion Report.md`, `Contributor Learning Path.md`, `Engineering Lifecycle.md`, `Future Work Package Guidelines.md`, and extends Engineering Governance with Repository Organisation (§11) and Naming Conventions (§12) | `eb19605` |
| 2026-07-27 | `v0.4.0` "Platform Foundation" released — rescoped to the Foundation-phase scope, merged into `main`, tagged `v0.4.0` | `2c88c07` (prep), `5802b92` (merge) |
| 2026-07-27 | Navigation Framework Architecture (`WP 5.0A`), the first Work Package of the `v0.5.0` "Developer Experience" release: `ADR-0031`/`ADR-0032`, `Navigation Framework Architecture.md`, `RD-0030`–`RD-0033`, and the `v0.4.0`→`v0.5.0` Developer Experience Work Package renumbering | `c3f9246` |
| 2026-07-27 | **This Work Package** — Navigation Framework Implementation (`WP 5.0B`): `Tempest.Core.Navigation` (`NavigationItem`, `INavigationProvider`/`NavigationService`, `NavigationRequestedEvent`, exception hierarchy), three new `Tempest.Samples` reference modules, registered in `TempestHost`'s existing Platform Services Registered phase, 45 new tests (400 total) | This commit |

## Observed Pattern

Governance discipline was **not** present from the repository's absolute
beginning — it was **adopted deliberately, in the first week of
Claude-developed history** (Academy: 2026-07-22; Engineering Governance:
2026-07-22; Rejected Designs Log: 2026-07-23), then **applied
retroactively and consistently** to every Work Package from that point
forward. This governance baseline (`WP 4.5A`) is the next deliberate step
in that same pattern: formalising registers over material that has, by
this point, already accumulated 43 Claude-authored commits' worth of
disciplined ADRs, retrospectives, and risk tracking.

## Cross-Reference Check

Every commit hash above is cross-checked directly against
`git log --oneline` — no date or hash was reconstructed from memory or
estimated.
