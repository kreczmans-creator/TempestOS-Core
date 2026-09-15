# TempestOS v0.21.0 — Execution Plan: the recovery tranche

**Branch:** `release/v0.21.0`, cut from `release/v0.20.0` at `88311649`
(the v0.20.0 candidate with the design system in the repository) on
2026-09-15, at the Product Owner's instruction to close every technical
weakness named in the lead's assessment of that afternoon. `VERSION`
`0.21.0`. The seven `WP 20.10A`–`20.10G` packages answering the Product
Owner's test findings land on `release/v0.20.0` first and are merged
into this branch as they arrive; wave 2 below bases on them.

## 1. What the Product Owner gets

Every seam the assessment called fragile made sound, every Core model
with no surface given one, the operations gaps closed, and the quality
signals raised — so that what follows is the v1.0.0 release candidate
rather than another recovery.

## 2. The weaknesses and the packages that close them

| Weakness (assessment, 2026-09-15) | Package | Wave | Effort (days) | Depends on |
|---|---|---|---|---|
| Docking and windows: the layout model cannot express floating or tabbed panels (`TD-91`), focus after re-render (`TD-90`), keyboard tab reorder residual (`TD-133`), a floating window the model does not know | `WP 21.0A` the layout forest, one controller, one persisted document; `WP 21.0B` torn-out windows and cross-window drag; `WP 21.0C` areas as dockable units, monitor-aware restore, focus, keyboard — all to `ADR-0153` | 2 | 7 + 8 + 5 | `WP 20.10D`, the Product Owner's review of `ADR-0153` |
| Headless tests pass where the real shell fails (T4, T6) | `WP 21.5C` a real-shell run in CI: the built application launched on the Windows runner and driven through a smoke journey by UI Automation, mouse only | 3 | 3 | `WP 21.0C` |
| Undo covers Rename and Favourite only | `WP 21.1A` Undo across Create, Delete, Move, Copy, status changes and field edits — compensating commands recorded on the history, `Ctrl+Z`/`Ctrl+Y` through the one canonical path | 2 | 6 | `WP 20.10C` |
| `ObjectEditorView` at about 2,600 lines with a section per Kind | `WP 21.1B` the editor split into a shell and per-Kind section builders behind one contract, behaviour identical, the layout walk unchanged | 2 | 3 | `WP 20.10A`, `20.10F` |
| Two renderers only; no invoice document, no reports, nothing from the other templates | `WP 21.2A` documents from the templates: invoice, technical report, progress report, drawing register, purchase order, timesheet — each through the shared template `WP 20.10G` builds | 2 | 6 | `WP 20.10G` |
| Engineering Assets built in Core with no surface (`TD-165`, `TD-160`) | `WP 21.2B` the Engineering Assets surfaces: the bracket verification artefact filled in from the Desktop, the merged capability's own area — included on the Product Owner's "close all of those"; their decision at the RC stands if they withdraw it | 2 | 5 | — |
| The calculation engine has no result bound and retains no inputs (`TD-22`, `TD-29`); the result interfaces have no implementation (`TD-30`) | `WP 21.3A` typed, bounded results; inputs retained on the record so a calculation re-runs with changed parameters and compares against its last run | 1 | 5 | — |
| Commercial edges: no expenses, no purchase orders or bills raised inside, no VAT on lines, one principal at a time | `WP 21.3B` expenses against a project; purchase orders (the design system has the template); VAT on quotation and invoice lines; a second principal signs in to check | 2 | 6 | `WP 20.10A`, `20.10E` |
| The viewer: no SVG, no markup, full-page rasterisation, DWG external | `WP 21.4A` SVG page source (`Svg.Skia`, MIT — the licence stated in the notes), markup and annotation (`TD-98`), tiled rendering (`TD-101`) | 1 | 5 | — |
| No installer, upgrade, backup or restore | `WP 21.5A` the programme's `RC.0A` brought forward: a Velopack-packaged Windows installer with in-place update from the GitHub Release feed, the database backed up beside itself before migration, a `--persistence-root` argument and a first-run data-location dialog, backup and restore from Settings, the support matrix | 1 | 5 | — |
| Startup linear in object count (`TD-88`) | `WP 21.5B` lazy, project-scoped materialisation over the index `WP 20.1C2` built, the seventy list-result callers moved to the index, behaviour proven identical | 1 | 3 | — |
| Mutation score 67.58 % against a 70 % break | `WP 21.5D` the surviving mutants in Core answered with assertions, the threshold met | 3 | 3 | everything else merged |
| Live Xero never authorised | `WP 21.6` the first live authorisation against the Product Owner's organisation, with the lead on hand; any mismatch fixed | 3 | 0.5 + the Product Owner | — |
| Release | `WP 21.9.0` notes, figures, `PHYSICAL_REVIEW.md` §7e, three green CI runs, the candidate page | 3 | 1 | all |

Effort: about 71.5 developer-days across sixteen packages.

## 3. Waves

- **Wave 1, started 2026-09-15 15:50 beside the seven `20.10` packages:** `21.3A`, `21.4A`, `21.5A`, `21.5B` — none touches a file a `20.10` package owns.
- **Wave 2, as the `20.10` packages merge:** `21.1A` after `20.10C`; `21.1B` after `20.10A` and `20.10F`; `21.2A` after `20.10G`; `21.3B` after `20.10A` and `20.10E`; `21.2B` any time; `21.0A`→`21.0B`→`21.0C` after `20.10D` and the ADR review.
- **Wave 3:** `21.5C` after `21.0C`; `21.5D` last; `21.6` when the Product Owner is at the computer; `21.9.0`.

## 4. Method

As for `v0.20.0`: one worktree per package on `wp/<id>` off this
branch, Sonnet throughout, merged `--no-ff` after its own gate with
targeted tests at each merge, the union of both packages' suites when
two touch one subsystem, commit and push gated on the build and the
console test line, one full gate at the tranche head, three green CI
Gate runs, the candidate page. `release/v0.20.0` stays the candidate
under the Product Owner's test until this branch is green; each `20.10`
merge there is merged here the same hour.
