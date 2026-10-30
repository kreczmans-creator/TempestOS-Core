# 84 cable.impedance

**In plain terms.** The voltage dip when a motor starts depends on the resistance and reactance of the cable feeding it. This file holds one cable size per row, at the temperature the datasheet states.

Kind: `cable.impedance` (Cable impedance per kilometre).
Status: existing CalcRecordKind; Track S template (DC-10, 2026-10-27). Track P had no file for this kind.

## Used by
- E9 motor start voltage dip and PF correction (built, `elec.motor-start`) reads R_km and X_km.

## Copy from
- Type the values from the cable manufacturer's datasheet, or from your own licensed table, for the cross-section and the temperature the table states.
- Cite the manufacturer or the publication, its edition and the table.

## Columns
- `cable[text]`: cable size or designation, as the table prints it. Joins the record title.
- `R_km[ohm/km]`: resistance per kilometre per conductor.
- `X_km[ohm/km]`: reactance per kilometre per conductor.

## Edition to record
Record the edition and the conductor temperature the table assumes in notes.

## Importer checks
- Reject a blank cable, R_km or X_km. Reject a non-positive R_km or a negative X_km.
- Reject a duplicate cable.
- Warn when a value is outside the usual range of the E9 input it fills.

## Maps to CalcDataRecord
Kind is `cable.impedance`. The importer saves one CalcDataRecord per CSV row. Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit. The `cable` text joins the record title.
