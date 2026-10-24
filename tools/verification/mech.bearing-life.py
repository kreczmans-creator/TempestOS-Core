#!/usr/bin/env python3
"""Independent re-derivation of mech.bearing-life (Track P, P1).

Sources, cited by title and number only:
- ISO 281:2007, clauses 6 to 9: dynamic equivalent load, basic rating life, modified rating life, a_1 (Table 1).
- ISO 76:2006, clauses 6 and 7: static equivalent load and static safety factor.
- Shigley's Mechanical Engineering Design, 11th ed., Chapter 11: rolling-contact bearings.

Model:
- Step equivalent load: P_i = F_r when F_a/F_r <= e, else X F_r + Y F_a. A step with F_r = 0 uses the second form.
- Duty cycle: P = (sum(P_i^p U_i) / sum(U_i))^(1/p), p = 3 (ball) or 10/3 (roller).
- L_10 = (C/P)^p in million revolutions. L_10h = 1e6 L_10 / (60 n).
- Reliability factor a_1 = 0.05 + 0.95 (ln(100/R) / ln(100/90))^(2/3), the Weibull form behind ISO 281:2007 Table 1.
- L_nm = a_1 a_ISO L_10. L_nmh = 1e6 L_nm / (60 n).
- Static: P_0 = max(X_0 F_r + Y_0 F_a, F_r), s_0 = C_0 / P_0. For a duty cycle the peak step governs.

Exit code: 0 when every KAT row passes (tolerance max(KAT tol, 1e-6)), else 1.
EXTRA rows compare product values read from the Probe tool. They do not set the exit code.
"""
import math
import sys

BALL, ROLLER = "ball", "roller"


def a1(R):
    return 0.05 + 0.95 * (math.log(100.0 / R) / math.log(100.0 / 90.0)) ** (2.0 / 3.0)


def bearing(type_=BALL, F_r=2.0, F_a=0.0, duty=None, n=1000.0, C=20.0, C_0=11.0, e_lim=0.3, X=1.0, Y=0.0,
            X_0=0.6, Y_0=0.5, R=90.0, a_iso=1.0, L_req=20000.0, s_0_req=1.0):
    """duty: list of (F_r, F_a, U) or None. Loads in kN, n in rpm, L_req in h."""
    p = 3.0 if type_ == BALL else 10.0 / 3.0
    steps = duty if duty else [(F_r, F_a, 1.0)]

    def p_dyn(fr, fa):
        if fr > 0 and fa / fr <= e_lim:
            return fr
        return X * fr + Y * fa

    U = sum(s[2] for s in steps)
    P = (sum(p_dyn(s[0], s[1]) ** p * s[2] for s in steps) / U) ** (1.0 / p)
    L10 = (C / P) ** p
    L10h = 1e6 * L10 / (60.0 * n)
    a_1 = a1(R)
    Lnm = a_1 * a_iso * L10
    Lnmh = 1e6 * Lnm / (60.0 * n)
    P0 = max(max(X_0 * s[0] + Y_0 * s[1], s[0]) for s in steps)
    s0 = C_0 / P0
    return dict(P=P, L_10=L10, L_10h=L10h, a_1=a_1, L_nm=Lnm, L_nmh=Lnmh, P_0=P0, s_0=s0,
                u_life=L_req / Lnmh, u_s0=s_0_req / s0)


rows = []   # (kind, case, symbol, expected, value, rel, tol, ok)


def check(kind, name, sym, expected, value, tol):
    rel = abs(value - expected) / max(abs(expected), 1e-12)
    rows.append((kind, name, sym, expected, value, rel, tol, rel <= max(tol, 1e-6)))


# ---------------------------------------------------------------- KATs (exit code)
k = bearing(BALL, 2, 0, n=1000, C=20, C_0=11, R=95, a_iso=1, L_req=10000)
for sym, exp, tol in [("L_10", 1000, 1e-9), ("L_10h", 16666.67, 1e-6), ("a_1", 0.637912, 1e-5),
                      ("L_nmh", 10631.86, 1e-5), ("u_life", 0.940569, 1e-5)]:
    check("KAT", "ka-ball-95", sym, exp, k[sym], tol)
k = bearing(ROLLER, 5, 0, n=500, C=50, C_0=40, R=99, a_iso=2, L_req=30000)
for sym, exp, tol in [("L_10", 2154.435, 1e-6), ("L_10h", 71814.49, 1e-6), ("a_1", 0.248332, 1e-5),
                      ("L_nm", 1070.029, 1e-5), ("L_nmh", 35667.62, 1e-5)]:
    check("KAT", "ka-roller-99", sym, exp, k[sym], tol)
k = bearing(BALL, 3, 1.5, n=1200, C=30, C_0=16, e_lim=0.22, X=0.56, Y=2, X_0=0.6, Y_0=0.5, R=90, a_iso=1,
            L_req=3000, s_0_req=2)
for sym, exp, tol in [("P", 4.68, 1e-9), ("L_10", 263.4063, 1e-6), ("L_10h", 3658.421, 1e-6),
                      ("P_0", 3, 1e-9), ("s_0", 5.333333, 1e-6)]:
    check("KAT", "ka-combined", sym, exp, k[sym], tol)
k = bearing(BALL, 0, 0, duty=[(2, 0, 0.5), (4, 0, 0.5)], n=800, C=25, C_0=15, R=90, a_iso=1, L_req=5000)
for sym, exp, tol in [("P", 3.301927, 1e-6), ("L_10h", 9042.245, 1e-6), ("P_0", 4, 1e-9)]:
    check("KAT", "ka-duty-cycle", sym, exp, k[sym], tol)
k = bearing(BALL, 2.5, 0, n=1500, C=20.3, C_0=11.2, R=90, a_iso=1, L_req=5000)
for sym, exp, tol in [("L_10", 535.3873, 1e-6), ("L_10h", 5948.748, 1e-6), ("s_0", 4.48, 1e-6)]:
    check("KAT", "ka-skf-6206", sym, exp, k[sym], tol)
n_kat = len(rows)

# ---------------------------------------------------------------- extra checks
EXTRAS = []   # filled after probing the product

# ---------------------------------------------------------------- report
print(f"{'kind':6} {'case':24} {'symbol':10} {'expected':>13} {'independent':>13} {'rel.diff':>10} {'tol':>7}  result")
for kind, c, s_, e, v, r, t, ok in rows:
    print(f"{kind:6} {c:24} {s_:10} {e:13.7g} {v:13.7g} {r:10.2e} {t:7.0e}  {'PASS' if ok else 'FAIL'}")
kat_bad = sum(1 for r in rows[:n_kat] if not r[7])
ex = rows[n_kat:]
print(f"KAT rows pass: {n_kat - kat_bad}/{n_kat}; extra rows pass: {sum(1 for r in ex if r[7])}/{len(ex)}")
sys.exit(1 if kat_bad else 0)
