# WP 6.7 — Export/Import — Lessons Learned

## 1. When an Approved Interface's Own Signature Cannot Support a Requirement the Same Contract Names Elsewhere, Reuse a Proven Resolution Pattern Before Inventing a New One

`IImportService.ImportAsync(Stream source, CancellationToken)`'s own
approved shape carries no destination parameter, yet the same approved
contract requires reading a multi-source artifact back into more than
one owning service. It would have been easy to treat this as license to
add a destination parameter to the approved method, or to invent a
brand-new registration mechanism from scratch. Instead, `ADR-0044`'s own
`CurrentPrincipalAccessor` dual-registration precedent — solved once,
for a structurally identical problem ("a privileged registrant needs a
capability the approved public interface deliberately does not
expose") — was recognised and reapplied directly. **Lesson: before
designing a new resolution mechanism, check whether this codebase has
already solved the same *shape* of problem somewhere else, even if the
concrete need looks superficially different.**

## 2. Verify What a Container Actually Supports Before Designing Around an Assumed Capability

The obvious-seeming design for "route each artifact section to its own
handler" is to let the DI container resolve `IEnumerable<IImportable>`
and match by some property — a pattern familiar from more featureful
containers. Before committing to it, `TempestServiceProvider`'s own
source was read directly: registrations live in a single
`Dictionary<Type, ServiceDescriptor>`, exactly one entry per service
type, a second registration under the same type silently overwriting
the first. No collection-resolution mechanism exists at all. **Lesson:
a familiar pattern from other ecosystems is not a safe default — verify
the actual container's own resolution model before designing around a
capability it may not have.**

## 3. Two Failure Modes That Sound Similar in Prose Can Still Need Two Different Exception Types

"This artifact's version isn't supported" and "this artifact isn't
well-formed at all" both sound, at first glance, like flavours of "this
import failed." But a caller needs to react to them differently — a
version mismatch is recoverable information (retry against a different
platform version, or report a specific incompatibility to a user); a
corrupted file is not (the artifact itself needs to be re-obtained, not
merely re-tried). The approved contract's own Testing Requirements
independently named "Corrupted file tests" as its own category,
confirming this distinction mattered to the original design, not just
to this Work Package's own implementation-phase judgment. **Lesson:
when a testing requirement names two categories separately, that is
itself a signal the underlying failure modes are meant to stay
distinguishable to a caller, not merely internally.**

## 4. Deliberately Declining an Integration Is Itself a Documented Decision, Not a Gap

Persistence and Reporting were both considered as candidate integration
points and both deliberately rejected — the first because `ADR-0051`'s
own orthogonality decision forbids it structurally, the second because
`ADR-0040`'s own round-trip-safety disclosure makes it directly
contradictory. Neither omission needed discovering during review; both
were reasoned through and written down during design, before a single
line of implementation code existed. **Lesson: "we considered this and
rejected it for a stated reason" is exactly as valuable a piece of
documentation as "we built this and here's why" — both prevent a future
maintainer from re-litigating a question that already has an answer.**

## 5. A Governance Register's Own "Coverage Status: Complete" Line Can Silently Survive Multiple Work Packages Past the Point It Stopped Being True

`Interface Register.md`, `Dependency Injection Register.md`, and
`Module Register.md` had each gone stale since `WP 5.2` — missing every
public interface, DI registration, and sample module six subsequent
Work Packages introduced — yet each register's own metadata continued
to read "Coverage Status: Complete" the entire time, because nothing
forced a re-check unless a Work Package happened to touch that specific
register directly. This mirrors `WP 6.3`'s own `Hosted Services
Register.md` finding, but at six times the scale. **Lesson: a register
whose own Review Frequency depends on "whenever X changes" needs an
active, periodic cross-check against the current file system, not just
reactive updates from whichever Work Package happens to remember to
touch it — exactly the kind of check `WP 6.8`'s own closing audit exists
to perform systematically, rather than leaving it to chance which
future Work Package's own repository review happens to notice.**

## Related Documents

`WP6.7 Implementation Report.md`; `WP6.7 Engineering Review Report.md`;
`WP6.7 Platform Integration Demonstration.md`; `WP6.7 Platform Impact
Assessment.md`; `WP6.7 Technical Debt Assessment.md`; `WP6.7 Future
Capability Recommendations.md`; `docs/academy/03 Work
Packages/WP6.7-export-import-implementation.md`; `ADR-0044`; `ADR-0051`.
