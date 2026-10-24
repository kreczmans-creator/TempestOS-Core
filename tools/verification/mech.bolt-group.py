#!/usr/bin/env python3
"""Independent re-derivation of mech.bolt-group (Track P, P1).

Sources, cited by title and number only:
- AISC Steel Construction Manual, Part 7: elastic method for eccentrically loaded bolt groups.
- EN 1993-1-8:2005, Table 3.4: shear plus tension interaction (linear form with 1.4).
- Shigley's Mechanical Engineering Design, 9th ed., Chapter 8: bolted joints loaded in shear.
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
          V_R=30.0, T_R=45.0, a=None, b=None, tension="plane"):
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
    if tension == "decoupled":   # each axis alone, product of inertia ignored (textbook shortcut)
        t = A * (N / SA + (M_y * 1e3 / Sxx if Sxx > 0 else 0) * dx + (M_x * 1e3 / Syy if Syy > 0 else 0) * dy)
    else:
        t = A * (N / SA + c[0] * dx + c[1] * dy)
    # Moment carried by the tension distribution about each centroidal axis, kN*mm.
    res_Mx, res_My = float((t * dy).sum()), float((t * dx).sum())
    # A bolt in net compression carries no tension: the parts bear on each other there.
    tp = np.maximum(t, 0.0) * ((1 + b / a) if (a is not None and b is not None) else 1.0)
    u_circ = np.sqrt((v / V_R) ** 2 + (tp / T_R) ** 2)
    u_lin = v / V_R + tp / (1.4 * T_R)
    g = int(np.argmax(np.maximum(u_circ, u_lin)))
    gc = int(np.argmax(u_circ))
    return dict(xc=xc, yc=yc, T=T / 1e3, J=J, v=v, t=t, tp=tp, v_max=v.max(), t_max=t.max(),
                u_circ=u_circ, u_lin=u_lin, u_max_circ=u_circ[gc], u_max_lin=u_lin.max(),
                governing=g, governing_circ=gc, c=c, res_Mx=res_Mx, res_My=res_My)


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
# Pattern constants used by the probes.
G6 = [(x, y, A0) for x in (0, 80, 160) for y in (0, 100)]
TRI = [(0, 0, A0), (100, 0, A0), (0, 100, A0)]
COL = [(0, 0, A0), (100, 0, A0), (200, 0, A0)]
DIFFA = [(0, 0, 50), (100, 0, 150), (100, 100, 300), (0, 100, 80)]
OFF = [(x + 5e4, y + 5e4, A0) for x in range(0, 500, 100) for y in range(0, 500, 100)]
RING = [(1000 * math.cos(2 * math.pi * i / 100), 1000 * math.sin(2 * math.pi * i / 100), A0) for i in range(100)]
TWO = [(0, 0, A0), (150, 0, A0)]
TINY = [(0, 0, A0), (1e-4, 0, A0)]

# Product values come from the Probe tool (C# calculator): (name, pattern, loads, outputs, governing bolt no.).
# The product reports v_max, t_max and u_lin for its governing bolt (highest circular interaction).
EXTRAS = [
    ('grid6-all-loads', G6, {'F_x': 20, 'F_y': -80, 'e_x': 250, 'e_y': 30, 'T_z': 2, 'N': 30, 'M_x': 15, 'M_y': -8, 'V_R': 60, 'T_R': 90},
     {'T': -11.2, 'J': 3422580.0, 'v_max': 19.2256627, 't_max': 80.0, 'u_max': 0.944879556, 'u_lin': 0.955348347}, 2),
    ('grid6-prying', G6, {'F_x': 20, 'F_y': -80, 'e_x': 250, 'e_y': 30, 'T_z': 2, 'N': 30, 'M_x': 15, 'M_y': -8, 'V_R': 60, 'T_R': 90, 'a': 30, 'b': 60},
     {'T': -11.2, 'J': 3422580.0, 'v_max': 19.2256627, 't_max': 240.0, 'u_max': 2.68584903, 'u_lin': 2.22518962}, 2),
    ('two-bolts', TWO, {'F_y': -30, 'e_x': 300, 'e_y': 0, 'M_y': 5, 'V_R': 40, 'T_R': 50},
     {'T': -6.75, 'J': 948375.0, 'v_max': 60.0, 't_max': 33.3333333, 'u_max': 1.6414763, 'u_lin': 1.97619048}, 2),
    ('grid6-compression', G6, {'N': -100, 'M_x': 5, 'F_y': -20, 'e_x': 100, 'V_R': 40, 'T_R': 50},
     {'T': -0.4, 'J': 3422580.0, 'v_max': 4.15084518, 't_max': 0.0, 'u_max': 0.103771129, 'u_lin': 0.103771129}, 5),
    ('offset-5x5', OFF, {'F_y': -900, 'e_x': 50300, 'e_y': 50000, 'N': 200, 'M_x': 30, 'M_y': 20, 'V_R': 200, 'T_R': 300},
     {'T': -90.0, 'J': 84300000.0, 'v_max': 56.9209979, 't_max': 28.0, 'u_max': 0.299518132, 'u_lin': 0.351271656}, 25),
    ('ring-100', RING, {'F_x': 100, 'F_y': -500, 'e_x': 3000, 'e_y': 0, 'T_z': 50, 'N': -300, 'M_x': 100, 'M_y': 0, 'V_R': 40, 'T_R': 60},
     {'T': -1450.0, 'J': 8430000000.0, 'v_max': 19.5988701, 't_max': 0.0, 'u_max': 0.489971753, 'u_lin': 0.489971753}, 4),
    ('tiny-spread', TINY, {'F_y': -10, 'e_x': 100, 'V_R': 40},
     {'T': -0.9999995, 'J': 4.215e-07, 'v_max': 10000000.0, 't_max': 0.0, 'u_max': 250000.0, 'u_lin': 250000.0}, 2),
    ('zero-loads', G6, {'F_y': 0, 'e_x': 0},
     {'T': 0.0, 'J': 3422580.0, 'v_max': 0.0, 't_max': 0.0, 'u_max': 0.0, 'u_lin': 0.0}, 1),
    ('prying-a-only', G6, {'F_y': -10, 'e_x': 0, 'N': 60, 'V_R': 40, 'T_R': 50, 'a': 30},
     {'T': 0.8, 'J': 3422580.0, 'v_max': 3.38937298, 't_max': 10.0, 'u_max': 0.217209359, 'u_lin': 0.227591467}, 1),
    ('linear-mismatch', G6, {'F_x': 25, 'F_y': -75, 'e_x': 300, 'e_y': 100, 'N': 55, 'M_x': -5, 'M_y': 0, 'V_R': 60, 'T_R': 90},
     {'T': -17.75, 'J': 3422580.0, 'v_max': 54.1412742, 't_max': 0.0, 'u_max': 0.90235457, 'u_lin': 0.90235457}, 6),
    ('unequal-areas', DIFFA, {'F_x': 10, 'F_y': -40, 'e_x': 180, 'e_y': 20, 'N': 20, 'M_x': 3, 'V_R': 40, 'T_R': 50},
     {'T': -3.64137931, 'J': 2318965.52, 'v_max': 37.8830065, 't_max': 34.0290381, 'u_max': 1.16625106, 'u_lin': 1.43320428}, 3),
    ('triangle-Mx', TRI, {'M_x': 10, 'V_R': 40, 'T_R': 50, 'F_y': 0},
     {'T': 0.0, 'J': 1124000.0, 'v_max': 0.0, 't_max': 100.0, 'u_max': 2.0, 'u_lin': 1.42857143}, 3),
    ('triangle-Mx-My', TRI, {'M_x': 10, 'M_y': 10, 'V_R': 40, 'T_R': 50, 'F_y': 0},
     {'T': 0.0, 'J': 1124000.0, 'v_max': 0.0, 't_max': 50.0, 'u_max': 1.0, 'u_lin': 0.714285714}, 2),
    ('collinear-Mx', COL, {'M_x': 20, 'V_R': 40, 'T_R': 50, 'F_y': 0},
     {'T': 0.0, 'J': 1686000.0, 'v_max': 0.0, 't_max': 0.0, 'u_max': 0.0, 'u_lin': 0.0}, 1),
]

EX_TOL = 1e-4
for name, bl, kw, prod, gov in EXTRAS:
    p = group(bl, **kw)
    d = group(bl, tension="decoupled", **kw)
    gc = p["governing_circ"]
    check("EXTRA", name, "T", prod["T"], p["T"], EX_TOL)
    check("EXTRA", name, "J", prod["J"], p["J"], EX_TOL)
    # Same-bolt comparison: independent plane-section values at the bolt the product calls governing.
    check("EXTRA", name, "v_max(gov)", prod["v_max"], p["v"][gc], EX_TOL)
    check("EXTRA", name, "t_max(gov)", prod["t_max"], p["tp"][gc], EX_TOL)
    check("EXTRA", name, "u_max", prod["u_max"], p["u_circ"][gc], EX_TOL)
    check("EXTRA", name, "u_lin(gov)", prod["u_lin"], p["u_lin"][gc], EX_TOL)
    # Group-wide maxima against what the product reports (finding rows).
    check("GROUP", name, "v_max(all)", prod["v_max"], p["v"].max(), EX_TOL)
    check("GROUP", name, "t_max(all)", prod["t_max"], p["tp"].max(), EX_TOL)
    check("GROUP", name, "u_lin(all)", prod["u_lin"], p["u_lin"].max(), EX_TOL)
    # Root cause of tension differences: the decoupled formula reproduces the product.
    check("DECOUP", name, "t_max(gov)", prod["t_max"], d["tp"][int(np.argmax(d["u_circ"]))], EX_TOL)
    check("DECOUP", name, "u_max", prod["u_max"], d["u_max_circ"], EX_TOL)
    # Moment equilibrium of the decoupled distribution (1.0 means the applied moment is resisted).
    for sym, applied, got in (("Mx_resisted", kw.get("M_x", 0) * 1e3, d["res_Mx"]), ("My_resisted", kw.get("M_y", 0) * 1e3, d["res_My"])):
        if applied != 0:
            check("EQUIL", name, sym, 1.0, got / applied, EX_TOL)

# ---------------------------------------------------------------- report
print(f"{'kind':6} {'case':24} {'symbol':14} {'expected':>13} {'independent':>13} {'rel.diff':>10} {'tol':>7}  result")
for kind, c, s, e, v, r, t, ok in rows:
    print(f"{kind:6} {c:24} {s:14} {e:13.6g} {v:13.6g} {r:10.2e} {t:7.0e}  {'PASS' if ok else 'FAIL'}")
kat_bad = sum(1 for r in rows[:n_kat] if not r[7])
ex = rows[n_kat:]
print(f"KAT rows pass: {n_kat - kat_bad}/{n_kat}; extra rows pass: {sum(1 for r in ex if r[7])}/{len(ex)}")
sys.exit(1 if kat_bad else 0)
