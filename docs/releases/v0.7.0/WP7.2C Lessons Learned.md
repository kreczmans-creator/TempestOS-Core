# WP 7.2C — Requirements & Verification Platform Contract Review — Lessons Learned

## Status

Complete.

## 1. Writing concrete signatures surfaces questions an architecture review, by its own nature, cannot

`WP7.2B Security Architecture.md` reviewed authorisation as a dimension
in the abstract and found it "Implemented (inherited)." Only once this
Work Package actually wrote `IRequirementsService`'s own proposed
signatures did the specific question — should any individual method
gate internally, mirroring `IVerificationService.GetVerificationHistoryAsync`,
or remain calling-layer-only throughout, mirroring `IReportingService`
— become visible enough to name and reserve (`ADR-0061`). The lesson
generalises: an architecture review answers "is this concern
addressed," while a contract review answers "addressed *how, exactly*,
and does that choice need its own decision" — two genuinely different
levels of scrutiny, neither a substitute for the other.

## 2. Reserving a relationship-kind constant is nearly free; reserving a domain concept is not — the two should not be evaluated by the same standard

Every one of the six relationship kinds this Work Package reviewed
(`WP7.2C Relationship Model.md`) was included in the initial
implementation, a different outcome than the Engineering Foundation's
own repeated "leave it open, do not invent a taxonomy" pattern for
material properties or calculation input types. The reasoning is not
inconsistency — it is that a `string` constant costs nothing to reserve
and is directly load-bearing for another required contract (the
Lifecycle Model, the Requirement Group hierarchy), while a property-name
taxonomy or a calculation input type would have required inventing
domain content this project has no evidence for yet. The lesson:
"prefer openness over premature invention" is not a blanket rule to
apply identically everywhere — it applies specifically where the
alternative is inventing unvalidated domain content, not wherever a
choice merely looks similar in shape.

## 3. Disclosing a real, structural limitation is stronger governance than presenting an elegant design as complete

`WP7.2C Traceability Contract.md` §3 discloses that reverse allocation
traceability does not work when an allocation target is an open string
rather than a real document — a direct, structural consequence of the
discipline-neutrality design this Work Package itself endorses.
Presenting the traceability contract as uniformly complete across all
five dimensions would have been easier to write and would have looked
better in isolation; naming the one place it is not complete, and why,
is what makes the rest of the document trustworthy. This is the same
discipline `WP6.8`/`WP7.1F`'s own certification reports established for
disclosed technical debt, applied here one stage earlier, to a contract
rather than a shipped implementation.

## 4. A contract review that finds nothing new beyond its own architecture review is itself informative, not merely uneventful

`WP7.2C Security Review.md`'s own explicit "Nothing new was found"
finding could easily have been treated as a formality worth skipping.
Stating it directly — and explaining *why* nothing new emerged (every
proposed signature was a disciplined translation of an already-reviewed
responsibility, not a new capability invented during contract drafting)
— confirms the architecture review that preceded it was thorough enough
to not need a second pass to catch what the first missed. An
uneventful review, stated as a finding with its own reasoning, is
evidence of quality; an uneventful review passed over in silence is
merely an absence of evidence.

## Recommendations

- **The owning implementation Work Package should resolve all four
  reserved decisions (`ADR-0058`–`ADR-0061`) before writing any
  production code**, mirroring every Engineering Foundation
  implementation Work Package's own identical discipline.
- **`ADR-0061`'s own internal-vs-calling-layer gating question should be
  decided by the same test this review implicitly applied but did not
  formalise**: gate internally when the data exposed is itself
  evidentiary and permission-sensitive on its own terms (mirroring
  Verification's own history); leave to the calling layer when the data
  is ordinary operational state a caller's own context already governs
  (mirroring Reporting). Stating this test explicitly, rather than
  re-deriving it from scratch, would save the owning Work Package a
  step this review already did once.
- **The reverse-allocation-traceability limitation (`WP7.2C
  Traceability Contract.md` §3) should be re-examined once a real
  discipline module exists to allocate against** — at that point, a
  real usage pattern will show whether the limitation is a genuine,
  ongoing cost or a theoretical one that never actually matters in
  practice.

## Related Documents

`WP7.2C Requirements Platform Contracts.md`; `WP7.2C Security Review.md`;
`WP7.2C Required ADR Catalogue.md`; `WP7.2C Traceability Contract.md`;
`WP7.2C Relationship Model.md`; `docs/academy/03 Work Packages/
WP7.2C-requirements-and-verification-platform-contract-review.md`.
