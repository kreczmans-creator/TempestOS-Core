# ADR-0141: P06 Structures Knowledge, Ships None of It, and Never Judges a Response

## Status

Accepted — `Group F` (P06 AI Knowledge & Academy), 2026-09-06.

## Context

Three decisions belong together because they are the same restraint
applied to `F2`, `F3`, `F4` and `F5`.

**What to ship.** An empty Academy is unsatisfying, and the temptation is
to seed it: a module on beam bending, a lesson on tolerance stacks, some
worked examples. Every one of those would be engineering instruction
written by a platform rather than by a competent engineer, and it would
arrive carrying the authority of being built in. The same applies to `F4`:
a lessons database seeded with plausible failures is a set of engineering
stories with no basis, presented as organisational memory.

**Whether to grade.** `F3` holds challenges, and a challenge invites a
response. Automated grading is the obvious feature and it is wrong for
open engineering problems: a design challenge has no single right answer,
a trade-off is a judgement between defensible positions, and an
estimation problem is testing whether the responder notices what is
missing. A score attached to any of those is a judgement pretending to be
a measurement.

**How deep to model the hierarchy.** The Academy has subjects,
disciplines, modules, lessons and concepts. Five types, or one with a
kind?

## Decision

### Ships empty

`F2`, `F3`, `F4` and `F5` ship **no content**. No lesson, no module, no
challenge, no worked example, no failure. The structures are the
deliverable; filling them is the organisation's own work with its own
records and its own engineers.

Test fixtures are fictional and marked as such through
`KnowledgeOrigin.FictionalFixture` (`ADR-0139`), which can never become
authoritative.

### Never judges

**`F3` has no evaluator, no scoring and no adaptive sequencing.** What it
holds instead:

- `ReasoningArea` — what a good response *engages with*, rather than an
  answer key, with `IsEssential` marking what a response cannot ignore.
- `ChallengeGuidance` — explicitly guidance for a **human marker**: what
  a strong response looks like, common mistakes, and acceptable
  alternatives.
- `IsOpenEnded` is true for design, trade-off and estimation challenges
  **whatever the guidance says**, because a design challenge with one
  accepted answer is a problem mislabelled — and an open challenge whose
  guidance admits no alternative is reported.

`F4` applies the same restraint to causes. `CauseConfidence` separates
`NotInvestigated`, `Suspected`, `Probable` and `Established`, because most
failures are never fully investigated and a database recording every
plausible story as a root cause teaches wrong lessons confidently. A cause
marked `Established` with nothing checkable behind it is an **error**.

`F5` applies it to teaching. `IsInstructive` requires that every step
explains *why*, not merely what — a reader can follow arithmetic without
learning anything.

### One recursive node, not five types

`AcademyNode` carries an `AcademyNodeKind`, and
`AcademyNodeKinds.CanContain` enforces strict narrowing. This is a
deliberate exception to the "distinct types for distinct concepts" rule
the other programmes follow: subject, discipline, module, lesson and
concept are not five concepts, they are one concept at five depths, and
five near-identical types would duplicate the containment rule five times.

A node may contain anything strictly narrower, not merely the next level
down — a module holding concepts directly is a reasonable curriculum. What
is forbidden is a lesson containing a subject.

Nodes reference their parent rather than nesting, so the hierarchy is a
graph across records and a module can be revised without rewriting the
subject above it. `FindPathToAsync` stops at a cycle rather than looping:
validation reports the malformed hierarchy, and a read must not hang
because of one.

## Consequences

**The Academy is empty on delivery**, and its first user has to author a
curriculum. That is more work than a seeded one and produces material
somebody competent stands behind.

**"Is this answer right?" is not a question `P06` answers**, and the
libraries are built so that nobody can add it casually — the guidance is
addressed to a person throughout, and `ADR-0140`'s reflection test rejects
a `Grade` or `Score` method.

**A malformed Academy is reported, not prevented.** Cycles and misplaced
nodes are validation findings, so a half-built curriculum is still
recordable while somebody sorts it out.
