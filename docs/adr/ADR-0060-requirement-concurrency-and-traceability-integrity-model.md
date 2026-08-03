# ADR-0060: Requirement Concurrency and Traceability Integrity Model

## Status

Accepted — `WP 7.3A` (Requirements Engine), 2026-07-30.

## Context

`WP7.2B Security Architecture.md` and `WP7.2C Security Review.md` both
disclosed the same genuine gap: `IEngineeringDocumentStore.ReviseAsync`'s
own per-document lock prevents two concurrent revisions from colliding
on revision number, but provides no compare-and-swap or "expected prior
revision" check — two authors editing the same requirement concurrently
could each succeed, with the second silently becoming current.
`WP7.2C Required ADR Catalogue.md` reserved the question of whether this
must be resolved before implementation ships.

## Decision

**No optimistic-concurrency mechanism is implemented.**
`IRequirementsService.ReviseAsync`'s own shipped signature carries no
expected-prior-revision parameter, exactly matching the approved
contract (`WP7.2C Requirements Platform Contracts.md` §1) — implementing
one now would have been a deviation from the approved contract, not
merely an addition. This is accepted, disclosed, real Technical Debt
(`TD-25`), not resolved:

- No real, demonstrated multi-author collaborative-editing incident has
  occurred against any Engineering Core framework to date — Materials,
  Calculations, and Verification are each dominated by single-author or
  system-generated writes.
- Building the mechanism speculatively, ahead of a real demonstrated
  need, would violate Security Principle 7's own "do not build ahead of
  demonstrated need" discipline, applied here to a correctness mechanism
  rather than a security one — the identical reasoning `WP7.2C Required
  ADR Catalogue.md` itself anticipated as the likely outcome.

**Traceability integrity is confirmed as already provided by
`IEngineeringDocumentStore.LinkAsync`'s own append-only design** — a
recorded relationship cannot be silently altered or removed once
written, inherited directly, not newly designed here. The disclosed
limitation (no duplicate/contradiction detection for a recorded
relationship) is the identical, already-accepted scope `TD-18` covers
for `LinkAsync` generally — this Work Package introduces no new gap
beyond that one.

## Consequences

**Positive:**

- The Requirements Engine's own implementation matches the approved
  contract exactly, with zero unauthorised signature deviation.
- No speculative complexity (an expected-revision parameter every
  caller of `ReviseAsync` would need to thread through, whether or not
  it ever matters to that caller) is introduced ahead of real need.

**Negative:**

- A genuine correctness gap remains: two concurrent editors of the same
  requirement can silently overwrite one another's intent, with no
  error, no warning, and no merge — disclosed explicitly as `TD-25`, not
  hidden.
- This gap is more consequential for the Requirements Engine than for
  any prior Engineering Core framework, since the Requirements Platform
  is the first whose own target users (a systems engineering team, not
  a single calculation author) plausibly edit the same artefact
  concurrently as a matter of ordinary practice, not an edge case.

## Alternatives Considered

**Adding an expected-prior-revision parameter to `ReviseAsync`
speculatively** — considered and rejected. This would have been a
deviation from the approved contract requiring its own justification
("why could the approved contract not be implemented"), and no genuine
implementation defect — only a disclosed, anticipated design question —
justifies that deviation here.

**A per-requirement pessimistic lock held across a read-then-write
editing session** — considered and rejected. This would require a
session/lock-lifetime concept this platform has nowhere else, for a
concurrency profile not yet demonstrated to be a real problem.

## Related Documents

`WP7.2B Security Architecture.md`; `WP7.2C Security Review.md`;
`WP7.2C Required ADR Catalogue.md`; `docs/governance/Quality/Technical
Debt Register.md` (`TD-18`, and this Work Package's own new `TD-25`);
`docs/security/Security Principles.md` (Principle 7);
`docs/releases/v0.7.0/WP7.3A Security Review Report.md`;
`docs/releases/v0.7.0/WP7.3A Technical Debt Assessment.md`.
