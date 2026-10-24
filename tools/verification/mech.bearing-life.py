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
# Product values come from the Probe tool (C# calculator): (name, inputs, outputs). Hours are converted from seconds.
EXTRAS = [
    ('ball-R97-aiso1.5', {'type': 'ball', 'F_r': 2, 'R': 97, 'a_iso': 1.5},
     {'P': 2.0, 'L_10': 1000.0, 'L_10h': 16666.6667, 'a_1': 0.465353059, 'L_nm': 698.029588, 'L_nmh': 11633.8265, 'P_0': 2.0, 's_0': 5.5, 'u_life': 1.71912483, 'u_s0': 0.181818182}),
    ('roller-axial-above-e', {'type': 'roller', 'F_r': 10, 'F_a': 6, 'e_lim': 0.4, 'X': 0.4, 'Y': 1.5, 'C': 90, 'C_0': 100, 'n': 300, 'R': 95, 'a_iso': 2.2, 'L_req': 50000, 's_0_req': 2},
     {'P': 13.0, 'L_10': 632.40776, 'L_10h': 35133.7644, 'a_1': 0.637911663, 'L_nm': 887.524629, 'L_nmh': 49306.9238, 'P_0': 10.0, 's_0': 10.0, 'u_life': 1.01405637, 'u_s0': 0.2}),
    ('ratio-equals-e', {'F_r': 10, 'F_a': 3, 'e_lim': 0.3, 'X': 0.56, 'Y': 1.5, 'C': 40, 'C_0': 30},
     {'P': 10.0, 'L_10': 64.0, 'L_10h': 1066.66667, 'a_1': 1.0, 'L_nm': 64.0, 'L_nmh': 1066.66667, 'P_0': 10.0, 's_0': 3.0, 'u_life': 18.75, 'u_s0': 0.333333333}),
    ('ratio-just-above-e', {'F_r': 10, 'F_a': 3.0001, 'e_lim': 0.3, 'X': 0.56, 'Y': 1.5, 'C': 40, 'C_0': 30},
     {'P': 10.10015, 'L_10': 62.1150019, 'L_10h': 1035.25003, 'a_1': 1.0, 'L_nm': 62.1150019, 'L_nmh': 1035.25003, 'P_0': 10.0, 's_0': 3.0, 'u_life': 19.3190045, 'u_s0': 0.333333333}),
    ('duty-mixed-branches', {'duty': [(4, 0.5, 0.2), (2, 2, 0.5), (0, 3, 0.3)], 'e_lim': 0.3, 'X': 0.56, 'Y': 1.5, 'C': 40, 'C_0': 30, 'n': 750, 'R': 96, 'a_iso': 1.2},
     {'P': 4.219126, 'L_10': 852.143015, 'L_10h': 18936.5114, 'a_1': 0.554895105, 'L_nm': 567.419985, 'L_nmh': 12609.333, 'P_0': 4.0, 's_0': 7.5, 'u_life': 1.58612672, 'u_s0': 0.133333333}),
    ('P-equals-0.5C', {'F_r': 10, 'C': 20},
     {'P': 10.0, 'L_10': 8.0, 'L_10h': 133.333333, 'a_1': 1.0, 'L_nm': 8.0, 'L_nmh': 133.333333, 'P_0': 10.0, 's_0': 1.1, 'u_life': 150.0, 'u_s0': 0.909090909}),
    ('P-just-above-0.5C', {'F_r': 10.001, 'C': 20},
     {'P': 10.001, 'L_10': 7.99760048, 'L_10h': 133.293341, 'a_1': 1.0, 'L_nm': 7.99760048, 'L_nmh': 133.293341, 'P_0': 10.001, 's_0': 1.09989001, 'u_life': 150.045005, 'u_s0': 0.909181818}),
    ('light-load-limit', {'F_r': 0.002, 'C': 20},
     {'P': 0.002, 'L_10': 1000000000000.0, 'L_10h': 16666666700000.0, 'a_1': 1.0, 'L_nm': 1000000000000.0, 'L_nmh': 16666666700000.0, 'P_0': 0.002, 's_0': 5500.0, 'u_life': 1.2e-09, 'u_s0': 0.000181818182}),
    ('R-99.95-aiso-50', {'R': 99.95, 'a_iso': 50},
     {'P': 2.0, 'L_10': 1000.0, 'L_10h': 16666.6667, 'a_1': 0.0768322699, 'L_nm': 3841.6135, 'L_nmh': 64026.8916, 'P_0': 2.0, 's_0': 5.5, 'u_life': 0.312368749, 'u_s0': 0.181818182}),
    ('n-1-rpm', {'n': 1, 'F_r': 1, 'C': 20},
     {'P': 1.0, 'L_10': 8000.0, 'L_10h': 133333333.0, 'a_1': 1.0, 'L_nm': 8000.0, 'L_nmh': 133333333.0, 'P_0': 1.0, 's_0': 11.0, 'u_life': 0.00015, 'u_s0': 0.0909090909}),
    ('n-50000-rpm', {'n': 50000, 'F_r': 5, 'C': 20},
     {'P': 5.0, 'L_10': 64.0, 'L_10h': 21.3333333, 'a_1': 1.0, 'L_nm': 64.0, 'L_nmh': 21.3333333, 'P_0': 5.0, 's_0': 2.2, 'u_life': 937.5, 'u_s0': 0.454545455}),
    ('duty-U-not-normalised', {'duty': [(2, 0, 0.2), (4, 0, 0.2)], 'C': 25, 'C_0': 15},
     {'P': 3.30192725, 'L_10': 434.027778, 'L_10h': 7233.7963, 'a_1': 1.0, 'L_nm': 434.027778, 'L_nmh': 7233.7963, 'P_0': 4.0, 's_0': 3.75, 'u_life': 2.7648, 'u_s0': 0.266666667}),
    ('thrust-Fr-zero', {'F_r': 0, 'F_a': 5, 'e_lim': 0.3, 'X': 0.56, 'Y': 1.2, 'C': 30, 'C_0': 40},
     {'P': 6.0, 'L_10': 125.0, 'L_10h': 2083.33333, 'a_1': 1.0, 'L_nm': 125.0, 'L_nmh': 2083.33333, 'P_0': 2.5, 's_0': 16.0, 'u_life': 9.6, 'u_s0': 0.0625}),
    ('default-factors-Fa-4', {'F_r': 2, 'F_a': 4},
     {'P': 2.0, 'L_10': 1000.0, 'L_10h': 16666.6667, 'a_1': 1.0, 'L_nm': 1000.0, 'L_nmh': 16666.6667, 'P_0': 3.2, 's_0': 3.4375, 'u_life': 1.2, 'u_s0': 0.290909091}),
]

for name, kw, prod in EXTRAS:
    kw = dict(kw)
    if "type" in kw:
        kw["type_"] = kw.pop("type")
    ind = bearing(**kw)
    for sym, pv in prod.items():
        check("EXTRA", name, sym, pv, ind[sym], 1e-6)

# Behaviour notes from probes (no row, no effect on the exit code).
# 1. Defaults X = 1, Y = 0, e_lim = 0.3 make F_a drop out of P. F_r 2 kN, F_a 4 kN gave P = 2 kN.
#    With ball factors X 0.56, Y 1.5 the same load gives P = 7.12 kN and a life ratio of (7.12/2)^3 = 45.
p_default = bearing(F_r=2, F_a=4)["P"]
p_ball = bearing(F_r=2, F_a=4, e_lim=0.3, X=0.56, Y=1.5)["P"]
print(f"note  default factors: P {p_default:.3f} kN; ball factors: P {p_ball:.3f} kN; life ratio {(p_ball / p_default) ** 3:.1f}")
# 2. Double-row bearing, F_a/F_r <= e: ISO 281:2007 Table 3 uses P = F_r + Y_1 F_a. The product has no Y_1.
fr, fa, y1 = 10.0, 1.9, 3.3
print(f"note  double row, F_a/F_r = 0.19 <= e: P = F_r + Y_1 F_a = {fr + y1 * fa:.2f} kN; product gives {fr:.2f} kN; "
      f"life overstated by {((fr + y1 * fa) / fr) ** 3:.2f}x")

# ---------------------------------------------------------------- report
print(f"{'kind':6} {'case':24} {'symbol':10} {'expected':>13} {'independent':>13} {'rel.diff':>10} {'tol':>7}  result")
for kind, c, s_, e, v, r, t, ok in rows:
    print(f"{kind:6} {c:24} {s_:10} {e:13.7g} {v:13.7g} {r:10.2e} {t:7.0e}  {'PASS' if ok else 'FAIL'}")
kat_bad = sum(1 for r in rows[:n_kat] if not r[7])
ex = rows[n_kat:]
print(f"KAT rows pass: {n_kat - kat_bad}/{n_kat}; extra rows pass: {sum(1 for r in ex if r[7])}/{len(ex)}")
sys.exit(1 if kat_bad else 0)
