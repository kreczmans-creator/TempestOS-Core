# TempestOS v0.21.0 — Release Notes

**Status: in preparation on `release/v0.21.0`, cut from the `v0.20.0`
candidate (`88311649`) on 2026-09-15 at the Product Owner's instruction
to close every technical weakness named in the lead's assessment of that
afternoon.** Nothing in this document is certification. `v0.20.0` stays
under the Product Owner's manual test as the candidate, receiving the
`WP 20.10A`–`20.10G` fixes; every one of those is merged here too.

## Summary

**v0.21.0 is the recovery tranche** — the docking rewrite to `ADR-0153`,
Undo across commands, the editor split, documents from every template,
the Engineering Assets surfaces, typed calculation results with retained
inputs, the commercial edges, the viewer's remaining formats and markup,
the installer with upgrade, backup and restore, lazy rehydration, a
real-shell run in CI, and the mutation threshold met. See
`Execution Plan.md` for the packages and their waves.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 21.4A` The viewer: SVG in-app, markup and annotation, tiled rendering, and a same-session security fix (`TD-99`, `TD-98`, `TD-101`, `TD-184`) | `TD-99`: `Svg.Skia` **2.0.0.8** (MIT-licensed; see `THIRD-PARTY-NOTICES.md`), pinned to the last release on its 2.x line — the only one whose own `SkiaSharp` dependency floor (2.88.9) matches the version this solution already resolved through `PDFtoImage`, satisfying this Work Package's own kill switch without a version bump. `SvgDocumentPageSource` rasterises through it to the same `SKBitmap`-backed page `PdfDocumentPageSource` already produces; `DocumentFormatDetector` recognises `.svg` by extension, `image/svg+xml` by content type, and a bounded content sniff for a mislabelled file; a malformed SVG reports "This SVG could not be read: {reason}" with Open externally still offered, never a crash. `TD-98`: `AttachmentAnnotation`, a new record kept beside an attachment's owner (`EngineeringObjectState.Annotations`), written one transaction per mutation with an audit row through the same `MutateAndPersistAsync` path `AttachAsync` already uses, rehydrated with the owner and carried onto a revised instance exactly as attachments already are — never in the attachment's own bytes. `DocumentViewerView` gains the Annotations toolbar group (Rectangle, Ellipse, Freehand, Arrow, Text note, five design-token colour swatches, Delete, Clear page — asked first, through an embedded `ConfirmationDialog` — Save annotated copy…) over a `Canvas` overlay positioned exactly on the rendered page; each rendered shape hit-tests itself for select/delete through Avalonia's own pointer routing. **Save annotated copy…** composes a page's own annotations onto a copy of its rendered bitmap and writes a PNG directly, or — for a PDF source — a hand-written single-page PDF (`MinimalPdfWriter`: one FlateDecode raw-RGB image XObject; no new PDF-authoring package, per the kill switch), through the file picker, leaving the original attachment untouched. `TD-101`: `DocumentViewport.WithContentSizeSwapped` closes the `WP 20.2B`-disclosed rotation/fit bug at its root — content width/height now track the page's currently-displayed (rotated) bounding box, so a rotated landscape page fits with no manual zoom step. Tiled rendering: `TileGrid` (pure tile-planning math, tile size **512px**, stated) and `TileCache` (bounded least-recently-used cache, memory budget **256 MiB**, stated) back a new `ITiledDocumentPageSource`/`PdfDocumentPageSource.RenderTile`, rasterising one region directly through `PDFtoImage`'s own `RenderOptions.Bounds` rather than a whole-page render sliced afterwards; `DocumentViewerView` composes a page from cached tiles instead of one `MaxRasterEdge`-capped, degraded render whenever a whole-page render at the requested zoom would exceed that cap — an A0 sheet at deep zoom renders sharp, and a pan at that zoom issues zero further tile renders once the composite is cached (`RenderCurrentPage`'s own existing "re-rasterise only when the page/zoom/rotation actually changed" rule, unchanged). **`TD-184`** (found same-session by `WP 21.5E`'s defensive review, fixed before anything else per the Product Owner's rule): `AttachmentViewerLauncher.MaterialiseForExternalOpen` used to write a materialised copy under an attachment's own, completely unexamined file name into a directory keyed by the attachment id — so an attachment named `invoice.pdf.exe` whose bytes were a real executable ran as code the instant "Open externally" was pressed. Closed: a `DangerousExtensions` denylist refuses materialisation outright with an honest reason (`DocumentViewSession.ExternalOpenRefusedReason`); an `ExternalOnly` (DWG/DXF) attachment's written extension comes from the detector's own verified match, never the raw name; the file name is sanitised (path separators, control and Unicode bidi-override characters stripped, reserved device names guarded, trailing dots/spaces trimmed); the materialised copy lands in a fresh, randomly-named per-launch directory, deleted when the viewer closes. `Svg.Skia`'s own image resolution (`Svg.Model.SvgExtensions.GetImageFromWeb`) calls `WebRequest.Create(uri).GetResponse()` for any `<image>` reference that is not a `data:` URI — `http://`, `https://` and `file://` alike — so `SvgMarkupSanitiser` blanks every such reference to an inert `data:,` URI, and strips a `<!DOCTYPE>` (the XXE vector) and any `<script>` element, before the bytes ever reach `SKSvg.Load`. | *(pending gate)* |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`
