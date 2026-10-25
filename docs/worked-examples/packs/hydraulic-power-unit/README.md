# Calc pack — Hydraulic power unit (`pack.hydraulic-power-unit`)

**Chain.** Cylinder and pump circuit (`fp.cylinder`) → Emergency stroke accumulator (`fp.accumulator`) → Pressure line (`bs.pipe-pump`) → Pump drive motor (`elec.motor-sizing`) → Motor supply cable (`elec.conductor-sizing`).

**Purpose.** Takes a cylinder circuit and its pump through to the electric supply: the cylinder, pump and line velocities; the accumulator that carries the emergency stroke between the supply pressure and the lowest pressure the stroke can finish at; the pressure drop in the line to the cylinder; the motor that drives the pump continuously; and the cable that supplies the motor. The line's oil density and tube bore, and the motor's cable tables, are entered by the user: the cylinder step gives no output for the first two, and no build ships the tables.

A 100/70 × 500 mm cylinder at 160 bar and 36 L/min, fed by a 28 cm³/rev pump at 1450 rpm (38.57 L/min delivered), presses with 80 kN and pulls with 40 kN. Its swept volume (3.927 L, the extend stroke) and its supply pressure (160 bar) size an emergency-stroke accumulator (26.7 L of gas, n = 1.4, 10 to 50 °C, working range 120 to 160 bar; 120 bar still finishes the 80 kN push, which needs 107 bar). Its circuit flow (36 L/min = 2.16 m³/h) runs through 12 m of 16 mm pressure line; the oil (870 kg/m³, 30 mPa·s) and the tube bore are entered equal to the cylinder step's `rho` and `d_prs` (the cylinder step offers no output for them, and a test guards the match), the flow is laminar (Re 1385) and the line costs 157.5 kPa (1.6 bar of the 160). The pump's shaft torque (76.97 N·m) and speed (1450 rpm) are the continuous load of its drive motor (77.75 N·m through a 0.99 coupling, 11.81 kW, against a candidate 18.5 kW motor of 120 N·m), and that continuous shaft power sizes the supply cable (400 V, η 0.92, cos φ 0.86: 21.5 A, a 25 A type C device, 6 mm²). The cable is sized for the pump's continuous demand, not the nameplate of the motor that will be fitted, and the pressure line carries the circuit flow because the relief valve is taken to be at the pump (the cylinder step takes the larger pump delivery for its own line velocities). **The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**

**Issue.** Project “Press 5 hydraulic power unit”, pack “Press hydraulic power unit calc pack”, checked once as a bundle and issued under issue reference **`HPU-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-HPU-05 and DRG-HPU-510 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Cylinder and pump circuit | `fp.cylinder` | A | 6 of 7 PASS |
| Emergency stroke accumulator | `fp.accumulator` | A | 2 of 2 PASS |
| Pressure line | `bs.pipe-pump` | A | 1 of 1 PASS |
| Pump drive motor | `elec.motor-sizing` | A | 3 of 3 PASS |
| Motor supply cable | `elec.conductor-sizing` | A | 3 of 3 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an upstream input makes every step below it stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Cylinder and pump circuit | `dV` | Emergency stroke accumulator | `dV` | 3.92699 L |
| Cylinder and pump circuit | `p` | Emergency stroke accumulator | `p_2` | 160 bar |
| Cylinder and pump circuit | `Q` | Pressure line | `Q` | 2.16 m^3/h |
| Cylinder and pump circuit | `T_shaft` | Pump drive motor | `T_L` | 76.9731 N*m |
| Cylinder and pump circuit | `n_shaft` | Pump drive motor | `n_L` | 1450 rpm |
| Pump drive motor | `P_cont` | Motor supply cable | `P` | 11.8059 kW |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

The cable-capacity and device-rating tables are the library's synthetic test tables (`ConductorSizingCalculator.SyntheticAmpacity()` and `TypicalRatings()`), saved as unlicensed records titled “SYNTHETIC … (test values, not a regulation table)” and pinned to the cable step. They are not published values.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `f1-fp-cylinder.sheet.txt` — Cylinder and pump circuit, the issued sheet.
- `f2-fp-accumulator.sheet.txt` — Emergency stroke accumulator, the issued sheet.
- `b1-bs-pipe-pump.sheet.txt` — Pressure line, the issued sheet.
- `e1-elec-motor-sizing.sheet.txt` — Pump drive motor, the issued sheet.
- `e4-elec-conductor-sizing.sheet.txt` — Motor supply cable, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
