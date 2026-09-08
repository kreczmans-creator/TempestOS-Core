# WP 7.2C — Platform Integration Matrix

## Status

Contract review only. No report layout, no export format, no REST
endpoint, no implementation.

## Purpose

Confirms the interface boundary between the Requirements Platform and
five named Platform Core surfaces — Reporting, Export/Import, REST API,
Audit, Identity — mirroring `WP7.0C Platform Integration Matrix.md`'s
own role for the original five Engineering Foundation frameworks, and
`WP7.2B Platform Integration Report.md`'s own architecture-level
treatment of the first three, now confirmed at the contract level.

## Matrix

| Platform Surface | Interface Boundary | Requirements Platform's Own Responsibility | Platform Surface's Own Responsibility |
|---|---|---|---|
| **Reporting** (`IReportingService`) | `RegisterDefinition<TDefinition>`/`GenerateAsync` — an arbitrary future `TDefinition` requires no change to `IReportingService` itself, confirmed directly against its own generic signature | Author a future `IReportDefinition`/`IReportRenderer<TDefinition>` pair (a Requirements Traceability Report), gathering data through `IRequirementsService`/`IRequirementEvidence` | Registration, dispatch, and rendering — unchanged, unmodified |
| **Export/Import** (`IExportable`/`IImportService`) | `IExportable.ExportAsync(Stream, CancellationToken)`/`IImportService.RegisterImportable` — both already accept an arbitrary future implementer | A `IRequirementCollection` (or the whole Requirements store) authors its own `IExportable`/`IImportable` implementation, framing its own byte layout as an opaque payload | Artifact envelope framing, versioning, and multi-section packaging — unchanged, unmodified |
| **REST API** (`IApiEndpointRegistry`) | `MapCommand(method, path, commandId, requiredPermission)` — dispatches through the existing, unmodified `ICommandRegistry.InvokeAsync`, never a second invocation path | Author `ICommand`/`ICommandHandler<T>` pairs wrapping `IRequirementsService`'s own operations, then map a route to each, exactly as every existing REST-exposed capability already does | Route-to-command mapping and HTTP dispatch — unchanged, unmodified |
| **Audit** (`IAuditRecorder`) | `RecordAsync(action, detail, cancellationToken)`, composed at the calling layer, never inside `IRequirementsService` itself | The calling layer (a command handler) records `"requirement.created"`, `"requirement.revised"`, `"requirement.statusChanged"`, `"requirement.linked"` actions, mirroring every existing sample module's own permission-check-then-audit-record pattern | Action recording, attribution, and storage — unchanged, unmodified |
| **Identity** (`IPermissionEvaluator`/`ICurrentPrincipalAccessor`) | `RequirePermission(principal, permission)`/`HasPermission`, composed at the calling layer, never inside `IRequirementsService` itself | The calling layer defines and checks its own Requirements-specific `Permission` values (e.g., `"requirements.create"`, `"requirements.revise"`) before invoking `IRequirementsService` | Permission evaluation and principal resolution — unchanged, unmodified |

## Confirmed: No Interface Change Required to Any Platform Core Surface

**Every one of the five Platform Core surfaces reviewed requires zero
modification to integrate with the Requirements Platform.** This is the
same confirming finding `WP7.2B Dependency Analysis.md` reached at the
architecture level, now re-confirmed at the contract level against the
actual, real method signatures of `IReportingService`, `IExportable`,
`IApiEndpointRegistry`, `IAuditRecorder`, and `IPermissionEvaluator` —
each was checked directly, not assumed compatible.

## Permission Vocabulary (Illustrative, Not Final)

Following `Tempest.Core.Verification.ReadPermission`'s own precedent
(`new Permission("verification.read")`), a future implementation would
plausibly define its own Requirements-specific `Permission` constants —
illustrative only, not a contract this Work Package finalises:

```csharp
// Illustrative only — the owning implementation Work Package's own
// decision, not proposed as final by this Contract Review.
public static readonly Permission CreatePermission = new("requirements.create");
public static readonly Permission RevisePermission = new("requirements.revise");
public static readonly Permission ReadPermission = new("requirements.read");
```

Whether `IRequirementsService` itself gates any of its own read methods
internally (mirroring `IVerificationService.GetVerificationHistoryAsync`'s
own internal permission gate) or leaves every permission check to the
calling layer (mirroring `IReportingService`'s own explicit "the
enforcement point is the caller" precedent) is **not decided by this
document** — see `WP7.2C Security Review.md` for the disclosed,
unresolved question this represents.

## Related Documents

`WP7.0C Platform Integration Matrix.md` (the precedent this document's
own structure follows); `WP7.2B Platform Integration Report.md`;
`WP7.2C Requirements Platform Contracts.md`; `WP7.2C Security
Review.md`.
