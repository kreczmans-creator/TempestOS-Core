# WP 6.7 — Export/Import — Future Capability Recommendations

## Purpose

Recommendations for future Work Packages, arising directly from what
`WP 6.7`'s own implementation found, mirroring every prior Work
Package's own Future Capability Recommendations format.

## Recommendation 1 — Backfill `Interface Register.md`/`Dependency Injection Register.md`/`Module Register.md` as Part of `WP 6.8`'s Own Closing Audit

**What.** `WP 6.8` (Platform Services Integration Review) should
re-derive all three registers fully and directly against the current
file system — every public interface, every `TempestHost.cs`
registration call site, every production sample module — rather than
incrementally patching them further.

**Why this matters.** This Work Package found all three stale since
`WP 5.2`, missing six consecutive Work Packages' worth of entries (23
interfaces, 10 registration call sites, 6 modules). `WP 6.8`'s own
stated purpose — confirming the release's Work Packages compose
correctly, including a full repository review re-deriving every
governance register directly — is exactly the right, proportionate
place for this backfill, rather than any single feature Work Package
retrofitting six unrelated Work Packages' worth of history under its
own scope.

## Recommendation 2 — Any Future Service Wanting Export/Import Capability Should Implement `IExportable` (and, Optionally, `IExportableKind`) Directly, Never Wrap `IPersistenceStore`

**What.** A future service (Licensing, or any engineering module)
wanting its own data to be exportable should implement `IExportable`
against its own public interface's own data — reading through
`GetValueAsync`-style accessors, exactly as `SettingExportImportAdapter`
does for Settings — never by reaching into `IPersistenceStore` directly
even if that would be more convenient to write.

**Why this matters.** `ADR-0051`'s own orthogonality decision exists
specifically to prevent a portable artifact's format from becoming
coupled to an internal storage detail. This pattern needs to be
followed consistently as more services become exportable, not just
for Settings today.

## Recommendation 3 — A Future Schema-Upgrade Path Should Be Designed Only Once a Real, Shipped Schema Version Bump Exists

**What.** When a future release genuinely needs to import an older
schema version (not merely a hypothetical one), design the upgrade path
against that concrete version pair, as its own dedicated ADR — do not
attempt to design a general-purpose migration framework speculatively.

**Why not build it now.** `AT-12`'s own disclosure states no concrete
schema version bump exists yet in this release; building a speculative
migration mechanism now would be exactly the kind of premature
capability this project's own conventions warn against, and any
speculative design would very likely not match the real, eventual
requirement once one exists.

## Recommendation 4 — `WP 6.6` (Licensing), if It Ships Exportable Licensing State, Should Reuse `IExportable`/`IImportable` Directly, Not Invent a Parallel Mechanism

**What.** If Licensing's own approved contract names any exportable
state (a license key, an activation record), it should implement
`IExportable`/`IImportable` against `Tempest.Core.ExportImport`'s own
approved interfaces directly, following `SettingExportImportAdapter`'s
own established pattern.

**Why this is worth naming.** `Platform Service Map.md`'s own
Export/Import entry already names Licensing as a plausible future
consumer; making this expectation explicit here reduces the chance a
future Work Package reinvents its own bespoke export mechanism instead
of reusing the one this release already built and tested.

## Recommendation 5 — Any Future Multi-Section Artifact Consumer Should Reuse `IExportFormat`, Not Invent a Second Envelope Format

**What.** A future need to frame more than one opaque payload into a
single artifact (not necessarily Export/Import-specific) should reuse
`IExportFormat`/`JsonExportFormat` directly, or implement a new
`IExportFormat` for a different wire format (binary, XML) if genuinely
needed — rather than inventing a bespoke, one-off framing scheme
elsewhere in the codebase.

**Why this is worth naming.** `IExportFormat`'s own contract (frame N
opaque sections, each tagged with `Kind`+`SchemaVersion`, into one
artifact and back) is genuinely reusable beyond Export/Import's own
specific use — naming this explicitly here increases the chance a
future author reaches for it rather than reinventing it.

## Not Recommended

- **Building a general-purpose compression or encryption layer now.**
  No concrete deployment scenario names a specific requirement yet
  (`AT-11`); building one speculatively risks not matching the real,
  eventual requirement.
- **Adding a destinations parameter to `IImportService.ImportAsync`.**
  This would be an unapproved change to an already-shipped, approved
  interface for a need the additive `IExportableKind`/`IImportable`
  mechanism already satisfies without touching it.
- **Extending the custom DI container with `IEnumerable<T>`
  multi-registration support speculatively.** `ImportService`'s own
  `RegisterImportable` mechanism already solves the one concrete need
  this release has for "route to one of several registered handlers" —
  extending the container itself should wait for a second, independent
  need that specifically requires container-level multi-registration,
  not merely because one theoretical use case exists.

## Related Documents

`WP6.7 Implementation Report.md`; `WP6.7 Engineering Review Report.md`;
`WP6.7 Platform Integration Demonstration.md`; `WP6.7 Platform Impact
Assessment.md`; `WP6.7 Lessons Learned.md`; `WP6.7 Technical Debt
Assessment.md`; `ADR-0051`; `docs/releases/v0.6.0/Platform Service
Contracts.md` (Export/Import's own Future Extension Points);
`docs/releases/v0.6.0/WorkPackages.md` (`WP 6.6`, `WP 6.8`);
`docs/governance/Quality/Technical Debt Register.md` (`AT-11`, `AT-12`).
