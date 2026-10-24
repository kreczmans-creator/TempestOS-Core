#!/usr/bin/env python3
"""Independent re-derivation of mech.tolerance-stack (Track P, P1).

Method (written from first principles, not translated from the C#):
  mid-limit m = nominal + (upper + lower)/2 + shift          (size)
  half-band h = (upper - lower)/2                            (size)
  position/profile: h = (zone + bonus)/2, m = nominal + shift
  x_mean = sum(sign * m), t_wc = sum(h), t_rss = sqrt(sum h^2), t_mrss = f * t_rss
  sigma_i = h/k (normal), h/sqrt(3) (uniform), h/sqrt(6) (triangular)
  sigma = sqrt(sum sigma_i^2), Cp = (USL-LSL)/(6 sigma), Cpk = min(x-LSL, USL-x)/(3 sigma)
  DPMO = 1e6 * (Q((x-LSL)/sigma) + Q((USL-x)/sigma))
"""
import math
import sys
from scipy.stats import norm

IN_TO_MM = 25.4  # not used: KAT 1 is entered in inches and compared in inches


def stack(rows, lsl=None, usl=None, k=3.0, f=1.5):
    x_nom = 0.0
    x_mean = 0.0
    hs = []
    sig2 = 0.0
    for r in rows:
        sign = 1.0 if r["dir"] == "adds" else -1.0
        kind = r.get("kind", "size")
        n = r["nom"]
        u = r["up"]
        l = r.get("lo", 0.0)
        shift = r.get("shift", 0.0)
        if kind == "size":
            m = n + (u + l) / 2.0 + shift
            h = (u - l) / 2.0
        else:
            m = n + shift
            h = (u + r.get("bonus", 0.0)) / 2.0
        dist = r.get("dist", "normal")
        s = {"normal": h / k, "uniform": h / math.sqrt(3), "triangular": h / math.sqrt(6)}[dist]
        x_nom += sign * n
        x_mean += sign * m
        hs.append(h)
        sig2 += s * s
    out = {}
    out["x_nom"] = x_nom
    out["x_mean"] = x_mean
    out["t_wc"] = sum(hs)
    out["x_wc_min"] = x_mean - out["t_wc"]
    out["x_wc_max"] = x_mean + out["t_wc"]
    out["t_rss"] = math.sqrt(sum(h * h for h in hs))
    out["t_mrss"] = f * out["t_rss"]
    out["sigma"] = math.sqrt(sig2)
    sg = out["sigma"]
    if sg > 0:
        if lsl is not None and usl is not None:
            out["cp"] = (usl - lsl) / (6 * sg)
        cands = []
        if lsl is not None:
            cands.append(x_mean - lsl)
        if usl is not None:
            cands.append(usl - x_mean)
        if cands:
            out["cpk"] = min(cands) / (3 * sg)
            p = 0.0
            if lsl is not None:
                p += norm.sf((x_mean - lsl) / sg)
            if usl is not None:
                p += norm.sf((usl - x_mean) / sg)
            out["dpmo"] = p * 1e6
            out["yield"] = 1.0 - p
    return out


def row(name, d, a, b=None):
    return (name, d, a, b)


KATS = []
# ka-shigley-screw (inches)
shig = [
    dict(dir="adds", nom=1.750, up=0.003, lo=-0.003),
    dict(dir="subtracts", nom=0.750, up=0.001, lo=-0.001),
    dict(dir="subtracts", nom=0.120, up=0.005, lo=-0.005),
    dict(dir="subtracts", nom=0.875, up=0.001, lo=-0.001),
]
KATS.append(("ka-shigley-screw", stack(shig, lsl=0.003, k=3), [
    ("x_nom", 0.005, 1e-9), ("t_wc", 0.010, 1e-9), ("x_wc_min", -0.005, 1e-6),
    ("t_rss", 0.006, 1e-6), ("sigma", 0.002, 1e-6), ("cpk", 1.0 / 3.0, 1e-6)]))
# ka-rss-analytic (mm)
hous = [
    dict(dir="adds", nom=40, up=0.10, lo=0.0),
    dict(dir="subtracts", nom=20, up=0.0, lo=-0.12),
    dict(dir="subtracts", nom=19.5, up=0.02, lo=-0.02),
]
KATS.append(("ka-rss-analytic", stack(hous, lsl=0.40, usl=0.80, k=3), [
    ("x_mean", 0.61, 1e-9), ("t_wc", 0.13, 1e-9), ("t_rss", 0.080622577, 1e-6),
    ("sigma", 0.026874192, 1e-6), ("cpk", 2.3566600, 1e-6), ("dpmo", 7.7755e-7, 0.05)]))

# Extra checks: (label, short inputs, symbol, product value, independent value, tolerance)
EXTRAS = []

# Extra checks: product values are SI metres (x1000 to mm) as printed by the probe tool.
# Each entry: label, rows, lsl, usl, k, f, {symbol: product value}, finding flag.
def R(d, nom, up, lo=0.0, **kw):
    return dict(dir=d, nom=nom, up=up, lo=lo, **kw)


BASE4 = [R("adds", 10, 0.01, -0.01), R("subtracts", 9, 0.01, -0.01), R("subtracts", 0.5, 0.01, -0.01), R("adds", 0.0, 0.01, -0.01)]
LEN = {"x_nom", "x_mean", "t_wc", "x_wc_min", "x_wc_max", "t_rss", "t_mrss", "sigma"}
PROBES = [
    ("p1 typical 5 rows", [R("adds", 50, .05, -.05), R("subtracts", 10, .02, -.02), R("subtracts", 12, .03, -.01),
                           R("subtracts", 15, 0, -.04), R("subtracts", 12.5, .02, -.02)], 0.1, 1.0, 3, 1.5,
     dict(x_mean=0.00051, t_wc=0.00013, x_wc_min=0.00038, t_rss=6.403124237e-05, t_mrss=9.604686356e-05,
          sigma=2.134374746e-05, cp=7.027819285, cpk=6.403124237, dpmo=1.544884063e-76), False),
    ("p2 uniform/triang/shift", [R("adds", 30, .1, -.1, dist="uniform"), R("subtracts", 10, .06, -.06, dist="triangular", shift=.02),
                                 R("subtracts", 9, .05, -.05), R("subtracts", 10.5, 0, -.09, shift=-.01)], 0.2, 1.2, 3, 1.5,
     dict(x_mean=0.000535, t_wc=0.000255, t_rss=0.0001346291202, t_mrss=0.0002019436803, sigma=6.660413734e-05,
          cp=2.502347051, cpk=1.676572524, dpmo=0.2456014115), False),
    ("p3 position+bonus+profile", [R("adds", 25, .1, -.1), R("subtracts", 0, .2, kind="position", bonus=.1),
                                   R("adds", 0, .1, kind="profile"), R("subtracts", 24, .05, -.05)], 0.5, 1.5, 3, 1.5,
     dict(x_mean=0.001, t_wc=0.00035, t_rss=0.0001936491673, sigma=6.454972244e-05, cp=2.581988897,
          cpk=2.581988897, dpmo=9.516827414e-09), True),  # dpmo: upper tail at z=7.75 (finding 02)
    ("p4 min only, mean below", [R("adds", 10, .1, -.1), R("subtracts", 9.9, .1, -.1), R("subtracts", 0, 0, 0)], 0.3, None, 3, 1.5,
     dict(x_mean=0.0001, x_wc_min=-0.0001, sigma=4.714045208e-05, cpk=-1.414213562, dpmo=999988.9548), False),
    ("p5 zero tolerances", [R("adds", 10, 0, 0), R("subtracts", 9, 0, 0)], 0.5, 2.0, 3, 1.5,
     dict(x_mean=0.001, t_wc=0.0, sigma=0.0), False),
    ("p6 mixed in/mm", [R("adds", 50.8, .0508, -.0508), R("subtracts", 30, .05, -.05), R("subtracts", 25.4, .0254, -.0254),
                        R("adds", 10, .02, -.02), R("subtracts", 5, .01, -.01)], 5, 20, 3, 1.5,
     dict(x_mean=0.0004, t_wc=0.0001562, t_rss=7.890373882e-05, sigma=2.630124627e-05, cp=95.05252998,
          cpk=-58.29888506, dpmo=1000000.0), False),
    ("p8 nominal 9e5 mm", [R("adds", 900000, .5, -.5), R("subtracts", 899990, .5, -.5)], 5, 15, 3, 1.5,
     dict(x_mean=0.01, t_wc=0.001, t_rss=0.0007071067812, sigma=0.0002357022604, cp=7.071067812, cpk=7.071067812), False),
    ("p16 max only z=6", BASE4, None, 0.54, 3, 1.5, dict(cpk=2.0, dpmo=0.0009865875894), False),
    ("p9 upper z=7 (finding)", BASE4, -5, 0.5 + 7 * 0.0066666667, 3, 1.5, dict(cpk=2.333333345, dpmo=1.279865103e-06), True),
    ("p10 upper z=8 (finding)", BASE4, -5, 0.5 + 8 * 0.0066666667, 3, 1.5, dict(cpk=2.66666668, dpmo=6.661338148e-10), True),
    ("p10b upper z=8.6 (finding)", BASE4, -5, 0.5 + 8.6 * 0.0066666667, 3, 1.5, dict(cpk=2.866666681, dpmo=0.0), True),
]
for label, rows, lsl, usl, k, f, prod, is_finding in PROBES:
    res = stack(rows, lsl, usl, k, f)
    for sym, pv in prod.items():
        pv = pv * 1000.0 if sym in LEN else pv
        EXTRAS.append((label, "", sym, pv, res[sym] if sym in res else 0.0, 1e-6, is_finding))


def main():
    fails = 0
    print("== Known-answer cases ==")
    print(f"{'case':18} {'symbol':9} {'KAT expected':>14} {'independent':>14} {'rel diff':>10} {'KAT tol':>8} result")
    for case, res, exp in KATS:
        for sym, e, tol in exp:
            v = res[sym]
            rd = abs(v - e) / abs(e) if e != 0 else abs(v)
            ok = rd <= max(tol, 1e-6)
            fails += not ok
            print(f"{case:18} {sym:9} {e:14.8g} {v:14.8g} {rd:10.2e} {tol:8.0e} {'PASS' if ok else 'FAIL'}")
    print("== Extra checks (product values hard-coded from the probe tool) ==")
    for label, ins, sym, prod, ind, tol, finding in EXTRAS:
        rd = abs(prod - ind) / abs(ind) if ind != 0 else abs(prod)
        ok = rd <= tol
        if not ok and finding:
            tag = "FINDING (does not change exit code)"
        else:
            tag = "PASS" if ok else "FAIL"
            fails += not ok
        print(f"{label:26} {sym:9} product={prod:<14.8g} indep={ind:<14.8g} rd={rd:9.2e} {tag}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
