# ADR-0138: Verification, Review, Decision and Approval Are Distinct Records, and Missing Evidence Is Never a Pass

## Status

Accepted — `Group E` (P05 Engineering Assets), 2026-09-06.

## Context

This is `ADR-0127`'s rule for engineering conclusions and `ADR-0135`'s for
procurement, applied to the two places `P05` records engineering
judgement.

Six things circulate around a design review and are habitually collapsed
into two or three:

| | Who is saying it | What it commits |
|---|---|---|
| **Observation** | A reviewer noticed something | Nothing |
| **Recommendation** | That reviewer suggests something | Nothing |
| **Action** | Somebody agreed to do something | That person |
| **Decision** | Somebody settled a question | The engineering position |
| **Outcome** | The reviewers collectively | Their judgement that work may proceed |
| **Approval** | A person with authority | The organisation |

Collapsing any pair loses information the record exists to hold. The
common collapse is observation into action — "it was raised, so it will be
fixed" — and the expensive one is outcome into approval, which turns a
meeting's view into the organisation's commitment.

Verification has its own version. A verification artefact has a state, and
the tempting state model is a Boolean: verified, or not. But "nobody has
done it yet", "somebody did it and it did not settle the question", "the
requirement does not apply here" and "it failed" are four different
situations, and only one of them is a problem in the design. Worse, a
Boolean defaulting to false is harmless while a Boolean defaulting to true
— or a `Pass` reachable without evidence — turns an empty record into a
claim of compliance.

## Decision

### Six records, not one

`DesignReviewPack` carries `ReviewObservation` (with `Recommendation` as
its own field, because the suggestion is the observer's and not the
meeting's), `ReviewAction`, `ReviewDecision`, `ReviewOutcome` and
`Approval` as five separate things.

- A `ReviewDecision` refuses construction without a statement, a
  rationale and a named person. A decision nobody can explain cannot be
  reviewed later.
- A `ReviewAction` without an owner is reported: an unowned action is a
  wish.
- `ReviewOutcome` is the reviewers' collective judgement. `Approval` is a
  `BusinessAuthorisation`. A pack can conclude `Proceed` with the
  organisation having approved nothing, and that is an accurate state.
- **`ProceedsOverBlockingObservations` is a validation error.** Concluding
  that work may proceed while a `Critical` observation has no action and
  no decision against it means somebody accepted it without being recorded
  as accepting it. Accepting a critical finding is legitimate; doing so
  invisibly is not.

### Six standings, and one of them means demonstrated

`VerificationStanding` is `NotPerformed`, `InProgress`, `Inconclusive`,
`Failed`, `Passed`, `NotApplicable`.

- `VerificationStandings.IsDemonstrated` is true for `Passed` alone — not
  for `NotApplicable`, which means the question does not arise rather than
  that the answer is yes.
- `Weakest` over an empty set is `NotPerformed`, never `Passed`.
  Verifying nothing is not verifying everything.
- `VerificationArtefact.Standing` is **derived from the result**, not
  settable, so no caller can record a pass without a result that says so.
  A blank `NotApplicableReason` leaves the artefact at `NotPerformed`
  rather than letting an empty string retire a requirement.
- **`IsUnsupportedPass` is a validation error.** A recorded pass with no
  locatable evidence is the exact route by which missing evidence becomes
  a claim of compliance.
- Declaring a requirement inapplicable without saying why is also an
  error. That is how a requirement gets quietly dropped.

### Reporting, never repairing

`VerificationTraceService` answers "is this requirement verified, and how
do we know?" and changes nothing. A requirement with no artefacts comes
back `NotPerformed` with a concern saying so — never an empty clean
result. Its concerns distinguish "verified" from "verified, on the
asserting party's own material, against a requirement revision nobody
pinned", which is the more common situation.

## Consequences

**A review pack is more work to fill in** than a list of comments with
tick boxes. That is the intended trade: the extra fields are the ones
somebody needs eighteen months later.

**"Is this design verified?" frequently answers "no, and here is what is
missing."** For a project part-way through, that is the true answer and a
Boolean would have said "not verified" with no way to tell an unstarted
verification from a failed one.

**Nothing in `P05` approves anything**, enforced by a reflection test
rejecting any public method named `Approve`, `Authorise`, `SignOff`,
`Certify` or similar across the namespace. `P05` records approvals a named
person gave.
