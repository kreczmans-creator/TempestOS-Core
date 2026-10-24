#!/usr/bin/env python3
"""Independent re-derivation of elec.load-budget (Track P, P1).

Method sources (cited by title and number only):
  Load budget arithmetic: demand P*d*k per load, apparent power P*d*k/cos(phi), arithmetic sum;
    three-phase current I = S/(sqrt(3)*U), single-phase I = S/U, DC current I = P/U.
  Heat dissipated per load (1 - eta) * P_in.
  PSU sizing: P_out >= margin * P_load / f_derate (manufacturer derating curve value).
  Transformer: S >= margin * S_demand; starting check S_peak = S + max((m_i - 1) * S_i).
  Battery: C = I*t / (DoD * eta_b * k_temp * k_age).
  Peukert: Linden's Handbook of Batteries, 5th ed., Ch. 3-4 (Peukert relation t = H*(C/(I*H))^k),
    solved for the rated capacity: C = I*H*(t/H)^(1/k).
Units: kW, kVA, A, V, h, A*h.
Exit 0 when every check matches within max(tolerance, 1e-6).
"""
import math
import sys

AC3, AC1, DC = "AC three-phase", "AC single-phase", "DC"


def budget(loads, U3=400.0, U1=230.0, UDC=24.0, margin=1.25, f_derate=1.0, P_PSU=None, S_T=None,
           k_T=3.0, t_auto=0.0, Ub=24.0, eta_conv=1.0, DoD=0.8, eta_b=0.95, k_temp=1.0, k_age=0.8,
           k_p=None, H=None, C_batt=None):
    """loads: list of dicts with name, supply, P (kW), cosphi, eta, d, k, inrush, backed."""
    o = {}
    sums = {AC3: [0.0, 0.0], AC1: [0.0, 0.0], DC: [0.0, 0.0]}  # [P_dem, S_dem]
    o["P_conn"] = sum(l["P"] for l in loads)
    o["P_dem"] = 0.0
    o["S_dem"] = 0.0
    o["Phi"] = 0.0
    ib_w = 0.0
    s_i_max_extra = 0.0
    for l in loads:
        pdk = l["P"] * l["d"] * l["k"]
        cos = 1.0 if l["supply"] == DC else l["cosphi"]
        s = pdk / cos
        sums[l["supply"]][0] += pdk
        sums[l["supply"]][1] += s
        o["P_dem"] += pdk
        o["S_dem"] += s
        o["Phi"] += (1.0 - l["eta"]) * pdk
        if l["backed"]:
            ib_w += pdk * 1000.0
        # starting kVA: apparent power of the load at its demand
        extra = (l["inrush"] - 1.0) * s
        s_i_max_extra = max(s_i_max_extra, extra)
    o["I_3ph"] = sums[AC3][1] * 1000.0 / (math.sqrt(3.0) * U3)
    o["I_1ph"] = sums[AC1][1] * 1000.0 / U1
    o["I_DC"] = sums[DC][0] * 1000.0 / UDC
    o["P_PSU_req"] = margin * sums[DC][0] * 1000.0 / f_derate
    o["S_T_req"] = margin * o["S_dem"]
    o["S_peak"] = o["S_dem"] + s_i_max_extra
    o["I_b"] = ib_w / (Ub * eta_conv)
    if k_p and H:
        o["Ah"] = o["I_b"] * H * (t_auto / H) ** (1.0 / k_p)
    else:
        o["Ah"] = o["I_b"] * t_auto
    o["C_req"] = o["Ah"] / (DoD * eta_b * k_temp * k_age)
    if P_PSU:
        o["u_PSU"] = o["P_PSU_req"] / P_PSU
    if S_T:
        o["u_T"] = o["S_T_req"] / S_T
        o["u_peak"] = o["S_peak"] / (k_T * S_T)
    if C_batt:
        o["u_batt"] = o["C_req"] / C_batt
    return o


def L(name, supply, P, cosphi, eta, d, k, inrush, backed):
    return dict(name=name, supply=supply, P=P, cosphi=cosphi, eta=eta, d=d, k=k, inrush=inrush, backed=backed)


ROWS = []


def check(case, sym, kat, ind, tol):
    tol_eff = max(tol, 1e-6)
    rel = abs(ind - kat) / abs(kat) if kat != 0 else abs(ind - kat)
    ROWS.append((case, sym, kat, ind, rel, tol, rel <= tol_eff))


def kats():
    mixed = [L("Pump motor", AC3, 7.5, 0.85, 0.9, 1, 1, 6, False), L("Supply fan", AC3, 2.2, 0.8, 0.85, 0.5, 1, 6, False),
             L("Lighting", AC1, 0.5, 0.95, 1, 1, 0.8, 1, False), L("Controls", DC, 0.12, 1, 0.9, 1, 1, 1, True)]
    o = budget(mixed, t_auto=4)
    c = "ka-mixed-buses"
    for sym, kat, tol in [("P_dem", 9.12, 1e-9), ("S_dem", 10.73958, 1e-6), ("I_3ph", 14.72031, 1e-6),
                          ("I_1ph", 1.830664, 1e-6), ("I_DC", 5, 1e-9), ("Phi", 0.927, 1e-9),
                          ("P_PSU_req", 150, 1e-9), ("S_T_req", 13.42448, 1e-6), ("S_peak", 54.85723, 1e-6),
                          ("C_req", 32.89474, 1e-6)]:
        check(c, sym, kat, o[sym], tol)
    bat = [L("Controls", DC, 0.12, 1, 1, 1, 1, 1, True), L("Comms", DC, 0.048, 1, 1, 1, 1, 1, True)]
    o = budget(bat, t_auto=4, k_temp=0.9, C_batt=55)
    c = "ka-battery"
    for sym, kat, tol in [("I_b", 7, 1e-9), ("C_req", 51.16959, 1e-6), ("u_batt", 0.9303562, 1e-6)]:
        check(c, sym, kat, o[sym], tol)
    o = budget([L("Controls", DC, 0.12, 1, 1, 1, 1, 1, True)], t_auto=4, k_p=1.2, H=20)
    c = "ka-peukert"
    for sym, kat, tol in [("Ah", 26.15321, 1e-6), ("C_req", 43.01515, 1e-6)]:
        check(c, sym, kat, o[sym], tol)
    o = budget([L("PLC and I/O", DC, 0.2, 1, 1, 1, 1, 1, False)], f_derate=0.75, P_PSU=240)
    c = "ka-psu-derating"
    for sym, kat, tol in [("P_PSU_req", 333.3333, 1e-6), ("u_PSU", 1.388889, 1e-6)]:
        check(c, sym, kat, o[sym], tol)


def report():
    print(f"{'case':26s} {'symbol':10s} {'KAT':>12s} {'independent':>14s} {'rel diff':>10s} {'KAT tol':>9s}  result")
    bad = 0
    for case, sym, kat, ind, rel, tol, ok in ROWS:
        bad += 0 if ok else 1
        print(f"{case:26s} {sym:10s} {kat:12.7g} {ind:14.8g} {rel:10.2e} {tol:9.1e}  {'PASS' if ok else 'FAIL'}")
    print(f"\n{len(ROWS) - bad}/{len(ROWS)} pass")
    return bad


def x(case, sym, product, indep, tol=1e-6):
    """Extra check: product value hard-coded from the probe tool, compared with the independent value."""
    check(case, sym, product, indep, tol)


def s_peak_full(loads):
    """Physical starting peak: the starting load runs at its full rating (P/cos phi), not at P*d*k.
    S_peak = S_dem - S_i(demand) + m * S_i(full), largest over non-DC loads."""
    s_dem = sum(l["P"] * l["d"] * l["k"] / (1.0 if l["supply"] == DC else l["cosphi"]) for l in loads)
    best = s_dem
    for l in loads:
        if l["supply"] == DC:
            continue
        s_d = l["P"] * l["d"] * l["k"] / l["cosphi"]
        s_f = l["P"] / l["cosphi"]
        best = max(best, s_dem - s_d + l["inrush"] * s_f)
    return best


def extras():
    # Product values below were read from `Probe run/steps elec.load-budget` with the v0.26.0 sources.
    pump_half = [L("Pump", AC3, 11, 0.85, 0.9, 0.5, 1, 6, False)]
    o = budget(pump_half)
    for sym, v in [("P_conn", 11.0), ("P_dem", 5.5), ("S_dem", 6.470588235), ("I_3ph", 9.339489649),
                   ("Phi", 0.55), ("S_T_req", 8.088235294), ("S_peak", 38.82352941)]:
        x("x1-standby-pump-d0.5", sym, v, o[sym])
    pump_full = [L("Pump", AC3, 11, 0.85, 0.9, 1, 1, 6, False)]
    o = budget(pump_full)
    for sym, v in [("I_3ph", 18.6789793), ("S_peak", 77.64705882)]:
        x("x2-pump-d1", sym, v, o[sym])
    o = budget([L("DC", DC, 4, 1, 0.9, 1, 1, 1, True)], t_auto=2)
    for sym, v in [("I_DC", 166.6666667), ("P_PSU_req", 5000.0), ("I_b", 166.6666667), ("Ah", 1200000 / 3600.0),
                   ("C_req", 1973684.211 / 3600.0), ("I_3ph", 0.0), ("I_1ph", 0.0)]:
        x("x3-DC-only-4kW", sym, v, o[sym])
    dc = [L("c", DC, 0.24, 1, 1, 1, 1, 1, True)]
    o = budget(dc, t_auto=72, k_p=1.3, H=20)
    for sym, v in [("Ah", 1928668.099 / 3600.0), ("C_req", 3172151.478 / 3600.0)]:
        x("x4-peukert-t72-H20", sym, v, o[sym])
    o = budget(dc, t_auto=1, k_p=1.3, H=20)
    for sym, v in [("Ah", 71868.76667 / 3600.0), ("C_req", 118205.2083 / 3600.0)]:
        x("x5-peukert-t1-H20", sym, v, o[sym])
    o = budget([L("blank", AC3, 5, 1, 1, 1, 1, 1, False)])  # product treats blank cos phi, eta, d, k as 1
    for sym, v in [("S_dem", 5.0), ("I_3ph", 7.216878365), ("S_T_req", 6.25), ("Phi", 0.0)]:
        x("x6-blank-optional-cells", sym, v, o[sym])
    x("x7-psu-ambient-55C-f1", "P_PSU_req", 250.0, budget([L("x", DC, 0.2, 1, 1, 1, 1, 1, False)])["P_PSU_req"])
    two = [L("Pump", AC3, 7.5, 0.85, 0.9, 1, 1, 6, False), L("Fan", AC3, 2.2, 0.8, 0.85, 0.5, 1, 6, False)]
    o = budget(two, S_T=15, k_T=3, margin=1.5)
    for sym, v in [("S_dem", 10.19852941), ("I_3ph", 14.72030925), ("Phi", 0.915), ("S_T_req", 15.29779412),
                   ("S_peak", 54.31617647), ("u_T", 1.019852941), ("u_peak", 1.207026144)]:
        x("x8-transformer-check", sym, v, o[sym])
    o = budget([L("x", DC, 0.12, 1, 1, 1, 1, 1, True)], t_auto=10, eta_conv=0.5, Ub=2, DoD=0.05)
    for sym, v in [("I_b", 120.0), ("Ah", 1200.0), ("C_req", 113684210.5 / 3600.0)]:
        x("x9-battery-limits", sym, v, o[sym])
    o = budget([L("big", AC3, 200, 0.5, 0.5, 1, 1, 20, False)], U3=12)
    for sym, v in [("S_dem", 400.0), ("I_3ph", 19245.00897), ("Phi", 100.0), ("S_T_req", 500.0), ("S_peak", 8000.0)]:
        x("x10-200kW-12V-inrush20", sym, v, o[sym])
    o = budget([L("w", AC1, 0.5, 0.9, 1, 1, 1, 1, False)])
    for sym, v in [("S_dem", 0.5555555556), ("I_1ph", 2.415458937)]:
        x("x11-watt-input", sym, v, o[sym])
    o = budget([L("x", DC, 0.12, 1, 1, 1, 1, 1, True)], t_auto=4, k_p=1.2, H=None)  # exponent alone: not applied
    for sym, v in [("Ah", 20.0), ("C_req", 118421.0526 / 3600.0)]:
        x("x12-kp-without-H", sym, v, o[sym])
    o = budget([L("x", DC, 0.168, 1, 1, 1, 1, 1, True)], t_auto=4, P_PSU=240, C_batt=55)
    for sym, v in [("C_req", 165789.4737 / 3600.0), ("u_PSU", 0.875), ("u_batt", 0.8373205742)]:
        x("x13-psu-and-battery", sym, v, o[sym])


def demonstrations():
    """Differences between the product convention and the physical case. They do not change the exit code."""
    print("\nDemonstrations for the findings (product value | independent value):")
    pump_half = [L("Pump", AC3, 11, 0.85, 0.9, 0.5, 1, 6, False)]
    print(f"  Starting peak, 11 kW pump, d = 0.5, m = 6: product 38.824 kVA | full-rating start {s_peak_full(pump_half):.3f} kVA")
    print("  DC-only 4 kW budget: product I_3ph = I_1ph = 0 A | the AC input of the PSU is 4/(eta*pf) kW, not in either current")
    print("  Peukert credit at t = 72 h on a 20 h rating, k_p = 1.3: capacity is 0.744 of I*t")


if __name__ == "__main__":
    kats()
    extras()
    bad = report()
    demonstrations()
    sys.exit(1 if bad else 0)
