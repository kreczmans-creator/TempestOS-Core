# WP 7.0B — Platform Consumption Matrix

## Status

Complete. For every candidate Work Package in `WP7.0B Candidate Work
Package Catalogue.md`, identifies which of the eleven `v0.6.0`-certified
Platform Services are expected to be consumed — demonstrating that the
Platform Foundation `v0.6.0` completed is a real, active dependency for
`v0.7.0`'s own candidates, not infrastructure built and then bypassed.

## Matrix

| Candidate | Identity & Permissions | Settings | Audit | Notifications | Reporting | REST API | Export/Import | Licensing | Persistence | Diagnostics | Command Framework |
|---|---|---|---|---|---|---|---|---|---|---|---|
| A — Trust Isolation Retrofit | ✅ (`IPermissionEvaluator`, the mechanism being applied) | — | — | — | — | — | — | — | — | — | — |
| B — REST Auth & TLS | ✅ (extends `ADR-0043` identity model) | — | ✅ (attribution of authenticated requests) | — | — | ✅ (the surface being secured) | — | — | — | — | ✅ (dispatch target) |
| C — Governance Closeout | — | — | — | — | — | — | — | — | — | ✅ (`HostState` namespace subject) | — |
| D — Data Model Architecture | — | — | ✅ (a plausible consumer, if entity changes are audited) | — | — | — | ✅ (a plausible export candidate for engineering documents) | — | ✅ (`IPersistenceStore` as a plausible, not guaranteed, substrate) | — | — |
| E — Units & Quantities Architecture | — | — | — | — | — | — | — | — | — | — | — |
| F — Calculation Framework Architecture | — | — | ✅ (recording what was calculated, per `FCR-0032`'s own Audit-adjacent framing) | — | ✅ (a calculation result is a plausible report input) | — | — | — | — | — | — |
| G — Materials Framework Architecture | ✅ (access control over material specifications) | — | ✅ (traceability) | — | — | — | ✅ (export candidate) | — | ✅ (via Candidate D) | — | — |
| H — Verification & Validation Architecture | ✅ (permission-gated verification records, mirroring `IAuditQuery`'s own pattern) | — | ✅ (adjacent — a verification record and an audit record are related but distinct) | ✅ (a plausible trigger for a verification-failure notification) | ✅ (verification results as report input) | — | ✅ (export candidate) | — | ✅ (via Candidate D) | — | — |
| I — Requirements Engine Architecture | ✅ (permission-gated requirement access) | ✅ (plausible — project-level requirement configuration) | ✅ (who changed a requirement, when) | — | ✅ (requirements traceability reports) | ✅ (a plausible future REST surface, mirroring the REST API's own "expose existing capability, add no new capability" precedent) | ✅ (export candidate — the `FCR-0027` description names traceability explicitly) | — | ✅ (via Candidate D) | — | ✅ (a plausible dispatch target for requirement-related commands) |
| J — Project Engine Architecture | ✅ (access control, per `Security Roadmap.md` item 4) | ✅ (plausible — project-level settings) | ✅ (mandatory — `Security Roadmap.md` item 4 requires audit logging for classified/export-controlled fields as part of the same design) | — | ✅ (project status reporting) | ✅ (a plausible future REST surface) | ✅ (export candidate) | ✅ (licensed capability gating per-project features, if any) | ✅ (via Candidate D) | — | ✅ (a plausible dispatch target) |

## Reading the Matrix

- **Every one of the eleven `v0.6.0` Platform Services appears as a
  consumer in at least one candidate** — Identity & Permissions,
  Audit, and Export/Import are the three most broadly reused,
  consistent with `VISION.md`'s own Product Principle that an
  Engineering Module is built *on* the Platform, using its services
  exactly as every `v0.6.0` sample module already demonstrates.
- **A checkmark above means "plausible consumer," not "confirmed
  design decision.**" Every candidate is still an architecture-phase
  proposal — the actual architecture phase for each may find a
  different, better-justified integration, exactly as `WP 6.0` (Reporting)
  discovered during its own implementation that Persistence was
  deliberately *not* a consumer, contradicting an initial assumption.
- **Settings and Diagnostics are the two least-reused services in this
  matrix** — Settings appears only where a project- or requirement-level
  configurable value is plausible; Diagnostics appears only in Candidate
  C, since it is the one candidate whose own subject matter (`HostState`)
  is a Diagnostics-adjacent concern. Neither absence is a finding —
  most candidates simply have no natural need for either yet.

## Related Documents

`WP7.0B Candidate Work Package Catalogue.md`; `docs/releases/v0.6.0/
WP6.8 Platform Consumption Matrix.md` (the `v0.6.0` precedent this
document's own name and format follow).
