# The Attachment Sweep That Could Delete Live Content

*Companion to `ADR-0145`. Shipped in `v0.16.0` (`WP 16.4B`, remediated by
`WP 16.4B-R2` through `R5`); structurally resolved in `v0.17.0`
(`WP 17.1B`).*

**In plain terms.** Saving an attached file is really two writes: the
file's bytes, and a record saying "this item has this file." A
background housekeeping job also runs occasionally, deleting bytes
nothing points to any more, to reclaim space. A "race" is two things
running at almost the same moment, with the outcome depending on which
finishes first — like two people reaching for the same form at once. A
"lost update" is a race with a nastier shape: two writers each start
from the same snapshot, and whichever saves *second* overwrites the
first one's work completely, never having known the first writer
existed. This is the story of a housekeeping job that could, through
exactly this race, delete a file a user had just attached —
permanently, while the screen still said it was there — and of how many
reviews it took to close the problem rather than merely narrow it. No
user ever lost a file to it: every occurrence here was found before
release.

## A sweep built for a crash, not a race

`TD-97` recorded a real gap: deleting an object left its attachment
bytes on disk forever. `WP 16.4B` closed it with
`AttachmentContentReconciliationService`, an operator-run sweep
comparing every content-store key against every attachment Id any
object's durable state still names, deleting whatever nothing names.
`ADR-0114` already required writing a file's bytes *before* the record
naming them, so a crash between the two leaves inert bytes, not a
broken promise. The release plan asked for more: "a write-intent marker
before multi-file writes." The implementing stream substituted a
plainer two-read comparison instead — disclosed, but never re-checked
for safety under the condition the marker existed to cover. `TD-97` was
marked **Resolved** against this code the same day.

## Board 1: present, unreferenced, deleted

A nine-perspective review board ran against the integrated release
tree. Its security and persistence reviewers, independently, found the
race: the sweep read object states, then content keys. Attach a file;
the state read runs before the state write lands, sees nothing yet
referencing the content, and the later content-key read reports the
bytes orphaned. Deleted. The state write then lands anyway, naming a
file that no longer exists — reading states *first*, as shipped, made
this window *wider*, not narrower.

The first fix (`aa0daaf`) added a write-intent marker but sampled it
*last* — content, states, markers. The board's second pass proved this
only narrows the race: those three checks can still all read the wrong
way and delete a live file one read later. The corrected fix
(`ac562ce`) samples the marker strictly *between* the two scans —
content, markers, states — which is provably airtight: a key can only
look unmarked at that point once the marker has cleared, which can only
happen after the state write it protects has landed.

## Board 2: ordering writers is not the same as protecting them

A second, independent board of six reviewers ran against the remediated
tree, briefed to assume the first board had missed something. It had.
`EngineeringObjectBase.PersistStateAsync` captured an object's fields,
then wrote them to disk, with nothing serialising the pair. Two
concurrent mutations on one object could land their saves in the
opposite order to their captures — the later save, built from the
earlier snapshot, silently discarding the other's change (reproduced
against the real classes, trial 323 of 500). The `WP 16.4B-R2` marker
cannot see this: both writers complete cleanly and clear their own
markers, since a marker guards one write in flight, never the order
between two.

`WP 16.4B-R3` closed it with an `AsyncKeyedLock`, held across the whole
capture-then-save sequence, keyed by the object's `Id` rather than by
the calling instance — deliberately, because `ReviseAsync` builds a
*second* live instance for the same Id when an object is revised, and
an instance lock would never serialise that instance against its
predecessor. The fix's own doc comment said so: it was keyed that way
*because* `ReviseAsync` "creates a second live instance for one Id."

## Board 3: the lock's own justification was the gap

A third board — seven further reviewers, run against the release PR's
own tree — took that doc comment as its lead, and found that
`ReviseAsync` captured state and registered its successor *without ever
acquiring the lock it had just justified*. The reviewer reproduced the
consequence directly: attach a file, revise the object, rename the
revision — and the original attachment is gone from disk. With real
content attached, the predecessor's own save had already cleared the
write-intent marker, so the sweep saw content present, unmarked,
unreferenced, and deleted it as a genuine orphan (`fce2166`).

The successor is built from a snapshot taken at revision time, so a
predecessor write landing after that snapshot is invisible to it, and
the successor's next save overwrites the whole record — ordering two
writers is not the same as stopping one overwriting the other.
`WP 16.4B-R4` closed it in two parts, because one alone was not enough:
`ReviseAsync` now captures, hands off and registers *inside* the same
per-object lock, retiring the predecessor there; a later write through
a retired instance throws `SupersededEngineeringObjectException`
instead of silently landing — carefully enough that rename-then-revise,
what every `Revise*` command performs, still succeeds.

## Even the fix needed a fix

A fourth board, briefed that three boards running had each found a
defect inside the previous one's remediation, found a fourth.
`AttachContentAsync`'s durable sequence — mark, content, state, clear —
had no exception handling, because when written only a crash could
interrupt it. `WP 16.4B-R4` quietly falsified that: a concurrent
revision could now make the state write throw for an ordinary reason,
and the marker was never cleared — set forever, the bytes never
reclaimed, a permanent leak with no crash required.

The first version of `WP 16.4B-R5`'s own fix was also wrong, and the
existing suite caught it: it compensated for *every* exception, so a
failure *after* the state write had already landed deleted content a
live record still named. The correction narrows the compensation to one
exception, `SupersededEngineeringObjectException`, the only one known
to throw strictly before the write it guards — making deletion a true
rollback rather than a second data-loss path (`58c4cba`).

## A finding that wasn't a defect

Not every finding in this Work Package was a real defect, and the
register says so. `TD-67` had claimed `MoveToGroupAsync` appended new
relationship links without removing the old ones. Investigated against
the code, this turned out stale: the link is deliberately permanent
history, documented in `IRequirementsService`'s own contract, already
asserted by an existing test, and already the design
`EngineeringObjectBase.MoveAsync` used elsewhere — fixing it would have
reintroduced a bug an earlier Work Package had closed. `8fff32a`
corrected the register's own text, and referred the disagreement to
Technical Review rather than resolving it unilaterally. A "Resolved" or
"Open" row is a claim someone wrote, not a fact the code must agree
with.

## The actual fix: delete the mechanism, don't patch it again

Four boards, several Work Packages, and the underlying shape never
changed: more than one writer touched an object's truth, with no
boundary around them. Every remediation made the compensation more
careful. None made it unnecessary — and a compensating mechanism that
keeps needing a smarter version of itself is a signal the model
underneath it, not the mechanism, is wrong.

`v0.17.0`'s `WP 17.1B` (`ADR-0145`) took the other option: one durable
transaction per engineering change, under one lock, with the whole
compensating apparatus deleted rather than kept beside the fix — the
write-intent store, the reconciliation sweep, the rollback point and
the rollback-on-failure wrapper, roughly 1,802 lines per the `v0.17.0`
release notes. One transaction that commits completely or not at all
makes the whole *class* of race impossible, rather than narrowing this
shape of it once more; how it works is `02 Runtime Architecture/
54-one-transaction-per-engineering-change.md`'s subject, not this one.

Grepping the tree today for `WriteIntent`, `Reconciliation` or `Sweep`
confirms it: no `AttachmentWriteIntentStore`, `IAttachmentWriteIntentStore`
or `AttachmentContentReconciliationService` exist under `src/` any
longer — `EngineeringObjectBase`'s own remarks on `AttachContentAsync`
name all three and say why: "they compensated for a failure mode … that
this method can no longer produce." (Requirements' and Materials' own
reconciliation sweeps are a different, untouched mechanism for a
different debt row.)

## What to take away

- **Ordering two writers is not the same as stopping one overwriting
  the other from a stale copy** — a lock that only sequences access
  still lets a snapshot taken before a change be saved after it,
  discarding the change entirely.
- **A "Resolved" row in a register is a claim, not a fact** — `TD-97`
  was marked Resolved against code never reasoned about under
  concurrent access, and stayed marked Resolved through the whole chain
  of fixes that followed.
- **A compensating mechanism that keeps needing a cleverer version of
  itself is telling you the model is wrong, not that you haven't found
  the right patch yet** — four review boards made this one more
  careful; only deleting it, in `v0.17.0`, actually closed the class of
  defect it was built to catch.
