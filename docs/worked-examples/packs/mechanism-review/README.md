# Calc pack — Mechanism review (`pack.mechanism-review`)

**Chain.** Tamper cam (`mech.mechanism`) → Follower return spring, Cam shaft, Cam shaft bearing A, Cam drive motor; Cam shaft (`mech.shaft`) → Cam shaft bearing A; Cam drive motor (`elec.motor-sizing`) → Cam drive supply cable.

**Purpose.** Takes a cam mechanism through to its supply: the cam's motion, pressure angle, undercut and follower force; the return spring that holds the follower on the cam; the cam shaft that carries the contact force and the cam's alternating torque; the bearing on that shaft; the geared motor that drives the cam; and the cable that supplies the motor. The cam's spring force, contact force, torques and speed reach each step by link; the cable tables are entered by the user, as no build ships them.

A carton tamper: a 4 kg follower on a Ø30 roller (60 mm base circle, in line) rises 40 mm in 120° (cycloidal), dwells 60°, returns in 120° (modified trapezoid) and dwells 60°, at 150 cam rpm against a return spring of 9.874 N/mm and 50 N preload (all INVENTED). The cam step finds a peak follower speed of 0.6 m/s and acceleration of 14.1 m/s², a peak pressure angle of 22.3° against the 30° limit, no undercut (smallest radius of curvature 73.3 mm against the 15 mm roller), a follower that never leaves the cam (smallest force 50 N), a spring force at full lift of 444.96 N (which is also the peak roller contact force, because the pressure angle is zero at the top), a peak input torque of 10.11 N·m and an RMS input torque of 4.816 N·m. The spring force at full lift is the return spring's second working load (a 6 mm wire on a 50 mm coil, 10.5 active coils, 145 mm free: rate 9.874 N/mm, working travel 40.0 mm, shear stress 305.5 MPa at full lift, surge frequency 82 Hz, 33 times the 2.5 Hz cam frequency); the contact force (N into kN) is a cell of the cam shaft's load row (Ø20/25/20, bearings 250 mm apart, 222.5 N on each, M_max 27.8 N·m), the peak input torque is the shaft's alternating torque amplitude and its peak torque, and the speed is the shaft's speed; the shaft's bearing reaction and the cam speed give bearing A its load and speed (L₁₀ 4.33·10⁵ h on INVENTED ratings, C 3.5 kN and C₀ 1.8 kN); and the RMS torque and speed are the load of a 9.6:1 geared motor (0.557 N·m at 1440 rpm, 84.1 W, against an INVENTED nameplate of 1.0 N·m at that 1440 rpm = 150.8 W), whose rated power sizes the supply cable (0.34 A, a 6 A device, 1.5 mm²). **Not linked, deliberately.** The spring step cannot give its rate and preload back to the cam (a cycle), so the cam carries a typed 9.874 N/mm and 50 N, and a test checks that they are the spring step's computed rate (to 0.01 %) and preload; the cam's lift is the spring's expected working travel (`s_work`: the spring step warns `travel-mismatch` above 1 %, which is what a revised cam rate that the spring no longer matches shows at run time) and the cam frequency in Hz (`f_in`; a speed in rpm is an angular velocity and cannot be linked to a frequency) the spring's operating frequency; the cam's peak torque is checked against the motor's 3 N·m peak by a test, since the motor step is judged on the RMS torque. **The cam's torque enters the shaft as an alternating torque, with a steady torque of 0:** the cam's torque is fully alternating (mean zero, −9.61 to +10.11 N·m), so its peak magnitude is the shaft's alternating amplitude (a conservative bound on the swing); a test recomputes the shaft's fatigue factor independently (9.0, 6 times the 1.5 target) and checks that the cam's mean torque is a small fraction of the amplitude. Bending dominates here, so the earlier steady-RMS stand-in (8.97) read nearly the same; it would understate the stress on a torque-dominated or keyed section. **The cable-capacity and device-rating tables of the cable step are SYNTHETIC test values, invented for this example and labelled as such on the sheet; they are not from any regulation or manufacturer table, and the example must not be used to size a real cable.**

**Issue.** Project “Carton tamper station 4”, pack “Carton tamper cam calc pack”, checked once as a bundle and issued under issue reference **`TMP-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-TMP-04 and DRG-TMP-410 to 420 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Tamper cam | `mech.mechanism` | A | 3 of 3 PASS |
| Follower return spring | `mech.spring` | A | 5 of 5 PASS |
| Cam shaft | `mech.shaft` | A | 5 of 5 PASS |
| Cam shaft bearing A | `mech.bearing-life` | A | 2 of 2 PASS |
| Cam drive motor | `elec.motor-sizing` | A | 5 of 5 PASS |
| Cam drive supply cable | `elec.conductor-sizing` | A | 3 of 3 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Tamper cam | `F_spring_max` | Follower return spring | `F_2` | 444.96 N |
| Tamper cam | `lift` | Follower return spring | `s_work` | 40 mm |
| Tamper cam | `f_in` | Follower return spring | `f_op` | 2.5 Hz |
| Tamper cam | `F_pin_pk` | Cam shaft | `loads` row 1 (`F_y`), a table-cell link | 0.44496 kN |
| Tamper cam | `input_torque` | Cam shaft | `T_a` | 0.0101113 kN*m |
| Tamper cam | `input_torque` | Cam shaft | `T_pk` | 0.0101113 kN*m |
| Tamper cam | `n_in` | Cam shaft | `n` | 150 rpm |
| Cam shaft | `R_A` | Cam shaft bearing A | `F_r` | 0.22248 kN |
| Tamper cam | `n_in` | Cam shaft bearing A | `n` | 150 rpm |
| Tamper cam | `T_rms_in` | Cam drive motor | `T_L` | 4.81616 N*m |
| Tamper cam | `n_in` | Cam drive motor | `n_L` | 150 rpm |
| Cam drive motor | `P_rated` | Cam drive supply cable | `P` | 0.150796 kW |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

The cable-capacity and device-rating tables are the library's synthetic test tables (`ConductorSizingCalculator.SyntheticAmpacity()` and `TypicalRatings()`), saved as unlicensed records titled “SYNTHETIC … (test values, not a regulation table)” and pinned to the cable step. They are not published values.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `m9-mech-mechanism.sheet.txt` — Tamper cam, the issued sheet.
- `m7-mech-spring.sheet.txt` — Follower return spring, the issued sheet.
- `m4-mech-shaft.sheet.txt` — Cam shaft, the issued sheet.
- `m5-mech-bearing-life.sheet.txt` — Cam shaft bearing A, the issued sheet.
- `e1-elec-motor-sizing.sheet.txt` — Cam drive motor, the issued sheet.
- `e4-elec-conductor-sizing.sheet.txt` — Cam drive supply cable, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
