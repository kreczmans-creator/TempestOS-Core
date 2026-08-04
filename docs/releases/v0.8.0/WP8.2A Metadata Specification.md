# WP 8.2A — Engineering Domain Architecture — Metadata Specification

## Purpose

The common metadata envelope every Engineering Object carries,
regardless of family — the fields `Search`/`Governance`
(`WP8.2A Engineering Domain Architecture.md` §4/§6) operate over
uniformly. Every field below is either already real (grounded in
shipped code) or a disclosed, open, caller-defined convention — never a
new closed vocabulary invented speculatively.

## 1. Identity & Provenance Metadata

| Field | Definition | Grounding |
|---|---|---|
| Author | The principal who created the object's own first revision | `IDocumentRevision.AuthorPrincipalId` (real, shipped) |
| Reviewer | The principal(s) who performed a Review (`Canonical Object Catalogue.md` §10) | Realised as the Review object's own metadata, not a field on the reviewed object — a relationship, never a field, per `IRequirement`'s own "never owns what satisfies/verifies it" discipline extended platform-wide |
| Approver | The principal who recorded an Approval (`Canonical Object Catalogue.md` §10) | Realised as the Approval object's own metadata, same discipline as Reviewer |
| Owner | The principal or team accountable for the object going forward — distinct from Author (who merely created it) | Open, caller-defined field; no shipped precedent yet, first named here |
| Created | The object's own first revision's own `CreatedAt` | `IDocumentRevision.CreatedAt` (real, shipped) |
| Modified | The object's own current (most recent) revision's own `CreatedAt` | Derived — the `CreatedAt` of the revision at `CurrentRevisionNumber`, not a separately stored field |
| Revision | The object's own `CurrentRevisionNumber` | `IEngineeringDocument.CurrentRevisionNumber` (real, shipped) |

## 2. Workflow Metadata

| Field | Definition | Grounding |
|---|---|---|
| Status | The object's own current lifecycle state | `Lifecycle Specification.md`; real, shipped precedent `RequirementStatus` |
| Priority | An open, caller-defined ordering signal (e.g. for Task/Action/Issue) | Open field, no closed vocabulary — mirrors `Category`'s own open-string precedent |
| Severity | An open, caller-defined impact signal (e.g. for Risk/Hazard/Issue) | Open field, same precedent |
| Confidence | How much trust the current value deserves | Real, shipped precedent: `MaterialPropertyConfidenceLevel` (`Unknown`, `Low`, `Medium`, `High`) — this catalogue adopts that same four-value vocabulary platform-wide, not a Materials-only concept |
| Completion | An open, caller-defined progress signal (0–100%, or a discrete state) — distinct from lifecycle `Status`, since an object can be 100% complete and still `InReview` | Open field, no shipped precedent yet |

## 3. Classification Metadata

| Field | Definition | Grounding |
|---|---|---|
| Category | An open, caller-defined classification | Real, shipped precedent: `IRequirement.Category`/`IMaterialSpecification.Category` (both nullable `string?`) |
| Engineering discipline | An open, caller-defined value naming which engineering discipline owns the object (Structures, Electrical, Systems, ...) | Open field, same shape as Category — deliberately not a closed enum, since TempestOS's own discipline list grows with every new Engineering Discipline Module (`ADR-0067`'s own extensibility precedent, applied here) |
| Keywords / Tags | Open, caller-defined free-text labels, multiplicity many-per-object | Open field; realised as a simple string list, not a first-class Tag object (`Canonical Object Catalogue.md` §13) |
| Classification | A security/sensitivity classification value | `WP8.2A Engineering Domain Architecture.md` §5 — open field today, no enforcement built |

## 4. Engineering Value Metadata

| Field | Definition | Grounding |
|---|---|---|
| Units | The unit a numeric value is expressed in | Real, shipped precedent: `Tempest.Core.UnitsAndQuantities.Unit<TDimension>` — **every numeric Engineering Object field should be a `Quantity<TDimension>`, never a bare `double`**, exactly as Materials/Calculations already require |
| Tolerances | An engineering value's own permitted deviation range | Conceptual — no shipped precedent; proposed as a pair of `Quantity<TDimension>` bounds (same unit-safety discipline as Units, above), not a bare numeric range |
| Notes | Open, caller-defined free text | Open field, no constraints |

## 5. Provenance Is Mandatory Where It Governs a Decision

**Every metadata field that could influence a lifecycle, approval, or
verification decision must carry Confidence and, where applicable, a
source reference — never a bare, provenance-free value.** This is not
a new rule; it is `Engineering Principle 13` ("no provenance-free value
permitted, ever, by construction" — Materials' own mandatory
`MaterialPropertyProvenance`), restated as binding on the full
canonical object set, not only Material properties.

## 6. What This Specification Deliberately Does Not Define

No field in this specification is realised as a new stored column,
table, or schema — every Engineering Object's own metadata lives
exactly where `IDocumentRevision`/`IEngineeringDocument` metadata
already lives today; fields with no current storage precedent
(`Owner`, `Priority`, `Severity`, `Completion`, `Tolerances`) are named
architecturally, for a future implementation Work Package to place
correctly, not placed here (no implementation, per this Work Package's
own explicit constraint).

## Related Documents

`WP8.2A Engineering Domain Architecture.md`; `WP8.2A Lifecycle
Specification.md`; `Engineering Principle 13`; `MaterialProperty`/
`MaterialPropertyProvenance` (`src/Tempest.Core/Materials/`);
`Quantity<TDimension>`/`Unit<TDimension>` (`src/Tempest.Core/
UnitsAndQuantities/`).
