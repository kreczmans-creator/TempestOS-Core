# Calc pack — Electronics enclosure (`pack.electronics-enclosure`)

**Chain.** Power trace (`elec.pcb-trace`) → Board load budget; Board load budget (`elec.load-budget`) → Enclosure temperature; Enclosure temperature (`elec.enclosure-heat`) → Cover latches, Cover plate.

**Purpose.** Follows the loss of a board's power trace into the heat of its load budget, the temperature a sealed enclosure with a pressure-compensation breather settles at, the pressure difference that temperature puts across it (limited by the breather) and the force of that pressure on the front cover, the cover latches that must hold it, and the cover plate under that pressure. The trace loss is a row of the load table (a table cell); the enclosure's internal temperature is the latches' temperature of use; the pressure difference is the cover's pressure and, times the cover area, the force on the latches. The latches' and cover's material values are typed for 60 °C, the enclosure's limit, and do not follow the links; the trace's board ambient, the supply's loss row and the latch count are typed.

An 8 A supply trace (2 oz external copper, 2 mm wide, 150 mm long, 30 K allowed rise) loses 1.507 W (23.55 mΩ at 90 °C). That loss is the first row of the board's load table (a table cell, W into kW), all released as heat; with the drivers, the power stage's loss, the display and the 24 V supply's own loss (26.5 W: the supply sits inside the enclosure) the board dissipates 73.0 W, and with 4 W of terminals the 500 × 600 × 200 mm wall-mounted enclosure carries 77.0 W: 21.9 K above the 35 °C room, 56.9 °C inside, below its 60 °C limit. **The supply's loss row is TYPED, not computed (E6 counts a supply's loss only if it is a row); a test guards it against the loads it serves, but it does not follow them automatically.** The internal temperature is the cover latches' temperature of use. The enclosure is **sealed, closed at 25 °C, with a pressure-compensation breather that cracks at 1 kPa (an INVENTED rating, not a maker's data)**: the air warming to 56.9 °C at constant volume would put 10.83 kPa across its walls (an upper bound), and the breather limits it to 1 kPa. That 1 kPa is the uniform pressure on the 600 × 500 × 4 mm simply supported aluminium front cover (the enclosure's largest face, so the governing panel; 3 mm would deflect 2.03 mm against the 2 mm limit), which deflects 0.855 mm against its span/250 limit of 2 mm (utilisation 0.43; stress 5.9 MPa against an indicative 120 MPa); on the 0.3 m² cover it is a force of 300 N, which six cantilever snap hooks (a typed count, an invented illustration; each strained 1.67 % against 3 %) hold with 431.6 N (utilisation 0.695). With the breather governing, revising the trace leaves the pressure and the force unchanged, so the cover and the latch load stay current; revising the breather rating (1.5 kPa puts 450 N on the latches and fails them) makes both stale. **The trace's board ambient (60 °C), the latches' modulus and permissible strain and the cover's modulus, density, Poisson's ratio and limit stress are TYPED for 60 °C, the enclosure's limit, not linked:** the trace's real ambient is the enclosure's own temperature, downstream of it (a link back would be a cycle), and the latch and plate steps take their material values as typed numbers for a temperature; the enclosure step's own check (natural dissipation holds T_max) guards them at run time, and a test shows it failing when the trace current takes the enclosure past 60 °C. The plastic's and the alloy's values are INVENTED or indicative illustrations, not a maker's data. **Idealisation:** the cover plate is simply supported all round, whereas the real cover is held at six latches and a gasket; its edge reactions peak at mid-edge, so the latches nearest mid-edge carry more than F_req/n. Equal sharing is an idealisation: place the latches at the mid-edges or apply a load-share factor to F_req.

**Issue.** Project “Controller box 7”, pack “Motor controller enclosure calc pack”, checked once as a bundle and issued under issue reference **`ELX-ENC-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-ELX-02 and DRG-ELX-210 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Power trace | `elec.pcb-trace` | A | 1 of 1 PASS |
| Board load budget | `elec.load-budget` | A | none |
| Enclosure temperature | `elec.enclosure-heat` | A | 1 of 1 PASS |
| Cover latches | `mech.snap-fit` | A | 3 of 3 PASS |
| Cover plate | `struct.plate-bending` | A | 2 of 2 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Power trace | `trace_Ploss` | Board load budget | `loads` row 1 (`P`), a table-cell link | 0.00150734 kW |
| Board load budget | `Phi` | Enclosure temperature | `P_add` | 73.0082 W |
| Enclosure temperature | `T_int` | Cover latches | `T_use` | 56.8773 degC |
| Enclosure temperature | `F_seal` | Cover latches | `F_req` | 300 N |
| Enclosure temperature | `dp_seal` | Cover plate | `q` | 1 kPa |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `e7-elec-pcb-trace.sheet.txt` — Power trace, the issued sheet.
- `e6-elec-load-budget.sheet.txt` — Board load budget, the issued sheet.
- `e2-elec-enclosure-heat.sheet.txt` — Enclosure temperature, the issued sheet.
- `m8-mech-snap-fit.sheet.txt` — Cover latches, the issued sheet.
- `s9-struct-plate-bending.sheet.txt` — Cover plate, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
