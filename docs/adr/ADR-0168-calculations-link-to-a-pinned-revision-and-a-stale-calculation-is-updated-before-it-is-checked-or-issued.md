# ADR-0168: Calculations Link to a Pinned Revision, and a Stale Calculation Is Updated Before It Is Checked or Issued

## Status

Accepted (v0.26.0, Addendum A, L1). Extends ADR-0165 (Evidence governs) and ADR-0166 (a checked calculation is never a dead end).

## Context

The addendum's twelve calculators feed one another (motor torque into a shaft, shaft reactions into bearings, a cable into its fault protection). v0.26.0 had a first form of linking: an input whose provenance was `reference: calc:<id>` named another instance, and a changed upstream outputs hash made it stale before issue. It did not say *which output* fed the input, did not check units, did not see a revision as a change (a superseded revision keeps its outputs), allowed a stale calculation to be checked, and stranded a checked one whose upstream moved on.

## Decision

1. **A link is a provenance kind.** `CalcProvenanceKind.Link`, reference `calc:<instance id>#<output symbol>`, revision = the upstream revision. An instance is one revision, so the link is pinned to it, never to "latest".
2. **Output ports.** A template may declare `OutputPorts` (symbol, unit, description); declared ports enter the content hash. Templates that declare none offer the outputs a default run produces.
3. **Binding is checked.** `BindAsync` refuses a different dimension, a link to itself, a cycle and a chain longer than 20 links; the value is copied into the input's own unit.
4. **Stale is transitive.** A linked input is stale when its upstream is superseded, no longer gives the output, gives a different value, or is itself stale. A stale calculation cannot be checked, issued, or checked or issued as a pack member. Changes publish `CalcStalenessChanged` on the event bus with every affected downstream calculation.
5. **Update, never strand.** `UpdateLinksAsync` ("Update to revision n") re-pins every link to the current upstream revision and recalculates: a draft in place; a checked or issued calculation as a new revision (the old one superseded), so it is checked and issued again.
6. **Pack templates.** A saved chain (steps, links) instantiates a linked set of calculations in one pack; the pack is checked once and issued as one Evidence bundle (v0.26.0 packs). A chain built by hand can be saved as a template.

## Consequences

Imported files may state links; a forged link that does not match its upstream is simply stale and cannot be checked. Staleness reads the project's instances, so it costs one listing per question; at the 20-link cap and today's project sizes that is well inside the form's budgets.

## Amendment (2026-10-24, after the Opus review of Addendum A)

Decision 3's checks now run on **every path that can set a link**, not only `BindAsync`. That covers `UpdateInputsAsync` (the form's provenance field and CSV/JSON imports) and `UpdateLinksAsync` (re-pinning to a newer revision). One validator applies them all: the upstream exists, gives the output in the input's dimension, closes no cycle through any revision of either calculation, and keeps the chain within 20 links. A temperature difference (K) and an absolute temperature (°C/°F) are also refused, instead of being converted with a 273.15 offset. A forged looping link in an import is therefore refused when it is entered, not merely left stale. The test `Forged_Links_From_An_Import_That_Form_A_Cycle_Finish_And_Cannot_Be_Checked` is replaced by `Forged_Links_That_Would_Loop_Are_Refused_On_Entry` to match.

## Amendment 2 (2026-10-24, wet-heating chain) — table-cell links

A calculation takes some of its heat, load or force from a table (B6's `rooms` table holds each room's heat in its `Phi` column), and a top-level input link cannot reach a cell. A **number or quantity cell of a table input** can now be bound to an output of another calculation.

1. **The cell is the link's home.** `CalcCell` gains an optional `Provenance`; a cell whose provenance is a `Link` is a cell link, read back by `CalcLink.From`. A cell without provenance serialises exactly as before (null is not written), so existing inputs, stored instances and goldens are unchanged. The link travels with its row if rows are reordered or removed.
2. **Reading them.** `CalcInstance.CellLinks` lists `(table symbol, zero-based row, column key, link)`; `UpstreamIds` includes them and `HasLinks` is true for either kind. `Links` still lists top-level inputs only, so existing callers are unaffected.
3. **Binding.** `BindCellAsync(instance, table, row, column, upstream, output)` is draft-only, needs an existing table input and a Quantity or Number column and an existing row (a link never creates rows), makes the same refusals as `BindAsync` (own output, superseded, other project, no such output, different dimension, temperature difference against absolute temperature, cycle, depth), and sets the cell to the output converted to the column's default unit.
4. **Every path.** Staleness (direct and transitive), the `CalcStalenessChanged` event, cycle and depth checks, `UpdateLinksAsync` (re-pins the cell and recalculates it in the column's unit; a checked or issued calculation becomes a new revision), `CreateAsync` and `UpdateInputsAsync` validation (a forged cell link to a missing calculation or output, another dimension or a text column is refused) and provenance length (on create and import as on update, for inputs and cells) all treat cell links as they treat input links. Staleness judges many links to one upstream together, so 200 cells of one table cost one walk of that upstream. A cell link is named on the sheet under its row (`Phi: link: Room A rev A · Phi_HL`) and in staleness reasons as `rooms[1].Phi`.
5. **Record-pinned tables are not linked into.** A table whose own provenance is a reference-record pin takes its values from the record, which only the record can change; `BindCellAsync` and the create/update validation refuse a cell link inside it ("…is pinned to a reference record…"). Entering the table by hand drops the pin.
6. **No overwrite between read and write.** `BindAsync`, `BindCellAsync` and `UpdateLinksAsync` build the new inputs from their own read; they now hand that read's `UpdatedAt` to `UpdateInputsAsync(…, expectedUpdatedAt)`, which refuses ("was changed after these inputs were read") instead of overwriting an edit made in between.
7. **Pack templates.** `CalcPackBinding` gains optional `Row` and `Column`; when both are set the binding targets a cell. The step must be given the rows as inputs, because instantiating never creates them; `PackTemplateFromChainAsync` saves cell links with their row and column.

A CSV export of inputs does not carry cell provenance; a table re-imported from CSV is typed values until its cells are bound again. The export lock is unchanged: whole tables pinned to licensed records are withheld.
