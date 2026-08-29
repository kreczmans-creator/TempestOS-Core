# Document & Drawing Viewer

**Programme:** Product Convergence & Recovery, 2026-08-29 ·
**Debt:** `TD-80` (closed for scope delivered) · **Decision:** `ADR-0115` ·
**Code:** `Tempest.App.Workspace.Viewing`,
`Tempest.Desktop.Viewing`

## The question that decided the design

Mock-ups 2 and 3 both centre on a drawing viewer. The obvious pure-managed
way to "render" a PDF in .NET is to extract its text and lay it out at the
recorded positions — no native dependency, no platform binaries, works
everywhere.

It also fails completely at the thing being asked for.

An engineering drawing's content is **vector paths**. Text extraction on
one yields the title block and nothing else. That approach would have
passed for specifications and procedures, produced a nearly-empty page for
every actual drawing, and been indistinguishable from working right up
until someone opened one.

So the decision was to take the native dependency and rasterise for real
(PDFium, via `PDFtoImage`). It was verified before a line of viewer code
was written, by rendering a PDF containing one red stroked rectangle and
counting non-white pixels: 3516 of them, the first at (24,24) in
`R255 G36 B36`. That is the difference between a renderer and a blank
bitmap of the correct dimensions, and it is worth establishing before you
build on it.

> **The transferable lesson.** When a dependency decision turns on
> "does this actually do the thing", answer it with a five-line probe
> before you design around the answer. The cost of finding out afterwards
> is the whole design.

## Rendering on demand, at the zoom

```
IDocumentPageSource
    int  PageCount
    Size PageSize(pageIndex)          <- the page's own units
    Bitmap RenderPage(pageIndex, scale)
```

`RenderPage` takes the scale. A page is rasterised **at the current zoom**
rather than once at a fixed resolution and then stretched — which is the
whole reason a vector format is worth rasterising on demand. Zoom into a
detail and you get more of the drawing; the other way, you get larger
pixels.

Three sources implement it: PDF, raster image, text. The viewer knows page
counts, page sizes and how to draw a bitmap, and nothing about any format.
Adding one is a new implementation and a line in the factory — no change
to the viewport maths, the page navigation, the control, or the workspace
integration.

## The geometry is a value, not an event handler

`DocumentViewport` is immutable, pure, and contains no rendering type at
all. Every zoom, pan, fit and resize rule lives there and is tested with
no UI in the process — the same discipline `TD-72` used on the layout
tree, for the same reason: **geometry that lives in an event handler can
only be tested by raising events.**

Two rules are invariants of the type rather than habits of its callers:

- **Offset is always clamped.** Content larger than the view cannot scroll
  past its own edge; content smaller than the view is centred rather than
  pinned to a corner. "The drawing scrolled off into grey space" is not
  reachable.
- **Zoom is anchored.** The point under the pointer stays under it. That
  is the difference between a magnifier and a slider.

A bug caught while writing it: `PanBy` originally ran its delta through
the same sanitiser that guarantees a *dimension* is positive. A leftward
pan of −100 became a rightward pan of +1. It looks like a rendering
glitch and it is arithmetic.

## Three failure states, not one

```
Ready  |  Missing  |  Corrupt  |  Unsupported
```

`TD-31` was careful to keep "we never held this file" apart from "we held
it and this is not it". The viewer adds a third: "your file is intact and
we have no renderer for it."

Collapsing that into `Corrupt` would tell a user their perfectly sound
`.docx` is damaged — **a false accusation about their data.** An admission
about our capabilities is the honest message, and it is a different one.

## An ordinary panel, not a reserved surface

The viewer registers a `WorkspacePanelDescriptor` and docks into the
`TD-72` layout tree, tabbed with the document area. It therefore tabs,
splits, floats onto a second monitor, collapses and persists **with no
code in the viewer for any of it**. Opening a second document is the same
call again: there is no fixed number of viewers, because there is no fixed
grid to run out of.

And opening one is *not navigation*. The shell is untouched, so the
project, the open object and the Explorer selection are all still there
when the tab closes. A viewer that took over the window would make "look
at the drawing" cost the user their place.

## What the tests found

Eight mutations, eight killed — fit stops fitting, pan stops clamping,
zoom ignores its anchor, page navigation stops clamping, the renderer
ignores the zoom, corrupt is reported as missing, unsupported is reported
as corrupt, and the detector trusts the label over the bytes.

Two more interesting findings came from tests failing against *correct*
code:

**The headless platform's image decoder is a stub.** A test asserting a
4×3 PNG decodes to 4×3 failed. Probing showed the headless platform
decodes *everything* — a valid PNG, and eight bytes of garbage — to 1×1.
The tests now detect that and say which half of it they are in, rather
than asserting a falsehood; the image path is disclosed as unverified here
(`TD-100`) instead of quietly claimed.

**`RenderTargetBitmap.Save` writes zero bytes headlessly.** The helper
generating a "PNG" for those tests was producing an empty array, so every
assertion about decoding it was an assertion about nothing. Replaced with
a literal, byte-for-byte valid PNG.

> **The transferable lesson.** When a test fails against code you believe
> is right, the answer is a probe, not an adjusted assertion. Twice here
> the test harness was wrong in a way that would have silently weakened
> the suite if the assertion had simply been relaxed to match.

## What we did not do, and said so

- **No markup, annotation or rotation** (`TD-98`). `TD-80`'s own text
  lists them; this scope did not include them. They need an annotation
  model and a decision about whether an annotation is an engineering
  object. `TD-80` is closed against what was built, and `TD-98` exists so
  that what was not built is not absorbed into that closure.
- **No DWG or SVG** (`TD-99`) — both report `Unsupported`, honestly.
- **Image decoding unverified by the automated suite** (`TD-100`).
- **No tiled rendering** (`TD-101`) — bounded by a hard cap on the raster
  edge, so deep zoom degrades in sharpness rather than availability.

## Related

`ADR-0115` · `ADR-0114` (the durable content this reads) ·
`ADR-0095` (the layout tree this docks into) ·
`37-attachment-content-storage.md` · `36-workspace-layout-and-docking.md`
