# Demo — Hydraulic Press Cell (the seeded demonstration project)

**In plain terms.** One invented project, "Demo — Hydraulic Press Cell", that holds every linked chain this build ships, already built and linked, so you can open a calculation, change a figure and watch the chain go stale, update and recheck, without first making up a project. It is made only when you ask, it says DEMO on every sheet, and it can be removed again without touching anything else you have.

## Install and remove

1. Open **Settings** (the system page, at the foot of the left rail) and find the **Demo project** section.
2. **Install demo project** asks first, in the same confirmation window "Switch person" uses: *Add the project 'Demo — Hydraulic Press Cell' to this database?* Nothing is made until you answer Install. It takes under a minute; the section shows what it is making.
3. When it finishes the status line says how many calculations and packs it made (60 calculations in 13 packs), and the project is in the Projects area with the number `DEMO0-PRESS1`.
4. **Remove demo project** asks first as well (*Remove the project … and everything in it, including anything you added to it? No other project is touched.*). It is refused while the demo is the open project (close it first) and when any calculation or pack in it has been issued (see below).

Installing is refused, with a sentence, when a project with the number `DEMO0-PRESS1` is already live: the demo itself, or one of yours that happens to hold the number (that one is never touched). A failed install removes what it made and says why. **Nothing installs by itself**: not at start-up, not when a database is empty, not when Settings is opened.

## The story

A hydraulic press cell, invented. A 100/70 × 500 mm press cylinder at 160 bar, fed by a 28 cm³/rev pump on an 18 kW-class motor, with an emergency-stroke accumulator, a pressure line, a supply cable and a star-delta start. Around it: the infeed conveyor's head drive, a servo transfer axis and its panel, a part ejector, a blank-stack tamper, a support stand for the control desk, an anchor post for the heating main, a welfare room with its heating and its wall, the control panel with its cooling and the chilled-water line that feeds the coil, the operator pendant box, and the piston seal of the locking cylinder, whose groove was "measured" on 30 scanned parts.

Every figure is invented and every calculation cites an invented specification (`DEMO-SPEC-PC-01` … `-12`) or drawing (`DEMO-DRG-PC-130`); an input left at the sheet's own default says so. The figures are the pack templates' own example inputs, so the same figures are the worked examples under `docs/worked-examples/packs/`. What the story adds is the names (every calculation is "DEMO — *area*: *step*"), the rooms (the welfare room and the washroom), the control desk, and the panel's drives; the common figures are consistent where the packs share them: a 400 V three-phase supply (the press supply and the transfer axis), 35 °C ambient in both panels (the transfer axis and the control panel), and 21 °C rooms (the welfare room's heating and its wall). Figures that are not shared (the press motor is 18 kW, the control panel's drives are rated higher, the heating main's temperatures are its own) were not forced to agree: it is a demonstration of the chains, not a design of a cell.

## What is in it

| Area (pack) | Chain | Calculations |
|---|---|---|
| Press HPU | cylinder and pump, accumulator, pressure line, pump motor, supply cable, voltage drop, earth-fault protection, motor start | 8 |
| Infeed conveyor drive | motor, shaft, bearing life, bearing seat fit, coupling flange bolts | 5 |
| Transfer axis circuit | servo motor, panel load, cable and device, voltage drop, fault protection, enclosure heat | 6 |
| Part ejector | crank slider, replacement cylinder, emergency stroke accumulator | 3 |
| Blank-stack tamper | cam, spring, shaft, bearing, motor, supply cable | 6 |
| Support stand ES-4 | load combinations, frame, column, joint bolt group, connection bolts, holding-down bolts, **column base plate** | 7 |
| Heating main anchor | pipe expansion and anchor force, anchor post frame, anchor post base plate | 3 |
| Welfare room heating | welfare room and washroom heat loss, sealed system, pipework and pump, circulator motor | 5 |
| Welfare room wall | room heat loss, room air state, Glaser condensation | 3 |
| Control panel cooling | panel load budget, enclosure heat, cooling-water air coil, pipework and pump | 4 |
| Chilled water to panel coil | chilled-water pipe, plant room air, pipe insulation | 3 |
| Operator pendant box | power trace, board load budget, enclosure temperature, cover latches, cover plate | 5 |
| Locking cylinder seal (scan to seal) | groove-root tolerance stack (from the scan) → O-ring gland (from the same scan) | 2 |

That is every pack template in the build (twelve), and the scan-to-seal workflow (a pack of two made for the demo, since the workflow is not a template). The seven chains named in the plan are the first, second, third, sixth, eighth, tenth and last rows: the drive train, the motor circuit, the hydraulic power unit, the steel support with its base plate, the wet heating of the welfare room, the enclosure cooling, and scan to seal.

**Left out.** The *bracket assembly* pack (a press-fit, a bolted joint and a section: `docs/worked-examples/packs/bracket-assembly-m2-m3-s1`) is a test-only worked example of the pack path, not a pack template, so there is nothing to instantiate from. The *CAD-driven tolerance* workflow (W1) needs a CAD file the demo does not carry. Nothing else is left out.

## Every chain runs, and one check fails on purpose

Every calculation runs with no block, every link is bound and current (none is stale), and every sheet's checks pass except one: **Control panel cooling: Enclosure heat — "Natural dissipation holds T_max"** fails (utilisation about 15). The panel cannot lose its heat by itself, which is the reason the chain goes on to size a cooling coil; the coil, its pipework and the pump all pass. The tests name this one failing check and fail if any other appears.

## DEMO, on everything

- The project is named "Demo — Hydraulic Press Cell", numbered `DEMO0-PRESS1`, and carries a flag (the tag `demo-project`).
- Every calculation, pack and Evidence record is titled "DEMO — …".
- Every sheet made under a flagged project carries a notice, *DEMO — invented data for demonstration only; not for design, purchase or construction*: as the second line of the sheet text, and in the PDF in the header band ("CALCULATION SHEET — DRAFT — DEMO"), a notice line, the footer of every page and the file's title and subject. The mark follows the project's flag, set when the sheet is made: no calculation holds the word, so tagging a project of your own would mark its sheets too, and the demo's sheets cannot lose it. The issued sheet of a demo pack carries it as well.

## No licensed data

The demo uses only invented figures. The motor-supply cable steps need a cable-capacity and a device-rating table, and no build ships real ones; the demo gives them the calculator's own **synthetic** test tables (invented values, not from any regulation or manufacturer) as plain inputs, and each sheet says so. It stores no reference record, so none can be licensed. One figure set is not invented from nothing: the support stand's column is entered with the published geometry of a common 203 × 203 × 46 UC section, as in the steel-support worked example (A, I, h, b, t_w, t_f, r as plain inputs); replace it with your own.

## Scan to seal

The locking cylinder's piston-seal groove is the demo's scan-to-seal chain. The synthetic table [`demo-press-seal-scan.csv`](demo-press-seal-scan.csv) holds 30 parts, each "measured" at three features (the groove root, the bore and the groove width) as a scan software would export them. The demo stores two scan profiles (`demo-press-seal-stack` and `demo-press-seal-gland`, both starting `demo-press-`), reads the table into the groove-root stack (the scanned groove root becomes a normal contributor) and into the gland's measured rows, and links the gland's groove diameter to the stack's mean. The deviations are generated from a fixed seed and are invented, not measurements of anything; the table is the same on every machine. To try the import by hand, open the stack in the worksheet, press *Import scan deviations* and choose this file with the `demo-press-seal-stack` profile.

## Drafts, not checked or issued

The demo is left as drafts. A check records the name of a second, independent person on the Evidence, and the person who installs it is one person, so a check made by the installer would be a false signature. Each pack can be checked by a second person (Settings → Switch person), and the tests do exactly that for all 13 packs. **An issued record is never deleted**, so once any calculation or pack of the demo is issued, Remove refuses and says which one is in the way. Issue a copy in a project of your own if you want to see an issued sheet and still remove the demo.

## What removal does

Removal is the only delete the feature makes, and it keeps to the demo:

1. In one transaction it deletes the calculation, pack and scan-profile rows of the demo project (found by the project's own key, and the scan profiles by their `demo-press-` names) with their search entries, and nothing else. It refuses, and deletes nothing, if any of them is issued.
2. It then marks the demo project and everything under it deleted with the application's own delete, as deleting a project from the explorer does. The platform never erases an engineering object, so its history stays, and so does the audit trail: the install and the removal each leave one audit row (`Demo.Installed`, `Demo.Removed`) with the counts.
3. A real project, its calculations, packs, scan profiles and records are not read for deletion and are unchanged afterwards; the tests compare every stored row before the install and after the removal.

Anything you added to the demo project is removed with it; the confirmation says so.

## Tests

`tests/Tempest.Calc.Tests.Library/DemoProjectTests.cs` (install, refusal, every chain current, the one failing check, links, scan to seal, no licensed record, DEMO on sheets and issued sheets, removal leaving other data untouched, an issued pack stopping removal, nothing made by building the service); `tests/Tempest.Calc.Tests.Sheets/DemoWatermarkTests.cs` and `tests/Tempest.Desktop.Tests/CalcSheets/DemoWatermarkPdfTests.cs` (the watermark in the text and the PDF); `tests/Tempest.Desktop.Tests/CalcSheets/DemoProjectViewModelTests.cs` (the two menu actions, their confirmation, a fresh database starting with no demo rows, the real host over SQLite).
