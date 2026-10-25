#!/usr/bin/env python3
"""Independent re-derivation of fp.accumulator (hydro-pneumatic accumulator, gas law).

Written from first principles (gas law p V^n = const on a fixed mass of gas), not from the C# code.

Model (all pressures ABSOLUTE):
  gas charged to p0 at the maximum operating temperature Tmax, gas volume V0 (empty of oil).
  At temperature T the pre-charge pressure is p0(T) = p0 * T/Tmax (constant gas mass and volume).
  Gas volume at p1 and p2 from p V^n = const starting at the pre-charge state:
      V1 = V0 (p0T/p1)^(1/n),  V2 = V0 (p0T/p2)^(1/n),  dV = V1 - V2.
  The product (and the KAT set) scales V0 by Tmax/Tmin (first power) with p0 defined at Tmax.
  For n = 1 this is exact. For n > 1 the exact polytropic scale is (Tmax/Tmin)^(1/n);
  the section "model notes" prints the difference.
"""
import sys, math

K = 273.15
rows = []          # (case, symbol, expected, indep, tol)
fails = 0


def absolute(p, basis, patm):
    return p + patm if basis == "Gauge" else p


def accumulator(dV, p0, p1, p2, basis="Absolute", patm=1.01325, n=1.0, frg=1.0,
                Tmax=50.0, Tmin=20.0, Tc=20.0, r0=0.9, rp=4.0, sizes=None,
                PS=None, Vves=None):
    pa0, pa1, pa2 = (absolute(x, basis, patm) for x in (p0, p1, p2))
    bracket = (pa0 / pa1) ** (1.0 / n) - (pa0 / pa2) ** (1.0 / n)
    fT = (Tmax + K) / (Tmin + K)
    V0 = dV / bracket * frg * fT
    p0c = pa0 * (Tc + K) / (Tmax + K)
    p0set = p0c - patm if basis == "Gauge" else p0c
    p0min = pa0 * (Tmin + K) / (Tmax + K)
    u_r0 = pa0 / (r0 * pa1)
    u_rp = (pa2 / p0min) / rp
    out = dict(V0=V0, p0=p0c, p0_set=p0set, p0_min=p0min, f_T=fT, u_r0=u_r0, u_rp=u_rp)
    if sizes:
        ok = [s for s in sorted(sizes) if s >= V0]
        if ok:
            out["V_sel"] = ok[0]
            out["u_V"] = V0 / ok[0]
    vol = Vves if Vves is not None else out.get("V_sel")
    if PS is not None and vol is not None:
        out["bar_litres"] = PS * vol
    return out


def check(case, sym, expected, indep, tol):
    global fails
    t = max(tol, 1e-6)
    rel = abs(indep - expected) / abs(expected) if expected != 0 else abs(indep - expected)
    ok = rel <= t
    if not ok:
        fails += 1
    print(f"{case:34s} {sym:11s} {expected:16.8g} {indep:16.8g} {rel:10.2e} {tol:8.1e} {'PASS' if ok else 'FAIL'}")


print(f"{'case':34s} {'symbol':11s} {'KAT expected':>16s} {'independent':>16s} {'rel diff':>10s} {'tol':>8s} result")

# ---- KATs: inputs copied from the template, expected values from the KAT list ----
# defaults when not given: n=1, f_rg=1, Tmax=50, Tmin default 20 (but KATs set Tmin explicitly), Tc=20, patm=1.01325
def kat(case, inp, exp, tol=1e-9):
    r = accumulator(**inp)
    for sym, val in exp.items():
        check(case, sym, val, r[sym], tol)

kat("ka-parker-numbers-absolute",
    dict(dV=14, p0=90, p1=100, p2=210, basis="Absolute", n=1, Tmin=50),
    dict(V0=29.69696970, p0=81.64474702, u_r0=1.0, u_rp=0.5833333333))
kat("ka-parker-numbers-gauge",
    dict(dV=14, p0=90, p1=100, p2=210, basis="Gauge", patm=1.01325, n=1, Tmin=50),
    dict(V0=29.80703643, u_r0=1.001114540, u_rp=0.5796223352))
kat("ka-adiabatic",
    dict(dV=14, p0=90, p1=100, p2=210, basis="Absolute", n=1.4, Tmin=50),
    dict(V0=36.69278925))
kat("ka-charge-temperature",
    dict(dV=14, p0=90, p1=100, p2=210, basis="Absolute", Tmax=80, Tmin=20, Tc=20),
    dict(p0=74.70904715, p0_set=74.70904715, p0_min=74.70904715))
kat("ka-size-and-real-gas",
    dict(dV=14, p0=90, p1=100, p2=210, basis="Absolute", frg=1.05, Tmin=50, PS=230, sizes=[10, 20, 32, 50]),
    dict(V0=31.18181818, V_sel=32, u_V=0.9744318182, bar_litres=7360))
kat("ka-below-pssr",
    dict(dV=0.5, p0=40, p1=50, p2=100, basis="Absolute", Tmin=50, PS=110, Vves=2),
    dict(V0=1.25, bar_litres=220, u_r0=0.8888888889, u_rp=0.625))
kat("ka-ps-above-working-pressure",
    dict(dV=0.5, p0=40, p1=50, p2=100, basis="Gauge", patm=1.01325, Tmin=50, PS=110, Vves=2.4),
    dict(V0=1.256426685, bar_litres=264, u_rp=0.6157354635))
kat("ka-temperature-range",
    dict(dV=14, p0=90, p1=100, p2=210, basis="Absolute", Tmax=50, Tmin=20, sizes=[10, 20, 32, 50]),
    dict(f_T=1.102336688, V0=32.73605921, V_sel=50, u_V=0.6547211842, p0_min=81.64474702, u_rp=0.6430297345))
# Secondary, Unconfirmed: Parker HY10-4004/UK. KAT tolerance 0.005 on V0 = 29.7 (about 29.7-29.8 L).
kat("ka-parker-hy10-4004 (Secondary)",
    dict(dV=14, p0=90, p1=100, p2=210, basis="Absolute", n=1, Tmin=50, sizes=[10, 20, 32, 50]),
    dict(V0=29.7, V_sel=32), tol=0.005)

# ---------------------------------------------------------------------------------------------
# Extra checks: product values observed with the probe (C# calculator, 2026-10-25, release/v0.26.0).
# Product values are hard-coded; the independent value is recomputed here.
# Product pressures in bar, volumes in L, bar_litres in bar*L.
# ---------------------------------------------------------------------------------------------
print("\n=== Extra probes: product (hard-coded from probe run) vs independent ===")
psi, gal, F2C = 0.06894757293168361, 3.785411784, lambda f: (f - 32) / 1.8
EXTRA = [
 ("X01 gauge low-p 20/30/100, dV 5", dict(dV=5, p0=20, p1=30, p2=100, basis="Gauge", Tmax=20, Tmin=20),
  dict(V0=10.6488905, p0=21.01325, p0_set=20.0, p0_min=21.01325, f_T=1.0, u_r0=0.752841304, u_rp=1.201780424)),
 ("X02 same numbers as absolute", dict(dV=5, p0=20, p1=30, p2=100, basis="Absolute", Tmax=20, Tmin=20),
  dict(V0=10.71428571, p0=20.0, p0_set=20.0, u_r0=0.740740741, u_rp=1.25)),
 ("X03 p0 = p1 (abs 100/100/200)", dict(dV=10, p0=100, p1=100, p2=200, Tmax=20, Tmin=20),
  dict(V0=20.0, u_r0=1.111111111, u_rp=0.5)),
 ("X04 n 1.4, T 150 to -40 C", dict(dV=10, p0=90, p1=100, p2=200, n=1.4, Tmax=150, Tmin=-40, Tc=-40),
  dict(V0=50.11057681, p0=49.5887983, p0_min=49.5887983, f_T=1.814926013, u_r0=1.0, u_rp=1.00829223)),
 ("X05 T_max -40, T_min -40, T_c 100", dict(dV=10, p0=90, p1=100, p2=200, Tmax=-40, Tmin=-40, Tc=100),
  dict(V0=22.22222222, p0=144.0424619, p0_min=90.0)),
 ("X06 700 bar gauge, PS 700, V 5000", dict(dV=100, p0=600, p1=650, p2=700, basis="Gauge", Tmax=40, Tmin=40, PS=700, Vves=5000),
  dict(V0=1518.665068, p0=562.6282428, p0_set=561.6149928, p0_min=601.01325, bar_litres=3500000.0, u_r0=1.025774053, u_rp=0.291596421)),
 ("X07 n 1.2, 20-50 C, gauge", dict(dV=10, p0=45, p1=50, p2=200, basis="Gauge", n=1.2, Tmax=50, Tmin=20),
  dict(V0=17.63864523, p0=41.74155729, p0_set=40.72830729, f_T=1.102336688, u_r0=1.002206943, u_rp=1.203915612)),
 ("X08 p0 gauge 0, dV 0.001", dict(dV=0.001, p0=0, p1=5, p2=10, basis="Gauge", Tmax=20, Tmin=20),
  dict(V0=0.013071883, p0=1.01325, p0_set=0.0, u_r0=0.187225433, u_rp=2.717308167)),
 ("X09 sizes 20/25/35/50, PS 160 g", dict(dV=20, p0=63, p1=70, p2=160, basis="Gauge", Tmax=20, Tmin=20, PS=160, sizes=[20, 25, 35, 50]),
  dict(V0=39.69342866, V_sel=50.0, bar_litres=8000.0, u_V=0.793868573, u_r0=1.001585385, u_rp=0.628827821)),
 ("X10 shock, n 1.4, 0-40 C, gauge", dict(dV=3, p0=18, p1=20, p2=30, basis="Gauge", n=1.4, Tmax=40, Tmin=0),
  dict(V0=15.21822384, p0=17.79892779, p0_set=16.78567779, p0_min=16.58460558, f_T=1.146439685, u_r0=1.005357731, u_rp=0.467500566)),
 ("X11 f_rg 1.3, 300/330/350 gauge", dict(dV=10, p0=300, p1=330, p2=350, basis="Gauge", frg=1.3, Tmax=20, Tmin=20),
  dict(V0=250.8976726, p0=301.01325, p0_set=300.0, u_r0=1.010410208, u_rp=0.291526411)),
 ("X12 p_atm 0.5, gauge entry", dict(dV=10, p0=90, p1=100, p2=200, basis="Gauge", patm=0.5, Tmax=20, Tmin=20),
  dict(V0=22.26546961, p0=90.5, u_r0=1.000552792, u_rp=0.553867403)),
 ("X13 p_atm 1.1, gauge entry", dict(dV=10, p0=90, p1=100, p2=200, basis="Gauge", patm=1.1, Tmax=20, Tmin=20),
  dict(V0=22.31746432, p0=91.1, u_r0=1.001208924, u_rp=0.551866081)),
 ("X14 gauge 0/0/10 (p0 = p1 = atm)", dict(dV=10, p0=0, p1=0, p2=10, basis="Gauge", Tmax=20, Tmin=20),
  dict(V0=11.01325, p0=1.01325, p0_set=0.0, u_r0=1.111111111, u_rp=2.717308167)),
 # psi, degF and US gallon inputs, converted here with exact factors (rounded inputs 1305.34, 1450.377, 2900.755 psi; 2.64172052 gal; 68 F)
 ("X15 psi / degF / US gal inputs", dict(dV=2.64172052 * gal, p0=1305.34 * psi, p1=1450.377 * psi, p2=2900.755 * psi, Tmax=F2C(68), Tmin=F2C(68)),
  dict(V0=22.22220261, p0=90.00002485, u_r0=1.000000536, u_rp=0.555555449)),
 # Addendum B-2 re-check at 2f53791c (fp.accumulator 1.2.0): the F2 step of pack.hydraulic-power-unit (Emergency stroke, dV and p_2 linked from F1)
 ("X16 HPU pack F2, 105/120/160 bar g, n 1.4, 50 to 10 C", dict(dV=3.926990816987242, p0=105, p1=120, p2=160, basis="Gauge", n=1.4, Tmax=50, Tmin=10),
  dict(V0=26.69518128, p0=96.17138864, p0_min=92.89076818, f_T=1.141267879, u_r0=0.9733851458, u_rp=0.4333402908)),
]
for name, inp, prod in EXTRA:
    r = accumulator(**inp)
    for sym, pv in prod.items():
        iv = r[sym]
        rel = abs(pv - iv) / abs(iv) if iv != 0 else abs(pv - iv)
        ok = rel <= 1e-6
        if not ok:
            fails += 1
        print(f"{name:38s} {sym:11s} product {pv:16.9g} indep {iv:16.9g} rel {rel:9.2e} {'PASS' if ok else 'FAIL'}")

# Guard conditions the template documents (independent re-statement). Product result observed: all BLOCKED (probe exit code 3).
print("\n=== Block conditions (product blocked each; independent test of the condition) ===")
def would_block(dV, p0, p1, p2, basis="Absolute", patm=1.01325, Tmax=20, Tmin=20, PS=None, V0=None, Vves=None):
    pa0, pa1, pa2 = (absolute(x, basis, patm) for x in (p0, p1, p2))
    if pa0 <= 0: return "p0 absolute not above zero"
    if pa0 > pa1: return "p0 above p1"
    if not pa1 < pa2: return "p2 not above p1"
    if Tmin > Tmax: return "T_min above T_max"
    if PS is not None and PS < pa2 - patm: return "PS below p2 gauge"
    if Vves is not None and V0 is not None and Vves < V0: return "V_vessel below V0"
    return None
BLOCKS = [
 ("p0 101 > p1 100 (abs)", dict(dV=10, p0=101, p1=100, p2=200)),
 ("p0 = 0 absolute", dict(dV=10, p0=0, p1=100, p2=200)),
 ("p2 = p1", dict(dV=10, p0=90, p1=100, p2=100)),
 ("p2 < p1", dict(dV=10, p0=90, p1=100, p2=80)),
 ("T_min 30 > T_max 20", dict(dV=10, p0=90, p1=100, p2=200, Tmin=30)),
 ("PS 699 < p2 700 gauge", dict(dV=100, p0=600, p1=650, p2=700, basis="Gauge", PS=699)),
 ("V_vessel 1500 < V0 1622.3", dict(dV=100, p0=600, p1=650, p2=700, basis="Gauge", Tmax=40, Tmin=20, Vves=1500,
                                     V0=accumulator(100, 600, 650, 700, basis="Gauge", Tmax=40, Tmin=20)["V0"])),
]
for name, kw in BLOCKS:
    reason = would_block(**kw)
    ok = reason is not None
    if not ok: fails += 1
    print(f"{name:34s} independent: {reason}  {'PASS' if ok else 'FAIL'}")
# Out-of-range inputs also blocked by the product (hard limits): f_rg 0.9, n 1.5, dV 0.0009 and 1001 L, p2 701 bar, p_atm 1.2 bar.

# ---------------------------------------------------------------------------------------------
# Model notes (informational, not pass/fail). Findings P1-fp.accumulator-01 .. 03 are quantified here.
# ---------------------------------------------------------------------------------------------
print("\n=== Model note 1: temperature scale for n > 1 ===")
print("Exact polytropic scale from the cold pre-charge state is (Tmax/Tmin)^(1/n); the product uses the first power for every n.")
for n_, tmax, tmin in [(1.4, 50, 20), (1.4, 60, -20), (1.4, 150, -40), (1.2, 50, 20), (1.0, 150, -40)]:
    fT = (tmax + K) / (tmin + K)
    exact = fT ** (1 / n_)
    print(f"n {n_}, {tmin} to {tmax} C: product f_T {fT:.4f}, exact {exact:.4f}, product over exact {100 * (fT / exact - 1):.1f} %")

print("\n=== Model note 2: real-gas factor for nitrogen, 20 C (reference equation of state) ===")
# Reviewer 2026-10-24: the first pass used Peng-Robinson, which under-predicts Z for N2 by about 3 % at 200 bar
# and 9 % at 700 bar. Z below is from the Span et al. (2000) reference equation for nitrogen, evaluated with
# CoolProp 8.0.0 at 293.15 K (hard-coded so the script runs without CoolProp). Spot values: Z(200) 1.052,
# Z(300) 1.140, Z(500) 1.368, Z(700) 1.615.
Z_N2_293 = {90: 0.9984, 100: 1.0008, 135: 1.0129, 150: 1.0205, 170: 1.0318, 180: 1.0380, 190: 1.0447,
            200: 1.0518, 250: 1.0924, 270: 1.1107, 300: 1.1401, 350: 1.1927, 450: 1.3074, 500: 1.3676,
            600: 1.4905, 650: 1.5527, 700: 1.6150}
def real_factor(p0, p1, p2):
    z0, z1, z2 = (Z_N2_293[p] for p in (p0, p1, p2))
    ideal = p0 / p1 - p0 / p2
    real = (z1 / z0) * (p0 / p1) - (z2 / z0) * (p0 / p2)
    return z0, z1, z2, ideal / real
print("p0/p1/p2 bar abs     Z0    Z1    Z2   isothermal V0_real/V0_ideal   (f_rg = Z2 would give)")
for c in [(90, 100, 200), (135, 150, 200), (170, 190, 200), (180, 200, 250), (270, 300, 350), (450, 500, 700), (600, 650, 700)]:
    z0, z1, z2, f = real_factor(*c)
    warn = "warns" if c[2] > 200 else "NO warning"
    print(f"{str(c):18s} {z0:5.3f} {z1:5.3f} {z2:5.3f}   {f:6.3f}   {z2:5.3f}   {warn:10s} cap 1.5 {'< needed' if f > 1.5 else 'ok'}")

print("\n=== Model note 3: adiabatic real gas (isentropic, reference EOS via CoolProp 8.0.0, hard-coded) ===")
# Effective exponent n_eff = ln(p2/p1)/ln(V1/V2) along an isentrope from 20 C. The ideal-gas 1.4 holds only near 1 bar.
for (pp, neff) in [((1, 2, 4), 1.401), ((50, 60, 100), 1.534), ((90, 100, 200), 1.679), ((200, 250, 350), 2.132)]:
    print(f"isentrope from {pp[0]} bar abs, {pp[1]} -> {pp[2]} bar: n_eff {neff}  (product caps n at 1.4)")
# V0_real/V0_ideal(n = 1.4), both processes adiabatic from the pre-charge state at 20 C:
for (pp, f) in [((90, 100, 200), 1.144), ((135, 150, 200), 1.238), ((170, 190, 200), 1.320), ((270, 300, 350), 1.580)]:
    print(f"{str(pp):18s} adiabatic factor needed {f:5.3f}   {'NO warning' if pp[2] <= 200 else 'warns'}")

print("\n=== Model note 4: emergency stroke after a long hold at p2 (finding R1) ===")
# Gas held at p2 reaches ambient temperature, so the charge from p0 to p2 is isothermal: V2 = V0 p0/p2.
# The stroke is then a fast (adiabatic) expansion from p2 to p1: V1 = V2 (p2/p1)^(1/n).
# dV = V0 (p0/p2) [(p2/p1)^(1/n) - 1].  The product uses V0 = dV/[(p0/p1)^(1/n) - (p0/p2)^(1/n)] for every mode,
# which assumes the gas is still hot from an adiabatic charge.
def v0_product(dV, p0, p1, p2, n):
    return dV / ((p0 / p1) ** (1 / n) - (p0 / p2) ** (1 / n))
def v0_hold(dV, p0, p1, p2, n):
    return dV / ((p0 / p2) * ((p2 / p1) ** (1 / n) - 1))
for c in [(45, 50, 100), (90, 100, 150), (90, 100, 200), (90, 100, 300), (135, 150, 330)]:
    a, b = v0_product(10, *c, 1.4), v0_hold(10, *c, 1.4)
    print(f"{str(c):16s} dV 10 L, n 1.4: product {a:7.3f} L, hold-then-stroke {b:7.3f} L, product/true {a / b:5.3f}")
# Probe 2026-10-25, Emergency stroke, 14 L, 90/100/200 bar abs, n 1.4, T 20/20 C: product V0 = 38.654 L.
pv, iv = 38.65436223, v0_product(14, 90, 100, 200, 1.4)
if abs(pv - iv) / iv > 1e-6:
    fails += 1
print(f"probe check: product {pv} L, independent product-form {iv:.8f} L, hold-then-stroke {v0_hold(14, 90, 100, 200, 1.4):.3f} L")


print("\n=== Model note 5: the HPU pack accumulator (Addendum B-2 re-check at 2f53791c; finding R1 and R2 in the pack) ===")
# pack.hydraulic-power-unit F2: Emergency stroke, dV 3.92699 L (F1 swept volume), p0 105 / p1 120 / p2 160 bar gauge, n 1.4,
# T_max 50 C, T_min 10 C. Product V0 = 26.695 L. Gas held at p2 reaches ambient, then a fast stroke (hold-then-stroke).
def hold_ideal_hpu(dV, p0max, p1, p2, n, T_amb, T_max=50.0):
    p0 = p0max * (T_amb + K) / (T_max + K)           # pre-charge at ambient, constant gas mass and volume
    return dV / ((p0 / p2) * ((p2 / p1) ** (1 / n) - 1))
def end_pressure_ideal(V0, p0max, p2, dV, n, T_amb, T_max=50.0):
    p0 = p0max * (T_amb + K) / (T_max + K)
    V2 = V0 * p0 / p2
    return p2 * (V2 / (V2 + dV)) ** n
pa0, pa1, pa2, atm = 105 + 1.01325, 120 + 1.01325, 160 + 1.01325, 1.01325
dVh, Vprod = 3.926990816987242, 26.69518128
# Real-nitrogen values: Span et al. (2000) reference equation via CoolProp 8.0.0 (hard-coded; isothermal hold at ambient, isentropic stroke).
REAL_V0 = {10: 39.432, 20: 37.190, 50: 31.953}
REAL_PEND = {10: 106.6, 20: 108.8, 50: 114.1}       # bar gauge at the end of the stroke in the product's 26.695 L vessel
print("ambient   hold-then-stroke V0 ideal   real N2   product/ideal  product/real   end pressure in 26.695 L (bar g): ideal / real   (80 kN needs 107.2)")
for Ta in (10, 20, 50):
    vi = hold_ideal_hpu(dVh, pa0, pa1, pa2, 1.4, Ta)
    pe = end_pressure_ideal(Vprod, pa0, pa2, dVh, 1.4, Ta) - atm
    print(f"{Ta:4d} C   {vi:8.3f} L   {REAL_V0[Ta]:8.3f} L   {Vprod / vi:8.3f}   {Vprod / REAL_V0[Ta]:8.3f}   {pe:8.1f} / {REAL_PEND[Ta]:6.1f}")

print(f"\nRESULT: {'ALL PASS' if fails == 0 else str(fails) + ' FAIL'}")
sys.exit(0 if fails == 0 else 1)
