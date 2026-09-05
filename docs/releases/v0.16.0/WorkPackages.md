# v0.16.0 — Work Packages

## Status

**Engineering complete; blocked at the Product Approval boundary.** Every
row below is delivered and validated, and `WP 16.4A`'s five-run CI
determinism matrix — the last outstanding engineering gate — was obtained
on the frozen candidate `d7d3f3b`. `VERSION` is `0.16.0`, bumped by
`WP 16.9.0`. Scope, sequencing and acceptance for every row are in
`v0.16.0 Release Plan.md` in this folder.

`D-021`–`D-026` — the six scope decisions every row below cites — were
**ratified by the Product Owner on 2026-09-05** and are entered in
`Decision Register.md`. They were Proposed for the whole of the
engineering programme, and the register records that rather than showing
only the ratified state. Evidence: `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md`

What "complete" does **not** mean, stated here because the row statuses
below are the thing most likely to be read alone: the release is **not
merged** to `main`, **not tagged**, **not published**, and carries **no
Product Approval verdict**. Ratification was Product Owner gate 1 of 4
and authorises none of those. Governance §7.1 permits a release to be cut
only from `main` and §9 reserves the merge, the tag and the verdict to
Product Approval — so those three remain not engineering's to perform,
and their absence is the correct state, not an omission. Recommendation
and evidence: `WP16.9.0 Engineering Release Report.md`.

## Purpose

v1.0 Readiness Hygiene — every item `docs/releases/v1.0.0/v1.0.0
Release Candidate Audit.md` §5.1 rated mandatory under the approved
v1.0 definition, except the v1.0 gate itself. No new product capability.
Closes with the first Product Approval verdict since `v0.12.0`.

## Work Packages

| Work Package | Scope | Type | Wave | Closes | Status |
|---|---|---|---|---|---|
| `WP 16.0A` | v1.0 Scope & Support Decision Record — six decisions (governing definition, Companion, plugins, REST/`AT-10`, platform matrix, `v0.15.1` folder), each a `D-0xx` entry; `Product Roadmap.md` Phase 5.5 | Decision | 0 | Audit §2, M1, M9 | **Complete** — `D-021`–`D-026` drafted and reserved Proposed (`fb8b90c`, 2026-09-04), then **ratified by the Product Owner on 2026-09-05** and entered in `Decision Register.md` (20 → 26 entries). `D-025` carries a ratification constraint: Linux is not to be recorded as "supported" without qualification, nor claimed CI-verified. `D-026`'s already-performed folder deletion was disclosed at the gate and ratified in that knowledge. Evidence: `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md` |
| `WP 16.0B` | Integrate off-`main` work — merge `WP 15.2A`; fold `docs/releases/v0.15.1/` into this release; Companion branch decision applied; `FCR-0092` citation resolved | Governance/Integration | 1 | `TD-120`, `TD-82` | **Complete** (`4198289`, merged `a4f891b`) |
| `WP 16.1A` | Enforce the release gate — GitHub branch protection per `WP11.1B` §4; `CI Gate` depends on `Governance Health Check` | Configuration | 1 | `TD-45` | **Complete — workflow half** (`6338330`); GitHub branch-protection setting is a Product Owner action, `TD-45` stays Open |
| `WP 16.3A` | Durable state schema versioning — architecture, `ADR-0120` | Architecture | 1 | `TD-87` (design) | **Complete** (`a80e95d`, `ADR-0120` accepted at Technical Review) |
| `WP 16.4A` | Test determinism — `CompositeLogSink` console dependency, last fixed wait, real image decode, `MainWindow` resize coverage; five consecutive CI runs | Implementation (tests) | 1 | `TD-34`, `TD-119`, `TD-100`, `TD-83` | **Complete, acceptance bar met.** (`1dbb8ab` + part 2 `30f5cb2`; `TD-34`, `TD-83`, `TD-100`, `TD-119` Resolved; Core temp-dir leak closed.) The plan's acceptance line is "5/5 clean runs both configurations in CI". It went unmet for most of this release: GitHub Actions could not assign a runner from 2026-09-04 22:34 UTC, and three local Release runs stood in. An independent reviewer measured 16/16 clean locally across four configurations and still judged it insufficient, citing this project's own `WP 13.12.9` precedent — a flake that reproduced zero times in 25 local attempts and still failed a real release run. The reviewer was right to refuse the substitution: **the matrix has now run in CI**, five for five on the frozen candidate `d7d3f3b`, both configurations plus the governance check and CI Gate, all 25 jobs green, no retries — runs 195/197/199/201/202, evidence in the ERR §4.1. |
| `WP 16.5B` | Linux/X11 — timeboxed Avalonia/`Tmds.DBus.Protocol` upgrade spike; fix, or state the support matrix in three documents | Implementation or Documentation | 1 | `TD-116` | **Complete** (`309c15d`; Linux launches; `TD-116` Resolved) |
| `WP 16.2A` | Register & status currency — six `TD-57` registers, Feature and Release registers, Repository Metrics, `Product Roadmap.md`, `PROJECT_STATUS.md` lower sections archived and re-derived | Documentation/Governance | 2 | `TD-57`, `DNB-7` | **Complete** (`40e267d`; `TD-57` Resolved) |
| `WP 16.3B` | Durable state schema versioning — implementation, migration chain, golden corpus | Implementation | 2 | `TD-87` | **Complete** (`67e7ce3` after two Technical Review rounds; `TD-87` Resolved) |
| `WP 16.5A` | Accessibility baseline — modal dialogs, automation names, live regions, graph keyboard operability, contrast; modality test | Implementation | 2 | `TD-65` (partial) | **Complete** (`60d9966`; `TD-65` Partially resolved, `TD-83` Resolved; 453 Desktop tests) |
| `WP 16.1B` | Health-check extension — Interface/Exception/Namespace registers re-derived from `src/`, TD/FCR summary consistency, ADR count; script exception path | Implementation | 3 | `TD-57` root cause, `TD-43` | **Complete** (`b0a4150`; 16 checks; two findings closed at integration `8c0e791`) |
| `WP 16.2B` | Academy retrospective backfill — `v0.11.0`'s ten, `WP 15.1A`/`15.1B`, the pre-`v0.14.0` programme (~20); Academy Register annotations; Learning Path pointer | Documentation | 3 | `WP-Z3` §12 gap | **Complete** (three branches + closure; 41 retrospectives, 21 rows backfilled) |
| `WP 16.4B` | Durability & loopback hygiene — orphan detection and sweep, attachment content release, bounded keyed lock, DI duplicate guard, gated OpenAPI route, REST listener off by default | Implementation | 3 | `TD-67`, `TD-68`, `TD-69`, `TD-97`, `TD-62`, `AT-10` decision | **Complete** — three parallel streams (`WP16.4B-1/-2/-3` reports), merged and revalidated together (`ac87049`). `TD-62`, `TD-68`, `TD-69`, `TD-97` Resolved; `TD-67` Partially resolved — three of its five named defects closed, one found **not to be a defect** and referred to Technical Review, one (`VerificationService.RecordAsync`'s partial evidence graph) genuinely still open and disclosed |
| `WP 16.2C` | Academy retrospectives for `v0.16.0`'s own eleven Work Packages — Engineering Governance §6's thirteen-section template; Academy Index links, Academy Register rows and Documentation Register count reconciled at integration | Documentation | 3 | `TD-127` | **Complete** — raised by the `v0.16.0` review board, which found nothing scoped to write them. Its first pass wrote eleven and left six uncovered, including itself, while an integration commit claimed "closing `TD-127`" and that register row still read Open — found by the independent post-remediation governance audit and completed to 223 retrospectives. Release Definition of Done item 4 is now true for `v0.16.0`; `TD-127` Resolved |
| `WP 16.5A-R1` | Accessibility remediation — six review-board findings against `WP 16.5A`'s baseline, the blocker being a `:focus-visible` ring painted in the same brush as the Primary button's own fill (1.00:1, both themes) | Implementation | 3 | `TD-65` (further), `TD-128` raised | **Complete** — 474 Desktop tests; the focus-ring test now asserts measured contrast against the background it paints over, which the original could not see |
| `WP 16.1A-R1` | Release path enforcement — `release.yml` runs the Governance Health Check as a hard gate; `new-release.ps1` refuses to tag unless CI concluded `success`; `linux-launch-smoke` requires a verified startup marker, not just "still running"; two documentation corrections | Configuration | 3 | none — `TD-45` stays Open | **Complete** — raised by the review board's Build/CI perspective, which found the release path weaker than the merge path `WP 16.1A` had just hardened |
| `WP 16.4B-R3` | Per-object write serialisation — closes a lost update that let the attachment sweep permanently delete a live file. `PersistStateAsync` now takes an `AsyncKeyedLock` keyed by object Id, owned by `EngineeringDomainContext`, before `CaptureState()` and holds it through `SaveAsync` | Implementation | 3 | `TD-135` | **Complete** — found by the independent post-remediation review, which reproduced it against the real classes; the `WP 16.4B-R2` marker could not reach it. An instance-level lock would not have closed it: `ReviseAsync` proves more than one live instance can answer to one Id |
| `WP 16.4A-R1` | Test strength — three race tests that could not detect their own regression, a double-dispose test with a 0.45% detection rate, a golden corpus whose "every field" claim omitted `Metadata`, and unbounded interleaving gates | Implementation (tests) | 3 | none — strengthens `WP 16.4A`/`16.4B` | **Complete** — every fix mutation-proven: the fixed test demonstrated failing against the reverted production code and passing against the restored code. Gates now pause on whichever read arrives second rather than on a collection name; the double-dispose race is looped 2,000× (10/10 detection against a reverted guard); `--blame-hang-timeout` added to CI |
| `WP 16.4B-R4` | Close the lost update on the one durable-write path `WP 16.4B-R3` did not route through its lock — `ReviseAsync`'s unlocked capture and the stale predecessor that could overwrite after it | Implementation | 3 | `TD-136` | **Complete** — capture, hand-off and registration now happen inside the per-object write lock, and the predecessor is retired there; a later write through a retired instance throws `SupersededEngineeringObjectException` rather than silently discarding. Found by the final release review board, reproduced against the real classes. Mutation-proven both ways: 3 of 5 new tests fail against the reverted fix (one showing the on-disk attachment collection empty after an accepted write), 5 of 5 pass against it. Two of the five exist to catch over-correction — a predecessor write arriving *before* the revision must still be carried into the successor, because that is the rename-then-revise sequence every `Revise*Command` performs |
| `WP 16.5B-R1` | Bring `ci.yml`'s Linux comment into line with ratified `D-025` | Documentation (CI) | 3 | none | **Complete** — the comment claimed Linux becomes "CI-verified in effect once this job exists". `D-025` as ratified says the opposite and forbids that claim. Written before ratification and missed when every document was hedged; found by the final release review board's platform reviewer |
| `WP 16.5A-R2` | Give the Object Editor's attachment "Open" button a real accessible name | Implementation (UI) | 3 | none — undisclosed, outside `TD-132`'s scope | **Complete** — the button set no automation name at all and relied on `ContentControlAutomationPeer`'s `Content?.ToString()` fallback, so several attachments announced "Open, button" identically. Now named per file rather than per verb, which also avoids reproducing `TD-132`'s duplicate-name defect here |
| `WP 16.9.0` | Engineering Readiness Review, `VERSION` 0.16.0, Release Notes, merge/tag/publish under the enforced gate, **Product Approval verdict recorded** | Verification/Release | 4 | Audit finding 2 | **Engineering complete; blocked at the Product Approval boundary.** `VERSION` bumped to `0.16.0`; Release Notes and the Engineering Readiness Review written; the five-run CI determinism matrix obtained on the frozen release candidate. **Not done, and not engineering's to do** (Engineering Governance §7.1/§9): the merge to `main`, the tag, the publish, and the Product Approval verdict itself. Each requires an explicit, per-occasion instruction from the Product Owner, and `scripts/new-release.ps1` mechanically refuses to run anywhere but `main`. |

## Carried in from `main`'s own line, once `WP 16.0B` lands

| Work Package | Scope | Status |
|---|---|---|
| `WP 15.2A` | Desktop Test Suite Persistence Root Cleanup (closes `TD-120`) — delivered on `feature/wp15.2a-td120-persistence-root-cleanup`, CI green; merged into `feature/v0.16.0` by `WP 16.0B`, its report now at `docs/releases/v0.16.0/WP15.2A Desktop Test Suite Persistence Root Cleanup — Implementation Report.md`, the `v0.15.1` folder deleted (`D-026`, ratified 2026-09-05) | **Complete — merged** |

## Deferred out of this release, by the plan

| Item | Disposition |
|---|---|
| `TD-109` remaining `MainWindow` shell services, `TD-108`, `TD-118` | Conditional group C8 — `v0.17.0` or later; revisit triggers unchanged. |
| `TD-56`, `TD-61`, `TD-64` plugin-enablement preconditions | Conditional group C2 — only if `WP 16.0A` puts third-party plugins in v1.0. |
| `TD-13`, `TD-14`, `TD-16`, `FCR-0003`, `FCR-0004` | Conditional group C1 — only if Companion or non-loopback REST enters v1.0. |
| `TD-65` remaining inventory beyond the top four | Re-listed by `WP 16.5A`; v1.x. |
| `FCR-0073`, `FCR-0074`, `AT-23`, `TD-115`, `TD-77` | Conditional group C6. |

## Related Documents

`docs/releases/v0.16.0/v0.16.0 Release Plan.md`; `docs/releases/v1.0.0/
v1.0.0 Release Candidate Audit.md`; `docs/releases/v1.0.0/WorkPackages.md`;
`docs/governance/Quality/Technical Debt Register.md`; `PROJECT_STATUS.md`.
