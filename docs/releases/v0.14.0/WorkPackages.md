# v0.14.0 Work Packages

The `v0.14.0` release train — **In preparation** (see
`docs/governance/Delivery/Release Register.md`). Opened by `WP 14.0A`;
further scope not yet planned. `VERSION` remains `0.13.1` until release
preparation, per the `WP 13.12.4` precedent.

## Commits

| Commit | Work Package | Change |
|---|---|---|
| `f7f00af` | WP 14.0A | TempestOS Companion — mobile companion application, REST query-and-action surface, offline model, provisional brand realisation, full test/governance/documentation sweep |
| `5554687` | WP 14.1A | Brand alignment to the supplied Tempest Engineering Design System (Companion-only visual rework, verbatim brand geometry/assets, brand-conformance tests) |
| `57d0082` | WP 14.2A | Android & iOS platform heads: library/heads restructure, `Tempest.Companion.Mobile.slnx`, dispatch-only `mobile-heads.yml` (`ADR-0116`); `TD-57` → Partially resolved |
| *(pending push)* | WP 14.2.1 | CI pipeline remediation: diagnostics decoupled from the gate (`ADR-0117`), artefact cause removed, failed tests self-identifying; `TD-59`/`TD-60` |

## Delivered

| Work Package | Scope | Type | Status |
|---|---|---|---|
| WP 14.2.1 | CI remediation: `ADR-0117` (gate reflects build/test only; diagnostic uploads non-fatal; build-output artefacts on `main`/tags at 7-day retention; failed tests named in the Job Summary), `mobile-heads.yml` hardened, Academy CI article corrected, `TD-59`/`TD-60` added | Governance/Process (zero `src/`/`tests/` changes) | **Complete** |
| WP 14.2A | Platform heads: `Tempest.Companion` → shared app library; `Tempest.Companion.Desktop` (gating solution) + `.Android`/`.iOS` heads with brand launcher icons; `ADR-0116`; non-gating `mobile-heads.yml` | Implementation | **Complete** (heads' full-toolchain build pending first workflow dispatch — `TD-57` Partially resolved) |
| WP 14.1A | Brand alignment: `Tempest.Companion` visual layer re-derived from the authoritative design system pack (tokens/marks/fonts/idiom, `docs/design/` reference, `BrandConformanceTests`); `ADR-0113` corrected in place; `FCR-0092` | Implementation | **Complete** |
| WP 14.0A | TempestOS Companion: `Tempest.Companion` + `Tempest.Companion.Contracts` + `Tempest.Companion.Tests`; `IApiQueryRegistry`/`ApiQueryRequestHandler` query-and-action surface in `Tempest.Core.Api` (`ADR-0114`); server-side registration in `Tempest.App` composition (`CompanionApiRegistration`); `ADR-0113`–`ADR-0115`; `TD-57`/`TD-58`/`AT-24`; `FCR-0023` update + `FCR-0089`–`FCR-0091`; Academy retrospective; Companion Security Review; `TempestOS Companion Architecture.md` | Architecture + Implementation (single sweep) | **Complete** |
