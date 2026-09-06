# ADR-0131: Supplier Identity Is Recorded and Never Merged, and a Supplier Record Describes Rather Than Qualifies

## Status

Accepted — `Group D` (P03 Commercial Intelligence), 2026-09-06.

## Context

A supplier database goes wrong in two ways, and both are the software
being helpful.

**The first is merging.** The same business arrives in the records three
times — as "Notional Machining", "Notional Machining Ltd" and "N.
Machining (Leeds)" — and something offers to tidy it up. Fuzzy name
matching is very good at proposing merges and has no way of knowing that
two identically named companies at the same trading estate are a parent
and a subsidiary with different accounts, different approvals and
different insurance. A merge is destructive: the trading history of one
business is now attributed to another, and nothing in the record says it
happened.

**The second is qualification.** A field called `IsApproved`, or a status
value called `Qualified`, turns a description of what somebody found into
a statement that the organisation has approved this supplier. Approving a
supplier is an act with contractual and, in regulated work, legal weight.
It is not something a database schema confers by having a column.

## Decision

**Identity is recorded; it is never resolved by the platform.**
`SupplierIdentityService` compares two identities and reports what it
found. It has no merge operation.

- Only a registration-number match is treated as conclusive. Everything
  else — normalised name, alias, address — produces a *possible*
  duplicate, recorded on the record as `PossibleDuplicatesOf`, for a
  person to look at.
- `IdentityConfidence` is a recorded judgement (`NotAssessed`,
  `Possible`, `Probable`, `Confirmed`), not a computed score.
- Normalisation is public and deterministic, so a caller can see exactly
  what the comparison did rather than trusting a black box.

**A supplier record describes what somebody established.** Nothing on it
approves, qualifies or admits a supplier to an approved list.

- `CapabilityAssurance` runs `NotAssessed → Offered → Verified → Proven`,
  with `Declined` and `Disproven` alongside. Every value names who
  established what: `Offered` is the supplier's own claim, `Verified`
  means somebody checked, `Proven` means the organisation has actually
  had the work done. None of them is an approval.
- A capability claimed as independently established with no evidence
  attached is reported (`IsUnevidenced`), never downgraded.
- `SupplierStatus` records the trading relationship — prospective, active,
  dormant, ceased — and is not a quality judgement.

## Consequences

**The database will hold duplicates**, and will say so rather than
silently resolving them. That is the intended failure: a visible possible
duplicate costs somebody five minutes, and a wrong merge costs a
traceability investigation.

**"Is this supplier approved?" is not a question `D1` answers.** It
answers "what has anybody established about this supplier, and who
established it?" — which is the question that can be evidenced.

**A registration number is worth chasing.** It is the only identifier the
platform treats as conclusive, so populating it is the single highest-value
thing a user can do to the library.
