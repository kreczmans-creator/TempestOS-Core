# WP 6.6 — Licensing Framework — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
`WP 6.6`'s own implementation found, mirroring every prior Work
Package's own Future Capability Recommendations format.

## Recommendation 1 — `WP 6.8` Should Backfill `Interface Register.md`/`Dependency Injection Register.md`/`Module Register.md` in Full

**What.** `WP 6.8` (Platform Services Integration Review) should
re-derive all three registers fully and directly against the current
file system, closing the gap `WP 6.7` first disclosed and this Work
Package left exactly where it was.

**Why this matters.** This is now the second consecutive Work Package
to add only its own new entries rather than perform the full backfill —
a reasonable, proportionate choice each time, but one that should not
be deferred indefinitely. `WP 6.8`'s own stated purpose (confirming the
release's Work Packages compose correctly, including a full repository
review re-deriving every governance register directly) is exactly the
right, and by this point overdue, place for it.

## Recommendation 2 — Design Cryptographic License Signature Verification Only Once a Concrete Distribution Scenario Exists

**What.** When a real license distribution channel is chosen (a signed
file format, a licensing server, an activation flow), design signature
verification against that concrete mechanism, as its own dedicated ADR
— do not attempt to build a generic, speculative signature scheme now.

**Why not build it now.** `TD-16`'s own disclosure states no concrete
distribution channel or tamper-threat model exists yet in this
release's own approved scope; a speculative scheme risks not matching
the real, eventual requirement.

## Recommendation 3 — Any Future Commercially-Licensed Engineering Module Should Depend on `ILicenseProvider` Directly, Never Invent a Parallel Capability-Gating Mechanism

**What.** A future module wanting to gate a feature behind a license
should constructor-inject `ILicenseProvider` and call `HasCapability`
directly, following `LicensingSampleModule`'s own established pattern —
permission check, capability check, then business logic, all at the
calling layer.

**Why this is worth naming.** `Platform Service Map.md`'s own Licensing
entry already names "any commercially licensed engineering module" as a
plausible future consumer; making the expected pattern explicit here
reduces the chance a future Work Package reinvents its own bespoke
gating mechanism instead of reusing the one this release already built
and tested.

## Recommendation 4 — A Future License-Renewal/Grace-Period Model Should Be Designed Against a Real Expiry Scenario, Not Speculatively

**What.** If a future release needs graceful handling of an expiring
(not yet expired) license — a warning period, a reduced-capability
grace window — design it against that concrete requirement, as its own
ADR, rather than building a general-purpose grace-period framework now.

**Why not build it now.** `AT-13`'s own disclosure names this
explicitly as future scope; no concrete grace-period requirement exists
in this release's own approved contract.

## Recommendation 5 — `WP 6.8` Should Confirm the Missing-File-Is-Not-Invalid Resolution Holds for `Tempest.App`'s Own Real Entry Point Too

**What.** This Work Package verified the resolution against every
test file that builds a `TempestHost`; `WP 6.8`'s own closing review
should additionally confirm `Tempest.App`'s own real `Program.cs`
startup path behaves identically — starts normally, with an
`Unlicensed` default license — since no license file ships with this
repository today.

**Why this is worth naming.** The test-level verification is strong
evidence but covers `TempestHostBuilder` directly, not `Tempest.App`'s
own composition; a closing-milestone review is the natural place to
confirm the same guarantee holds end-to-end through the real
application entry point too.

## Not Recommended

- **Building cryptographic signature verification now.** No concrete
  distribution channel or threat model exists yet (`TD-16`) — see
  Recommendation 2.
- **Building remote validation/activation, floating/seat-based
  licensing, or a renewal/grace-period model now.** All three are named
  explicitly in `Platform Service Contracts.md`'s own Future Extension
  Points as plausible, not current, requirements (`AT-13`).
- **Adding a `PricingTier` or subscription concept to `ILicense`.**
  Would directly violate this Work Package's own "shall not implement
  commercial policy" instruction — any tier or pricing concept belongs
  entirely outside the platform, in whatever commercial back-office
  system a future integration might connect to.

## Related Documents

`WP6.6 Implementation Report.md`; `WP6.6 Engineering Review Report.md`;
`WP6.6 Platform Integration Demonstration.md`; `WP6.6 Platform Impact
Assessment.md`; `WP6.6 Lessons Learned.md`; `WP6.6 Technical Debt
Assessment.md`; `ADR-0050`; `docs/releases/v0.6.0/Platform Service
Contracts.md` (Licensing's own Future Extension Points);
`docs/releases/v0.6.0/WorkPackages.md` (`WP 6.8`);
`docs/governance/Quality/Technical Debt Register.md` (`TD-16`, `AT-13`).
