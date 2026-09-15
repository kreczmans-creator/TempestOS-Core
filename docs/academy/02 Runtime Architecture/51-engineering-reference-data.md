# Engineering Reference Data: Seven Libraries, One Catalogue Layer

**Programme:** P01 — Engineering Reference Data (`Group A`), integrated
into this repository at `v0.16.0` · **Work Package(s):** `WP 17.9.2`,
`WP 17.9.3` · **Decision:** `ADR-0124`, `ADR-0126`, `ADR-0143` (amended) ·
**Code:** `Tempest.Core.ReferenceData` (shared) and the seven libraries —
`Tempest.Core.Materials`, `Standards`, `Fasteners`, `Bearings`,
`Components`, `Constants`, `Manufacturing`

**In plain terms.** Every engineering calculation leans on numbers nobody
in the room measured personally: how strong a grade of steel is, what
size a bolt's thread is, what a physical constant equals, what a
standard's clause says. Typed into a spreadsheet cell, there would be no
way to tell a figure an engineer half-remembered from one traced to a
checked document — and a bracket is only as trustworthy as the numbers
holding it up. So TempestOS treats this data as a product of its own:
every fact gets a paper trail and passes a review before an engineer may
design against it. This chapter is the one shelf all seven kinds of that
data sit on, and the rules — Draft, checked, released — every one obeys
first.

## Reference data is evidence, not a lookup table

Get a material's yield strength wrong and every calculation built on it
is wrong, silently, because the arithmetic was correct — it was the
number fed to it that lied. So the product does not let a value simply
exist: every record carries **provenance** (where it came from) and a
**lifecycle** it must climb before anything may depend on it. A figure
typed from memory and one transcribed from a certified datasheet are
stored identically at first — both unverified — and nothing lets either
masquerade as more trustworthy until a named person has checked it.

## One programme, seven libraries, one shared layer

`Group A` (programme `P01`) is seven reference libraries: Materials,
Standards, Fasteners, Bearings, Mechanical Components, Engineering
Constants and Manufacturing Processes. `A4`, the Bearing Library, was
built first, and in building it, built far more than bearings: a
provenance record, a Draft → Checked → Validated → Released → Superseded
lifecycle with provenance gates, a catalogue with a typed index,
per-record write locking, a sourced-value shape, and a comparison result
distinguishing "not applicable" from "not recorded".

`ADR-0126` names the risk of repeating that six more times plainly: it
"would be duplicated infrastructure of exactly the kind this platform's
own charter prohibits, and would guarantee that the seven copies
diverged." The opposite mistake — one generic `EngineeringReferenceData`
class with a nullable field for everything any library might ever want —
was rejected too, because it "would fit no domain, make every domain rule
a runtime nullability check … " So `Tempest.Core.ReferenceData` holds only
what is genuinely identical in every domain — "where did this come from,
has a person checked it, and may engineering work rely on it?" — and each
library keeps everything genuinely engineering: `Fasteners` owns thread
form, `Bearings` owns contact angle, `Constants` owns uncertainty, and
none owes the others an explanation for it.

**Where this history lives.** `A4`'s code and the shared layer both first
appear in this repository in a single commit, `3e60019` (2026-09-07),
folding already-built work into the tree as part of the `v0.16.0`
integration. The programme's day-by-day commit history — how `A1`
through `A7` were actually built — is not in this repository; what is
recorded here begins at integration.

**Where Materials came from.** `Tempest.Core.Materials` predates `Group
A`: it shipped at `WP 7.1C` (`v0.7.0`, `ADR-0055`) with its own
provenance and exception types but no lifecycle, supersession, query or
validation. The uplift kept the document `Kind` unchanged, so older
records are still this library's own, and replaced what had been
quietly duplicating what `A4` had just built: provenance and exceptions
moved onto the shared types, and the open-string material category
became the closed `MaterialFamily` enum, because `A4`'s applicability
mechanism needs something closed to key off. `Group A` is one library
generalised outward, not seven built from nothing.

## What every library shares

- **Provenance** — `ReferenceProvenance`, `ReferenceExtractionMethod`,
  `ReferenceVerificationStatus`. A field a source did not supply stays
  `null`; nothing is inferred to fill a gap.
- **Lifecycle** — `ReferenceValidationState`/`ReferenceValidationStates`:
  `Draft ⇄ Checked ⇄ Validated → Released → Superseded`. Down-transitions
  are deliberate, so a check that finds a defect can send a record back;
  `Released` is terminal but for supersession, because downstream work
  has already consumed it.
- **The catalogue** — `IReferenceDataCatalog<T>`/`ReferenceDataCatalog<T>`:
  register, find, list, revise, change state, supersede, read history,
  read a past revision. A thin, typed index over the platform's document
  store, the pattern `ADR-0055` set for Materials — `A4`'s third
  repetition of it is what justified extracting it.
- **The record split** — `IReferenceRecord<T>` separates a record's own
  description (a plain type, no base class) from the governance around
  it: its Id, provenance, lifecycle state, and the document revision it
  is backed by.
- **Sourced values and comparison** — `ReferenceValue<T>`,
  `ReferenceRange<T>`, `ReferenceQuantityValue` (for Materials and
  Constants, whose values have no fixed dimension) and `ReferenceComparer`,
  which reports a cell Recorded, NotRecorded or NotApplicable, never one
  collapsed blank.
- **`ReferencePin`** — an exact, permanent identification of one record at
  one revision, taken from the record actually read, never from a caller:

  ```csharp
  public static ReferencePin For<TDefinition>(string library, IReferenceRecord<TDefinition> record)
      where TDefinition : class
  {
      ArgumentNullException.ThrowIfNull(record);
      return new ReferencePin(library, record.Id, record.RevisionNumber);
  }
  ```

  "A pin is a statement about the past" — a caller guessing the revision
  would defeat the point of having one.

## Bearings kept its own storage, and its own applicability rule

`ADR-0124` records why `A4` did not copy Materials' storage pattern
wholesale. Materials needed a codec because its properties are boxed
`Quantity<T>` values plain JSON cannot rebuild (`ADR-0055`); a bearing's
values are all declared at a fixed dimension, so `BearingDefinition` and
its roughly fifteen nested types simply serialise as themselves, enums as
strings so a reordered member can never silently reinterpret an old
record — copying the codec pattern regardless "would add ~15 hand-written
types … whose only novel contribution is the opportunity to drift." The
same ADR decides which properties apply to which family — a contact
angle means nothing on a deep-groove ball bearing — through a
`BearingFamilyTraits` lookup table, not a class per family, so adding a
family is an additive row, not a change to every switch on a base type.
`A4`'s own twenty-two validation rules were, deliberately, **not** copied
into the other six libraries.

## Two narrow seams, and seeding that stays Draft

A fastener names a material; a calculation may want a constant. Rather
than one library referencing another directly, the shared layer declares
two interfaces any library can implement and any consumer can call
without knowing which library answers. **`IStandardResolver.ExistsAsync`**
confirms a citation resolves, nothing more — "a citing library has no
business reading a standard's own title, scope or status." **`IReleasedConstantSource.FindReleasedAsync`**
returns a constant only if it exists *and* has been Released, `null` for
both "not found" and "not released", so a consumer cannot mistake one for
the other. Both are optional everywhere: a fastener must be recordable
before the material it names is registered at all.

Every library also ships a `Seeding/Datasets` module and a
`ReferenceSeedService` that registers a dataset through the ordinary
catalogue — "no pipeline, no staging area, no second store." Its remarks
are direct about the limit: "every record lands in Draft … Promotion
needs provenance this service has no standing to write — a named
reviewer and a date — which is exactly the guarantee that a populated
library cannot masquerade as a verified one." A seeded fastener and a
hand-entered one queue for review identically. What the datasets contain,
and the structured citation each value carries, is `v0.18.0` work — see
`60-source-citations-and-supersession.md`.

## Two things the reviews found wrong

The design-freeze review of 2026-09-08 rated one of its fourteen hazards
high specifically because it touched a governance promise:

> **H8/H13 — any signed-in principal can release reference data.** The
> review service checks that someone is signed in, not who … This is the
> one finding in the whole review that touches what the product promises
> about governance, and it should be fixed before `v0.17.0` is verified.

Until then, `ReferenceReviewService` asked only for *a* signed-in
principal before moving a record to Checked or Released — anybody signed
in could release anybody's material. It was fixed the same night, on the
Product Owner's instruction, as `WP 17.9.3` (commit `dfc913c`), and
`ADR-0143` was amended in place to record it:

```csharp
if (_permissions is not null && !_permissions.HasPermission(principal, required))
{
    throw new ReferenceReviewException(
        library, recordId,
        $"principal '{reviewer}' does not hold the '{required.Key}' permission. "
        + "The act is refused, and nothing was recorded.");
}
```

Verifying now needs `reference.verify`, releasing needs
`reference.release`, both checked through the platform's one
`IPermissionEvaluator`. Both sit in `ApplicationPermissions.LocalSession`
today, so — as the release notes put it — "nothing changes in the flow,
but the gate exists" for configuration or a future role model to
withdraw.

The first Windows review, the same day, surfaced a plainer problem: the
only way into the Materials library was the shipped seed corpus, so an
engineer holding a real datasheet had nowhere to put it. `WP 17.9.2` gave
the Engineering Calculations workspace an *Add a Material* action,
`BracketCalculationWorkbench.AddMaterialAsync`, that registers the record
exactly like a seeded one — Draft, released only through the same review.
A blank source organisation or document is refused outright: "a record
that names no source can never be released," with no exception carved
out for convenience.

## What Group A deliberately does not do

None of the seven libraries selects, calculates, prices, judges supplier
capability, or asserts conformity to anything it cites — `A2`'s
`StandardReference` records what a source *cited*, never that the
citation is met. Every library also shipped, at this point, with **no
data at all**: every test fixture uses an unmistakably fictional "FX-"
designation so nothing could pass for real reference data. Populating the
libraries for real is later work — see `60-source-citations-and-supersession.md`.

## What to take away

- **Shared infrastructure earns its extraction the third time it is
  written, not the first.** `Group A` generalised the lifecycle only
  after it had already repeated across Materials, Requirements and `A4` —
  three real instances, not one imagined future.
- **A permission that changes nothing today is not a wasted permission.**
  `reference.release` altered no visible behaviour the day it shipped; it
  turned an unwriteable assumption into a setting a future role model can
  actually withdraw.
- **Governance can be identical everywhere while the engineering it
  governs stays completely different.** Provenance and a lifecycle mean
  the same thing for a bolt thread and a physical constant; the thread
  form and the uncertainty figure do not, and neither was forced to
  pretend otherwise.
