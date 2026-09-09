# ADR-0139: Knowledge Carries Its Origin and Its Review State, and Authority Is Derived From Both

## Status

Accepted — `Group F` (P06 AI Knowledge & Academy), 2026-09-06.

## Context

A knowledge base is only as good as its worst entry that somebody
believes. `P06` will eventually hold prompts, lessons, worked examples,
challenges and Academy content, arriving from very different places: a
published standard, a textbook, a named engineer's experience, an expert's
opinion, a machine, and — during development — a test fixture.

Two failures follow if those are not distinguished.

**The first is that everything reads equally.** Content in a library looks
authoritative by being in the library. A handbook value and somebody's
recollection render identically, and a reader has no way to tell which is
which.

**The second is specific to this decade.** Machine-generated content is
fluent, plausible and cheap, and it will be produced faster than anybody
reviews it. A knowledge base that cannot mark it as such will fill with
generated material that reads exactly like reviewed material — and the
generated engineering value that is subtly wrong is the one that survives
longest, because nothing about it looks wrong.

Test fixtures are the same problem in miniature. Fixture content is
written to exercise code, is realistic by design, and is exactly what
ends up in a production library when somebody seeds a database with the
test data.

## Decision

**Three orthogonal axes**, and no collapsing of any pair.

| Axis | Question | Type |
|---|---|---|
| Origin | Where did it come from? | `KnowledgeOrigin` |
| Review | Who has checked it? | `KnowledgeReviewState` |
| Lifecycle | How far did the *record* get through governance? | `ReferenceValidationState` |

A Released, Validated record of Authored content that nobody has reviewed
is an accurate description of a real and common situation, and the model
must be able to say it.

**`KnowledgeOrigin` has ten values**, including two that can never become
authoritative:

- `FictionalFixture` — because a reviewed fiction is still a fiction.
  This is the single guard that keeps test data out of the knowledge base,
  and registering it in any library is a validation **error**.
- `Unspecified` — content that does not say where it came from has
  nothing to be trusted on.

**`MachineGenerated` is its own origin**, and content carrying it that no
person has reviewed is a validation **error**, not a warning. Generated
content is legitimate raw material; a person's review is what makes it
knowledge. Once reviewed it can be authoritative like anything else —
that is the point. The review is what confers it, not the generation.

**`IsAuthoritative` is derived and never set.** Three conditions must all
hold: an origin that can become authoritative, a review by a competent
person, and — for content taken from an external source — a citation
specific enough to find. No caller can assert authority the facts do not
support.

**Citations must be findable.** A title and an author are not enough:
editions differ, and an engineering value from the wrong edition is a
wrong value. `KnowledgeCitation.IsSpecific` requires an identifier, or a
title with an edition or a year.

## Consequences

**Most content in a new knowledge base is not authoritative**, and the
model says so plainly. That is the true state of a library somebody has
just started filling.

**Fictional fixtures cannot be promoted, ever.** If an organisation wants
fixture-derived content in its library it must re-author it under a real
origin — which is the correct amount of friction.

**Generated content is usable and never silently authoritative.** The
platform neither refuses it nor trusts it; it records what it is and
requires a person before it counts.

**Three axes is more to fill in than a `IsApproved` Boolean.** Accepted:
the Boolean is the thing that makes a knowledge base untrustworthy over
time, and it fails silently.
