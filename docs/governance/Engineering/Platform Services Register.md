# Platform Services Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Platform Services Register |
| **Purpose** | The governance-level index of every platform service TempestOS provides — status, originating Work Package, and ADR backing — cross-referenced against the ADR and Test Registers. |
| **Scope** | Every service listed in `docs/architecture/Platform Service Map.md`'s own "At a Glance" table. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `docs/architecture/Platform Service Map.md` — the full responsibility/dependency/consumer/lifecycle detail for each service lives there; this register does not repeat it, only indexes it against governance status. |
| **Review Frequency** | Updated whenever `Platform Service Map.md` itself is updated (Engineering Governance §6) — i.e., whenever a service is added, removed, or changes responsibility/dependencies/consumers. |
| **Last Reviewed** | 2026-07-25 (WP 4.5A). |
| **Related Documents** | `docs/architecture/Platform Service Map.md`; `Architecture Document Register.md`; `Module Register.md`; `Hosted Services Register.md`; `Event Catalogue.md`. |
| **Related ADRs** | ADR-0005 through ADR-0030 — nearly every ADR concerns one of these services directly or the boundary between them. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/` (The Module Pipeline, The Startup Sequence, Working with the TempestOS Host, Platform Layering, Plugin Architecture, Failure Isolation Across TempestOS). |
| **Coverage Status** | Complete. |

---

## Entries

| Service | Status | Originating Work Package | Key ADRs |
|---|---|---|---|
| Platform Version | Implemented | WP 4.2A | ADR-0009, ADR-0023 |
| Configuration | Implemented | WP 2.5 | ADR-0009 |
| Logging | Implemented | WP 2.6 | ADR-0009, ADR-0010 |
| Dependency Injection | Implemented | WP 2.4 | ADR-0005, ADR-0006, ADR-0007, ADR-0008, ADR-0009 |
| Discovery | Implemented | WP 2.1 | ADR-0003, ADR-0008 |
| Registration | Implemented | WP 2.2 | ADR-0001, ADR-0002 |
| Lifecycle | Implemented | WP 2.3 | ADR-0002, ADR-0003, ADR-0004, ADR-0007 |
| Module SDK | Implemented (developer convenience layer, not Host-orchestrated) | WP 4.1 | None new (applies ADR-0003) |
| Host | Implemented | WP 2.7 (design), WP 2.7B (implementation) | ADR-0004, ADR-0008, ADR-0009, ADR-0011–ADR-0019 |
| Event Bus | Implemented | WP 4.4 (design), WP 4.4D (implementation), WP 4.4E (first consumer) | ADR-0020, ADR-0028 |
| Background Services | Implemented | WP 4.5 (design), WP 4.5 (implementation) | ADR-0021, ADR-0029, ADR-0030 |
| Command Framework | Contract only (WP 4.0); dispatcher **not yet implemented** | WP 4.0; dispatcher planned WP 4.7 | ADR-0022, ADR-0024 |
| Plugin Manifest | Implemented | WP 4.2 (design and implementation), WP 4.2A, WP 4.2B, WP 4.2C | ADR-0025, ADR-0026 |
| Project Engine | Not implemented as a platform service — bootstrap-era code (`Tempest.Core.Projects`, `ProjectService`, `JsonProjectRepository`) predates and is independent of the module pipeline | Planned, no Work Package assigned | None |
| Requirements Engine | Not implemented — no code exists | Planned, no Work Package assigned | None |

**Total: 15 entries — 11 Implemented, 1 contract-only (Command Framework), 2 planned with no code (Project Engine, Requirements Engine), 1 developer-convenience layer (Module SDK).**

## Verification of "Implemented" Status

Each service marked Implemented above is **Verified** by direct
correspondence to a namespace under `src/Tempest.Core/` (or
`src/Samples/Tempest.Samples/` for the Event Bus's first consumer): the
service's key types exist in source, are exercised by at least one test in
the Test Register, and are described as implemented in
`Platform Service Map.md` itself. Project Engine and Requirements Engine
are marked "not implemented as a platform service" because the pre-module
bootstrap code they might relate to (`Tempest.Core.Projects`,
`Tempest.Core.Repositories`) was never integrated into, or classified
under, the module pipeline's own platform-service model (ADR-0013) — this
is **Verified** directly: no ADR classifies either, and no Work Package
claims to have implemented either as a platform service.

## Cross-Reference Check

Every service above appears in exactly one row of
`Platform Service Map.md`'s own "At a Glance" table — no service exists
in one document but not the other. Every Implemented service has at least
one corresponding entry in `Test Register.md` and at least one Work
Package retrospective in `Academy Register.md`.
