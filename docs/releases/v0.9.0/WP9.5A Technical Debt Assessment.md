# WP 9.5A — Manufacturing Workspace — Technical Debt Assessment

## Purpose

Reviews the Technical Debt Register for items this Work Package's own
implementation created, extended, or should have created and did not.

## New Items

### `TD-33` — `EngineeringCockpit.FormatCoverage`'s Own Zero-Denominator Text Is Hardcoded Requirements-Specific, Inaccurate When Reused by a Different Discipline's Own Coverage Card

**What.** `EngineeringCockpit.FormatCoverage(int numerator, int
denominator)` (`Tempest.App.Workspace`, `WP 9.1A`, unmodified by this
Work Package) returns the literal string `"— (no requirements yet)"`
when `denominator` is zero, regardless of what is actually being
measured. `CalculationsKpiCards`'s own "Verification Coverage" card
(`WP 9.2A`) and `VerificationKpiCards`'s own "Verification Coverage"
card (`WP 9.3A`) both already reuse this helper for a zero-Calculation/
zero-Activity denominator, both already displaying the identical
Requirements-specific text for an unrelated discipline — a pre-existing
inaccuracy this Work Package did not introduce, but did notice while
building `ManufacturingKpiCards`'s own "Manufacturing Readiness"/
"Supplier Status" cards.

**How it was found.** By direct inspection while designing
`ManufacturingKpiCards`, before any code was written — not by a failing
test. Reusing `FormatCoverage` for a zero-Operation/zero-Supplier-Operation
denominator would have produced a third instance of the same
inaccurate text (`"no requirements yet"` on a Manufacturing card), which
was judged unacceptable to introduce knowingly.

**Disposition — worked around locally, not fixed at the shared
helper.** `ManufacturingKpiCards` defines its own local `FormatShare`
function with an accurate, per-card empty-state message
(`"— (no Operations yet)"`/`"— (no Supplier Operations yet)"`), rather
than calling `EngineeringCockpit.FormatCoverage`. This avoids
compounding the existing inaccuracy with a third instance, but does
**not** fix the two pre-existing instances in `CalculationsKpiCards`/
`VerificationKpiCards` — doing so is a one-line, low-risk fix, but is a
change to already-shipped `WP 9.2A`/`WP 9.3A` Cockpit code, outside this
Work Package's own Manufacturing-scoped controlling instruction, and is
therefore disclosed rather than silently made.

**Why this is debt, not merely a limitation.** The helper's own name
(`FormatCoverage`) and signature (`int numerator, int denominator`)
promise a discipline-agnostic formatter; its own hardcoded zero-case
text breaks that promise the moment a second discipline reuses it with
a zero denominator — a genuine, if minor, latent inaccuracy in shipped,
user-visible Cockpit text, not a data-correctness defect (the numerator/
denominator values themselves are always correct; only the all-zero
display text is discipline-mismatched).

**Revisit trigger.** A future Work Package touching
`EngineeringCockpit.cs` for any reason, which could parameterise
`FormatCoverage`'s own empty-state message (e.g. an optional
`emptyLabel` parameter, the identical shape `ManufacturingKpiCards`'s
own local `FormatShare` already demonstrates) and update
`CalculationsKpiCards`/`VerificationKpiCards`'s own call sites to pass
an accurate label, retiring the local `FormatShare` duplication this
Work Package introduces at the same time.

**Disposition.** Open.

## Existing Items Reviewed for Extension or Change

- **`TD-22`/`TD-24`/every prior real-discipline Work Package's own
  equivalent finding** (`ListAllAsync`/list-and-filter reads scale with
  total object count) — the same pattern recurs in
  `ManufacturingNodeProvider`/`EngineeringCockpit.LiveManufacturingObjects`/
  `ManufacturingKpiCards`. Not separately re-registered; see `WP9.5A
  Security Review Report.md`.
- **`TD-26`** (Runtime Host module-initialisation timing) — unaffected by
  this Work Package; the same test-level `HasRegistered` wait continues
  to be sufficient, confirmed by four consecutive full clean runs with
  zero flakes, including this Work Package's own further cross-module
  dependency edges.
- **`TD-27`** (unspecified `ConcurrentDictionary`/`IPersistenceStore`
  iteration order) — this Work Package's own new node-provider ordering
  (`ManufacturingNodeProvider`, every category's own children sorted by
  Title via explicit `OrderBy`) was written with `TD-27`'s own lesson
  already in mind — no reliance on iteration order anywhere, confirmed by
  four consecutive full clean runs with zero flakes.
- **`TD-30`** (`ICalculationResult`/`IVerificationResult`/`IApprovalGate`
  family — zero concrete implementations anywhere) — confirmed still
  fully open; a Manufacturing object's own "Released" state is
  necessarily `LifecycleState`-derived alone, the identical,
  already-disclosed treatment `ADR-0087`/`ADR-0090` established for
  Calculations/Verification.
- **`TD-31`** (no file/URL attachment storage service) — confirmed still
  open; this Work Package's own Tooling/Fixture Documents carry the
  identical, already-registered `IAttachment` metadata-only shape, not
  re-registered here.
- **`TD-32`** (`VerificationService.RecordAsync`'s own subject→record
  link invisible to `RelationshipRepository`) — confirmed still open and
  directly consequential for this Work Package: the Inspection's own
  recorded `Pass` result, created via the identical, reused
  `IVerificationService.RecordAsync` call, carries the identical
  characteristic. `ManufacturingOperationPropertyFacetProvider`'s own
  Digital Thread facets read the subject→Inspection `"verifiedBy"` link
  (created via real `EngineeringObjectBase.LinkAsync`, fully visible in
  `RelationshipRepository`) rather than the Inspection→record link
  `TD-32` already describes — no new functional gap is introduced,
  since this Work Package never needed to read the latter link directly.

## Items Considered and Not Raised

- **`"Test"` (a real, compiled `VerificationActivity` subtype) remains
  uninstantiated anywhere in the platform** — not raised as new debt:
  this is the identical, already-disclosed non-use pattern `WP 9.3A`
  established for the bare `Verification` marker Kind. This Work
  Package's own scope names "Inspection Operations" explicitly, never
  "Test Operations" — building one anyway would be scope expansion, not
  filling a gap.
- **No dedicated field distinguishes a Manufacturing Resource from
  Tooling/Fixture beyond `Classification`** — considered and not raised:
  the identical, already-accepted shape `ADR-0088` established for
  Specification/Report/Procedure/Standard — a disclosed, precedent-
  following convenience over the existing vocabulary, not a new Domain
  guarantee, and not a gap distinct from `ADR-0088`'s own already-accepted
  consequence.
- **`ManufacturingOperation`'s own `PartId` is not validated to
  reference a live Mechanical Part/Assembly at write time** — considered
  and not raised: identical, already-accepted shape to every other
  Guid-reference field in the Engineering Domain (for example,
  `IVerificationActivity.SubjectId`, `IWorkInstruction.ManufacturingOperationId`)
  — none are validated against `IEngineeringObjectRepository` at
  construction time anywhere in this platform; a read-time confirmation
  guard exists only for `IHasParent.ParentId` (`WP 9.0B`,
  `Validation.cs`), not for cross-Kind reference fields generally.

## Verdict

**One new item formally registered (`TD-33`)** — a genuine, pre-existing
minor Cockpit display-text inaccuracy, found while designing this Work
Package's own KPI cards and worked around locally rather than fixed at
the shared source, with zero remaining functional gap in this Work
Package's own shipped KPI cards. `TD-30`/`TD-31`/`TD-32` are all
confirmed still open, `TD-32` directly, harmlessly consequential for
this Work Package's own Inspection recording. No existing item's own
disposition worsened; `TD-27`'s own lesson was applied proactively, with
zero recurrence.

**Re-verified row count before this Work Package's own edit, per `WP
9.3A`'s own disclosed "never carry a stated total forward unchecked"
discipline:** a direct, exhaustive count of every `TD-NN` row present in
the register before this Work Package's own addition confirmed exactly
32 rows (`TD-01` through `TD-32`, sequential, no gaps, no duplicates) —
matching the register's own stated total exactly, no drift found this
time.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md`; `WP9.0A Technical
Debt Assessment.md` (`TD-26`); `WP9.0B Technical Debt Assessment.md`
(`TD-27`); `WP9.1A Technical Debt Assessment.md` (`TD-28`); `WP9.2A
Technical Debt Assessment.md` (`TD-29`, `TD-30`); `WP9.4A Technical Debt
Assessment.md` (`TD-31`); `WP9.3A Technical Debt Assessment.md`
(`TD-32`); `ADR-0091`; `WP9.5A Future Capability Assessment.md`.
