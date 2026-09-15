# Source Citations and Supersession

**Release:** `v0.18.0` · **Work Package(s):** `WP 18.0B`, `WP 18.0B-R1` ·
**Decision:** `ADR-0149` · **Debt:** `TD-157`, `TD-163` ·
**Code:** `Tempest.Core.ReferenceData` (`SourceCitation`, `ReferencePin`),
`Tempest.Core.ReferenceData.Seeding`

**In plain terms.** When an engineer signs off a calculation, they are
trusting numbers they did not measure themselves — a material's
strength, a bolt's size, a physical constant — and those numbers came
from somewhere: a datasheet, a standard, a handbook. If nobody can say
exactly which page of which document a number came from, the calculation
is only as sound as someone's memory. So a reference number in TempestOS
can carry an exact address: publisher, document, edition, table, row.
And when that document is later revised, "supersession" is the promise
that the old calculation can still point at precisely the page it
actually relied on, even though a newer edition now exists.

## An address, not just a value

`51-engineering-reference-data.md` already covers `ReferenceProvenance` —
the governance question every record answers before it may be trusted at
all: which organisation supplied this, has anyone checked it. `ADR-0149`
adds a sharper, optional question on top: point at the exact line.
`SourceCitation` is a small record —

```csharp
public sealed record SourceCitation(
    string Publisher,
    string Work,
    string? Edition = null,
    string? Page = null,
    string? TableOrFigure = null,
    string? RowOrEntry = null)
```

— `Publisher` and `Work` required (a citation naming neither points at
nothing), the rest left `null` wherever the source itself does not state
one; nothing is guessed to fill a gap. Its `ToString()` renders as one
issue-sheet line — every absent part simply omitted, never printed as a
blank. `Source` is optional on every record across the five governed
libraries — Materials, Fasteners, Bearings, Standards, Constants —
persisted alongside a record's `Definition` and `Provenance`.

## Carried forward, not wiped, on revise

A citation has to survive the routine act of revising a record. `WP
18.0B` (commit `55e6ba8`) adds a citation-aware overload of `RegisterAsync`
and `ReviseAsync` alongside the pre-existing ones, and draws a careful
line: the *old* `ReviseAsync` overload — the one `ReferenceReviewService.
VerifyAsync`/`ReleaseAsync` already call on every verify and release —
says nothing about a citation, and that means "leave it exactly as it
is," not "wipe it to null." Only the *new* overload may change or
withdraw one, and passing `null` through it is a deliberate withdrawal,
distinct from a caller that never mentioned a citation at all.

A test proves this over a real SQLite database and a host restart, not an
in-memory shortcut —
`SourceCitationTests.RegisterWithCitation_ReviseThroughTheCitationUnawareOverload_CarriesTheCitationForward`
registers a material with a citation, revises it through the old
overload, restarts the host, and reads both revisions back identically
cited. That is the path a real release takes: a citation set at
registration must still be there once the record is Released and cited
from evidence.

## Proving supersession, not assuming it

Nothing in a reference library is ever deleted or overwritten. A revise
writes a new revision; a supersession writes a new revision of the
superseded record's own state, naming its replacement. That was already
the design, but nobody had tested it under an arbitrary sequence of the
things a record's lifetime can throw at it: revise, advance a lifecycle
stage, retreat one, supersede, in any order, any number of times.

`WP 18.0B` (commit `891e02b`) closes that with a **property-based test**.
Where an ordinary test checks one or two hand-picked scenarios, a
property test states a rule that must hold for *any* sequence of actions,
then has the computer generate a hundred different random sequences and
check the rule against every one. The rule here is the supersession
invariant: whatever a record goes through, **every `ReferencePin` ever
minted must still resolve to the exact content it pinned**, and a
superseded record must still report itself as `Superseded` rather than
vanishing:

```csharp
// The invariant: every pin ever minted still resolves to its exact
// content, whatever happened afterwards.
foreach (var (revision, expectedJson) in pins)
{
    var atRevision = await catalog.GetRevisionAsync("primary", revision);
    Assert.Equal(expectedJson, Json(atRevision.Definition));
}
```

It runs against all **five real libraries**, not one invented stand-in,
because the invariant belongs to how the catalogue behaves with each
library's own real definitions. Building it surfaced a real trap: two
definition types hold a `Dictionary`-typed property, and a C# record's
automatic equality compares a dictionary by *reference* — so the test
compares both sides as JSON instead, which is what the storage contract
actually promises.

## The plan said 79, the truth was 41

The release plan originally stated that the Libraries tab would list "the
79 seeded reference records." Once `WP 18.0B` actually gave every record
naming a real source its citation, the true count was 41 — Materials 6,
Fasteners 7, Bearings 2, Standards 14, Constants 12.

The honest move, taken in commit `46a1530`, was to correct the plan
itself rather than quietly ship 41 under a document still claiming 79 —
its own words: *"the 41 seeded records (materials 6, fasteners 7,
bearings 2, standards 14, constants 12; '79' was a stale estimate
corrected by `WP 18.0B`)."*

A plan is written before the work is done, so it is allowed to be wrong;
what is not allowed is leaving a wrong number standing once the truth is
known. `WP 18.0B` also pinned the corrected per-library counts as a test
(commit `fe60d00`), so a future change to who gets cited shows up as a
deliberate, visible change to an assertion — not silent drift.

## The review found: an empty Libraries tab

`WP 18.0B` populated all 41 records in the *seed datasets*, but only
`MaterialSeed` reached a shipped call site
(`BracketCalculationWorkbench.PopulateMaterialLibraryAsync`). A real
launch opened the Libraries tab to one populated library out of five.

The fix, `WP 18.0B-R1` (commit `588da9c`), changes *how* seeding runs at
start-up. `ApplyAsync` is deliberately additive per record — right for a
library a person has already worked in, wrong for an unattended start-up
phase, where the question is not "does this record exist" but "has
anyone touched this library at all." `ApplyIfEmptyAsync` asks that
coarser question once, for the whole library, and
`EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync` — the one
composition-root sequence both `Tempest.Desktop` and `Tempest.Harness`
run at start — now calls it for all five libraries before rehydrating
engineering objects. `StartupReferenceLibrarySeedingTests` (commit
`f456c1f`) proves it: a fresh host seeds all 41 records, Draft, with
their citations; a second start adds none; a root where a material was
registered by hand first leaves that library alone while the other four
still seed in full.

`TD-163` is marked closed with evidence rather than simply deleted
(commit `e3afe4a`) — the entry records the gap, the fix and the test that
proves it, so the closure itself is traceable.

## The honest gap: a warning that cannot fire

Not everything `ADR-0149` describes shipped complete, and `v0.18.0` says
so itself. A `ReferencePin` names a library, a record and a revision; if
that record is later superseded, something ought to warn that the source
cited has moved on. The mechanism exists — `IReferencePinResolver`, which
answers "what is this pin's record's current state" without its caller
needing to depend on every library it might have cited — but nothing in
the running product ever registers one. `TempestHost` never wires a
resolver into the container, so the two consumers built to accept one
always resolve an empty dictionary. This is `TD-157`: **the "pinned
source superseded" warning can never fire, because the resolver behind it
is never connected.**

This is not a defect in the pin itself — a pin stays exactly valid and
readable regardless, the invariant the property test above proves. What
is missing is only the notification on top. The Release Notes say so
under Warnings: *"A cited record that is later superseded is not yet
flagged on the evidence (`TD-157`)… the 'superseded' warning is a
`v0.19.0` item."* `BACKLOG.md` records the same fact against the row,
verified against the shipped tree rather than assumed from the title.

## What evidence does with a citation

Evidence — described in `59-evidence.md` — is the actual consumer this
citation mechanism exists for. `WP 18.0A` (Evidence) and `WP 18.0B`
(citation) were built as parallel Work Packages, and `WP 18.0A`'s own
citation type shipped with nothing yet to fill its citation-shaped field.
Commit `57e1dd2` is the join: `EvidenceService.CiteAsync` now copies the
cited record's `SourceCitation.ToString()` onto
`EvidenceCitation.SourceCitationSnapshot`, so an issue sheet can print the
citation line without a second lookup. How that snapshot appears on the
printed sheet, and in the Libraries tab it is picked from, belongs to
`59-evidence.md` and `63-the-evidence-workspace.md`, not here.

## What was deliberately not built

`ADR-0149` was originally reserved for interpolating between reference
values and structured "value bands" for use inside a calculation. `D-028`
withdrew in-app calculation from `v1.0` — an engineer does the
calculation wherever they already do it, and Tempest records the result
as evidence — so nothing in this release would ever read an interpolated
value. The ADR number was re-scoped to what the product actually consumes
today, and interpolation is deferred to whenever, if ever, a real
consumer needs its own decision.

## What to take away

- **A number is only as trustworthy as the address it can point back
  to.** `SourceCitation` exists because "verified" and "traceable to an
  exact page" are different claims, and only one can be checked by a
  stranger holding the same document.
- **An invariant worth relying on is worth generating hundreds of random
  cases against, not just the two or three you thought of.** The
  supersession property test found nothing broken; its value was turning
  "we believe this holds" into "this has been checked under cases
  nobody hand-picked."
- **Correcting a stale plan in public is not a failure to hide; it is the
  job.** Fixing "79" to "41" in the same breath as shipping the feature
  is what keeps a number anyone might later cite from quietly becoming
  folklore.
