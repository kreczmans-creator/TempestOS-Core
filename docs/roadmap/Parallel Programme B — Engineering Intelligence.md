# Parallel Programme B — Engineering Intelligence

**Part of** [Parallel Work Programme A–G](Parallel%20Work%20Programme%20A–G.md).
**Position in recommended order:** 6th, deliberately last.
**Status of every sub-package below:** Defined, not started (2026-09-05).
**Claude Code required for this programme:** No.

## Programme Purpose

The decision rules that turn reference data into engineering judgement:
how a material is chosen, how a process is chosen, what a design must
satisfy, what a review must ask, and how competing options are traded
off.

**Why this programme runs last.** Rules written before the data they
cite are confident and unsourced — the single most damaging failure mode
available to this whole programme. Every rule defined here is required
to cite a row in Programme A, a figure in Programme C, or a real worked
example in Programme F. Run this programme first and it will instead
cite recollection.

**Programme-level acceptance:** every rule is traceable to a source, a
dataset row, or a named real project. A rule that cannot cite anything
is recorded as **Unknown / unvalidated** and is not promoted into the
active rule set.

---

## B.1 — Material Selection Logic

**1. Purpose.** Make material choice repeatable: given a set of
requirements, produce a shortlist with the reasoning attached.

**2. Scope.** *In:* the selection criteria that actually decide
(strength, stiffness, mass, corrosion environment, temperature,
machinability, weldability, availability, cost); the ranking method;
the environment-driven exclusion rules; the substitution table. *Out:*
optimisation mathematics, and any criterion the business does not
genuinely apply.

**3. Required inputs.** `A.1` (Materials Database) populated; `A.7`
(process constraints); real past selections to test the logic against.

**4. Data / content fields.** `RuleID`; `Requirement Type`;
`Condition`; `Implication`; `Materials Favoured`;
`Materials Excluded`; `Reason`; `Exceptions`; `Source`;
`Confidence`; `Validated Against` (project or example).
Plus a selection matrix: criteria as columns, candidate materials as
rows, with a stated weighting basis.

**5. Outputs / artefacts.** `Material Selection Logic.md` (the rules);
`Material Selection Matrix.csv`; `Material Substitution Table.csv`;
a one-page selection checklist for use in real enquiries.

**6. Acceptance criteria.** Every rule cites a `MaterialID` from `A.1`
or a named standard. The logic is tested against at least three past
real selections and reproduces the choice actually made — or explains,
explicitly, why it does not. Weightings are stated, not implied.

**7. Dependencies.** `A.1` (hard); `A.7`, `C.2` (useful).

**8. Recommended next action.** Write the exclusion rules first
(environment and temperature rule materials *out* far more reliably than
any weighting rules them in).

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — as rule content behind a
materials advisor. Any product feature built on it is a separate
numbered Work Package with its own ADR.

---

## B.2 — Manufacturing Decision Trees

**1. Purpose.** Choose a manufacturing route from part characteristics
and quantity, with the reasoning recorded rather than assumed.

**2. Scope.** *In:* decision trees for process family selection by
geometry, material, tolerance, finish, quantity and lead time; the
make-versus-buy question; the tolerance-driven process escalation
points. *Out:* supplier choice (`C.5`) and price (`C.2`).

**3. Required inputs.** `A.7` (Manufacturing Process Library); real job
histories showing which route was actually chosen and why.

**4. Data / content fields.** `NodeID`; `Question`; `Answer Options`;
`Next Node`; `Terminal Recommendation`; `Rationale`;
`Cost Sensitivity`; `Quantity Break Point`; `Tolerance Trigger`;
`Common Mistake At This Node`; `Source`; `Confidence`.

**5. Outputs / artefacts.** `Manufacturing Decision Trees.md` (one tree
per part family, each rendered as an indented decision list);
`Process Selection Quick Reference.md`; a quantity break-point table.

**6. Acceptance criteria.** Every terminal recommendation names a
process present in `A.7`. Every quantity break point cites either real
quotation evidence or is marked **Unknown**. Each tree is walked against
two real past parts and reaches the route actually used.

**7. Dependencies.** `A.7` (hard); `C.2`, `C.3` (for break points).

**8. Recommended next action.** Build the sheet-metal-versus-machined
tree first; it is the most frequently contested real decision.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — as guided-decision
content in the Manufacturing Workspace.

---

## B.3 — Mechanical Design Rules

**1. Purpose.** Capture the standing design rules the business works to,
so they survive outside one person's head.

**2. Scope.** *In:* safety factor conventions by application class;
fits and tolerance selection; fastener and joint design rules; shaft and
bearing arrangement rules; weld and sheet-metal design rules;
stiffness-versus-strength guidance; standard clearances. *Out:* full
calculation methods (`E.2`) and anything that must be computed per case.

**3. Required inputs.** `A.6` (constants and conventions); `A.3`–`A.5`;
existing drawings, whose implicit rules are the honest starting set.

**4. Data / content fields.** `RuleID`; `Domain`; `Rule Statement`;
`Applies When`; `Does Not Apply When`; `Basis` (standard, calculation,
experience); `Consequence If Violated`; `Typical Value/Range`;
`Verification Method`; `Source`; `Confidence`; `Related Rules`.

**5. Outputs / artefacts.** `Mechanical Design Rules.md`, sectioned by
domain; `Design Rules Quick Card.md` (one page, the twenty rules that
matter most); a fits and tolerances selection table.

**6. Acceptance criteria.** Every rule states its basis and its
consequence if violated — a rule with neither is folklore and is marked
as such. No rule contradicts another; contradictions are resolved
explicitly, not left standing.

**7. Dependencies.** `A.3`–`A.6`. Feeds `B.4`, `E.1`, `E.2`, `F.2`.

**8. Recommended next action.** Extract the rules already implicit in
existing drawings; write them down before adding aspirational ones.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — as check content behind
design review and validation features.

---

## B.4 — Engineering Review Logic

**1. Purpose.** Make an engineering review a defined procedure with a
defined output, not a conversation whose depth depends on who is in the
room.

**2. Scope.** *In:* review types (concept, detail, pre-release,
pre-manufacture); the question set for each; severity classification;
disposition rules; the evidence a review must see before it can pass.
*Out:* the review record templates themselves — those are `E.4`.

**3. Required inputs.** `B.3` (design rules — reviews check against
them); past review findings, if any exist; the applicable standards from
`A.2`.

**4. Data / content fields.** `CheckID`; `Review Type`; `Category`;
`Question`; `Why It Matters`; `Evidence Required`;
`Pass Criteria`; `Severity If Failed` (Blocking / Major / Minor /
Observation); `Typical Finding`; `Related Design Rule`; `Source`.

**5. Outputs / artefacts.** `Engineering Review Logic.md`; one checklist
per review type; a severity and disposition definition table.

**6. Acceptance criteria.** Every check states the evidence required and
a pass criterion — a check that cannot be failed objectively is
rewritten or removed. Severity definitions are unambiguous enough that
two reviewers classify the same finding identically.

**7. Dependencies.** `B.3` (hard); `A.2`; feeds `E.4`.

**8. Recommended next action.** Write the pre-manufacture review
checklist first — it is the review whose omission costs real money.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — as review content in the
Verification Management Workspace (`WP 9.3A`).

---

## B.5 — Design Trade-off Framework

**1. Purpose.** Make trade-offs explicit, comparable and recorded, so a
design decision can be defended a year later.

**2. Scope.** *In:* the trade-off axes (cost, mass, performance, lead
time, risk, manufacturability, maintainability); the weighting method;
the scoring scale; the decision-record format; the sensitivity check.
*Out:* formal multi-criteria optimisation, and any axis the business
does not actually weigh.

**3. Required inputs.** `B.1`–`B.3`; `C.2`/`C.3` for the cost and
lead-time axes; real past decisions to calibrate against.

**4. Data / content fields.** `DecisionID`; `Decision Statement`;
`Options`; `Criteria`; `Weighting`; `Weighting Basis`; `Score per
Option`; `Scoring Basis`; `Weighted Result`; `Sensitivity Check`;
`Chosen Option`; `Why Not The Others`; `Assumptions`;
`Review Date`; `Outcome If Known`.

**5. Outputs / artefacts.** `Design Trade-off Framework.md`;
`Trade-off Matrix Template.csv`; `Decision Record Template.md`; at least
two worked, real examples.

**6. Acceptance criteria.** The framework is applied retrospectively to
two real past decisions and produces a defensible result. Every
weighting states its basis. "Why not the others" is mandatory — a
trade-off record with only the winner explained is not accepted.

**7. Dependencies.** `B.1`–`B.3`, `C.2`, `C.3`. This sub-package sits
last in the last programme by design.

**8. Recommended next action.** Fix the scoring scale and the weighting
basis first; everything else is a form around those two decisions.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — decision records map
naturally onto the project and review structures the product already
carries.
