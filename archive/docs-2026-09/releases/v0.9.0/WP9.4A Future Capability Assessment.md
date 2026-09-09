# WP 9.4A — Engineering Documents Workspace — Future Capability Assessment

## Purpose

Records candidate future capabilities this Work Package's own
implementation surfaced but deliberately did not build.

## `FCR-0054` — Real File/URL Attachment Storage Service

`TD-31` (this Work Package's own Technical Debt Assessment) discloses
that `Attachment`/`IAttachment` carry descriptive metadata only — no
actual file bytes, no resolvable path, no URL-fetch capability exists
anywhere in this platform. A future implementation would need a genuine
Platform Service decision (local filesystem storage? a blob-storage
abstraction? an external document-management-system integration?) —
a real architectural question this Work Package's own narrow,
integration-only scope cannot and should not answer speculatively.
**Recommended once a real, demonstrated need for actual file content
(rather than metadata-only Attachments, which already serve every scope
item this Work Package's own controlling instruction names) exists.**

## `FCR-0055` — Verification Workspace

This Work Package's own disclosed `WP 9.3A` numbering gap (see
Implementation Report) means no Verification Workspace, and therefore no
live Verification Domain object, exists anywhere in this platform today.
`WP 9.4A`'s own Digital Thread scope names Verification as one of eight
nodes Documents should navigate to/from; this Work Package satisfies
that structurally (the identical, generic `LinkAsync`/`GetRelationshipsAsync`
mechanism every other Digital Thread link already uses) but has no live
Verification object to link a representative document to today — the
Test Report instead `references` the one real Requirement with an
actually-recorded Verification, the closest real, live anchor available.
`Tempest.Core.Verification` (`WP 7.1E`) is itself already real,
tested, and Workspace-invisible — architecturally the closest remaining
precedent to `WP 9.2A`'s own "already-real Calculation Framework, never
introduced to the Workspace" starting point. **Recommended as the most
natural next real-discipline Work Package** — it would also retroactively
complete every other discipline's own already-disclosed "Verification
status is always Unknown" placeholder (`RequirementsStatus`'s own
Cockpit sibling, `VerificationStatus`, unchanged since `WP 8.1C`), not
only this one's own.

## `FCR-0056` — Governance & Risk Workspace (Risks, Issues, Decisions, Hazards, Assumptions)

This Work Package creates one live `Decision` (`WP 8.2C`, instantiated
by no sample module anywhere before this) and reads one already-live
`Risk` (created by the base `EngineeringDomainSampleModule`), purely to
honour its own explicit "Documents ↔ Risks/Decisions" Digital Thread
requirement — neither gets its own Explorer area, Property Inspector
Kind registration, or dedicated Workspace commands; both remain reachable
only indirectly, through a Document's own Digital Thread facets. `Issue`/
`Risk`/`Hazard`/`Decision`/`Assumption` (`Contracts/GovernanceRisk.cs`,
`WP 8.2C`) are all already real, compiled, `EngineeringObjectBase`-derived
concrete classes, architecturally ready for the identical Kind-keyed
Workspace treatment every other discipline has now received four times.
The Engineering Cockpit's own `OpenDecisions`/`RiskSummary` members
remain fixed, disclosed placeholder content, unchanged by this Work
Package. **Recommended once a real, demonstrated need for a dedicated
Governance & Risk Workspace presence exists** — the two objects this
Work Package creates already prove the underlying Domain classes are
Workspace-ready; no further Domain-layer work would be needed to build
this, only the same, now four-times-proven Workspace-layer pattern.

## Not Recommended: A Dedicated Document-Baseline Binding Contract

Considered directly during implementation, for the "Baseline awareness"
scope item. `Configuration`/`Baseline`/`Release` (`WP 9.0B`) already
accept any relationship kind from any `IEngineeringObject` via the
existing, shared `LinkAsync` mechanism — a Document is already
"Baseline aware" in the identical sense every other Engineering Object
is. **Not recommended** — a dedicated binding contract would duplicate
a capability the existing Digital Thread mechanism already provides.

## Not Recommended: Five New Concrete Domain Classes for Specification/Report/Procedure/Standard/External Reference

Considered directly and rejected as this Work Package's own delivered
design (`ADR-0088`), not merely as a future candidate — see the ADR's
own Alternatives Considered section. **Not recommended** unless a future
Work Package identifies a genuine, demonstrated need for one of these
five to carry its own structured fields or its own distinct lifecycle
rules beyond what `Document`/`IHasMetadata.Classification` already
provides.

## Verdict

Three new candidates recorded (`FCR-0054`–`FCR-0056`); none built
speculatively ahead of genuine need; two further candidates considered
and explicitly not recommended, with reasoning recorded rather than
silently dropped. `FCR-0055` (Verification Workspace) is this Work
Package's own explicit recommendation for what should follow it,
mirroring `WP 7.2A`'s/`WP 8.9.0`'s own precedent of naming a concrete
next candidate rather than leaving the choice fully open.

## Related Documents

`docs/governance/Future Capability Register.md`; `ADR-0088`; `WP9.2A
Future Capability Assessment.md` (`FCR-0051`–`FCR-0053`); `WP9.4A
Technical Debt Assessment.md` (`TD-30`, `TD-31`); `WP9.4A Engineering
Review Report.md`.
