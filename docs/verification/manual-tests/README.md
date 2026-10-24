# Manual tests — TempestOS v0.26.0 calculators

You follow these scripts by hand in the desktop app. Each file tests one calculator.
Each step lists the inputs, the expected output, the tolerance and the basis of the expected value.
You fill in the Result column. You sign off in the table at the end of this file.

## Index

Time is the estimate in each file. The listed total covers the files that exist now (1370 min, 607 steps).
The electrical, building-services and chain files come from a parallel writer. Check each link before you start.

| Order | Code | File | Liability | Time (min) | Steps |
|---|---|---|---|---|---|
| 1 | E4 | [elec.conductor-sizing](elec.conductor-sizing.md) — Conductor sizing and overload coordination | High | 60 | 30 |
| 2 | E5 | [elec.fault-protection](elec.fault-protection.md) — Fault protection: Zs, disconnection, adiabatic, fault level | High | 35 | 24 |
| 3 | S4 | [struct.connection-ec3](struct.connection-ec3.md) — Bolts and fillet welds (EN 1993-1-8) | High | 45 | 23 |
| 4 | S6 | [struct.base-plate](struct.base-plate.md) — Column base plate (EN 1993-1-8) | High | 75 | 28 |
| 5 | E1 | [elec.motor-sizing](elec.motor-sizing.md) — Motor and actuator sizing | High | 40 | 20 |
| 6 | M4 | [mech.shaft](mech.shaft.md) — Shaft: static, fatigue, keys, critical speed | High | 50 | 18 |
| 7 | M3a | [mech.bolted-joint](mech.bolted-joint.md) — Bolted joint (simplified VDI 2230) | High | 35 | 17 |
| 8 | M3b | [mech.bolt-group](mech.bolt-group.md) — Bolt group (elastic method) | High | 35 | 16 |
| 9 | M6 | [mech.weld-group](mech.weld-group.md) — Weld group (elastic line method) | High | 30 | 15 |
| 10 | S3 | [struct.en1993-member](struct.en1993-member.md) — EN 1993-1-1 member checks (basic) | High | 45 | 23 |
| 11 | S1 | [struct.section-properties](struct.section-properties.md) — Section properties | High | 45 | 19 |
| 12 | M2a | [mech.iso286-fit](mech.iso286-fit.md) — ISO 286 limits and fits | High | 30 | 20 |
| 13 | M2b | [mech.press-fit](mech.press-fit.md) — Press and shrink fit | High | 25 | 13 |
| 14 | E6 | [elec.load-budget](elec.load-budget.md) — Load budget, PSU, transformer and battery | High | 40 | 21 |
| 15 | B1 | [bs.pipe-pump](bs.pipe-pump.md) — Pipe pressure drop and pump duty | High | 45 | 20 |
| 16 | B2 | [bs.psychrometrics](bs.psychrometrics.md) — Psychrometrics | High | 40 | 21 |
| 17 | B6 | [bs.sealed-system](bs.sealed-system.md) — Sealed heating system: emitters and expansion vessel | High | 40 | 20 |
| 18 | M5 | [mech.bearing-life](mech.bearing-life.md) — Rolling bearing life (ISO 281) | Medium | 25 | 16 |
| 19 | S2 | [struct.frame-2d](struct.frame-2d.md) — Beam and 2D frame analysis | Medium | 45 | 18 |
| 20 | S5 | [struct.load-combinations](struct.load-combinations.md) — Load combinations (EN 1990) | Medium | 25 | 17 |
| 21 | M1 | [mech.tolerance-stack](mech.tolerance-stack.md) — Tolerance stack-up (1D) v2 | Medium | 25 | 13 |
| 22 | M2c | [mech.thick-cylinder](mech.thick-cylinder.md) — Thick-walled cylinder stresses (Lamé) | Medium | 20 | 13 |
| 23 | S2b | [struct.beam-formulas](struct.beam-formulas.md) — Beam formulas (single span) | Medium | 30 | 18 |
| 24 | E2 | [elec.enclosure-heat](elec.enclosure-heat.md) — Enclosure heat dissipation and fan sizing | Medium | 35 | 18 |
| 25 | E3 | [elec.voltage-drop](elec.voltage-drop.md) — Voltage drop and I²R loss | Medium | 30 | 18 |
| 26 | B3 | [bs.duct-sizing](bs.duct-sizing.md) — Duct sizing | Medium | 40 | 19 |
| 27 | B4 | [bs.room-heat-loss](bs.room-heat-loss.md) — U-values and room heat loss | Medium | 60 | 24 |
| 28 | B5 | [bs.heat-exchanger](bs.heat-exchanger.md) — Heat exchanger, LMTD and effectiveness-NTU | Medium | 35 | 18 |
| 29 | chain | [chain.drive-train](chain.drive-train.md) — Drive train: motor, shaft, bearing life, seat fit | High | 75 | 21 |
| 30 | chain | [chain.motor-circuit](chain.motor-circuit.md) — Motor circuit: motor, load budget, cable, voltage drop, fault protection, enclosure heat | High | 110 | 23 |
| 31 | chain | [chain.wet-heating](chain.wet-heating.md) — Wet heating: room heat loss, sealed system, pipework and pump, circulator motor | High | 100 | 23 |

E4 and E5 ship together. Test E4 first, then E5.

## Recommended order

Test the highest liability first. The order is E4, E5, S4, S6, E1, M4, M3, M6 and S3.
M3 means both M3a and M3b. After S3, take S1 first: its composite mode has a Critical defect.
Then take the remaining High files, then the Medium files, then the chain files.
Run the chain files last. They need the single-calculator files to pass first.

## How to open a calculator

1. Press Ctrl+K. Type "Open worksheet: " and the calculator title. Press Enter.
2. Or open the Engineering tree. Go to Modules, Calculations, Worksheets. Pick the discipline. Pick the calculator.
3. The header shows the id and version, for example mech.shaft@1.0.0. Check it against the file name.
4. Do not use the node named Calculators. That is an older set of calculators. It is not under test, except where a step says so.

## Common actions

**Import a block.** Copy the whole line of a block to the clipboard. Press *Import inputs (CSV/JSON)*.
The status line must read "Imported n input(s)". The form fills from the block. Any input the block leaves out goes back to its default.
Tables (nodes, bolts, welds, segments and so on) have no form field. Only a block can fill them.

**Reset the form.** Pick another worksheet in the left list. Then pick the worksheet under test again. The form returns to its defaults.

**Change a unit.** Pick the unit in the list beside the box. The number in the box converts. The result must not change.
A unit list offers only units of the right kind. A length box never offers newtons.

**Read the results.** The bar above the results shows the governing check. It reads PASS or FAIL with a utilisation, or Blocked with a reason.
The text No pass/fail check means the calculator has no limit check. Warnings print below the results as Warning lines.
Values show five significant digits. *Show working* prints each formula with its substitution.

**Save, check and issue.** *Save as calculation* is disabled while the bar reads Blocked. *Record check* needs every input to carry a source.
Pick the source in the list beside each box (drawing, specification, assumption, evidence or reference) and type a reference.
*Issue* needs a checked calculation. The fault-protection file needs a second signed-in user for the check.

## Reading the columns

- **Inputs.** Value and unit for each input to set. A line starting with Block names an import block at the end of the same file.
- **Expected output.** The symbol and the value to find. The value uses the unit shown.
- **Tolerance.** *Shown digits* means the last shown digit may differ by one. A percentage is relative to the expected value. *Text match* means the wording agrees in sense.
- **Expected PASS/FAIL/BLOCK/WARN.** The state the sheet should reach. PASS or FAIL is the bar. BLOCK means Blocked. WARN means a Warning line.
- **Basis.** Where the expected value comes from. KAT is a known-answer case from a published source. Independent script is a Python program in tools/verification that does not share code with the product. Hand formula is arithmetic you can repeat. Product behaviour (no number) means the step checks wording or state only. P1 report means the independent review says what is right.
- **Result.** You fill this in. See the next section.

## How to record results

1. Copy the file you are testing. Keep the original clean. Fill in the copy.
2. For each step write one word in the Result column. PASS means the sheet matched every expected item. FAIL means it did not. SKIPPED means you could not run the step.
3. On FAIL, write the values or text you saw in the Notes column. Add the screenshot name, for example mech.shaft-step12.png.
4. Steps marked "known defect P1-... ; expect FAIL until fixed" are expected to FAIL today. Record FAIL and the values you saw. A PASS there means the defect is fixed or the step was misread. Say which.
5. A step marked "suspected defect" or "not yet in a P1 report" needs your judgement. Write what you found and what your reference says.
6. Add one line to the defect log below for every FAIL that is not a known defect.
7. At the end of a file count the results. Enter them in the sign-off table. Initial and date the row.

Do not record a PASS from memory. Record it when you have compared each expected value on the screen.

### Defect log

| Date | File and step | What you saw | What was expected | Screenshot | Known defect id or NEW |
|---|---|---|---|---|---|
|  |  |  |  |  |  |

## Procedure L: stale upstream test

Most calculators have a last step called Stale upstream. It tests the link between two saved calculations.
Use one project for every calculation in the test. Open the project first.

1. Open the upstream worksheet named in the step. Enter its inputs. Type the name UP in *Calculation name*. Press *Save as calculation*. The watermark reads DRAFT.
2. Open the downstream worksheet. Enter its inputs. Type the name DOWN. Press *Save as calculation*.
3. In the Link row under the inputs, pick the input named in the step. Pick the calculation UP · rev A · Draft. Pick the output named in the step. Press *Link input*.
   The input shows a link as its source. The results recalculate from the upstream value.
4. Go back to UP. Change one input as the step says. Press *Save changes*.
5. Open DOWN again. Expect an orange banner that starts with Stale — and a button *Update linked inputs*.
6. Press *Record check* on DOWN. Expect a refusal in the status line. It says the calculation is stale and must be updated before checking. The status stays DRAFT.
7. Press *Update linked inputs*. The banner clears. The results use the new upstream value.

Open point for the builder. The code has no screen that reopens a saved calculation in the Worksheets view.
The method that does this (CalcSheetCoordinator.OpenAsync) has no caller in the desktop code.
If you cannot reopen UP or DOWN in step 4 or 5, record SKIPPED and say so in Notes. Look first in the Calculation register and the project tree for an Open action.
The chain file says to reopen from the Calculation register. My reading is that this register opens the older workbench records, not worksheet instances. Try it and report what happens.
An issued upstream works the same way: use *Revise* on UP, and DOWN goes stale. *Issue* on a checked but stale DOWN is also refused.
The load-combinations and frame-2d files have no stale step. No calculation outputs a load factor or a modulus for them to take.

## Source questions for you

Some expected values rest on a book I could not read. Please check these against your copy and note the answer.

| Calculator | Book or table | What to read |
|---|---|---|
| mech.shaft | Shigley 11th ed., Example 7-1 and Table 6-2 | S_ut, S_y, q, k_a for the shoulder example. The KAT source is contradicted. |
| mech.bolted-joint | Shigley, joint stiffness equation (Eq. 8-20 in the 9th ed.) | Which diameter sits in the log term. The sheet uses the hole diameter. |
| mech.tolerance-stack | Shigley 9th ed., Ch. 1 | The shouldered-screw stack. Gap 0.005 in, worst case ±0.010 in, RSS ±0.006 in. |
| mech.iso286-fit | ISO 286-2 tables | Tabulated deviations for the findings steps (18 mm, N9, K8, p6 and u6, t6, k6 above 500 mm). |
| struct.connection-ec3 | SCI P358, P363 and EN 1993-1-8 | Fin plate example, M20 8.8 table, the M12 hole clause, the hole-size factors. |
| struct.load-combinations | Designers' Guide to EN 1990 and your National Annex | The example with G 20, Q 8 and 3 kN/m. Your xi, psi and K_FI values. |
| struct.en1993-member | EN 1993-1-1 and Annex B | Clause 6.2.8, k_yy values, shear buckling limit. |
| struct.beam-formulas | Roark, 8th ed., Table 8.1 | The case numbers beside each formula. |
| struct.section-properties | Blue Book tables | Torsion constants J for channel, angle and tee. |

## Sign-off

Sign a row when every step in its file has a Result and every FAIL has a Notes entry.
Verdict is one of: accept, accept with conditions, reject. Conditions go in the defect log.

| Order | Code | File | Steps run | PASS | FAIL | SKIPPED | Known defects seen | Tester initials | Date | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | E4 | elec.conductor-sizing |  |  |  |  |  |  |  |  |
| 2 | E5 | elec.fault-protection |  |  |  |  |  |  |  |  |
| 3 | S4 | struct.connection-ec3 |  |  |  |  |  |  |  |  |
| 4 | S6 | struct.base-plate |  |  |  |  |  |  |  |  |
| 5 | E1 | elec.motor-sizing |  |  |  |  |  |  |  |  |
| 6 | M4 | mech.shaft |  |  |  |  |  |  |  |  |
| 7 | M3a | mech.bolted-joint |  |  |  |  |  |  |  |  |
| 8 | M3b | mech.bolt-group |  |  |  |  |  |  |  |  |
| 9 | M6 | mech.weld-group |  |  |  |  |  |  |  |  |
| 10 | S3 | struct.en1993-member |  |  |  |  |  |  |  |  |
| 11 | S1 | struct.section-properties |  |  |  |  |  |  |  |  |
| 12 | M2a | mech.iso286-fit |  |  |  |  |  |  |  |  |
| 13 | M2b | mech.press-fit |  |  |  |  |  |  |  |  |
| 14 | E6 | elec.load-budget |  |  |  |  |  |  |  |  |
| 15 | B1 | bs.pipe-pump |  |  |  |  |  |  |  |  |
| 16 | B2 | bs.psychrometrics |  |  |  |  |  |  |  |  |
| 17 | B6 | bs.sealed-system |  |  |  |  |  |  |  |  |
| 18 | M5 | mech.bearing-life |  |  |  |  |  |  |  |  |
| 19 | S2 | struct.frame-2d |  |  |  |  |  |  |  |  |
| 20 | S5 | struct.load-combinations |  |  |  |  |  |  |  |  |
| 21 | M1 | mech.tolerance-stack |  |  |  |  |  |  |  |  |
| 22 | M2c | mech.thick-cylinder |  |  |  |  |  |  |  |  |
| 23 | S2b | struct.beam-formulas |  |  |  |  |  |  |  |  |
| 24 | E2 | elec.enclosure-heat |  |  |  |  |  |  |  |  |
| 25 | E3 | elec.voltage-drop |  |  |  |  |  |  |  |  |
| 26 | B3 | bs.duct-sizing |  |  |  |  |  |  |  |  |
| 27 | B4 | bs.room-heat-loss |  |  |  |  |  |  |  |  |
| 28 | B5 | bs.heat-exchanger |  |  |  |  |  |  |  |  |
| 29 | chain | chain.drive-train |  |  |  |  |  |  |  |  |
| 30 | chain | chain.motor-circuit |  |  |  |  |  |  |  |  |
| 31 | chain | chain.wet-heating |  |  |  |  |  |  |  |  |

Overall verdict: ______________   Signed: ______________   Date: ______________
