# Calc pack — Steel support (`pack.steel-support`)

**Chain.** Load combinations (`struct.load-combinations`) → Support frame; Support frame (`struct.frame-2d`) → Column check, Arm joint bolt group, Arm connection bolts, Column base plate; Arm joint bolt group (`mech.bolt-group`) → Arm connection bolts; Holding-down bolts (`struct.connection-ec3`) → Column base plate.

**Purpose.** Combines the actions on an equipment support, analyses the steel frame, checks the column, checks the bolts of the bracket's joint, designs the column base plate and files the anchorage design done elsewhere as Evidence. What it does not do: the arm joint is checked at the bolts only (shear, bearing on the thinner ply, tension, combined and block tearing), not the end plate in bending with prying, the weld, the column flange in bending or the column web; the bolt group must be symmetric with the load concentric, because the per-bolt shear and tension come from the governing bolt; the column is checked in its plane only, with no lateral-torsional buckling check (the column step warns) and the minor-axis moment typed 0; the bolt group's bolt resistances are typed copies of the arm connection step's (change them together; a test guards them); the anchorage is not assessed unless the report's governing utilisation is entered in the base plate step, and the base plate's utilisation excludes it; and nothing checks the frame's numbering below. The links assume the frame's numbering: nodes 1 (column base, fixed), 2 (column top), 3 (arm tip); member 1 the column, member 2 the arm; the first load row the vertical load at node 3 (filled from the load combinations), the second the horizontal design action at node 2. Signs: the column check takes the axial force tension positive (S2's N_e1), the base plate takes it compression positive (S2's R1_y). One ULS case is analysed (every action unfavourable); the favourable-permanent case is not.

The equipment's dead (12 kN) and maintenance (8 kN) loads, both downward, are combined (EN 1990 6.10: 28.2 kN); that ULS effect is a cell of the frame's load table; the frame's column forces check the column; the arm's end shear and moment load a six-bolt group whose governing-bolt shear and tension are the arm connection's design forces (S4); the frame's base reactions are the base plate's actions; and a second S4 run for the 25 mm base plate gives S6 its holding-down bolt data. The 5 kN horizontal action at the column top is an ULS design value (combined at factor 1). **The links depend on the frame's numbering: nodes 1 (column base, fixed), 2 (column top) and 3 (arm tip); member 1 the column (1 → 2) and member 2 the arm (2 → 3); the first load row the vertical load at node 3 (filled from the load combinations) and the second the horizontal action at node 2.** The bolt group's bolt resistances (94.0 kN shear, 141.0 kN tension) are typed values equal to what the arm connection step computes for M20 8.8; a test guards the match. The holding-down bolts' own demand fields are zero: S6 checks the anchor bolts against the base reactions. **The anchorage (concrete and anchor design) is done in Hilti PROFIS: the file attached to the base-plate step is a PLACEHOLDER, not a PROFIS report, to be replaced by the engineer's own report before the pack is used.**

**Issue.** Project “Equipment support ES-4”, pack “Equipment support steelwork calc pack”, checked once as a bundle and issued under issue reference **`STR-SUP-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-STR-09 and DRG-STR-310 to 315 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Load combinations | `struct.load-combinations` | A | none |
| Support frame | `struct.frame-2d` | A | none |
| Column check | `struct.en1993-member` | A | 7 of 7 PASS |
| Arm joint bolt group | `mech.bolt-group` | A | 4 of 4 PASS |
| Arm connection bolts | `struct.connection-ec3` | A | 6 of 6 PASS |
| Holding-down bolts | `struct.connection-ec3` | A | 5 of 5 PASS |
| Column base plate | `struct.base-plate` | A | 6 of 6 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Load combinations | `E_d_min` | Support frame | `loads` row 1 (`F_y`), a table-cell link | -28.2 kN |
| Support frame | `N_e1` | Column check | `N_Ed` | -28.2 kN |
| Support frame | `M_abs_e1` | Column check | `M_y_Ed` | 43.2 kN*m |
| Support frame | `V_e1` | Column check | `V_Ed` | 5 kN |
| Support frame | `V_e2` | Arm joint bolt group | `F_y` | 28.2 kN |
| Support frame | `M_abs_e2` | Arm joint bolt group | `M_x` | 28.2 kN*m |
| Arm joint bolt group | `v_max` | Arm connection bolts | `F_v_Ed` | 4.7 kN |
| Arm joint bolt group | `t_max` | Arm connection bolts | `F_t_Ed` | 47 kN |
| Support frame | `V_e2` | Arm connection bolts | `V_Ed` | 28.2 kN |
| Support frame | `R1_y` | Column base plate | `N_Ed` | 28.2 kN |
| Support frame | `R1_m_abs` | Column base plate | `M_Ed` | 43.2 kN*m |
| Support frame | `R1_x_abs` | Column base plate | `V_Ed` | 5 kN |
| Holding-down bolts | `F_t_Rd` | Column base plate | `F_t_Rd` | 141.002 kN |
| Holding-down bolts | `A_s` | Column base plate | `A_s` | 244.794 mm^2 |
| Holding-down bolts | `F_b_Rd` | Column base plate | `F_1_vb_Rd` | 248.485 kN |
| Holding-down bolts | `f_ub` | Column base plate | `f_ub` | 800 MPa |
| Holding-down bolts | `f_yb` | Column base plate | `f_yb` | 640 MPa |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

The base-plate step cites `PROFIS-BP1-PLACEHOLDER.txt`, filed against its Evidence; that file only says it is a placeholder, and the base plate's anchorage condition reads “external report attached: PROFIS-BP1-PLACEHOLDER.txt” so the issued sheet names what was attached. Its PASS in the base-plate step therefore records that a file is attached, not that the anchorage has been designed.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `s5-struct-load-combinations.sheet.txt` — Load combinations, the issued sheet.
- `s2-struct-frame-2d.sheet.txt` — Support frame, the issued sheet.
- `s3-struct-en1993-member.sheet.txt` — Column check, the issued sheet.
- `m3-mech-bolt-group.sheet.txt` — Arm joint bolt group, the issued sheet.
- `s4a-struct-connection-ec3.sheet.txt` — Arm connection bolts, the issued sheet.
- `s4b-struct-connection-ec3.sheet.txt` — Holding-down bolts, the issued sheet.
- `s6-struct-base-plate.sheet.txt` — Column base plate, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
