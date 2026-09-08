# WP 7.1D — Engineering Calculation Framework — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
this Work Package's own implementation and Security Review found —
mirroring `WP7.1A`/`WP7.1B`/`WP7.1C Future Capability Recommendations.md`'s
own format.

## Recommendation 1 — A Future Discipline Module Should Register Its Own Formula as a Single, Focused `ICalculationDefinition`, Never a Multi-Purpose "Calculator" Type

**What.** When a real Mechanical/Structural/Electrical/HVAC capability
is eventually designed, each distinct formula (a beam deflection
calculation, an HVAC duct-sizing calculation) should be its own
`ICalculationDefinition<TInput, TResult>`, registered under its own
`CalculationId`, rather than one large type branching internally on an
input discriminator.

**Why this matters.** This Work Package's own `DoubleLengthCalculationDefinition`
proves the registration/dispatch/recording path works correctly for a
single, focused definition; a multi-purpose type would obscure which
assumptions and constraints actually apply to which formula, undermining
the "engineering evidence" requirement this framework exists to satisfy.

## Recommendation 2 — `FCR-0035` (Execution Cancellation) Should Be Resolved Alongside the First Real, Long-Running Calculation, Not in Isolation

**What.** A future Work Package resolving `FCR-0035` should design
cancellation support against a real, demonstrated long-running
calculation's own needs, rather than adding a `CancellationToken`
parameter speculatively.

**Why not build it now.** No current calculation definition is
long-running; `Calculate`'s own signature has already changed once in
this Work Package (`ADR-0056` Decision 3) — a second, unmotivated
signature change would risk repeated churn for any consumer registered
by then.

## Recommendation 3 — A Future Consumer Needing Guaranteed Intermediate-Result Type Fidelity Should Layer Its Own Typed Wrapper, Not Request a Framework Change

**What.** If a future consumer genuinely needs to deserialize a stored
calculation record's own intermediate results back into their exact
original CLR type, it should encode that type information itself
(mirroring `Materials.MaterialPropertyValueCodec`'s own bounded,
explicit-dimension approach) rather than asking `CalculationContext`
itself to solve general-purpose polymorphic deserialization.

**Why not build it now.** No current consumer reads a calculation
record's own intermediate results back from durable storage — only from
the in-memory record `ExecuteAsync` returns immediately (`TD-22`).

## Recommendation 4 — Candidate H (Verification & Validation) Should Consider Recording That a Specific Calculation Record Satisfied a Requirement, Once Designed

**What.** When Verification & Validation is eventually designed, a
plausible integration is recording a verification outcome against a
`CalculationRecord<TResult>`'s own `Id` (itself an
`EngineeringData.IEngineeringDocument` Id) — reusing the same
document-reference mechanism Materials already demonstrates for
"derivedFrom"-style relationships, rather than inventing a parallel
calculation-to-verification linkage.

**Why not build it now.** Verification & Validation does not exist yet;
this recommendation is forward-looking, not a current gap.

## Not Recommended

- **Adding a hard dependency on `Tempest.Core.Materials` to validate
  material references.** `AT-16` already covers this — an open string
  reference is sufficient absent a real, demonstrated need for
  framework-internal validation.
- **Building a Roslyn-scripting-based purity enforcement mechanism
  ahead of a real, demonstrated convention-only failure.** `ADR-0056`
  already resolved this in favour of convention-only enforcement,
  verified by test; building enforcement infrastructure now would be
  speculative.

## Related Documents

`WP7.1D Implementation Report.md`; `ADR-0056`; `docs/releases/v0.7.0/
WP7.0C Engineering Foundation Contracts.md`; `docs/governance/Quality/
Technical Debt Register.md` (`TD-21`, `TD-22`, `AT-16`); `docs/governance/
Future Capability Register.md` (`FCR-0035`).
