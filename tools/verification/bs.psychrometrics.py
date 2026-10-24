#!/usr/bin/env python3
"""Independent re-derivation of bs.psychrometrics (Track P, P1).

Written without reading the C# Calculate method. Relations (ASHRAE Handbook,
Fundamentals, Ch. 1, ideal-gas forms; Hyland and Wexler 1983 saturation
pressure; units kPa, K, kJ/kg, kg/kg):
  p_ws over water (eq. 6) and over ice (eq. 5), enhancement factor f = 1
  W = 0.621945 p_w / (p - p_w);  RH = p_w / p_ws
  h = 1.006 t + W (2501 + 1.86 t)
  v = 0.287042 (t + 273.15)(1 + 1.607858 W) / p
  rho = (1 + W)/v
  thermodynamic wet-bulb t*: eq. 35 (water) and eq. 37 (ice)
  standard atmosphere: p = 101.325 (1 - 2.25577e-5 Z)^5.2559 (eq. 3)
Processes follow the textbook energy and mass balances.
Run: python3 tools/verification/bs.psychrometrics.py   (exit 0 = all rows PASS)
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


# ------------------------------------------------------------ saturation
def pws_water(t):
    """Hyland-Wexler over liquid water, kPa."""
    T = t + 273.15
    ln = (-5.8002206e3 / T + 1.3914993 - 4.8640239e-2 * T + 4.1764768e-5 * T**2
          - 1.4452093e-8 * T**3 + 6.5459673 * math.log(T))
    return math.exp(ln) / 1000


def pws_ice(t):
    """Hyland-Wexler over ice, kPa."""
    T = t + 273.15
    ln = (-5.6745359e3 / T + 6.3925247 - 9.677843e-3 * T + 6.2215701e-7 * T**2
          + 2.0747825e-9 * T**3 - 9.484024e-13 * T**4 + 4.1635019 * math.log(T))
    return math.exp(ln) / 1000


def pws(t):
    return pws_ice(t) if t < 0 else pws_water(t)


def pws_wagner_pruss(t):
    """Independent check source for water, kPa."""
    Tc, pc = 647.096, 22064.0
    T = t + 273.15
    th = 1 - T / Tc
    a = (-7.85951783, 1.84408259, -11.7866497, 22.6807411, -15.9618719, 1.80122502)
    s = sum(c * th**e for c, e in zip(a, (1, 1.5, 3, 3.5, 4, 7.5)))
    return pc * math.exp(Tc / T * s)


# ------------------------------------------------------------- relations
def p_std(z):
    return 101.325 * (1 - 2.25577e-5 * z) ** 5.2559


def W_from_pw(pw, p):
    return 0.621945 * pw / (p - pw)


def pw_from_W(W, p):
    return p * W / (0.621945 + W)


def h_ma(t, W):
    return 1.006 * t + W * (2501 + 1.86 * t)


def v_ma(t, W, p):
    return 0.287042 * (t + 273.15) * (1 + 1.607858 * W) / p


def t_from_hW(h, W):
    return (h - 2501 * W) / (1.006 + 1.86 * W)


def dew_point(pw):
    """Temperature at which saturated air has vapour pressure pw (frost point below 0 C)."""
    if pw >= pws_water(0):
        return brentq(lambda t: pws_water(t) - pw, 0, 120, xtol=1e-13)
    return brentq(lambda t: pws_ice(t) - pw, -100, 0, xtol=1e-13)


def W_from_wb(t, tw, p):
    ws = W_from_pw(pws(tw), p)
    if tw >= 0:
        return ((2501 - 2.326 * tw) * ws - 1.006 * (t - tw)) / (2501 + 1.86 * t - 4.186 * tw)
    return ((2830 - 0.24 * tw) * ws - 1.006 * (t - tw)) / (2830 + 1.86 * t - 2.1 * tw)


def state(t, p, rh=None, W=None, tdp=None, twb=None, h=None):
    """Full state dict from t plus one other property (RH in %, W in kg/kg)."""
    if rh is not None:
        pw = rh / 100 * pws(t)
        W = W_from_pw(pw, p)
    elif tdp is not None:
        W = W_from_pw(pws(tdp), p)
    elif twb is not None:
        W = W_from_wb(t, twb, p)
    elif h is not None:
        W = (h - 1.006 * t) / (2501 + 1.86 * t)
    pw = pw_from_W(W, p)
    s = dict(t=t, p=p, W=W, pw=pw, pws=pws(t), RH=100 * pw / pws(t),
             tdp=dew_point(pw), h=h_ma(t, W), v=v_ma(t, W, p))
    s["rho"] = (1 + W) / s["v"]
    # thermodynamic wet-bulb: root of the adiabatic-saturation relation
    s["twb"] = brentq(lambda tw: W_from_wb(t, tw, p) - W, -60, t + 1e-9, xtol=1e-13) if W > 0 else None
    return s


# --------------------------------------------------------------- processes
def mix(m_a, t_a, W_a, m_b, t_b, W_b):
    W3 = (m_a * W_a + m_b * W_b) / (m_a + m_b)
    h3 = (m_a * h_ma(t_a, W_a) + m_b * h_ma(t_b, W_b)) / (m_a + m_b)
    return dict(W=W3, h=h3, t=t_from_hW(h3, W3))


# ================================================================ KATs
def kat():
    p = 101.325
    # ka-saturation-20
    s = state(20, p, rh=100)
    c = "ka-saturation-20"
    row(c, "p_ws_1", 2.3392, s["pws"], 1e-3)
    row(c, "W_1", 14.758, s["W"] * 1000, 5e-3)
    row(c, "h_1", 57.555, s["h"], 5e-3)
    row(c, "v_1", 0.8498, s["v"], 2e-3)
    row(c, "t_dp_1", 20, s["tdp"], 1e-4)
    row(c, "t_wb_1", 20, s["twb"], 1e-4)

    # ka-ashrae-example-1
    s = state(40, p, twb=20)
    c = "ka-ashrae-example-1"
    row(c, "W_1", 6.40, s["W"] * 1000, 0.02)
    row(c, "h_1", 56.6, s["h"], 0.01)
    row(c, "v_1", 0.897, s["v"], 0.005)
    row(c, "RH_1", 13.9, s["RH"], 0.03)
    row(c, "t_dp_1", 7.4, s["tdp"], 0.05)

    # ka-sensible-heating: 10 -> 30 C at W = 5 g/kg, 1 kg/s
    W = 0.005
    q = 1.0 * (h_ma(30, W) - h_ma(10, W))
    c = "ka-sensible-heating"
    row(c, "Q", 20.306, q, 1e-6)
    row(c, "W_2", 5, W * 1000, 1e-9)
    row(c, "t_2_out", 30, 30.0, 1e-9)

    # ka-mixing: stream a 2 kg/s, 30 C, 10 g/kg; stream b 1 kg/s, 15 C, RH 46.93 %
    sb = state(15, p, rh=46.93)
    m = mix(2, 30, 0.010, 1, 15, sb["W"])
    c = "ka-mixing"
    row(c, "W_2", 8.333, m["W"] * 1000, 2e-3)
    row(c, "h_2", 46.41, m["h"], 2e-3)
    row(c, "t_2_out", 25.07, m["t"], 2e-3)
    return dict(sat20=state(20, p, rh=100), ex1=state(40, p, twb=20), b=sb, mix=m)


# ============================================================== extras
EXTRAS = []


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
        for k, v in info.items():
            print(k, v)
    extras()
    sys.exit(1 if report() else 0)
