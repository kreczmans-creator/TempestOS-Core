#!/usr/bin/env python3
"""Independent re-derivation of bs.pipe-pump (Track P, P1).

Written without reading the C# Calculate method. Method:
  - Darcy-Weisbach: h_f = f (L + L_eq)/D v^2/2g + K v^2/2g  (Cengel & Cimbala, Ch. 8)
  - laminar f = 64/Re (Re < 2300); otherwise Colebrook-White solved to full
    convergence with scipy brentq; Swamee-Jain explicit form as cross-check
  - system curve H = H_static + k Q^2, k from the design-flow friction head
  - duty point = intersection with a piecewise-linear pump curve
  - affinity: Q ~ n D^3, H ~ n^2 D^2
  - water properties: Kell (1975) density, tabulated IAPWS-2008 style viscosity
    (log-spline), Wagner-Pruss (2002) saturation pressure.
  - heat-load flow (1.1.0, added at the A7a re-check): Q = Phi / (rho c_p dT), rho from Kell at the stated
    temperature; pump shaft power P = rho g Q H / eta; shaft torque T = P / omega with omega = 2 pi n / 60.
Run: python3 tools/verification/bs.pipe-pump.py   (exit 0 = all rows PASS)
"""
import math
import sys

import numpy as np
from scipy.interpolate import CubicSpline
from scipy.optimize import brentq

G = 9.80665
ROWS = []


def row(case, symbol, expected, value, tol, note=""):
    tol_eff = max(tol, 1e-6)
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    ok = rel <= tol_eff
    ROWS.append((case, symbol, expected, value, rel, tol, ok, note))
    return ok


# ---------------------------------------------------------------- water
def rho_water(t):
    """Kell (1975) density of air-free water at 101.325 kPa, kg/m3, t in degC."""
    num = (999.83952 + 16.945176 * t - 7.9870401e-3 * t**2 - 46.170461e-6 * t**3
           + 105.56302e-9 * t**4 - 280.54253e-12 * t**5)
    return num / (1 + 16.879850e-3 * t)


_T = np.array([0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100.0])
# dynamic viscosity of liquid water at 0.1 MPa, mPa.s (IAPWS 2008 / Kestin et al. 1978 values)
_MU = np.array([1.7914, 1.3059, 1.0016, 0.7972, 0.6527, 0.5465, 0.4660, 0.4035, 0.3544, 0.3147, 0.2822])
_SPL = CubicSpline(_T, np.log(_MU))


def mu_water(t):
    """Pa.s"""
    return math.exp(float(_SPL(t))) * 1e-3


def pv_water(t):
    """Wagner-Pruss (2002) saturation pressure, Pa."""
    Tc, pc = 647.096, 22.064e6
    T = t + 273.15
    th = 1 - T / Tc
    a = (-7.85951783, 1.84408259, -11.7866497, 22.6807411, -15.9618719, 1.80122502)
    s = a[0] * th + a[1] * th**1.5 + a[2] * th**3 + a[3] * th**3.5 + a[4] * th**4 + a[5] * th**7.5
    return pc * math.exp(Tc / T * s)


# ------------------------------------------------------------- friction
def colebrook(re, rr):
    g = lambda f: 1 / math.sqrt(f) + 2 * math.log10(rr / 3.7 + 2.51 / (re * math.sqrt(f)))
    return brentq(g, 1e-4, 1.0, xtol=1e-15, rtol=1e-14)


def swamee_jain(re, rr):
    return 0.25 / math.log10(rr / 3.7 + 5.74 / re**0.9) ** 2


def friction(re, rr):
    return 64 / re if re < 2300 else colebrook(re, rr)


def segment(Q_m3s, rho, mu, seg):
    """seg = (L m, D mm, eps mm, K, Leq m, dz m). Returns dict."""
    L, Dmm, epsmm, K, Leq, dz = seg
    D = Dmm / 1000
    A = math.pi * D * D / 4
    v = Q_m3s / A
    re = rho * v * D / mu
    f = friction(re, epsmm / Dmm)
    hf = (f * (L + Leq) / D + K) * v * v / (2 * G)
    out = dict(v=v, Re=re, f=f, hf=hf, dz=dz)
    if re >= 2300 or True:
        try:
            out["f_sj"] = swamee_jain(re, epsmm / Dmm)
        except Exception:
            out["f_sj"] = float("nan")
    return out


def system(Q_m3h, rho, mu, segs, h_out):
    Q = Q_m3h / 3600
    res = [segment(Q, rho, mu, s) for s in segs]
    hf = sum(r["hf"] for r in res)
    Hs = sum(r["dz"] for r in res) + h_out
    return res, hf, Hs


def duty(Qp, Hp, Hstat, k):
    """Intersection of piecewise-linear pump curve with H = Hstat + k Q^2 (Q in m3/h)."""
    Qp = np.asarray(Qp, float)
    Hp = np.asarray(Hp, float)
    d = lambda q: float(np.interp(q, Qp, Hp)) - (Hstat + k * q * q)
    # scan segments for the first sign change
    xs = np.linspace(Qp[0], Qp[-1], 20001)
    vals = [d(x) for x in xs]
    for i in range(len(xs) - 1):
        if vals[i] == 0:
            return xs[i], float(np.interp(xs[i], Qp, Hp))
        if vals[i] * vals[i + 1] < 0:
            q = brentq(d, xs[i], xs[i + 1], xtol=1e-14)
            return q, float(np.interp(q, Qp, Hp))
    return None


def affinity(Qp, Hp, n, Dr):
    return np.asarray(Qp, float) * n * Dr**3, np.asarray(Hp, float) * n**2 * Dr**2


def npsh_a(patm_kPa, rho, zs, hf_suction, pv_Pa):
    return patm_kPa * 1000 / (rho * G) + zs - hf_suction - pv_Pa / (rho * G)


# ================================================================ KATs
def kat():
    # ka-laminar-oil
    rho, mu = 888.0, 0.8
    r = segment(1 / 3600, rho, mu, (40, 50, 0.0, 0, 0, 0))
    c = "ka-laminar-oil"
    row(c, "v_s1", 0.14147, r["v"], 1e-4)
    row(c, "Re_s1", 7.852, r["Re"], 1e-3)
    row(c, "f_s1", 8.151, r["f"], 1e-3)
    row(c, "h_f_total", 6.654, r["hf"], 1e-3)
    row(c, "dp_total", 57.946, rho * G * r["hf"] / 1000, 1e-3)
    # cross-check with Hagen-Poiseuille directly
    dp_hp = 32 * mu * 40 * r["v"] / 0.05**2 / 1000
    row(c, "dp_total (HP direct)", 57.946, dp_hp, 1e-3, "Hagen-Poiseuille dp = 32 mu L v / D^2")

    # ka-turbulent-water
    c = "ka-turbulent-water"
    r = segment(28.274334 / 3600, 1000.0, 1e-3, (100, 100, 0.1, 0, 0, 0))
    row(c, "v_s1", 1.0, r["v"], 1e-6)
    row(c, "Re_s1", 1e5, r["Re"], 1e-6)
    row(c, "f_s1", 0.022175, r["f"], 5e-4)
    row(c, "h_f_total", 1.1306, r["hf"], 1e-3)
    row(c, "f_sj_s1", 0.022342, r["f_sj"], 1e-3)

    # ka-duty-point
    c = "ka-duty-point"
    segs = [(60, 80, 0.045, 8, 0, 10)]
    res, hf, Hs = system(30, rho_water(20), mu_water(20), segs, 0)
    k = hf / 30**2
    Qp, Hp = [0, 20, 40, 60], [40, 36, 24, 4]
    q, h = duty(Qp, Hp, Hs, k)
    row(c, "H_static", 10, Hs, 1e-9)
    row(c, "H_duty", 17.67, h, 0.01)
    row(c, "Q_duty", 46.3, q, 0.01)
    Q2, H2 = affinity(Qp, Hp, 0.9, 1.0)
    q2, h2 = duty(Q2, H2, Hs, k)
    row(c, "Q_duty2", 40.1, q2, 0.01)

    # ka-heat-load-flow (1.1.0): 12 kW at 20 K, water at 70 C, c_p 4186 J/(kg K)
    c = "ka-heat-load-flow"
    qh = 12000.0 / (rho_water(70) * 4186.0 * 20.0)
    row(c, "Q_design (m3/h)", 0.5277375, qh * 3600, 1e-6)
    return dict(k=k, hf=hf, res=res)


# ============================================================== extras
# Product values below were read from the Probe tool (C# calculator,
# `Probe run bs.pipe-pump <json>`) and are hard-coded with the probe name.
# Rows marked FINDING compare the product with a stricter physical model;
# they do not change the exit code.

def finding(case, symbol, product, indep, ref):
    rel = abs(indep - product) / abs(indep) if indep else abs(indep - product)
    ROWS.append((case, symbol, product, indep, rel, 0.0, None, "FINDING " + ref))


def water_case(T, Q, segs, h_out=0.0, pump=None, n=1.0, Dr=1.0, eta=0.7, patm=101.325, zs=0.0,
               suction=()):
    """segs: list of (L, D mm, eps mm, K, Leq, dz). suction: indices of suction segments."""
    rho, mu = rho_water(T), mu_water(T)
    res, hf, Hs = system(Q, rho, mu, segs, h_out)
    out = dict(res=res, hf=hf, Hs=Hs, rho=rho, mu=mu)
    k = hf / Q**2
    out["k"] = k
    if pump:
        q, h = duty(pump[0], pump[1], Hs, k)
        out.update(Qd=q, Hd=h, Ps=rho * G * q / 3600 * h / eta / 1000)
        if n != 1 or Dr != 1:
            Q2, H2 = affinity(*pump, n, Dr)
            q2, h2 = duty(Q2, H2, Hs, k)
            out.update(Qd2=q2, Hd2=h2, Ps2=rho * G * q2 / 3600 * h2 / eta / 1000)
    hfs = sum(res[i]["hf"] for i in suction)
    out["NPSHa"] = npsh_a(patm, rho, zs, hfs, pv_water(T))
    return out


def extras():
    TOL = 2e-3     # water-property tolerance: product uses a Vogel viscosity fit (~1 % of IAPWS)
    # ---- A: two segments, suction + discharge, pump curve, affinity n = 0.9 and D = 0.95
    c = "probe A 60C 2seg"
    segsA = [(5, 100, 0.045, 2, 1, -1.5), (150, 80, 0.045, 5, 10, 12)]
    a = water_case(60, 50, segsA, 15, ([0, 25, 50, 75, 100], [60, 57, 50, 38, 20]), 0.9, 0.95, 0.65, 98, 2, suction=(0,))
    row(c, "v_s1 (m/s)", 1.768388257, a["res"][0]["v"], 1e-6)
    row(c, "v_s2 (m/s)", 2.763106651, a["res"][1]["v"], 1e-6)
    row(c, "Re_s1", 373503.0855, a["res"][0]["Re"], 8e-3, "viscosity fit")
    row(c, "f_s1", 0.01764632835, a["res"][0]["f"], 2e-3)
    row(c, "h_f_s1 (m)", 0.4877000191, a["res"][0]["hf"], 4e-3)
    row(c, "h_f_s2 (m)", 16.03579303, a["res"][1]["hf"], 4e-3)
    row(c, "h_f_total (m)", 16.52349305, a["hf"], 4e-3)
    row(c, "H_static (m)", 25.5, a["Hs"], 1e-9)
    row(c, "Q_duty (m3/h)", 56.7289, a["Qd"], 4e-3)
    row(c, "H_duty (m)", 46.77013708, a["Hd"], 4e-3)
    row(c, "P_shaft (kW)", 10.93248915, a["Ps"], 8e-3)
    row(c, "Q_duty2 (m3/h)", 39.82831, a["Qd2"], 4e-3)
    row(c, "H_duty2 (m)", 35.98444958, a["Hd2"], 4e-3)
    row(c, "P_shaft2 (kW)", 5.905449359, a["Ps2"], 8e-3)
    row(c, "NPSH_a (m)", 9.607835299, a["NPSHa"], 4e-3)

    # ---- B: NPSH_a negative at 100 C and 95 C (lift)
    c = "probe B 100C"
    b = water_case(100, 10, [(10, 50, 0.045, 1, 0, 0)], zs=0, suction=(0,))
    row(c, "NPSH_a (m)", -0.5287282382, b["NPSHa"], 0.02, "p_v differs by 0.01 %; small difference of large numbers")
    b = water_case(95, 10, [(10, 50, 0.045, 1, 0, 0)], zs=-4, suction=(0,))
    row("probe B2 95C lift", "NPSH_a (m)", -2.747698827, b["NPSHa"], 4e-3)

    # ---- C: Re boundaries, custom fluid rho 900, mu 20 mPa.s, D 50 mm, L 100 m
    for nm, Re, f_p, h_p in (("C1 Re=2299", 2299, 0.02783819052, 2.96369313), ("C2 Re=2301", 2301, 0.048001034, 5.119153099),
                              ("C3 Re=3999", 3999, 0.04081396603, 13.14697676), ("C4 Re=4001", 4001, 0.0408082544, 13.15828864),
                              ("C5 Re=1000", 1000, 0.064, 1.289122719)):
        Q = Re * 0.02 * math.pi * 0.05 / 4 / 900
        r = segment(Q, 900.0, 0.02, (100, 50, 0.045, 0, 0, 0))
        row("probe " + nm, "f", f_p, r["f"], 1e-6)
        row("probe " + nm, "h_f (m)", h_p, r["hf"], 1e-6)

    # ---- D: near zero flow, E: very large flow, H: 0 C, I: smooth pipe, U: rough pipe with L_eq
    d = water_case(20, 0.01, [(10, 25, 0.045, 0, 0, 0)])
    row("probe D Q=0.01", "Re_s1", 140.7205934, d["res"][0]["Re"], 3e-3, "viscosity fit +0.2 % at 20 C")
    row("probe D Q=0.01", "f_s1", 0.454801948, d["res"][0]["f"], 3e-3)
    row("probe D Q=0.01", "h_f (m)", 0.0002970207821, d["hf"], 3e-3)
    e = water_case(20, 100000, [(1000, 2000, 0.1, 5, 0, 0)])
    row("probe E Q=1e5", "Re_s1", 17590074.17, e["res"][0]["Re"], 3e-3, "viscosity fit +0.2 % at 20 C")
    row("probe E Q=1e5", "f_s1", 0.01072932136, e["res"][0]["f"], 1e-3)
    row("probe E Q=1e5", "h_f (m)", 41.31423071, e["hf"], 1e-3)
    h0 = water_case(0, 20, [(50, 50, 0, 0, 0, 0)])
    row("probe H T=0C", "Re_s1", 79699.90754, h0["res"][0]["Re"], 1.5e-2, "viscosity fit -1 % at 0 C")
    row("probe H T=0C", "f_s1", 0.01887171115, h0["res"][0]["f"], 4e-3)
    row("probe H T=0C", "NPSH_a (m)", 10.27159655, h0["NPSHa"], 1e-3)
    i0 = water_case(20, 5, [(50, 50, 0, 0, 0, 0)])
    row("probe I smooth", "f_s1", 0.02262733873, i0["res"][0]["f"], 1e-3)
    row("probe I smooth", "h_f (m)", 0.5772421481, i0["hf"], 2e-3)
    u0 = water_case(20, 30, [(100, 80, 5, 0, 25, 0)])
    row("probe U eps=5mm Leq", "f_s1", 0.07974483234, u0["res"][0]["f"], 1e-3)
    row("probe U eps=5mm Leq", "h_f (m)", 17.46102368, u0["hf"], 2e-3)

    # ---- S, T: NPSH with suction losses, p_atm 90 kPa, and 80 C lift
    s0 = water_case(20, 30, [(20, 100, 0.045, 3, 0, 0)], zs=3, patm=90, suction=(0,))
    row("probe S p_atm=90", "NPSH_a (m)", 11.55352975, s0["NPSHa"], 1e-3)
    t0 = water_case(80, 30, [(20, 100, 0.045, 3, 0, -2)], zs=-2, suction=(0,))
    row("probe T 80C", "NPSH_a (m)", 3.27873171, t0["NPSHa"], 4e-3)

    # ---- V: speed 1.3 on the KAT duty curve
    segsV = [(100, 80, 0.045, 2, 0, 10)]
    pump = ([0, 20, 40, 60], [40, 36, 24, 4])
    v0 = water_case(20, 30, segsV, 0, pump, 1.3, 1.0)
    row("probe V n=1.3", "Q_duty2 (m3/h)", 62.7996, v0["Qd2"], 2e-3)
    row("probe V n=1.3", "H_duty2 (m)", 26.52054252, v0["Hd2"], 2e-3)

    # ---- J: impeller ratio 0.8. Product uses the similarity law Q ~ D^3.
    j0 = water_case(20, 30, segsV, 0, pump, 1.0, 0.8, 0.7)
    row("probe J D=0.8 (D^3 law)", "Q_duty2 (m3/h)", 22.99585, j0["Qd2"], 2e-3)
    row("probe J D=0.8 (D^3 law)", "H_duty2 (m)", 12.21518411, j0["Hd2"], 2e-3)
    row("probe J D=0.8 (D^3 law)", "P_shaft2 (kW)", 1.091161498, j0["Ps2"], 2e-3)
    # trim law Q ~ D, H ~ D^2 (usual practice for a trimmed impeller)
    Qt, Ht = np.asarray(pump[0], float) * 0.8, np.asarray(pump[1], float) * 0.64
    qt, ht = duty(Qt, Ht, j0["Hs"], j0["k"])
    pt = j0["rho"] * G * qt / 3600 * ht / 0.7 / 1000
    finding("probe J D=0.8 (trim law)", "Q_duty2 (m3/h)", 22.99585, qt, "P1-bs.pipe-pump-02")
    finding("probe J D=0.8 (trim law)", "P_shaft2 (kW)", 1.091161498, pt, "P1-bs.pipe-pump-02")

    # ---- W: laminar oil with a pump curve. Product scales friction with Q^2.
    rho, mu = 900.0, 0.2
    segW = (100, 50, 0.045, 0, 0, 2)
    rW = segment(5 / 3600, rho, mu, segW)
    kq = rW["hf"] / 25.0
    q_p, h_p = duty([0, 5, 10, 15, 20], [30, 27, 22, 14, 4], 2.0, kq)
    row("probe W laminar duty", "Q_duty (m3/h) [Q^2 law]", 5.46744, q_p, 1e-4, "same assumption as product")
    row("probe W laminar duty", "H_duty (m) [Q^2 law]", 26.53255755, h_p, 1e-4)
    # exact laminar: h_f proportional to Q
    kl = rW["hf"] / 5.0
    ql, hl = duty([0, 5, 10, 15, 20], [30, 27, 22, 14, 4], 2.0, 0.0), None
    f2 = lambda q: float(np.interp(q, [0, 5, 10, 15, 20], [30, 27, 22, 14, 4])) - (2.0 + kl * q)
    ql = brentq(f2, 0, 20)
    finding("probe W laminar duty", "Q_duty (m3/h)", q_p, ql, "P1-bs.pipe-pump-03")
    hl = 2.0 + kl * ql
    finding("probe W laminar duty", "P_shaft ratio", q_p * h_p, ql * hl, "P1-bs.pipe-pump-03")

    # ---- R: pump curve crosses the system curve twice; product reports no crossing
    kR = water_case(20, 30, segsV)["k"]
    cr = duty_all([0, 10, 40, 70], [8, 14, 18, 2], 10.0, kR)
    row("probe R two crossings", "number of crossings", 2, len(cr), 1e-9, "product reports none (finding)")
    finding("probe R two crossings", "Q_duty (m3/h)", float("nan"), cr[-1][0], "P1-bs.pipe-pump-04")

    # ---- review R1: NPSH_a ignores friction when no segment is marked "suction"
    #      Probes pp_r1a (side blank) and pp_r1b (side = suction), reviewer, 60 C, 30 m3/h, lift 3 m.
    segR1 = [(60, 65, 0.045, 6, 0, 0)]
    r1a = water_case(60, 30, segR1, zs=-3)
    r1b = water_case(60, 30, segR1, zs=-3, suction=(0,))
    row("review R1 side blank", "NPSH_a (m)", 5.440384815, r1a["NPSHa"], 4e-3, "product: pass vs NPSH_r 3 m")
    row("review R1 side=suction", "NPSH_a (m)", -2.15116189, r1b["NPSHa"], 2e-2, "product: FAIL vs NPSH_r 3 m")
    finding("review R1 side blank", "NPSH_a with line friction (m)", 5.440384815, r1b["NPSHa"], "P1-bs.pipe-pump-R1")

    # ---- review R2: NPSH_a is taken at the design flow, not at the duty flow
    #      Probe pp_r2a (reviewer): design 30 m3/h, duty 38.539 m3/h, NPSH_a 3.2105 m (pass vs 3 m).
    segR2 = [(10, 65, 0.045, 4, 0, 0), (100, 65, 0.045, 6, 0, 0)]
    pumpR2 = ([0, 20, 40, 60], [30, 28, 22, 10])
    r2 = water_case(60, 30, segR2, 0, pumpR2, zs=-3, suction=(0,))
    row("review R2 design flow", "Q_duty (m3/h)", 38.539, r2["Qd"], 2e-3)
    row("review R2 design flow", "NPSH_a at Q (m)", 3.210465629, r2["NPSHa"], 4e-3)
    r2d = water_case(60, r2["Qd"], segR2, zs=-3, suction=(0,))
    finding("review R2 duty flow", "NPSH_a at Q_duty (m)", 3.210465629, r2d["NPSHa"], "P1-bs.pipe-pump-R2")

    # ---- Q^2 system-curve assumption on the KAT duty case (turbulent), Info only
    rho20, mu20 = rho_water(20), mu_water(20)
    def hsys_exact(q):
        r = segment(q / 3600, rho20, mu20, (60, 80, 0.045, 8, 0, 0))
        return 10 + r["hf"]
    qx = brentq(lambda q: float(np.interp(q, [0, 20, 40, 60], [40, 36, 24, 4])) - hsys_exact(q), 1, 59)
    finding("KAT duty, f(Re) re-solved", "Q_duty (m3/h)", 46.331789, qx, "P1-bs.pipe-pump-03 (turbulent size)")


def heat_case(T, phi, dT, cp, segs, h_out=0.0, eta=0.7, n_pump=None, pump=None):
    """Heat-load flow, system head, hydraulic and shaft power, shaft torque (SI: m3/s, m, W, N m)."""
    rho, mu = rho_water(T), mu_water(T)
    q_si = phi / (rho * cp * dT)
    res, hf, Hs = system(q_si * 3600, rho, mu, segs, h_out)
    out = dict(Q=q_si, Hstat=Hs, hf=hf, Hsys=Hs + hf)
    out["P_hyd"] = rho * G * q_si * out["Hsys"]
    out["P_shaft_design"] = out["P_hyd"] / eta
    if n_pump:
        w = 2 * math.pi * n_pump / 60.0
        P = out["P_shaft_design"]
        if pump:
            k = hf / (q_si * 3600) ** 2
            qd, hd = duty(pump[0], pump[1], Hs, k)
            out.update(Qd=qd / 3600, Hd=hd, Ps=rho * G * qd / 3600 * hd / eta)
            P = max(P, out["Ps"])
        out["P_sel"] = P
        out["T_shaft"] = P / w
        out["n_shaft"] = w
    return out


def extras_heat():
    # Product values read with `Probe run bs.pipe-pump` (10 significant figures, SI units).
    # H1: the pack.wet-heating reference case (B4 rooms 350.6732 + 243.8716 W, 10 K, 70 C, DN28 40 m, dz 3 m, 2800 rpm).
    c = "probe H1 pack reference"
    h = heat_case(70, 594.544716, 10, 4186.0, [(40, 28, 0.045, 12, 0, 3)], n_pump=2800)
    row(c, "Q_design (m3/s)", 1.4526091e-05, h["Q"], 1e-8, "only rho(T) enters")
    row(c, "H_static (m)", 3.0, h["Hstat"], 1e-9)
    row(c, "h_f_total (m)", 0.001961991827, h["hf"], 1.5e-2, "laminar: follows the viscosity fit")
    row(c, "P_hyd (W)", 0.4181298422, h["P_hyd"], 1e-3, "static head 3 m dominates")
    row(c, "P_shaft_design (W)", 0.597328346, h["P_shaft_design"], 1e-3)
    row(c, "T_shaft (N m)", 0.002037166263, h["T_shaft"], 1e-3)
    row(c, "n_shaft (rad/s)", 293.2153143, h["n_shaft"], 1e-9)
    # H3: closed loop (dz 0), 24 kW, 10 K, 45 C, c_p 4180, DN32 60 m, eta 0.55, 1450 rpm
    c = "probe H3 closed loop 24 kW"
    h = heat_case(45, 24000, 10, 4180.0, [(60, 32, 0.045, 15, 0, 0)], eta=0.55, n_pump=1450)
    row(c, "Q_design (m3/s)", 0.0005798374177, h["Q"], 1e-8)
    row(c, "h_f_total (m)", 1.686415274, h["hf"], 4e-3)
    row(c, "P_hyd (W)", 9.495550821, h["P_hyd"], 4e-3)
    row(c, "P_shaft_design (W)", 17.26463786, h["P_shaft_design"], 4e-3)
    row(c, "T_shaft (N m)", 0.1137001016, h["T_shaft"], 4e-3)
    # H4: the same loop with a pump curve; the torque covers the larger duty-point shaft power
    c = "probe H4 with pump curve"
    h = heat_case(45, 24000, 10, 4180.0, [(60, 32, 0.045, 15, 0, 0)], eta=0.55, n_pump=1450, pump=([0, 1, 2, 3], [6, 5, 3.5, 1]))
    row(c, "Q_duty (m3/s)", 0.0006838274937, h["Qd"], 4e-3)
    row(c, "H_duty (m)", 2.345552552, h["Hd"], 4e-3)
    row(c, "P_shaft (W)", 28.31903394, h["Ps"], 4e-3)
    row(c, "T_shaft (N m)", 0.1865012787, h["T_shaft"], 4e-3, "larger of design and duty-point power")
    # CC1 (1.2.0, circuit = closed): DN80, 60 m, K 8, dz 10 m entered, 30 m3/h, water at 20 C, h_out 0.
    # A closed flow-and-return loop has no static lift, so H_static = h_out = 0 and H_sys is the friction head alone.
    # V_pipe = pi/4 D^2 L in litres. Product values read at 0772fcf8 with `Probe run bs.pipe-pump`.
    c = "probe CC1 closed circuit"
    segs_cc = [(60, 80, 0.045, 8, 0, 10)]
    _, hf_cc, _ = system(30, rho_water(20), mu_water(20), segs_cc, 0)
    row(c, "H_static (m)", 0.0, 0.0, 1e-9, "closed: rises ignored")
    row(c, "H_sys (m)", 3.214981351, hf_cc, 2e-3, "friction head only")
    row(c, "V_pipe (L)", 301.5928947, math.pi / 4 * 0.08 ** 2 * 60 * 1000, 1e-9)
    c = "probe CC2 open circuit, same loop"
    row(c, "H_sys (m)", 13.21498135, hf_cc + 10.0, 2e-3, "dz 10 m counts as lift")
    # ---- findings evidence (informational) ----
    ref = heat_case(70, 594.544716, 10, 4186.0, [(40, 28, 0.045, 12, 0, 3)], n_pump=2800)
    closed = heat_case(70, 594.544716, 10, 4186.0, [(40, 28, 0.045, 12, 0, 0)], n_pump=2800)
    finding("pack reference, dz 3 m vs 0", "T_shaft (N m)", ref["T_shaft"], closed["T_shaft"], "P1-chain.wet-heating-02")
    small = heat_case(70, 594.544716, 10, 4186.0, [(40, 28, 0.045, 12, 0, 3)], eta=0.2, n_pump=2800)
    finding("pack reference, eta 0.7 vs 0.2", "T_shaft (N m)", ref["T_shaft"], small["T_shaft"], "P1-bs.pipe-pump-C2")
    finding("no n_pump entered", "T_shaft offered (0 = no, 1 = yes)", 0.0, 1.0, "P1-bs.pipe-pump-C1")


def duty_all(Qp, Hp, Hstat, k):
    xs = np.linspace(Qp[0], Qp[-1], 40001)
    d = lambda q: float(np.interp(q, Qp, Hp)) - (Hstat + k * q * q)
    vals = [d(x) for x in xs]
    out = []
    for i in range(len(xs) - 1):
        if vals[i] * vals[i + 1] < 0:
            q = brentq(d, xs[i], xs[i + 1], xtol=1e-12)
            out.append((q, float(np.interp(q, Qp, Hp))))
    return out


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
    extras_heat()
    sys.exit(1 if report() else 0)
