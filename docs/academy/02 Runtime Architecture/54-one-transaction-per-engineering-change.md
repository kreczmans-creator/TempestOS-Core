# One Transaction per Engineering Change

**Release:** `v0.17.0` · **Work Package(s):** `WP 17.1B` ·
**Debt:** `TD-135`, `TD-136`, `TD-140`–`TD-150` (`TD-147` resolved,
Release Blocking; `TD-142`, `TD-144`–`TD-146`, `TD-148` resolved;
`TD-141`, `TD-158`, `TD-170` deferred, see below) ·
**Decision:** `ADR-0145` ·
**Code:** `Tempest.Core.EngineeringDomain.EngineeringDomainContext`,
`EngineeringObjectBase`, `EngineeringObjectFactory<T>`,
`Tempest.Core.Persistence.IQueryablePersistenceStore`

**In plain terms.** When you save an engineering change in TempestOS —
rename a part, revise a drawing, attach a PDF, delete an item — that one
action is really several separate pieces of writing under the bonnet.
This release made TempestOS guarantee that all of those pieces land
together or none of them land at all. Before it, a change could
half-succeed: the software could tell you a save had failed while
quietly keeping part of it anyway. One save is now one indivisible unit
— "it failed" and "it happened" can no longer both be true of the same
action.

## A transaction, in one example

Transferring £50 between two bank accounts is really two writes: subtract
£50 from account A, add £50 to account B. If the bank's computer did the
first and crashed before the second, £50 would simply vanish from the
ledger — not stolen, not refunded, gone. No bank allows that. The two
writes are grouped into one **transaction**: a unit of work the system
guarantees either completes in full or leaves no trace of having started.

An engineering change is the same shape, with more pieces: the object's
own state (its name, its status); its document revision (the durable
history entry the change creates); its relationships (what it is grouped
under, what refers to it); its attachment bytes, where relevant — how
those are stored at all is `37-attachment-content-storage.md`'s own
subject; and an audit row recording who changed what and when. `WP
17.1B` made all of that one transaction, exactly as a transfer is one
over two balances.

## Five writers, and nothing holding them together

Before this Work Package, an engineering object's truth lived in up to
five places at once: its own in-memory fields, its state record, its
document and revision records, its relationship cache, and its
attachment bytes. Nothing spanned them, because — as
`53-sqlite-persistence.md` covers — the platform did not yet have a
transaction capable of spanning them. Every mutator wrote memory first,
then disk, hoping nothing failed in between.

Something eventually did, and it is the only row `BACKLOG.md`'s history
records as having been marked **Release Blocking**:

> `TD-147` — an object creation whose initial durable write failed still
> registered the object in memory, so its next successful write made a
> reported failure real.

`EngineeringObjectFactory<T>.CreateAsync` put a new object into the
in-memory repository — findable by the rest of the platform — *before*
writing its first state record to disk. If that write then failed, the
caller was told creation had failed, but the object was already sitting
in memory, live and mutable; its next successful write of anything at
all made the "failed" creation durably real. It was the sharpest case of
a wider family `v0.16.0` had already disclosed — `TD-135`, `TD-136` and
`TD-140` through `TD-150`, all the same defect wearing different hats:
**more than one writer, and no boundary around them.**

## A cure that could only treat the symptom

`v0.16.0`'s own attempt at this problem was not wrong, only insufficient.
It built roughly 1,802 lines of compensation: rollback-on-failure and a
mutation rollback point (`RollBackOnFailureAsync`, `MutationRollbackPoint`),
to snapshot an object's fields and restore them if a later write threw;
and, for the hardest case — attachment bytes on the filesystem — a
write-intent store marking a payload "being written" before its bytes
landed, plus a reconciliation sweep collecting whatever that marker could
not account for afterwards. None of it could fix `TD-147`: restoring an
in-memory field cannot unwrite a record already committed to disk. It
could only shrink the gap between memory and disk, never close it — and
the reconciliation sweep once nearly deleted live content while doing its
job, told in full in
`05 Case Studies/06-the-attachment-sweep-that-could-delete-live-content.md`.
`WP 17.1B` deletes every line of it, named individually in `ADR-0145`'s
Consequences, rather than keeping it beside the real fix.

## The decision: project, commit, apply

`ADR-0145` makes one store authoritative and gives every engineering
change exactly one way to happen. `EngineeringDomainContext.ExecuteWriteAsync`
takes the platform's one domain-wide write lock, opens one transaction on
the persistence store, and every mutator — rename, revise, link, attach,
move, delete — and both factories run their change through it in three
ordered steps: **project** the next state from the current one (this may
throw to refuse, and nothing has been touched yet, so a refusal simply is
not an operation); **commit** the projected state, everything that goes
with it, and the audit row, through that one transaction; then, only
after the commit returns and only while still holding the lock, **apply**
the change to the object's own fields and the in-memory caches.

```csharp
await _domainWriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
try
{
    await PersistenceStore.ExecuteInTransactionAsync(work, cancellationToken).ConfigureAwait(false);

    // Committed. Memory is updated here, before the lock is
    // released, so the next writer projects from it.
    afterCommit?.Invoke();
}
finally
{
    _domainWriteLock.Release();
}
```

`EngineeringObjectFactory<T>.CreateAsync` now calls `Register` in exactly
that position, which is `TD-147` closed at its cause; `DeleteAsync`
likewise reads which attachments to release from the state it is about
to commit, not what it knew before taking the lock (`08731fb`).
`InMemoryEngineeringObjectRepository` and its relationship counterpart
stop being co-equal writers and become caches, rebuilt from what has
committed (`34-engineering-object-rehydration.md`). Even
`InMemoryEngineeringDocumentStore` — a second document-store kept only so
tests could dodge a filesystem — is deleted outright (`80bc767`): keeping
it meant a fourth writer, reintroduced purely to test a design whose
point is that there are no longer four; every sample module's test rig
now registers one store under all three shapes the domain needs
(`80bc767`, `8a1c407`). And the split runs through `EngineeringObjectBase`
itself (`be6a387`): it fell from 1,819 lines to 817 (821 by the Work
Package's end), plus a new 203-line `EngineeringObjectBase.State.cs` —
the mutators decide *when* a change becomes durable, the state file
*what* it is, different reasons to change, so now different files.

The same commit also advances the persistence store's own sequence
counter, still inside the transaction, which is later how the desktop
screen learns a change has landed without polling for it — a forward
reference only, to `61-the-screen-follows-the-store.md`.

## One lock for the whole domain, and why

The transaction opens under a single `SemaphoreSlim` shared by every
engineering write in the process — not one per project, not one per
object. `ADR-0145` states the reasoning rather than leaving it to look
like an oversight: TempestOS is a single-user desktop system of record,
one person, one process, saving actions seconds apart. The graph checks a
mutation runs — no parent cycle, no delete while children are live — each
read an unbounded slice of the object graph, which fine-grained locking
would need an order-sensitive discipline over to stay correct. One lock
is the honest shape for this product, and the first thing to revisit
should TempestOS ever become multi-user.

## Two defects found in the Work Package's own first attempt

The first attempt shipped a real bug, caught before release. Applying
memory happened *after* `ExecuteWriteAsync` returned — after the lock had
already been released. Since a mutator projects its next state from the
object's own fields, that reopened a window where a second writer could
take the lock, read a field the first write had committed but not yet
applied, and commit over it — durably losing the first writer's change:
the same lost-update bug `WP 16.4B-R3` had already closed once,
reintroduced by the new write path. `9b1cfcb` fixes it by moving
`afterCommit` inside the lock hold, so commit and apply are one critical
section, not two steps with a gap between.

The second defect lived in the tests built to prove the design.
`GatedPersistenceStore` — the rig used to pause a transaction mid-flight
so a second writer's behaviour could be observed without relying on
timing — took its release signal out of a field the moment a transaction
parked, so `Release()` found nothing there and did nothing; every test
using the gate simply hung until it timed out. `be10ddb` leaves the
completion source in place instead, so `Release()` works whether it runs
before or after the park.

## Adversarial rigs: testing built to break the design

An **adversarial test rig** is not written to show a feature works; it is
written by someone trying to prove it does not, attacking the places a
design's own author is least likely to look. `WP 17.1B` rebuilds three —
`MutatorRefusalAdversarialTests`, `AttachmentRevisionAtomicityTests` and
`RevisionAttachmentInterleavingTests` — on the new transaction (`bb75f4a`).
Their own remarks say the intent plainly: they exist "to falsify
`ADR-0145`, not to demonstrate it." Each keeps two kinds of fact distinct
— a **guard-rail**, which must stay true for ever, and a
**characterisation**, which names a known defect until fixed and is then
*inverted* rather than deleted, so the suite proving a defect is gone
still records what it once was. Nine facts were inverted this way,
closing `TD-141`, `TD-142`, `TD-145` and `TD-146` in place.
`GatedPersistenceStore` parks one writer at the very end of its
transaction — everything staged, both locks held — so a second writer's
wait is provable, not merely likely.

## The unplanned neighbour: letting work finish before shutdown

Making the persistence store disposable (`WP 17.1A`) had a side effect
nobody had asked this Work Package to fix: a command triggered from a
button could still be running its cockpit-refresh tail after the
platform started disposing its store, throwing `ObjectDisposedException`
into work that should have finished cleanly. `88f36de` gives
`ICommandRegistry` an `InFlightInvocations` count and a
`WhenIdleAsync(timeout)`, and has `WorkspaceManager.DisposeAsync` wait up
to ten seconds for it before disposing the host. `a7c33cf` closes a gap a
day later: the count only began at dispatch, after a prompt was answered,
so a caller checking idleness while a prompt sat open saw nothing in
flight. Neither commit carries a `WP 17.1B` tag, but both exist because
this Work Package made the store something that could be pulled out from
under running work.

## What was deliberately not built

`EngineeringObjectRehydrationService` stays **eager** — reading every
state record in one call at startup — rather than becoming the lazy,
per-kind warm-up the Work Package's own row once proposed: a lazy cache
with two population paths is a second consistency problem, and this Work
Package exists to remove one, not add one back. The three separate
verification models the platform still carries were not collapsed here
either — `ADR-0145` records that as an unrelated change, deliberately
deferred to `WP 18.2B` rather than folded into a substrate change already
large enough to review on its own.

## What to take away

- **A transaction is not extra caution; it is what makes "it failed" and
  "it happened" mutually exclusive.** Without one, a system can only
  shrink the gap between those two claims — with one, the gap does not
  exist.
- **Compensation cannot undo a write that has already landed.** Once a
  record is on disk, resetting in-memory fields cannot reach it; the only
  fix for a structural race is to stop the two writes ever being two.
- **The order of "commit" and "apply" is itself part of the design, not
  an implementation detail** — get it wrong, as this Work Package briefly
  did, and the very race the transaction was built to remove can reappear
  in the seam between committing and remembering.

## Postscript (release candidates, September 2026)

On the unreleased `v0.19.1`/`v0.20.0` candidates, `ADR-0145`'s
one-transaction discipline spreads to every writer this chapter left
outside it. `TD-150` (`WP 19.10J`) and `TD-170` (`WP 20.3B`) close, as
does `TD-18` (`WP 20.3B`, `6f45509`: a twenty-writer battery finds
`LinkAsync` already correct under the write lock). `WP 19.10L`
(`21e81d0`) closes `TD-23`, `TD-32` and `TD-141`:
`VerificationService.RecordAsync` and its links now commit as one
transaction, its `verifiedBy` edge joins `IEngineeringRelationshipRepository`
like any other, and `EngineeringRelationshipFactory.CreateAsync`
refuses a superseded end before writing. `TD-28` (`WP 20.1A1`,
`0a389dc`) brings Requirements onto the change bus this chapter
started. `WP 19.10K` (`78aa625`) extends the same primitive to a
fourth writer family, the reference-data catalogue (`TD-156`,
`TD-158`). None of this has shipped.
