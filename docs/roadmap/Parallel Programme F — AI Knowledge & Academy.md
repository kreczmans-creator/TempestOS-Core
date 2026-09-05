# Parallel Programme F — AI Knowledge & Academy

**Part of** [Parallel Work Programme A–G](Parallel%20Work%20Programme%20A–G.md).
**Position in recommended order:** 5th (after A, G, C, E).
**Status of every sub-package below:** Defined, not started (2026-09-05).
**Claude Code required for this programme:** No.

## Programme Purpose

The knowledge layer: how the business asks an AI assistant for
engineering help reliably, how an engineer is taught the way this
business works, how designs are challenged, what has gone wrong before,
and what a good worked example looks like.

**Relationship to the existing Academy.** TempestOS already has an
Academy (`docs/academy/`) with its own register, index and standards —
but it teaches *how TempestOS is built as software*. Programme F is
**engineering-domain** teaching content: how engineering work is done.
The two are deliberately separate, exactly as
`docs/engineering/Engineering Principles.md` is separate from
`docs/academy/06 Engineering Standards/`. Nothing in this programme
edits the existing Academy or its registers.

**Standing rule for this programme:** every teaching artefact is built
from something that actually happened or actually exists. A worked
example from a real job teaches; an invented one rehearses.

---

## F.1 — Prompt Library

**1. Purpose.** Make AI assistance repeatable: known prompts, known
inputs, known output shape, known failure modes.

**2. Scope.** *In:* prompts for the recurring tasks — material
selection support, process selection, calculation checking,
specification drafting, review question generation, supplier enquiry
drafting, document summarising. Each with its required context, expected
output format, and known limitations. *Out:* anything requiring the
assistant to invent engineering data; and prompts that produce output
nobody checks.

**3. Required inputs.** The recurring tasks, observed honestly; the
reference data from Programmes A and C that a prompt must be *given*
rather than allowed to recall.

**4. Data / content fields.** `PromptID`; `Task`; `Category`;
`Prompt Text`; `Required Context/Attachments`; `Expected Output
Format`; `Verification Required` (what a human must check before the
output is used); `Known Failure Modes`; `Do Not Use For`;
`Example Input`; `Example Output`; `Version`; `Last Validated`.

**5. Outputs / artefacts.** `Prompt Library.md` grouped by task
category; `Prompt Usage Standard.md` (what must be checked before any AI
output is used in issued work).

**6. Acceptance criteria.** Every prompt states what a human must verify
before the output is used — a prompt without that field is not accepted.
Every prompt has been run at least once and its real output recorded. No
prompt asks for engineering values the assistant would have to invent;
data is supplied as context.

**7. Dependencies.** Programme A and `C.2` (as supplied context).

**8. Recommended next action.** Write the verification standard first.
It is the control that makes the rest of the library safe to use.

**9. Claude Code required?** **No** — this is prompt content, not code.

**10. TempestOS integration.** **Yes, later** — a plausible assistant
capability. Any such feature is a separate numbered Work Package with
its own ADR and its own security review.

---

## F.2 — Engineering Academy Structure

**1. Purpose.** Define how someone is brought up to speed on how this
business does engineering — in an order, with checkpoints.

**2. Scope.** *In:* the curriculum structure; module list and sequence;
learning objectives; the assessment checkpoints; the mapping from module
to the standard, rule or dataset it teaches. *Out:* writing every
module's own full content (that follows, module by module), and any
claim to formal accreditation.

**3. Required inputs.** `B.3` (design rules); Programme A datasets;
`E.1`/`E.2` (templates and calculations); the existing
`docs/academy/Contributor Learning Path.md` as a structural precedent,
not as content.

**4. Data / content fields.** `ModuleID`; `Title`; `Level`
(Foundation / Practitioner / Advanced); `Prerequisites`;
`Learning Objectives`; `Content Outline`; `Source Material`;
`Exercises`; `Assessment`; `Duration Estimate`; `Author`; `Status`;
`Last Reviewed`.

**5. Outputs / artefacts.** `Engineering Academy Structure.md`;
`Curriculum Map.csv`; `Module Template.md`; a learning path per role.

**6. Acceptance criteria.** Every module has objectives that can be
assessed. Every module cites the rule, dataset or artefact it teaches —
a module citing nothing has no source of truth. Prerequisites form a
directed sequence with no cycles.

**7. Dependencies.** `A.*`, `B.3`, `E.1`, `E.2`. Deliberately kept
distinct from `docs/academy/`.

**8. Recommended next action.** Write the curriculum map — module
titles, levels and prerequisites only — before writing any module.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — as engineering-domain
Academy content, kept separate from the software Academy.

---

## F.3 — What-If / Challenge Library

**1. Purpose.** Build the habit of attacking a design before reality
does.

**2. Scope.** *In:* structured challenge questions by domain (loading,
environment, manufacture, assembly, maintenance, misuse, end of life);
failure-mode prompts; the "what would have to be true" test;
worst-credible-case framing. *Out:* formal FMEA methodology as such —
though the question set is deliberately compatible with it.

**3. Required inputs.** `B.3`, `B.4`; `F.4` (real failures are the best
challenge questions); real project experience.

**4. Data / content fields.** `ChallengeID`; `Domain`; `Question`;
`Why This Matters`; `Typical Weak Answer`; `What Good Looks Like`;
`Related Failure` (link to `F.4`); `Applies To` (part / assembly /
system); `Severity If Missed`; `Source`.

**5. Outputs / artefacts.** `Challenge Library.md` grouped by domain;
`Design Challenge Card.md` (one page, used in reviews);
`What-If Session Guide.md`.

**6. Acceptance criteria.** Every challenge question states why it
matters and what a good answer contains. At least a quarter of the
questions link to a real recorded failure in `F.4` — questions grounded
in real failures are the ones that get taken seriously.

**7. Dependencies.** `B.3`, `B.4`, `F.4`.

**8. Recommended next action.** Write ten questions from the last real
problem encountered. That is a better start than a hundred generic ones.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — as review-support
content alongside `B.4`.

---

## F.4 — Failure & Lessons Database

**1. Purpose.** Make sure a mistake is made once. This is the highest
long-term value artefact in Programme F, and the one most easily lost.

**2. Scope.** *In:* failures, defects, non-conformances, rework, missed
requirements, commercial misjudgements and near-misses — internal and,
where genuinely instructive, published external cases. *Out:* anything
that identifies an individual as culpable; this is a record of causes,
not of blame, and is written so it can be read by anyone in the
business.

**3. Required inputs.** `D.5` (NCR register — the ongoing feed); project
histories; honest recollection, marked as such.

**4. Data / content fields.** `LessonID`; `Date`; `Category`
(design / manufacture / supply / commercial / process);
`What Happened`; `Detected When` (design / manufacture / test /
in service); `Root Cause`; `Contributing Factors`; `Cost of Failure`;
`Time Lost`; `How It Was Fixed`; `Lesson`;
`Rule Changed As A Result` (link to `B.3`, `D.5` or a template);
`Recurrence Since?`; `Source`; `Confidence`.

**5. Outputs / artefacts.** `Failure & Lessons Database.csv`;
`Lessons Learned Summary.md` (the recurring themes);
`Lesson Capture Template.md`.

**6. Acceptance criteria.** Every entry has a root cause and a lesson —
an entry with a description alone is a story, not a lesson. Every entry
states whether a rule, template or process changed as a result; "no
change made" is a valid, and revealing, answer. No entry names an
individual.

**7. Dependencies.** `D.5` (the ongoing feed). Feeds `B.3`, `B.4`,
`F.3`, `F.5`.

**8. Recommended next action.** Record the five most expensive problems
of the last two years, from memory, marked `Confidence: Inferred`. Waiting
for a perfect record is how this database stays empty.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — links naturally to
non-conformance and review records.

---

## F.5 — Worked Engineering Examples

**1. Purpose.** Show the whole method end to end on real problems —
requirement to concept to calculation to detail to verification to
manufacture.

**2. Scope.** *In:* complete worked examples on real (anonymised) jobs,
each exercising the data, rules, templates and calculations the earlier
programmes produced. *Out:* invented examples, and examples that skip
the unglamorous steps, which are usually the instructive ones.

**3. Required inputs.** Programmes A, B, C, E populated enough to be
cited; real completed projects; client permission or thorough
anonymisation.

**4. Data / content fields.** `ExampleID`; `Title`; `Domain`;
`Problem Statement`; `Requirements`; `Options Considered`;
`Trade-off Applied` (link to `B.5`); `Material Selected` and why
(link to `B.1`); `Process Selected` and why (link to `B.2`);
`Calculations Performed` (link to `E.2`); `Verification` (link to
`E.3`); `Manufacturing Outcome`; `Cost Outcome vs Estimate`;
`What Went Wrong`; `What Would Be Done Differently`;
`Artefacts Referenced`; `Anonymisation Applied`.

**5. Outputs / artefacts.** One document per worked example;
`Worked Examples Index.md`; the artefact set accompanying each example.

**6. Acceptance criteria.** Every example cites real artefacts from
Programmes A, B, C and E — an example citing nothing is a narrative.
Every example includes what went wrong; a worked example with no
difficulty in it is not credible and teaches nothing. Anonymisation is
recorded explicitly and checked before any external use.

**7. Dependencies.** `A.*`, `B.*`, `C.2`, `E.2`, `E.3`. Genuinely last
in this programme.

**8. Recommended next action.** Choose one completed job and list which
artefacts already exist for it. That gap list is the example's own work
plan.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — worked examples are
strong candidate case-study content, mirroring the existing
`docs/academy/05 Case Studies/` pattern in the engineering domain.
