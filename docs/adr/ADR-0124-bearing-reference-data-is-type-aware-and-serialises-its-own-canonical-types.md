# ADR-0124: Bearing Reference Data is Type-Aware, and Serialises Its Own Canonical Types

## Status

Accepted — `A4` (Bearing Library, P01 Engineering Reference Data), 2026-09-05.

## Context

`A4` establishes the authoritative bearing reference library: the
structured, traceable bearing data that bearing selection, shaft and
mechanical design, verification, and future engineering-intelligence and
commercial capabilities will all consume.

Three questions arose that the existing decisions do not already settle,
and that a reader of the code would otherwise have to reverse-engineer.

**1. How to model properties that only some bearing families have.** A
nominal contact angle is a defining characteristic of an angular-contact
ball bearing and meaningless for a deep-groove ball bearing. An internal
clearance class is meaningless for a plain bush, which has no rolling
elements. A flat schema would have to carry every family's own properties
on every record, leaving a reader unable to tell "this family has no such
property" from "nobody has recorded this yet" — a distinction that
matters enormously when the record is engineering evidence.

**2. How to store the record.** `ADR-0055` gave Materials a
`MaterialPropertyValueCodec` because a material property's own value is a
boxed `Quantity<TDimension>` whose closed generic `System.Text.Json`
cannot recover. A bearing record's dimensioned values are not boxed —
every one is declared at a statically-known dimension — so the codec's
own reason for existing does not apply here, and the question of whether
to build a parallel DTO graph anyway is a real one.

**3. Whether to extend `Tempest.Core.UnitsAndQuantities`.** Bearing
reference data quotes speed ratings in rev/min and contact angles in
degrees. Neither rotational speed nor plane angle was among the seven
dimensions `ADR-0054` established, and `ADR-0055`'s own Negative
consequences already named that boundary as a known limitation
(`FCR-0034`).

Everything else about A4's own architecture is already decided and is
simply followed, not re-decided here: it is a thin, typed index over
`IEngineeringDocumentStore` with its own `IPersistenceStore` index
(`ADR-0053`, `ADR-0055`, `ADR-0058`); its records are an
`IEngineeringDocumentStore`-backed `Kind` (`ADR-0072`); its relationships
are open-string `DocumentReference`s (`ADR-0073`), and it introduces no
new relationship value of its own — supersession reuses the platform's
existing `supersedes` (`GovernanceRelationshipKinds.Supersedes`), in the
direction `Decision.SupersedesAsync` already established; and its
validation lifecycle is a family-specific specialisation of the canonical
`LifecycleState` vocabulary (`ADR-0074`).

## Decision

**1. Applicability is a property of the family, stated once, in
`BearingFamilyTraits`.** `BearingFamily` is a closed enum (unlike
`IMaterialSpecification.Category`'s deliberately-open string, because a
bearing's family genuinely determines which properties are interpretable),
and `BearingFamilyTraits` answers, per family, whether a contact angle,
rolling elements, internal clearance, a row configuration or a cage is
meaningful. A property not applicable to a family is simply not set on
the record; readers distinguish that from a data gap by consulting the
traits table, which is what `BearingComparer` does to produce
`NotApplicable` rather than a blank cell, and what
`BearingValidationService` does to reject a contact angle recorded on a
deep-groove ball bearing.

`BearingFamilyTraits.IsApplicabilityKnown` is part of the contract:
`Unspecified` and `Other` are unclassified, every trait answers a
conservative `false` for them, and that `false` must be read as "not
known to apply", never "known not to apply".

Extending the taxonomy is two purely additive edits — an enum member and
a traits row. Nothing in the namespace switches exhaustively on the enum.

**2. The canonical types are stored directly; no parallel DTO graph.**
One internal envelope, `BearingDocumentDto(BearingId, Definition,
ValidationState, SupersededByBearingId)`, is serialised as the backing
document's revision content. `BearingDefinition` and its ~15 nested types
are serialised as themselves, with `JsonStringEnumConverter` so that
adding or reordering an enum member can never silently reinterpret an
already-stored record, and `[JsonIgnore]` on every computed property so a
stored document holds data rather than derived views. No value codec
exists, and none is needed.

**3. `RotationalSpeed` and `PlaneAngle` are added to
`Tempest.Core.UnitsAndQuantities`.** Two new `IDimension` markers and two
unit catalogues, built exactly like the seven that preceded them.
`RotationalSpeed`'s base unit is the revolution per second (this dimension
counts whole revolutions, which is what a catalogue speed rating means),
with rev/min and rad/s alongside; `PlaneAngle`'s base unit is the radian,
with degrees and arcminutes alongside. `MaterialPropertyValueCodec` is
deliberately **not** extended to cover them — its seven-dimension bound is
Materials' own disclosed scope boundary, and nothing in A4 goes through it.

**Temperature remains absent, and A4 does not model temperature limits.**
`Unit<TDimension>` is a purely multiplicative factor and cannot express an
affine scale such as degrees Celsius, so a lubricant temperature limit
cannot be stored as a dimensioned quantity today. Storing one as a bare
number would be exactly the loss of engineering meaning this library
exists to prevent, so `BearingLubrication` carries no temperature field at
all. This is a disclosed gap, not an oversight (`FCR-0034`).

## Consequences

**Positive:**

- "Not applicable to this family" and "not recorded" are structurally
  different answers, so a comparison table and a validation report can
  both be read honestly.
- A bearing family can be added without touching the storage format, the
  query evaluator, the comparer or the validator.
- One shape, not two: the canonical types and the stored shape cannot
  drift apart, because they are the same types. Enum-as-string keeps that
  safe across versions.
- Speed and angle are dimensioned quantities throughout, so a query for
  bearings above a given speed works whether the source quoted rev/min or
  rad/s, and no conversion logic is reimplemented inside A4.

**Negative:**

- Serialising the public types directly makes them a storage contract:
  renaming a property on `BearingDefinition` changes the on-disk shape.
  There is no migration mechanism here today — `ADR-0120`'s schema-version
  machinery is scoped to `EngineeringObjectState`, not to catalogue
  documents, and wiring A4 into it is deferred rather than assumed.
- `BearingCatalog.FindAsync`/`ListAsync` read a record's own full revision
  history to reconstruct current state, since `IEngineeringDocumentStore`
  offers no "latest revision only" lookup — the same disclosed
  characteristic `ADR-0055` records for Materials, inherited rather than
  introduced.
- `SearchAsync` lists and filters in memory. Deterministic and correct,
  but linear in catalogue size; a future index would change the
  implementation without changing `BearingQuery`.
- Two `IPersistenceStore` index collections rather than Materials' one,
  because manufacturer-part-number uniqueness cannot be enforced through
  the `bearingId` index.

## Alternatives Considered

**A flat schema carrying every family's properties on every record** —
rejected. It cannot distinguish an inapplicable property from an
unrecorded one, and it grows by a column every time a family is added.

**A per-family class hierarchy (`AngularContactBallBearing : Bearing`)** —
rejected. It moves the applicability question into the type system at the
cost of making polymorphic storage, querying and comparison substantially
harder, and it fixes the taxonomy in the inheritance graph: adding a
family would then be a code change to every switch that handles the base
type, which is precisely the "architectural redesign per new family" the
requirement forbids.

**A parallel DTO graph mirroring `BearingDefinition`, for consistency with
`MaterialSpecificationDto`** — rejected. Materials' DTO exists to solve
the boxed-generic problem, which A4 does not have; copying the pattern
without the reason would add ~15 hand-written types and hand-written
mapping whose only novel contribution is the opportunity to drift.

**Storing speeds and angles as bare `double`s to avoid touching
`UnitsAndQuantities`** — rejected. It would make A4 the one place in this
platform where an engineering value has no dimension, in a library whose
entire purpose is that engineering data be structured and unambiguous.

**Adding a `Temperature` dimension so lubricant limits could be
recorded** — rejected for now. A correct temperature dimension needs
affine units, which `Unit<TDimension>` cannot express; adding a
kelvin-only dimension would satisfy the letter of the requirement while
being unusable for the data engineers actually have.

## Related Documents

`ADR-0053` (Engineering Data Model built on the existing persistence
abstraction); `ADR-0054` (the units framework these two dimensions extend);
`ADR-0055` (the reference-data catalogue pattern A4 follows, and the codec
boundary A4 does not need); `ADR-0058` (the second application of that
pattern); `ADR-0072` (canonical objects are `EngineeringDocumentStore`-backed
Kinds); `ADR-0073` (open-string relationships); `ADR-0074` (family-specific
lifecycle specialisation of one canonical vocabulary);
`docs/architecture/A4 Bearing Library.md`.
