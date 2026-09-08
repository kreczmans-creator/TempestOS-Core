# WP 9.5A — Manufacturing Workspace — Security Review Report

## Purpose

A proportionate security review of the Manufacturing Workspace layer's
eight commands, the disclosed cross-Work-Package reuse of Documents'/
Verification's own facet/view providers, and the Engineering Cockpit's
new Manufacturing reads — reviewed across the same dimensions this
project's own established Security Review convention uses. Seventh
consecutive dedicated Security Review (after `WP 9.0A`/`WP 9.0B`/
`WP 9.1A`/`WP 9.2A`/`WP 9.4A`/`WP 9.3A`).

## Review

| Dimension | Finding | Classification |
|---|---|---|
| **Authorisation boundaries** | Every new Manufacturing command performs no internal permission gating of its own — mirrors every prior real-discipline command's own identical, calling-layer-enforced posture (`ADR-0061`, unchanged). | Not Applicable — reviewed, design consistent with established precedent |
| **Cross-Work-Package facet/view provider reuse (`Inspection`/`WorkInstruction`)** | `DocumentsPropertyFacetProvider`/`VerificationActivityPropertyFacetProvider`, constructed with a Manufacturing Kind string, perform an identity check against `EngineeringDomainContext.Repository` exactly as they do for their own native Kind — no Kind-string bypass or authorisation weakening is introduced by reuse, since neither provider ever performed Kind-specific authorisation logic to begin with. Confirmed by direct inspection and by dedicated tests constructing each provider against a live Manufacturing object. | Not Applicable — reviewed, reuse introduces no new surface |
| **`CreateManufacturingObjectCommand`'s own input surface** | Accepts plain `string`/`Guid?`/`string?` fields, switching on a closed set of three supported Kind strings — an unrecognised Kind returns `CommandResult.Failure` via a caught `ArgumentException`, never an unhandled exception. No deserialisation of any kind occurs. | Not Applicable — reviewed, no reachable deserialisation surface |
| **Soft-delete integrity** | `DeleteManufacturingObjectCommand` never erases a document, revision, or relationship — mirrors every other Domain mutation's own append-only ethos (`EngineeringObjectBase.DeleteAsync`, unchanged); `IsDeleted` is the only state that changes. | Not Applicable — reviewed, secure by construction |
| **`DeleteManufacturingObjectCommand`'s has-children guard** | Correctly blocks deletion of a Routing with live `IHasParent`-nested Operation steps, reusing `EngineeringObjectBase.DeleteAsync`'s own already-proven guard unmodified. Proven by a dedicated test. | Not Applicable — reviewed, guard proven effective |
| **Release/Archive aliasing** | Both dispatch through the one `SetManufacturingObjectStatusCommand`/`IHasLifecycle.TransitionAsync`, which defers entirely to the existing, unmodified `LifecycleTransitionTable` — an impermissible transition is rejected identically regardless of which Command Palette entry a caller reaches it through. Proven by a dedicated test (`SetStatus_ImpermissibleTransition_Fails`). | Not Applicable — reviewed, secure by construction |
| **Reused `RecordVerificationResultCommand` against an `"Inspection"` target** | Confirmed by direct inspection: the handler dispatches through `IVerificationService.RecordAsync` by Id alone, never checking the target's own Kind string — an `"Inspection"` target is handled identically to a `"VerificationActivity"` target, with no new authorisation path introduced. | Not Applicable — reviewed, avoided by construction |
| **Cross-sample-module dependency graph** | `EngineeringManufacturingWorkspaceSampleModule` constructor-injects four prior sample modules directly, plus a fifth's own already-created Supplier object queried by Kind — the same, already-established ordinal-Id-ordering precedent every prior Work Package's own sample module sets, extended by none; deliberately does not depend on `EngineeringVerificationWorkspaceSampleModule`, checked and disclosed as a genuine ordering-safety concern, not merely an unneeded dependency. | Not Applicable — reviewed, dependency graph verified safe |
| **`Mechanical.SetBomLineCommand` dispatched against a Manufacturing Kind** | Confirmed by direct inspection and by a dedicated test: the handler casts to `IHasBomLine` only, never checking the target's own Kind string — dispatching it against a `"ManufacturingOperation"` introduces no new authorisation path, and no cross-discipline data leak (the BOM line facets remain scoped to the one target object). | Not Applicable — reviewed, avoided by construction |
| **Resource exhaustion** | `ManufacturingNodeProvider`/`EngineeringCockpit.LiveManufacturingObjects`/`ManufacturingKpiCards` are all O(n) in total Manufacturing-Kind document count, plus O(m) in records per Inspection for result-history reads — the same already-tracked, disclosed characteristic every prior real-discipline Work Package's own equivalent finding carries. | Technical Debt — mirrors the existing, already-tracked pattern; not separately re-registered |
| **Serialization safety** | No new serialised type is introduced by this Work Package; every Manufacturing command parameter is a plain, closed-shape primitive or existing `Tempest.Core` type. | Not Applicable |
| **Dependency risk** | No new third-party dependency. | Not Applicable |
| **Backwards compatibility** | Every existing `EngineeringCockpit`/`DocumentObjectFactoryRegistry`/`DocumentsNodeProvider` consumer is unaffected — every new member is additive; confirmed by the full, unmodified prior test suites passing unchanged alongside the 54 new tests. | Not Applicable |

## New Debt Disclosed by This Review

None registered specifically by this review — the resource-exhaustion
finding above mirrors an already-tracked, existing pattern across six
consecutive Work Packages now. The `EngineeringCockpit.FormatCoverage`
zero-denominator text inaccuracy found during this Work Package (see
Implementation Report) is a display-accuracy characteristic, not a
security finding, and is recorded in `WP9.5A Technical Debt
Assessment.md` instead.

## Verdict

**Zero Release Blocking findings.** No permission-gating availability
defect was introduced. No new attack surface was introduced by the
disclosed cross-Work-Package provider/command reuse — every reused type
was already generic over its own Kind parameter or already Kind-agnostic
by construction, confirmed by direct inspection and proven by dedicated
tests, not merely assumed compatible. Every new external input boundary
accepts closed, non-polymorphic types only, with no deserialisation
anywhere.

## Related Documents

`ADR-0091`; `WP9.0A Security Review Report.md`; `WP9.0B Security Review
Report.md`; `WP9.1A Security Review Report.md`; `WP9.2A Security Review
Report.md`; `WP9.4A Security Review Report.md`; `WP9.3A Security Review
Report.md`; `WP9.5A Technical Debt Assessment.md`; `docs/governance/
Quality/Technical Debt Register.md`.
