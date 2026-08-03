# WP 7.2B — Requirements & Verification Platform Architecture — Lessons Learned

## Status

Complete.

## 1. A cross-cutting foundation's own design discipline generalises to a second layer without needing reinvention

Every architectural decision this Work Package made was resolved by
asking "which existing Engineering Core precedent already answers this,"
not "what is the best abstract design." The business-identifier index
(`MaterialCatalog`'s own `materialId` pattern), the open-reference
convention for unvalidated dependencies (`AT-16`/`AT-17`), the
calling-layer composition of Identity/Audit (`IReportingService`'s own
precedent), and the direct reuse of `LinkAsync`/`GetReferencesAsync` for
traceability (`Verification`'s own `ADR-0057` finding) — all four were
available, proven, and directly applicable before this Work Package
began. The lesson generalises past the Engineering Foundation programme
itself: a genuinely reusable architectural pattern, once proven once,
keeps answering the identical question at the next layer up, without
needing to be re-derived from first principles.

## 2. Discipline-neutrality is enforced by refusing to resolve one specific tension, not by adding abstraction

The single hardest design decision in this architecture — how a
Requirement Allocation target is represented — was resolved by
deliberately *not* choosing a concrete type. Modelling the target as
"either a reference to any `IEngineeringDocument`, or an open string
when none exists yet" looks, superficially, like avoiding a decision.
It is in fact the decision: any more specific choice (a typed
`IAllocationTarget` interface, for example) would have required
inventing what an allocation target *is*, which is exactly the
discipline-specific behaviour this Work Package's own controlling
instruction forbids. The lesson: sometimes the correct architectural
answer to "what type is this" is "deliberately underspecified, and here
is exactly why," not a plausible-sounding invented type.

## 3. An architecture-only Work Package can still find, and disclose, a genuine new gap

`WP7.2B Security Architecture.md`'s own "Concurrent editing" finding (no
compare-and-swap check exists on `ReviseAsync`) was not previously
disclosed by any Engineering Foundation Work Package, because no prior
Engineering Core consumer had a real multi-author collaborative-editing
profile to surface it. This Work Package found it purely by asking "what
does *this* consumer's own usage pattern stress that no prior consumer
did" — a genuinely new question, not a re-derivation of an existing one.
The lesson: a new consumer of a shared foundation is itself a security-
relevant event, worth a fresh review pass, even when the foundation
itself has already been through two dedicated Security Reviews.

## 4. Reviewing seven illustrative standards together, rather than one at a time, surfaces the generalisable capability faster

`WP7.2B Standards Mapping.md` found the same four generic capabilities
(traceability, baseline management, independent verification, evidence
retention) recurring across all seven named standard families before
any standard-specific detail was considered. Reviewing them together,
rather than mapping one standard fully before starting the next, made
the shared pattern visible almost immediately — the same efficiency
`WP7.2A Strategic Roadmap Review.md` found in reading seven candidate
programmes together rather than sequentially.

## 5. "No principle extension" is sometimes the evidence-disciplined answer, and saying so explicitly matters

`docs/engineering/Engineering Principles.md`'s own governing rule —
principles are derived from real, shipped code, never asserted in
advance — meant this Work Package could not honestly add a Systems
Engineering principle, having produced no implementation. Stating this
explicitly in `WP7.2B Academy Plan.md`, rather than silently skipping
the section, keeps the document's own review checklist complete and
gives the owning implementation Work Package a clear, deliberate
starting point rather than an unexplained gap.

## Recommendations

- **The owning implementation Work Package should resolve `WP7.2B
  Required ADR Catalogue.md`'s own three reserved decisions
  (`ADR-0058`–`ADR-0060`) before writing any production code**, mirroring
  every Engineering Foundation implementation Work Package's own
  identical discipline.
- **`ADR-0060`'s own concurrent-editing question should be resolved with
  real evidence of the Requirements Platform's own actual usage
  pattern**, not decided speculatively at architecture time — this
  Work Package deliberately declines to pre-empt that evidence.
- **The new concept guide `WP7.2B Academy Plan.md` recommends should be
  written once real, tested code exists to derive its own worked
  examples from** — not drafted ahead of implementation, consistent
  with every existing Academy concept guide's own origin.

## Related Documents

`WP7.2B Requirements Platform Architecture.md`; `WP7.2B Systems
Engineering Architecture.md`; `WP7.2B Security Architecture.md`; `WP7.2B
Standards Mapping.md`; `WP7.2B Required ADR Catalogue.md`; `WP7.2B
Academy Plan.md`; `docs/academy/03 Work Packages/
WP7.2B-requirements-and-verification-platform-architecture.md`.
