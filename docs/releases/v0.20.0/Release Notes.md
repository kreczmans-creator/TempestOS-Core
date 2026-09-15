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
| `WP 20.2A` | The object picker (`FCR-0073`): `ObjectPickerDialog`, listing every live object by Kind with a filter box, the current project's own objects first, returning a string exactly like `InputDialog` (blank is "no destination"). Closes `S2-2` — `calculations.move/copy`, `documents.move/copy`, `manufacturing.move/copy`, `mechanical.move/copy`, `verification.move/copy`, `requirements.move`, `requirements.move-group` each gain a real binding collecting their destination through it, and `Ctrl+Shift+M`/`Ctrl+Shift+C` (an object selected in the Project Explorer) resolve the right command Id from the selection's own Kind and invoke through the same canonical path the Palette uses. Closes `TD-115` too — the same picker parameter kind serves `requirements.link`/`requirements.add-to-collection`/`mechanical.compare-baselines` trivially, so all fifteen of the platform's own object-picker-unavailable descriptors are real bindings now, none narrowed. Closes `TD-77` — the Command Palette's empty-query open now lists only what applies (grouped by category, most-recently-invoked first this session), rather than every registered command with most of them disabled. | pending merge into `release/v0.20.0` |

## Figures

*(re-derived at `WP 20.9.0`)*

## Warnings

- *(filled at each merge)*
- `WP 20.2A`: the object picker does not pre-filter a Move's own source object or its descendants from the candidate list (a doomed choice fails cleanly with `CircularParentAssignmentException`'s own message, exactly as the Project Explorer's drag-and-drop reparenting already leaves it — never pre-filtered there either). The Command Palette's new "most recently invoked" ranking is session-only, never persisted (`RecentObjectsState`'s own persistence pattern was judged not worth adding for what a query substring already finds instantly) — it resets on every restart.

## Related

- `docs/releases/v0.20.0/Execution Plan.md`
- `docs/releases/v0.19.1/Release Notes.md`
- `docs/releases/v0.19.1/Product Owner Decisions 2026-09-15.md`
- `docs/releases/v0.19.1/Technical Debt Rationalisation — Part 1.md` and `Part 2.md`
