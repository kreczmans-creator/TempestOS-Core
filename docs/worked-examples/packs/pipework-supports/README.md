# Calc pack — Pipework supports (`pack.pipework-supports`)

**Chain.** Pipe expansion and anchor force (`bs.pipe-expansion`) → Anchor post frame; Anchor post frame (`struct.frame-2d`) → Anchor post base plate.

**Purpose.** Takes the force a heated pipe puts on its anchor from the pipe-expansion step to the steel post that holds the anchor and the post's base plate, and files the anchorage design done elsewhere as Evidence. The links assume the post frame's numbering: node 1 the column base (fixed), node 2 the anchor, member 1 the post, the first load row the thermal force along the pipe; the weight in the second row and the load factors are the user's. The pipe step is a screening: a line that fails it needs a formal stress analysis, and its moment is not carried to the post. The holding-down bolt data are typed (no bolt-check step).

A 219.1 × 6.3 mm steel line anchored 60 m apart, 10 to 150 °C, absorbed by a 9.5 m offset leg: the line grows 100.8 mm, the leg needs 9.33 m at an INVENTED allowable range of 160 MPa (u 0.98) and puts 7.07 kN on its anchor (the 4.4 N along the leg from the run is negligible); the screening ratio, 292 mm²/m², is inside an INVENTED limit of 400. That anchor force is the first load row (a table cell) of a 2.5 m cantilever post (a 203 × 203 UC); with the 6 kN weight, the ULS combination takes 1.5 × 7.07 = 10.6 kN along the pipe at the top and 8.1 kN down, which is 26.5 kN·m, 8.1 kN and 10.6 kN at the base: the base plate's actions (the pipe step's moment is NOT carried to the post: it is the guided-cantilever root moment, not an applied one). The 350 × 300 × 25 base plate on C25/30 passes with 56 kN in each bolt. **The anchorage (concrete and anchor design) is done in Hilti PROFIS: the file attached to the base-plate step is a PLACEHOLDER, not a PROFIS report, to be replaced by the engineer's own report before the pack is used.** **The links depend on the frame's numbering: node 1 the column base (fixed), node 2 the anchor; member 1 the post; the first load row the thermal force.** The holding-down bolt data (M20 8.8 in a 25 mm plate) are typed because this chain has no bolt-check step; a test guards them against the connection step. The pipe step is a screening, not a stress analysis: its allowable range and screening limit are the owner's records and the example's are invented, not code values.

**Issue.** Project “Pipe rack 4 anchor A2”, pack “Pipe anchor post calc pack”, checked once as a bundle and issued under issue reference **`PIP-SUP-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-PIP-11 and DRG-PIP-610/611 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Pipe expansion and anchor force | `bs.pipe-expansion` | A | 3 of 3 PASS |
| Anchor post frame | `struct.frame-2d` | A | none |
| Anchor post base plate | `struct.base-plate` | A | 6 of 6 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Pipe expansion and anchor force | `anchor_force` | Anchor post frame | `loads` row 1 (`F_x`), a table-cell link | 7.06946 kN |
| Anchor post frame | `R1_y` | Anchor post base plate | `N_Ed` | 8.1 kN |
| Anchor post frame | `R1_m_abs` | Anchor post base plate | `M_Ed` | 26.5105 kN*m |
| Anchor post frame | `R1_x_abs` | Anchor post base plate | `V_Ed` | 10.6042 kN |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

The base-plate step cites `PROFIS-BP1-PLACEHOLDER.txt`, filed against its Evidence; that file only says it is a placeholder, and the base plate's anchorage condition reads “external report attached: PROFIS-BP1-PLACEHOLDER.txt” so the issued sheet names what was attached. Its PASS in the base-plate step therefore records that a file is attached, not that the anchorage has been designed.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `b8-bs-pipe-expansion.sheet.txt` — Pipe expansion and anchor force, the issued sheet.
- `s2-struct-frame-2d.sheet.txt` — Anchor post frame, the issued sheet.
- `s6-struct-base-plate.sheet.txt` — Anchor post base plate, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
