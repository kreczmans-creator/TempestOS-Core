# ADR-0088: The Document Classification Taxonomy (Specification/Report/Procedure/Standard/Datasheet/External Reference) Is Realised as `Classification`-Tagged `Document` Objects, Never New Concrete Domain Classes

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.4A` (Engineering Documents Workspace), 2026-08-06.

## Context

`WP 9.4A`'s own controlling instruction names eight Document types as first-class scope: Drawings, Specifications, Reports, Procedures, Standards, Datasheets, External References, plus Attachments and Supporting Evidence as related capabilities. Its own explicit constraints match every prior real-discipline Work Package's own: "No architectural redesign. No contract redesign. No duplicate framework. Reuse the existing Engineering Domain, Workspace and Digital Thread exclusively."

`Contracts/DocumentationDesign.cs`/`Implementation/DocumentationDesign.cs` (`WP 8.2B`/`WP 8.2C`) declare and compile exactly three concrete Documentation & Design Domain classes: `Document` (a plain `EngineeringObjectBase`-derived base), `Drawing` (adds `DrawingNumber`), and `CadModel` (adds `ModelFormat`). `Specification`, `Report`, `Procedure`, `Standard`, and `External Reference` have no interface, no concrete class, and no Kind string registered anywhere in this platform. Adding five new concrete Domain classes to realise them would be exactly the "contract redesign"/"architectural redesign" this Work Package's own controlling instruction forbids — the identical situation `ADR-0087` (`WP 9.2A`) already faced for Calculation Approval State, and `RequirementsKpiCards`'s own "Released→Satisfied" mapping (`WP 9.1A`) faced for status vocabulary.

`IHasMetadata` (`Contracts/Facets.cs`, `WP 8.2B`) already declares `Classification` — a free-text `string?` facet every `Document` already carries via `EngineeringObjectBase`'s own unconditional facet implementation, populated at construction from `EngineeringObjectMetadata`, and never validated against a closed vocabulary anywhere in the platform (mirrors `RelationshipCategory`'s own "descriptive metadata only" precedent, `ADR-0076`).

## Decision

**Specification, Report, Procedure, Standard, and External Reference are realised as plain `"Document"` Domain objects, distinguished only by `EngineeringObjectMetadata.Classification`** — never a new Domain Kind, interface, or concrete class:

- `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry` declares five named `string` constants (`Specification`, `Report`, `Procedure`, `Standard`, `Datasheet`, `ExternalReference`) — Workspace-layer values only, never a Domain-layer enum or registry contract, mirroring `MechanicalObjectFactoryRegistry`/`CalculationObjectFactoryRegistry`'s own established "Workspace-layer composition helper, no Domain-layer registry contract" precedent (`WP8.2B Dependency Rules.md` §8).
- `Drawing` and `CadModel` remain their own distinct, already-compiled Domain Kinds (`WP 8.2C`) — their own real Kind already is the classification; `DocumentCategory.Of` (`Tempest.App.Workspace.Documents.DocumentsNodeProvider`) maps them to their own Explorer category directly by Kind, never by a redundant `Classification` value.
- `DocumentsNodeProvider`'s own Explorer categorisation and `EngineeringCockpit`'s own KPI reads both derive every named type from this one `Classification` field — never a second, competing classification mechanism.

## Consequences

**Positive:**

- Zero Domain-layer change of any kind — `src/Tempest.Core/EngineeringDomain/` is untouched by this Work Package, honouring "no contract redesign" and "no duplicate framework" exactly.
- Every Document Management verb (Create/Rename/Edit/Delete/Move/Copy/Duplicate/SetStatus/Attach) already works uniformly across all eight named types, since they share one real Domain Kind (`"Document"`) and one real facet set (`IHasBusinessIdentifier`/`IHasMetadata`/`IHasLifecycle`/`IHasRevisions`/`IHasRelationships`/`IHasAttachments`) — no per-type command variant is ever needed.
- A future Work Package that does need a genuinely distinct Specification/Report/Procedure/Standard Domain concept (its own lifecycle rules, its own structured fields) can introduce one without this Work Package's own Workspace-layer classification constants standing in the way — they are a display/categorisation convenience, never load-bearing Domain state.

**Negative:**

- `Classification` is free text, never validated against this Work Package's own six named constants at write time — a caller could set `Classification: "Speciffication"` (misspelled) and get a real, live, but silently un-categorised (`"Uncategorized"`) Document. Judged acceptable: the same open-string, non-validated shape `RelationshipCategory`/`RelationshipKindCategoryMap` already establishes platform-wide (`ADR-0076`), never previously treated as a defect.
- A Specification and a Report are, at the Domain layer, indistinguishable from each other except by this one string field — no compiler-enforced guarantee that a caller passing `"Report"` really means a report. Accepted for the identical reason `WP 9.2A`'s own "Failed"/"Out-of-date" KPI-name mappings were accepted: a disclosed, precedent-following convenience over the existing vocabulary, not a new Domain guarantee.

## Alternatives Considered

**Add five new concrete Domain classes (`Specification`, `Report`, `Procedure`, `Standard`, `ExternalReferenceDocument`), each implementing `IDocument` directly, mirroring `Drawing`/`CadModel`'s own shape.** Considered and rejected outright; this is precisely the "contract redesign"/"architectural redesign" `WP 9.4A`'s own controlling instruction forbids, and would require `WP 8.2A`/`WP 8.2B`/`WP 8.2C` (all `Complete`, all already `Engineering Review APPROVED`) to be reopened to add five new canonical objects to a catalogue those Work Packages explicitly closed.

**Introduce a `DocumentType` enum in `Tempest.Core.EngineeringDomain`.** Considered and rejected for the same reason — any new Domain-layer type, even a small enum, is still a Domain contract change this Work Package's own scope forbids. A Workspace-layer `string` constant costs nothing architecturally and is trivially extensible by a future Work Package without touching `Tempest.Core` at all.

## Related Documents

`ADR-0072`; `ADR-0075`; `ADR-0076`; `ADR-0087`; `Contracts/DocumentationDesign.cs`; `Contracts/Facets.cs`; `WP9.4A Implementation Report.md`; `WP9.4A Technical Debt Assessment.md`; `src/Tempest.App/Workspace/Documents/DocumentObjectFactoryRegistry.cs`; `src/Tempest.App/Workspace/Documents/DocumentsNodeProvider.cs`.
