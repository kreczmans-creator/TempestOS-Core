# WP 7.1D — Engineering Calculation Framework — Lessons Learned

## Status

Complete.

## 1. An evidentiary requirement can justify a signature change a contract review could not have anticipated

`WP7.0C`'s own illustrative `Calculate(TInput input)` was a reasonable,
minimal shape for the responsibilities named at contract-review time
(dispatch, purity, a four-field record). Once this Work Package's own
controlling instruction demanded intermediate results, per-execution
constraint checks, and material references — none of which existed as a
concept in `WP 7.0B`/`WP 7.0C` — the original signature had nowhere to
put any of them. The lesson generalises from `WP 7.1C`'s own identical
finding (Materials' property-value type change): a contract reviewed
before a framework's own full evidentiary requirements are known will
sometimes need a signature change at implementation time, and that is a
normal, healthy resolution when the reserved ADR explicitly anticipates
the question, not a failure of the review that preceded it.

## 2. Reusing a sibling framework's own dispatch pattern works, provided the one deciding property is kept explicit

`CalculationEngine`'s own type-erased registration
(`ConcurrentDictionary<string, object>`, cast back via a type pattern)
is structurally almost identical to the Command Framework's own
dispatch shape. What makes Calculation a genuinely different
abstraction, not a renamed Command Framework, is purity — and purity is
not visible in the registration mechanism at all, only in the
documented contract on `Calculate` itself and the concurrency test that
proves it. The lesson: reusing a proven mechanical pattern is safe and
efficient, but the reused pattern is not what makes two frameworks
different — the domain-specific contract riding on top of it is, and
that contract deserves its own explicit test, not an assumption that
"it looks like Commands, so it behaves like Commands."

## 3. A calculation's own true "evidence" value comes from what it discloses, not from its final number

Before writing `CalculationContext`, it would have been easy to treat
`CalculationRecord<TResult>.Result` as the interesting output and
everything else as bookkeeping. Writing
`ExecuteAsync_RecordsIntermediateResults` and
`ExecuteAsync_RecordIncludesDefinitionsOwnAssumptions` made the opposite
clear: a bare `Result` with no attached assumptions, intermediate
steps, or validation outcome is exactly the kind of "hidden engineering
judgement" this Work Package's own controlling instruction named as the
thing to avoid. The record's own value as engineering evidence comes
almost entirely from the surrounding context, not the number itself.

## 4. A dedicated Security Review, run for the first time, earns its own place in future Engineering Foundation Work Packages

Neither the Engineering Review nor the Contract Review had previously
surfaced `TD-21` (no cancellation) or `TD-22` (no bound on recorded
data) — both were found only once the Security Review's own explicit
category list (Resource Exhaustion, Denial-of-Service) was worked
through deliberately, category by category, against the real
implementation. The lesson: a structured checklist review, performed
even when no obvious problem is suspected, finds real, disclosable
findings a general-purpose Engineering Review's own narrower checklist
does not ask about.

## Recommendations

- **Candidate H (Verification & Validation) remains available, but is
  not required before a real discipline module could begin** — see
  `WP7.1D Engineering Core Impact Assessment.md` for the full reasoning.
- **Future Work Packages introducing a new type-erased registry
  (mirroring `CalculationEngine`'s own `ConcurrentDictionary<string,
  object>` pattern) should write an explicit mismatched-signature test**
  (`ExecuteAsync_MismatchedSignature_ThrowsCalculationDefinitionNotFoundException`),
  not merely assume the cast behaves safely.
- **A dedicated Security Review should continue for future Engineering
  Foundation Work Packages**, not only ones whose own controlling
  instruction happens to name it explicitly — this Work Package's own
  findings (`TD-21`, `TD-22`) would very plausibly generalise to a
  future Verification & Validation Work Package's own execution/
  recording model.

## Related Documents

`WP7.1D Implementation Report.md`; `WP7.1D Engineering Review Report.md`;
`WP7.1D Security Review Report.md`; `ADR-0056`; `docs/academy/03 Work
Packages/WP7.1D-engineering-calculation-framework-implementation.md`.
