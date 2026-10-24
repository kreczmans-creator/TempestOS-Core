#!/usr/bin/env python3
"""Independent re-derivation of mech.shaft (Track P, P1).

Method sources (cited by title, edition and section only):
  Budynas and Nisbett, Shigley's Mechanical Engineering Design, 11th ed.:
    Sec. 6-9 (endurance limit S_e', Marin factors k_a to k_f, reliability factors),
    Sec. 6-10 (K_f = 1 + q (K_t - 1)), Sec. 6-12 (Goodman, Gerber, ASME-elliptic, Soderberg),
    Sec. 7-4 (DE-Goodman for shafts, first-cycle yield), Sec. 7-6 (Rayleigh and Dunkerley),
    Sec. 7-7 (parallel keys).  Equation numbers are not quoted; check them in your copy.
  Euler-Bernoulli beam theory, solved here with an own finite-element model (cubic Hermite
  elements, consistent mass matrix for the exact first natural frequency).
Run: python3 tools/verification/mech.shaft.py
Exit code: 0 when every known-answer case matches within its tolerance, else 1.
Extra checks print PASS, FAIL or INFO.  A FAIL there is a product finding (named in the last column),
not a script error, and it does not change the exit code.
"""
import math
import sys
import numpy as np
from scipy.stats import norm

G = 9.80665


# ----------------------------------------------------------------- fatigue ---
KA_TABLE = {  # Shigley Table 6-2, S_ut in MPa
    "ground": (1.58, -0.085),
    "machined or cold-drawn": (4.51, -0.265),
    "hot-rolled": (57.7, -0.718),
    "as-forged": (272.0, -0.995),
}


def se_prime(sut):
    return 0.5 * sut if sut <= 1400.0 else 700.0


def k_a(sut, finish):
    a, b = KA_TABLE[finish]
    return a * sut ** b


def k_b(d):
    if d <= 51.0:
        return 1.24 * d ** -0.107
    return 1.51 * min(d, 254.0) ** -0.157  # held above 254 mm (spec)


def k_e(rel_percent):
    z = norm.ppf(rel_percent / 100.0)
    return 1.0 - 0.08 * z


def endurance(sut, finish, d, rel, kd=1.0, kf=1.0, kc=1.0):
    return k_a(sut, finish) * k_b(d) * kc * kd * k_e(rel) * kf * se_prime(sut)


def stresses(d, di, Ma, Tm, kt, kts, q, qs):
    """Return (sigma_a, tau_m) in MPa for M in N*mm, T in N*mm, d in mm."""
    kf = 1 + q * (kt - 1)
    kfs = 1 + qs * (kts - 1)
    den = math.pi * (d ** 4 - di ** 4)
    return kf * 32 * Ma * d / den, kfs * 16 * Tm * d / den


def n_goodman(sa, tm, se, sut):
    return 1.0 / (sa / se + math.sqrt(3.0) * tm / sut)


def n_gerber(sa, tm, se, sut):
    """DE-Gerber, sigma_m = sqrt(3) tau_m, sigma_a = sa (bending reversed)."""
    A, B = sa, math.sqrt(3.0) * tm
    if B == 0:
        return se / A
    return 0.5 * (sut / B) ** 2 * (A / se) * (-1 + math.sqrt(1 + (2 * B * se / (sut * A)) ** 2))


def n_soderberg(sa, tm, se, sy):
    return 1.0 / (sa / se + math.sqrt(3.0) * tm / sy)


def n_asme(sa, tm, se, sy):
    """ASME-elliptic with sigma_a' = sa, sigma_m' = sqrt(3) tau_m."""
    A, B = sa, math.sqrt(3.0) * tm
    return 1.0 / math.sqrt((A / se) ** 2 + (B / sy) ** 2)


def n_yield(sa, tm, sy):
    return sy / math.sqrt(sa ** 2 + 3 * tm ** 2)


# -------------------------------------------------------------------- keys ---
def key_stress(T_Nmm, d, w, h, L):
    F = 2 * T_Nmm / d
    return F / (w * L), F / (0.5 * h * L)


def key_length_required(T_Nmm, d, w, h, sy_key, n):
    """Shear on 0.577 Sy (distortion energy), bearing on Sy."""
    Ls = 2 * T_Nmm * n / (d * w * 0.577 * sy_key)
    Lb = 4 * T_Nmm * n / (d * h * sy_key)
    return max(Ls, Lb), Ls, Lb


# -------------------------------------------------------------- beam model ---
class Shaft:
    """Euler-Bernoulli FE model.  Units: mm, N, MPa (E in MPa), mass in tonne-free kg via SI helpers."""

    def __init__(self, segs, xA, xB, E_GPa, rho=7850.0, sub=8):
        # segs: list of (x_end, d, di) left to right starting at x = 0
        self.segs = segs
        self.xA, self.xB = xA, xB
        self.E = E_GPa * 1e3  # MPa
        self.rho = rho
        self.sub = sub
        ends = [s[0] for s in segs]
        self.L = ends[-1]

    def build(self, pts_extra):
        pts = {0.0, self.L, self.xA, self.xB}
        pts.update(s[0] for s in self.segs)
        pts.update(pts_extra)
        pts = sorted(pts)
        nodes = [pts[0]]
        for a, b in zip(pts[:-1], pts[1:]):
            for k in range(1, self.sub + 1):
                nodes.append(a + (b - a) * k / self.sub)
        self.nodes = np.array(nodes)
        n = len(nodes)
        K = np.zeros((2 * n, 2 * n))
        M = np.zeros((2 * n, 2 * n))
        for e in range(n - 1):
            x1, x2 = self.nodes[e], self.nodes[e + 1]
            xm = 0.5 * (x1 + x2)
            for (xe, d, di) in self.segs:
                if xm < xe:
                    break
            d_, di_ = d, di
            I = math.pi * (d_ ** 4 - di_ ** 4) / 64.0
            A = math.pi * (d_ ** 2 - di_ ** 2) / 4.0
            l = x2 - x1
            ke = self.E * I / l ** 3 * np.array([
                [12, 6 * l, -12, 6 * l],
                [6 * l, 4 * l * l, -6 * l, 2 * l * l],
                [-12, -6 * l, 12, -6 * l],
                [6 * l, 2 * l * l, -6 * l, 4 * l * l]])
            rho_mm = self.rho * 1e-9  # kg/mm^3
            me = rho_mm * A * l / 420.0 * np.array([
                [156, 22 * l, 54, -13 * l],
                [22 * l, 4 * l * l, 13 * l, -3 * l * l],
                [54, 13 * l, 156, -22 * l],
                [-13 * l, -3 * l * l, -22 * l, 4 * l * l]])
            idx = [2 * e, 2 * e + 1, 2 * e + 2, 2 * e + 3]
            for i in range(4):
                for j in range(4):
                    K[idx[i], idx[j]] += ke[i, j]
                    M[idx[i], idx[j]] += me[i, j]
        self.K, self.M = K, M
        return self

    def node(self, x):
        return int(np.argmin(abs(self.nodes - x)))

    def fixed_dofs(self):
        return [2 * self.node(self.xA), 2 * self.node(self.xB)]

    def solve_static(self, loads):
        """loads: list of (x, F_N) with F positive up.  Returns u vector (mm, rad)."""
        f = np.zeros(self.K.shape[0])
        for x, F in loads:
            f[2 * self.node(x)] += F
        return self.solve_f(f)

    def solve_f(self, f):
        fixed = self.fixed_dofs()
        free = [i for i in range(self.K.shape[0]) if i not in fixed]
        u = np.zeros(self.K.shape[0])
        u[free] = np.linalg.solve(self.K[np.ix_(free, free)], f[free])
        return u

    def reactions(self, loads):
        xA, xB = self.xA, self.xB
        s = sum(F for _, F in loads)
        mA = sum(F * (x - xA) for x, F in loads)
        RB = mA / (xB - xA)
        RA = s - RB
        return RA, RB

    def moment(self, loads, x):
        RA, RB = self.reactions(loads)
        allf = list(loads) + [(self.xA, -RA), (self.xB, -RB)]
        # sagging-positive: sum over upward forces to the left; applied loads are F (up +), reactions oppose
        m = 0.0
        for xf, F in allf:
            if xf < x:
                m += -F * (x - xf)
        return m  # sign irrelevant, magnitude used

    # lateral critical speed ----------------------------------------------
    def point_mass_matrix(self, masses):
        Mt = self.M.copy()
        for x, m in masses:
            Mt[2 * self.node(x), 2 * self.node(x)] += m
        return Mt

    def exact_first_speed(self, masses):
        Mt = self.point_mass_matrix(masses)
        fixed = self.fixed_dofs()
        free = [i for i in range(self.K.shape[0]) if i not in fixed]
        Kf = self.K[np.ix_(free, free)] * 1e3  # N/mm -> N/m per kg*mm basis
        Mf = Mt[np.ix_(free, free)]
        lam = np.linalg.eigvals(np.linalg.solve(Kf, Mf)).real  # flexibility times mass
        return math.sqrt(1.0 / lam.max())  # rad/s

    def rayleigh(self, masses):
        Mt = self.point_mass_matrix(masses)
        n2 = self.K.shape[0]
        ones = np.zeros(n2)
        ones[0::2] = 1.0
        fg = Mt @ ones * G  # N (kg*m/s^2)
        u = self.solve_f(fg)  # mm
        um = u * 1e-3
        num = G * (ones @ Mt @ um)
        den = um @ Mt @ um
        return math.sqrt(num / den)  # rad/s


def rpm(w):
    return w * 60.0 / (2 * math.pi)


# ------------------------------------------------------------ KAT drivers ---
rows = []


def check(case, sym, kat, ind, tol):
    rd = abs(ind - kat) / abs(kat) if kat != 0 else abs(ind - kat)
    ok = rd <= max(tol, 1e-6)
    rows.append((case, sym, kat, ind, rd, tol, ok))
    return ok


def kat1():
    sh = Shaft([(400, 30, 0)], 0, 400, 207, rho=0.0).build([200])
    loads = [(200, -2000.0)]
    u = sh.solve_static(loads)
    RA, RB = sh.reactions(loads)
    M = abs(sh.moment(loads, 200))
    thA = abs(u[2 * sh.node(0) + 1])
    y = abs(u[2 * sh.node(200)])
    # single mass 20 kg at mid-span, massless shaft
    wr = sh.rayleigh([(200, 20.0)])
    sh2 = Shaft([(400, 30, 0)], 0, 400, 207, rho=1e-3).build([200])
    wex = sh2.exact_first_speed([(200, 20.0)])
    I = math.pi * 30 ** 4 / 64
    nc_closed = rpm(math.sqrt(48 * 207e9 * I * 1e-12 / (0.4 ** 3 * 20.0)))
    check("ka-simply-supported", "R_A", 1, abs(RA) / 1000, 1e-6)
    check("ka-simply-supported", "M_max", 0.2, M / 1e6, 1e-6)
    check("ka-simply-supported", "theta_A", 2.429991e-3, thA, 1e-5)
    check("ka-simply-supported", "y_load", 0.3239989, y, 1e-5)
    check("ka-simply-supported", "n_c (Rayleigh, FE)", 5305.174, rpm(wr), 1e-5)
    check("ka-simply-supported", "n_c (closed form)", 5305.174, nc_closed, 1e-5)
    # Dunkerley with a massless shaft: 1/w^2 = m * delta, delta = L^3/(48EI)
    delta = 0.4 ** 3 / (48 * 207e9 * I * 1e-12)
    check("ka-simply-supported", "n_c_D", 5305.174, rpm(1 / math.sqrt(20.0 * delta)), 1e-5)
    check("ka-simply-supported", "n_c (exact eigen)", 5305.174, rpm(wex), 1e-4)


def kat2():
    T = 0.1243e6  # N*mm
    tau, sig = key_stress(T, 28, 8, 7, 30)
    check("ka-key", "tau_key", 36.99405, tau, 1e-6)
    check("ka-key", "sigma_key", 84.55782, sig, 1e-6)


def kat3():
    sut, sy = 735.0, 574.0
    d = 28.0
    sh = Shaft([(400, d, 0)], 0, 400, 207, rho=0.0).build([200])
    loads = [(200, -1424.0)]
    M = abs(sh.moment(loads, 200))
    se = endurance(sut, "machined or cold-drawn", d, 99.0)
    sa, tm = stresses(d, 0.0, M, 0.1243e6, 1.68, 1.42, 0.85, 0.92)
    check("ka-shigley-7-1", "S_e", 203.7091, se, 1e-5)
    check("ka-shigley-7-1", "n_f_min", 1.650015, n_goodman(sa, tm, se, sut), 1e-5)
    check("ka-shigley-7-1", "n_y_min", 4.585856, n_yield(sa, tm, sy), 1e-5)


def other_criteria():
    """Same stresses as ka-shigley-7-1 under the other fatigue criteria (information, no KAT)."""
    sut, sy, d = 735.0, 574.0, 28.0
    se = endurance(sut, "machined or cold-drawn", d, 99.0)
    sa, tm = stresses(d, 0.0, 142400.0, 124300.0, 1.68, 1.42, 0.85, 0.92)
    print()
    print("Other fatigue criteria for the ka-shigley-7-1 stresses (information only):")
    print(f"  DE-Goodman  {n_goodman(sa, tm, se, sut):.4f}   DE-Gerber {n_gerber(sa, tm, se, sut):.4f}   "
          f"DE-ASME {n_asme(sa, tm, se, sy):.4f}   DE-Soderberg {n_soderberg(sa, tm, se, sy):.4f}")


def report():
    print(f"{'case':26s} {'symbol':20s} {'KAT':>13s} {'independent':>13s} {'rel.diff':>10s} {'tol':>8s}  result")
    allok = True
    for case, sym, kat, ind, rd, tol, ok in rows:
        allok &= ok
        print(f"{case:26s} {sym:20s} {kat:13.7g} {ind:13.7g} {rd:10.2e} {tol:8.1e}  {'PASS' if ok else 'FAIL'}")
    return allok



# ------------------------------------------------------------- full case ---
def analyze(c):
    """Independent solution of one shaft case (dict, see the cases below).  Units: mm, N, MPa."""
    sh_segs = c["segs"]
    L = sh_segs[-1][0]
    xs_special = [x for x, *_ in c.get("loads", [])] + [x for x, *_ in c.get("secs", [])] + [c["xT1"], c["xT2"]]
    xs_special = [x for x in xs_special if 0 <= x <= L]
    sh = Shaft(sh_segs, c["xA"], c["xB"], c.get("E", 207.0), rho=c.get("rho", 7850.0)).build(xs_special)
    out = {}
    planes = []
    for k in (1, 2):
        ld = [(l[0], l[k] * 1000.0) for l in c.get("loads", []) if l[k] != 0]
        planes.append(ld)
    ra = rb = 0.0
    u_planes = []
    for ld in planes:
        if not ld:
            u_planes.append(None)
            continue
        RA, RB = sh.reactions(ld)
        ra += RA ** 2
        rb += RB ** 2
        u_planes.append(sh.solve_static(ld))
    out["R_A"] = math.sqrt(ra) / 1000.0
    out["R_B"] = math.sqrt(rb) / 1000.0

    def mom(x):
        return math.sqrt(sum(sh.moment(ld, x) ** 2 for ld in planes if ld))

    stations = sorted(set([0.0, L, c["xA"], c["xB"]] + xs_special + [e[0] for e in sh_segs]))
    out["M_max"] = max(mom(x) for x in stations) / 1e6  # kN*m

    def vec(i, x):
        n = sh.node(x)
        return math.sqrt(sum(u[2 * n + i] ** 2 for u in u_planes if u is not None))

    out["theta_A"] = vec(1, c["xA"])
    out["theta_B"] = vec(1, c["xB"])
    out["y_load"] = max([vec(0, l[0]) for l in c.get("loads", [])] or [0.0])
    # fatigue and yield per section
    sut, sy = c.get("Sut", 600.0), c.get("Sy", 450.0)
    nf_list, ny_list, se_list = [], [], []
    T = c["T"] * 1e6
    lo, hi = min(c["xT1"], c["xT2"]), max(c["xT1"], c["xT2"])
    for (x, kt, kts, q, qs) in c.get("secs", []):
        touching = [s for k, s in enumerate(sh_segs) if (sh_segs[k - 1][0] if k else 0.0) - 1e-9 <= x <= s[0] + 1e-9]
        tm = T if lo - 1e-9 <= x <= hi + 1e-9 else 0.0
        inv, se, sa, tau = -1.0, 0.0, 0.0, 0.0
        for _, d, di in touching:  # a shoulder is checked on both sides; the worse side governs
            se_i = endurance(sut, c.get("finish", "machined or cold-drawn"), d, c.get("R", 99.0), c.get("kd", 1.0), c.get("kf", 1.0))
            sa_i, tau_i = stresses(d, di, mom(x), tm, kt, kts, q, qs)
            inv_i = sa_i / se_i + math.sqrt(3) * tau_i / sut
            if inv_i > inv:
                inv, se, sa, tau = inv_i, se_i, sa_i, tau_i
        nf_list.append(1 / inv if inv > 0 else 1e9)
        ny_list.append(n_yield(sa, tau, sy) if (sa or tau) else 1e9)
        se_list.append(se)
    if nf_list:
        k = int(np.argmin(nf_list))
        out["n_f_min"], out["n_y_min"], out["S_e"] = nf_list[k], min(ny_list), se_list[k]
        out["nf_sections"] = nf_list
    # key
    if c.get("key"):
        xk, w, h, Lk, syk = c["key"]
        touching = [s for k, s in enumerate(sh_segs) if (sh_segs[k - 1][0] if k else 0.0) - 1e-9 <= xk <= s[0] + 1e-9]
        dk = min(s[1] for s in touching)
        out["tau_key"], out["sigma_key"] = key_stress(T, dk, w, h, Lk)
        out["L_key_req"] = key_length_required(T, dk, w, h, syk, c.get("n_target", 1.5))[0]
    # critical speed
    masses = [(l[0], l[3]) for l in c.get("loads", []) if l[3] > 0]
    if c.get("rho", 7850.0) > 0 or masses:
        out["n_c_exact"] = rpm(sh.exact_first_speed(masses))
        out["n_c_rayleigh_signed"] = rpm(sh.rayleigh(masses))
        sh0 = Shaft(sh_segs, c["xA"], c["xB"], c.get("E", 207.0), rho=c.get("rho", 7850.0)).build(xs_special)
        w_shaft = sh0.exact_first_speed([]) if c.get("rho", 7850.0) > 0 else math.inf
        inv = (1 / w_shaft ** 2 if math.isfinite(w_shaft) else 0.0)
        for xm, m in masses:
            f = np.zeros(sh.K.shape[0]); f[2 * sh.node(xm)] = 1.0
            inv += m * sh.solve_f(f)[2 * sh.node(xm)] * 1e-3
        out["n_c_dunkerley"] = rpm(1 / math.sqrt(inv))
    return out


# Each case: inputs, then the values read from the real product (Probe tool, release/v0.26.0 build).
SEG1 = [(60, 25, 0), (140, 30, 0), (260, 40, 0), (340, 30, 0), (400, 25, 0)]
CASES = {
    "E1 stepped, two planes, ground, R 95 %": dict(
        segs=SEG1, xA=20, xB=380, loads=[(100, -3, 1.5, 0), (300, 2, -4, 0)], T=0.35, xT1=100, xT2=300,
        secs=[(140, 1.7, 1.4, 0.85, 0.9), (260, 1.7, 1.4, 0.85, 0.9), (100, 2.14, 3.0, 0.8, 0.9)],
        Sut=800, Sy=600, finish="ground", R=95,
        product=dict(R_A=1.909204475, R_B=2.916534389, M_max=0.2333227511, theta_A=0.001363406643, theta_B=0.002129205355,
                     y_load=0.1254453166e-3 * 1e3, S_e=267.9475629, n_f_min=1.232453542, n_y_min=1.771984258),
        rayleigh=dict(product=30485.0, tol=0.03)),
    "E2 hollow 60/40, S_ut 1500, hot-rolled, R 99.9, k_d 0.95, k_f 0.9": dict(
        segs=[(500, 60, 40)], xA=50, xB=450, loads=[(250, -5, 0, 0)], T=2.0, xT1=0, xT2=500,
        secs=[(250, 2.0, 1.6, 0.9, 0.95)], Sut=1500, Sy=1300, finish="hot-rolled", R=99.9, kd=0.95, kf=0.9,
        product=dict(R_A=2.5, R_B=2.5, M_max=0.5, theta_A=0.0004731473596, theta_B=0.0004731473596, y_load=0.06308631462,
                     S_e=108.2137716, n_f_min=1.606616543, n_y_min=7.679970578)),
    "E3 d = 300 mm as-forged, R 50 % (k_b held at the 254 mm value)": dict(
        segs=[(3000, 300, 0)], xA=100, xB=2900, loads=[(1500, -50, 0, 0)], T=20, xT1=0, xT2=3000,
        secs=[(1500, 1, 1, 0, 0)], Sut=700, Sy=500, finish="as-forged", R=50,
        product=dict(R_A=25.0, R_B=25.0, M_max=35.0, theta_A=0.0002976739438, theta_B=0.0002976739438, y_load=0.2778290142,
                     S_e=88.95747605, n_f_min=6.338551417, n_y_min=33.93897649)),
    "E4 near zero load, no torque": dict(
        segs=[(400, 20, 0)], xA=0, xB=400, loads=[(200, -0.001, 0, 0)], T=0.0, xT1=0, xT2=400,
        secs=[(200, 1, 1, 0, 0)], Sut=600, Sy=450, finish="machined or cold-drawn", R=99,
        product=dict(R_A=0.0005, R_B=0.0005, M_max=0.0001e-0 * 1.0, theta_A=6.150915675e-06, theta_B=6.150915675e-06,
                     y_load=0.00082012209, S_e=181.913974, n_f_min=1428.749011, n_y_min=3534.291735)),
    "E5 key 14 x 9 x 40 on d 50, S_y,key 350, n_target 2": dict(
        segs=[(300, 50, 0)], xA=0, xB=300, loads=[(150, -3, 0, 0)], T=1.2, xT1=0, xT2=300,
        secs=[(150, 1, 1, 0, 0)], Sut=650, Sy=450, finish="machined or cold-drawn", R=99, key=(50, 14, 9, 40, 350), n_target=2.0,
        product=dict(R_A=1.5, R_B=1.5, M_max=0.225, theta_A=0.0002657195572, theta_B=0.0002657195572, y_load=0.02657195572,
                     S_e=174.9188561, n_f_min=4.253485872, n_y_min=5.193535793, tau_key=85.71428571, sigma_key=266.6666667,
                     L_key_req=60.95238095)),
    "E6 torque span trap: L 800, T 1 kN*m, default x_T2 = 400 (section at 600 and 300)": dict(
        segs=[(800, 40, 0)], xA=50, xB=750, loads=[(400, -2, 0, 0)], T=1.0, xT1=0, xT2=400,
        secs=[(600, 1, 1, 0, 0), (300, 1, 1, 0, 0)], Sut=600, Sy=450, finish="machined or cold-drawn", R=99,
        product=dict(n_f_min=2.149234871, n_y_min=3.136755711), finding="P1-mech.shaft-03 (default torque span)"),
    "E8 shoulder hollow 50/46 mm to solid 40 mm: both sides must be checked": dict(
        segs=[(200, 50, 46), (400, 40, 0)], xA=0, xB=400, loads=[(200, -2, 0, 0)], T=0.0, xT1=0, xT2=400,
        secs=[(200, 1, 1, 0, 0)], Sut=600, Sy=450, finish="machined or cold-drawn", R=99,
        product=dict(n_f_min=5.306470136), finding="P1-mech.shaft-02 (shoulder side chosen by outer diameter)"),
    "E9 lower bounds: d 5 mm, S_ut 300 MPa, S_y 150 MPa, R 99.9 %, 2-plane load, T 0.5 N*m": dict(
        segs=[(100, 5, 0)], xA=0, xB=100, loads=[(50, -0.01, 0.0, 0)], T=0.0005, xT1=0, xT2=100,
        secs=[(50, 3.0, 2.0, 0.5, 0.6)], Sut=300, Sy=150, finish="machined or cold-drawn", R=99.9,
        product=dict(R_A=0.005, R_B=0.005, M_max=0.00025, theta_A=0.000984146508, theta_B=0.000984146508, y_load=0.0328048836,
                     S_e=117.2550219, n_f_min=1.866834947, n_y_min=2.154466454)),
    "E10 upper bounds: d 300 mm, L 3000 mm, S_ut 2000 MPa, T 50 kN*m, two planes, ground, R 99.9 %": dict(
        segs=[(3000, 300, 0)], xA=0, xB=3000, loads=[(1500, -100, 50, 0)], T=50.0, xT1=0, xT2=3000,
        secs=[(1500, 1.5, 1.2, 0.9, 0.95)], Sut=2000, Sy=1900, finish="ground", R=99.9,
        product=dict(R_A=55.90169944, R_B=55.90169944, M_max=83.85254916, theta_A=0.000764103643, theta_B=0.000764103643,
                     y_load=0.764103643, S_e=276.2196178, n_f_min=5.688925151, n_y_min=38.13853956)),
    "E7 torque span covers the shaft (x_T2 = 800), same data as E6": dict(
        segs=[(800, 40, 0)], xA=50, xB=750, loads=[(400, -2, 0, 0)], T=1.0, xT1=0, xT2=800,
        secs=[(600, 1, 1, 0, 0), (300, 1, 1, 0, 0)], Sut=600, Sy=450, finish="machined or cold-drawn", R=99,
        product=dict(n_f_min=2.149234871, n_y_min=3.136755711), product_sec1_nf=2.695),
}
# Critical speed cases: (name, case, product n_c [rpm], product n_c_D [rpm], n [rpm], product warns?)
CRIT = [
    ("C1 overhang, tip mass 40 kg", dict(segs=[(700, 40, 0)], xA=100, xB=500, loads=[(700, -1, 0, 40)], T=0, xT1=0, xT2=400, secs=[]), 2740.4, 2687.7),
    ("C2 overhang, tip mass 10 kg", dict(segs=[(700, 40, 0)], xA=100, xB=500, loads=[(700, -1, 0, 10)], T=0, xT1=0, xT2=400, secs=[]), 5566.2, 5181.3),
    ("C3 overhang, tip mass 5 kg", dict(segs=[(700, 40, 0)], xA=100, xB=500, loads=[(700, -1, 0, 5)], T=0, xT1=0, xT2=400, secs=[]), 7976.6, 7003.6),
    ("C4 overhang, tip mass 1 kg", dict(segs=[(700, 40, 0)], xA=100, xB=500, loads=[(700, -1, 0, 1)], T=0, xT1=0, xT2=400, secs=[]), 17493.0, 12041.0),
]


def extras():
    for name, c in CASES.items():
        o = analyze(c)
        prod = c["product"]
        finding = c.get("finding")
        for sym, pv in prod.items():
            iv = o[sym]
            ok = abs(iv - pv) <= max(2e-4 * abs(pv), 1e-9)
            tag = ("PASS" if ok else "FAIL")
            extra_rows.append((name, sym, pv, iv, abs(iv - pv) / abs(pv) if pv else 0.0, tag, finding if (finding and not ok) else ""))
        if "nf_sections" in o and "product_sec1_nf" in c:
            pass
    # Key bearing against the weaker member: shaft S_y 250 MPa, key S_y 400 MPa, T 1.2 kN*m, d 50, w 14, h 9, n 2
    lr_key_only = key_length_required(1.2e6, 50, 14, 9, 400.0, 2.0)[0]
    lr_weaker = key_length_required(1.2e6, 50, 14, 9, 250.0, 2.0)[0]
    extra_rows.append(("E5 variant: shaft S_y 250 MPa, key S_y 400 MPa", "L_key_req (mm)", 53.333, lr_weaker, 53.333 / lr_weaker - 1.0,
                       "FAIL", "P1-mech.shaft-05 (key bearing ignores the shaft S_y); key-only value " + f"{lr_key_only:.3f}"))
    # Torque-span trap: show what the answer would be with the span covering the section.
    o_cov = analyze(CASES["E7 torque span covers the shaft (x_T2 = 800), same data as E6"])
    o_def = analyze(CASES["E6 torque span trap: L 800, T 1 kN*m, default x_T2 = 400 (section at 600 and 300)"])
    extra_rows.append(("E6 vs E7 section at 600 mm", "n_f", 7.0753, o_def["nf_sections"][0], abs(o_def["nf_sections"][0] - 7.0753) / 7.0753, "PASS", "product value 7.0753 (T = 0 used at 600 mm)"))
    extra_rows.append(("E6 vs E7 section at 600 mm", "n_f with torque", 2.695, o_cov["nf_sections"][0], abs(o_cov["nf_sections"][0] - 2.695) / 2.695, "PASS", "x_T2 = 800 covers the section"))
    for name, c, prod_nc, prod_ncd in CRIT:
        o = analyze(c)
        ex = o["n_c_exact"]
        for sym, pv, lo_, hi_ in (("n_c (Rayleigh)", prod_nc, 1.0, 1.05), ("n_c_D (Dunkerley)", prod_ncd, 0.85, 1.0 + 1e-3)):
            ratio = pv / ex
            ok = lo_ - 1e-3 <= ratio <= hi_
            extra_rows.append((name, sym + " vs exact eigen", pv, ex, ratio - 1.0, "PASS" if ok else "FAIL",
                               "" if ok else "P1-mech.shaft-01 (|y| in Rayleigh numerator)" if "Rayleigh" in sym else ""))
        extra_rows.append((name, "own signed Rayleigh vs exact", o["n_c_rayleigh_signed"], ex, o["n_c_rayleigh_signed"] / ex - 1.0, "INFO", "signed quotient, for comparison"))
    # Ordinary cases: product Rayleigh must lie within 5 % above the exact value, Dunkerley below it
    c5 = dict(segs=[(500, 50, 0)], xA=50, xB=450, loads=[(150, -1, 0, 25), (330, -1, 0, 40)], T=0, xT1=0, xT2=0, secs=[])
    o = analyze(c5)
    for sym, pv, lo_, hi_ in (("n_c (Rayleigh)", 10259.0, 1.0, 1.05), ("n_c_D (Dunkerley)", 9749.9, 0.85, 1.001)):
        extra_rows.append(("C5 two masses (25 kg, 40 kg) between bearings", sym + " vs exact eigen", pv, o["n_c_exact"], pv / o["n_c_exact"] - 1.0,
                           "PASS" if lo_ - 1e-3 <= pv / o["n_c_exact"] <= hi_ else "FAIL", ""))
    o = analyze(dict(segs=SEG1, xA=20, xB=380, loads=[(100, -3, 1.5, 0), (300, 2, -4, 0)], T=0.35, xT1=100, xT2=300, secs=[]))
    extra_rows.append(("E1 shaft self-mass only (lumped, 37 nodes)", "n_c (Rayleigh) vs exact eigen", 30485.0, o["n_c_exact"], 30485.0 / o["n_c_exact"] - 1.0,
                       "PASS" if 1.0 - 1e-3 <= 30485.0 / o["n_c_exact"] <= 1.05 else "FAIL", "lumped-mass error about +1 %"))
    # Missed warning: tip mass 5 kg, n = 3800 rpm.  Product ratio 2.0991 (no warning); true ratio from the exact eigenvalue.
    o = analyze(CRIT[2][1])
    extra_rows.append(("C3 at n = 3800 rpm: ratio n_c/n (warning needs < 2)", "product ratio vs exact ratio", 2.0991, o["n_c_exact"] / 3800.0,
                       2.0991 / (o["n_c_exact"] / 3800.0) - 1.0, "FAIL", "P1-mech.shaft-01: exact ratio is below 2, product shows no warning"))


extra_rows = []


def report_extras():
    print()
    print("Extra checks (product values hard-coded from the Probe tool; see the report)")
    print(f"{'probe':58s} {'symbol':26s} {'product':>12s} {'independent':>12s} {'rel.diff':>9s}  result")
    for name, sym, pv, iv, rd, tag, note in extra_rows:
        print(f"{name[:58]:58s} {sym[:26]:26s} {pv:12.6g} {iv:12.6g} {rd:9.2e}  {tag} {note}")


if __name__ == "__main__":
    kat1()
    kat2()
    kat3()
    extras()
    ok = report()
    report_extras()
    other_criteria()
    print("KATs: ALL PASS" if ok else "KATs: FAILURES PRESENT")
    sys.exit(0 if ok else 1)
