# WP 6.4 — Settings Framework — Lessons Learned

## 1. A Brief's Own Deliverable List Is Not Automatically Approved Scope

The implementation brief named "User settings" and a "Strongly typed
settings abstraction" as deliverables "where defined by the approved
architecture" — and neither actually was defined there. Reading the
qualifier literally, rather than treating the deliverable list as a
blanket authorization, meant this Work Package built exactly what the
architecture approved and disclosed the gap explicitly, rather than
inventing new public contracts on the spot to satisfy a longer-sounding
deliverable list. **Lesson: when a brief's own deliverable list exceeds
what the approved design actually specifies, the approved design wins —
check the qualifier, don't assume every named item is pre-authorized.**

## 2. A Shared Internal Utility Is Worth Extracting the Moment a Second Real Consumer Appears

`AsyncKeyedLock` was first needed for `PersistenceStore`. Writing
`SettingsProvider` immediately afterward surfaced the identical need
(serialize a cache-populate-or-write sequence per key). Rather than
duplicating a nearly-identical class in two namespaces, it was promoted
to a small, neutral, internal namespace once — exactly at the point a
genuine second consumer existed, not preemptively and not left
duplicated. **Lesson: "don't generalize before a second use case" is not
an excuse to duplicate once that second use case has actually arrived —
it's a trigger to promote.**

## 3. An Explicit Default Named in a Prior Document Should Be Implemented, Not Reconsidered

The Contract Review explicitly pre-decided two questions it flagged as
"WP 6.4's own architecture phase should settle" — cache-then-invalidate,
and always-publish-even-for-a-no-op-write — by stating its own default
opinion in the same breath. Implementing exactly that default, rather
than treating the "should settle" language as an invitation to relitigate,
kept this Work Package's own decisions traceable to a document written
before implementation began. **Lesson: a hedge that includes its own
stated default is a decision, not an open question — implement the
default unless a genuine defect makes it impossible.**

## 4. Testing a Real Failure Is More Convincing Than Testing a Simulated One

`PersistenceStoreTests` forces genuine OS-level failures — an open file
handle blocking a read/write/delete, a file occupying the exact path a
directory needs — rather than injecting a fake exception through a test
double. This caught nothing wrong in this case, but it means the
`PersistenceStoreUnavailableException` path is proven reachable in
practice, on this actual platform, not merely reachable in a mocked
approximation of it. **Lesson: when a real failure is cheap to force
(a file lock costs one extra `FileStream`), prefer it over a fake one —
the extra confidence is close to free.**

## 5. Declining to Change an Approved Interface Is Itself a Recordable Decision

Not adding an `IsSensitive` flag to `ISettingDefinition` could have been
treated as simply "not doing something," with no record. Writing it up
explicitly in `ADR-0042` — with the actual reasoning (no real sensitive
setting exists yet; adding the member now would be a speculative
interface change) — means a future Work Package that *does* have a real
sensitive setting will find the reasoning already laid out, rather than
wondering whether the omission was an oversight. **Lesson: a deliberate
"not now" on an approved interface deserves the same documentation rigor
as a deliberate "yes, and here's the ADR" would.**

## Related Documents

`WP6.4 Implementation Report.md`; `WP6.4 Engineering Review Report.md`;
`WP6.4 Technical Debt Assessment.md`; `WP6.4 Future Capability
Recommendations.md`; `docs/academy/03 Work Packages/
WP6.4-settings-framework-implementation.md`; `ADR-0041`; `ADR-0042`.
