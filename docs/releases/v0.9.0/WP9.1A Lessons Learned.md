# WP 9.1A — Requirements Management Workspace — Lessons Learned

## Purpose

Records what went well, what was harder than expected, and what a
future Work Package facing a similar situation should know going in.

## What Went Well

**The Kind-keyed Workspace extension model generalised cleanly to a
genuinely different Domain architecture.** `ADR-0067`'s own registration
pattern (`RegisterExplorerArea`/`RegisterView`/`RegisterFacetProvider`)
was designed once, against Mechanical's facet-composed
`EngineeringObjectBase` objects. Requirements' own immutable-snapshot,
service-oriented architecture is genuinely different underneath — and
the extension model did not care. `RequirementsNodeProvider`/
`RequirementsWorkspaceViewFactory`/`RequirementsPropertyFacetProvider`
mirror their Mechanical counterparts almost line-for-line in shape,
despite reading from `IRequirementsService` instead of
`EngineeringDomainContext.Repository`. Strong, now twice-proven evidence
the abstraction boundary was drawn in the right place.

**Search needed zero new code.** `ProjectExplorer.FilterAsync` (`WP8.1B`)
was already generic over whatever `IProjectExplorerNodeProvider` is
registered for the current area — the entire "Search" scope item was
satisfied the moment `RequirementsNodeProvider` was registered. Worth
remembering: a capability scoped as if it needs new implementation
sometimes turns out to already exist, one layer up, generically.

## What Was Harder Than Expected

**Two genuine gaps in the Requirements Framework's own enumeration
surface, undiscovered until the Explorer tree needed them.**
`IRequirementsService` had no way to list every Collection or every
Group — `WP 7.3A` never needed one, since nothing before this Work
Package ever needed to *show* every Requirement Set at once. Building
the Explorer tree surfaced this immediately; `ListCollectionsAsync`/
`ListGroupsAsync` (`ADR-0084`) closed it using the exact same
`IPersistenceStore`-direct-registry pattern `FindByIdentifierAsync`
already proved for the identical reason. Worth remembering: a framework
built to serve one consumer (a validation-and-audit service, in
`WP 7.3A`'s own case) can have real, unexercised gaps for a second,
different consumer (a browsable tree) that only implementation surfaces.

**A permission-gated read hiding behind three different passive
surfaces.** `GetEvidenceAsync`'s own transitive gate on
`Verification.ReadPermission` was correct and intentional when `WP 7.3A`
built it — evidence aggregation is a deliberately protected read. It
was never exercised by an unprivileged principal before this Work
Package, because nothing before it built a Property Inspector facet, a
Cockpit KPI, or a validation check that needed *only* the fact of
verification, not its full protected detail. All three needed fixing,
found only once the representative sample data (built under its own,
deliberately unprivileged principal, matching every other sample
module's own precedent) was exercised through the real Workspace
integration suite — not by any unit test in isolation, since a
unit test typically grants exactly the permission the method under
test needs. Mirrors `WP 9.0B`'s own `ReviseAsync` finding almost
exactly: representative data that tells a complete, realistic story
finds defects a narrowly-scoped unit test, written to prove one thing,
never will.

**A file placed in the wrong project, caught before it mattered.**
`RequirementCollectionExportAdapter` was first written under
`Tempest.App.Workspace.Requirements`, then needed by
`RequirementsWorkspaceSampleModule` (`Tempest.Samples`) — but
`Tempest.Samples` does not, and must not, reference `Tempest.App` (the
dependency runs the other way). Caught immediately by the build, before
any commit; moved to `Tempest.Samples` directly, alongside
`RequirementExportAdapter`'s own identical precedent, which was the
right location all along. Worth remembering: an adapter's own natural
home is determined by what it depends on and what depends on it, not by
which Work Package's own scope item it satisfies — this one depended on
nothing Workspace-specific from the start.

## Process Observations

Two genuine implementation-defect findings (the permission-gated read,
the `RequirementGroupDto` resolution ambiguity) surfaced during this
Work Package's own forward work, not a dedicated audit — both in code
written earlier in this same, not-yet-committed session. Fixing them
immediately, with regression coverage, rather than only disclosing
them, was the right call specifically because neither had yet become
part of any commit or tagged release — the same pattern `WP 9.0B`'s own
Lessons Learned already established, now confirmed a second time.

## Recommendation for Future Work Packages

When wiring a second (or later) real Engineering discipline into the
Workspace, budget explicit time to run the full Workspace integration
suite against real, representative data under a deliberately
unprivileged principal — not just the Domain layer's own unit tests,
which typically grant exactly the permission the method under test
needs and will never surface a permission-gated-read-from-a-passive-surface
defect. This Work Package's own three fixes were all found this way, at
the Workspace-integration layer, none at the Domain-unit-test layer.

## Related Documents

`WP9.1A Implementation Report.md`; `WP9.1A Technical Debt Assessment.md`
(`TD-28`); `ADR-0084`; `ADR-0085`; `WP9.0A Lessons Learned.md`; `WP9.0B
Lessons Learned.md` (`TD-27`).
