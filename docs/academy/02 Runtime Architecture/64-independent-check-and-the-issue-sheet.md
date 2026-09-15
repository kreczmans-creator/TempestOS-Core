# Independent Check and the Issue Sheet

**Release:** `v0.18.0` · **Work Package(s):** `WP 18.2B` parts 1 and 2 ·
**Debt:** `TD-171`, `TD-25`, `TD-38` (owned by this Work Package, not
closed by it — see below) · **Decision:** `ADR-0148` (Decision 4) ·
**Code:** `src/Tempest.Core/Evidence/`,
`src/Tempest.Workspace/Workspace/Evidence/`,
`src/Tempest.Workspace/Evidence/`, `src/Tempest.Desktop/IssueSheets/`,
`src/Tempest.Desktop/Editors/ObjectEditorView.cs`

**In plain terms.** Two of the five promises TempestOS makes about a
piece of engineering evidence are: someone other than the person who did
the work looked at it, and there is a paper trail of exactly what was
formally sent to the client and when. This Work Package builds both. A
"check" is a second opinion, recorded in the reviewer's own words. An
"issue sheet" is the cover page every consultancy already produces by
hand — who did the work, who checked it, what it relied on, what it
found — except here it is generated automatically from the record
itself, so it can never say something the record does not actually show.

## Why the author cannot be the checker

Independent check is one of the oldest ideas in engineering practice: a
second, competent person examines the work before it goes anywhere,
because a person checking their own work reliably misses their own
mistakes — the same assumptions that produced an error are the ones the
author would use to review it. A checker who is also the author is not a
check. `59-evidence.md` covers how `Evidence` is built and governed;
this chapter covers check and issue, the third of the five `v1.0.0`
promises made about it.

TempestOS today has one user: one Windows account, one login.
Independence must be *possible* (a second engineer will eventually have
a second account) without being forced on a business with nobody to
enforce it against. `ADR-0148` Decision 4 records the Product Owner's
own answer, 2026-09-09:

> The independent-check rule (`Evidence:IndependentCheck`)... off by
> default — a one-person consultancy has one login and enters the
> client's review by hand.

So the rule is built, tested in both positions, and ships off. The
client's own reviewer — very often not a Tempest user at all — is typed
in by hand: name and organisation, no account needed. The Release Notes
say exactly when to change that: *"switch `Evidence:IndependentCheck` on
when a second member of staff has their own Windows account."*

## A form kept exactly as typed

The check form (`CheckEntry.cs`) collects four things: the checker's
name, their organisation, a free-text statement, and an outcome —
`Accepted`, `AcceptedWithComments` or `Rejected`. All four land unchanged
in `CheckRecord`: the statement is never trimmed or reshaped, unlike most
other text fields in the platform. `be0fab4` pins this with a statement
carrying leading and trailing spaces and an embedded quotation mark:

```csharp
const string statement = "  Reviewed against \"BS EN 10025-2\" and accepted.  ";
Assert.Equal(statement, result.Evidence!.Check!.Statement);
```

A reviewer's own words are the evidence here, not a summary of them.
Normalising whitespace or quoting would be a silent, unannounced edit to
what a named person is on record as having said.

## The rule itself: comparing ids, not names

`EvidenceService.RecordCheckAsync` reads the setting and, when it is on,
compares the acting principal's identity id against
`Evidence.AuthorIdentityId` (an ordinal string comparison), refusing a
match — and refusing outright if nobody is signed in, because *"a check
nobody can be held to is not a check."* This is the same identity id
`55-configuration-logging-and-the-session-principal.md` calls "what the
platform reasons about" rather than a display name; that chapter names
this exact rule as the reason. Off, `CheckerIdentityId` stays `null` —
the check carries no second Tempest principal at all.

## What an issue sheet is, and how it is rendered

An issue sheet is the cover document a consultancy sends with its work:
what is being issued, at what revision, to whom, who prepared it, who
checked it, and exactly which reference figures it relied on. It is not
the evidence — it is the receipt that the evidence was formally handed
over in a known state.

`IssueSheetModel.From` builds that receipt as an immutable snapshot from
one issued, checked `Evidence` record (throwing if either is missing),
plus what only the caller can supply — project name and code, resolved
author/checker display names, the application's version, and the
store's write sequence at issue — captured once, never re-derived
against a clock that has since moved on.

The Execution Plan (§3 decision 8) and the Product Owner's own answer
(§4 answer 2) name the renderer: **SkiaSharp**'s PDF backend, "which
already ships in the Desktop build under `PDFtoImage`" — no new package.
`IssueSheetRenderer` (`e105fd3`, tested in `31e508b`) lays an A4 page out
at 20 mm margins: a title block, a Review block naming author and
checker, citations and declared-figures tables that wrap and paginate, a
Signatures block, and a footer on every page carrying the application
version, the write sequence, the generation timestamp and "Page n of N".
Layout is **plan-then-draw**: every line is measured across as many
pages as needed before any footer is stamped, so the page count is
always true, and nothing reads the clock, so the same model renders
byte-identical twice, proved directly by `31e508b`'s own tests. The
sheet is **regenerated from the record and never edited**.

## Issuing: three transactions, not one

`IssueEvidenceCommandHandler` (`0deda7f`) issues, renders, attaches and
records the sheet's attachment id, but its own remarks disclose this is
three separate commits: `IssueAsync` commits the issue record first
(attachment id still `null`); `AttachContentAsync` commits the sheet's
bytes and metadata together; `RecordIssueSheetAsync` commits the
attachment id onto the issue record. A crash between the first two
leaves an Issued record with no sheet, recoverable only by re-issuing —
"never Issued-and-lying-about-a-sheet." Folding the three into one write
needs `EngineeringObjectBase`'s own mutator surface widened, outside
this Work Package's file list, so the handler names the window instead.
Where no renderer exists — the console harness — issuing still succeeds
and the sheet is skipped, never refused. Once attached, the sheet
behaves like any other file: `ObjectEditorView`'s Attachments row gained
**Export** (`7dc0baa`) beside the already-generic **Open**.

## Revise leaves the issue, and the sheet, exactly as sent

`ReviseAsync` is the only way back from `Issued`, and it is a
**supersession** (`60-source-citations-and-supersession.md`): a new
revision is written, the issued one is never touched, and only the new
revision reopens as `Draft`. `18803da`'s desktop journey test asserts
this directly — the predecessor instance is read back and checked
unmutated, both right after Revise and after the new Draft is edited
further. The issue sheet, an attachment on the issued revision, is
immutable for the same reason its record is.

`959140a` closed a gap `WP 18.2A` disclosed: the physical review tags
evidence to a Part *after* creating it, not at creation.
`Evidence.SetSubjectAsync` retags freely while Draft or Checked; once
Issued, `IEvidenceService.SetSubjectAsync` refuses it
(`SubjectLockedAfterIssue`) — "issued means fixed" applied to the tag as
much as to the content.

`cd2bdf5` closed `WP 18.2A`'s second gap the same way, on the Libraries
tab: a `ReviseReferenceRecordEntry` dialog edits a Draft reference
record's definition as JSON — one dialog spanning all five
differently-shaped library types, the raw-JSON idiom the Calculation
workspace already uses — carrying the record's own provenance forward
unchanged, shown only where `ReferenceValidationStates.IsRevisable`
allows it.

## Proving it with two principals

`7dc0baa` wires Check, Issue, Revise and Change Subject into the Object
Editor, each visible only when `EvidenceStatusTransitions` actually
permits that move from the record's own status. `18803da` proves the
independence rule through the real window: one `WorkspaceHost`, two
`PlatformPrincipal`s swapped in with `CurrentPrincipalAccessor.SetCurrent`
— the author's own check on their own evidence is refused and the status
stays `Draft`; the second principal's check on the same evidence
succeeds and records that principal's own id as both checker and
recorder. Adding one new command (`evidence.set-subject`) moved several
platform-wide contract tests that pin exact descriptor and parameter
counts. `d6d8be7` did not loosen them: every count (64→65 invocable,
82→83 production, and so on) was updated with a one-line comment naming
the cause — the discipline `06-governance-automation.md`'s own tests
expect everywhere else.

## The honest costs

- **The sheet is about 630 KB for one page**, because SkiaSharp embeds
  the whole default typeface rather than a subset — a later refinement,
  not a defect in what the record says.
- **`TD-171`** (three verification models still coexist —
  `Core.Verification`, `EngineeringDomain.RequirementsVerification`,
  `EngineeringAssets.Verification`; see `14-verification-framework.md`),
  deferred from `WP 17.1B` to this Work Package, was not collapsed here
  either: the Execution Plan's decision 5 states Check and Issue are "not
  a fourth verification model" and leave the three untouched, and
  `BACKLOG.md` still lists `TD-171` under Live Backlog, owned by
  `WP 18.2B`, not under Closed. The debt survives this release.
- **`TD-25`** (`RequirementsService` has no compare-and-swap) and
  **`TD-38`** (`EngineeringObjectFactory` enforces no business-identifier
  uniqueness) are also listed as owned by `WP 18.2B`, but neither is
  named in any commit this Work Package made nor appears in
  `BACKLOG.md`'s Closed section — open still, owner named, not closer.

## What was deliberately not built

No email or transmittal mechanism — issuing attaches a PDF; sending it
is still a human act. No font subsetting, and no folding the three issue
transactions into one, which needs a wider `EngineeringObjectBase` hook
than this Work Package's files could touch.

## What to take away

- **A rule can be built and switched off at the same time.** The
  independence check exists and is tested in both positions, costing the
  one-person consultancy nothing until a second person exists to enforce
  it against.
- **A generated document is safer than an editable one.** The issue
  sheet can never drift from the record, because there is no way to open
  it and change a word — only to issue again and get a faithful one.
- **Naming a crash window honestly is not the same as leaving it open.**
  `IssueEvidenceCommand`'s own remarks say exactly what state a crash
  between its three commits leaves, and why re-issuing is the recovery,
  rather than pretending three commits are one.
