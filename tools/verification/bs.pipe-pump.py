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
    return dict(k=k, hf=hf, res=res)


# ============================================================== extras
# Product values below were read from the Probe tool (C# calculator) and are
# hard-coded here with a note. Filled in after Step 2 probing.
EXTRAS = []


def extras():
    for fn in EXTRAS:
        fn()


def report():
    print(f"{'case':24s} {'symbol':22s} {'KAT/product':>14s} {'independent':>14s} {'rel.diff':>10s} {'tol':>8s}  result")
    bad = 0
    for case, sym, e, v, rel, tol, ok, note in ROWS:
        if not ok:
            bad += 1
        print(f"{case:24s} {sym:22s} {e:14.6g} {v:14.6g} {rel:10.2e} {tol:8.1e}  {'PASS' if ok else 'FAIL'}  {note}")
    print(f"\n{len(ROWS) - bad}/{len(ROWS)} rows pass")
    return bad


if __name__ == "__main__":
    info = kat()
    if "--info" in sys.argv:
        print(info)
    extras()
    sys.exit(1 if report() else 0)
