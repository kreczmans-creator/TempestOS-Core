# WP 7.1F — Engineering Core Integration Review & Certification — Lessons Learned

## Status

Complete.

## 1. A designated closing-review Work Package still needs to be scheduled, or the gap it exists to close keeps growing

`WP 6.8` fully backfilled `Interface Register.md`, `Dependency Injection
Register.md`, and `Module Register.md` for `v0.6.0`, and `FCR-0005`
(Governance Register Health-Check Tooling) was raised specifically to
stop the same drift from recurring undetected. It recurred anyway,
across all five Engineering Foundation Work Packages, because no closing
review of this phase existed until now. The lesson is not that `WP 6.8`'s
own backfill was wrong — it is that a governance register's own
correctness has a half-life bounded by how long it goes between closing
reviews, not by how thoroughly it was last backfilled. `FCR-0005`'s own
priority is raised from Medium to High as a direct result: three
recurrences of the identical pattern (`v0.5.0`-era, `v0.6.0`, now the
Engineering Foundation programme) is a confirmed failure mode, not
residual risk.

## 2. A concept guide named in a plan is not the same thing as a concept guide that exists

`WP7.0C Academy Plan.md` named the Engineering Data Model's own concept
guide as the single highest-priority piece of new Academy content this
entire programme would produce. It was never written, and — more
notably — its absence was never disclosed by `WP 7.1A` or by any of the
four Work Packages that subsequently built directly on the framework it
would have explained. A plan naming a deliverable is a commitment, not
evidence the deliverable exists; only a closing review that checks the
file system directly, rather than trusting a prior Work Package's own
retrospective, catches the gap between the two.

## 3. Reviewing five frameworks together surfaces a finding no single framework's own review could produce

Two independent frameworks (`Calculations`, `Verification`) each,
separately, chose not to validate a material reference — the identical
design boundary, reached twice, for the identical reason. Reviewed in
isolation, each looked like an ordinary, well-justified scope decision.
Reviewed together, the repetition is itself evidence the boundary is
principled rather than accidental — a form of corroboration a
single-framework review has no way to produce, since it has nothing to
compare against. The same cross-framework vantage point also surfaced
`TD-18`'s own quietly increased relevance: disclosed by the first
framework to use `LinkAsync`, now load-bearing for a fourth consumer,
in a way no individual framework's own review would have had reason to
re-examine.

## 4. "Zero new production code" is a real constraint, and real findings still fit inside it

This Work Package's own controlling instruction permitted production
code only for a genuine certification-blocking defect — none was found.
Both genuine findings this review produced (the register drift, the
missing concept guide) were fully closed anyway, because both are
documentation and governance-register corrections, not `src/` changes.
The lesson generalises from `WP 6.8`'s own identical experience: a
closing review's own value does not depend on writing code — it depends
on checking claims against the file system directly and fixing what
does not match, which is available regardless of the "no production
code" constraint.

## 5. Five-for-five scope discipline, extended to a sixth Work Package of a different shape

Every implementation Work Package in this programme (`WP 7.1A`–`WP 7.1E`)
independently reported that explicitly-named scope exclusions produced
close-to-automatic scope discipline. This Work Package's own controlling
instruction named an equally explicit exclusion — no production code
unless a genuine defect is found — and it held here too, for a
Work Package whose own shape (a whole-programme audit, not a single
framework's implementation) differs from every one of the five it
reviewed. The pattern generalises beyond "implementation Work Packages
respect named exclusions" to "any Work Package respects named
exclusions, regardless of its own shape."

## Recommendations

- **Build `FCR-0005`** before the next multi-Work-Package release phase
  begins — three recurrences of the identical governance-register-drift
  pattern is enough evidence to justify the tooling investment `WP 6.8`
  itself first proposed only tentatively.
- **The next Work Package that begins a new, multi-Work-Package
  programme should schedule its own closing certification review in
  advance**, rather than leaving it to be discovered as needed — this
  Work Package existed only because the user's own controlling
  instruction requested it explicitly, not because any prior Work
  Package's own Future Capability Recommendations named it as required.
- **When an Academy Plan names a required concept guide, its owning Work
  Package's own Definition of Done review should check the file exists**,
  not merely that a retrospective was written — the same file-system-
  direct verification discipline this Work Package applied to
  governance registers applies equally to Academy deliverables.

## Related Documents

`WP7.1F Executive Summary.md`; `WP7.1F Engineering Core Certification
Report.md`; `WP7.1F Engineering Core Architecture Conformance Report.md`;
`docs/governance/Future Capability Register.md` (`FCR-0005`);
`ENGINEERING_CORE_COMPLETION_REPORT.md`; `docs/academy/03 Work Packages/
WP7.1F-engineering-core-integration-review-and-certification.md`.
