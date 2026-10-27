# 86 starter.limits

**In plain terms.** A soft-starter or a drive caps the current the motor draws while it starts. This file holds one device and parameter setting per row with the limit it gives, as a multiple of the rated current.

Kind: `starter.limits` (Soft-starter and drive current limits).
Status: existing CalcRecordKind; Track S template (DC-10, 2026-10-27). Track P had no file for this kind.

## Used by
- E9 motor start voltage dip and PF correction (built, `elec.motor-start`) reads k_soft for a soft-starter and k_vfd for a drive.

## Copy from
- Type the limit from the soft-starter or drive datasheet and the parameter setting you will use.
- Cite the manufacturer, the document number, its revision and the parameter name.

## Columns
- `starter[text]`: device designation and parameter setting, as the datasheet prints them. Joins the record title.
- `k_soft[-]`: soft-starter current limit as a multiple of the rated current; blank for a drive.
- `k_vfd[-]`: drive line-current limit as a multiple of the rated current; blank for a soft-starter.

## Edition to record
Record the datasheet revision and the parameter setting in notes.

## Importer checks
- Reject a blank starter. Reject a row with neither k_soft nor k_vfd.
- Reject a non-positive number.
- Reject a duplicate starter.
- Warn when a value is outside the usual range of the E9 input it fills.

## Maps to CalcDataRecord
Kind is `starter.limits`. The importer saves one CalcDataRecord per CSV row. Each filled numeric column becomes one CalcDataValue with its Symbol and Number. The `starter` text joins the record title.
