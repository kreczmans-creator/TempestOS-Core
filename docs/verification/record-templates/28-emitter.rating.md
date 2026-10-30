# 28 emitter.rating

Kind: `emitter.rating` (Emitter exponent).
Status: existing CalcRecordKind

## Used by
- B6 sealed system (built) reads n for the emitter rating.

## Copy from
- Type the exponent from the emitter manufacturer's catalogue, which follows BS EN 442-2.
- Cite the manufacturer, the catalogue edition and the page.

## Columns
- `emitter[text]`: emitter model, as the catalogue prints it.
- `n[-]`: emitter exponent.

## Edition to record
Record the catalogue edition or year, the page or table number, and the catalogue download date.

## Importer checks
- Reject any row with a blank or non-positive number in a numeric column.
- Warn when n is outside 1.1 to 1.5.
- Reject a duplicate emitter.

## Maps to CalcDataRecord
Kind is `emitter.rating`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `emitter` text joins the record title. Columns the kind does not list are stored but never applied.
