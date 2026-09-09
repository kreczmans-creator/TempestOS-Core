# WP 7.1C — Materials Framework — Implementation

## 1. Introduction

`WP 7.1C` is the third implementation Work Package of the Engineering
Foundation phase (`v0.7.0`), following `WP 7.1A` (Engineering Data
Model) and `WP 7.1B` (Units & Quantities Framework). It implements
`Tempest.Core.Materials` — a Platform Service providing engineering
material specifications, each backed by the Engineering Data Model and
carrying dimensioned properties from Units & Quantities — exactly as
`WP7.0C Engineering Foundation Contracts.md` proposed, extended with a
structured, provenance-carrying property type this Work Package's own
controlling instruction required.

## 2. Purpose

To give every future Materials- and Manufacturing-discipline capability,
and any future Engineering Calculation consuming a material's own
properties, a single, canonical way to represent a material
specification — so none of them invents its own storage shape or its
own notion of "where did this value come from."

## 3. Background

`WP 7.0B` identified Materials (`FCR-0031`) as depending directly on
both the Data Model and Units & Quantities. `WP 7.0C` proposed its
public contract — `IMaterialCatalog`, `IMaterialSpecification` — and
reserved `ADR-0055` for two open questions: whether
`IMaterialSpecification.Properties` should remain a bare, boxed
`IReadOnlyDictionary<string, object>`, and the `ADR-0013`
Platform-Service-vs-Module classification. This Work Package resolves
both, and adds a requirement neither `WP 7.0B` nor `WP 7.0C` named:
every engineering property must carry provenance.

## 4. The Problem

A material specification is only as trustworthy as the reader's ability
to judge where each of its values came from and how much to trust them
— an un-provenanced number is indistinguishable from an invented one.
No existing framework provided a way to attach "where this came from,
how confident we are, under what conditions it applies" to an
individual dimensioned value, and `IMaterialCatalog` itself needed a way
to look a material up by a caller-chosen string, which
`IEngineeringDocumentStore`'s own `Guid`-only lookup does not provide.

## 5. The Design

Every material specification is itself an `IEngineeringDocument` of
`Kind = "MaterialSpecification"` — `MaterialCatalog` is a thin, typed
index over `IEngineeringDocumentStore`, exactly as `WP7.1A Future
Capability Recommendations.md`'s own Recommendation 1 anticipated. Each
property is a `MaterialProperty(object Value, MaterialPropertyProvenance
Provenance)` — `Value` a boxed `Quantity<TDimension>` bounded to the
seven dimensions `Tempest.Core.UnitsAndQuantities` defines, `Provenance`
a mandatory, never-omissible record (source reference, revision,
validation status, confidence level, applicable conditions, notes).
`MaterialCatalog` additionally depends directly on `IPersistenceStore`
for its own small `materialId`-to-`documentId` index — a genuine
implementation finding, since `IEngineeringDocumentStore` has no
lookup-by-string capability, resolved in `ADR-0055`. A per-`materialId`
`AsyncKeyedLock` guarantees duplicate-registration atomicity. See
`WP7.1C Implementation Report.md` for the complete file-by-file account.

## 6. Alternatives Considered

**A bare, un-provenanced `IReadOnlyDictionary<string, object>`, confirmed
as-is** — considered and rejected in `ADR-0055`, since this Work
Package's own provenance requirement has nowhere to attach to a bare
boxed value.

**A discriminated-union-style property value, or a fixed, extensible
enum of well-known property kinds** — considered and rejected, exactly
as `WP7.0C Required ADR Catalogue.md` itself already reasoned: closing
the property-name set now would encode assumptions no real discipline
requirement yet validates.

**Extending `IEngineeringDocumentStore` itself with native lookup-by-
string capability**, rather than giving `MaterialCatalog` its own direct
`IPersistenceStore` index — considered and rejected in `ADR-0055`,
mirroring `ADR-0053`'s own identical reasoning for `FCR-0007`: coupling
Materials' own timeline to an unscheduled query-capability extension
would be an avoidable planning risk.

## 7. Why This Solution Was Chosen

It satisfies the provenance requirement structurally (a `MaterialProperty`
literally cannot omit one) rather than by convention, reuses proven
infrastructure (`IEngineeringDocumentStore`, `IPersistenceStore`,
`AsyncKeyedLock`) rather than inventing new storage, and keeps
`IMaterialCatalog` thin exactly as the strongest prior recommendation
(`WP7.1A`) suggested.

## 8. Architectural Principles

Applies `FOUNDATION.md`'s existing principles without modification: one
component, one reason to change; immutability by construction
(`MaterialProperty`/`MaterialPropertyProvenance` are both records).
Extends `docs/engineering/Engineering Principles.md` with four further
principles (13-16) — the same "derived from working code" discipline
applied to a third framework.

## 9. Files Added

14 new production files under `src/Tempest.Core/Materials/`; 5 new
sample files under `src/Samples/Tempest.Samples/`; 1 file modified
(`TempestHost.cs`); 8 new test files under `tests/Tempest.Core.Tests/
Materials/`, `Runtime/`, and `Samples/`; 1 test file modified
(`ClockModuleDiscoveryTests.cs`). Full list: `WP7.1C Implementation
Report.md`.

## 10. Trade-offs

`MaterialCatalog.FindAsync`/`ListAsync` read a material's own complete
revision history to reconstruct current state, since
`IEngineeringDocumentStore` has no dedicated "latest revision only"
lookup (`TD-20`). `IMaterialCatalog` performs no permission-gating of
its own — calling-layer enforcement is expected, mirroring Reporting/
Navigation's own precedent (`AT-15`) — both disclosed in `WP7.1C
Technical Debt Assessment.md`, neither believed to be a current
correctness risk.

## 11. Common Mistakes

A future consumer should **not** assume two materials can share a
`materialId` — `RegisterAsync` throws `DuplicateMaterialException`,
proven atomic under concurrent registration by a dedicated test. A
future consumer should **not** attempt to represent a property outside
the seven supported dimensions — `MaterialProperty`'s own constructor
rejects it immediately (`MaterialsException`), rather than silently
storing an unusable value.

## 12. Future Evolution

Candidate `F` (Engineering Calculation Framework, `FCR-0032`) can now
consume a real material's own dimensioned properties directly. A future
Work Package resolving `FCR-0034` (affine unit conversion) would extend
the seven dimensions `MaterialPropertyValueCodec` supports, purely
additively. See `WP7.1C Engineering Foundation Impact Assessment.md`
for the complete account.

## 13. Key Takeaways

1. A provenance requirement introduced at implementation time, not
   contract-review time, can still resolve a contract's own explicitly
   reserved question cleanly — `ADR-0055`'s property-typing decision was
   driven directly by needing somewhere to attach provenance, not by
   the property-typing question in isolation.
2. A "thin index" framework built on two already-implemented
   frameworks (Data Model, Units & Quantities) surfaces its own new
   implementation-time findings even when both dependencies are
   already proven — the direct `IPersistenceStore` dependency was not
   visible until `FindAsync`/`ListAsync`'s own lookup-by-string
   requirement was actually implemented.
3. Bounding a boxed, heterogeneous value type to a small, explicit,
   already-established set (the seven `Tempest.Core.UnitsAndQuantities`
   dimensions) avoids both an unsafe general-purpose polymorphic
   mechanism and a premature, closed property-name taxonomy — the two
   extremes this Work Package's own governing discipline warned against.

## Architectural Debt Assessment

`TD-20` (full revision history read on every lookup) and `AT-15` (no
framework-internal permission-gating) — both newly disclosed, neither
Release Blocking. Full detail: `WP7.1C Technical Debt Assessment.md`.

## Observations

This is the third consecutive implementation Work Package of the
Engineering Foundation phase, and the first to depend on both of the
prior two simultaneously — validated by the same discipline as its
predecessors (clean Debug/Release builds, 1174/1174 tests, both
configurations, clean rebuild).

## Related Documents

`docs/releases/v0.7.0/WP7.1C Implementation Report.md` and its six
companion deliverables; `ADR-0055`; `docs/engineering/Engineering
Principles.md`; `docs/releases/v0.7.0/WP7.0C Engineering Foundation
Contracts.md`; `docs/releases/v0.7.0/WP7.1A Future Capability
Recommendations.md`.
