#!/usr/bin/env python3
"""Independent re-derivation of fp.oring-gland (O-ring and seal gland: squeeze, fill, stretch, extrusion gap).

Written from the spec and first principles, not translated from the C# code.

Relations (lengths in mm, ratios as fractions):
  ring factor   f = (1 + a (T-20)) (1 + s)^(1/3)       thermal expansion times cube root of volume swell
  metal factor  g = 1 + a_m (T-20)                     (sealing-surface part: g_s from a_s, else g_m)
  stretch       X = g D_ref / (f d) - 1                D_ref = groove root D_g (piston, face) or rod D_s (rod seal)
                internal-pressure face seal: the ring OD (d + 2 W) sits on the groove wall D_g: X = g D_g/(f OD) - 1
  OD compression c = max(0, 1 - g D_g / (f (d + 2W)))  only where the OD is held (rod seal, internal face seal)
  cross-section W_eff = f W (1 - r(X)) / sqrt(1 - c)
  depth         piston (g_s D_s - g_m D_g)/2, rod (g_m D_g - g_s D_s)/2, face g_m h_f
  squeeze       q = 1 - h / W_eff
  groove area   A = (g b)(depth) - 2 (1 - pi/4) (g r_g)^2
  fill, hot     (pi/4)(f W_max)^2 * max(f/g, 1/(1-c)) / A_min ;  fill, cold (pi/4) W_eff,min^2 / A_max
  reduction r   none | 1 - 1/sqrt(1+X) | Parker 3.5 quadratic (held above 25 %, linear blend to 0 below 3 %) | nu X
  gap           piston g_s D_s - g_m D_c ; rod g_m D_c - g_s D_s ; face e_f ; largest over installed, cold, hot, + dD_p
Worst case: each dimension is taken at the end that is worst for the quantity, ends of one dimension independent.
Monte Carlo: dimensions drawn in the order W, d, D_s, D_g, D_c, h_f, b, e_f from normal / uniform / triangular laws.
The sample fractions use a port of the sampler the engine documents (xoshiro256** seeded by SplitMix64, polar-method
normal), so the seeded counts match exactly. C_pk uses the secant-slope linearisation, sigma by root-sum-square.
Extra-probe rows print SI values for the product (fractions, metres) and fractions for the model; gap rows are in mm.
Exit code 0 means every KAT value and every extra-probe value agrees within max(tolerance, 1e-6).
"""
import json
import math
import sys

import numpy as np

PI4 = math.pi / 4.0
rows = []
fails = 0


# ---------------------------------------------------------------- model
def red_fn(kind, nu=0.49):
    """Cross-section reduction r(X); works on floats and numpy arrays."""
    def r(X):
        X = np.asarray(X, dtype=float)
        if kind == "none":
            out = np.zeros_like(X)
        elif kind == "incomp":
            out = 1.0 - 1.0 / np.sqrt(1.0 + X)
        elif kind == "linear":
            out = nu * X
        elif kind == "parker":
            Xp = np.minimum(100.0 * X, 25.0)
            y = lambda x: 0.56 + 0.59 * x - 0.0046 * x * x
            Y = np.where(Xp >= 3.0, y(Xp), y(3.0) * Xp / 3.0)
            out = np.where(X <= 0, 0.0, Y / 100.0)
        else:
            raise ValueError(kind)
        return float(out) if out.ndim == 0 else out
    return r


DEF = dict(
    type="piston", duty="static", face_p="ext",
    d=50.0, d_tol=0.4, W=3.5, W_tol=0.1,
    D_s=(56.9, 0.046, 0.0), D_g=(51.5, 0.0, -0.05), D_c=(56.9, -0.03, -0.06),
    h_f=(2.7, 0.05, 0.0), e_f=(0.05, 0.05, 0.0), b=(4.8, 0.1, 0.0), r_g=0.2,
    T_min=-20.0, T_max=80.0, alpha=1.5e-4, alpha_m=1.2e-5, alpha_s=None, s_min=0.0, s_max=5.0,
    red="incomp", nu=0.49,
    q_lo_s=15.0, q_hi_s=30.0, q_lo_d=10.0, q_hi_d=25.0, fill_lo=60.0, fill_hi=85.0, x_lo=0.0, x_hi=6.0, od_hi=6.0,
    H_A=70.0, p_op=160.0, dD_p=0.0, gap_allow=None, ext=None,
    dist="normal", k_sigma=3.0, measured=None,
)


def P(**kw):
    p = dict(DEF)
    p.update(kw)
    return p


def dims_band(p):
    """Return {name: (lo, hi)} for every dimension."""
    out = {"W": (p["W"] - p["W_tol"], p["W"] + p["W_tol"]), "d": (p["d"] - p["d_tol"], p["d"] + p["d_tol"])}
    for n in ("D_s", "D_g", "D_c", "h_f", "e_f", "b"):
        nom, u, l = p[n]
        out[n] = (nom + l, nom + u)
    for n, mean, sig, dist in (p["measured"] or []):       # a measured row replaces the toleranced band
        half = {"normal": p["k_sigma"], "uniform": math.sqrt(3), "triangular": math.sqrt(6)}[dist] * sig
        out[n] = (mean - half, mean + half)
    return out


def factors(p, T, s):
    a_s = p["alpha_s"] if p["alpha_s"] is not None else p["alpha_m"]
    f = (1 + p["alpha"] * (T - 20)) * (1 + s / 100.0) ** (1.0 / 3.0)
    return f, 1 + p["alpha_m"] * (T - 20), 1 + a_s * (T - 20)


def evaluate(p, dim, T, s):
    """Exact model for one set of dimensions (dict name -> value) at temperature T and swell s (per cent)."""
    f, gm, gs = factors(p, T, s)
    r = red_fn(p["red"], p["nu"])
    typ = p["type"]
    d, W, b = dim["d"], dim["W"], dim["b"]
    OD = d + 2 * W
    gl = {}
    if typ == "piston":
        X = gm * dim["D_g"] / (f * d) - 1
        c = 0.0
        h = (gs * dim["D_s"] - gm * dim["D_g"]) / 2
        gap = gs * dim["D_s"] - gm * dim["D_c"]
    elif typ == "rod":
        X = gs * dim["D_s"] / (f * d) - 1
        c = np.maximum(0.0, 1 - gm * dim["D_g"] / (f * OD))
        h = (gm * dim["D_g"] - gs * dim["D_s"]) / 2
        gap = gm * dim["D_c"] - gs * dim["D_s"]
    else:
        h = gm * dim["h_f"]
        gap = dim["e_f"]
        if p["face_p"] == "ext":
            X = gm * dim["D_g"] / (f * d) - 1
            c = 0.0
        else:
            X = gm * dim["D_g"] / (f * OD) - 1
            c = np.maximum(0.0, 1 - gm * dim["D_g"] / (f * OD))
    # internal-pressure face seal: the OD compression c already is the thickening of a ring that is not stretched
    rX = r(np.maximum(X, 0.0)) if (typ == "face" and p["face_p"] == "int") else r(X)
    Weff = f * W * (1 - rX) / np.sqrt(1 - c)
    q = 1 - h / Weff
    A = (gm * b) * h - 2 * (1 - math.pi / 4) * (gm * p["r_g"]) ** 2
    fill_cold = PI4 * Weff ** 2 / A
    gref = gs if typ == "rod" else gm            # the held circumference follows the part the ring ID sits on
    K = np.maximum(f / gref, 1 / (1 - c))
    fill_hot = PI4 * (f * W) ** 2 * K / A
    return dict(f=f, gm=gm, gs=gs, X=X, c=c, h=h, Weff=Weff, q=q, A=A, fill=fill_cold, fill_hot=fill_hot, gap=gap,
                od_comp=c)


def mid_dims(p):
    return {k: 0.5 * (lo + hi) for k, (lo, hi) in dims_band(p).items()}


def worst_case(p):
    """Worst-case outputs (fractions, mm) from independent end choices."""
    B = dims_band(p)
    typ = p["type"]
    r = red_fn(p["red"], p["nu"])
    out = {}
    cold = (p["T_min"], p["s_min"])
    hot = (p["T_max"], p["s_max"])
    inst = (20.0, 0.0)

    def fac(Ts):
        return factors(p, *Ts)

    def depth(Ts, which):          # which: 'max' or 'min' ; thermal depth of one end
        f, gm, gs = fac(Ts)
        if typ == "piston":
            hi = (gs * B["D_s"][1] - gm * B["D_g"][0]) / 2
            lo = (gs * B["D_s"][0] - gm * B["D_g"][1]) / 2
        elif typ == "rod":
            hi = (gm * B["D_g"][1] - gs * B["D_s"][0]) / 2
            lo = (gm * B["D_g"][0] - gs * B["D_s"][1]) / 2
        else:
            hi, lo = gm * B["h_f"][1], gm * B["h_f"][0]
        return hi if which == "max" else lo

    def ref(Ts, which):           # stretch ratio D_ref/(d) per end: which 'max' gives largest X
        f, gm, gs = fac(Ts)
        if typ == "piston" or (typ == "face" and p["face_p"] == "ext"):
            num = gm * (B["D_g"][1] if which == "max" else B["D_g"][0])
            den = f * (B["d"][0] if which == "max" else B["d"][1])
        elif typ == "rod":
            num = gs * (B["D_s"][1] if which == "max" else B["D_s"][0])
            den = f * (B["d"][0] if which == "max" else B["d"][1])
        else:  # internal face: ring OD on groove wall
            ODmin = B["d"][0] + 2 * B["W"][0]
            ODmax = B["d"][1] + 2 * B["W"][1]
            num = gm * (B["D_g"][1] if which == "max" else B["D_g"][0])
            den = f * (ODmin if which == "max" else ODmax)
        return num / den - 1

    holds = typ == "rod" or (typ == "face" and p["face_p"] == "int")

    def cmp_(Ts, which):          # OD compression; 'max' = largest, 'min' = smallest (not below 0)
        if not holds:
            return 0.0
        f, gm, gs = fac(Ts)
        if which == "max":
            return max(0.0, 1 - gm * B["D_g"][0] / (f * (B["d"][1] + 2 * B["W"][1])))
        return max(0.0, 1 - gm * B["D_g"][1] / (f * (B["d"][0] + 2 * B["W"][0])))

    rr = (lambda X: r(max(X, 0.0))) if (typ == "face" and p["face_p"] == "int") else r

    def Weff(Ts, which):          # smallest or largest effective section
        f, gm, gs = fac(Ts)
        if which == "min":
            X = ref(Ts, "max")
            return f * B["W"][0] * (1 - rr(X)) / math.sqrt(1 - cmp_(Ts, "min"))
        X = ref(Ts, "min")
        return f * B["W"][1] * (1 - rr(X)) / math.sqrt(1 - cmp_(Ts, "max"))

    def area(Ts, which):          # groove area, 'min' or 'max'
        f, gm, gs = fac(Ts)
        bb = B["b"][0] if which == "min" else B["b"][1]
        hh = depth(Ts, "min" if which == "min" else "max")
        return (gm * bb) * hh - 2 * (1 - math.pi / 4) * (gm * p["r_g"]) ** 2

    out["squeeze_min"] = 1 - depth(cold, "max") / Weff(cold, "min")
    out["squeeze_max"] = 1 - depth(hot, "min") / Weff(hot, "max")
    f, gm, gs = fac(hot)
    gref = gs if typ == "rod" else gm
    Kh = max(f / gref, 1 / (1 - cmp_(hot, "max")))
    out["fill_max"] = PI4 * (f * B["W"][1]) ** 2 * Kh / area(hot, "min")
    out["fill_min"] = PI4 * Weff(cold, "min") ** 2 / area(cold, "max")
    out["stretch"] = ref(inst, "max")
    out["stretch_min"] = ref(inst, "min")
    out["X_c"] = ref(cold, "max")
    ODmax_, ODmin_ = B["d"][1] + 2 * B["W"][1], B["d"][0] + 2 * B["W"][0]
    out["od_comp"] = (ODmax_ - B["D_g"][0]) / ODmax_ if holds else 0.0      # signed: negative means a gap
    out["c_h"] = cmp_(hot, "max")
    # nominal (mean dimensions, installed)
    nd = mid_dims(p)
    ev = evaluate(p, nd, 20.0, 0.0)
    out["squeeze_nom"] = ev["q"]
    out["stretch_nom"] = ev["X"]
    out["red_nom"] = rr(ev["X"])
    ODm_ = nd["d"] + 2 * nd["W"]
    out["od_comp_nom"] = (ODm_ - nd["D_g"]) / ODm_ if holds else 0.0
    # gap
    gaps_hi, gaps_lo = [], []
    for Ts in (inst, cold, hot):
        f, gm, gs = fac(Ts)
        if typ == "piston":
            gaps_hi.append(gs * B["D_s"][1] - gm * B["D_c"][0])
            gaps_lo.append(gs * B["D_s"][0] - gm * B["D_c"][1])
        elif typ == "rod":
            gaps_hi.append(gm * B["D_c"][1] - gs * B["D_s"][0])
            gaps_lo.append(gm * B["D_c"][0] - gs * B["D_s"][1])
        else:
            gaps_hi.append(B["e_f"][1])
            gaps_lo.append(B["e_f"][0])
    dDp = p["dD_p"] if typ != "face" else 0.0
    out["gap_max"] = max(gaps_hi) + dDp
    out["gap_min"] = min(gaps_lo)
    return out


def extrusion_allow(p):
    """Permitted gap: min of gap_allow and the record interpolated in hardness and pressure."""
    vals = []
    if p["gap_allow"] is not None:
        vals.append(p["gap_allow"])
    ext = p["ext"]
    if ext:
        Hs = sorted({h for h, _, _ in ext})
        curves = {h: sorted((pp, g) for hh, pp, g in ext if hh == h) for h in Hs}

        def at(h, pr):
            c = curves[h]
            if pr <= c[0][0]:
                return c[0][1]
            for (p0, g0), (p1, g1) in zip(c, c[1:]):
                if pr <= p1:
                    return g0 + (g1 - g0) * (pr - p0) / (p1 - p0)
            return c[-1][1]
        H = p["H_A"]
        if H >= Hs[-1]:
            v = at(Hs[-1], p["p_op"])
        elif H <= Hs[0]:
            v = at(Hs[0], p["p_op"])
        else:
            for h0, h1 in zip(Hs, Hs[1:]):
                if H <= h1:
                    g0, g1 = at(h0, p["p_op"]), at(h1, p["p_op"])
                    v = g0 + (g1 - g0) * (H - h0) / (h1 - h0)
                    break
        vals.append(v)
    return min(vals) if vals else None


# ---------------------------------------------------------------- output helpers
def check(case, sym, expected, indep, tol):
    global fails
    t = max(tol, 1e-6)
    rel = abs(indep - expected) / abs(expected) if expected != 0 else abs(indep - expected)
    ok = rel <= t
    if not ok:
        fails += 1
    print(f"{case:30s} {sym:12s} {expected:16.10g} {indep:16.10g} {rel:10.2e} {tol:8.1e} {'PASS' if ok else 'FAIL'}")


PCT = {"squeeze_min", "squeeze_max", "squeeze_nom", "fill_min", "fill_max", "stretch", "stretch_min", "stretch_nom",
       "X_c", "od_comp", "od_comp_nom", "c_h", "red_nom"}


def kat(case, p, exp, tol=1e-9):
    wc = worst_case(p)
    allow = extrusion_allow(p)
    wc["gap_allow"] = allow
    if allow:
        wc["u_gap"] = wc["gap_max"] / allow
    wc["u_stretch"] = wc["stretch"] / (p["x_hi"] / 100.0)
    for sym, val in exp.items():
        v = wc[sym] * (100.0 if sym in PCT else 1.0)
        check(case, sym, val, v, tol)


# ---------------------------------------------------------------- Monte Carlo and capability
def used_dims(p):
    if p["type"] == "face":
        return ["W", "d", "D_g", "h_f", "b", "e_f"]
    return ["W", "d", "D_s", "D_g", "D_c", "b"]


def dim_stats(p):
    """name -> (mean, sigma, half, dist) for each dimension used."""
    B = dims_band(p)
    out = {}
    meas = {m[0]: m for m in (p["measured"] or [])}
    for n in used_dims(p):
        lo, hi = B[n]
        mean, half = 0.5 * (lo + hi), 0.5 * (hi - lo)
        dist = p["dist"]
        if n in meas:
            _, mean, sig, dist = meas[n]
            half = {"normal": p["k_sigma"], "uniform": math.sqrt(3), "triangular": math.sqrt(6)}[dist] * sig
        else:
            sig = {"normal": half / p["k_sigma"], "uniform": half / math.sqrt(3), "triangular": half / math.sqrt(6)}[dist]
        out[n] = (mean, sig, half, dist)
    return out


def sample(p, N, rng):
    st = dim_stats(p)
    dims = dict(mid_dims(p))
    for n, (mean, sig, half, dist) in st.items():
        if dist == "normal":
            dims[n] = rng.normal(mean, sig, N)
        elif dist == "uniform":
            dims[n] = rng.uniform(mean - half, mean + half, N)
        else:
            dims[n] = rng.triangular(mean - half, mean, mean + half, N)
    return dims


M64 = (1 << 64) - 1


class Xoshiro:
    """xoshiro256** seeded through SplitMix64, with the polar-method normal: the sampler the product's engine documents."""

    def __init__(self, seed):
        x = seed & M64
        st = []
        for _ in range(4):
            x = (x + 0x9E3779B97F4A7C15) & M64
            z = x
            z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & M64
            z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & M64
            st.append(z ^ (z >> 31))
        self.s = st
        self.spare = None

    @staticmethod
    def rotl(x, k):
        return ((x << k) | (x >> (64 - k))) & M64

    def raw(self):
        s0, s1, s2, s3 = self.s
        result = (self.rotl((s1 * 5) & M64, 7) * 9) & M64
        t = (s1 << 17) & M64
        s2 ^= s0
        s3 ^= s1
        s1 ^= s2
        s0 ^= s3
        s2 ^= t
        s3 = self.rotl(s3, 45)
        self.s = [s0, s1, s2, s3]
        return result

    def u01(self):
        return (self.raw() >> 11) * (1.0 / 9007199254740992.0)

    def normal(self):
        if self.spare is not None:
            v, self.spare = self.spare, None
            return v
        while True:
            u = 2.0 * self.u01() - 1.0
            v = 2.0 * self.u01() - 1.0
            q = u * u + v * v
            if q < 1.0 and q != 0.0:
                break
        m = math.sqrt(-2.0 * math.log(q) / q)
        self.spare = v * m
        return u * m


def sample_exact(p, N, seed):
    """Reproduce the engine's sample set: dimensions drawn in order W, d, D_s, D_g, D_c, h_f, b, e_f."""
    order = ["W", "d", "D_s", "D_g", "D_c", "h_f", "b", "e_f"]
    st = dim_stats(p)
    names = [n for n in order if n in st and st[n][2] > 0 and st[n][1] > 0]
    rng = Xoshiro(seed)
    cols = {n: np.empty(N) for n in names}
    for i in range(N):
        for n in names:
            mean, sig, half, dist = st[n]
            if dist == "normal":
                v = mean + sig * rng.normal()
            elif dist == "uniform":
                v = (mean - half) + 2 * half * rng.u01()
            else:
                u = rng.u01()
                lo, hi = mean - half, mean + half
                rngw = hi - lo
                fc = (mean - lo) / rngw
                v = lo + math.sqrt(u * rngw * (mean - lo)) if u < fc else hi - math.sqrt((1 - u) * rngw * (hi - mean))
            cols[n][i] = v
    dims = dict(mid_dims(p))
    dims.update(cols)
    return dims


def fractions_exact(p, N, seed):
    dim = sample_exact(p, N, seed)
    cr = criteria(p, dim)
    lim = limits(p)
    out = {}
    for k, v in cr.items():
        v = np.broadcast_to(np.asarray(v, dtype=float), (N,))
        lo, hi = lim[k]
        n = 0
        if lo is not None:
            n += int((v < lo).sum())
        if hi is not None:
            n += int((v > hi).sum())
        out[k] = n / N
    return out


def criteria(p, dim):
    """Exact criteria for one dimension set (arrays allowed): values in fractions / mm."""
    cold = evaluate(p, dim, p["T_min"], p["s_min"])
    hot = evaluate(p, dim, p["T_max"], p["s_max"])
    inst = evaluate(p, dim, 20.0, 0.0)
    gaps = [inst["gap"], cold["gap"], hot["gap"]]
    gapmax = np.maximum(np.maximum(gaps[0], gaps[1]), gaps[2]) + (p["dD_p"] if p["type"] != "face" else 0.0)
    out = dict(sq_min=cold["q"], sq_max=hot["q"], fill_max=hot["fill_hot"], fill_min=cold["fill"],
               stretch=inst["X"], gap=gapmax)
    if p["type"] == "rod" or (p["type"] == "face" and p["face_p"] == "int"):
        OD = dim["d"] + 2 * dim["W"]
        out["od"] = (OD - dim["D_g"]) / OD
    return out


def limits(p):
    ql, qh = (p["q_lo_s"], p["q_hi_s"]) if p["duty"] == "static" else (p["q_lo_d"], p["q_hi_d"])
    return dict(sq_min=(ql / 100, None), sq_max=(None, qh / 100), fill_max=(None, p["fill_hi"] / 100),
                fill_min=(p["fill_lo"] / 100, None),
                stretch=(None if (p["type"] == "face" and p["face_p"] == "int") else p["x_lo"] / 100, p["x_hi"] / 100),
                gap=(None, extrusion_allow(p)), od=(None, p["od_hi"] / 100))


def fractions(p, N=2_000_000, seed=12345):
    rng = np.random.default_rng(seed)
    dim = sample(p, N, rng)
    cr = criteria(p, dim)
    lim = limits(p)
    out = {}
    for k, v in cr.items():
        lo, hi = lim[k]
        bad = np.zeros(N, dtype=bool)
        if lo is not None:
            bad |= v < lo
        if hi is not None:
            bad |= v > hi
        out[k] = bad.mean()
    return out


def cpk(p):
    """C_pk per criterion from the first-order stack: secant slope across each band, sigma = root-sum-square."""
    st = dim_stats(p)
    mean = dict(mid_dims(p))
    for n, (m, sg, half, dist) in st.items():
        mean[n] = m
    lim = limits(p)
    F0 = criteria(p, mean)
    out = {}
    for k in F0:
        var = 0.0
        for n, (m, sg, half, dist) in st.items():
            if half == 0:
                continue
            up, dn = dict(mean), dict(mean)
            up[n], dn[n] = m + half, m - half
            a = (float(criteria(p, up)[k]) - float(criteria(p, dn)[k])) / (2 * half)
            var += (a * sg) ** 2
        sig = math.sqrt(var)
        if sig == 0.0:                       # no spread: the product prints no C_pk
            out[k] = None
            continue
        lo, hi = lim[k]
        m0 = float(F0[k])
        c = []
        if lo is not None:
            c.append((m0 - lo) / (3 * sig))
        if hi is not None:
            c.append((hi - m0) / (3 * sig))
        out[k] = min(c) if c else None
    return out


# ---------------------------------------------------------------- known-answer cases (inputs from the template)
def run_kats():
    print(f"{'case':30s} {'symbol':12s} {'KAT expected':>16s} {'independent':>16s} {'rel diff':>10s} {'tol':>8s} result")
    kat("ka-thermal-swell", P(gap_allow=0.15), dict(
        squeeze_min=16.9560030299, squeeze_max=26.9574115556, fill_max=84.6429192000, fill_min=63.9511150431,
        squeeze_nom=21.0318008103, red_nom=1.4431477137, stretch=3.8306451613, X_c=4.4072499513, gap_max=0.10607632))
    kat("ka-worst-case-hand", P(T_min=20, T_max=20, s_min=0, s_max=0, red="none", gap_allow=0.15), dict(
        squeeze_min=19.1764705882, squeeze_max=25.0, fill_max=78.6439962535, fill_min=67.5132455669,
        stretch=3.8306451613, stretch_min=2.0833333333, squeeze_nom=22.1714285714, gap_max=0.106, gap_min=0.03))
    kat("ka-rod-seal", P(type="rod", d=39.5, d_tol=0.35, W=3.0, W_tol=0.1,
                         D_s=(40, -0.025, -0.05), D_g=(44.8, 0.062, 0), D_c=(40, 0.039, 0), b=(4.1, 0.1, 0)), dict(
        squeeze_min=13.7088267470, squeeze_max=26.8202916965, fill_max=84.5655009226, fill_min=61.7816677682,
        stretch=2.1072796935, stretch_nom=1.1708860759, od_comp=2.7144408252, od_comp_nom=1.4703296703,
        squeeze_nom=18.9869065887, c_h=5.06929787, gap_max=0.08906408), tol=1e-9)
    face = dict(type="face", d=60, d_tol=0.5, W=3.5, W_tol=0.1, D_g=(60.8, 0.05, 0), h_f=(2.75, 0.05, 0),
                e_f=(0.05, 0.05, 0), b=(4.9, 0.1, 0))
    kat("ka-face-seal", P(**face), dict(
        squeeze_min=16.0233435665, squeeze_max=26.1854858169, fill_max=81.4038310717, fill_min=62.4444221122,
        stretch=2.2689075630, gap_max=0.1))
    facei = dict(face, face_p="int", D_g=(66.7, 0.05, 0))
    kat("ka-face-seal-internal", P(**facei), dict(
        stretch=0.6787330317, stretch_min=-1.4771048744, stretch_nom=-0.4104477612, od_comp=1.4771048744,
        od_comp_nom=0.4104477612, c_h=3.8619124, squeeze_min=16.6787759509, squeeze_max=26.9136545944,
        fill_min=63.4307031934, fill_max=82.6242783141, squeeze_nom=20.8771662406, X_c=1.2378342453))
    kat("ka-gap-over-temperature", P(alpha_s=2.3e-5, dD_p=0.02, gap_allow=0.2), dict(
        gap_max=0.16366068, gap_min=0.0049496, u_gap=0.8183034, squeeze_min=17.3347820634, squeeze_max=26.4498064079,
        fill_min=64.2445202775, fill_max=84.0579883229))
    base = dict(d_tol=0.0, D_s=(57, 0.046, 0), D_g=(52, 0, 0))
    kat("ka-incompressible-reduction", P(**dict(base, D_s=(57, 0.046, 0)), red="incomp"), dict(
        red_nom=1.9419324309, stretch_nom=4.0, squeeze_nom=26.8217856664))
    kat("ka-linear-reduction", P(**base, red="linear", nu=0.49), dict(red_nom=1.96))
    ext = [(70, 0, 0.30), (70, 100, 0.15), (70, 200, 0.08), (90, 0, 0.45), (90, 100, 0.25), (90, 200, 0.15)]
    # seeded Monte Carlo cases: fractions from a port of the engine's documented sampler, C_pk from the secant stack
    pm = P(red="parker", q_lo_s=18, q_hi_s=26, fill_lo=65, fill_hi=80, x_lo=2.5, x_hi=3.5, gap_allow=0.09)
    fr = fractions_exact(pm, 20000, 7)
    ck = cpk(pm)
    for sym, key, val in (("p_sq_min", "sq_min", 2.08), ("p_sq_max", "sq_max", 0.115), ("p_fill_max", "fill_max", 15.995),
                          ("p_fill_min", "fill_min", 0.485), ("p_stretch", "stretch", 7.25), ("p_gap", "gap", 0.77)):
        check("ka-monte-carlo-seeded", sym, val, 100 * fr[key], 1e-9)
    for sym, key, val in (("cpk_sq_min", "sq_min", 0.699319526), ("cpk_sq_max", "sq_max", 0.979671603),
                          ("cpk_fill_max", "fill_max", 0.331310178), ("cpk_fill_min", "fill_min", 0.849060611),
                          ("cpk_stretch", "stretch", 0.545342867), ("cpk_gap", "gap", 0.798834566)):
        check("ka-monte-carlo-seeded", sym, val, ck[key], 1e-7)
    meas = [("D_g", 51.48, 0.012, "normal"), ("W", 3.505, 0.03, "uniform")]
    pmm = P(dist="triangular", measured=meas, q_lo_s=18, q_hi_s=26, fill_lo=65, fill_hi=80, x_lo=2.5, x_hi=3.5, gap_allow=0.09)
    fr = fractions_exact(pmm, 10000, 11)
    ck = cpk(pmm)
    for sym, key, val in (("p_fill_max", "fill_max", 24.76), ("p_stretch", "stretch", 16.13), ("p_gap", "gap", 2.29)):
        check("ka-monte-carlo-measured", sym, val, 100 * fr[key], 1e-9)
    for sym, key, val in (("cpk_sq_min", "sq_min", 1.174984132), ("cpk_sq_max", "sq_max", 0.934797809),
                          ("cpk_fill_max", "fill_max", 0.289602138), ("cpk_stretch", "stretch", 0.454802341),
                          ("cpk_gap", "gap", 0.652245692)):
        check("ka-monte-carlo-measured", sym, val, ck[key], 1e-7)
    mcp = dict(q_lo_s=18, q_hi_s=26, fill_lo=65, fill_hi=80, x_lo=2.5, x_hi=3.5, gap_allow=0.09)
    kat("ka-monte-carlo-measured", P(dist="triangular", measured=meas, **mcp), dict(
        squeeze_min=18.1295724925, squeeze_max=26.2969508948, fill_max=82.8770696497, fill_min=65.8694064569,
        stretch=3.8629032258))
    kat("ka-extrusion-record", P(ext=ext, H_A=80, p_op=150), dict(gap_allow=0.1575, u_gap=0.10607632 / 0.1575))
    kat("ka-parker-quadratic", P(**base, red="parker"), dict(red_nom=2.8464, squeeze_nom=26.1405209306))
    kat("ka-stretch-rule", P(d_tol=0.0, D_s=(57.6, 0.046, 0), D_g=(52.9, 0, 0), D_c=(57.5, -0.03, -0.06)),
        dict(stretch=5.8, u_stretch=0.9666666667))


# ---------------------------------------------------------------- extra probes: product values observed with the probe tool
# One JSON object per line: harness-style inputs ("inp") and the product outputs in SI ("prod": fractions, metres).
# The product values were read from `Probe run fp.oring-gland` on release/v0.26.0; they are hard-coded here.
EXTRA_LINES = r"""
{"name":"p01-piston-dynamic","inp":{"d_r":34.65,"d_r_tol":0.3,"W":3.53,"W_tol":0.08,"duty":"dynamic","D_s":41.3,"D_s_u":0.039,"D_s_l":0,"D_g":35.6,"D_g_u":0,"D_g_l":-0.062,"D_c":41.2,"D_c_u":-0.05,"D_c_l":-0.1,"b":4.8,"b_u":0.2,"b_l":0,"T_min":-30,"T_max":100,"alpha":0.0002,"s_max":10,"s_min":-1,"gap_allow":0.12,"n_mc":2000},"prod":{"squeeze_nom":0.1747507943,"squeeze_min":0.1275229107,"squeeze_max":0.2577525075,"fill_min":0.599237673,"fill_max":0.8617617278,"stretch_nom":0.02652236652,"stretch":0.03639010189,"stretch_min":0.01682403433,"X_c":0.04974144073,"u_stretch":0.6065016982,"red_nom":0.01300309288,"gap_min":0.00014991,"gap_max":0.00023922944,"gap_allow":0.00012,"u_gap":1.993578667,"p_sq_min":0.0,"cpk_sq_min":2.928023145,"p_sq_max":0.0005,"cpk_sq_max":1.059098247,"p_fill_max":0.0005,"cpk_fill_max":1.245273048,"p_fill_min":0.0,"cpk_fill_min":1.570554547,"p_stretch":0.0,"cpk_stretch":2.968959686,"p_gap":1.0,"cpk_gap":-2.353366806}}
{"name":"p02-rod-highp-record","inp":{"type":"rod","d_r":39.5,"d_r_tol":0.35,"W":3.0,"W_tol":0.1,"D_s":40,"D_s_u":-0.025,"D_s_l":-0.05,"D_g":44.8,"D_g_u":0.062,"D_g_l":0,"D_c":40,"D_c_u":0.2,"D_c_l":0.0,"b":4.1,"b_u":0.1,"b_l":0,"red":"parker","H_A":90,"p_op":400,"ext":[[70,0,0.3],[70,100,0.15],[70,200,0.08],[90,0,0.45],[90,100,0.25],[90,200,0.15]],"dD_p":0.05,"n_mc":2000},"prod":{"squeeze_nom":0.1873114961,"squeeze_min":0.1306646587,"squeeze_max":0.2601097774,"fill_min":0.608720187,"fill_max":0.8456550092,"stretch_nom":0.01170886076,"stretch":0.02107279693,"stretch_min":0.002509410289,"X_c":0.02674314084,"u_stretch":0.3512132822,"red_nom":0.008932299578,"od_comp_nom":0.0147032967,"od_comp":0.02714440825,"c_h":0.05069297866,"gap_min":2.4988e-05,"gap_max":0.00030018,"gap_allow":0.00015,"u_gap":2.0012,"p_sq_min":0.0055,"cpk_sq_min":0.8658237161,"p_sq_max":0.0,"cpk_sq_max":2.695308499,"p_fill_max":0.0,"cpk_fill_max":1.512066827,"p_fill_min":0.0,"cpk_fill_min":1.577472773,"p_stretch":0.0005,"cpk_stretch":1.305219794,"p_od":0.0,"cpk_od":5.173020451,"p_gap":0.875,"cpk_gap":-0.3728181298}}
{"name":"p03-face-ext-dynamic","inp":{"type":"face","duty":"dynamic","d_r":60,"d_r_tol":0.5,"W":3.5,"W_tol":0.1,"D_g":60.8,"D_g_u":0.05,"D_g_l":0,"h_f":2.75,"h_f_u":0.05,"h_f_l":0,"e_f":0.05,"e_f_u":0.05,"e_f_l":0,"b":4.9,"b_u":0.1,"b_l":0,"T_max":120,"red":"parker","n_mc":1000},"prod":{"squeeze_nom":0.1987380871,"squeeze_min":0.1535796261,"squeeze_max":0.2586523895,"fill_min":0.6146651518,"fill_max":0.8274555784,"stretch_nom":0.01375,"stretch":0.02268907563,"stretch_min":0.004958677686,"X_c":0.02836839525,"u_stretch":0.3781512605,"red_nom":0.01048941667,"gap_min":5e-05,"gap_max":0.0001,"p_sq_min":0.0,"cpk_sq_min":3.654506913,"p_sq_max":0.005,"cpk_sq_max":0.8436116031,"p_fill_max":0.0,"cpk_fill_max":1.836955937,"p_fill_min":0.0,"cpk_fill_min":1.806929296,"p_stretch":0.0,"cpk_stretch":1.6255315}}
{"name":"p04-face-int-swell25","inp":{"type":"face","face_p":"int","d_r":60,"d_r_tol":0.5,"W":3.5,"W_tol":0.1,"D_g":66.7,"D_g_u":0.05,"D_g_l":0,"h_f":2.75,"h_f_u":0.05,"h_f_l":0,"e_f":0.05,"e_f_u":0.05,"e_f_l":0,"b":4.9,"b_u":0.1,"b_l":0,"s_max":25,"T_max":150,"n_mc":1000,"fill_hi":95},"prod":{"squeeze_nom":0.2087716624,"squeeze_min":0.1667877595,"squeeze_max":0.3396436152,"fill_min":0.6343070319,"fill_max":1.012099963,"stretch_nom":-0.004104477612,"stretch":0.006787330317,"stretch_min":-0.01477104874,"X_c":0.01237834245,"u_stretch":0.1131221719,"red_nom":0.0,"od_comp_nom":0.004104477612,"od_comp":0.01477104874,"c_h":0.101488647,"gap_min":5e-05,"gap_max":0.0001,"p_sq_min":0.0,"cpk_sq_min":2.06741499,"p_sq_max":0.938,"cpk_sq_max":-0.5001488896,"p_fill_max":0.112,"cpk_fill_max":0.3760705501,"p_fill_min":0.0,"cpk_fill_min":2.152966016,"p_stretch":0.0,"cpk_stretch":7.999405413,"p_od":0.0,"cpk_od":6.975034522}}
{"name":"p05-cold-shrink","inp":{"s_min":-5,"T_min":-50,"s_max":0,"T_max":60,"red":"linear","nu":0.5,"n_mc":1000},"prod":{"squeeze_nom":0.2100627107,"squeeze_min":0.1412366125,"squeeze_max":0.2483910765,"fill_min":0.5980225604,"fill_max":0.7995291126,"stretch_nom":0.0295,"stretch":0.03830645161,"stretch_min":0.02083333333,"X_c":0.06652308011,"u_stretch":0.6384408602,"red_nom":0.01475,"gap_min":2.99748e-05,"gap_max":0.00010605088,"p_sq_min":0.002,"cpk_sq_min":1.106393445,"p_sq_max":0.0,"cpk_sq_max":3.604576923,"p_fill_max":0.0,"cpk_fill_max":2.509998096,"p_fill_min":0.0,"cpk_fill_min":1.350723836,"p_stretch":0.0,"cpk_stretch":3.575025462}}
{"name":"p06-parker-12pct","inp":{"d_r":50,"d_r_tol":0.2,"D_s":60,"D_s_u":0.05,"D_s_l":0,"D_g":56.0,"D_g_u":0,"D_g_l":-0.05,"D_c":59.9,"D_c_u":-0.05,"D_c_l":-0.1,"red":"parker","x_hi":15,"n_mc":1000},"prod":{"squeeze_nom":0.3781903637,"squeeze_min":0.3446403762,"squeeze_max":0.4271073012,"fill_min":0.7663571219,"fill_max":1.143210157,"stretch_nom":0.1195,"stretch":0.124497992,"stretch_min":0.1145418327,"X_c":0.1307426891,"u_stretch":0.8299866131,"red_nom":0.069536085,"gap_min":0.000149928,"gap_max":0.00025018,"p_sq_min":0.0,"cpk_sq_min":11.86243956,"p_sq_max":1.0,"cpk_sq_max":-5.686080436,"p_fill_max":1.0,"cpk_fill_max":-3.324460508,"p_fill_min":0.0,"cpk_fill_min":4.780627476,"p_stretch":0.0,"cpk_stretch":6.768904513}}
{"name":"p07-parker-30pct-hold","inp":{"d_r":50,"d_r_tol":0.0,"D_s":65,"D_s_u":0.05,"D_s_l":0,"D_g":64.0,"D_g_u":0,"D_g_l":0,"D_c":64.9,"D_c_u":-0.05,"D_c_l":-0.1,"red":"parker","x_hi":40,"n_mc":1000},"prod":{"squeeze_nom":0.8327772838,"squeeze_min":0.8226811367,"squeeze_max":0.8452895915,"fill_min":2.694333199,"fill_max":4.59755089,"stretch_nom":0.28,"stretch":0.28,"stretch_min":0.28,"X_c":0.2871082495,"u_stretch":0.7,"red_nom":0.12435,"gap_min":0.000149928,"gap_max":0.00025018,"p_sq_min":0.0,"cpk_sq_min":107.8913099,"p_sq_max":1.0,"cpk_sq_max":-87.58044623,"p_fill_max":1.0,"cpk_fill_max":-12.64399069,"p_fill_min":0.0,"cpk_fill_min":12.63801852,"p_stretch":0.0}}
{"name":"p08-linear-20pct","inp":{"d_r":50,"d_r_tol":0.0,"D_s":65,"D_s_u":0.05,"D_s_l":0,"D_g":60.0,"D_g_u":0,"D_g_l":0,"D_c":64.9,"D_c_u":-0.05,"D_c_l":-0.1,"red":"linear","nu":0.5,"x_hi":40,"n_mc":1000},"prod":{"squeeze_nom":0.2023809524,"squeeze_min":0.167170894,"squeeze_max":0.2590293374,"fill_min":0.5843142022,"fill_max":0.9142405436,"stretch_nom":0.2,"stretch":0.2,"stretch_min":0.2,"X_c":0.2066639839,"u_stretch":0.5,"red_nom":0.1,"gap_min":0.000149928,"gap_max":0.00025018,"p_sq_min":0.0,"cpk_sq_min":1.924685905,"p_sq_max":0.0,"cpk_sq_max":2.96663209,"p_fill_max":0.537,"cpk_fill_max":-0.01960834122,"p_fill_min":0.008,"cpk_fill_min":0.7832686535,"p_stretch":0.0}}
{"name":"p09-zero-tol","inp":{"d_r_tol":0,"W_tol":0,"D_s_u":0,"D_s_l":0,"D_g_u":0,"D_g_l":0,"D_c_u":0,"D_c_l":0,"b_u":0,"b_l":0,"D_c":56.8,"n_mc":1000,"gap_allow":0.2},"prod":{"squeeze_nom":0.2170855078,"squeeze_min":0.2105548,"squeeze_max":0.2453391811,"fill_min":0.7098132854,"fill_max":0.8000584569,"stretch_nom":0.03,"stretch":0.03,"stretch_min":0.03,"X_c":0.03571991952,"u_stretch":0.5,"red_nom":0.01467072184,"gap_min":9.9952e-05,"gap_max":0.000100072,"gap_allow":0.0002,"u_gap":0.50036,"p_sq_min":0.0,"p_sq_max":0.0,"p_fill_max":0.0,"p_fill_min":0.0,"p_stretch":0.0,"p_gap":0.0}}
{"name":"p10-large-ring","inp":{"d_r":970,"d_r_tol":2.0,"W":8,"W_tol":0.15,"D_s":993.2,"D_s_u":0.09,"D_s_l":0,"D_g":980,"D_g_u":0,"D_g_l":-0.12,"D_c":993.0,"D_c_u":-0.1,"D_c_l":-0.2,"b":10.6,"b_u":0.3,"b_l":0,"r_g":0.5,"n_mc":1000,"gap_allow":0.4,"x_hi":10},"prod":{"squeeze_nom":0.1641876735,"squeeze_min":0.1334130574,"squeeze_max":0.2162507983,"fill_min":0.6442819627,"fill_max":0.8037980835,"stretch_nom":0.01024742268,"stretch":0.01239669421,"stretch_min":0.008106995885,"X_c":0.01801885694,"u_stretch":0.1239669421,"red_nom":0.005084666,"gap_min":0.000299856,"gap_max":0.0004903528,"gap_allow":0.0004,"u_gap":1.225882,"p_sq_min":0.093,"cpk_sq_min":0.4367034333,"p_sq_max":0.0,"cpk_sq_max":6.68875882,"p_fill_max":0.0,"cpk_fill_max":3.016813309,"p_fill_min":0.0,"cpk_fill_min":3.076473837,"p_stretch":0.0,"cpk_stretch":4.91739833,"p_gap":0.427,"cpk_gap":0.07005112072}}
{"name":"p11-rod-alpha_s","inp":{"type":"rod","d_r":39.5,"d_r_tol":0.35,"W":3.0,"W_tol":0.1,"D_s":40,"D_s_u":-0.025,"D_s_l":-0.05,"D_g":44.8,"D_g_u":0.062,"D_g_l":0,"D_c":40,"D_c_u":0.039,"D_c_l":0,"b":4.1,"b_u":0.1,"b_l":0,"alpha_s":2.3e-05,"alpha_m":1.2e-05,"dD_p":0.03,"gap_allow":0.2,"n_mc":1000,"T_max":120},"prod":{"squeeze_nom":0.1898690659,"squeeze_min":0.134189416,"squeeze_max":0.282330316,"fill_min":0.6158800549,"fill_max":0.8675036179,"stretch_nom":0.01170886076,"stretch":0.02107279693,"stretch_min":0.002509410289,"X_c":0.0262911569,"u_stretch":0.3512132822,"red_nom":0.005803515403,"od_comp_nom":0.0147032967,"od_comp":0.02714440825,"c_h":0.05585199791,"gap_min":-1.89425e-05,"gap_max":0.00013653528,"gap_allow":0.0002,"u_gap":0.6826764,"p_sq_min":0.003,"cpk_sq_min":0.9177934375,"p_sq_max":0.0,"cpk_sq_max":2.008350952,"p_fill_max":0.001,"cpk_fill_max":1.129590251,"p_fill_min":0.0,"cpk_fill_min":1.678807191,"p_stretch":0.0,"cpk_stretch":1.305219794,"p_od":0.0,"cpk_od":5.173020451,"p_gap":0.0,"cpk_gap":4.12313275}}
{"name":"p12-alpha-lt-alpha_m","inp":{"alpha":8e-06,"alpha_m":2.3e-05,"n_mc":1000,"T_min":-40,"T_max":150,"gap_allow":0.2},"prod":{"squeeze_nom":0.2103180081,"squeeze_min":0.1775419717,"squeeze_max":0.2583281557,"fill_min":0.6519842739,"fill_max":0.8209550117,"stretch_nom":0.0295,"stretch":0.03830645161,"stretch_min":0.02083333333,"X_c":0.03737152704,"u_stretch":0.6384408602,"red_nom":0.01443147714,"gap_min":2.99586e-05,"gap_max":0.00010631694,"gap_allow":0.0002,"u_gap":0.5315847,"p_sq_min":0.0,"cpk_sq_min":2.638393514,"p_sq_max":0.0,"cpk_sq_max":3.197209205,"p_fill_max":0.0,"cpk_fill_max":1.997614206,"p_fill_min":0.0,"cpk_fill_min":2.634161504,"p_stretch":0.0,"cpk_stretch":3.575025462,"p_gap":0.0,"cpk_gap":4.785443515}}
{"name":"p13-contact-loss","inp":{"D_g":52.5,"D_g_u":0,"D_g_l":-0.05,"D_s":56.9,"n_mc":1000,"gap_allow":0.2,"q_lo_s":5},"prod":{"squeeze_nom":0.3490345185,"squeeze_min":0.3140951723,"squeeze_max":0.3990821171,"fill_min":0.7670784866,"fill_max":1.039112722,"stretch_nom":0.0495,"stretch":0.05846774194,"stretch_min":0.04067460317,"X_c":0.06434575193,"u_stretch":0.9744623656,"red_nom":0.02386748687,"gap_min":2.99856e-05,"gap_max":0.00010607632,"gap_allow":0.0002,"u_gap":0.5303816,"p_sq_min":0.0,"cpk_sq_min":14.99878273,"p_sq_max":1.0,"cpk_sq_max":-3.875787002,"p_fill_max":1.0,"cpk_fill_max":-1.980517368,"p_fill_min":0.0,"cpk_fill_min":4.796788497,"p_stretch":0.0,"cpk_stretch":1.248304195,"p_gap":0.0,"cpk_gap":4.801916071}}
{"name":"p14-near-zero-stretch","inp":{"d_r_tol":0.0,"D_g":50.0,"D_g_u":0,"D_g_l":0,"D_s":56.0,"D_s_u":0.0,"D_s_l":0.0,"D_c":55.9,"D_c_u":0,"D_c_l":0,"red":"parker","W_tol":0.0,"x_lo":-1,"n_mc":1000,"gap_allow":0.2,"alpha":0.0001},"prod":{"squeeze_nom":0.1428571429,"squeeze_min":0.1375025251,"squeeze_max":0.1611105948,"fill_min":0.6471528989,"fill_max":0.7135543689,"stretch_nom":0.0,"stretch":0.0,"stretch_min":0.0,"X_c":0.003534136546,"u_stretch":0.0,"red_nom":0.0,"gap_min":9.9952e-05,"gap_max":0.000100072,"gap_allow":0.0002,"u_gap":0.50036,"p_sq_min":1.0,"p_sq_max":0.0,"p_fill_max":0.0,"cpk_fill_max":19.72796323,"p_fill_min":0.0,"cpk_fill_min":7.976097754,"p_stretch":0.0,"p_gap":0.0}}
{"name":"p15-ext-out-of-range","inp":{"H_A":60,"p_op":250,"ext":[[70,0,0.3],[70,100,0.15],[70,200,0.08]],"n_mc":1000},"prod":{"squeeze_nom":0.2103180081,"squeeze_min":0.1695600303,"squeeze_max":0.2695741156,"fill_min":0.6395111504,"fill_max":0.846429192,"stretch_nom":0.0295,"stretch":0.03830645161,"stretch_min":0.02083333333,"X_c":0.04407249951,"u_stretch":0.6384408602,"red_nom":0.01443147714,"gap_min":2.99856e-05,"gap_max":0.00010607632,"gap_allow":8e-05,"u_gap":1.325954,"p_sq_min":0.0,"cpk_sq_min":2.287236683,"p_sq_max":0.0,"cpk_sq_max":2.724558438,"p_fill_max":0.0,"cpk_fill_max":1.422173898,"p_fill_min":0.0,"cpk_fill_min":2.354466823,"p_stretch":0.0,"cpk_stretch":3.575025462,"p_gap":0.106,"cpk_gap":0.4349180654}}
{"name":"p16-static-ext-gap-only","inp":{"gap_allow":0.05,"n_mc":1000,"p_op":0},"prod":{"squeeze_nom":0.2103180081,"squeeze_min":0.1695600303,"squeeze_max":0.2695741156,"fill_min":0.6395111504,"fill_max":0.846429192,"stretch_nom":0.0295,"stretch":0.03830645161,"stretch_min":0.02083333333,"X_c":0.04407249951,"u_stretch":0.6384408602,"red_nom":0.01443147714,"gap_min":2.99856e-05,"gap_max":0.00010607632,"gap_allow":5e-05,"u_gap":2.1215264,"p_sq_min":0.0,"cpk_sq_min":2.287236683,"p_sq_max":0.0,"cpk_sq_max":2.724558438,"p_fill_max":0.0,"cpk_fill_max":1.422173898,"p_fill_min":0.0,"cpk_fill_min":2.354466823,"p_stretch":0.0,"cpk_stretch":3.575025462,"p_gap":0.97,"cpk_gap":-0.6568314361}}
{"name":"p17-rod-od-compression","inp":{"type":"rod","d_r":80.0,"d_r_tol":0.4,"W":5.33,"W_tol":0.13,"D_s":81.0,"D_s_u":0,"D_s_l":-0.04,"D_g":90.7,"D_g_u":0.08,"D_g_l":0,"D_c":81.15,"D_c_u":0.035,"D_c_l":0,"b":7.1,"b_u":0.15,"b_l":0,"duty":"dynamic","T_min":-40,"T_max":120,"alpha":0.00018,"s_max":8,"red":"incomp","n_mc":2000,"gap_allow":0.15,"od_hi":4},"prod":{"squeeze_nom":0.07883695717,"squeeze_min":0.03290585151,"squeeze_max":0.1837172186,"fill_min":0.5689909517,"fill_max":0.7775974732,"stretch_nom":0.01225,"stretch":0.0175879397,"stretch_min":0.006965174129,"X_c":0.0279572143,"u_stretch":0.2931323283,"red_nom":0.00606929493,"od_comp_nom":-0.0008824178248,"od_comp":0.006789312308,"c_h":0.04792054113,"gap_min":0.000149892,"gap_max":0.00022527,"gap_allow":0.00015,"u_gap":1.5018,"p_sq_min":1.0,"cpk_sq_min":-1.51201971,"p_sq_max":0.0,"cpk_sq_max":4.284291528,"p_fill_max":0.0,"cpk_fill_max":3.409083569,"p_fill_min":0.1385,"cpk_fill_min":0.3566227166,"p_stretch":0.0,"cpk_stretch":2.417343153,"p_od":0.0,"cpk_od":7.734952244,"p_gap":1.0,"cpk_gap":-1.417846375}}
{"name":"p18-face-ext-gap-only","inp":{"type":"face","d_r":120,"d_r_tol":0.6,"W":3.53,"W_tol":0.1,"D_g":123.0,"D_g_u":0.06,"D_g_l":0,"h_f":2.7,"h_f_u":0.05,"h_f_l":0,"e_f":0.02,"e_f_u":0.03,"e_f_l":0,"b":4.8,"b_u":0.1,"b_l":0,"n_mc":2000,"gap_allow":0.1},"prod":{"squeeze_nom":0.2183601545,"squeeze_min":0.1792658166,"squeeze_max":0.2759417416,"fill_min":0.6552018683,"fill_max":0.860595125,"stretch_nom":0.02525,"stretch":0.03065326633,"stretch_min":0.01990049751,"X_c":0.03637681365,"u_stretch":0.5108877722,"red_nom":0.01239083615,"gap_min":2e-05,"gap_max":5e-05,"gap_allow":0.0001,"u_gap":0.5,"p_sq_min":0.0,"cpk_sq_min":2.623797817,"p_sq_max":0.0,"cpk_sq_max":2.371555754,"p_fill_max":0.001,"cpk_fill_max":1.116620595,"p_fill_min":0.0,"cpk_fill_min":2.673196579,"p_stretch":0.0,"cpk_stretch":4.919658124,"p_gap":0.0,"cpk_gap":4.333333333}}
{"name":"fz00-piston-incomp","inp":{"type":"piston","duty":"static","red":"incomp","d_r":105.48,"d_r_tol":0.54,"W":3.53,"W_tol":0.093,"n_mc":1000,"T_min":-30.556953066318215,"T_max":120.15963313500288,"alpha":0.00022320829469108303,"alpha_m":2.3e-05,"s_min":-1.4938442510482925,"s_max":11.730416929885,"b":4.71,"b_u":0.1,"b_l":0,"r_g":0.4,"alpha_s":2.3e-05,"D_g":109.272,"D_g_u":0,"D_g_l":-0.075,"D_s":114.45,"D_s_u":0.061,"D_s_l":0,"D_c":114.368,"D_c_u":-0.02,"D_c_l":-0.05,"seed":263731,"dist":"triangular"},"prod":{"squeeze_nom":0.2438317518,"squeeze_min":0.1929518473,"squeeze_max":0.3339836002,"fill_min":0.6696955009,"fill_max":1.008107308,"stretch_nom":0.03559442548,"stretch":0.04128073185,"stretch_min":0.02996604414,"X_c":0.05723168839,"u_stretch":0.6880121974,"red_nom":0.01733576902,"gap_min":0.0001018813934,"gap_max":0.0001934446086,"p_sq_min":0.0,"cpk_sq_min":2.879313723,"p_sq_max":0.745,"cpk_sq_max":-0.2339028598,"p_fill_max":1.0,"cpk_fill_max":-1.354823889,"p_fill_min":0.0,"cpk_fill_min":2.609005738,"p_stretch":0.0,"cpk_stretch":3.750114007}}
{"name":"fz01-rod-parker","inp":{"type":"rod","duty":"static","red":"parker","d_r":199.93,"d_r_tol":0.05,"W":5.33,"W_tol":0.081,"n_mc":1000,"T_min":-39.867864459594,"T_max":59.7243696652835,"alpha":0.0001896151573186803,"alpha_m":1.7e-05,"s_min":-2.535459847346006,"s_max":0.49478603121552833,"b":7.99,"b_u":0.1,"b_l":0,"r_g":0.2,"dD_p":0.08909687294491964,"D_s":208.803,"D_s_u":0,"D_s_l":-0.037,"D_g":216.52,"D_g_u":0.084,"D_g_l":0,"D_c":208.983,"D_c_u":0.03,"D_c_l":0,"seed":917959},"prod":{"squeeze_nom":0.2471961943,"squeeze_min":0.20588674,"squeeze_max":0.2742399466,"fill_min":0.6036524426,"fill_max":0.765530574,"stretch_nom":0.0442880008,"stretch":0.04464178507,"stretch_min":0.04393439344,"X_c":0.06463611589,"u_stretch":0.7440297512,"red_nom":0.03082766405,"od_comp_nom":-0.02835842158,"od_comp":-0.02712497984,"c_h":0.0,"gap_min":0.0001798168043,"gap_max":0.0003362636756,"p_sq_min":0.0,"cpk_sq_min":5.863450963,"p_sq_max":0.0,"cpk_sq_max":3.528585044,"p_fill_max":0.0,"cpk_fill_max":5.083487683,"p_fill_min":0.0,"cpk_fill_min":1.576262932,"p_stretch":0.0,"cpk_stretch":56.70739597,"p_od":0.0,"cpk_od":103.7571656}}
{"name":"fz02-rod-linear","inp":{"type":"rod","duty":"dynamic","red":"linear","d_r":184.18,"d_r_tol":0.34,"W":5.33,"W_tol":0.147,"n_mc":1000,"T_min":-24.093540711260545,"T_max":152.72424015069498,"alpha":0.00017783776088185034,"alpha_m":1.2e-05,"s_min":-1.0912068017324348,"s_max":19.25450514326433,"b":7.64,"b_u":0.1,"b_l":0,"r_g":0.4,"alpha_s":1e-05,"D_s":186.282,"D_s_u":0,"D_s_l":-0.054,"D_g":193.966,"D_g_u":0.03,"D_g_l":0,"D_c":186.437,"D_c_u":0.03,"D_c_l":0,"nu":0.4348606325230092,"seed":749232},"prod":{"squeeze_nom":0.2732735569,"squeeze_min":0.2357714004,"squeeze_max":0.3948286,"fill_min":0.674927713,"fill_max":1.025448503,"stretch_nom":0.01126615268,"stretch":0.01328328982,"stretch_min":0.009256449165,"X_c":0.02458182906,"u_stretch":0.2213881636,"red_nom":0.004899206279,"od_comp_nom":0.004408745637,"od_comp":0.007714580967,"c_h":0.08440282051,"gap_min":0.0001384903201,"gap_max":0.0002888145927,"p_sq_min":0.0,"cpk_sq_min":7.842064439,"p_sq_max":1.0,"cpk_sq_max":-6.857019436,"p_fill_max":1.0,"cpk_fill_max":-1.944629973,"p_fill_min":0.0,"cpk_fill_min":3.066914609,"p_stretch":0.0,"cpk_stretch":6.01640848,"p_od":0.0,"cpk_od":24.19045984}}
{"name":"fz03-face-linear","inp":{"type":"face","duty":"dynamic","red":"linear","d_r":116.36,"d_r_tol":0.48,"W":3.53,"W_tol":0.06,"n_mc":1000,"T_min":-45.822806204809325,"T_max":156.4578799729424,"alpha":8.157707121691536e-05,"alpha_m":1.2e-05,"s_min":-2.066143741516396,"s_max":11.945464307169795,"b":4.98,"b_u":0.1,"b_l":0,"r_g":0.4,"face_p":"int","D_g":122.243,"D_g_u":0.05,"D_g_l":0,"h_f":2.666,"h_f_u":0.05,"h_f_l":0,"e_f":0.05,"e_f_u":0.05,"e_f_l":0,"nu":0.465234702076252,"seed":847207,"dist":"triangular"},"prod":{"squeeze_nom":0.2412431489,"squeeze_min":0.2055051316,"squeeze_max":0.3129588244,"fill_min":0.6685605303,"fill_max":0.8953798895,"stretch_nom":-0.009333981526,"stretch":-0.004290832112,"stretch_min":-0.01432833414,"X_c":0.007279508276,"u_stretch":-0.07151386853,"red_nom":0.0,"od_comp_nom":0.009333981526,"od_comp":0.01432833414,"c_h":0.05962827731,"gap_min":5e-05,"gap_max":0.0001,"p_sq_min":0.0,"cpk_sq_min":6.81873893,"p_sq_max":1.0,"cpk_sq_max":-2.49224629,"p_fill_max":0.362,"cpk_fill_max":0.1339657166,"p_fill_min":0.0,"cpk_fill_min":3.324209398,"p_stretch":0.0,"cpk_stretch":14.23582783,"p_od":0.0,"cpk_od":10.40287461}}
{"name":"fz04-face-linear","inp":{"type":"face","duty":"dynamic","red":"linear","d_r":167.97,"d_r_tol":0.55,"W":1.78,"W_tol":0.015,"n_mc":1000,"T_min":-11.746680809883095,"T_max":149.91176437371033,"alpha":0.00016757946658135426,"alpha_m":2.3e-05,"s_min":-0.4681764558582815,"s_max":3.660976732860175,"b":2.49,"b_u":0.1,"b_l":0,"r_g":0.4,"dD_p":0.02664661400263285,"face_p":"int","D_g":167.385,"D_g_u":0.05,"D_g_l":0,"h_f":1.535,"h_f_u":0.05,"h_f_l":0,"e_f":0.05,"e_f_u":0.05,"e_f_l":0,"nu":0.48329613955919826,"seed":96646},"prod":{"squeeze_nom":0.1341847112,"squeeze_min":0.1030023829,"squeeze_max":0.1944267933,"fill_min":0.6075238182,"fill_max":0.7597361785,"stretch_nom":-0.02401912202,"stretch":-0.02056156771,"stretch_min":-0.02745337284,"X_c":-0.01450161732,"u_stretch":-0.3426927952,"red_nom":0.0,"od_comp_nom":0.02401912202,"od_comp":0.02745337284,"c_h":0.05670468192,"gap_min":5e-05,"gap_max":0.0001,"p_sq_min":0.0,"cpk_sq_min":1.641742105,"p_sq_max":0.0,"cpk_sq_max":5.113629301,"p_fill_max":0.0,"cpk_fill_max":5.927654315,"p_fill_min":0.0,"cpk_fill_min":2.140831499,"p_stretch":0.0,"cpk_stretch":26.77907587,"p_od":0.0,"cpk_od":11.46804011}}
{"name":"fz05-piston-incomp","inp":{"type":"piston","duty":"static","red":"incomp","d_r":120.32,"d_r_tol":0.15,"W":5.33,"W_tol":0.093,"n_mc":1000,"T_min":-7.370149655257151,"T_max":173.89136476513073,"alpha":0.00019364111228885084,"alpha_m":1.7e-05,"s_min":-3.856496207948287,"s_max":0.10262195966284304,"b":7.9,"b_u":0.1,"b_l":0,"r_g":0.4,"D_g":125.057,"D_g_u":0,"D_g_l":-0.031,"D_s":133.056,"D_s_u":0.033,"D_s_l":0,"D_c":132.918,"D_c_u":-0.02,"D_c_l":-0.05,"seed":627192},"prod":{"squeeze_nom":0.2319834177,"squeeze_min":0.193250467,"squeeze_max":0.278598583,"fill_min":0.6094173591,"fill_max":0.7946591389,"stretch_nom":0.03924119016,"stretch":0.04066738787,"stretch_min":0.03781854404,"X_c":0.05952461709,"u_stretch":0.6777897978,"red_nom":0.01906140042,"gap_min":0.0001579264838,"gap_max":0.0002215781699,"p_sq_min":0.0,"cpk_sq_min":4.372953902,"p_sq_max":0.0,"cpk_sq_max":2.870203752,"p_fill_max":0.0,"cpk_fill_max":3.339102154,"p_fill_min":0.0,"cpk_fill_min":1.700239523,"p_stretch":0.0,"cpk_stretch":15.9439412}}
{"name":"fz06-face-incomp","inp":{"type":"face","duty":"dynamic","red":"incomp","d_r":65.23,"d_r_tol":0.56,"W":6.99,"W_tol":0.118,"n_mc":1000,"T_min":4.051129900083268,"T_max":82.84060575871366,"alpha":0.00015004665528512925,"alpha_m":1.2e-05,"s_min":-2.879814469505499,"s_max":3.9339422249803224,"b":8.77,"b_u":0.1,"b_l":0,"r_g":0.1,"alpha_s":1.2e-05,"dD_p":0.03470178443660769,"face_p":"ext","D_g":67.073,"D_g_u":0.05,"D_g_l":0,"h_f":5.183,"h_f_u":0.05,"h_f_l":0,"e_f":0.05,"e_f_u":0.05,"e_f_l":0,"seed":26825,"dist":"triangular"},"prod":{"squeeze_nom":0.2443426722,"squeeze_min":0.2101707574,"squeeze_max":0.2871036518,"fill_min":0.7428322293,"fill_max":0.9312098729,"stretch_nom":0.02863713015,"stretch":0.03793103448,"stretch_min":0.01950144399,"X_c":0.05040331057,"u_stretch":0.632183908,"red_nom":0.01401819287,"gap_min":5e-05,"gap_max":0.0001,"p_sq_min":0.0,"cpk_sq_min":7.674040075,"p_sq_max":1.0,"cpk_sq_max":-1.13089728,"p_fill_max":1.0,"cpk_fill_max":-1.090463034,"p_fill_min":0.0,"cpk_fill_min":5.371508796,"p_stretch":0.0,"cpk_stretch":2.64508994}}
{"name":"fz07-rod-linear","inp":{"type":"rod","duty":"static","red":"linear","d_r":95.53,"d_r_tol":0.57,"W":3.53,"W_tol":0.086,"n_mc":1000,"T_min":-3.6542969051647773,"T_max":61.54468180817854,"alpha":0.0001683343340097931,"alpha_m":2.3e-05,"s_min":-2.2599416373780885,"s_max":9.176345109674276,"b":4.7,"b_u":0.1,"b_l":0,"r_g":0.4,"alpha_s":2.3e-05,"dD_p":0.06472304606546123,"D_s":96.852,"D_s_u":0,"D_s_l":-0.057,"D_g":102.241,"D_g_u":0.024,"D_g_l":0,"D_c":97.032,"D_c_u":0.03,"D_c_l":0,"nu":0.4281705456801195,"seed":507344},"prod":{"squeeze_nom":0.2277359547,"squeeze_min":0.1861316052,"squeeze_max":0.3052228336,"fill_min":0.6791651064,"fill_max":0.9160220132,"stretch_nom":0.01354024914,"stretch":0.0199241786,"stretch_min":0.007232049948,"X_c":0.0312724597,"u_stretch":0.3320696434,"red_nom":0.005797535861,"od_comp_nom":0.003284920558,"od_comp":0.01055820075,"c_h":0.0448559032,"gap_min":0.0001799020712,"gap_max":0.000331978172,"p_sq_min":0.0,"cpk_sq_min":3.240274608,"p_sq_max":0.0,"cpk_sq_max":1.132592395,"p_fill_max":0.558,"cpk_fill_max":-0.02251881051,"p_fill_min":0.0,"cpk_fill_min":3.542586648,"p_stretch":0.0,"cpk_stretch":2.236182966,"p_od":0.0,"cpk_od":9.802414599}}
{"name":"fz08-face-incomp","inp":{"type":"face","duty":"static","red":"incomp","d_r":34.54,"d_r_tol":0.1,"W":1.78,"W_tol":0.127,"n_mc":1000,"T_min":-32.701999402817464,"T_max":99.5495986537749,"alpha":0.00015039475394463398,"alpha_m":1.2e-05,"s_min":-3.7008879346441064,"s_max":3.7439756608107633,"b":2.33,"b_u":0.1,"b_l":0,"r_g":0.2,"face_p":"int","D_g":37.745,"D_g_u":0.05,"D_g_l":0,"h_f":1.417,"h_f_u":0.05,"h_f_l":0,"e_f":0.05,"e_f_u":0.05,"e_f_l":0,"seed":466179},"prod":{"squeeze_nom":0.193403631,"squeeze_min":0.0850451263,"squeeze_max":0.2889981331,"fill_min":0.5691293589,"fill_max":0.9497876585,"stretch_nom":-0.008661417323,"stretch":0.001298150797,"stretch_min":-0.01843761377,"X_c":0.02141889095,"u_stretch":0.02163584662,"red_nom":0.0,"od_comp_nom":0.008661417323,"od_comp":0.01843761377,"c_h":0.04093878604,"gap_min":5e-05,"gap_max":0.0001,"p_sq_min":0.188,"cpk_sq_min":0.296243606,"p_sq_max":0.0,"cpk_sq_max":1.316440746,"p_fill_max":0.049,"cpk_fill_max":0.5174115132,"p_fill_min":0.004,"cpk_fill_min":0.8793253505,"p_stretch":0.0,"cpk_stretch":9.625617053,"p_od":0.0,"cpk_od":7.197135686}}
{"name":"fz09-piston-none","inp":{"type":"piston","duty":"dynamic","red":"none","d_r":161.35,"d_r_tol":0.51,"W":6.99,"W_tol":0.035,"n_mc":1000,"T_min":6.7571714261052875,"T_max":163.0069077281349,"alpha":0.0001897654653800664,"alpha_m":2.3e-05,"s_min":-2.729639063031353,"s_max":16.671091891749718,"b":10.81,"b_u":0.1,"b_l":0,"r_g":0.1,"D_g":163.378,"D_g_u":0,"D_g_l":-0.066,"D_s":174.266,"D_s_u":0.052,"D_s_l":0,"D_c":174.2,"D_c_u":-0.02,"D_c_l":-0.05,"seed":750794,"dist":"triangular"},"prod":{"squeeze_nom":0.2169527897,"squeeze_min":0.1996695532,"squeeze_max":0.2809681643,"fill_min":0.6185246526,"fill_max":0.8245963508,"stretch_nom":0.01236442516,"stretch":0.0157796568,"stretch_min":0.008970715433,"X_c":0.02746366229,"u_stretch":0.26299428,"red_nom":0.0,"gap_min":8.597380569e-05,"gap_max":0.0001685525787,"p_sq_min":0.0,"cpk_sq_min":17.63831423,"p_sq_max":1.0,"cpk_sq_max":-4.177562127,"p_fill_max":0.0,"cpk_fill_max":3.612655352,"p_fill_min":0.0,"cpk_fill_min":3.439576154,"p_stretch":0.0,"cpk_stretch":3.148477643}}
{"name":"fz10-face-parker","inp":{"type":"face","duty":"dynamic","red":"parker","d_r":148.88,"d_r_tol":0.4,"W":6.99,"W_tol":0.02,"n_mc":1000,"T_min":-5.292180117518527,"T_max":53.50030372800374,"alpha":0.00015412898875114892,"alpha_m":1.2e-05,"s_min":-1.89271625606301,"s_max":15.18225466553172,"b":10.5,"b_u":0.1,"b_l":0,"r_g":0.1,"dD_p":0.031355090645541296,"face_p":"int","D_g":159.03,"D_g_u":0.05,"D_g_l":0,"h_f":6.071,"h_f_u":0.05,"h_f_l":0,"e_f":0.05,"e_f_u":0.05,"e_f_l":0,"seed":268566},"prod":{"squeeze_nom":0.1381449507,"squeeze_min":0.1177864625,"squeeze_max":0.2093164302,"fill_min":0.5827572565,"fill_max":0.7264147079,"stretch_nom":-0.0233636252,"stretch":-0.02056396995,"stretch_min":-0.02614819351,"X_c":-0.01074832341,"u_stretch":-0.3427328326,"red_nom":0.0,"od_comp_nom":0.0233636252,"od_comp":0.02614819351,"c_h":0.07536800813,"gap_min":5e-05,"gap_max":0.0001,"p_sq_min":0.0,"cpk_sq_min":5.509831232,"p_sq_max":0.0,"cpk_sq_max":11.37826583,"p_fill_max":0.0,"cpk_fill_max":21.19444046,"p_fill_min":1.0,"cpk_fill_min":-1.312929832,"p_stretch":0.0,"cpk_stretch":34.51088926,"p_od":0.0,"cpk_od":15.16673334}}
{"name":"fz11-piston-linear","inp":{"type":"piston","duty":"dynamic","red":"linear","d_r":88.21,"d_r_tol":0.58,"W":1.78,"W_tol":0.009,"n_mc":1000,"T_min":-43.534223425325465,"T_max":83.38861418681475,"alpha":9.595792477272829e-05,"alpha_m":1.7e-05,"s_min":-2.5411136900747016,"s_max":18.722221830105905,"b":2.44,"b_u":0.1,"b_l":0,"r_g":0.2,"D_g":92.553,"D_g_u":0,"D_g_l":-0.045,"D_s":95.093,"D_s_u":0.033,"D_s_l":0,"D_c":94.974,"D_c_u":-0.02,"D_c_l":-0.05,"nu":0.49582242321360004,"seed":921073},"prod":{"squeeze_nom":0.257530756,"squeeze_min":0.2235357482,"squeeze_max":0.3397692817,"fill_min":0.6748405947,"fill_max":0.9830136528,"stretch_nom":0.04897970752,"stretch":0.05617939062,"stretch_min":0.04187408492,"X_c":0.07065703763,"u_stretch":0.936323177,"red_nom":0.02428523727,"gap_min":0.0001388498686,"gap_max":0.0002022176765,"p_sq_min":0.0,"cpk_sq_min":15.11494936,"p_sq_max":1.0,"cpk_sq_max":-8.919343026,"p_fill_max":1.0,"cpk_fill_max":-3.78089445,"p_fill_min":0.0,"cpk_fill_min":6.024651145,"p_stretch":0.0,"cpk_stretch":1.596615472}}
"""


def params_from(inp):
    """Translate the probe inputs to model parameters."""
    m = {}
    for k in ("type", "duty", "face_p", "red", "dist", "nu", "T_min", "T_max", "alpha", "alpha_m", "alpha_s", "s_min", "s_max",
              "q_lo_s", "q_hi_s", "q_lo_d", "q_hi_d", "fill_lo", "fill_hi", "x_lo", "x_hi", "od_hi", "H_A", "p_op", "dD_p",
              "gap_allow", "r_g", "k_sigma"):
        if k in inp:
            m[k] = inp[k]
    if "ext" in inp:
        m["ext"] = [tuple(r) for r in inp["ext"]]
    if "measured" in inp:
        m["measured"] = [tuple(r) for r in inp["measured"]]
    for a, b in (("d_r", "d"), ("d_r_tol", "d_tol"), ("W", "W"), ("W_tol", "W_tol")):
        if a in inp:
            m[b] = inp[a]
    for n in ("D_s", "D_g", "D_c", "h_f", "e_f", "b"):
        if n in inp or n + "_u" in inp or n + "_l" in inp:
            nom, u, l = DEF[n]
            m[n] = (inp.get(n, nom), inp.get(n + "_u", u), inp.get(n + "_l", l))
    return P(**m)


def run_extras():
    print()
    print("Extra probes (product values hard-coded from the probe tool; tolerance 1e-6 relative)")
    mc_map = {"p_sq_min": "sq_min", "p_sq_max": "sq_max", "p_fill_max": "fill_max", "p_fill_min": "fill_min",
              "p_stretch": "stretch", "p_gap": "gap", "p_od": "od"}
    n_cases = 0
    for line in EXTRA_LINES.strip().splitlines():
        case = json.loads(line)
        name, inp, prod = case["name"], case["inp"], case["prod"]
        p = params_from(inp)
        wc = worst_case(p)
        allow = extrusion_allow(p)
        if allow:
            wc["gap_allow"] = allow / 1000.0
            wc["u_gap"] = wc["gap_max"] / allow
        wc["u_stretch"] = wc["stretch"] / (p["x_hi"] / 100.0)
        n_cases += 1
        for sym, pv in prod.items():
            if sym in wc:
                iv = wc[sym]
                if sym in ("gap_min", "gap_max"):
                    iv, pv = iv, pv * 1000.0          # model in mm, product in m
                elif sym == "gap_allow":
                    iv, pv = wc[sym] * 1000.0, pv * 1000.0
                check(name, sym, pv, iv, 1e-6)
        n_mc = int(inp.get("n_mc", 10000))
        seed = int(inp.get("seed", 1))
        fr = fractions_exact(p, n_mc, seed)
        ck = cpk(p)
        for sym, key in mc_map.items():
            if sym in prod and key in fr:
                check(name, sym, prod[sym], fr[key], 1e-6)
            csym = "cpk_" + sym[2:]
            if csym in prod and ck.get(key) is not None:
                check(name, csym, prod[csym], ck[key], 1e-6)
    print(f"{n_cases} extra probe cases")


def run_notes():
    """Evidence for findings. Printed for the report; these lines do not change the exit code."""
    print()
    print("Notes (evidence for the report; no pass/fail)")
    # N1: face seal squeeze does not add the flange gap e_f to the compression height
    p = P(type="face", d=60, d_tol=0.5, W=3.5, W_tol=0.1, D_g=(60.8, 0.05, 0), h_f=(2.75, 0.05, 0), e_f=(0.05, 0.05, 0), b=(4.9, 0.1, 0))
    B = dims_band(p)
    f, gm, gs = factors(p, p["T_min"], p["s_min"])
    X = gm * B["D_g"][1] / (f * B["d"][0]) - 1
    Weff = f * B["W"][0] * (1 - red_fn("incomp")(X))
    q0 = 1 - gm * B["h_f"][1] / Weff
    q1 = 1 - (gm * B["h_f"][1] + B["e_f"][1]) / Weff
    print(f"N1 face seal squeeze_min as the product reports it {100*q0:.3f} %  (product value 16.023 %, any e_f);"
          f" with the flange gap e_f,max = {B['e_f'][1]:.2f} mm added to the compression height {100*q1:.3f} %")
    # N2: ring expands less than the groove metal, true extremes over a temperature scan against the two ends
    import itertools
    p = P(alpha=8e-6, alpha_m=2.3e-5, T_min=-40, T_max=150)
    B = dims_band(p)
    names = ["W", "d", "D_s", "D_g", "D_c", "b"]
    res = dict(qmin=9, qmax=-9, fmax=0, fmin=9)
    for T in np.linspace(-40, 150, 20):
        for sw in (0.0, 5.0):
            for combo in itertools.product(*[B[n] for n in names]):
                e = evaluate(p, dict(zip(names, combo)), T, sw)
                res["qmin"] = min(res["qmin"], e["q"]); res["qmax"] = max(res["qmax"], e["q"])
                res["fmax"] = max(res["fmax"], e["fill_hot"]); res["fmin"] = min(res["fmin"], e["fill"])
    print("N2 ring alpha 8e-6 below metal 2.3e-5, -40 to 150 degC, swell 0 to 5 %: scan extremes "
          f"squeeze {100*res['qmin']:.2f} to {100*res['qmax']:.2f} %, fill {100*res['fmin']:.2f} to {100*res['fmax']:.2f} %;"
          " product (two ends) squeeze 17.75 to 25.83 %, fill 65.20 to 82.10 %")
    # N3: Parker relation gives no thickening for a compressed ring, the other relations do
    for red in ("incomp", "parker"):
        p = P(red=red, alpha=3e-4, T_max=150, s_max=15)
        wc = worst_case(p)
        print(f"N3 hot ring much larger than its seat, relation {red}: squeeze_max {100*wc['squeeze_max']:.2f} %")


if __name__ == "__main__":
    run_kats()
    run_extras()
    run_notes()
    print("fails", fails)
    sys.exit(1 if fails else 0)
