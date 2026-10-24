# Worked examples — v0.26.0

32 worked examples (two per worksheet template) and 6 calculation packs (the bracket assembly and the five linked chains). Each folder holds `README.md` (the problem, the inputs and the expected results with their source), `inputs.json` (the inputs as the worksheet reads them), `sheet.txt` (the rendered sheet) and `sheet.pdf` (the Desktop's PDF).

**How to use one.** In the Desktop open Modules → Calculations → Worksheets (or the discipline's own Calculations node) and pick the template named in the table; the form opens on the template's defaults. Enter the values from the example's README (every input lists its value, unit and source); the form recalculates at once, *Show working* is the `sheet.txt` text and the issued sheet is `sheet.pdf`. `inputs.json` is the same input set in the exchange form the Calc API reads: headless, `CalcInputJson.Read` → `CalcRunner.Run` → `CalcSheetBuilder.Build` → `CalcSheetTextRenderer.Render` reproduces the sheet (`tests/Tempest.Calc.Tests.Calculators/WorkedExamples.cs`). To load an example into the form, copy the text of its `inputs.json` to the clipboard and press *Import inputs (CSV/JSON)*; the form re-seeds from it (table inputs included) and recalculates. *Export inputs (CSV)* copies the current inputs back out in the CSV the same button reads.

Seven examples end on a **FAIL** governing check on purpose: they show how an overloaded or undersized design reads on the sheet. The verification footers in the stored sheets carry fixed placeholder values (hash `0000…`, engine `engine`); a sheet rendered in the product carries the real hash, engine version and test-run time.

| Discipline | Template | Example | Governing check |
|---|---|---|---|
| building-services | `bs.duct-sizing` | [extract-velocity-method](building-services/bs.duct-sizing/extract-velocity-method/README.md) | PASS — Velocity ≤ limit |
| building-services | `bs.duct-sizing` | [office-supply-equal-friction](building-services/bs.duct-sizing/office-supply-equal-friction/README.md) | PASS — Velocity ≤ limit |
| building-services | `bs.pipe-pump` | [chilled-water-riser-duty](building-services/bs.pipe-pump/chilled-water-riser-duty/README.md) | FAIL — Velocity ≤ limit |
| building-services | `bs.pipe-pump` | [oil-transfer-laminar](building-services/bs.pipe-pump/oil-transfer-laminar/README.md) | PASS — Velocity ≤ limit |
| building-services | `bs.psychrometrics` | [cooling-coil-summer](building-services/bs.psychrometrics/cooling-coil-summer/README.md) | no pass/fail checks |
| building-services | `bs.psychrometrics` | [winter-mixing-and-steam](building-services/bs.psychrometrics/winter-mixing-and-steam/README.md) | no pass/fail checks |
| electrical | `elec.enclosure-heat` | [outdoor-kiosk-cooling-unit](electrical/elec.enclosure-heat/outdoor-kiosk-cooling-unit/README.md) | FAIL — Natural dissipation holds T_max |
| electrical | `elec.enclosure-heat` | [wall-mounted-plc-panel](electrical/elec.enclosure-heat/wall-mounted-plc-panel/README.md) | FAIL — Natural dissipation holds T_max |
| electrical | `elec.motor-sizing` | [ball-screw-pick-and-place](electrical/elec.motor-sizing/ball-screw-pick-and-place/README.md) | PASS — RMS torque SF·T_rms ≤ T_rated |
| electrical | `elec.motor-sizing` | [geared-index-table](electrical/elec.motor-sizing/geared-index-table/README.md) | PASS — RMS torque SF·T_rms ≤ T_rated |
| electrical | `elec.voltage-drop` | [dc-battery-feed](electrical/elec.voltage-drop/dc-battery-feed/README.md) | FAIL — Voltage drop ≤ limit |
| electrical | `elec.voltage-drop` | [three-phase-motor-feeder](electrical/elec.voltage-drop/three-phase-motor-feeder/README.md) | PASS — I ≤ I_z |
| mechanical | `mech.bolt-group` | [end-plate-moment](mechanical/mech.bolt-group/end-plate-moment/README.md) | PASS — Circular shear–tension interaction (governing bolt) |
| mechanical | `mech.bolt-group` | [four-bolt-bracket](mechanical/mech.bolt-group/four-bolt-bracket/README.md) | PASS — Circular shear–tension interaction (governing bolt) |
| mechanical | `mech.bolted-joint` | [m12-88-flange](mechanical/mech.bolted-joint/m12-88-flange/README.md) | PASS — Yield in the working state |
| mechanical | `mech.bolted-joint` | [m12-88-overloaded](mechanical/mech.bolted-joint/m12-88-overloaded/README.md) | FAIL — No separation |
| mechanical | `mech.iso286-fit` | [h7-g6-25mm](mechanical/mech.iso286-fit/h7-g6-25mm/README.md) | PASS — Fit classified |
| mechanical | `mech.iso286-fit` | [h7-p6-50mm](mechanical/mech.iso286-fit/h7-p6-50mm/README.md) | PASS — Fit classified |
| mechanical | `mech.press-fit` | [hollow-shaft](mechanical/mech.press-fit/hollow-shaft/README.md) | PASS — Hub yield |
| mechanical | `mech.press-fit` | [solid-steel-shaft](mechanical/mech.press-fit/solid-steel-shaft/README.md) | PASS — Hub yield |
| mechanical | `mech.thick-cylinder` | [external-pressure-closed](mechanical/mech.thick-cylinder/external-pressure-closed/README.md) | PASS — von Mises against allowable |
| mechanical | `mech.thick-cylinder` | [internal-pressure-closed](mechanical/mech.thick-cylinder/internal-pressure-closed/README.md) | PASS — von Mises against allowable |
| mechanical | `mech.tolerance-stack` | [bearing-housing-gap](mechanical/mech.tolerance-stack/bearing-housing-gap/README.md) | PASS — C_pk against target |
| mechanical | `mech.tolerance-stack` | [shouldered-screw-gap](mechanical/mech.tolerance-stack/shouldered-screw-gap/README.md) | FAIL — Worst case within requirement |
| structural | `struct.beam-formulas` | [propped-cantilever-udl](structural/struct.beam-formulas/propped-cantilever-udl/README.md) | no pass/fail checks |
| structural | `struct.beam-formulas` | [ss-point-offcentre](structural/struct.beam-formulas/ss-point-offcentre/README.md) | no pass/fail checks |
| structural | `struct.en1993-member` | [ub-beam-bending-shear](structural/struct.en1993-member/ub-beam-bending-shear/README.md) | PASS — Bending y M_y,Ed ≤ M_y,c,Rd |
| structural | `struct.en1993-member` | [uc-column-axial-moment](structural/struct.en1993-member/uc-column-axial-moment/README.md) | FAIL — Member interaction (simplified) |
| structural | `struct.frame-2d` | [pinned-portal-gravity-wind](structural/struct.frame-2d/pinned-portal-gravity-wind/README.md) | no pass/fail checks |
| structural | `struct.frame-2d` | [two-span-continuous-beam](structural/struct.frame-2d/two-span-continuous-beam/README.md) | no pass/fail checks |
| structural | `struct.section-properties` | [fabricated-box-girder](structural/struct.section-properties/fabricated-box-girder/README.md) | no pass/fail checks |
| structural | `struct.section-properties` | [ub-457x191x98](structural/struct.section-properties/ub-457x191x98/README.md) | no pass/fail checks |

## Calculation packs

- [bracket-assembly-m2-m3-s1](packs/bracket-assembly-m2-m3-s1/README.md) — three independent calculations under one issue sheet.
- [drive-train](packs/drive-train/README.md) — E1 → M4 → M5 → M2 → M3: motor, shaft, bearing, seat fit, coupling flange bolts.
- [motor-circuit](packs/motor-circuit/README.md) — E1 → E6 → E4 → E3 / E5 → E2: motor, load budget, cable (synthetic tables), voltage drop, fault protection, enclosure heat.
- [wet-heating](packs/wet-heating/README.md) — B4 (two rooms) → B6 → B1 → E1: room heat losses, sealed system, pipework and pump, circulator motor.
- [cooling](packs/cooling/README.md) — E6 → E2 → B5 → B1: panel heat, enclosure, cooling-water exchanger, pipework and pump.
- [steel-support](packs/steel-support/README.md) — S5 → S2 → S3; M3 bolt group → S4 (arm joint); S4 (holding-down bolts) → S6: load combinations, frame, column check, arm joint, base plate, and a placeholder for the PROFIS anchorage report.

Each chain pack is one issued pack: every step is linked to the one above it (the README lists every link with the value it carries), the pack was checked once and issued under one reference, and the folder holds the pack's issue sheet, each step's issued sheet and `inputs.json`. Project, drawing and specification numbers in them are invented. Regenerate them with `TEMPEST_WRITE_WORKED_EXAMPLES=1 dotnet test tests/Tempest.Calc.Tests.Library --filter ChainPackWorkedExampleTests`.

Regenerate every example with `TEMPEST_WRITE_WORKED_EXAMPLES=1 dotnet test tests/Tempest.Calc.Tests.Calculators` (inputs, README expectations, sheet text) and `TEMPEST_WRITE_WORKED_EXAMPLES=1 dotnet test tests/Tempest.Desktop.Tests --filter CalcSheets` (PDFs).
