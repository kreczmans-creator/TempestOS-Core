#!/usr/bin/env python3
"""Independent re-derivation of bs.insulation (Track P, P1).

Method sources (cited by title and number only):
  Steady radial conduction through a cylindrical layer: R' = ln(De/Di) / (2 pi lambda) per metre;
    series film resistance 1 / (h pi De). Plane layer: t / lambda + 1 / h.
    Same relations as BS EN ISO 12241 (formula route); Incropera, Fundamentals of Heat and Mass Transfer, 7th ed., Ch. 3.
  Radiation coefficient h_r = eps sigma (Ts^2 + Ta^2)(Ts + Ta), sigma = 5.670374419e-8 (CODATA 2018).
  Free convection to air: Holman, Heat Transfer, simplified air relations (horizontal cylinder and vertical plate,
    laminar and turbulent forms, larger one used).
  Forced convection: Churchill and Bernstein cross-flow cylinder (Incropera 7th ed., Eq. 7.57);
    flat plate, laminar and mixed boundary layer (Incropera 7th ed., Eq. 7.30 and 7.41), transition Re = 5e5.
  Mixed convection blend (h_nat^3 + h_forced^3)^(1/3) (Incropera 7th ed., Sec. 9.9).
  Air: k = 0.0241 (T/273.15)^0.81 W/mK, nu = 1.33e-5 (T/273.15)^1.7 m2/s, Pr = 0.71 (fits quoted in the spec).
  Dew point: saturation vapour pressure by the Magnus form as in ISO 13788 Annex (over water above 0 degC, over ice below).
Exit 0 when every KAT value matches within max(KAT tolerance, 1e-6).
"""
import math
import sys

import numpy as np
from scipy.optimize import brentq

SIG = 5.670374419e-8
PR = 0.71


def psat(t):
    if t >= 0:
        return 610.5 * math.exp(17.269 * t / (237.3 + t))
    return 610.5 * math.exp(21.875 * t / (265.5 + t))


def dew_point(t, rh):
    p = psat(t) * rh / 100.0
    x = math.log(p / 610.5)
    if t >= 0:
        # branch by the dew temperature itself
        td = 237.3 * x / (17.269 - x)
        if td >= 0:
            return td
    return 265.5 * x / (21.875 - x)


def air(tfilm_c):
    T = tfilm_c + 273.15
    return 0.0241 * (T / 273.15) ** 0.81, 1.33e-5 * (T / 273.15) ** 1.7


def h_free(geom, dT, size):
    dT = abs(dT)
    if dT <= 0:
        return 0.0
    if geom == "pipe":
        return max(1.32 * (dT / size) ** 0.25, 1.24 * dT ** (1.0 / 3.0))
    return max(1.42 * (dT / size) ** 0.25, 1.31 * dT ** (1.0 / 3.0))


def h_forced(geom, v, size, ts, ta):
    if v <= 0:
        return 0.0
    k, nu = air((ts + ta) / 2.0)
    re = v * size / nu
    if geom == "pipe":
        nu_d = 0.3 + 0.62 * re ** 0.5 * PR ** (1 / 3) / (1 + (0.4 / PR) ** (2 / 3)) ** 0.25 * (1 + (re / 282000.0) ** (5 / 8)) ** 0.8
    else:
        nu_d = 0.664 * re ** 0.5 * PR ** (1 / 3) if re < 5e5 else (0.037 * re ** 0.8 - 871.0) * PR ** (1 / 3)
    return nu_d * k / size


def h_corr(geom, ts, ta, eps, v, size):
    Ts, Ta = ts + 273.15, ta + 273.15
    hr = eps * SIG * (Ts ** 2 + Ta ** 2) * (Ts + Ta)
    hn = h_free(geom, ts - ta, size)
    hf = h_forced(geom, v, size, ts, ta)
    hc = (hn ** 3 + hf ** 3) ** (1 / 3)
    return hc, hr


def model(inp, t_mm):
    """State at insulation thickness t_mm. Returns dict with q (signed, W/m or W/m2), theta_se, h's, R, R_oth."""
    pipe = inp.get("geometry", "pipe") == "pipe"
    ti, ta = inp.get("theta_i", 80.0), inp.get("theta_a", 20.0)
    lam = inp.get("lambda", 0.04)
    t = t_mm * 1e-3
    o = {}
    if pipe:
        Di = inp.get("D_i", 100.0) * 1e-3
        De = Di + 2 * t
        roth = 0.0
        if inp.get("h_i"):
            Dp = Di - 2 * inp.get("t_w", 0.0) * 1e-3
            roth += 1.0 / (inp["h_i"] * math.pi * Dp)
        if inp.get("t_w") and inp.get("lambda_w"):
            Dp = Di - 2 * inp["t_w"] * 1e-3
            roth += math.log(Di / Dp) / (2 * math.pi * inp["lambda_w"])
        rins = math.log(De / Di) / (2 * math.pi * lam)
        area = math.pi * De
        size = De
    else:
        roth = 0.0
        if inp.get("h_i"):
            roth += 1.0 / inp["h_i"]
        if inp.get("t_w") and inp.get("lambda_w"):
            roth += inp["t_w"] * 1e-3 / inp["lambda_w"]
        rins = t / lam
        area = 1.0
        size = inp.get("L_surf", 1.0)
    o["R_oth"] = roth
    rin = roth + rins
    if inp.get("h_mode", "given") == "given":
        h = inp.get("h_se", 10.0)
        r = rin + 1.0 / (h * area)
        q = (ti - ta) / r
        ts = ta + q / (h * area)
        hc = hr = None
    else:
        eps, v = inp.get("eps", 0.9), inp.get("v_wind", 0.0)

        def f(ts_):
            hc_, hr_ = h_corr("pipe" if pipe else "flat", ts_, ta, eps, v, size)
            return (hc_ + hr_) * area * (ts_ - ta) - (ti - ts_) / rin

        lo, hi = min(ti, ta), max(ti, ta)
        ts = ti if rin <= 0 else brentq(f, lo, hi, xtol=1e-14, rtol=1e-14, maxiter=200)
        hc, hr = h_corr("pipe" if pipe else "flat", ts, ta, eps, v, size)
        h = hc + hr
        r = rin + 1.0 / (h * area)
        q = (ti - ta) / r
    o.update(R=r, q=q, theta_se=ts, h_se=h, h_c=hc, h_r=hr, area=area)
    # mean insulation temperature: inner face to surface
    tface = ti - q * roth
    o["theta_m"] = (tface + ts) / 2.0
    return o


def required_t(inp, ok):
    """Least thickness (mm) beyond which ok(state) holds for every thicker layer up to 1 m; inf if none."""
    grid = np.unique(np.concatenate(([0.0], np.geomspace(1e-3, 1000.0, 500), np.linspace(0, 1000.0, 500))))
    bad = [i for i, t in enumerate(grid) if not ok(model(inp, t))]
    if not bad:
        return 0.0
    last = bad[-1]
    if last == len(grid) - 1:
        return math.inf
    lo, hi = grid[last], grid[last + 1]
    for _ in range(200):
        mid = 0.5 * (lo + hi)
        if ok(model(inp, mid)):
            hi = mid
        else:
            lo = mid
        if hi - lo < 1e-13:
            break
    return 0.5 * (lo + hi)


def ins(inp):
    pipe = inp.get("geometry", "pipe") == "pipe"
    ti, ta = inp.get("theta_i", 80.0), inp.get("theta_a", 20.0)
    hot = ti > ta
    o = {}
    qmax = inp.get("q_max_m") if pipe else inp.get("q_max_A")
    sel = {}
    if qmax:
        sel["q"] = lambda s: abs(s["q"]) <= qmax
    if hot and inp.get("theta_se_max") is not None:
        sel["se"] = lambda s: s["theta_se"] <= inp["theta_se_max"]
    if (not hot) and inp.get("dew_margin") is not None:
        td = inp["theta_dew"] if inp.get("theta_dew") is not None else dew_point(ta, inp.get("RH_a", 60.0))
        o["theta_dew"] = td
        sel["dew"] = lambda s: s["theta_se"] >= td + inp["dew_margin"]
    req = {k: required_t(inp, f) for k, f in sel.items()}
    names = {"q": "t_q", "se": "t_se", "dew": "t_dew"}
    for k, v in req.items():
        o[names[k]] = v
    thick = max(req.values()) if req else None
    if thick is not None:
        o["thickness"] = thick
    t_eval = inp["t_chosen"] if inp.get("t_chosen") is not None else thick
    s = model(inp, t_eval)
    o["R"], o["R_oth"], o["theta_se"], o["h_se"] = s["R"], s["R_oth"], s["theta_se"], s["h_se"]
    o["theta_m"] = s["theta_m"]
    if s["h_c"] is not None:
        o["h_c"], o["h_r"] = s["h_c"], s["h_r"]
    o["q_per_m" if pipe else "q_area"] = abs(s["q"])
    if inp.get("t_chosen") is not None:
        if qmax:
            o["u_q"] = abs(s["q"]) / qmax
        if hot and inp.get("theta_se_max") is not None:
            o["u_se"] = (s["theta_se"] - ta) / (inp["theta_se_max"] - ta)
        if (not hot) and inp.get("dew_margin") is not None:
            o["u_dew"] = (ta - s["theta_se"]) / (ta - (td + inp["dew_margin"]))
    if pipe:
        b = model(inp, 0.0)
        o["D_crit"] = 2 * inp.get("lambda", 0.04) / b["h_se"] * 1e3
    return o


def conv(inputs):
    d = {}
    m = {"D_i": "D_i", "theta_i": "theta_i", "theta_a": "theta_a", "lambda": "lambda", "h_se": "h_se", "eps": "eps",
         "v_wind": "v_wind", "L_surf": "L_surf", "h_i": "h_i", "t_w": "t_w", "lambda_w": "lambda_w", "q_max_m": "q_max_m",
         "q_max_A": "q_max_A", "theta_se_max": "theta_se_max", "dew_margin": "dew_margin", "theta_dew": "theta_dew",
         "RH_a": "RH_a", "t_chosen": "t_chosen", "theta_lambda": "theta_lambda"}
    for kind, sym, val in inputs:
        if kind in ("Q", "CalcInputValue.Of"):
            d[m[sym]] = float(val.split(",")[0])
        elif sym == "geometry":
            d["geometry"] = "pipe" if val == "Pipe" else "flat"
        elif sym == "h_mode":
            d["h_mode"] = "given" if val == "HGiven" else "corr"
    return d

KATS = {
 "ka-pipe-chosen": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Pipe'),
   ('Q', 't_chosen', '30, "mm"'),
   ('Q', 'q_max_m', '30, "W/m"'),
   ('Q', 'theta_se_max', '45, "degC"'),
 ], expected=[
   ('R', 2.0690287005046017, "m*K/W", 1e-9),
   ('q_per_m', 28.99911440830521, "W/m", 1e-9),
   ('theta_se', 25.769190504211473, "degC", 1e-9),
   ('u_q', 0.9666371469435070, "", 1e-9),
 ]),
 "ka-pipe-heat-loss": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Pipe'),
   ('Q', 'q_max_m', '25, "W/m"'),
 ], expected=[
   ('thickness', 37.30359728106675, "mm", 1e-9),
   ('t_q', 37.30359728106675, "mm", 1e-9),
   ('theta_se', 24.557513895432887, "degC", 1e-9),
 ]),
 "ka-pipe-surface": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Pipe'),
   ('Q', 'q_max_m', '25, "W/m"'),
   ('Q', 'theta_se_max', '45, "degC"'),
 ], expected=[
   ('t_se', 5.325913504887262, "mm", 1e-9),
   ('t_q', 37.30359728106675, "mm", 1e-9),
   ('thickness', 37.30359728106675, "mm", 1e-9),
 ]),
 "ka-flat-closed-form": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Flat'),
   ('Q', 'q_max_A', '20, "W/m^2"'),
 ], expected=[
   ('thickness', 116, "mm", 1e-9),
   ('theta_se', 22, "degC", 1e-9),
   ('q_area', 20, "W/m^2", 1e-9),
 ]),
 "ka-flat-condensation": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Flat'),
   ('Q', 'theta_i', '5, "degC"'),
   ('Q', 'theta_a', '25, "degC"'),
   ('Q', 'RH_a', '70, "%"'),
   ('Q', 'dew_margin', '1, "K"'),
 ], expected=[
   ('theta_dew', 19.14535190118326, "degC", 1e-9),
   ('thickness', 12.479052316788733, "mm", 1e-9),
 ]),
 "ka-pipe-condensation": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Pipe'),
   ('Q', 'theta_i', '5, "degC"'),
   ('Q', 'theta_a', '25, "degC"'),
   ('Q', 'RH_a', '70, "%"'),
   ('Q', 'dew_margin', '1, "K"'),
 ], expected=[
   ('thickness', 11.290660460665531, "mm", 1e-9),
   ('q_per_m', 18.695277853154153, "W/m", 1e-9),
   ('theta_se', 20.14535190118326, "degC", 1e-9),
 ]),
 "ka-correlation-still": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Pipe'),
   ('Q', 'D_i', '114, "mm"'),
   ('Q', 'theta_i', '120, "degC"'),
   ('Q', 'theta_a', '15, "degC"'),
   ('Choice', 'h_mode', 'HCorrelation'),
   ('CalcInputValue.Of', 'eps', '0.1'),
   ('Q', 'v_wind', '0, "m/s"'),
   ('Q', 'q_max_m', '60, "W/m"'),
   ('Q', 'theta_se_max', '60, "degC"'),
   ('Q', 't_chosen', '50, "mm"'),
 ], expected=[
   ('theta_se', 27.854972274669827, "degC", 1e-8),
   ('h_c', 3.6748386376329623, "W/(m^2*K)", 1e-8),
   ('h_r', 0.5800655776199201, "W/(m^2*K)", 1e-8),
   ('h_se', 4.254904215252882, "W/(m^2*K)", 1e-8),
   ('q_per_m', 36.77262036715434, "W/m", 1e-8),
   ('t_q', 23.374326102060238, "mm", 1e-8),
   ('t_se', 7.8422918751036015, "mm", 1e-8),
 ]),
 "ka-correlation-wind": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Pipe'),
   ('Q', 'D_i', '114, "mm"'),
   ('Q', 'theta_i', '120, "degC"'),
   ('Q', 'theta_a', '15, "degC"'),
   ('Choice', 'h_mode', 'HCorrelation'),
   ('CalcInputValue.Of', 'eps', '0.9'),
   ('Q', 'v_wind', '3, "m/s"'),
   ('Q', 'q_max_m', '60, "W/m"'),
   ('Q', 't_chosen', '50, "mm"'),
 ], expected=[
   ('theta_se', 18.03869096438963, "degC", 1e-8),
   ('h_c', 14.955903095093321, "W/(m^2*K)", 1e-8),
   ('h_se', 19.917640766341446, "W/(m^2*K)", 1e-8),
   ('q_per_m', 40.69003615128239, "W/m", 1e-8),
   ('thickness', 29.585912146391017, "mm", 1e-8),
 ]),
 "ka-flat-correlation": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Flat'),
   ('Q', 'theta_i', '60, "degC"'),
   ('Q', 'theta_a', '20, "degC"'),
   ('Choice', 'h_mode', 'HCorrelation'),
   ('CalcInputValue.Of', 'eps', '0.9'),
   ('Q', 'v_wind', '0, "m/s"'),
   ('Q', 'L_surf', '2, "m"'),
   ('Q', 'q_max_A', '30, "W/m^2"'),
   ('Q', 't_chosen', '50, "mm"'),
 ], expected=[
   ('theta_se', 23.942457683302294, "degC", 1e-8),
   ('h_c', 2.0694755947709167, "W/(m^2*K)", 1e-8),
   ('h_r', 5.247288761516622, "W/(m^2*K)", 1e-8),
   ('q_area', 28.846033853358165, "W/m^2", 1e-8),
 ]),
 "ka-wall-and-film": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Pipe'),
   ('Q', 'h_i', '500, "W/(m^2*K)"'),
   ('Q', 't_w', '3, "mm"'),
   ('Q', 'lambda_w', '50, "W/(m*K)"'),
   ('Q', 'q_max_m', '30, "W/m"'),
   ('Q', 't_chosen', '30, "mm"'),
 ], expected=[
   ('R_oth', 0.006969506297018693, "m*K/W", 1e-9),
   ('R', 2.0759982068016205, "m*K/W", 1e-9),
   ('q_per_m', 28.90175906868378, "W/m", 1e-9),
   ('theta_se', 25.74982227479005, "degC", 1e-9),
 ]),
 "ka-dew-point-entered": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Flat'),
   ('Q', 'theta_i', '5, "degC"'),
   ('Q', 'theta_a', '25, "degC"'),
   ('Q', 'theta_dew', '20, "degC"'),
   ('Q', 'dew_margin', '1, "K"'),
 ], expected=[
   ('theta_dew', 20, "degC", 1e-9),
   ('thickness', 16, "mm", 1e-9),
   ('theta_se', 21, "degC", 1e-9),
 ]),
 "ka-flat-condensation-ice": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Flat'),
   ('Q', 'theta_i', '-30, "degC"'),
   ('Q', 'theta_a', '-10, "degC"'),
   ('Q', 'RH_a', '90, "%"'),
   ('Q', 'dew_margin', '1, "K"'),
 ], expected=[
   ('theta_dew', -11.178796330412547, "degC", 1e-9),
   ('thickness', 443.43647599148875, "mm", 1e-9),
 ]),
 "ka-flat-wind-turbulent": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Flat'),
   ('Q', 'theta_i', '60, "degC"'),
   ('Q', 'theta_a', '20, "degC"'),
   ('Choice', 'h_mode', 'HCorrelation'),
   ('CalcInputValue.Of', 'eps', '0.9'),
   ('Q', 'v_wind', '20, "m/s"'),
   ('Q', 'L_surf', '10, "m"'),
   ('Q', 'q_max_A', '40, "W/m^2"'),
   ('Q', 't_chosen', '50, "mm"'),
 ], expected=[
   ('theta_se', 20.693130399686403, "degC", 1e-8),
   ('h_c', 40.206477210567066, "W/(m^2*K)", 1e-8),
   ('h_r', 5.160881795008252, "W/(m^2*K)", 1e-8),
   ('q_area', 31.44549568025088, "W/m^2", 1e-8),
   ('thickness', 39.118252413929866, "mm", 1e-8),
 ]),
 "ka-bare-complies-window": dict(secondary=False, inputs=[
   ('Choice', 'geometry', 'Pipe'),
   ('Q', 'D_i', '10, "mm"'),
   ('Q', 'lambda', '0.2, "W/(m*K)"'),
   ('Q', 'q_max_m', '25, "W/m"'),
 ], expected=[
   ('thickness', 74.29172044012402, "mm", 1e-9),
   ('q_per_m', 25, "W/m", 1e-9),
   ('D_crit', 40, "mm", 1e-9),
 ]),
}
# Extra probes: name, model inputs, product values observed with tools/verification/probe (run, 2026-10-25).
# Units as the KAT symbols: mm for thicknesses and D_crit, degC, W/m, W/m2, K*m/W.
# Y6 (unattainable, blocked by the product) and the guard probes are in the report only.
EXTRAS = [
    ('Y1 hot pipe 168.3 mm, lam 0.035, 150/10, h 8, q 40, se 50, 60 mm chosen',
     {'geometry': 'pipe', 'D_i': 168.3, 'theta_i': 150, 'theta_a': 10, 'lambda': 0.035, 'h_se': 8, 'q_max_m': 40, 'theta_se_max': 50, 't_chosen': 60},
     {'thickness': 93.10859698, 't_q': 93.10859698, 't_se': 10.32813628, 'theta_se': 17.4727708, 'theta_m': 83.7363854, 'q_per_m': 54.14597338, 'R': 2.585603162, 'D_crit': 8.75, 'u_q': 1.353649335, 'u_se': 0.1868192706}),
    ('Y2 chilled pipe 22 mm, 6/28 degC, RH 80, margin 0, gain limit 10 W/m',
     {'geometry': 'pipe', 'D_i': 22, 'theta_i': 6, 'theta_a': 28, 'lambda': 0.035, 'h_se': 8, 'RH_a': 80, 'dew_margin': 0, 'q_max_m': 10},
     {'thickness': 14.31564017, 't_q': 1.615088944, 't_dew': 14.31564017, 'theta_dew': 24.2219748, 'theta_se': 24.2219748, 'theta_m': 15.1109874, 'q_per_m': 4.807547893, 'R': 4.576137459, 'D_crit': 8.75}),
    ('Y3 hot flat, correlation, cladding eps 0.3, 1 m/s, L 1.5, 70 -> 5 degC, q 40',
     {'geometry': 'flat', 'theta_i': 70, 'theta_a': 5, 'h_mode': 'corr', 'eps': 0.3, 'v_wind': 1, 'L_surf': 1.5, 'q_max_A': 40, 'theta_se_max': 35},
     {'thickness': 57.31548423, 't_q': 57.31548423, 't_se': 7.340448674, 'theta_se': 12.6845158, 'theta_m': 41.3422579, 'q_area': 40.0, 'R': 1.625, 'h_c': 3.679165075, 'h_r': 1.526107608, 'h_se': 5.205272683}),
    ('Y4 cold pipe correlation, still air, eps 0.9, 273 mm, -10/25, RH 75 margin 2',
     {'geometry': 'pipe', 'D_i': 273, 'theta_i': -10, 'theta_a': 25, 'lambda': 0.034, 'h_mode': 'corr', 'eps': 0.9, 'v_wind': 0, 'RH_a': 75, 'dew_margin': 2, 't_chosen': 50},
     {'thickness': 46.12852729, 't_dew': 46.12852729, 'theta_dew': 20.2573604, 'theta_se': 22.4627311, 'theta_m': 6.2313655, 'q_per_m': 22.21983617, 'R': 1.57516913, 'h_c': 2.131760198, 'h_r': 5.341595341, 'h_se': 7.473355539, 'D_crit': 7.579410092, 'u_dew': 0.9251193293}),
    ('Y5 near zero: theta_i - theta_a = 0.05 K, q limit 25 W/m',
     {'geometry': 'pipe', 'theta_i': 20.05, 'theta_a': 20, 'q_max_m': 25},
     {'thickness': 0.0, 't_q': 0.0, 'theta_se': 20.05, 'theta_m': 20.05, 'q_per_m': 0.1570796327, 'R': 0.3183098862, 'D_crit': 8.0}),
    ('Y7 wall and film, flat: h_i 300, wall 5 mm lam 45, 90 -> 20, q 30, 40 mm chosen',
     {'geometry': 'flat', 'theta_i': 90, 'theta_a': 20, 'h_i': 300, 't_w': 5, 'lambda_w': 45, 'q_max_A': 30, 't_chosen': 40},
     {'thickness': 89.19555556, 't_q': 89.19555556, 'theta_se': 26.343772, 'theta_m': 58.0626322, 'q_area': 63.43772027, 'R': 1.103444444, 'R_oth': 0.003444444444, 'u_q': 2.114590676}),
    ('Y8 hot pipe correlation with wind 8 m/s eps 0.9, 60.3 mm, 200/0, q 50, se 55',
     {'geometry': 'pipe', 'D_i': 60.3, 'theta_i': 200, 'theta_a': 0, 'lambda': 0.045, 'h_mode': 'corr', 'eps': 0.9, 'v_wind': 8, 'q_max_m': 50, 'theta_se_max': 55},
     {'thickness': 61.95623584, 't_q': 61.95623584, 't_se': 2.355283914, 'theta_se': 2.5138754, 'theta_m': 101.2569377, 'q_per_m': 50.0, 'R': 4.0, 'h_c': 30.15022566, 'h_r': 4.218019771, 'h_se': 34.36824543, 'D_crit': 1.668856373}),
    ('Y9 imperial: 6 in pipe 6.625 in, 300 degF, 70 degF air, 2 in chosen, lam 0.25 Btu in/(hr ft2 F) as 0.036 W/mK',
     {'geometry': 'pipe', 'D_i': 168.27499999999998, 'theta_i': 148.88888888888889, 'theta_a': 21.11111111111111, 'lambda': 0.036, 'q_max_m': 30, 't_chosen': 50.8},
     {'thickness': 132.7272537, 't_q': 132.7272537, 'theta_se': 27.9422205, 'theta_m': 88.4155547, 'q_per_m': 57.91669473, 'R': 2.206233943, 'D_crit': 7.2, 'u_q': 1.930556491}),
    ('Y10 bare complies: 300 mm hot pipe lam 0.04 q_max 1000 W/m (required 0)',
     {'geometry': 'pipe', 'D_i': 300, 'q_max_m': 1000},
     {'thickness': 0.0, 't_q': 0.0, 'theta_se': 80.0, 'theta_m': 80.0, 'q_per_m': 565.4866776, 'R': 0.1061032954, 'D_crit': 8.0}),
    ('Y11 flat, wind 40 m/s, L 3 m, corr eps 0.1, 150/-20, q 25',
     {'geometry': 'flat', 'theta_i': 150, 'theta_a': -20, 'h_mode': 'corr', 'eps': 0.1, 'v_wind': 40, 'L_surf': 3, 'q_max_A': 25, 't_chosen': 100},
     {'thickness': 271.5826641, 't_q': 271.5826641, 'theta_se': -19.2930939, 'theta_m': 65.353453, 'q_area': 67.71723756, 'R': 2.510439086, 'h_c': 95.42431524, 'h_r': 0.3695083104, 'h_se': 95.79382355, 'u_q': 2.708689503}),
    ('Y12 cold flat, entered dew 12 degC margin 3 K, q gain 15 W/m2',
     {'geometry': 'flat', 'theta_i': 2, 'theta_a': 22, 'theta_dew': 12, 'dew_margin': 3, 'q_max_A': 15, 'lambda': 0.03},
     {'thickness': 37.0, 't_q': 37.0, 't_dew': 5.571428571, 'theta_dew': 12.0, 'theta_se': 20.5, 'theta_m': 11.25, 'q_area': 15.0, 'R': 1.333333333}),
    ("Y13 sub-zero air, RH 90 % used as relative to ice (the product's reading), -30/-10, margin 0, flat",
     {'geometry': 'flat', 'theta_i': -30, 'theta_a': -10, 'RH_a': 90, 'dew_margin': 0},
     {'thickness': 63.86583733, 't_dew': 63.86583733, 'theta_dew': -11.1787963, 'theta_se': -11.1787963, 'theta_m': -20.5893982, 'q_area': 11.7879633, 'R': 1.696645933}),
    ('Y14 same, dew point entered as -10.10045758 degC (RH 90 % relative to water, frost point)',
     {'geometry': 'flat', 'theta_i': -30, 'theta_a': -10, 'theta_dew': -10.10045758, 'dew_margin': 0},
     {'thickness': 792.3560341, 't_dew': 792.3560341, 'theta_dew': -10.1004576, 'theta_se': -10.1004576, 'theta_m': -20.0502288, 'q_area': 1.0045758, 'R': 19.90890085}),
]


def rel(a, b):
    return abs(a - b) / max(abs(b), 1e-300) if b != 0 else abs(a - b)


def main():
    fails = 0
    n = 0
    print("%-26s %-12s %-16s %-16s %-9s %-8s %s" % ("case", "symbol", "KAT expected", "independent", "rel.diff", "KAT tol", "result"))
    for name, k in KATS.items():
        o = ins(conv(k["inputs"]))
        for sym, exp, unit, tol in k["expected"]:
            got = o.get(sym)
            if got is None:
                print("%-26s %-12s %-16s %-16s %-9s %-8s %s" % (name, sym, exp, "n/a", "-", tol, "FAIL (no independent value)"))
                fails += 1
                n += 1
                continue
            r = rel(got, exp)
            ok = r <= max(tol, 1e-6)
            n += 1
            fails += 0 if ok else 1
            print("%-26s %-12s %-16.10g %-16.10g %-9.2e %-8.0e %s" % (name, sym, exp, got, r, tol, "PASS" if ok else "FAIL"))
    print("\nExtra checks (product values hard-coded from the probe tool, bs.insulation 1.1.0)")
    for name, inp, prod in EXTRAS:
        o = ins(inp)
        for sym, pv in prod.items():
            got = o.get(sym)
            r = rel(got, pv) if got is not None else float("inf")
            ok = r <= 1e-6
            n += 1
            fails += 0 if ok else 1
            print("%-26s %-12s %-16.10g %-16.10g %-9.2e %-8s %s" % (name.split()[0], sym, pv, got if got is not None else float("nan"), r, "1e-6", "PASS" if ok else "FAIL"))
    print("\n%d values compared, %d failed" % (n, fails))
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
