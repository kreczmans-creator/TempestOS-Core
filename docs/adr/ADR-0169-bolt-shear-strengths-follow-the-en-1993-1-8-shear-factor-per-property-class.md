# ADR-0169: Bolt Shear Strengths Follow the EN 1993-1-8 Shear Factor per Property Class

## Status

Accepted (v0.26.0, Addendum A, A-3). Changes the v0.25.2 `calc.bolt-shear-capacity` standard-value picker; no test is superseded.

## Context

The v0.25.2 bolt-shear module offered a picker of "ultimate shear strengths" taken as 0.6 × f_ub for every property class. EN 1993-1-8 Table 3.4 uses α_v = 0.6 for classes 4.6, 5.6 and 8.8, but α_v = 0.5 for 4.8, 5.8, 6.8 and 10.9 when the threads are in the shear plane. A user picking "Class 10.9" therefore got a shear strength 20 % too high (600 instead of 500 MPa). Addendum A §A6 S4 asks for the reconciliation, with an ADR, CHANGELOG entry and regression tests.

## Decision

1. The picker offers α_v·f_ub for threads in the shear plane: 4.6 → 240, 4.8 → 200, 5.6 → 300, 5.8 → 250, 6.8 → 300, 8.8 → 480, 10.9 → 500 MPa. Classes 4.8, 5.8 and 6.8 are added; labels of the 0.5 classes say "αv 0.5".
2. Class 12.9, outside EN 1993-1-8, is kept and takes 0.5 (600 MPa), labelled as an assumption.
3. The module's arithmetic (gross area × strength × planes ÷ safety factor) is unchanged; the full EN 1993-1-8 bolt checks live in the S4 worksheet `struct.connection-ec3`.
4. Regression tests pin each class's value, the 10.9 change and an M20 10.9 capacity.

## Consequences

A stored v0.25.2 calculation keeps the number it was given (inputs are stored as values, not picker keys); recalculating one that used "Class 10.9" with a re-picked value gives a capacity one sixth lower. The labels of the unchanged classes stay as they were, so existing picks and tests still match.
