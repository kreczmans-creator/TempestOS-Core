# TempestOS v0.6.0 — Contract Review: Engineering Governance Confirmation

## Purpose

The closing check of the Contract Review, confirming four specific
properties across all nine proposed `v0.6.0` services before
implementation begins: four-layer dependency-rule compliance, absence
of circular dependencies, absence of public-interface overlap, and
absence of duplicated responsibilities. Each is checked explicitly
below, against the concrete artifacts this review produced (`Platform
Service Dependency Diagram.md`, `Public Interface Catalogue.md`,
`Platform Service Contracts.md`), not asserted without evidence.

## 1. Four-Layer Dependency Rules

**Rule (`ADR-0023`).** Modules depend on Platform Services, which depend
on Dependency Injection (and, where named, other Platform Services),
which sit above the Runtime Host. No layer may depend downward past its
own tier, and no Platform Service may depend on a Module.

**Check.** Every one of the nine proposed services was individually
classified in `Release Architecture.md`'s Layer Classification table and
re-confirmed in `Service Registration Matrix.md`:

| Service | Layer | Downward-only? |
|---|---|---|
| Persistence | Platform Service | Yes — depends only on DI |
| Reporting | Platform Service | Yes — DI, optionally Command Framework (a peer Platform Service) |
| Identity & Permissions | Platform Service | Yes — depends only on DI |
| Notifications | Platform Service | Yes — DI, Event Bus (a peer Platform Service) |
| Settings | Platform Service | Yes — DI, Persistence, Event Bus (all peer Platform Services or DI) |
| Audit | Platform Service | Yes — DI, Persistence, Identity & Permissions (peers) |
| Licensing | Platform Service (validator: Composition-Root-level) | Yes — no dependency at all; its Host-level construction timing mirrors Configuration's own precedent, not a downward violation |
| Export/Import | Platform Service | Yes — DI; reads from a peer Platform Service's own public interface, never a Module |
| REST API | Platform Service (hosted-service scaffold) | Yes — Background Services, Command Framework, Identity & Permissions (all peers or existing infrastructure) |

**Finding: Satisfied.** No proposed service depends on a Module. No
proposed service depends on anything below the Runtime Host except
Licensing's validator, whose Composition-Root-level construction timing
is the same pattern Configuration and Platform Version already use
without being considered a layering violation (`Service Lifecycle.md`).

## 2. Circular Dependencies

**Rule.** A dependency graph among platform services must be acyclic —
`ADR-0023`'s "dependencies flow downward only" is meaningless if two
services depend on each other.

**Check.** Read directly from `Platform Service Dependency Diagram.md`'s
Mermaid graph, traced explicitly:

- Persistence → DI (terminal, no further outgoing edge).
- Identity & Permissions → DI (terminal).
- Reporting → DI, (optional) Command Framework → DI (terminal).
- Notifications → DI, Event Bus → DI (terminal).
- Settings → DI, Persistence → DI, Event Bus → DI (all terminal beyond
  Settings itself).
- Audit → DI, Persistence → DI, Identity & Permissions → DI (all
  terminal beyond Audit itself).
- Licensing → Host (terminal, and a different kind of edge entirely —
  Composition Root construction, not a runtime service dependency).
- Export/Import → DI, (reads from) Settings/Reporting (both already
  shown terminal above).
- REST API → Background Services → Host (terminal); → Command Framework
  → DI (terminal); → Identity & Permissions → DI (terminal).

**Finding: Satisfied — no cycle exists.** Every path terminates at
either Dependency Injection or the Host within two hops; no proposed
service appears in its own dependency chain, and no pair of proposed
services depends on each other mutually. This was also stated as a
direct finding in `Platform Service Dependency Diagram.md`'s own
"Reading the Graph" section; this check independently re-traces it
rather than merely citing it, per this release's own re-derivation
discipline (`Technical Debt Assessment.md`'s closing note).

## 3. Public Interface Overlap

**Rule.** No two proposed public interfaces should expose overlapping
responsibility such that a consumer could reasonably be unsure which one
to call for a given need — implicitly required by Engineering Governance
§2 (Single Responsibility) applied to a public contract rather than a
single class.

**Check.** Every proposed interface in `Public Interface Catalogue.md`
was reviewed pairwise for the specific confusions this platform has
already had to resolve once before for existing services (Navigation/
Commands, `ADR-0022`; Commands/Event Bus, `ADR-0037`/`RD-0039`):

- `IReportingService` vs. `IExportService`/`IImportService` — no method
  overlap; distinguished explicitly (`ADR-0040`, `Release Architecture.
  md`'s Cross-Service Orthogonality table) by presentation-vs-round-trip
  guarantee.
- `ISettingsProvider` vs. `IConfigurationProvider` (existing) — no
  method overlap; `ISettingsProvider` is read-write, `IConfigurationProvider`
  remains read-only (`ADR-0042`).
- `INotificationDispatcher` vs. `IEventBus` (existing) — no method
  overlap; `INotificationDispatcher` is a distinct, higher-level
  contract built *on* `IEventBus`, not a re-exposure of its own
  `Subscribe`/`PublishAsync` surface under new names (`ADR-0046`).
- `IAuditRecorder`/`IAuditQuery` vs. `ILogger` (existing) and
  `IDiagnosticsProvider` (existing) — no method overlap; none of the
  three shares a method signature or purpose, confirmed against
  `Platform Service Contracts.md`'s own explicit distinctions
  (`ADR-0045`).
- `IPersistenceStore` vs. `IExportable`/`IExportService` — no method
  overlap; `IPersistenceStore` is key/value, `IExportable` is
  `Stream`-based (`ADR-0051`).
- `ILicenseProvider.HasCapability` vs. `IPermissionEvaluator.HasPermission`
  — the two most surface-similar signatures in the entire catalogue
  (both a boolean capability check). Reviewed explicitly: `HasCapability`
  answers "is this feature enabled by the current license," a
  product-entitlement question with no acting principal involved;
  `HasPermission` answers "may this specific principal perform this
  specific action," an authorization question. Confirmed distinct in
  purpose despite the surface-level signature similarity — named
  explicitly here precisely because it is the one pair a future reader
  might plausibly conflate.

**Finding: Satisfied.** No two proposed interfaces expose the same
responsibility under different names. The one pair worth a reader's
deliberate attention (`ILicenseProvider`/`IPermissionEvaluator`) is
documented above so the distinction is never rediscovered from scratch.

## 4. Duplicated Responsibilities

**Rule.** No two of the nine proposed services (or a proposed service
and an existing one) should exist to solve the same underlying problem —
broader than interface-level overlap (check 3, above), this checks
whether the *service itself*, regardless of its interface's exact
shape, duplicates another's reason to exist.

**Check**, service by service, against both the other eight proposed
services and the existing platform:

- **Persistence** — no existing service persists platform state (the
  bootstrap-era `JsonProjectRepository` is dead code, confirmed in
  `Release Architecture.md`'s Repository Investigation). No overlap.
- **Reporting** — no existing service produces formatted output; overlap
  with Export/Import considered and rejected explicitly (`ADR-0040`).
- **Identity & Permissions** — no existing service has any authorization
  concept. No overlap.
- **Notifications** — built deliberately *on* the Event Bus rather than
  beside it, specifically to avoid duplicating its dispatch machinery
  (`ADR-0046`) — the one case in this release where "building on top of"
  rather than "beside" was the explicit mechanism for avoiding
  duplication.
- **Settings** — overlap with Configuration considered and rejected
  explicitly (`ADR-0042`); the two solve genuinely different problems
  (immutable startup data vs. runtime-mutable state).
- **Audit** — overlap with Logging and Diagnostics considered and
  rejected explicitly (`ADR-0045`); all three describe "what happened"
  but answer different questions (diagnostic vs. live-state-snapshot vs.
  durable-history), per `Platform Service Contracts.md`'s own explicit
  distinction.
- **Licensing** — no existing service. No overlap.
- **Export/Import** — overlap with Persistence and Reporting each
  considered and rejected explicitly (`ADR-0051`, `ADR-0040`).
- **REST API** — no existing service exposes platform capability over a
  network; explicitly reuses (never duplicates) the Command Framework
  for dispatch (`ADR-0048`) and Background Services for its own
  lifecycle (`ADR-0047`), rather than inventing parallel mechanisms for
  either.

**Finding: Satisfied.** Every proposed service has a distinct reason to
exist; every genuine risk of duplication identified during this and the
prior architecture review was resolved by an explicit, named ADR
decision rather than left ambiguous.

## Overall Confirmation

All four Engineering Governance properties required before
implementation begins are satisfied by the proposed `v0.6.0` design as
reviewed:

| # | Property | Result |
|---|---|---|
| 1 | Four-layer dependency rules | Satisfied |
| 2 | No circular dependencies | Satisfied |
| 3 | No public interface overlap | Satisfied |
| 4 | No duplicated responsibilities | Satisfied |

This confirmation is itself subject to `WP 6.8`'s own re-verification
once real implementation exists — a design-time check confirms the
*proposed* contracts satisfy these properties; only `WP 6.8`'s own
file-system-level review can confirm the *shipped* code still does,
per this release's own repeated re-derivation discipline.

## Related Documents

`Release Architecture.md`; `Platform Service Dependency Diagram.md`;
`Public Interface Catalogue.md`; `Platform Service Contracts.md`;
`Required ADRs.md`; `docs/adr/ADR-0022`, `ADR-0023`, `ADR-0037`,
`ADR-0040` through `ADR-0051` (as anticipated); `docs/academy/06
Engineering Standards/Engineering Governance.md` §2.
