# WP 8.2C — Engineering Domain Implementation

## 1. Introduction

`WP 8.2C` is `v0.8.0`'s own ninth Work Package, and its fourth
implementation — the first to give the Engineering Domain
(`WP 8.2A`/`WP 8.2B`) real, compiled, tested code. It follows the
identical two-stage-then-implementation sequence every prior
Engineering Core framework already used (architecture, `WP 8.2A`;
contracts, `WP 8.2B`; implementation, this Work Package), and mirrors
`WP 7.1A`'s own role as the very first implementation of a brand-new
shared foundation.

## 2. Purpose

To prove `WP 8.2B`'s own frozen contracts actually compile and hold
together as running code, and to give every future Engineering
Discipline Module a real, tested, reusable shared implementation to
build on — rather than leaving each one to reinvent identity, metadata,
lifecycle, revision, relationship, traceability, and validation
plumbing independently, the exact fragmentation `WP 8.2A`'s own Core
Principle exists to prevent.

## 3. Background

By the time this Work Package began, `Tempest.Core.EngineeringDomain`
existed only as proposed, uncompiled C# — `WP8.2B Interface
Catalogue.md` and its six companions described ~49 canonical object
interfaces, ten facet interfaces, one relationship interface, and a
full lifecycle/validation/digital-thread contract set, none of it
tested against a compiler. Four sibling frameworks
(`Tempest.Core.Requirements`/`Verification`/`Materials`/`Calculations`)
already demonstrated, independently, that a thin, typed layer over
`IEngineeringDocumentStore` is a working, provable pattern — the
question this Work Package answers is whether that same pattern
generalises across ~49 object shapes at once, through one shared base
rather than four independently-written ones.

## 4. The Problem

1. **How does "no persistence" for this Work Package coexist with
   `ADR-0072`'s own mandate that every canonical object is
   `IEngineeringDocumentStore`-backed** — the same shape of tension
   `ADR-0076` already resolved once at the contract stage, now
   recurring at the implementation stage (`ADR-0077`).
2. **Do the five canonical Kinds an existing framework already
   implements get a second, competing concrete class from the new
   shared framework** — forced by simultaneously being told to
   "implement every canonical Engineering Object class" and to write no
   Requirements/Verification/Calculations logic (`ADR-0078`).
3. **Does "one factory per Kind" mean one hand-written class per Kind**,
   which would mean roughly sixty near-identical types for ~49 objects
   and ~20 relationship kinds (`ADR-0079`)?
4. **How does a shared base class avoid contradicting "composition over
   inheritance"** when that principle was stated, in `WP 8.2A`/`WP 8.2B`,
   entirely in terms of interfaces, and this Work Package is the first
   to introduce a concrete implementation at all?
5. **What does `IEngineeringRelationship` do about the metadata
   `EngineeringData.DocumentReference` was never designed to carry**
   (`Category`, `CreatedByPrincipalId`, `CreatedAt`) — a gap invisible
   at the contract stage, unavoidable the moment real code has to
   populate those members from somewhere?

## 5. The Design

`EngineeringObjectBase` is one shared, concrete class implementing
`IEngineeringObject` and all nine facet interfaces unconditionally —
every concrete Kind class derives from it (directly, or via one
further concrete specialisation mirroring its own interface's single
level of specialisation, e.g. `SubAssembly : Assembly`), declaring only
the specific canonical interface(s) its own Kind actually composes. Its
own real storage — identity, revisions, relationship writes — flows
through an injected `IEngineeringDocumentStore`, satisfying `ADR-0072`
exactly; in production that store is the same, already-registered,
persistence-backed instance every Engineering Core sibling already
shares, introducing zero new persistence (`ADR-0077`). A new, purely
in-memory `IEngineeringObjectRepository`/`IEngineeringRelationshipRepository`
pair is the genuinely new "in-memory repositories" layer this Work
Package's own brief names, answering the one question the document
store cannot: "list every object of Kind X."

Two generic factory types — `EngineeringObjectFactory<T>`,
`EngineeringRelationshipFactory` — construct every one of the 39
concrete object classes and every named relationship kind, each
instance permanently bound to the one Kind/RelationshipKind it was
constructed for (`ADR-0079`), never resolved from a registry
(`WP8.2B Dependency Rules.md` §8). The five already-Implemented
canonical Kinds (`Requirement`, `RequirementCollection`/`Group`,
`VerificationRecord`, `CalculationRecord`, `MaterialSpecification`)
compile as Domain interfaces but receive no competing concrete class —
their ownership stays exactly where `WP 8.2A` already placed it
(`ADR-0078`).

## 6. Alternatives Considered

**A second, in-memory `IEngineeringDocumentStore` registered as the
Host's own production store** — considered and rejected; see `ADR-0077`.
Would silently break every existing Engineering Core framework's own
persistence.

**New concrete classes for the five already-Implemented Kinds**, either
under the same `Kind` strings or new, Domain-prefixed ones — considered
and rejected; see `ADR-0078`. Both risk either data corruption or a
permanently-forked representation of the same real-world concept.

**One hand-written factory class per Kind** — considered and rejected;
see `ADR-0079`. Pure boilerplate proportional to catalogue size, not to
genuine structural variation.

**No shared base class at all — 39 independent implementations of every
facet** — considered and rejected. Would multiply `WP7.1A`-style
per-framework plumbing 39 times over, the exact fragmentation `WP 8.2A`'s
own Core Principle exists to prevent, and would make "composition over
inheritance" a purely aspirational statement rather than one the
implementation actually demonstrates saves real work.

## 7. Why This Solution Was Chosen

It is the smallest implementation that fully honours every one of `WP
8.2B`'s own frozen contracts while introducing zero new persistence
mechanism, zero competing concrete realisation of an already-owned
Kind, and zero unnecessary type proliferation — three separate
temptations a more literal reading of the controlling instruction could
each have produced, each resolved the same way `ADR-0076` was resolved
one Work Package earlier: by distinguishing what the instruction
actually needs from what a literal reading would produce.

## 8. Architectural Principles

- **Composition Over Inheritance** — still governs contracts exactly as
  `ADR-0075` states; `EngineeringObjectBase` is ordinary implementation
  reuse, orthogonal to it, mirroring `ModuleLifecycleBase`'s own
  identical role for modules.
- **Open/Closed** — a fortieth canonical object needs a new concrete
  class and one factory-construction line, never a change to
  `EngineeringObjectBase` or either generic factory type.
- **Honesty over completeness-theatre** — the five already-Implemented
  Kinds are disclosed as out of scope rather than given a hollow,
  half-working concrete class that could never actually be constructed
  safely.

## 9. Files Added

21 files under `src/Tempest.Core/EngineeringDomain/Contracts/`; 24
files under `src/Tempest.Core/EngineeringDomain/Implementation/`
(base class, in-memory store, two repositories, lifecycle table,
validation rule set, reference integrity checker, three digital-thread
services in one class, evidence composer, two generic factories, an
attachment class, three exception types, and eleven family files of
concrete object classes). One new sample module and its command pair
under `src/Samples/Tempest.Samples/`. Three new test files under
`tests/Tempest.Core.Tests/`. `TempestHost.cs` modified to register ten
new shared services. `ClockModuleDiscoveryTests.cs` modified for the
new sample module count (21 → 22).

## 10. Trade-offs

- Two `IEngineeringDocumentStore` implementations now exist —
  disclosed, not hidden (`ADR-0077`).
- `IRequirement`/`IVerificationResult`/`ICalculationResult`/`IMaterial`
  compile but cannot currently be constructed by anything in this
  namespace (`ADR-0078`).
- `RelationshipCategory` inference for a relationship kind this
  framework did not itself create (`RelationshipKindCategoryMap.InferCategory`)
  is a best-effort, disclosed convention lookup, defaulting to
  `Reference` — the same trade-off `ADR-0073`/`ADR-0076` already
  accepted for `RelationshipKind`/`Category` generally, now applied to
  one more inference site.
- `IValidationRuleSet` enforces zero Kind-specific rules today —
  correct for a shared framework with no discipline logic of its own,
  but means `ValidateAsync` is currently a no-op for every object,
  disclosed rather than silently implied to do more than it does.

## 11. Common Mistakes

Assuming "implement every canonical Engineering Object class" requires
duplicating the five Kinds an existing framework already owns. It does
not — `ADR-0078` disclosed exactly why duplicating them would itself
have been the discipline-specific logic this Work Package's own
controlling instruction explicitly forbade. A second mistake worth
naming: treating `WP8.2B Interface Catalogue.md`'s own `IRelease :
IBaseline : IConfiguration` chain as license to write similarly deep
concrete-class hierarchies elsewhere — it is compiled here exactly as
frozen and disclosed as a `WP 8.2B`-era authoring inconsistency against
that same document's own Dependency Rules §6, not treated as a pattern
to repeat.

## 12. Future Evolution

1. **A real Physical/Configuration Engineering Discipline Module**
   built directly on the now-compiled `IAssembly`/`ISubAssembly`/`IPart`/
   `IComponent` classes — the most natural, and now most concrete,
   next Work Package.
2. **Reconciling the five already-Implemented Kinds** (`ADR-0078`) —
   retrofitting the real `Requirement`/`VerificationRecord`/
   `CalculationRecord`/`MaterialSpecification` classes to additionally
   implement their own Domain facet interfaces, once a genuine consumer
   needs it.
3. **Registering real validation rules** against the now-working,
   currently-empty `ValidationRuleSet`, once a discipline module has a
   genuine rule to enforce.
4. **Rebuilding the in-memory repository from the real store on Host
   startup** (`ADR-0077`'s own disclosed gap).

## 13. Key Takeaways

1. A shared concrete base class and "composition over inheritance" are
   not in tension — one governs contracts, the other governs
   implementation reuse, and confusing the two would have cost 39
   independent reimplementations of the same nine facets.
2. Not every named canonical object needs a new concrete class — five
   of forty-nine already have a perfectly good one, and the discipline
   is knowing which five, not building all forty-nine regardless.
3. A contract gap (`IRevisionRecord`, referenced but never defined) is
   only found by actually trying to implement the contract that
   references it — the strongest evidence yet that this project's
   own two-stage architecture-then-contracts-then-implementation
   discipline catches real gaps a contract-only review cannot.
4. Two generic types, each instantiated many times, can satisfy a
   "one per Kind" rule just as literally as sixty hand-written types
   would — and far more maintainably.

## Architectural Debt Assessment

**Zero new Technical Debt items raised.** Every genuine limitation this
Work Package's own implementation surfaced is disclosed as an ADR
consequence or a Future Evolution item, not silently absorbed — see
`WP8.2C Engineering Domain Implementation Report.md`'s own Technical
Debt Assessment section for the complete, itemised account.

## Observations

The single strongest confirmation this Work Package produced: every
one of `WP 8.2A`'s and `WP 8.2B`'s own architectural bets — Kind-backed
identity, open-string relationships, composed facets, one generic
relationship type — held up under an actual compiler and an actual test
suite, not merely under review. Nothing about implementing 39 concrete
classes against ten facet interfaces required reopening any of the six
prior ADRs this Work Package builds on; every tension this Work Package
itself found (`ADR-0077`–`ADR-0079`) was a genuinely new question those
six ADRs had not yet had occasion to answer, not a defect in them.

## Related Documents

`docs/releases/v0.8.0/WP8.2C Engineering Domain Implementation
Report.md`; `ADR-0072`–`ADR-0079`; `docs/academy/02 Runtime
Architecture/18-engineering-domain-architecture.md`;
`docs/academy/03 Work Packages/WP8.2A-engineering-domain-architecture.md`;
`docs/academy/03 Work Packages/WP8.2B-engineering-domain-contracts.md`.
