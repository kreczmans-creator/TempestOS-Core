# Calc pack — Motor circuit (`pack.motor-circuit`)

**Chain.** Motor sizing (`elec.motor-sizing`) → Load budget; Load budget (`elec.load-budget`) → Cable and device, Voltage drop, Enclosure heat; Cable and device (`elec.conductor-sizing`) → Voltage drop, Fault protection.

**Purpose.** Sizes a motor's supply from the motor duty to the enclosure: demand, cable and device, voltage drop, earth-fault disconnection and the heat the panel must lose.

A ball-screw servo axis (50 kg, 0.5 m in 0.5 s) is sized, its panel demand and heat found, its cable and device chosen, and the voltage drop, earth-fault disconnection and enclosure temperature checked. The protective conductor is taken as the same size as the cable step's line conductor (the fault-protection step's S_cpc is linked to the cable's CSA). The bus current that sizes the cable is the running current of the loads (their duty is not applied). **The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**

**Issue.** Project “Line 12 axis 3”, pack “Servo axis motor circuit calc pack”, checked once as a bundle and issued under issue reference **`LINE-ELEC-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-LINE-12 and DRG-LINE-120 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Motor sizing | `elec.motor-sizing` | A | 3 of 3 PASS |
| Load budget | `elec.load-budget` | A | none |
| Cable and device | `elec.conductor-sizing` | A | 3 of 3 PASS |
| Voltage drop | `elec.voltage-drop` | A | 1 of 1 PASS |
| Fault protection | `elec.fault-protection` | A | 4 of 4 PASS |
| Enclosure heat | `elec.enclosure-heat` | A | 1 of 1 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Motor sizing | `P_rms` | Load budget | `P_add` | 0.784142 kW |
| Motor sizing | `P_rated` | Load budget | `P_rated_add` | 0.628319 kW |
| Load budget | `I_3ph` | Cable and device | `I_b_in` | 1.37273 A |
| Cable and device | `I_b` | Voltage drop | `I` | 1.37273 A |
| Cable and device | `L` | Voltage drop | `L` | 25 m |
| Cable and device | `CSA` | Voltage drop | `A_c` | 1.5 mm^2 |
| Load budget | `U_L` | Voltage drop | `V_n` | 400 V |
| Load budget | `pf_link` | Voltage drop | `cos_phi` | 0.85 |
| Cable and device | `T_op` | Voltage drop | `T_c` | 70 degC |
| Cable and device | `CSA` | Fault protection | `S_line` | 1.5 mm^2 |
| Cable and device | `CSA` | Fault protection | `S_cpc` | 1.5 mm^2 |
| Cable and device | `L` | Fault protection | `L` | 25 m |
| Cable and device | `I_n` | Fault protection | `I_n` | 6 A |
| Cable and device | `device_code` | Fault protection | `device_code` | 2 |
| Cable and device | `T_op` | Fault protection | `T_op` | 70 degC |
| Load budget | `Phi` | Enclosure heat | `P_add` | 153.333 W |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

The cable-capacity and device-rating tables are the library's synthetic test tables (`ConductorSizingCalculator.SyntheticAmpacity()` and `TypicalRatings()`), saved as unlicensed records titled “SYNTHETIC … (test values, not a regulation table)” and pinned to the cable step. They are not published values.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `e1-elec-motor-sizing.sheet.txt` — Motor sizing, the issued sheet.
- `e6-elec-load-budget.sheet.txt` — Load budget, the issued sheet.
- `e4-elec-conductor-sizing.sheet.txt` — Cable and device, the issued sheet.
- `e3-elec-voltage-drop.sheet.txt` — Voltage drop, the issued sheet.
- `e5-elec-fault-protection.sheet.txt` — Fault protection, the issued sheet.
- `e2-elec-enclosure-heat.sheet.txt` — Enclosure heat, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
