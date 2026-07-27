# Rejected Designs Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Rejected Designs Register |
| **Purpose** | The complete governance index of every design seriously considered and deliberately declined — the permanent record of "we thought about this and chose not to," per Engineering Governance §10. |
| **Scope** | Every entry (`RD-0001` through the highest-numbered entry present) in `docs/architecture/Rejected Designs.md`. |
| **Owner** | Project Maintainer (see `ADR Register.md` for the same ownership note — no separate review-board structure exists as of this baseline). |
| **Source of Truth** | `docs/architecture/Rejected Designs.md`. This register indexes and cross-references that log; the full "Rejected because" reasoning, reversibility assessment, and revisit trigger for each entry live only there. |
| **Review Frequency** | Updated whenever a new design is seriously considered and declined during a Work Package (Engineering Governance §10) — in practice, once per Work Package that surfaces a genuine alternative. |
| **Last Reviewed** | 2026-07-27 (WP 5.0A, Navigation Framework Architecture). |
| **Related Documents** | `docs/architecture/Rejected Designs.md`; `ADR Register.md`; `Decision Register.md`. |
| **Related ADRs** | ADR-0025 through ADR-0032 each have one or more directly-paired RD entries (see table); most earlier ADRs (ADR-0001–ADR-0019) predate the Rejected Designs Log's own introduction (WP 4.0-era, commit `466334c`) and so have no paired RD entry — this is expected, not a gap (see "Coverage Status" note below). |
| **Related Academy Articles** | Every Work Package retrospective that produced an RD entry cites it in its own "Alternatives Considered" section. |
| **Coverage Status** | Complete for the period the Rejected Designs Log has existed (WP 4.0 onward). **Partial** relative to the Runtime Foundation (WP 2.1–2.7B): those work packages predate the Log's existence, so any alternatives they considered and declined were recorded only in prose, inside each retrospective's own "Alternatives Considered" section, never as a numbered RD entry — this is a real, disclosed gap in retroactive numbering, not a claim that no alternatives were considered during that period. |

---

## How to Read This Register

Each entry's originating Work Package is **Verified** directly from the
Rejected Designs Log's own "Considered during" line. No entry has been
reversed (a rejected design later adopted) as of this baseline; a reversal
would be recorded here as a new status column value, not a silent removal.

## Entries

| RD | Title | Considered During | Status |
|---|---|---|---|
| RD-0001 | `ICommand<TResult>` / `ICommandHandler<T>` Now | WP 4.0 | Rejected — deferral, revisit trigger WP 4.7 |
| RD-0002 | `INavigationProvider` / `IDiagnosticsProvider` in WP 4.0 | WP 4.0 | Rejected — deferral; `INavigationProvider` half fulfilled by WP 5.0A (formerly WP 4.6A), `IDiagnosticsProvider` half still deferred to WP 5.2 (formerly WP 4.8) |
| RD-0003 | Module Builder Pattern | WP 4.1 | Rejected |
| RD-0004 | Registration Helpers | WP 4.1 | Rejected |
| RD-0005 | Module Metadata / `ToString()` Convenience | WP 4.1 | Rejected |
| RD-0006 | A Dedicated `Tempest.SDK` Project | WP 4.1 | Rejected |
| RD-0007 | Service-Locator Workaround for Module Constructor Dependencies | WP 4.1 | Rejected |
| RD-0008 | `IPluginManifestSource` Abstraction | WP 4.2 | Rejected |
| RD-0009 | Maximum / "Tested Up To" Platform Version in the Manifest | WP 4.2 | Rejected |
| RD-0010 | Host-Fatal Plugin Failures | WP 4.2B (ADR-0025) | Rejected |
| RD-0011 | Per-Plugin `IsCritical` Manifest Opt-In | WP 4.2B (ADR-0025) | Rejected |
| RD-0012 | A Single Combined Plugin Discovery/Loading Phase | WP 4.2C (ADR-0026) | Rejected |
| RD-0013 | Renumbering All Thirteen Existing Host Lifecycle Phases | WP 4.2C (ADR-0026) | Rejected |
| RD-0014 | Plugin Discovery Reading Platform Version Metadata Independently | WP 4.2C (ADR-0026) | Rejected |
| RD-0015 | Packaging the WP 4.3 Sample Module Through the Plugin Manifest System | WP 4.3 | Rejected — deferral |
| RD-0016 | Deferring Module Metadata Reading Until After Dependency Injection Is Built | WP 4.4A (ADR-0027) | Rejected |
| RD-0017 | A Second, Always-Parameterless "Descriptor" Type Per Module | WP 4.4A (ADR-0027) | Rejected |
| RD-0018 | Static Abstract Interface Members on `IModule` for Metadata | WP 4.4A (ADR-0027) | Rejected |
| RD-0019 | DI-Auto-Discovered Event Handlers | WP 4.4 (ADR-0028) | Rejected |
| RD-0020 | Deferred, Queued Re-Entrant Publishing | WP 4.4 (ADR-0028) | Rejected |
| RD-0021 | Polymorphic Event Dispatch | WP 4.4 (ADR-0028) | Rejected |
| RD-0022 | A Per-Subscriber Critical Opt-In, Mirroring `ICriticalBackgroundService` | WP 4.4 (ADR-0028) | Rejected |
| RD-0023 | DI Container Multi-Registration Resolution for Auto-Discovering Hosted Services | WP 4.5 (ADR-0029) | Rejected |
| RD-0024 | A Dedicated `HostedServiceDescriptor` Type | WP 4.5 (ADR-0029) | Rejected |
| RD-0025 | Extending `ReflectionFrameworkDiscoveryService` to Also Discover Hosted Services | WP 4.5 (ADR-0029) | Rejected |
| RD-0026 | Active Host-Level Monitoring of a Hosted Service's Own Background Work | WP 4.5 (ADR-0029) | Rejected — deferral |
| RD-0027 | A New, Dedicated Host Lifecycle Phase for Hosted Service Discovery/Registration | WP 4.5 (ADR-0029/0030) | Rejected |
| RD-0028 | Concurrent (Parallel) Start of Independent Hosted Services | WP 4.5 (ADR-0029) | Rejected |
| RD-0029 | Automatic Restart/Backoff for Isolated Hosted Service Failures | WP 4.5 (ADR-0029) | Rejected — deferral |
| RD-0030 | Declarative, Attribute-Based Navigation Contribution | WP 5.0A (ADR-0032) | Rejected — deferral |
| RD-0031 | A Dedicated Navigation Publish/Subscribe Mechanism, Separate From the Event Bus | WP 5.0A (ADR-0032) | Rejected |
| RD-0032 | Navigation as a Host-Owned Collaborator | WP 5.0A (ADR-0032) | Rejected |
| RD-0033 | A First-Class Permission/Role Model in Navigation | WP 5.0A | Rejected — deferral |

**Total: 33 entries, all Rejected (none later reversed/adopted).**

## Distribution by Work Package

| Work Package | Entries |
|---|---|
| WP 4.0 | RD-0001, RD-0002 (2) |
| WP 4.1 | RD-0003–RD-0007 (5) |
| WP 4.2 / 4.2B / 4.2C | RD-0008–RD-0014 (7) |
| WP 4.3 | RD-0015 (1) |
| WP 4.4A | RD-0016–RD-0018 (3) |
| WP 4.4 | RD-0019–RD-0022 (4) |
| WP 4.5 | RD-0023–RD-0029 (7) |
| WP 5.0A | RD-0030–RD-0033 (4) |

No Rejected Design entry exists for WP 2.1 through WP 2.7B, WP 4.2A, WP
4.2D, WP 4.4B, WP 4.4D, or WP 4.4E — **Inferred** to mean either (a) the
Log did not yet exist (WP 2.x, predating commit `466334c`) or (b) the
work package's own brief was narrow enough that no genuine, seriously-
considered alternative arose worth a permanent entry (an implementation
work package realising an already-fully-decided architecture, such as
WP 4.2D or WP 4.4D, is expected to produce zero new RD entries — this is
the same pattern the WP 4.4D and WP 4.2 implementation retrospectives
themselves note explicitly under "Alternatives Considered: None").

## Cross-Reference Check

Every RD entry above traces to exactly one Work Package retrospective's
own "Alternatives Considered" section, and every RD-0010 through RD-0033
entry is also cited directly by the ADR (ADR-0025–ADR-0032) its own
Decision/Alternatives Considered section names. No RD entry was found
that lacks a corresponding Work Package or ADR citation.
