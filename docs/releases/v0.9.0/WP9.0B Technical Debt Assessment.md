# WP 9.0B — Product Configuration & BOM Management — Technical Debt Assessment

## Purpose

Reviews the Technical Debt Register for items this Work Package's own
implementation created, extended, or should have created and did not.

## New Item

### `TD-27` — `InMemoryEngineeringObjectRepository` Iteration Order Is Unspecified; a Test Assumed Otherwise

**What.** `InMemoryEngineeringObjectRepository`'s own backing store is a
`ConcurrentDictionary<Guid, IEngineeringObject>`. `.Values` iteration
order over a `ConcurrentDictionary` is not guaranteed to be insertion
order (or any other stable order) — a fact this Work Package's own first
draft of `MechanicalProductStructureNodeProvider.OrderForBom`'s XML
documentation, and one of its own tests, both incorrectly assumed
("falls back to insertion order").

**How it was found.** A newly-added test
(`GetChildrenAsync_SomeChildrenLackItemNumber_...`) passed reliably in
isolation but failed intermittently when run as part of the full 1738-
test suite — traced directly to dictionary iteration order differing
between runs, not to any product defect.

**Disposition — fixed, not merely disclosed.** The test itself was
corrected to assert membership, not sequence, when no ordering guarantee
exists; `OrderForBom`'s own XML documentation was corrected to state the
true, unspecified-order behaviour rather than a false insertion-order
claim. Re-run four consecutive times after the fix with zero failures.

**Why this is debt, not merely a limitation.** `MechanicalProductStructureNodeProvider`'s
own behaviour when a BOM is only partially numbered — a real, expected
state for an in-progress product structure — is "whatever order the
repository happens to return," which is honest but not especially
useful to a real user browsing a partially-numbered tree.

**Revisit trigger.** A real user complaint about unstable/confusing
ordering for a partially-numbered BOM, or `InMemoryEngineeringObjectRepository`
itself being replaced by an implementation with a real ordering
guarantee worth exposing.

**Disposition.** Open — the test-level and documentation-level
inaccuracies are fixed; the underlying "no stable order for a partially-
numbered BOM" characteristic itself is accepted, not solved, since no
ordering rule was ever specified by the WP's own controlling instruction
beyond "sorting" and "filtering" generally.

## Existing Items Reviewed for Extension or Change

- **`TD-22`/`TD-24`/`WP 9.0A`'s own equivalent finding** (`ListAllAsync`-
  and-filter reads scale with total object count) — the same pattern
  recurs in all five new `IValidationRule`s. Not separately re-
  registered; see `WP9.0B Security Review Report.md`.
- **`TD-26`** (Runtime Host module-initialisation timing) — unaffected
  by this Work Package; the same test-level `HasRegistered` wait
  continues to be sufficient, confirmed by six consecutive full clean
  runs with zero flakes on that dimension.

## Two Findings Fixed, Not Registered as Debt

The `TEMPEST-VAL` code collision (`WP 9.0A`'s own `-002`/`-003`) and the
`ReviseAsync` structural-state loss are **not** registered as Technical
Debt items — both are genuine implementation defects in not-yet-
committed code, fully fixed with regression coverage, not accepted,
ongoing trade-offs. Recorded in `WP9.0B Implementation Report.md` and
`WP9.0B Lessons Learned.md` instead, matching how this project
distinguishes a fixed bug from a disclosed, accepted limitation.

## Items Considered and Not Raised

- **`UnitOfMeasure`/`FindNumber`/`ItemNumber`/`ReferenceDesignator` are
  unvalidated free text** — not newly raised here: already fully
  disclosed and reasoned in `ADR-0083` itself, the more precise,
  permanent record for a deliberate, ADR-ratified design trade-off.
- **Product Variants have no real implementation** — not Technical Debt:
  explicitly out of scope per the WP's own "placeholder architecture
  only" instruction, not an oversight. Recorded in the Future
  Capability Register (`FCR-0044`) instead.

## Verdict

**One new item (`TD-27`), formally registered**, itself already largely
remediated (test and documentation corrected; only the underlying
"no ordering guarantee" characteristic remains open, by design). Two
genuine defects found and fully fixed, not registered as debt. No
existing item's own disposition worsened.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md`; `WP9.0A Technical
Debt Assessment.md` (`TD-26`); `ADR-0083`; `WP9.0B Lessons Learned.md`.
