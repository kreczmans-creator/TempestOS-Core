# ADR-0161: Second-Person Sign-Off Is One Global Switch, Off by Default, and a Self-Approval Says So

## Status

Accepted — Product Owner decision of 2026-10-01, recorded for `v0.23.0`:
*"This software is initially for a single-user consultancy, so EVERYTHING
needing a second person to verify/approve cannot be the case. Add into the
settings a switch to flick second-person sign-off on/off globally."*

Amends `ADR-0152` (the quote review's separation of duty, its amendment
§2) and `ADR-0148` (§4, evidence's independent-check rule). Builds on
`ADR-0041`/`ADR-0045` (Settings and Audit over one persistence store) and
`ADR-0146` (identity is one session principal).

## Context

`v0.23.0` made a quote's approval refuse the person who opened it,
submitted it, or changed a line since the last approval
(`ReviewerMustDifferFromAuthor`), and refuse outright when the submitter or
author is not on record (`AuthorUnknown`) — colour review board B1, with no
setting to turn it off. Evidence's own independent check
(`Evidence:IndependentCheck`, `ADR-0148`) refuses the author when on. For
the one-person consultancy TempestOS is first built for, the quote rule
made every quote unsendable: nobody else exists to approve it.

A survey of the code found exactly two rules of the form "a person may not
approve, check, verify or release what they authored, submitted or
changed": the two above. Reference-data verify and release
(`ReferenceReviewService`) need a signed-in principal holding the
`reference.verify`/`reference.release` permission, but have never required
the verifier, releaser and author to differ. Timesheets, calculations,
requirements, documents and the generic lifecycle's `InReview` carry no
such rule. `AssetGovernanceRules.SelfReviewed` (`TEMPEST-EAG-005`) is a
warning, never a refusal, and is unchanged.

## Decision

**1. One switch, one abstraction.** `Tempest.Core.Governance.ISignOffPolicy`
(`SignOffPolicy`) is registered once in `TempestHost`. It stores one
setting, `Governance.SecondPersonSignOff`, through `ISettingsProvider`, so
it is durable across a restart and the same answer for every module. Its
default is **off**; configuration `Governance:SecondPersonSignOff=true`
makes it on by default, and a saved value wins, the shape
`Evidence:IndependentCheck` already has. Every separation-of-duty rule
consults this policy rather than holding a switch of its own; a rule added
later does the same.

**2. Off relaxes only "must be somebody else".** With the switch off:

- `QuotationService.ApproveAsync` lets the author, submitter or a line
  editor approve, and does not refuse `AuthorUnknown`. Somebody must still
  be signed in (`NoPrincipalSignedIn`), and the revision still records who
  submitted and who approved.
- `EvidenceService.RecordCheckAsync`, with the independent-check rule on,
  lets the author check their own evidence; the check names them
  (`CheckerIdentityId`), and nobody signed in is still refused.

**3. The record is honest.** A self-approval made with the switch off is
marked: `QuotationRevision.SelfApproved` and `CheckRecord.SelfCheck` are
`true`, and the audit row's detail ends "self-approval: second-person
sign-off is off" (quote) or "Checked by its own author — self-approval:
second-person sign-off is off" (evidence). Both fields are new JSON members
with a `false` default, so a revision or check stored before this ADR reads
back `false` — true of it, since the rule was then on. An approval by a
person shown to differ is not marked, whichever way the switch is set.

**4. On is today's rule, exactly.** With the switch on, every B1 rule —
`ReviewerMustDifferFromAuthor` for author, submitter and line editors,
`AuthorUnknown` including the legacy first-revision author resolution, and
`CheckerMustDifferFromAuthor` — applies as before, with the same messages.
A service constructed by hand without a policy (only tests do) keeps the
rule on.

**5. Changing the switch is audited.** `SetSecondPersonRequiredAsync` writes
the setting and an audit row, action
`governance.second-person-sign-off.changed`, detail `Subject`, `OldValue`,
`NewValue` (`Off`/`On`), attributed to the signed-in principal at the time
of the change by `IAuditRecorder` as every row is. Setting the value it
already has records nothing. Settings → **Sign-off** writes through the
policy, never the raw setting.

**6. Surfaces.** Settings gains a **Sign-off** section with one switch
(automation name *Second-person sign-off*): "Second-person sign-off —
require a different person to approve quotes and verify/release records.
Off for a one-person consultancy." The Quote tab's review panel shows
"Self-approval allowed (second-person sign-off is off)." or "A second person
must approve (second-person sign-off is on).", drops the second-person
wording from its review line when off, and marks a self-approved revision
"(self-approved)".

## Consequences

**Positive:** a one-person consultancy can draft, approve, send and issue
its own work without inventing a second login; turning the rule on for a
larger team restores every B1 guard unchanged; nothing a self-approval
produces claims an independence it did not have, and every change of the
switch is on the audit trail.

**Negative:** with the switch off, an `R1` quote carries no independent
review — the record says so, but the quote still goes to the client. The
switch is global, not per module or per project, by the Product Owner's
own instruction. `ReferenceReviewService` does not consult the policy,
because it has no separation-of-duty rule to relax; if one is added it
must. The policy is a setting a signed-in person can change, not an
administrator-only control: TempestOS has one session principal
(`ADR-0146`), and the audit row is the control.

## Related Documents

`ADR-0148` (amended, §4); `ADR-0152` (amended, review amendment §2);
`ADR-0146`; `docs/reviews/RC PO Feedback Actions (2026-10-01).md`;
`docs/releases/v0.23.0/Release Notes.md`; `PHYSICAL_REVIEW.md` §7l (N5, N10).
