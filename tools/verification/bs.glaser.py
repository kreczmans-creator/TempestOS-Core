#!/usr/bin/env python3
"""Independent re-derivation of bs.glaser (Track P, P1).

Method (written before reading the C# Calculate method and GlaserModel.cs):
  Layers listed inside first.  R_j = d_j/lambda_j (m2K/W), s_d,j = mu_j d_j (m).
  R_T = R_si + sum R_j + R_se.  Interface temperature (series resistance):
      theta_k = theta_i - (theta_i - theta_e) (R_si + sum_{j<=k} R_j) / R_T.
  Saturation pressure, ISO 13788 Annex E / Magnus form (Pa):
      p_sat = 610.5 exp(a theta/(b + theta));  a = 17.269, b = 237.3 for theta >= 0 degC;
      a = 21.875, b = 265.5 for theta < 0 degC (over ice).  Inverse: theta = b ln(x)/(a - ln(x)), x = p/610.5.
  Outside pressure p_e = RH_e p_sat(theta_e).  Inside pressure p_i = RH_i p_sat(theta_i), or
      p_i = p_e + f_dp dv(theta_e) R_v (T_i + T_e)/2, R_v = 461.5 J/(kg K), dv in kg/m3, capped at p_sat(theta_i).
  Glaser profile: vapour pressure against cumulative s_d.  With no condensate the profile is the taut string
      (lower convex hull) of the end points (0, p_i), (S_d, p_e) and the interface saturation points: it follows
      the straight line where the line stays under saturation and touches saturation at the interfaces it
      would otherwise cross (the tangent construction).  A wet interface (condensate present) is held at
      saturation: it is an obligatory point of the string; between obligatory points take the lower hull.
  Flux through a layer pair g = delta0 dp/ds_d, delta0 = 2e-10 kg/(m s Pa).  Net moisture arriving at an
      interface g_c = g_in - g_out = delta0 (slope_after - slope_before); Mc += g_c t (t in s) x 1000 g/kg,
      Mc = max(0, .) on contact or wet interfaces.
  Monthly cycle: start = first month that condenses (dry profile) after a month that does not (row 1 if all do);
      run 12 months from there; balance passes if nothing remains.
  Surface: f_Rsi = 1 - R_si,f/R_T,f; theta_si,min from p_sat = p_i/phi_crit (inverse form);
      f_Rsi,min = max over months with theta_e <= theta_i - 1 of (theta_si,min - theta_e)/(theta_i - theta_e).
Sources: ISO 13788 (Hygrothermal performance of building components and elements, internal surface
temperature and interstitial condensation; Annex E for p_sat); Glaser, Kaelte-Technik 1959.
"""
import math
import sys

ROWS = []
DELTA0 = 2e-10
RV = 461.5
DAYS = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31]
SEASON_T = [2, 3, 6, 9, 13, 16, 18, 18, 15, 11, 6, 3]
SEASON_RH = [90, 88, 84, 80, 76, 74, 74, 76, 82, 87, 90, 91]


def check(case, symbol, expected, value, tol, note=""):
    tol_eff = max(tol, 1e-6)
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    ROWS.append((case, symbol, expected, value, rel, tol, rel <= tol_eff, note))


def psat(t):
    a, b = (17.269, 237.3) if t >= 0 else (21.875, 265.5)
    return 610.5 * math.exp(a * t / (b + t))


def theta_of_psat(p):
    x = math.log(p / 610.5)
    a, b = (17.269, 237.3) if p >= 610.5 else (21.875, 265.5)
    return b * x / (a - x)


def lower_hull(pts):
    """Lower convex hull of points sorted by s. Collinear points are dropped."""
    h = []
    for p in pts:
        while len(h) >= 2:
            (x1, y1), (x2, y2) = h[-2], h[-1]
            cross = (x2 - x1) * (p[1] - y1) - (y2 - y1) * (p[0] - x1)
            if cross <= 1e-12 * max(1.0, abs(y1), abs(y2), abs(p[1])) * (p[0] - x1):
                h.pop()
            else:
                break
        h.append(p)
    return h


def profile(s, ps, p_i, p_e, wet):
    """Node vapour pressures (len n+1) for nodes 0..n; node 0 = p_i, node n = p_e, interior held by hull."""
    n = len(s) - 1
    y = [p_i] + list(ps[1:n]) + [p_e]
    fixed = [0] + [k for k in range(1, n) if wet[k]] + [n]
    val = [None] * (n + 1)
    for a, b in zip(fixed[:-1], fixed[1:]):
        h = lower_hull([(s[k], y[k]) for k in range(a, b + 1)])
        for (x1, y1), (x2, y2) in zip(h[:-1], h[1:]):
            for k in range(a, b + 1):
                if x1 - 1e-15 <= s[k] <= x2 + 1e-15:
                    val[k] = y1 + (y2 - y1) * (s[k] - x1) / (x2 - x1)
    val[0], val[n] = p_i, p_e
    return val


def gc_nodes(s, val):
    n = len(s) - 1
    g = [0.0] * (n + 1)
    for k in range(1, n):
        after = (val[k + 1] - val[k]) / (s[k + 1] - s[k])
        before = (val[k] - val[k - 1]) / (s[k] - s[k - 1])
        g[k] = DELTA0 * (after - before)
    return g


def dv_of(te, dv_a, th_a, dv_b, th_b):
    if te <= th_a:
        return dv_a
    if te >= th_b:
        return dv_b
    return dv_a + (dv_b - dv_a) * (te - th_a) / (th_b - th_a)


def glaser(layers, climate, theta_i=20.0, RH_i=50.0, Rsi=0.13, Rse=0.04, Rsi_f=0.25, RH_crit=80.0,
           Mc_limit=None, hum=None, dp_f=1.10, days=None):
    """layers: [(d_mm, lam, mu)], climate: [(theta_e, RH_e)] x12, hum = (dv_a, th_a, dv_b, th_b) or None."""
    d = [l[0] / 1000.0 for l in layers]
    R = [di / l[1] for di, l in zip(d, layers)]
    sd = [di * l[2] for di, l in zip(d, layers)]
    n = len(layers)
    RT = Rsi + sum(R) + Rse
    S = sum(sd)
    s = [0.0]
    for x in sd:
        s.append(s[-1] + x)
    cum = Rsi
    cumR = [Rsi]
    for r in R:
        cum += r
        cumR.append(cum)
    o = dict(R_T=RT, S_d=S, psat_i=psat(theta_i))
    p_e_m, p_i_m = [], []
    for (te, rhe) in climate:
        pe = rhe / 100.0 * psat(te)
        if hum is None:
            pi = RH_i / 100.0 * psat(theta_i)
        else:
            dv = dv_of(te, *hum) * 1e-3
            pi = pe + dp_f * dv * RV * ((theta_i + 273.15) + (te + 273.15)) / 2.0
            pi = min(pi, psat(theta_i))
        p_e_m.append(pe)
        p_i_m.append(pi)
    o["p_ext_m"], o["p_int_m"] = p_e_m, p_i_m
    dayl = days or DAYS

    def month_profile(m, wet):
        te = climate[m][0]
        th = [theta_i - (theta_i - te) * cumR[k] / RT for k in range(n + 1)]   # node k = after layer k
        ps = [psat(t) for t in th]
        # interface k (1..n-1) sits after layer k (index k-1): temperature th[k]
        val = profile(s, ps, p_i_m[m], p_e_m[m], wet)
        return th, ps, val

    # which months condense with the dry profile
    cond = []
    for m in range(12):
        if n < 2:
            cond.append(False)
            continue
        th, ps, val = month_profile(m, [False] * (n + 1))
        g = gc_nodes(s, val)
        cond.append(any(g[k] > 1e-15 for k in range(1, n)))
    o["cond"] = cond
    if not any(cond):
        start = 0
    elif all(cond):
        start = 1
    else:
        start = next(i + 1 for i in range(12) if cond[i] and not cond[i - 1])
    o["n_start"] = start
    Mc = [0.0] * (n + 1)
    Mc_row = [0.0] * 12
    Mc_if_peak = [0.0] * (n + 1)
    tot_peak = 0.0
    if start:
        for step in range(12):
            m = (start - 1 + step) % 12
            wet = [False] + [Mc[k] > 0 for k in range(1, n)] + [False]
            th, ps, val = month_profile(m, wet)
            g = gc_nodes(s, val)
            t = dayl[m] * 86400.0
            for k in range(1, n):
                if wet[k] or g[k] > 1e-15:
                    Mc[k] = max(0.0, Mc[k] + g[k] * t * 1000.0)
                Mc_if_peak[k] = max(Mc_if_peak[k], Mc[k])
            Mc_row[m] = sum(Mc)
            tot_peak = max(tot_peak, Mc_row[m])
    o["Mc_row"] = Mc_row
    o["Mc_if"] = Mc_if_peak
    o["Mc_max"] = max(Mc_if_peak)
    o["Mc_tot"] = tot_peak
    o["Mc_end"] = sum(Mc)
    o["balance"] = o["Mc_end"] <= 1e-9
    if Mc_limit:
        o["u_Mc"] = o["Mc_max"] / Mc_limit
    # surface
    RTf = Rsi_f + sum(R) + Rse
    fRsi = 1 - Rsi_f / RTf
    best, nf = 0.0, 0
    for m, (te, rhe) in enumerate(climate):
        if te <= theta_i - 1.0:
            ths = theta_of_psat(p_i_m[m] / (RH_crit / 100.0))
            f = (ths - te) / (theta_i - te)
            if f > best:
                best, nf = f, m + 1
    o.update(R_Tf=RTf, fRsi=fRsi, fRsi_min=best, n_fcrit=nf)
    o["u_f"] = best / fRsi
    ok_lim = (not Mc_limit) or o["Mc_max"] <= Mc_limit
    o["pass"] = 1 if (o["balance"] and ok_lim and fRsi >= best) else 0
    return o


INS_BRICK = [(100, 0.04, 2), (200, 0.8, 10)]
VAPOUR_OPEN = [(15, 0.5, 10), (215, 0.8, 8)]
TWO_PLANES = [(12.5, 0.25, 8), (140, 0.04, 1), (100, 0.8, 10), (50, 0.04, 1), (3, 0.9, 150)]
SEASON = list(zip(SEASON_T, SEASON_RH))
CONST = lambda t, rh: [(t, rh)] * 12


def kat():
    r = glaser(INS_BRICK, SEASON, Mc_limit=3000)
    c = "ka-seasonal-drying"
    check(c, "n_start", 11, r["n_start"], 1e-9)
    for k, e in [(11, 314.26204088410316), (12, 1115.1297163923496), (1, 2055.4959133011744), (2, 2773.36347024196),
                 (3, 3083.081081674129), (4, 2828.6599162337693), (5, 1648.0232742406943), (6, 0)]:
        check(c, f"Mc_{k}", e, r["Mc_row"][k - 1], 1e-9)
    check(c, "Mc_max", 3083.081081674129, r["Mc_max"], 1e-9)
    check(c, "Mc_end", 0, r["Mc_end"], 1e-9)
    check(c, "u_Mc", 1.0276936938913763, r["u_Mc"], 1e-9)
    check(c, "pass", 0, r["pass"], 1e-9)

    r = glaser(VAPOUR_OPEN, SEASON, RH_i=40)
    c = "ka-psat-20"
    for sym, e in [("psat_i", 2336.951143802342), ("fRsi", 0.5753715498938429), ("fRsi_min", 0.4038351769856001),
                   ("R_T", 0.46875), ("S_d", 1.87), ("Mc_max", 0), ("Mc_end", 0), ("n_start", 0), ("pass", 1)]:
        check(c, sym, e, r[sym], 1e-9)

    r = glaser(INS_BRICK, CONST(0, 90), Mc_limit=20000)
    c = "ka-single-interface-all-year"
    for sym, e in [("R_T", 2.92), ("S_d", 2.2), ("n_start", 1), ("Mc_max", 14139.573041777), ("Mc_end", 14139.573041777),
                   ("u_Mc", 0.70697865208885), ("pass", 0)]:
        check(c, sym, e, r[sym], 1e-9)
    check(c, "Mc_1", 1200.8952446441, r["Mc_row"][0], 1e-9)
    check(c, "Mc_2", 2285.5748204517, r["Mc_row"][1], 1e-9)
    check(c, "Mc_12", 14139.573041777, r["Mc_row"][11], 1e-9)
    check(c, "Mc_if_1", 14139.573041777, r["Mc_if"][1], 1e-9)

    r = glaser(TWO_PLANES, CONST(0, 90))
    c = "ka-two-planes-tangent"
    check(c, "R_T", 5.098333333333334, r["R_T"], 1e-9)
    check(c, "S_d", 1.74, r["S_d"], 1e-9)
    for k, e in [(1, 0), (2, 5143.147099105272), (3, 0), (4, 772.0081436491156)]:
        check(c, f"Mc_if_{k}", e, r["Mc_if"][k], 1e-9)
    check(c, "Mc_1", 502.3830480147563, r["Mc_row"][0], 1e-9)
    check(c, "Mc_max", 5143.147099105272, r["Mc_max"], 1e-9)
    check(c, "Mc_end", 5915.155242754388, r["Mc_end"], 1e-9)
    check(c, "pass", 0, r["pass"], 1e-9)

    r = glaser(VAPOUR_OPEN, SEASON, RH_i=50, Rsi_f=0.25)
    c = "ka-surface-factor"
    for sym, e in [("R_Tf", 0.58875), ("fRsi", 0.5753715498938429), ("fRsi_min", 0.5902560168111524), ("n_fcrit", 1),
                   ("u_f", 1.0258693133507482), ("Mc_max", 0), ("pass", 0)]:
        check(c, sym, e, r[sym], 1e-9)

    hum = (5, 0, 0, 20)
    r = glaser(INS_BRICK, SEASON, hum=hum, dp_f=1.10)
    c = "ka-humidity-class"
    check(c, "n_start", 10, r["n_start"], 1e-9)
    for k, e in [(10, 144.81298290481683), (4, 5111.051756092058), (8, 645.9625409543693)]:
        check(c, f"Mc_{k}", e, r["Mc_row"][k - 1], 1e-9)
    for sym, e in [("Mc_max", 5111.051756092058), ("Mc_end", 91.74381353425309), ("fRsi_min", 0.6704737425044534),
                   ("n_fcrit", 1), ("pass", 0)]:
        check(c, sym, e, r[sym], 1e-9)

    r = glaser(INS_BRICK, SEASON, hum=hum, dp_f=1.0)
    c = "ka-humidity-class-no-margin"
    check(c, "n_start", 10, r["n_start"], 1e-9)
    for k, e in [(10, 64.53411829081696), (4, 4249.892047228058), (8, 0)]:
        check(c, f"Mc_{k}", e, r["Mc_row"][k - 1], 1e-9)
    for sym, e in [("Mc_max", 4249.892047228058), ("Mc_end", 0), ("fRsi_min", 0.6302837752670817), ("n_fcrit", 1), ("pass", 1)]:
        check(c, sym, e, r[sym], 1e-9)


kat()

# ---------------- Extra checks ----------------
EXTRA = [  # (name, layers, climate, kwargs, product values read from the probe; Mc in g/m2)
    ('X1 six-layer UK wall, constant 50 %',
     [(12.5, 0.21, 8), (50, 0.035, 1), (18, 0.13, 50), (140, 0.04, 1), (9, 0.13, 30), (102, 0.84, 10)],
     [(4.3, 86), (4.4, 84), (6.5, 80), (8.9, 76), (12.3, 74), (15.1, 74), (17.0, 75), (16.8, 77), (14.3, 80), (11.0, 84), (7.2, 87), (4.9, 88)],
     {'Mc_limit': 500},
     {'n_start': 12.0, 'Mc_1': 129.2768365, 'Mc_2': 183.6030497, 'Mc_3': 160.4056473, 'Mc_4': 32.47580393, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 0.0, 'Mc_12': 59.2269478, 'Mc_if_1': 0.0, 'Mc_if_2': 0.0, 'Mc_if_3': 0.0, 'Mc_if_4': 183.6030497, 'Mc_if_5': 0.0, 'Mc_max': 183.6030497, 'Mc_tot': 183.6030497, 'Mc_end': 0.0, 'R_T': 5.487216117, 'S_d': 2.48, 'psat_i': 2336.951144, 'R_Tf': 5.607216117, 'fRsi': 0.9554145953, 'fRsi_min': 0.5302298282, 'n_fcrit': 1.0, 'u_f': 0.5549735485, 'pass': 1.0, 'u_Mc': 0.3672060993}),
    ('X2 humidity class, f_dp 1.3, not capped',
     [(12.5, 0.21, 8), (100, 0.035, 1), (100, 0.8, 12)],
     [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)],
     {'hum': (9, -10, 2, 20), 'dp_f': 1.3},
     {'n_start': 9.0, 'Mc_1': 11899.03913, 'Mc_2': 14886.58521, 'Mc_3': 17541.97947, 'Mc_4': 19259.8498, 'Mc_5': 19885.91681, 'Mc_6': 19685.09857, 'Mc_7': 18996.47326, 'Mc_8': 18629.05574, 'Mc_9': 774.5743923, 'Mc_10': 2580.33898, 'Mc_11': 5133.09794, 'Mc_12': 8345.349065, 'Mc_if_1': 0.0, 'Mc_if_2': 19885.91681, 'Mc_max': 19885.91681, 'Mc_tot': 19885.91681, 'Mc_end': 18629.05574, 'R_T': 3.211666667, 'S_d': 1.4, 'psat_i': 2336.951144, 'R_Tf': 3.331666667, 'fRsi': 0.9249624812, 'fRsi_min': 1.205193879, 'n_fcrit': 7.0, 'u_f': 1.302965151, 'pass': 0.0}),
    ('X3 record starting in July, explicit days',
     [(12.5, 0.21, 8), (100, 0.035, 1), (100, 0.8, 12)],
     [(17.0, 75), (16.8, 77), (14.3, 80), (11.0, 84), (7.2, 87), (4.9, 88), (4.3, 86), (4.4, 84), (6.5, 80), (8.9, 76), (12.3, 74), (15.1, 74)],
     {'Mc_limit': 1000, 'days': [31, 31, 30, 31, 30, 31, 31, 28, 31, 30, 31, 30]},
     {'n_start': 5.0, 'Mc_1': 0.0, 'Mc_2': 0.0, 'Mc_3': 0.0, 'Mc_4': 0.0, 'Mc_5': 199.1868001, 'Mc_6': 813.3316068, 'Mc_7': 1516.944085, 'Mc_8': 2131.301794, 'Mc_9': 2435.641546, 'Mc_10': 2256.840629, 'Mc_11': 1273.429847, 'Mc_12': 0.0, 'Mc_if_1': 0.0, 'Mc_if_2': 2435.641546, 'Mc_max': 2435.641546, 'Mc_tot': 2435.641546, 'Mc_end': 0.0, 'R_T': 3.211666667, 'S_d': 1.4, 'psat_i': 2336.951144, 'R_Tf': 3.331666667, 'fRsi': 0.9249624812, 'fRsi_min': 0.5302298282, 'n_fcrit': 7.0, 'u_f': 0.5732446872, 'pass': 0.0, 'u_Mc': 2.435641546}),
    ('X4 condensation spell wraps Nov to Feb',
     [(15, 0.5, 10), (80, 0.035, 2), (215, 0.8, 8)],
     [(1, 88), (8, 70), (12, 70), (14, 70), (16, 70), (18, 70), (18, 70), (17, 70), (15, 75), (11, 80), (7, 85), (3, 90)],
     {},
     {'n_start': 11.0, 'Mc_1': 1132.649726, 'Mc_2': 1004.025544, 'Mc_3': 296.3626924, 'Mc_4': 0.0, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 27.55847575, 'Mc_12': 492.549781, 'Mc_if_1': 0.0, 'Mc_if_2': 1132.649726, 'Mc_max': 1132.649726, 'Mc_tot': 1132.649726, 'Mc_end': 0.0, 'R_T': 2.754464286, 'S_d': 2.03, 'psat_i': 2336.951144, 'R_Tf': 2.874464286, 'fRsi': 0.9130272722, 'fRsi_min': 0.6118214896, 'n_fcrit': 1.0, 'u_f': 0.6701020969, 'pass': 1.0}),
    ('X5 two separate spells, dry between',
     [(12.5, 0.21, 8), (100, 0.035, 1), (100, 0.8, 12)],
     [(0, 95), (0, 95), (14, 60), (15, 60), (16, 60), (18, 60), (18, 60), (17, 60), (15, 60), (14, 60), (0, 95), (0, 95)],
     {},
     {'n_start': 11.0, 'Mc_1': 3957.196134, 'Mc_2': 5161.560175, 'Mc_3': 3625.807514, 'Mc_4': 1856.297124, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 1290.390044, 'Mc_12': 2623.793089, 'Mc_if_1': 0.0, 'Mc_if_2': 5161.560175, 'Mc_max': 5161.560175, 'Mc_tot': 5161.560175, 'Mc_end': 0.0, 'R_T': 3.211666667, 'S_d': 1.4, 'psat_i': 2336.951144, 'R_Tf': 3.331666667, 'fRsi': 0.9249624812, 'fRsi_min': 0.6312304151, 'n_fcrit': 1.0, 'u_f': 0.6824389399, 'pass': 1.0}),
    ('X6 vapour control layer inside',
     [(0.2, 0.3, 100000), (12.5, 0.21, 8), (100, 0.035, 1), (100, 0.8, 12)],
     [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)],
     {},
     {'n_start': 0.0, 'Mc_1': 0.0, 'Mc_2': 0.0, 'Mc_3': 0.0, 'Mc_4': 0.0, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 0.0, 'Mc_12': 0.0, 'Mc_if_1': 0.0, 'Mc_if_2': 0.0, 'Mc_if_3': 0.0, 'Mc_max': 0.0, 'Mc_tot': 0.0, 'Mc_end': 0.0, 'R_T': 3.212333333, 'S_d': 21.4, 'psat_i': 2336.951144, 'R_Tf': 3.332333333, 'fRsi': 0.9249774932, 'fRsi_min': 0.7365931537, 'n_fcrit': 1.0, 'u_f': 0.796336299, 'pass': 1.0}),
    ('X7 eight layers, -15 degC ice, foil',
     [(12.5, 0.21, 8), (0.5, 0.3, 50000), (50, 0.035, 1), (18, 0.13, 50), (140, 0.04, 1), (9, 0.13, 30), (25, 0.04, 1), (102, 0.84, 10)],
     [(-15, 90), (-15, 90), (-15, 90), (-15, 90), (-2, 85), (-2, 85), (-2, 85), (-2, 85), (8, 80), (8, 80), (8, 80), (8, 80)],
     {},
     {'n_start': 1.0, 'Mc_1': 3.656517734, 'Mc_2': 6.959178912, 'Mc_3': 10.61569665, 'Mc_4': 14.15426219, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 0.0, 'Mc_12': 0.0, 'Mc_if_1': 0.0, 'Mc_if_2': 0.0, 'Mc_if_3': 0.0, 'Mc_if_4': 0.0, 'Mc_if_5': 0.0, 'Mc_if_6': 0.0, 'Mc_if_7': 14.15426219, 'Mc_max': 14.15426219, 'Mc_tot': 14.15426219, 'Mc_end': 0.0, 'R_T': 6.113882784, 'S_d': 27.505, 'psat_i': 2336.951144, 'R_Tf': 6.233882784, 'fRsi': 0.9598965831, 'fRsi_min': 0.7892745229, 'n_fcrit': 1.0, 'u_f': 0.822249539, 'pass': 1.0}),
    ('X8 single layer (no interface)',
     [(200, 0.8, 10)],
     [(2, 90), (3, 88), (6, 84), (9, 80), (13, 76), (16, 74), (18, 74), (18, 76), (15, 82), (11, 87), (6, 90), (3, 91)],
     {},
     {'n_start': 0.0, 'Mc_1': 0.0, 'Mc_2': 0.0, 'Mc_3': 0.0, 'Mc_4': 0.0, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 0.0, 'Mc_12': 0.0, 'Mc_max': 0.0, 'Mc_tot': 0.0, 'Mc_end': 0.0, 'R_T': 0.42, 'S_d': 2.0, 'psat_i': 2336.951144, 'R_Tf': 0.54, 'fRsi': 0.537037037, 'fRsi_min': 0.5902560168, 'n_fcrit': 1.0, 'u_f': 1.099097411, 'pass': 0.0}),
    ('X9 summer, inside colder than outside',
     [(100, 0.035, 1), (100, 0.8, 12), (12, 0.7, 20)],
     [(26, 70), (28, 75), (30, 80), (32, 85), (30, 80), (28, 75), (26, 70), (28, 75), (30, 80), (32, 85), (30, 80), (28, 75)],
     {},
     {'n_start': 0.0, 'Mc_1': 0.0, 'Mc_2': 0.0, 'Mc_3': 0.0, 'Mc_4': 0.0, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 0.0, 'Mc_12': 0.0, 'Mc_if_1': 0.0, 'Mc_if_2': 0.0, 'Mc_max': 0.0, 'Mc_tot': 0.0, 'Mc_end': 0.0, 'R_T': 3.169285714, 'S_d': 1.54, 'psat_i': 2336.951144, 'R_Tf': 3.289285714, 'fRsi': 0.9239956569, 'fRsi_min': 0.0, 'n_fcrit': 0.0, 'u_f': 0.0, 'pass': 1.0}),
    ('X10 inside 40 degC and 100 %',
     [(100, 0.035, 1), (100, 0.8, 12)],
     [(4.3, 86), (4.4, 84), (6.5, 80), (8.9, 76), (12.3, 74), (15.1, 74), (17.0, 75), (16.8, 77), (14.3, 80), (11.0, 84), (7.2, 87), (4.9, 88)],
     {'theta_i': 40, 'RH_i': 100},
     {'n_start': 1.0, 'Mc_1': 34315.78241, 'Mc_2': 65273.40302, 'Mc_3': 98772.99028, 'Mc_4': 130217.8486, 'Mc_5': 161052.1596, 'Mc_6': 189339.6526, 'Mc_7': 217347.2504, 'Mc_8': 245507.2594, 'Mc_9': 274304.2701, 'Mc_10': 305863.8001, 'Mc_11': 338048.1992, 'Mc_12': 372165.9132, 'Mc_if_1': 372165.9132, 'Mc_max': 372165.9132, 'Mc_tot': 372165.9132, 'Mc_end': 372165.9132, 'R_T': 3.152142857, 'S_d': 1.3, 'psat_i': 7370.927674, 'R_Tf': 3.272142857, 'fRsi': 0.9235974678, 'fRsi_min': 1.18484132, 'n_fcrit': 7.0, 'u_f': 1.282854665, 'pass': 0.0}),
    ('X11 extreme mu 1e8 and 2000 mm',
     [(2000, 0.8, 100000000.0), (0.01, 0.005, 1), (100, 0.04, 1), (500, 1.0, 3)],
     [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)],
     {},
     {'n_start': 0.0, 'Mc_1': 0.0, 'Mc_2': 0.0, 'Mc_3': 0.0, 'Mc_4': 0.0, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 0.0, 'Mc_12': 0.0, 'Mc_if_1': 0.0, 'Mc_if_2': 0.0, 'Mc_if_3': 0.0, 'Mc_max': 0.0, 'Mc_tot': 0.0, 'Mc_end': 0.0, 'R_T': 5.672, 'S_d': 200000001.6, 'psat_i': 2336.951144, 'R_Tf': 5.792, 'fRsi': 0.9568370166, 'fRsi_min': 0.7365931537, 'n_fcrit': 1.0, 'u_f': 0.7698209213, 'pass': 1.0}),
    ('X12 outside 100 % at 5 degC all year',
     [(100, 0.04, 1), (200, 0.8, 10)],
     [(5, 100), (5, 100), (5, 100), (5, 100), (5, 100), (5, 100), (5, 100), (5, 100), (5, 100), (5, 100), (5, 100), (5, 100)],
     {},
     {'n_start': 1.0, 'Mc_1': 1054.959805, 'Mc_2': 2007.826725, 'Mc_3': 3062.78653, 'Mc_4': 4083.715374, 'Mc_5': 5138.675179, 'Mc_6': 6159.604022, 'Mc_7': 7214.563827, 'Mc_8': 8269.523632, 'Mc_9': 9290.452475, 'Mc_10': 10345.41228, 'Mc_11': 11366.34112, 'Mc_12': 12421.30093, 'Mc_if_1': 12421.30093, 'Mc_max': 12421.30093, 'Mc_tot': 12421.30093, 'Mc_end': 12421.30093, 'R_T': 2.92, 'S_d': 2.1, 'psat_i': 2336.951144, 'R_Tf': 3.04, 'fRsi': 0.9177631579, 'fRsi_min': 0.5083072202, 'n_fcrit': 1.0, 'u_f': 0.5538544621, 'pass': 0.0}),
    ('X13 R_si,f 0.04 and RH_crit 30 %',
     [(100, 0.04, 1), (200, 0.8, 10)],
     [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)],
     {'RH_crit': 30, 'Rsi_f': 0.04},
     {'n_start': 10.0, 'Mc_1': 10726.15294, 'Mc_2': 14122.75263, 'Mc_3': 16790.68764, 'Mc_4': 17761.89928, 'Mc_5': 16444.56457, 'Mc_6': 13232.98884, 'Mc_7': 8141.714733, 'Mc_8': 3684.532297, 'Mc_9': 1993.220906, 'Mc_10': 684.6307725, 'Mc_11': 3044.411228, 'Mc_12': 6610.951509, 'Mc_if_1': 17761.89928, 'Mc_max': 17761.89928, 'Mc_tot': 17761.89928, 'Mc_end': 1993.220906, 'R_T': 2.92, 'S_d': 2.1, 'psat_i': 2336.951144, 'R_Tf': 2.83, 'fRsi': 0.9858657244, 'fRsi_min': 5.262996557, 'n_fcrit': 7.0, 'u_f': 5.338451705, 'pass': 0.0}),
    ('X14 uninsulated 102+15 mm, two layers',
     [(102, 1.0, 8), (15, 1.0, 2)],
     [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)],
     {'RH_i': 50, 'Mc_limit': 500},
     {'n_start': 0.0, 'Mc_1': 0.0, 'Mc_2': 0.0, 'Mc_3': 0.0, 'Mc_4': 0.0, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 0.0, 'Mc_12': 0.0, 'Mc_if_1': 0.0, 'Mc_max': 0.0, 'Mc_tot': 0.0, 'Mc_end': 0.0, 'R_T': 0.287, 'S_d': 0.846, 'psat_i': 2336.951144, 'R_Tf': 0.407, 'fRsi': 0.3857493857, 'fRsi_min': 0.7365931537, 'n_fcrit': 1.0, 'u_f': 1.909512188, 'pass': 0.0, 'u_Mc': 0.0}),
    ('X15 same wall, brick in 12 slices (24 layers)',
     [(8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (8.5, 1.0, 8), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2), (1.25, 1.0, 2)],
     [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)],
     {'RH_i': 50, 'Mc_limit': 500},
     {'n_start': 12.0, 'Mc_1': 1403.22904, 'Mc_2': 1917.766982, 'Mc_3': 1020.70184, 'Mc_4': 0.0, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 0.0, 'Mc_12': 297.9163617, 'Mc_max': 1917.766982, 'Mc_tot': 1917.766982, 'Mc_end': 0.0, 'R_T': 0.287, 'S_d': 0.846, 'psat_i': 2336.951144, 'R_Tf': 0.407, 'fRsi': 0.3857493857, 'fRsi_min': 0.7365931537, 'n_fcrit': 1.0, 'u_f': 1.909512188, 'pass': 0.0, 'u_Mc': 3.835533965}),
    ('X16 three layers, as given, Mc_limit 1000',
     [(25, 0.035, 8), (50, 0.5, 10), (215, 0.5, 40)],
     [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)],
     {'RH_i': 40, 'Mc_limit': 1000},
     {'n_start': 12.0, 'Mc_1': 537.759028, 'Mc_2': 741.3435463, 'Mc_3': 447.0671847, 'Mc_4': 380.0676676, 'Mc_5': 0.0, 'Mc_6': 0.0, 'Mc_7': 0.0, 'Mc_8': 0.0, 'Mc_9': 0.0, 'Mc_10': 0.0, 'Mc_11': 0.0, 'Mc_12': 129.4096174, 'Mc_if_1': 499.738774, 'Mc_if_2': 380.0676676, 'Mc_max': 499.738774, 'Mc_tot': 741.3435463, 'Mc_end': 0.0, 'R_T': 1.414285714, 'S_d': 9.3, 'psat_i': 2336.951144, 'R_Tf': 1.534285714, 'fRsi': 0.8370577281, 'fRsi_min': 0.6167511852, 'n_fcrit': 1.0, 'u_f': 0.7368084237, 'pass': 1.0, 'u_Mc': 0.499738774}),
    ('X17 same wall, each layer in 9 slices (27 layers)',
     [(2.7777777777777777, 0.035, 8), (2.7777777777777777, 0.035, 8), (2.7777777777777777, 0.035, 8), (2.7777777777777777, 0.035, 8), (2.7777777777777777, 0.035, 8), (2.7777777777777777, 0.035, 8), (2.7777777777777777, 0.035, 8), (2.7777777777777777, 0.035, 8), (2.7777777777777777, 0.035, 8), (5.555555555555555, 0.5, 10), (5.555555555555555, 0.5, 10), (5.555555555555555, 0.5, 10), (5.555555555555555, 0.5, 10), (5.555555555555555, 0.5, 10), (5.555555555555555, 0.5, 10), (5.555555555555555, 0.5, 10), (5.555555555555555, 0.5, 10), (5.555555555555555, 0.5, 10), (23.88888888888889, 0.5, 40), (23.88888888888889, 0.5, 40), (23.88888888888889, 0.5, 40), (23.88888888888889, 0.5, 40), (23.88888888888889, 0.5, 40), (23.88888888888889, 0.5, 40), (23.88888888888889, 0.5, 40), (23.88888888888889, 0.5, 40), (23.88888888888889, 0.5, 40)],
     [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)],
     {'RH_i': 40, 'Mc_limit': 1000},
     {'n_start': 12.0, 'Mc_1': 537.759028, 'Mc_2': 741.3435463, 'Mc_3': 447.0671847, 'Mc_4': 403.6591943, 'Mc_5': 429.810427, 'Mc_6': 425.9775437, 'Mc_7': 398.7033275, 'Mc_8': 382.8843994, 'Mc_9': 407.6508793, 'Mc_10': 462.3855509, 'Mc_11': 528.643586, 'Mc_12': 129.4096174, 'Mc_max': 520.8194344, 'Mc_tot': 741.3435463, 'Mc_end': 528.643586, 'R_T': 1.414285714, 'S_d': 9.3, 'psat_i': 2336.951144, 'R_Tf': 1.534285714, 'fRsi': 0.8370577281, 'fRsi_min': 0.6167511852, 'n_fcrit': 1.0, 'u_f': 0.7368084237, 'pass': 0.0, 'u_Mc': 0.5208194344}),
    ('X18 humidity class above saturation, capped, f_dp 1.5',
     [(12.5, 0.21, 8), (100, 0.035, 1), (100, 0.8, 12)],
     [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)],
     {'hum': (12, -10, 6, 20), 'dp_f': 1.5},
     {'n_start': 1.0, 'Mc_1': 5280.78642, 'Mc_2': 9893.768371, 'Mc_3': 14440.12349, 'Mc_4': 18009.53814, 'Mc_5': 20463.03249, 'Mc_6': 21795.84118, 'Mc_7': 22230.44996, 'Mc_8': 23018.48854, 'Mc_9': 25205.01096, 'Mc_10': 28745.59066, 'Mc_11': 33043.5705, 'Mc_12': 38058.35917, 'Mc_if_1': 0.0, 'Mc_if_2': 38058.35917, 'Mc_max': 38058.35917, 'Mc_tot': 38058.35917, 'Mc_end': 38058.35917, 'R_T': 3.211666667, 'S_d': 1.4, 'psat_i': 2336.951144, 'R_Tf': 3.331666667, 'fRsi': 0.9249624812, 'fRsi_min': 2.828086656, 'n_fcrit': 7.0, 'u_f': 3.057514995, 'pass': 0.0}),
]

UK_NOTE = "Product values were read from `Probe run bs.glaser` (10 significant figures); Mc in g/m2."


def run_extra():
    for name, layers, climate, kw, prod in EXTRA:
        r = glaser(layers, climate, **kw)
        ind = dict(r)
        for m in range(12):
            ind[f"Mc_{m + 1}"] = r["Mc_row"][m]
        for k in range(1, len(layers)):
            ind[f"Mc_if_{k}"] = r["Mc_if"][k]
        for sym, pv in prod.items():
            if sym not in ind:
                continue
            check("extra " + name.split()[0], sym, pv, ind[sym], 1e-6, "product value hard-coded")


run_extra()


# ---------------- Findings evidence (informational) ----------------
FIND = []


def finding(tag, text):
    FIND.append((tag, text))

# F1: layer splitting.  The saturation line is joined straight between interfaces, so condensation inside a layer
# is only seen when the layer is split.  Same wall, brick (102 mm) in n slices, independent model.
W = [(102, 1.0, 8), (15, 1.0, 2)]
COLD_ = [(-8, 85), (-6, 84), (-1, 82), (5, 78), (11, 72), (15, 70), (18, 72), (17, 74), (12, 80), (6, 84), (0, 87), (-5, 86)]
for N in (1, 2, 4, 8, 12):
    L = []
    for d, l, m in W:
        L += [(d / N, l, m)] * N
    r = glaser(L, COLD_, RH_i=50, Mc_limit=500)
    finding("mesh", f"102 mm + 15 mm uninsulated wall, 50 % inside, each layer in {N} slice(s): n_start {r['n_start']}, "
            f"Mc_max {r['Mc_max']:.1f} g/m2, Mc_tot {r['Mc_tot']:.1f} g/m2.")
# F4: the same verdict flips with layer resolution (probe X16 against X17).
for label, L in (("as given", [(25, 0.035, 8), (50, 0.5, 10), (215, 0.5, 40)]),
                 ("each layer in 5 slices", [(25 / 5, 0.035, 8)] * 5 + [(50 / 5, 0.5, 10)] * 5 + [(215 / 5, 0.5, 40)] * 5),
                 ("each layer in 9 slices", [(25 / 9, 0.035, 8)] * 9 + [(50 / 9, 0.5, 10)] * 9 + [(215 / 9, 0.5, 40)] * 9)):
    r = glaser(L, COLD_, RH_i=40, Mc_limit=1000)
    finding("mesh-flip", f"25+50+215 mm wall, 40 % inside, {label}: Mc_max {r['Mc_max']:.1f} g/m2, Mc_end {r['Mc_end']:.1f} g/m2, "
            f"balance {'PASS' if r['balance'] else 'FAIL'}, pass {r['pass']}.")
# F5: inside surface below the dew point of the inside air: the interstitial flux into the first interface scales with 1/s_d of the first slice.
for N in (12, 24, 48, 96, 200):
    L = []
    for d, l, m in W:
        L += [(d / N, l, m)] * N
    r = glaser(L, COLD_, RH_i=50)
    finding("surface-saturated", f"102+15 mm wall, N={N} slices per layer: all condensate sits on interface 1; Mc_max {r['Mc_max']:.0f} g/m2 (grows with N).")
# F2: physical cross-checks of the saturation relation against commonly tabulated values (values only).
finding("psat", f"Magnus (ISO 13788 Annex E form): p_sat(20 degC) {psat(20):.1f} Pa (tables 2339 Pa), p_sat(30) {psat(30):.0f} Pa (tables 4246 Pa), "
        f"p_sat(-10) {psat(-10):.1f} Pa over ice (tables 259.9 Pa); dew point at 20 degC / 50 % = {theta_of_psat(0.5 * psat(20)):.2f} degC (tables 9.3 degC).")
# F3: balance check display when condensate remains.
finding("display", "Annual-balance check prints u = Infinity when Mc_end > 0 (probe X10, X12, X18).")


def main():
    print(f"{'case':34s} {'symbol':10s} {'KAT expected':>16s} {'independent':>16s} {'rel diff':>10s} {'KAT tol':>9s}  result")
    bad = 0
    for case, sym, e, v, rel, tol, ok, note in ROWS:
        if not ok:
            bad += 1
        print(f"{case:34s} {sym:10s} {e:16.10g} {v:16.10g} {rel:10.2e} {tol:9.1e}  {'PASS' if ok else 'FAIL'}  {note}")
    print(f"{len(ROWS) - bad}/{len(ROWS)} rows within tolerance")
    if FIND:
        print("\nFindings evidence (informational):")
        for tag, text in FIND:
            print(f"  {tag}: {text}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
