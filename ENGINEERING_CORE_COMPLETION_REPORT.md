# The TempestOS Engineering Core — Completion Report

**Status: Certified With Accepted Technical Debt (`WP 7.1F`, 2026-07-30).**

This is a permanent historical milestone document, recording why the
Engineering Core exists, what it is, and how it relates to both the
Platform Core beneath it and the Engineering Modules expected to be
built on top of it. It is written once, at the point the Engineering
Foundation programme completed and was certified, and is not expected
to be substantially rewritten afterward — future work extends the
Engineering Core; it does not retroactively change what this document
records about how the first five frameworks came to exist.

## Why the Engineering Core Exists

TempestOS's Platform Services phase (`v0.6.0`) proved this platform can
host cross-cutting, reusable capability — Reporting, Identity &
Permissions, Audit, Settings, Notifications, a REST API, Export/Import,
Licensing — consumed by ordinary modules through the same Discovery,
Registration, and Dependency Injection machinery every module already
uses. `VISION.md` (`WP 7.0A`) named what that platform capability is
*for*: engineering-practice capability, not merely general-purpose
software infrastructure. But no discipline-specific Engineering Module
— a structural calculation tool, a requirements traceability system, a
materials database — can be built directly on Platform Services alone,
because none of Reporting, Identity, Audit, or any other `v0.6.0` service
knows anything about engineering documents, dimensioned physical
quantities, materials, calculations, or verification. Something has to
exist between the general-purpose Platform Core and any real engineering
discipline module: a shared vocabulary and shared mechanism for
representing engineering information itself. That is the Engineering
Core.

`WP 7.0B`'s own Capability Dependency Analysis identified five such
frameworks as architecturally necessary — not five arbitrary features,
but the minimum shared substrate any future discipline module would
structurally require to exist at all:

1. **Engineering Data Model** — stable identity, explicit revision
   history, and typed references for engineering entities.
2. **Units & Quantities** — a dimensioned-value type system preventing
   an entire well-known class of engineering error (unit-conversion
   mistakes) at compile time.
3. **Materials** — a shared material specification catalogue with
   structural provenance.
4. **Calculation** — a shared, pure-function calculation registration
   and dispatch model, producing durable engineering evidence rather
   than a bare numeric answer.
5. **Verification** — a mechanism for recording whether an engineering
   claim has been demonstrated, distinct from Audit and from a
   Calculation Record.

## Architectural Philosophy

The Engineering Core follows the same disciplines the Platform Core
already established, applied one layer up:

- **Reuse over reinvention.** Every framework was checked against
  existing platform mechanisms before inventing anything new. The
  clearest instance: `Verification`'s own history query needed no new
  index at all — it reuses `IEngineeringDocumentStore.LinkAsync`/
  `GetReferencesAsync`, a mechanism `EngineeringData` already provided
  for an entirely different original purpose.
- **One shared foundation, not five independent storage shapes.**
  `Materials`, `Calculations`, and `Verification` all build on the same
  `IEngineeringDocumentStore`, rather than each inventing its own
  identity/revision/reference model — the same "one shared abstraction"
  discipline `ADR-0041` established for Settings and Audit, now applied
  to engineering-domain data.
- **Deliberate, structural avoidance of circularity, not merely
  convention.** `Verification` depends on `EngineeringData`'s generic
  document concept, never on a concrete Requirements Engine type — a
  design choice made specifically so a future Requirements Engine
  (`FCR-0027`) can depend on `Verification` without a circular
  dependency resulting, confirmed unchanged through the entire
  programme's implementation.
- **Explicit exclusions, honoured, not merely stated.** Every framework's
  own controlling instruction named what it must *not* implement
  (Verification: not Validation, not Requirements Management;
  Calculation: no specific formula of its own; Materials: no design
  allowable or safety factor). Every one of the five implementation Work
  Packages independently reported this produced close-to-automatic scope
  discipline — a five-for-five pattern across the entire programme.
- **Evidence over convenience, throughout.** A document revision is
  never overwritten. A calculation's own assumptions travel with its own
  result, not merely its final number. A verification's own criteria and
  evidence are explicit, never an unstated judgement call. Twenty-eight
  Engineering Principles (`docs/engineering/Engineering Principles.md`),
  one set contributed by each framework, together state this discipline
  precisely, each backed by a specific, named test — not asserted in
  the abstract.

## Relationship to Platform Core

The Engineering Core is built *on* the Platform Core, never beside it or
ahead of it. `EngineeringData` depends on `IPersistenceStore` (`WP 6.4`)
and `Identity` (`WP 6.1`); `Materials` additionally depends on
`IPersistenceStore` directly for its own material-lookup index;
`Calculations` and `Verification` depend on `Identity` for attribution.
No Platform Service depends on any Engineering Core framework in return
— confirmed directly, zero circular dependencies exist anywhere between
the two layers (`WP7.1F Engineering Core Architecture Conformance
Report.md`). `Units & Quantities` is the one exception worth naming
explicitly: it depends on nothing at all, Platform Service or otherwise
— a pure, dependency-free mathematical library, deliberately unlike
every other framework in this platform's history.

## Relationship to Future Engineering Modules

No discipline-specific Engineering Module exists yet. The Engineering
Core was built to be consumed by one, not to anticipate what any
specific discipline (Structural, Electrical, Mechanical, HVAC,
Manufacturing) will actually need — `WP 7.0B`'s own Capability
Dependency Analysis deliberately confined itself to cross-cutting
foundation reasoning, never discipline-specific capability invention.
A future Engineering Module is expected to:

- Represent its own domain entities as `IEngineeringDocument`s, gaining
  identity, revisioning, and cross-referencing for free.
- Express every physical quantity as `Quantity<TDimension>`, gaining
  compile-time dimensional safety for free.
- Reference `Tempest.Core.Materials` where a material specification is
  relevant, rather than inventing its own.
- Register its own discipline-specific formulas as
  `ICalculationDefinition<TInput, TResult>`, gaining durable, evidentiary
  execution records for free.
- Record verification outcomes against its own documents through
  `IVerificationService`, gaining traceable, explicit pass/fail/
  conditional evidence for free.

A future Requirements Engine (`FCR-0027`) is `Verification`'s own most
natural next consumer — recording that a requirement has been verified
should call `IVerificationService.RecordAsync` directly against the
requirement's own document Id, never a parallel mechanism.

## Lessons Learned (Programme-Wide)

- **The best design for a cross-cutting framework can be reusing an
  existing mechanism completely, not extending it** — `Verification`'s
  own history query is the clearest proof; the pattern generalises to
  any future framework asking "how do I find every X for this Y," which
  may already be answerable through `GetReferencesAsync` rather than a
  new index.
- **Validating some links while leaving others open is a legitimate,
  asymmetric design when the asymmetry tracks a real dependency
  asymmetry** — `Calculations` and `Verification` each validate a
  document/calculation-record reference (a hard dependency exists) while
  leaving a material reference unvalidated (no hard dependency exists),
  independently, in both frameworks.
- **A closing certification review is not a formality — it recurs the
  exact same value a prior one already proved.** `WP 7.1F` found and
  closed a repeat of `WP 6.8`'s own governance-register-drift finding,
  and a missing Academy concept guide four Work Packages overdue and
  never disclosed. Neither was a defect in the Engineering Core's own
  architecture, security, or test coverage — both were exactly the kind
  of cross-cutting drift a single implementation Work Package's own
  scope cannot be expected to catch in itself.
- **A "five-for-five" scope-discipline pattern**, and now a sixth
  Work Package of a different shape confirming the same discipline
  applies regardless of a Work Package's own type (implementation vs.
  closing review): naming exclusions explicitly, by name, in a
  controlling instruction produces measurably easier scope discipline
  than naming only inclusions.

## Future Recommendations

With the Engineering Foundation programme complete, the next Work
Package is a genuinely open product choice, not an engineering one —
`WP7.1F Engineering Core Certification Report.md` and `WP7.1F Future
Capability Register Review.md` name three candidates with no outstanding
technical dependency: a real, discipline-specific Engineering Module
(proving the five foundation frameworks compose correctly for an actual
domain problem); Platform Hardening work (Candidates `A`–`C`); or design
work toward `FCR-0027` (Requirements Engine), Verification's own most
natural next consumer. This report does not recommend one path over the
others — that choice belongs to Product Approval.

`FCR-0005` (Governance Register Health-Check Tooling) is recommended
ahead of the next multi-Work-Package release phase, having now proven
its own value a second time by catching exactly the drift it was
designed to prevent, twice, without yet being built.

## Related Documents

`VISION.md`; `docs/engineering/Engineering Principles.md`;
`ADR-0053`–`ADR-0057`; `docs/governance/Future Capability Register.md`
(`FCR-0029`–`FCR-0036`); `WP7.1F Engineering Core Certification
Report.md`; `WP7.1F Engineering Core Architecture Conformance Report.md`;
`WP7.1F Engineering Core Consumption Matrix.md`; `WP7.1F Executive
Summary.md`; `WP7.1F Lessons Learned.md`; every `WP7.1x Implementation
Report.md` under `docs/releases/v0.7.0/`; `PROJECT_STATUS.md`.
