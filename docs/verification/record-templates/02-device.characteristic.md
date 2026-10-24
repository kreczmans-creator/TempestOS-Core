# 02 device.characteristic

Kind: `device.characteristic` (Device time-current points).
Status: existing CalcRecordKind

## Used by
- E5 fault protection (built) reads the curve for fuses and moulded-case breakers.
- E5 also reads the let-through energy and the breaking capacity.
- E4 conductor sizing (built) lists no device characteristic record. It reads template 47, `device.ratings`, instead.

## Copy from
- Type the points from the manufacturer's datasheet for the exact device.
- Cite the datasheet title, document number, edition or date, and the curve figure number.

## Columns
- `I2t_let[A2*s]`: let-through energy below 0.1 s. Fill on the first row only.
- `I_cn[kA]`: rated breaking capacity. Fill on the first row only.
- `t[s]`: disconnection time at the point.
- `I[A]`: prospective current at the point.

## Edition to record
Record the datasheet edition or date. Set licensed_source to false only if the manufacturer allows redistribution.

## Importer checks
- Reject non-positive t or I. Reject more than 40 point rows.
- Reject a curve where current rises while time rises. Time must fall as current rises.
- Warn when I2t_let is outside 1 to 1e9 A2*s or I_cn is outside 1 to 150 kA.
- Warn when the points do not bracket the required disconnection time.

## Maps to CalcDataRecord
Kind is `device.characteristic`. The importer saves one CalcDataRecord for the file.
Scalar columns (`I2t_let`, `I_cn`) become one CalcDataValue each, read from the first data row.
The other columns become Rows under one CalcDataValue with Symbol `curve`.
