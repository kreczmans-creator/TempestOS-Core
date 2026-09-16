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
| No security audit since the v0.5.0 baseline (2026-07-28); no dependency scan in CI; new surfaces unread (connectors, loopback listener, Open externally, macros, import, tonight's SVG parser and installer) | `WP 21.5E` the programme's `RC.0C` brought forward: a review of every surface added since the baseline with RED findings fixed and AMBER filed, the security posture statement, `dotnet list package --vulnerable` as a required CI check, Dependabot, the third-party notices | 1 | 3 | — |
| No adversarial testing has ever been run: no one has actively tried to break the parsers, the Open-externally path, the OAuth loopback, import, macros or the release pipeline | `WP 21.5F` a full offensive audit — break every local surface with proof-of-concept exploits, then fully close, strengthen and solidify every finding in this tranche (nothing filed and left); actions SHA-pinned, permissions least-privilege | 1 | 5 | — |
| Three services write durable data with no audit row; the current principal can be set by any component; the audit collection is writable by anything holding the store (offensive audit OSA-12/13/14/15, deferred by `WP 21.5F` as architectural) | `WP 21.6A` audit rows inside the existing transactions of `RequirementsService`, `VerificationService` and `ReferenceDataCatalog`; the principal settable only through the sign-in seam; the audit collection writable only by the audit writer — fixed, not filed, on the Product Owner's rule | 2 | 3 | `WP 21.3B` (the sign-in seam) |
| The engineering calculation modules: five definitions, no catalogue-driven surface (Product Owner, 2026-09-15: "like RoyMech's online calculators … no code") | `WP 21.7A` eleven calculation modules with specs in a teaching register, worked-example vectors, descriptors per input, materials through the catalogue (the Product Owner's second session, `tempestos-4c`); `WP 21.7B` the dynamic calculator surface generated from the descriptors, catalogue by category, live results with the working and the method reference (same session) | 2 | 5 + 4 | `WP 21.3A` (the engine) |
| The calculators' own gaps: one material per module, fastener and bearing grades typed rather than picked, no Re-run/Compare on the surface, no form-less entry point | `WP 21.7C` calculator completeness — a picker per material-bearing input, fastener and bearing records from the reference libraries, Re-run and Compare on the result pane through the 21.3A commands, a governed per-module service, the calculators' §7 script (second session) | 2 | 3 | `WP 21.7B` |
| Live Xero never authorised | `WP 21.6` the first live authorisation against the Product Owner's organisation, with the lead on hand; any mismatch fixed | 3 | 0.5 + the Product Owner | — |
| Release | `WP 21.9.0` notes, figures, `PHYSICAL_REVIEW.md` §7e, three green CI runs, the candidate page | 3 | 1 | all |

Effort: about 94.5 developer-days across twenty-two packages (`21.6A`, `21.7A` and `21.7B` added on 2026-09-15 evening: the first from the offensive audit's deferred findings, the two calculation packages from the Product Owner's instruction to build the engineering modules in a second session beside this tranche).

## 3. Waves

- **Wave 1, started 2026-09-15 15:50 beside the seven `20.10` packages:** `21.3A`, `21.4A`, `21.5A`, `21.5B`, `21.5E` and `21.5F` (added 16:20–16:40 on the Product Owner's security and offensive-audit instructions) — none touches a file a `20.10` package owns; `21.5F` is the red team to `21.5E`'s defensive review, owning the exploit report and the CI pinning while `21.5E` owns the posture statement and the vulnerability scan.
- **Wave 2, as the `20.10` packages merge:** `21.1A` after `20.10C`; `21.1B` after `20.10A` and `20.10F`; `21.2A` after `20.10G`; `21.3B` after `20.10A` and `20.10E`; `21.2B` any time; `21.0A`→`21.0B`→`21.0C` after `20.10D` and the ADR review.
- **Wave 2, added:** `21.6A` after `21.3B`; `21.7A` (second session) after `21.3A`, then `21.7B` on it.
- **Wave 3:** `21.5C` after `21.0C`; `21.5D` last; `21.6` when the Product Owner is at the computer; `21.9.0`.
- **Interruption, 19:40–20:10:** the session's usage limit terminated six wave-2 agents mid-task; all six were resumed with their context after the reset and told to checkpoint after every scope item.

## 4. Method

As for `v0.20.0`: one worktree per package on `wp/<id>` off this
branch, Sonnet throughout, merged `--no-ff` after its own gate with
targeted tests at each merge, the union of both packages' suites when
two touch one subsystem, commit and push gated on the build and the
console test line, one full gate at the tranche head, three green CI
Gate runs, the candidate page. `release/v0.20.0` stays the candidate
under the Product Owner's test until this branch is green; each `20.10`
merge there is merged here the same hour.

## 5. Outcome (`WP 21.9.0`, 2026-09-15 late evening)

Seventeen of the twenty-two packages merged in one day, 77 of the 94.5
planned days (81 %), every one behind its own gate and the lead's
reconciliation at merge; `WP 21.9.0` (1 day) is this release package.
The four not started are all gated on the Product Owner — `WP 21.0B`
and `WP 21.0C` (13 days, docking steps 3–4) on the `ADR-0153` review,
`WP 21.5C` (3 days) on `21.0C`, `WP 21.6` (0.5 day) on the Product Owner
at the keyboard for the first live Xero authorisation — 16.5 days (17 %).

| Merged, in order | Commit |
|---|---|
| the seven `WP 20.10` fixes from `release/v0.20.0` | `122a34d0` … `51641133` |
| `WP 21.5E` security posture, dependency scan | `728556e3` |
| `WP 21.5A` installer, backup and restore | `3a88de8c` |
| `WP 21.3A` typed intermediates, re-run, compare | `81e1cdfa` |
| `WP 21.5F` the offensive audit, ten findings fixed | `967eb536` |
| `WP 21.5B` lazy rehydration, the index idiom | `581496ac` |
| `WP 21.4A` the viewer's remaining formats and markup | `a431eb5a` |
| `WP 21.2B` the Engineering Assets surfaces | `124d601b` |
| `WP 21.7A` eleven calculation modules | `876ca187` |
| `WP 21.2A` documents from the templates | `977610ab` |
| `WP 21.1A` Undo across commands | `714f86bf` |
| `WP 21.7B` the Engineering Calculators surface | `62c6cbb8` |
| `WP 21.3B` the commercial edges | `8370381d` |
| `WP 21.1B` the object editor split | `60824923` (+ `4268c21c`, the lead's compile fix) |
| `WP 21.0A` docking steps 1–2 | `2bfaa036` |
| `WP 21.7C` the Engineering Calculators completed | `76b90c77` |
| `WP 21.6A` the audit residuals fixed, Requirements undo | `4c393842` |
| `WP 21.5D` the mutation threshold met | `b664fea6` |

Two lead-only commits on the way: `16dbabd6` (the dependency-scan
script's wrapped `Write-Error`, the one cause of every red core leg
between the `21.5E` merge and it) and `904b0f81` (the OAuth round-trip
tests off the default port). Everything the packages and the merges
disclosed is in the Release Notes' Warnings.

## 6. The overnight acceptance campaign (2026-09-15 23:11 → 2026-09-16, on `claude/tempestos-v1-final-acceptance-19hka8`)

Not a new tranche: the candidate at `a4ab1915` taken through the remaining
technically actionable work, as lead with four package agents in their
own worktrees (`wp/21.0K`, `wp/21.5C-linux`, `wp/21.9.1-docs`,
`wp/21.9.1-quality`), each merged `--no-ff` after its own gate, the full
gate re-run on the final head, and the morning package written
(`PRODUCT_OWNER_ACCEPTANCE.md`, `OVERNIGHT_FINAL_ACCEPTANCE_REPORT.md`).
Scope discipline: nothing deliberately deferred was reopened — docking
steps 3–4 stay gated on the `ADR-0153` review, the live Xero sign-in and
the installer run stay with the Product Owner.

| Package | What | Merge |
|---|---|---|
| the two unmerged branches | `cancel-in-progress` off for `release/*`; the Academy chapters | `3d256792`, `fb0c8f8a` |
| `FileSecretStore` (lead) | the `0700`/`0600` promise for an existing directory or file, found by the store's own test on Linux | `69668268` |
| `WP 21.6P` (lead) | the Xero authorisation path a user can actually take — three defects found by reading the `WP 21.6` path against the code; the `WP 21.6` script is `PHYSICAL_REVIEW.md` §7k | `709d01a4` |
| `WP 21.9.1` quality | the release-quality evidence in one place; backup/restore and restart persistence driven in the real application | `2a2d1a33` |
| `WP 21.9.1` docs | permanent documentation aligned with the product, provenance kept | `aa91273c` |
| `WP 21.0K` | `ADR-0153` decision 8 on keys, keyboard gestures through the controller with visible focus, the floating-window close defect fixed; K1/K2/K3/K6 on a real screen | `5c170f34` |
| `WP 21.5C` (Linux) | the real-shell acceptance journey on Linux/Xvfb — a non-shipped runner drives the real application with genuine X11 input through 35 steps and an 8-step relaunch verify; one major defect fixed (a picker under its prompt), four reported, three of those fixed by the lead the same night | `3ba0709f`, fixes `37264671` |
| release documents (lead) | backlog audit (7 of 30), `PHYSICAL_REVIEW.md` §7j corrected and §7k added, `D-028` addendum, release notes, this section, `PROJECT_STATUS.md` | the final commits |

Owed after tonight, all on the Product Owner: `WP 21.0B`/`21.0C` after the
`ADR-0153` review; the live Xero sign-in (`WP 21.6`, §7k); the first
`Setup.exe` from the pipeline (a tag, or `package-installer.ps1` on
Windows); K4/K5 on two monitors and the save-on-close half of docking
persistence on Windows; the Windows UI-Automation form of `WP 21.5C`.
