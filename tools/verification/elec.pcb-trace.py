#!/usr/bin/env python3
"""Independent re-derivation of elec.pcb-trace (Track P, P1).

Method (written before reading the C# Calculate method):
  IPC-2221 legacy curve-fit (section 6.2 chart fit): I = k dT^0.44 A^0.725,
    I in A, dT in K (degC), A in mil^2 (1 mil = 25.4 um), k = 0.048 external, 0.024 internal.
  => A = (I / (k dT^0.44))^(1/0.725)
  Copper thickness: 1 oz/ft^2 = 1.378 mil (35 um nominal; IPC-2221 uses 1.378 mil/oz in the
  width relation; 34.8 um = 1.37 mil is the other common rounding).
  w_min = A / t ; u_w = w_min / w_act
  Resistance: rho(T) = rho20 (1 + alpha (T - 20)), rho20 = 0.01724 ohm mm^2/m (annealed copper,
  IACS 100 %), alpha = 0.00393 /K; T = T_amb + dT (trace at design rise).
  R = rho(T) L / (w_act t); V = I R; P = I^2 R.
"""
import sys

MIL_MM = 0.0254
ROWS = []


def check(case, symbol, expected, value, tol, note=""):
    tol_eff = max(tol, 1e-6)
    rel = abs(value - expected) / abs(expected) if expected != 0 else abs(value - expected)
    ROWS.append((case, symbol, expected, value, rel, tol, rel <= tol_eff, note))


def trace(I, dT, layer="ext", oz=1.0, w_act=2.0, L=50.0, T_amb=25.0,
          k_ext=0.048, k_int=0.024, n_dT=0.44, n_A=0.725, t_oz=1.378,
          rho20=0.01724, alpha=0.00393):
    k = k_ext if layer == "ext" else k_int
    A = (I / (k * dT ** n_dT)) ** (1.0 / n_A)          # mil^2
    t_mil = oz * t_oz                                    # mil
    t_mm = t_mil * MIL_MM
    w_min_mm = A / t_mil * MIL_MM
    T = T_amb + dT
    rho = rho20 * (1 + alpha * (T - 20.0))               # ohm mm^2/m
    R = rho * (L / 1000.0) / (w_act * t_mm)              # ohm  (mm^2 * m / m... rho in ohm mm^2/m)
    return dict(A_req=A, w_min=w_min_mm, u_w=w_min_mm / w_act, trace_R=R * 1e3,   # mohm
                vdrop=I * R * 1e3, trace_Ploss=I * I * R * 1e3, t_mm=t_mm, rho=rho)


# ---------------- Known-answer cases ----------------
r = trace(3, 10, "ext", 1, 2)
check("ka-3a-10k-1oz-external", "A_req", 74.16164155, r["A_req"], 1e-9)
check("ka-3a-10k-1oz-external", "w_min", 1.366985265, r["w_min"], 1e-9)
check("ka-3a-10k-1oz-external", "u_w", 0.6834926326, r["u_w"], 1e-9)

r = trace(3, 10, "int", 1, 4)
check("ka-3a-10k-1oz-internal", "A_req", 192.927049, r["A_req"], 1e-9)
check("ka-3a-10k-1oz-internal", "w_min", 3.556129930, r["w_min"], 1e-9)
check("ka-3a-10k-1oz-internal", "u_w", 0.8890324826, r["u_w"], 1e-9)

r = trace(5, 20, "ext", 2, 2.5, 100, 25)
check("ka-5a-20k-2oz-resistance", "w_min", 0.9079000638, r["w_min"], 1e-9)
check("ka-5a-20k-2oz-resistance", "trace_R", 10.818960492, r["trace_R"], 1e-9)
check("ka-5a-20k-2oz-resistance", "vdrop", 54.09480246, r["vdrop"], 1e-9)
check("ka-5a-20k-2oz-resistance", "trace_Ploss", 270.4740123, r["trace_Ploss"], 1e-9)
check("ka-5a-20k-2oz-resistance", "u_w", 0.3631600255, r["u_w"], 1e-9)

r = trace(1, 30, "int", 0.5, 0.5, 200, 40)
check("ka-half-oz-internal-narrow", "w_min", 0.8023171756, r["w_min"], 1e-9)
check("ka-half-oz-internal-narrow", "u_w", 1.604634351, r["u_w"], 1e-9)
check("ka-half-oz-internal-narrow", "trace_R", 471.4732066, r["trace_R"], 1e-9)
check("ka-half-oz-internal-narrow", "trace_Ploss", 471.4732066, r["trace_Ploss"], 1e-9)

# Secondary (Unconfirmed) published widths, tolerance 0.5 %
check("ka-published-3a-10k-1oz", "w_min", 1.37, trace(3, 10, "ext", 1, 2)["w_min"], 0.005)
check("ka-published-3a-10k-1oz-internal", "w_min", 3.56, trace(3, 10, "int", 1, 4)["w_min"], 0.005)

# ---------------- Extra checks ----------------
# Product values read from the probe (`Probe run elec.pcb-trace`), converted to mil^2, mm, mohm, mV, mW.
EXTRA = [  # (name, kwargs, product values)
    ('P1 typical 10 A, 20 K, 1 oz external, 5 mm x 80 mm', {'I': 10, 'dT': 20, 'layer': 'ext', 'oz': 1, 'w_act': 5, 'L': 80, 'T_amb': 25}, {'A_req': 256.2697413, 'w_min': 4.723694795, 'u_w': 0.944738959, 'trace_R': 8.655168394, 'vdrop': 86.55168394, 'trace_Ploss': 865.5168394}),
    ('P2 upper edge 35 A, 100 K, 3 oz, 85 degC ambient', {'I': 35, 'dT': 100, 'layer': 'ext', 'oz': 3, 'w_act': 10, 'L': 300, 'T_amb': 85}, {'A_req': 543.167188, 'w_min': 3.337311701, 'u_w': 0.3337311701, 'trace_R': 8.119515331, 'vdrop': 284.1830366, 'trace_Ploss': 9946.40628}),
    ('P3 near zero 10 mA, 1 K, 0.05 mm internal', {'I': 0.01, 'dT': 1, 'layer': 'int', 'oz': 1, 'w_act': 0.05, 'L': 1, 'T_amb': 25}, {'A_req': 0.2989308252, 'w_min': 0.005510045691, 'u_w': 0.1102009138, 'trace_R': 10.08337954, 'vdrop': 0.1008337954, 'trace_Ploss': 0.001008337954}),
    ('P4 very large 100 A, 40 K, 2 oz, 30 mm wide', {'I': 100, 'dT': 40, 'layer': 'ext', 'oz': 2, 'w_act': 30, 'L': 500, 'T_amb': 25}, {'A_req': 4030.079458, 'w_min': 37.14224174, 'u_w': 1.238074725, 'trace_R': 4.83052343, 'vdrop': 483.052343, 'trace_Ploss': 48305.2343}),
    ('P5 cold -40 degC, 6 oz internal, 1 m long', {'I': 2, 'dT': 10, 'layer': 'int', 'oz': 6, 'w_act': 3, 'L': 1000, 'T_amb': -40}, {'A_req': 110.2830139, 'w_min': 0.3387988089, 'u_w': 0.1129329363, 'trace_R': 21.98708743, 'vdrop': 43.97417485, 'trace_Ploss': 87.94834971}),
    ('P6 0.5 oz external 8 A, 125 degC ambient', {'I': 8, 'dT': 15, 'layer': 'ext', 'oz': 0.5, 'w_act': 20, 'L': 20, 'T_amb': 125}, {'A_req': 224.3123758, 'w_min': 8.26928062, 'u_w': 0.413464031, 'trace_R': 1.449686525, 'vdrop': 11.5974922, 'trace_Ploss': 92.7799376}),
    ('P7 extreme 300 K rise at 500 degC ambient', {'I': 1, 'dT': 300, 'layer': 'int', 'oz': 1, 'w_act': 1, 'L': 100, 'T_amb': 500}, {'A_req': 5.380653717, 'w_min': 0.09917895821, 'u_w': 0.09917895821, 'trace_R': 200.2431231, 'vdrop': 200.2431231, 'trace_Ploss': 200.2431231}),
]

for name, kw, prod in EXTRA:
    r = trace(**kw)
    for sym, pv in prod.items():
        check("extra " + name, sym, pv, r[sym], 1e-6, "product value hard-coded")

# ---------------- Evidence for findings (informational) ----------------
# Operating state of a trace narrower than the minimum. The product evaluates R at T_amb + dT (the allowed rise).
# Inverting the curve-fit for the drawn cross-section gives the rise the drawn trace would really reach.
def actual_rise(I, layer, oz, w_act, k_ext=0.048, k_int=0.024, t_oz=1.378):
    k = k_ext if layer == "ext" else k_int
    A_act = w_act / MIL_MM * oz * t_oz
    return (I / (k * A_act ** 0.725)) ** (1 / 0.44)
FIND = []


def finding(tag, text):
    FIND.append((tag, text))


# Thickness convention: 34.8 um (1.37 mil) against 35 um (1.378 mil) per oz.
a = trace(3, 10, "ext", 1, 2, t_oz=1.378)
b = trace(3, 10, "ext", 1, 2, t_oz=1.37)
finding("thickness", f"3 A/10 K/1 oz external: w_min {a['w_min']:.4f} mm at 1.378 mil, {b['w_min']:.4f} mm at 1.37 mil "
        f"({100 * (b['w_min'] / a['w_min'] - 1):.2f} % wider); R differs by the same {100 * (b['trace_R'] / a['trace_R'] - 1):.2f} %.")
# Physical density-based thickness of 1 oz/ft^2 copper (8.96 g/cm^3).
t_phys = 28.3495e-3 / 0.09290304 / 8960.0 * 1e6
finding("thickness", f"1 oz/ft^2 at density 8960 kg/m^3 is {t_phys:.2f} um = {t_phys / 25.4:.3f} mil (nominal 34.8 to 35 um).")


narrow = trace(1, 30, "int", 0.5, 0.5, 200, 40)
dT_act = actual_rise(1, "int", 0.5, 0.5)
T_act = 40 + dT_act
R_act = narrow["trace_R"] * (1 + 0.00393 * (T_act - 20)) / (1 + 0.00393 * (70 - 20))
finding("narrow-trace", f"ka-half-oz-internal-narrow (u_w 1.605): the drawn trace would rise {dT_act:.1f} K, not 30 K. "
        f"R at that temperature is {R_act:.1f} mohm against {narrow['trace_R']:.1f} mohm reported "
        f"({100 * (R_act / narrow['trace_R'] - 1):.1f} % higher). The FAIL check is raised, the R, V and P are not flagged.")
# Operating-temperature note for the extreme probes.
finding("laminate-temp", "Probe P2 (85 degC + 100 K = 185 degC) and P7 (800 degC) give no notice about the trace temperature against laminate Tg or copper limits.")


def main():
    print(f"{'case':36s} {'symbol':12s} {'KAT expected':>14s} {'independent':>14s} {'rel diff':>10s} {'KAT tol':>9s}  result")
    bad = 0
    for case, sym, e, v, rel, tol, ok, note in ROWS:
        if not ok:
            bad += 1
        print(f"{case:36s} {sym:12s} {e:14.8g} {v:14.8g} {rel:10.2e} {tol:9.1e}  {'PASS' if ok else 'FAIL'}  {note}")
    print(f"{len(ROWS) - bad}/{len(ROWS)} rows within tolerance")
    print("\nFindings evidence (informational):")
    for tag, text in FIND:
        print(f"  {tag}: {text}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
