# WP 6.7 — Export/Import — Platform Impact Assessment

## Purpose

A dedicated assessment of whether `WP 6.7`'s own implementation
confirms, extends, or exposes a weakness in the platform architecture
established by prior Work Packages — distinct from its Implementation
Report, Engineering Review Report, and Platform Integration
Demonstration.

## Does This Work Package Confirm Earlier Platform Architecture?

**Yes, on three separate points, each independently verified rather
than assumed:**

1. **The Composition Root / ordinary-singleton registration pattern
   (`ADR-0009`) continues to scale cleanly to an eleventh new service**
   (`IExportService`), registered in the same Phase 6 block as every
   other DI-public Platform Service since `WP 4.4`.
2. **`ADR-0044`'s own dual-registration pattern
   (`CurrentPrincipalAccessor`, registered under both its own concrete
   type and its public interface) generalises cleanly to a second,
   structurally different problem.** `ImportService` reuses the
   identical mechanism to solve "a privileged registrant needs a
   capability the approved public interface deliberately does not
   expose" — a different concrete need (registering `IImportable`
   handlers, not writing an ambient principal), the same resolution
   shape, confirming this is a genuinely reusable pattern, not a
   one-off fix specific to `CurrentPrincipalAccessor`.
3. **The "additive elaboration over approved-interface modification"
   convention (`IReportTemplate` in `WP 6.0`, `IPlatformNotification` in
   `WP 6.2`, and now `IExportableKind`/`IImportable`/`IExportFormat`/
   `IExportPayloadSerializer` here) continues to resolve every
   gap-in-the-approved-catalogue this release has produced**, without
   ever requiring a single approved interface to be changed — four
   Work Packages in, this pattern shows no sign of running out of
   headroom.

## Does This Work Package Extend Earlier Platform Architecture?

**Yes, in one specific, disclosed way:**

**This platform's first genuinely multi-destination artifact-routing
mechanism.** Every prior registry-style platform service
(`IReportingService`, `ICommandRegistry`) routes by a single, simple
key to a single registered handler. `ImportService` is the first to
combine *framing* (multiple opaque sections in one artifact, via
`IExportFormat`) with *routing* (each section dispatched to its own
registered `IImportable` by `Kind`) with *all-or-nothing validation*
(every section checked before any is applied) — a genuinely new
combination of concerns this codebase can reuse for any future
multi-part artifact or batch-operation need.

## Does This Work Package Expose Any Architectural Weakness?

**One, directly confirmed rather than merely anticipated:** this
codebase's own custom DI container (`ADR-0005`) has exactly one
registration per service type, with no collection-resolution mechanism
at all — confirmed by direct inspection of `TempestServiceProvider`,
not merely assumed. This is not a defect (nothing in this release has
ever needed multi-registration before), but it is a real, now-confirmed
constraint any future Work Package needing "resolve every registered
implementation of X" will need to design around explicitly — either via
an explicit registration method (as `ImportService.RegisterImportable`
does here) or via a future container enhancement, should a second,
independent need for the same capability arise.

**A second, disclosed observation, not a weakness in the platform
architecture itself:** three genuine, pre-existing
governance-documentation drifts were found during this Work Package's
own repository review (two small, one substantial) — see this Work
Package's own Lessons Learned and Engineering Review Report for the
full account. These are disclosed weaknesses in this project's own
governance-maintenance discipline, not the platform's own technical
architecture — the smaller ones corrected in this same commit, the
larger one (three registers stale since `WP 5.2`) explicitly deferred
to `WP 6.8`'s own closing audit.

## Explicit Assessment: Interactions With Identity, Settings, Persistence, Audit, Notifications, and Reporting

**Recorded per this Work Package's own explicit instruction — see
`WP6.7 Platform Integration Demonstration.md` for the complete,
per-service account.** In summary:

- **Identity & Permissions, Audit, Notifications.** All three used, but
  entirely inside `ExportImportSampleModule`'s own command handlers —
  `Tempest.Core.ExportImport` itself has zero direct dependency on any
  of the three, confirmed by direct inspection.
- **Settings.** Used, genuinely — the primary, practical integration
  point this release names, via the additive `IExportable`/
  `IImportable` pattern, with zero change to `ISettingsProvider` itself.
- **Persistence, Reporting.** Both deliberately **not** consumed — one
  because the approved contract states "Persistence Requirements:
  None," the other because `ADR-0040`'s own round-trip-safety
  disclosure makes exporting a `ReportResult` directly contradictory to
  an already-accepted architectural decision.

**Summary: Export/Import has zero core-level platform dependencies at
all — a stricter posture than either Reporting (zero) or the REST API
(two, Identity and Audit) achieved. Every cross-service interaction this
Work Package demonstrates exists entirely because
`ExportImportSampleModule`'s own commands happen to use those services,
never because `IExportService`/`IImportService` themselves need them.**
This is architecturally the cleanest possible outcome for a framework
whose own brief states "shall not introduce business logic" and "do not
introduce unnecessary dependencies" — the two dependencies the contract
does name (Persistence: none; a source's own owning service: Settings,
here) are exactly right, and nothing more was added.

## Related Documents

`WP6.7 Implementation Report.md`; `WP6.7 Engineering Review Report.md`;
`WP6.7 Platform Integration Demonstration.md`; `WP6.7 Lessons
Learned.md`; `WP6.7 Technical Debt Assessment.md`; `WP6.7 Future
Capability Recommendations.md`; `ADR-0005`; `ADR-0009`; `ADR-0040`;
`ADR-0044`; `ADR-0051`; `docs/governance/Engineering/Interface
Register.md`, `Dependency Injection Register.md`, `Module Register.md`
(each disclosed as Partial).
