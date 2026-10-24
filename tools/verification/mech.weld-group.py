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
- Out-of-plane: F_z/L_w plus a plane-section law. It uses I_ux, I_uy and the product I_uxy, so the
  distribution is in moment equilibrium for any layout.
- The resultant f_r is the 3D vector sum. Its magnitude is convex along a line, so the maximum is at an end.
- Throat a = 0.707 s, tau = f_r,max / a, s_req = f_r,max / (0.707 f_w), u = tau / f_w.

Exit code: 0 when every KAT row passes (tolerance max(KAT tol, 1e-6)), else 1.
EXTRA rows compare product values read from the Probe tool. They do not set the exit code.
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
    mat = np.array([[Iuy, Iuxy], [Iuxy, Iux]])
    rhs = np.array([M_y * 1e3, M_x * 1e3])
    if outofplane == "decoupled":
        c = np.array([M_y * 1e3 / Iuy if Iuy > 1e-12 else 0.0, M_x * 1e3 / Iux if Iux > 1e-12 else 0.0])
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
                crit=(px[k] + xc, py[k] + yc), fr=fr, res_Mx=float(1e3 * (fz * 0).sum()))


V2 = [(0, 0, 0, 100), (50, 0, 50, 100)]
BOX = [(0, 0, 100, 0), (100, 0, 100, 100), (100, 100, 0, 100), (0, 100, 0, 0)]
H2 = [(0, -50, 100, -50), (0, 50, 100, 50)]

rows = []   # (kind, case, symbol, expected, value, rel, tol, ok)


def check(kind, name, sym, expected, value, tol):
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
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
n_kat = len(rows)

# ---------------------------------------------------------------- extra checks
EXTRAS = []   # filled after probing the product

# ---------------------------------------------------------------- report
print(f"{'kind':6} {'case':26} {'symbol':14} {'expected':>13} {'independent':>13} {'rel.diff':>10} {'tol':>7}  result")
for kind, c, s_, e, v, r, t, ok in rows:
    print(f"{kind:6} {c:26} {s_:14} {e:13.6g} {v:13.6g} {r:10.2e} {t:7.0e}  {'PASS' if ok else 'FAIL'}")
kat_bad = sum(1 for r in rows[:n_kat] if not r[7])
ex = rows[n_kat:]
print(f"KAT rows pass: {n_kat - kat_bad}/{n_kat}; extra rows pass: {sum(1 for r in ex if r[7])}/{len(ex)}")
sys.exit(1 if kat_bad else 0)
