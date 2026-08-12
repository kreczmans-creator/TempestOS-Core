# ADR-0105: Engineering Classification and Relationship Vocabulary Values Are Declared Once, as Named Constants, and Tracked in a Non-Enforcing Governance Register — Never Inline-Duplicated, Never a Closed or Validated Set

## Status

Accepted — `v0.12.0`, `WP 12.1A` (Classification & Relationship
Vocabulary Safety Net Architecture), 2026-08-12. Architecture only; no
production code accompanies this decision — `WP 12.1B` (already named
in `WP11.0B Architecture Roadmap.md` §3 as this Work Package's own
paired implementation successor) would apply it.

## Context

`WP11.0A Platform Architecture Review.md` Finding `A-6` named a real,
confirmed characteristic of this platform: "Domain classification/
relationship vocabulary is entirely stringly-typed, by deliberate
design, with no compile-time safety net." Its own evidence cites
`ADR-0073`/`ADR-0076`/`ADR-0088`/`ADR-0091`, each independently
choosing an open string over a closed, typed alternative, "to avoid a
combinatorial explosion of per-discipline concrete types." Its own
recommendation was narrow and explicit: *"a lightweight, additive
safety net — e.g. per-discipline `internal static class` string-
constant catalogues, or a build-time check validating classification/
relationship strings against the live registries — would preserve
`ADR-0073`'s open-string contract while catching the failure mode this
pattern is most exposed to."* This Work Package investigates that
recommendation directly, across every shipped discipline, rather than
assuming its shape in advance.

**Four prior ADRs already settle the open-string question itself, and
this decision does not reopen any of them.** `ADR-0072` (every
canonical object is a `Kind` string, never a closed type). `ADR-0073`
(`RelationshipKind` is an open, unvalidated string, never validated
against a target's own `Kind`). `ADR-0076` (`RelationshipCategory` is
descriptive metadata only, never validated against `RelationshipKind`
at write time). `ADR-0088`/`ADR-0091` (Document/Manufacturing
sub-classification is a free-text `Classification` string, a
Workspace-layer `const` catalogue, never a Domain-layer enum) — each
independently, explicitly rejecting a closed enum or per-value
validation, for the identical reason: a closed set cannot scale to an
extensible, module-defined object catalogue without becoming a review
bottleneck. `ADR-0090` separately establishes that `Classification` is
not always the right sub-typing axis — Verification Plan/Activity
deliberately reuses `LifecycleState` instead, "rather than... two
fields for one distinction, never reconciled against each other."

**What none of the five prior ADRs decided: how does a value, once
real, avoid being re-typed by hand at every site that needs it, and
how does a future contributor discover what values already exist
before inventing a colliding or duplicate one?** This is the specific,
still-open gap `A-6` named and this ADR closes.

**Direct evidence, gathered by full-repository investigation, that the
gap is real, not hypothetical:**

- The literal Kind string `"Part"` is written out, independently, at
  14 separate sites across 5 different `src/` files spanning three
  layers (`Tempest.Samples`, `Tempest.App`, `Tempest.Desktop`) — zero
  of them referencing a shared constant, because none exists.
  `MechanicalObjectFactoryRegistry` (the Mechanical discipline's own
  Kind-construction switch) repeats every one of its own eight Kind
  strings three times in the same file (once in its own
  `SupportedKinds` list, once as a switch label, once as the literal
  argument to `EngineeringObjectFactory<T>`'s own constructor) with no
  declared constant anywhere.
- `Tempest.Desktop.DigitalThread.DigitalThreadGraphModel` independently
  declares `private const string VerifiedByRelationshipKind =
  "verifiedBy";` — the identical value, and the identical name, already
  declared as the real, owning constant,
  `Tempest.Core.Verification.VerificationService.VerifiedByRelationshipKind`.
  A second, silent copy of the same fact, in a different layer, is
  exactly the drift risk `A-6`'s own Impact statement described in the
  abstract; this is a concrete, present instance of it, not a
  speculative one.
- Four disciplines (`Requirements`, `Verification`, `Calculations`,
  `Materials` — the original `Tempest.Core` Engineering Foundation
  frameworks) already declare their own Kind and/or relationship-kind
  values as `public const string`, in one owning class each
  (`RequirementsService`, `VerificationService`, `CalculationEngine`,
  `MaterialCatalog`, `RequirementRelationshipKinds`). Two further
  disciplines (`Documents`, `Manufacturing`) already declare their own
  `Classification` sub-values the identical way (`DocumentObjectFactoryRegistry`,
  `ManufacturingObjectFactoryRegistry`, per `ADR-0088`/`ADR-0091`), but
  — inconsistently, within those same two files — their own *base*
  Kind strings (`"Document"`, `"ManufacturingOperation"`, `"WorkInstruction"`,
  `"Inspection"`) are not: those exist only inside a `SupportedKinds`
  array literal, still hand-retyped at every switch-case and factory
  call site. Two disciplines (`Mechanical`, and every purely-structural
  Manufacturing/Documents Kind) declare no constant at all for any of
  their own values.
- A real, working, partial safety net for relationship kinds already
  exists and already proves the shape works: `RelationshipKindCategoryMap`
  (`Tempest.Core.EngineeringDomain.Implementation`) infers a
  `RelationshipCategory` from 17 known conventional `RelationshipKind`
  strings, defaulting non-blockingly to `Reference` for anything
  unrecognised — never throwing, never rejecting a write. The identical
  defensive "recognise what I can, degrade honestly for the rest"
  shape appears independently a second and third time, in
  `DocumentCategory.Of`/`ManufacturingCategory.Of` (`Tempest.App.Workspace`),
  each falling back to an honest `"Uncategorized"` label for an
  unrecognised `Classification` value rather than dropping or
  rejecting the object.
- `WP8.2A Canonical Object Catalogue.md`/`WP8.2A Relationship
  Catalogue.md` (`ADR-0073`'s own cited "resulting platform-wide
  vocabulary") already prove the *documentation-layer* half of this
  same idea works, and have already been kept current well past their
  own originating Work Package (`calculatedBy`/`manufacturedBy`, both
  added by later Work Packages, are both present) — but neither
  catalogue covers `Classification` at all, and neither is a
  continuously-reviewed governance register in the sense
  `Module Register.md`/`Namespace Register.md` already are (no "Last
  Reviewed" discipline, no `Review Frequency` field, no register-health
  check coverage).

## Decision

**Every live Kind, `Classification`, and `RelationshipKind` string
value is declared exactly once, as a `public const string`, inside the
single class that owns it — never re-typed as an inline literal at any
other use site, and never re-declared as a second, independent
constant anywhere else in the codebase.** The owning class is:

- **`Tempest.Core`**, for a value a Domain-layer framework itself
  defines and writes (the existing, already-correct pattern for
  `Requirements`/`Verification`/`Calculations`/`Materials` — unchanged,
  reaffirmed, extended to cover every relationship-kind value those
  four frameworks use that is not yet declared).
- **`Tempest.App.Workspace.{Discipline}`**, for a value only the
  Workspace layer defines (the existing, already-correct pattern
  `ADR-0088`/`ADR-0091` already establish for `Classification` values —
  unchanged, reaffirmed, and extended to also cover each discipline's
  own base Kind strings, not only its `Classification` sub-values, and
  extended to disciplines that today declare no constant at all,
  `Mechanical` most conspicuously).

**Ownership is determined by which component *writes* the value —
whichever class calls `CreateAsync`/`LinkAsync` with that Kind,
`Classification`, or `RelationshipKind` string — never by which
project happens to compile the underlying object's own type.** These
usually coincide (`RequirementsService` both compiles and writes
`Requirement`) but do not always: `VerificationActivity`'s own type is
compiled in `Tempest.Core.EngineeringDomain.Implementation`, yet
nothing there ever constructs one — only
`Tempest.App.Workspace.Verification.VerificationActivityFactoryRegistry`
does, so the Workspace layer, not `Tempest.Core`, is its correct owner
under this rule. This is `ADR-0078`'s own "one Kind, one owner" test
("a canonical object interface is owned by whichever framework
implements it"), applied to where a *value's own declaration* lives
rather than to where a *type* is compiled — the same distinction, one
level down.

**Every declared constant is `public`, deliberately, not `internal` —
a narrower reading than `A-6`'s own illustrative "e.g. per-discipline
`internal static class` string-constant catalogues" suggestion.** The
whole purpose of a single, owning declaration is enabling every other
consumer, in any layer, to reference it directly rather than retyping
it — the exact rule that closes the confirmed
`VerifiedByRelationshipKind` duplicate, where `Tempest.Desktop` needs
to reference a constant `Tempest.Core.Verification` owns. An `internal`
declaration would be invisible outside its own assembly, defeating
that cross-layer reuse entirely and forcing every consumer back to a
local copy — precisely the failure mode this ADR exists to close. This
is not a deviation from `A-6`'s own recommendation, which offered
`internal` only as one illustrative shape ("e.g."), before this
Work Package's own investigation found that every already-shipped
precedent it generalises (`RequirementDocumentKind`,
`VerifiedByRelationshipKind`, `DocumentObjectFactoryRegistry`'s own
`Classification` constants) is already `public`, for this identical
reason.

**Every consumer outside the owning class — including every other
discipline, every `Tempest.Desktop` collaborator, every sample module —
references the owning declaration directly. It never declares its own
local copy of the same value, however small the apparent scope.** This
is the specific, narrow rule that closes the confirmed
`VerifiedByRelationshipKind` duplicate and every future instance of
its exact shape.

**A new governance register, the Engineering Vocabulary Register
(`docs/governance/Engineering/`), lists every live Kind,
`Classification`, and `RelationshipKind` value platform-wide: its
value, the class that declares it as a constant, and a one-line
meaning.** This generalises `WP8.2A Canonical Object/Relationship
Catalogue.md`'s own already-proven shape — extended, for the first
time, to also cover `Classification` — and gives it the same ongoing
review discipline (`Review Frequency`, a "Last Reviewed" narrative
field) every other register under `docs/governance/Engineering/`
already carries, rather than leaving it a one-time `WP 8.2A` deliverable
no register-health mechanism ever checks again.

**A single, additive, non-blocking consistency check — a real test,
not a build-time gate, not a new tool — flags exactly two narrow
conditions: the identical string value declared as a named constant in
two different classes (the `VerifiedByRelationshipKind` failure mode,
caught the moment a second copy is introduced, not discovered later by
accident), and a value present in the Engineering Vocabulary Register
with no declared constant anywhere the reflection scan can find (the
"documentation drifts from code" failure mode).** This directly
realises `A-6`'s own explicit second recommendation ("a build-time
check validating classification/relationship strings against the live
registries") without inventing new tooling — it is an ordinary xUnit
test, discovered and run exactly like every other test in this
platform's own suite, never a separate script or CI gate.

**None of this validates a Kind, `Classification`, or `RelationshipKind`
value at write time, ever.** `ADR-0073`'s "never validated against the
target's own Kind" and `ADR-0076`'s "never validated against
`RelationshipKind` at write time" both remain completely intact. A
caller may still construct an object or a relationship with any string
value at all, including one absent from every constant and every
register entry — this decision is a coordination and discoverability
safety net, never a gate. `RelationshipCategory` (`ADR-0076`) is
unchanged by this decision — it remains a small, closed, seventeen-value
descriptive enum, orthogonal to the open-string values this decision
governs.

## Consequences

**Positive:**

- Closes the exact, confirmed defect this investigation found
  (`VerifiedByRelationshipKind` independently redeclared across two
  layers) by naming the general rule its own specific violation
  falls under, rather than fixing that one instance in isolation and
  leaving the next one to recur undetected.
- Every future discipline Work Package (three more named in
  `WP11.0B Architecture Roadmap.md` §3 alone) inherits a single,
  already-proven convention to follow from its own first line of code,
  rather than each independently rediscovering the "declare it once,
  Workspace-layer, `const string`" shape `ADR-0088`/`ADR-0091` each
  already had to reason through separately.
- The consistency check (component three) turns a silent, discoverable-
  only-by-full-text-search coordination cost — `A-6`'s own named
  Impact — into an explicit, immediate test failure, at zero runtime
  cost and zero new tooling, reusing this platform's own existing
  test-suite-as-safety-net convention (mirroring how `ModuleLifecycleStabilityTests`
  already catches a comparable class of platform-health regression).
- Zero of the five prior ADRs this decision touches (`ADR-0072`,
  `ADR-0073`, `ADR-0076`, `ADR-0088`, `ADR-0091`) requires any
  amendment — every one of their own stated reasons for rejecting a
  closed or validated vocabulary remains exactly as true after this
  decision as before it.

**Negative:**

- A `const string` declaration is still, ultimately, a string — this
  decision cannot and does not prevent a caller from typing a raw
  literal instead of referencing the constant; it relies on code
  review and the consistency check's own narrow scope (exact-value
  collisions and register/constant mismatches only) to catch drift,
  not a compiler-enforced guarantee. A future contributor who ignores
  the convention entirely produces exactly the same defect this
  decision exists to prevent — disclosed, not solved outright.
- The Engineering Vocabulary Register is a new document a future
  Work Package must remember to update — the identical maintenance
  burden `Module Register.md`/`Namespace Register.md` already carry,
  and the identical risk (a register the health-check tooling does not
  yet cover can drift stale) `FCR-0005`'s own already-tracked finding
  names for every comparable register in this platform.
- Retrofitting the two disciplines with the largest, most conspicuous
  gaps (`Mechanical`'s own zero declared Kind constants; the
  `Manufacturing`/`Documents` base-Kind-string gap inside otherwise-
  disciplined files) is real, if small, implementation work this
  architecture-only Work Package does not perform — named directly as
  `WP 12.1B`'s own first, concrete scope item, not left merely implied.

## Alternatives Considered

Recorded in full, with the specific evidence and precedent each
verdict rests on, in `Classification & Relationship Vocabulary Safety
Net Architecture.md`'s own "Alternatives Considered — Full Evaluation"
section: **string literals, unchanged** (rejected — the status quo
this decision exists to improve on, evidenced by the confirmed
duplication above); **a closed enum per vocabulary** (rejected —
already rejected five times, independently, by `ADR-0072`/`ADR-0073`/
`ADR-0076`/`ADR-0088`/`ADR-0091`, for the identical extensibility
reason each time; not re-litigated here); **a strongly-typed value
object wrapping the string** (rejected — solves none of the
duplication problem on its own, since the wrapped value still needs a
single declared source, while adding a new `Tempest.Core` type every
discipline and every future module must adopt); **a runtime,
DI-resolved registry service validating or centrally issuing values**
(rejected as the primary mechanism — architecturally the same shape
`ADR-0073` already rejected for per-Kind-pair relationship validation,
generalised, and would misclassify a documentation/coordination
concern as a Platform Service); **an extensible, fully free-form
metadata model replacing the named `Classification`/`Category`/
`Discipline` facet fields** (rejected — a genuine `IHasMetadata`
contract redesign, directly contradicting every real-discipline Work
Package's own repeated "no contract redesign" constraint, and would
widen rather than narrow the vocabulary-coordination problem).

## Future Considerations

**This ADR governs where a vocabulary value's own declaration lives
and how its existence is tracked — it does not, and is not intended
to, ever become a validation gate.** A future Work Package proposing
that a Kind, `Classification`, or `RelationshipKind` value be
validated at write time is proposing to reopen `ADR-0073`/`ADR-0076`/
`ADR-0088`/`ADR-0091`, not to extend this one — that is a materially
larger, separate architectural question, decided on its own evidence,
never assumed to follow from this decision.

**The Engineering Vocabulary Register's own completeness is
`WP 12.1B`'s responsibility to establish and every future discipline
Work Package's own responsibility to keep current** — the identical
"Review Frequency: updated whenever a new value is introduced"
discipline every comparable register in `docs/governance/Engineering/`
already states for itself.

## Related Documents

`ADR-0072`; `ADR-0073`; `ADR-0076`; `ADR-0088`; `ADR-0090`; `ADR-0091`;
`ADR-0067` (the direct Kind-keyed-registration precedent this
decision's own governance-register component mirrors, at the
documentation layer rather than the runtime layer); `FOUNDATION.md`
non-negotiable 7 (every non-obvious decision recorded in writing) and
non-negotiable 9 (downward-only dependencies — this decision introduces
no new dependency edge between `Tempest.Core`/`Tempest.App`/
`Tempest.Desktop`, only a documentation-layer register and a test);
`docs/releases/v0.11.0/WP11.0A Platform Architecture Review.md`
(Finding `A-6`, the origin of this Work Package); `docs/releases/v0.8.0/WP8.2A
Canonical Object Catalogue.md`; `docs/releases/v0.8.0/WP8.2A
Relationship Catalogue.md`; `docs/architecture/Classification &
Relationship Vocabulary Safety Net Architecture.md` (this ADR's own
full evaluation and evidence); `docs/academy/03 Work
Packages/WP12.1A-classification-and-relationship-vocabulary-safety-net-architecture.md`;
`docs/releases/v0.12.0/WorkPackages.md` (`WP 12.1A` row).
