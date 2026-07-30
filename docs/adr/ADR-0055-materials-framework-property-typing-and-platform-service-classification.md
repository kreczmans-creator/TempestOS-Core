# ADR-0055: Materials Framework — Property Typing and Platform-Service Classification

## Status

Accepted — `WP 7.1C` (Materials Framework), 2026-07-30.

## Context

`WP7.0C Required ADR Catalogue.md` reserved two questions for this Work
Package: whether `IMaterialSpecification.Properties` should remain the
approved contract's own proposed open, boxed
`IReadOnlyDictionary<string, object>` shape, or become a stronger
alternative; and the `ADR-0013` Platform-Service-vs-Module classification
for `Tempest.Core.Materials`.

A third question arose during implementation, not anticipated by the
catalogue: `IEngineeringDocumentStore` provides no way to look up a
document by an arbitrary caller-chosen string, and no way to enumerate
every document of a given `Kind` — both are needed for
`IMaterialCatalog.FindAsync`/`ListAsync`/duplicate-registration checking,
which the approved contract's own "indirectly, through
`IEngineeringDocumentStore`" framing for Persistence did not anticipate
requiring a *direct* dependency.

This Work Package's own controlling instruction also introduced a
requirement `WP7.0C Engineering Foundation Contracts.md` never named:
every engineering property must carry provenance (source reference,
revision, validation status, confidence level, applicable conditions,
notes) — a requirement that itself bears directly on the property-typing
question below.

## Decision

**1. Property typing: a structured `MaterialProperty` record, not a bare
boxed value.** `IMaterialSpecification.Properties` is
`IReadOnlyDictionary<string, MaterialProperty>`, where
`MaterialProperty(object Value, MaterialPropertyProvenance Provenance)`
pairs a boxed `Quantity<TDimension>` with a `MaterialPropertyProvenance`
record every property carries — never optionally, never
omissible by construction. This resolves the reserved property-typing
question in favour of "design a stronger alternative," the second option
`WP7.0C Required ADR Catalogue.md` itself named, driven directly by this
Work Package's own provenance requirement: a bare `object` has nowhere
to attach provenance to.

`Value` remains bounded to the seven dimensions
`Tempest.Core.UnitsAndQuantities` already defines (Length, Mass,
Duration, Force, Pressure, Area, Volume) — `MaterialPropertyValueCodec`
encodes/decodes each via ordinary type-pattern matching, no reflection,
no type-name-based deserialization. A fixed, closed set of *property
names* (density, yield strength, and so on) remains explicitly rejected,
exactly as `WP7.0C Required ADR Catalogue.md`'s own "Alternative
Considered and Rejected" already reasoned — this decision concerns the
shape of a value, not which property names are legitimate.

**2. Platform-Service classification: confirmed, `ADR-0013`.**
`Tempest.Core.Materials` is a Platform Service — `IMaterialCatalog` is
DI-registered as an ordinary singleton in `TempestHost.cs`, mirroring
`IEngineeringDocumentStore`'s own registration shape, exactly as
`WP7.0C Engineering Foundation Contracts.md` proposed as its own working
default.

**3. `MaterialCatalog` depends directly on `IPersistenceStore`, not only
indirectly through `IEngineeringDocumentStore`.** A genuine
implementation-time finding, not anticipated by the reserved-ADR
catalogue: since `IEngineeringDocumentStore` has no lookup-by-string or
enumerate-by-`Kind` capability, `MaterialCatalog` maintains its own small
index — one `IPersistenceStore` collection
(`Materials.Index`) mapping each registered `materialId` to its own
backing document Id, mirroring `EngineeringDocumentStore`'s own
collection-ownership convention (`ADR-0053`, itself extending
`ADR-0041`). A per-`materialId` `AsyncKeyedLock` serialises
`RegisterAsync`'s own check-then-write sequence, mirroring
`EngineeringDocumentStore`'s own per-document lock rationale for
`ReviseAsync`.

**4. `IMaterialCatalog` remains a thin index — no new linking methods.**
"Material references" (this Work Package's own Implementation Scope) is
resolved by using `IEngineeringDocumentStore.LinkAsync`/`GetReferencesAsync`
directly against `IMaterialSpecification.UnderlyingDocumentId` — exactly
`WP7.1A Future Capability Recommendations.md`'s own Recommendation 1
("`IMaterialCatalog` should be a thin, typed index... never its own
storage"), now demonstrated directly
(`UnderlyingDocument_CanBeLinkedToAnotherDocumentDirectlyThroughEngineeringDocumentStore`).
No new method was added to `IMaterialCatalog` for this.

**5. `ReviseAsync` added — an additive extension, not a change to any
shown contract member.** The approved contract showed only
`RegisterAsync`/`FindAsync`/`ListAsync`. This Work Package's own
Implementation Scope names "Material revision support" explicitly;
`IMaterialCatalog.ReviseAsync` records a new revision of an existing
material's own properties through the underlying document, giving
`MaterialNotFoundException` (declared but, per `WP7.0C`'s own Error
Handling section, previously without a concrete throwing use) its first
real use.

## Consequences

**Positive:**

- Provenance is structurally guaranteed, not merely conventional — a
  `MaterialProperty` cannot be constructed without one (defaulting to
  `MaterialPropertyProvenance.Unknown`, an honest "not assessed" value,
  never an invented one).
- The direct `IPersistenceStore` dependency is a small, disclosed,
  minimal addition — one collection, reusing infrastructure
  `EngineeringDocumentStore` itself already depends on, not a second,
  parallel storage mechanism.
- `IMaterialCatalog` stays thin, exactly as recommended — no
  duplication of revisioning or reference-tracking the Data Model
  already provides.

**Negative:**

- `MaterialCatalog.FindAsync`/`ListAsync` read a material's own full
  revision history to reconstruct current state, since
  `IEngineeringDocumentStore` provides no dedicated "latest revision
  only" lookup — a real, disclosed performance characteristic for a
  material with many revisions (see Technical Debt Assessment).
- Property values remain bounded to the seven dimensions
  `Tempest.Core.UnitsAndQuantities` currently defines — a material
  property requiring an eighth dimension (or an affine one, e.g. a
  temperature-dependent property) cannot be represented until that
  dimension itself exists (`FCR-0034`).

## Alternatives Considered

**A bare, un-provenanced `IReadOnlyDictionary<string, object>`, confirmed
as-is** — considered and rejected, since this Work Package's own
provenance requirement has no field to attach to a bare boxed value.

**A discriminated-union-style property value, or a fixed, extensible
enum of well-known property kinds** — considered and rejected, exactly
as `WP7.0C Required ADR Catalogue.md`'s own "Alternative Considered and
Rejected" already reasoned: closing the property-name set now would
encode assumptions about which material properties matter without a
real discipline requirement to validate them against.

**Extending `IEngineeringDocumentStore` itself with native query/lookup
capability**, rather than giving `MaterialCatalog` its own direct
`IPersistenceStore` index — considered and rejected, mirroring
`ADR-0053`'s own identical reasoning for `FCR-0007`: coupling Materials'
own timeline to an unrelated, unscheduled query-capability extension
would be a planning risk this Work Package declines to introduce.

## Related Documents

`ADR-0053` (the storage/indexing precedent this decision extends);
`ADR-0054` (the seven dimensions this decision's property-value codec is
bounded to); `docs/releases/v0.7.0/WP7.0C Engineering Foundation
Contracts.md`; `docs/releases/v0.7.0/WP7.0C Required ADR Catalogue.md`;
`docs/releases/v0.7.0/WP7.1A Future Capability Recommendations.md`
(Recommendation 1); `docs/releases/v0.7.0/WP7.1C Implementation
Report.md`.
