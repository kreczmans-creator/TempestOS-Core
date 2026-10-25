# Calc pack — Hydraulic power unit (`pack.hydraulic-power-unit`)

**Chain.** Cylinder and pump circuit (`fp.cylinder`) → Emergency stroke accumulator (`fp.accumulator`) → Pressure line (`bs.pipe-pump`) → Pump drive motor (`elec.motor-sizing`) → Motor supply cable (`elec.conductor-sizing`).

**Purpose.** Takes a cylinder circuit and its pump through to the electric supply: the cylinder, pump and line velocities; the accumulator that carries the emergency stroke between the supply pressure and the lowest pressure the stroke can finish at; the pressure drop in the line to the cylinder; the motor that drives the pump continuously; and the cable that supplies the motor. The motor's cable tables are entered by the user: no build ships them. The pressure line takes its flow, oil density and tube bore from the cylinder step, and the cable is sized for the rated power of the candidate motor.

A 100/70 × 500 mm cylinder at 160 bar and 36 L/min, fed by a 28 cm³/rev pump at 1450 rpm (38.57 L/min delivered), presses with 80 kN and pulls with 40 kN. Its swept volume (3.927 L, the extend stroke) and its supply pressure (160 bar) size an emergency-stroke accumulator (26.7 L of gas, n = 1.4, 10 to 50 °C, working range 120 to 160 bar; 120 bar still finishes the 80 kN push, which needs 107 bar). The pressure-line step judges the line the cylinder step judges, the line to the cylinder cap port: its flow is the cylinder step's `Q_prs`, the larger of the two strokes, the 75.6 L/min retract discharge (4.54 m³/h; the 36 L/min circuit flow only passes the cap port on the extend stroke), and the oil density (870 kg/m³) and the 20 mm bore (the first row of its segment table) are linked from the cylinder step's `rho` and `d_prs` too, so revising the cylinder's line bore makes the pressure line stale (a test proves it). The 12 m line runs at 4.01 m/s, inside the 5 m/s limit (a 16 mm line would read 6.3 m/s and fail the cylinder step). **Transitional flow:** at 75.6 L/min in 20 mm with the 30 mPa·s oil the Reynolds number is 2327, just above the 2300 where the pressure-line sheet switches from f = 64/Re to Colebrook–White; the sheet warns that the friction factor is uncertain and uses the Colebrook value (f = 0.0489, against 0.0275 for 64/Re), which is the larger and so the conservative one, so the line costs 247.5 kPa (2.5 bar, 1.5 % of the 160) where the laminar relation would give 157.6 kPa; the step is not flagged laminar, and the oil's viscosity is taken at the 50 °C working temperature only (colder oil is more viscous and would drop more pressure). The pump's shaft torque (76.97 N·m) and speed (1450 rpm) are the continuous load of its drive motor (77.75 N·m through a 0.99 coupling, 11.81 kW); the candidate motor is rated 120 N·m, which at the 1450 rpm running speed (taken as its rated speed) is 18.22 kW, and **that rated power, not the load, sizes the supply cable** (400 V, η 0.92, cos φ 0.86: 33.2 A, a 40 A type C device, 10 mm²), so the cable is good for the motor that will be fitted up to its rating, not only for this pump; the 18.5 kW nameplate of a real 4-pole motor would be 18.5 kW at its own rated speed, which the sheet does not take as an input. **The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**

**Issue.** Project “Press 5 hydraulic power unit”, pack “Press hydraulic power unit calc pack”, checked once as a bundle and issued under issue reference **`HPU-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-HPU-05 and DRG-HPU-510 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Cylinder and pump circuit | `fp.cylinder` | A | 7 of 7 PASS |
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
| Cylinder and pump circuit | `Q_prs` | Pressure line | `Q` | 4.53765 m^3/h |
| Cylinder and pump circuit | `rho` | Pressure line | `rho_custom` | 870 kg/m^3 |
| Cylinder and pump circuit | `d_prs` | Pressure line | `segments` row 1 (`D`), a table-cell link | 20 mm |
| Cylinder and pump circuit | `T_shaft` | Pump drive motor | `T_L` | 76.9731 N*m |
| Cylinder and pump circuit | `n_shaft` | Pump drive motor | `n_L` | 1450 rpm |
| Pump drive motor | `P_rated` | Motor supply cable | `P` | 18.2212 kW |

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
