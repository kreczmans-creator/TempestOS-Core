# WP 7.0B — Candidate Work Package Catalogue

## Status

Complete. **These are candidates, not approvals.** Every entry below
still requires its own Architecture, Planning, and Contract Review
phase before a real Work Package number is assigned, per this project's
standing discipline. This catalogue extends `WP7.0A Recommended v0.7
Candidate Work Packages.md` (Candidates A–C, reproduced here for a
single point of reference) with seven further candidates this Work
Package's own Capability Dependency Analysis identified.

## Candidates A–C (from `WP 7.0A`, reproduced)

| Candidate | Objective | FCR(s) |
|---|---|---|
| **A** — Plugin & Registration Trust Isolation | Apply the existing `IPermissionEvaluator` mechanism to plugin loading, `NavigationService.Unregister`, and Command/Navigation registration ownership | `FCR-0001` |
| **B** — REST API Authentication & Transport Security | Design and implement real REST API authentication and TLS | `FCR-0003`, `FCR-0004` |
| **C** — Engineering Foundation Governance Closeout | Governance-register health-check tooling; resolve the `Runtime`↔`Diagnostics` namespace reference | `FCR-0005`, `FCR-0006` |

See `WP7.0A Recommended v0.7 Candidate Work Packages.md` for full
rationale, cautions, and sequencing on A–C.

## Candidates D–J (new, `WP 7.0B`)

### Candidate D — Engineering Data Model & Document Foundation Architecture

| Field | Value |
|---|---|
| **Objective** | Design (architecture only) a shared engineering-entity data model — documents, revisions, references — for Requirements, Project, and future discipline modules to build on. |
| **Approximate Scope** | Architecture Work Package only, mirroring `WP 5.0A`/`WP 5.1A`'s own design-then-implementation split. Decide: relationship to `IPersistenceStore`; whether `FCR-0007`'s query-capability gap must be resolved first or can be deferred; `ADR-0013` classification. |
| **Dependencies** | None upstream. |
| **Expected Outputs** | An architecture document; one or more ADRs; an explicit `ADR-0013` classification decision. |
| **Academy Impact** | New concept guide — a genuinely new data-modelling pattern for this platform. |
| **Estimated Implementation Complexity** | Unknown at architecture time; likely Medium-High once scoped, given no existing abstraction to build from. |

### Candidate E — Units & Quantities Framework Architecture

| Field | Value |
|---|---|
| **Objective** | Design (architecture only) a shared dimensioned-quantity representation and conversion model. |
| **Approximate Scope** | Architecture Work Package only. Decide the representation shape (a value type? a generic `Quantity<TDimension>`?) and where it lives (`Tempest.Core` vs. a new namespace). |
| **Dependencies** | None upstream — can proceed in parallel with Candidate D. |
| **Expected Outputs** | An architecture document; likely one ADR. |
| **Academy Impact** | New concept guide. |
| **Estimated Implementation Complexity** | Medium — well-precedented in other engineering software, though TempestOS's own shape is undesigned. |

### Candidate F — Engineering Calculation Framework Architecture

| Field | Value |
|---|---|
| **Objective** | Design (architecture only) a shared calculation/formula execution model, mirroring the Command Framework's own dispatch-mechanism precedent. |
| **Approximate Scope** | Architecture Work Package only. Must not begin before Candidate E completes (`FCR-0032` depends on `FCR-0030`). |
| **Dependencies** | Candidate E (Units & Quantities). |
| **Expected Outputs** | An architecture document; likely one or more ADRs. |
| **Academy Impact** | New concept guide. |
| **Estimated Implementation Complexity** | High — a genuine new abstraction, not a small extension. |

### Candidate G — Materials Framework Architecture

| Field | Value |
|---|---|
| **Objective** | Design (architecture only) shared material specification and traceability capability. |
| **Approximate Scope** | Architecture Work Package only. Must not begin before Candidates D and E complete. |
| **Dependencies** | Candidate D (Data Model), Candidate E (Units & Quantities). |
| **Expected Outputs** | An architecture document. |
| **Academy Impact** | None until designed beyond the architecture phase itself. |
| **Estimated Implementation Complexity** | Unknown — first real content for the `Materials` category. |

### Candidate H — Verification & Validation Framework Architecture

| Field | Value |
|---|---|
| **Objective** | Design (architecture only) a cross-cutting pass/fail verification-record mechanism, distinct from Audit and Reporting. |
| **Approximate Scope** | Architecture Work Package only. Must not begin before Candidate I (Requirements Engine) has at least reached its own architecture phase, since verification requires a requirement to verify against. |
| **Dependencies** | Candidate I (Requirements Engine Architecture), Candidate D (Data Model). |
| **Expected Outputs** | An architecture document. |
| **Academy Impact** | New concept guide, likely combined with Candidate I's own retrospective given the tight coupling. |
| **Estimated Implementation Complexity** | Unknown. |

### Candidate I — Requirements Engine Architecture

| Field | Value |
|---|---|
| **Objective** | The first real architecture phase for `FCR-0027` — decide `ADR-0013` classification, scope, and relationship to Candidate D's own data model. |
| **Approximate Scope** | Architecture Work Package only, mirroring `WP 6.1`'s own "Permissions & Identity Architecture" precedent (the least architecturally grounded objective of its own release, per that Work Package's own risk disclosure) — this is the equivalent moment for Systems Engineering. |
| **Dependencies** | Candidate D (Data Model) should exist first, or be designed concurrently. |
| **Expected Outputs** | An architecture document; ADR(s); the `ADR-0013` classification decision `FCR-0027` itself named as still open. |
| **Academy Impact** | First Academy content for the Systems Engineering category. |
| **Estimated Implementation Complexity** | Unknown — the least architecturally grounded candidate in this catalogue, by the same reasoning `WP 6.1` disclosed for Identity. |

### Candidate J — Project Engine Architecture

| Field | Value |
|---|---|
| **Objective** | The first real architecture phase for `FCR-0028` — decide `ADR-0013` classification, and design encryption at rest, access control, and audit logging for classified/export-controlled data as part of the same design phase, per `Security Roadmap.md` item 4. |
| **Approximate Scope** | Architecture Work Package only. Security design is **not** a follow-up phase — `Security Roadmap.md` item 4 explicitly requires it in the same design pass. |
| **Dependencies** | Candidate D (Data Model); benefits from `FCR-0021` (Multi-User/Tenant) if concurrent access is in scope, though this is not blocking. |
| **Expected Outputs** | An architecture document; ADR(s); a Threat Model addendum (this is a security-relevant architecture phase, mirroring `WP 6.1`'s own addendum for Identity). |
| **Academy Impact** | First Academy content for the Project Management category. |
| **Estimated Implementation Complexity** | Unknown — likely High once security requirements are fully scoped. |

## Summary Table

| Candidate | Programme | Depends On |
|---|---|---|
| A | Platform Hardening | — |
| B | Platform Hardening | — |
| C | Platform Hardening | — |
| D | Engineering Foundation | — |
| E | Engineering Foundation | — |
| F | Engineering Foundation | E |
| G | Engineering Foundation | D, E |
| H | Engineering Foundation / Discipline | D, I |
| I | Engineering Discipline | D |
| J | Engineering Discipline | D |

## What This Catalogue Does Not Do

It does not approve any candidate. It does not assign a Work Package
number. It does not scope implementation detail beyond what each
candidate's own "Approximate Scope" states at the architecture-phase
level.

## Related Documents

`WP7.0A Recommended v0.7 Candidate Work Packages.md`; `WP7.0B
Capability Dependency Report.md`; `WP7.0B Engineering Foundation
Architecture.md`; `WP7.0B Platform Consumption Matrix.md`.
