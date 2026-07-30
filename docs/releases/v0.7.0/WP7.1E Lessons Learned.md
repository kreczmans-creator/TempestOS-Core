# WP 7.1E — Verification Framework — Lessons Learned

## Status

Complete.

## 1. The best design for a cross-cutting framework can be reusing an existing mechanism completely, not extending it

Every prior Engineering Foundation Work Package needed at least some
new storage shape (Materials' own `materialId` index; Calculation's own
append-only recording). Verification needed none — the "how do I find
every verification for this subject" problem is structurally identical
to "how do I find every reference from this document," which
`IEngineeringDocumentStore.GetReferencesAsync` already answers. The
lesson generalises: before designing a new index or storage shape for a
cross-cutting framework, check whether an existing framework's own
mechanism already answers the same question from a different angle.

## 2. Validating some links while leaving others open is a legitimate, asymmetric design

`LinkDocument`/`LinkCalculationRecord` are validated (real
`EngineeringDocumentNotFoundException` on a bad Id); `ReferenceMaterial`
is not. This looks inconsistent at first glance, but the reasoning is
principled: Verification already has a hard dependency on the Data
Model, so validating document/calculation-record links costs nothing
extra. It has no dependency on Materials at all, so validating material
references would require adding one purely for validation — a cost the
approved contract's own "where appropriate" framing does not justify.
Asymmetric validation is defensible when the asymmetry tracks a real
asymmetry in dependencies, not merely convenience.

## 3. Narrow, well-scoped exclusions produce close-to-automatic scope discipline

This Work Package's own controlling instruction excluded Validation and
Requirements Management explicitly, by name, alongside the more usual
exclusions (design-code logic, approval workflows). No moment during
implementation created temptation to drift toward either — consistent
with every prior Engineering Foundation Work Package's own identical
experience (`WP 7.1A` through `WP 7.1D` each reported the same finding).
This is now a five-for-five pattern across the entire programme, worth
stating as a settled observation, not merely a repeated one: a
controlling instruction that names its own exclusions explicitly, by
name, produces measurably easier scope discipline than one that only
names inclusions.

## 4. Zero new exception types is itself worth confirming, not merely defaulting to

It would have been easy to reflexively add a `VerificationException`
hierarchy mirroring every prior framework's own pattern. The approved
contract explicitly said to reuse `EngineeringDocumentNotFoundException`
instead, and `WP7.1A Future Capability Recommendations.md` had already
recommended exactly this two Work Packages earlier. Checking the
contract's own literal text before defaulting to the "usual" pattern
avoided inventing an unnecessary type.

## Recommendations

- **The Engineering Foundation programme is complete** — see `WP7.1E
  Engineering Core Impact Assessment.md` for the genuinely open choice
  Product Approval now faces.
- **Future Work Packages needing a "find related records by subject"
  capability should check `IEngineeringDocumentStore.GetReferencesAsync`
  first**, before designing a new index — this Work Package's own
  experience is the second consecutive proof (after `ADR-0053`'s own
  design) that the Data Model's existing mechanism generalises further
  than each individual contract review anticipated.
- **A dedicated Security Review should continue for any future
  Engineering Module built on this foundation** — two consecutive Work
  Packages (`WP 7.1D`, `WP 7.1E`) each found genuine, proportionate
  findings a general Engineering Review's own checklist did not surface.

## Related Documents

`WP7.1E Implementation Report.md`; `WP7.1E Engineering Review Report.md`;
`WP7.1E Security Review Report.md`; `ADR-0057`; `docs/academy/03 Work
Packages/WP7.1E-verification-framework-implementation.md`.
