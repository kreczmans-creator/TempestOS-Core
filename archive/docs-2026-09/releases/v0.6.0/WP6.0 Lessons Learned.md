# WP 6.0 — Reporting Framework — Lessons Learned

## 1. Not Every Brief-Named Noun Maps Onto a New Type

The brief named "Report model," "Report metadata," "Report builder,"
and "Report generation pipeline" as implementation scope — none of
these required a new type beyond what `Public Interface Catalogue.md`
already drafted. "Report model" is `IReportDefinition`/`ReportRequest`/
`ReportResult`; "Report metadata" is `IReportDefinition.Id`/`Name`;
"Report builder" is `RegisterDefinition` itself (the mechanism by which
a report is built up into the registered catalogue); "Report generation
pipeline" is `GenerateAsync`'s own dispatch flow. Only "Template
abstraction" required a genuinely new, additive type. **Lesson: when a
brief lists several nouns as scope, check each against the already-
approved interface catalogue before assuming each needs its own new
type — some brief-named concepts are simply a different name for
something already fully specified.**

## 2. An Orthogonality Decision Protects a Sibling Work Package's Own Future Design Space, Not Just the Current One

`ADR-0040`'s own decision not to build an "Export abstraction" — despite
the brief naming it as scope — was motivated as much by protecting `WP
6.7` (Export/Import)'s own freedom to design its own versioned contract
from scratch as by Reporting's own restraint. Building even a partial
export interface now would have created a precedent `WP 6.7` would then
need to either adopt or explicitly reject, narrowing its own design
space before that Work Package has even begun. **Lesson: an
orthogonality ADR between a current Work Package and a not-yet-started
sibling is not just "don't build this here" — it's actively preserving
the sibling's own future ability to make its own design decisions
unconstrained by premature groundwork.**

## 3. Cross-Service Integration Can Be Demonstrated Without Touching the Service Itself

Every prior Work Package's own cross-service integration this release
(Settings depending on Persistence and the Event Bus; Audit depending
on Persistence and Identity) built the dependency directly into the
service's own constructor. This Work Package took a different, equally
valid approach: `IReportingService` itself has zero dependency on
Identity, Settings, Audit, or Notifications, and every integration is
demonstrated at the sample module's own calling layer instead. Both
approaches are legitimate — the difference is whether the *service*
needs the dependency to do its own job (Audit needs Persistence to
store records) or whether only a *specific caller's own use case* needs
it (report generation doesn't need Identity to run; a specific,
permission-gated consumer of report generation does). **Lesson: before
wiring a cross-service dependency directly into a new service's own
constructor, ask whether the *service* needs it or only a *specific
calling pattern* needs it — the two have very different answers to "is
this dependency justified."**

## 4. A Notification's Own Payload Discipline Must Be Actively Checked, Not Assumed

`Platform Service Contracts.md`'s own Notification Framework Security
Considerations named the exact "report is ready" scenario this Work
Package builds, months before this Work Package began — a warning that
a report-ready notification must not leak content. It would have been
easy to include a content summary or byte count in the published
notification "for convenience," since that information is readily
available at the point of publishing. This Work Package's own handler
deliberately withholds it, returning the full result only to the
command's own direct caller instead. **Lesson: a security consideration
named in an approved contract months before implementation begins is
easy to forget by the time the actual code is being written — checking
the approved contract's own text at the moment of writing the
integration code, not relying on memory of having read it once, is what
actually prevents the leak.**

## 5. A Clean First Attempt Is Still Worth Verifying, Not Just Assuming

`WP 6.2`'s own exact-static-type-dispatch defect was a genuine surprise
found only through integration testing. This Work Package's own
cross-service integration tests passed on first attempt — but that
success was itself verified by writing the same category of test (a
real subscriber, a real publisher, wired through the real dispatcher)
rather than assumed because "this Work Package doesn't use generic
dispatch, so it should be fine." **Lesson: the absence of a known
failure class in a new design is a hypothesis, not a fact, until the
same category of test that would have caught it in a prior Work
Package is actually run against the new one.**

## Related Documents

`WP6.0 Implementation Report.md`; `WP6.0 Engineering Review Report.md`;
`WP6.0 Platform Integration Demonstration.md`; `WP6.0 Platform Impact
Assessment.md`; `WP6.0 Technical Debt Assessment.md`; `WP6.0 Future
Capability Recommendations.md`; `docs/academy/03 Work
Packages/WP6.0-reporting-framework-implementation.md`; `ADR-0040`.
