# WP 7.2B — Platform Integration Report

## Status

Architecture only. No production code, no report layout, no REST
endpoint accompanies this document.

## Purpose

Details the architectural interaction between the Requirements &
Verification Platform and three specific Platform Core/Engineering Core
surfaces this Work Package's own controlling instruction calls out by
name: Verification, Reporting, and the REST API. Each section states
integration points only — no implementation, no report layout, no
endpoint route.

## 1. Verification Integration

**Design:** the Systems Engineering Foundation does not wrap, extend, or
reimplement any part of `Tempest.Core.Verification`. Recording that a
requirement has been demonstrated is a direct call:

```
IVerificationService.RecordAsync(
    subjectDocumentId: requirement.Id,
    outcome: VerificationOutcome.Pass | Fail | Conditional,
    method: <caller-supplied method description>,
    context: <a VerificationContext the caller populates with
              criteria, evidence, and any linked calculation records
              or material references relevant to this requirement>)
```

**Reuse, not duplication:** every one of `VerificationContext`'s own
existing capabilities — `RecordCriterion`, `RecordEvidence`,
`LinkDocument`, `LinkCalculationRecord`, `ReferenceMaterial` — is used
exactly as `Tempest.Core.Verification` already defines it. The
Requirements Platform's own contribution is *only* that the
`subjectDocumentId` passed happens to be a `Requirement`'s own Id —
`VerificationService` has no awareness of, and needs no awareness of,
what kind of document it is verifying, confirmed directly by
`RecordAsync`'s own real signature (`Guid subjectDocumentId`, not a
typed `Requirement` parameter).

**Read side:** `GetVerificationHistoryAsync(requirementId)` returns
every verification ever recorded against a requirement, permission-
gated exactly as it is today — no new read path, no new permission
model.

**What this integration explicitly does not do:** it does not add a
`Requirement`-specific verification method, a `Requirement`-specific
`VerificationOutcome` value, or any Requirements-owned mirror of
`IVerificationRecord`. Doing so would violate this Work Package's own
explicit "reuse Engineering Core capability, do not duplicate
verification behaviour" instruction, and would also reintroduce exactly
the circular-dependency risk `WP7.0C Cross-Framework Dependency
Report.md` deliberately avoided by keeping `Verification`'s own
dependency generic (`WP7.2B Requirements Platform Architecture.md` §4).

## 2. Reporting Integration

**Design (architectural interaction only — no layout designed):** a
future Requirements Traceability Report is a plausible
`IReportDefinition`/`IReportRenderer<TDefinition>` pair, registered with
`IReportingService.RegisterDefinition` exactly as every existing report
definition is registered today. Its own renderer would read requirement,
allocation, traceability, and verification-evidence data through this
Platform's own read APIs (§1, above, plus `WP7.2B Requirements Domain
Model.md`'s own read-side concepts) — the same "gather data, then hand
it to a renderer" separation `IReportingService`'s own existing design
already enforces for every other report.

**Integration point named, not designed:** `IReportingService` itself
requires no change — its own contract (`RegisterDefinition`,
`GenerateAsync`) already supports an arbitrary future `TDefinition`
without modification, confirmed directly by its own generic method
signature. This Work Package names the *fact* that a Requirements
Traceability Report is a natural future consumer; it does not design
that report's own fields, layout, or rendering logic, per this Work
Package's own explicit "do not design report layouts" instruction.

**Authorization:** `IReportingService` itself performs no permission
check (§ its own documented remarks: "the enforcement point is the
caller, not this service") — a future Requirements Traceability Report's
own generation would be gated by whatever command handler dispatches
it, exactly as `ReportingSampleModule` already demonstrates.

## 3. REST API Integration

**Design (architectural interaction only — no endpoint implemented):**
if a future module chooses to expose Requirements operations over the
REST API, it does so exactly as every existing REST-exposed capability
does — mapping a route to an already-registered `ICommand` via
`IApiEndpointRegistry.MapCommand(method, path, commandId,
requiredPermission)`. The Systems Engineering Foundation itself has no
awareness of, and no dependency on, `Tempest.Core.Api` — the REST API is
never a required integration, only an optional one a calling module may
choose to add.

**No new invocation mechanism:** confirmed directly against
`IApiEndpointRegistry`'s own real contract — it dispatches through the
existing, unmodified `ICommandRegistry.InvokeAsync`, never a second,
competing invocation path. A future Requirements REST surface would
therefore first require Requirements operations to be exposed as
ordinary `ICommand`/`ICommandHandler<T>` pairs (the same pattern every
other REST-exposed capability already follows), then mapped — two
already-proven steps, not a new mechanism this Platform must invent.

**Authentication, disclosed, not resolved here:** `TD-13`/`TD-14` (no
real REST API authentication, no TLS) apply identically to any future
Requirements REST surface as they do to every existing one — this
Work Package does not resolve them, consistent with `WP7.2A Recommended
Programme.md`'s own sequencing decision (Platform Hardening recommended
second, at `v0.9.0`, not before Programme A).

## Related Documents

`Tempest.Core.Verification`; `Tempest.Core.Reporting`;
`Tempest.Core.Api`; `WP7.2B Requirements Platform Architecture.md`;
`WP7.2B Dependency Analysis.md`; `WP7.2A Recommended Programme.md`.
