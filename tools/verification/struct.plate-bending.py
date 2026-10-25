#!/usr/bin/env python3
"""Independent re-derivation of struct.plate-bending (Track P, P1).

Method sources (cited by title and chapter only; no text or tables copied):
  Timoshenko and Woinowsky-Krieger, Theory of Plates and Shells, 2nd ed.: Ch. 3 (circular plates),
    Ch. 5 (Navier solution), Ch. 6 (clamped rectangle; coefficients used only as a sanity check).
  Roark's Formulas for Stress and Strain, 8th ed., Table 11.2 (circular) and 11.4 (rectangular): cross-check only.
  Leissa, Vibration of Plates (NASA SP-160): frequency parameters, used as a sanity check.
  BS EN 1993-1-1:2005+A1:2014, 6.2.1(5): yield criterion used by the utilisation.
How each part is built here, independently of the product:
  Circular, any axisymmetric load: statics gives the shear, then two integrations (numerical quadrature)
    with the edge conditions fixed by a linear solve. No closed form of the product is used for patch and ring.
  Rectangular simply supported: Navier double series, summed with numpy to 2048 odd terms per direction
    and extrapolated from the last two sums.
  Rectangular clamped: Ritz with clamped-clamped beam functions (a different function space from the
    product's polynomial basis), 24 symmetric modes per direction; also used for the frequency.
  Circular frequency: determinant of the two-term Bessel mode shape, solved with a root finder.
Run: python3 tools/verification/struct.plate-bending.py
Exit code: 0 when every known-answer value matches within max(KAT tolerance, 1e-6), else 1.
Extra checks print PASS, FAIL or INFO. A FAIL there is a product finding and does not change the exit code.
Product values in the extra checks were observed with the probe tool on the release/v0.26.0 build.
Units: N, mm, MPa for stress; moments in N*m/m (= N*mm/mm); frequency in Hz.
"""
import math
import sys
import numpy as np
from scipy.integrate import quad
from scipy.optimize import brentq, minimize_scalar
from scipy.special import jv, iv

PI = math.pi


def rigidity(E_GPa, nu, t):
    """D in N*mm (E in MPa, t in mm)."""
    return E_GPa * 1e3 * t ** 3 / (12 * (1 - nu ** 2))


def vm(sx, sy, txy=0.0):
    return math.sqrt(sx * sx - sx * sy + sy * sy + 3 * txy * txy)


# ------------------------------------------------------------ circular plate ---
def circ_axisym(a, D, nu, clamped, kind, q=0.0, c=0.0, F=0.0):
    """Axisymmetric plate by statics and two quadratures. kind: 'uniform', 'patch' (radius c), 'ring' (radius c, force F).
    w positive down. Returns functions of r: w, Mr, Mth and the centre deflection.
    theta = laplacian(w);  D * r * theta' = F(r)/(2 pi)  with F(r) the load inside radius r."""
    if kind == "uniform":
        g = lambda s: q * s * s / (4 * D)
    elif kind == "patch":
        g = lambda s: q * s * s / (4 * D) if s <= c else q * c * c / (4 * D) + q * c * c / (2 * D) * math.log(s / c)
    elif kind == "ring":
        g = lambda s: 0.0 if s <= c else F / (2 * PI * D) * math.log(s / c)
    else:
        raise ValueError(kind)
    # w'(r) = theta0*r/2 + S(r)/r,  S(r) = int_0^r s*g(s) ds ; theta(r) = theta0 + g(r)
    pts = [c] if kind != "uniform" else None

    def S(r):
        return quad(lambda s: s * g(s), 0, r, points=([c] if (pts and c < r) else None), epsabs=1e-13, epsrel=1e-13, limit=200)[0]

    Sa = S(a)
    if clamped:
        th0 = -2 * Sa / a ** 2                      # w'(a) = 0
    else:
        # M_r(a) = 0  <=>  theta(a) = (1 - nu) * w'(a)/a ;  w'(a) = th0*a/2 + Sa/a
        # th0 + g(a) = (1-nu)*(th0/2 + Sa/a^2)
        th0 = ((1 - nu) * Sa / a ** 2 - g(a)) / (1 - (1 - nu) / 2)

    def wp(r):
        return th0 * r / 2 + (S(r) / r if r > 0 else 0.0)

    def moments(r):
        th = th0 + g(r)
        slope_r = (th0 / 2 + S(r) / r ** 2) if r > 0 else th0 / 2 + 0.0   # w'/r (limit at centre is theta0/2)
        Mr = -D * (th - (1 - nu) * slope_r)
        Mt = -D * (nu * th + (1 - nu) * slope_r)
        return Mr, Mt

    w0 = -quad(wp, 0, a, points=([c] if kind != "uniform" else None), epsabs=1e-14, epsrel=1e-13, limit=200)[0]
    return w0, moments


def circ_scan(a, t, moments, extra=()):
    rs = sorted(set(list(np.linspace(0, a, 801)) + list(extra)))
    best = (0, None)
    bp = 0
    for r in rs:
        Mr, Mt = moments(r)
        sr, st = 6 * Mr / t ** 2, 6 * Mt / t ** 2
        v = vm(sr, st)
        if v > best[0]:
            best = (v, r)
        bp = max(bp, abs(sr), abs(st))
    # refine the best point
    r0 = best[1]
    lo, hi = max(0.0, r0 - a / 800), min(a, r0 + a / 800)
    res = minimize_scalar(lambda r: -vm(6 * moments(r)[0] / t ** 2, 6 * moments(r)[1] / t ** 2), bounds=(lo, hi), method="bounded", options={"xatol": 1e-10})
    v = max(best[0], -res.fun)
    return v, bp


def circ_freq_lambda2(clamped, nu):
    """Lowest axisymmetric frequency parameter: w = A J0(kr) + B I0(kr) on r in [0,1]."""
    def det(k):
        J0, J1, I0, I1 = jv(0, k), jv(1, k), iv(0, k), iv(1, k)
        if clamped:       # w = 0 and w' = 0
            return J0 * (k * I1) - I0 * (-k * J1)
        wpJ, wpI = -k * J1, k * I1
        wppJ, wppI = -k * k * (J0 - J1 / k), k * k * (I0 - I1 / k)
        return J0 * (wppI + nu * wpI) - I0 * (wppJ + nu * wpJ)
    ks = np.linspace(0.5, 4.0, 700)
    vals = [det(k) for k in ks]
    for k0, k1, v0, v1 in zip(ks, ks[1:], vals, vals[1:]):
        if v0 * v1 < 0:
            k = brentq(det, k0, k1, xtol=1e-14)
            return k * k
    raise RuntimeError("no root")


def circle(case):
    a, t, q, E, nu, rho = 250.0, 5.0, 0.02, 200.0, 0.3, 7850.0
    D = rigidity(E, nu, t)
    clamped = case["clamped"]
    kind = case["kind"]
    c = case.get("c", 0.0)
    P = case.get("F", 0.0)
    w0, mom = circ_axisym(a, D, nu, clamped, kind, q=q, c=c, F=P)
    vmax, pmax = circ_scan(a, t, mom, extra=[c] if c else [])
    lam2 = circ_freq_lambda2(clamped, nu)
    Dsi = D / 1000.0                                   # N*m
    f1 = lam2 / (2 * PI * (a / 1000) ** 2) * math.sqrt(Dsi / (rho * t / 1000))
    return dict(D=Dsi, w_max=w0, sigma_max=vmax, sigma_principal=pmax, f1=f1, lambda2=lam2)


# -------------------------------------------------------- rectangular, Navier ---
def navier(a, b, t, E, nu, q, u=None, v=None, N=1024):
    """Return w_max (centre), M_x_c, M_y_c, |M_xy| at the corner, in N, mm. N = odd terms per direction."""
    D = rigidity(E, nu, t)
    k = np.arange(N)
    m = (2 * k + 1).astype(float)
    M, Nn = np.meshgrid(m, m, indexing="ij")
    sm = np.where(((M - 1) / 2) % 2 == 0, 1.0, -1.0)
    sn = np.where(((Nn - 1) / 2) % 2 == 0, 1.0, -1.0)
    qmn = 16 * q / (PI ** 2 * M * Nn)
    if u is not None:
        qmn = qmn * sm * sn * np.sin(M * PI * u / (2 * a)) * np.sin(Nn * PI * v / (2 * b))
    W = qmn / (PI ** 4 * D * (M ** 2 / a ** 2 + Nn ** 2 / b ** 2) ** 2)
    sc = sm * sn                                       # sin(m pi/2) sin(n pi/2)
    w = np.sum(W * sc)
    Mx = D * PI ** 2 * np.sum(W * (M ** 2 / a ** 2 + nu * Nn ** 2 / b ** 2) * sc)
    My = D * PI ** 2 * np.sum(W * (nu * M ** 2 / a ** 2 + Nn ** 2 / b ** 2) * sc)
    Mxy = D * (1 - nu) * PI ** 2 * np.sum(W * M * Nn / (a * b))
    return np.array([w, Mx, My, abs(Mxy)])


def navier_converged(a, b, t, E, nu, q, u=None, v=None):
    s1 = navier(a, b, t, E, nu, q, u, v, N=512)
    s2 = navier(a, b, t, E, nu, q, u, v, N=1024)
    s3 = navier(a, b, t, E, nu, q, u, v, N=2048)
    ext = s3 + (s3 - s2) / 3.0                          # tail of 1/N^2
    return ext, s3, s2, s1


# ------------------------------------------------- rectangular, clamped, Ritz ---
def beam_roots(n):
    """Roots beta of tan(beta/2) + tanh(beta/2) = 0 (symmetric clamped-clamped modes)."""
    roots = []
    for k in range(1, n + 1):
        lo = (4 * k - 3) * PI / 2 + 1e-9                # beta/2 in ((4k-3)pi/4 ...) -> solve in x = beta/2
        x_lo, x_hi = (4 * k - 3) * PI / 4 + 1e-9, (4 * k - 1) * PI / 4 + 0.3
        f = lambda x: math.sin(x) * math.cosh(x) + math.cos(x) * math.sinh(x)
        # bracket by scanning
        xs = np.linspace((4 * k - 3) * PI / 4 + 0.05, (4 * k + 1) * PI / 4 - 0.05, 400)
        for x0, x1 in zip(xs, xs[1:]):
            if f(x0) * f(x1) < 0:
                roots.append(2 * brentq(f, x0, x1, xtol=1e-15))
                break
    return roots


def beam_basis(n, xi):
    """Symmetric clamped-clamped beam functions on xi in [0,1]; returns phi, phi', phi'' arrays (n, len(xi))."""
    betas = beam_roots(n)
    s = xi - 0.5
    ph, d1, d2 = [], [], []
    for be in betas:
        half = be / 2
        e = np.exp(be * (np.abs(s) - 0.5))
        ch = e * (1 + np.exp(-2 * be * np.abs(s))) / (1 + np.exp(-be))      # cosh(be s)/cosh(be/2)
        sh = np.sign(s) * e * (1 - np.exp(-2 * be * np.abs(s))) / (1 + np.exp(-be))  # sinh(be s)/cosh(be/2)
        cc = np.cos(be * s) / math.cos(half)
        ss = np.sin(be * s) / math.cos(half)
        ph.append(cc - ch)
        d1.append(be * (-ss - sh))
        d2.append(be ** 2 * (-cc - ch))
    return np.array(ph), np.array(d1), np.array(d2), betas


def clamped_ritz(a, b, t, E, nu, rho, q, n=20, nx=None, ny=None):
    """Ritz with clamped-clamped beam functions: nx modes in x and ny modes in y (n for both when not given).
    Energy of a clamped plate: nu drops out of the stiffness (the cross terms combine by integration by parts)."""
    nx = nx or n
    ny = ny or n
    D = rigidity(E, nu, t)
    xg, wg = np.polynomial.legendre.leggauss(240)
    xi = 0.5 * (xg + 1)
    wt = 0.5 * wg
    px, d1x, d2x, _ = beam_basis(nx, xi)
    py, d1y, d2y, _ = beam_basis(ny, xi)
    Ax, Bx, Cx = (d2x * wt) @ d2x.T, (d1x * wt) @ d1x.T, (px * wt) @ px.T
    Ay, By, Cy = (d2y * wt) @ d2y.T, (d1y * wt) @ d1y.T, (py * wt) @ py.T
    K = D * a * b * (np.kron(Ax, Cy) / a ** 4 + np.kron(Cx, Ay) / b ** 4 + 2 * np.kron(Bx, By) / (a ** 2 * b ** 2))
    f = q * a * b * np.kron(px @ wt, py @ wt)
    c = np.linalg.solve(K, f)
    cm = c.reshape(nx, ny)
    pcx, _, d2cx, _ = beam_basis(nx, np.array([0.5]))
    pcy, _, d2cy, _ = beam_basis(ny, np.array([0.5]))
    pex, _, d2ex, _ = beam_basis(nx, np.array([0.0]))
    pey, _, d2ey, _ = beam_basis(ny, np.array([0.0]))
    w_c = pcx[:, 0] @ cm @ pcy[:, 0]
    wxx_c = d2cx[:, 0] @ cm @ pcy[:, 0] / a ** 2
    wyy_c = pcx[:, 0] @ cm @ d2cy[:, 0] / b ** 2
    Mx_c = -D * (wxx_c + nu * wyy_c)
    My_c = -D * (wyy_c + nu * wxx_c)
    # moment at the middle of the edge x = 0 (normal moment M_x) and of the edge y = 0 (normal moment M_y)
    M_edge_x = -D * (d2ex[:, 0] @ cm @ pcy[:, 0] / a ** 2)
    M_edge_y = -D * (pcx[:, 0] @ cm @ d2ey[:, 0] / b ** 2)
    return dict(w=w_c, Mx_c=Mx_c, My_c=My_c, M_edge=M_edge_x, M_edge_y=M_edge_y, D=D)


def clamped_extrapolated(a, b, t, E, nu, rho, q, n1=40, n2=48, ratio=1.0):
    """Ritz with beam functions at two orders. The moments converge like 1/n^2 (checked at n = 24, 32, 40, 48),
    so they are extrapolated from the two highest orders; the deflection is taken at the higher order.
    ratio: modes in x per mode in y (use about a/b for a long plate)."""
    r1 = clamped_ritz(a, b, t, E, nu, rho, q, nx=max(2, round(n1 * ratio)), ny=n1)
    r2 = clamped_ritz(a, b, t, E, nu, rho, q, nx=max(2, round(n2 * ratio)), ny=n2)
    f = (1 / n2 ** 2) / (1 / n1 ** 2 - 1 / n2 ** 2)
    out = dict(w=r2["w"], D=r2["D"])
    for k in ("Mx_c", "My_c", "M_edge", "M_edge_y"):
        out[k] = r2[k] + (r2[k] - r1[k]) * f
    return out


def clamped_freq_lambda2(n=16):
    """Lowest eigenvalue of a clamped square: omega = lambda2/a^2 * sqrt(D/(rho t))."""
    xg, wg = np.polynomial.legendre.leggauss(160)
    xi = 0.5 * (xg + 1)
    wt = 0.5 * wg
    ph, d1, d2, betas = beam_basis(n, xi)
    A = (d2 * wt) @ d2.T
    B = (d1 * wt) @ d1.T
    C = (ph * wt) @ ph.T
    K = np.kron(A, C) + np.kron(C, A) + 2 * np.kron(B, B)       # unit a, unit D
    M = np.kron(C, C)
    from scipy.linalg import eigh
    ev = eigh(K, M, eigvals_only=True, subset_by_index=[0, 0])
    return math.sqrt(ev[0])


def rect_ss_freq(a, b, E, nu, rho, t):
    D = rigidity(E, nu, t) / 1000.0
    am, bm, tm = a / 1000, b / 1000, t / 1000
    return PI ** 2 * (1 / am ** 2 + 1 / bm ** 2) * math.sqrt(D / (rho * tm)) / (2 * PI)


# ----------------------------------------------------------------------- KATs ---
KATS = []


def run_kats():
    ok = True
    n = m = 0
    print("%-30s %-16s %16s %16s %10s %8s  %s" % ("case", "symbol", "KAT expected", "independent", "rel diff", "KAT tol", "result"))

    def row(case, sym, exp, val, tol):
        nonlocal ok, n, m
        rd = abs(val - exp) / abs(exp)
        good = rd <= max(tol, 1e-6)
        n += 1
        m += good
        ok &= good
        print("%-30s %-16s %16.10g %16.10g %10.2e %8.0e  %s" % (case, sym, exp, val, rd, tol, "PASS" if good else "FAIL"))

    # circular
    cc = circle(dict(clamped=True, kind="uniform"))
    row("ka-circular-clamped-uniform", "D [N*m]", 2289.37728937729, cc["D"], 1e-9)
    row("ka-circular-clamped-uniform", "w_max [mm]", 0.533203125, cc["w_max"], 1e-9)
    row("ka-circular-clamped-uniform", "sigma_principal", 37.5, cc["sigma_principal"], 1e-9)
    row("ka-circular-clamped-uniform", "sigma_max", 37.5 * math.sqrt(1 - 0.3 + 0.09), cc["sigma_max"], 1e-9)
    row("ka-circular-clamped-uniform", "f1 [Hz]", 198.67922896885142, cc["f1"], 1e-8)
    cs = circle(dict(clamped=False, kind="uniform"))
    row("ka-circular-simple-uniform", "w_max [mm]", 2.173828125, cs["w_max"], 1e-9)
    row("ka-circular-simple-uniform", "sigma_principal", 61.875, cs["sigma_principal"], 1e-9)
    row("ka-circular-simple-uniform", "sigma_max", 61.875, cs["sigma_max"], 1e-9)
    row("ka-circular-simple-uniform", "f1 [Hz]", 95.97966768057351, cs["f1"], 1e-8)
    cp = circle(dict(clamped=False, kind="patch", c=100.0))
    row("ka-circular-simple-patch", "w_max [mm]", 0.733270526039671, cp["w_max"], 1e-9)
    row("ka-circular-simple-patch", "sigma_principal", 25.958135417236814, cp["sigma_principal"], 1e-7)
    row("ka-circular-simple-patch", "sigma_max", 25.958135417236814, cp["sigma_max"], 1e-7)
    cr = circle(dict(clamped=True, kind="ring", c=125.0, F=4000.0))
    row("ka-circular-clamped-ring", "w_max [mm]", 0.8764297443823545, cr["w_max"], 1e-9)
    row("ka-circular-clamped-ring", "sigma_principal", 57.29577951308233, cr["sigma_principal"], 1e-6)
    row("ka-circular-clamped-ring", "sigma_max", 50.92560276039233, cr["sigma_max"], 1e-6)

    # square, simply supported, uniform: a = b = 500, t = 8, q = 10 kPa, E = 210 GPa, nu = 0.3
    ext, s3, s2, s1 = navier_converged(500, 500, 8, 210, 0.3, 0.01)
    w, Mx, My, Mxy = ext
    Dm = rigidity(210, 0.3, 8)
    sig_c = vm(6 * Mx / 64, 6 * My / 64)
    sig_k = math.sqrt(3) * 6 * Mxy / 64
    row("ka-rect-simple-square-series", "w_max [mm]", 0.2578641825623815, w, 2e-6)
    row("ka-rect-simple-square-series", "M_x_c", 119.71594911499169, Mx, 2e-6)
    row("ka-rect-simple-square-series", "M_xy_k", 81.20587840546958, Mxy, 1e-6)
    row("ka-rect-simple-square-series", "sigma_max", 13.186189518664579, max(sig_c, sig_k), 1e-6)
    row("ka-rect-simple-square-book", "w_max [mm]", 0.25771484375, w, 1e-3)
    # natural frequency of 600 x 400 x 6 steel
    row("ka-rect-simple-natural", "f1 [Hz]", 133.17278663541322, rect_ss_freq(600, 400, 210, 0.3, 7850, 6), 1e-9)
    # patch 150 x 100 on 600 x 400 x 6, 50 kPa
    ext, *_ = navier_converged(600, 400, 6, 210, 0.3, 0.05, 150, 100)
    row("ka-rect-simple-patch", "w_max [mm]", 0.39813227096225886, ext[0], 2e-6)
    row("ka-rect-simple-patch", "M_x_c", 117.0624988680948, ext[1], 2e-6)
    row("ka-rect-simple-patch", "M_y_c", 152.72187253536302, ext[2], 2e-6)
    # clamped square (Ritz with beam functions)
    r = clamped_extrapolated(500, 500, 8, 210, 0.3, 7850, 0.01)
    row("ka-rect-clamped-square", "w_max [mm]", 0.08031810613887827, r["w"], 1e-6)
    row("ka-rect-clamped-square", "M_x_c", 57.262726987305946, r["Mx_c"], 1e-4)
    row("ka-rect-clamped-square", "M_edge", 128.33419152103113, abs(r["M_edge"]), 1e-3)
    row("ka-rect-clamped-square-book", "w_max [mm]", 0.07998046875, r["w"], 5e-3)
    lam2 = clamped_freq_lambda2(20)
    D = rigidity(210, 0.3, 8) / 1000.0
    f = lam2 / (2 * PI * 0.5 ** 2) * math.sqrt(D / (7850 * 0.008))
    row("ka-rect-clamped-natural", "f1 [Hz]", 286.85169688278467, f, 1e-6)
    print("KATs matched: %d/%d" % (m, n))
    return ok


EXTRAS = []


def run_extras():
    print()
    print("Extra checks (product values observed with the probe tool)")
    print("%-40s %-14s %14s %14s %10s  %s" % ("probe", "symbol", "product", "independent", "rel diff", "result"))
    for label, fn, sym, prod, tol in EXTRAS:
        v = fn()
        if prod is None:
            print("%-40s %-14s %14s %14.8g %10s  INFO" % (label, sym, "-", v, "-"))
            continue
        rd = abs(v - prod) / abs(v) if v != 0 else abs(prod)
        print("%-40s %-14s %14.8g %14.8g %10.2e  %s" % (label, sym, prod, v, rd, "PASS" if rd <= tol else "FAIL"))


if __name__ == "__main__":
    allok = run_kats()
    run_extras()
    sys.exit(0 if allok else 1)
