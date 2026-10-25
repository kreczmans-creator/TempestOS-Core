# Worked examples — v0.26.0

87 worked examples (three for one of the 54 worksheet templates, two for thirty-one, one for each of the other twenty-two) and 13 calculation packs (the bracket assembly and the twelve linked chains). The 54 templates carry 303 known-answer cases between them (the eleven templates of the legacy replacement carry every legacy test vector as a known-answer case). Each example folder holds `README.md` (the problem, the inputs and the expected results with their source), `inputs.json` (the inputs as the worksheet reads them), `sheet.txt` (the rendered sheet) and `sheet.pdf` (the Desktop's PDF). A pack folder holds the pack's issue sheet, each step's issued sheet, a README and an `inputs.json` that records every step's inputs (one object keyed by step); that file is a record of what was issued, not one worksheet's import, so load a pack step by copying that step's list out of it.

**How to use one.** In the Desktop open Modules → Calculations → Worksheets (or the discipline's own Calculations node) and pick the template named in the table; the form opens on the template's declared defaults only. A table or a required value with no default (a frame's nodes, a shaft's segments, a base plate's `m` and `l_eff`) starts empty, so a new sheet for many templates opens blocked with a message saying what to enter next. Enter the values from the example's README (every input lists its value, unit and source); the form recalculates at once, *Show working* is the `sheet.txt` text and the issued sheet is `sheet.pdf`. `inputs.json` is the same input set in the exchange form the Calc API reads: headless, `CalcInputJson.Read` → `CalcRunner.Run` → `CalcSheetBuilder.Build` → `CalcSheetTextRenderer.Render` reproduces the sheet (`tests/Tempest.Calc.Tests.Calculators/WorkedExamples.cs`). To load an example into the form, copy the text of its `inputs.json` to the clipboard and press *Import inputs (CSV/JSON)*; the form re-seeds from it (table inputs included) and recalculates. *Export inputs (CSV)* copies the current inputs back out in the CSV the same button reads.

Eighteen examples end on a **FAIL** governing check on purpose: they show how an overloaded or undersized design reads on the sheet. The verification footers in the stored sheets carry fixed placeholder values (hash `0000…`, engine `engine`); a sheet rendered in the product carries the real hash, engine version and test-run time.

| Discipline | Template | Example | Governing check |
|---|---|---|---|
| building-services | `bs.duct-sizing` | [extract-velocity-method](building-services/bs.duct-sizing/extract-velocity-method/README.md) | PASS — Velocity ≤ limit |
| building-services | `bs.duct-sizing` | [office-supply-equal-friction](building-services/bs.duct-sizing/office-supply-equal-friction/README.md) | PASS — Velocity ≤ limit |
| building-services | `bs.glaser` | [internal-insulation-no-vcl](building-services/bs.glaser/internal-insulation-no-vcl/README.md) | FAIL — Peak condensate on an interface against its limit |
| building-services | `bs.glaser` | [internal-insulation-with-vcl](building-services/bs.glaser/internal-insulation-with-vcl/README.md) | PASS — Surface condensation (f_Rsi against f_Rsi,min) |
| building-services | `bs.heat-exchanger` | [lthw-plate-hx-sizing](building-services/bs.heat-exchanger/lthw-plate-hx-sizing/README.md) | PASS — Approach against minimum |
| building-services | `bs.insulation` | [chilled-water-pipe-condensation](building-services/bs.insulation/chilled-water-pipe-condensation/README.md) | PASS — Chosen thickness against the heat-gain limit |
| building-services | `bs.insulation` | [clad-hot-water-pipe-correlation](building-services/bs.insulation/clad-hot-water-pipe-correlation/README.md) | PASS — Chosen thickness against the heat-loss limit |
| building-services | `bs.pipe-expansion` | [copper-axial-bellows](building-services/bs.pipe-expansion/copper-axial-bellows/README.md) | PASS — Movement against the rated axial movement of the bellows |
| building-services | `bs.pipe-expansion` | [copper-offset-leg-too-short](building-services/bs.pipe-expansion/copper-offset-leg-too-short/README.md) | FAIL — Screening OD·Y/(L − U)² ≤ K1 |
| building-services | `bs.pipe-expansion` | [lthw-main-expansion-loop](building-services/bs.pipe-expansion/lthw-main-expansion-loop/README.md) | PASS — Available leg against the required leg |
| building-services | `bs.pipe-pump` | [chilled-water-riser-duty](building-services/bs.pipe-pump/chilled-water-riser-duty/README.md) | FAIL — Velocity ≤ limit |
| building-services | `bs.pipe-pump` | [oil-transfer-laminar](building-services/bs.pipe-pump/oil-transfer-laminar/README.md) | PASS — Velocity ≤ limit |
| building-services | `bs.plane-wall` | [double-pane-window](building-services/bs.plane-wall/double-pane-window/README.md) | no pass/fail checks |
| building-services | `bs.plane-wall` | [single-pane-window](building-services/bs.plane-wall/single-pane-window/README.md) | no pass/fail checks |
| building-services | `bs.psychrometrics` | [cooling-coil-summer](building-services/bs.psychrometrics/cooling-coil-summer/README.md) | no pass/fail checks |
| building-services | `bs.psychrometrics` | [winter-mixing-and-steam](building-services/bs.psychrometrics/winter-mixing-and-steam/README.md) | no pass/fail checks |
| building-services | `bs.room-heat-loss` | [semi-detached-living-room](building-services/bs.room-heat-loss/semi-detached-living-room/README.md) | PASS — U-value of Party wall against its target (1.2 W/m²K) |
| building-services | `bs.sealed-system` | [heat-pump-retrofit-55-45](building-services/bs.sealed-system/heat-pump-retrofit-55-45/README.md) | FAIL — Selected emitters against room heat (governing room) |
| electrical | `elec.conductor-sizing` | [conveyor-motor-circuit](electrical/elec.conductor-sizing/conveyor-motor-circuit/README.md) | PASS — Design current within device rating (I_b ≤ I_n) |
| electrical | `elec.containment` | [servo-cable-pull-three-bends](electrical/elec.containment/servo-cable-pull-three-bends/README.md) | PASS — Fill against the space factor |
| electrical | `elec.enclosure-heat` | [outdoor-kiosk-cooling-unit](electrical/elec.enclosure-heat/outdoor-kiosk-cooling-unit/README.md) | FAIL — Natural dissipation holds T_max |
| electrical | `elec.enclosure-heat` | [wall-mounted-plc-panel](electrical/elec.enclosure-heat/wall-mounted-plc-panel/README.md) | FAIL — Natural dissipation holds T_max |
| electrical | `elec.fault-protection` | [workshop-radial-c16](electrical/elec.fault-protection/workshop-radial-c16/README.md) | PASS — Loop impedance within maximum (disconnection in time) |
| electrical | `elec.load-budget` | [pump-skid-panel](electrical/elec.load-budget/pump-skid-panel/README.md) | FAIL — Starting peak against transformer overload |
| electrical | `elec.motor-sizing` | [ball-screw-pick-and-place](electrical/elec.motor-sizing/ball-screw-pick-and-place/README.md) | PASS — RMS torque SF·T_rms ≤ T_rated |
| electrical | `elec.motor-sizing` | [geared-index-table](electrical/elec.motor-sizing/geared-index-table/README.md) | PASS — RMS torque SF·T_rms ≤ T_rated |
| electrical | `elec.motor-start` | [pump-dol-500kva](electrical/elec.motor-start/pump-dol-500kva/README.md) | PASS — Point-of-common-coupling dip within the limit |
| electrical | `elec.motor-start` | [pump-star-delta-weak-supply](electrical/elec.motor-start/pump-star-delta-weak-supply/README.md) | PASS — Point-of-common-coupling dip within the limit |
| electrical | `elec.pcb-trace` | [motor-driver-supply-rail](electrical/elec.pcb-trace/motor-driver-supply-rail/README.md) | PASS — Trace width as drawn against the minimum |
| electrical | `elec.thermal-chain` | [three-stage-25W](electrical/elec.thermal-chain/three-stage-25W/README.md) | PASS — Source temperature T_1 ≤ T_max |
| electrical | `elec.thermal-chain` | [three-stage-40W-fails](electrical/elec.thermal-chain/three-stage-40W-fails/README.md) | FAIL — Source temperature T_1 ≤ T_max |
| electrical | `elec.voltage-drop` | [dc-battery-feed](electrical/elec.voltage-drop/dc-battery-feed/README.md) | FAIL — Voltage drop ≤ limit |
| electrical | `elec.voltage-drop` | [three-phase-motor-feeder](electrical/elec.voltage-drop/three-phase-motor-feeder/README.md) | PASS — I ≤ I_z |
| fluid-power | `fp.accumulator` | [clamp-circuit-bladder-accumulator](fluid-power/fp.accumulator/clamp-circuit-bladder-accumulator/README.md) | PASS — Pressure ratio within the p2/p0 rule |
| fluid-power | `fp.cylinder` | [press-ram-125-90-pump-and-lines](fluid-power/fp.cylinder/press-ram-125-90-pump-and-lines/README.md) | PASS — Return line velocity within its limit |
| fluid-power | `fp.oring-gland` | [locking-cylinder-piston-seal](fluid-power/fp.oring-gland/locking-cylinder-piston-seal/README.md) | PASS — Squeeze maximum within the band |
| mechanical | `mech.bearing-life` | [skf-6206-pump](mechanical/mech.bearing-life/skf-6206-pump/README.md) | PASS — Modified rating life against required life |
| mechanical | `mech.bearing-life-basic` | [ball-bearing-combined-95](mechanical/mech.bearing-life-basic/ball-bearing-combined-95/README.md) | PASS — Modified life against required life |
| mechanical | `mech.bearing-life-basic` | [ball-bearing-radial](mechanical/mech.bearing-life-basic/ball-bearing-radial/README.md) | PASS — Modified life against required life |
| mechanical | `mech.bolt-group` | [end-plate-moment](mechanical/mech.bolt-group/end-plate-moment/README.md) | PASS — Circular shear–tension interaction (governing bolt) |
| mechanical | `mech.bolt-group` | [four-bolt-bracket](mechanical/mech.bolt-group/four-bolt-bracket/README.md) | PASS — Circular shear–tension interaction (governing bolt) |
| mechanical | `mech.bolted-joint` | [m12-88-flange](mechanical/mech.bolted-joint/m12-88-flange/README.md) | PASS — Yield in the working state |
| mechanical | `mech.bolted-joint` | [m12-88-overloaded](mechanical/mech.bolted-joint/m12-88-overloaded/README.md) | FAIL — No separation |
| mechanical | `mech.fatigue-miner` | [detail-71-spectrum](mechanical/mech.fatigue-miner/detail-71-spectrum/README.md) | PASS — Cumulative damage D ≤ 1 |
| mechanical | `mech.fatigue-miner` | [slope-5-endurance-limit](mechanical/mech.fatigue-miner/slope-5-endurance-limit/README.md) | FAIL — Cumulative damage D ≤ 1 |
| mechanical | `mech.iso286-fit` | [h7-g6-25mm](mechanical/mech.iso286-fit/h7-g6-25mm/README.md) | PASS — Fit classified |
| mechanical | `mech.iso286-fit` | [h7-p6-50mm](mechanical/mech.iso286-fit/h7-p6-50mm/README.md) | PASS — Fit classified |
| mechanical | `mech.lifting-lug` | [lug-120kN-fails](mechanical/mech.lifting-lug/lug-120kN-fails/README.md) | FAIL — Bearing |
| mechanical | `mech.lifting-lug` | [lug-50kN-passes](mechanical/mech.lifting-lug/lug-50kN-passes/README.md) | PASS — Pin bending |
| mechanical | `mech.material-margin` | [bracket-steel-margin](mechanical/mech.material-margin/bracket-steel-margin/README.md) | PASS — Factor of safety against the required factor |
| mechanical | `mech.material-margin` | [s355-yield-only](mechanical/mech.material-margin/s355-yield-only/README.md) | FAIL — Factor of safety against the required factor |
| mechanical | `mech.mechanism` | [carton-tucker-crank-rocker](mechanical/mech.mechanism/carton-tucker-crank-rocker/README.md) | PASS — Transmission angle μ_min at or above the required minimum |
| mechanical | `mech.mechanism` | [indexing-cam-offset-roller](mechanical/mech.mechanism/indexing-cam-offset-roller/README.md) | PASS — Pressure angle φ_max within the allowed maximum |
| mechanical | `mech.press-fit` | [hollow-shaft](mechanical/mech.press-fit/hollow-shaft/README.md) | PASS — Hub yield |
| mechanical | `mech.press-fit` | [solid-steel-shaft](mechanical/mech.press-fit/solid-steel-shaft/README.md) | PASS — Hub yield |
| mechanical | `mech.shaft` | [gearbox-input-shaft](mechanical/mech.shaft/gearbox-input-shaft/README.md) | PASS — Slope at the bearings against limit |
| mechanical | `mech.shaft-stress` | [shaft-25mm-shoulder](mechanical/mech.shaft-stress/shaft-25mm-shoulder/README.md) | PASS — Factor of safety against the required factor |
| mechanical | `mech.shaft-stress` | [shaft-40mm](mechanical/mech.shaft-stress/shaft-40mm/README.md) | PASS — Factor of safety against the required factor |
| mechanical | `mech.snap-fit` | [enclosure-lid-hook](mechanical/mech.snap-fit/enclosure-lid-hook/README.md) | PASS — Strain at the critical section against the permissible strain |
| mechanical | `mech.spring` | [plunger-return-spring](mechanical/mech.spring/plunger-return-spring/README.md) | PASS — Static shear stress at solid length |
| mechanical | `mech.thermal-expansion` | [bar-elastic-restraint](mechanical/mech.thermal-expansion/bar-elastic-restraint/README.md) | PASS — Thermal stress against allowable |
| mechanical | `mech.thermal-expansion` | [bar-with-gap](mechanical/mech.thermal-expansion/bar-with-gap/README.md) | PASS — Thermal stress against allowable |
| mechanical | `mech.thick-cylinder` | [external-pressure-closed](mechanical/mech.thick-cylinder/external-pressure-closed/README.md) | PASS — von Mises against allowable |
| mechanical | `mech.thick-cylinder` | [internal-pressure-closed](mechanical/mech.thick-cylinder/internal-pressure-closed/README.md) | PASS — von Mises against allowable |
| mechanical | `mech.thin-vessel` | [receiver-2MPa](mechanical/mech.thin-vessel/receiver-2MPa/README.md) | PASS — Provided thickness against required |
| mechanical | `mech.thin-vessel` | [vessel-3MPa-thin](mechanical/mech.thin-vessel/vessel-3MPa-thin/README.md) | FAIL — Provided thickness against required |
| mechanical | `mech.tolerance-stack` | [bearing-housing-gap](mechanical/mech.tolerance-stack/bearing-housing-gap/README.md) | PASS — C_pk against target |
| mechanical | `mech.tolerance-stack` | [shouldered-screw-gap](mechanical/mech.tolerance-stack/shouldered-screw-gap/README.md) | FAIL — Worst case within requirement |
| mechanical | `mech.weld-group` | [bracket-c-weld](mechanical/mech.weld-group/bracket-c-weld/README.md) | PASS — Throat stress against design strength |
| structural | `struct.base-plate` | [portal-column-base-uc254](structural/struct.base-plate/portal-column-base-uc254/README.md) | PASS — Base plate tension T-stub (bending at the bolts) |
| structural | `struct.beam-formulas` | [propped-cantilever-udl](structural/struct.beam-formulas/propped-cantilever-udl/README.md) | no pass/fail checks |
| structural | `struct.beam-formulas` | [ss-point-offcentre](structural/struct.beam-formulas/ss-point-offcentre/README.md) | no pass/fail checks |
| structural | `struct.column-buckling` | [rhs-column-curve-c](structural/struct.column-buckling/rhs-column-curve-c/README.md) | PASS — Axial load against compression resistance |
| structural | `struct.column-buckling` | [slender-strut-curve-b](structural/struct.column-buckling/slender-strut-curve-b/README.md) | FAIL — Axial load against compression resistance |
| structural | `struct.connection-ec3` | [beam-splice-m20](structural/struct.connection-ec3/beam-splice-m20/README.md) | PASS — Bolt bearing |
| structural | `struct.en1993-member` | [ub-beam-bending-shear](structural/struct.en1993-member/ub-beam-bending-shear/README.md) | PASS — Bending y M_y,Ed ≤ M_y,c,Rd |
| structural | `struct.en1993-member` | [uc-column-axial-moment](structural/struct.en1993-member/uc-column-axial-moment/README.md) | FAIL — Member interaction (simplified) |
| structural | `struct.fillet-weld` | [normal-load-overloaded](structural/struct.fillet-weld/normal-load-overloaded/README.md) | FAIL — Weld resistance |
| structural | `struct.fillet-weld` | [s355-lap-shear](structural/struct.fillet-weld/s355-lap-shear/README.md) | PASS — Weld resistance |
| structural | `struct.frame-2d` | [pinned-portal-gravity-wind](structural/struct.frame-2d/pinned-portal-gravity-wind/README.md) | no pass/fail checks |
| structural | `struct.frame-2d` | [two-span-continuous-beam](structural/struct.frame-2d/two-span-continuous-beam/README.md) | no pass/fail checks |
| structural | `struct.lifting` | [lifting-beam-lug-pair](structural/struct.lifting/lifting-beam-lug-pair/README.md) | PASS — BTH-1 bearing (feature flag `Lifting`, off by default) |
| structural | `struct.load-combinations` | [office-floor-beam-uk-na](structural/struct.load-combinations/office-floor-beam-uk-na/README.md) | no pass/fail checks |
| structural | `struct.ltb-interaction` | [ub-457x191x98-beam-column](structural/struct.ltb-interaction/ub-457x191x98-beam-column/README.md) | PASS — Interaction, equation 6.62 |
| structural | `struct.plate-bending` | [enclosure-lid-aluminium](structural/struct.plate-bending/enclosure-lid-aluminium/README.md) | PASS — Deflection w_max ≤ span/ratio |
| structural | `struct.section-properties` | [fabricated-box-girder](structural/struct.section-properties/fabricated-box-girder/README.md) | no pass/fail checks |
| structural | `struct.section-properties` | [ub-457x191x98](structural/struct.section-properties/ub-457x191x98/README.md) | no pass/fail checks |

## Calculation packs

- [bracket-assembly-m2-m3-s1](packs/bracket-assembly-m2-m3-s1/README.md) — three independent calculations under one issue sheet.
- [drive-train](packs/drive-train/README.md) — E1 → M4 → M5 → M2 → M3: motor, shaft, bearing, seat fit, coupling flange bolts.
- [motor-circuit](packs/motor-circuit/README.md) — E1 → E6 → E4 → E3 / E5 → E2: motor, load budget, cable (synthetic tables), voltage drop, fault protection, enclosure heat.
- [wet-heating](packs/wet-heating/README.md) — B4 (two rooms) → B6 → B1 → E1: room heat losses, sealed system, pipework and pump, circulator motor.
- [cooling](packs/cooling/README.md) — E6 → E2 → B5 → B1: panel heat, enclosure, cooling-water exchanger, pipework and pump.
- [steel-support](packs/steel-support/README.md) — S5 → S2 → S3; M3 bolt group → S4 (arm joint); S4 (holding-down bolts) → S6: load combinations, frame, column check, arm joint, base plate, and a placeholder for the PROFIS anchorage report.
- [hydraulic-power-unit](packs/hydraulic-power-unit/README.md) — F1 → F2, B1, E1; E1 → E4, E9; E4 → E3, E5, E9; E5 → E9: cylinder and pump circuit, emergency-stroke accumulator, pressure line (laminar oil), pump drive motor, motor supply cable (synthetic tables), voltage drop, fault protection and the pump motor's star-delta start (§B7 chain 1).
- [mechanism-review](packs/mechanism-review/README.md) — M9 → M7, M4, M5, E1; M4 → M5; E1 → E4: tamper cam, follower return spring, cam shaft, shaft bearing, drive motor and its supply cable (§B7 chain 4).
- [actuated-linkage](packs/actuated-linkage/README.md) — M9 → F1 → F2: ejector slider-crank, the cylinder that replaces its crank, and its emergency-stroke accumulator (§B7 chain 5).
- [envelope-moisture](packs/envelope-moisture/README.md) — B4 → B7 ← B2: room heat loss and room air state into the wall's Glaser condensation check (§B7 chain 7).
- [cold-pipe](packs/cold-pipe/README.md) — B1 → B9 ← B2: chilled-water pipe and plant room air into the pipe insulation (§B7 chain 8).
- [pipework-supports](packs/pipework-supports/README.md) — B8 → S2 → S6: pipe expansion and anchor force, anchor post frame, base plate (§B7 chain 9).
- [electronics-enclosure](packs/electronics-enclosure/README.md) — E7 → E6 → E2 → M8, S9: power trace, board load budget, enclosure temperature, cover latches and cover plate (§B7 chain 10).

Each chain pack is one issued pack: every step is linked to the one above it (the README lists every link with the value it carries), the pack was checked once and issued under one reference, and the folder holds the pack's issue sheet, each step's issued sheet and `inputs.json`. Project, drawing and specification numbers in them are invented. Regenerate them with `TEMPEST_WRITE_WORKED_EXAMPLES=1 dotnet test tests/Tempest.Calc.Tests.Library --filter ChainPackWorkedExampleTests`.

Regenerate every example with `TEMPEST_WRITE_WORKED_EXAMPLES=1 dotnet test tests/Tempest.Calc.Tests.Calculators` (inputs, README expectations, sheet text) and `TEMPEST_WRITE_WORKED_EXAMPLES=1 dotnet test tests/Tempest.Desktop.Tests --filter CalcSheets` (PDFs).
