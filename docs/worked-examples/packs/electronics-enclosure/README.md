# Calc pack — Electronics enclosure (`pack.electronics-enclosure`)

**Chain.** Power trace (`elec.pcb-trace`) → Board load budget (`elec.load-budget`); Board load budget (`elec.load-budget`) → Enclosure temperature (`elec.enclosure-heat`); Enclosure temperature (`elec.enclosure-heat`) → Lid latch (`mech.snap-fit`) and Lid plate (`struct.plate-bending`).

**Purpose.** Follows the loss of a board's power trace into the heat of its load budget, the temperature a sealed enclosure settles at and the pressure difference that temperature puts across it, the temperature the lid latch works at, and the lid plate under that pressure. The trace loss is a row of the load table (a table cell); the enclosure's internal temperature is the latch's temperature of use; the enclosure's sealed-air pressure difference is the lid's pressure. The latch's and lid's material values are typed for 60 °C, the enclosure's limit, and do not follow the links; the trace's board ambient is typed (a link back would be a cycle).

An 8 A supply trace (2 oz external copper, 2 mm wide, 150 mm long, 30 K allowed rise) loses 1.507 W (23.55 mΩ at 90 °C). That loss is the first row of the board's load table (a table cell, W into kW), all released as heat; with the drivers, the power stage's loss, the display and the 24 V supply's own loss (26.5 W: the supply sits inside the enclosure) the board dissipates 73.0 W, and with 4 W of terminals the 500 × 600 × 200 mm wall-mounted enclosure carries 77.0 W: 21.9 K above the 35 °C room, 56.9 °C inside, below its 60 °C limit. The internal temperature is the lid latch's temperature of use (a cantilever snap hook, strain 1.67 % against 3 %). The enclosure is **sealed with no breather, closed at 25 °C**, so its air warming to 56.9 °C at constant volume puts 10.83 kPa (an upper bound) across its walls; that is the uniform pressure on the 500 × 200 × 4 mm simply supported aluminium lid, which deflects 0.483 mm against a span/250 limit of 0.8 mm (utilisation 0.60; the stress is 16.0 MPa against an indicative 120 MPa). **The trace's board ambient (60 °C), the latch's modulus and permissible strain and the lid's modulus, density, Poisson's ratio and limit stress are TYPED for 60 °C, the enclosure's limit, not linked:** the trace's real ambient is the enclosure's own temperature, downstream of it (a link back would be a cycle), and the latch and plate steps take their material values as typed numbers for a temperature; the enclosure step's own check (natural dissipation holds T_max) guards them at run time, and a test shows it failing when the trace current takes the enclosure past 60 °C. The supply's loss row is typed (E6 counts a supply's loss only if it is a row) and a test guards it against the loads. The plastic's and the alloy's values are INVENTED or indicative illustrations, not a maker's data.

**Issue.** Project “Controller box 7”, pack “Motor controller enclosure calc pack”, checked once as a bundle and issued under issue reference **`ELX-ENC-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-ELX-02 and DRG-ELX-210 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Power trace | `elec.pcb-trace` | A | 1 of 1 PASS |
| Board load budget | `elec.load-budget` | A | none |
| Enclosure temperature | `elec.enclosure-heat` | A | 1 of 1 PASS |
| Lid latch | `mech.snap-fit` | A | 2 of 2 PASS |
| Lid plate | `struct.plate-bending` | A | 2 of 2 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an upstream input makes the steps that take from it, and every step that takes from those, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Power trace | `trace_Ploss` | Board load budget | `loads` row 1 (`P`), a table-cell link | 0.00150734 kW |
| Board load budget | `Phi` | Enclosure temperature | `P_add` | 73.0073 W |
| Enclosure temperature | `T_int` | Lid latch | `T_use` | 56.8771 degC |
| Enclosure temperature | `dp_seal` | Lid plate | `q` | 10.8333 kPa |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `e7-elec-pcb-trace.sheet.txt` — Power trace, the issued sheet.
- `e6-elec-load-budget.sheet.txt` — Board load budget, the issued sheet.
- `e2-elec-enclosure-heat.sheet.txt` — Enclosure temperature, the issued sheet.
- `m8-mech-snap-fit.sheet.txt` — Lid latch, the issued sheet.
- `s9-struct-plate-bending.sheet.txt` — Lid plate, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
