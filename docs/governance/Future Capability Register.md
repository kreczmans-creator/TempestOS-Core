# Future Capability Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Future Capability Register |
| **Purpose** | The single, permanent, authoritative list of every identified future TempestOS capability — replacing every informal "Car Park" discussion, scattered Technical Debt Register trade-off, and per-Work-Package "Future Capability Recommendations" document with one register future roadmap planning and Work Package selection is drawn from. |
| **Scope** | Every future capability identified from a release retrospective, a Future Capability Recommendations document, a Technical Debt Register entry, a governance review, an architecture discussion, or existing project documentation, as of `WP 7.0A`. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | This document, from `WP 7.0A` onward. Prior to this Work Package, the same information existed only in fragments — see each entry's own "Sourced From" field for where it previously lived. |
| **Review Frequency** | Updated whenever a new future capability is identified (by any Work Package, retrospective, or review), or whenever an existing capability's Status changes (a Work Package begins, completes, or a capability is formally deferred/rejected). |
| **Last Reviewed** | 2026-07-30 (`WP 7.0B`, Engineering Foundation Planning & Capability Architecture) — added `FCR-0029` through `FCR-0033`, the five cross-cutting Engineering Foundation frameworks this Work Package's own dependency analysis identified as architecturally necessary before any discipline-specific Engineering Module can begin (see `WP7.0B Engineering Foundation Architecture.md`). Each is marked **Inferred**, not Verified — architectural necessity reasoning, not a capability named in a prior document, per the same discipline `FCR-0026` already applied. Previously reviewed 2026-07-30 (`WP 7.0A`, established). |
| **Related Documents** | `Capability Categories.md`; `Product Roadmap.md`; `VISION.md`; `docs/governance/Quality/Technical Debt Register.md`; `docs/security/Security Roadmap.md`; `docs/security/Threat Model.md`; `docs/releases/v0.7.0/WorkPackages.md`; the eight `WP6.x Future Capability Recommendations.md` documents under `docs/releases/v0.6.0/`. |
| **Related ADRs** | ADR-0013, ADR-0040, ADR-0044, ADR-0045, ADR-0046, ADR-0049, ADR-0050, ADR-0052 — see individual entries. |
| **Related Academy Articles** | `WP6.8-platform-services-integration-review.md` §6 (the direct source of `FCR-0001`, `FCR-0003`, `FCR-0004`, `FCR-0005`, `FCR-0006`); `WP7.0B-engineering-foundation-planning-and-capability-architecture.md`. |
| **Coverage Status** | Complete for every future capability traceable to an existing, real document, plus five architecturally-inferred Engineering Foundation entries (`WP 7.0B`). **Not** claimed complete for the five Engineering Discipline categories with no identified candidate — see Coverage Note, below, and `Capability Categories.md`'s own identical disclosure. |

---

## Governing Rules

1. **Identifiers are permanent.** Once assigned, an `FCR-NNNN` identifier
   is never reused or renumbered, even if the capability it names is
   later rejected or merged into another entry — mirroring `ADR`/`RD`
   numbering discipline elsewhere in this governance suite.
2. **This register replaces informal tracking, not existing governance
   registers.** A capability already tracked as Technical Debt Register
   debt or a disclosed trade-off keeps its own `TD-NN`/`AT-NN` identifier
   there — this register adds a roadmap-facing `FCR-NNNN` entry
   cross-referencing it, it does not duplicate or supersede it.
3. **Do not invent implementation details.** Every entry below is
   sourced from an existing, cited document. Where the source material
   is a category-level ambition rather than a specific design (the nine
   Engineering Discipline categories in `Capability Categories.md`, most
   conspicuously), no entry is invented to fill the gap — see Coverage
   Note.
4. **Merge duplicates.** Several Technical Debt Register items and
   `WP6.x Future Capability Recommendations.md` documents describe the
   same underlying capability from different angles (for example,
   `TD-09`/`TD-10`/`TD-11` and Security Roadmap items 1/2/10 are one
   capability — a plugin/registration trust boundary — not three). Each
   is merged into a single `FCR` entry citing every source.

## Entries

### Platform

#### FCR-0001 — Plugin & Registration Trust Isolation Retrofit

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Insert real `IPermissionEvaluator` enforcement calls at the three call sites `WP 6.1` deliberately left unretrofitted: plugin/module load trust boundary, `NavigationService.Unregister`, and Command/Navigation registration-order ownership. The enforcement mechanism itself already exists (`ADR-0044`); this capability is applying it, not building it. |
| **Status** | Identified, not started |
| **Priority** | High — three Security Roadmap items (1, 2, 10) name this as a hard prerequisite before third-party plugins ship |
| **Business Value** | High once real third-party plugin authors exist; zero cost to defer while `src/Plugins/` remains empty |
| **Engineering Effort** | Medium — three call sites, one shared enforcement mechanism already built |
| **Dependencies** | None technical; gated on a real trigger (a genuine third-party plugin) per Security Principle 7 (do not build security machinery ahead of real need) |
| **Proposed Target Release** | Whenever `FCR-0002` (Third-Party Plugin Ecosystem Enablement) is scheduled — must land no later than that, not after |
| **Related ADRs** | ADR-0044 |
| **Related Work Packages** | `WP 5.0S` (found), `WP 5.1A` (widened scope), `WP 6.1` (built the mechanism), `WP 6.8` (recommended for `v0.7.0`) |
| **Academy Impact** | Would warrant its own concept-guide update to `08-failure-isolation.md`/security case studies once implemented |
| **Notes** | Sourced from `Technical Debt Register.md` TD-09/TD-10/TD-11; `Security Roadmap.md` items 1, 2, 10; `WP6.8-platform-services-integration-review.md` §6. This is `v0.7.0` candidate `C3` in `docs/releases/v0.7.0/WorkPackages.md`. |

#### FCR-0002 — Third-Party Plugin Ecosystem Enablement

| Field | Value |
|---|---|
| **Category** | Integrations |
| **Description** | Ship the first real, non-first-party plugin, exercising the Plugin Manifest infrastructure (`WP 4.2`) end to end with a genuine external author rather than a sample. |
| **Status** | Identified, not started — infrastructure exists, no real plugin exists |
| **Priority** | Low until a concrete third-party plugin author or use case exists |
| **Business Value** | Unknown until a real plugin author's need is known |
| **Engineering Effort** | Unknown — depends entirely on the real plugin's own scope |
| **Dependencies** | `FCR-0001` must land first or alongside |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | ADR-0025, ADR-0026 |
| **Related Work Packages** | `WP 4.2`, `WP 4.2B`, `WP 4.2C` |
| **Academy Impact** | Would be this platform's first real "Plugin Register" entry (`Plugin Register.md`, currently empty by design, `AT-06`) |
| **Notes** | Sourced from `Technical Debt Register.md` AT-06; `Plugin Register.md`. |

#### FCR-0003 — REST API Authentication Mechanism

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Design and implement a genuine authentication mechanism (API keys, OAuth/OIDC, or mutual TLS) for the REST API, replacing the current trusted, unauthenticated `X-Identity-Id` header. |
| **Status** | Identified, not started |
| **Priority** | High — this platform's first network-facing surface, currently mitigated only by binding to loopback |
| **Business Value** | High — required before any deployment beyond a trusted local network |
| **Engineering Effort** | Medium-High — a genuine architecture decision plus implementation, mirroring the rigor `WP 6.1`'s own identity model required |
| **Dependencies** | Builds on the existing Identity & Permissions model (`WP 6.1`); may need to revisit whether identity remains local-only (`ADR-0043`) once real authentication is designed |
| **Proposed Target Release** | Once a concrete deployment scenario beyond a trusted local network exists |
| **Related ADRs** | ADR-0043, ADR-0049, ADR-0052 |
| **Related Work Packages** | `WP 6.3` (disclosed the gap), `WP 6.8` (recommended for `v0.7.0`) |
| **Academy Impact** | Would warrant a new Academy concept guide or a substantial update to the REST API implementation retrospective |
| **Notes** | Sourced from `Technical Debt Register.md` TD-13; `WP6.3 Future Capability Recommendations.md` Recommendation 1; `Security Roadmap.md` item 6/7. `v0.7.0` candidate `C4` (paired with `FCR-0004`). |

#### FCR-0004 — REST API Transport Security (TLS)

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Configure TLS for the REST API's Kestrel listener. |
| **Status** | Identified, not started |
| **Priority** | High — paired with `FCR-0003`; both are named together as the same deployment-scenario trigger |
| **Business Value** | High once any deployment scenario beyond local development exists |
| **Engineering Effort** | Low-Medium — primarily configuration once a certificate/deployment story exists |
| **Dependencies** | Best designed alongside `FCR-0003`, not separately |
| **Proposed Target Release** | Once a concrete deployment scenario beyond local development exists |
| **Related ADRs** | ADR-0049 |
| **Related Work Packages** | `WP 6.3` (disclosed the gap), `WP 6.8` (recommended for `v0.7.0`) |
| **Academy Impact** | None beyond updating the REST API concept guide once resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-14; `WP6.3 Future Capability Recommendations.md` Recommendation 2. `v0.7.0` candidate `C4`. |

#### FCR-0005 — Governance Register Health-Check Tooling

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A lightweight, periodic (not only closing-review-triggered) check — a script or a documented convention — that flags a governance register whose own "Last Reviewed" Work Package predates the most recent Work Package that actually touched its subject area. |
| **Status** | Identified, not started |
| **Priority** | Medium — this exact pattern (`Governance Register.md` going stale for nine Work Packages) has now recurred twice (`v0.5.0`, `v0.6.0`) |
| **Business Value** | Medium — prevents a class of documentation-drift finding that has, twice now, taken a whole-release closing review to catch |
| **Engineering Effort** | Low-Medium — likely a simple script comparing each register's own "Last Reviewed" Work Package number against the highest Work Package number that touched its declared Scope |
| **Dependencies** | None |
| **Proposed Target Release** | `v0.7.0` candidate |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 5.3` (first instance of this exact drift pattern), `WP 6.7`/`WP 6.6`/`WP 6.8` (second instance) |
| **Academy Impact** | Would warrant a new "Design Patterns" or "Engineering Standards" article documenting the convention once built |
| **Notes** | Sourced from `WP6.8-platform-services-integration-review.md` §6. `v0.7.0` candidate `C2`. |

#### FCR-0006 — `Runtime`↔`Diagnostics` Namespace Reference Resolution

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Formally resolve the one open architectural finding `WP 6.8` disclosed: `Tempest.Core.Diagnostics` imports `Tempest.Core.Runtime` for a single enum (`HostState`), a mutual namespace reference a literal reading of `ADR-0023` would flag. Either document it as an accepted, narrow `ADR-0023` exception, or relocate `HostState` to a neutral namespace. |
| **Status** | Identified, not started — shipped safely since `v0.5.0`, non-blocking |
| **Priority** | Medium — a genuine, disclosed architectural note, not urgent |
| **Business Value** | Low direct value; closes a standing architectural finding |
| **Engineering Effort** | Low — either an ADR documenting the exception, or a small, mechanical namespace move |
| **Dependencies** | None |
| **Proposed Target Release** | `v0.7.0` candidate |
| **Related ADRs** | ADR-0023 (the rule this finding is an exception to) |
| **Related Work Packages** | `WP 5.2` (introduced the reference), `WP 6.8` (found and disclosed it) |
| **Academy Impact** | Would warrant a small update to the Diagnostics concept guide once resolved |
| **Notes** | Sourced from `WP6.8-platform-services-integration-review.md` §6; `PROJECT_STATUS.md`'s own Current Work Package section. `v0.7.0` candidate `C1`. |

#### FCR-0007 — Native Query/Filter Capability for Persistence

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Extend `IPersistenceStore` (or introduce a companion abstraction) with native query/filter capability, rather than the current key-lookup-plus-full-enumeration shape `IAuditQuery` builds its own client-side filtering over today. |
| **Status** | Identified, not started |
| **Priority** | Low — deliberately not extended by `WP 6.5`; no measured performance problem exists yet |
| **Business Value** | Would grow with real data scale; currently unmeasured |
| **Engineering Effort** | Medium-High — a genuine abstraction change touching every `IPersistenceStore` consumer (Settings, Audit) |
| **Dependencies** | None technical; gated on a real, measured performance problem or a concrete scale requirement |
| **Proposed Target Release** | Not yet scheduled — explicit revisit trigger: a real, measured performance problem |
| **Related ADRs** | ADR-0041, ADR-0045 |
| **Related Work Packages** | `WP 6.4` (shipped the current shape), `WP 6.5` (confirmed the limitation via explicit Persistence Validation) |
| **Academy Impact** | Would warrant updates to the Settings/Audit concept guides once resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-12; `docs/releases/v0.6.0/Risk Register.md` R8; `WP6.5 Future Capability Recommendations.md` Recommendation 2. |

#### FCR-0008 — Legacy Logging Consolidation

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Resolve the coexistence of `ILogger` and the legacy `LoggingService` — either by migrating or deliberately deleting the legacy path. |
| **Status** | Identified, repeatedly reassessed, not started |
| **Priority** | Low — `Program.cs` has not called the legacy path since `WP 5.0D`; no behavioural benefit identified from migrating dead code |
| **Business Value** | Low unless the legacy path is genuinely revived |
| **Engineering Effort** | Low if deleted; Medium if migrated |
| **Dependencies** | None |
| **Proposed Target Release** | Revisit trigger: the legacy bootstrap code is either genuinely revived or deliberately deleted |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 2.6` (origin), `WP 5.2` (reassessed, `D-020`) |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-01. |

#### FCR-0009 — Disposal Tracking for DI-Registered Singletons

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Add disposal tracking for `AddInstance`-registered or reflection-constructed singletons implementing `IDisposable`. |
| **Status** | Identified, not started |
| **Priority** | Low — no current platform service is disposable |
| **Business Value** | Would grow if a future platform service holds an unmanaged or disposable resource |
| **Engineering Effort** | Medium — a DI container change |
| **Dependencies** | None |
| **Proposed Target Release** | Revisit trigger: a real disposable platform service is introduced |
| **Related ADRs** | ADR-0009 |
| **Related Work Packages** | `WP 2.4` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-03. |

#### FCR-0010 — Configurable Plugin Root/Manifest Conventions

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Make the plugins root directory (`Plugins/`) and manifest file name (`plugin.manifest.json`) configurable rather than fixed conventions. |
| **Status** | Identified, not started |
| **Priority** | Low — disclosed as a purely additive future enhancement |
| **Business Value** | Low until a real deployment scenario needs a different convention |
| **Engineering Effort** | Low |
| **Dependencies** | None |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | None |
| **Related Work Packages** | `WP 4.2` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-06. |

#### FCR-0011 — `IHostedService` Naming Disambiguation

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Resolve the naming proximity between `Tempest.Core.BackgroundServices.IHostedService` and `Microsoft.Extensions.Hosting.IHostedService`, now that both coexist in the same solution (`WP 6.3`'s ASP.NET Core dependency). |
| **Status** | Identified — revisit trigger arguably now met |
| **Priority** | Low-Medium — no confusion has actually been reported yet |
| **Business Value** | Low direct value; reduces a real, now-materialised naming-collision risk |
| **Engineering Effort** | Low-Medium — a rename touches every implementer |
| **Dependencies** | None |
| **Proposed Target Release** | Not yet scheduled — a judgment call for a future Work Package |
| **Related ADRs** | ADR-0024, ADR-0049 |
| **Related Work Packages** | `WP 4.0` (origin), `WP 6.3` (trigger arguably met) |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-04. |

#### FCR-0012 — Reporting Delivery-Channel, History & Scheduling Capability

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A separate consuming layer over `IReportingService.GenerateAsync` providing delivery (email/webhook/push), durable report history, generation progress/streaming for long-running renderers, and scheduled/recurring generation — explicitly not a change to `IReportingService` itself. |
| **Status** | Identified, not started |
| **Priority** | Low — no concrete delivery, history, or scheduling requirement exists yet |
| **Business Value** | Would grow with a real consuming engineering module naming a concrete need |
| **Engineering Effort** | Medium-High — likely several separable capabilities, not one |
| **Dependencies** | `IReportingService` (shipped, `WP 6.0`); likely benefits from `FCR-0013`'s own delivery-channel work if built together |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0040 |
| **Related Work Packages** | `WP 6.0` |
| **Academy Impact** | Would warrant a new Reporting concept-guide section once any part ships |
| **Notes** | Sourced from `Technical Debt Register.md` AT-09; `WP6.0 Future Capability Recommendations.md` Recommendations 4 and 5. |

#### FCR-0013 — Notification History/Inbox & Delivery-Channel Capability

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A durable notification history/inbox (a Shell notification centre, or an audit-adjacent record of what was shown to a user), plus first-party delivery-channel handler implementations (email, webhook, push), reusing `NotificationSeverity`/`Category` rather than a parallel classification. |
| **Status** | Identified, not started |
| **Priority** | Low — an explicit, approved-contract scope exclusion for `v0.6.0`, not a current defect |
| **Business Value** | Would grow with a real UI Shell notification-centre requirement or external delivery need |
| **Engineering Effort** | Medium |
| **Dependencies** | `NotificationDispatcher` (shipped, `WP 6.2`); may integrate with `FCR-0003`/REST API for webhook/callback delivery |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0046 |
| **Related Work Packages** | `WP 6.2` |
| **Academy Impact** | Would warrant a new Notifications concept-guide section once any part ships |
| **Notes** | Sourced from `Technical Debt Register.md` AT-08; `WP6.2 Future Capability Recommendations.md` Recommendations 2, 3, 4, 5. |

#### FCR-0014 — Advanced Settings Capability (Sensitive Values, Per-Principal, Strongly-Typed)

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Three related, separately-scoped Settings enhancements: a sensitive-value flag on `ISettingDefinition` (once a real sensitive setting is named); per-principal (user-specific) settings distinct from global ones; and a strongly-typed settings abstraction beyond the current key-lookup shape. |
| **Status** | Identified, not started |
| **Priority** | Low — each has its own named trigger, none met yet |
| **Business Value** | Would grow once a real sensitive setting, per-user preference, or type-safety need is named |
| **Engineering Effort** | Low (sensitive-value flag) to Medium (per-principal, strongly-typed) |
| **Dependencies** | `ISettingsService`/`IPersistenceStore` (shipped, `WP 6.4`); per-principal settings would depend on `WP 6.1`'s Identity model |
| **Proposed Target Release** | Revisit trigger: a real sensitive setting, per-user preference, or type-safety need is named |
| **Related ADRs** | ADR-0041, ADR-0042 |
| **Related Work Packages** | `WP 6.4` |
| **Academy Impact** | Would warrant Settings concept-guide updates once any part ships |
| **Notes** | Sourced from `WP6.4 Future Capability Recommendations.md` Recommendations 2 and 3. |

#### FCR-0015 — Export Artifact Compression & Encryption

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Compression and/or encryption of exported artifact content, currently the individual responsibility of each `IExportable` implementation with no content-level policy imposed by `IExportService`/`IImportService`. |
| **Status** | Identified, not started |
| **Priority** | Low — an explicit, approved-contract scope exclusion for `v0.6.0` |
| **Business Value** | Would grow with a concrete deployment scenario naming artifact confidentiality or size as a requirement |
| **Engineering Effort** | Medium |
| **Dependencies** | `IExportService`/`IImportService` (shipped, `WP 6.7`) |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0051 |
| **Related Work Packages** | `WP 6.7` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` AT-11; `WP6.7 Future Capability Recommendations.md`. |

#### FCR-0016 — Export Schema Migration/Upgrade Path

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A migration path for `IImportService.ImportAsync` to upgrade or downgrade an artifact section whose schema version does not exactly match the currently registered `IImportable`, rather than rejecting outright. |
| **Status** | Identified, not started |
| **Priority** | Low — no real, shipped schema version bump exists yet |
| **Business Value** | Would grow once a real backward-compatibility requirement exists |
| **Engineering Effort** | Medium-High |
| **Dependencies** | `IExportService`/`IImportService` (shipped, `WP 6.7`) |
| **Proposed Target Release** | Revisit trigger: a real, shipped schema version bump with a genuine backward-compatibility requirement |
| **Related ADRs** | ADR-0051 |
| **Related Work Packages** | `WP 6.7` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` AT-12; `WP6.7 Future Capability Recommendations.md` Recommendation 3. |

#### FCR-0017 — License File Integrity Verification (Cryptographic Signature)

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Cryptographic signature or tamper-resistance verification of license file contents, currently trusted at face value. |
| **Status** | Identified, not started |
| **Priority** | Low — no concrete distribution channel or tamper/forgery threat model exists yet |
| **Business Value** | Would grow once a real license distribution channel exists |
| **Engineering Effort** | Medium |
| **Dependencies** | `ILicenseValidator`/`ILicenseProvider` (shipped, `WP 6.6`) |
| **Proposed Target Release** | Revisit trigger: a concrete license distribution scenario naming a real tamper/forgery threat model |
| **Related ADRs** | ADR-0050 |
| **Related Work Packages** | `WP 6.6` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-16; `WP6.6 Future Capability Recommendations.md` Recommendation 2. |

#### FCR-0018 — REST Request-Parameter Binding

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | Thread an inbound REST request's own body or query string into the invoked command, rather than every REST-exposed command dispatching only its own parameterless `CreateDefault` factory instance. |
| **Status** | Identified, not started |
| **Priority** | Low — an explicit, approved-contract scope exclusion for `v0.6.0` |
| **Business Value** | Would grow with a real REST-exposed command needing caller-supplied parameters |
| **Engineering Effort** | Medium |
| **Dependencies** | `IApiEndpointRegistry`/`RestApiHostedService` (shipped, `WP 6.3`) |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0047, ADR-0048 |
| **Related Work Packages** | `WP 6.3` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` AT-10; `WP6.3 Future Capability Recommendations.md` Recommendation 4. |

#### FCR-0019 — Explicit Actor Parameter for Cross-Boundary Audit Attribution

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | For any future command needing precise per-request Audit attribution under REST invocation, accept an explicit actor parameter rather than relying on `IAuditRecorder.RecordAsync`'s own ambient-current-principal auto-attribution, which the REST API deliberately never establishes (`ADR-0052`). |
| **Status** | Identified, not started |
| **Priority** | Low — the caller's real identity is not lost, only recorded in a different Audit entry's `Detail` field |
| **Business Value** | Would grow with a real command needing precise per-command REST-invoked Audit attribution |
| **Engineering Effort** | Low-Medium |
| **Dependencies** | `IAuditRecorder` (shipped, `WP 6.5`); `RestApiHostedService` (shipped, `WP 6.3`) |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need |
| **Related ADRs** | ADR-0045, ADR-0052 |
| **Related Work Packages** | `WP 6.3`, `WP 6.5` |
| **Academy Impact** | None until resolved |
| **Notes** | Sourced from `Technical Debt Register.md` TD-15; `WP6.3 Future Capability Recommendations.md` Recommendation 3. |

### Infrastructure

#### FCR-0020 — Secrets-Redaction Logging Convention

| Field | Value |
|---|---|
| **Category** | Infrastructure |
| **Description** | A redaction convention (a marker attribute, a wrapper type, or an `ILogSink`-level filter) for any credential, token, or connection string entering the platform, adopted before the platform's first real secret exists rather than retrofitted after. |
| **Status** | Identified, not started |
| **Priority** | Medium — trigger is authentication (`FCR-0003`) or cloud synchronisation (`FCR-0022`), neither of which exists yet |
| **Business Value** | High once a real secret exists in the codebase; near-zero before then |
| **Engineering Effort** | Low-Medium |
| **Dependencies** | Should land before `FCR-0003` or `FCR-0022`, whichever arrives first |
| **Proposed Target Release** | Revisit trigger: authentication or cloud synchronisation introduces the platform's first real secret |
| **Related ADRs** | None yet |
| **Related Work Packages** | `WP 5.0S` (found, `SEC-02`) |
| **Academy Impact** | Would warrant a new Security case study once designed |
| **Notes** | Sourced from `Security Roadmap.md` item 3. |

#### FCR-0021 — Multi-User / Tenant Isolation Architecture

| Field | Value |
|---|---|
| **Category** | Infrastructure |
| **Description** | A deliberate, ADR-backed decision on how TempestOS achieves multi-user isolation — separate OS processes per user (no DI change) versus a genuine Scoped DI lifetime and per-tenant isolation model (a DI redesign) — before multi-user support is implemented. |
| **Status** | Identified, not started |
| **Priority** | Medium-High strategic value, but explicitly gated — Security Principle 7 forbids building this ahead of real need |
| **Business Value** | High — a prerequisite for any real multi-user deployment |
| **Engineering Effort** | High — potentially a DI container redesign |
| **Dependencies** | The current DI container has no Scoped lifetime at all (`FR-1`); this decision determines whether that changes |
| **Proposed Target Release** | Not yet scheduled — trigger: multi-user support becomes a real, scheduled requirement |
| **Related ADRs** | None yet — this capability's own first deliverable is an ADR |
| **Related Work Packages** | `WP 5.0S` (found, `FR-1`) |
| **Academy Impact** | Would be a major new architectural decision warranting its own Academy case study |
| **Notes** | Sourced from `Security Roadmap.md` item 5; `Threat Model.md` assumption 4. |

#### FCR-0022 — Cloud Synchronisation

| Field | Value |
|---|---|
| **Category** | Infrastructure |
| **Description** | Synchronisation of TempestOS data with a cloud service — no design work exists yet; `Threat Model.md` explicitly names this as one of the assumptions this platform's eventual mission requires planning around. |
| **Status** | Identified, no readiness work — explicitly furthest from the current codebase |
| **Priority** | Low until a concrete design is proposed |
| **Business Value** | Unknown — no concrete scenario named yet |
| **Engineering Effort** | Unknown |
| **Dependencies** | Likely depends on `FCR-0021` (multi-user/tenant model) and `FCR-0003` (authentication) |
| **Proposed Target Release** | Not yet scheduled — `Security Principles.md` Principle 7 explicitly recommends against speculative design |
| **Related ADRs** | None |
| **Related Work Packages** | None yet |
| **Academy Impact** | None until designed |
| **Notes** | Sourced from `Threat Model.md` assumption 8; `Security Roadmap.md`'s own Explicit Non-Recommendations. |

#### FCR-0023 — Offline Synchronisation & Mobile Client Support

| Field | Value |
|---|---|
| **Category** | Infrastructure |
| **Description** | Support for non-desktop clients and offline synchronisation — explicitly named as the capability furthest from anything in the current codebase. |
| **Status** | Identified, no readiness work recommended yet |
| **Priority** | Low |
| **Business Value** | Unknown — no concrete scenario named yet |
| **Engineering Effort** | Unknown, likely High |
| **Dependencies** | Likely depends on `FCR-0022` (cloud synchronisation) |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | None |
| **Related Work Packages** | None yet |
| **Academy Impact** | None until designed |
| **Notes** | Sourced from `Security Roadmap.md` item 9. |

### AI

#### FCR-0024 — AI/Automation Command Invocation

| Field | Value |
|---|---|
| **Category** | AI |
| **Description** | A future AI service or automation script enumerating `ICommandRegistry.Items`, filtering by permission, and invoking by Id — a capability the Command Framework's own design has anticipated as a caller since its own architecture phase, requiring no AI-specific or automation-specific mode of the framework itself. |
| **Status** | Identified — the framework already supports this caller shape; no concrete AI/automation consumer exists yet |
| **Priority** | Low until a concrete AI/automation consumer is proposed |
| **Business Value** | Would depend entirely on the concrete AI/automation use case proposed |
| **Engineering Effort** | Low for the framework itself (already supports Id-based invocation); effort lies entirely in the AI/automation consumer itself, out of this register's own scope |
| **Dependencies** | `ICommandDispatcher`/`ICommandRegistry` (shipped, `WP 5.1A`/`WP 5.1B`) |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | ADR-0036, ADR-0037, ADR-0038 |
| **Related Work Packages** | `WP 5.1A`, `WP 5.1B` |
| **Academy Impact** | `11-command-framework.md` already documents this future-caller shape; a real consumer would warrant a new case study |
| **Notes** | Sourced directly from `docs/architecture/Command Framework Architecture.md`'s own repeated "future AI service" framing (its own "Future: AI Invocation, Automation, Scripting" section) and `Engineering Glossary.md`. This is the one capability in this register describing an already-supported extension point rather than a gap. |

### Commercial

#### FCR-0025 — Commercial Licensing Model (Remote Activation, Floating/Seat-Based, Renewal)

| Field | Value |
|---|---|
| **Category** | Commercial |
| **Description** | Remote validation/activation, floating/seat-based licensing, and a license-renewal/grace-period model — all deliberately out of `WP 6.6`'s own scope, which built licensing *capability* (`ILicenseProvider`) without any commercial *policy*. |
| **Status** | Identified, not started |
| **Priority** | Low — no concrete deployment scenario naming any of the three exists yet |
| **Business Value** | Would grow substantially once TempestOS has real paying customers with a concrete licensing-model requirement |
| **Engineering Effort** | Medium-High — a genuine commercial-policy layer built on top of the existing mechanism |
| **Dependencies** | `ILicenseValidator`/`ILicenseProvider` (shipped, `WP 6.6`); would also benefit from `FCR-0017`'s own integrity verification |
| **Proposed Target Release** | Revisit trigger: a real, demonstrated need for remote activation, concurrent-seat limits, or graceful expiry handling |
| **Related ADRs** | ADR-0050 |
| **Related Work Packages** | `WP 6.6` |
| **Academy Impact** | Would warrant a new Licensing concept-guide section once any part ships |
| **Notes** | Sourced from `Technical Debt Register.md` AT-13; `WP6.6 Future Capability Recommendations.md` Recommendation 4. |

#### FCR-0026 — Defence-Sector / Regulated-Environment Compliance Readiness

| Field | Value |
|---|---|
| **Category** | Commercial |
| **Description** | Compliance posture for operating TempestOS within defence or similarly regulated organisations — the bootstrap-era, currently-dead `ProjectModel` already models a `SecurityLevel` field defaulting to `"BPSS"` (the UK Baseline Personnel Security Standard), a strong, concrete signal of original intent even though the code is currently unreferenced. |
| **Status** | Identified — no active design work; the only concrete trace is dead code |
| **Priority** | Low until a real defence-sector customer or requirement is named |
| **Business Value** | Unknown — entirely dependent on whether a real defence-sector opportunity materialises |
| **Engineering Effort** | Unknown — likely substantial, spanning data classification, access control, and audit requirements well beyond this platform's current Audit Framework |
| **Dependencies** | `FCR-0029` (Project Engine / Secure Project Data Management) most directly, since classification/export-control fields already live in the same dormant subsystem |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | None — this is inferred from dormant, bootstrap-era code, not a Work Package finding |
| **Academy Impact** | None until designed |
| **Notes** | Sourced from `Threat Model.md` assumption 10 and its own note on `ProjectModel`'s `Classification`/`SecurityLevel`/`ExportControlled`/`Customer`/`ContractNumber` fields; `Platform Security Review v0.5.0.md`, File System section. Marked **Inferred**, not Verified — no document confirms this is an actual, current business objective, only that the original bootstrap-era code modelled toward it. |

### Engineering Foundation (Cross-Cutting)

Five entries below were identified by `WP 7.0B`'s own Capability
Dependency Analysis, not sourced from a prior document naming them
directly — each is marked **Inferred**: architectural necessity,
reasoned from what any Engineering Discipline module would structurally
require to be built at all, mirroring how `Capability Categories.md`
itself defines the Engineering Discipline categories. This is
architecture/planning reasoning about shared technical substrate, not
an invented business capability within a discipline (no Mechanical,
Structural, Electrical, Building Services/HVAC, or Manufacturing
capability is registered below, or anywhere in this register — see
Coverage Note).

#### FCR-0029 — Engineering Data Model & Document Management Foundation

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A shared engineering-entity data model — documents, revisions, references, and their relationships — that Requirements, Project, and every future discipline module build on rather than each inventing its own storage shape, mirroring `ADR-0041`'s own "one shared Persistence abstraction, not reinvented per service" precedent for Settings/Audit. |
| **Status** | Identified (`WP 7.0B`) — architectural necessity, no design work exists |
| **Priority** | High relative to other unscheduled capabilities — almost every Engineering Foundation and Engineering Module capability depends on it |
| **Business Value** | Unknown in isolation; high as an enabler, since `FCR-0027`, `FCR-0028`, `FCR-0031`, `FCR-0033`, and every future discipline module would otherwise each invent an incompatible storage shape |
| **Engineering Effort** | Unknown — requires its own Architecture Work Package; needs its own Platform-Service-vs-Module classification (`ADR-0013`) |
| **Dependencies** | None upstream; `IPersistenceStore` (shipped, `WP 6.4`) is a plausible, not guaranteed, storage substrate — `FCR-0007`'s own query-capability gap should be resolved or explicitly ruled out-of-scope before this capability commits to it |
| **Proposed Target Release** | Not yet scheduled — recommended as the first Engineering Foundation capability, before `FCR-0027`/`FCR-0028`/`FCR-0031`/`FCR-0033` |
| **Related ADRs** | ADR-0013, ADR-0041 |
| **Related Work Packages** | None yet |
| **Academy Impact** | Would warrant a new Academy concept guide once designed — a genuinely new data-modelling pattern for this platform |
| **Notes** | Inferred from `Threat Model.md` assumption 1's own generic "CAD, requirements, analysis, verification records" framing and the shared-storage need `FCR-0027`/`FCR-0028` both independently implied in `WP 7.0A`. Not sourced from any document naming this capability directly. |

#### FCR-0030 — Units & Quantities Framework

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A shared representation for dimensioned physical quantities (length, force, temperature, and so on) and unit conversion between them, usable by every future Engineering Discipline module rather than each implementing its own conversion logic. |
| **Status** | Identified (`WP 7.0B`) — architectural necessity, no design work exists |
| **Priority** | High relative to other unscheduled capabilities — a prerequisite for `FCR-0032` and for any Mechanical/Structural/Electrical/HVAC/Materials/Manufacturing capability, once one is identified |
| **Business Value** | Unknown in isolation; mitigates a well-known, industry-wide class of defect (unit-conversion error) that becomes possible the moment more than one discipline module performs a physical calculation |
| **Engineering Effort** | Unknown — likely Medium; this class of library is well-precedented in other engineering software, though TempestOS's own shape has not been designed |
| **Dependencies** | None upstream |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | None yet |
| **Academy Impact** | Would warrant a new Academy concept guide once designed |
| **Notes** | Inferred purely from `Capability Categories.md`'s own Engineering Discipline list (Mechanical, Structural, Electrical, Building Services/HVAC, Materials, Manufacturing all fundamentally operate on dimensioned quantities) — no prior document names this capability. The weakest-sourced entry in this register alongside `FCR-0032`; recommended for explicit confirmation, not assumed, the first time any discipline module is seriously designed. |

#### FCR-0031 — Materials Framework

| Field | Value |
|---|---|
| **Category** | Materials |
| **Description** | Material selection, specification, and traceability capability shared across disciplines — the first identified candidate for the previously-empty `Materials` category in `Capability Categories.md`. |
| **Status** | Identified (`WP 7.0B`) — architectural necessity, no design work exists |
| **Priority** | Medium — enables the `Materials` and `Manufacturing` discipline categories and supports `Quality` (material traceability is a common non-conformance/inspection concern), but not a hard prerequisite for `FCR-0027`/`FCR-0028` |
| **Business Value** | Unknown — dependent on TempestOS's eventual engineering-domain customer base, same as every other discipline-adjacent entry |
| **Engineering Effort** | Unknown — requires its own Architecture Work Package |
| **Dependencies** | `FCR-0029` (data model), `FCR-0030` (units — material properties are dimensioned quantities) |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | None yet |
| **Academy Impact** | None until designed |
| **Notes** | Inferred purely from the existence of the `Materials` category in `Capability Categories.md` and the structural observation that a `Manufacturing` or `Materials` discipline module cannot exist without some shared material-data capability. Not sourced from any document naming a concrete Materials capability — the weakest-sourced entry in this register alongside `FCR-0030`/`FCR-0032`. |

#### FCR-0032 — Engineering Calculation Framework

| Field | Value |
|---|---|
| **Category** | Platform |
| **Description** | A shared calculation/formula execution model usable by every future discipline module (a structural load calculation, an HVAC sizing calculation, an electrical load calculation), mirroring the Command Framework's own "one dispatch mechanism, not reinvented per consumer" precedent (`ADR-0037`/`ADR-0038`), rather than each discipline module inventing its own ad hoc computation approach. |
| **Status** | Identified (`WP 7.0B`) — architectural necessity, no design work exists |
| **Priority** | High relative to other unscheduled capabilities — a prerequisite for any Mechanical/Structural/Electrical/HVAC capability, once one is identified |
| **Business Value** | Unknown in isolation; prevents each future discipline module from independently reinventing calculation infrastructure |
| **Engineering Effort** | Unknown, likely High — a genuine new abstraction, not a small extension of an existing one |
| **Dependencies** | `FCR-0030` (Units & Quantities) — a calculation framework operating on undimensioned raw numbers would reintroduce the exact defect class `FCR-0030` exists to prevent |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | None yet |
| **Academy Impact** | Would warrant a new Academy concept guide once designed |
| **Notes** | Inferred purely from `Capability Categories.md`'s own Engineering Discipline list, the same basis as `FCR-0030` — no prior document names this capability. The weakest-sourced entry in this register alongside `FCR-0030`/`FCR-0031`. |

#### FCR-0033 — Verification & Validation Framework

| Field | Value |
|---|---|
| **Category** | Quality |
| **Description** | A cross-cutting mechanism for recording a pass/fail verification against a requirement or specification, usable by every discipline's own quality process — the first identified candidate for the previously-empty `Quality` category in `Capability Categories.md`, and distinct from Audit (who did what) and Reporting (presentation). |
| **Status** | Identified (`WP 7.0B`) — architectural necessity, no design work exists |
| **Priority** | Medium-High — `Threat Model.md` assumption 1 names "verification records" directly as engineering IP TempestOS will eventually manage |
| **Business Value** | Unknown in isolation; strengthens `FCR-0027` (Requirements Engine) directly, since a requirement without a verification record against it is only half the traceability chain `FCR-0027`'s own description names |
| **Engineering Effort** | Unknown — requires its own Architecture Work Package |
| **Dependencies** | `FCR-0027` (Requirements Engine — verification is meaningless without a requirement to verify against), `FCR-0029` (data model, to attach a verification record to an entity) |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | None yet |
| **Related Work Packages** | None yet |
| **Academy Impact** | Would warrant a new Academy concept guide once designed |
| **Notes** | Inferred from `Threat Model.md` assumption 1's explicit "verification records" phrase and `FCR-0027`'s own description naming "verification" as part of the Requirements Engine's scope — this entry formalises verification as its own cross-cutting capability rather than an implicit part of Requirements alone, since Quality/Manufacturing disciplines would need it independent of Systems Engineering. |

### Systems Engineering

#### FCR-0027 — Requirements Engine

| Field | Value |
|---|---|
| **Category** | Systems Engineering |
| **Description** | A platform capability for requirements management, verification, and traceability across a real engineering programme — named in `PROJECT_STATUS.md`'s own Long-Term Vision as one of two aspirational platform services beyond the current Platform Services phase. |
| **Status** | Identified — aspirational, no design work of any kind exists |
| **Priority** | Unknown — no Work Package has yet been proposed |
| **Business Value** | Unknown — dependent entirely on TempestOS's eventual engineering-domain customer base |
| **Engineering Effort** | Unknown — requires its own Architecture Work Package before any estimate is meaningful |
| **Dependencies** | Requires its own explicit Platform-Service-vs-Module classification decision (`ADR-0013`'s own Future Considerations name this exact capability as an example) before design begins; benefits from `FCR-0029` (shared data model) and `FCR-0033` (Verification & Validation Framework, identified `WP 7.0B`) rather than building either itself |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | ADR-0013 (names this capability directly in its own Future Considerations) |
| **Related Work Packages** | None yet |
| **Academy Impact** | None until designed |
| **Notes** | Sourced from `PROJECT_STATUS.md`'s own Long-Term Vision section; `ADR-0013`'s own Future Considerations; `Threat Model.md` assumption 1 ("requirements, analysis, verification records"). |

### Project Management

#### FCR-0028 — Project Engine / Secure Project Data Management

| Field | Value |
|---|---|
| **Category** | Project Management |
| **Description** | A platform capability for programme/project-level planning, scheduling, and tracking of a real engineering effort, reviving or replacing the bootstrap-era `JsonProjectRepository`/`ProjectModel` subsystem — with encryption at rest, access control, and audit logging for classified/export-controlled fields designed in from the start, not retrofitted. |
| **Status** | Identified — aspirational; the only concrete trace is dormant, unreferenced, pre-Claude-era code |
| **Priority** | Unknown — no Work Package has yet been proposed |
| **Business Value** | Unknown — dependent entirely on TempestOS's eventual engineering-domain customer base |
| **Engineering Effort** | Unknown — requires its own Architecture Work Package; security design (encryption, access control, audit) must be part of that same design phase, not a follow-up, per `Security Roadmap.md` item 4 |
| **Dependencies** | Requires its own explicit Platform-Service-vs-Module classification decision (`ADR-0013`); benefits from `FCR-0021` (multi-user/tenant) if concurrent access is in scope; benefits from `FCR-0029` (shared data model, identified `WP 7.0B`) rather than building its own storage shape |
| **Proposed Target Release** | Not yet scheduled |
| **Related ADRs** | ADR-0013 (names this capability directly in its own Future Considerations) |
| **Related Work Packages** | None yet — `JsonProjectRepository`/`ProjectModel` predate the Claude-developed history |
| **Academy Impact** | None until designed |
| **Notes** | Sourced from `PROJECT_STATUS.md`'s own Long-Term Vision section; `ADR-0013`'s own Future Considerations; `Security Roadmap.md` item 4 (`FS-1`, `FS-2`); `Threat Model.md` assumptions 1–3. |

## Coverage Note

**33 capabilities identified** (`FCR-0001` through `FCR-0033`).
`FCR-0001`–`FCR-0028` were each traceable to a specific, cited,
pre-existing document, established `WP 7.0A`. `FCR-0029`–`FCR-0033`
were added by `WP 7.0B`'s own Capability Dependency Analysis — each
marked **Inferred**, architectural necessity reasoning rather than a
capability named in a prior document, and each says so explicitly in
its own Notes field. No entry was invented to fill a category without
disclosing that it was inferred rather than sourced.

`Materials` and `Quality` each now have exactly one entry (`FCR-0031`,
`FCR-0033` respectively) — both cross-cutting *foundation* capabilities
identified as prerequisites for their category's own eventual discipline
modules, not a discipline-specific capability within either category.
**Five of the nine Engineering Discipline categories still have zero
entries** (Mechanical, Structural, Electrical, Building Services/HVAC,
Manufacturing) — disclosed explicitly, not papered over. `AI` and
`Academy` likewise remain at their `WP 7.0A` count (one and zero
respectively). Identifying real candidates for the five still-empty
disciplines remains recommended as its own future exercise engaging
real engineering-domain stakeholders — `WP 7.0B`'s own Capability
Dependency Analysis deliberately confined itself to cross-cutting
*foundation* reasoning (what any discipline module would structurally
need), not discipline-specific capability invention, exactly as `WP
7.0A` itself declined to do.

## Cross-Reference Check

Every `TD-NN`/`AT-NN` citation above is cross-checked directly against
`Technical Debt Register.md` — no citation references a debt item or
trade-off that does not exist there, and no `TD`/`AT` item disclosing a
genuine future capability (as opposed to a pure code-quality concern with
no roadmap relevance, such as `TD-05`, `TD-07`, `TD-08`, all already
Resolved) is missing an `FCR` entry above. Every `Security Roadmap.md`
item (1 through 10) is represented in at least one entry above, save
items 6 and 7 (authentication, API/networking), both already substantially
addressed by shipped `v0.6.0` capability (`WP 6.1`, `WP 6.3`) and
therefore not carried forward as a future capability in their original
form — item 6's remaining gap is `FCR-0003`. Every `WP6.x Future
Capability Recommendations.md` document's own recommendations are
represented above or explicitly excluded as already-resolved,
implementation-pattern guidance rather than a genuine future capability
(for example, "reuse the existing permission pattern" recommendations
are guidance for a future Work Package's own implementation discipline,
not a capability in their own right, and are not separately registered).
Every `FCR-0029`–`FCR-0033` cross-reference (to each other, and to
`FCR-0001`–`FCR-0028`) is verified consistent in both directions — see
`WP7.0B Capability Dependency Report.md` for the full dependency graph
this check is drawn from.

## Related Documents

`Capability Categories.md`; `Product Roadmap.md`; `VISION.md`;
`docs/governance/Quality/Technical Debt Register.md`;
`docs/security/Security Roadmap.md`; `docs/security/Threat Model.md`;
`ADR-0013`; `PROJECT_STATUS.md`; `docs/releases/v0.7.0/WorkPackages.md`;
`docs/releases/v0.7.0/WP7.0B Capability Dependency Report.md`.
