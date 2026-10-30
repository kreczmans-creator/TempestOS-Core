# 83 motor.rating

**In plain terms.** A motor is chosen for a drive by its inertia, its rated and peak torque and its speeds. This file holds one candidate motor per row so the motor sizing sheet can take them from a record instead of being typed on the sheet.

Kind: `motor.rating` (Candidate motor rating).
Status: existing CalcRecordKind; Track S template (DC-10, 2026-10-27). Track P had no file for this kind.

## Used by
- E1 motor and actuator sizing (built, `elec.motor-sizing`) reads J_motor, T_rated, T_peak, n_rated and n_max.

## Copy from
- Type the values from the motor's datasheet or nameplate.
- Cite the manufacturer, the document number and its revision.

## Columns
- `motor[text]`: motor designation, as the datasheet prints it. Joins the record title.
- `J_motor[kg*m2]`: rotor inertia.
- `T_rated[N*m]`: continuous (rated) torque.
- `T_peak[N*m]`: peak torque.
- `n_rated[rpm]`: rated (nameplate) speed; leave blank when the datasheet gives none.
- `n_max[rpm]`: maximum speed.

## Edition to record
Record the datasheet revision or the catalogue year.

## Importer checks
- Reject a blank motor, J_motor, T_rated, T_peak or n_max. Reject a non-positive number.
- Reject a row where T_rated is above T_peak, or n_rated is above n_max.
- Reject a duplicate motor.
- Warn when a value is outside the usual range of the E1 input it fills (the importer reads those limits from the calculator).

## Maps to CalcDataRecord
Kind is `motor.rating`. The importer saves one CalcDataRecord per CSV row. Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit. The `motor` text joins the record title.
