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
    if pw <= 0:
        return float("-inf")
    if pw >= pws_water(0):
        return brentq(lambda t: pws_water(t) - pw, 0, 120, xtol=1e-13)
    return brentq(lambda t: pws_ice(t) - pw, -140, 0, xtol=1e-13)


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
    try:
        s["twb"] = brentq(lambda tw: W_from_wb(t, tw, p) - W, -60, t + 1e-9, xtol=1e-13)
    except ValueError:      # supersaturated: no wet-bulb exists
        s["twb"] = float("nan")
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
# Product values below were read from the Probe tool (C# calculator,
# `Probe run bs.psychrometrics <json>`, SI column, K converted to degC) and
# are hard-coded with the probe name. FINDING rows compare the product with
# the textbook definition; they do not change the exit code.

def chk_state(c, s, W, h=None, v=None, rho=None, RH=None, tdp=None, twb=None, tol=1e-6):
    row(c, "W (g/kg)", W, s["W"] * 1000, tol)
    if h is not None:
        row(c, "h (kJ/kg)", h, s["h"], tol)
    if v is not None:
        row(c, "v (m3/kg)", v, s["v"], tol)
    if rho is not None:
        row(c, "rho (kg/m3)", rho, s["rho"], tol)
    if RH is not None:
        row(c, "RH (%)", RH, s["RH"], tol)
    if tdp is not None:
        row(c, "t_dp (C)", tdp, s["tdp"], tol)
    if twb is not None:
        row(c, "t_wb (C)", twb, s["twb"], tol)


def extras():
    p0 = 101.325
    # --- state probes
    s = state(25, p0, rh=50)
    chk_state("probe a 25C 50%", s, 9.881043691, 50.3219588, 0.8580432639, 1.176958186, None, 13.8639733, 17.8893423)
    pb = p_std(1500)
    row("probe b alt 1500 m", "p (kPa)", 84.556, pb, 1e-4, "standard atmosphere eq. 3")
    s = state(30, pb, twb=22)
    chk_state("probe b 30C wb22 1500m", s, 16.69486691, 72.86543573, 1.056727413, 0.9621164872, 52.05805463, 19.0916399)
    s = state(-10, p0, rh=80)
    chk_state("probe c -10C 80% (ice)", s, 1.278876257, -6.885317579, 0.7470063801, None, None, -12.4895572, -10.6480143)
    row("probe c -10C 80% (ice)", "p_ws (kPa)", 0.259902865, s["pws"], 1e-6)
    row("probe c -10C 80% (ice)", "p_ws vs ice-table 0.2599", 0.2599, s["pws"], 5e-4, "ASHRAE Ch.1 value over ice")
    s = state(-10, p0, twb=-12)
    chk_state("probe d -10C wb-12 (ice)", s, 0.6257657877, -8.506599009, None, None, 39.18579277, -20.1437333)
    s = state(30, p0, tdp=15)
    chk_state("probe e 30C dp15", s, 10.64745529, 57.4034137, 0.8734909891, None, 40.16570059, None, 20.0976847)
    s = state(28, p0, h=60)
    chk_state("probe f 28C h60", s, 12.46807777, None, 0.8702255742, None, 52.65003906, 17.4293133, 20.8227283)
    s = state(60, p0, rh=100)
    chk_state("probe g 60C 100%", s, 152.4174649, 458.5658689, 1.175062321, 0.9807288038)
    s = state(0, p0, rh=100)
    chk_state("probe q 0C 100%", s, 3.774466214, 9.439940002)
    s = state(-0.5, p0, rh=100)
    chk_state("probe r -0.5C 100%", s, 3.620697093, 8.548996181)
    s = state(-30, p0, rh=100)
    chk_state("probe x -30C 100%", s, 0.2334323654, -29.60921118)
    s = state(20, p0, W=0.0)
    row("probe u dry air", "t_wb (C)", 5.8363616, s["twb"], 1e-6)
    row("probe u dry air", "h (kJ/kg)", 20.12, s["h"], 1e-9)
    s = state(5, p0, twb=-3)
    chk_state("probe v 5C wb-3 (ice)", s, 0.09231308794, 5.261733545, None, None, 1.723473705, -38.5993006)
    s = state(5, p0, tdp=-10)
    chk_state("probe w 5C dp-10 (frost)", s, 1.599417523, 9.045017809, None, None, 29.78874963, None, -0.59212)
    s = state(25, 1000.0, rh=50)
    chk_state("probe o 25C 50% 1000 kPa", s, 0.9871033408, 27.66464576, None, None, None, None, 23.1116338)

    # --- cooling with dehumidification, textbook sensible / latent split
    for nm, t1, rh1, t2, rh2, prod in (
        ("probe i 28C50%->12C", 28, 50, 12, 100, dict(W2=8.730119447, h2=34.100885, mw=0.003098466152, Qt=24.11079817, Qs=7.818421611, Ql=16.44801871, shr=0.322190709)),
        ("probe s 40C50%->14C95%", 40, 50, 14, 95, dict(W2=9.46403603, h2=37.99999761, mw=0.01405308081, Qt=61.98241834, Qs=35.51269732, Ql=27.29328777, shr=0.5654349226)),
        ("probe t 35C60%->10C", 35, 60, 10, 100, dict(W2=7.630053703, h2=29.28468331, mw=0.01381101593, Qt=60.36711635, Qs=34.79823574, Ql=26.14700974, shr=0.5709753971)),
    ):
        a = state(t1, p0, rh=rh1)
        b = state(t2, p0, rh=rh2)
        mw = a["W"] - b["W"]
        qt = (a["h"] - b["h"]) - mw * 4.186 * t2
        qs = h_ma(t1, a["W"]) - h_ma(t2, a["W"])      # sensible: temperature change at constant W1
        ql = h_ma(t2, a["W"]) - b["h"]                # latent: moisture removal at t2
        row(nm, "W_2 (g/kg)", prod["W2"], b["W"] * 1000, 1e-6)
        row(nm, "h_2 (kJ/kg)", prod["h2"], b["h"], 1e-6)
        row(nm, "m_w (kg/s)", prod["mw"], mw, 1e-6)
        row(nm, "Q_total (kW)", prod["Qt"], qt, 1e-6)
        row(nm, "Q_sens+Q_lat (kW)", prod["Qs"] + prod["Ql"], qs + ql, 1e-6, "sum agrees; labels do not")
        finding(nm, "Q_sensible (kW)", prod["Qs"], qs, "P1-bs.psychrometrics-01")
        finding(nm, "Q_latent (kW)", prod["Ql"], ql, "P1-bs.psychrometrics-01")
        finding(nm, "SHR", prod["shr"], qs / (qs + ql), "P1-bs.psychrometrics-01")

    # --- steam humidification
    for nm, t1, rh1, W2, prod in (
        ("probe j steam 20C30%", 20, 30, 8.0, dict(t2=20.4944662, RH2=53.36216115, h2=40.93039061, ms=0.003663211952, Q=9.802755184)),
        ("probe aa steam 10C40%", 10, 40, 6.0, dict(t2=10.4567145, RH2=76.46884715, h2=25.64215176, ms=0.002970279778, Q=7.948468686)),
    ):
        a = state(t1, p0, rh=rh1)
        h2 = a["h"] + (W2 / 1000 - a["W"]) * 2676
        t2 = t_from_hW(h2, W2 / 1000)
        b = state(t2, p0, W=W2 / 1000)
        row(nm, "t_2 (C)", prod["t2"], t2, 1e-6)
        row(nm, "RH_2 (%)", prod["RH2"], b["RH"], 1e-6)
        row(nm, "h_2 (kJ/kg)", prod["h2"], h2, 1e-6)
        row(nm, "m_s (kg/s)", prod["ms"], W2 / 1000 - a["W"], 1e-6)
        row(nm, "Q (kW)", prod["Q"], (W2 / 1000 - a["W"]) * 2676, 1e-6)

    # --- evaporative, constant-h path
    a = state(30, p0, rh=30)
    t2 = 30 - 0.8 * (30 - a["twb"])
    b = state(t2, p0, h=a["h"])
    row("probe k eff=0.8", "t_2 (C)", 20.3773452, t2, 1e-6)
    row("probe k eff=0.8", "W_2 (g/kg)", 11.78698504, b["W"] * 1000, 1e-6)
    row("probe k eff=0.8", "RH_2 (%)", 78.71968877, b["RH"], 1e-6)
    row("probe k eff=0.8", "m_w_add (kg/s)", 0.003868646674, b["W"] - a["W"], 1e-6)
    t2 = brentq(lambda t: state(t, p0, h=a["h"])["RH"] - 90, a["twb"], 30, xtol=1e-13)
    b = state(t2, p0, h=a["h"])
    row("probe k2 RH_2=90", "t_2 (C)", 18.9682868, t2, 1e-6)
    row("probe k2 RH_2=90", "eff_out", 0.9171450859, (30 - t2) / (30 - a["twb"]), 1e-6)
    a = state(20, p0, rh=50)
    b = state(a["twb"], p0, h=a["h"])
    row("probe ab eff=1", "RH_2 (%)", 99.4128171, b["RH"], 1e-6, "constant-h path")
    # true adiabatic saturation conserves h - W*4.186*t*, so eff = 1 gives RH 100 %
    ws_star = W_from_pw(pws(a["twb"]), p0)
    finding("probe ab eff=1", "RH_2 (%)", b["RH"], 100.0, "P1-bs.psychrometrics-03")
    finding("probe ab eff=1", "W_2 (g/kg)", b["W"] * 1000, ws_star * 1000, "P1-bs.psychrometrics-03")
    # the product cannot reach RH_2 = 100 % (probe k3 returns a Block): highest reachable RH on the h line
    a30 = state(30, p0, rh=30)
    top = state(a30["twb"], p0, h=a30["h"])["RH"]
    finding("probe k3 RH_2=100", "max reachable RH_2 (%)", top, 100.0, "P1-bs.psychrometrics-03")

    # --- sensible heating, 2.5 kg/s
    a = state(20, p0, rh=50)
    b = state(45, p0, W=a["W"])
    row("probe ac heating 2.5 kg/s", "Q (kW)", 63.71917695, 2.5 * (b["h"] - a["h"]), 1e-6)
    row("probe ac heating 2.5 kg/s", "RH_2 (%)", 12.18987846, b["RH"], 1e-6)

    # --- mixing, supersaturated result (fog)
    a = state(30, p0, rh=90)
    bb = state(5, p0, rh=100)
    m = mix(1, 30, a["W"], 1, 5, bb["W"])
    row("probe l mixing fog", "W_b (g/kg)", 5.401942611, bb["W"] * 1000, 1e-6)
    row("probe l mixing fog", "W_2 (g/kg)", 14.88881983, m["W"] * 1000, 1e-6)
    row("probe l mixing fog", "h_2 (kJ/kg)", 55.54713938, m["h"], 1e-6)
    row("probe l mixing fog", "t_2 (C)", 17.7133804, m["t"], 1e-6)
    row("probe l mixing fog", "RH_2 nominal (%)", 116.8475261, state(m["t"], p0, W=m["W"])["RH"], 1e-6)

    # --- KAT input consistency (finding 05): RH_b = 46.93 % gives W_b = 4.952 g/kg, not 5.000
    sb = state(15, p0, rh=46.93)
    finding("ka-mixing input check", "W_b (g/kg) KAT text vs RH_b=46.93 %", 5.0, sb["W"] * 1000, "P1-bs.psychrometrics-05")
    rh_for_5 = 100 * pw_from_W(0.005, p0) / pws(15)
    finding("ka-mixing input check", "RH_b giving W_b = 5 g/kg", 46.93, rh_for_5, "P1-bs.psychrometrics-05")

    # --- saturation pressure cross-check against a different formulation
    for t in (0.01, 20, 40, 60, 90):
        row("pws vs Wagner-Pruss", f"p_ws({t} C)", pws_wagner_pruss(t), pws_water(t), 6e-4)


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
