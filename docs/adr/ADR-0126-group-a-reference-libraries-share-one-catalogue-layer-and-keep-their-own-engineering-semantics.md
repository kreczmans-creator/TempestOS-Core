# ADR-0126: Group A Reference Libraries Share One Catalogue Layer, and Keep Their Own Engineering Semantics

## Status

Accepted — `Group A` (P01 Engineering Reference Data), 2026-09-06.

## Context

`A4` built the Bearing Library and, in doing so, built a great deal that
is not about bearings: a provenance record, a Draft → Checked → Validated
→ Released → Superseded lifecycle with a transition table and provenance
gates, a catalogue over `IEngineeringDocumentStore` with a typed index
and a secondary uniqueness index, per-record and per-key write locks, a
sourced-value and sourced-range shape, a comparison result that
distinguishes "not applicable" from "not recorded", a validation-service
shape, and an exception family.

`Group A` completes six more reference libraries — Materials, Standards,
Fasteners, Mechanical Components, Engineering Constants and Manufacturing
Processes. Every one of them needs all of the above, unchanged. The
governance question a reference library answers ("where did this come
from, has a person checked it, and may engineering work rely on it?") is
identical whether the record describes a bearing, a standard or a
casting process.

Building it seven times would be duplicated infrastructure of exactly the
kind this platform's own charter prohibits, and would guarantee that the
seven copies diverged. Building one generic record type that carried
every domain's fields would be the opposite mistake — a
`EngineeringReferenceData` class with a nullable field for every property
any library might ever want, which fits none of them and makes every
domain rule a runtime check.

The real question is where the line falls.

## Decision

**A shared `Tempest.Core.ReferenceData` layer holds what is genuinely
common; each library holds its own engineering semantics, and nothing
else moves.**

**1. What is shared, and why each item is.**

- `ReferenceProvenance`, `ReferenceExtractionMethod`,
  `ReferenceVerificationStatus` — "where did this come from" has one
  answer shape in every domain.
- `ReferenceValidationState` and `ReferenceValidationStates` — the
  lifecycle, its transition table, and the provenance each state
  requires. A family-specific specialisation of the canonical
  `LifecycleState` vocabulary (`ADR-0074`), stated once rather than seven
  times.
- `IReferenceDataCatalog<TDefinition>` and
  `ReferenceDataCatalog<TDefinition>` — register, find, list, revise,
  transition, supersede, read history, read a past revision. A thin,
  typed index over `IEngineeringDocumentStore` plus a direct
  `IPersistenceStore` index, which is the pattern `ADR-0055` established
  for Materials, `ADR-0058` repeated for Requirements and `A4` repeated a
  third time. The third repetition is what justifies extracting it.
- `IReferenceRecord<TDefinition>` — the split between a record's own
  engineering content and the catalogue governance around it.
- `ReferenceValue<TDimension>`, `ReferenceRange<TDimension>`,
  `ReferenceQuantityValue` and `ReferenceQuantityCodec`.
- `ReferenceComparer` and the comparison result, including the
  Recorded / NotRecorded / NotApplicable distinction.
- `IReferenceValidationService<TDefinition>`,
  `ReferenceValidationService<TDefinition>` and the `TEMPEST-REF-` rule
  series covering the rules that are about being reference data at all.
- `ReferenceDataException` and its six subtypes, each carrying the
  library's own name so a message says which library failed.
- `StandardReference` — a citation of a standard, needed by every library
  and owned by none of them.

**2. What is not shared, and stays per library.**

Each library declares its own definition type, its own family taxonomy,
its own family-traits table, its own query type and evaluator, its own
comparison property list, and its own rule series. These are engineering
semantics: a bearing query and a standards query have nothing in common
but the word, and a shared query type would fit neither.

**3. The domain type is a generic parameter, not a base class.** A
definition is a plain record with no base type, no lifecycle field and
no provenance field. It says only what a source said about the thing.

**4. Cross-library dependencies go through narrow seams declared in the
shared layer, not through library references.** `IStandardResolver` lets
any library confirm its own standard citations resolve without depending
on A2; `IReleasedConstantSource` lets a future calculation consume a
constant without depending on A6. Both are optional collaborators: a
fastener must be recordable and checkable before the material it names
has been registered, and no library may become a hard prerequisite for
holding data in another.

**5. A4 and A1 migrate onto the shared layer rather than being left
alongside it.** Bearings loses its own provenance, lifecycle,
serialisation and exception types; Materials loses its own property,
provenance and exception types. Both keep their existing document `Kind`,
so records written before the change remain their own.

## Consequences

### Positive

- One implementation of the lifecycle, the provenance gates, the write
  locking and the index maintenance, tested once, behaving identically in
  seven libraries.
- A new reference library is a definition, a taxonomy, a traits table, a
  query and a rule set — the engineering content, and nothing else.
- A4's own bearing rules were not copied into six other libraries. Each
  library's rule series says only what is true of its own domain, which
  is what §15 of the programme charter requires.
- The cross-library seams are narrow enough to be honest: a citing
  library can confirm a citation resolves and can learn nothing else.

### Negative

- A4 and A1 both churned substantially, and their tests with them. This
  is a real cost, paid once, and disclosed rather than avoided by leaving
  two libraries on their own copies of the infrastructure.
- `ReferenceDataCatalog<TDefinition>`'s `ListAsync` and the `FilterAsync`
  every library's search is built on enumerate the whole library.
  Reference catalogues are small and the result is exactly deterministic;
  should one grow large enough to matter, an index can be added behind
  `FilterAsync` without any library's own query type changing.
- Two libraries — Materials, whose property set is deliberately open
  (`ADR-0055`), and Constants, whose values are of whatever dimension the
  constant has — must box their quantities and pay for a codec. The other
  five declare every value at a statically-known dimension and do not.

### Neutral

- The shared layer sits in `Tempest.Core` alongside the libraries that
  use it, not in a separate assembly. Group A is one programme and its
  libraries ship together; a separate assembly would be structure without
  a boundary behind it.

## Alternatives Considered

**Leave A4 as it is; let each new library copy from it.** Rejected.
Seven copies of a provenance gate is seven places for one rule to drift,
and the programme charter names duplicated infrastructure explicitly.

**One `EngineeringReferenceData` class with every possible field.**
Rejected, and explicitly prohibited by the charter. It would fit no
domain, make every domain rule a runtime nullability check, and lose the
type-aware applicability every library depends on.

**A shared base class for the definition types too.** Rejected. There is
nothing every definition has in common except being a record: a bearing
has a manufacturer, a standard has a publisher, a constant has neither.
A base class would exist only to look symmetrical.

**Make the cross-library dependencies direct project references.**
Rejected. It would make A2 a hard prerequisite for holding a fastener,
create a dependency cycle the moment A2 wanted to cite a material, and
prevent any library from being used on its own.

**Force every library into an identical file layout for its own
domain types.** Rejected as cosmetic uniformity. The charter asks for
architectural convergence, and a library whose domain genuinely needs
three typed detail records (A5) should have three.

## Related Documents

- `ADR-0053`, `ADR-0055`, `ADR-0058`, `ADR-0072` — the document-store and
  typed-index pattern this generalises.
- `ADR-0073` — open-string relationship kinds; `Group A` introduces none
  of its own and reuses `supersedes`.
- `ADR-0074` — the canonical `LifecycleState` vocabulary
  `ReferenceValidationState` specialises.
- `ADR-0084` — reuse of `IValidationResult` for result shape only.
- `ADR-0124` — A4's own decisions, which this generalises rather than
  replaces.
- `ADR-0125` — affine units, the enabler for temperature in A1, A5 and A7.
- `docs/architecture/` — the per-library architecture documents.
