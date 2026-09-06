# Group F — AI Knowledge & Academy

**Programme:** P06 — AI Knowledge & Academy
**Namespace:** `Tempest.Core.Knowledge`
**Governing ADRs:** `ADR-0139`, `ADR-0140`, `ADR-0141`
**Status:** Architecturally complete, `Group F`. Every library ships
**empty** — see §10.

---

## 1. Purpose

`P01` established what is true. `P02` established what follows. `P03`,
`P05` and `P07` established what things cost, what engineering work
produces, and what the organisation is committed to. `P06` establishes
**what the organisation knows** — and, just as importantly, how well it
knows it.

Five questions, and a sixth about every answer:

| | Question | Package |
|---|---|---|
| 1 | How do we ask for this well? | `F1` / `WP06.1` |
| 2 | What should somebody learn, and in what order? | `F2` / `WP06.2` |
| 3 | What questions make an engineer think? | `F3` / `WP06.3` |
| 4 | What went wrong before, and what did we learn? | `F4` / `WP06.4` |
| 5 | What does doing this properly look like? | `F5` / `WP06.5` |
| 6 | *Where did that come from, and who has checked it?* | all |

The sixth is what separates a knowledge base from a pile of documents.

---

## 2. What P06 is not

- **Not an AI runtime.** No executor, no agent, no model binding, no
  provider dependency — enforced three ways (`ADR-0140`).
- **Not a tutor.** No grading, no scoring, no adaptive sequencing.
- **Not a curriculum.** `F2` is the shape an Academy takes, not its
  content.
- **Not the repository's own `docs/academy/`.** That is developer
  documentation *about TempestOS*. This is a structure for teaching
  *engineering*, held in the platform for its users. The name collision is
  unfortunate and the two never meet.

---

## 3. The shared core

Three files, and the first is the reason the programme exists.

### `KnowledgeProvenance` — three orthogonal axes

| Axis | Question |
|---|---|
| `KnowledgeOrigin` | Where did it come from? |
| `KnowledgeReviewState` | Who has checked it? |
| `ReferenceValidationState` | How far did the *record* get through governance? |

A Released, Validated record of Authored content nobody has reviewed is an
accurate description of a real situation, and all three axes are needed to
say it (`ADR-0139`).

**`IsAuthoritative` is derived and never set.** It requires an origin that
can bear authority, a competent review, and — for external content — a
citation specific enough to find. Two origins can never bear authority:
`FictionalFixture`, because a reviewed fiction is still a fiction, and
`Unspecified`, because content that does not say where it came from has
nothing to be trusted on.

**`MachineGenerated` is its own origin**, and unreviewed generated content
is a validation **error**. Generated content is raw material; a person's
review is what makes it knowledge — and once reviewed it is authoritative
like anything else. That is the point.

**Citations must be findable.** A title and an author are not enough:
editions differ, and a value from the wrong edition is a wrong value.

### `KnowledgeApplicability` and `KnowledgeEnquiry`

Discipline (reusing `P05`'s `EngineeringDiscipline` rather than a second
vocabulary), topics, level, audience, exclusions, validity. A reader
asking at `Intermediate` is offered introductory material too, and never
specialist material.

### `KnowledgeGovernanceValidation`

The provenance checks every library shares, `TEMPEST-KNG-001`–`016`.

---

## 4. Governance and storage

| Library | Document kind |
|---|---|
| `KnowledgePrompts` | `KnowledgePrompt` |
| `KnowledgeAcademy` | `KnowledgeAcademyNode` |
| `KnowledgeChallenges` | `KnowledgeChallenge` |
| `KnowledgeLessons` | `KnowledgeLessonRecord` |
| `KnowledgeWorkedExamples` | `KnowledgeWorkedExample` |

Each on `ReferenceDataCatalog<TDefinition>` with the full lifecycle, and
each with its own diagnostic prefix: `TEMPEST-KNP` (prompts), `-KNA`
(Academy), `-KNC` (challenges), `-KNL` (lessons), `-KNW` (worked
examples), over the shared `-KNG`.

---

## 5. F1 / WP06.1 — Prompt library

Instruction, purpose, input and output slots, constraints, known failure
modes, and what a person must check before relying on the output.

`HumanReviewGuidance` missing is an **error** — every prompt in an
engineering context produces something somebody must check, and one that
does not say what checking looks like is an invitation to skip it. A
prompt whose instruction asks for something to be approved, certified or
signed off is reported: a prompt may ask for an assessment and must not
ask for the act.

---

## 6. F2 / WP06.2 — Academy structure

One recursive `AcademyNode` with a kind — subject, discipline, module,
lesson, concept — because these are one concept at five depths rather
than five concepts (`ADR-0141`). `CanContain` enforces strict narrowing.

Nodes reference their parent rather than nesting, so a module can be
revised without rewriting the subject above it. `FindPathToAsync` stops at
a cycle rather than looping.

`LearningOutcome` is a statement about the learner — "can calculate the
maximum bending stress", not "covers beam bending" — because only the
first can be assessed. An outcome nothing assesses is a promise nobody
checks, and validation says so.

Activities cite `F5` worked examples and `F3` challenges by reference, so
one example serves a lesson, a challenge and a standalone reference
without being copied into any of them.

---

## 7. F3 / WP06.3 — What-if and challenge library

`ReasoningArea` rather than an answer key, because an open design
challenge has no single right answer. `ChallengeGuidance` is explicitly
for a **human marker**.

`IsOpenEnded` is true for design, trade-off and estimation challenges
whatever the guidance says, and an open challenge whose guidance admits no
alternative is reported.

`DeliberateOmissions` is its own field because what a challenge withholds
is often the whole point.

---

## 8. F4 / WP06.4 — Failure and lessons database

**The lesson is the point.** An incident with no transferable lesson is an
**error**, and a lesson filed under nothing is a lesson nobody finds — so
applicability is checked too, and `FindApplicableLessonsAsync` frames the
lookup as "what should I know before I start this?".

`CauseConfidence` separates `NotInvestigated`, `Suspected`, `Probable` and
`Established`. Most failures are never fully investigated, and a database
recording every plausible story as a root cause teaches wrong lessons
confidently.

**Implemented and effective are different things**, and the gap between
them is where organisations learn nothing twice. `IsVerifiedEffective`
requires evidence, not a state.

**Confidentiality is first-class.** A serious failure classified only
Internal is flagged, and a lesson marked shareable whose text names a
customer, supplier or company is flagged — a shareable lesson should carry
the learning without the parties.

---

## 9. F5 / WP06.5 — Worked engineering examples

**The reasoning is the content.** Every step carries `Reasoning` separate
from its working, because a reader can follow arithmetic without learning
anything. `IsInstructive` requires that no step is unexplained.

`WorkedValue` keeps the unit as its own field, so validation can ask
whether a quantity has one. A quantity with no unit and no explicit
dimensionless flag is an **error**: unit mistakes are the commonest way an
engineering calculation goes wrong, and an example that omits them teaches
the habit.

Where the platform did the arithmetic, the example links the `E2`
calculation pack rather than restating results (`ADR-0137`).

---

## 10. What ships

**Every library ships empty.** No prompt, no lesson, no module, no
challenge, no worked example, no failure.

Seeding the Academy would put engineering instruction written by a
platform in front of learners, carrying the authority of being built in.
Seeding the lessons database would present invented engineering stories as
organisational memory. Both are worse than an empty library.

Test fixtures carry `KnowledgeOrigin.FictionalFixture` wherever the test
is about behaviour, and that origin can never become authoritative
however it is reviewed. Registering fictional content in any library is a
validation error. Fixtures live only in the test project, backed by
in-memory stores that die with the test.

---

## 11. Dependencies

`P06` depends on:

- **`P01`'s shared reference-data layer** for the lifecycle and
  `ReferencePin`.
- **`P07`** for `EffectivePeriod` and `ConfidentialityClassification`.
- **`P05`** for `EngineeringDiscipline` and `EngineeringEvidence`, and to
  confirm a cited `E2` calculation pack exists — optionally, as with every
  cross-library collaborator here.

`P06` does not depend on `P02` or `P03`, and nothing depends on `P06`.

**No model or provider assembly is referenced**, asserted by a test over
`Tempest.Core`'s own referenced assemblies.
