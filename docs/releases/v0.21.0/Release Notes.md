# TempestOS v0.21.0 — Release Notes

**Status: candidate — the overnight acceptance campaign's head on `claude/tempestos-v1-final-acceptance-19hka8` (2026-09-15/16; the exact SHA, gate figures and CI runs are in `PRODUCT_OWNER_ACCEPTANCE.md` and `OVERNIGHT_FINAL_ACCEPTANCE_REPORT.md` at the repository root), built on `release/v0.21.0` at `a4ab1915` (`b033651d` the last code commit of the tranche, plus its release documents), cut from the `v0.20.0`
candidate (`88311649`) on 2026-09-15 at the Product Owner's instruction
to close every technical weakness named in the lead's assessment of that
afternoon.** Nothing in this document is certification. `v0.20.0` stays
under the Product Owner's manual test as the candidate, receiving the
`WP 20.10A`–`20.10G` fixes; every one of those is merged here too.

## Summary

**v0.21.0 is the recovery tranche** — docking steps 1–2 of `ADR-0153` (the layout forest and the one
controller),
Undo across commands, the editor split, documents from every template,
the Engineering Assets surfaces, typed calculation results with retained
inputs, the commercial edges, the viewer's remaining formats and markup,
the installer with upgrade, backup and restore, lazy rehydration and the mutation threshold met. Docking steps 3–4, the
real-shell run in CI and the first live Xero authorisation are owed, not
shipped — each waits on the Product Owner (Warnings, last item). See
`Execution Plan.md` for the packages and their waves.

**The overnight acceptance campaign (2026-09-15/16) on top of the tranche**
took the candidate through the remaining technically actionable work
without reopening anything deliberately deferred: the Xero authorisation
path a user can actually take (`WP 21.6P` — three defects that made the
first live authorisation impossible from the product, fixed with
thirteen tests and driven in the real application), docking's keyboard
closure and the real-application checks of steps 1–2 (`WP 21.0K`), a
real-process, real-input acceptance journey on Linux/Xvfb (`WP 21.5C`,
Linux variant), permanent documentation brought into factual alignment
with the product (`WP 21.9.1`), the release-quality evidence gathered in
one place (`WP 21.9.1`), a Linux secrets-directory mode fix, the CI
cancel-in-progress fix for release branches, the Academy chapters merged,
and the backlog audited row by row (7 of 30). Docking steps 3–4 stay
gated on the Product Owner's `ADR-0153` review; the live Xero sign-in
itself and the Windows installer run stay with the Product Owner.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 21.0K` Docking: keyboard closure and real-shell verification of steps 1–2 (overnight campaign) | `ADR-0153` decision 8 as written: `Ctrl+Shift+,`/`Ctrl+Shift+.` reorder a tab within its own group through the one canonical `Apply` (`WorkspaceLayoutTree.ReorderTab`, already pure-tested), no-op at either end, the help text naming the keys, the order surviving save and restore. The keyboard move/resize gestures now reach the controller (they were applied by the host, so decision 7's focus restore never ran for them) and a typed gesture restores *keyboard* focus so the ring is drawn. **A critical defect fixed, not introduced:** closing one tab inside a floating window discarded every panel in every other window — `FloatingPanelWindow.Update` gave its host a synthetic single-window tree, and every gesture that host applied became the whole forest (`WP 20.10D`, carried through `WP 21.0A`); now spliced into its one window entry (`AdoptSecondaryWindowSubtree`), reproduced headlessly first. 8 tests added, 1 rebuilt on a real controller. K1/K2/K3/K6 and the new keys driven in the real application; K4/K5 owed to hardware (`WP21.0K Docking Keyboard Closure and Real-Shell Verification.md`, `evidence/docking/`). | `5c170f34` |
| `WP 21.6P` The Xero authorisation path a user can actually take (overnight campaign) | Three defects found by reading the morning's `WP 21.6` path against the code, each **Verified** before the fix: **Authorise** only re-read the stored state (`OAuthAuthoriser.AuthoriseAsync` had no caller in the product); Settings stored the client id under `Invoicing:ClientId` while the authoriser reads `Invoicing:<Provider>:ClientId` (`ADR-0151`); the connector chosen in Settings was saved to the settings store but the host chose from configuration only, so it never took effect. Fixed: `IAuthorisableConnector` (Xero and QuickBooks implement it; the Fake deliberately not); Settings stores credentials under the provider's own key, shows and migrates the legacy value; the host honours the persisted choice when configuration is silent; **Authorise** saves, then runs the interactive sign-in for an unauthorised real provider with a five-minute wait and every outcome in words, and says *restart* when the chosen connector is not the running one (the Fake's own "Authorised." had been shown for a never-signed-in Xero — caught only in the real application); a browser that cannot open is a result, not an exception. 13 tests. Driven in the real application under Xvfb up to the token exchange (`WP21.6P Xero Authorisation Path Report.md`, `evidence/xero/`). | `709d01a4` |
| `FileSecretStore` on Linux/macOS: the `0700`/`0600` promise holds for an existing directory or file | `Directory.CreateDirectory(path, mode)` and `UnixCreateMode` apply only on creation; a pre-existing secrets directory stayed `0755`. `SetUnixFileMode` after both. Found by the store's own test (`SetAsync_OnNonWindows_RestrictsTheSecretsDirectoryAndFile_ToTheCurrentUserOnly`) running on Linux for the first time; the shipped Windows build uses DPAPI and is unaffected. | `69668268` |
| `WP 21.9.1` Documentation alignment | `VISION.md` provenance-dated with a "Where the product stands at the v1.0 release candidate" section (the "zero Engineering Modules" present tense corrected, history kept); `README.md` what-the-product-does-today; `Product Roadmap.md` and `Future Capability Register.md` dated review notes (`FCR-0032`/`0051`/`0052`/`0053` delivered-by notes); Academy landing corrections; 166 links checked, 0 broken. 21 drift rows in `WP21.9.1 Documentation Alignment Report.md`. | `aa91273c` |
| `WP 21.9.1` Release-quality evidence | Governance 5/5; every workflow `uses:` SHA-pinned (19 of 19); Dependabot covers NuGet and Actions; the six third-party notices present; backup created **through the real OS picker under Xvfb** and verified with `sqlite3` (`integrity_check` ok); restore refused while a project is open; a project survives close and relaunch; zero exceptions in the logs of two launches; the installer's `Setup.exe` **has never been produced by the pipeline** (`release.yml` fires on tags and the newest tag is `v0.18.0`) — Windows-only, for the Product Owner. `WP21.9.1 Release Quality Evidence.md`, `evidence/quality/`. | `2a2d1a33` |
| CI: `cancel-in-progress` off for `release/*` | Runs #420–#427 on this branch were each cancelled by the next push, so seven consecutive heads were never gated; a superseded run on any other ref is still cancelled. | `3d256792` |
| Academy: chapters 42–64, the glossary, the index and the candidate postscripts | Docs only, from `claude/academy-docs-review-completion-iqzgwv`; no file overlap with the tranche. | `fb0c8f8a` |
| `WP 21.3B` Commercial edges: expenses, purchase orders, VAT on lines, a second principal signs in to check | `ProjectExpense` — date, description, a closed category (Travel/Subsistence/Materials/Subcontract/Other), net and VAT amounts, a receipt attachment, billable, `InvoicedBy` set once — recorded from Business → Timesheets ("Record expense…" beside Record) and the project's own Details tab; a billable expense becomes an invoice request line exactly as a timesheet entry does, and shows in Business → Invoices' own "Available to invoice". `PurchaseOrder` — `PO-<yyyy>-<nnn>` (the identical scan-the-store discipline as `Q-`/`CO-`), a supplier, lines with net and VAT, Draft → Issued → Received → Closed \| Cancelled, one act ("Record as expenses") turning a received order's own lines into project expenses with no ledger of its own; Business gains a **Purchase orders** entry, grouped New/Issued/Received/Closed, with New Purchase Order…/Add line…/Issue/Receive/Close/Cancel/Record as expenses. `VatRate` (Standard 20%, Reduced 5%, Zero, Exempt, Out of scope — the enum's own default, so every line recorded before this Work Package reads unchanged) on `QuotationLine`/`InvoiceRequestLine`, each with a computed VAT amount; `Quotation`/`InvoiceRequest` gain net/VAT/gross totals; Settings → Organisation identity carries the consultant's own default rate for a new line. The Xero/Fake connector seam maps the five rates to Xero's own tax types (`OUTPUT2`/`RROUTPUT`/`ZERORATEDOUTPUT`/`EXEMPTOUTPUT`/`NONE`) and refuses a send whose rate cannot be expressed, naming the line. Settings → Principal becomes a real sign-in: **Switch person…** lists every released person the People directory (the `IPeopleDirectory` seam, standing in for `WP 20.10F` where it has not yet merged) carries a known identity for, confirms by name with no password, and publishes them as the session's own principal from that moment on; the Evidence Check refusal for a checker who is also the recorder now names the fix directly ("An independent check needs a second person; switch person first."). `ADR-0150` addendum. | `wp/21.3B` → `release/v0.21.0` |
| `WP 21.2A` Documents from the templates: invoice, purchase order, timesheet, technical report, drawing register, progress report | `IDocumentRenderer<TModel>` — one contract every document renderer (the six new ones, and `QuotationSheetRenderer`/`IssueSheetRenderer`, retrofitted) satisfies — and `DocumentExporter`, naming a file `<reference>-<template>.pdf` through the same file-picker path the Quote tab's own Export already uses. Six new renderers against the design system's own template folders (`docs/design/templates/README.md`'s own mapping, extended): the invoice (net only — `WP 21.3B`'s VAT fields are not in this Work Package's own base), the weekly timesheet, the technical report (a Document's own revisions and content, split into sections by leading `#` markers), the drawing register and the progress report (both A4 landscape — `DocumentTemplate` gained landscape page geometry, additive and backward compatible with every existing portrait caller), and the purchase order (model-less, against a fixture only — the real `PurchaseOrder` Kind, `WP 21.3B`, lives only under `src/Frozen/` in this Work Package's own base, so no live "Export PO" button exists). The design system's three type families (Chakra Petch, Inter, Space Mono) and the horizontal navy lockup are embedded as `Tempest.Desktop` resources and loaded through `SKTypeface.FromStream`/`SKBitmap.Decode` — no running Avalonia application needed — closing `WP 20.10G`'s own disclosed "still the platform default face, still no logo" gap; Chakra Petch and Space Mono draw real, correctly-extractable PDF text (verified — and a genuine bug found and fixed in the test-only `PdfTextExtractor` along the way: it merged every embedded font's own CID space into one dictionary, corrupting text extraction once more than one custom font could appear in the same document), Inter stays the platform default (SkiaSharp's PDF backend does not embed a variable-format `SKTypeface` as extractable text at all — verified empirically, not assumed). Settings → Organisation gained a **Bank details** section (sort code, account number, account name, IBAN) for the invoice's own "Payment details" section. Six buttons wired where a user expects them: Business → Invoices **Export invoice**, Business → Timesheets **Export week**, project → Documents **Export register**, a Document's editor **Export as report**, Projects Dashboard **Export progress report** (every open project now lists there, not only Blocked/At risk/Ready to invoice). `PHYSICAL_REVIEW.md` §7e. | *(pending — `wp/21.2A`, not yet merged)* |
| `WP 21.2B` The Engineering Assets surfaces: the bracket verification artefact filled in from the Desktop, calculation traces rendered, the merged capability's own area (`TD-165`, `TD-160`) | Ships on the Product Owner's "close all of those" instruction against the gap list that named this surface's absence; their own decision to park it until the release candidate, and the RC-time call, stand if either is later withdrawn. Engineering → Modules → **Engineering Assets** (`EngineeringAssetsView.cs`): five tabs — Calculation packs, Templates, Verification artefacts (each a filtered list with Open, its own detail showing `AssetApplicability` and its own governance/validation, every finding named by its own rule code), Engineering evidence (every item any of the three cite, flattened, naming which record cites it), and Bracket verification. The last is `TD-165`: a form over `GovernedBracketCheckRequest` (a material picker; applied load, section area, member length, mass limit, each with its own unit picker) — **Check** runs the identical `GovernedBracketCheckService` the unchanged Engineering Calculations surface already uses, and **Record verification artefact** is the first Desktop caller of `BracketEngineeringRecordService.RecordCalculationAsync`/`.RecordVerificationAsync`, writing into an existing calculation pack and verification artefact picked from the two libraries' own live records, so the artefact then lists under Verification artefacts at its own new standing. `TD-160`'s other named gap closes alongside it: a calculation pack's own **Trace** tab renders `EngineeringTraceRegister.CalculationTrace` (inputs traced to the governed references they pin, the template used, the outputs), read-only, exported as text through the file picker. Proven end to end through the real `MainWindow` by `tests/Tempest.Desktop.Tests/EngineeringAssetsJourneyTests.cs`; the shared `AutomationNameCoverageTests` and `LayoutWalkTests` structural walks extended to the new tree entry. | *(pending — `wp/21.2B`, not yet merged to this branch)* |
| `WP 21.4A` The viewer: SVG in-app, markup and annotation, tiled rendering, and a same-session security fix (`TD-99`, `TD-98`, `TD-101`, `TD-184`) | `TD-99`: `Svg.Skia` **2.0.0.8** (MIT-licensed; see `THIRD-PARTY-NOTICES.md`), pinned to the last release on its 2.x line — the only one whose own `SkiaSharp` dependency floor (2.88.9) matches the version this solution already resolved through `PDFtoImage`, satisfying this Work Package's own kill switch without a version bump. `SvgDocumentPageSource` rasterises through it to the same `SKBitmap`-backed page `PdfDocumentPageSource` already produces; `DocumentFormatDetector` recognises `.svg` by extension, `image/svg+xml` by content type, and a bounded content sniff for a mislabelled file; a malformed SVG reports "This SVG could not be read: {reason}" with Open externally still offered, never a crash. `TD-98`: `AttachmentAnnotation`, a new record kept beside an attachment's owner (`EngineeringObjectState.Annotations`), written one transaction per mutation with an audit row through the same `MutateAndPersistAsync` path `AttachAsync` already uses, rehydrated with the owner and carried onto a revised instance exactly as attachments already are — never in the attachment's own bytes. `DocumentViewerView` gains the Annotations toolbar group (Rectangle, Ellipse, Freehand, Arrow, Text note, five design-token colour swatches, Delete, Clear page — asked first, through an embedded `ConfirmationDialog` — Save annotated copy…) over a `Canvas` overlay positioned exactly on the rendered page; each rendered shape hit-tests itself for select/delete through Avalonia's own pointer routing. **Save annotated copy…** composes a page's own annotations onto a copy of its rendered bitmap and writes a PNG directly, or — for a PDF source — a hand-written single-page PDF (`MinimalPdfWriter`: one FlateDecode raw-RGB image XObject; no new PDF-authoring package, per the kill switch), through the file picker, leaving the original attachment untouched. `TD-101`: `DocumentViewport.WithContentSizeSwapped` closes the `WP 20.2B`-disclosed rotation/fit bug at its root — content width/height now track the page's currently-displayed (rotated) bounding box, so a rotated landscape page fits with no manual zoom step. Tiled rendering: `TileGrid` (pure tile-planning math, tile size **512px**, stated) and `TileCache` (bounded least-recently-used cache, memory budget **256 MiB**, stated) back a new `ITiledDocumentPageSource`/`PdfDocumentPageSource.RenderTile`, rasterising one region directly through `PDFtoImage`'s own `RenderOptions.Bounds` rather than a whole-page render sliced afterwards; `DocumentViewerView` composes a page from cached tiles instead of one `MaxRasterEdge`-capped, degraded render whenever a whole-page render at the requested zoom would exceed that cap — an A0 sheet at deep zoom renders sharp, and a pan at that zoom issues zero further tile renders once the composite is cached (`RenderCurrentPage`'s own existing "re-rasterise only when the page/zoom/rotation actually changed" rule, unchanged). **`TD-184`** (found same-session by `WP 21.5E`'s defensive review, fixed before anything else per the Product Owner's rule): `AttachmentViewerLauncher.MaterialiseForExternalOpen` used to write a materialised copy under an attachment's own, completely unexamined file name into a directory keyed by the attachment id — so an attachment named `invoice.pdf.exe` whose bytes were a real executable ran as code the instant "Open externally" was pressed. Closed: a `DangerousExtensions` denylist refuses materialisation outright with an honest reason (`DocumentViewSession.ExternalOpenRefusedReason`); an `ExternalOnly` (DWG/DXF) attachment's written extension comes from the detector's own verified match, never the raw name; the file name is sanitised (path separators, control and Unicode bidi-override characters stripped, reserved device names guarded, trailing dots/spaces trimmed); the materialised copy lands in a fresh, randomly-named per-launch directory, deleted when the viewer closes. `Svg.Skia`'s own image resolution (`Svg.Model.SvgExtensions.GetImageFromWeb`) calls `WebRequest.Create(uri).GetResponse()` for any `<image>` reference that is not a `data:` URI — `http://`, `https://` and `file://` alike — so `SvgMarkupSanitiser` blanks every such reference to an inert `data:,` URI, and strips a `<!DOCTYPE>` (the XXE vector) and any `<script>` element, before the bytes ever reach `SKSvg.Load`. | *(pending gate)* |
| `WP 21.5B` `TD-88` — lazy, project-scoped materialisation over the index, closed | `EngineeringObjectRehydrationService.RehydrateAsync` no longer reconstructs the estate unconditionally: for every state with a known Kind and an existing document (the one document read that stays eager — a lightweight existence check, no revision content, needed for orphan detection), it registers the object lazily (`IEngineeringObjectRepository.RegisterLazy`), deferring revision-content reads and the rehydrator's own type-specific parsing to first access. `FindAsync` is the single-flight materialising loader — an id materialises once no matter how many callers ask concurrently, and joins the identity map permanently (no eviction shipped). `ListAllAsync`/`ListByKindAsync`/`ListChildrenAsync` answer `EngineeringObjectIndexEntry` rows from the index alone, computed live for a materialised object so a rename or delete is never stale. Every one of `WP 20.1C2`'s own named ~seventy/eighty callers across `Tempest.Core`/`Tempest.Workspace`/`Tempest.Desktop`/`Tempest.Samples` now either reads the index type directly (the compiler proves it needs nothing else) or materialises explicitly through the new `EngineeringObjectRepositoryExtensions.MaterialiseAsync<T>` seam before touching a type-specific field — no caller keeps an untyped "list then cast". Opening a project materialises its own subtree eagerly (`IEngineeringObjectRepository.MaterialiseSubtreeAsync`, wired at `ProjectContext.OpenAsync`/`LoadAsync`); closing one releases nothing. **Kill switch** (named, bounded): `WP 20.1A2`'s business-identifier index rebuild still materialises every enforced-Kind object eagerly inside `RehydrateAsync` — `BusinessIdentifier` is a computed projection absent from the index row, and `BusinessIdentifierScope.ResolveProjectId`'s own synchronous repository read (outside this Work Package's files, the business-identifier index's contract not to be touched) depends on an already-materialised ancestor chain — bounded to the enforced-Kind set, never the unenforced majority a large estate is mostly made of. See the `TD-88` row (`BACKLOG.md`, "Closed") for the full measured figures and their own disclosed caveat: `IndexBuilt`/project-open/first-read all improved (a 10,000-object estate: ~387 ms/~55 ms (300-object subtree)/~1.4 ms respectively); `RehydrateAsync` returning as a whole measured slower in raw total than `WP 20.1C2`'s own ~190 ms/1000 figure, dominated by relationship rebuilding this Work Package left unchanged and did not benchmark past 1,000 objects before. Three new tests (single-flight, identity-map preservation, a rehydrated revision chain read from its newest end) plus a benchmark; `tests/Tempest.Core.Tests` (4,673) and `tests/Tempest.Desktop.Tests` (660) green in Debug; both configurations build clean, warnings as errors; governance 5/5. | *(pending merge)* |
| `WP 21.3A` Typed calculation results with retained inputs — `TD-22`, `TD-29`, `TD-30` | `TD-22`: `CalculationIntermediateResult` carries its own declared `ValueTypeName`; a typed read-back (`As<TValue>()`) returns the declared type or throws `CalculationReadbackException` (naming the key and both types) — never an `InvalidCastException` — whether the value is still the in-process CLR object or a `JsonElement` read back after persistence. `CalculationContext` takes a configured count/total-size bound (defaults 200 intermediates / 1 MiB, disclosed as guesses); exceeding either throws `CalculationBoundExceededException` naming the definition, refusing the record rather than recording without limit. `TD-29`: `CalculationRecord<TResult>` retains the input it ran with (`Input`/`InputTypeName`, the same informal nullable-trailing-field precedent `ResultTypeName` already set — no export-schema migration needed, confirmed against `WP 20.3A`'s own, separate export/import scope). `ICalculationEngine` gains `ReRunAsync` (identical retained input, or a supplied changed input) and `CompareAsync`, producing a typed `CalculationComparison` — which input and result fields changed, old and new, with units; a record with no retained input is reported in the diff rather than thrown for. Surfaced as two new commands, `calculations.rerun` (`Mutates = true`) and `calculations.compare-with-previous` (read-only), invocable through the Command Palette and the Ribbon's own data-driven Calculations tab — no new view. `TD-30`: `ICalculationResult`/`IVerificationResult`/`IApprovalGate` stay declared (still referenced structurally by `EvidenceComposer`/`IEvidence`/`ISimulation`) but are retired — each interface's own remark now says plainly why no implementation exists or is planned: the first two would require turning an immutable evidentiary snapshot into a live, addressable `IEngineeringObject` with its own revision/relationship service dependencies (`FCR-0051`, "a real Domain design question, not a mechanical add"); the third is a workflow-gate concept the product's own "no workflow engine" guard and `ADR-0087`/`ADR-0090` already rule out. The six built-in calculation definitions and their unit-invariance property proofs are unchanged. |
| `WP 21.5E` | Security review of every surface added since the `v0.5.0` baseline, widened mid-review (Product Owner) to the whole live `src/` tree: **1 RED, 1 AMBER, 11 GREEN** findings (`docs/security/Security Posture.md`; RED filed as `TD-184`, owned by `WP 21.4A`'s own files and applied at its merge; AMBER filed as `TD-185`). `docs/security/Security Posture.md` (`WP RC.0C`, brought forward) replaces `Threat Model.md`/`Security Roadmap.md` for `v1.0` (both kept, pointer added). Dependency vulnerability scanning is now a required `ci.yml` check (`dependency-scan`, parsed by `scripts/check-vulnerable-packages.ps1`); `.github/dependabot.yml` added; `THIRD-PARTY-NOTICES.md` added (20 direct packages, all MIT or Apache-2.0). Proved, with a test, that the frozen REST API/plugin-loading/licensing layers (`src/Frozen/`) are unreachable in a default build and configuration. | *(pending)* |
| `WP 21.5F` | The offensive security audit of the full live codebase (`docs/security/Offensive Security Audit.md`): ten findings fixed with a proof-of-concept test each — OSA-02 Open externally on a directly-executable attachment (High; reconciled with `WP 21.4A`'s launcher, six more blocked extensions), OSA-01 unbounded decoded size in the file parsers, OSA-04/04b `FileSecretStore` permissions and token redaction, OSA-05 import/export depth, size and count caps (`TD-185`), OSA-06 the persistence root refusing a system path and the lock file no longer following a symlink, OSA-09 the harness principal, OSA-10/10b Actions pinned to commit SHAs and `SHA256SUMS.txt` on release assets, OSA-11 `THIRD-PARTY-NOTICES.md`; OSA-12/13/14/15 handed to `WP 21.6A` and fixed there; the OAuth loopback listener, macros, SQL/store and `src/Frozen/` reachability reviewed with no finding | `967eb536` |
| `WP 21.1A` | Undo across Create, Delete, Move, Copy, a status change and a field edit (Revise) — closing "Undo covers Rename and Favourite only." `CommandResult` gains an optional `Compensation` (`ADR-0099` addendum): the handler that made a change now also says how to reverse it, dispatched as a real command through `ICommandDispatcher`, never a direct repository write, checked against the archived-project guard explicitly since a compensation is never itself Ribbon- or Palette-visible. `IDeletable.UndeleteAsync` closes `ADR-0098`'s own disclosed "no restore operation" gap. Recorded onto the existing Undo/Redo stack wherever the real `CommandResult` is still held (the Ribbon's own two dispatch paths, the Command Palette's `CommandInvoked`, drag-and-drop reparenting) — no compensation is invented in a view. A status transition the platform-wide lifecycle table will not permit reversing (Approved → Released) carries none, and Command History says so rather than offering a silent no-op. A macro's own run now undoes and redoes as one compound action. The Undo/Redo stack itself now puts a refused Undo/Redo back where it came from rather than swapping it to the other stack (a real stack-consistency bug this Work Package's own scope surfaced), gains `Clear()`, and clears itself — reporting once — on every project switch. Delivered for Documents, Manufacturing, Calculations, Verification and Mechanical (`SetStatus` aside — Mechanical registers none); **Requirements is a disclosed exception**, unchanged, named in `BACKLOG.md`'s own entry (a different persistence path, `IRequirementsService`/`IEngineeringDocumentStore`, needing its own investigation before the identical guard-safety guarantee could be given). `PHYSICAL_REVIEW.md` §7e. | *(pending)* |
| `WP 21.5A` (`WP RC.0A`, brought forward) | Velopack-packaged Windows installer (`TempestOS-<tag>-Setup.exe`) with in-place update, off by default until enabled in Settings → Updates; an installed run's default persistence root (`%LOCALAPPDATA%\TempestOS\persistence-data`), a first-run location dialog, and `--persistence-root`/`Persistence:RootPath` overrides, closing `TD-36` for the installed case; a pre-migration backup (`BackupService`, the online SQLite backup API) fired automatically when a launch finds an older schema version, and Settings → Data's own "Back up now…"/"Restore from backup…"; the support matrix in `PHYSICAL_REVIEW.md` §2a. Adds **Velopack 1.2.0 (MIT)** — see `THIRD-PARTY-NOTICES.md` — referenced only by `Tempest.Desktop`. | *(pending)* |
| `WP 21.1B` The object editor split | `ObjectEditorView` — 3,007 lines, twenty-three sections in one constructor and one class, the file every package touching any Kind's own editor conflicted in — is now the shell alone: 1,001 lines (header, the change subscription, Identity and Content, the one pair the split's own Kill Switch keeps here for a hidden coupling through the shell's Save/Cancel/read-only state). Every other section (Lifecycle, Relationships, Validation, Bill of Materials, Owner/Priority, Execute/the Calculation pointer/Due, Record Result, Attachments, Description, Where used, Evidence's own five, Commercial, Invoice Lines/Connector, Quotation Lines) moved verbatim into its own file under `src/Tempest.Desktop/Editors/Sections/` behind one contract, `IEditorSection` (`Title`/`AppliesTo`/`Build`/`LoadAsync`/`React`) and one context record; the shell takes its ordered list from a single factory, `EditorSections.All(context)` — a future Kind's own new section adds one file and one line there, and two packages touching two different Kinds' own sections no longer touch the same file. Proved behaviour-identical by a golden automation-name tree per Kind (`tests/Tempest.Desktop.Tests/Editors/Golden/*.txt`, nine Kinds — Part, Assembly, Project, Calculation, Requirement, Verification Activity, Evidence, Invoice Request, Quotation — captured before the split, asserted byte-identical after every section moved) alongside the full existing editor test suite, untouched. | *(pending)* |
| `WP 21.7A` | Eleven engineering calculation modules under `Tempest.Core.Calculations.Modules` — beam bending and deflection, bolted joint preload, bolt group under eccentric shear, fillet weld throat stress (EN 1993-1-8), lifting lug and pin, column buckling (Perry-Robertson), shaft combined stress, bearing rating life (ISO 281), thick-walled cylinder (Lamé), thermal expansion stress, fatigue with Miner's rule — each specified under `docs/engineering/calculations/`, registered in the product catalogue, refusing inputs outside its method (`EngineeringCheckOutcome.OutsideMethodLimits`), material properties read from released records through `MaterialPropertyReader`, form descriptors for `WP 21.7B`; the Engineering Calculations catalogue now derives from the product list | `876ca187` |
| `WP 21.7B` | The Engineering Calculators — Engineering → Modules → Calculators: every product calculation (the five original definitions and the eleven `WP 21.7A` modules, sixteen in all) in a catalogue by category, a form generated from the descriptor of each calculation with a unit picker per quantity, material inputs filled from a released record and pinned onto the calculation, the result with its working, its constraint checks and its method reference, a refusal shown as the outcome; `CalculationModuleWorkbench` in `Tempest.Workspace`, `CalculationInputUnits` and descriptors for the five originals in `Tempest.Core` | `62c6cbb8` |
| `WP 21.0A` Docking everywhere, steps 1–2: the layout forest and the one controller (`ADR-0153`, Product Owner P0 2026-09-14) | `WorkspaceLayoutTree`'s `Root`/`Floating` are generalised into `Windows: IReadOnlyList<WorkspaceLayoutWindow>` — an ordered set of `(Id, Root, IsPrimary, MonitorKey, X, Y, Width, Height)` entries, exactly one primary — with every dock/float/remove/resize operation now reasoning across the whole forest rather than one root plus a separate floating list; a cross-window dock removes a panel from its source window's own subtree and inserts it into the target's as one atomic tree edit, and a non-primary window emptied by one closes, exactly as a floating window already did. `ReorderTab(groupId, panelId, direction)` is new (`ADR-0153` decision 8's own pure operation, not yet wired to a keyboard shortcut — that is `WP 21.0C`). The pre-existing `Root`/`Floating`/`DockedPanels`/`IsFloating`/`AllPanels` projection and 3-arg constructor stay exactly as they were, so no view and no other consumer needed a line changed; the full pre-existing suite passed unmodified before any new coverage was added. `WorkspaceLayoutSerializer.CurrentVersion` is now 2 (the window forest); a version-1 document still loads, into a forest with one primary window and every floating entry as a secondary window. `WorkspaceLayoutController` now owns every window's own tree: every secondary window's own host joins the same drag machinery the primary one already had (`WireHost`), so a drag started in one window resolves, in screen coordinates via `Control.PointToScreen`, against a candidate rendered in a completely different top-level window — proven by two real headless windows, with the one genuinely unprovable part named rather than assumed: Avalonia's headless platform does not incorporate a window's own `Position` into `PointToScreen`'s result (found empirically while writing the test), so the mechanism is proven self-consistently, not against a real per-monitor offset, which needs the manual pass below. Persistence carries a monitor identity and a monitor-relative, DPI-normalised rectangle (`ScreenList.cs`'s injectable `IScreenList`; the new pure `MonitorRelativePlacement`, converting a window's live geometry to relative-and-physical at save and back at restore against whichever screen its saved `MonitorKey` names, falling back to the primary screen when that monitor is no longer attached), proven by 9 pure tests including a rectangle saved at 150% and restored at 100% landing neither off-screen nor degenerate. Focus is restored after every re-render that moves a panel, including across windows — closing `TD-90` — by panel id rather than control identity, posted at `DispatcherPriority.Loaded` since the re-render's own brand-new tab header has not yet had a layout pass when `Apply` returns. The capture-lost routing defect is fixed, not merely inherited: `PointerCaptureLostEvent` moves from `RoutingStrategies.Tunnel` to `.Direct` (confirmed by compilation against the referenced Avalonia 11.3.20 that the event is `Direct`-only), proven by a test that drives the real routed event through the real controller. `tests/Tempest.Core.Tests`: 99 layout tests (was 81). `tests/Tempest.Desktop.Tests`: 73 controller/monitor tests (was 66), plus every named ADR seam suite (`ShellDockingSeamTests`, `ResponsiveWorkspaceTests`, `MainWindowResizeTests`, `ProductSpineAcceptanceTests`, `PanelNeverLostJourneyTests`, `AutomationNameCoverageTests`, `NoBlockingPersistenceCallsTests`, `LayoutWalkTests`) unmodified and green. **Manual verification still owed, named rather than assumed** (`ADR-0153`'s own risks 1–3): real multi-monitor input capture crossing a window boundary, and real mixed-DPI placement, on real Windows hardware — this package's own model, persistence shape and single-window mechanics are proven; multi-monitor dragging is not yet. Shell-level surfaces (which tabs and panes become dockable) and the Product Owner's own review of the ADR both wait for `WP 21.0B`. | *(pending — `wp/21.0A`, not yet merged)* |
| `WP 21.7C` | The Engineering Calculators completed — one released record per reference input (the lug's two materials each from their own picker), fastener and bearing records from their own released libraries driving the bolted-joint, bolt-group and bearing-life modules, the record pinned on the input and cited on the record; Re-run and Compare with previous offered on the result through the canonical `calculations.rerun` and `calculations.compare-with-previous` commands, a second Calculate recording onto the same calculation through `calculations.execute`, the comparison a table of the changed inputs and results with units; every product calculation registered as a Calculation Template; `CalculationModuleService`, the governed form-less entry point that runs any module on pinned inputs and returns the identical record; follow-ups from driving the real application for the Product Owner's screenshots — `b1318e00`: a read-back intermediate is materialised through its recorded type so the Working section never shows stored JSON after Re-run or Compare, and a different name typed at Calculate starts a new calculation instead of recording onto the previous one; `b033651d`: constraint lines report the offending value in the input's own unit, and the comparison table shows plain numbers to six significant figures and outcomes as words | `76b90c77` + `b1318e00` + `b033651d` |
| `WP 21.6A` | `OSA-12`/`OSA-13`/`OSA-14`/`OSA-15` (`WP 21.5F`) closed — the audit-log gap `WP 21.5F` sized as a feature and the principal/audit-collection architectural risk it deferred, all four fixed rather than filed or re-deferred (the Product Owner's own rule for this package). `RequirementsService`'s fourteen mutators, `VerificationService.RecordAsync`, and `ReferenceDataCatalog`'s `RegisterAsync`/`ReviseAsync`/`SupersedeAsync` now write an audit row (`AuditTransactionWriter.WriteAsync`, the identical primitive `EngineeringObjectBase` already uses) inside the same transaction as their own write — `RequirementsAuditActions`/`VerificationAuditActions`/`ReferenceDataAuditActions`, one action constant per mutator. `CurrentPrincipalAccessor.SetCurrent` is now `internal`; `PrincipalSession` is the one seam that can still reach it (an internal constructor, a single `Establish` method), used by `WorkspaceHost`'s start-up/"Switch person…" and `Tempest.Harness`'s own start-up — a component holding only `ICurrentPrincipalAccessor` cannot set or clear the current principal, closing `OSA-14` alongside `OSA-12`. The audit collection is write-protected at the store: `SqlitePersistenceStore` and the shared in-memory test double refuse a direct `Put`/`Delete`/transactional write against it (`AuditCollectionProtectedException`), the one route in a capability (`IAuditCollectionWriter`/`IAuditCollectionTransactionWriter`) held only by `AuditRecorder`/`AuditTransactionWriter`. **Item 1b, alongside the audit-row work on the same fourteen mutators:** `WP 21.1A`'s own disclosed Requirements exclusion is closed — `IRequirementsService.UndeleteAsync` (the soft-delete/undelete pair Requirements never had) and a `CommandCompensation` on `requirements.create`/`delete`/`move`/`move-group`/`set-status`, refusing Undo/Redo when a destination group has since been deleted or the lifecycle table forbids the reverse status transition, exactly as every `WP 21.1A` discipline already does; `BACKLOG.md`'s own exclusion note marked closed. `docs/security/Offensive Security Audit.md`'s OSA-12/13/14/15 rows and `docs/security/Security Posture.md`'s own residual audit-log note updated to reflect the closure. | *(pending)* |
| `WP 21.5D` | The mutation threshold met — the surviving and uncovered mutants in `Tempest.Core`'s `Calculations`/`UnitsAndQuantities` scope answered with real assertions. The one completed CI run (`34931908177` on `d1fef00b`) scored **67.58 %** against the 70 % break, concentrated in seven files (`QuantityVector.cs`, `BracketEngineeringRecordService.cs`, `BracketSectionCheck.cs`, `Dimension.cs`, `CalculationEngine.cs`, `Quantity.cs`, `GovernedBracketCheckService.cs`); this Work Package's own local scoped run (`--mutate` limited to those seven, thresholds and every other file left untouched) went **67.58 % → 80.86 % → 89.51 %** across two passes (435/49/2 killed/survived/no-coverage of 486, the second pass fixing bugs the first pass's own tests had — a unity conversion factor masking a `*`/`/` swap, a same-unit "fast path" round-trip that happened to be bit-exact for the values chosen, four SI exponents `Dimension`'s own multiply/divide tests never set non-zero, and two real branch-selection defects in `BracketEngineeringRecordService.Agrees()` a mutation would have hidden — the near-zero absolute-tolerance fallback keying off the *larger* of two magnitudes, not the smaller, and a normal-scale relative agreement that a forced absolute comparison must not reject). New/extended test files: `QuantityVectorTests.cs`, `DimensionTests.cs` (both new — neither type had a dedicated test file before), `QuantityTests.cs`, `BracketSectionCheckTests.cs`, `GovernedBracketCheckTests.cs`, `CalculationEngineTests.cs`, `CalculationInputRetentionAndReadbackTests.cs`, `BracketEngineeringDemonstrationTests.cs`, and a new `CalculationEngineListRecordsAndCorruptionTests.cs` (`ListRecordsAsync`/`ReadSummary`/`FindRecordAsync`'s own corrupted-content handling had zero coverage at all). Per-file scoped score after: `QuantityVector.cs` 98.9 % (92/93), `Quantity.cs` 98.3 % (58/59), `BracketSectionCheck.cs` 100 % (95/95), `Dimension.cs` 100 % (61/61), `GovernedBracketCheckService.cs` 92 % (23/25), `CalculationEngine.cs` 66.7 % (32/48), `BracketEngineeringRecordService.cs` 70.5 % (74/105) — the last two carry most of the remaining survivors, named with reasons in this Work Package's own report (equivalent mutants, `ConfigureAwait` literals unobservable under xUnit's default synchronization context, and a defensive branch needing store-internal access this Work Package did not build a spy for). `tests/Tempest.Core.Tests` (5,233) green in Debug and Release; `tests/Tempest.Desktop.Tests` (857) green in Debug; both configurations build clean, warnings as errors; governance 5/5. The lead's own full CI dispatch, over the entire `Calculations`/`UnitsAndQuantities` scope rather than these seven files alone, is the authoritative score. | `b664fea6` |

## Figures

Re-derived at `WP 21.9.0` on `904b0f81`; the two `WP 21.7C` follow-up
merges (`b1318e00`, `b033651d`) landed after — a handful of files each —
so the line figures move by under two hundred and the test counts below
are from the last one's own gate (`git grep -c '' -- 'src/*.cs'` excluding `Frozen/`; the test
counts from the gate; the effort from `Execution Plan.md` §5).

| Figure | v0.20.0 (`88311649`, the cut) | v0.21.0 (`904b0f81`) |
|---|---|---|
| Live source lines (`src/`, excluding `Frozen/`) | 145,377 | 171,715 |
| Live test lines (`tests/`) | 147,379 | 167,358 |
| `Tempest.Core` source lines | 65,926 | 75,257 |
| Core tests | 4,695 (at `ec20a535`, with the seven fixes) | 5,317 |
| Desktop tests | 705 (at `ec20a535`) | 884 |
| ADRs | 153 | 153 (`ADR-0153` steps 1–2 delivered; its status line updated by `WP 21.0A`) |
| Live backlog | 10 of 30 (after `WP 21.4A` closed `TD-184`/`TD-185`) | 10 of 30 (`TD-90` closed by `WP 21.0A`; `TD-183` kept open with the dynamic-port cause added) |
| Smoke-test sections | §7a–§7d | §7e–§7j added: Engineering Assets, Documents, Undo, Commercial edges, Calculators, Docking |
| Commits on the branch | — | 159 since `88311649`, 33 of them merges |
| Effort | — | 94.5 days planned across twenty-two packages; 77 merged (81 %), 1 in this package, 16.5 gated on the Product Owner |

## Gate on the candidate head

- Build: 0 warnings, 0 errors, Debug and Release, `TreatWarningsAsErrors`
- Core tests: 5,317 passed, 0 failed, Debug and Release (`b033651d`)
- Desktop tests: 884 passed, 0 failed, Debug (9 m 16 s) and Release (8 m 9 s), both on `b033651d`
- Governance health check: 5 of 5 (`b033651d`)
- CI: the sharded workflow ran on every merge head tonight; on `76b90c77` (the 21.7C merge) fully green, on `4c393842` the one Debug core failure was the dynamic-port collision `904b0f81` fixed (Release core green on re-run); the three runs on this candidate head — the push run plus two dispatched — are recorded on the candidate page and in `PROJECT_STATUS.md` at acceptance, with the CI Gate job as the criterion

## Warnings

- **The `TD-22` intermediate-result bound's defaults are guesses**
  (`WP 21.3A`): 200 intermediates / 1 MiB total per execution, sized off
  the six built-in definitions (at most five intermediates each) with two
  orders of magnitude of headroom, not measured against any real
  step-heavy calculation the product does not yet have. `CalculationContext`'s
  own constructor lets a caller override either; the lead should confirm
  the defaults before a calculation with a genuinely large intermediate
  set ships against them.
- **Rehydration's own kill switch, `WP 21.5B`**: `WP 20.1A2`'s
  business-identifier index rebuild still materialises every enforced-Kind
  object (Part, Calculation/CalculationSet, Document/Drawing/CadModel, the
  three Manufacturing Kinds, VerificationActivity, Evidence) eagerly inside
  `RehydrateAsync`, because `BusinessIdentifier` is a computed projection
  not on the index row and `BusinessIdentifierScope.ResolveProjectId`'s own
  synchronous repository read (outside this Work Package's files) needs an
  already-materialised ancestor chain. Bounded to that Kind set, not the
  unenforced majority a large estate is mostly made of. Separately, the
  benchmark's own `RehydrateAsync`-returning figure (~514 ms/1000 objects)
  reads slower in raw total than `WP 20.1C2`'s own ~190 ms/1000 — disclosed
  rather than hidden: it is dominated by relationship rebuilding, which
  this Work Package left fully eager and unchanged (id-keyed, not
  materialisation), and which was never benchmarked past a 1,000-object
  estate before now.

- **Docking: what is verified on a real screen and what is still owed to
  hardware (`WP 21.0A`, `WP 21.0K`).** K1 (a tab dropped onto a floating
  window docks into it), K2 (a floating window closes with its last panel,
  the main window untouched), K3 (a keyboard move keeps a visible focus
  ring), K6 (Reset Layout) and the new `Ctrl+Shift+,`/`.` reorder keys are
  verified in the real application on a Linux/X11 screen
  (`evidence/docking/`); a saved floating window restores to its exact
  position and size on one monitor. Still owed to real Windows hardware:
  K4/K5 (a second monitor, and the monitor-unplugged fallback through
  Avalonia's own `Screens`) and the save-on-close half of persistence
  (under Xvfb no window manager delivers `WM_DELETE_WINDOW`, so
  `MainWindow.Closing` could not be driven). Steps 3–4 (`WP 21.0B`,
  `WP 21.0C`) wait on the Product Owner's `ADR-0153` review.
- **The mutation score is scoped, not full (`WP 21.5D`).** 89.51 %
  (435 of 486 killed) is a local run with `--mutate` limited to the seven
  files behind the 67.58 % CI score; the full figure is re-measured by the
  next dispatched CI run (about three hours, advisory). From the CI
  report's own per-file table those seven files held 486 of the 842
  mutants and 267 of the 273 open ones (122 survived + 151 uncovered);
  with 51 now open there and the 6 elsewhere unchanged the whole would
  score about 93 % if the mutant sets matched — they do not exactly (the
  scoped run is local, and code from `WP 21.3A`/`21.7A` is mutated too),
  so treat 75 % as the floor until the dispatched run reports. Named
  equivalents and unobservables, left as they are: `separatorIndex <= 0`
  in both `TryParse` methods (`Trim()` makes index 0 unreachable); every
  `ConfigureAwait(false→true)` literal (about eleven across
  `GovernedBracketCheckService`/`CalculationEngine`) and
  `CalculationEngine`'s `_logger?.Information` call (no captured
  `SynchronizationContext`, no logger double under xUnit);
  `ListRecordsAsync`'s malformed-Guid `continue` (absorbed by the next
  guard); the `revisions.Count == 0` branch (unreachable through the
  public API). `BracketEngineeringRecordService.cs` keeps 31 survivors
  (descriptive string literals in the record builders; bit-exact `<`/`<=`
  boundaries in `Agrees()` at the 1e-12 scale) and `CalculationEngine.cs`
  its wrong-type-readback text — a deliberate cutoff once the score
  cleared 75 % with margin. No product code was touched; every kill came
  from tests.
- **The principal seam is narrowed, not removed from the container
  (`WP 21.6A`, OSA-12/14).** `CurrentPrincipalAccessor.SetCurrent` is
  internal and `PrincipalSession` has one public `Establish`, but
  `PrincipalSession` stays registered in DI because a dozen
  `Tempest.Samples` demo modules (never shipped, exercised by real
  `TempestHostBuilder` tests) constructor-inject it. The exploited surface
  went from "any resolver of the mutable accessor" to "any resolver of one
  named `Establish` call"; the "no live untrusted actor" disposition is
  unchanged. The audit-store guard (OSA-13) authorises both
  `AuditRecorder.RecordAsync` and `AuditTransactionWriter.WriteAsync` —
  refusing the former would have stopped ordinary audit recording
  everywhere. `EngineeringCockpit.RecentlyChanged` now excludes a row it
  cannot resolve to a title (the new Requirements/Verification/ReferenceData
  audit rows are not `EngineeringObjectBase` documents) rather than
  showing a raw Guid. `ReferenceDataCatalog.SetValidationStateAsync` is
  deliberately not audited a second time — `ReferenceReviewService`
  already writes that row.
- **The OAuth default loopback port sits inside Windows' dynamic port
  range.** `OAuthAuthoriser.DefaultLoopbackPort` is 49301; Windows hands
  out 49152–65535 to outbound connections, so another process's socket
  can hold that port for longer than `OAuthLoopbackListener`'s same-port
  retry budget. Six round-trip tests failed together on the hosted Debug
  core leg of run 35026262148 (each after about half a second, each with
  the honest "Port 49301 is already in use" refusal), green on re-run.
  `WP 21.9.0` moved every round-trip test onto a free port and let the
  one test that proves the default port accept either the bound redirect
  URI or that refusal. The product question is the Product Owner's: the
  registered redirect URI in `ADR-0151`'s addendum names 49301, and a port
  below 49152 would remove the collision for real users too — cheapest
  to change before the first live authorisation (`WP 21.6`). `TD-183`
  stays open with this cause added.
- **Two load races, seen once each in tonight's full-suite runs, green
  alone and on rerun:**
  `EngineeringAssetsHostRegistrationTests.TheTraceService_ReadsTheSameLibraryTheContainerHandsOut`
  (a null under load at the `WP 21.5B` merge) and
  `OAuthAuthoriserTests.AuthoriseAsync_UsesRealmIdFromTheCallback_NeverCallsTheTenantResolutionEndpoint`
  (the callback listener reset the fake browser's connection with four
  test hosts on the machine — the same family as above).
- **The golden automation trees compare line-ending-neutral
  (`WP 21.1B`).** The nine goldens under
  `tests/Tempest.Desktop.Tests/Editors/Golden/` are LF in the index; a
  checkout under `core.autocrlf=true` (this machine, the hosted Windows
  runners) reads them back CRLF while the walker emits LF. The comparison
  normalises both sides; the first-capture path still writes LF.
- **One merge was pushed with the Desktop test project not compiling.**
  `60824923` (the `WP 21.1B` merge) ran its chain with `;` after the
  build, so a stale test binary passed while
  `ObjectEditorGoldenAutomationTreeTests` did not compile; `4268c21c`
  repaired it minutes later. Recorded because the branch history shows it.
- **Every CI run between the `WP 21.5E` merge and `16dbabd6` failed on
  the core legs for one reason:** `check-vulnerable-packages.ps1` wrote
  its refusal through `Write-Error`, which Windows PowerShell wraps at the
  console width when stderr is redirected; the runner's long checkout path
  pushed the sentence over the wrap and `CheckVulnerablePackagesScriptTests`
  could not find it. It never failed locally. The script now writes
  through `[Console]::Error.WriteLine`.
- **Two descriptor counts, by design.** `CommandDescriptorBindingTests`
  counts every production descriptor (123 / 3 unavailable / 120 bindable,
  the new categories included); `CommandInvocationContractTests`'s
  production set excludes the new categories (111 / 3 / 108). A package
  adding a category updates the first, not the second.
- **Seeded fasteners are geometry-only (`WP 21.7C`).** A bolted-joint run
  on a seeded fastener record refuses as `RecordIncomplete` naming the
  missing property (proof strength, stress area) — honest, and the smoke
  test's C-steps will show that refusal until the Fasteners library
  carries mechanical properties.
- **Seen once, not reproduced (the Calculators, driving the real
  application on 2026-09-15 for the Product Owner's screenshots):** the
  catalogue's selected calculator jumped from Beam to Bolted joint after a
  mouse-wheel event over the right column. A controlled retry (wheel over
  the form, then over the catalogue) scrolled the intended panel and left
  the selection alone both times. Recorded as an unexplained one-off, not
  a finding; say so if it happens under your hand. The same drive found
  two real defects, fixed in the `WP 21.7C` follow-ups on this head: the
  Working section showing stored JSON after Re-run or Compare, and a
  renamed Calculate recording onto the previous object. Also noted there
  and left as they are: the Libraries panel reads "Fasteners: 0 released"
  on a fresh install (release one from the library first), and the
  default calculation name is the title plus the timestamp with " (2)"
  only when two runs share a second.
- **Not in this release, all gated on the Product Owner:** `WP 21.0B` and
  `WP 21.0C` (docking steps 3–4, after the `ADR-0153` review), `WP 21.5C`
  (the real-shell CI run, after `21.0C`), `WP 21.6` (the first live Xero
  authorisation, with the Product Owner at the keyboard). The Summary
  above names them as owed, not shipped.

- **One Desktop test diverges on Linux only (the overnight campaign's
  full runs on Linux):** `StatusBarCollapseTests.ALongerAreaTitle_AfterTheFirstLayout_StillCollapsesTheHint_AndTheMessageAreaKeepsItsWidth`
  fails deterministically under the Linux headless host ("With a short
  AREA title everything fits, so nothing should be hidden" — at 1,180 px the
  hint segment is already hidden), while the same test is green on the
  Windows runners (CI runs 430 and 433 on `a4ab1915`). Cause **Inferred**,
  not proven: the container carries only the DejaVu system families, so a
  text run that falls back to a system font measures wider than on
  Windows. Not a product defect on the supported platform; the real Linux
  application at 1,600 px shows the hint. Recorded, not filed — Linux is
  advisory (§2a of `PHYSICAL_REVIEW.md`).
- **Ten Core tests are Windows-only and fail on Linux by construction:**
  six spawn `powershell` (Windows PowerShell, not `pwsh`) for the release
  and dependency-scan scripts, four exercise DPAPI
  (`WindowsDpapiSecretStore`, and `AStoredToken_IsNeverWrittenToTempestDb`
  which constructs it). Green on the Windows runners. A conditional skip
  would make a Linux run read 0 failed; not changed tonight (the gate is
  Windows).
- **The installer has never been built by the pipeline** (`WP 21.9.1`
  quality evidence, item 3): `package-installer.ps1` and its eight unit
  tests are real, but `release.yml` only runs on a `v*.*.*` tag and the
  newest tag is `v0.18.0`, before `WP 21.5A`. Producing, installing and
  updating a `Setup.exe` is Windows work for the Product Owner (or a
  `-rc` tag) — `PRODUCT_OWNER_ACCEPTANCE.md` §3 and §5.
- **The first live Xero authorisation is still owed** (`WP 21.6`): `WP 21.6P`
  made the path exist and proved it up to the token exchange with a
  simulated redirect; the sign-in against the Product Owner's organisation
  needs their Xero app credentials and consent — `PHYSICAL_REVIEW.md` §7k.

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`
