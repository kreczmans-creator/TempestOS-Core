#!/usr/bin/env python3
"""Independent re-derivation of elec.fault-protection (Track P, P1).

Method sources (cited by title and number only):
  BS 7671:2018+A2:2022, reg. 411.3.2.2/411.3.2.3 (disconnection times),
    reg. 411.4.4 (Zs * Ia <= Cmin * U0), reg. 543.1.3 and 434.5.2 (adiabatic S = sqrt(I2t)/k).
  IEC 60364-5-54 (2011), Annex A, equation for k from Q_c, beta, rho_20, theta_i, theta_f.
  IEC 60909-0 (2016), cl. 6.3.1 (transformer impedance Z_T = u_k * U_rT^2 / S_rT) and
    cl. 5.3.1 and Table 1 (voltage factor c; I_k3 = c * U_n / (sqrt(3) * Z_k)), owner to confirm.
Units: ohm, A, s, mm^2, rho in ohm*mm, Q_c in J/(K*mm^3).
Exit 0 when every check matches within max(tolerance, 1e-6).
"""
import math
import sys

CU = dict(rho20=17.241e-6, alpha=0.00393, Qc=3.45e-3, beta=234.5)


def loglog(x, xs, ys):
    """Log-log interpolation of y(x) between neighbouring points. None if outside the points."""
    pts = sorted(zip(xs, ys))
    if x < pts[0][0] or x > pts[-1][0]:
        return None
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        if x0 <= x <= x1:
            s = math.log(y1 / y0) / math.log(x1 / x0)
            return y0 * (x / x0) ** s
    return None


def k_adiabatic(theta_i, theta_f, rho20=CU["rho20"], Qc=CU["Qc"], beta=CU["beta"]):
    return math.sqrt(Qc * (beta + 20.0) / rho20 * math.log(1.0 + (theta_f - theta_i) / (beta + theta_i)))


def fault(U0=230.0, Ze=0.35, Cmin=0.95, L=20.0, Sl=2.5, Sc=1.5, theta=70.0,
          rho20=CU["rho20"], alpha=CU["alpha"], Qc=CU["Qc"], beta=CU["beta"],
          theta_i=70.0, theta_f=160.0, device="B", In=32.0, curve=None, t_req=0.4,
          I2t_let=None, Sr=None, uk=None, cmax=1.1, Icn=None):
    o = {}
    f_t = 1.0 + alpha * (theta - 20.0)
    # rho [ohm*mm] * L [m] * 1000 [mm/m] / S [mm^2] = ohm
    r12_20 = rho20 * (L * 1000.0) * (1.0 / Sl + 1.0 / Sc)
    o["R_12_20"] = r12_20
    o["R_12"] = r12_20 * f_t
    o["Z_s"] = Ze + o["R_12"]
    o["I_f"] = Cmin * U0 / o["Z_s"]
    mult = {"B": 5.0, "C": 10.0, "D": 20.0}
    if device in mult:
        o["I_a"] = mult[device] * In
        # An MCB trips within 0.1 s only when I_f reaches I_a. Below that no disconnection time is stated (DA-17/18).
        o["t_d"] = 0.1 if o["I_f"] >= o["I_a"] else None
    else:
        ts = [p[0] for p in curve]
        Is = [p[1] for p in curve]
        o["I_a"] = loglog(t_req, ts[::-1], Is[::-1])  # I as a function of t (ascending t)
        o["t_d"] = loglog(o["I_f"], Is, ts)
        if o["t_d"] is None and o["I_f"] > max(Is):
            # Above the highest current entered: the fastest time entered is an upper bound (v0.26.0 behaviour).
            o["t_d"] = min(ts)
    o["Z_s_max"] = Cmin * U0 / o["I_a"]
    o["u_Zs"] = o["Z_s"] / o["Z_s_max"]
    o["Z_test"] = 0.8 * o["Z_s_max"]
    k = k_adiabatic(theta_i, theta_f, rho20, Qc, beta)
    o["k"] = k
    if o["t_d"] is None:
        # No disconnection time: the fault energy is unknown and the adiabatic check is not made.
        o["S_min"] = None
        o["u_cpc"] = None
    else:
        # I2t = I_f^2 * t for t >= 0.1 s on a curve, else the let-through energy, else I_f^2 * 0.1 s.
        if device not in mult and o["t_d"] >= 0.1:
            i2t = o["I_f"] ** 2 * o["t_d"]
        elif I2t_let is not None:
            i2t = I2t_let
        else:
            i2t = o["I_f"] ** 2 * 0.1
        o["S_min"] = math.sqrt(i2t) / k
        o["u_cpc"] = o["S_min"] / Sc
    if Sr is not None and uk is not None:
        Un = math.sqrt(3.0) * U0
        o["Z_T"] = (uk / 100.0) * Un ** 2 / (Sr * 1000.0)
        o["I_k3"] = cmax * Un / (math.sqrt(3.0) * o["Z_T"]) / 1000.0  # kA
        if Icn:
            o["u_Icn"] = o["I_k3"] / Icn
    return o


ROWS = []


def check(case, sym, kat, ind, tol):
    tol_eff = max(tol, 1e-6)
    rel = abs(ind - kat) / abs(kat) if kat != 0 else abs(ind - kat)
    ok = rel <= tol_eff
    ROWS.append((case, sym, kat, ind, rel, tol, ok))


def kats():
    o = fault()
    c = "ka-mcb-b32"
    for sym, kat, tol in [("Z_s", 0.7900823, 1e-6), ("I_f", 276.5535, 1e-6), ("Z_s_max", 1.365625, 1e-9),
                          ("u_Zs", 0.5785499, 1e-6), ("k", 114.8361, 1e-6), ("S_min", 0.7615538, 1e-6)]:
        check(c, sym, kat, o[sym], tol)
    curve = [(5, 120), (0.4, 220), (0.1, 320), (0.01, 700)]
    o = fault(device="curve", curve=curve)
    c = "ka-fuse-curve"
    for sym, kat, tol in [("I_a", 220, 1e-9), ("Z_s_max", 0.9931818, 1e-6), ("t_d", 0.1715774, 1e-6),
                          ("S_min", 0.9975407, 1e-6)]:
        check(c, sym, kat, o[sym], tol)
    o = fault(Sr=500, uk=5, Icn=25)
    c = "ka-fault-level"
    for sym, kat, tol in [("Z_T", 0.01587, 1e-6), ("I_k3", 15.94203, 1e-6), ("u_Icn", 0.6376812, 1e-6)]:
        check(c, sym, kat, o[sym], tol)
    o = fault()
    check("ka-wiring-matters-b32", "Z_s_max", 1.37, o["Z_s_max"], 0.005)
    o = fault(theta_i=90, theta_f=250, theta=90)
    check("ka-schneider-k", "k", 143, o["k"], 0.005)


def report():
    print(f"{'case':26s} {'symbol':8s} {'KAT':>12s} {'independent':>14s} {'rel diff':>10s} {'KAT tol':>9s}  result")
    bad = 0
    for case, sym, kat, ind, rel, tol, ok in ROWS:
        if not ok:
            bad += 1
        print(f"{case:26s} {sym:8s} {kat:12.7g} {ind:14.8g} {rel:10.2e} {tol:9.1e}  {'PASS' if ok else 'FAIL'}")
    print(f"\n{len(ROWS) - bad}/{len(ROWS)} pass")
    return bad


def x(case, sym, product, indep, tol=1e-6):
    """Extra check: product value hard-coded from the probe tool, compared with the independent value."""
    check(case, sym, product, indep, tol)


CURVE = [(5, 120), (0.4, 220), (0.1, 320), (0.01, 700)]


def extras():
    # Product values below were read from `Probe run/steps elec.fault-protection` with the v0.26.0 sources.
    B16 = dict(L=50, Sl=1.5, Sc=1.0, In=16, device="C")
    o = fault(**B16)
    for sym, v in [("Z_s", 2.069071375), ("I_f", 105.6029302), ("I_a", 160), ("Z_s_max", 1.365625), ("u_Zs", 1.515109474)]:
        x("x1-C16-long-run", sym, v, o[sym])
    o = fault(device="D")
    for sym, v in [("I_a", 640), ("Z_s_max", 0.34140625), ("u_Zs", 2.31419979)]:
        x("x2-D32-default", sym, v, o[sym])
    o = fault(theta=90, theta_i=90, theta_f=250)
    for sym, v in [("Z_s", 0.8189919808), ("I_f", 266.7913791), ("k", 142.8740485), ("S_min", 0.5904980135)]:
        x("x3-xlpe-90-250", sym, v, o[sym])
    # Let-through I2t entered: S_min = sqrt(I2t)/k, product 1.231505789 mm2 for 20 kA2s.
    x("x4-I2t-20kA2s", "S_min", 1.231505789, math.sqrt(20000.0) / k_adiabatic(70, 160))
    x("x4b-I2t-90kA2s", "S_min", 2.612418284, math.sqrt(90000.0) / k_adiabatic(70, 160))
    o = fault(L=0.1, Ze=0.01)
    for sym, v in [("Z_s", 0.01220041136), ("I_f", 17909.2322), ("S_min", 49.3172018)]:
        x("x5-near-source-MCB", sym, v, o[sym])
    o = fault(device="curve", curve=CURVE, L=30, t_req=5)
    for sym, v in [("I_a", 120), ("t_d", 0.4292091212), ("Z_s_max", 1.820833333), ("S_min", 1.234050514)]:
        x("x6-fuse-t5s", sym, v, o[sym])
    o = fault(device="curve", curve=[(5, 120), (0.1, 320)])
    for sym, v in [("I_a", 226.0478829), ("t_d", 0.1789590711), ("Z_s_max", 0.9666093627)]:
        x("x7-two-point-curve", sym, v, o[sym])
    o = fault(U0=1000, Sr=5000, uk=1)
    for sym, v in [("Z_T", 0.006), ("I_k3", 183.3333333)]:
        x("x8-fault-level-1000V", sym, v, o[sym])
    o = fault(U0=120, Sr=10, uk=4, cmax=1.0)
    for sym, v in [("Z_T", 0.1728), ("I_k3", 0.6944444444), ("I_f", 144.288771), ("Z_s_max", 0.7125)]:
        x("x9-120V-small-tx", sym, v, o[sym])
    o = fault(rho20=28.264e-6, alpha=0.00403, Qc=2.5e-3, beta=228.0, Sl=16, Sc=16)
    for sym, v in [("R_12", 0.08489799), ("Z_s", 0.43489799), ("k", 76.08665308)]:
        x("x10-aluminium", sym, v, o[sym])
    o = fault(Cmin=0.9, theta=20)
    for sym, v in [("Z_s", 0.717808), ("I_f", 288.3779506), ("Z_s_max", 1.29375)]:
        x("x11-Cmin-0.9-20C", sym, v, o[sym])
    x("x12-Cmin-1.0", "Z_s_max", 1.4375, fault(Cmin=1.0)["Z_s_max"])
    o = fault(U0=400, device="curve", curve=[(5, 120), (0.4, 220), (0.2, 260), (0.1, 320), (0.01, 700)])
    for sym, v in [("I_a", 220), ("Z_s_max", 1.727272727), ("t_d", 0.030161)]:
        x("x13-U0-400-curve", sym, v, o[sym], 1e-4)
    # Re-check after DA-17/DA-18: no disconnection time means no energy and no adiabatic check.
    o = fault(**B16)
    assert o["t_d"] is None and o["S_min"] is None, "C16 long run: no t_d and no S_min expected"
    o = fault(device="curve", curve=CURVE, L=200, Sl=1.5, Sc=1.0)
    assert o["I_f"] < 120.0 and o["t_d"] is None and o["S_min"] is None, "fault current below the curve: no t_d"
    # Fault current above the highest curve point: the fastest time entered is the upper bound (finding R1).
    o = fault(device="curve", curve=[(5, 120), (0.4, 220)])
    for sym, v in [("I_a", 220), ("t_d", 0.4), ("S_min", 1.523107581), ("u_cpc", 1.015405054)]:
        x("x15-R1-curve-ends-0.4s", sym, v, o[sym])
    o = fault(device="curve", curve=CURVE, L=0.1, Ze=0.02)
    for sym, v in [("I_f", 9842.15997), ("t_d", 0.01), ("S_min", 27.10265766)]:
        x("x16-above-curve-0.01s", sym, v, o[sym])
    # Boundary: Z_e chosen so that Z_s equals Z_s_max for B32 (product gives u_Zs = 1, pass).
    ze = 1.365625 - fault()["R_12"]
    x("x14-boundary-Zs-eq-Zsmax", "u_Zs", 1.0, fault(Ze=ze)["u_Zs"])
    # Independent illustration (no product value): the I_f-based S_min is not the short-circuit S_min.
    # For I_f = 276.55 A the earth-fault S_min is 0.762 mm2. For a 90 kA2s let-through at the origin it is 2.61 mm2.
    # See finding P1-elec.fault-protection-01 in the report.


def demonstrations():
    """Differences between the product and a practical reading. They do not change the exit code."""
    print("\nDemonstrations for the findings (product value | independent value):")
    # Finding 01: conductor resistance. Maximum DC resistance at 20 degC of plain copper conductors,
    # IEC 60228 (2004) Table 2, class 1: 2.5 mm2 = 7.41 ohm/km, 1.5 mm2 = 12.1 ohm/km.
    r20 = (7.41 + 12.1) * 1e-3 * 20.0
    r70 = r20 * 1.20
    o = fault()
    print(f"  R1+R2 at 20 degC, 20 m of 2.5/1.5 mm2: product {o['R_12_20']:.5f} ohm | IEC 60228 maxima {r20:.5f} ohm "
          f"({(r20 / o['R_12_20'] - 1) * 100:.1f} % higher)")
    print(f"  Z_s at 70 degC (multiplier 1.20): product {o['Z_s']:.5f} ohm | with IEC 60228 maxima {0.35 + r70:.5f} ohm "
          f"({((0.35 + r70) / o['Z_s'] - 1) * 100:.1f} % higher)")
    # Finding 02: adiabatic check when the device does not trip in the instantaneous region.
    o = fault(L=50, Sl=1.5, Sc=1.0, In=16, device="C")
    k = o["k"]
    print(f"  C16 MCB, I_f = {o['I_f']:.1f} A < I_a = {o['I_a']:.0f} A: product (v0.26.0) states no S_min and no pass (adiabatic check not made) | "
          f"with an assumed 5 s disconnection {o['I_f'] * math.sqrt(5.0) / k:.3f} mm2")
    # Finding 03: line conductor against the short-circuit let-through.
    print(f"  B32 MCB, 2.5 mm2 line: product S_min {fault()['S_min']:.4f} mm2 from I_f | "
          f"with an assumed 90 kA2s let-through at the origin {math.sqrt(90000.0) / k_adiabatic(70, 160):.4f} mm2")
    # Info: temperature multiplier.
    print(f"  Multiplier at 70 degC: product (alpha 0.00393) {1 + 0.00393 * 50:.4f} | practical value 1.20")


if __name__ == "__main__":
    kats()
    extras()
    bad = report()
    demonstrations()
    sys.exit(1 if bad else 0)
