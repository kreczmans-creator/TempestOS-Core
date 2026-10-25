# 19 concrete.strengths

Kind: `concrete.strengths` (Concrete strength classes).
Status: proposed — S6 is built but lists no CalcRecordKind for f_ck; Track S decides whether to add one

## Used by
- S6 base plate (built) takes f_ck as a typed input. It lists no record kind for it, so the importer cannot apply this file yet.
- Track S may add a kind to S6 later. Until then this template stays proposed.

## Copy from
- Type the values from BS EN 1992-1-1, Table 3.1.
- You may cross-check the class names against BS EN 206.

## Columns
- `class[text]`: strength class name, as the table prints it.
- `f_ck[MPa]`: characteristic cylinder compressive strength.
- `f_ck_cube[MPa]`: characteristic cube compressive strength.

## Edition to record
Record the BS EN 1992-1-1 edition and amendment.

## Importer checks
- Reject any row with a blank or non-positive number in a numeric column.
- Reject a row where f_ck_cube is below f_ck.
- Reject a duplicate class.
- Warn when f_ck is outside 12 to 90 MPa.

## Maps to CalcDataRecord
Kind is `concrete.strengths`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `class` text joins the record title. Columns the kind does not list are stored but never applied.
