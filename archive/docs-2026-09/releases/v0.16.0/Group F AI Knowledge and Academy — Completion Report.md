# Group F — AI Knowledge & Academy: Completion Report

**Programme:** P06 — AI Knowledge & Academy
**Work Packages:** F1 (`WP06.1`), F2 (`WP06.2`), F3 (`WP06.3`), F4 (`WP06.4`), F5 (`WP06.5`)
**Date:** 2026-09-06
**Branch:** `claude/tempestos-a4-bearing-library-unobtf`

---

## 0. Programme status — the honest four facts

| | State |
|---|---|
| **Framework** | Complete. Five governed libraries, 10 services, all registered in the real host. |
| **Authoritative knowledge** | **None.** Every library ships empty. |
| **Fictional test data** | Present, test-project only, carrying `KnowledgeOrigin.FictionalFixture`. |
| **AI execution** | **Deliberately absent**, and enforced three ways. |

| Gate | Result |
|---|---|
| Build, Debug | 0 errors, 0 warnings |
| Build, Release | 0 errors, 0 warnings |
| Tests, Debug | **4,789 / 4,789** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Tests, Release | **4,789 / 4,789** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Governance health check | **13 passed, 3 warned, 0 failed** of 16 |

The three warnings are pre-existing and environmental: no `v*` git tags
reachable in this container (two checks), two historical release folders
predating the `WorkPackages.md` convention.

`P06` added **177 tests** (4,612 → 4,789).

---

## 1. Numbering

| Package | Roadmap identifier | Subject |
|---|---|---|
| `F1` | `WP06.1` | Prompt library |
| `F2` | `WP06.2` | Engineering Academy structure |
| `F3` | `WP06.3` | What-if / challenge library |
| `F4` | `WP06.4` | Failure & lessons database |
| `F5` | `WP06.5` | Worked engineering examples |

Five packages. No `F6`. No subdivision.

---

## 2. The decision the programme rests on

`ADR-0139`: **three orthogonal axes**, and authority derived rather than
asserted.

| Axis | Question |
|---|---|
| `KnowledgeOrigin` | Where did it come from? |
| `KnowledgeReviewState` | Who has checked it? |
| `ReferenceValidationState` | How far did the record get through governance? |

`IsAuthoritative` is computed from an origin that can bear authority, a
competent review, and — for external content — a findable citation. No
caller can set it.

Two origins can never bear authority. `FictionalFixture`, because a
reviewed fiction is still a fiction; and `Unspecified`, because content
that does not say where it came from has nothing to be trusted on.

`MachineGenerated` is its own origin, and generated content no person has
reviewed is a validation **error**. Once reviewed it is authoritative like
anything else — the review is what confers it, not the generation.

---

## 3. What P06 will not do

Enforced three ways (`ADR-0140`):

1. **A reflection test** over every type in the namespace rejects any
   public method beginning `Execute`, `Invoke`, `Run`, `Complete`,
   `Generate`, `Infer`, `Grade`, `Score`, `Mark`, `Chat` or `Ask`.
2. **An assembly test** asserts `Tempest.Core` references nothing matching
   OpenAI, Anthropic, Azure.AI, Microsoft.ML, SemanticKernel, LangChain or
   HuggingFace.
3. **A host test** composes the real `TempestHost` and asserts no
   executor, agent, runner, completion or model-client type exists.

The guard is itself tested. Its first version matched forbidden verbs
anywhere in a name and flagged `IsMachineGenerated` and
`EvaluateDuplicateReferences`; a guard that cries wolf is a guard somebody
deletes, so it now matches a leading verb followed by an uppercase letter,
and a test asserts both what it catches and what it must not.

---

## 4. The five packages

### F1 / WP06.1 — Prompt library

Instruction, purpose, slots, constraints, known failure modes, human
review guidance. Missing review guidance is an **error**; a prompt asking
for something to be approved or certified is reported — a prompt may ask
for an assessment and must not ask for the act. 9 diagnostics,
`TEMPEST-KNP-001`–`009`.

### F2 / WP06.2 — Academy structure

One recursive `AcademyNode` with a kind, because subject, discipline,
module, lesson and concept are one concept at five depths rather than five
concepts (`ADR-0141`). `LearningOutcome` is a statement about the
*learner*, so it can be assessed; an outcome nothing assesses is reported.
Cycles and misplaced nodes are errors, and `FindPathToAsync` stops at a
cycle rather than looping. 12 diagnostics, `TEMPEST-KNA-001`–`012`.

### F3 / WP06.3 — Challenge library

`ReasoningArea` rather than an answer key; `ChallengeGuidance` explicitly
for a human marker. `IsOpenEnded` holds for design, trade-off and
estimation challenges whatever the guidance says, and an open challenge
admitting no alternative answer is reported. 9 diagnostics,
`TEMPEST-KNC-001`–`009`.

### F4 / WP06.4 — Failure & lessons database

An incident with no transferable lesson is an **error**.
`CauseConfidence` separates not-investigated from suspected, probable and
established, and an established cause with nothing checkable behind it is
an error. `IsVerifiedEffective` needs evidence, not a state — implemented
and effective are different things. Confidentiality is first-class: a
serious failure classified only Internal is flagged, and a shareable
lesson naming a party is flagged. 15 diagnostics,
`TEMPEST-KNL-001`–`015`.

### F5 / WP06.5 — Worked examples

Every step carries `Reasoning` separate from its working — a reader can
follow arithmetic without learning anything. A quantity with no unit and
no explicit dimensionless flag is an **error**. 11 diagnostics,
`TEMPEST-KNW-001`–`011`.

---

## 5. Persistence

The full §35 cycle against the real document-backed store, plus a blanket
JSON round-trip guard over every `P06` type — serialise, deserialise,
compare the rendered form. Nested structure, enums, nullable fields,
lists, `ReferencePin`s, dates and the whole provenance graph are asserted
individually.

No new persistence defect was found in `P06`. The guard exists so one
cannot arrive later unnoticed.

---

## 6. Registers

| Register | Before | After | Change |
|---|---|---|---|
| ADR Register | 138 | 141 | `ADR-0139`–`ADR-0141` |
| Architecture Document Register | 44 | 45 | `Group F AI Knowledge and Academy.md` |
| Namespace Register | 81 | 87 | Six `P06` namespaces |
| Interface Register | 283 | 293 | Ten `P06` interfaces |
| Governance Index | 138 ADRs stated | 141 | Corrected |
| Exception Register | 99 | 99 | Unchanged — `P06` declares no new exception type |

Three ADRs, one per genuine decision: provenance and derived authority; no
AI runtime dependency; structures knowledge without shipping it or judging
a response.

---

## 7. What P06 did not touch

No `WP16` work, no Desktop functionality, no release tags, no release
claims, no `P01`, `P02`, `P03`, `P05` or `P07` behaviour, no UI. The only
change outside `Tempest.Core.Knowledge` is the `P06` registration block in
`TempestHost`, added after `P05`'s.

---

## 8. Known gaps and deferred work

**No knowledge.** §0. Seeding the Academy would put platform-written
engineering instruction in front of learners carrying the authority of
being built in; seeding the lessons database would present invented
stories as organisational memory.

**No execution path.** Deliberately (`ADR-0140`). A caller wanting to run
a prompt builds that outside `P06`.

**No UI.** Deliberately, per the commissioning instruction.

**`F2` does not resolve activity citations.** An activity naming an `F5`
worked example or `F3` challenge is not checked against those libraries;
`F3`'s prerequisites are checked against the Academy but not the reverse.
A small addition, left for integration.

**`F4`'s disclosure check is a heuristic.** Scanning a shareable lesson
for "supplier", "client", " ltd" and similar catches the obvious cases and
will miss a name it does not recognise. It is a prompt to a human
reviewer, not a redaction tool, and is documented as such.

**No cross-library knowledge graph.** Lessons, challenges and examples
reference each other by string reference rather than through a resolved
graph. Sufficient for a foundation; a real Academy would want traversal.

---

## 9. Git

| Commit | Subject |
|---|---|
| `f444396` | P06 shared knowledge core |
| `6f981b6` | P06 F1 / WP06.1: prompt library |
| `6f81c38` | P06 F2 / WP06.2: engineering academy structure |
| `b3a56b1` | P06 F3 / WP06.3: what-if and challenge library |
| `74049d5` | P06 F4 / WP06.4: failure and lessons database |
| `075da1e` | P06 F5 / WP06.5: worked engineering examples |
| `0b49ad7` | P06: tests, host registration, and a sharper structural guard |

Branch: `claude/tempestos-a4-bearing-library-unobtf`. No pull request
opened; none was asked for.
