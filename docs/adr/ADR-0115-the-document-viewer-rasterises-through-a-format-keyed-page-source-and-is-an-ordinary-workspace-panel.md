# ADR-0115: The Document Viewer Rasterises Through a Format-Keyed Page Source, and Is an Ordinary Workspace Panel

## Status

Accepted — `TD-80` (Drawing / Document Viewer), 2026-08-29. Depends on `ADR-0114` (durable attachment content) and `ADR-0095` (the data-driven workspace layout). Closes `TD-80` for the scope delivered; opens `TD-98`–`TD-101`. Visually accepted against the supplied mock-ups on 2026-08-29, after the audit recorded below.

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

## What the Visual Acceptance Audit Found

The decisions above were taken with 287 green Desktop tests and no rendered frame. The Desktop suite runs on Avalonia's headless platform with `UseHeadlessDrawing` on, so nothing is ever rasterised and `CaptureRenderedFrame` throws; the audit therefore drove the real `MainWindow` through a throwaway `UseSkia()` harness and looked at the PNGs. Four user-visible defects were in the delivered scope — one of them fatal to the feature — and all four are fixed:

**There was no Open button, so the viewer was unreachable from the running application.** `ObjectEditorView.TryCreate` populates the editor before it returns; an attachment row only carries an Open button when something can handle the request; and `WorkspaceViewCoordinator` subscribes to `OpenAttachmentRequested` after `TryCreate` returns. On a freshly opened object the button was therefore never built. Every headless test passed because every one of them called `AttachmentViewers.OpenAsync` directly — verifying the destination and never the door. `OpenAttachmentRequested` is now a custom event accessor that re-populates the attachment rows on its first subscriber, closing the ordering hazard in the one place that can see it rather than requiring every caller to refresh after wiring up.

**A viewer tab closed from the tab strip could never be re-opened.** `LayoutTabGroupView`'s close button removes the panel from the layout tree, which is `TD-72`'s business and tells `AttachmentViewerLauncher` nothing. The launcher kept the attachment in its open map, so the next open took the bring-forward path and called `SelectPanel` on a panel that no longer existed — a silent no-op that left the drawing unreachable for the rest of the session. The launcher now reconciles its map against `WorkspaceLayoutTree.Contains` before trusting it, which also covers a panel that is floating rather than docked.

**`Missing`, `Corrupt` and `Unsupported` each showed a full page-and-zoom toolbar over a surface that could not use any of it** — page arrows, a zoom stepper, Fit and 100%, disabled, around an empty gap where the page indicator had been, above "No content stored". Chrome that reads as a working viewer whose right button the user has not found yet. Those controls are now hidden rather than merely disabled when nothing is open: in that state the message is the whole of what the viewer has to say, and it should be the whole of what the viewer shows.

**Turning to a page of another size drew it stretched into the previous page's shape.** `DocumentViewport` carries the content size that decides both the fit zoom and the rendered width and height a page is drawn at, and `DocumentViewSession.WithPageSize` re-fits for exactly this case — but nothing called it on a page turn, so the new page was drawn into the previous page's rectangle, measured on the audit's own two-sheet drawing at roughly half the second sheet's true height. This codebase's multi-page test fixture had been built with three deliberately different page sizes precisely so that this case existed. The correct model was there from the first commit and the view never asked it. `DocumentViewerView` now tells the viewport the current page's own size before anything is measured or drawn.

Everything else held. PDF rendering is genuinely vector-derived — the captured frames show stroked paths and a filled rectangle, not extracted text. The viewer is a real tab in the document group, tabs and selects alongside the object editors, keeps per-document page and zoom state, re-fits on resize, and leaves the module rail, ribbon, Explorer, Properties panel and status bar exactly where they were.

Four divergences from the mock-ups are real and none is closed here. Three are capabilities rather than defects: the sheet-thumbnail navigator beside the drawing and the zoom **slider** in the footer (mock-up 4), and the markup palette, rotate and layers tools (mock-up 1, already `TD-98`/`TD-99`).

The fourth is this ADR's own decision 5, seen rendered. Mock-up 4 places the drawing in a column *beside* the object it belongs to; tabbing it *into* the document group means opening a drawing hides the object editor behind a tab instead. That is not a defect — the panel drags out to a split in one gesture, and a viewer that claimed its own column would be the reserved surface decision 5 rejected — but it is a real difference in information hierarchy, and it is recorded rather than quietly changed, because re-deciding a default docking position is a design call and not an audit finding.

The mock-ups' "Sheet N of M" is "Page N of M" here, which is the honest wording for a surface that also shows specifications and datasheets.

## Alternatives Considered

**Text extraction (PdfPig or similar).** Pure managed, no native dependency, and unable to render a drawing. Rejected: it would have passed for documents and failed silently for exactly the case the mock-ups are about.

**Rendering once and scaling the bitmap.** Simpler and cheaper, and it makes zoom show larger pixels rather than more detail — which defeats the purpose of zooming into a drawing.

**A reserved viewer surface in the shell.** Rejected as the fixed-grid thinking `ADR-0095` removed. A viewer that is an ordinary panel gets tabbing, splitting, floating and persistence for free.

**One `DocumentViewStatus` for every failure.** Rejected: `Missing`, `Corrupt` and `Unsupported` call for different actions from the user, and a single "could not open" hides which one they are in.

## Related

`ADR-0114` (the durable content this reads) · `ADR-0095` (the layout tree this docks into) · `TD-31` (closed, and the precondition) · `TD-80` (closed here) · `TD-98`/`TD-99`/`TD-100`/`TD-101` (opened here) · Mock-ups 2 and 3; brief §12
