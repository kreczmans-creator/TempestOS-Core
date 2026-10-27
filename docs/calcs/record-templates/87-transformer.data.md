# 87 transformer.data

**In plain terms.** The supply transformer's impedance voltage and X/R ratio set how far the voltage dips when a large motor starts on it. This file holds one transformer per row.

Kind: `transformer.data` (Transformer impedance data).
Status: existing CalcRecordKind; Track S template (DC-10, 2026-10-27). Track P had no file for this kind.

## Used by
- E9 motor start voltage dip and PF correction (built, `elec.motor-start`) reads u_k and XR.

## Copy from
- Type the values from the transformer's rating plate or its test certificate.
- Cite the manufacturer, the serial number and the certificate number.

## Columns
- `transformer[text]`: transformer designation or serial number. Joins the record title.
- `u_k[%]`: impedance voltage, in per cent.
- `XR[-]`: X/R ratio of the transformer.

## Edition to record
Record the certificate number and date.

## Importer checks
- Reject a blank transformer, u_k or XR. Reject a non-positive u_k or a negative XR.
- Reject a duplicate transformer.
- Warn when a value is outside the usual range of the E9 input it fills.

## Maps to CalcDataRecord
Kind is `transformer.data`. The importer saves one CalcDataRecord per CSV row. Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit. The `transformer` text joins the record title.
