# Classification & Relationship Vocabulary Safety Net Architecture

**Status: Designed — `WP 12.1A` (`ADR-0105`). No implementation;
`WP 12.1B` (already named in `WP11.0B Architecture Roadmap.md` §3)
would apply this document's own recommendations and `ADR-0105`'s own
rules.**

## Objective

Realise `WP11.0A Platform Architecture Review.md` Finding `A-6`
("Domain classification/relationship vocabulary is entirely stringly-
typed, by deliberate design, with no compile-time safety net") — audit
every classification and relationship vocabulary this platform
currently uses across `Tempest.Core`, `Tempest.App`, and
`Tempest.Desktop`, confirm exactly which parts of `A-6`'s own concern
are real (not merely theoretical), and design a canonical, lightweight,
additive model that closes the confirmed gaps without reopening any of
the five prior ADRs that already, deliberately, chose an open-string
vocabulary over a closed or validated one. Architecture only — no code,
no contract change, no implementation.

## Repository Investigation

### 1. The Kind vocabulary — `IEngineeringObject.Kind`

`Kind` (`Tempest.Core.EngineeringDomain.IEngineeringObject`, `ADR-0072`)
is a plain `string`, deliberately never a closed type — "every
canonical Engineering Object... is realised as a `Kind` string...
never a closed enum." `EngineeringObjectFactory<T>` (the one generic
factory type every discipline uses) takes a `Kind` string as a plain
constructor argument; `WP8.2B Dependency Rules.md` §8 explicitly
proposes no registry contract for it. This is unchanged, and this
Work Package does not propose changing it.

**How each discipline actually declares its own Kind values today —
confirmed by direct read, not assumed uniform:**

| Discipline | Layer | Kind values declared as named constants? |
|---|---|---|
| Requirements | `Tempest.Core.Requirements.RequirementsService` | Yes — `RequirementDocumentKind`, `RequirementCollectionDocumentKind`, `RequirementGroupDocumentKind` |
| Verification | `Tempest.Core.Verification.VerificationService` | Yes — `VerificationRecordDocumentKind` |
| Calculations | `Tempest.Core.Calculations.CalculationEngine` | Yes — `CalculationRecordDocumentKind` (`"CalculationSet"`/`"CalculationTemplate"` are not — see `CalculationObjectFactoryRegistry`, below) |
| Materials | `Tempest.Core.Materials.MaterialCatalog` | Yes — `MaterialSpecificationDocumentKind` |
| Mechanical | `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry` | **No** — eight Kinds (`Project`/`Assembly`/`SubAssembly`/`Part`/`Component`/`Configuration`/`Baseline`/`Release`), each typed as an inline literal at every use site; only a `SupportedKinds` array literal names them together |
| Documents | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry` | **No**, for the three base Kinds (`Document`/`Drawing`/`CadModel`) — only inside `SupportedKinds`. **Yes**, for the six/nine `Classification` sub-values (see §2) |
| Manufacturing | `Tempest.App.Workspace.Manufacturing.ManufacturingObjectFactoryRegistry` | **No**, for the three base Kinds (`ManufacturingOperation`/`WorkInstruction`/`Inspection`) — only inside `SupportedKinds`. **Yes**, for the three `Classification` sub-values (see §2) |
| Calculations (Workspace) | `Tempest.App.Workspace.Calculations.CalculationObjectFactoryRegistry` | **No** — `"CalculationSet"` is an inline literal at every use site (three occurrences in one file alone) |
| Verification (Workspace) | `Tempest.App.Workspace.Verification.VerificationActivityFactoryRegistry` | Yes — `SupportedKind = "VerificationActivity"` |

**Quantified duplication, confirmed by direct search of `src/` (excluding `bin`/`obj`):** the literal Kind string
`"Part"` appears **14 times across 5 different files** spanning three
layers (`Tempest.Samples.EngineeringDomainSampleModule`,
`Tempest.Samples.MechanicalProductStructureSampleModule`,
`Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry`,
`Tempest.Desktop.Composition.RibbonObjectActionHandlers`,
`Tempest.Desktop.Icons.IconRegistry`) — zero of them referencing a
shared constant, because none exists.
`MechanicalObjectFactoryRegistry` itself repeats every one of its own
eight Kind strings three times within the same file (once in
`SupportedKinds`, once as a switch label, once as the literal argument
to `EngineeringObjectFactory<T>`'s own constructor).
`Tempest.Desktop.Icons.IconRegistry`'s own Kind-to-glyph dictionary
independently re-types 22 further Kind string literals as dictionary
keys, none referencing any owning discipline's own constant (where one
even exists).

### 2. The Classification vocabulary — `IHasMetadata.Classification`

`Classification` (`Contracts/Facets.cs`, `IHasMetadata`) is a free-text
`string?`, alongside sibling free-text fields `Category`, `Discipline`,
`Owner`, `Tags`, `Notes` — none validated against any vocabulary,
anywhere, confirmed by direct read of `EngineeringObjectBase`'s own
unconditional facet implementation.

**Two disciplines have already, independently, invented the identical
pattern — using `Classification` to distinguish several logical
sub-types sharing one real Kind — each with its own ADR:**

- **`ADR-0088`** (Documents, `WP 9.4A`): Specification/Report/
  Procedure/Standard/Datasheet/External Reference (plus Resource/
  Tooling/Fixture, added `WP 9.5A`) are all plain `"Document"` objects,
  distinguished only by `Classification`. Six/nine named `string`
  constants declared once, on `DocumentObjectFactoryRegistry`.
  `DocumentCategory.Of` maps a live object to its own display category
  by pattern-matching against those same constants, with an honest
  `"Uncategorized"` fallback for anything unrecognised — never
  dropped, never rejected.
- **`ADR-0091`** (Manufacturing, `WP 9.5A`): Routing/Operation/Supplier
  Operation are all plain `"ManufacturingOperation"` objects,
  distinguished only by `Classification`. Three named `string`
  constants declared once, on `ManufacturingObjectFactoryRegistry`.
  `ManufacturingCategory.Of` mirrors `DocumentCategory.Of`'s own
  identical shape exactly, cited directly in `ADR-0091`'s own Related
  Documents.

**One discipline deliberately rejected this exact pattern, for a
principled reason directly relevant to this Work Package's own
canonical model:** `ADR-0090` (Verification, `WP 9.3A`) considered "a
`Category`/`Classification`-style metadata tag distinguishing 'Plan'
from 'Activity,' mirroring `ADR-0088`'s own mechanism exactly" and
rejected it — `LifecycleState` already carries the identical
distinction for free (`Draft` = not yet started), and adding
`Classification` too would mean "two fields for one distinction, never
reconciled against each other, a genuine source of drift `ADR-0088`'s
own single-field design does not risk." **This is direct, in-repository
evidence that `Classification`-tagging is not universally the right
sub-typing axis — the canonical model this Work Package proposes must
not mandate it where an existing signal (most often `LifecycleState`)
already carries the same distinction.**

Both `ADR-0088` and `ADR-0091` are already internally disciplined about
their own `Classification` *sub-values* (each declared exactly once,
each pattern-matched through one owning `X Category.Of` function) —
but, as §1 shows, that same discipline is not applied to either
discipline's own *base Kind string*, inside the identical file, one
field over. The gap this Work Package closes is narrower and more
precise than "Classification is undisciplined" — it is "the
declare-once discipline `ADR-0088`/`ADR-0091` already prove works for
`Classification` values is not applied uniformly, to every vocabulary,
in every discipline."

### 3. The relationship vocabulary — `IEngineeringRelationship.RelationshipKind`/`RelationshipCategory`

`RelationshipKind` (`ADR-0073`) is a plain, open, unvalidated string —
"never validated against the target's own Kind." `RelationshipCategory`
(`ADR-0076`) is a small, closed, seventeen-value enum, carried as
*descriptive metadata only*, never validated against `RelationshipKind`
at write time. Both are unchanged by this Work Package.

**A real, working, partial safety net for this exact vocabulary
already exists and already proves the eventual shape works:**
`RelationshipKindCategoryMap` (`Tempest.Core.EngineeringDomain.Implementation`)
infers a `RelationshipCategory` from 17 named, conventional
`RelationshipKind` strings (`groupedUnder`, `collects`, `dependsOn`,
`blocks`, `derivesFrom`, `allocatedTo`, `references`, `relatedTo`,
`satisfies`, `verifiedBy`, `calculatedBy`, `basedOnCalculation`,
`supersedes`, `duplicates`, `manufacturedBy`, `documentedBy`,
`approvedBy`), defaulting non-blockingly to `RelationshipCategory.Reference`
for anything unrecognised — never throwing, never rejecting a write.
Its own XML doc discloses it directly: "Never validated against the
caller's own declared category (`ADR-0076`) — purely descriptive."

`RequirementRelationshipKinds` (`Tempest.Core.Requirements`) is a real,
already-shipped, per-discipline `const string` catalogue — seven named
relationship kinds (`GroupedUnder`, `CollectedIn`, `DependsOn`,
`DerivesFrom`, `AllocatedTo`, `References`, `Satisfies`), declared once
each, referenced throughout the Requirements framework — the exact
shape `A-6`'s own
recommendation names by example ("per-discipline `internal static
class` string-constant catalogues"). No other discipline has an
equivalent relationship-kind catalogue of its own; `VerificationService`
declares three relationship-kind constants directly on itself rather
than in a dedicated catalogue class, a smaller-scale variant of the
identical idea.

**Confirmed, concrete cross-layer duplication — the exact failure mode
`A-6`'s own Impact statement described in the abstract, found here as
a real, present instance:** `Tempest.Desktop.DigitalThread.DigitalThreadGraphModel`
independently declares:

```csharp
private const string VerifiedByRelationshipKind = "verifiedBy";
```

— the identical value, and the identical name, as the real, owning
constant, `Tempest.Core.Verification.VerificationService.VerifiedByRelationshipKind`.
Nothing in the platform today prevents, or even flags, two classes in
two different layers each independently declaring the same fact. This
is the single clearest, most concrete piece of evidence this
investigation found that `A-6`'s own concern is real, not
speculative.

`WP8.2A Relationship Catalogue.md` (`ADR-0073`'s own cited "resulting
platform-wide vocabulary") already proves the documentation-layer half
of the eventual model works, and — confirmed by direct read — has
already been kept current well past its own originating Work Package:
both `calculatedBy` (added `WP 9.2A`) and `manufacturedBy` (added
`WP 9.5A`) are present in it today. It has no equivalent for
`Classification`, and it is not a continuously-reviewed governance
register in the sense `Module Register.md`/`Namespace Register.md`
already are — no "Last Reviewed" narrative field, no stated Review
Frequency, no coverage from `scripts/governance-healthcheck.ps1`.

### 4. Digital Thread — the platform's own most vocabulary-hungry consumer

`Tempest.Desktop.DigitalThread.DigitalThreadGraphModel` (`WP 10.4A`) is
the one place in the platform that reads `Kind`, `RelationshipKind`,
and `RelationshipCategory` most broadly and most generically — every
node and edge in a live Digital Thread graph. Confirmed by direct
read: it is fully Kind-agnostic and `RelationshipKind`-agnostic except
for exactly two special cases (`VerificationActivityKind`/
`VerifiedByRelationshipKind`, both hand-typed locally, the duplicate
named above), reading every other Kind/RelationshipKind/Category value
generically, via the same `IEngineeringRelationship`/`IEngineeringObject`
contracts every other consumer already uses. This confirms directly
that the eventual safety net's own compatibility risk with the Digital
Thread roadmap is low: nothing about generalising `Kind`/`Classification`/
`RelationshipKind` declaration discipline requires touching
`DigitalThreadGraphModel`'s own generic read path at all — only its
own two hand-typed local duplicates, which this Work Package's own
canonical model directly targets for retirement.

## Architecture

**Three additive components, none of which validates a vocabulary
value at write time, none of which reopens `ADR-0072`/`ADR-0073`/
`ADR-0076`/`ADR-0088`/`ADR-0090`/`ADR-0091`:**

### Component 1 — Declaration discipline

Every live Kind, `Classification`, and `RelationshipKind` string value
is declared exactly once, as a `public const string`, on the single
class that owns it:

- A Domain-layer framework's own value (one it defines and is the sole
  writer of, per `ADR-0078`'s own "one Kind, one owner" rule) is
  declared in `Tempest.Core`, on that framework's own service class —
  the pattern `RequirementsService`/`VerificationService`/
  `CalculationEngine`/`MaterialCatalog` already follow, unchanged,
  extended only to cover the handful of relationship-kind values those
  same frameworks use today without yet naming a constant for.
- A Workspace-only discipline's own value (Mechanical's own eight
  structural Kinds; Documents'/Manufacturing's own base Kinds; every
  discipline's own `Classification` sub-values) is declared in
  `Tempest.App.Workspace.{Discipline}`, on that discipline's own
  `XObjectFactoryRegistry` — the pattern `DocumentObjectFactoryRegistry`/
  `ManufacturingObjectFactoryRegistry` already follow for their own
  `Classification` values, extended to also cover their own base Kind
  strings, and extended to `Mechanical`/`Calculations` (Workspace),
  which today declare none at all.

Every other consumer — a sibling discipline, a `Tempest.Desktop`
collaborator, a sample module, a test — references the owning
declaration directly. **It never declares its own local copy of the
same value**, however small or however local the apparent scope
(the specific rule `DigitalThreadGraphModel`'s own confirmed duplicate
violates).

### Component 2 — Engineering Vocabulary Register

A new governance register, `docs/governance/Engineering/Engineering
Vocabulary Register.md`, mirroring the established shape of its own
siblings in the same directory (`Module Register.md`, `Namespace
Register.md`, `Interface Register.md`, `Event Catalogue.md`):

| Value | Vocabulary | Declaring Class | Meaning |
|---|---|---|---|
| `"Part"` | Kind | `MechanicalObjectFactoryRegistry` (proposed — not yet declared) | A discrete Mechanical component with no further internal structure |
| `"Specification"` | Classification | `DocumentObjectFactoryRegistry` | A `"Document"` object realising a formal Specification |
| `"verifiedBy"` | RelationshipKind | `VerificationService` | Records that the source object is verified by the target |
| … | … | … | … |

One row per live value, generalising `WP8.2A Canonical Object/
Relationship Catalogue.md`'s own already-proven shape and extending it,
for the first time, to also cover `Classification`. Carries the same
`Review Frequency`/"Last Reviewed" discipline every other register
under `docs/governance/Engineering/` already states for itself — a
continuously-maintained governance asset, not a one-time `WP 8.2A`-era
deliverable left to drift.

Populating this register from a full, direct repository scan — the
identical scan this architecture document's own Repository
Investigation already performed — is `WP 12.1B`'s own first concrete
scope item, not attempted here (architecture only).

### Component 3 — Consistency check (a test, not a new tool)

One additive, non-blocking xUnit test suite (`EngineeringVocabularyConsistencyTests`,
`Tempest.Desktop.Tests`, alongside `ModuleLifecycleStabilityTests`'s
own comparable platform-health-regression precedent), realised as four
`[Fact]` checks rather than one undifferentiated assertion, so a
failure names precisely which guarantee broke:

1. **Register drift.** Reflects over every class the Engineering
   Vocabulary Register names as a value's own declaring class and
   fails, naming the missing value, if a value listed in the register
   has no corresponding declared constant the reflection scan can
   find — the "documentation silently drifts from code" failure mode.
2. **Duplicate canonical-owner detection (cross-owner duplication).**
   Fails, naming both offending classes and the shared value, if the
   identical string value is declared as a named constant by two
   different registered declaring classes — the exact, confirmed
   `VerifiedByRelationshipKind` failure mode, caught the moment a
   second copy is introduced. The one disclosed, pre-existing exception
   (`references`, independently and legitimately owned by both
   `RequirementRelationshipKinds` and `VerificationService`) is excluded
   by name, not silently tolerated.
3. **Register self-consistency.** Fails if the register itself declares
   the same (Value, Vocabulary) pair with two different values — a
   defence against a mistake in the register's own authoring, not the
   code it describes.
4. **Rogue duplicate vocabulary scan.** Reflects over every type in
   `Tempest.Core`, `Tempest.App`, `Tempest.Samples`, and
   `Tempest.Desktop` — not only the classes named in the register — and
   fails if any class outside the register redeclares a registered
   value as its own `public` or `private const string`. This is
   strictly broader than checks 1–3 (which only reason about classes
   the register already names) and is what actually catches a *future*,
   previously-unknown duplicate the moment it is written, anywhere in
   the four scanned assemblies — verified directly, not merely assumed:
   a temporary rogue duplicate was deliberately introduced during
   `WP 12.1B`'s own implementation and confirmed to fail this check with
   a precise message, then reverted.

This directly realises `A-6`'s own explicit second recommendation ("a
build-time check validating classification/relationship strings
against the live registries") using this platform's own existing
test-suite-as-safety-net convention — no new script, no new CI job, no
new tool. It runs, and can fail a build, exactly like every other test
in `dotnet test src/TempestOS.slnx`.

**Why `Tempest.Desktop.Tests`, specifically, not `Tempest.Core.Tests`.**
The confirmed, motivating defect this whole architecture is built on
(`VerifiedByRelationshipKind`, independently redeclared in
`Tempest.Core.Verification.VerificationService` *and*
`Tempest.Desktop.DigitalThread.DigitalThreadGraphModel`) spans all
three layers — a cross-layer duplicate, not a same-layer one. A
reflection scan can only find a duplicate declared in an assembly it
can actually reference. `Tempest.Core.Tests` (`Tempest.Core.Tests.csproj`)
references `Tempest.Core`, `Tempest.Samples`, `Tempest.App`, and
`Tempest.Validation` — never `Tempest.Desktop` — so a test placed there
structurally cannot see `DigitalThreadGraphModel` at all, and could
never have caught the exact defect that motivated Component 3 in the
first place. `Tempest.Desktop.Tests` (`Tempest.Desktop.Tests.csproj`)
already references all three layers this platform has —
`Tempest.Core`, `Tempest.App`, and `Tempest.Desktop` — confirmed
directly against its own `.csproj`. It is the only test project able
to reflect across every layer a vocabulary value's own declaration or
duplicate might live in, and is therefore the only structurally
correct home for a check whose own stated purpose is catching
cross-layer duplication.

**What this architecture deliberately does not add:** no runtime
registry service; no `IVocabularyRegistry` Platform Service; no
validation at `LinkAsync`/`CreateAsync` time; no change to
`IHasMetadata`, `IEngineeringObject`, or `IEngineeringRelationship`;
no change to `RelationshipCategory`'s own seventeen-value enum. Every
one of `ADR-0072`/`ADR-0073`/`ADR-0076`/`ADR-0088`/`ADR-0090`/`ADR-0091`'s
own stated reasoning for an open-string, non-validated vocabulary
remains exactly as true after this architecture as before it.

## Compatibility Analysis

Evaluated against every named discipline and the Digital Thread
roadmap, directly, not by inference:

- **Engineering Domain Architecture (`ADR-0072`/`ADR-0075`).** Fully
  compatible — this architecture adds no new facet, no new contract,
  no new Domain-layer type. `IHasMetadata`/`IEngineeringObject`/
  `IEngineeringRelationship` are byte-for-byte unchanged.
- **Mechanical Product Structure.** The discipline with the largest
  confirmed gap (zero declared Kind constants across eight Kinds) and
  therefore the clearest, most concrete beneficiary — `WP 12.1B`'s own
  natural first retrofit target, alongside the confirmed
  `VerifiedByRelationshipKind` duplicate.
- **Requirements Management.** Already substantially compliant —
  `RequirementsService`/`RequirementRelationshipKinds` already declare
  every Kind and most relationship-kind values it uses as named
  constants. Requires only registration in the new Engineering
  Vocabulary Register, no code change.
- **Documents.** Already compliant for `Classification` sub-values
  (`ADR-0088`); the confirmed gap is narrow — three base Kind strings
  (`Document`/`Drawing`/`CadModel`) currently exist only inside a
  `SupportedKinds` array literal.
- **Calculations.** Domain-layer `CalculationRecordDocumentKind`
  already exists; Workspace-layer `"CalculationSet"`/`"CalculationTemplate"`
  do not yet have declared constants — a small, disclosed gap,
  comparable in shape to Documents'.
- **Verification.** Already substantially compliant
  (`VerificationRecordDocumentKind`, three relationship-kind constants
  on `VerificationService`). `ADR-0090`'s own deliberate choice to use
  `LifecycleState` rather than `Classification` for Plan/Activity is
  explicitly preserved, not overridden — this architecture's own
  Component 1 only governs *where a value is declared once it is
  chosen*, never *which facet a discipline should choose* to carry a
  distinction.
- **Manufacturing.** Already compliant for `Classification` sub-values
  (`ADR-0091`); the confirmed gap mirrors Documents' own exactly —
  three base Kind strings not yet declared as constants.
- **Digital Thread roadmap.** Confirmed low-risk directly — `§4`,
  above. `DigitalThreadGraphModel`'s own generic `Kind`/
  `RelationshipKind`/`Category` read path needs no change at all; only
  its own two hand-typed local duplicates are retrofit targets, and
  retrofitting them (referencing the real owning constants instead)
  is a pure simplification, not a behavioural change.

No discipline requires a contract change, a new dependency edge, or a
behavioural change to adopt this architecture — every retrofit named
above is "replace an inline literal or a locally-duplicated constant
with a reference to one, newly-declared, owning constant," never a
change to what value is written or read.

## Migration Strategy & Backwards Compatibility

**Zero breaking change, by construction.** This architecture governs
*where a vocabulary value's own declaration lives*, never *what values
are valid* — every existing Kind, `Classification`, and
`RelationshipKind` string value already in use remains completely
unchanged. No consumer's observable behaviour changes whether or not
it has adopted the declare-once convention; the platform continues to
compile and run identically either way, since the underlying values
these constants would merely name are exactly the values already
flowing through `IHasMetadata`/`IEngineeringObject`/
`IEngineeringRelationship` today.

**Incremental, opportunistic retrofit — no dedicated, big-bang
migration Work Package.** Consistent with this project's own
established "disclose and fix pre-existing drift found along the way"
convention (the identical discipline `WP 12.0B`'s own architecture
review, `WP 12.4A`'s own duplication finding, and this very
investigation's own methodology all already follow): a discipline's
existing Kind/`Classification`/`RelationshipKind` literals are
consolidated into declared constants the next time that discipline's
own Workspace layer is touched for any real reason — never a dedicated
Work Package sweeping all nine disciplines at once.

**`WP 12.1B`'s own concrete, minimum scope** (named directly, not left
merely implied): create and populate the Engineering Vocabulary
Register from a full repository scan; add the Component 3 consistency
test; retrofit the two confirmed, concrete defects this investigation
found by name — `DigitalThreadGraphModel`'s own `VerifiedByRelationshipKind`
duplicate (reference `VerificationService`'s own constant instead), and
`Mechanical`'s own complete absence of declared Kind constants (the
discipline with the largest, most conspicuous gap) — as the initial,
concrete proof the new convention closes real drift, not merely
hypothetical drift. Retrofitting every remaining, smaller gap
(Documents'/Manufacturing's own base-Kind-string gap; Calculations'
own Workspace-layer gap) is real, disclosed, future work, not
required for `WP 12.1B` to close its own scope.

## Alternatives Considered — Full Evaluation

### 1. String literals (status quo — no discipline)

- **Advantages.** Zero new code, zero new document, zero new test —
  the platform's own current, real state.
- **Disadvantages.** The confirmed defect this investigation found
  (`VerifiedByRelationshipKind` duplicated across layers) is exactly
  what this option leaves unaddressed; `A-6`'s own Impact statement
  ("discoverable only via... a full-text search") remains true
  indefinitely, growing linearly with each of the nine-and-growing
  planned disciplines.
- **Layering impact.** None.
- **Verdict.** Rejected as the ongoing default — this is the problem
  `A-6` asked this Work Package to solve, not a candidate solution.

### 2. A closed enum per vocabulary

- **Advantages.** Full compiler enforcement — an invalid value is a
  compile error, not a runtime typo.
- **Disadvantages.** Already rejected, independently, five separate
  times, by five separate ADRs (`ADR-0072`, `ADR-0073`, `ADR-0076`,
  `ADR-0088`, `ADR-0091`) — every one of them for the identical
  reason: a closed set cannot scale to an extensible, module-defined
  Kind catalogue (`ADR-0072`'s own extensibility promise) without
  becoming a platform-review bottleneck for every future value.
- **Layering impact.** Would centralise every discipline's own
  vocabulary into `Tempest.Core`, directly contradicting `ADR-0072`'s
  own "a module may mint a new canonical object without platform
  review" promise.
- **Verdict.** Rejected — not re-litigated here in full; the five
  prior ADRs' own reasoning is not weaker for this investigation
  having found real duplication, since a closed enum would fix the
  duplication at the cost of the extensibility every one of those five
  ADRs already, deliberately, chose to keep.

### 3. A strongly-typed value object wrapping the string

(e.g., a `readonly struct Kind` or `ClassificationValue` wrapping a
validated or unvalidated `string`, with implicit conversions.)

- **Advantages.** Genuinely different from a closed enum — does not
  close the set, still permits any value at construction — while
  giving call sites a distinct type rather than a bare `string`,
  making a `Kind`-typed parameter harder to accidentally pass a
  `Classification`-typed value into.
- **Disadvantages.** Does not, by itself, solve the confirmed
  duplication problem — a value object still needs its own single
  declared source; the wrapper adds a second concern (type confusion
  prevention) this investigation found no confirmed evidence is
  actually occurring (no instance of a `Classification` value being
  passed where a `Kind` was expected, or vice versa, was found anywhere
  in this repository). Introduces a new `Tempest.Core` type every one
  of nine-and-growing disciplines and every future module must adopt
  — a real, if small, extensibility cost `ADR-0072` explicitly designed
  around avoiding for exactly this reason (a plain `Kind` string, "no
  new identifier scheme," `ADR-0067`'s own identical reasoning).
- **Layering impact.** A new shared `Tempest.Core` type, referenced by
  every layer — a mild recentralisation of what `ADR-0072` deliberately
  decentralised.
- **Verdict.** Rejected — no material benefit over a plain `const
  string` for the specific, confirmed problem (duplication,
  discoverability) this Work Package exists to solve, at a real
  adoption cost across every current and future discipline.

### 4. Registry-driven vocabularies (a runtime, DI-resolved service)

(e.g., an `IVocabularyRegistry` Platform Service, analogous to
`ICommandRegistry`, that every discipline registers its own values
into at startup, resolved and queried at runtime.)

- **Advantages.** A single, queryable, live source of truth; could, in
  principle, support runtime validation if a future Work Package ever
  wanted it.
- **Disadvantages.** Architecturally the closest of the five
  alternatives to what `ADR-0073` already explicitly rejected — "the
  store checks that a `verifiedBy` link's own target is actually
  `Kind = ...`" — generalised from relationships to every vocabulary. A
  runtime registry for a concern that needs no runtime resolution (a
  value's own name and meaning are known at compile time, by the
  developer writing the code that uses it) adds a real duplicate-
  registration hazard (mirroring `ADR-0067`'s own `DuplicateWorkspaceRegistrationException`
  cost) and a DI-resolution dependency for zero behavioural gain over
  a plain constant. Registering it as a Platform Service would
  misclassify a documentation/coordination concern as a platform
  capability — the identical misclassification `ADR-0104` (`WP 12.4B`)
  just finished warning against for Desktop-local wiring, one release
  earlier.
- **Layering impact.** A new Platform Service (`ADR-0009`-governed),
  DI-resolved by every consumer — real, if modest, new coupling for a
  concern this investigation found needs none.
- **Verdict.** Rejected as the *primary* mechanism. Component 2 (the
  Engineering Vocabulary Register) is, in the loosest sense, a
  "registry" — but a documentation-layer one, hand-maintained, never
  DI-resolved, never queried at runtime — precisely the distinction
  that keeps it compatible with `ADR-0073`/`ADR-0076`'s own "never
  validated" rule rather than reopening it.

### 5. Extensible metadata models

(e.g., replacing the named `Classification`/`Category`/`Discipline`
facet fields on `IHasMetadata` with a single, fully free-form
`IReadOnlyDictionary<string, string>` tag bag.)

- **Advantages.** Maximal flexibility — any future discipline could
  invent any new metadata dimension without a Domain contract change.
- **Disadvantages.** `IHasMetadata` is a frozen `WP 8.2B` contract;
  changing its own shape is a genuine Domain contract redesign,
  directly contradicting every real-discipline Work Package's own
  repeated, explicit "no contract redesign" constraint (`ADR-0088`'s/
  `ADR-0091`'s own controlling instructions both state this verbatim).
  An unbounded key-space is a *larger*, not smaller, version of the
  exact coordination problem `A-6` named — more ways to duplicate or
  collide, not fewer, with no equivalent of `Classification`'s own
  already-proven, disciplined, two-instance precedent to build on.
- **Layering impact.** A Domain-layer contract change, the only one of
  the five alternatives that is.
- **Verdict.** Rejected — moves in the opposite direction from what
  `A-6` asked for; would make the vocabulary-coordination problem
  worse, not better, while also being out of scope for an
  architecture-only Work Package that inherits, rather than reopens,
  every prior contract-freezing decision.

**The chosen model (Components 1–3) is a synthesis, not a single
alternative from this list** — it reuses `RequirementRelationshipKinds`/
`DocumentObjectFactoryRegistry`/`ManufacturingObjectFactoryRegistry`'s
own already-proven `const string` declaration shape (a lightweight
form of "string literals," disciplined), `WP8.2A Canonical Object/
Relationship Catalogue.md`'s own already-proven documentation-layer
shape (a lightweight, non-runtime form of "registry-driven," extended
to `Classification`), and this platform's own existing test-suite
convention (Component 3) — deliberately choosing the smallest,
already-validated pieces over any single, larger new mechanism,
exactly the "reuse what exists" resolution this project has reached
repeatedly (`ADR-0072`, `ADR-0067`, `ADR-0099`).

## Required ADR

**`ADR-0105`** — produced. This investigation establishes a genuine,
lasting architectural convention (Engineering Governance §5: the
decision "establishes a convention future work packages are expected
to follow," and resolves "an explicit ambiguity or tension between two
stated requirements" — `ADR-0073`'s own open-string commitment versus
`A-6`'s own compile-time-safety-net concern), with at least one genuine
alternative (four, in fact — enum, value object, runtime registry,
free-form metadata) seriously considered and rejected. No existing ADR
already fully governs this specific question: `ADR-0072`/`ADR-0073`/
`ADR-0076`/`ADR-0088`/`ADR-0090`/`ADR-0091` each settle *whether* a
vocabulary is open-string (yes, unanimously, unchanged by this
decision) but none settles *how a value's own declaration is kept
single-sourced and discoverable once it is real* — the specific,
still-open gap `A-6` named and `ADR-0105` closes.

## Documentation Impact

- **New**: `ADR-0105`; this document; `docs/academy/03 Work
  Packages/WP12.1A-classification-and-relationship-vocabulary-safety-net-architecture.md`.
- **Designed, not created** (implementation, `WP 12.1B`'s own scope):
  `docs/governance/Engineering/Engineering Vocabulary Register.md`; the
  Component 3 consistency test.
- **Updated, this Work Package**: `ADR Register.md`, `Architecture
  Document Register.md`, `Documentation Register.md`, `Academy
  Register.md`, `docs/releases/v0.12.0/WorkPackages.md`,
  `PROJECT_STATUS.md`.
- **Unchanged**: every existing ADR this document cites; `WP8.2A
  Canonical Object/Relationship Catalogue.md` (remain exactly as they
  are — this architecture generalises their own shape going forward,
  it does not supersede or absorb them); `Contracts/Facets.cs`;
  `Contracts/Relationships.cs`; every discipline's own existing Kind/
  `Classification`/`RelationshipKind` values.

## Validation Against Governing Documents

- **`FOUNDATION.md` non-negotiable 7** ("every decision that was not
  the only reasonable choice is recorded, in writing") — satisfied
  directly by `ADR-0105`.
- **`FOUNDATION.md` non-negotiable 9** (downward-only dependencies) —
  satisfied; this architecture introduces no new project-to-project
  dependency edge. The Engineering Vocabulary Register is a
  documentation artifact, not a code dependency; the consistency test
  reflects over classes its own test project (`Tempest.Desktop.Tests`,
  which already references `Tempest.Core`, `Tempest.App`, and
  `Tempest.Desktop`) already references — no new `ProjectReference` of
  any kind.
- **`FOUNDATION.md`'s own "willingness to document a contradiction
  honestly rather than resolve it silently"** — satisfied by naming
  the confirmed `VerifiedByRelationshipKind` duplicate explicitly,
  rather than fixing it quietly without explaining why the general
  rule was needed.
- **`ADR-0073`/`ADR-0076`** — reaffirmed, unmodified; every stated
  "never validated" guarantee remains true.
- **`ADR-0088`/`ADR-0091`** — reaffirmed, unmodified; both ADRs'
  own `Classification`-as-sub-typing mechanism is generalised as a
  *declaration-location* convention, never altered in what it
  decides.
- **`ADR-0090`** — reaffirmed, unmodified; this architecture
  explicitly preserves its own deliberate choice not to use
  `Classification` for Verification Plan/Activity.
- **Engineering Governance §5** — satisfied; see "Required ADR,"
  above.

## Related Documents

`ADR-0105`; `ADR-0072`; `ADR-0073`; `ADR-0076`; `ADR-0088`; `ADR-0090`;
`ADR-0091`; `ADR-0067`; `ADR-0078`; `ADR-0104` (the identical
"misclassifying a coordination concern as a Platform Service" reasoning
this document's own §4 Alternative reuses); `FOUNDATION.md`;
`docs/releases/v0.11.0/WP11.0A Platform Architecture Review.md`
(Finding `A-6`); `docs/releases/v0.11.0/WP11.0B Architecture
Roadmap.md` §3 (`WP 12.1A`/`WP 12.1B` rows); `docs/releases/v0.8.0/WP8.2A
Canonical Object Catalogue.md`; `docs/releases/v0.8.0/WP8.2A
Relationship Catalogue.md`; `docs/academy/03 Work
Packages/WP12.1A-classification-and-relationship-vocabulary-safety-net-architecture.md`;
`docs/releases/v0.12.0/WorkPackages.md` (`WP 12.1A` row).
