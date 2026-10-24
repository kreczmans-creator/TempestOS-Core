#!/usr/bin/env python3
"""Independent re-derivation of mech.bolt-group (Track P, P1).

Sources, cited by title and number only:
- AISC Steel Construction Manual, Part 7: elastic method for eccentrically loaded bolt groups.
- EN 1993-1-8:2005, Table 3.4: shear plus tension interaction (linear form with 1.4).
- Shigley's Mechanical Engineering Design, 9th ed., sect. 8-11: bolted joints under eccentric loading.
Units: mm, kN, kN*mm inside the model. Moments are given in kN*m and converted.

Model: the connected parts are rigid.
- Shear: direct share A_i F/sum(A) plus torsion share T A_i r_i / J, perpendicular to the radius.
- Tension: plane-section distribution t_i = A_i (N/sum(A) + c1 x'_i + c2 y'_i). The constants c1 and c2
  satisfy moment equilibrium about both centroidal axes, including the product Sum(A x' y').
- Bolts with negative tension (net compression) carry no tension.
- Prying: t_p = t (1 + b/a) (simple lever model, upper bound).
- Interaction: circular u = sqrt((v/V_R)^2 + (t_p/T_R)^2) and the EN 1993-1-8 linear form v/V_R + t_p/(1.4 T_R).

Exit code: 0 when every KAT row passes (tolerance max(KAT tol, 1e-6)), else 1.
EXTRA rows compare product values read from the Probe tool. They do not set the exit code.
"""
import math
import sys

import numpy as np


def group(bolts, F_x=0.0, F_y=0.0, e_x=0.0, e_y=0.0, T_z=0.0, N=0.0, M_x=0.0, M_y=0.0,
          V_R=30.0, T_R=45.0, a=None, b=None):
    """bolts: list of (x, y, A) in mm, mm, mm^2. Loads in kN, mm and kN*m. Returns a dict."""
    B = np.array(bolts, dtype=float)
    x, y, A = B[:, 0], B[:, 1], B[:, 2]
    SA = A.sum()
    xc, yc = (A * x).sum() / SA, (A * y).sum() / SA
    dx, dy = x - xc, y - yc
    J = (A * (dx ** 2 + dy ** 2)).sum()                       # mm^4
    T = F_y * (e_x - xc) - F_x * (e_y - yc) + T_z * 1e3       # kN*mm
    # Shear: forces on the bolts, same sense as the applied load.
    vx = A * F_x / SA + T * A * (-dy) / J
    vy = A * F_y / SA + T * A * (dx) / J
    v = np.hypot(vx, vy)
    # Tension: plane section.
    Sxx, Syy, Sxy = (A * dx * dx).sum(), (A * dy * dy).sum(), (A * dx * dy).sum()
    rhs = np.array([M_y * 1e3, M_x * 1e3])
    mat = np.array([[Sxx, Sxy], [Sxy, Syy]])
    if abs(np.linalg.det(mat)) < 1e-9 * max(Sxx * Syy, 1.0):
        c = np.linalg.lstsq(mat, rhs, rcond=None)[0]
    else:
        c = np.linalg.solve(mat, rhs)
    t = A * (N / SA + c[0] * dx + c[1] * dy)
    # A bolt in net compression carries no tension: the parts bear on each other there.
    tp = np.maximum(t, 0.0) * ((1 + b / a) if (a is not None and b is not None) else 1.0)
    u_circ = np.sqrt((v / V_R) ** 2 + (tp / T_R) ** 2)
    u_lin = v / V_R + tp / (1.4 * T_R)
    g = int(np.argmax(np.maximum(u_circ, u_lin)))
    gc = int(np.argmax(u_circ))
    return dict(xc=xc, yc=yc, T=T / 1e3, J=J, v=v, t=t, tp=tp, v_max=v.max(), t_max=t.max(),
                u_circ=u_circ, u_lin=u_lin, u_max_circ=u_circ[gc], u_max_lin=u_lin.max(),
                governing=g, governing_circ=gc, c=c)


A0 = 84.3
SQUARE = [(0, 0, A0), (100, 0, A0), (100, 100, A0), (0, 100, A0)]
TWO_ROWS = [(0, -100, A0), (100, -100, A0), (0, 100, A0), (100, 100, A0)]

rows = []   # (kind, case, symbol, expected, value, rel, tol, ok)


def check(kind, name, sym, expected, value, tol):
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    rows.append((kind, name, sym, expected, value, rel, tol, rel <= max(tol, 1e-6)))


# ---------------------------------------------------------------- KATs (exit code)
g1 = group(SQUARE, F_y=-50, e_x=200, e_y=50, V_R=40, T_R=45)
check("KAT", "ka-four-bolt-eccentric", "T", -7.5, g1["T"], 1e-6)
check("KAT", "ka-four-bolt-eccentric", "J", 1.686e6, g1["J"], 1e-6)
check("KAT", "ka-four-bolt-eccentric", "v_max", 36.443, g1["v_max"], 1e-3)
check("KAT", "ka-four-bolt-eccentric", "u_max", 0.91109, g1["u_max_circ"], 1e-3)
g2 = group(TWO_ROWS, M_x=20, V_R=40, T_R=60)
check("KAT", "ka-moment-tension", "t_max", 50.0, g2["t_max"], 1e-6)
check("KAT", "ka-moment-tension", "u_max", 0.83333, g2["u_max_circ"], 1e-4)
n_kat = len(rows)

# ---------------------------------------------------------------- extra checks
EXTRAS = []   # filled after probing the product

# ---------------------------------------------------------------- report
print(f"{'kind':6} {'case':24} {'symbol':14} {'expected':>13} {'independent':>13} {'rel.diff':>10} {'tol':>7}  result")
for kind, c, s, e, v, r, t, ok in rows:
    print(f"{kind:6} {c:24} {s:14} {e:13.6g} {v:13.6g} {r:10.2e} {t:7.0e}  {'PASS' if ok else 'FAIL'}")
kat_bad = sum(1 for r in rows[:n_kat] if not r[7])
print(f"KAT rows pass: {n_kat - kat_bad}/{n_kat}")
sys.exit(1 if kat_bad else 0)
