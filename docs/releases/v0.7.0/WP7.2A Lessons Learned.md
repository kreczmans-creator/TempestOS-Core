# WP 7.2A — Strategic Roadmap Selection & Programme Architecture — Lessons Learned

## Status

Complete.

## 1. A repository can already contain most of the answer to "what's next," if it is read as a whole rather than department by department

Every individual input this Work Package reviewed — `Future Capability
Register.md`, `Product Roadmap.md`, `VISION.md`, `WP7.0B Engineering
Discipline Assessment.md` — had already, independently, converged on the
same conclusion (Systems Engineering is the only Engineering Discipline
with real evidence behind it; five others have none). No single one of
those documents was written to answer this Work Package's own question
directly, and none needed to be — reading them together made the answer
nearly self-evident before any new scoring was performed. The lesson
generalises: a strategic roadmap review's own first move should be
reading what already exists as a whole, not generating new analysis
before checking whether the repository has already produced it in
pieces.

## 2. A stated vision and an actual practice can diverge, and the honest response is to name the divergence, not silently pick a side

`Product Roadmap.md`'s own Phase 4 "working premise" called for Platform
Hardening before Engineering Modules; `WP 7.0B` built Engineering
Foundation frameworks instead, and that choice is now certified as sound.
`VISION.md`'s own Long-Term Objective 2 states a "readiness" bar
(authentication, trust-boundary closure, governance tooling) that this
recommendation does not fully satisfy either. Both tensions were real,
both are named explicitly in this Work Package's own deliverables
(`WP7.2A Strategic Roadmap Review.md` §4, `WP7.2A Recommended
Programme.md`'s own "Why Not Programme F" section), and both are
resolved with stated reasoning rather than quietly choosing whichever
reading was more convenient for the recommendation this review reached.
A future reader disagreeing with this review's own resolution has
everything needed to see exactly where the disagreement would need to
begin.

## 3. "Cannot be sequenced from existing evidence" is a legitimate, reusable finding — it does not need to be re-derived from scratch every time it still applies

`WP7.0B Engineering Discipline Assessment.md` reached this conclusion
for five Engineering Discipline categories one Work Package (and five
implementation Work Packages) ago. This review re-checked it directly
rather than assuming it still held, and found it unchanged — no
document produced since named a Mechanical, Structural, Electrical,
Building Services/HVAC, or Manufacturing capability. Confirming a prior
finding still holds is cheap; the lesson is that it is still worth doing
explicitly (a fresh `grep`, a fresh read) rather than citing the prior
finding on faith, exactly as `WP 6.8` and `WP 7.1F` both already
established for governance-register claims specifically.

## 4. Scoring only clarifies a decision that is already well-supported by qualitative evidence — it does not substitute for that evidence

The eleven-criterion scoring matrix (`WP7.2A Programme Comparison
Matrix.md`) produced a clean, defensible ranking, but every individual
score was itself derived from a specific, cited fact, not assigned by
impression. A scoring framework applied without that discipline would
have produced a plausible-looking table backed by nothing — the
Sensitivity Check section exists specifically to show the ranking
survives even a deliberately generous re-scoring of the second-place
programme, which would not be a meaningful check if the underlying scores
were not each independently defensible in the first place.

## 5. Recommending a programme "second" is a genuine, real commitment, not a polite way of declining it

Programme F (Platform Hardening) was not rejected — it was sequenced
second, with each of its own triggers named explicitly as actively
monitored, and with `WP7.2A Candidate Work Package Catalogue.md`
carrying `WP7.0B Candidate Work Package Catalogue.md`'s own Candidates
A–C forward unmodified rather than requiring them to be re-derived when
their own turn comes. The lesson: sequencing a strong candidate second
is only an honest recommendation if the deliverables actually preserve
enough of that candidate's own evidence and scope for a future Work
Package to pick it up cheaply — not merely a soft "no" dressed as a
"later."

## Recommendations

- **Build `FCR-0005` as part of, or immediately alongside, whatever
  Work Package eventually executes Programme F** — this review's own
  reading of the repository confirms the tooling gap it addresses is
  now a three-time-confirmed pattern, not a single observation.
- **The Requirements Engine's own architecture phase (Candidate K)
  should explicitly revisit `WP7.0B Roadmap Risk Register.md`'s own
  `RR-1`** before considering the Engineering Foundation's own design
  final — this review carries that risk forward without resolving it,
  deliberately.
- **A future capability-identification exercise for the five empty
  Engineering Discipline categories should be scheduled as its own,
  explicit future initiative** — not assumed to happen automatically
  once Programme A ships, and not treated as blocked by it either; the
  two can proceed independently once a real stakeholder engagement is
  possible.

## Related Documents

`WP7.2A Executive Summary.md`; `WP7.2A Strategic Roadmap Review.md`;
`WP7.2A Recommended Programme.md`; `WP7.2A Programme Comparison
Matrix.md`; `WP7.0B Engineering Discipline Assessment.md`; `WP7.0B
Roadmap Risk Register.md`; `docs/governance/Future Capability
Register.md` (`FCR-0005`).
