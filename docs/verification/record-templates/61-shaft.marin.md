# 61 shaft.marin

Kind: `shaft.marin` (Marin surface factor).
Status: existing CalcRecordKind `shaft.marin`

## Used by
- M4 shaft (built, `mech.shaft`) reads `k_a` as the surface factor override.
- Leave the M4 input blank to let M4 compute k_a from the tensile strength and its built-in coefficients.

## Symbol mapping
- `k_a[-]` fills the M4 input `k_a`.
- `finish` names the surface finish. The importer stores it and M4 never reads it.
- The importer saves one record per row. Pick the row for your shaft surface.

## Copy from
- Type the factor from the edition of the fatigue reference you use.
- The coefficients changed between editions of the common references. Cite the edition and the table.
- You may instead calculate k_a from the coefficients for your finish and S_ut. Mark origin Derived.

## Columns
- `finish[text]`: surface finish, as your reference names it.
- `k_a[-]`: surface factor for that finish.

## Edition to record
Record the reference edition, amendment and table number.

## Importer checks
- Reject a blank finish. Reject a duplicate finish.
- Reject a blank or non-positive k_a.
- M4 refuses k_a outside 0.05 to 2. Warn outside 0.1 to 1.5.
- Warn when k_a is above 1.

## Maps to CalcDataRecord
Kind is `shaft.marin`. The importer saves one CalcDataRecord per CSV row.
The `k_a` column becomes one CalcDataValue with Symbol `k_a`, a Number and the Unit `-`.
The `finish` text joins the record title. Columns the kind does not list are stored but never applied.
