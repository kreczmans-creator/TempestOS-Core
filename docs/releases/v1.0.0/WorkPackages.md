# v1.0.0 — Work Packages (Proposed)

## Status

**Proposed, not approved.** This table is the §5/§6 output of `v1.0.0
Release Candidate Audit.md` (2026-09-04) in this folder, laid out in
this project's `WorkPackages.md` convention so the folder satisfies
`governance-healthcheck.ps1`'s mandatory-documentation check and so a
future approving Work Package has a table to adopt, renumber, or strike.
No Work Package below has begun. Numbers follow `WP11.0B Architecture
Roadmap.md`'s own `WP RC.0A` precedent for the v1.0.0 release itself and
extend the `WP 16.x`/`WP 17.x` series for the two releases in front of
it. `VERSION` remains `0.15.0`.

## Governing scope decision

Every row assumes **`WP11.0B` §1's definition of v1.0** (single-user or
small-trusted-team, locally-trusted desktop; six disciplines; no
third-party plugins; REST loopback-only) unless `WP 16.0A` decides
otherwise. Rows marked *Conditional* enter the plan only by that
decision.

## Release `v0.16.0` — v1.0 Readiness Hygiene

| Work Package | Scope | Type | Closes | Status |
|---|---|---|---|---|
| `WP 16.0A` | v1.0 Scope & Support Decision Record — confirm or widen the `WP11.0B` §1 definition; rule on Companion, third-party plugins, REST beyond loopback, `AT-10` activation, and the platform support matrix (`TD-116`) | Decision | Audit §2, M1, M9 | Not started |
| `WP 16.0B` | Merge `WP 15.2A` (`feature/wp15.2a-td120-persistence-root-cleanup`); dispose of the Companion branch (`claude/tempestos-companion-mobile-ubznt3`) — merge with `TD-57`/`TD-58` renumbering and `FCR-0092` registration, or formally reject with a decision record | Governance/Integration | `TD-120`, `TD-82` | Not started |
| `WP 16.1A` | Enforcement — configure branch protection on `main` (required `CI Gate`, CODEOWNERS review, merge-commit-only); promote `governance-health-check` to a required check | Configuration | `TD-45`, `FCR-0005` residual | Not started |
| `WP 16.1B` | Health-check extension — re-derive Interface, Exception, Namespace (at minimum) from `src/` so `TD-57`'s registers are machine-audited; harden the script's exception path | Implementation | `TD-57` root cause, `TD-43` | Not started |
| `WP 16.2A` | Register & status currency remediation — the six `TD-57` registers, `Feature Register.md`, `Product Roadmap.md` (Phase 5.5), `PROJECT_STATUS.md` lower sections (`DNB-7`), Repository Metrics | Documentation/Governance | `TD-57`, `DNB-7` | Not started |
| `WP 16.2B` | Academy retrospective backfill — `v0.11.0`'s ten, `WP 15.1A`/`15.1B`, pre-`v0.14.0` programme commits; Academy Register annotations | Documentation | `WP-Z3` §12 gap | Not started |
| `WP 16.3A` | Engineering object state schema versioning — architecture and ADR (version stamp, migration seam, string-valued enums) | Architecture | `TD-87` | Not started |
| `WP 16.3B` | Engineering object state schema versioning — implementation | Implementation | `TD-87` | Not started |
| `WP 16.4A` | Test determinism closure — `TD-34` console-redirect race, the retained fixed wait, real image-decode assertion, `MainWindow`-level resize and dialog-modality tests | Implementation (tests) | `TD-34`, `TD-119`, `TD-100`, `TD-83` | Not started |
| `WP 16.4B` | Durability & loopback hygiene — crash-window orphan detection, orphaned attachment collection, bounded `AsyncKeyedLock`, DI duplicate-registration guard, gated OpenAPI route | Implementation | `TD-67`, `TD-68`, `TD-69`, `TD-97`, `TD-62` | Not started |
| `WP 16.5A` | Accessibility baseline — modal dialog behaviour, `AutomationProperties.Name` pass, live-region announcements, Digital Thread keyboard operability | Implementation | `TD-65` | Not started |
| `WP 16.5B` | Linux/X11 launch — apply the `WP 16.0A` decision (pin relaxation with advisory posture, Avalonia upgrade, or Windows/macOS-only statement in `PHYSICAL_REVIEW.md` and Release Notes) | Implementation or Documentation | `TD-116` | Not started |
| `WP 16.9.0` | `v0.16.0` Engineering Readiness Review and **Product Approval certification** (§9 verdict) — the first since `v0.12.0` | Verification/Release | Audit finding 2 | Not started |

## Release `v0.17.0` — Product Completion (**Conditional** on `WP 16.0A`)

One Architecture/Implementation pair per item `WP 16.0A` pulls into v1.0
from the audit's §5.2 (C1 REST auth/TLS; C2 plugin-enablement
preconditions; C3 product-spine modules; C4 per-discipline surfaces; C5
viewer completion; C6 command-framework completion; C7 Desktop polish;
C8 shell/async residuals). Not tabulated until selected; skipped
entirely under the unmodified `WP11.0B` definition.

## Release `v1.0.0` — General Availability

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP RC.0A` | Physical review on Windows per `PHYSICAL_REVIEW.md` §7, executed and recorded, findings triaged into the registers | Verification | Not started |
| `WP RC.0B` | v1.0 Release Readiness Review — full five-category ERR (`ADR-0106`), six disciplines, multiple consecutive full-suite runs, every register cross-checked; `VERSION` → `1.0.0`; Release Notes; root CHANGELOG | Verification | Not started |
| — | Product Approval; tag `v1.0.0`; `release.yml` publish | Release | Not started |

## Related Documents

`docs/releases/v1.0.0/v1.0.0 Release Candidate Audit.md`; `docs/releases/
v0.11.0/WP11.0B Architecture Roadmap.md`; `docs/governance/Quality/
Technical Debt Register.md`; `docs/governance/Future Capability
Register.md`; `PROJECT_STATUS.md`.
