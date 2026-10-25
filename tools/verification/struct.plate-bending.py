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



def _clamped_solve(a, b, t, E, nu, q, nx, ny, u=None, v=None, grid=40):
    """One Ritz solve with beam functions: centre values, edge-middle moments and the largest von Mises
    moment on a quarter-plate grid. q in MPa. u, v: centred patch sizes (None = whole plate)."""
    D = rigidity(E, nu, t)
    xg, wg = np.polynomial.legendre.leggauss(240)
    xi = 0.5 * (xg + 1)
    wt = 0.5 * wg
    px, d1x, d2x, _ = beam_basis(nx, xi)
    py, d1y, d2y, _ = beam_basis(ny, xi)
    Ax, Bx, Cx = (d2x * wt) @ d2x.T, (d1x * wt) @ d1x.T, (px * wt) @ px.T
    Ay, By, Cy = (d2y * wt) @ d2y.T, (d1y * wt) @ d1y.T, (py * wt) @ py.T
    K = D * a * b * (np.kron(Ax, Cy) / a ** 4 + np.kron(Cx, Ay) / b ** 4 + 2 * np.kron(Bx, By) / (a ** 2 * b ** 2))

    def load(n, span):
        lo, hi = (0.0, 1.0) if span is None else (0.5 - span / 2, 0.5 + span / 2)
        ph, _, _, _ = beam_basis(n, lo + (hi - lo) * xi)
        return ph @ (wt * (hi - lo))
    f = q * a * b * np.kron(load(nx, None if u is None else u / a), load(ny, None if v is None else v / b))
    cm = np.linalg.solve(K, f).reshape(nx, ny)
    g = np.linspace(0, 0.5, grid + 1)
    pxg, d1xg, d2xg, _ = beam_basis(nx, g)
    pyg, d1yg, d2yg, _ = beam_basis(ny, g)
    wxx = (d2xg.T @ cm @ pyg) / a ** 2
    wyy = (pxg.T @ cm @ d2yg) / b ** 2
    wxy = (d1xg.T @ cm @ d1yg) / (a * b)
    Mx, My, Mxy = -D * (wxx + nu * wyy), -D * (wyy + nu * wxx), -D * (1 - nu) * wxy
    vmm = np.sqrt(Mx ** 2 - Mx * My + My ** 2 + 3 * Mxy ** 2)
    return dict(w=pxg[:, -1] @ cm @ pyg[:, -1], Mx_c=Mx[-1, -1], My_c=My[-1, -1],
                M_edge=max(abs(Mx[0, -1]), abs(My[-1, 0])), vm=vmm.max())


def clamped_case(a, b, t, E, nu, rho, q, u=None, v=None, n1=14, n2=20):
    """Reference values for a clamped rectangle: two orders, moments extrapolated with a 1/n^2 tail."""
    ar = a / b
    sol = [_clamped_solve(a, b, t, E, nu, q, max(2, round(n * max(ar, 1))), max(2, round(n * max(1 / ar, 1))), u, v) for n in (n1, n2)]
    fac = (1 / n2 ** 2) / (1 / n1 ** 2 - 1 / n2 ** 2)
    out = {k: sol[1][k] + (sol[1][k] - sol[0][k]) * fac for k in ("Mx_c", "My_c", "M_edge", "vm")}
    out["w"] = sol[1]["w"]
    # fundamental frequency from the eigenproblem at a small order (dense generalised eigenvalue problem)
    from scipy.linalg import eigh
    n = 12
    nx, ny = max(2, round(n * max(ar, 1))), max(2, round(n * max(1 / ar, 1)))
    D = rigidity(E, nu, t)
    xg, wg = np.polynomial.legendre.leggauss(240)
    xi, wt = 0.5 * (xg + 1), 0.5 * wg
    px, d1x, d2x, _ = beam_basis(nx, xi)
    py, d1y, d2y, _ = beam_basis(ny, xi)
    Ax, Bx, Cx = (d2x * wt) @ d2x.T, (d1x * wt) @ d1x.T, (px * wt) @ px.T
    Ay, By, Cy = (d2y * wt) @ d2y.T, (d1y * wt) @ d1y.T, (py * wt) @ py.T
    K = D * a * b * (np.kron(Ax, Cy) / a ** 4 + np.kron(Cx, Ay) / b ** 4 + 2 * np.kron(Bx, By) / (a ** 2 * b ** 2))
    Mm = rho * 1e-12 * t * a * b * np.kron(Cx, Cy)                    # kg/m^3 = 1e-12 N s^2/mm^4
    out["f1"] = math.sqrt(eigh(K, Mm, eigvals_only=True, subset_by_index=[0, 0])[0]) / (2 * PI)
    return out


_CACHE = {}


def independent(spec):
    """Independent values for one extra-check case, in the units the probe tool displays (mm, MPa, N*m/m, Hz)."""
    key = repr(sorted(spec.items()))
    if key in _CACHE:
        return _CACHE[key]
    s = spec
    if s["shape"] == "circular":
        a, t = s["a"], s["t"]
        D = rigidity(s["E"], s["nu"], t)
        clamped = s["support"] == "clamped"
        w0, mom = circ_axisym(a, D, s["nu"], clamped, s["kind"], q=s["q"] / 1000, c=s["c"], F=s["F"])
        vmax, pmax = circ_scan(a, t, mom, extra=[s["c"]] if s["c"] else [])
        lam2 = circ_freq_lambda2(clamped, s["nu"])
        f1 = lam2 / (2 * PI * (a / 1000) ** 2) * math.sqrt(D / 1000 / (s["rho"] * t / 1000))
        res = dict(w_max=w0, sigma_max=vmax, sigma_principal=pmax, f1=f1)
    elif s["support"] != "clamped":
        a, b, t, nu = s["a"], s["b"], s["t"], s["nu"]
        w, Mx, My, Mxy = navier_converged(a, b, t, s["E"], nu, s["q"] / 1000, s["u"], s["v"])[0]
        res = dict(w_max=w, M_x_c=Mx, M_y_c=My, M_xy_k=Mxy,
                   sigma_max=max(vm(6 * Mx / t ** 2, 6 * My / t ** 2), math.sqrt(3) * 6 * Mxy / t ** 2),
                   sigma_principal=6 * max(abs(Mx), abs(My), Mxy) / t ** 2, f1=rect_ss_freq(a, b, s["E"], nu, s["rho"], t))
    else:
        c = clamped_case(s["a"], s["b"], s["t"], s["E"], s["nu"], s["rho"], s["q"] / 1000, s["u"], s["v"])
        res = dict(w_max=c["w"], M_x_c=c["Mx_c"], M_y_c=c["My_c"], M_edge=c["M_edge"], sigma_max=6 * c["vm"] / s["t"] ** 2, f1=c["f1"])
    _CACHE[key] = res
    return res


EXTRAS = [   # (label, case, symbol, product value as printed by the probe tool, 9 significant digits)
    ('circ clamped uniform Al', {'shape': 'circular', 'a': 300, 't': 6, 'q': 15, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'clamped', 'kind': 'uniform', 'c': 0.0, 'F': 0.0}, 'w_max', 1.34261719),
    ('circ clamped uniform Al', {'shape': 'circular', 'a': 300, 't': 6, 'q': 15, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'clamped', 'kind': 'uniform', 'c': 0.0, 'F': 0.0}, 'sigma_max', 24.8218063),
    ('circ clamped uniform Al', {'shape': 'circular', 'a': 300, 't': 6, 'q': 15, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'clamped', 'kind': 'uniform', 'c': 0.0, 'F': 0.0}, 'sigma_principal', 28.125),
    ('circ clamped uniform Al', {'shape': 'circular', 'a': 300, 't': 6, 'q': 15, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'clamped', 'kind': 'uniform', 'c': 0.0, 'F': 0.0}, 'f1', 168.777932),
    ('circ SS uniform nu=0.49', {'shape': 'circular', 'a': 300, 't': 6, 'q': 15, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'kind': 'uniform', 'c': 0.0, 'F': 0.0}, 'w_max', 98.4339844),
    ('circ SS uniform nu=0.49', {'shape': 'circular', 'a': 300, 't': 6, 'q': 15, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'kind': 'uniform', 'c': 0.0, 'F': 0.0}, 'sigma_max', 49.078125),
    ('circ SS uniform nu=0.49', {'shape': 'circular', 'a': 300, 't': 6, 'q': 15, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'kind': 'uniform', 'c': 0.0, 'F': 0.0}, 'sigma_principal', 49.078125),
    ('circ SS uniform nu=0.49', {'shape': 'circular', 'a': 300, 't': 6, 'q': 15, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'kind': 'uniform', 'c': 0.0, 'F': 0.0}, 'f1', 30.1711049),
    ('circ clamped patch beta=0.25', {'shape': 'circular', 'a': 300, 't': 6, 'q': 40, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'clamped', 'kind': 'patch', 'c': 75, 'F': 0.0}, 'w_max', 0.775568728),
    ('circ clamped patch beta=0.25', {'shape': 'circular', 'a': 300, 't': 6, 'q': 40, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'clamped', 'kind': 'patch', 'c': 75, 'F': 0.0}, 'sigma_max', 17.480182),
    ('circ clamped patch beta=0.25', {'shape': 'circular', 'a': 300, 't': 6, 'q': 40, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'clamped', 'kind': 'patch', 'c': 75, 'F': 0.0}, 'sigma_principal', 17.480182),
    ('circ clamped patch beta=0.25', {'shape': 'circular', 'a': 300, 't': 6, 'q': 40, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'clamped', 'kind': 'patch', 'c': 75, 'F': 0.0}, 'f1', 168.777932),
    ('circ SS patch beta=0.8', {'shape': 'circular', 'a': 300, 't': 5, 'q': 40, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'patch', 'c': 240, 'F': 0.0}, 'w_max', 8.05743643),
    ('circ SS patch beta=0.8', {'shape': 'circular', 'a': 300, 't': 5, 'q': 40, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'patch', 'c': 240, 'F': 0.0}, 'sigma_max', 162.858694),
    ('circ SS patch beta=0.8', {'shape': 'circular', 'a': 300, 't': 5, 'q': 40, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'patch', 'c': 240, 'F': 0.0}, 'sigma_principal', 162.858694),
    ('circ SS patch beta=0.8', {'shape': 'circular', 'a': 300, 't': 5, 'q': 40, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'patch', 'c': 240, 'F': 0.0}, 'f1', 66.652547),
    ('circ SS patch beta=1', {'shape': 'circular', 'a': 300, 't': 5, 'q': 40, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'patch', 'c': 300, 'F': 0.0}, 'w_max', 9.0153),
    ('circ SS patch beta=1', {'shape': 'circular', 'a': 300, 't': 5, 'q': 40, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'patch', 'c': 300, 'F': 0.0}, 'sigma_max', 178.2),
    ('circ SS patch beta=1', {'shape': 'circular', 'a': 300, 't': 5, 'q': 40, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'patch', 'c': 300, 'F': 0.0}, 'sigma_principal', 178.2),
    ('circ SS patch beta=1', {'shape': 'circular', 'a': 300, 't': 5, 'q': 40, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'patch', 'c': 300, 'F': 0.0}, 'f1', 66.652547),
    ('circ clamped ring beta=0.1', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'ring', 'c': 25, 'F': 3000}, 'w_max', 1.53802096),
    ('circ clamped ring beta=0.1', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'ring', 'c': 25, 'F': 3000}, 'sigma_max', 134.637096),
    ('circ clamped ring beta=0.1', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'ring', 'c': 25, 'F': 3000}, 'sigma_principal', 134.637096),
    ('circ clamped ring beta=0.1', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'ring', 'c': 25, 'F': 3000}, 'f1', 198.679229),
    ('circ SS ring beta=0.9', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'ring', 'c': 225, 'F': 3000}, 'w_max', 0.50774361),
    ('circ SS ring beta=0.9', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'ring', 'c': 225, 'F': 3000}, 'sigma_max', 11.6578961),
    ('circ SS ring beta=0.9', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'ring', 'c': 225, 'F': 3000}, 'sigma_principal', 11.6578961),
    ('circ SS ring beta=0.9', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'kind': 'ring', 'c': 225, 'F': 3000}, 'f1', 95.9796677),
    ('circ SS ring beta=0.5 nu=0', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'kind': 'ring', 'c': 125, 'F': 3000}, 'w_max', 3.40807187),
    ('circ SS ring beta=0.5 nu=0', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'kind': 'ring', 'c': 125, 'F': 3000}, 'sigma_max', 61.2003253),
    ('circ SS ring beta=0.5 nu=0', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'kind': 'ring', 'c': 125, 'F': 3000}, 'sigma_principal', 61.2003253),
    ('circ SS ring beta=0.5 nu=0', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'kind': 'ring', 'c': 125, 'F': 3000}, 'f1', 82.4396207),
    ('circ clamped ring beta=0.99', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'ring', 'c': 247.5, 'F': 3000}, 'w_max', 0.000324780787),
    ('circ clamped ring beta=0.99', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'ring', 'c': 247.5, 'F': 3000}, 'sigma_max', 1.01341949),
    ('circ clamped ring beta=0.99', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'ring', 'c': 247.5, 'F': 3000}, 'sigma_principal', 1.14018601),
    ('circ clamped ring beta=0.99', {'shape': 'circular', 'a': 250, 't': 5, 'q': 0, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'ring', 'c': 247.5, 'F': 3000}, 'f1', 198.679229),
    ('circ clamped patch beta=0.02', {'shape': 'circular', 'a': 250, 't': 5, 'q': 2000, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'patch', 'c': 5, 'F': 0.0}, 'w_max', 0.0851534085),
    ('circ clamped patch beta=0.02', {'shape': 'circular', 'a': 250, 't': 5, 'q': 2000, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'patch', 'c': 5, 'F': 0.0}, 'sigma_max', 15.2572797),
    ('circ clamped patch beta=0.02', {'shape': 'circular', 'a': 250, 't': 5, 'q': 2000, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'patch', 'c': 5, 'F': 0.0}, 'sigma_principal', 15.2572797),
    ('circ clamped patch beta=0.02', {'shape': 'circular', 'a': 250, 't': 5, 'q': 2000, 'E': 200, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'kind': 'patch', 'c': 5, 'F': 0.0}, 'f1', 198.679229),
    ('SS 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'w_max', 0.642932714),
    ('SS 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_max', 20.6659184),
    ('SS 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_principal', 23.8319711),
    ('SS 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'f1', 98.3429809),
    ('SS 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_x_c', 115.8757),
    ('SS 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_y_c', 254.207692),
    ('SS 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_xy_k', 115.667644),
    ('SS 5:1 uniform', {'shape': 'rect', 'a': 2500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'w_max', 0.82334383),
    ('SS 5:1 uniform', {'shape': 'rect', 'a': 2500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_max', 25.9426601),
    ('SS 5:1 uniform', {'shape': 'rect', 'a': 2500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_principal', 29.208861),
    ('SS 5:1 uniform', {'shape': 'rect', 'a': 2500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'f1', 81.8213601),
    ('SS 5:1 uniform', {'shape': 'rect', 'a': 2500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_x_c', 94.3629668),
    ('SS 5:1 uniform', {'shape': 'rect', 'a': 2500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_y_c', 311.561185),
    ('SS 5:1 uniform', {'shape': 'rect', 'a': 2500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_xy_k', 118.726981),
    ('SS 10:1 uniform', {'shape': 'rect', 'a': 5000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'w_max', 0.826515527),
    ('SS 10:1 uniform', {'shape': 'rect', 'a': 5000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_max', 26.039564),
    ('SS 10:1 uniform', {'shape': 'rect', 'a': 5000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_principal', 29.2968123),
    ('SS 10:1 uniform', {'shape': 'rect', 'a': 5000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'f1', 79.4611286),
    ('SS 10:1 uniform', {'shape': 'rect', 'a': 5000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_x_c', 93.7503848),
    ('SS 10:1 uniform', {'shape': 'rect', 'a': 5000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_y_c', 312.499331),
    ('SS 10:1 uniform', {'shape': 'rect', 'a': 5000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_xy_k', 118.72755),
    ('SS square nu=0 (corner governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 210, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'w_max', 0.550244593),
    ('SS square nu=0 (corner governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 210, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_max', 42.865587),
    ('SS square nu=0 (corner governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 210, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_principal', 24.7484582),
    ('SS square nu=0 (corner governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 210, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'f1', 175.899796),
    ('SS square nu=0 (corner governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 210, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_x_c', 117.874153),
    ('SS square nu=0 (corner governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 210, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_y_c', 117.874153),
    ('SS square nu=0 (corner governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 210, 'nu': 0.0, 'rho': 7850, 'support': 'simply supported', 'u': None, 'v': None}, 'M_xy_k', 148.490749),
    ('SS square nu=0.49 (centre governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'u': None, 'v': None}, 'w_max', 29.2691606),
    ('SS square nu=0.49 (centre governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_max', 29.2720813),
    ('SS square nu=0.49 (centre governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'u': None, 'v': None}, 'sigma_principal', 29.2720813),
    ('SS square nu=0.49 (centre governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'u': None, 'v': None}, 'f1', 64.4282863),
    ('SS square nu=0.49 (centre governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'u': None, 'v': None}, 'M_x_c', 175.632488),
    ('SS square nu=0.49 (centre governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'u': None, 'v': None}, 'M_y_c', 175.632488),
    ('SS square nu=0.49 (centre governs)', {'shape': 'rect', 'a': 400, 'b': 400, 't': 6, 'q': 20, 'E': 3, 'nu': 0.49, 'rho': 1100, 'support': 'simply supported', 'u': None, 'v': None}, 'M_xy_k', 75.730282),
    ('SS 3:1 patch 20% x 20%', {'shape': 'rect', 'a': 1200, 'b': 400, 't': 6, 'q': 50, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'simply supported', 'u': 240, 'v': 80}, 'w_max', 1.59500322),
    ('SS 3:1 patch 20% x 20%', {'shape': 'rect', 'a': 1200, 'b': 400, 't': 6, 'q': 50, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'simply supported', 'u': 240, 'v': 80}, 'sigma_max', 28.0314732),
    ('SS 3:1 patch 20% x 20%', {'shape': 'rect', 'a': 1200, 'b': 400, 't': 6, 'q': 50, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'simply supported', 'u': 240, 'v': 80}, 'sigma_principal', 32.0865507),
    ('SS 3:1 patch 20% x 20%', {'shape': 'rect', 'a': 1200, 'b': 400, 't': 6, 'q': 50, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'simply supported', 'u': 240, 'v': 80}, 'f1', 101.911203),
    ('SS 3:1 patch 20% x 20%', {'shape': 'rect', 'a': 1200, 'b': 400, 't': 6, 'q': 50, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'simply supported', 'u': 240, 'v': 80}, 'M_x_c', 118.389357),
    ('SS 3:1 patch 20% x 20%', {'shape': 'rect', 'a': 1200, 'b': 400, 't': 6, 'q': 50, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'simply supported', 'u': 240, 'v': 80}, 'M_y_c', 192.519304),
    ('SS 3:1 patch 20% x 20%', {'shape': 'rect', 'a': 1200, 'b': 400, 't': 6, 'q': 50, 'E': 70, 'nu': 0.33, 'rho': 2700, 'support': 'simply supported', 'u': 240, 'v': 80}, 'M_xy_k', 9.25591523),
    ('SS square patch 2% (cap warning)', {'shape': 'rect', 'a': 500, 'b': 500, 't': 6, 'q': 500, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 10, 'v': 10}, 'w_max', 0.0348693698),
    ('SS square patch 2% (cap warning)', {'shape': 'rect', 'a': 500, 'b': 500, 't': 6, 'q': 500, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 10, 'v': 10}, 'sigma_max', 3.7550987),
    ('SS square patch 2% (cap warning)', {'shape': 'rect', 'a': 500, 'b': 500, 't': 6, 'q': 500, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 10, 'v': 10}, 'sigma_principal', 3.7550987),
    ('SS square patch 2% (cap warning)', {'shape': 'rect', 'a': 500, 'b': 500, 't': 6, 'q': 500, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 10, 'v': 10}, 'f1', 118.011577),
    ('SS square patch 2% (cap warning)', {'shape': 'rect', 'a': 500, 'b': 500, 't': 6, 'q': 500, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 10, 'v': 10}, 'M_x_c', 22.5305922),
    ('SS square patch 2% (cap warning)', {'shape': 'rect', 'a': 500, 'b': 500, 't': 6, 'q': 500, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 10, 'v': 10}, 'M_y_c', 22.5305922),
    ('SS square patch 2% (cap warning)', {'shape': 'rect', 'a': 500, 'b': 500, 't': 6, 'q': 500, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 10, 'v': 10}, 'M_xy_k', 3.04699536),
    ('SS patch = whole plate', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 400}, 'w_max', 2.38014296),
    ('SS patch = whole plate', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 400}, 'sigma_max', 99.1470097),
    ('SS patch = whole plate', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 400}, 'sigma_principal', 108.213448),
    ('SS patch = whole plate', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 400}, 'f1', 133.172787),
    ('SS patch = whole plate', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 400}, 'M_x_c', 398.741582),
    ('SS patch = whole plate', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 400}, 'M_y_c', 649.28069),
    ('SS patch = whole plate', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 400}, 'M_xy_k', 343.455316),
    ('SS patch 100% x 10%', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 40}, 'w_max', 0.38343472),
    ('SS patch 100% x 10%', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 40}, 'sigma_max', 19.5032012),
    ('SS patch 100% x 10%', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 40}, 'sigma_principal', 22.4995032),
    ('SS patch 100% x 10%', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 40}, 'f1', 133.172787),
    ('SS patch 100% x 10%', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 40}, 'M_x_c', 72.5332361),
    ('SS patch 100% x 10%', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 40}, 'M_y_c', 134.997019),
    ('SS patch 100% x 10%', {'shape': 'rect', 'a': 600, 'b': 400, 't': 6, 'q': 50, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'simply supported', 'u': 600, 'v': 40}, 'M_xy_k', 46.1495215),
    ('clamped 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'w_max', 0.160783325),
    ('clamped 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'M_x_c', 39.5200739),
    ('clamped 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'M_y_c', 102.887475),
    ('clamped 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'M_edge', 207.164718),
    ('clamped 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'sigma_max', 17.2623778),
    ('clamped 2:1 uniform', {'shape': 'rect', 'a': 1000, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'f1', 195.918316),
    ('clamped 3:1 uniform', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'w_max', 0.16613271),
    ('clamped 3:1 uniform', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'M_x_c', 31.7319936),
    ('clamped 3:1 uniform', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'M_y_c', 104.753339),
    ('clamped 3:1 uniform', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'M_edge', 209.441625),
    ('clamped 3:1 uniform', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'sigma_max', 17.4522584),
    ('clamped 3:1 uniform', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 10, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': None, 'v': None}, 'f1', 184.90666),
    ('clamped square patch 50%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 100, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 250, 'v': 250}, 'w_max', 0.538431319),
    ('clamped square patch 50%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 100, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 250, 'v': 250}, 'M_x_c', 448.222023),
    ('clamped square patch 50%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 100, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 250, 'v': 250}, 'M_y_c', 448.222023),
    ('clamped square patch 50%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 100, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 250, 'v': 250}, 'M_edge', 650.997875),
    ('clamped square patch 50%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 100, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 250, 'v': 250}, 'sigma_max', 54.2455845),
    ('clamped square patch 50%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 100, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 250, 'v': 250}, 'f1', 286.851697),
    ('clamped square patch 25%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 300, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 125, 'v': 125}, 'w_max', 0.56754478),
    ('clamped square patch 25%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 300, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 125, 'v': 125}, 'M_x_c', 644.341968),
    ('clamped square patch 25%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 300, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 125, 'v': 125}, 'M_y_c', 644.341968),
    ('clamped square patch 25%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 300, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 125, 'v': 125}, 'M_edge', 565.435995),
    ('clamped square patch 25%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 300, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 125, 'v': 125}, 'sigma_max', 60.4070595),
    ('clamped square patch 25%', {'shape': 'rect', 'a': 500, 'b': 500, 't': 8, 'q': 300, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 125, 'v': 125}, 'f1', 286.851697),
    ('clamped 3:1 band patch 25% x 100%', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 20, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 375, 'v': 500}, 'w_max', 0.249415747),
    ('clamped 3:1 band patch 25% x 100%', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 20, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 375, 'v': 500}, 'M_x_c', 99.3342567),
    ('clamped 3:1 band patch 25% x 100%', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 20, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 375, 'v': 500}, 'M_y_c', 166.04572),
    ('clamped 3:1 band patch 25% x 100%', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 20, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 375, 'v': 500}, 'M_edge', 345.149936),
    ('clamped 3:1 band patch 25% x 100%', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 20, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 375, 'v': 500}, 'sigma_max', 28.7602475),
    ('clamped 3:1 band patch 25% x 100%', {'shape': 'rect', 'a': 1500, 'b': 500, 't': 8, 'q': 20, 'E': 210, 'nu': 0.3, 'rho': 7850, 'support': 'clamped', 'u': 375, 'v': 500}, 'f1', 184.90666),
]


def run_extras():
    print()
    print("Extra checks (product values observed with the probe tool)")
    print("%-40s %-16s %14s %14s %10s  %s" % ("probe", "symbol", "product", "independent", "rel diff", "result"))
    for label, spec, sym, prod in EXTRAS:
        spec = dict(spec)
        if spec.get("shape") == "rect":
            spec["shape"] = "rectangular"
        v = independent(spec)[sym]
        if spec["shape"] == "circular":
            tol = 1e-7
        elif spec["support"] != "clamped":
            tol = 2e-5 if sym in ("M_x_c", "M_y_c") else 3e-6
            if sym == "M_x_c" and abs(v) < 1e-3 * abs(independent(spec)["M_y_c"]):
                tol = 1e-2                        # a centre moment far smaller than the other one: the product stops summing early
        else:
            tol = 1e-5 if sym == "f1" else (1e-5 if sym == "w_max" else 3e-3)
        rd = abs(v - prod) / abs(v) if v != 0 else abs(prod)
        print("%-40s %-16s %14.8g %14.8g %10.2e  %s" % (label, sym, prod, v, rd, "PASS" if rd <= tol else "FAIL"))
    # strip limit of a long clamped plate (analytic): edge moment q*b^2/12, w = q*b^4/(384 D)
    q, b = 0.01, 500.0
    print("%-40s %-16s %14.8g %14.8g %10.2e  %s" % ("clamped 10:1 uniform vs strip limit", "M_edge", 208.386300, q * b * b / 12,
                                                    abs(208.3863 - q * b * b / 12) / (q * b * b / 12), "INFO (K=16 stalled; +0.025 %)"))
    # textbook coefficients (Roark 8th ed. Tables 11.2 and 11.4, values from memory, not pinned): simply supported
    # square beta = 0.2874, alpha = 0.0444; clamped square beta = 0.3078, alpha = 0.0138; circular clamped 0.75, SS 1.2375
    D = rigidity(210, 0.3, 8)
    ss = navier_converged(500, 500, 8, 210, 0.3, 0.01)[0]
    cl = _clamped_solve(500, 500, 8, 210, 0.3, 0.01, 40, 40)
    print("INFO Roark cross-check (coefficients from memory): SS square beta %.4f (book 0.2874), alpha %.4f (book 0.0444)"
          % (6 * ss[1] / (0.01 * 500 ** 2) * 1e0 / 64 * 64 / 6 * 6 / 64 * 64 / 1, ss[0] * 210e3 * 8 ** 3 / (0.01 * 500 ** 4)))
    print("INFO clamped square: edge beta %.4f (book 0.3078), alpha %.4f (book 0.0138)"
          % (6 * cl["M_edge"] / (0.01 * 500 ** 2), cl["w"] * 210e3 * 8 ** 3 / (0.01 * 500 ** 4)))


if __name__ == "__main__":
    allok = run_kats()
    run_extras()
    sys.exit(0 if allok else 1)
