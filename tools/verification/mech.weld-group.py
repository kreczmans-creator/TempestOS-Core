#!/usr/bin/env python3
"""Independent re-derivation of mech.weld-group (Track P, P1).

Sources, cited by title and number only:
- Shigley's Mechanical Engineering Design, 11th ed., Chapter 9, Tables 9-1 and 9-2: welds treated as lines.
- Blodgett, Design of Welded Structures (1966), section 7.4: the weld as a line.
- EN 1993-1-8:2005, clause 4.5.3.3: simplified method, resultant force per unit length against F_w,Rd.
Units inside the model: mm, kN, kN*mm. Force per unit length is reported in N/mm.

Model (rigid parts, elastic):
- Each weld is a straight line of unit throat. Line properties include the own-axis term l*d^2/12.
- In-plane: direct F/L_w plus torsion (T/J_u) times the radius, perpendicular to it.
- Out-of-plane: F_z/L_w plus a plane-section law. F_z at (e_x, e_y) adds F_z(e_y - y_c) to M_x and F_z(e_x - x_c) to M_y. It uses I_ux, I_uy and the product I_uxy, so the
  distribution is in moment equilibrium for any layout.
- The resultant f_r is the 3D vector sum. Its magnitude is convex along a line, so the maximum is at an end.
- Throat a = 0.707 s, tau = f_r,max / a, s_req = f_r,max / (0.707 f_w), u = tau / f_w.

Exit code: 0 when every KAT row passes (tolerance max(KAT tol, 1e-6)), else 1.
EXTRA rows compare product values read from the Probe tool. They do not set the exit code.
Product version 1.1.0 (since 3c637a3d) solves the plane-section law with I_uxy and blocks only a collinear group whose moment
acts about the line. The one-line cases are no longer refused when no such moment acts.
"""
import math
import sys

import numpy as np

THROAT = 0.707


def weld(welds, s=6.0, f_w=220.0, F_x=0.0, F_y=0.0, F_z=0.0, e_x=0.0, e_y=0.0,
         M_x=0.0, M_y=0.0, M_z=0.0, outofplane="plane"):
    """welds: list of (x1, y1, x2, y2) in mm. Forces in kN, moments in kN*m. Returns a dict."""
    W = np.array(welds, dtype=float)
    x1, y1, x2, y2 = W.T
    dx, dy = x2 - x1, y2 - y1
    l = np.hypot(dx, dy)
    xm, ym = (x1 + x2) / 2, (y1 + y2) / 2
    Lw = l.sum()
    xc, yc = (l * xm).sum() / Lw, (l * ym).sum() / Lw
    ax, ay = xm - xc, ym - yc
    Iux = (l * ay ** 2 + l * dy ** 2 / 12).sum()
    Iuy = (l * ax ** 2 + l * dx ** 2 / 12).sum()
    Iuxy = (l * ax * ay + l * dx * dy / 12).sum()
    Ju = Iux + Iuy
    T = F_y * (e_x - xc) - F_x * (e_y - yc) + M_z * 1e3          # kN*mm
    # Out-of-plane coefficients: f_z = F_z/Lw + c1 x' + c2 y'  (kN/mm)
    # F_z acts at (e_x, e_y): its eccentricity about the centroid adds to the out-of-plane moments (v0.26.0, DA-17).
    Mx_tot = M_x * 1e3 + F_z * (e_y - yc)       # kN*mm
    My_tot = M_y * 1e3 + F_z * (e_x - xc)
    mat = np.array([[Iuy, Iuxy], [Iuxy, Iux]])
    rhs = np.array([My_tot, Mx_tot])
    if outofplane == "decoupled":
        c = np.array([My_tot / Iuy if Iuy > 1e-12 else 0.0, Mx_tot / Iux if Iux > 1e-12 else 0.0])
    elif abs(np.linalg.det(mat)) < 1e-9 * max(Iux * Iuy, 1.0):
        c = np.linalg.lstsq(mat, rhs, rcond=None)[0]
    else:
        c = np.linalg.solve(mat, rhs)
    # Ends: two per weld.
    px = np.concatenate([x1, x2]) - xc
    py = np.concatenate([y1, y2]) - yc
    fx = F_x / Lw + (T / Ju) * (-py) if Ju > 0 else F_x / Lw + 0 * px
    fy = F_y / Lw + (T / Ju) * (px) if Ju > 0 else F_y / Lw + 0 * px
    fz = F_z / Lw + c[0] * px + c[1] * py
    fr = 1e3 * np.sqrt(fx ** 2 + fy ** 2 + fz ** 2)               # N/mm
    k = int(np.argmax(fr))
    frmax = fr[k]
    a = THROAT * s
    tau = frmax / a
    return dict(L_w=Lw, x_c=xc, y_c=yc, I_ux=Iux, I_uy=Iuy, I_uxy=Iuxy, J_u=Ju, T=T / 1e3,
                f_r_max=frmax, a=a, tau=tau, s_req=frmax / (THROAT * f_w), u=tau / f_w,
                crit=(px[k] + xc, py[k] + yc), fr=fr)


V2 = [(0, 0, 0, 100), (50, 0, 50, 100)]
BOX = [(0, 0, 100, 0), (100, 0, 100, 100), (100, 100, 0, 100), (0, 100, 0, 0)]
H2 = [(0, -50, 100, -50), (0, 50, 100, 50)]

rows = []   # (kind, case, symbol, expected, value, rel, tol, ok)


def check(kind, name, sym, expected, value, tol):
    rel = abs(value - expected) / max(abs(expected), 1e-6)      # absolute below 1e-6
    rows.append((kind, name, sym, expected, value, rel, tol, rel <= max(tol, 1e-6)))


# ---------------------------------------------------------------- KATs (exit code)
w1 = weld(V2, s=6, f_w=220, F_y=-20, e_x=200, e_y=50)
for sym, exp, tol in [("L_w", 200, 1e-9), ("J_u", 291666.7, 1e-6), ("T", -3.5, 1e-9),
                      ("f_r_max", 721.1103, 1e-6), ("tau", 169.9930, 1e-5)]:
    check("KAT", "ka-two-vertical", sym, exp, w1[sym], tol)
w2 = weld(BOX, F_y=0, e_x=50, e_y=50, M_z=2)
for sym, exp, tol in [("J_u", 1333333.3, 1e-6), ("f_r_max", 106.0660, 1e-6)]:
    check("KAT", "ka-box-torsion", sym, exp, w2[sym], tol)
w3 = weld(H2, F_y=10, e_x=50, e_y=0, M_x=5)
for sym, exp, tol in [("I_ux", 500000, 1e-9), ("f_r_max", 502.4938, 1e-6), ("s_req", 3.230640, 1e-5)]:
    check("KAT", "ka-two-horizontal-bending", sym, exp, w3[sym], tol)
w4 = weld([(0, 0, 150, 0), (0, 0, 0, 50)], F_y=0, M_x=1)
for sym, exp, tol in [("I_ux", 33854.16667, 1e-9), ("I_uy", 492187.5, 1e-9), ("I_uxy", -70312.5, 1e-9), ("f_r_max", 1500.0, 1e-9)]:
    check("KAT", "ka-l-group-bending", sym, exp, w4[sym], tol)
n_kat = len(rows)

# ---------------------------------------------------------------- extra checks
L = [(0, 0, 150, 0), (0, 0, 0, 100)]
C = [(0, 0, 60, 0), (0, 0, 0, 100), (0, 100, 60, 100)]
TRAP = [(0, 0, 200, 0), (0, 0, 50, 120), (200, 0, 150, 120), (50, 120, 150, 120)]
BIG = [(-2000, -2000, 2000, -2000), (-2000, 2000, 2000, 2000), (2000, -2000, 2000, 2000), (-2000, -2000, -2000, 2000)]
RING = [(100 * math.cos(2 * math.pi * i / 48), 100 * math.sin(2 * math.pi * i / 48),
         100 * math.cos(2 * math.pi * (i + 1) / 48), 100 * math.sin(2 * math.pi * (i + 1) / 48)) for i in range(48)]
RECT = [(0, 0, 60, 0), (60, 0, 60, 140), (60, 140, 0, 140), (0, 140, 0, 0)]
H2 = [(0, 0, 100, 0), (0, 50, 100, 50)]
H2Z = H2 + [(10, 10, 10, 10)]      # the zero-length row has no effect

# Product values come from the Probe tool (C# calculator): (name, layout, inputs, outputs).
EXTRAS = [
    ('L-shape-in-plane', L, {'F_x': 5, 'F_y': -30, 'e_x': 200, 'e_y': -40, 'M_z': 1},
     {'L_w': 250.0, 'x_c': 45.0, 'y_c': 20.0, 'I_ux': 233333.3333, 'I_uy': 618750.0, 'I_uxy': -225000.0, 'J_u': 852083.3333, 'T': -3.35, 'f_r_max': 536.0279074, 'tau': 126.3620715, 's_req': 3.446238314, 'u': 0.5743730523}),
    ('L-shape-Mx-only', L, {'F_z': 10, 'M_x': 5, 'F_y': 0, 'e_x': 0, 'e_y': 0},
     {'L_w': 250.0, 'x_c': 45.0, 'y_c': 20.0, 'I_ux': 233333.3333, 'I_uy': 618750.0, 'I_uxy': -225000.0, 'J_u': 852083.3333, 'T': 0.0, 'f_r_max': 2020.0, 'tau': 476.1904762, 's_req': 12.98701299, 'u': 2.164502165}),
    ('L-shape-Mx-My', L, {'F_z': 10, 'M_x': 3, 'M_y': 4, 'F_y': 0, 'e_x': 0, 'e_y': 0},
     {'L_w': 250.0, 'x_c': 45.0, 'y_c': 20.0, 'I_ux': 233333.3333, 'I_uy': 618750.0, 'I_uxy': -225000.0, 'J_u': 852083.3333, 'T': 0.0, 'f_r_max': 1500.0, 'tau': 353.6067893, 's_req': 9.643821525, 'u': 1.607303588}),
    ('C-section', C, {'F_y': -25, 'e_x': 180, 'e_y': 50, 's': 5, 'f_w': 180},
     {'L_w': 220.0, 'x_c': 16.36363636, 'y_c': 50.0, 'I_ux': 383333.3333, 'I_uy': 85090.90909, 'I_uxy': 0.0, 'J_u': 468424.2424, 'T': -4.090909091, 'f_r_max': 659.8739283, 'tau': 186.6687209, 's_req': 5.185242246, 'u': 1.037048449}),
    ('trapezoid-3D', TRAP, {'F_x': -12, 'F_y': -40, 'F_z': 15, 'e_x': 300, 'e_y': 60, 'M_x': 2, 'M_z': -1},
     {'L_w': 560.0, 'x_c': 100.0, 'y_c': 49.28571429, 'I_ux': 1327714.286, 'I_uy': 2266666.667, 'I_uxy': 0.0, 'J_u': 3594380.952, 'T': -8.871428571, 'f_r_max': 357.7402997, 'tau': 84.3329325, 's_req': 2.299989068, 'u': 0.3833315114}),
    ('ring-48-torsion', RING, {'F_y': 0, 'e_x': 0, 'e_y': 0, 'M_z': 10, 's': 6},
     {'L_w': 627.8700406, 'x_c': 2.897079469e-15, 'y_c': -1.335372568e-15, 'I_ux': 3130397.678, 'I_uy': 3130397.678, 'I_uxy': -3.637978807e-11, 'J_u': 6260795.355, 'T': 10.0, 'f_r_max': 159.7241154, 'tau': 37.65302108, 's_req': 1.026900575, 'u': 0.1711500958}),
    ('rect-60x140-torsion', RECT, {'F_y': 0, 'e_x': 30, 'e_y': 70, 'M_z': 4},
     {'L_w': 400.0, 'x_c': 30.0, 'y_c': 70.0, 'I_ux': 1045333.333, 'I_uy': 288000.0, 'I_uxy': 0.0, 'J_u': 1333333.333, 'T': 4.0, 'f_r_max': 228.4731932, 'tau': 53.85978151, 's_req': 1.468903132, 'u': 0.2448171887}),
    ('zero-loads', H2, {'F_y': 0},
     {'L_w': 200.0, 'x_c': 50.0, 'y_c': 25.0, 'I_ux': 125000.0, 'I_uy': 166666.6667, 'I_uxy': 0.0, 'J_u': 291666.6667, 'T': 0.0, 'f_r_max': 0.0, 'tau': 0.0, 's_req': 0.0, 'u': 0.0}),
    ('tiny-load', H2, {'F_y': -1e-06, 'e_x': 0, 'e_y': 0},
     {'L_w': 200.0, 'x_c': 50.0, 'y_c': 25.0, 'I_ux': 125000.0, 'I_uy': 166666.6667, 'I_uxy': 0.0, 'J_u': 291666.6667, 'T': 5e-08, 'f_r_max': 1.423204203e-05, 'tau': 3.355031125e-06, 's_req': 9.150084886e-08, 'u': 1.525014148e-08}),
    ('large-4000mm-box', BIG, {'F_y': -90000, 'F_z': 5000, 'e_x': 1500, 'e_y': 0, 'M_x': 800, 'M_y': 500, 's': 25, 'f_w': 600},
     {'L_w': 16000.0, 'x_c': 0.0, 'y_c': 0.0, 'I_ux': 42666666670.0, 'I_uy': 42666666670.0, 'I_uxy': 0.0, 'J_u': 85333333330.0, 'T': -135000.0, 'f_r_max': 9369.340219, 'tau': 530.0899699, 's_req': 22.08708208, 'u': 0.8834832832}),
    ('zero-length-row', H2Z, {'F_y': -10, 'e_x': 50, 'e_y': 40, 'M_x': 1},
     {'L_w': 200.0, 'x_c': 50.0, 'y_c': 25.0, 'I_ux': 125000.0, 'I_uy': 166666.6667, 'I_uxy': 0.0, 'J_u': 291666.6667, 'T': 0.0, 'f_r_max': 206.1552813, 'tau': 48.59860473, 's_req': 1.325416493, 'u': 0.2209027488}),
    ('leg-3mm', H2, {'F_y': -10, 'e_x': 50, 'e_y': 40, 's': 3},
     {'L_w': 200.0, 'x_c': 50.0, 'y_c': 25.0, 'I_ux': 125000.0, 'I_uy': 166666.6667, 'I_uxy': 0.0, 'J_u': 291666.6667, 'T': 0.0, 'f_r_max': 50.0, 'tau': 23.57378595, 's_req': 0.3214607175, 'u': 0.1071535725}),
    ('single-vertical (Table 9-1 case 1)', [(0, 0, 0, 100)], {'F_y': -10, 'e_x': 100, 'e_y': 50},
     {'L_w': 100.0, 'x_c': 0.0, 'y_c': 50.0, 'I_ux': 83333.33333, 'I_uy': 0.0, 'I_uxy': 0.0, 'J_u': 83333.33333, 'T': -1.0, 'f_r_max': 608.276253, 'tau': 143.3937419, 's_req': 3.910738415, 'u': 0.6517897358}),
    ('vertical-Fz-off-centre', [(0, 0, 0, 100)], {'F_z': 10, 'e_x': 0, 'e_y': 80},
     {'L_w': 100.0, 'x_c': 0.0, 'y_c': 50.0, 'I_ux': 83333.33333, 'I_uy': 0.0, 'I_uxy': 0.0, 'J_u': 83333.33333, 'T': 0.0, 'f_r_max': 280.0, 'tau': 66.00660066, 's_req': 1.800180018, 'u': 0.300030003}),
    ('horizontal-My', [(0, 0, 100, 0)], {'M_y': 5},
     {'L_w': 100.0, 'x_c': 50.0, 'y_c': 0.0, 'I_ux': 0.0, 'I_uy': 83333.33333, 'I_uxy': 0.0, 'J_u': 83333.33333, 'T': 0.0, 'f_r_max': 3000.0, 'tau': 707.2135785, 's_req': 19.28764305, 'u': 3.214607175}),
    ('diagonal-Mx-My-along', [(0, 0, 100, 100)], {'M_x': 5, 'M_y': 5},
     {'L_w': 141.4213562, 'x_c': 50.0, 'y_c': 50.0, 'I_ux': 117851.1302, 'I_uy': 117851.1302, 'I_uxy': 117851.1302, 'J_u': 235702.2604, 'T': 0.0, 'f_r_max': 2121.320344, 'tau': 500.0755171, 's_req': 13.63842319, 'u': 2.273070532}),
    ('L-Fz-at-corner', L, {'F_z': 10, 'e_x': 0, 'e_y': 0},
     {'L_w': 250.0, 'x_c': 45.0, 'y_c': 20.0, 'I_ux': 233333.3333, 'I_uy': 618750.0, 'I_uxy': -225000.0, 'J_u': 852083.3333, 'T': 0.0, 'f_r_max': 160.0, 'tau': 37.71805752, 's_req': 1.028674296, 'u': 0.171445716}),
    ('L-My-only', L, {'M_y': 4},
     {'L_w': 250.0, 'x_c': 45.0, 'y_c': 20.0, 'I_ux': 233333.3333, 'I_uy': 618750.0, 'I_uxy': -225000.0, 'J_u': 852083.3333, 'T': 0.0, 'f_r_max': 853.3333333, 'tau': 201.1629734, 's_req': 5.486262912, 'u': 0.914377152}),
]
KEYS = ["L_w", "x_c", "y_c", "I_ux", "I_uy", "I_uxy", "J_u", "T", "f_r_max", "tau", "s_req", "u"]
for name, wl, kw, prod in EXTRAS:
    p = weld([w for w in wl if (w[0], w[1]) != (w[2], w[3])], **kw)
    for k in KEYS:
        if k == "I_uxy" and abs(prod[k]) < 1e-3 and abs(p[k]) < 1e-3:
            continue          # both are rounding noise around zero
        check("EXTRA", name, k, prod[k], p[k], 1e-6)

# Closed forms from Shigley, 11th ed., Table 9-1 (independent of the numeric kernel).
b_, d_ = 60.0, 100.0
Ju_c = (8 * b_ ** 3 + 6 * b_ * d_ ** 2 + d_ ** 3) / 12 - b_ ** 4 / (2 * b_ + d_)       # case 3, C-shape
check("CLOSED", "C-section", "J_u", dict((e[0], e[3]) for e in EXTRAS)["C-section"]["J_u"], Ju_c, 1e-6)
check("CLOSED", "rect-60x140-torsion", "J_u", dict((e[0], e[3]) for e in EXTRAS)["rect-60x140-torsion"]["J_u"], (60 + 140) ** 3 / 6, 1e-6)
ring_J = dict((e[0], e[3]) for e in EXTRAS)["ring-48-torsion"]["J_u"]
check("CLOSED", "ring-48-torsion", "J_u(2*pi*r^3)", 2 * math.pi * 100 ** 3, ring_J, 5e-3)   # 48-sided polygon, 0.4 % below the circle

# Guard checks.  Product 1.1.0 blocks a collinear group only when the out-of-plane moment has a part across the line.
def blocked(welds_, F_z=0.0, e_x=0.0, e_y=0.0, M_x=0.0, M_y=0.0, **_):
    W = np.array(welds_, dtype=float)
    x1, y1, x2, y2 = W.T
    dx, dy = x2 - x1, y2 - y1
    l = np.hypot(dx, dy)
    xm, ym = (x1 + x2) / 2, (y1 + y2) / 2
    xc, yc = (l * xm).sum() / l.sum(), (l * ym).sum() / l.sum()
    ax, ay = xm - xc, ym - yc
    Iux, Iuy, Iuxy = (l * ay ** 2 + l * dy ** 2 / 12).sum(), (l * ax ** 2 + l * dx ** 2 / 12).sum(), (l * ax * ay + l * dx * dy / 12).sum()
    if abs(Iux * Iuy - Iuxy ** 2) > 1e-9 * (Iux + Iuy) ** 2:
        return False
    rhs = np.array([M_y * 1e3 + F_z * (e_x - xc), M_x * 1e3 + F_z * (e_y - yc)])
    mat = np.array([[Iuy, Iuxy], [Iuxy, Iux]])
    c = np.linalg.lstsq(mat, rhs, rcond=None)[0]
    return np.linalg.norm(mat @ c - rhs) > 1e-6 * max(np.linalg.norm(rhs), 1e-30)


for name, wl, kw, prod_blocked in (
        ("single-vertical, in-plane load only", [(0, 0, 0, 100)], {"F_y": -10, "e_x": 100, "e_y": 50}, 0),
        ("single-vertical, F_z with e_x 10 (moment about the line)", [(0, 0, 0, 100)], {"F_z": 10, "e_x": 10, "e_y": 50}, 1),
        ("single-diagonal, M_x 5 (old -03)", [(0, 0, 100, 100)], {"M_x": 5}, 1),
        ("single-diagonal, M_x 5 and M_y -5 (across the line)", [(0, 0, 100, 100)], {"M_x": 5, "M_y": -5}, 1),
        ("single-diagonal, M_x 5 and M_y 5 (along the line)", [(0, 0, 100, 100)], {"M_x": 5, "M_y": 5}, 0),
        ("single-horizontal, M_x 5", [(0, 0, 100, 0)], {"M_x": 5}, 1),
        ("single-horizontal, M_y 5", [(0, 0, 100, 0)], {"M_y": 5}, 0)):
    check("BLOCK", name, "blocked (1 = yes)", float(prod_blocked), 1.0 if blocked(wl, **kw) else 0.0, 1e-9)

# ---------------------------------------------------------------- report
print(f"{'kind':6} {'case':26} {'symbol':14} {'expected':>13} {'independent':>13} {'rel.diff':>10} {'tol':>7}  result")
for kind, c, s_, e, v, r, t, ok in rows:
    print(f"{kind:6} {c:26} {s_:14} {e:13.6g} {v:13.6g} {r:10.2e} {t:7.0e}  {'PASS' if ok else 'FAIL'}")
kat_bad = sum(1 for r in rows[:n_kat] if not r[7])
ex = rows[n_kat:]
print(f"KAT rows pass: {n_kat - kat_bad}/{n_kat}; extra rows pass: {sum(1 for r in ex if r[7])}/{len(ex)}")
sys.exit(1 if kat_bad else 0)
