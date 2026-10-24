#!/usr/bin/env python3
"""Independent re-derivation of bs.duct-sizing (Track P, P1).

Written without reading the C# Calculate method. Method:
  - dry air: rho = p / (R T), R = 287.055 J/(kg K); Sutherland viscosity
    mu = 1.458e-6 T^1.5 / (T + 110.4)  (T in K, mu in Pa.s)
  - Darcy-Weisbach friction rate dp/L = f rho v^2 / (2 D), Colebrook-White
    solved to full convergence (laminar f = 64/Re below Re 2300)
  - equal friction: D from the target rate; velocity method: D = sqrt(4Q/(pi v))
  - rectangular: D_e = 1.30 (ab)^0.625 / (a + b)^0.25   (ASHRAE Fundamentals Ch. 21)
  - flat oval:   D_e = 1.55 A^0.625 / P^0.25, A = pi b^2/4 + b (a - b), P = pi b + 2 (a - b)
  - friction through the equivalent round duct at the same flow (v_e = 4Q/(pi D_e^2))
  - fitting loss = C rho v^2 / 2 at the actual section velocity
  - sizes rounded up to a multiple of the step
Run: python3 tools/verification/bs.duct-sizing.py   (exit 0 = all rows PASS)
"""
import math
import sys

from scipy.optimize import brentq

ROWS = []


def row(case, symbol, expected, value, tol, note=""):
    tol_eff = max(tol, 1e-6)
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    ok = rel <= tol_eff
    ROWS.append((case, symbol, expected, value, rel, tol, ok, note))
    return ok


def finding(case, symbol, product, indep, ref):
    rel = abs(indep - product) / abs(indep) if indep else abs(indep - product)
    ROWS.append((case, symbol, product, indep, rel, 0.0, None, "FINDING " + ref))


# ----------------------------------------------------------------- air
def air(T_C=20.0, p_kPa=101.325):
    T = T_C + 273.15
    rho = p_kPa * 1000 / (287.055 * T)
    mu = 1.458e-6 * T**1.5 / (T + 110.4)
    return rho, mu


def colebrook(re, rr):
    g = lambda f: 1 / math.sqrt(f) + 2 * math.log10(rr / 3.7 + 2.51 / (re * math.sqrt(f)))
    return brentq(g, 1e-4, 2.0, xtol=1e-15, rtol=1e-14)


def friction(re, rr):
    return 64 / re if re < 2300 else colebrook(re, rr)


def round_duct(Q, D, eps_mm, rho, mu):
    """Friction in a round duct. Q m3/s, D m. Returns dict."""
    v = 4 * Q / (math.pi * D * D)
    re = rho * v * D / mu
    f = friction(re, eps_mm / 1000 / D)
    dpl = f * rho * v * v / (2 * D)
    return dict(v=v, Re=re, f=f, dpL=dpl)


def De_rect(a, b):
    return 1.30 * (a * b) ** 0.625 / (a + b) ** 0.25


def De_oval(a, b):
    A = math.pi * b * b / 4 + b * (a - b)
    P = math.pi * b + 2 * (a - b)
    return 1.55 * A**0.625 / P**0.25


def area_oval(a, b):
    return math.pi * b * b / 4 + b * (a - b)


def ceil_to(x_mm, step):
    return math.ceil(x_mm / step - 1e-9) * step


def section(Q, L, shape, a, D, C, dp_eq, eps_mm, rho, mu):
    """Analyse a section of given size (mm). shape: round | rect | oval. Returns dict."""
    if shape == "round":
        De = D / 1000
        area = math.pi * De * De / 4
    elif shape == "rect":
        De = De_rect(a / 1000, D / 1000)
        area = a / 1000 * D / 1000
    else:
        De = De_oval(a / 1000, D / 1000)
        area = area_oval(a / 1000, D / 1000)
    r = round_duct(Q, De, eps_mm, rho, mu)
    v = Q / area
    out = dict(De=De * 1000, v=v, Re=r["Re"], f=r["f"], dpL=r["dpL"])
    out["dp_f"] = r["dpL"] * L
    out["dp_fit"] = C * rho * v * v / 2
    out["dp"] = out["dp_f"] + out["dp_fit"] + dp_eq
    return out


def size_equal_friction_round(Q, target, eps_mm, rho, mu):
    f = lambda D: round_duct(Q, D, eps_mm, rho, mu)["dpL"] - target
    return brentq(f, 0.01, 10.0, xtol=1e-14) * 1000      # mm


def size_equal_friction_De(Q, target, eps_mm, rho, mu):
    return size_equal_friction_round(Q, target, eps_mm, rho, mu)   # same equivalent diameter, mm


# ================================================================ KATs
def kat():
    rho, mu = air(20, 101.325)
    # ka-round-given: 1 m3/s, 400 mm, eps 0.09 mm, L = 10 m, no fittings
    c = "ka-round-given"
    s = section(1.0, 10, "round", 0, 400, 0, 0, 0.09, rho, mu)
    row(c, "v_s1", 7.958, s["v"], 1e-3)
    row(c, "f_s1", 0.01714, s["f"], 5e-3)
    row(c, "dpL_s1", 1.633, s["dpL"], 5e-3)
    row(c, "dp_index", 16.33, s["dp"], 5e-3)

    # ka-rectangular-equivalent: 0.8 m3/s, 600 x 300, L = 12, C = 0.5, eps default 0.09 mm
    c = "ka-rectangular-equivalent"
    s = section(0.8, 12, "rect", 600, 300, 0.5, 0, 0.09, rho, mu)
    row(c, "De_s1", 457.3, s["De"], 2e-3)
    row(c, "v_s1", 4.444, s["v"], 1e-3)
    row(c, "dpL_s1", 0.556, s["dpL"], 1e-2)

    # ka-velocity-method: 0.5 m3/s at 6 m/s, round_to 50, C = 1.2, dp_eq = 50 Pa, L = 8
    c = "ka-velocity-method"
    D0 = math.sqrt(4 * 0.5 / (math.pi * 6)) * 1000
    D = ceil_to(D0, 50)
    s = section(0.5, 8, "round", 0, D, 1.2, 50, 0.09, rho, mu)
    row(c, "D_s1", 350, D, 1e-9)
    row(c, "v_s1", 5.197, s["v"], 1e-3)
    row(c, "dp_fit_s1", 19.47, s["dp_fit"], 5e-3)
    return dict(rho=rho, mu=mu, D0=D0)


# ============================================================== extras
def extras():
    pass


def report():
    print(f"{'case':28s} {'symbol':26s} {'KAT/product':>14s} {'independent':>14s} {'rel.diff':>10s} {'tol':>8s}  result")
    bad = 0
    n_find = 0
    for case, sym, e, v, rel, tol, ok, note in ROWS:
        if ok is None:
            n_find += 1
            res = "INFO"
        else:
            res = "PASS" if ok else "FAIL"
            if not ok:
                bad += 1
        print(f"{case:28s} {sym:26s} {e:14.6g} {v:14.6g} {rel:10.2e} {tol:8.1e}  {res}  {note}")
    n = len(ROWS) - n_find
    print(f"\n{n - bad}/{n} rows pass; {n_find} finding rows (informational, not counted)")
    return bad


if __name__ == "__main__":
    info = kat()
    if "--info" in sys.argv:
        print(info)
    extras()
    sys.exit(1 if report() else 0)
