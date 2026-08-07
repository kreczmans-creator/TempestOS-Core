# WP 9.4A — Engineering Documents Workspace — Lessons Learned

## Purpose

Records what went well, what was harder than expected, and what a
future Work Package facing a similar situation should know going in.

## What Went Well

**The Kind-keyed Workspace extension model generalised a fourth time,
this time to "many display categories over one real Kind" rather than
"one synthetic Kind with no Domain identity."** `WP 9.2A` proved the
model against content with no `Guid` at all (Calculation Templates).
This Work Package proves a different generalisation: nine Explorer
categories (`DocumentCategory.Of`), computed purely from each object's
own already-real `Kind`/`Classification`, over exactly one real Domain
Kind (`"Document"`, plus the two already-distinct `"Drawing"`/`"CadModel"`
Kinds). No new provider abstraction was needed — `DocumentsNodeProvider`
still returns ordinary `ProjectExplorerNode`s for its own category
"roots," the identical shape `CalculationsNodeProvider`'s own
`"Templates"` node already established.

**This Work Package's own named statuses mapped onto `LifecycleState`
with zero aliasing — the simplest lifecycle integration of the four
real-discipline Work Packages so far.** Where Calculations needed five
descriptive aliases over four states (`ADR-0087`) and Requirements
needed a "Released→Satisfied" naming translation, Draft/Review/Approved/
Released map one-for-one onto `LifecycleState`'s own existing values.
Worth noting as a genuine data point: not every discipline's own named
vocabulary needs translation — sometimes the existing platform-wide
enum already says exactly what is needed, and the honest thing to do is
notice that and change nothing.

**Zero Domain-layer changes, a fourth consecutive time.** Like `WP 9.2A`
before it, this Work Package added no new Domain-layer member — `Document`/
`Drawing`/`CadModel`/`IHasAttachments`/`IHasMetadata.Classification` were
already sufficient, once the Workspace layer knew how to read, categorise,
and dispatch them. Two of four real-discipline Work Packages now needing
zero Domain-layer change is a meaningful signal that `WP 8.2A`–`WP 8.2C`'s
own Engineering Domain architecture was drawn with real headroom, not
merely enough for the first consumer.

## What Was Harder Than Expected

**Deciding how six named Document types, with only three real Domain
Kinds, should be represented — without silently under-delivering three
of them.** The instinctive first read of "Specification, Report,
Procedure, Standard, Datasheet" is "five more Kinds like Drawing" — but
building them would have violated this Work Package's own explicit "no
contract redesign" instruction outright. Resolved by recognising
`IHasMetadata.Classification` already exists, is already free text, and
is already unvalidated platform-wide (`RelationshipCategory`'s own
identical precedent) — the same category of realisation `WP 9.2A`'s own
"Failed"/"Out-of-date" KPI-name mappings required, applied here to an
entire taxonomy rather than a handful of status names.

**Building representative data whose own cross-links stayed internally
consistent across four prior sample modules' own already-fixed data,
while a fifth, pre-existing sample module (the base
`EngineeringDomainSampleModule`) turned out to already seed one further,
un-parented `"Drawing"`.** This Work Package's own Documents Cockpit KPI
test initially assumed a total of nine live Documents — the exact count
this Work Package's own sample module creates — and failed once run
against the real, composed graph, which correctly also counts the base
sample's own pre-existing "Sample Drawing" (ten total). Caught
immediately by the failing integration test itself, not missed silently
— but a reminder that "how many Documents exist" in an integration test
must always be reasoned about against the *full* composed sample graph a
test's own module list assembles, never only the Work Package's own new
module's own count in isolation.

**Choosing exactly one, honest "Missing Evidence" example, not zero and
not several.** A KPI card reporting `"0"` for every run would never
prove the heuristic actually counts anything; reporting several by
accident (as an early draft did, before the Standard document was
deliberately `references`-linked to the Procedure) would have diluted
the demonstration and made the exact expected count harder to reason
about by inspection. Resolved by deliberately choosing exactly one
Document (the External Reference) to leave genuinely unlinked, and
linking every other new Document to something real — mirrors `WP 9.2A`'s
own lesson about representative data needing its own arithmetic checked
by hand, applied here to relationship-graph shape rather than a
calculation formula.

## Process Observations

Unlike `WP 9.1A`'s permission-gated-read finding and `WP 9.0B`'s
`ReviseAsync` finding, this Work Package's own implementation surfaced
no genuine, pre-existing defect in already-real code — every disclosed
gap (`TD-31`, and `TD-30`'s own extended consequence for Verification
traceability) is a pre-existing absence, not a bug in a capability that
already existed. Consistent with this Work Package's own narrow
footprint: touching zero Domain-layer files leaves zero surface for a
Domain-layer regression to hide in — the same observation `WP 9.2A`'s
own Lessons Learned already recorded, now confirmed a second time.

This Work Package is also the first to begin under an explicitly
disclosed Work Package numbering gap (`WP 9.3A` never having been
built) rather than a clean, sequential predecessor. The governing
instruction ("disclose all inconsistencies... do not silently modify
historical records") was followed by recording the gap plainly in this
release's own Implementation Report and `PROJECT_STATUS.md`, rather than
either silently renumbering this Work Package to `9.3A` or silently
proceeding as though nothing were unusual.

## Recommendation for Future Work Packages

When a controlling instruction names more sub-types of a concept than
the Domain layer has dedicated contracts for, check first whether an
existing, general, already-open facet (a free-text `Classification`, an
existing enum, a named intermediate value) already expresses every named
sub-type honestly before reaching for a new Domain type — this Work
Package is the second consecutive real-discipline Work Package (after
`WP 9.2A`) to find the honest answer was "reuse and disclose the
mapping," not "invent something more precise." When a controlling
instruction is received out of the expected numbering sequence, disclose
the gap plainly, once, in the Implementation Report and the top-level
status document — never silently absorb it into an unremarkable-looking
sequential narrative.

## Related Documents

`WP9.4A Implementation Report.md`; `WP9.4A Technical Debt Assessment.md`
(`TD-31`); `ADR-0088`; `WP9.0B Lessons Learned.md`; `WP9.1A Lessons
Learned.md`; `WP9.2A Lessons Learned.md`.
