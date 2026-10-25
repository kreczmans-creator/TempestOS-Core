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
# EXTRA_BEGIN
# EXTRA_END

# ---------------- Findings evidence (informational) ----------------
FIND = []


def finding(tag, text):
    FIND.append((tag, text))


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
