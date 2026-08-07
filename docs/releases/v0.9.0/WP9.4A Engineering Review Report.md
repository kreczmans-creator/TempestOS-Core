# WP 9.4A — Engineering Documents Workspace — Engineering Review Report

## Purpose

Reviews whether the shipped implementation satisfies `WP 9.4A`'s own
controlling instruction, and whether every engineering judgement call
made along the way was reasonable and disclosed.

## Acceptance Criteria Review

| Requirement | Verdict | Evidence |
|---|---|---|
| Engineering Documents, Drawings, Specifications, Reports, Procedures, Standards, Datasheets, External References, Attachments, Supporting Evidence | **Met, with a disclosed representation for the six items with no dedicated Domain Kind** | `IDocument`/`IDrawing`/`ICadModel` (`WP 8.2C`, unchanged); Specification/Report/Procedure/Standard/Datasheet/External Reference are `Classification`-tagged `Document` objects (`ADR-0088`); Attachments via `IHasAttachments` (existing since `WP 8.2C`, now wrapped by `AttachDocumentCommand` for the first time); Supporting Evidence via the existing Digital Thread relationship reads (`ITraceable.GetEvidenceAsync` remains structurally empty platform-wide, `TD-30`, not fixed here). |
| Workspace / Cockpit / Project Explorer / Property Inspector / Navigation / context menus / Command Palette / Search / Workspace Commands | **Met** | `DocumentsNodeProvider`/`DocumentsWorkspaceView(Factory)`/`DocumentsPropertyFacetProvider`, 11 registered commands, real Cockpit KPIs; Search needed zero new code (`ProjectExplorer.FilterAsync`, `WP8.1B`, already generic). |
| Create/Edit metadata/Rename/Delete/Copy/Duplicate/Move/Revision management/Status changes/Baseline awareness/Document relationships | **Met, with a disclosed scope note on Baseline awareness** | Nine command classes; Revision management is `IHasRevisions`/`ReviseDocumentCommand` (unchanged mechanism, `WP 8.2C`); Baseline awareness reuses the existing `Configuration`/`Baseline`/`Release` Mechanical concepts (`WP 9.0B`) unmodified — a Document can be `references`-linked to a Baseline exactly like any other object, no dedicated "Document baseline" mechanism was built or asked for beyond that — see Scope Discipline Review, below. |
| Drawing numbers, Document numbers, Revision identifiers, Approval status, Document classification, Ownership, Discipline, Metadata, Attachments, External file references, Engineering notes | **Met** | `DrawingNumber` (`IDrawing`, unchanged); "Document Number" reuses `IHasBusinessIdentifier.Identifier`; Revision identifiers reuse `CurrentRevisionNumber`; Approval status/Classification/Owner/Discipline/Tags/Notes all reuse existing, unmodified `IHasLifecycle`/`IHasMetadata` facets; External file references realised as the `"External Reference"` Classification, Content field carrying a placeholder URI (no file/URL storage service exists anywhere in this platform, `TD-31`, disclosed, not fixed). |
| Digital Thread navigation: Requirements, Calculations, Verification, Mechanical Product Structure, Documents, Risks, Decisions, Evidence | **Met, with one disclosed structural-only item** | Real, live links to Requirements, Calculations, Mechanical Product Structure, Risks (existing, queried), and a newly-created Decision, all via already-mapped relationship kinds; Verification is structurally supported (the identical `LinkAsync` mechanism) but has no live Verification Domain object anywhere in the platform to link to today (`TD-30`) — the Test Report instead `references` the one real Requirement with an actually-recorded Verification, the closest real, live anchor. |
| Engineering Cockpit real KPIs (Total Documents/Draft/Review/Approved/Released/Outstanding Reviews/Missing Evidence/Documentation Health) | **Met, one disclosed heuristic** | `DocumentsKpiCards`/`DocumentationStatus`; "Missing Evidence" → a disclosed heuristic (zero Attachments and zero Digital Thread links) — see Implementation Report. |
| Representative data: GA Drawing, Detail Drawing, Specification, Test Report, Design Report, Material Datasheet — linked to Requirements, Calculations, Verification, Assemblies, Parts | **Met, expanded to nine documents, with the disclosed Verification-anchor substitution above** | `EngineeringDocumentsWorkspaceSampleModule` — nine real documents (the six named plus a Procedure, a Standard, and an External Reference, disclosed as a scope expansion mirroring `WP 8.1C`'s own precedent), real links to the Mechanical/Requirements/Calculations sample data. |
| Quality: existing architecture/layering/contracts, Digital Thread compatibility, Workspace consistency | **Met** | See Architecture Conformance Review. |
| Unit/integration/Workspace tests; repeated Debug/Release verification | **Met** | 57 new tests, 1922/1922, four full clean-rebuild-and-test runs. |
| Documentation and Governance | **Met** | This document and its siblings; governance registers updated; the `WP 9.3A` numbering gap disclosed, not silently resolved. |
| No architectural redesign; no contract redesign; no duplicate framework; reuse existing services exclusively | **Met, one disclosed additive Workspace-layer decision (`ADR-0088`), zero Domain-layer changes** | See Architecture Conformance Review. |

## Scope Discipline Review

**"Baseline awareness" is not a new Document-specific mechanism.** The
Work Package's own controlling instruction names it alongside Revision
management/Status changes/Document relationships, as one of several
Document Management capabilities, not as a request for a new
Document-Baseline binding contract. `Configuration`/`Baseline`/`Release`
already exist (`WP 9.0B`), already accept `references`/any relationship
kind from any `IEngineeringObject` via the shared `LinkAsync` mechanism.
A Document is therefore already "Baseline aware" in exactly the sense
every other Engineering Object is — reachable from, and able to
reference, a Baseline through the one, existing Digital Thread
mechanism. No dedicated `IDocumentBaseline` contract or command was
built; none was judged necessary or in scope.

**"Supporting Evidence" is the existing Digital Thread read, not
`ITraceable.GetEvidenceAsync`.** Identical reasoning to `WP 9.2A`'s own
treatment of Calculation evidence: `EvidenceComposer`/`GetEvidenceAsync`
honestly resolves empty for every Document today (`TD-30`, a pre-existing,
platform-wide gap, not introduced here). `DocumentsPropertyFacetProvider`/
`EngineeringCockpit.HasMissingEvidence` both read
`GetRelationshipsAsync`/`RelationshipRepository` directly instead — real,
correct, evidentiary data, just not through the Domain's own composed
`IEvidence` shape.

**Nine representative documents, not six.** The Work Package's own
"Representative Data" section names six by name but its own broader
Scope section names eight Document types plus Attachments/Supporting
Evidence as first-class capabilities. Building only the six literally
named would leave Procedures/Standards/External References — three of
the Scope section's own eight named types — with zero representative
data to demonstrate the classification taxonomy against. Judged
necessary for a genuine, complete demonstration; disclosed directly here
and in the Implementation Report, mirroring `WP 8.1C`'s own precedent
for an expanded controlling instruction.

## Engineering Judgement Calls Requiring Explicit Ratification

1. **Specification/Report/Procedure/Standard/Datasheet/External Reference realised as `Classification`-tagged `Document` objects, never five new concrete Domain classes.** Ratified — the only way to honour "no contract redesign" for six of the Work Package's own eight named Document types; recorded as `ADR-0088`.
2. **`AttachDocumentCommand`, the one genuinely new command class.** Ratified — `IHasAttachments` already exists and is already on `IDocument`; a Workspace command wrapping it is the narrowest possible additive gap-fill, not a new Domain capability.
3. **"Missing Evidence" read from Attachments-plus-relationships, never `GetEvidenceAsync`.** Ratified — `GetEvidenceAsync` is confirmed, directly, to resolve empty for every Document today (`TD-30`); the disclosed heuristic reads real, live data instead.
4. **Documents↔Verification traceability left structural, not populated with a fabricated Verification object.** Ratified — no live Verification Domain object exists anywhere in this platform (a consequence of the disclosed `WP 9.3A` numbering gap); inventing one to populate a demonstration link would misrepresent the platform's own actual current state, which this Work Package's own governance discipline ("disclose all inconsistencies") explicitly forbids.
5. **`EngineeringDocumentsWorkspaceSampleModule` constructor-injects three prior sample modules and queries a fourth's own already-created Risk object by Kind.** Ratified — the same, already-established ordinal-Id-ordering precedent `WP 9.1A`/`WP 9.2A` both set, extended by one dependency and one query-not-inject variant (for the base sample's own Risk, which exposes no public Id); confirmed safe by four clean test runs with zero flakes.

## Verdict

**No Release Blocking findings.** Every acceptance criterion is met;
the six Document types with no dedicated Domain contract are represented
honestly through the existing, open `Classification` facet rather than
through invented new Domain types; the one genuine platform gap this
Work Package's own scope runs directly into (no live Verification
object to demonstrate traceability against) is disclosed plainly, not
worked around by fabrication; every engineering judgement call above is
ratified with its own recorded reasoning.

## Related Documents

`WP9.4A Implementation Report.md`; `ADR-0088`; `WP9.4A Architecture
Conformance Review.md`; `WP9.4A Technical Debt Assessment.md`.
