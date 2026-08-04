# ADR-0074: Lifecycle Status Is a Common Canonical Vocabulary, Specialised Per Object Family

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.2A` (Engineering
Domain Architecture), 2026-08-04. Resolves how roughly fifty canonical
Engineering Object families relate to the one lifecycle model already
shipped for Requirements.

## Context

`WP 8.2A`'s own controlling instruction names eight lifecycle states
(Draft, In Review, Approved, Released, Superseded, Obsolete, Archived,
Cancelled) that "every object" should have. Meanwhile,
`Tempest.Core.Requirements` already ships a real, closed, seven-value
`RequirementStatus` (`Draft`, `Reviewed`, `Approved`, `Allocated`,
`Verified`, `Satisfied`, `Obsolete`) with its own fixed transition
table (`RequirementStatusTransitions`) — a shape that does not exactly
match the controlling instruction's own eight-state list (different
names for some states; extra domain-specific states inserted; several
canonical states entirely absent).

Two extremes are both wrong: forcing every future object family to use
the literal eight-value enum, unmodified, would contradict
`RequirementStatus`'s own already-shipped, already-correct, and
already-tested shape (a genuine regression this Work Package's own
"no implementation" constraint could not even fix). Leaving lifecycle
entirely ad hoc per family, with no shared vocabulary at all, would
mean the fifty-object canonical catalogue this Work Package produces
has no common way to answer "is this object still in force" without
inspecting every family's own bespoke states individually — directly
undermining `Search`/`Governance`'s own need for a uniform status
signal (`WP8.2A Engineering Domain Architecture.md` §4/§6).

## Decision

**The eight named states form one canonical vocabulary and one
canonical default transition table (`WP8.2A Lifecycle Specification.md`
§1/§2). Every object family specialises this table in exactly three
permitted ways: inserting additional states between `Approved` and its
own terminal states, omitting states it has no use for, and adding
stricter approval gates — never inventing a transition the canonical
table forbids outright, and never a lifecycle model unrelated to the
canonical vocabulary.** `RequirementStatus` is reconciled, not
redesigned, against this rule (`WP8.2A Lifecycle Specification.md`
§4.1): `Reviewed` maps to canonical `InReview` (a disclosed naming
divergence); `Allocated`/`Verified`/`Satisfied` are permitted inserted
states; `Released`/`Superseded`/`Archived`/`Cancelled` are permitted
omissions. No code change is implied or required.

## Consequences

**Positive:**

- `RequirementStatus` remains exactly as shipped — this decision
  explains it as a valid specialisation rather than requiring it be
  redesigned to fit a rigid global enum, honouring this Work Package's
  own "no implementation" constraint in the strongest sense (zero
  pressure toward a future breaking change).
- A future object family (Risk, say) gets a real, load-bearing
  starting point — the canonical eight states and their own default
  transitions — rather than inventing lifecycle from nothing, while
  still being free to shape it to Risk's own real workflow (open →
  mitigating → closed, for instance, inserted between `Approved` and
  `Released`/`Obsolete`).
- A platform-wide "is this object still valid" query
  (`Search`/`Governance`, `WP8.2A Engineering Domain Architecture.md`
  §4/§6) can always ask "is the current state one of the two universally
  understood terminal states, `Archived` or `Cancelled`" without
  needing family-specific knowledge — the three specialisation rules
  guarantee every family's own terminal states are drawn from, or map
  cleanly onto, the canonical set.

**Negative:**

- Two families can specialise the same canonical states inconsistently
  (one family's own `Approved` implying stronger commitment than
  another's) with no platform-level check that a specialisation stays
  "close enough" to the canonical meaning — a disclosed, accepted
  cost, mirroring `ADR-0073`'s own identical, already-accepted
  vocabulary-drift risk for relationships.
- A family choosing to insert many domain-specific states (as
  Requirements already does, three of them) makes the canonical
  vocabulary a genuine minority of that family's own real states —
  accepted because the alternative (forcing exactly eight states on
  every family) would have required redesigning `RequirementStatus`
  itself, contradicting this Work Package's own explicit "no
  implementation" scope.

## Alternatives Considered

**One rigid, global, unspecialisable eight-value enum for every
object** — considered and rejected; directly contradicts
`RequirementStatus`'s own already-shipped, already-correct seven-value
shape, and would require an implementation Work Package this
architecture-only Work Package is not scoped to authorise.

**No shared vocabulary — each family invents lifecycle independently**
— considered and rejected; would leave `Search`/`Governance` with no
uniform way to ask "is this object still in force" across the fifty
named canonical families, undermining the stated purpose of a
*canonical* domain architecture.

## Related Documents

`WP8.2A Engineering Domain Architecture.md`; `WP8.2A Lifecycle
Specification.md`; `RequirementStatus`/`RequirementStatusTransitions`
(`src/Tempest.Core/Requirements/`); `docs/releases/v0.7.0/WP7.2C
Requirements Platform Contracts.md` (Requirement Lifecycle Model).
