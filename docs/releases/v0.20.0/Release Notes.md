# TempestOS v0.20.0 — Release Notes

**Status: in preparation on `release/v0.20.0`.** Nothing in this document
is certification; each Work Package row below is verified only to the
gate figures its own commits show.

## Summary

v0.20.0 continues the technical-debt and Product Owner decisions raised
against the `v0.19.1` candidate. This document is written incrementally
by each Work Package as it lands — figures and warnings accumulate across
packages, and the lead reconciles this file where two packages' own
commits touch it concurrently.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.2B` The viewer: DWG opens externally, SVG stopped, rotation | `ViewableDocumentFormat` gains `ExternalOnly`; `DocumentFormatDetector` recognises `.dwg`/`.dxf` by extension (case-insensitive, checked before content type) and by the content types CAD tools commonly send, since neither format has an IANA-registered one; `AttachmentViewerLauncher` materialises the real bytes to a file under the OS temp folder (never beside the persistence root) whenever no in-app source exists, and `DocumentViewerView` gains an Open externally button (`Process.Start`/`UseShellExecute` via an injectable, test-overridable launcher) offered for `ExternalOnly` and for any plain `Unsupported` format alike, with `ExternalOnly`'s own honest headline; Rotate left/Rotate right (90° steps, automation-named), applied at render only — the page source is never asked to rotate anything, proven by a recording `IDocumentPageSource` — remembered for as long as a tab shows its document, reset when a different document opens into the same tab. SVG (Product Owner decision 2026-09-15 §5's "small" half) was stopped, not shipped: no SVG rasteriser (`Svg.Skia`, `SkiaSharp.Extended`, `Avalonia.Svg.Skia`) is referenced anywhere in this solution, and the brief's own fallback for that case was to add no new NuGet package and report rather than implement; `TD-99`'s row is narrowed to the SVG half only, not closed. Markup and annotation stay out, `ADR-0115`'s own disclosed scope cut. | (pending) |

## Figures

(pending — re-derived once the release candidate is cut)

## Gate

- `WP 20.2B`: Debug and Release build clean under `TreatWarningsAsErrors` (0 warnings, 0 errors); `Tempest.Core.Tests` 4,462 passed / 0 failed (Debug); `Tempest.Desktop.Tests` and the Release configuration are recorded in that Work Package's own report.

## Warnings

- **SVG still reports `Unsupported`** (`WP 20.2B`): the Product Owner's 2026-09-15 decision §5 expected SVG to be a small in-app addition, but no SVG rasteriser is referenced anywhere in this solution (`Svg.Skia`, `SkiaSharp.Extended` and `Avalonia.Svg.Skia` were all checked and are all absent), and the brief's own instruction was to add none and report rather than implement. `TD-99` stays in the Live Backlog for this half.
- **Rotation is render-only; the viewport's own fit/zoom maths does not know about it** (`WP 20.2B`): a rotated page's displayed box is swapped to match the turned bitmap, but "Fit" does not re-derive a new fit zoom for the now-landscape (or now-portrait) shape — a disclosed, deliberately minimal scope cut, not a defect found later.

## Related

- `docs/releases/v0.19.1/Release Notes.md`
- `docs/releases/v0.19.1/Product Owner Decisions 2026-09-15.md`
