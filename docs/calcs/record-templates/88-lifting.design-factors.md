# 88 lifting.design-factors

**In plain terms.** A below-the-hook lifting device is designed with a design factor for its Design Category and a service-class factor for how often it is used. This file holds one category and class pair per row, from the owner's copy of the lifting code.

Kind: `lifting.design-factors` (Design factor and service-class factor).
Status: existing CalcRecordKind; Track S template (DC-10, 2026-10-27). Track P had no file for this kind.

## Used by
- S8 lifting lugs and spreader beams (built, `struct.lifting`, hidden until the lifting flag is on) reads design_category, service_class, Nd and k_svc.
- The kind is listed only while the lifting flag is on; the importer refuses the file until then.

## Copy from
- Type the values from your licensed copy of the below-the-hook lifting device design code, the clauses on Design Categories and Service Classes.
- Cite the code, its edition and the clause or table.

## Columns
- `design_category[text]`: the Design Category the design factor belongs to, as the code names it. Printed on the sheet.
- `service_class[text]`: the Service Class the service factor belongs to, as the code names it. Printed on the sheet.
- `Nd[-]`: design factor of the Design Category.
- `k_svc[-]`: service-class factor on the load.

## Edition to record
Record the code edition and amendment.

## Importer checks
- Reject a blank design_category, service_class, Nd or k_svc. Reject a non-positive number, or a factor below 1.
- Reject a duplicate design_category and service_class pair.
- Warn when a value is outside the usual range of the S8 input it fills.

## Maps to CalcDataRecord
Kind is `lifting.design-factors`. The importer saves one CalcDataRecord per CSV row. Nd and k_svc become CalcDataValues with their Symbol and Number; design_category and service_class are text values of the kind (the labels the sheet prints), so the record title carries the row number.
