# WP 6.6 — Licensing Framework — Platform Impact Assessment

## Purpose

A dedicated assessment of whether `WP 6.6`'s own implementation
confirms, extends, or exposes a weakness in the platform architecture
established by prior Work Packages — distinct from its Implementation
Report, Engineering Review Report, and Platform Integration
Demonstration.

## Does This Work Package Confirm Earlier Platform Architecture?

**Yes, on three separate points, each independently verified rather
than assumed:**

1. **The "deliberately a leaf, no constructor dependencies" construction
   pattern (`ADR-0023`, `PlatformVersionProvider`) generalises to a
   second, genuinely different timing constraint.** `LicenseValidator`
   reuses the identical shape for a structurally different reason
   (validating before the container exists, not merely before
   Discovery), confirming this is a reusable pattern, not a one-off
   fix specific to Platform Version.
2. **The Composition-Root `AddInstance` pattern (`ADR-0009`,
   `IPlatformVersionProvider`/`IDiagnosticsProvider`) continues to scale
   to a service whose own construction the container cannot perform.**
   `ILicenseProvider` is the third independent application of this
   exact shape.
3. **`ADR-0013`'s platform-service-failure classification (Host-fatal
   vs. isolated) extends cleanly to a genuinely new *kind* of failure**
   — a business/entitlement condition, not a technical fault — with
   zero modification to the classification mechanism itself. `RunAsync`'s
   own existing `catch (Exception ex) { EnterFaulted(ex); throw; }`
   block required no change at all.

## Does This Work Package Extend Earlier Platform Architecture?

**Yes, in one specific, disclosed way:**

**This platform's first service that must exist and make a Host-fatal
decision before the DI container is built at all.** Every prior
Composition-Root-constructed service (Platform Version, Diagnostics)
still runs after the container exists, or is registered into a
container that is about to be built moments later. `ILicenseValidator`
is the first to run, decide, and potentially abort the entire process
*before* `ServiceCollection` is even instantiated — a genuinely earlier
point in the startup sequence than any previous Work Package has
touched.

## Does This Work Package Expose Any Architectural Weakness?

**One, directly confirmed rather than merely anticipated:** `Risk
Register.md`'s own `R5` predicted that a naive, literal reading of "an
invalid license aborts startup" would make the platform impossible to
run in its own normal, unlicensed development/open-source state. This
Work Package confirms that prediction was exactly right — a literal
reading would have broken all 24 pre-existing `TempestHost`-building
test files — and resolves it by defining "invalid" precisely rather
than loosely. The underlying lesson (a Host-fatal classification
inherited from a technical-fault model does not automatically transfer
correctly to a business/entitlement-condition model) is now understood
concretely, not vaguely, and is written down in `ADR-0050` for any
future Work Package introducing its own new *kind* of startup-time
failure to consult.

**A second, disclosed observation, not a weakness in the platform
architecture itself:** this Work Package's own repository review
re-confirmed the `Interface Register.md`/`Dependency Injection
Register.md`/`Module Register.md` gap `WP 6.7` had already found and
disclosed as `Partial` — no new drift, but also no improvement; the
gap remains exactly where `WP 6.7` left it, correctly deferred to `WP
6.8`'s own closing audit rather than addressed piecemeal by every
subsequent Work Package.

## Explicit Assessment: Interactions With Identity, Settings, Audit, Notifications, and REST API

**Recorded per this Work Package's own explicit instruction — see
`WP6.6 Platform Integration Demonstration.md` for the complete,
per-service account.** In summary:

- **Identity & Permissions, Audit, Notifications, REST API.** All four
  used, but entirely inside `LicensingSampleModule`'s own command
  handler and module initialisation — `Tempest.Core.Licensing` itself
  has zero direct dependency on any of the four, confirmed by direct
  inspection.
- **Settings.** Used, genuinely, but only on the capability-granted
  success path — a licensed capability's own behaviour may be further
  customised via an ordinary setting, with zero change to
  `ISettingsProvider` itself.

**Summary: Licensing has zero core-level platform dependencies at all —
matching Export/Import's own identical posture (`WP 6.7`) and stricter
than the REST API's own two core-level dependencies (Identity, Audit,
`WP 6.3`). Every cross-service interaction this Work Package
demonstrates exists entirely because `LicensingSampleModule`'s own
command happens to use those services, never because
`ILicenseValidator`/`ILicenseProvider` themselves need them.** This is
architecturally the cleanest possible outcome for a framework whose own
brief states "shall expose capability only" and "avoid unnecessary
dependencies" — zero platform-service dependencies at the core level,
with every integration demonstrated exclusively at the calling layer.

## Related Documents

`WP6.6 Implementation Report.md`; `WP6.6 Engineering Review Report.md`;
`WP6.6 Platform Integration Demonstration.md`; `WP6.6 Lessons
Learned.md`; `WP6.6 Technical Debt Assessment.md`; `WP6.6 Future
Capability Recommendations.md`; `ADR-0009`; `ADR-0013`; `ADR-0023`;
`ADR-0050`; `docs/releases/v0.6.0/Risk Register.md` (`R5`);
`docs/governance/Engineering/Interface Register.md`, `Dependency
Injection Register.md`, `Module Register.md` (each still disclosed as
Partial).
