# Real Files and Real Records: Attachments, the Library Editor and the Viewer

**Release:** `v0.19.1` release candidate (`release/v0.19.1`, head
`94998b9`) · `v0.20.0` release candidate (`release/v0.20.0`, head
`3ce8f20`) · **Work Package(s):** `WP 19.4B`, `WP 19.6A`, `WP 19.10P`,
`WP 20.1C1`, `WP 20.2B` · **Debt:** `TD-95`, `TD-96` (closed) · `TD-98`
(rotation closed; markup stays open) · `TD-99` (narrowed, not closed) ·
**Code:** `Tempest.Desktop.Editors.ObjectEditorView`,
`Tempest.Desktop.Views.ReferenceRecordView`/`LibrariesView`,
`Tempest.Core.EngineeringDomain.AttachmentContentStore`,
`Tempest.Core.Persistence.SqlitePersistenceStore` (`OpenReadAsync`),
`Tempest.Workspace.Viewing.ViewableDocument` (`DocumentFormatDetector`),
`Tempest.Desktop.Viewing.AttachmentViewerLauncher`/`DocumentViewerView`

**In plain terms.** A calculation with a spreadsheet clipped to it, a
material's yield strength looked up in a library — both only count as
real once a person can drag the actual file onto the object, and open
the actual record rather than read one summarised line about it. This
chapter covers four pieces of work that made that true everywhere: every
object that can carry a file gained a real drop zone, every one of the
eight reference libraries gained a real screen with its history and who
relies on it, a file attached twice is now stored once rather than
twice, and a drawing format TempestOS cannot draw now says so honestly
and hands it to the program that can. Neither candidate is released yet.

## Two comments from the same testing session

`WP 19.4B` and `WP 19.6A` both answer the Product Owner's first pass at
`v0.19.0`. Comment 2 found a Calculation's Attachments section still
asking for typed metadata — File Name, Content Type, Size — when
Evidence already let a person drag a real file onto the screen
(`63-the-evidence-workspace.md`, `WP 18.2A`). Comment 5 found a library
record rendered as "one text line" — `fst-m10-coarse — M10 x 1.5 • rev
3 • Checked • …` — when the Product Owner wanted the record itself:
fields, history, citation, what depends on it. Both point at the same
failure mode: a capability built for one surface that never
generalised. Both Work Packages are the generalising.

## Attach means attach, on every `IHasAttachments` Kind

The picker button on an Attachments section had been gated to
Evidence's own Kind by name, even though the byte-carrying path
underneath it (`IHasAttachments.AttachContentAsync`) was never
Evidence-only — only the button's visibility was. `WP 19.4B` removes
the gate, renames the button "Browse…", and adds a drop zone beside it
(the same `DragDrop` wiring `EvidenceWorkspaceView` already used) that
funnels a real drop into one internal method both the UI and the tests
call directly, since headless Avalonia cannot raise a real OS drop:

```csharp
internal async Task AttachFilesAsync(IReadOnlyList<string> paths)
{
    foreach (var path in paths)
    {
        var fileName = Path.GetFileName(path);
        var content = await File.ReadAllBytesAsync(path).ConfigureAwait(true);
        await attachable.AttachContentAsync(fileName, FileContentTypes.ForFileName(fileName), content).ConfigureAwait(true);
    }
}
```

The old typed form does not disappear; it moves under a collapsed
expander headed **"Record a reference without the file"** — the honest
name for what it always did. Six new tests (`AttachmentsSectionTests`)
proved Browse and the drop zone on Calculation, Part and Document, a
real `.xlsx` landing with the right size and SHA-256, and one defensive
case: an object with no live Kind lacking `IHasAttachments` shows no
Attachments section (every canonical class implements every facet
unconditionally, so this never actually happens). A seventh confirmed a
Calculation's attachment opens through the real viewer exactly as a
Document's does. The rehearsal's own D8 step matched this live.

## A record is not a line of text

`WP 19.6A` gives all eight governed libraries a real screen,
`ReferenceRecordView`, without building eight near-identical editors:
it renders **any** library's definition generically, by reflecting
over its public, non-`[JsonIgnore]` properties — a quantity shows its
own unit, a nested type indents as a group, a list or dictionary
becomes a small table, and anything with no sensible generic display
falls back to its own `ToString()`, the one deliberate kill switch the
Execution Plan allowed. Beside the definition sit revision history (the current
revision marked), source citation
(`60-source-citations-and-supersession.md`), and a new **cited by**
section from `ReferenceCitationIndex` — one scan of every Evidence
object across every project, because nothing in the reference-data
model tracks the reverse direction on its own. Verify, Release and
Revise call the identical governed acts `LibrariesView` already used.

`LibrariesView` becomes master and detail: Open or a double-tap opens
the record beside the list at ordinary widths, or replaces the list
with a Back control below `DesignTokens.CompactShellWidth` — the same
threshold the rail, header and ribbon already fold at. Add and Revise
both open the new record right up.

**The mistake worth keeping.** The master/detail container was first
built as a `Grid`. Existing tests find a list row by searching
`GetLogicalDescendants().OfType<Grid>().First(row text match)` — and
once the *container* was itself a `Grid`, that search matched the
container before it ever reached a real row, since the container's own
descendants transitively contain every row's text too. `d4599a5`
swapped it for a `DockPanel`, which gives the identical
side-by-side-or-full-width behaviour without being reachable by that
search. **A layout container is not a neutral choice once a test finds
its target by walking the control tree.**

## The rehearsal's own correction

`Rehearsal.md`'s D15 step opened a Materials record and found identity,
history with "(current)", cited-by and Verify/Release all correct. It
also found two real defects: `LibrariesView.RefreshAsync` grouped only
records that already existed, so Manufacturing and Components — no
baseline seed — were invisible rather than listed at zero, which
`PHYSICAL_REVIEW.md` names as an explicit failure; and the eighth
library reached the screen as the literal routing key
`"BusinessRateCards"`. `WP 19.10P` fixed both without touching what
code keys on: `RefreshAsync` now iterates the eight catalogues' own
`LibraryName` values directly, so every library always gets a heading
and an empty one reads "No records yet"; `ReferenceLibraryAccess.DisplayNameFor`
maps the one irregular key to **"Rate cards"** at the
two places it reaches a person, while every switch case and the
catalogue's own `LibraryName` stay unchanged underneath. Only what
reaches the screen changed.

## One file, one copy

`ADR-0114` had explicitly deferred deduplication as `TD-95`: "content-
addressed storage would deduplicate for free, and was rejected here:
it makes deletion a reference-counting problem." Storing the same
document three times cost real disk, silently, every time one file was
attached to more than one object.

`WP 20.1C1` closes it. `AttachmentContentStore` now keys bytes by their
own SHA-256 rather than by attachment Id, riding in three collections
of the platform's one generic `records` table (no SQL schema change —
that table was already collection/key-agnostic): the bytes themselves,
a reference count per hash, and a mapping from each attachment's Id to
the hash it currently uses. In plain terms: a file is now named by
*what it contains*, not by who attached it first — attach the same
drawing to three parts and the store holds it once; delete one
attachment and the other two keep their content until the last
reference is gone. A pre-existing, attachment-Id-keyed row migrates
into this layout the first time `ReadAsync` finds it, rather than all
at once — so an old attachment is deduplicated only once something
actually reads it, and restarting the platform costs nothing extra.

## Reading a large drawing without holding all of it

`ADR-0114` also deferred `TD-96`: `ReadBytesAsync` returns a `byte[]`,
so opening anything meant materialising the whole file. `WP 20.1C1`
adds `IBinaryPersistenceStore.OpenReadAsync`, a seekable `Stream` over
one BLOB through `Microsoft.Data.Sqlite`'s `SqliteBlob` — real
incremental blob I/O, not a chunked read dressed up as one.
`AttachmentContentStore.OpenReadAsync` layers verification on top: a
bounded-memory pass recomputes the hash over the stream in 64 KB chunks
before a **second**, independent stream is handed back to consume — the
file is still checked before it is trusted, at the cost of one extra
pass rather than a second copy in memory. The Document Viewer's page
sources (`38-document-and-drawing-viewer.md`) read through this path
first, falling back to the byte-array read only where the streamed
sources do not yet cover a format. A 20 MB attachment now opens in some
three hundred reads of 64 KB each, allocating under 5 MB.

## DWG opens in its own program; SVG does not open at all

The Product Owner's decision of 2026-09-15 (§5) settled DWG plainly:
keep it a stored attachment with an *Open externally* action rather
than license a CAD-rendering SDK. `WP 20.2B` adds `ExternalOnly` as a
third outcome beside `Unsupported`, on purpose distinct: "your file
opens in its own application" is a truer sentence than "we don't know
what this is." `DocumentFormatDetector` — which actually lives in
`Tempest.Workspace`, not alongside the viewer's own Desktop classes —
recognises `.dwg`/`.dxf` by extension first, since neither format has
an IANA-registered content type, then by the content types CAD tools
commonly send. `AttachmentViewerLauncher` writes the real bytes to a
file under the OS's own temp folder — never beside the persistence
root — and `DocumentViewerView` offers Open externally through an
injectable launcher (`Process.Start`, `UseShellExecute = true`, by
default) for `ExternalOnly` and for any plain `Unsupported` format
alike.

Rotation shipped alongside it, careful about what it claims: Rotate
left/right turn the **rendered bitmap only**, in 90° steps, remembered
per open tab — the page source itself is never asked to rotate
anything. A recording `IDocumentPageSource` in
`DocumentViewerRotationTests` proves it directly: rotating right issues
one more `RenderPage` call, at the same page index and scale as before.
The viewport's own fit-and-zoom maths still runs over the page's
un-rotated size, so a rotated landscape page may need an extra zoom
step to fit — disclosed, not hidden.

**The kill switch, invoked honestly.** The same decision had called SVG
"small" — an in-app page source, unlike DWG's external open. Building
it found the opposite: no SVG rasteriser (`Svg.Skia`, `SkiaSharp.Extended`,
`Avalonia.Svg.Skia`) is referenced anywhere in the solution,
and the Execution Plan's own fallback for exactly that case was to add
no new package and report rather than implement. `WP 20.2B` did precisely
that — SVG stays exactly the `Unsupported` format it already was,
pinned by a test that asserts nothing changed — and `TD-99` is
**narrowed**, not closed: its DWG/DXF half is done, its SVG half stays
open. A plan's own estimate of a task's size is not a fact about the
codebase, and finding out it was wrong belongs in the backlog, not
smoothed over in the release notes.

## What was deliberately not built

Markup, annotation and any DWG rendering stay out — `TD-98`'s and
`TD-99`'s own remaining halves, `ADR-0115`'s disclosed scope cut from
the start. Nothing collects an attachment's orphaned content once every
reference to a hash is released — this closed deduplication and
streaming, not garbage collection. Rotation persists only for an open
tab's own life, not across a restart, and a materialised temp copy for
Open externally is left under the user's own temp folder for the OS to
reclaim, never deleted by TempestOS itself.

## What to take away

- **A capability built for one surface is not finished until it
  generalises**, and the Product Owner's own testing — not a design
  review — is usually what finds the surface it forgot.
- **A layout container is part of a test's own contract once a test
  finds its target by walking the control tree**, so a `Grid` becoming
  a `DockPanel` is not a purely visual change.
- **Change what reaches the screen, not what code keys on** — "Rate
  cards" and a content-addressed hash both prove it from opposite
  directions: the display changed, the identifier did not.
- **When a decision's "this part is small" turns out to need a library
  the plan forbids adding, say so and leave the row open** — never
  ship something smaller than what was asked for under the same name.
