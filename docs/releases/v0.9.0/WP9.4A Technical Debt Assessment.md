# WP 9.4A — Engineering Documents Workspace — Technical Debt Assessment

## Purpose

Reviews the Technical Debt Register for items this Work Package's own
implementation created, extended, or should have created and did not.

## New Items

### `TD-31` — No File/URL Attachment Storage Service

**What.** `Attachment`/`IAttachment` (`Tempest.Core.EngineeringDomain`,
`WP 8.2C`, unmodified by this Work Package) carry only descriptive
metadata — `FileName`, `ContentType`, `SizeInBytes`. No actual file
bytes, no resolvable file path, no URL-fetch or storage capability
exists anywhere in this platform. `AttachDocumentCommand` (this Work
Package's own one genuinely new command) therefore only ever records
metadata about a file, never the file itself; the `"External Reference"`
Classification's own representative document holds a placeholder URI in
its `Content` field, purely descriptive, never resolved or fetched.

**How it was found.** Confirmed directly while implementing
`AttachDocumentCommand` — `IHasAttachments.AttachAsync(IAttachment
attachment, ...)` accepts an already-constructed `IAttachment`; no
overload, adjacent contract, or Platform Service anywhere in
`Tempest.Core` accepts a file stream, byte array, or URL to fetch.

**Disposition — disclosed, not fixed.** Both `AttachDocumentCommand` and
the External Reference Classification's own XML documentation/remarks
state this limitation directly. No data-correctness issue — every
Attachment record, and every External Reference's own Content field, is
exactly what it claims to be: descriptive metadata, never fabricated
file content.

**Why this is debt, not merely a limitation.** "Attachments" and
"External Reference" documents are named as first-class scope items in
this Work Package's own controlling instruction, and a reader
encountering `AttachDocumentCommand`/`"External Reference"` for the
first time would reasonably expect the underlying file to be reachable
in some form. It is not, anywhere in this platform, today.

**Revisit trigger.** A future Work Package building a real file/URL
storage Platform Service (`FCR-0054`), or a real UI consumer of this
Workspace surface demonstrating a genuine need to upload/download actual
file content.

**Disposition.** Open.

## Existing Items Reviewed for Extension or Change

- **`TD-22`/`TD-24`/`WP 9.0A`'s, `WP 9.0B`'s, `WP 9.1A`'s, and `WP 9.2A`'s
  own equivalent findings** (`ListAllAsync`/list-and-filter reads scale
  with total object count) — the same pattern recurs in
  `DocumentsNodeProvider`/`EngineeringCockpit.LiveDocuments`/
  `DocumentsKpiCards`, here across three Kinds (`Document`/`Drawing`/
  `CadModel`) rather than one. Not separately re-registered; see
  `WP9.4A Security Review Report.md`.
- **`TD-26`** (Runtime Host module-initialisation timing) — unaffected by
  this Work Package; the same test-level `HasRegistered` wait continues
  to be sufficient, confirmed by four consecutive full clean runs with
  zero flakes on that dimension, including this Work Package's own
  further cross-module dependency edges.
- **`TD-27`** (unspecified `ConcurrentDictionary`/`IPersistenceStore`
  iteration order) — this Work Package's own new node-provider ordering
  (`DocumentsNodeProvider`, every category's own children sorted by
  Title via explicit `OrderBy`) was written with `TD-27`'s own lesson
  already in mind — no reliance on iteration order anywhere, confirmed
  by four consecutive full clean runs with zero flakes. No recurrence.
- **`TD-30`** (`ICalculationResult`/`IVerificationResult`/`IApprovalGate`
  family — zero concrete implementations anywhere) — confirmed still
  fully open, and now additionally consequential for this Work Package:
  no live Verification Domain object exists anywhere in the platform
  (a direct, disclosed consequence of the `WP 9.3A` numbering gap — see
  Implementation Report), so Documents↔Verification Digital Thread
  traceability is demonstrated structurally only, never against a real,
  live Verification object. Not separately re-registered as a new item —
  the underlying absence is exactly `TD-30`'s own, not a new gap this
  Work Package introduces.

## Items Considered and Not Raised

- **No dedicated Document↔Baseline binding command** — not Technical
  Debt: `Configuration`/`Baseline`/`Release` (`WP 9.0B`) already accept
  any relationship kind from any `IEngineeringObject`; a Document is
  already reachable from a Baseline through the existing, generic
  mechanism — see `WP9.4A Engineering Review Report.md`'s own Scope
  Discipline Review.
- **`DocumentCategory.Of`'s own six-way `Classification` string match is
  not validated at write time** — considered directly: the identical,
  already-accepted open-string shape `RelationshipKindCategoryMap`
  (`ADR-0076`) already establishes platform-wide; a misspelled
  Classification value degrades honestly to the `"Uncategorized"`
  category, never silently dropped or crashed on. Not raised as debt —
  disclosed directly in `ADR-0088`'s own Consequences section instead.
- **No live Verification object created to populate a Documents↔Verification
  demonstration link** — considered directly, and deliberately not done;
  see `TD-30`'s own extended entry above and `WP9.4A Engineering Review
  Report.md`'s own fourth ratified judgement call. Fabricating one to
  avoid a disclosure would itself be the governance defect this Work
  Package's own "disclose all inconsistencies" instruction forbids.

## Verdict

**One new item formally registered (`TD-31`)**, a disclosed limitation
rather than a correctness defect — no data-correctness issue exists
anywhere in the shipped implementation. `TD-30`'s own existing entry is
confirmed still open and its consequences extended to a second
discipline. No existing item's own disposition worsened; `TD-27`'s own
lesson was applied proactively, with zero recurrence.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md`; `WP9.0A Technical
Debt Assessment.md` (`TD-26`); `WP9.0B Technical Debt Assessment.md`
(`TD-27`); `WP9.1A Technical Debt Assessment.md` (`TD-28`); `WP9.2A
Technical Debt Assessment.md` (`TD-29`, `TD-30`); `ADR-0088`; `WP9.4A
Future Capability Assessment.md`.
