# Display changes of the C-2 fix (phase A)

This page lists what a reader of a sheet sees differently after the review fix of C-2 phase A (decisions C2-20 to C2-28 in [`sheet-standard.md`](sheet-standard.md)).
No calculated value changes in this page except for the five templates of item M1 (C2-24) and the twelve of the verification findings F1, F2 and F7 (C2-35 to C2-38, in
their spec pages); a displayed number changes, its value, its SI value, its stored output, its hash and every export stay as they were. "Old" is what phase A printed, "new" is what the sheet prints now. Every example below is a line of a regenerated worked example or a case of
`DisplayRulesTests`.

## 1. Significant figures (C2-21)

A result is shown with at least four and at most five significant figures; the decimals of the quantity type are the least shown. A whole number is printed exactly.

| Output | Old | New |
|---|---|---|
| `theta_se` (insulation) | `25.3 °C` | `25.27 °C` |
| `sigma_run` (frame) | `0.086 MPa` | `0.08606 MPa` |
| `tau_max` (lifting lug) | `67.5 MPa` | `67.55 MPa` |
| `theta_A` (shaft) | `0.00090 rad` | `0.0008961 rad` |
| `R_A` (shaft) | `3.20 kN` | `3.195 kN` |
| `m_min` (tolerance stack) | `0.080 mm` | `0.08000 mm` |
| `m_min` (tolerance stack, inch) | `-0.0080 in` | `-0.008000 in` |
| `X` (voltage drop) | `0.0064 Ω` | `0.006400 Ω` |
| `d_A` (shaft) | `35.0 mm` | `35 mm` |
| `sigma_r_a` (thick cylinder) | `-50.0 MPa` | `-50 MPa` |
| `mu_0` (mechanism) | `90.00°` | `90°` |
| a duration of 150000 h, a volume of 13824 m³ | not printed exactly | `150000 h`, `13824 m³` |

## 2. Safety factors and "at least" ratios round down (C2-21)

An output a template declares `AtLeast` (`c.MarkAtLeast(...)`: a safety factor, a margin, a ratio that must reach a target) is rounded down, so the text never reads at
or above its target when the value is below it.

| Value | Target | Old | New |
|---|---|---|---|
| `n_f_min` 1.69081 | 1.5 | `1.69` | `1.6908` |
| `n_y_min` 4.15309 | 1.5 | `4.15` | `4.1531` |
| `n_f` 1.5578 (spring) | 1.5 | `1.56` | `1.5578` |
| a safety factor of 1.4999 | 1.5 | `1.50` | `1.4999` |
| a safety factor of 1.495 | 1.5 | `1.50` | `1.495` |

## 3. Utilisation (C2-21)

A utilisation is what a template declares (`c.MarkUtilisation(...)`) or the symbol a utilisation check is made on, never what its symbol or its description says. It is two
decimals, rounded up whenever it exceeds its own limit, away from zero when negative, and two significant figures below 0.005.

| Value | Old | New |
|---|---|---|
| u = 1.004 | `1.00` (a failing check read as 1.00) | `1.01` |
| u = 0.504 against a limit of 0.5 (`CheckUtilisation(..., limit: 0.5)`) | `0.50` | `0.51` |
| u = -1.004 | `-1.00` | `-1.01` |
| u = 0.00022 | `0.00` | `0.00022` |
| `u_ratio`, "Factor applied to utilisation" | shown as a utilisation | a plain number |
| a failing check line, demand 40.004° against 40° | `40.00°` against `40°` | `40.01°` against `40°` |

## 4. Zero for solver noise (C2-20)

A value is shown as `0` for being small only when its template flags it noise-prone, and then only against the working values of its own dimension and unit in the same run.
Phase A compared any output with every other output of the same dimension and so printed real values as `0`.

| Case | Old | New |
|---|---|---|
| `Z_x` 3.1595 cm³ beside `S_run` 13824 m³ | `0 cm³` | `3.160 cm³` |
| 0.4 ms beside 150000 h | zeroed as noise | `0.4000 ms` |
| 3 mN·m beside 5 MJ | zeroed as noise | printed as it is, not zeroed |
| `sigma_r_b` of a thick cylinder, 1.8626e-15 MPa beside 144.9 MPa (flagged) | `0 MPa` | `0 MPa` |

## 5. One formatter for the form, the banner and the register (C2-22)

| Where | Old | New |
|---|---|---|
| governing banner, `u` = 1.2516 | `1.25` (three figures, not rounded up) | `1.26` (the figure the sheet prints) |
| register, stored utilisation 1.2516 | `1.25` | `1.26` |
| a yes/no check on the form | `u = 0` | the check text of the sheet (`FormCheckText`) |
| `docs/testing/v0.26.0-test-values.md`, Expected column | `kN*m`, `deg` | `kN·m`, `°` |

## 6. Units in table cells and ranges (L2)

Table-cell and range units are written with the unit symbols (`m³`, `kN·m`, `°C`), as the other units of the sheet are.

## 7. The rows of a failing check (C2-30)

The Results rows of the two quantities of a failing check round the way the check line does: the demand up (away from zero), the capacity down (toward zero), so that no row
prints on the passing side of its limit.

| Case | Old | New |
|---|---|---|
| `sigma_vm_hub` of `mech.press-fit` at u = 1.0000012 against 350 MPa | `350.0 MPa` | `350.1 MPa` |
| `y_cr` of `mech.spring` 11.99999 mm against s_2 = 12 mm | `12.00 mm` | `11.99 mm` |

## 8. A utilisation rounds by the verdict, not by a threshold (C2-31)

| Case | Old | New |
|---|---|---|
| u = 1.0000000001 on a check with a tolerance of 1e-12 (failing) | `1.00` | `1.01` |
| u = 1.25 held as 1.2500000000000002 (failing) | `1.25` | `1.25` |
| u = 1.006 on a check that passes within a tolerance of 0.01 | `1.01` | `1.00` |
| `u_rated_speed` of `elec.motor-sizing` | not a utilisation | a utilisation |

## 9. A yes/no governing check prints no utilisation (C2-32), and the README governing line is the sheet's own text

The register printed `PASS u = 0.00` for a governing check that has no utilisation (a classification, a yes/no condition); it prints `PASS` or `FAIL` alone. The worked-example
README's "Governing check" line is written by the sheet's formatter (`CalcCheck.UtilisationText`): two decimals, rounded up when the check fails, nothing for a yes/no check.
Every README governing line that changed against the C-2 phase A line (outcome and name are unchanged in each):

| Example | Old | New |
|---|---|---|
| `building-services/bs.duct-sizing/extract-velocity-method` | PASS (utilisation 0.557) | PASS (utilisation 0.56) |
| `building-services/bs.duct-sizing/office-supply-equal-friction` | PASS (utilisation 0.884) | PASS (utilisation 0.88) |
| `building-services/bs.glaser/internal-insulation-no-vcl` | FAIL (utilisation 6.045) | FAIL (utilisation 6.05) |
| `building-services/bs.glaser/internal-insulation-with-vcl` | PASS (utilisation 0.673) | PASS (utilisation 0.67) |
| `building-services/bs.heat-exchanger/lthw-plate-hx-sizing` | PASS (utilisation 0.186) | PASS (utilisation 0.19) |
| `building-services/bs.insulation/chilled-water-pipe-condensation` | PASS (utilisation 0.799) | PASS (utilisation 0.80) |
| `building-services/bs.insulation/clad-hot-water-pipe-correlation` | PASS (utilisation 0.613) | PASS (utilisation 0.61) |
| `building-services/bs.pipe-expansion/copper-axial-bellows` | PASS (utilisation 0.612) | PASS (utilisation 0.61) |
| `building-services/bs.pipe-expansion/copper-offset-leg-too-short` | FAIL (utilisation 2.366) | FAIL (utilisation 2.37) |
| `building-services/bs.pipe-expansion/lthw-main-expansion-loop` | PASS (utilisation 0.857) | PASS (utilisation 0.86) |
| `building-services/bs.pipe-pump/chilled-water-riser-duty` | FAIL (utilisation 1.005) | FAIL (utilisation 1.01) |
| `building-services/bs.pipe-pump/oil-transfer-laminar` | PASS (utilisation 0.047) | PASS (utilisation 0.05) |
| `building-services/bs.room-heat-loss/semi-detached-living-room` | PASS (utilisation 0.994) | PASS (utilisation 0.99) |
| `building-services/bs.sealed-system/heat-pump-retrofit-55-45` | FAIL (utilisation 1.107) | FAIL (utilisation 1.11) |
| `electrical/elec.conductor-sizing/conveyor-motor-circuit` | PASS (utilisation 0.828) | PASS (utilisation 0.83) |
| `electrical/elec.containment/servo-cable-pull-three-bends` | PASS (utilisation 0.895) | PASS (utilisation 0.90) |
| `electrical/elec.enclosure-heat/outdoor-kiosk-cooling-unit` | FAIL (utilisation 11.044) | FAIL (utilisation 11.05) |
| `electrical/elec.enclosure-heat/wall-mounted-plc-panel` | FAIL (utilisation 3.190) | FAIL (utilisation 3.19) |
| `electrical/elec.fault-protection/workshop-radial-c16` | FAIL (utilisation —) | FAIL |
| `electrical/elec.load-budget/pump-skid-panel` | FAIL (utilisation 1.111) | FAIL (utilisation 1.12) |
| `electrical/elec.motor-sizing/ball-screw-pick-and-place` | PASS (utilisation 0.900) | PASS (utilisation 0.90) |
| `electrical/elec.motor-sizing/geared-index-table` | PASS (utilisation 0.934) | PASS (utilisation 0.93) |
| `electrical/elec.motor-start/pump-dol-500kva` | PASS (utilisation 0.935) | PASS (utilisation 0.93) |
| `electrical/elec.motor-start/pump-star-delta-weak-supply` | PASS (utilisation 0.847) | PASS (utilisation 0.85) |
| `electrical/elec.pcb-trace/motor-driver-supply-rail` | PASS (utilisation 0.363) | PASS (utilisation 0.36) |
| `electrical/elec.thermal-chain/three-stage-25W` | PASS (utilisation 0.735) | PASS (utilisation 0.74) |
| `electrical/elec.thermal-chain/three-stage-40W-fails` | FAIL (utilisation 1.176) | FAIL (utilisation 1.18) |
| `electrical/elec.voltage-drop/dc-battery-feed` | FAIL (utilisation 1.719) | FAIL (utilisation 1.72) |
| `electrical/elec.voltage-drop/three-phase-motor-feeder` | PASS (utilisation 0.840) | PASS (utilisation 0.84) |
| `fluid-power/fp.accumulator/clamp-circuit-bladder-accumulator` | PASS (utilisation 1.000) | PASS (utilisation 1.00) |
| `fluid-power/fp.cylinder/press-ram-125-90-pump-and-lines` | PASS (utilisation 0.968) | PASS (utilisation 0.97) |
| `fluid-power/fp.oring-gland/locking-cylinder-piston-seal` | PASS (utilisation 0.000) | PASS |
| `mechanical/mech.bearing-life-basic/ball-bearing-combined-95` | PASS (utilisation 0.728) | PASS (utilisation 0.73) |
| `mechanical/mech.bearing-life-basic/ball-bearing-radial` | PASS (utilisation 0.666) | PASS (utilisation 0.67) |
| `mechanical/mech.bearing-life/skf-6206-pump` | PASS (utilisation 0.659) | PASS (utilisation 0.66) |
| `mechanical/mech.bolt-group/end-plate-moment` | PASS (utilisation 0.833) | PASS (utilisation 0.83) |
| `mechanical/mech.bolt-group/four-bolt-bracket` | PASS (utilisation 0.911) | PASS (utilisation 0.91) |
| `mechanical/mech.bolted-joint/m12-88-flange` | PASS (utilisation 0.969) | PASS (utilisation 0.97) |
| `mechanical/mech.bolted-joint/m12-88-overloaded` | FAIL (utilisation —) | FAIL |
| `mechanical/mech.fatigue-miner/detail-71-spectrum` | PASS (utilisation 0.469) | PASS (utilisation 0.47) |
| `mechanical/mech.fatigue-miner/slope-5-endurance-limit` | FAIL (utilisation 1.433) | FAIL (utilisation 1.44) |
| `mechanical/mech.iso286-fit/h7-g6-25mm` | PASS (utilisation 0.000) | PASS |
| `mechanical/mech.iso286-fit/h7-p6-50mm` | PASS (utilisation 0.000) | PASS |
| `mechanical/mech.lifting-lug/lug-120kN-fails` | FAIL (utilisation 1.600) | FAIL (utilisation 1.60) |
| `mechanical/mech.lifting-lug/lug-50kN-passes` | PASS (utilisation 0.490) | PASS (utilisation 0.49) |
| `mechanical/mech.material-margin/bracket-steel-margin` | PASS (utilisation 0.600) | PASS (utilisation 0.60) |
| `mechanical/mech.material-margin/s355-yield-only` | FAIL (utilisation 1.056) | FAIL (utilisation 1.06) |
| `mechanical/mech.mechanism/carton-tucker-crank-rocker` | PASS (utilisation 0.826) | PASS (utilisation 0.83) |
| `mechanical/mech.mechanism/indexing-cam-offset-roller` | PASS (utilisation 0.887) | PASS (utilisation 0.89) |
| `mechanical/mech.press-fit/hollow-shaft` | PASS (utilisation 0.373) | PASS (utilisation 0.37) |
| `mechanical/mech.press-fit/solid-steel-shaft` | PASS (utilisation 0.408) | PASS (utilisation 0.41) |
| `mechanical/mech.shaft-stress/shaft-25mm-shoulder` | PASS (utilisation 0.811) | PASS (utilisation 0.81) |
| `mechanical/mech.shaft-stress/shaft-40mm` | PASS (utilisation 0.858) | PASS (utilisation 0.86) |
| `mechanical/mech.shaft/gearbox-input-shaft` | PASS (utilisation 0.896) | PASS (utilisation 0.90) |
| `mechanical/mech.snap-fit/enclosure-lid-hook` | PASS (utilisation 0.187) | PASS (utilisation 0.19) |
| `mechanical/mech.spring/plunger-return-spring` | PASS (utilisation 0.880) | PASS (utilisation 0.88) |
| `mechanical/mech.thermal-expansion/bar-elastic-restraint` | PASS (utilisation 0.640) | PASS (utilisation 0.64) |
| `mechanical/mech.thermal-expansion/bar-with-gap` | PASS (utilisation 0.627) | PASS (utilisation 0.63) |
| `mechanical/mech.thick-cylinder/external-pressure-closed` | PASS (utilisation 0.624) | PASS (utilisation 0.62) |
| `mechanical/mech.thick-cylinder/internal-pressure-closed` | PASS (utilisation 0.577) | PASS (utilisation 0.58) |
| `mechanical/mech.thin-vessel/receiver-2MPa` | PASS (utilisation 0.968) | PASS (utilisation 0.97) |
| `mechanical/mech.thin-vessel/vessel-3MPa-thin` | FAIL (utilisation 1.201) | FAIL (utilisation 1.21) |
| `mechanical/mech.tolerance-stack/bearing-housing-gap` | PASS (utilisation 0.564) | PASS (utilisation 0.56) |
| `mechanical/mech.tolerance-stack/shouldered-screw-gap` | FAIL (utilisation —) | FAIL |
| `mechanical/mech.weld-group/bracket-c-weld` | PASS (utilisation 0.305) | PASS (utilisation 0.31) |
| `structural/struct.base-plate/portal-column-base-uc254` | PASS (utilisation 0.558) | PASS (utilisation 0.56) |
| `structural/struct.column-buckling/rhs-column-curve-c` | PASS (utilisation 0.561) | PASS (utilisation 0.56) |
| `structural/struct.column-buckling/slender-strut-curve-b` | FAIL (utilisation 1.069) | FAIL (utilisation 1.07) |
| `structural/struct.connection-ec3/beam-splice-m20` | PASS (utilisation 0.553) | PASS (utilisation 0.55) |
| `structural/struct.en1993-member/ub-beam-bending-shear` | PASS (utilisation 0.700) | PASS (utilisation 0.70) |
| `structural/struct.en1993-member/uc-column-axial-moment` | FAIL (utilisation 1.044) | FAIL (utilisation 1.05) |
| `structural/struct.fillet-weld/normal-load-overloaded` | FAIL (utilisation 1.193) | FAIL (utilisation 1.20) |
| `structural/struct.fillet-weld/s355-lap-shear` | PASS (utilisation 0.596) | PASS (utilisation 0.60) |
| `structural/struct.lifting/lifting-beam-lug-pair` | PASS (utilisation 0.572) | PASS (utilisation 0.57) |
| `structural/struct.ltb-interaction/ub-457x191x98-beam-column` | PASS (utilisation 0.931) | PASS (utilisation 0.93) |
| `structural/struct.plate-bending/enclosure-lid-aluminium` | PASS (utilisation 0.737) | PASS (utilisation 0.74) |

The `steel-support` pack's purpose line states its `ltb` assumption (C2-37); no other pack README changes.

## 10. Not changed

The README expectation tables of the worked examples hold the same numbers; the lines that do change are the governing-check line (section 9, all of them listed) and the inputs
table of three examples that now state an input the sheet no longer assumes (`ltb` of `struct.en1993-member/ub-beam-bending-shear` and `uc-column-axial-moment`, `w_masses` of
`mech.shaft/gearbox-input-shaft`; C2-37). The pack READMEs change only where a check was added by item M1 (`4 of 4 PASS` becomes
`5 of 5 PASS` for `elec.motor-sizing` and `mech.shaft`, and for `mech.spring` in the mechanism-review pack), and the voltage-drop, shaft and press-fit hand arithmetic of their
spec pages follows the templates it describes.
