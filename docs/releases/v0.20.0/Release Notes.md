# TempestOS v0.20.0 — Release Notes

**Status: in preparation on `release/v0.20.0`, cut from the `v0.19.1`
candidate (`4f5ee83a`) on 2026-09-15 at the Product Owner's instruction
to close the P1–P3 technical debt before the release rather than after
it.** Nothing in this document is certification. `v0.19.1` stays as
pushed and gated as the fallback candidate.

## Summary

**v0.20.0 is the debt tranche** — every item the technical-debt
rationalisation of 2026-09-14 rated P1 to P3 that a gate can prove,
plus the two Product Owner definitions of 2026-09-15 (payment terms per
client; a calculation is a task from creation) and the draft ADR for
tear-out and dock everywhere. No new surface beyond what those closures
require.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.1C1` Attachments: content-addressed storage and streamed reads (`TD-95`, `TD-96`) | `AttachmentContentStore` keys bytes by SHA-256 content hash with a reference count, so two attachments of the same file share one BLOB and deleting one keeps the other's content; a legacy, attachment-Id-keyed row migrates on first read; no SQL schema change. `IBinaryPersistenceStore.OpenReadAsync` opens a seekable `Stream` over one BLOB through `Microsoft.Data.Sqlite`'s `SqliteBlob` (real incremental blob I/O); `IAttachmentContentStore.OpenReadAsync` verifies size and hash over a bounded-memory pass before handing back a second stream. The Document Viewer's read path (`DocumentPageSourceFactory.CreateFromStream`; `AttachmentViewerLauncher`) reads a large drawing through it instead of materialising the whole file; falls back to the byte-array path for a format the streamed sources do not (yet) cover |  |

## Figures

*(re-derived at `WP 20.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.20.0/Execution Plan.md`
- `docs/releases/v0.19.1/Release Notes.md`
- `docs/releases/v0.19.1/Product Owner Decisions 2026-09-15.md`
- `docs/releases/v0.19.1/Technical Debt Rationalisation — Part 1.md` and `Part 2.md`
