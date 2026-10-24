# ADR-0169: Bolt Shear Strengths Follow the EN 1993-1-8 Shear Factor per Property Class

## Status

Accepted (v0.26.0, Addendum A, A-3); **revised the same day after the Opus review** (finding 1). Scopes the v0.25.2 `calc.bolt-shear-capacity` module; no pre-existing test is superseded.

## Context

The v0.25.2 bolt-shear module offers a picker of shear strengths taken as 0.6 × f_ub for every property class and multiplies by the **gross** area π d²/4. Addendum A §A6 S4 asks to reconcile that blanket 0.6 with EN 1993-1-8 Table 3.4, which distinguishes two cases:

- shear plane through the **unthreaded shank**: A = gross area, α_v = 0.6 for every class;
- shear plane through the **threads**: A = tensile stress area A_s, α_v = 0.6 for 4.6, 5.6, 8.8 and 0.5 for 4.8, 5.8, 6.8, 10.9.

The first version of this ADR lowered the 10.9 picker value to 0.5 × f_ub while the module kept the gross area. The review showed that mixes the two cases: 0.5 × 1000 × 314.2 mm² = 157.1 kN against the threads-in-plane 0.5 × 1000 × 244.8 = 122.4 kN, 28 % unconservative.

## Decision

1. The legacy module is the **shank-in-plane** case of Table 3.4, which is what its arithmetic computes. Its picker offers 0.6 × f_ub for every class (4.6 → 240, 4.8 → 240, 5.6 → 300, 5.8 → 300, 6.8 → 360, 8.8 → 480, 10.9 → 600, 12.9 → 720 MPa; 4.8, 5.8, 6.8 added), the 0.5-class labels say "shank in shear plane", and the module's description and a new assumption state the scope and point threads-in-plane users to S4.
2. The threads-in-plane case — A_s and the class's α_v — is the S4 worksheet `struct.connection-ec3`, which handles both cases.
3. Regression tests pin each value, the stated scope and an M20 10.9 shank capacity.

## Consequences

No stored calculation changes value. A user who previously applied the module to a threaded shear plane now sees that it does not cover that case. Previously that use was silently 28 % unconservative for 8.8 and 10.9.
