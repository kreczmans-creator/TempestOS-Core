#!/usr/bin/env python3
"""Independent re-derivation of elec.enclosure-heat (Track P, P1).

Method (written before reading the C# Calculate method):
  P = sum(P_i n_i d_i) + alpha I_sun A_sun
  A_eff = sum(f_face A_face); faces: top/bottom W*D, front/rear W*H, left/right D*H
  Natural rise: dT = P / (h A_eff);  T_int = T_amb + dT
  Natural capacity at the limit: P_nat = h A_eff (T_max - T_amb);  P_cool = max(0, P - P_nat)
  Air density: rho = p(z) / (287.055 T_amb[K]); p(z) = 101325 (1 - 2.25577e-5 z)^5.25588 (ISA, Pa)
  Airflow: V = P / (rho c_p dT_allow), c_p = 1006 J/(kg K); dT_allow = T_max - T_amb.
  Fan duty: p_fan(V) = p_sys (V/V_ref)^2 with linear interpolation of the fan curve.
Sources: IEC TR 60890 (verification of temperature rise by calculation; surface factors b are in its
surface-factor table, not used by the plain-area method here); IEC 61439-1 clause 10.10;
ISO 2533 standard atmosphere; manufacturer fan-sizing practice (V = 3.1 P/dT at sea level).
"""
import math
import sys

ROWS = []


def check(case, symbol, expected, value, tol, note=""):
    tol_eff = max(tol, 1e-6)
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    ROWS.append((case, symbol, expected, value, rel, tol, rel <= tol_eff, note))


def rho_air(T_amb_C, z):
    p = 101325.0 * (1 - 2.25577e-5 * z) ** 5.25588
    return p / (287.055 * (T_amb_C + 273.15))


def encl(loads, W=0.6, H=0.8, D=0.3, f=(1, 1, 0, 1, 1, 0), h=5.5, T_amb=35.0, T_max=50.0, z=0.0,
         I_sun=0.0, A_sun=0.0, alpha=0.5, Vp=None, pp=None, p_sys=20.0, V_ref=100.0):
    """f order: top, front, rear, left, right, bottom."""
    o = {}
    P_loads = sum(p * q * d for p, q, d in loads)
    P_solar = alpha * I_sun * A_sun
    P = P_loads + P_solar
    areas = (W * D, W * H, W * H, D * H, D * H, W * D)
    A_eff = sum(fi * a for fi, a in zip(f, areas))
    dT_nat = P / (h * A_eff)
    dT_allow = T_max - T_amb
    P_nat_max = h * A_eff * dT_allow
    P_cool = max(0.0, P - P_nat_max)
    rho = rho_air(T_amb, z)
    V_req = P / (rho * 1006.0 * dT_allow) * 3600.0  # m3/h
    V_req_net = max(0.0, P - P_nat_max) / (rho * 1006.0 * dT_allow) * 3600.0
    o.update(P_loads=P_loads, P_solar=P_solar, P_total=P, A_eff=A_eff, dT_nat=dT_nat,
             T_int=T_amb + dT_nat, P_nat_max=P_nat_max, P_cool=P_cool, rho_air=rho,
             V_req=V_req, V_req_net=V_req_net)
    if Vp is not None:
        # duty point: scan segments of the piecewise-linear curve for a root of g(V)=p_fan-p_sys(V/Vref)^2
        def pfan(V):
            for j in range(len(Vp) - 1):
                if Vp[j] <= V <= Vp[j + 1]:
                    t = (V - Vp[j]) / (Vp[j + 1] - Vp[j])
                    return pp[j] + t * (pp[j + 1] - pp[j])
            return None

        def g(V):
            return pfan(V) - p_sys * (V / V_ref) ** 2

        root = None
        N = 200000
        prev_V, prev_g = Vp[0], g(Vp[0])
        for n in range(1, N + 1):
            V = Vp[0] + (Vp[-1] - Vp[0]) * n / N
            gv = g(V)
            if prev_g > 0 >= gv:
                lo, hi = prev_V, V
                for _ in range(100):
                    mid = 0.5 * (lo + hi)
                    if g(mid) > 0:
                        lo = mid
                    else:
                        hi = mid
                root = 0.5 * (lo + hi)
                break
            prev_V, prev_g = V, gv
        if root is not None:
            o.update(V_duty=root, p_duty=pfan(root), u_fan=V_req / root)
    return o


# ---------------- Known-answer cases ----------------
r = encl([(80, 1, 1), (55, 4, 1)])
for sym, e, t in [("P_total", 300, 1e-9), ("A_eff", 1.14, 1e-9), ("dT_nat", 47.847, 1e-4),
                  ("V_req", 62.48, 1e-3), ("P_cool", 205.95, 1e-4)]:
    check("ka-wall-mounted", sym, e, r[sym], t)

r = encl([(300, 1, 1)], Vp=[0, 100, 200], pp=[60, 30, 0], p_sys=20, V_ref=100)
for sym, e, t in [("V_duty", 113.75, 1e-3), ("p_duty", 25.88, 2e-3), ("u_fan", 0.5493, 2e-3)]:
    check("ka-fan-duty", sym, e, r[sym], t)

# ---------------- Extra checks ----------------
# Product values below were read from `Probe run elec.enclosure-heat` (10 significant figures).
EXTRA_CASES = [
    ('P1 free-standing 600x1800x400, 800 W, 2000 m, T_max 55',
     {'loads': [(800, 1, 1)], 'W': 0.6, 'H': 1.8, 'D': 0.4, 'f': (1, 1, 0, 1, 1, 0), 'T_amb': 35, 'T_max': 55, 'z': 2000},
     {'P_total': 800.0, 'A_eff': 2.76, 'dT_nat': 52.70092227, 'P_nat_max': 303.6, 'P_cool': 496.4, 'rho_air': 0.8986968166, 'T_int_C': 87.7009223, 'V_req': 159.2763549, 'V_req_net': 98.8309782}),
    ('P2 sunlit, 1000 W/m2 on 1.5 m2, alpha 0.6, rear exposed',
     {'loads': [(200, 1, 1)], 'f': (1, 1, 1, 1, 1, 0), 'I_sun': 1000, 'A_sun': 1.5, 'alpha': 0.6},
     {'P_total': 1100.0, 'A_eff': 1.62, 'dT_nat': 123.4567901, 'P_nat_max': 133.65, 'P_cool': 966.35, 'rho_air': 1.145484783, 'T_int_C': 158.4567901, 'V_req': 229.0955333, 'V_req_net': 201.260426}),
    ('P3 zero heat load',
     {'loads': [(0, 1, 1)]},
     {'P_total': 0.0, 'A_eff': 1.14, 'dT_nat': 0.0, 'P_nat_max': 94.05, 'P_cool': 0.0, 'rho_air': 1.145484783, 'T_int_C': 35.0, 'V_req': 0.0, 'V_req_net': 0.0}),
    ('P4 large 5x5x5 m, 50 kW, -20 C, 4000 m',
     {'loads': [(50000, 1, 1)], 'W': 5, 'H': 5, 'D': 5, 'T_amb': -20, 'T_max': 40, 'z': 4000},
     {'P_total': 50000.0, 'A_eff': 100.0, 'dT_nat': 90.90909091, 'P_nat_max': 33000.0, 'P_cool': 17000.0, 'rho_air': 0.8482428656, 'T_int_C': 70.9090909, 'V_req': 3515.629163, 'V_req_net': 1195.313916}),
    ('P9 qty and duty columns',
     {'loads': [(100, 3, 0.5), (20, 2, 1)]},
     {'P_total': 190.0, 'A_eff': 1.14, 'dT_nat': 30.3030303, 'P_nat_max': 94.05, 'P_cool': 95.95, 'rho_air': 1.145484783, 'T_int_C': 65.3030303, 'V_req': 39.57104664, 'V_req_net': 19.98337856}),
    ('P11 6-point fan curve, 45 C, 1500 m',
     {'loads': [(450, 1, 1)], 'T_amb': 45, 'T_max': 60, 'z': 1500, 'Vp': [0, 50, 100, 150, 200, 250], 'pp': [80, 75, 60, 40, 18, 0], 'p_sys': 30, 'V_ref': 120},
     {'P_total': 450.0, 'A_eff': 1.14, 'dT_nat': 71.77033493, 'P_nat_max': 94.05, 'P_cool': 355.95, 'rho_air': 0.925863661, 'T_int_C': 116.7703349, 'V_req': 115.9521313, 'V_req_net': 91.71813589, 'V_duty': 143.1986622, 'p_duty': 42.72053512, 'u_fan': 0.8097291521}),
    ('P6 fan, zero system resistance',
     {'loads': [(300, 1, 1)], 'Vp': [0, 100, 200], 'pp': [60, 30, 0], 'p_sys': 0, 'V_ref': 100},
     {'P_total': 300.0, 'A_eff': 1.14, 'dT_nat': 47.84688995, 'P_nat_max': 94.05, 'P_cool': 205.95, 'rho_air': 1.145484783, 'T_int_C': 82.84689, 'V_req': 62.48059999, 'V_req_net': 42.89293188, 'V_duty': 200.0, 'p_duty': 0.0, 'u_fan': 0.3124029999}),
    ('P13 fan curve starting at 50 m3/h',
     {'loads': [(300, 1, 1)], 'Vp': [50, 100, 200], 'pp': [60, 30, 0], 'p_sys': 20, 'V_ref': 100},
     {'P_total': 300.0, 'A_eff': 1.14, 'dT_nat': 47.84688995, 'P_nat_max': 94.05, 'P_cool': 205.95, 'rho_air': 1.145484783, 'T_int_C': 82.84689, 'V_req': 62.48059999, 'V_req_net': 42.89293188, 'V_duty': 113.7458609, 'p_duty': 25.87624174, 'u_fan': 0.5492999877}),
    ('P14 P_add 300 W with an empty loads table (v1.1.0 linked heat load)',
     {'loads': [(300, 1, 1)]},
     {'P_total': 300.0, 'A_eff': 1.14, 'dT_nat': 47.84688995, 'P_cool': 205.95}),
    ('P12 altitude -500 m',
     {'loads': [(300, 1, 1)], 'z': -500},
     {'P_total': 300.0, 'A_eff': 1.14, 'dT_nat': 47.84688995, 'P_nat_max': 94.05, 'P_cool': 205.95, 'rho_air': 1.215039555, 'T_int_C': 82.84689, 'V_req': 58.90390668, 'V_req_net': 40.43753194}),
]
for name, kw, prod in EXTRA_CASES:
    r = encl(**kw)
    r["T_int_C"] = r["T_int"]
    for sym, pv in prod.items():
        # Tolerance 1e-5: the product uses the pressure exponent 5.2559, this script 5.25588 (ISA);
        # the density differs by 1.9e-6 (relative) at 4000 m.
        check("extra " + name.split()[0], sym, pv, r[sym], 1e-5, "product value hard-coded")

# ---------------- Findings evidence (informational, does not change the exit code) ----------------
FIND = []


def finding(tag, text):
    FIND.append((tag, text))


# F01: effective surface with the IEC TR 60890 surface factors b (top 1.4, vertical faces 0.9,
# bottom on the floor 0) against the plain-area sum used by the product. Factors quoted from memory:
# check them against the owner's copy of the surface-factor table.
W_, H_, D_ = 0.6, 1.8, 0.4
plain = W_ * D_ + 2 * W_ * H_ + 2 * D_ * H_
b_eff = 1.4 * W_ * D_ + 0.9 * (2 * W_ * H_ + 2 * D_ * H_)
finding("P1-elec.enclosure-heat-01", f"Free-standing 0.6 x 1.8 x 0.4 m, floor-mounted: plain A_eff {plain:.3f} m2; with b factors {b_eff:.3f} m2; "
        f"plain-area rise is {100 * (1 - b_eff / plain):.1f} % lower for the same h.")
finding("P1-elec.enclosure-heat-02", "All four exposure factors f_top, f_front, f_left, f_right set to 0 (rear and bottom default 0): "
        "product returns Block 'engine: Division by zero' (A_eff = 0).")
finding("P1-elec.enclosure-heat-03", f"P2 sunlit case: natural rise {EXTRA_CASES[1][2]['dT_nat']:.1f} K, T_int {EXTRA_CASES[1][2]['T_int_C']:.1f} degC "
        "printed from a constant h = 5.5 W/(m2 K) with no validity warning (the cooling-unit warning does fire).")


def main():
    print(f"{'case':28s} {'symbol':8s} {'KAT expected':>14s} {'independent':>14s} {'rel diff':>10s} {'KAT tol':>9s}  result")
    bad = 0
    for case, sym, e, v, rel, tol, ok, note in ROWS:
        if not ok:
            bad += 1
        print(f"{case:28s} {sym:8s} {e:14.6g} {v:14.6g} {rel:10.2e} {tol:9.1e}  {'PASS' if ok else 'FAIL'}  {note}")
    print(f"{len(ROWS) - bad}/{len(ROWS)} rows within tolerance")
    print("\nFindings evidence (informational):")
    for tag, text in FIND:
        print(f"  {tag}: {text}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
