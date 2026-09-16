# TempestOS — Project Status

**Branch:** `claude/tempestos-v1-final-acceptance-19hka8` — the overnight acceptance campaign's branch (2026-09-15 23:11 UTC → 2026-09-16), built on `release/v0.21.0` @ `a4ab1915` (the recovery tranche's candidate: `b033651d`, its last code commit, plus its release documents). **Last product-code commit tonight: `37264671`** (the gate ran on it; the real-shell runner's rate-card step followed at `943e6f2e`, test-side code only); the release documents (`PRODUCT_OWNER_ACCEPTANCE.md`, `OVERNIGHT_FINAL_ACCEPTANCE_REPORT.md`, this file) are committed on top — `git log -1` is the head.
**VERSION:** `0.21.0`. `v0.18.0` is the last release merged to `main`, tagged and published (2026-09-14); `v0.19.1`, `v0.20.0` and `v0.21.0` are all contained in this branch. **Status: v1.0 release candidate, ready for Product Owner acceptance — not accepted.** The PR to `main`, the tag and the GitHub Release follow the Product Owner's decision.

## What a user can do today

Measured against `docs/releases/v1.0.0/WorkPackages.md` ("What v1.0.0
is"), the five things v1.0.0 must let an engineer do, plus the one the
Product Owner added on 2026-09-14 — every one now drivable end to end in
the running application from a clean persistence root (the real-shell
journey of `WP 21.5C`, and tonight's fix for the first rate card):

1. ● Open a project for a client, with a PO reference, a budget and a
   pinned rate card — **and create that rate card in the first place**
   (Engineering → Reference data → *Add a rate card*, new 2026-09-16; a
   clean install had no way to do this) — and a quotation opened with the
   project that defines its requirements and deliverables when accepted
   (the sibling tabs now refresh in place). — `v0.19.0`/`v0.19.1`/tonight
2. ● Record a calculation done in the engineer's own tool as evidence on
   the project, or run one of the eleven built-in calculators with
   materials, fasteners and bearings from the released libraries, re-run
   and compare. — **released** `v0.18.0`; `v0.21.0` (`WP 21.7A`–`21.7C`)
3. ● Have a second principal independently check that evidence and
   issue it with an issue sheet attached to the project. — **released**
   `v0.18.0`
4. ● Record time priced from the pinned rate card and, on marking a
   deliverable complete, emit an invoice request to Xero or QuickBooks
   (the Fake connector by default; **the real connectors can now be
   authorised from Settings** — `WP 21.6P`, tonight; the first live
   sign-in is the Product Owner's, `PHYSICAL_REVIEW.md` §7k). — `v0.19.0`/tonight
5. ● See the consultancy at a glance: task tiles and the commercial
   snapshot on Home (all six project-health buckets counted, tonight),
   project status and a Gantt on Projects, receivable, payable and cash
   flow on Business. — `v0.19.1`
6. ● Work in the shell the Product Owner sketched — Home, Projects,
   Tasks, Engineering, Business — with docking steps 1–2 verified on a
   real screen and tab reordering on the keyboard (`WP 21.0K`, tonight);
   documents from every template; undo across commands; backup and
   restore from Settings; a Windows installer whose first `Setup.exe` is
   still to be produced. — `v0.19.1`–`v0.21.0`

## Work in flight

None in code. The campaign's package record is `docs/releases/v0.21.0/Execution Plan.md` §6; what changed and why is `docs/releases/v0.21.0/Release Notes.md` (the top rows of "What shipped" and the last warnings); the engineering record is `OVERNIGHT_FINAL_ACCEPTANCE_REPORT.md`; the Product Owner's test pack is `PRODUCT_OWNER_ACCEPTANCE.md`.

Owed, all on the Product Owner: `WP 21.0B`/`21.0C` (docking steps 3–4) after the `ADR-0153` review; `WP 21.6` (the first live Xero sign-in, §7k); the first `Setup.exe` (`WP RC.0A`'s installer has never been produced by the pipeline — a tag or `scripts/package-installer.ps1` on Windows); docking K4/K5 on two monitors and the save-on-close half of persistence in a Windows session; the answer to `TD-188` (per-line VAT on the Quote tab).

## Gate (the last code commit `37264671`, re-derived 2026-09-16 on Linux; CI on the Windows runners)

- Build: 0 warnings, 0 errors, Debug and Release, `TreatWarningsAsErrors` (9 projects — the real-shell runner is the ninth, never shipped)
- Core tests: 5,314 passed of 5,324, Debug and Release — the 10 failures are Windows-only tests (6 spawn `powershell`, 4 DPAPI), green on the Windows runners
- Desktop tests: Debug 901 passed of 902, 1 failed (the Linux-only status-bar test), 6 m 0 s; Release 901 passed of 902, 1 failed (the same Linux-only test), 5 m 55 s — the one Linux-only failure (`StatusBarCollapseTests…`, font fallback) is green on the Windows runners
- Governance health check: 5 of 5
- Dependency scan: 9 projects, none vulnerable; every workflow action SHA-pinned
- Real shell: the `WP 21.5C` runner on this head — the `WP 21.5C` runner on the final head, display :105, 2026-09-16 01:38–01:42 UTC: **journey 37 steps — 37 Verified, 0 Failed; relaunch verify 8 of 8 — PASSED**, now including the rate card created and released through the new form, the New Project prompt offering it, the timesheet prompt offering its grade, and the invoice request raised on completion (`evidence/realshell/gate-run-*-on-the-final-head.md`). Two earlier runs tonight on the same product code: 34/35 (before the runner learned the rate-card form) and 35/36 (the stale Raise-invoice expectation); hand-driven evidence under `docs/releases/v0.21.0/evidence/{xero,docking,fixes,quality}/`
- CI: green on `a4ab1915` (runs 430, 433), `7fdc33df` (435) and `f7e2af66` (436); the final head: the Windows run on this head is recorded by the campaign's last commit (every push cancels the previous run on a non-release branch, so only the final head's run completes)
- Live backlog: 10 of 30, every row classified (`BACKLOG.md`)

## Released

- `v0.18.0` Evidence and Check: PR merged to `main` (`3680257`), tag `v0.18.0`, GitHub Release published by run 34823372128 on 2026-09-14 with `TempestOS-v0.18.0.zip` and `TempestOS-v0.18.0-engineering-harness.zip`.
- `v0.17.0`: merged to `main`, tagged, published 2026-09-09.

## Where things are recorded now

- **`PRODUCT_OWNER_ACCEPTANCE.md`** — the morning test pack: what changed, what was tested, the 19-step journey, the risks, the limitations, Xero, the checklist.
- **`OVERNIGHT_FINAL_ACCEPTANCE_REPORT.md`** — the engineering record of the campaign.
- **`BACKLOG.md`** — the live technical-debt list a user could still notice (10 of 30).
- **`CONTRIBUTING.md`** — how a Work Package becomes a branch, a PR and a merge.
- **`PHYSICAL_REVIEW.md`** — the running-application checklist every release is verified against; §7c–§7k are the journeys, §7k the first live Xero authorisation.
- **`docs/releases/v1.0.0/WorkPackages.md`** — the programme this and every following Work Package executes.

Everything this file used to carry — Work Package history, governance
narrative, superseded metrics — is archived, not deleted:
`archive/docs-2026-09/`; the `v0.19`–`v0.21` gate histories that stood
here are in the release notes of those releases.
