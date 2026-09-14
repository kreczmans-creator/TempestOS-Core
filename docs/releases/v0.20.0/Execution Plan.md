# TempestOS v0.20.0 — Execution Plan

**Opened 2026-09-15 at 00:10 on `release/v0.20.0`, cut from the
`v0.19.1` candidate (`4f5ee83a`).** The Product Owner's instruction in
the early hours: go straight into the next tranche and close every
technical-debt item the rationalisation rated P1 to P3 that can be
closed with a gate, leaving P4s and the two P0 features (tear-out and
dock everywhere; document templates) for a decision with the
application open. `v0.19.1` stays as pushed and gated as the fallback
candidate; `v0.20.0` is the candidate if it lands green.

## 1. What the Product Owner gets

The debt the audit could see, closed: requirements writes reach the
change bus and business identifiers are unique within a project;
payment terms per client and a calculation as a task from creation
(the Product Owner's own definitions of 2026-09-15); SVG in the viewer,
DWG opened externally and said so, rotation; issuing evidence as one
transaction, export files that migrate across schema versions, BOM
units as a vocabulary; the object picker that makes Copy real and Move
reachable by keyboard and the Palette, and a Palette that lists what
applies; macros over real commands; streamed attachment reads and
content-addressed storage; lazy, project-scoped rehydration; seven
small hygiene rows; CI in shards; and the docking ADR drafted for
review.

## 2. Waves

| Wave | Work Package | Depends on | Effort (days) | Model | Owns |
|---|---|---|---|---|---|
| 1 | `WP 20.1B` Commercial data model — payment terms per client (TD-180), a calculation is a task from creation (TD-181) | decisions §1–2 | 2.5 | Sonnet | `Organisation` terms, `InvoiceRequest` terms and due, `TaskEquations`, `TasksSnapshot`, the Calculation Kind's completion, Engineering → Tasks |
| 1 | `WP 20.1A1` Every Requirements write reaches the change bus (TD-28) | — | 2 | Sonnet | `RequirementsService`, the change publisher seam |
| 1 | `WP 20.1A2` A business identifier is unique within its project (TD-38) | — | 2 | Sonnet | `EngineeringObjectFactory`, an identifier index, the rename path |
| 1 | `WP 20.2B` The viewer — SVG in-app, DWG opened externally, rotation (TD-99, TD-98 in part) | decision §5 | 1.5 | Sonnet | viewing formats, page sources, `DocumentViewerView` |
| 1 | `WP 20.3A` Persistence hygiene — issue in one transaction (B2), export schema migrations (ADR-0051), BOM units (ADR-0083) | — | 2 | Sonnet | `IssueEvidenceCommand`, `ExportImport`, the BOM unit |
| 1 | `WP 20.0A` (design) ADR-0153 tear-out and dock everywhere — draft for review | — | 0.5 | Sonnet | `docs/adr/ADR-0153`, a design note |
| 1 | `WP 20.3B` Seven small P3 closures (TD-20, 18, 21, 03, 05, 84, 170) | — | 1.5 | Sonnet | per row |
| 1 | `WP 20.3D` CI in shards; the ceiling back to 45 minutes (B6) | — | 0.5 | Sonnet | `ci.yml` |
| 2 | `WP 20.2A` The object picker — Copy real, Move by keyboard and Palette; the Palette lists what applies (S2-2, TD-77) | 19.10R | 3 | Sonnet | `ObjectPickerDialog`, `WorkspaceCommandBindings`, `CommandPaletteOverlay` listing |
| 2 | `WP 20.2C` Macros over real commands; unregister (ADR-0099) | — | 1.5 | Sonnet | `ICommandRegistry.Unregister`, the Macros files |
| 2 | `WP 20.1C1` Attachments — streamed reads (TD-96), content-addressed storage (TD-95) | — | 2 | Sonnet | `IBinaryPersistenceStore`, `AttachmentContentStore`, the viewer read path |
| 2 | `WP 20.1C2` Lazy, project-scoped rehydration with the same behaviour (TD-88) | 20.1A2 | 2 | Sonnet | `EngineeringObjectRehydrationService`, the repository's materialisation |
| 3 | `WP 20.9.0` Release — notes, figures, `PHYSICAL_REVIEW.md` §7d if any step changes, three green CI runs, the candidate page | all | 1 | lead | — |

Effort: 22 days. Deferred to the morning with the application open:
the P4 rows; Undo across commands (L); the calculation engine's typed
results and retained inputs (TD-22, TD-29); TD-101 tiled rendering;
TD-165 / TD-160 the Engineering Assets surfaces; the two P0 features
after their ADRs.

## 3. Method

As for `v0.19.1`: one worktree per package on a throwaway branch off
the candidate head, merged `--no-ff` into `release/v0.20.0` after its
own gate, targeted tests at each merge, one full gate at the tranche
head, three green CI Gate runs, the candidate page. The Product Owner
is asleep; every decision the packages needed was taken from
`docs/releases/v0.19.1/Product Owner Decisions 2026-09-15.md` or
disclosed as the lead's default in the brief.
