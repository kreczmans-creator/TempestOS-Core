# WP 6.0 — Reporting Framework — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
`WP 6.0`'s own implementation found, mirroring `WP6.1`/`WP6.4`/`WP6.5`/
`WP6.2 Future Capability Recommendations.md`'s own format.

## Recommendation 1 — `WP 6.7` (Export/Import) Should Design Its Own Contract Independently, Using `ReportResult` Only as a Plausible Input

**What.** When `WP 6.7` begins, it should design Export/Import's own
versioned, round-trip-safe contract from first principles — not as an
extension of `Tempest.Core.Reporting`. A generated `ReportResult`
(`ContentType`, `Content`) is one plausible data source `IExportService`
might accept, exactly as Settings (`WP 6.4`) already is, but Reporting
itself should never gain export-specific members.

**Why this matters.** `ADR-0040`'s own orthogonality decision exists
precisely so `WP 6.7` retains full freedom to design its own contract
without inheriting assumptions from a Work Package that deliberately
declined to anticipate it.

## Recommendation 2 — Any Future Renderer Needing a Different Output Format Should Implement `IReportTemplate<TDefinition>` Directly, Not Modify `PlainTextReportTemplate<TDefinition>`

**What.** An HTML, PDF, or CSV renderer should implement its own
`IReportTemplate<TDefinition>` (or its own `IReportRenderer<TDefinition>`
directly, bypassing the template abstraction entirely if it prefers).
`PlainTextReportTemplate<TDefinition>` should remain a simple,
general-purpose fallback, not grow format-specific branching logic.

**Why this matters.** The Template Strategy's own separation (data,
layout, rendering pipeline) is only useful if each new format gets its
own template implementation — accreting format-specific logic onto one
shared template would erode the exact separation this Work Package's
own brief required.

## Recommendation 3 — A Future REST API Endpoint Exposing Report Generation Should Reuse the Existing Permission Pattern, Not Invent a New One

**What.** `WP 6.3` (REST API), when it eventually exposes report
generation over HTTP, should check its own permission
(`reporting.generate` or a REST-specific equivalent) via
`IPermissionEvaluator` at its own endpoint handler — exactly the
pattern `GenerateSampleReportCommandHandler` already demonstrates —
rather than expecting `IReportingService.GenerateAsync` to enforce
anything itself.

**Why this is worth naming.** `IReportingService` will never check
permissions internally, by design (`ADR-0040`); every future consumer
must remember to enforce authorization at its own calling layer, and
naming the pattern explicitly here reduces the chance of a future
consumer forgetting it entirely.

## Recommendation 4 — Report Generation Progress/Streaming and Scheduled/Recurring Generation Remain Named, Not Designed

**What.** `Platform Service Contracts.md`'s own Future Extension Points
for Reporting name both "report generation progress/streaming for a
long-running renderer" and "scheduled/recurring report generation"
explicitly. Neither should be designed until a concrete renderer with a
genuinely long generation time, or a concrete scheduling requirement,
actually exists.

**Why not build it now.** No current report definition (including the
sample) takes more than milliseconds to render; building progress
reporting or scheduling now would be speculative capability with no
real use case to validate the design against.

## Recommendation 5 — A Future Delivery-Channel or Report-History Capability Should Be a Separate Consuming Layer, Not a Change to `IReportingService`

**What.** If a real need emerges for email/webhook/push delivery of a
generated report, or a durable report history (`AT-09`'s own revisit
trigger), the natural shape is a new type that consumes
`IReportingService.GenerateAsync`'s own output and does something with
it (send it, store it via `IPersistenceStore`) — not a change to
`IReportingService` itself, which should remain a pure "generate on
request" service.

**Why not build it now.** No concrete delivery or history requirement
exists yet in this release's own approved scope; `AT-09` names the
correct revisit trigger (a real, demonstrated need) rather than
building speculatively.

## Not Recommended

- **Adding permission-gating to `IReportingService.GenerateAsync`
  itself.** The approved contract names the caller as the enforcement
  point explicitly; changing this now would contradict `Platform
  Service Contracts.md`'s own Security Considerations and this
  release's own Navigation/Command Framework precedent.
- **Building a generalised "report scheduler" or background report
  generation now.** No named `v0.6.0` Work Package has a concrete
  requirement for one; `Platform Service Contracts.md` itself names
  this as a plausible future requirement, not a current one.
- **Adding a dedicated export interface to `Tempest.Core.Reporting`.**
  Explicitly rejected by `ADR-0040`; belongs to `WP 6.7`'s own future
  scope.

## Related Documents

`WP6.0 Implementation Report.md`; `WP6.0 Engineering Review Report.md`;
`WP6.0 Platform Integration Demonstration.md`; `WP6.0 Platform Impact
Assessment.md`; `WP6.0 Lessons Learned.md`; `WP6.0 Technical Debt
Assessment.md`; `ADR-0040`; `docs/releases/v0.6.0/Platform Service
Contracts.md` (Reporting's own Future Extension Points);
`docs/releases/v0.6.0/WorkPackages.md` (`WP 6.3`, `WP 6.7`);
`docs/governance/Quality/Technical Debt Register.md` (`AT-09`).
