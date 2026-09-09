# WP 9.3A — Verification Management Workspace — Technical Debt Assessment

## Purpose

Reviews the Technical Debt Register for items this Work Package's own
implementation created, extended, or should have created and did not.

## New Items

### `TD-32` — `VerificationService.RecordAsync`'s Own Subject→Record Link Is Never Visible via `EngineeringDomainContext.RelationshipRepository`

**What.** `VerificationService.RecordAsync` (`Tempest.Core.Verification`,
`WP 7.1E`, unmodified by this Work Package) links its own
`subjectDocumentId` to the newly-created record via
`IEngineeringDocumentStore.LinkAsync` directly — never through
`IHasRelationships.LinkAsync` on an `EngineeringObjectBase`-derived
Domain object. `EngineeringDomainContext.RelationshipRepository` is
populated only by the latter path (`EngineeringObjectBase.LinkAsync`
calls both `_context.Store.LinkAsync` *and*
`_context.RelationshipRepository.Record`; `VerificationService` calls
only the store method). The consequence: a live Verification Activity's
own `"verifiedBy"`-linked record is durably stored and correctly
readable via the raw `IEngineeringDocumentStore`, but invisible to any
code that queries `RelationshipRepository` for it specifically — unlike
every *other* relationship this Work Package creates (subject→Activity
links, made through real `EngineeringObjectBase.LinkAsync` calls on
Mechanical/Requirements objects), which are fully visible there.

**How it was found.** By a failing test, not by inspection alone: the
first implementation draft of `VerificationRecordReader` mirrored
`CalculationRecordReader` verbatim, reading
`EngineeringDomainContext.RelationshipRepository.GetOutgoingAsync`.
Nine tests failed outright (every test that recorded a result and then
tried to read it back reported zero records). Root-caused directly by
comparing `VerificationService.RecordAsync`'s own source against
`CalculationTemplateRegistry.ExecuteAsync`'s own source — the latter
explicitly calls the Calculation Domain object's own `.LinkAsync()`
*in addition to* `ICalculationEngine.ExecuteAsync` itself (which links
nothing internally); `VerificationService.RecordAsync` has no
Workspace-layer equivalent step, and is itself the one and only place
the link is created.

**Disposition — disclosed, worked around at the read side, not fixed at
the source.** `VerificationRecordReader` reads
`IEngineeringDocumentStore.GetReferencesAsync` directly instead — the
identical raw data `IVerificationService.GetVerificationHistoryAsync`
itself reads internally, just without that method's own permission
gate. This is a **correct, complete fix for every one of this Work
Package's own read paths** — no functional gap remains in the shipped
Workspace. The debt is narrower and more specific: any *future* code
that queries `RelationshipRepository` directly (rather than through
`VerificationRecordReader`, or the raw store) for a Verification
Activity's own record links will silently see none, a genuine trap for
a future maintainer unaware of this asymmetry.

**Why this is debt, not merely a limitation.** `RelationshipRepository`
is presented, platform-wide, as *the* Digital Thread read (`WP 8.2A`'s
own Relationship Catalogue design), and every other Engineering
discipline's own inter-object links populate it faithfully. Verification
Records are the one, narrow exception — a genuine asymmetry between what
the platform's own architecture documents describe and what one real
Framework method actually does, not a defect this Work Package
introduced (the asymmetry has existed, latent and unexercised, since
`WP 7.1E`/`WP 8.2C` — this Work Package is simply the first to build a
Workspace-layer reader that needed to notice it).

**Revisit trigger.** A future Work Package extending
`VerificationService.RecordAsync` itself (a `Tempest.Core.Verification`
change, deliberately out of this Work Package's own "reuse, do not
redesign execution" scope) to additionally call `.LinkAsync()` on the
subject when it is a real `EngineeringObjectBase`-derived object, or any
new Workspace-layer code that queries `RelationshipRepository` for a
`"verifiedBy"` link touching a `VerificationRecord`-Kind document and
needs to be told about this asymmetry before writing that query.

**Disposition.** Open.

## Existing Items Reviewed for Extension or Change

- **`TD-22`/`TD-24`/every prior real-discipline Work Package's own
  equivalent finding** (`ListAllAsync`/list-and-filter reads scale with
  total object count) — the same pattern recurs in
  `VerificationActivityNodeProvider`/`EngineeringCockpit.LiveVerificationActivities`/
  `VerificationKpiCards`. Not separately re-registered; see `WP9.3A
  Security Review Report.md`.
- **`TD-26`** (Runtime Host module-initialisation timing) — unaffected by
  this Work Package; the same test-level `HasRegistered` wait continues
  to be sufficient, confirmed by four consecutive full clean runs with
  zero flakes, including this Work Package's own further cross-module
  dependency edges.
- **`TD-27`** (unspecified `ConcurrentDictionary`/`IPersistenceStore`
  iteration order) — this Work Package's own new node-provider ordering
  (`VerificationActivityNodeProvider`, every category's own children
  sorted by Title via explicit `OrderBy`) was written with `TD-27`'s own
  lesson already in mind — no reliance on iteration order anywhere,
  confirmed by four consecutive full clean runs with zero flakes.
- **`TD-30`** (`ICalculationResult`/`IVerificationResult`/`IApprovalGate`
  family — zero concrete implementations anywhere) — confirmed still
  fully open; "Verification Approval State" is necessarily
  `LifecycleState`-derived (`ADR-0090`), the identical, already-disclosed
  treatment `ADR-0087` established for Calculations. `WP 9.4A`'s own
  Documents↔Verification Digital Thread link, structurally proven but
  previously unpopulated against a real Verification object, is now
  genuinely populated for the first time (the Analysis Activity's own
  recorded result) — a real, disclosed partial resolution of the
  practical consequence `WP 9.4A` named, though `IVerificationResult`
  itself remains formally unimplemented.
- **`TD-31`** (no file/URL attachment storage service) — confirmed still
  open; a piece of Verification Evidence's own `Reference` field is,
  identically to a Document's own External Reference Content field,
  descriptive text only, never a resolvable file — the same, already-
  registered gap, not re-registered here.

## Items Considered and Not Raised

- **Witness information has no dedicated field** — not raised as debt:
  `VerificationEvidenceEntry` (`WP 7.1E`) was designed with exactly two
  fields, and this Work Package's own controlling instruction forbids
  redesigning the Framework; representing witness identity as ordinary
  evidence text is a disclosed, honest use of the existing shape, not a
  gap in it — see `WP9.3A Engineering Review Report.md`'s own Scope
  Discipline Review.
- **No `VerificationSet`-equivalent grouping (mirroring `CalculationSet`)**
  — considered directly and not raised: this Work Package's own scope
  never names a "Verification Set"/"Verification Plan Group" concept,
  and no Domain Kind for one exists; inventing one would be scope
  expansion, not a gap in what was asked for.

## Verdict

**One new item formally registered (`TD-32`)** — a genuine, disclosed
platform characteristic found by a failing test and fully worked around
at the read side, with zero remaining functional gap in this Work
Package's own shipped Workspace. `TD-30`/`TD-31` are both confirmed
still open, `TD-30`'s own practical Documents↔Verification consequence
now partially, genuinely resolved. No existing item's own disposition
worsened; `TD-27`'s own lesson was applied proactively, with zero
recurrence.

**A further, disclosed correction found while adding `TD-32`:** the
register's own "Total tracked" summary line had read "29" (stale since
at least `WP 9.2A`; the row count was already 31 by the time `WP 9.4A`
wrote "29," and `WP 9.4A` itself carried the staleness forward rather
than recomputing it). Re-verified here by a direct, exhaustive count of
every `TD-NN` row actually present in the register (`TD-01` through
`TD-32`, confirmed sequential, no gaps, no duplicates) — corrected to
32, not silently.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md`; `WP9.0A Technical
Debt Assessment.md` (`TD-26`); `WP9.0B Technical Debt Assessment.md`
(`TD-27`); `WP9.1A Technical Debt Assessment.md` (`TD-28`); `WP9.2A
Technical Debt Assessment.md` (`TD-29`, `TD-30`); `WP9.4A Technical Debt
Assessment.md` (`TD-31`); `ADR-0089`; `ADR-0090`; `WP9.3A Future
Capability Assessment.md`.
