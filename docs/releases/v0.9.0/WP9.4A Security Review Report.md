# WP 9.4A — Engineering Documents Workspace — Security Review Report

## Purpose

A proportionate security review of the Documents Workspace layer's
nine commands, `DocumentsNodeProvider`/`DocumentsPropertyFacetProvider`,
and the Engineering Cockpit's new Documentation reads — reviewed across
the same dimensions this project's own established Security Review
convention uses. Fifth consecutive dedicated Security Review (after
`WP 9.0A`/`WP 9.0B`/`WP 9.1A`/`WP 9.2A`).

## Review

| Dimension | Finding | Classification |
|---|---|---|
| **Authorisation boundaries** | Every new Documents command performs no internal permission gating of its own — mirrors every `WP 9.0A`/`WP 9.0B`/`WP 9.1A`/`WP 9.2A` command's own identical, calling-layer-enforced posture (`ADR-0061`, unchanged). | Not Applicable — reviewed, design consistent with established precedent |
| **`AttachDocumentCommand`'s own attachment metadata surface** | Accepts `FileName`/`ContentType`/`SizeInBytes` only — plain `string`/`long` fields on the existing `Attachment` record (`WP 8.2C`, unchanged). No file bytes, no file path, no URL is ever accepted, stored, or resolved by this command or by `IHasAttachments` itself — there is no file I/O surface to attack, because no file storage capability exists anywhere in this platform (`TD-31`). | Not Applicable — reviewed, no reachable file-system/network surface |
| **External Reference Document's own Content field** | Holds a plain, descriptive `string` (a placeholder URI in the representative data) — never parsed, never fetched, never used to construct a file path or a network request anywhere in this Work Package's own code. Confirmed by direct inspection of every read site (`DocumentsPropertyFacetProvider`, `EngineeringCockpit`). | Not Applicable — reviewed, inert display text only |
| **Soft-delete integrity** | `DeleteDocumentObjectCommand` never erases a document, revision, or relationship — mirrors every other Domain mutation's own append-only ethos (`EngineeringObjectBase.DeleteAsync`, unchanged); `IsDeleted` is the only state that changes. | Not Applicable — reviewed, secure by construction |
| **`DeleteDocumentObjectCommand`'s has-children guard** | Correctly blocks deletion of a Document with live `IHasParent`-nested children, reusing `EngineeringObjectBase.DeleteAsync`'s own already-proven guard unmodified. Proven by a dedicated test. | Not Applicable — reviewed, guard proven effective |
| **`SetDocumentStatusCommand`'s own transition guard** | Defers entirely to the existing, unmodified `LifecycleTransitionTable` — an impermissible transition (e.g. Draft straight to Released) is rejected identically to every other discipline's own identical mechanism. Proven by a dedicated test (`SetStatus_ImpermissibleTransition_Fails`). | Not Applicable — reviewed, secure by construction |
| **`ICalculationResult`/`IVerificationResult`/`IApprovalGate` family reachable only through `ITraceable.GetEvidenceAsync`, never called by this Work Package** | Confirmed by direct inspection: `DocumentsPropertyFacetProvider`/`EngineeringCockpit.HasMissingEvidence` never call `GetEvidenceAsync` on any Document — the same, now four-times-established avoidance pattern `WP 9.1A`/`WP 9.2A` already set, applied from the start. No permission-gating surprise is possible because the gated path is never reached at all. | Not Applicable — reviewed, avoided by construction |
| **Cross-sample-module Risk query** | `EngineeringDocumentsWorkspaceSampleModule` reads `_context.Repository.ListByKindAsync("Risk")` and links to the first live result — a read-only query against the same shared, in-process `EngineeringDomainContext` every other Workspace read already uses, never a new access path or elevated capability. | Not Applicable — reviewed, no new access path |
| **Resource exhaustion** | `DocumentsNodeProvider`/`EngineeringCockpit.LiveDocuments`/`DocumentsKpiCards` are all O(n) in total Document-Kind-document count (`DocumentsNodeProvider.LiveDocumentsAsync` additionally queries three Kinds, so O(3n)) — the same already-tracked, disclosed characteristic `TD-22`/`TD-24`/`WP 9.0A`'s, `WP 9.0B`'s, `WP 9.1A`'s, and `WP 9.2A`'s own equivalent findings carry. | Technical Debt — mirrors the existing, already-tracked pattern; not separately re-registered |
| **Serialization safety** | `EngineeringObjectMetadata`, `Attachment`, and every command's own plain properties are closed-shape C# types — no polymorphic or type-name-carrying deserialisation anywhere this Work Package touches (unlike `WP 9.2A`'s own `ExecuteCalculationCommand.InputJson`, this Work Package has no JSON-deserialisation surface at all). | Not Applicable |
| **Dependency risk** | No new third-party dependency of any kind. | Not Applicable |
| **Backwards compatibility** | Every existing `IDocument`/`IDrawing`/`ICadModel`/`EngineeringCockpit` consumer is unaffected — every new member is additive; confirmed by the full, unmodified `WP 7.x`/`WP 8.x`/`WP 9.0A`–`WP 9.2A` test suites passing unchanged alongside the 57 new tests. | Not Applicable |

## New Debt Disclosed by This Review

**`TD-31` — No File/URL Attachment Storage Service.** `Attachment`
(`WP 8.2C`, unchanged) carries only descriptive metadata
(`FileName`/`ContentType`/`SizeInBytes`) — no actual file bytes, no
resolvable path, no URL-fetch capability exists anywhere in this
platform. `AttachDocumentCommand` and the `"External Reference"`
Classification both, honestly, only ever record or display metadata/
placeholder text — see `WP9.4A Technical Debt Assessment.md` for the
full entry.

No further new Technical Debt item is registered by this review
specifically — the one finding above classified as debt (O(n)/O(3n)
list-and-filter reads) mirrors an already-tracked, existing pattern
across five consecutive Work Packages now. The one further, pre-existing
gap this review confirms but does not itself introduce
(`ICalculationResult`/`IVerificationResult`/`IApprovalGate`'s own total
absence, `TD-30`) is registered in full in `WP9.2A Technical Debt
Assessment.md`, not duplicated here.

## Verdict

**Zero Release Blocking findings.** No permission-gating availability
defect was introduced (the class of issue `WP 9.1A` found and fixed was
avoided here from the start, by never calling `GetEvidenceAsync` at
all). No new attack surface was introduced — the one new external input
boundary this Work Package adds (`AttachDocumentCommand`'s own
metadata fields) accepts plain, inert descriptive text only; no file
I/O, no URL resolution, no deserialisation of any kind is reachable
anywhere in the new code.

## Related Documents

`ADR-0088`; `WP9.0A Security Review Report.md`; `WP9.0B Security Review
Report.md`; `WP9.1A Security Review Report.md`; `WP9.2A Security Review
Report.md`; `WP9.4A Technical Debt Assessment.md`; `docs/governance/Quality/Technical Debt
Register.md`.
