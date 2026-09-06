# A2 Standards Library

**Programme:** P01 — Engineering Reference Data
**Namespace:** `Tempest.Core.Standards`
**Governing ADR:** `ADR-0126`
**Status:** Implemented, `Group A`.

> The provenance model, lifecycle, catalogue mechanics, comparison
> semantics, data-quality principles and boundaries A2 shares with every
> other reference library are described once in
> `Group A Engineering Reference Data.md` and are not restated here.

---

## 1. Purpose

A2 is the register of engineering standards: which standards exist, who
publishes them, what edition is current, and how they relate to one
another.

It is also the library every other Group A library cites through.
`IStandardResolver` was declared in the shared layer before A2 existed,
with no implementation behind it; `StandardCatalog` is that
implementation.

---

## 2. The two decisions that carry the library

### 2.1 Publisher status is not record validation state

`StandardPublicationStatus` says what the **issuing body** holds:
`Unknown`, `Draft`, `Current`, `Amended`, `Superseded`, `Withdrawn`,
`Obsolete`.

`ReferenceValidationState` says how far **TempestOS** has checked its own
record of that standard.

They are different questions about different things, they vary
independently, and both directions occur in practice:

- A `Withdrawn` standard in a fully `Released` record — an accurate,
  verified record of a withdrawn standard is exactly what a legacy design
  review needs.
- A `Current` standard in a `Draft` record nobody has checked yet.

They are kept as separate fields, separate query criteria and separate
comparison rows. Collapsing them would be the single most damaging
modelling error available to A2: it would let TempestOS's own
record-keeping confidence be read as a statement about a standard's
standing in the world, which TempestOS has no authority to make.

`StandardPublicationStatuses` answers questions about the publisher's
position (`IsKnown`, `IsCurrent`, `IsNoLongerInForce`,
`ExpectsWithdrawalDate`) and deliberately answers none about whether a
standard may be used for new design — that rests on contract, regulation
and customer requirements A2 knows nothing about.

### 2.2 An edition is a record, not a revision

The uniqueness key is body, designation **and** edition. Two editions of
one standard are two records, both holdable at once, related by
supersession. Where no edition is recorded the key collapses to body and
designation, so the library holds at most one undated record per standard
— the right answer, since a second undated record could not be told apart
from the first.

`FindByDesignationAsync` without an edition finds the *undated* record,
not "the latest edition": which edition applies to a design is a
contractual question A2 cannot answer. `FindEditionsAsync` returns them
all and picks none.

---

## 3. A2 registers standards; it never reproduces them

There is no field for a standard's clauses, tables, figures or
requirements, and there deliberately never will be. That content is the
copyright of the issuing body, and reproducing it would be both unlawful
and a category error — TempestOS would then be asserting technical
requirements it has no authority to state.

`ScopeSummary` is a summary written by whoever recorded the standard, in
their own words. `TEMPEST-STD-009` warns when one exceeds 600 characters,
on the reasoning that a genuine one- or two-sentence summary fits
comfortably and a reproduced scope clause generally does not. It is a
heuristic and is reported as a warning for exactly that reason: no length
test can tell a long summary from a short quotation, so it asks a person
to look rather than making a claim.

---

## 4. Canonical model

`StandardDefinition` requires a `Body` and a `Designation`. Everything
else is optional: `Title`, `Edition`, `PartNumber`, `Classification`,
`Disciplines`, `SourceClassification`, `PublicationStatus`,
`PublicationDate`, `EffectiveDate`, `WithdrawalDate`, `ConfirmationDate`,
`ScopeSummary`, `ReplacesDesignations`, `Equivalences`,
`NormativeReferences`, `Language`, `Notes`.

### 4.1 Taxonomies

`StandardsBody` carries a free-text `Code` plus a `StandardsBodyKind`.
The set of standards bodies is open, changes without notice and includes
every company that writes an internal standard; a closed list would be
obsolete the day it was written. What *is* classifiable is the kind of
authority the organisation carries, and that is small and stable.

`StandardClassification` says what kind of document the standard is;
`StandardDiscipline` says what subject it covers. They are orthogonal — a
test method and a product specification can both be about fasteners — and
`Disciplines` is a list, because a standard legitimately covers several.

`StandardClassificationTraits` says which parts of a record are
meaningful: whether a standard states conformity requirements, whether it
defines a test method, and whether it is one another record would
legitimately cite as the basis of a dimensioned value.

### 4.2 Relationships between standards

Supersession is TempestOS's own governance act and goes through the
catalogue's `SupersedeAsync` and the platform's existing `supersedes`
relationship.

Everything else is **data**, recorded in the definition:

- `ReplacesDesignations` — what the publisher said this edition replaces.
  A standard can replace one nobody has registered here, and A2 must be
  able to record that without inventing a record to point at.
- `Equivalences` — national adoptions and cross-body equivalence, each
  carrying a `StandardEquivalenceKind` and a `ReferenceValueOrigin`
  saying **who claimed it**. TempestOS never decides that two standards
  are equivalent; an equivalence marked `DerivedByTempestOS` is flagged
  (`TEMPEST-REF-004`), and one with no recorded origin is warned about
  (`TEMPEST-STD-014`).
- `NormativeReferences` — reusing the shared `StandardReference`.

A2 introduces no relationship kind of its own.

---

## 5. Validation

`TEMPEST-STD-001`…`014`. Beyond the identity and classification rules:

- **Contradictions** (007) — a standard both `Current` and carrying a
  withdrawal date.
- **Date ordering** (008) — effective before published, withdrawn before
  published, withdrawn before effective, confirmed before published.
- **Self-reference** (010) — a standard listing itself as an equivalent,
  a normative reference, or something it replaces. Checked against both
  the record's own key and its undated form, so a citation that omits the
  edition is still caught.
- **The one place the two axes inform each other** (012) — a record
  superseded in TempestOS but still describing its standard as `Current`.
  A warning, never an error: the record could have been superseded by a
  better-sourced record of the same, still-current edition.

---

## 6. Boundaries

A2 holds no standard content, performs no conformity assessment, makes no
statement that anything complies with anything, and gives no advice on
which standard to apply.

Registering a standard is not endorsing the body that publishes it, and a
`StandardReference` in another library records only that a source cited
the standard.

---

## 7. Dataset

Empty. See `Group A Engineering Reference Data.md` §9. Population is
tracked as its own Future Capability Record, and is licence-constrained
in a way the other libraries are not: a standards index is itself a
copyrighted work in many jurisdictions.
