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
Monte Carlo: normal / uniform / triangular dimension samples; the product's xoshiro sampler is not reproduced here,
so the sample fractions are checked against a large numpy sample (statistical check) and C_pk against the
secant-slope linearisation.
"""
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
        lo, hi = lim[k]
        m0 = float(F0[k])
        c = []
        if lo is not None:
            c.append((m0 - lo) / (3 * sig))
        if hi is not None:
            c.append((hi - m0) / (3 * sig))
        out[k] = min(c)
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


if __name__ == "__main__":
    run_kats()
    print("fails", fails)
    sys.exit(1 if fails else 0)
