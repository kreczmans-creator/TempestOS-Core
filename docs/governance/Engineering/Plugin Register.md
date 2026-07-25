# Plugin Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Plugin Register |
| **Purpose** | The index of every real plugin (a `plugin.manifest.json` plus loadable assembly under `src/Plugins/`) TempestOS ships, distinct from the Plugin Manifest *infrastructure* itself (discovery, loading), which is fully implemented and tracked by `Platform Services Register.md`. |
| **Scope** | Any real plugin package present under `src/Plugins/` at time of review. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | `src/Plugins/` (direct directory inspection). |
| **Review Frequency** | Updated whenever a real plugin is added to `src/Plugins/`. |
| **Last Reviewed** | 2026-07-25 (WP 4.5A). |
| **Related Documents** | `docs/architecture/Plugin Manifest Architecture.md`; `Platform Services Register.md`; `Rejected Designs Register.md` (RD-0015). |
| **Related ADRs** | ADR-0025, ADR-0026. |
| **Related Academy Articles** | `docs/academy/02 Runtime Architecture/07-plugin-architecture.md`; `docs/academy/03 Work Packages/WP4.2-plugin-manifest-architecture.md`, `WP4.2-plugin-manifest-implementation.md`. |
| **Coverage Status** | Not Yet Applicable. |

---

## Reason

`src/Plugins/` exists but is empty — **Verified** directly: `find
src/Plugins -type f` returns zero results. This directory has been empty
since it was first noted as a gap in `Runtime Host Architecture.md` (WP
2.7A-era) and remains empty as of this baseline. The Plugin Manifest
*infrastructure* that would discover and load a real plugin
(`PluginManifestDiscoveryService`, `PluginAssemblyLoader`) is fully
implemented and tested (WP 4.2) — see `Platform Services Register.md` —
but no Work Package has yet placed a real, shipped plugin package into
`src/Plugins/`. `RD-0015` (Rejected Designs Register) records that `WP
4.3`'s Sample Module deliberately did *not* package itself as a plugin,
by choice, not by omission — the option remains available, not exercised.

## Review Trigger

The first Work Package that places a real plugin package (a
`plugin.manifest.json` plus its assembly) under `src/Plugins/` must
populate this register with at least one entry and update Coverage
Status accordingly.

## Test-Only Plugin Fixtures (Noted for Completeness)

Plugin infrastructure tests build genuinely loadable, dynamically-compiled
assemblies at test time (`DynamicPluginAssemblyBuilder`, using
`System.Reflection.Emit.PersistedAssemblyBuilder`) under
`tests/Tempest.Core.Tests/Plugins/` — these prove the loading mechanism
against real, valid PE assemblies rather than synthetic stand-ins, but are
constructed in-memory during test execution, not shipped plugin packages.
Full detail is tracked by `Test Register.md`, not duplicated here.

## Cross-Reference Check

This register's "Not Yet Applicable" status is consistent with
`Platform Services Register.md`'s "Plugin Manifest: Implemented" entry —
the infrastructure is complete; the catalogue of real plugins running on
it is empty, exactly as `Plugin Manifest Architecture.md` itself
describes `src/Plugins/`'s current state.
