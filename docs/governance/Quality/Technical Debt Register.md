# Technical Debt Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Technical Debt Register |
| **Purpose** | The single, consolidated list of every disclosed debt item across the entire Claude-developed history of TempestOS — pulled from each Work Package retrospective's own "Architectural Debt Assessment" section and `WP 4.2D`'s own consolidated review, updated to current status as of this baseline. |
| **Scope** | Every debt item explicitly named in a Work Package retrospective's "Architectural Debt Assessment" section, `docs/releases/v0.4.0/Platform Services Architecture Review.md`'s "Remaining Technical Debt" section, or an ADR's own disclosed, accepted trade-off. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | Every retrospective under `docs/academy/03 Work Packages/`; `docs/releases/v0.4.0/Platform Services Architecture Review.md`. |
| **Review Frequency** | Updated whenever a Work Package resolves, worsens, or discloses a new debt item — every retrospective's own "Architectural Debt Assessment" section is the trigger. |
| **Last Reviewed** | 2026-07-28 (WP 5.2, Diagnostics Improvements) — TD-02 resolved (`CompositeLogSink`); TD-01 reassessed and re-scoped forward again (not migrated — the legacy code it concerns remains dead). |
| **Related Documents** | `docs/releases/v0.4.0/Risks.md`; `Validation Register.md`; `Hosted Services Register.md`; `Plugin Register.md`; `docs/security/Platform Security Review v0.5.0.md`; `docs/security/Security Roadmap.md`; `docs/architecture/Command Framework Architecture.md`; `docs/architecture/Diagnostics Architecture.md`. |
| **Related ADRs** | ADR-0009, ADR-0021, ADR-0024, ADR-0025, ADR-0026, ADR-0028, ADR-0029, ADR-0032, ADR-0037, ADR-0039. |
| **Related Academy Articles** | Every Work Package retrospective's own "Trade-offs" and "Architectural Debt Assessment" sections. |
| **Coverage Status** | Complete as of this baseline — every debt item disclosed by any retrospective through `WP 4.5`'s implementation is represented below, either as still Open or Resolved with the resolving Work Package named. |

---

## Governing Distinction

This project consistently distinguishes **debt** (something that should
eventually be fixed, currently costing nothing to leave alone) from a
**disclosed, accepted trade-off** (a deliberate design exclusion, not
expected to ever need fixing unless a real consumer need emerges). Both
are tracked below, in separate tables, because conflating them would
misrepresent deliberate, reasoned scope decisions as unaddressed problems.

## Entries — Technical Debt (Expected to Eventually Be Addressed)

| # | Debt Item | Since | Owning Work Package | Status |
|---|---|---|---|---|
| TD-01 | Two logging mechanisms coexist (`ILogger` vs. legacy `LoggingService`) | WP 2.6 | WP 5.2 assessed this (formerly WP 4.8); decided **not** to migrate — re-scoped forward again, no new owning Work Package named | **Open — reassessed, not resolved.** `WP 5.2`'s own investigation confirmed `Program.cs` has not called the legacy path since `WP 5.0D`; migrating code with zero live callers was judged pure risk with no behavioural benefit (`D-020`). Revisit trigger: the legacy bootstrap code is either genuinely revived or deliberately deleted. |
| TD-02 | Single-sink logging (no composite `ILogSink` fan-out) | WP 2.6 | WP 5.2 (formerly WP 4.8) | **Resolved** — `CompositeLogSink` (`ADR-0039`, `Diagnostics Architecture.md`) fans a log entry out to any number of child sinks, isolating one child's own write failure from every other, with no change to `Logger`, `ILoggerFactory`, or any existing `ILogger` consumer. |
| TD-03 | No disposal tracking for `AddInstance`-registered or reflection-constructed singletons implementing `IDisposable` | WP 2.4 / ADR-0009 | None named yet | Open — not urgent; no current platform service is disposable |
| TD-04 | `IHostedService` naming proximity to `Microsoft.Extensions.Hosting.IHostedService` | WP 4.0 / ADR-0024 | Revisit trigger: real usage evidence | **Open — trigger not yet met.** WP 4.5 implemented the infrastructure but zero real hosted services exist yet (see `Hosted Services Register.md`); the "real usage evidence" this item's revisit trigger names has still not arrived. |
| TD-05 | Parameterless-constructor-only constraint on discovered modules | WP 4.1 | Partially addressed — WP 4.4A/4.4B (ADR-0027) | **Partially resolved.** A module carrying `[ModuleMetadata]` may now declare a DI-resolvable constructor; a module without the attribute remains constrained exactly as before. This is a deliberate, additive lift, not a full removal of the constraint. |
| TD-06 | Plugins root directory (`Plugins/`) and manifest file name (`plugin.manifest.json`) are fixed conventions, not configurable | WP 4.2 | None named yet | Open — disclosed as a purely additive future enhancement, not a current limitation with a known cost |
| TD-07 | Navigation's `Tempest.Core` placement is an open architectural question | WP 4.2D (named), pre-existing since v0.4.0 planning | WP 5.0A (formerly WP 4.6A) | **Resolved** — WP 5.0A's own Repository Investigation and `ADR-0031` settle this: the Navigation *model* belongs in `Tempest.Core`; rendering is `Tempest.App`'s own responsibility. Stale "Open"/`WP 4.6A` labels found and corrected here, `WP 5.0D`, having survived unnoticed through three prior Work Packages (`WP 5.0A`–`WP 5.0C`) that each resolved or built on this exact question without this register being updated to match — a disclosed governance-debt finding in its own right, not a new architectural gap. |
| TD-08 | Background Services would need to extend `Host Lifecycle.md`'s phase table a second time | WP 4.2D (named as future work) | WP 4.5 | **Resolved** — WP 4.5 implemented Phases 8.1/10.1 exactly per ADR-0029/ADR-0030, no renumbering. See `Risks.md` R1/R4, both now Retired. |
| TD-09 | No isolation boundary exists between a loaded plugin and a first-party module — a plugin gets identical DI-container trust once its assembly loads. **Scope widened, WP 5.1A**: this same gap now also applies to the Command Framework — a plugin's own command handler/descriptor is indistinguishable from a first-party one once registered (`Command Framework Architecture.md`'s own Security Review). One root cause, two affected surfaces, not two separate debt items. | Implicit since WP 4.2/ADR-0025/ADR-0026; named explicitly as security debt by WP 5.0S; scope widened WP 5.1A | `WP 6.1` built the mechanism (`IPermissionEvaluator`, `ADR-0044`); no owning Work Package yet for the actual retrofit | **Open — mechanism now exists, not yet applied.** `WP 6.1` (Permissions & Identity) deliberately built only the single authorization enforcement point, not a retrofit of plugin loading itself — inserting an enforcement call into plugin/module trust boundaries was outside that Work Package's own brief. Revisit trigger unchanged: real third-party plugin support, now addressable via `IPermissionEvaluator.RequirePermission` rather than a new mechanism. See `docs/security/Platform Security Review v0.5.0.md` Finding SEC-01, `Security Roadmap.md` item 1. |
| TD-10 | `NavigationService.Unregister` performs no ownership check — any caller can unregister any other component's navigation item by ID | WP 5.0B/ADR-0032; named explicitly as security debt by WP 5.0S | `WP 6.1` built the mechanism (`IPermissionEvaluator`, `ADR-0044`); no owning Work Package yet for the actual retrofit | **Open — mechanism now exists, not yet applied.** Closing this requires one `RequirePermission` call inserted into `NavigationService.Unregister` itself — a change to already-shipped `v0.5.0` architecture `WP 6.1`'s own brief ("Implement: `WP 6.1` — Permissions & Identity") did not authorise. See `docs/security/Platform Security Review v0.5.0.md` Finding NAV-1, `Security Roadmap.md` item 2. |
| TD-11 | Command and Navigation registration-order squatting: "first registration wins" rejects a *later* duplicate but does not establish the *first* registrant was the intended owner of a well-known Id — a plugin whose module Id sorts earlier (by `ModuleLifecycleManager`'s existing ascending-Id initialisation order) can legitimately claim a well-known command or navigation Id before its real owner registers | Pre-existing for Navigation since WP 5.0B/ADR-0032 (undetected until now); newly designed-in for Commands, WP 5.1A; both disclosed together by WP 5.1A's own Security Review (Finding CMD-1) | `WP 6.1` built the mechanism (`IPermissionEvaluator`, `ADR-0044`); no owning Work Package yet for the actual retrofit | **Open — mechanism now exists, not yet applied.** An ownership/priority/reservation model would itself need to call `IPermissionEvaluator` at the Command/Navigation registration path — not built by `WP 6.1`, which scoped itself to the enforcement point alone. See `docs/architecture/Command Framework Architecture.md` Finding CMD-1, `docs/security/Security Roadmap.md` item 10. |

**Total: 11 tracked debt items — 3 Resolved, 1 Partially resolved, 7 Open.**

## Entries — Disclosed, Accepted Trade-offs (Not Expected to Need Fixing Unless a Real Need Emerges)

| # | Trade-off | Disclosed By | Revisit Trigger |
|---|---|---|---|
| AT-01 | No automatic unsubscription on module stop/dispose (Event Bus) | ADR-0028 / WP 4.4D | A real, demonstrated need |
| AT-02 | Subscriber references held strongly for the Event Bus's whole lifetime | ADR-0028 / WP 4.4D | A real, demonstrated need |
| AT-03 | Exact-event-type-only dispatch, no polymorphic dispatch (Event Bus) | RD-0021 / ADR-0028 | A real, demonstrated need |
| AT-04 | No ongoing supervision/monitoring of a hosted service's own work after `StartAsync` returns | RD-0026 / ADR-0029 | A real, demonstrated need |
| AT-05 | No automatic restart/backoff for an isolated hosted service failure | RD-0029 / ADR-0021/ADR-0029 | A real, demonstrated need |
| AT-06 | `src/Plugins/` remains empty — no real plugin built yet | WP 4.2 (by design) | The first Work Package that ships a real plugin (see `Plugin Register.md`) |
| AT-07 | Zero real hosted services exist beyond the infrastructure | WP 4.5 (by explicit scope decision) | The first Work Package that ships a real hosted service (see `Hosted Services Register.md`) |

**Total: 7 disclosed trade-offs, none requiring action absent a real,
demonstrated need.**

## Cross-Reference Check

Every item above is traceable to a specific retrospective or ADR cited in
its own row. TD-08's resolution is cross-checked directly against
`Risk Register.md`'s R1/R4 (both Retired on the same date, by the same
Work Package) — consistent, no discrepancy. No debt item was found in any
retrospective that is missing from this consolidated list.
