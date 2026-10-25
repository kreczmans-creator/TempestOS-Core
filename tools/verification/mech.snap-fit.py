#!/usr/bin/env python3
"""Independent re-derivation of mech.snap-fit (Track P, P1).

Method sources (cited by title and section only; no text or tables copied):
  Covestro (formerly Bayer MaterialScience), Snap-Fit Joints for Plastics: A Design Guide, section on
    cantilever, annular and torsion snap-fits (permissible deflection y = c*eps*l^2/h, deflection
    force P = b*h^2*Es*eps/(6*l), joining force W = P*(mu + tan a)/(1 - mu*tan a)).
  Euler-Bernoulli beam theory (any strength-of-materials text): tapered-beam factors are integrated here
    numerically with scipy (quad and a bounded scalar minimiser), not taken from a closed form.
  Thin ring (hoop) mechanics and torsion of a solid round bar: first principles.
Run: python3 tools/verification/mech.snap-fit.py
Exit code: 0 when every known-answer value matches within max(KAT tolerance, 1e-6), else 1.
Extra checks print PASS, FAIL or INFO. A FAIL there is a product finding and does not change the exit code.
Product values in the extra checks were observed with the probe tool on the release/v0.26.0 build.
Units: mm, N, MPa, N*m for torque, strain as a fraction (printed in % where the product uses %).
"""
import math
import sys
from scipy.integrate import quad
from scipy.optimize import minimize_scalar

PI = math.pi


# ------------------------------------------------------------------ method ---
def beam_factor(mode, ratio):
    """Return (K_y, phi_max, t_crit) for a rectangular cantilever by numerical integration.
    t = x/l from the root. Section property ratio r = tip/root."""
    if mode == "const":
        return 2.0 / 3.0, 1.0, None
    a = 1.0 - ratio
    if mode == "thick":
        phi = lambda t: (1 - t) / (1 - a * t) ** 2          # surface strain shape
        jint = lambda t: (1 - t) ** 2 / (1 - a * t) ** 3     # deflection integrand (I ~ h^3)
    elif mode == "width":
        phi = lambda t: (1 - t) / (1 - a * t)
        jint = lambda t: (1 - t) ** 2 / (1 - a * t)
    else:
        raise ValueError(mode)
    J = quad(jint, 0, 1, epsabs=1e-14, epsrel=1e-13)[0]
    res = minimize_scalar(lambda t: -phi(t), bounds=(0, 1), method="bounded", options={"xatol": 1e-12})
    tm, pm = res.x, phi(res.x)
    if pm < phi(0.0) * (1 + 1e-12):
        tm, pm = 0.0, phi(0.0)
    if mode == "thick" and a > 0.5:
        tm = (2 * a - 1) / a                                 # analytic location, used for the report
        pm = phi(tm)
    t_crit = tm if tm > 1e-9 else None
    return 2.0 * J / pm, pm, t_crit


def interp_es(points, eps):
    pts = sorted(points)
    if eps <= pts[0][0]:
        return pts[0][1]
    if eps >= pts[-1][0]:
        return pts[-1][1]
    for (e0, s0), (e1, s1) in zip(pts, pts[1:]):
        if e0 <= eps <= e1:
            return s0 + (s1 - s0) * (eps - e0) / (e1 - e0)


def joining(P, mu, al_deg, alr_deg):
    ta = math.tan(math.radians(al_deg))
    W = P * (mu + ta) / (1 - mu * ta) if mu * ta < 1 else float("inf")
    lock = math.degrees(math.atan(1 / mu)) if mu > 0 else 90.0
    if alr_deg >= lock - 1e-12 or mu * math.tan(math.radians(alr_deg)) >= 1:
        Wr = float("inf")
    else:
        tr = math.tan(math.radians(alr_deg))
        Wr = P * (mu + tr) / (1 - mu * tr)
    return W, lock, Wr


def snap(i):
    mode = i.get("mode", "const")
    o = {}
    l = i.get("l", 15.0)
    h = i.get("h", 2.0)
    b = i.get("b", 6.0)
    y = i.get("y", 1.0)
    eps_perm = i["eps_perm"]
    mu = i["mu"]
    Es = i.get("Es")
    pts = i.get("pts")
    if mode in ("const", "thick", "width"):
        ratio = i.get("ratio", 0.5)
        Ky, phimax, tc = beam_factor(mode, ratio)
        eps = y * h / (Ky * l ** 2)
        if pts:
            Es = interp_es(pts, eps)
        P = b * h ** 2 * Es * eps / (6 * l * phimax)
        o.update(K_y=Ky, phi_max=phimax, eps=eps * 100, Es=Es, P=P, y_perm=Ky * eps_perm * l ** 2 / h)
        if tc is not None:
            o["t_crit"] = tc
        e_used = eps
    elif mode == "ring":
        D, dD, Kann = i["D"], i["dD"], i["K_ann"]
        KF = i.get("K_F", 1.0)
        e_nom = dD / D
        e_in = dD / (D - h)
        e_used = max(Kann * e_nom, e_in)
        if pts:
            Es = interp_es(pts, e_nom)
        P = KF * 2 * PI * Es * e_nom * h * b
        o.update(eps_nom=e_nom * 100, eps_inner=e_in * 100, eps=e_used * 100, K_F=KF, Es=Es, P=P,
                 y_perm=eps_perm * min(D / Kann, D - h))
    elif mode == "torsion":
        r, a, nu = i["r"], i["a"], i["nu"]
        n, leff = (2, l / 2) if i.get("both") else (1, l)
        phi = y / a
        gamma = phi * r / leff
        e_used = gamma / (1 + nu)
        if pts:
            Es = interp_es(pts, e_used)
        G = Es / (2 * (1 + nu))
        T = n * G * (PI * r ** 4 / 2) * phi / leff / 1000.0      # N*m
        P = T * 1000.0 / a
        gperm = eps_perm * (1 + nu)
        o.update(l_eff=leff, phi=math.degrees(phi), gamma=gamma, eps=e_used * 100, gamma_perm=gperm,
                 G_sec=G, T_bar=T, P=P, y_perm=gperm * leff * a / r, Es=Es)
    o["strain_util"] = e_used / eps_perm
    W, lock, Wr = joining(P, mu, i.get("alpha", 30.0), i.get("alpha_r", 60.0))
    o.update(assembly_force=W, alpha_lock=lock, retention_force=Wr)
    return o


# --------------------------------------------------------------------- KATs ---
BASE = dict(mu=0.3, alpha=30, alpha_r=60)
RING = dict(mode="ring", b=5.0, D=30.0, dD=0.4, Es=2400, eps_perm=0.04, **BASE)
TOR = dict(mode="torsion", l=20.0, r=2.0, a=10.0, y=1.5, nu=0.38, Es=2000, eps_perm=0.03, **BASE)
# (case, inputs, [(symbol, expected, unit, tol)])
KATS = [
    ("ka-constant", dict(l=15, h=2, b=6, y=1, Es=2400, eps_perm=0.04, **BASE), [
        ("K_y", 2.0 / 3.0, "", 1e-12), ("eps", 1.33333333333, "%", 1e-9), ("P", 8.53333333333, "N", 1e-9),
        ("strain_util", 0.333333333333, "", 1e-9), ("y_perm", 3, "mm", 1e-9),
        ("assembly_force", 9.05511405892, "N", 1e-9), ("alpha_lock", 73.3007557660, "deg", 1e-9),
        ("retention_force", 36.0964135774, "N", 1e-9)]),
    ("ka-taper-thickness", dict(mode="thick", ratio=0.5, l=15, h=2, b=6, y=1, Es=2400, eps_perm=0.04, **BASE), [
        ("K_y", 1.09035488896, "", 1e-9), ("phi_max", 1, "", 1e-12), ("eps", 0.815228966174, "%", 1e-9),
        ("P", 5.21746538331, "N", 1e-9), ("strain_util", 0.203807241544, "", 1e-9), ("y_perm", 4.90659700032, "mm", 1e-9),
        ("assembly_force", 5.53649345463, "N", 1e-9), ("retention_force", 22.0701314424, "N", 1e-9)]),
    ("ka-taper-thickness-deep", dict(mode="thick", ratio=0.3, l=20, h=2.5, b=8, y=1.2, Es=2200, eps_perm=0.05,
                                     mu=0.25, alpha=25, alpha_r=70), [
        ("t_crit", 0.571428571429, "", 1e-9), ("phi_max", 1.19047619048, "", 1e-9), ("K_y", 1.26843822527, "", 1e-9),
        ("eps", 0.591278301977, "%", 1e-9), ("P", 4.55284292522, "N", 1e-9), ("strain_util", 0.118255660395, "", 1e-9),
        ("y_perm", 10.1475058022, "mm", 1e-9), ("assembly_force", 3.69159048182, "N", 1e-9),
        ("retention_force", 43.5825878892, "N", 1e-9)]),
    ("ka-taper-width", dict(mode="width", ratio=0.25, l=12, h=1.5, b=8, y=0.8, Es=3000, eps_perm=0.03,
                            mu=0.35, alpha=35, alpha_r=45), [
        ("K_y", 0.855198329221, "", 1e-9), ("eps", 0.974432836057, "%", 1e-9), ("P", 7.30824627042, "N", 1e-9),
        ("strain_util", 0.324810945350, "", 1e-9), ("y_perm", 2.46297118816, "mm", 1e-9),
        ("assembly_force", 10.1667732745, "N", 1e-9), ("retention_force", 15.1786653309, "N", 1e-9)]),
    ("ka-annular", dict(RING, h=1.5, K_ann=1.2), [
        ("eps_nom", 1.33333333333, "%", 1e-9), ("eps", 1.6, "%", 1e-9), ("eps_inner", 1.40350877193, "%", 1e-9),
        ("K_F", 1, "", 1e-12), ("P", 1507.96447372, "N", 1e-9), ("strain_util", 0.4, "", 1e-9), ("y_perm", 1, "mm", 1e-9),
        ("assembly_force", 1600.17073903, "N", 1e-9), ("retention_force", 6378.76280900, "N", 1e-9)]),
    ("ka-annular-inner-surface", dict(RING, h=3.0, K_ann=1.0), [
        ("eps_nom", 1.33333333333, "%", 1e-9), ("eps_inner", 1.48148148148, "%", 1e-9), ("eps", 1.48148148148, "%", 1e-9),
        ("strain_util", 0.370370370370, "", 1e-9), ("y_perm", 1.08, "mm", 1e-9), ("P", 3015.92894745, "N", 1e-9),
        ("assembly_force", 3200.34147805, "N", 1e-9), ("retention_force", 12757.5256180, "N", 1e-9)]),
    ("ka-annular-force-factor", dict(RING, h=1.5, K_ann=1.2, K_F=1.25), [
        ("eps", 1.6, "%", 1e-9), ("K_F", 1.25, "", 1e-12), ("P", 1884.95559215, "N", 1e-9), ("strain_util", 0.4, "", 1e-9),
        ("assembly_force", 2000.21342378, "N", 1e-9), ("retention_force", 7973.45351125, "N", 1e-9)]),
    ("ka-torsion", dict(TOR), [
        ("phi", 8.59436692696, "deg", 1e-9), ("gamma", 0.015, "", 1e-9), ("eps", 1.08695652174, "%", 1e-9),
        ("gamma_perm", 0.0414, "", 1e-9), ("strain_util", 0.362318840580, "", 1e-9), ("G_sec", 724.637681159, "MPa", 1e-9),
        ("T_bar", 0.136590984939, "N*m", 1e-9), ("P", 13.6590984939, "N", 1e-9), ("y_perm", 4.14, "mm", 1e-9),
        ("assembly_force", 14.4943001723, "N", 1e-9), ("retention_force", 57.7786486322, "N", 1e-9)]),
    ("ka-torsion-both-ends", dict(TOR, both=True), [
        ("l_eff", 10, "mm", 1e-9), ("gamma", 0.03, "", 1e-9), ("eps", 2.17391304348, "%", 1e-9),
        ("strain_util", 0.724637681159, "", 1e-9), ("T_bar", 0.546363939755, "N*m", 1e-9), ("P", 54.6363939755, "N", 1e-9),
        ("y_perm", 2.07, "mm", 1e-9), ("assembly_force", 57.9772006894, "N", 1e-9),
        ("retention_force", 231.114594529, "N", 1e-9)]),
    ("ka-curve", dict(l=15, h=2, b=6, y=1.5, pts=[(0.005, 2600), (0.01, 2400), (0.03, 1800)], eps_perm=0.04, **BASE), [
        ("eps", 2, "%", 1e-9), ("Es", 2100, "MPa", 1e-9), ("P", 11.2, "N", 1e-9), ("strain_util", 0.5, "", 1e-9)]),
]


def run_kats():
    ok = True
    print("%-26s %-16s %16s %16s %10s %8s  %s" % ("case", "symbol", "KAT expected", "independent", "rel diff", "KAT tol", "result"))
    n = m = 0
    for case, inp, exp in KATS:
        res = snap(inp)
        for sym, e, unit, tol in exp:
            v = res[sym]
            rd = abs(v - e) / abs(e) if e != 0 else abs(v)
            good = rd <= max(tol, 1e-6)
            n += 1
            m += good
            ok &= good
            print("%-26s %-16s %16.9g %16.9g %10.2e %8.0e  %s" % (case, sym + (" [" + unit + "]" if unit else ""), e, v, rd, tol, "PASS" if good else "FAIL"))
    print("KATs matched: %d/%d" % (m, n))
    return ok


# -------------------------------------------------------------- extra checks ---
EXTRAS = [   # (label, inputs, symbol, product value as printed by the probe tool, 10 significant digits)
    ('typical constant section', {'l': 12, 'h': 1.2, 'b': 5, 'y': 0.8, 'Es': 1800, 'eps_perm': 0.03, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'eps', 1.0),
    ('typical constant section', {'l': 12, 'h': 1.2, 'b': 5, 'y': 0.8, 'Es': 1800, 'eps_perm': 0.03, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'P', 1.8),
    ('typical constant section', {'l': 12, 'h': 1.2, 'b': 5, 'y': 0.8, 'Es': 1800, 'eps_perm': 0.03, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'strain_util', 0.3333333333),
    ('typical constant section', {'l': 12, 'h': 1.2, 'b': 5, 'y': 0.8, 'Es': 1800, 'eps_perm': 0.03, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'y_perm', 2.4),
    ('typical constant section', {'l': 12, 'h': 1.2, 'b': 5, 'y': 0.8, 'Es': 1800, 'eps_perm': 0.03, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'assembly_force', 1.916899809),
    ('typical constant section', {'l': 12, 'h': 1.2, 'b': 5, 'y': 0.8, 'Es': 1800, 'eps_perm': 0.03, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'retention_force', float('inf')),
    ('slender l/h<5 (warns)', {'l': 4, 'h': 1.5, 'b': 5, 'y': 0.2, 'Es': 1800, 'eps_perm': 0.03, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'eps', 2.8125),
    ('slender l/h<5 (warns)', {'l': 4, 'h': 1.5, 'b': 5, 'y': 0.2, 'Es': 1800, 'eps_perm': 0.03, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'P', 23.73046875),
    ('thickness taper r=0.49', {'mode': 'thick', 'ratio': 0.49, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'K_y', 1.104711882),
    ('thickness taper r=0.49', {'mode': 'thick', 'ratio': 0.49, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'phi_max', 1.00040016),
    ('thickness taper r=0.49', {'mode': 'thick', 'ratio': 0.49, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 't_crit', 0.03921568627),
    ('thickness taper r=0.49', {'mode': 'thick', 'ratio': 0.49, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'eps', 0.8046341344),
    ('thickness taper r=0.49', {'mode': 'thick', 'ratio': 0.49, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'P', 5.147598597),
    ('thickness taper r=0.99', {'mode': 'thick', 'ratio': 0.99, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'K_y', 0.6717070029),
    ('thickness taper r=0.99', {'mode': 'thick', 'ratio': 0.99, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'P', 8.469301145),
    ('thickness taper r=0.975', {'mode': 'thick', 'ratio': 0.975, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'K_y', 0.6794219891),
    ('thickness taper r=0.975', {'mode': 'thick', 'ratio': 0.975, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'P', 8.373130367),
    ('thickness taper r=1', {'mode': 'thick', 'ratio': 1.0, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'K_y', 0.6666666667),
    ('thickness taper r=0.05', {'mode': 'thick', 'ratio': 0.05, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'K_y', 0.7066957445),
    ('thickness taper r=0.05', {'mode': 'thick', 'ratio': 0.05, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'phi_max', 5.263157895),
    ('thickness taper r=0.05', {'mode': 'thick', 'ratio': 0.05, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 't_crit', 0.9473684211),
    ('thickness taper r=0.05', {'mode': 'thick', 'ratio': 0.05, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'P', 1.529496813),
    ('thickness taper r=0.05', {'mode': 'thick', 'ratio': 0.05, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'y_perm', 3.18013085),
    ('width taper r=0.02', {'mode': 'width', 'ratio': 0.02, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'K_y', 0.9820840152),
    ('width taper r=0.02', {'mode': 'width', 'ratio': 0.02, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'P', 5.792670282),
    ('width taper r=0.02', {'mode': 'width', 'ratio': 0.02, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'y_perm', 4.419378068),
    ('width taper r=0.5', {'mode': 'width', 'ratio': 0.5, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'K_y', 0.7725887222),
    ('width taper r=0.5', {'mode': 'width', 'ratio': 0.5, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'P', 7.363411768),
    ('width taper r=0.99', {'mode': 'width', 'ratio': 0.99, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'K_y', 0.6683400335),
    ('width taper r=0.99', {'mode': 'width', 'ratio': 0.99, 'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.4, 'alpha': 25, 'alpha_r': 70}, 'P', 8.511967866),
    ('mu=1 alpha=44.9 (just below jam)', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 1.0, 'alpha': 44.9, 'alpha_r': 20}, 'assembly_force', 4889.234887),
    ('mu=1 alpha=44.9 (just below jam)', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 1.0, 'alpha': 44.9, 'alpha_r': 20}, 'retention_force', 18.29979239),
    ('mu=0.01 alpha=89', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.01, 'alpha': 89, 'alpha_r': 20}, 'assembly_force', 1144.83548),
    ('mu=0.01 alpha=89', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.01, 'alpha': 89, 'alpha_r': 20}, 'retention_force', 3.20287016),
    ('mu=0.5 alpha_r=63.43 (just below lock)', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.5, 'alpha': 20, 'alpha_r': 63.43}, 'retention_force', 98796.01507),
    ('mu=0.5 alpha_r=63.43 (just below lock)', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.5, 'alpha': 20, 'alpha_r': 63.43}, 'alpha_lock', 63.43494883),
    ('mu=0.5 alpha_r=63.44 (locked)', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.5, 'alpha': 20, 'alpha_r': 63.44}, 'retention_force', float('inf')),
    ('mu=0 alpha_r=89.9', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.0, 'alpha': 20, 'alpha_r': 89.9}, 'retention_force', 4889.234887),
    ('mu=0 alpha_r=89.9', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.0, 'alpha': 20, 'alpha_r': 89.9}, 'assembly_force', 3.105879332),
    ('mu=0 alpha_r=90 (locked)', {'l': 15, 'h': 2, 'b': 6, 'y': 1, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.0, 'alpha': 20, 'alpha_r': 90}, 'retention_force', float('inf')),
    ('y=20 mm (strain far over limit)', {'l': 15, 'h': 2, 'b': 6, 'y': 20, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'eps', 26.66666667),
    ('y=20 mm (strain far over limit)', {'l': 15, 'h': 2, 'b': 6, 'y': 20, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'P', 170.6666667),
    ('y=20 mm (strain far over limit)', {'l': 15, 'h': 2, 'b': 6, 'y': 20, 'Es': 2400, 'eps_perm': 0.04, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'strain_util', 6.666666667),
    ('Es curve, strain below points', {'l': 15, 'h': 2, 'b': 6, 'y': 0.1, 'pts': [(0.005, 2600), (0.01, 2400), (0.03, 1800)], 'eps_perm': 0.04, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'Es', 2600.0),
    ('Es curve, strain below points', {'l': 15, 'h': 2, 'b': 6, 'y': 0.1, 'pts': [(0.005, 2600), (0.01, 2400), (0.03, 1800)], 'eps_perm': 0.04, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'P', 0.9244444444),
    ('Es curve, strain above points', {'l': 15, 'h': 2, 'b': 6, 'y': 3, 'pts': [(0.005, 2600), (0.01, 2400), (0.03, 1800)], 'eps_perm': 0.04, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'Es', 1800.0),
    ('Es curve, strain above points', {'l': 15, 'h': 2, 'b': 6, 'y': 3, 'pts': [(0.005, 2600), (0.01, 2400), (0.03, 1800)], 'eps_perm': 0.04, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'P', 19.2),
    ('Es curve unsorted, zero-strain point', {'l': 15, 'h': 2, 'b': 6, 'y': 0.5, 'pts': [(0.03, 1800), (0.0, 2800), (0.01, 2400)], 'eps_perm': 0.04, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'Es', 2533.333333),
    ('Es curve unsorted, zero-strain point', {'l': 15, 'h': 2, 'b': 6, 'y': 0.5, 'pts': [(0.03, 1800), (0.0, 2800), (0.01, 2400)], 'eps_perm': 0.04, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'P', 4.503703704),
    ('ring K_ann=1 (inner surface governs)', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'eps_inner', 1.315789474),
    ('ring K_ann=1 (inner surface governs)', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'P', 1884.955592),
    ('ring K_ann=1 (inner surface governs)', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'strain_util', 0.4385964912),
    ('ring K_ann=1 (inner surface governs)', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'y_perm', 1.14),
    ('ring K_ann=1 (inner surface governs)', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'assembly_force', 2000.213424),
    ('ring K_ann=1 (inner surface governs)', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'retention_force', 7973.453511),
    ('ring thick wall D=5 h=3', {'mode': 'ring', 'D': 5, 'dD': 0.1, 'h': 3, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'eps', 5.0),
    ('ring thick wall D=5 h=3', {'mode': 'ring', 'D': 5, 'dD': 0.1, 'h': 3, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'P', 4523.893421),
    ('ring thick wall D=5 h=3', {'mode': 'ring', 'D': 5, 'dD': 0.1, 'h': 3, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'y_perm', 0.06),
    ('ring K_ann=2, K_F=0.5', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 2.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'K_F': 0.5}, 'eps', 2.5),
    ('ring K_ann=2, K_F=0.5', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 2.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'K_F': 0.5}, 'P', 942.4777961),
    ('ring K_ann=2, K_F=0.5', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 2.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'K_F': 0.5}, 'y_perm', 0.6),
    ('ring K_ann=2, K_F=0.5', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': 1500, 'eps_perm': 0.03, 'K_ann': 2.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'K_F': 0.5}, 'strain_util', 0.8333333333),
    ('ring Es curve', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': None, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'pts': [(0.005, 1800), (0.02, 1200)]}, 'Es', 1500.0),
    ('ring Es curve', {'mode': 'ring', 'D': 40, 'dD': 0.5, 'h': 2, 'b': 8, 'Es': None, 'eps_perm': 0.03, 'K_ann': 1.0, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'pts': [(0.005, 1800), (0.02, 1200)]}, 'P', 1884.955592),
    ('torsion one end', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.4, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'phi', 12.73239545),
    ('torsion one end', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.4, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'G_sec', 642.8571429),
    ('torsion one end', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.4, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'T_bar', 0.09422623726),
    ('torsion one end', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.4, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'P', 10.46958192),
    ('torsion one end', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.4, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'y_perm', 6.125),
    ('torsion one end', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.4, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'strain_util', 0.3265306122),
    ('torsion one end', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.4, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'assembly_force', 11.10975685),
    ('torsion both ends nu=0.45', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.45, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'both': True}, 'eps', 2.206896552),
    ('torsion both ends nu=0.45', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.45, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'both': True}, 'T_bar', 0.3639082267),
    ('torsion both ends nu=0.45', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.45, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'both': True}, 'P', 40.43424741),
    ('torsion both ends nu=0.45', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.45, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'both': True}, 'y_perm', 3.171875),
    ('torsion Es curve', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.4, 'Es': None, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'pts': [(0.0, 2200), (0.01, 1900), (0.04, 1100)]}, 'Es', 1861.904762),
    ('torsion Es curve', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.4, 'Es': None, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60, 'pts': [(0.0, 2200), (0.01, 1900), (0.04, 1100)]}, 'P', 10.8296469),
    ('torsion nu=-0.5', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': -0.5, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'gamma_perm', 0.0175),
    ('torsion nu=-0.5', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': -0.5, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'P', 29.31482937),
    ('torsion nu=0.499', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.499, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'G_sec', 600.4002668),
    ('torsion nu=0.499', {'mode': 'torsion', 'l': 25, 'r': 1.8, 'a': 9, 'y': 2, 'nu': 0.499, 'Es': 1800, 'eps_perm': 0.035, 'mu': 0.3, 'alpha': 30, 'alpha_r': 60}, 'P', 9.778128542),
]


def run_extras():
    print()
    print("Extra checks (product values observed with the probe tool)")
    print("%-40s %-14s %14s %14s %10s  %s" % ("probe", "symbol", "product", "independent", "rel diff", "result"))
    for label, inp, sym, prod in EXTRAS:
        tol = 1e-8
        v = snap(inp)[sym]
        if prod is None:
            print("%-40s %-14s %14s %14.8g %10s  INFO" % (label, sym, "-", v, "-"))
            continue
        if math.isinf(v) and math.isinf(prod):
            print("%-40s %-14s %14s %14s %10s  PASS" % (label, sym, "inf", "inf", "0"))
            continue
        rd = abs(v - prod) / abs(v) if v != 0 else abs(prod)
        print("%-40s %-14s %14.8g %14.8g %10.2e  %s" % (label, sym, prod, v, rd, "PASS" if rd <= tol else "FAIL"))


def double_integration_ky(mode, ratio, n=200000):
    """K_y from the moment-curvature relation integrated twice (no closed form, no unit-load theorem).
    Unit tip load, l = h = b = E = 1 at the root. Returns K_y = y*h/(eps*l^2) using the peak surface strain."""
    import numpy as np
    a = 1.0 - ratio
    t = np.linspace(0.0, 1.0, n + 1)
    M = 1.0 - t                                        # P = 1, l = 1
    if mode == "thick":
        hh, bb = 1 - a * t, 1.0
    elif mode == "width":
        hh, bb = 1.0, 1 - a * t
    else:
        hh, bb = 1.0, 1.0
    I = bb * hh ** 3 / 12.0
    kappa = M / I                                      # curvature, E = 1
    eps = M * (hh / 2.0) / I                           # surface strain
    dt = t[1] - t[0]
    slope = np.concatenate(([0.0], np.cumsum(0.5 * (kappa[1:] + kappa[:-1]) * dt)))
    defl = np.concatenate(([0.0], np.cumsum(0.5 * (slope[1:] + slope[:-1]) * dt)))
    return defl[-1] / eps.max()                        # y*h/(eps*l^2) with h = 1, l = 1


def elastica_ratios(y_over_l):
    """Large-deflection check (INFO): cantilever with a tip load of fixed direction, exact elastica.
    Returns (strain_true/strain_linear, force_true/force_linear) at the same tip deflection."""
    from scipy.integrate import solve_ivp
    from scipy.optimize import brentq

    def tip(alpha):
        f = lambda s, u: [u[1], -alpha * math.cos(u[0]), math.cos(u[0]), math.sin(u[0])]
        end = lambda k0: solve_ivp(f, [0, 1], [0, k0, 0, 0], rtol=1e-11, atol=1e-13).y[:, -1]
        k0 = brentq(lambda k: end(k)[1], 0, 1.5 * alpha + 1e-9)
        return k0, end(k0)
    alpha = brentq(lambda a_: tip(a_)[1][3] - y_over_l, 1e-6, 6.0)
    k0, u = tip(alpha)
    lin = 3.0 * y_over_l                               # linear theory: curvature at root = force = 3 y/l (EI = l = 1)
    return k0 / lin, alpha / lin


def self_checks():
    """Cross-checks of the method itself (closed forms, energy balance)."""
    for mode, ratio, ref in (("const", 1.0, 2 / 3), ("thick", 0.5, 1.09035488896), ("width", 0.25, 0.855198329221)):
        assert abs(double_integration_ky(mode, ratio) - ref) / ref < 1e-6, (mode, ratio)
    # thick, steep taper: peak strain inside the beam; K_y = y*h/(eps_max*l^2) from the curvature integral
    assert abs(double_integration_ky("thick", 0.3) - 1.26843822527) < 1e-5
    r = 0.5
    a = 1 - r
    j_thick = (2 * r - r * r / 2 - math.log(r) - 1.5) / a ** 3
    assert abs(2 * j_thick - beam_factor("thick", 0.5)[0]) < 1e-9
    j_w = (0.5 - 2 * r + 1.5 * r * r - r * r * math.log(r)) / a ** 3
    assert abs(2 * j_w - beam_factor("width", 0.5)[0]) < 1e-9
    assert abs(beam_factor("thick", 1 - 1e-6)[0] - 2 / 3) < 1e-5
    # torsion strain energy: U = G*gamma^2*pi*r^2*L/4 for the bar held at one end
    o = snap(TOR)
    G, g, r_, L = o["G_sec"], o["gamma"], TOR["r"], TOR["l"]
    U = G * g ** 2 * PI * r_ ** 2 * L / 4
    assert abs(o["P"] * TOR["y"] / 2 - U) / U < 1e-9
    # ring strain energy: work of the radial force over the radial growth dD/2
    ring = snap(dict(RING, h=1.5, K_ann=1.2))
    Ering = 0.5 * 2400 * (0.4 / 30) ** 2 * PI * 30 * 1.5 * 5
    assert abs(ring["P"] * (0.4 / 2) / 2 - Ering) / Ering < 1e-9


if __name__ == "__main__":
    self_checks()
    allok = run_kats()
    run_extras()
    print()
    print("INFO large-deflection check (exact elastica, fixed-direction tip load) against the linear theory of the sheet")
    print("%8s %26s %26s" % ("y/l", "true/linear root strain", "true/linear force"))
    for yl in (0.1, 0.2, 0.3, 0.4, 0.5):
        es, fs = elastica_ratios(yl)
        print("%8.2f %26.4f %26.4f" % (yl, es, fs))
    print("INFO example l = 20, h = 1.2, y = 8.2 mm (y/l = 0.41): linear strain %.2f %%; true strain about %.2f %%"
          % (100 * 1.5 * 1.2 * 8.2 / 400, 100 * 1.5 * 1.2 * 8.2 / 400 * elastica_ratios(0.41)[0]))
    sys.exit(0 if allok else 1)
