# ADR-0127: P02 Reports What the Rules Concluded, and Never Decides

## Status

Accepted — `Group B` (P02 Engineering Intelligence), 2026-09-06.

## Context

`Group B` gives TempestOS engineering reasoning: material selection
logic, manufacturing decision trees, mechanical design rules, engineering
review logic and a design trade-off framework. Every one of those five is
one careless API away from becoming something the platform must not be.

The failure mode is not exotic. It is the ordinary shape almost every
engineering tool of this kind takes:

- A selection routine that returns "the recommended material".
- A decision tree whose terminal node is "use investment casting".
- A rule assessment whose result is a Boolean, so a design that passed
  reads as a design that is right.
- A review that reports no findings, when what happened is that nobody
  checked the things a rule cannot check.
- A trade study that multiplies weights by scores and prints a winner.

Each of those is defensible in isolation and indefensible in an
engineering record. They share one property: the software makes a claim
it has no standing to make, in a form that reads as authority. An
engineer who signs a drawing is accountable for it. Software that
produced the recommendation is not, and cannot be.

There is a second, quieter failure that enables all of the above: a
missing value silently reading as a satisfied condition. A material with
no recorded yield strength has not passed a yield-strength rule. A
process with no recorded tolerance capability has not been shown capable.
Any system that treats absence as compliance will, given enough records,
approve something it never examined.

## Decision

`P02` reports what the rules concluded, on the data recorded, at the
revisions it read. It never decides.

Concretely, and enforced in code rather than in guidance:

1. **Outcomes are eight-valued, not Boolean.** `AssessmentOutcome` is
   `NotEvaluated`, `Pass`, `Fail`, `Concern`, `NotApplicable`,
   `NotRecorded`, `EvidenceRequired` and `Indeterminate`. "Not recorded",
   "does not apply", "needs a person", and "the rule could not be
   evaluated" are four different answers and none of them is a pass.
   `AssessmentOutcomes.IsAffirmative` is true for exactly one member.

2. **Severity is explicit and never flattened.** `RuleSeverity`
   distinguishes `Prohibition`, `Requirement` and `Constraint` (binding)
   from `Warning`, `Recommendation` and `Advisory` (not). A rule that is
   not satisfied yields `Fail` when binding and `Concern` when not. "Must
   not" and "prefer" are not the same rule with a different weight.

3. **Absence never becomes a value.** No code path substitutes a default,
   treats a missing property as zero, or infers suitability from the
   absence of a failure. Applicability comes from `P01`'s own traits
   tables, so "there is none" is a fact the reference library asserted,
   never one `P02` invented; where a library cannot say, the answer is
   `NotRecorded`, not `NotApplicable`.

4. **A result says what it did not check.** `AssessmentScopeStatement`
   records how many released rules applied, how many ran, and how many
   applicable rules were skipped for not being released. An assessment of
   a subject nothing applies to says, in words, that it established
   nothing.

5. **Nothing returns a recommendation.** There is no method anywhere in
   `P02` that reads a set of results and returns a chosen option. A
   decision tree's terminal node names *candidate* process families and
   carries `RequiresHumanDecision`. A material selection reports
   `SatisfyingCandidates` — constraints satisfied, silent about
   everything not checked — and `RequiresHumanDecision` is
   unconditionally true. A trade study's decision is a
   `TradeStudyDecision` a caller constructs on behalf of a named person,
   and it cannot be constructed without a rationale and that person's id.

6. **A person may overrule any of it, and the record shows both.** An
   engineer's review finding replaces a rule's, and the rule's answer is
   retained as evidence. A trade-study decision that departs from the
   assessment is recorded and flagged, never blocked.

## Consequences

**What this costs.** `P02` is less immediately satisfying than a system
that answers. A caller wanting "which material should I use" gets a set
of candidates and a list of things nobody has established yet. Some
results will be mostly gaps, particularly early, when the rule libraries
are thin.

That cost is the point. A gap-heavy result is an accurate description of
a gap-heavy state of knowledge, and it is strictly more useful than a
confident answer derived from the same absence.

**What it buys.** Every conclusion `P02` records is attributable: to a
rule, at a revision, against a subject, at a revision, with a stated
reason. Every conclusion it does *not* reach is visible as such. And the
question "did a person decide this?" always has an answer, because a
decision without a person cannot be represented.

**On LLMs.** No part of the `P02` rule engine, decision walker or
assessment path calls a language model. The reasoning is deterministic,
inspectable and reproducible. A future capability may use a model to
*draft* a rule for an engineer to review and release — the governance
lifecycle already handles exactly that — but a drafted rule is a
proposal, and only the release gate makes it guidance.

## Alternatives considered

**A Boolean result with a separate "confidence" field.** Rejected: it
collapses four distinct engineering states into a number, and the number
invites arithmetic that means nothing.

**Returning a ranked list, with a note that it is advisory.** Rejected:
the note is read once and the ranking is read every time. Position in a
list is a recommendation whatever the surrounding prose says.

**Blocking a decision that departs from the assessment.** Rejected:
overruling a rule is a legitimate engineering act, and a system that
forbids it will be worked around. Recording it is what matters.
