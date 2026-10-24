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
    for label, ins, sym, prod, ind, tol in EXTRAS:
        rd = abs(prod - ind) / abs(ind) if ind != 0 else abs(prod)
        ok = rd <= tol
        fails += not ok
        print(f"{label:22} {sym:9} product={prod:<14.8g} indep={ind:<14.8g} rd={rd:9.2e} {'PASS' if ok else 'FAIL'}")
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
