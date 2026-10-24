# 38 containment.conduit-trunking

Kind: `containment.conduit-trunking` (IET conduit and trunking factors).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- E8 containment (planned) compares the cable factor sum with the conduit or trunking factor.

## Copy from
- Type the values from the IET On-Site Guide, the appendix on cable capacities of conduit and trunking.
- Cite the guide edition and table numbers. Confirm them against your copy.

## Columns
- `system[text]`: conduit straight, conduit with bends or trunking.
- `size[text]`: conduit diameter or trunking size, as the table prints it.
- `length_run[m]`: run length the factor applies to. Blank for trunking.
- `n_bends[-]`: number of bends the factor applies to. Blank for trunking.
- `F_container[-]`: conduit or trunking factor.

## Edition to record
Record the guide edition and the BS 7671 amendment it follows.

## Importer checks
- Reject a blank or non-positive F_container.
- Reject a duplicate system, size, length_run and n_bends row.
- Within one system and size, F_container must not rise as length_run or n_bends rises.

## Maps to CalcDataRecord
Kind is `containment.conduit-trunking`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `container_factor_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
