# TempestOS — Project Status

**Last Updated:** 2026-09-06 (`WP 16.2B` closure, following `WP 16.2A`'s
rewrite, `v0.16.0`) — this file was rewritten from a ~9,068-line/565 KB
accumulation of superseded status paragraphs into the short, current
dashboard below. Everything previously here of standing value is
retained, not deleted: see `docs/governance/Documentation/PROJECT_STATUS
Archive (v0.5.0–v0.15.0).md` for the full verbatim history this file
carried from `## READY FOR WP14 UI/UX` onward, and the "Maintaining
This Document" section below for where each kind of fact now lives
permanently.

---

## Current Release

**`v0.15.0` is RELEASED AND PUBLISHED (2026-09-04).** Merged to `main`
(`350922d`, plus one follow-up documentation commit `a35365a` that the
tag itself points to). Tagged `v0.15.0`. Published as [GitHub Release
`382812261`](https://github.com/kreczmans-creator/TempestOS-Core/releases/tag/v0.15.0)
on 2026-09-04T15:00:00Z with both required assets. Real GitHub-hosted CI
confirmed green three times — on `main` itself at `a35365a` (run
`33864515369`), on the tag push (`ci.yml` run `33885783239`), and
`release.yml` (run `33885783286`) before publishing.

**It is NOT CERTIFIED** — no §9 Product Approval verdict has been
recorded; publication is not certification. See `docs/governance/
Delivery/Release Register.md`'s own `v0.15.0` row (corrected `WP
16.2A`) for the full, independently re-verified account.

`v0.15.0` — **"Governance Currency & Desktop Productisation"**: Desktop
brand recovery, a real Windows startup crash fixed (`TD-121`, Resolved),
two phases of Desktop productisation defect-fixing, the Ribbon overflow
affordance fixed (`TD-122`, Resolved), and governance currency restored
(`WP 11.5A` found the drift; `WP 15.1A`/`WP 15.1B` formalised and
independently re-verified it). 3,088/3,088 Core tests, 408/408 Desktop
tests, 0 failures, both configurations — see `docs/releases/v0.15.0/
Release Notes.md`.

## Current Development Branch

**`main`** — `v0.16.0` was merged on 2026-09-05 (`cc7ef4d`), and again at `9bba720` to carry the `WP 16.4B-R4` remediation the first merge predated. It is **not tagged and not published**, and carries **no Product Approval verdict**, so `v0.15.0` above remains the current *released* version.

`feature/v0.16.0` was cut from `main` at the `v0.15.0` tag commit (`a35365a`) and remains the working branch for the release's outstanding remediation. Scope, waves and acceptance criteria: `docs/releases/v0.16.0/v0.16.0 Release Plan.md`. The live,

`v1.0` scope is **decided**: the six decision records `WP 16.0A` drafted
(`D-021`–`D-026`) were **ratified by the Product Owner on 2026-09-05**
and are now entered in `Decision Register.md`; `Product Roadmap.md`'s
"Phase 5.5" entry is promoted from Proposed to Decided accordingly.
Approval evidence: `docs/releases/v0.16.0/WP16.0A Product Owner Ratification — D-021 to D-026.md`.

They were `Proposed — awaiting Product Owner approval` from 2026-09-04
until then, and the whole of v0.16.0 was built while they were, which
this file said at the time and the register's own rows still record.
Ratification is **Product Owner gate 1 of 4** and settles scope only —
it authorises no merge, no tag, no publication, and no release verdict.

## Current Work Package

**Landed on `feature/v0.16.0` as of this review** (Wave 0/1 of the
Release Plan):

| Work Package | What it did | Report |
|---|---|---|
| `WP 16.0A` | Drafted `D-021`–`D-026` (v1.0 scope decisions); Proposed 2026-09-04, **ratified by the Product Owner 2026-09-05** | `docs/releases/v0.16.0/WP16.0A v0.16.0 Scope Decision.md` |
| `WP 16.1A` | CI workflow half of `TD-45` (release-gate enforcement); GitHub branch-protection settings handed to the Product Owner | `docs/releases/v0.16.0/WP16.1A Enforce the Release Gate.md` |
| `WP 15.2A` (carried in) | Closed `TD-120` — Desktop test suite persistence-root cleanup; `Tempest.Desktop.Tests` 412/412 | `docs/releases/v0.16.0/WP15.2A Desktop Test Suite Persistence Root Cleanup — Implementation Report.md` |
| `WP 16.2A` | Register and status currency — re-derived every count in every touched governance register; this file's own rewrite | `docs/releases/v0.16.0/WP16.2A Register and Status Currency Report.md` |
| `WP 16.0B` | Merged `WP 15.2A`; folded the short-lived release folder that had been created to hold it into this release (`D-026`, ratified 2026-09-05 — that folder's own `WorkPackages.md` said "Not a release", and no tag or Release Register row for it ever existed); Companion deferred (`D-022`, ratified 2026-09-05) | `docs/releases/v0.16.0/WP16.0B Integration Report.md` |
| `WP 16.3A` | `ADR-0120` — durable state carries a schema version, migrations apply only on read; accepted at Technical Review | `docs/releases/v0.16.0/WP16.3A Durable State Schema Versioning — Architecture Report.md` (written retrospectively at the review board, which found this the only landed Work Package without one); `docs/adr/ADR-0120-durable-state-carries-a-schema-version-and-migrations-apply-only-on-read.md`; `docs/architecture/State Schema Versioning Architecture.md` |
| `WP 16.5B` | Linux/X11 launch fixed: Avalonia 11.3.20, `Tmds.DBus.Protocol` 0.21.3; `TD-116` Resolved; advisory Linux launch smoke job in CI | `docs/releases/v0.16.0/WP16.5B Linux Launch Spike Report.md` |
| `WP 16.2B` | 41 Academy retrospectives written, 21 register rows backfilled; Academy at 206 retrospectives | `docs/releases/v0.16.0/WP16.2B Academy Retrospective Backfill Report.md` |
| `WP 16.4A` | Test determinism: `TD-34`, `TD-119`, `TD-83`, `TD-100` Resolved; Core temp-directory leak closed; worktree-safe `SampleSeparationTests` | `docs/releases/v0.16.0/WP16.4A Test Determinism Report.md` |
| `WP 16.3B` | Schema versioning implemented per `ADR-0120`; `TD-87` Resolved; golden corpus and restart proof committed | `docs/releases/v0.16.0/WP16.3B Schema Versioning Implementation Report.md` |
| `WP 16.5A` | Accessibility baseline: modal dialogs, automation names, live regions, graph keyboard, focus ring, contrast ≥ 4.5:1; `TD-65` Partially resolved | `docs/releases/v0.16.0/WP16.5A Accessibility Baseline Report.md` |
| `WP 16.1B` | Governance health check extended 8 → 16 checks with induced-failure proof; `TD-43` fixed; two register drifts it caught closed at integration | `docs/releases/v0.16.0/WP16.1B Health-Check Extension Report.md` |

**`WP 16.2A`'s disclosed gap, resolved.** `WP 16.2A` found no standalone
`WP 16.0B` report at its base; the report now exists (row above), written
at the `WP 16.2B` closure. `WP 16.2A` also recorded `WP 16.3A` as not
landed; it was — its base `8b4c394` is the `WP 16.3A` merge commit.

**In progress on its own branch** (not yet merged): `WP 16.4B`
(durability and loopback hygiene, `D-024`).

## Repository Metrics

Full snapshot, every figure independently re-derived and commanded
directly: `docs/governance/Quality/Repository Metrics Register.md`
(`WP 16.2A` snapshot, 2026-09-04). Headline figures:

| Metric | Value |
|---|---|
| `VERSION` | `0.16.0` — bumped by `WP 16.9.0`. Not yet tagged, merged or published; `v0.15.0` remains the newest tag |
| `src/` `.cs` files / lines | 805 / 76,890 |
| `tests/` `.cs` files / lines | 371 / 83,485 |
| `[Fact]`/`[Theory]` attributes | 3,197 (including `[AvaloniaFact]`/`[AvaloniaTheory]`) |
| ADRs | 123 (`ADR-0001`–`ADR-0123`) |
| Public interfaces (`src/Tempest.Core/`) | 195 (194 distinct names) |
| Custom exception types | 90 (`SupersededEngineeringObjectException` added `WP 16.4B-R4`) |
| Technical Debt Register rows | 139 (`TD-001`–`TD-139`): 56 Resolved, 6 Closed, 71 Open, 5 Partially resolved, 1 Deferred |
| Total commits | 322 |

## Repository Health

**Last real, CI-verified full-suite totals** (`f593e5c`): **3,209/3,209 Core tests, 474/474 Desktop tests**, 0 failures, both configurations, on `windows-2022` — verified five consecutive times, not once. `WP 16.4A`'s acceptance required a five-run CI determinism matrix on one immutable commit; runs 218, 220, 223, 226 and 227 all concluded `success`, every job in every run, no retries.

That matrix is the **second** one this release obtained. The first ran on `d7d3f3b` (runs 195–202) and was invalidated when review-board remediation changed `src/`, `tests/`, `scripts/` and `.github/`; its re-run's first attempt then failed genuinely (run 214 on `fce2166`, Governance Health Check) and is preserved in the record rather than deleted. Full evidence, including that failure: `docs/releases/v0.16.0/WP16.9.0 Engineering Release Report.md` §4.1–§4.3.

A **third** matrix is required and outstanding: `WP 16.4B-R5` again changes `src/` and `tests/`, so the `f593e5c` evidence no longer covers the tree being released.

These figures are from a candidate merged to `main` but **not tagged and not published**; the last *released* CI-verified totals remain **3,088/3,088 Core, 408/408 Desktop** at the `v0.15.0` tag — `docs/releases/v0.15.0/Release Notes.md`.

**Since `WP 16.5B` (Avalonia 11.3.20), locally on Linux** — the
`feature/v0.16.0` merge base was rebuilt and re-tested in this session:
Release build 0 warnings / 0 errors with `TreatWarningsAsErrors`;
after the Wave 2/3 merges **`Tempest.Core.Tests` 3,124/3,124 and
`Tempest.Desktop.Tests` 453/453** (Release); the Desktop launched under
Xvfb with a full startup log and no crash log. **CI outage — occurred, diagnosed, and now
resolved.** Every GitHub-hosted run on this repository from 22:34 UTC on
2026-09-04 failed within seconds with no runner assigned (Windows and
Ubuntu alike, on unchanged workflow files, with no GitHub incident
reported) — the signature of an exhausted Actions allowance on a private
repository. It was resolved on 2026-09-05 by the Product Owner making
the repository public, which restores free hosted-runner minutes: run
`171` at 06:06 UTC is the first success after the last failure (run
`170`, 05:44 UTC, dead in ten seconds). Work Package reports written
during the window state their evidence as local-only, correctly for when
they were written; `WP 16.9.0` re-established CI evidence on the
integrated tree rather than carrying those local figures forward as the
release's certification — the five-run matrix on `d7d3f3b`, above.

Build: 0 warnings, 0 errors, both configurations, at the `v0.15.0` tag
— every merge to `main` is now gated by real CI (`WP 11.1A`) and, as of
`WP 16.1A`, `CI Gate` depends on `Governance Health Check` passing.

## Governance / Academy / Documentation Status

Each of the following registers is now the sole, current authority for
its own subject — this file no longer duplicates their content, only
points to it:

- **Interfaces** — `docs/governance/Engineering/Interface Register.md` (195 rows / 194 distinct public interface names, re-derived `WP 16.2A`, +3 schema-versioning contracts at the Wave 2/3 closure).
- **Exceptions** — `docs/governance/Engineering/Exception Register.md` (84, re-derived `WP 16.2A`).
- **Dependency Injection** — `docs/governance/Engineering/Dependency Injection Register.md` (55 named registrations, 60 total statements, re-derived at the independent post-remediation review, 2026-09-05, which found `WP 16.4B-R2`'s `IAttachmentWriteIntentStore` missing — the third recurrence of the gap `TD-123` records).
- **Namespaces** — `docs/governance/Engineering/Namespace Register.md` (47 namespaces / 715 files in declared scope, 62/805 across all of `src/`, re-derived `WP 16.2A`, corrected at the `WP 16.1B` integration follow-up).
- **Platform Services** — `docs/governance/Engineering/Platform Services Register.md` (35 entries, re-derived `WP 16.2A`).
- **Feature history** — `docs/governance/Delivery/Feature Register.md` (extended through `v0.15.0`, `WP 16.2A`).
- **Release history** — `docs/governance/Delivery/Release Register.md` (17 versions referenced, `v0.15.0` corrected to Released, `WP 16.2A`).
- **Technical Debt** — `docs/governance/Quality/Technical Debt Register.md` (122 rows; `TD-57` Resolved, `TD-45` gained a pointer, `WP 16.2A`).
- **Validation / test gates** — `docs/governance/Quality/Validation Register.md` (current-state section added, `WP 16.2A`).
- **Repository size/shape** — `docs/governance/Quality/Repository Metrics Register.md` (new snapshot, `WP 16.2A`).
- **Product roadmap** — `docs/governance/Product Roadmap.md` (Phase 5 marked delivered, Phase 5.5 added, `WP 16.2A`).
- **Governance suite index** — `docs/governance/Governance Index.md`.
- **Documentation inventory** — `docs/governance/Documentation/Documentation Register.md`.
- **Academy inventory** — `docs/governance/Documentation/Academy Register.md` (206 retrospectives, reconciled `WP 16.2B`).
- **Overall governance audit** — `docs/governance/Governance Audit Report.md`.

## Known Unknowns

Recorded honestly, not guessed at — full detail in `docs/governance/
Governance Audit Report.md` — carried forward verbatim from this file's
own prior version:

1. `docs/releases/v0.2.0.md` (renamed `WP 12.9.1` from a misnamed stray
   file, `docs/releases/v0.2.0`, no extension, no folder) — a
   never-completed release-notes skeleton, every field blank; whether
   v0.2.0 was ever released, skipped, or reserved is unknown.
2. `docs/roadmap/`, `docs/diagrams/` (each gains a tracked marker
   `README.md`, `WP 12.9.1`, disclosing this in place — see
   `Documentation Register.md`) — intended purpose unknown; unreferenced
   by any document reviewed.
3. Exact original authorship of four pre-Claude namespaces
   (`Tempest.Core.Hosting`, `Bootstrap`, `Projects`, `Repositories`) and
   seven unnamespaced bootstrap-era files.
4. A five-day gap in earliest git history (2026-07-15 to 2026-07-21).
5. v0.1.0's full scope beyond its own commit message.
6. Intermediate historical test-count totals for `WP 4.1` and `WP 4.3`
   (each retrospective states only the tests it added, not a running
   total).

## Next Planned Work Package

The remaining `v0.16.0` waves, per `docs/releases/v0.16.0/v0.16.0
Release Plan.md` and the live status list in `docs/releases/v0.16.0/
WorkPackages.md`:

- **Landed** — Wave 0/1: `WP 16.0A` (Proposed records), `WP 16.0B`,
  `WP 16.1A` (workflow half), `WP 16.3A`, `WP 16.5B`; Wave 2: `WP 16.2A`,
  `WP 16.2B`.
- **Landed** — Wave 2/3: `WP 16.4A`, `WP 16.3B`, `WP 16.5A`, `WP 16.1B`.
- **In progress** — `WP 16.4B`.
- **Closing** — `WP 16.9.0` (Engineering Readiness Review and the
  first §9 Product Approval verdict recorded since `v0.12.0`), preceded
  by the release review board.

## Maintaining This Document

Keep this short — it is a dashboard, not a narrative. Update it when,
and only when, one of the following genuinely changes: the current
release, branch, or Work Package; a headline Repository Metrics figure;
a Repository Health fact; or a Known Unknown being resolved. For
everything else, correct the authoritative register directly (see
"Governance / Academy / Documentation Status" above) and link to it —
do not re-narrate that register's own content here. When a correction
is needed, correct it in place with a brief note of what changed and
why, in this file's own established convention; reach for a new
archive document, mirroring `docs/governance/Documentation/PROJECT_STATUS
Archive (v0.5.0–v0.15.0).md`, only if this file's own accumulated
correction-history threatens to make it unusable as a dashboard again —
that is what triggered `WP 16.2A`'s own rewrite, and is the standard for
the next one.
