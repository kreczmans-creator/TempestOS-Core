# ADR-0115: The Document Viewer Rasterises Through a Format-Keyed Page Source, and Is an Ordinary Workspace Panel

## Status

Accepted — `TD-80` (Drawing / Document Viewer), 2026-08-29. Depends on `ADR-0114` (durable attachment content) and `ADR-0095` (the data-driven workspace layout). Closes `TD-80` for the scope delivered; opens `TD-98`–`TD-101`.

## Context

Mock-ups 2 and 3 both centre on a drawing viewer. The 2026-08-28 Product Compliance Audit found nothing behind them: no image, PDF, DWG, SVG or bitmap rendering anywhere in `Tempest.Desktop`, `ZoomBy`/`PanBy` existing only in the Digital Thread *graph*, and — decisively — attachments that carried no bytes at all, so there was nothing to view even in principle.

`ADR-0114` removed the second obstacle. This one addresses the first, and the substantive question is what "render" is allowed to mean.

**A text-extraction viewer would not have been one.** The obvious pure-managed approach reads a PDF's text and lays it out at its recorded positions. That serves a specification or a procedure, and it fails completely at the case the mock-ups are actually about: an engineering drawing whose content is vector paths, where text extraction yields the title block and nothing else. Shipping that and calling it a drawing viewer would be the placeholder this work package was told not to build.

## Decision

**1. PDF pages are rasterised by a real rasteriser, on demand, at the current zoom.**

`PdfDocumentPageSource` renders through PDFium (`PDFtoImage`), which draws the page's real content — vector artwork, embedded raster images and text alike. Rendering happens per request at the requested scale rather than once at a fixed resolution, so zooming into a detail re-rasterises and shows more of the drawing rather than magnifying earlier pixels.

A native dependency was accepted deliberately. It is the only way to make "open the drawing" true, PDFium ships binaries for every platform this desktop application runs on, and the alternative was a viewer that works for text documents and lies about drawings.

**2. Format is decided by an `IDocumentPageSource`, keyed by format in one factory.**

PDF, raster image and text each implement page count, page size and render-at-scale. The viewer knows those three things and nothing about any format. Adding one is a new implementation and a line in the factory — no change to the viewport maths, the page navigation, the control, or the workspace integration.

A format with no source is `Unsupported`, which is a **third** state distinct from `Missing` and `Corrupt`. Collapsing it into `Corrupt` would tell a user their intact file is damaged, which is a false accusation about their data; the honest message is about us, not them.

**3. The viewport is a pure immutable value in `Tempest.App`, with no rendering type in it.**

`DocumentViewport` decides every zoom, pan, fit and resize rule; `DocumentViewSession` decides page navigation and what a failed open means. Both are tested with no UI in the process — `ADR-0095`'s discipline applied again, for the same reason: geometry that lives in an event handler can only be tested by raising events.

Two invariants are properties of the type rather than habits of its callers: the offset is always clamped so content larger than the view cannot scroll past its own edge, and content smaller than the view is centred rather than pinned to a corner. Zooming is anchored, so the point under the pointer stays under it — the difference between a magnifier and a slider.

**4. Format is decided by the bytes first, the declared content type second.**

A content type is a claim by whoever attached the file; the first few bytes are the file. Renaming rather than converting is something people do, and a viewer that trusts the extension shows an error over a picture it could have rendered.

**5. A viewer is an ordinary `TD-72` panel, not a reserved surface.**

`AttachmentViewerLauncher` registers a `WorkspacePanelDescriptor` and docks it into the layout tree, tabbed with the document area. It therefore tabs, splits, floats onto a second monitor, collapses and persists with no code for any of it, and opening a second document is the same call again — there is no fixed number of viewers because there is no fixed grid to run out of.

**6. Opening a document is never navigation.**

The shell is untouched: the project context, the open object and the Explorer selection are all still there when the tab is closed. A viewer that took over the window would make "look at the drawing" cost the user their place.

## Consequences

**What this buys.** A user can attach a drawing, restart TempestOS, open it, zoom, pan, fit and page through it, open a second beside it, and close it without losing where they were. Mock-ups 2 and 3 have a real surface behind them for the first time.

**Markup and annotation are not implemented.** `TD-80`'s own original text lists them alongside zoom, pan and fit, and this work package's scope did not. They need an annotation model, persistence for it, and a decision about whether annotations are engineering objects — a work package, not a control. Recorded as `TD-98`, and the reason `TD-80` is closed *for the scope delivered* rather than silently declared complete.

**Rotation is not implemented.** Cheap in isolation, but it belongs with markup rather than on its own. Part of `TD-98`.

**DWG and SVG are not viewable.** DWG needs a licensed library and SVG needs a renderer this platform does not have; both currently report `Unsupported`, honestly. Recorded as `TD-99`.

**Image decoding is not verified by the automated suite.** The headless platform the Desktop tests run on substitutes a stub decoder that reports every image as 1×1 and accepts bytes that are not an image at all. The tests probe for that and say plainly which half of it they are in, rather than asserting a falsehood; real decoding is exercised on a real platform. Recorded as `TD-100`.

**A page is rasterised at full page size even when only part is visible.** Adequate for the document sizes the workflow names, and a real cost for a very large drawing at high zoom. There is a hard cap on the raster edge so deep zoom degrades in sharpness rather than in availability. Tiled rendering is recorded as `TD-101`.

**A new native dependency.** `PDFtoImage` brings PDFium binaries per platform. It builds and runs clean on Linux and on the Windows CI runner; on any platform PDFium does not support, the factory reports PDF as `Unsupported` through a runtime guard rather than a suppressed analyser warning.

## Alternatives Considered

**Text extraction (PdfPig or similar).** Pure managed, no native dependency, and unable to render a drawing. Rejected: it would have passed for documents and failed silently for exactly the case the mock-ups are about.

**Rendering once and scaling the bitmap.** Simpler and cheaper, and it makes zoom show larger pixels rather than more detail — which defeats the purpose of zooming into a drawing.

**A reserved viewer surface in the shell.** Rejected as the fixed-grid thinking `ADR-0095` removed. A viewer that is an ordinary panel gets tabbing, splitting, floating and persistence for free.

**One `DocumentViewStatus` for every failure.** Rejected: `Missing`, `Corrupt` and `Unsupported` call for different actions from the user, and a single "could not open" hides which one they are in.

## Related

`ADR-0114` (the durable content this reads) · `ADR-0095` (the layout tree this docks into) · `TD-31` (closed, and the precondition) · `TD-80` (closed here) · `TD-98`/`TD-99`/`TD-100`/`TD-101` (opened here) · Mock-ups 2 and 3; brief §12
