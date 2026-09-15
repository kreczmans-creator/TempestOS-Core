# Test Determinism and Suite Hygiene

**Release:** v0.14.0 – v0.17.0 · **Work Package(s):** WP-F, WP-Z4
(Stages 4–15), WP 15.2A, WP 16.4A, WP 16.4A-R1, WP 17.0A, WP 17.0C ·
**Debt:** `TD-114`, `TD-119`, `TD-120`, `TD-34`, `TD-83`, `TD-100` ·
**Decision:** `ADR-0121`, `ADR-0122` ·
**Code:** `tests/Tempest.Core.Tests/Runtime/RunningHostFixture.cs`,
`tests/Tempest.Core.Tests/Logging/RecordingLogSink.cs`,
`stryker-config.json`

**In plain terms.** A test suite is the set of small, automatic checks
that run every time the software changes, to catch a mistake before a
person sees it. A "flaky" test passes sometimes and fails sometimes for
reasons that have nothing to do with whether the software works — which
trains everyone to re-run it and ignore the red, so the day it fails for
a real reason nobody notices. A test that can never fail, whatever is
wrong with the code, is worse than no test at all, because it sits in a
green report claiming to have checked something it never checked. This
chapter is TempestOS finding both problems across four releases and
building the habits — twice, the rules — that stop them coming back.

## Waiting versus guessing

Most of this history's flakiness has one shape: a click or a dialogue
answer returns before the background work it triggers — a command
dispatch, a disk write, a screen refresh — has finished. The lazy fix is
`Task.Delay(500)`, wait and hope; it fails exactly when a machine is
briefly slower than usual, which a shared CI runner produces on a bad
day. The fix used throughout is a **real join**: read the actual state
the assertion cares about, in a loop bounded by a real deadline
(typically two seconds), and stop as soon as it changes. The assertion
itself never changes, so a genuine defect still fails on its own
message. `RunningHostFixture`, met later in this chapter, is the idiom
at its plainest: poll live state, bound the wait, leave the assertion
alone.

## Two of four findings were wrong

`WP-F` (`3a9b777`, `TD-114`) is the chapter's first lesson, and it is
about the audit itself, not delays: an external review named four
test-hygiene problems, and each was checked rather than fixed on faith.
**F-11** (exact-count brittleness) was real, and became set assertions
over declared IDs. **F-12** claimed one test made the suite slow; it
boots in only 9 of 236 Desktop runs — the real cost was mandatory
collection-wide serialisation, and no code changed. **F-15** was real: a
helper with zero production callers and 32 test call sites was deleted.
**F-18** claimed "84 weak assertions"; inspection found 269 single-
assertion tests, nearly all legitimate contract tests, and only 8 that
could not fail by construction — 3 of those were real lifecycle checks a
scan cannot see. The result was one deletion and three strengthenings,
not eighty-four. **A finding is a claim to check, not a fact to act on.**

## Fifty-two fixed waits, in stages, until one was left

`WP-Z4` began when PR #5's CI run failed a different Desktop test on
Debug and Release at the identical commit. `b09a620` (Stage 5) named the
mechanism `TD-46` had already described at `v0.11.0`, and opened
`TD-119` rather than guessing: 64 `Task.Delay` calls across 16 Desktop
test files, 3 legitimate, 61 fixed waits, of which 52 were tracked for
remediation. `a1e64d2` (Stage 8) converted the three sites that had
actually failed on CI; `e7357b6` (Stage 11) converted 45 of the 52,
re-reading each assertion's own state; `f0fcad6` (Stage 13) converted
six helper-level waits inside acceptance-test helpers, adding
`RenderUntilAsync`, `SurfaceOrNull` and `ClickWhenPresentAsync`.
`c13f10e` (Stage 15) recorded the honest outcome: the 52 tracked waits
reduced to 1 across those three stages — `WorkflowInteractionTests.cs:335`,
whose cancelled path raises no signal to join — so `TD-119` moved to
**Partially resolved**, not Resolved.

## The suite that filled its own disk

`WP 15.2A` (`8b6cc1e`, closes `TD-120`) fixed a different mundane habit:
every Desktop test creates its own isolated persistence folder, and
nothing deleted any of them. Running the suite six times over exhausted
a container's disk. The fix nests every root under one shared per-run
parent, deleted once by a `PersistenceRootCleanupFixture` when xUnit
disposes the collection — closing a second gap along the way, where one
test class carried no collection attribute at all.

## Four debts, one sweep, and a race reopened eleven minutes later

`WP 16.4A` (`1dbb8ab`) closed four debts together. `TD-34`: a log sink's
failure handler wrote straight to process-wide `Console.Error`, racing
any test that redirected the same stream — fixed with an additive,
injectable writer. `TD-119`'s last retained wait was removed after 30
clean runs once its guarded path was shown to complete synchronously.
`TD-83` added a real window-resize test. `TD-100`: the headless test
host had no real rendering, so image decoding always reported a fake
1×1 result; `30f5cb2` turns on Skia so a decoding test proves decoding.
A Core-side sibling of `TD-120` and a latent `LifecycleTestLog` race were
closed the same day.

Hygiene does not stay fixed by itself. `7c4554d` found that
`RestartProofTests`, on a branch cut about eleven minutes before
`WP 16.4A` merged, redirected `Console.Out` without joining the
collection that exists to stop exactly that. The same commit fixed a
**test that could not fail**: a "golden corpus" migration test asserted
only that a record's ID and schema version survived a read, so a
migration silently dropping every other field would still pass. It now
compares every field-bearing branch — proven by deliberately making the
read path lossy and watching the strengthened assertion catch it.

## Five tests that could not fail

`WP 16.4A-R1` (`faa1735`, `151281b`), an independent mutation-testing
review, is the chapter's clearest statement of the theme. Two race tests
for the Materials and Requirements sweeps paused an interleaving using a
gate keyed to a collection's *name* — so if the read order the fix
depends on were ever reverted, the gate fired at the wrong moment and
the race never occurred; replaced with a gate keyed to arrival order,
which cannot make that mistake. A double-dispose race test ran one trial
of a two-thread race, measured to catch the bug about 1 time in 200;
looped 2,000 times instead, for 10-for-10 measured detection. The
golden-corpus gap reappeared and was closed the same way. A fifth fix
bounded every unbounded wait gate in the suite to 10 seconds and added a
CI hang collector. Every one of the five was proven broken, then proven
fixed, by reverting the real code and watching the test's verdict flip.

## Two decisions the release had already made, unwritten

The same review board's architecture pass found two real decisions that
had shipped with no ADR. `12d89e5` writes both. **`ADR-0121`**: a
test-only construction seam is an `internal` constructor, never a
container-visible registration. The first attempt at letting a test
target a different schema version used
`services.AddInstance(typeof(int?), someValue)` — a *global*
registration any other `int?` parameter would silently receive; the
container's own rule of exactly one public constructor rules out a
second public one too. The answer is one public constructor delegating
to an `internal` one reachable only from the test assembly.
**`ADR-0122`**: registering a service twice is now an error, not a
replacement (`TD-69`). An audit of roughly 330 call sites found nothing
that legitimately relied on silent overwrite, and one real bug it had
been hiding — a sample fixture registering the same service twice with
the first line already dead. First registration now wins; a second
throws unless a caller opts in with `allowReplace: true`.

## Scaling the clock, and staying off the real data

`WP 17.0A` (`eea8230`) generalised the timing lesson: every Desktop
render deadline is now scaled by an environment variable,
`TEMPEST_TEST_TIMEOUT_FACTOR`, defaulting to 1 so nothing changes
locally; CI sets 3, because a fixed ten-second deadline had already
passed quiet and failed loaded. The same commit stopped a test suite
from being able to write to or delete the real persistence-data root a
running TempestOS depends on.

## Ninety-three loops become one, and the suite runs in parallel

`WP 17.0C` is six commits run as one campaign, each verified against the
full suite before the next began:

| Commit | What it did |
|---|---|
| `1692c5e` | One `RunningHostFixture` replaces ~93 hand-copied polling loops, bounded by a real 30 s timeout instead of an unbounded spin. |
| `62e753f` | Of 83 classes tagged `"Console output capture"`, only 8 actually read the captured text; those move to a new `RecordingLogSink`. Found and fenced a second, previously-masked race: 15 classes building a real dynamic plugin assembly, unsafe to run concurrently — a dedicated collection. |
| `54d1806` | Strips console redirection from the remaining ~60 files outright, freeing them to run in parallel. Removing that serialisation exposed a further genuine race in two files legitimately capturing real console output; fixed at the source with a private, injectable writer. |
| `12f09f9` | 19 exact-count fixture pins become property assertions (no duplicate ID, every field present) plus one explicit structural check — an exact count is brittle whenever the number is incidental to the invariant. |
| `56d6d74` | Deletes six passing tests whose only purpose was documenting a known, still-open defect — a bug pinned as a permanently green test is a backlog row wearing a test's clothes. Moved to `BACKLOG.md`. |
| `fe10a42` | Adds Stryker.NET mutation testing, scoped to `Calculations/**` and `UnitsAndQuantities/**`, on a job gated to manual dispatch and a weekly schedule only. Also corrects ten places where "mutation-proved" had been written before any mutation tool existed — every one was a hand-run exercise, now labelled "hand-mutation-checked (not tool-produced)". |

## What the numbers say

Comparing the `v0.16.0` tree to `v0.17.0`: the local Core suite went
from about 5 minutes to 20 seconds–1 minute 20 seconds; the local
Desktop suite went from about 15 minutes to about 3 minutes. Core test
count fell from 5,153 to 4,961 and Desktop rose slightly, 500 to 508 —
by design in both directions: console-capture scaffolding and
defect-characterisation facts are gone; behaviour coverage is not.

## What this deliberately did not do

Mutation testing covers two namespaces, not the platform, and its job is
advisory — not in `gate`'s dependency list, so a broken score reports
without blocking a merge. `WorkflowInteractionTests.cs:335` still has
its one retained fixed wait, named and accepted rather than worked
around a second time.

## The rules now

- **No fixed delay in a test.** Wait for the condition the assertion
  reads, in a bounded, re-reading loop, and leave the assertion alone.
- **A finding is a claim to check, not a fact to act on.**
- **Prove a test can fail before trusting it** — revert the fix, watch
  it fail, restore the fix, watch it pass — and say honestly whether
  that was by hand or by a tool.
- **An exact count is not an invariant** unless the number itself is
  what is being protected.
- **A test-only construction path is an `internal` seam, never a
  container-wide registration**, and a container must refuse a silent
  second registration.
- **A test that only suppresses noise it never reads should not
  serialise anything;** a real log assertion belongs on a real sink.
- **A known, open defect belongs in the backlog, not pinned green in the
  suite.**

## What to take away

- **A test that cannot fail is worse than no test**, because it reports
  coverage a reviewer will believe.
- **A fixed delay is a guess wearing a test's clothes**; a real join
  waits for the thing that actually happened.
- **Suite hygiene is never finished by one Work Package** — the same
  race this release closed was reopened eleven minutes later by a
  branch that never saw the fix land.

## Related Documents

`02-testing-strategy.md` (the standard this chapter extends);
`04-continuous-integration.md` (the pipeline the mutation job and
`TEMPEST_TEST_TIMEOUT_FACTOR` run inside);
`../02 Runtime Architecture/46-the-ui-thread-and-blocking-calls.md` (the
same async-continuation shape, found in production code);
`08-the-physical-review-and-the-release-gate.md` (where the `v0.16.0`
review board that produced `ADR-0121`/`ADR-0122` sits in the release
process).
