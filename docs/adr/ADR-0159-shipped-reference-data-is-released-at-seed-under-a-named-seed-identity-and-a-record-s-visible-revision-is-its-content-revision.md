# ADR-0159: Shipped Reference Data Is Released at Seed Under a Named Seed Identity, and a Record's Visible Revision Is Its Content Revision

## Status

Accepted — Product Owner decisions of 2026-10-01 (shipped libraries
"fully usable from day 1, otherwise it's a hard block against v1.0.0";
runbook B2, "a release shouldn't change the revision of the data. It's a
rev 1 item until it's revised"), recorded for `v0.23.0` at the colour
review board's request (M20). Attribution corrected by board finding B3
before the tag.

Amends `ADR-0149` (a reference record's revision semantics). Builds on
`ADR-0126` (the shared `ReferenceDataCatalog<T>`), `ADR-0143` (a
calculation pins the reference revision it stood on) and `ADR-0144`.

## Context

Until `v0.23.0` a seeded reference record always landed `Draft`, and
every calculator reads `Released` records only. A fresh install filled its
libraries and could still calculate nothing: the runbook step that bends a
beam in S355J2 could not be run. Releasing a record requires verified
provenance (a named principal, a recorded date) and the Draft → Checked →
Validated → Released path through `ReferenceReviewService`, with its
permission checks and audit rows.

Separately, every catalogue write — lifecycle moves included — advances
the underlying document's revision number. A record that was added,
verified and released showed the Product Owner "rev 5" for data nobody
had changed.

## Decision

**1. Release at seed is a policy the host carries, not a default of the
seeding mechanism.** `ReferenceSeedReleasePolicy` is registered in
`TempestHost`; `ReferenceSeedService` uses it for datasets marked
`IReferenceSeed<T>.ReleaseAtSeed` (the five shipped libraries: Standards,
Materials, Constants, Fasteners, Bearings). Authored engineering assets
and demonstration content stay `Draft`. Constructed without the policy,
the service seeds `Draft` exactly as before.

**2. It walks the ordinary governed path.** Each record is verified and
released through `ReferenceReviewService` — same provenance check, same
state path, same audit rows — never by writing a validation state or a
verification field directly.

**3. The actor is a named, non-human seed identity.**
`tempest.reference-seed` ("Tempest reference seed (release at seed, PO
decision 2026-10-01)") holds exactly the verify and release permissions.
For the whole of a releasing seed pass — registration, any refresh, the
verify and release — it is the audit actor and the document revision
author as well as the reviewer: the pass opens an async-flow-scoped
override on the host's `CurrentPrincipalAccessor`
(`BeginActingAs`, internal to `Tempest.Core`) and disposes it at the end,
so the person signed in is never recorded as having verified or released
shipped data, and their own concurrent work on another flow is never
attributed to the seed. `ReleaseAsync` is internal; nothing outside
`Tempest.Core` can release a record under this identity.

**4. What it says it checked.** The recorded statement names the cited
source, says the check was against the secondary source as transcribed
at acquisition, not against the primary standard, and says the primary
standard must be consulted before issue. A person who checks a record
against the primary standard supersedes it with their own verified
revision.

**5. Only untouched shipped records are refreshed.** A shipped record
still exactly as an earlier seed left it (Draft, document revision 1,
not verified, same source organisation) is revised to the current
dataset and released (`ReferenceSeedAction.Refreshed`). Anything a person
has revised, checked or released is left alone, and at start-up a library
holding any record the shipped dataset does not name is not touched at
all (`ApplyAtStartupAsync`).

**6. The opt-out is the stricter choice.**
`ReferenceData:ReleaseAtSeed=false` (command line or
`TEMPEST_ReferenceData__ReleaseAtSeed=false`) makes every shipped record
land `Draft` and wait for a person's review.

**7. A record has two revision numbers, with different jobs.**
`RevisionNumber` is the stored version stamp: it advances on every
catalogue write, lifecycle moves included, and it is what a
`ReferencePin` (`ADR-0143`) and audit rows cite. `ContentRevision` is the
revision a person is shown: 1 when registered, advancing only when the
content changes — the definition, the source citation, or the source
identity in the provenance (verification status, reviewer, date and notes
are excluded). Verify, check, validate, release and supersede leave it
unchanged. It is stored on the record (`ReferenceDocumentDto.ContentRevision`);
content written before the field existed derives it from the revision
history, so no migration is needed. This amends `ADR-0149`, under which
the one revision number served both jobs.

## Consequences

- A fresh install calculates on day one; about 276 shipped records are
  released at first launch.
- Audit and revision history of shipped data name `tempest.reference-seed`,
  which is honest about who did the work and cannot be mistaken for a
  person's review.
- Release at seed takes several separate writes per record and does not
  resume: a crash part-way through can leave a record verified but not
  released, which needs a manual review to finish (board M17, open).
- Pins remain exact: they cite `RevisionNumber`, which still identifies
  each stored state. Screens show `ContentRevision`, so a pin's number and
  the "rev" a person sees may differ for the same record.
- Seeding with the policy switched off still attributes registrations to
  whoever is signed in; the seed identity is used only for a releasing
  pass.

## Alternatives Considered

**Seed records directly as `Released`.** Rejected: it bypasses the
provenance rule and the review path every other release obeys.

**Attribute release to the person who first launches the app.** Rejected:
that person did not verify anything, and the rows are permanent.

**Put the seed identity on the process-wide principal session for the
pass.** Rejected in favour of the async-flow-scoped override: a person
pressing "Populate" while working would have had their concurrent writes
attributed to the seed identity for the duration.

**Stop advancing the version stamp on lifecycle moves.** Rejected: pins
and audit rows need every stored state to be addressable.

## Related Documents

- `src/Tempest.Core/ReferenceData/Seeding/ReferenceSeedReleasePolicy.cs`
- `src/Tempest.Core/ReferenceData/Seeding/ReferenceSeedService.cs`
- `src/Tempest.Core/Identity/CurrentPrincipalAccessor.cs`
- `src/Tempest.Core/ReferenceData/ReferenceDataCatalog.cs`
- `docs/adr/ADR-0149-a-reference-record-carries-a-structured-source-citation-and-supersession-keeps-every-pin-valid.md`
- `tests/Tempest.Core.Tests/Population/ReleaseAtSeedAttributionTests.cs`
- `tests/Tempest.Core.Tests/ReferenceData/ReferenceContentRevisionTests.cs`
