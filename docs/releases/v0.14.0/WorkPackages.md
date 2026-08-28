# v0.14.0 Work Packages

The `v0.14.0` release train — **In preparation** (see
`docs/governance/Delivery/Release Register.md`). Opened by `WP 14.0A`;
further scope not yet planned. `VERSION` remains `0.13.1` until release
preparation, per the `WP 13.12.4` precedent.

## Commits

| Commit | Work Package | Change |
|---|---|---|
| `f7f00af` | WP 14.0A | TempestOS Companion — mobile companion application, REST query-and-action surface, offline model, provisional brand realisation, full test/governance/documentation sweep |
| *(pending push)* | WP 14.1A | Brand alignment to the supplied Tempest Engineering Design System (Companion-only visual rework, verbatim brand geometry/assets, brand-conformance tests) |

## Delivered

| Work Package | Scope | Type | Status |
|---|---|---|---|
| WP 14.1A | Brand alignment: `Tempest.Companion` visual layer re-derived from the authoritative design system pack (tokens/marks/fonts/idiom, `docs/design/` reference, `BrandConformanceTests`); `ADR-0113` corrected in place; `FCR-0092` | Implementation | **Complete** |
| WP 14.0A | TempestOS Companion: `Tempest.Companion` + `Tempest.Companion.Contracts` + `Tempest.Companion.Tests`; `IApiQueryRegistry`/`ApiQueryRequestHandler` query-and-action surface in `Tempest.Core.Api` (`ADR-0114`); server-side registration in `Tempest.App` composition (`CompanionApiRegistration`); `ADR-0113`–`ADR-0115`; `TD-57`/`TD-58`/`AT-24`; `FCR-0023` update + `FCR-0089`–`FCR-0091`; Academy retrospective; Companion Security Review; `TempestOS Companion Architecture.md` | Architecture + Implementation (single sweep) | **Complete** |
