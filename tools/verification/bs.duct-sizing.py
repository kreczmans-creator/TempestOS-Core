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
# Product values below were read from the Probe tool (C# calculator,
# `Probe run bs.duct-sizing <json>`, SI column converted to mm, m/s, Pa/m, Pa)
# and are hard-coded with the probe name. FINDING rows compare the product
# with the textbook definition; they do not change the exit code.

def size_other_side(Q, shape, a_mm, De_target_mm, step):
    """Solve b (mm) so the equivalent diameter equals the target, then round up."""
    fn = De_rect if shape == "rect" else De_oval
    b = brentq(lambda b: fn(a_mm / 1000, b / 1000) * 1000 - De_target_mm, 1.0, 20000.0 if shape == "rect" else a_mm, xtol=1e-12)
    return b, ceil_to(b, step)


def cmp_section(case, sec, prod, tol=2e-4):
    # tol 2e-4: the product uses mu0 = 1.716e-5 at 273.15 K; this script uses C1 = 1.458e-6. The two differ by 5e-5.
    for key, label in (("v", "v (m/s)"), ("Re", "Re"), ("f", "f"), ("dpL", "dp/L (Pa/m)"), ("dp", "dp (Pa)"), ("De", "De (mm)")):
        if key in prod:
            row(case, label, prod[key], sec[key], tol)


def extras():
    rho, mu = air(20, 101.325)
    eps = 0.09

    # P1, P2: round, equal friction, step 50 and 10 mm
    for nm, step, prod in (("P1 round 0.3 m3/s, step 50", 50, dict(v=4.244131816, Re=84546.71871, f=0.01999412297, dpL=0.7227538841, dp=14.45507768)),
                           ("P2 round 0.3 m3/s, step 10", 10, dict(v=4.541877092, f=0.01992855636, dpL=0.8534541583, dp=17.06908317)),
                           ("P17 round 0.3 m3/s, step 1", 1, dict(v=4.837474999, dpL=0.9963077172, dp=19.92615434))):
        Dt = size_equal_friction_round(0.3, 1.0, eps, rho, mu)
        D = ceil_to(Dt, step)
        row("probe " + nm, "D (mm)", {50: 300, 10: 290, 1: 281}[step], D, 1e-9)
        cmp_section("probe " + nm, section(0.3, 20, "round", 0, D, 0, 0, eps, rho, mu), prod)

    # P3: rectangular a = 500 given, b solved; P4: a blank
    Dt = size_equal_friction_De(0.8, 1.0, eps, rho, mu)
    b_raw, b = size_other_side(0.8, "rect", 500, Dt, 50)
    row("probe P3 rect a=500", "b (mm)", 300, b, 1e-9)
    cmp_section("probe P3 rect a=500", section(0.8, 20, "rect", 500, b, 0, 0, eps, rho, mu),
                dict(v=5.333333333, De=419.9793069, f=0.01771248559, dpL=0.8467824745, dp=16.93564949))
    a = ceil_to(1.5 * Dt, 50)
    b_raw, b = size_other_side(0.8, "rect", a, Dt, 50)
    row("probe P4 rect a blank", "a, b (mm) = 650, 250", 650 + 250, a + b, 1e-9, "a = 1.5 De rounded up is a product choice")
    cmp_section("probe P4 rect a blank", section(0.8, 20, "rect", a, b, 0, 0, eps, rho, mu),
                dict(v=4.923076923, De=428.7124327, dpL=0.7651499906, dp=15.30299981))

    # P5: flat oval 800 x 400 given; P5b: oval a = 600 solved; P15: given as 400 x 800 (swapped)
    cmp_section("probe P5 oval 800x400", section(0.8, 20, "oval", 800, 400, 0, 0, eps, rho, mu),
                dict(v=2.800495768, De=591.4958477, f=0.0183401102, dpL=0.1582250543, dp=3.164501087))
    cmp_section("probe P15 oval 400x800", section(0.8, 20, "oval", 800, 400, 0, 0, eps, rho, mu),
                dict(v=2.800495768, De=591.4958477, dpL=0.1582250543))
    b_raw, b = size_other_side(0.8, "oval", 600, Dt, 50)
    row("probe P5b oval a=600", "b (mm)", 300, b, 1e-9)
    cmp_section("probe P5b oval a=600", section(0.8, 20, "oval", 600, b, 0, 0, eps, rho, mu),
                dict(v=4.978659142, De=443.6218858, dpL=0.6466837928, dp=12.93367586))

    # P6, P6c, P6d: velocity method, rectangular and oval with a = 500 given (target 6 m/s)
    for nm, step, shape, bprod, vprod in (("P6 rect, step 50", 50, "rect", 200, 5.0), ("P6c rect, step 10", 10, "rect", 190, 5.263157895),
                                          ("P6d oval, step 10", 10, "oval", 200, 5.469506452)):
        # textbook velocity method: area = Q / v
        if shape == "rect":
            b_area = 0.5 / 6 / 0.5 * 1000
            b_round = ceil_to(b_area, step)
            v_ind = 0.5 / (0.5 * b_round / 1000)
        else:
            f_ = lambda b: area_oval(0.5, b / 1000) - 0.5 / 6
            b_area = brentq(f_, 1.0, 499.0)
            b_round = ceil_to(b_area, step)
            v_ind = 0.5 / area_oval(0.5, b_round / 1000)
        # product sizes b from the equivalent diameter of a round duct at 6 m/s
        De_t = math.sqrt(4 * 0.5 / (math.pi * 6)) * 1000
        _, b_prod_expected = size_other_side(0.5, shape, 500, De_t, step)
        row("probe " + nm, "b by equivalent-D rule (mm)", bprod, b_prod_expected, 1e-9, "reproduces the product rule")
        finding("probe " + nm, "b (mm) vs area rule", bprod, b_round, "P1-bs.duct-sizing-01")
        finding("probe " + nm, "v (m/s) vs 6 m/s target", vprod, v_ind, "P1-bs.duct-sizing-01")

    # P7: velocity method, D lands exactly on a step
    Q7 = math.pi / 4 * 0.35**2 * 6
    D7 = ceil_to(math.sqrt(4 * Q7 / (math.pi * 6)) * 1000, 50)
    row("probe P7 exact step", "D (mm)", 350, D7, 1e-9)

    # P8: laminar and transitional flow in a given 400 mm duct
    cmp_section("probe P8 Q=0.001 laminar", section(0.001, 20, "round", 0, 400, 0, 0, eps, rho, mu),
                dict(v=0.007957747155, Re=211.3667968, f=0.3027911714, dpL=2.885991789e-05))
    cmp_section("probe P8b Q=0.05 transition", section(0.05, 20, "round", 0, 400, 0, 0, eps, rho, mu),
                dict(v=0.3978873577, Re=10568.33984, f=0.03079048224, dpL=0.007336828753))

    # P9: T = 40 C, p = 80 kPa;  P13: eps = 0 and 3 mm;  P14: T = -20 and 200 C
    for nm, T, pk, ep, prod in (("P9 40C 80kPa", 40, 80, 0.09, dict(Re=148511.8749, f=0.01798316011, dpL=1.266860371)),
                                ("P13 eps=0", 20, 101.325, 0.0, dict(f=0.01546917094, dpL=1.474412219)),
                                ("P13b eps=3", 20, 101.325, 3.0, dict(f=0.03481472759, dpL=3.318294172)),
                                ("P14 -20C", -20, 101.325, 0.09, dict(Re=274778.963, f=0.01660297501, dpL=1.832524391)),
                                ("P14b 200C", 200, 101.325, 0.09, dict(Re=92351.74859, f=0.01937956288, dpL=1.144423887))):
        r_, m_ = air(T, pk)
        cmp_section("probe " + nm, section(1.0, 10, "round", 0, 400, 0, 0, ep, r_, m_), prod, 2e-3)

    # P10: very large duct sized by equal friction
    Dt = size_equal_friction_round(100, 0.3, eps, rho, mu)
    D = ceil_to(Dt, 50)
    row("probe P10 100 m3/s", "D (mm)", 3250, D, 1e-9)
    cmp_section("probe P10 100 m3/s", section(100, 50, "round", 0, D, 0, 0, eps, rho, mu),
                dict(v=12.05433889, f=0.01095448897, dpL=0.2948676693, dp=14.74338347), 2e-3)

    # P11: three sections, index flags
    Dt = size_equal_friction_round(1.2, 1.0, eps, rho, mu)
    s1 = section(1.2, 15, "round", 0, ceil_to(Dt, 50), 0.3, 20, eps, rho, mu)
    Dt2 = size_equal_friction_De(0.7, 1.0, eps, rho, mu)
    a2 = 400
    _, b2 = size_other_side(0.7, "rect", a2, Dt2, 50)
    s2 = section(0.7, 10, "rect", a2, b2, 1.0, 0, eps, rho, mu)
    Dt3 = size_equal_friction_round(0.5, 1.0, eps, rho, mu)
    s3 = section(0.5, 8, "round", 0, ceil_to(Dt3, 50), 2.0, 30, eps, rho, mu)
    row("probe P11 three sections", "dp_s1 (Pa)", 38.1687671, s1["dp"], 1e-5)
    row("probe P11 three sections", "dp_s2 (Pa)", 22.60118668, s2["dp"], 1e-5)
    row("probe P11 three sections", "dp_s3 (Pa)", 69.47578587, s3["dp"], 1e-5)
    row("probe P11 three sections", "dp_index (Pa) = s1 + s2", 60.76995378, s1["dp"] + s2["dp"], 1e-5)
    row("probe P11 three sections", "dp_all (Pa)", 130.2457396, s1["dp"] + s2["dp"] + s3["dp"], 1e-5)

    # P18: aspect ratio 10:1 is accepted with no warning (product value, De from the relation)
    cmp_section("probe P18 1000x100", section(0.8, 20, "rect", 1000, 100, 0, 0, eps, rho, mu),
                dict(v=8.0, De=301.0198819, dpL=4.417872092))

    # Size range of the product's equal-friction search: 20 mm to 5000 mm (probes P12 and the Q scan)
    finding("equal-friction size range", "D (mm), 400 m3/s at 0.3 Pa/m (limit 5000)", 5000.0, size_equal_friction_round(400, 0.3, eps, rho, mu), "P1-bs.duct-sizing-04")
    finding("equal-friction size range", "D (mm), 1e-4 m3/s at 5 Pa/m (limit 20)", 20.0, size_equal_friction_round(1e-4, 5.0, eps, rho, mu), "P1-bs.duct-sizing-04")

    # Colebrook and viscosity cross-check at 20 C against a reference viscosity
    finding("air viscosity at 20 C (uPa.s)", "Sutherland (product) vs about 18.2", 18.134, 18.2, "P1-bs.duct-sizing-06")


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
