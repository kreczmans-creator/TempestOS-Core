# The Governance Reset, and How a Release Is Now Run

**Release:** `v0.17.0` (`WP 17.0B`, 2026-09-08) · **Debt:** `TD-45`,
`TD-123`–`TD-125`, `TD-127`, `TD-151`–`TD-153`, `TD-164` · **Code:**
`CONTRIBUTING.md`, `BACKLOG.md`, `PROJECT_STATUS.md`,
`.github/pull_request_template.md`, `scripts/governance-healthcheck.ps1`

**In plain terms.** For most of this project's life, almost every piece
of work came with a matching stack of paperwork: a written explanation of
why it was done, and master lists confirming everything like it was still
accurate. That paperwork earned its keep — it is what let a stranger
trust the project — but by September 2026 it took longer to produce and
correct than the product itself, and still fell out of date faster than
anyone could fix it. `WP 17.0B` threw the process out, filing the old
documents safely away rather than deleting them, and replaced it with a
much smaller set of rules: describe the change, prove it works, list
what's left, and ship. This chapter explains why, and what a release
looks like now.

## What governance meant, and what it cost

`Engineering Governance.md` (still normative on principle, per its own
status note — the mechanics below are what changed) required every Work
Package to produce a completion report, a thirteen-section retrospective
under `docs/academy/03 Work Packages/`, and an ADR where warranted.
`03-governance-registers.md` explains what sat above that: registers
answering questions no single document could — "does every service have
a test, an ADR, an Academy article?" — checked by a health check that
re-derived several from source on every push. None of it was for show;
it caught real problems.

But by `v0.16.0` the cost was visible in the numbers: `PROJECT_STATUS.md`
had grown to **565,445 bytes** before that release cut it to about
11,000; the Technical Debt Register had reached **468 KB and 170 rows**;
the Academy had run **one release behind since `v0.10.0`**, and closing
that gap took **fifty-two retrospectives written in one release** —
forty-one historical plus eleven of the release's own — commissioned only
because a review board noticed nobody had been scoped to write them.

## The finding that settled it

The most telling line in `v0.16.0`'s own Release Notes is about a bug in
the paperwork, found while the paperwork was being fixed:

> `TD-123` — the Dependency Injection, Platform Services and Validation
> registers have no machine derivation, which is how register drift
> **recurred inside this very release**.

A release whose entire subject was "the registers have drifted" shipped
with a fresh case of that same drift. The `v1.0.0 Release Candidate
Programme` (`docs/releases/v1.0.0/WorkPackages.md`, adopted 2026-09-08,
`d3218a6`) names the conclusion directly: "the governance process now
costs more than the product." `WP 17.0B` — "Governance reset" — replaces
it rather than certifying it.

## What replaced it: `CONTRIBUTING.md`

`CONTRIBUTING.md` (`4dd80d1`) is one page:

- **A Work Package is a branch and a pull request.** No separate
  retrospective, readiness review or completion certification — "the PR
  description is the retrospective."
- **One review, one round.** Findings are fixed in the same PR or filed
  to `BACKLOG.md` before merge; never dropped, never reopened.
- **Release Blocking narrows to one thing**: "data loss a user can
  reproduce from the running UI." A rough edge or a missing surface goes
  in `BACKLOG.md`, not in the way of the merge.
- **The Markdown budget.** A PR may not add more Markdown lines than
  code lines, enforced by `scripts/governance-healthcheck.ps1` comparing
  `git diff --numstat` against the branch's merge base with `main` — the
  rule that stops the `PROJECT_STATUS.md` story recurring.
- **The Definition of Done is largely unchanged**: 0 warnings/0 errors in
  both build configurations, every test passing, the architecture
  invariants green, an ADR for any decision that constrains future code,
  a `PHYSICAL_REVIEW.md` §7 row for any new surface, one Release Notes
  line — no readiness review, colour review board or Academy
  retrospective sits alongside it any more.
- **Branch protection is now configured on `main`** — required, strict
  `CI Gate`; PR required; no force pushes; conversation resolution
  required — closing `TD-45`, open since `v0.16.0` because the gate
  "exists in the workflow; nothing on GitHub enforces it." Zero required
  approvals is deliberate: a solo owner cannot approve their own PR, so
  the `CI Gate` check and the one-round review stand in for a signature.

## `BACKLOG.md`, `PROJECT_STATUS.md` and the reduced health check

`BACKLOG.md` replaces the Technical Debt Register. Its heading states the
rule it was built from: of the register's 170 rows, **101** had a final
status of Open, Partially resolved or Deferred (93 Open alone); each was
sorted into **Owned by Programme** (closed by a named substrate or
surface Work Package — `17.1A`, `17.1B`, `17.2A`, `17.3A`, `18.0A`,
`18.1A`, `18.2A`, `19.2A`, `19.2B` — no row of its own needed),
**Archived with the Layer** (plugins, REST, licensing, a frozen
`P02`–`P07` discipline — not debt in a product that does not ship it), or
the **Live Backlog**, capped at 30. The live count was 27 at release;
today the file's own heading reads 25, rows having since closed
(`TD-147`, `TD-163`, and others). Judgement calls are flagged in place
rather than silently misfiled.

`PROJECT_STATUS.md` is now one screen: branch, version, the five "What
v1.0.0 is" sentences marked against what a user can do today, the Work
Package in flight, current gate figures, and links to `BACKLOG.md`,
`CONTRIBUTING.md`, `PHYSICAL_REVIEW.md` and the programme document.
`scripts/governance-healthcheck.ps1` fell from 16 checks to 5 — the ADR
Register against `docs/adr/`, `VERSION` against a release folder, every
tagged folder having a Release Notes file, the ADR file count against
the Register's own row count, and the new Markdown budget — the other
eleven, including three that re-derived a register from `src/`, deleted
with the registers they compared against.

## The archive: moved, not deleted

`WP 17.0B` moved **807 governance files** — every retrospective,
review-board disposition, status report and superseded register — into
`archive/docs-2026-09/` with `git mv`, and live documentation fell from
**1,062 files to 257**. `archive/docs-2026-09/README.md` states why:

> Nothing here was deleted: every file was moved with `git mv`, so its
> full history — every prior edit, every author, every commit it was
> part of — remains intact and reachable by `git log --follow` on its new
> path, and by checking out any commit before this reset for its old one.

A deleted file is gone until someone remembers which commit removed it; a
file moved with `git mv` carries its whole prior life to its new address,
retrievable by anyone who knows where to look — slower to reach, never
lost.

## The honest cost

The reset does not pretend this was free. A retrospective's real value
was a document written specifically to be read later — problem,
alternatives, choice, trade-off — independent of memory or a commit
message's terseness. A PR description still carries "why," but written
once, under the Markdown budget, alongside the code; not the same
document at the same depth.

The cost lands squarely on this Academy. Chapters 42 through 49 could
still draw on an archived retrospective for every Work Package they
cover, because the archive holds one for everything up to `v0.16.0`.
Chapters 50 through 64 — `v0.17.0` and `v0.18.0` — could not: no
retrospective was ever written for them, and they were reconstructed
after the fact from commit messages, ADRs, release notes and the code
itself, because that is what the reset left behind. Say this plainly:
**the per-Work-Package "why" record is a real thing this repository
stopped producing, and this Academy's own later chapters are the cost
of that landing where it did.**

## How a release is now run: the `v0.18.0` Execution Plan

The clearest statement of the day-to-day process is
`docs/releases/v0.18.0/Execution Plan.md`, written by the chief engineer
the day `v0.17.0` was accepted. Its own header names its method exactly:
**"the `v0.17.0` method."** `v0.17.0` was run this way first; `v0.18.0`
is the same shape, applied a second time once it had proven itself.

Both releases sit inside `docs/releases/v1.0.0/WorkPackages.md`, which
states in five sentences **what `v1.0.0` is**: open a client project,
record engineering evidence as one immutable record, have a second
principal check and issue it, record time and raise an invoice, see
utilisation and margin on the Home screen — "everything that survives
... is kept because it serves one of those five sentences." Its
**sequencing rule**, "Substrates before surfaces," governs the order:
the evidence record sits on the SQLite persistence and dimension-vector
units delivered in `v0.17.0`, so later surfaces never inherit a
foundation still being decided. Each release carries a **developer-day
estimate** (`v0.17.0` 41, `v0.18.0` 37, `v0.19.0` 34, the `v1.0.0`
candidate 14, totalling 126 across a 21-week target — a target, not a
delivery date, with its own critical-path chain named so a slip is
reported rather than absorbed silently).

A release now runs on **one release branch** for its whole duration;
nothing merges to `main` until it is accepted. Work Packages group into
**waves by dependency and by the files they touch**, "so that agents in
one wave never edit the same file," and the full gate runs after every
merge. Each Work Package gets its own **worktree** — a separate,
disposable working copy — on a throwaway local branch that the lead
merges `--no-ff` into the release branch, then deletes; nothing but the
release branch is ever pushed.

Each brief states, in the plan's own words, "the seam map, the
acceptance test, the files it owns, the files it must not touch, and the
kill switch: stop and report if the acceptance test cannot be met
without touching a file outside the list." The plan also names three
tiers of agent for the work — a mid-sized model for implementation and
audits, a small one for counts and mechanical checks, and the largest
only where two attempts on the same Work Package have already failed
(§5 names them) — the lowest model that can do the job, not the largest
available.

Two habits close the risk parallel work would otherwise open: an agent
"commits a compiling checkpoint within its first hour and at every green
test run," and the full gate — both build configurations under
`TreatWarningsAsErrors`, every test in both, the health check at 5 of 5
— runs at every merge, findings "reported as they arise." Its §7, the
manual test script, becomes `PHYSICAL_REVIEW.md`'s next section directly
(see `08-the-physical-review-and-the-release-gate.md`); its §4 records
the Product Owner's actual answers taken while planning — independent
check off by default, SkiaSharp for the issue sheet, full-text search,
proceed with the archive — at the point they were made. The Introduction
chapter `01-how-tempestos-gets-built.md` walks this method for a
non-engineer reader; `Engineering Lifecycle.md` names which stages the
reset changed.

## The Markdown budget's edge case

One consequence is worth naming, because this chapter demonstrates it: a
documentation-only change adds Markdown lines and no code lines, so it
**fails the budget by construction**, however carefully it is written.
That is a known property of a rule built to stop prose outgrowing
product, not a defect in the work — the branch discloses the finding in
its own PR rather than being mistaken for a check the repository failed.

## What to take away

- **A process that costs more to maintain than the product it protects
  will eventually be abandoned, however sound its original reasoning
  was** — the only question is whether it is replaced deliberately, as
  here, or simply stops being followed.
- **Moving history with `git mv` is a third option between deleting and
  keeping everything live, and usually the right one**: nothing is lost,
  and nothing sits in the way of daily work either.
- **A rule that enforces a ratio will always have a legitimate case that
  fails it** — the fix is to disclose the case, not weaken the rule for
  everyone to accommodate it.
