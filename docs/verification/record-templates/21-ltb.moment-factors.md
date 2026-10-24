# 21 ltb.moment-factors

Kind: `ltb.moment-factors` (LTB moment factors C1, C2 and C3).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- S7 lateral-torsional buckling and N+M (planned) uses them for the elastic critical moment.

## Copy from
- Type the values from the NCCI SN003b-EN-EU, the tables for end-moment and transverse loading.
- Confirm the table numbers against your copy. Cite the document and its edition.

## Columns
- `case[text]`: loading case, such as end moments or transverse load.
- `k[-]`: end-fixity factor.
- `psi_M[-]`: ratio of end moments. Fill on end-moment rows only.
- `C_1[-]`: moment gradient factor C1.
- `C_2[-]`: load-position factor C2. Fill where the table gives it.
- `C_3[-]`: factor C3. Fill where the table gives it.

## Edition to record
Record the NCCI document number and issue. Add any later correction in notes.

## Importer checks
- Reject a blank or non-positive C_1. Reject a duplicate case, k and psi_M row.
- Warn when k is outside 0.5 to 1.
- Warn when psi_M is outside -1 to 1.
- Warn when C_1 is below 1.

## Maps to CalcDataRecord
Kind is `ltb.moment-factors`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `C_factor_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
