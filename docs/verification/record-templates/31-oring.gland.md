# 31 oring.gland

Kind: `oring.criteria` (squeeze, fill and stretch acceptance bands).
Status: existing CalcRecordKind `oring.criteria`
Planned name: `oring.gland`. The built calculator names the kind `oring.criteria`. The file name stays.

## Used by
- F3 O-ring gland (built, `fp.oring-gland`) judges squeeze, gland fill and stretch against these bands.

## Symbol mapping
- `q_lo_s`, `q_hi_s`, `q_lo_d`, `q_hi_d`, `fill_lo`, `fill_hi`, `x_lo`, `x_hi`, `od_hi` fill the F3 inputs of the same symbol.
- `basis` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- F3 ships defaults for every band. Record them with the handbook and edition you design to.
- The sheet default for `x_hi` is 6 %. The 2001 Parker handbook prints 5 %. See `docs/verification/kat-sources.md`, C4.
- The default for `od_hi` is a placeholder equal to the stretch limit. It is not a handbook value.
- The groove depth, width and radius of the old planned layout are not records. F3 takes the groove from its inputs.

## Copy from
- Type the bands from your O-ring handbook or the seal maker's gland guide, or from ISO 3601-2.
- Cite the handbook, its edition and the section or table.

## Columns
- `basis[text]`: handbook, compound class and seal type the row describes.
- `q_lo_s[%]`: lowest acceptable squeeze, static duty.
- `q_hi_s[%]`: highest acceptable squeeze, static duty.
- `q_lo_d[%]`: lowest acceptable squeeze, dynamic duty.
- `q_hi_d[%]`: highest acceptable squeeze, dynamic duty.
- `fill_lo[%]`: lowest acceptable gland fill.
- `fill_hi[%]`: highest acceptable gland fill.
- `x_lo[%]`: lowest acceptable installed stretch. A negative value allows a ring compressed on its seat.
- `x_hi[%]`: highest acceptable installed stretch.
- `od_hi[%]`: highest acceptable outside-diameter compression of a rod seal or an internal-pressure face seal.

## Edition to record
Record the handbook edition and section.

## Importer checks
- Reject a blank basis. Reject a duplicate basis row.
- Reject a pair where the high value is below the low value.
- F3 refuses q_lo_s and q_lo_d outside 0 to 60 %, and q_hi_s and q_hi_d outside 0 to 80 %.
- F3 refuses fill_lo and fill_hi outside 0 to 100 %, and x_lo outside -50 to 50 %.
- F3 refuses x_hi and od_hi outside 0.1 to 100 %. Warn outside 1 to 10 %.

## Maps to CalcDataRecord
Kind is `oring.criteria`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `basis` text joins the record title. Columns the kind does not list are stored but never applied.
