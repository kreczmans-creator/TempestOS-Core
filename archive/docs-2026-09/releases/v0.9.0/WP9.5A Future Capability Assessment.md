# WP 9.5A — Manufacturing Workspace — Future Capability Assessment

## Purpose

Records candidate future capabilities this Work Package's own
implementation surfaced but deliberately did not build.

## `FCR-0060` — A Genuine `Routing`/`SupplierOperation` Domain Kind, Each With Its Own Structured Fields

`ADR-0091` (this Work Package's own new decision) realises Routings and
Supplier Operations as `Classification`-tagged `ManufacturingOperation`
objects. A future implementation could introduce genuine, distinct
Domain Kinds — a `Routing` carrying, for example, a real standard
cycle-time or revision-controlled step list as structured fields rather
than `IHasParent`-nested siblings; a `SupplierOperation` carrying a real
lead-time or cost field. **Recommended once a real consumer demonstrates
a genuine need for structured fields beyond `Classification`/`PartId`/
`"manufacturedBy"`** — every named `WP 9.5A` scope item is satisfied by
the current representation; see `ADR-0091`'s own Alternatives
Considered section.

## `FCR-0061` — Parameterising `EngineeringCockpit.FormatCoverage`'s Own Empty-State Message

`TD-33` (this Work Package's own Technical Debt Assessment) discloses
that `FormatCoverage`'s own zero-denominator text is hardcoded
Requirements-specific, already inaccurately reused by
`CalculationsKpiCards`/`VerificationKpiCards`, and deliberately not
reused by this Work Package's own `ManufacturingKpiCards` for the same
reason. A future capability could add an optional `emptyLabel`
parameter to `FormatCoverage` (the identical shape this Work Package's
own local `FormatShare` function already demonstrates) and update every
existing call site to pass an accurate label, retiring the local
duplication this Work Package introduces. **Recommended the next time
`EngineeringCockpit.cs` is touched for any reason** — a small, low-risk,
purely additive fix, but outside this Work Package's own Manufacturing-
scoped controlling instruction to make unprompted.

## `FCR-0062` — Extending `VerificationService.RecordAsync`'s Own `IHasRelationships` Linking to Cover Inspection Subjects

`FCR-0057` (`WP 9.3A`) already recommends `RecordAsync` additionally
link through `IHasRelationships` when its own subject is a real Domain
object. This Work Package's own Inspection recording is a direct,
disclosed instance of the identical underlying gap (`TD-32`), now
exercised by a second discipline. **Recommended for the identical
reason `FCR-0057` already states** — this entry exists only to record
that Manufacturing is now a second, real consumer with a genuine stake
in that future capability, not to duplicate it as a separately-reasoned
candidate.

## Not Recommended: A Dedicated `ManufacturingResource`/`Tooling`/`Fixture` Domain Kind Distinct From `"Document"`

Considered directly and rejected as this Work Package's own delivered
design (extending `ADR-0088`) — see the Implementation Report's own
Disclosed Design Decisions. **Not recommended** unless a future Work
Package identifies a genuine, demonstrated need for a Resource/Tooling/
Fixture to carry its own structured fields (a calibration due-date, a
capacity rating) beyond what a plain `"Document"` already provides via
`Classification`/metadata.

## Not Recommended: Reusing Manufacturing's Own Commands From Documents/Verification

Considered directly during implementation and rejected as this Work
Package's own delivered design — see the Implementation Report's own
"Commands remain this Work Package's own" section. **Not recommended**
unless a future Work Package identifies a genuine need for a single,
Kind-agnostic command set spanning all disciplines, which would itself
be a larger, platform-wide redesign well beyond this Work Package's own
scope.

## Verdict

Three new candidates recorded (`FCR-0060`–`FCR-0062`); none built
speculatively ahead of genuine need. `FCR-0062` extends `WP 9.3A`'s own
`FCR-0057` directly, disclosed as an extension rather than duplicated as
a new, separately-reasoned candidate for the identical underlying
capability — the same disclosure discipline `WP 9.3A` itself applied to
`FCR-0058`/`WP 9.2A`'s `FCR-0052`. Two further candidates considered and
explicitly not recommended, with reasoning recorded rather than
silently dropped.

## Related Documents

`docs/governance/Future Capability Register.md`; `ADR-0088`; `ADR-0091`;
`WP9.3A Future Capability Assessment.md` (`FCR-0057`); `WP9.4A Future
Capability Assessment.md` (`FCR-0054`–`FCR-0056`); `WP9.5A Technical
Debt Assessment.md` (`TD-32`, `TD-33`); `WP9.5A Engineering Review
Report.md`.
