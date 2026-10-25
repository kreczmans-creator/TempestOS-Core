# Calc pack — Hydraulic power unit (`pack.hydraulic-power-unit`)

**Chain.** Cylinder and pump circuit (`fp.cylinder`) → Emergency stroke accumulator, Pressure line, Pump drive motor; Pump drive motor (`elec.motor-sizing`) → Motor supply cable, Pump motor start; Motor supply cable (`elec.conductor-sizing`) → Motor supply voltage drop, Motor supply fault protection, Pump motor start; Motor supply fault protection (`elec.fault-protection`) → Pump motor start.

**Purpose.** Takes a cylinder circuit and its pump through to the electric supply: the cylinder, pump and line velocities; the accumulator that carries the emergency stroke between the supply pressure and the lowest pressure the stroke can finish at; the pressure drop in the line to the cylinder; the motor that drives the pump continuously; the cable that supplies the motor, its voltage drop and its earth-fault disconnection; and the voltage dip when the motor starts on that cable and supply. The motor's cable tables are entered by the user: no build ships them. The pressure line takes its flow, oil density and tube bore from the cylinder step, the cable is sized for the rated power of the candidate motor, and the start is judged on the cable and the supply fault level the pack found.

A 100/70 × 500 mm cylinder at 160 bar and 36 L/min, fed by a 28 cm³/rev pump at 1450 rpm (38.57 L/min delivered), presses with 80 kN and pulls with 40 kN. Its swept volume (3.927 L, the extend stroke) and its supply pressure (160 bar, a gauge pressure, so the accumulator's pressures stay on the Gauge basis; Absolute would read the linked value one atmosphere low and the sheet would warn) size an emergency-stroke accumulator (30.1 L of gas, held at 160 bar after a slow charge and discharged at n = 1.4, 10 to 50 °C, working range 120 to 160 bar; 120 bar still finishes the 80 kN push, which needs 107 bar).

The pressure-line step judges the line the cylinder step judges, the line to the cylinder cap port: its flow is the cylinder step's `Q_prs`, the larger of the two strokes, the 75.6 L/min retract discharge (4.54 m³/h; the 36 L/min circuit flow only passes the cap port on the extend stroke), and the oil density (870 kg/m³) and the 20 mm bore (the first row of its segment table) are linked from the cylinder step's `rho` and `d_prs` too, so revising the cylinder's line bore makes the pressure line stale (a test proves it). B1 judges the cap-port line at the retract discharge, where its pressure drop (about 2.5 bar over the 78.5 cm² cap area) is the back pressure the retract stroke pushes against; neither that drop nor the supply-side drop is fed back into F1, whose back pressure and supply pressure are entered.

The 12 m line runs at 4.01 m/s, inside the 5 m/s limit (a 16 mm line would read 6.3 m/s and fail the cylinder step).

**Transitional flow:** at 75.6 L/min in 20 mm with the 30 mPa·s oil the Reynolds number is 2327, just above the 2300 where the pressure-line sheet switches from f = 64/Re to Colebrook–White; the sheet warns that the friction factor is uncertain and uses the Colebrook value (f = 0.0489, against 0.0275 for 64/Re), which is the larger and so the conservative one, so the line costs 247.5 kPa (2.5 bar, 1.5 % of the 160) where the laminar relation would give 157.6 kPa; the step is not flagged laminar, and the oil's viscosity is taken at the 50 °C working temperature only (colder oil is more viscous and would drop more pressure).

The 6 velocity heads of fittings are a turbulent-flow coefficient; the real K rises at low Reynolds number, so at Re 2327 the fittings loss (42.0 kPa of the 247.5) is understated, and the sheet says so (`fittings` warning).

The pump's shaft torque (76.97 N·m) and speed (1450 rpm) are the continuous load of its drive motor (77.75 N·m through a 0.99 coupling, 11.81 kW); the candidate motor is rated 120 N·m at a rated speed of 1460 rpm (an INVENTED nameplate figure for this example, to be replaced from the real motor's nameplate; the pump runs at 1450 rpm), which is 18.35 kW, and **that rated power, not the load, sizes the supply cable** (400 V, η 0.92, cos φ 0.86: 33.5 A, a 40 A type C device, 10 mm²), so the cable is good for the motor that will be fitted up to its rating, not only for this pump.

The cable's design current, length and conductor area give the **voltage drop** (3.16 V, 0.79 % of 400 V) and, with its device rating and type code, the **earth-fault check** (loop impedance 0.174 Ω against a 0.546 Ω limit for the 40 A type C device, 400 A to trip; the 10 mm² protective conductor is the cable's size by hand; the 0.05 Ω external loop is the INVENTED figure of a close-coupled 400 kVA transformer).

The supply transformer (400 kVA, 4 %: INVENTED) is entered once, on the fault-protection step, and its three-phase fault current (15.94 kA, `I_k3`, computed with c = 1.1, which also reaches the start step as `c_k3`) is the source of the **motor start**: the start step takes that fault current, the cable's length and conductor area, and the motor's rated power (18.35 kW) by link, and judges a star-delta start (7 × I_rated / 3 = 78.1 A, the 7 × locked-rotor ratio and 0.3 starting power factor INVENTED) as a 1.25 % dip at the motor terminals (0.53 % at the point of common coupling; **this is the fault level at the transformer terminals, a lower bound on the dip**: the supply cabling and any upstream network are not in it, and the start step's `S_kQ` is where an upstream network is added), with 5.28 kvar to correct the running power factor from 0.86 to 0.95.

The start step comes last because it takes from both the cable and the fault protection; its rated current is **not** fed back to the cable step (cable, fault protection and start would then form a cycle), so the cable is still sized on the motor's rated power, and the two steps' copies of the motor's efficiency and power factor are checked equal by a test (and their rated current and design current agree at 33.47 A).

The pump is started **unloaded**, through a vent or unloading valve to tank, not against its relief valve (a pump held at relief pressure is at full load, and in star the motor gives about a third of its direct-on-line torque: the drive step's run-up check, made at the direct-on-line peak torque, would not cover a loaded star start; a test checks that a third of the 275 N·m peak covers the safety factor times the run-up and an invented 20 N·m unloaded torque).

The start current is for coordination only: the star-delta 78 A stays below the 200 A at which a 40 A type C device's instantaneous band begins, where a direct-on-line start of the same motor (234 A) would not; the open-transition changeover from star to delta is not modelled and can briefly approach the direct-on-line current; the fault current was computed at √3 × 230 V = 398.4 V and the start at the motor's 400 V, 0.4 % apart (conservative).

**The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**

**Issue.** Project “Press 5 hydraulic power unit”, pack “Press hydraulic power unit calc pack”, checked once as a bundle and issued under issue reference **`HPU-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-HPU-05 and DRG-HPU-510 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Cylinder and pump circuit | `fp.cylinder` | A | 7 of 7 PASS |
| Emergency stroke accumulator | `fp.accumulator` | A | 2 of 2 PASS |
| Pressure line | `bs.pipe-pump` | A | 1 of 1 PASS |
| Pump drive motor | `elec.motor-sizing` | A | 3 of 3 PASS |
| Motor supply cable | `elec.conductor-sizing` | A | 3 of 3 PASS |
| Motor supply voltage drop | `elec.voltage-drop` | A | 1 of 1 PASS |
| Motor supply fault protection | `elec.fault-protection` | A | 5 of 5 PASS |
| Pump motor start | `elec.motor-start` | A | 1 of 1 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Cylinder and pump circuit | `dV` | Emergency stroke accumulator | `dV` | 3.92699 L |
| Cylinder and pump circuit | `p` | Emergency stroke accumulator | `p_2` | 160 bar |
| Cylinder and pump circuit | `Q_prs` | Pressure line | `Q` | 4.53765 m^3/h |
| Cylinder and pump circuit | `rho` | Pressure line | `rho_custom` | 870 kg/m^3 |
| Cylinder and pump circuit | `d_prs` | Pressure line | `segments` row 1 (`D`), a table-cell link | 20 mm |
| Cylinder and pump circuit | `T_shaft` | Pump drive motor | `T_L` | 76.9731 N*m |
| Cylinder and pump circuit | `n_shaft` | Pump drive motor | `n_L` | 1450 rpm |
| Pump drive motor | `P_rated` | Motor supply cable | `P` | 18.3469 kW |
| Motor supply cable | `I_b` | Motor supply voltage drop | `I` | 33.47 A |
| Motor supply cable | `L` | Motor supply voltage drop | `L` | 30 m |
| Motor supply cable | `CSA` | Motor supply voltage drop | `A_c` | 10 mm^2 |
| Motor supply cable | `CSA` | Motor supply fault protection | `S_line` | 10 mm^2 |
| Motor supply cable | `L` | Motor supply fault protection | `L` | 30 m |
| Motor supply cable | `I_n` | Motor supply fault protection | `I_n` | 40 A |
| Motor supply cable | `device_code` | Motor supply fault protection | `device_code` | 2 |
| Pump drive motor | `P_rated` | Pump motor start | `P` | 18.3469 kW |
| Motor supply cable | `CSA` | Pump motor start | `A_c` | 10 mm^2 |
| Motor supply cable | `L` | Pump motor start | `L_cab` | 30 m |
| Motor supply fault protection | `I_k3` | Pump motor start | `I_k` | 15.942 kA |
| Motor supply fault protection | `c_k3` | Pump motor start | `c_src` | 1.1 |

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
- `e3-elec-voltage-drop.sheet.txt` — Motor supply voltage drop, the issued sheet.
- `e5-elec-fault-protection.sheet.txt` — Motor supply fault protection, the issued sheet.
- `e9-elec-motor-start.sheet.txt` — Pump motor start, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
