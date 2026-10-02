# Engineering Knowledge Foundation v1.9.0

## First source-backed steel observations

This release contains the first actual source-bound steel observations rather than only sourcing placeholders.

### S235JR / S355JR

A thyssenkrupp delivery-program document gives EN 10025-2 mechanical-property tables for hot strip. For the ≤16 mm thickness range it lists:
- S235JR: yield 235 MPa; tensile 360–510 MPa.
- S355JR: yield 355 MPa; tensile 510–680 MPa.

The source explicitly describes the values as transverse values for the hot-strip context. Therefore these observations retain product-form and thickness applicability instead of being promoted to generic grade-level values.

### 42CrMo4

Ovako Steel Navigator provides condition- and dimension-specific data. For +QT round bar at 25 < 40 mm it gives 750 MPa minimum yield, 1000–1200 MPa tensile strength, 11% minimum elongation, 300–350 HB and 35 J Charpy ISO-V at 20°C longitudinal. The same page also shows how the required strength changes with bar diameter, demonstrating why dimension belongs in the observation identity.

### C45

Ovako's page distinguishes the generic C45 designation from supplier variants. The 5081 +QT round-bar variant at 20 < 40 mm is listed at 430 MPa minimum yield, 650–800 MPa tensile strength and 190–240 HB. These observations are explicitly tagged with the supplier variant so the system cannot silently treat them as universal C45 properties.

### Promotion status

These observations are **sourced**, not **verified**. They still require review against the exact source applicability and the TempestOS property semantics before they can become verified engineering data.
