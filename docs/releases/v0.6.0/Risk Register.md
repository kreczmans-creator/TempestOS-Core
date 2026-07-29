# TempestOS v0.6.0 — Risk Register

## Purpose

Release-level risks that span more than one Work Package, or that
concern this release as a whole rather than a single package's own
execution — mirroring `docs/releases/v0.4.0/Risks.md`'s own established
shape and standing rules. Each Work Package's own entry in
`WorkPackages.md` also names risks specific to itself; this document
does not repeat those, only the ones bigger than any one package. This
is a new register, scoped to `v0.6.0` — it does not extend or replace
`docs/releases/v0.4.0/Risks.md`, whose own rows (`R1`–`R10`) all belong
to the release that retired them.

## How to Use This Document

Update it as the release proceeds. When a risk is retired (mitigated, or
the underlying decision made), mark it retired with the date and the
decision that retired it — **rows are never deleted**, per the
established convention. This register is written before any Work
Package has begun; every row below is therefore a genuinely open risk,
not yet mitigated by anything beyond the architectural review this
document accompanies.

---

## Register

| # | Risk | Affects | Likelihood | Impact | Mitigation | Status |
|---|---|---|---|---|---|---|
| R1 | ~~**Permissions & Identity (`WP 6.1`) has no existing architectural grounding.**~~ This platform has never had an authorization concept; `WP 6.1` must invent one from nothing, mirroring Navigation's own pre-`WP 5.0A` position — historically this project's single highest-risk category. **Update, 2026-07-29 — `WP 6.1` implemented**: `IIdentity`/`IPrincipal`/`ICurrentPrincipalAccessor`/`IPermissionEvaluator`/`Permission` are implemented exactly as drafted; `ADR-0043`/`ADR-0044` are Accepted, resolving the local-only-scope and enforcement-point questions. Residual risk is now narrower and explicitly named: `TD-09`/`TD-10`/`TD-11` remain genuinely Open (the mechanism exists; no consumer has been retrofitted to call it — deliberately out of `WP 6.1`'s own scope), and `CurrentPrincipalAccessor`'s ambient (not `AsyncLocal<T>`) design will need real reconsideration once `WP 6.3` introduces genuine request concurrency. Neither residual item carries the "invent an authorization concept from nothing" risk this row originally named — that part is retired. | `WP 6.1` (closed), residual: `WP 6.3`, `WP 6.5`, and any future Work Package retrofitting `TD-09`/`TD-10`/`TD-11` | Reduced — Medium (residual only) | Reduced — Medium (residual only) | Original mitigation (split into a dedicated architecture phase before implementation) was **not** taken — `WP 6.1` implemented in a single pass per direct instruction, consistent with this review's own recommendation that Identity start first given its risk profile. No architectural surprise resulted. Residual mitigation: `WP 6.3`'s own architecture phase must revisit `IIdentityService.GetPrincipal`'s current "trusts the caller completely" behaviour before exposing it to a network caller (see `ADR-0043`'s own Negative consequence), and must decide whether `CurrentPrincipalAccessor` needs to become request-scoped. | Partially Retired |
| R2 | **REST API (`WP 6.3`) shipping pressure ahead of Identity being genuinely ready.** `WP 6.3` is explicitly blocked on `WP 6.1`, but a REST API is often the most externally-visible, most-demanded capability in a release — schedule pressure could push implementation to start with a stub authorization model "for now," which risks becoming permanent, uncorrected debt exactly like `TD-09`/`TD-10`/`TD-11` did. | `WP 6.3` | Medium | High | Treat the `WP 6.1` → `WP 6.3` dependency as a hard gate in `WorkPackages.md`, not a soft preference — `WP 6.8` (Platform Services Integration Review) should explicitly verify `WP 6.3` did not begin before `WP 6.1`'s own architecture (and ideally implementation) landed. | Open |
| R3 | **ASP.NET Core/Kestrel is this platform's first substantial dependency on a pre-built framework component beyond the bare .NET SDK.** Every existing platform service was built on this project's own custom container (`ADR-0005`); `WP 6.3` is a genuine first, and carries integration risk this project has no direct precedent for (hosting model conflicts, middleware pipeline interaction with the existing Composition Root, and so on). | `WP 6.3` | Medium | Medium | `ADR-0049` (see `Required ADRs.md`) explicitly scopes ASP.NET Core/Kestrel to HTTP hosting only — this platform's own DI container and every other service remain untouched. `WP 6.3`'s own architecture phase should prototype the integration boundary explicitly before committing to it in implementation. | Open |
| R4 | ~~**The un-owned Persistence abstraction gets reinvented ad hoc, per-Work-Package, if `ADR-0041`'s recommendation isn't followed.**~~ Nothing currently named in `WorkPackages.md` explicitly owns "Persistence" — this review's own recommendation (establish it as part of `WP 6.4`'s scope) is a recommendation, not yet a ratified decision. **Retired, 2026-07-29 — `WP 6.4` implemented**: `ADR-0041` is Accepted; `IPersistenceStore`/`PersistenceStore` (`Tempest.Core.Persistence`) is real, tested, and registered. Residual risk: `WP 6.5` (Audit) has not yet begun, so whether it actually reuses this abstraction (rather than inventing its own) remains to be confirmed when that Work Package starts — named explicitly in this Work Package's own Future Capability Recommendations. | `WP 6.4` (closed), residual: `WP 6.5` | Reduced — Low (residual only) | Reduced — Low (residual only) | `WP 6.4`'s own implementation ratified `ADR-0041` exactly as recommended. Residual mitigation: `WP 6.5`'s own architecture phase should explicitly confirm it depends on `IPersistenceStore` rather than building a second mechanism, per this Work Package's own Future Capability Recommendations. | Partially Retired |
| R5 | **License validation being too aggressively Host-fatal.** `ADR-0050`'s anticipated decision treats any invalid license as startup-aborting, mirroring `ADR-0013`'s existing platform-service-failure classification — but Licensing is a new *kind* of failure (a business/entitlement condition, not a technical fault), and an overly strict interpretation could make the platform impossible to run at all in a degraded-but-useful state (e.g., an expired license during an offline grace period). | `WP 6.6` | Low–Medium | High (if wrong, blocks all use of the platform) | `WP 6.6`'s own architecture phase should explicitly define what "invalid" means (missing vs. expired vs. malformed) and confirm whether every one of those categories genuinely warrants Host-fatal treatment, or whether some degrade to a reduced-capability running state instead. | Open |
| R6 | **Nine Work Packages is a large release** — mirroring `v0.4.0`'s own retired `R8` almost exactly (that release ultimately shipped 7 of 11 originally-planned packages; `v0.5.0` shipped 9). Governance discipline (ADRs, Academy retrospectives, the 13-section template) must scale with this count, and this release additionally introduces more genuinely new architectural surfaces in parallel than either `v0.4.0` or `v0.5.0` did. | Whole release | Medium | High | Treat Academy/ADR updates as part of each Work Package's own Definition of Done, never a follow-up pass — the standing mitigation that held across both `v0.4.0` and `v0.5.0`. `WP 6.8`'s own Acceptance Criteria already requires re-deriving every governance count directly from the file system, not trusting prior registers' own arithmetic — a direct lesson from `WP 5.4`'s own findings. | Open |
| R7 | **The Audit/Notification/Settings-vs-Logging/Event-Bus/Configuration distinctions are easy to blur during implementation if the eventual ADRs aren't followed precisely.** This review draws each boundary explicitly (`Release Architecture.md`'s Cross-Service Orthogonality section; `ADR-0042`, `ADR-0045`, `ADR-0046`), but a boundary written down is not automatically a boundary respected during implementation, especially under schedule pressure. **Update, 2026-07-29 — `WP 6.4` implemented**: the Settings-vs-Configuration half of this risk is confirmed resolved — `ADR-0042` was followed precisely; `ISettingsProvider` never reads or writes `IConfigurationProvider`, and no blurring occurred. Residual risk unchanged for `WP 6.2` (Notifications-vs-Event-Bus) and `WP 6.5` (Audit-vs-Logging/Diagnostics), neither yet implemented. | `WP 6.2`, `WP 6.5` (Settings half retired) | Low–Medium | Medium | Each owning Work Package's own architecture phase should restate its specific orthogonality boundary explicitly in its own design document (mirroring how `WP 5.1A`'s `Command Framework Architecture.md` restated the event/command distinction ahead of implementation, per `v0.4.0`'s retired `R3`), rather than relying solely on this release-wide document being remembered later. | Partially Retired |
| R8 | ~~**The shared Persistence abstraction's first iteration is minimal (key-value only) and may not satisfy a query pattern Audit actually needs**~~ (e.g., range queries by date for `IAuditQuery`), forcing either a premature Persistence redesign mid-release or an Audit-side workaround that partially reintroduces the "ad hoc storage" problem `ADR-0041` exists to prevent. **Update, 2026-07-29 — `WP 6.4` implemented**: confirmed exactly as this risk anticipated — `IPersistenceStore` shipped with key lookup and full-collection enumeration only, no filtered or range query capability, disclosed explicitly in `ADR-0041`'s own Negative consequences and this Work Package's own Technical Debt Assessment. Not retired: the risk's own concern (Audit may need more) remains open until `WP 6.5` actually attempts to build `IAuditQuery` against this shape. | `WP 6.5` | Medium (confirmed, not just anticipated) | Medium | `WP 6.5`'s own architecture phase must decide, before implementation, whether client-side filtering over `ListKeysAsync` is sufficient for `IAuditQuery`'s own needs, or whether `IPersistenceStore` must grow a query capability first — named explicitly in `WP 6.4`'s own Future Capability Recommendations as a decision for `WP 6.5` to make early, not discover mid-implementation. | Open |

---

## Risks Considered and Not Included

- **Third-party dependency risk, generally** — not applicable beyond the
  one explicit, named exception (`R3`, ASP.NET Core/Kestrel for `WP 6.3`
  specifically). No other Work Package in this release proposes adopting
  an external package; `ADR-0005`'s custom-container philosophy continues
  to hold everywhere else.
- **Performance risk** — not currently assessed for any of the nine Work
  Packages; none has a stated performance requirement beyond the existing
  Build and Test Gates continuing to pass. Revisit if the REST API's own
  architecture phase (`WP 6.3`) surfaces a genuine throughput or
  concurrency concern, given it is this platform's first network-facing
  surface.
- **Data-loss risk from the new Persistence abstraction** — not
  separately registered here because it is a direct consequence of `R4`/
  `R8` above (an under-specified or ad hoc Persistence design), not an
  independent risk in its own right.

## Related Documents

`Release Architecture.md`; `Platform Services Overview.md`; `Required
ADRs.md`; `Technical Debt Assessment.md`; `docs/releases/v0.6.0/
WorkPackages.md`; `docs/releases/v0.4.0/Risks.md` (the format and
standing rules this register follows); `docs/security/Security
Roadmap.md`; `docs/security/Threat Model.md`.
