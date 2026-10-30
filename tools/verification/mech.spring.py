#!/usr/bin/env python3
"""Independent re-derivation of mech.spring (Track P, P1).

Method sources (cited by title, edition and section only; equation numbers are not quoted):
  Budynas and Nisbett, Shigley's Mechanical Engineering Design, 10th ed., Ch. 10 (helical springs):
    rate, Bergstraesser and Wahl shear factors, end types and solid length (Table 10-1),
    buckling (critical deflection and absolute stability), surge frequency, extension spring hook
    stresses and initial tension, torsion spring rate and inside-fibre factor K_i, Goodman fatigue (Ch. 6).
  EN 13906-1 (compression), -2 (extension), -3 (torsion): cylindrical helical springs of round wire,
    calculation. The relations for solid length with d_max, minimum coil clearance S_a and the torsion
    factor q are the ones the spec states. They are recalled here from the standard and are NOT
    confirmed against the owner's copy.
Run: python3 tools/verification/mech.spring.py
Exit code: 0 when every known-answer case matches within its tolerance, else 1.
Extra checks print PASS, FAIL or INFO. A FAIL there is a product finding, not a script error, and
does not change the exit code. Product values in the extra checks were observed with the probe tool
on the release/v0.26.0 build.
"""
import math
import sys

PI = math.pi


# ------------------------------------------------------------------ method ---
def bergstraesser(C):
    return (4 * C + 2) / (4 * C - 3)


def wahl(C):
    return (4 * C - 1) / (4 * C - 4) + 0.615 / C


def kc_of(corr, C):
    return {"B": bergstraesser, "W": wahl, "N": lambda c: 1.0}[corr](C)


# end type -> (e_n, e_s) for EN; pitch (a, b); Shigley (e_n, e_s)
EN_ENDS = {"plain": (0, 1), "plain_g": (1, 0), "sq": (2, 1.5), "sq_g": (2, 0)}
SH_ENDS = {"plain": (0, 1), "plain_g": (1, 0), "sq": (2, 1), "sq_g": (2, 0)}
PITCH = {"plain": (1, 0), "plain_g": (0, 1), "sq": (3, 0), "sq_g": (2, 0)}


def goodman_inv(x1, x2, se, su):
    a = (x2 - x1) / 2
    m = (x2 + x1) / 2
    return a / se + m / su


def spring(i):
    """Return a dict of results. Units: mm, N, MPa, GPa, kg/m3, Hz. Torques in N*m, angles in deg."""
    o = {}
    mode = i["mode"]
    d, D, n = i["d"], i["D"], i["n"]
    C = D / d
    o["C"] = C
    G = i.get("G", 80) * 1e3
    E = i.get("E", 206) * 1e3
    rho = i.get("rho", 7850)
    # tensile strength
    if "Rm" in i:
        Rm = i["Rm"]
    else:
        Rm = i["Rm_A"] / d ** i["Rm_m"]
    o["Rm"] = Rm
    f_tau, f_sig, f_su = i.get("f_tau", 0.45), i.get("f_sigma", 0.75), i.get("f_su", 0.67)
    Se = i["S_e"]
    # surge (both ends fixed): f = d/(2 pi n D^2) sqrt(G/(2 rho)), d and D in m
    def surge(nn):
        return (d / 1e3) / (2 * PI * nn * (D / 1e3) ** 2) * math.sqrt(G * 1e6 / (2 * rho))
    if mode in ("comp", "ext"):
        k = G * d ** 4 / (8 * D ** 3 * n)
        kc = kc_of(i.get("corr", "B"), C)
        o["k"], o["k_c"] = k, kc
        t = lambda F: kc * 8 * F * D / (PI * d ** 3)
        if mode == "comp":
            if i["work"] == "loads":
                F1, F2 = i["F_1"], i["F_2"]
                s1, s2 = F1 / k, F2 / k
            else:
                s1, s2 = i["s_1"], i["s_2"]
                F1, F2 = k * s1, k * s2
            o.update(F_1=F1, F_2=F2, s_1=s1, s_2=s2)
            L0 = i["L0"]
            ends = i["ends"]
            if i.get("solid", "EN") == "EN":
                en, es = EN_ENDS[ends]
                dm = i.get("d_max", d)
            else:
                en, es = SH_ENDS[ends]
                dm = d
            nt = n + en
            Ls = dm * (nt + es)
            o["n_t"], o["L_s"] = nt, Ls
            a, b = PITCH[ends]
            o["p"] = (L0 - a * d) / (n + b)
            o["L_1"], o["L_2"] = L0 - s1, L0 - s2
            sc = L0 - Ls
            Fc = k * sc
            o["s_c"], o["F_c"] = sc, Fc
            o["tau_1"], o["tau_2"], o["tau_c"] = t(F1), t(F2), t(Fc)
            tp = f_tau * Rm
            o["u_2"], o["u_c"], o["u_s"] = t(F2) / tp, t(Fc) / tp, s2 / sc
            if i.get("solid", "EN") == "EN":
                Sa = (0.0015 * D ** 2 / d + 0.1 * d) * n
                Ln = Ls + Sa
                o["S_a"], o["L_n"] = Sa, Ln
                o["s_n"] = L0 - Ln
                o["u_n"] = s2 / (L0 - Ln)
            # buckling
            nu = i["nu_seat"]
            lam = nu * L0 / D
            C1p = E / (2 * (E - G))
            C2p = 2 * PI ** 2 * (E - G) / (2 * G + E)
            if lam ** 2 < C2p:
                o["y_cr"] = math.inf
                o["u_b"] = 0.0
            else:
                ycr = L0 * C1p * (1 - math.sqrt(1 - C2p / lam ** 2))
                o["y_cr"], o["u_b"] = ycr, s2 / ycr
            o["tau_lo"], o["tau_hi"] = t(F1), t(F2)
            o["u_f"] = goodman_inv(t(F1), t(F2), Se, f_su * Rm)
        else:  # extension
            F0 = i["F_0"]
            if i["work"] == "loads":
                F1, F2 = i["F_1"], i["F_2"]
                s1, s2 = (F1 - F0) / k, (F2 - F0) / k
            else:
                s1, s2 = i["s_1"], i["s_2"]
                F1, F2 = F0 + k * s1, F0 + k * s2
            o.update(F_1=F1, F_2=F2, s_1=s1, s_2=s2)
            o["L_s"] = d * (n + 1)
            o["tau_0"], o["tau_1"], o["tau_2"] = t(F0), t(F1), t(F2)
            o["u_2"] = t(F2) / (f_tau * Rm)
            C1 = 2 * i["r_h1"] / d
            C2 = 2 * i["r_h2"] / d
            KA = (4 * C1 ** 2 - C1 - 1) / (4 * C1 * (C1 - 1))
            KB = (4 * C2 - 1) / (4 * C2 - 4)
            sA = F2 * (KA * 16 * D / (PI * d ** 3) + 4 / (PI * d ** 2))
            tB = KB * 8 * F2 * D / (PI * d ** 3)
            o.update(K_A=KA, K_B=KB, sigma_A=sA, tau_B=tB, u_A=sA / (f_sig * Rm), u_B=tB / (f_tau * Rm))
            o["u_f"] = goodman_inv(t(F1), t(F2), Se, f_su * Rm)
        o["f_n"] = surge(n)
        if "f_op" in i:
            o["r_f"] = o["f_n"] / i["f_op"]
        o["n_f"] = 1 / o["u_f"]
    else:  # torsion
        l1, l2 = i.get("l_1", 0), i.get("l_2", 0)
        na = n + (l1 + l2) / (3 * PI * D)
        kphi = E * d ** 4 / (64 * D * na) / 1e3  # N*mm/rad -> N*m/rad
        o["n_a"], o["k_phi"] = na, kphi
        if i.get("tors", "EN") == "EN":
            q = (C + 0.07) / (C - 0.75)
            o["q"] = q
        else:
            q = (4 * C ** 2 - C - 1) / (4 * C * (C - 1))
            o["K_i"] = q
        if i["work"] == "loads":
            M1, M2 = i["M_1"], i["M_2"]
            p1, p2 = M1 / kphi, M2 / kphi
        else:
            p1, p2 = math.radians(i["phi_1"]), math.radians(i["phi_2"])
            M1, M2 = kphi * p1, kphi * p2
        o.update(phi_1=math.degrees(p1), phi_2=math.degrees(p2), M_1=M1, M_2=M2)
        sg = lambda M: q * 32 * M * 1e3 / (PI * d ** 3)
        o["sigma_1"], o["sigma_2"] = sg(M1), sg(M2)
        o["u_2"] = sg(M2) / (f_sig * Rm)
        turns = p2 / (2 * PI)
        o["D_w"] = D * n / (n + turns)
        o["L_w"] = d * (n + 1 + turns)
        o["u_f"] = goodman_inv(sg(M1), sg(M2), Se, Rm)
        o["n_f"] = 1 / o["u_f"]
    return o


# ----------------------------------------------------------- KAT inputs ------
ka_comp = dict(mode="comp", d=2, D=16, n=8, corr="B", ends="sq_g", L0=45, work="loads", F_1=10, F_2=40,
               G=80, E=206, rho=7850, Rm=1800, f_tau=0.45, f_su=0.67, nu_seat=0.5, S_e=400, f_op=10)
ka_wahl = dict(mode="comp", d=2, D=20, n=10, corr="W", ends="plain_g", L0=80, work="defl", s_1=4, s_2=12,
               G=80, E=206, rho=7850, Rm=1800, f_tau=0.45, f_su=0.67, nu_seat=1, S_e=350, f_op=25)
ka_ext = dict(mode="ext", d=2, D=16, n=20, corr="B", work="loads", F_0=10, F_1=30, F_2=60, r_h1=8, r_h2=4,
              G=80, rho=7850, Rm=1800, f_tau=0.45, f_sigma=0.75, f_su=0.67, S_e=400, f_op=10)
ka_tors = dict(mode="tors", d=2, D=16, n=6, tors="SH", work="loads", l_1=30, l_2=30, M_1=0.05, M_2=0.15,
               E=206, Rm=1800, f_sigma=0.75, S_e=500)
ka_tors_en = dict(ka_tors, tors="EN")
ka_sq = dict(mode="comp", d=2, d_max=2.04, D=16, n=8, corr="B", ends="sq", solid="EN", L0=45, work="loads",
             F_1=10, F_2=40, G=80, E=206, rho=7850, Rm=1800, f_tau=0.45, f_su=0.67, nu_seat=0.5, S_e=400)
ka_rm = dict(mode="comp", d=2, D=16, n=8, corr="B", ends="sq_g", L0=45, work="loads", F_1=10, F_2=40,
             G=80, E=206, rho=7850, Rm_A=1800, Rm_m=0.1, f_tau=0.45, f_su=0.67, nu_seat=0.5, S_e=400)
ka_shig = dict(mode="comp", d=2.5, D=28.5, n=13, corr="B", ends="plain_g", solid="SH", L0=162.72, work="loads",
               F_1=40, F_2=120, G=81, E=206, rho=7850, Rm=1935.9, f_tau=0.45, f_su=0.67, nu_seat=0.5, S_e=400)

# (case, inputs, [(symbol, KAT expected, KAT tol)])
KATS = [
    ("ka-compression", ka_comp, [("C", 8, 1e-9), ("k", 4.8828125, 1e-9), ("k_c", 1.17241379, 1e-7), ("L_s", 20, 1e-9),
        ("s_2", 8.192, 1e-9), ("tau_2", 238.842177, 1e-7), ("F_c", 122.070312, 1e-7), ("tau_c", 728.888479, 1e-7),
        ("u_c", 0.899862319, 1e-7), ("f_n", 350.845041, 1e-7), ("r_f", 35.0845041, 1e-7), ("u_f", 0.347692617, 1e-7)]),
    ("ka-wahl-slender", ka_wahl, [("k", 2, 1e-9), ("F_1", 8, 1e-9), ("F_2", 24, 1e-9), ("k_c", 1.14483333, 1e-7),
        ("L_s", 22, 1e-9), ("y_cr", 15.7950074, 1e-7), ("u_b", 0.759733736, 1e-7), ("f_n", 179.632661, 1e-7),
        ("r_f", 7.18530644, 1e-7), ("n_f", 3.79821968, 1e-7)]),
    ("ka-extension", ka_ext, [("k", 1.953125, 1e-9), ("s_1", 10.24, 1e-9), ("s_2", 25.6, 1e-9), ("L_s", 42, 1e-9),
        ("tau_0", 59.7105442, 1e-7), ("tau_2", 358.263265, 1e-7), ("sigma_A", 693.006095, 1e-7),
        ("tau_B", 381.971863, 1e-7), ("u_A", 0.513337848, 1e-7), ("f_n", 140.338016, 1e-7), ("u_f", 0.446715079, 1e-7)]),
    ("ka-torsion", ka_tors, [("n_a", 6.39788736, 1e-7), ("k_phi", 0.50309576, 1e-7), ("K_i", 1.10267857, 1e-7),
        ("phi_1", 5.69432145, 1e-7), ("phi_2", 17.0829644, 1e-7), ("sigma_2", 210.596094, 1e-7),
        ("D_w", 15.8744525, 1e-7), ("L_w", 14.0949054, 1e-7), ("u_2", 0.155997107, 1e-7), ("u_f", 0.21839595, 1e-7)]),
    ("ka-torsion-en", ka_tors_en, [("n_a", 6.39788736, 1e-7), ("q", 1.11310344828, 1e-9),
        ("sigma_2", 212.587099159, 1e-7), ("u_2", 0.157471925303, 1e-7), ("u_f", 0.220460695424, 1e-7)]),
    ("ka-squared-en", ka_sq, [("k", 4.8828125, 1e-9), ("n_t", 10, 1e-9), ("L_s", 23.46, 1e-9), ("S_a", 3.136, 1e-9),
        ("L_n", 26.596, 1e-9), ("L_2", 36.808, 1e-9), ("s_c", 21.54, 1e-9), ("s_n", 18.404, 1e-9),
        ("u_s", 0.380315691736, 1e-7), ("u_n", 0.445120625951, 1e-7), ("F_c", 105.17578125, 1e-7),
        ("u_c", 0.775321374273, 1e-7)]),
    ("ka-rm-curve", ka_rm, [("Rm", 1679.45938477, 1e-9), ("u_2", 0.316030502082, 1e-7),
        ("u_c", 0.964448553718, 1e-7), ("u_f", 0.356576598403, 1e-7)]),
    ("ka-shigley-10-3", ka_shig, [("C", 11.4, 1e-9), ("k", 1.3142, 1e-4), ("k_c", 1.1174, 1e-4), ("L_s", 35, 1e-9),
        ("F_c", 167.86, 2e-4), ("u_c", 1.0, 1e-4)]),
]


def rel(a, b):
    return abs(a - b) / max(abs(b), 1e-300) if b != 0 else abs(a)


def main():
    fails = 0
    total = 0
    print("Known-answer cases")
    print(f"{'case':<18}{'symbol':<9}{'KAT expected':>16}{'independent':>16}{'rel diff':>11}{'KAT tol':>9}  result")
    for case, inp, exp in KATS:
        out = spring(inp)
        for sym, ev, tol in exp:
            iv = out[sym]
            r = rel(iv, ev)
            ok = r <= max(tol, 1e-6)
            total += 1
            fails += 0 if ok else 1
            print(f"{case:<18}{sym:<9}{ev:>16.9g}{iv:>16.9g}{r:>11.2e}{tol:>9.0e}  {'PASS' if ok else 'FAIL'}")
    print(f"\nKAT rows matched: {total - fails}/{total}")

    # Shigley 10-3 cross-check of the published chain (solid force from permissible shear, then L0)
    d, D, n, G, Rm = 2.5, 28.5, 13, 81e3, 1935.9
    C = D / d
    KB = bergstraesser(C)
    k = G * d ** 4 / (8 * D ** 3 * n)
    Fs = PI * d ** 3 * 0.45 * Rm / (8 * KB * D)
    L0 = 35 + Fs / k
    print(f"\nShigley 10-3 chain: k={k:.4f} N/mm, K_B={KB:.4f}, F_s={Fs:.3f} N (manual 167.86), L0={L0:.3f} mm (manual 162.72)")
    print(f"  Sut = 2211/2.5^0.145 = {2211 / 2.5 ** 0.145:.1f} MPa (Table 10-4 music wire A, m as I recall them)")

    extra_checks()
    return 1 if fails else 0


# ----------------------------------------------------------- extra checks ----
# Each row: (name, inputs, [(symbol, product value observed with the probe tool)]).
# P1 plain ends, Wahl, deflections; P2 squared EN with d_max; P3 squared Shigley; P4 slender, buckling WARN;
# P5 index 3, near-zero load, uncorrected; P6 d = 20 mm, surge WARN, clearance FAIL; P7 d = 1 mm, index 3;
# P8 R_m from the W3 curve, one end free; P9 extension by deflections; P10 extension with small hook radii;
# P11 torsion EN q by angles with legs; P12 torsion Shigley, index 4, zero first torque; P13 torsion, d = 1 mm;
# P14 the KAT compression case with the correction factor set to None (fatigue uses the uncorrected stress too).
PROBES = [
    ('P1', {'mode': 'comp', 'd': 3, 'D': 24, 'n': 6.5, 'corr': 'W', 'ends': 'plain', 'L0': 70, 'work': 'defl', 's_1': 5, 's_2': 20, 'Rm': 1700, 'S_e': 450, 'f_op': 15, 'nu_seat': 0.5},
     [('C', 8.0), ('Rm', 1700.0), ('k', 9.014423077), ('k_c', 1.184017857), ('n_t', 6.5), ('L_s', 22.5), ('p', 10.30769231), ('F_1', 45.07211538), ('F_2', 180.2884615), ('s_1', 5.0), ('s_2', 20.0), ('L_1', 65.0), ('L_2', 50.0), ('s_c', 47.5), ('F_c', 428.1850962), ('tau_1', 120.7963427), ('tau_2', 483.185371), ('tau_c', 1147.565256), ('u_2', 0.631614864), ('u_c', 1.500085302), ('u_s', 0.4210526316), ('S_a', 3.822), ('L_n', 26.322), ('s_n', 43.678), ('u_n', 0.4578964238), ('u_b', 0.0), ('f_n', 287.8728541), ('r_f', 19.19152361), ('u_f', 0.6677913124), ('n_f', 1.497473809)]),
    ('P2', {'mode': 'comp', 'd': 3, 'D': 24, 'n': 6.5, 'corr': 'B', 'ends': 'sq', 'L0': 70, 'work': 'loads', 's_1': 5, 's_2': 20, 'Rm': 1700, 'S_e': 450, 'f_op': 15, 'nu_seat': 0.5, 'solid': 'EN', 'd_max': 3.05, 'F_1': 50, 'F_2': 200},
     [('C', 8.0), ('Rm', 1700.0), ('k', 9.014423077), ('k_c', 1.172413793), ('n_t', 8.5), ('L_s', 30.5), ('p', 9.384615385), ('F_1', 50.0), ('F_2', 200.0), ('s_1', 5.546666667), ('s_2', 22.18666667), ('L_1', 64.45333333), ('L_2', 47.81333333), ('s_c', 39.5), ('F_c', 356.0697115), ('tau_1', 132.6900981), ('tau_2', 530.7603926), ('tau_c', 944.9384994), ('u_2', 0.6938044348), ('u_c', 1.235213725), ('u_s', 0.5616877637), ('S_a', 3.822), ('L_n', 34.322), ('s_n', 35.678), ('u_n', 0.6218584749), ('u_b', 0.0), ('f_n', 287.8728541), ('r_f', 19.19152361), ('u_f', 0.7335428604), ('n_f', 1.363246858)]),
    ('P3', {'mode': 'comp', 'd': 3, 'D': 24, 'n': 6.5, 'corr': 'B', 'ends': 'sq', 'L0': 70, 'work': 'loads', 's_1': 5, 's_2': 20, 'Rm': 1700, 'S_e': 450, 'f_op': 15, 'nu_seat': 0.5, 'solid': 'SH', 'F_1': 50, 'F_2': 200},
     [('C', 8.0), ('Rm', 1700.0), ('k', 9.014423077), ('k_c', 1.172413793), ('n_t', 8.5), ('L_s', 28.5), ('p', 9.384615385), ('F_1', 50.0), ('F_2', 200.0), ('s_1', 5.546666667), ('s_2', 22.18666667), ('L_1', 64.45333333), ('L_2', 47.81333333), ('s_c', 41.5), ('F_c', 374.0985577), ('tau_1', 132.6900981), ('tau_2', 530.7603926), ('tau_c', 992.7834867), ('u_2', 0.6938044348), ('u_c', 1.297756192), ('u_s', 0.5346184739), ('u_b', 0.0), ('f_n', 287.8728541), ('r_f', 19.19152361), ('u_f', 0.7335428604), ('n_f', 1.363246858)]),
    ('P4', {'mode': 'comp', 'd': 3, 'D': 30, 'n': 12, 'corr': 'B', 'ends': 'plain_g', 'L0': 200, 'work': 'defl', 's_1': 10, 's_2': 60, 'Rm': 1700, 'S_e': 450, 'nu_seat': 1, 'f_op': 5},
     [('C', 10.0), ('Rm', 1700.0), ('k', 2.5), ('k_c', 1.135135135), ('n_t', 13.0), ('L_s', 39.0), ('p', 15.38461538), ('F_1', 25.0), ('F_2', 150.0), ('s_1', 10.0), ('s_2', 60.0), ('L_1', 190.0), ('L_2', 140.0), ('s_c', 161.0), ('F_c', 402.5), ('tau_1', 80.2943857), ('tau_2', 481.7663142), ('tau_c', 1292.73961), ('u_2', 0.6297598879), ('u_c', 1.689855699), ('u_s', 0.3726708075), ('S_a', 9.0), ('L_n', 48.0), ('s_n', 152.0), ('u_n', 0.3947368421), ('y_cr', 13.01699955), ('u_b', 4.609357154), ('f_n', 99.79592275), ('r_f', 19.95918455), ('u_f', 0.692814205), ('n_f', 1.443388419)]),
    ('P5', {'mode': 'comp', 'd': 6, 'D': 18, 'n': 5, 'corr': 'N', 'ends': 'sq_g', 'L0': 60, 'work': 'loads', 'F_1': 0, 'F_2': 0.01, 'Rm': 1500, 'S_e': 300, 'nu_seat': 0.5},
     [('C', 3.0), ('Rm', 1500.0), ('k', 444.4444444), ('k_c', 1.0), ('n_t', 7.0), ('L_s', 42.0), ('p', 9.6), ('F_1', 0.0), ('F_2', 0.01), ('s_1', 0.0), ('s_2', 2.25e-05), ('L_1', 60.0), ('L_2', 59.9999775), ('s_c', 18.0), ('F_c', 8000.0), ('tau_1', 0.0), ('tau_2', 0.002122065908), ('tau_c', 1697.652726), ('u_2', 3.143801345e-06), ('u_c', 2.515041076), ('u_s', 1.25e-06), ('S_a', 3.405), ('L_n', 45.405), ('s_n', 14.595), ('u_n', 1.541623844e-06), ('u_b', 0.0), ('f_n', 1330.612303), ('u_f', 4.592530696e-06), ('n_f', 217744.8701)]),
    ('P6', {'mode': 'comp', 'd': 20, 'D': 160, 'n': 7, 'corr': 'B', 'ends': 'sq_g', 'L0': 300, 'work': 'defl', 's_1': 20, 's_2': 100, 'Rm': 1400, 'S_e': 350, 'nu_seat': 0.7, 'G': 79, 'E': 206, 'f_op': 20},
     [('C', 8.0), ('Rm', 1400.0), ('k', 55.10602679), ('k_c', 1.172413793), ('n_t', 9.0), ('L_s', 180.0), ('p', 37.14285714), ('F_1', 1102.120536), ('F_2', 5510.602679), ('s_1', 20.0), ('s_2', 100.0), ('L_1', 280.0), ('L_2', 200.0), ('s_c', 120.0), ('F_c', 6612.723214), ('tau_1', 65.80821693), ('tau_2', 329.0410846), ('tau_c', 394.8493016), ('u_2', 0.5222874359), ('u_c', 0.6267449231), ('u_s', 0.8333333333), ('S_a', 27.44), ('L_n', 207.44), ('s_n', 92.56), ('u_n', 1.080380294), ('u_b', 0.0), ('f_n', 39.84518443), ('r_f', 1.992259222), ('u_f', 0.5865209952), ('n_f', 1.704968805)]),
    ('P7', {'mode': 'comp', 'd': 1, 'D': 3, 'n': 10, 'corr': 'W', 'ends': 'plain_g', 'L0': 40, 'work': 'loads', 'F_1': 1, 'F_2': 3, 'Rm': 2000, 'S_e': 500, 'nu_seat': 0.5},
     [('C', 3.0), ('Rm', 2000.0), ('k', 37.03703704), ('k_c', 1.58), ('n_t', 11.0), ('L_s', 11.0), ('p', 3.636363636), ('F_1', 1.0), ('F_2', 3.0), ('s_1', 0.027), ('s_2', 0.081), ('L_1', 39.973), ('L_2', 39.919), ('s_c', 29.0), ('F_c', 1074.074074), ('tau_1', 12.07031088), ('tau_2', 36.21093265), ('tau_c', 12964.40799), ('u_2', 0.04023436961), ('u_c', 14.40489776), ('u_s', 0.002793103448), ('S_a', 1.135), ('L_n', 12.135), ('s_n', 27.865), ('u_n', 0.002906872421), ('y_cr', 2.60339991), ('u_b', 0.03111316079), ('f_n', 3991.83691), ('u_f', 0.04215601115), ('n_f', 23.72140942)]),
    ('P8', {'mode': 'comp', 'd': 12, 'D': 100, 'n': 4, 'corr': 'B', 'ends': 'plain', 'L0': 150, 'work': 'defl', 's_1': 5, 's_2': 60, 'rm_source': 'W3 material curve R_m = A/d^m', 'Rm_A': 2000, 'Rm_m': 0.15, 'S_e': 300, 'nu_seat': 2},
     [('C', 8.333333333), ('Rm', 1377.694132), ('k', 51.84), ('k_c', 1.164835165), ('n_t', 4.0), ('L_s', 60.0), ('p', 34.5), ('F_1', 259.2), ('F_2', 3110.4), ('s_1', 5.0), ('s_2', 60.0), ('L_1', 145.0), ('L_2', 90.0), ('s_c', 90.0), ('F_c', 4665.6), ('tau_1', 44.49342585), ('tau_2', 533.9211102), ('tau_c', 800.8816653), ('u_2', 0.8612153656), ('u_c', 1.291823048), ('u_s', 0.6666666667), ('S_a', 9.8), ('L_n', 69.8), ('s_n', 80.2), ('u_n', 0.7481296758), ('y_cr', 61.93212002), ('u_b', 0.9688026178), ('f_n', 107.7795966), ('u_f', 1.129028099), ('n_f', 0.8857175486)]),
    ('P9', {'mode': 'ext', 'd': 3, 'D': 24, 'n': 15, 'corr': 'W', 'work': 'defl', 'F_0': 30, 's_1': 5, 's_2': 30, 'r_h1': 10, 'r_h2': 6, 'Rm': 1700, 'S_e': 450, 'f_op': 5, 'f_sigma': 0.75},
     [('C', 8.0), ('Rm', 1700.0), ('k', 3.90625), ('k_c', 1.184017857), ('L_s', 48.0), ('F_1', 49.53125), ('F_2', 147.1875), ('s_1', 5.0), ('s_2', 30.0), ('tau_1', 132.7471276), ('tau_2', 394.4725368), ('u_2', 0.515650375), ('f_n', 124.7449034), ('r_f', 24.94898069), ('u_f', 0.5222457225), ('n_f', 1.91480745), ('tau_0', 80.40204573), ('K_A', 1.125735294), ('K_B', 1.25), ('sigma_A', 770.9325013), ('tau_B', 416.4554344), ('u_A', 0.6046529422), ('u_B', 0.5443861888)]),
    ('P10', {'mode': 'ext', 'd': 1.5, 'D': 12, 'n': 30, 'corr': 'N', 'work': 'loads', 'F_0': 5, 'F_1': 5, 'F_2': 20, 'r_h1': 1.0, 'r_h2': 0.8, 'Rm': 1800, 'S_e': 400, 'f_sigma': 0.75},
     [('C', 8.0), ('Rm', 1800.0), ('k', 0.9765625), ('k_c', 1.0), ('L_s', 46.5), ('F_1', 5.0), ('F_2', 20.0), ('s_1', 0.0), ('s_2', 15.36), ('tau_1', 45.27073937), ('tau_2', 181.0829575), ('u_2', 0.2235592068), ('f_n', 124.7449034), ('u_f', 0.2636100889), ('n_f', 3.793481517), ('tau_0', 45.27073937), ('K_A', 2.6875), ('K_B', 12.25), ('sigma_A', 984.6385813), ('tau_B', 2218.266229), ('u_A', 0.729361912), ('u_B', 2.738600283)]),
    ('P11', {'mode': 'tors', 'd': 3, 'D': 24, 'n': 7.25, 'tors': 'EN', 'work': 'defl', 'l_1': 40, 'l_2': 60, 'phi_1': 20, 'phi_2': 90, 'Rm': 1700, 'S_e': 500, 'f_sigma': 0.75},
     [('C', 8.0), ('Rm', 1700.0), ('u_2', 0.7306305535), ('u_f', 1.059414303), ('n_f', 0.9439177832), ('n_a', 7.692097064), ('k_phi', 1.412265233), ('q', 1.113103448), ('phi_1', 20.0), ('phi_2', 90.00000001), ('M_1', 0.4929735644), ('M_2', 2.21838104), ('sigma_1', 207.0119902), ('sigma_2', 931.5539558), ('D_w', 23.2), ('L_w', 25.5)]),
    ('P12', {'mode': 'tors', 'd': 2, 'D': 8, 'n': 3, 'tors': 'SH', 'work': 'loads', 'l_1': 0, 'l_2': 0, 'M_1': 0, 'M_2': 0.2, 'Rm': 1800, 'S_e': 500, 'f_sigma': 0.75},
     [('C', 4.0), ('Rm', 1800.0), ('u_2', 0.2318553492), ('u_f', 0.3999504774), ('n_f', 2.500309555), ('n_a', 3.0), ('k_phi', 2.145833333), ('K_i', 1.229166667), ('phi_1', 0.0), ('phi_2', 5.340189159), ('M_1', 0.0), ('M_2', 0.2), ('sigma_1', 0.0), ('sigma_2', 313.0047214), ('D_w', 7.960637675), ('L_w', 8.029667718)]),
    ('P13', {'mode': 'tors', 'd': 1, 'D': 14, 'n': 12, 'tors': 'EN', 'work': 'loads', 'l_1': 20, 'l_2': 20, 'M_1': 0.001, 'M_2': 0.01, 'Rm': 2000, 'S_e': 500, 'f_sigma': 0.75},
     [('C', 14.0), ('Rm', 2000.0), ('u_2', 0.07210860033), ('u_f', 0.1270914081), ('n_f', 7.868352512), ('n_a', 12.30315227), ('k_phi', 0.01868713881), ('q', 1.061886792), ('phi_1', 3.066054151), ('phi_2', 30.66054151), ('M_1', 0.001), ('M_2', 0.01), ('sigma_1', 10.81629005), ('sigma_2', 108.1629005), ('D_w', 13.90133738), ('L_w', 13.08516817)]),
    ('P14', {'mode': 'comp', 'd': 2, 'D': 16, 'n': 8, 'corr': 'N', 'ends': 'sq_g', 'L0': 45, 'work': 'loads', 'F_1': 10, 'F_2': 40, 'G': 80, 'E': 206, 'rho': 7850, 'Rm': 1800, 'f_tau': 0.45, 'f_su': 0.67, 'nu_seat': 0.5, 'S_e': 400, 'f_op': 10},
     [('C', 8.0), ('Rm', 1800.0), ('k', 4.8828125), ('k_c', 1.0), ('n_t', 10.0), ('L_s', 20.0), ('p', 5.125), ('F_1', 10.0), ('F_2', 40.0), ('s_1', 2.048), ('s_2', 8.192), ('L_1', 42.952), ('L_2', 36.808), ('s_c', 25.0), ('F_c', 122.0703125), ('tau_1', 50.92958179), ('tau_2', 203.7183272), ('tau_c', 621.6989965), ('u_2', 0.2515041076), ('u_c', 0.7675296253), ('u_s', 0.32768), ('S_a', 3.136), ('L_n', 23.136), ('s_n', 21.864), ('u_n', 0.374679839), ('u_b', 0.0), ('f_n', 350.8450409), ('r_f', 35.08450409), ('u_f', 0.29656135), ('n_f', 3.371983571)]),
]


def extra_checks():
    print("\nExtra checks (product values observed with the probe tool)")
    print(f"{'probe':<10}{'symbol':<9}{'product':>16}{'independent':>16}{'rel diff':>11}  result")
    for name, inp, obs in PROBES:
        out = spring(inp)
        for sym, pv in obs:
            iv = out.get(sym)
            if iv is None:
                print(f"{name:<10}{sym:<9}{pv:>16.9g}{'n/a':>16}")
                continue
            if isinstance(pv, str):
                print(f"{name:<10}{sym:<9}{pv:>16}{iv!s:>16}  INFO")
                continue
            r = rel(pv, iv) if math.isfinite(iv) else (0.0 if not math.isfinite(pv) else 1.0)
            print(f"{name:<10}{sym:<9}{pv:>16.9g}{iv:>16.9g}{r:>11.2e}  {'PASS' if r <= 1e-6 else 'FAIL'}")


if __name__ == "__main__":
    sys.exit(main())
