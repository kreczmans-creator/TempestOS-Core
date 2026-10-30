# 85 motor.start

**In plain terms.** A motor draws several times its rated current while it starts, at a low power factor. This file holds one motor per row with those two datasheet figures.

Kind: `motor.start` (Motor starting data).
Status: existing CalcRecordKind; Track S template (DC-10, 2026-10-27). Track P had no file for this kind.

## Used by
- E9 motor start voltage dip and PF correction (built, `elec.motor-start`) reads I_LR_r and cos_phi_st.

## Copy from
- Type the values from the motor's datasheet.
- Cite the manufacturer, the document number and its revision.

## Columns
- `motor[text]`: motor designation, as the datasheet prints it. Joins the record title.
- `I_LR_r[-]`: locked-rotor current as a multiple of the rated current, I_LR/I_n.
- `cos_phi_st[-]`: power factor at locked rotor (starting).

## Edition to record
Record the datasheet revision.

## Importer checks
- Reject a blank motor, I_LR_r or cos_phi_st. Reject a non-positive number.
- Reject a cos_phi_st above 1.
- Reject a duplicate motor.
- Warn when a value is outside the usual range of the E9 input it fills.

## Maps to CalcDataRecord
Kind is `motor.start`. The importer saves one CalcDataRecord per CSV row. Each numeric column becomes one CalcDataValue with its Symbol and Number. The `motor` text joins the record title.
