# The Debt Tranche: Writes on the Bus, Unique Identifiers, Index-First Rehydration

**Release:** release candidate on `release/v0.20.0` (head `3ce8f20`,
2026-09-15) · **Work Package(s):** `WP 20.1A1`, `WP 20.1A2`, `WP 20.1C2`,
`WP 20.3A`, `WP 20.3B` · **Debt:** `TD-28` (closed), `TD-38` (closed),
`TD-88` (index stage shipped, materialisation left open), `TD-20`,
`TD-18`, `TD-21`, `TD-03`, `TD-05`, `TD-170` (closed), `TD-84`
(re-scoped) · **Decision:** `ADR-0145` (applied to a new service),
`ADR-0051` (addendum), `ADR-0083` (addendum) · **Code:**
`Tempest.Core.Requirements.RequirementsService`,
`Tempest.Core.EngineeringDomain.BusinessIdentifierIndex`/
`EngineeringObjectFactory<T>`/`EngineeringObjectBase`/
`EngineeringObjectRehydrationService`, `Tempest.Core.Evidence.EvidenceService`,
`Tempest.Core.ExportImport.IExportSchemaMigration`,
`Tempest.Core.EngineeringDomain.BomUnitsOfMeasure`

**In plain terms.** Not every release adds something you can point at on
screen. This one fixes things that were quietly wrong underneath the
screen: a change to a requirement that other open windows never found
out about; two parts or documents in the same project secretly allowed
to share one name; a startup sequence that could not yet say what
exists without first doing all the slow work of rebuilding it; a formal
"issue" of engineering evidence to a client that could, on a bad night,
half-happen. None of it is a feature you would ask for by name. Left
alone, each becomes a support call or a mistaken drawing months later.
This chapter is a release built entirely from closing named, proven
gaps — and being honest about the two it could not finish in one night.

## One instruction, and what "candidate" means here

At the Product Owner's instruction — close the P1–P3 technical debt
before the release rather than after it — the lead cut `v0.20.0` from the `v0.19.1` candidate and
worked through the backlog the previous night's own
`Technical Debt Rationalisation — Part 1.md` had just rated. Nothing
here is released or merged to `main`: `v0.20.0` is a release candidate
on `release/v0.20.0`, under the Product Owner's own manual test. Other
packages from the same tranche have their own chapters; this one covers
the five that are debt closures pure and simple.

## Requirements finally reach the change bus

`61-the-screen-follows-the-store.md` covers the platform's change feed:
every engineering write commits through
`EngineeringDomainContext.ExecuteWriteAsync`, the one place that
publishes a `WorkspaceChange` once a commit lands. `RequirementsService`
was never wired through it — its fourteen mutating methods wrote
straight to the document and persistence stores, so a docked
Requirements Explorer or Cockpit went stale until a user navigated away
and back. `BACKLOG.md`'s audit named this `TD-28` and corrected its own
earlier framing: not a bulk-command defect, but every Requirements
write, single or bulk.

`WP 20.1A1` (`8c13eff`) makes each method commit through
`IQueryablePersistenceStore.ExecuteInTransactionAsync` — the only
operation that advances the store's sequence counter — then publish:

```csharp
private void PublishChange(Guid objectId, string kind, WorkspaceChangeType changeType) =>
    _workspaceChanges?.Publish(new WorkspaceChange(
        _transactionalStore.CurrentSequence,
        [new WorkspaceChangeEntry(objectId, kind, changeType)]));
```

A side benefit fell out rather than being planned: moving a requirement
to a group, and creating a collection or a group, were each two
separate, non-transactional writes. Needing a valid sequence number at
all meant wrapping both in one transaction, so those pairs became single,
indivisible commits as a consequence of the fix, not its purpose.

New tests (`65f1185`) prove the publish per mutator and that a
refused write publishes nothing; a `Tempest.Desktop.Tests` integration
test proves a docked subscriber sees a real write through the real
`WorkspaceHost`. `BACKLOG.md` closes `TD-28` directly, the last of three
rows once carried as "judgement calls".

## A name that cannot collide, within a project

`64-independent-check-and-the-issue-sheet.md` left `TD-38` open, owned
but untouched: nothing stopped two Parts, Calculations or Documents in
the same project sharing one identifier. `WP 20.1A2` (`5d10f79`) closes
it with no new stored field. `IEngineeringObject.BusinessIdentifier` is
a read-only projection — a Part's or Calculation's own name, a
Document's number if it has one else its name, unchanged for
Requirement, which already had its own correct index.

`IBusinessIdentifierIndex` is the in-memory claim table behind the rule,
maintained by `EngineeringObjectFactory<T>.CreateAsync` at creation and
`EngineeringObjectBase.RenameAsync` at rename, both under the domain
write lock `54-one-transaction-per-engineering-change.md` and
`ADR-0145` already established. A duplicate is refused, naming the
clash: *"A Part named 'Bracket' already exists in project P-001 (id
…)."* The same identifier in a different project is accepted; a
soft-deleted holder's claim counts as free; the whole index is rebuilt
after rehydration. A Ribbon create of a duplicate Part now fails on the
status bar, not with an unhandled exception (`7adaf09`).

The scope is deliberately narrow. The first pass enforced all eight
Mechanical Kinds; the full gate then found two real collisions —
fixtures that create several Projects sharing the default name
"Quotation Test Project", and an adversarial test that renames four
Parts to the identical literal "Renamed" concurrently. `e3ed603`
narrowed enforcement to Part alone from Mechanical. Only the Kinds a
person actually creates by hand — Part, Calculation, Document,
Manufacturing, Verification, Evidence — carry the rule.

The lead's own pass (`a330024`) closed a second, disclosed gap: the five
discipline rename command handlers still let
`DuplicateBusinessIdentifierException` escape unhandled rather than
returning it as a refusal. Each gained the same two-line catch, pinned
by a test through the real command handler — the difference between a
status-bar message and a crash.

## Index first, not yet lazy — and saying so

`WP 20.1C2` (`278b291`) targets `TD-88`: startup rehydration reads and
reconstructs the whole persisted estate eagerly, every time. What
shipped is a first stage only. `EngineeringObjectRehydrationService.RehydrateAsync`
now builds a deterministic `EngineeringObjectIndexEntry` — id, Kind,
identifier, display name, parent, status, deleted flag — for every
persisted object straight from its state record, and raises a new
`IndexBuilt` event with the whole list *before* a single document is
read or object materialised. That is the seam `WP 20.1A2`'s own
identifier-index rebuild runs from.

Full materialisation itself stays eager, and the commit says so rather
than implying otherwise: about seventy existing callers —
`EngineeringCockpit.PrimeAsync`, `InvoicingService.ListCarriedSourcesAsync`,
`MechanicalPropertyFacetProvider.GetBaselineDisplayAsync` among them —
read full, type-specific object state straight off list results with no
intervening `FindAsync`, all outside this Work Package's own files.
Deferring materialisation behind those callers could not be proven
behaviourally equivalent overnight, so `TD-88` stays open. A measured
1,000-object, ten-project estate showed no material wall-clock change
either way — the Release Notes record roughly 185 ms, the Execution
Plan roughly 190 ms, "before and after", because the still-eager loop
dominates the cost regardless of the index stage in front of it.

This is the Execution Plan's own "kill switch": stop rather than claim a result
the evidence does not support. The lead's own pass then restored a
closing brace lost during the merge (`d4b106a8`) — a small reminder that
a tranche worked across several worktrees in one night still needs a
human reading the merged result, not just a green gate.

## Never falsely Issued

`64-independent-check-and-the-issue-sheet.md` disclosed a genuine
hazard: issuing evidence committed the issue record first, with no
sheet attached, the sheet's bytes second, and the record's pointer to it
third — a crash between the first two left an Issued record with no
sheet, recoverable only by re-issuing, which was not even offered.
`WP 20.3A` (`ce246c2`) closes the dangerous half: `IEvidenceService.IssueAsync`
now takes a callback that renders and attaches the sheet *after* every
refusal check but *before* the one remaining write, folding its
attachment id straight into the same `IssueRecord` that commit writes.
A fault attaching the sheet now leaves the evidence merely Checked,
never falsely Issued. It is still two commits, not one — folding the
sheet's bytes into the same transaction needs a private mutator surface
widened, outside this package's files — and the commit says so.

The same package closes two older, disclosed ADR gaps. `ADR-0051`'s own
Negative consequences named strict schema-version equality with no
upgrade path; `IExportSchemaMigration` (`7ef7826`) now walks a section
forward one registered migration at a time before that equality check
runs, refusing — naming the artefact's own original version — the
moment a step is missing. `ADR-0083`'s own Negative consequences named a
BOM line's unit of measure as an unvalidated string; `BomUnitsOfMeasure`
(`3597233`) is the small closed vocabulary that ADR disclosed and never
built — twelve symbols (`EA`, `SET`, `PR`, `BOX`, `ROLL`, `SHT`, `M`,
`MM`, `KG`, `G`, `L`, `HR`), each with a short alias list, checked on
write and read leniently on the way back — `"EA"`, `"ea"` and `"Each"`
are one unit whichever a record happens to carry.

## Seven small closures, one of them a defect that was not there

`WP 20.3B` closed seven P3 rows: `TD-20` gave reference-data lookups a
single-revision read instead of walking a document's whole history;
`TD-21` threaded a `CancellationToken` through
`ICalculationDefinition.Calculate`; `TD-03` made `TempestServiceProvider`
dispose every reflection-constructed singleton, in reverse order;
`TD-05` proved, by scanning every concrete module type this platform
ships, that all thirty-two already carry `[ModuleMetadata]`; `TD-170`
stopped a failed compensating withdrawal being collapsed into an
ordinary "not deletable" message; `TD-84` was honestly re-scoped from a
four-row grouping down to the one row (`TD-79`) still actually open.

`TD-18` deserves a sentence for what it is *not*: a parallel test fired
twenty sources linking to one shared object, and that object linking
back to all twenty at once, checking for a lost link, a duplicate, or
two racing writers confusing a reciprocal pair. It found nothing wrong —
the domain write lock already serialised every write correctly.
**"No defect found" is a real, positive result**, not a gap left
untested because nothing broke.

## The gate, and what it caught overnight

The candidate's gate, re-run at `WP 20.9.0` on the merged head, moved
from `v0.19.1`'s 4,455 Core tests to 4,666, and from 628 Desktop tests
to 660 — the tranche's new coverage in one figure. The sharded
CI run on the pre-fix tree (`94cbb5ba`) failed twice, both named rather
than waved past: the governance health check caught `ADR-0153`'s own
register row missing from `ADR Register.md`, and the Debug Desktop shard
hit a genuine race in `ProjectAreaAcceptanceTests` — its own assertion
running before an already-in-flight "opened" continuation had finished.
`WP 20.9.0` (`4d06cf1`) fixed both on the candidate head; the affected
Release leg was re-run (660 of 660), while Core, untouched, needed none.

Two warnings carry forward from this chapter, stated rather than
implied: business identifier uniqueness is enforced only for the Kinds a
person actually creates by hand, not every Mechanical Kind a factory can
construct; and rehydration is still eager in full — the index stage
exists, but nothing yet defers materialising every object at startup.

## What to take away

- **A release can add no visible feature and still be the most valuable
  one this month, when every line in it closes a gap a gate can prove
  closed.** Nobody will screenshot a change-bus publish or a rebuilt
  brace; the value is that a named risk stopped being a risk.
- **A "kill switch" invoked honestly — shipping the safe half and
  leaving the debt row open — is worth more than a claimed fix that
  quietly does not hold**, exactly as `TD-88`'s own measurement and
  `TD-38`'s own narrowed scope both say plainly what they did not do.
- **"No defect found" is a real result, not a null one.** `TD-18`'s
  concurrency battery cost real effort and answered a real question;
  recording "the lock already handled this" is as valuable as recording
  a bug it found.
