# TempestOS v0.12.0 — "Desktop Composition & Domain Vocabulary Hardening"

## 1. Executive Summary

`v0.12.0` is TempestOS's second consecutive engineering-hardening
release — no new Engineering Discipline, no new product feature. Its
scope is closing three real, evidence-based gaps found in `WP11.0A
Platform Architecture Review.md` and hardening the release process
itself: `MainWindow`/`EngineeringCockpit` decomposed from two
monolithic composition roots into eighteen focused collaborators
(`ADR-0103`, Finding `A-1`), every live Kind/`Classification`/
`RelationshipKind` vocabulary value now declared exactly once as a
named constant and tracked in a non-enforcing governance register
(`ADR-0105`, Finding `A-6`), and a permanently-exposed false-positive
in `IDiagnosticsProvider`'s own health reporting eliminated by
isolating the one deliberately-always-failing sample module behind
project-reference isolation and a default-excluded discovery marker
(`ADR-0102`). A fourth Work Package reviewed Desktop command/event
wiring after the composition-root decomposition and formalised the
communication rules future collaborators must follow (`ADR-0104`). The
roadmap-predicted `WP 12.2A` (Presentation Strategy Execution) found
its own entire scope already delivered by `v0.11.0`'s own `WP 11.3A`/
`WP 11.3B`, closed by disposition rather than re-work. The release's
own closing Work Package, `WP 12.9.0`, produced this project's first
formal, permanent Engineering Readiness Review model (`ADR-0106`),
replacing the ad hoc, independently-worded sign-off checklist `v0.10.0`
and `v0.11.0` each used with a named, five-category, four-verdict
framework every future release now cites rather than reinvents.

Ten Work Packages delivered this release's own engineering scope
(`WP 12.3A`/`WP 12.3B`, `WP 12.0A`/`WP 12.0B`, `WP 12.4A`/`WP 12.4B`,
`WP 12.1A`/`WP 12.1B`, `WP 12.2A`, `WP 12.9.0`), five new ADRs
(`ADR-0102`–`ADR-0106`), five new standing architecture documents; an
eleventh, `WP 12.9.1`, closed the two genuine governance findings
`WP 12.9.0`'s own first Engineering Readiness Review execution
disclosed (§5, below, updated in place). `WP 12.3A`/`WP 12.3B` were
directly commissioned outside the roadmap's own predicted `12.0`–`12.2`
sequence — the same pattern `v0.11.0`'s own `WP 11.3A`/`WP 11.3B` and
`WP 11.4A`/`WP 11.4B` already established — after tracing the real
production composition path and finding a genuine, previously
undisclosed defect: a deliberately-always-failing fault-injection
module was being discovered and initialised on every real launch of
`Tempest.App`/`Tempest.Desktop`, permanently leaving one module
`ModuleState.Failed` and permanently defeating the one health check
that exists specifically to report a clean platform.

**`WP 12.9.0` itself — this release's own closing sign-off architecture
— was reviewed twice** before being trusted: a read-only architecture
review found two blocking findings (an incomplete ADR Register
reconciliation; an internally contradictory verdict-derivation model
that made one of the four verdicts logically unreachable), both closed
by a same-day follow-up before any commit. The actual `v0.12.0`
Engineering Readiness Review, executed under the corrected model, is
`WP12.9.0 Engineering Release Report.md` — full detail, all six
discipline reviews, and the reconciled Definition of Done there.

## 2. Major Capabilities Added, by Work Package

- **`WP 12.3A`/`WP 12.3B` — Fault Injection & Validation Framework.**
  A new project, `Tempest.Validation` (namespace
  `Tempest.Validation.FaultInjection`), isolates the one
  deliberately-always-failing module (`DuplicateNavigationModule`,
  proving `ModuleLifecycleManager`'s per-module isolation, `ADR-0013`)
  by project reference *and* a default-excluded discovery marker
  (`IFaultInjectionModule`) — neither alone was sufficient. Closes a
  genuine, previously-undisclosed defect: every real
  `Tempest.App`/`Tempest.Desktop` launch now genuinely reaches
  `Running` with zero modules `Failed`, verified directly, not merely
  asserted. `ADR-0102`.
- **`WP 12.0A`/`WP 12.0B` — Desktop Composition Root Decomposition.**
  `ADR-0103` ("Composition Roots Own Collaborators") — a general,
  platform-wide pattern extending `ADR-0009` one layer down.
  `MainWindow` (1,556 → 544 lines) decomposes into nine
  `Tempest.Desktop.Composition` collaborators; `EngineeringCockpit`
  (1,398 → 575 lines) into six per-discipline collaborators. Refactor
  only: zero public API changes, zero behavioural changes, zero new
  Platform Services. Closes `WP11.0A` Finding `A-1` in full.
- **`WP 12.4A`/`WP 12.4B` — Desktop Command & Event Wiring.** A second
  directly-commissioned Work Package reviewing Desktop command/event
  wiring after the `WP 12.0B` decomposition. `ADR-0104`: direct
  delegates remain the default; typed callback interfaces sanctioned
  narrowly (three or more bundled callbacks); a Desktop-local Mediator,
  Command Dispatcher, and Event Dispatcher each explicitly rejected.
  `RibbonObjectActionHandlers`'s own 16 duplicated report-then-refresh
  tails consolidated into one local function.
- **`WP 12.1A`/`WP 12.1B` — Classification & Relationship Vocabulary
  Safety Net.** The roadmap-defined Work Package realising `WP11.0A`
  Finding `A-6`. `ADR-0105`: every live Kind/`Classification`/
  `RelationshipKind` value declared exactly once, as a named constant,
  on its owning class; a new Engineering Vocabulary Register (46
  values, 11 declaring classes); one additive consistency test,
  verified to actually catch a deliberately-reintroduced rogue
  duplicate. Mechanical's own complete absence of declared Kind
  constants closed (all eight); `DigitalThreadGraphModel`'s own
  confirmed cross-layer duplicate (three constants, not one) closed.
  No write-time validation, no runtime registry, no enum, no value
  object introduced anywhere — the open-vocabulary philosophy fully
  preserved.
- **`WP 12.2A` — Presentation Strategy Execution.** Discovery found
  this roadmap-predicted item's entire scope — executing `WP 11.2A`'s
  decision — already delivered, in `v0.11.0`, by `WP 11.3A`/`WP 11.3B`
  (the pair that actually ratified and executed the Desktop & Console
  Presentation Strategy Decision, since `WP 11.2A`'s own label was
  reused for Governance Health-Check Automation). No ADR, no new
  architecture document — neither was justified. Disposed as
  **Delivered by `WP 11.3A`/`WP 11.3B`**, mirroring this project's own
  identical disposition of the sibling `WP 11.1B`/`WP 11.2A`
  renumbering.
- **`WP 12.9.0` — Release Preparation & Engineering Sign-Off.**
  Designs the permanent TempestOS Engineering Readiness Review:
  five readiness categories (Architecture/Implementation/Verification/
  Governance/Release), a written blocking taxonomy (Release Blocking /
  Disclosed Non-Blocking / Pre-Existing Unaffected), a fixed
  four-value verdict vocabulary (`CERTIFIED`; `CERTIFIED WITH ACCEPTED
  TECHNICAL DEBT`; `ACCEPT WITH OBSERVATIONS`; `NOT READY`), executed
  via the six-discipline Programme Review `WP 10.9A`/`WP 11.9.0` each
  already, independently, converged on. `ADR-0106`. A same-day
  architecture review found and closed two blocking findings before
  any commit (full account: `WP12.9.0`'s own retrospective, §8
  Addendum). Performs a direct Phase A repository assessment finding
  one genuine, `v0.12.0`-caused governance gap (the Engineering
  Vocabulary Register never linked from `Governance Index.md`) and one
  stale Architecture Document Register row (corrected in passing).
- **`WP 12.9.1` — Governance Health Check Remediation.** Repairs the
  four genuine `[FAIL]` results `governance-healthcheck.ps1` reported
  against `main`, per `WP 12.9.0`'s own Phase A finding. Academy Index:
  59 missing links restored — the `v0.7.0` tail through all of `v0.12.0`
  (closing the Academy Index gap named in §5, below, and disclosed
  since `WP 11.2A`, larger every release since). Documentation
  Register/`PROJECT_STATUS.md`: bare numbered-Academy-folder shorthand
  fully qualified (a documented, deliberate `governance-healthcheck.ps1`
  scope limit, confirmed by direct code reading, left unmodified);
  three hyphen-hard-wrap filename defects rewrapped; `docs/diagrams/`,
  `docs/roadmap/`, `src/Plugins/` each given a tracked, honestly-
  disclosed `README.md` marker, closing a `WP 11.2A`-disclosed,
  previously-deferred gap (git cannot track an empty directory);
  `docs/releases/v0.2.0` renamed `v0.2.0.md` (found to be a misnamed
  tracked file, not an empty directory as `WP 11.2A`'s own finding had
  conflated it). Governance Index: the Engineering Vocabulary Register
  link added, closing the one genuine, `v0.12.0`-caused governance gap
  named in §5, below. `scripts/governance-healthcheck.ps1` left
  unmodified throughout. Documentation/governance only; zero `src/`/
  `tests/` files touched. `governance-healthcheck.ps1` re-run clean: 7
  passed, 1 warned (informational only), 0 failed.

## 3. Testing Summary

**Independently re-verified from source on `main`, post-merge, both
configurations, by `WP 12.9.0` itself:**

| Configuration | `Tempest.Core.Tests` | `Tempest.Desktop.Tests` | Combined | Failed | Skipped |
|---|---|---|---|---|---|
| Debug | 2034/2034 | 221/221 | **2,255/2,255** | 0 | 0 |
| Release | 2034/2034 | 221/221 | **2,255/2,255** | 0 | 0 |

Both builds: **0 Warnings / 0 Errors**, both configurations. The
2228/2228 (`v0.11.0`) → 2,255/2,255 net +27 reflects new
characterization tests added before each refactor-only Work Package
(`WP 12.0B`: 12; `WP 12.1B`: 3; `WP 12.4B`: 3) plus the new
`EngineeringVocabularyConsistencyTests` (`WP 12.1B`: 4) and
`FaultInjectionModuleDiscoveryTests` (`WP 12.3B`) — every net addition
is a real, confirmed-by-direct-search coverage gap closed before its
own refactor, not padding.

## 4. Known Technical Debt

Full detail: `Technical Debt Register.md` (48 tracked items —
**unchanged in count since `v0.11.0`'s own close**; no `v0.12.0` Work
Package added a new tracked item). The three most release-relevant
Open items, all pre-existing and already disclosed at `v0.11.0`'s own
sign-off: `TD-42` (`new-release.ps1`'s `git tag`/`git push` steps
still carry no exit-code verification); `TD-43`
(`governance-healthcheck.ps1`'s generic exception handler still loses
the failing check's own identity); `TD-45` (branch protection still
documented, not configured, in GitHub).

## 5. Deferred / Open Findings

- ~~**Engineering Vocabulary Register not linked from `Governance
  Index.md`**~~ — **Closed, `WP 12.9.1`.** First disclosed this
  release, by `WP 12.9.0`; the link was added, `governance-
  healthcheck.ps1`'s Governance Index check re-confirmed `[PASS]`.
- ~~**Academy Index gap**~~ — **Closed, `WP 12.9.1`.** `WP 11.2A`'s own
  disclosed, previously-unaddressed gap (grown every release since);
  59 missing links restored, spanning the `v0.7.0` tail through all of
  `v0.12.0`; `governance-healthcheck.ps1`'s Academy Index check
  re-confirmed `[PASS]`.
- **`WorkspaceShell` Stage 5** (further test/feature trimming,
  `WP 11.3A`) — reconfirmed still deliberately deferred, `WP 12.2A`;
  trigger condition ("a real, demonstrated cost problem") still unmet.
- **`FCR-0084`** (a typed callback interface for
  `WorkspaceViewCoordinator`'s three bundled callbacks, `WP 12.4B`
  review follow-up) — deliberately deferred engineering judgement, not
  urgency; `83 → 84` Future Capability Register total.
- **Branch protection, CI enforcement gaps** (`TD-42`/`TD-43`/`TD-45`)
  — all pre-existing, all still open, all disclosed above (§4).

## 6. Statistics

- **ADR count: 100 → 105** (`ADR-0102`–`ADR-0106`).
- **Architecture document count: 20 → 25** (`Fault Injection &
  Validation Architecture.md`, `Desktop Composition Architecture.md`,
  `Desktop Command & Event Wiring Architecture.md`, `Classification &
  Relationship Vocabulary Safety Net Architecture.md`, `Engineering
  Readiness Review Architecture.md`).
- **Test count: 2,228/2,228 (`v0.11.0`) → 2,255/2,255** — net +27, see
  §3.
- **Technical Debt Register: 48 tracked items, unchanged in count.**
- **Future Capability Register: 83 → 84** (`FCR-0084`).
- **Academy `03 Work Packages` retrospectives: +11** — every `v0.12.0`
  Work Package has one, closing the exact gap `v0.11.0`'s own Release
  Notes named as an immediate `v0.12.0` fast-follow (§5, `v0.11.0`
  Release Notes).
- **Eleven Work Packages** completed (`WP 12.3A`/`WP 12.3B`, `WP 12.0A`/
  `WP 12.0B`, `WP 12.4A`/`WP 12.4B`, `WP 12.1A`/`WP 12.1B`, `WP 12.2A`,
  `WP 12.9.0`, `WP 12.9.1`).

## 7. Final Engineering Assessment

Every hard engineering gate — clean build (both configurations, 0
Warnings/0 Errors), full test suite (both configurations, 2,255/2,255,
independently re-run from source, not carried forward from any Work
Package's own claim), and governance-register health
(`governance-healthcheck.ps1` run directly: **7 passed, 1 warned, 0
failed of 8**, `WP 12.9.1` — the one remaining `[WARN]` is the same
informational, already-accepted exception `WP 11.2A`'s own design
names) — passed on independent, from-source re-verification. `WP
12.9.0`'s own first Engineering Readiness Review execution found one
Release Blocking finding (real CI not yet run against the pre-tag
commit, since `main` had not yet been pushed) and two Disclosed,
Non-Blocking governance findings (both closed by `WP 12.9.1`, above).
The Engineering Readiness Review's own re-execution against this
updated state — `WP 12.9.2` — is `WP12.9.2 Engineering Release
Report.md`; its own verdict is this release's own current, authoritative
one, superseding `WP12.9.0 Engineering Release Report.md`'s, which
remains as the historical record of the first execution, not corrected
or deleted.

## Related Documents

`docs/releases/v0.12.0/WorkPackages.md`; every `WP12.0A`–`WP12.9.0`
document under `docs/academy/03 Work Packages/`; `docs/architecture/Engineering
Readiness Review Architecture.md`; `ADR-0106`; `Technical Debt
Register.md`; `Future Capability Register.md`; `Release Register.md`;
`PROJECT_STATUS.md`.
