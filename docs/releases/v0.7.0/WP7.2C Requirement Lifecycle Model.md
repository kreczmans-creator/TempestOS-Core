# WP 7.2C — Requirement Lifecycle Model

## Status

Contractual state model only. **No workflow logic is implemented or
designed here** — this document defines which `RequirementStatus`
transitions are permitted, not how, when, or by whom a transition is
triggered in practice.

## Purpose

Architecturally defines the permitted lifecycle for `RequirementStatus`
(`WP7.2C Requirements Platform Contracts.md` §8), using the seven
example states this Work Package's own controlling instruction names
(Draft, Reviewed, Approved, Allocated, Verified, Satisfied, Obsolete).

## The Governing Distinction: Status Is Workflow Position, Never Derived From Verification Outcome

Per `WP7.2B Requirements Domain Model.md` §9, `RequirementStatus` is a
caller-driven workflow position — it is never automatically computed
from a `VerificationRecord`'s own `Outcome`. This has one direct,
important consequence for the `Verified` and `Satisfied` states
specifically: **recording a verification (`IVerificationService.
RecordAsync`) never itself changes a requirement's own `Status`.** A
caller who has reviewed a `Pass`-outcome `VerificationRecord` decides,
separately, to call `SetStatusAsync(requirementId, RequirementStatus.
Verified, ...)`. This deliberate decoupling means `IRequirementsService`
and `IVerificationService` remain independent — recording a verification
outcome never has a hidden side effect on a requirement's own workflow
state, and setting a requirement's own status to `Verified` never
requires a `VerificationRecord` to exist (though a caller would, in
practice, be expected to have one before making that call — an
expectation this contract does not, and should not, enforce
mechanically).

## State Diagram

```
                    ┌─────────┐
              ┌────►│  Draft  │◄────┐
              │     └────┬────┘     │
              │          │          │
              │          ▼          │
              │     ┌──────────┐    │
              └─────┤ Reviewed │    │
                    └────┬─────┘    │
                         │          │
                         ▼          │
                    ┌──────────┐    │
              ┌─────┤ Approved ├────┘
              │     └────┬─────┘
              │          │
              │          ▼
              │     ┌───────────┐
              └─────┤ Allocated │
                    └─────┬─────┘
                          │
                          ▼
                    ┌──────────┐
              ┌─────┤ Verified │
              │     └────┬─────┘
              │          │
              │          ▼
              │     ┌───────────┐
              └─────┤ Satisfied │
                    └───────────┘

   Every state above (including Draft) may also transition
   directly to:

                    ┌──────────┐
                    │ Obsolete │  (terminal — no transition out)
                    └──────────┘
```

## Permitted Transition Table

| From | To | Rationale |
|---|---|---|
| `Draft` | `Reviewed` | Forward progression — the requirement's own statement is considered stable enough for review. |
| `Reviewed` | `Draft` | Review found the statement needs rework — sent back, not rejected outright. |
| `Reviewed` | `Approved` | Review accepted the statement as-is. |
| `Approved` | `Draft` | Re-opened — a later finding requires the statement itself to change; an approved requirement is not permanently locked against revision (Principle 2, revision history remains explicit and unbroken regardless of status). |
| `Approved` | `Allocated` | The requirement has been allocated to a target (`WP7.2C Relationship Model.md` §3). |
| `Allocated` | `Approved` | An allocation was removed or changed, and no replacement allocation yet exists. |
| `Allocated` | `Verified` | A caller has reviewed recorded verification evidence and judges the requirement demonstrated (see "The Governing Distinction," above — never automatic). |
| `Verified` | `Allocated` | A later verification attempt recorded a `Fail`/`Conditional` outcome the caller judges significant enough to revoke the `Verified` status — a caller decision, not an automatic reaction to `RecordAsync`. |
| `Verified` | `Satisfied` | The requirement is judged fully satisfied by its own allocated target's own real, delivered design/implementation — a judgement beyond verification alone (verification demonstrates a claim; satisfaction confirms the claim is also what the delivered target actually does). |
| `Satisfied` | `Verified` | Satisfaction is revoked — e.g., the allocated target changed and no longer demonstrably satisfies the requirement. |
| **Any state** | `Obsolete` | The requirement is withdrawn, superseded, or descoped — always permitted, from any state, mirroring how a real engineering programme may retire a requirement at any lifecycle point, not only at the end of a linear sequence. |
| `Obsolete` | *(none)* | Terminal — no transition out is permitted. A requirement mistakenly marked `Obsolete` is corrected by creating a new requirement, or (if the owning implementation Work Package judges this too rigid) is itself a candidate for `ADR-0059`'s own reserved decision to reconsider — not decided here. |

## What This Model Deliberately Does Not Decide

- **Who may trigger a transition** — an authorization concern, addressed
  generically (calling-layer `IPermissionEvaluator`) but not specifically
  per-transition here; a future implementation may reasonably require a
  different permission for `Draft → Reviewed` than for `Verified →
  Satisfied`, a decision this contract review does not make.
- **Whether `Obsolete` is truly terminal, or reversible under some
  future, real need** — disclosed as an open question, not decided.
- **Any notification, audit entry, or side effect a transition should
  trigger** — each remains the calling layer's own responsibility,
  mirroring every other Engineering Foundation write path.
- **Whether every listed transition must be implemented in the initial
  release**, or whether a smaller, initial subset is sufficient with the
  remainder added additively — deferred to the owning implementation
  Work Package's own scoping decision, informed by this document's own
  complete table.

## Enforcement

`IRequirementsService.SetStatusAsync` throws
`InvalidRequirementStatusTransitionException` for any transition not
listed in the table above — the contract's own enforcement point, per
`WP7.2C Requirements Platform Contracts.md` §1. No transition is
silently coerced or ignored.

## Related Documents

`WP7.2C Requirements Platform Contracts.md` §1, §8; `WP7.2B Requirements
Domain Model.md` §9; `WP7.2C Relationship Model.md`; `WP7.2C
Verification Integration Contract.md`.
