#!/usr/bin/env python3
"""Independent re-derivation of bs.pipe-expansion (Track P, P1).

Method sources (cited by title and number only):
  Thermal growth: dL = alpha * L * dT (linear expansion).
  Guided cantilever (beam fixed against rotation at both ends, end deflection d):
    F = 12 E I d / H^3, end moment M = F H / 2, root stress sigma = M / Z = 3 E OD d / H^2
    (Roark's Formulas for Stress and Strain, case of a beam with one end guided; Timoshenko, Strength of Materials).
  Required leg L_req = sqrt(3 E OD d / Sa) from the same relation with sigma = Sa.
  Screening ratio OD*Y/(L_dev - U)^2 compared with K1: ASME B31.3 flexibility exemption, para. 319.4.1 (form only).
  Bellows anchor force = P * A_eff + k * dL (pressure thrust plus spring).
Sa and K1 are inputs; the script ships no code value.
Exit 0 when every KAT value matches within max(KAT tolerance, 1e-6).
"""
import math
import sys


def pe(inp):
    """inp: dict with E GPa, alpha um/mK, OD mm, t mm, L m, L2 m|None, mode, T_install, T_operate, dT_in K,
    layout in {'offset','uloop','bellows'}, H m, Sa MPa, K1 mm2/m2, E_hot GPa, P_b bar, A_eff cm2, k_b N/mm, dL_rated mm."""
    o = {}
    E = inp["E"] * 1e9
    Er = (inp.get("E_hot") or inp["E"]) * 1e9
    al = inp["alpha"] * 1e-6
    OD = inp["OD"] * 1e-3
    t = inp["t"] * 1e-3
    di = OD - 2 * t
    Am = math.pi / 4 * (OD ** 2 - di ** 2)
    I = math.pi / 64 * (OD ** 4 - di ** 4)
    o["A_m"] = Am * 1e6
    L1 = inp["L"]
    L2 = inp.get("L2")
    layout = inp["layout"]
    Ltot = L1 + (L2 or 0.0) if layout == "offset" else L1
    if inp.get("mode") == "change":
        dT = inp["dT_in"]
    else:
        dT = inp["T_operate"] - inp["T_install"]
    o["dT"] = dT
    adT = abs(dT)
    dL = al * Ltot * adT  # m
    o["dL"] = dL * 1e3
    o["dL_signed"] = al * Ltot * dT * 1e3
    o["L_tot"] = Ltot
    o["sigma_res"] = E * al * adT / 1e6
    o["F_res"] = Er * al * adT * Am / 1e3  # kN
    if layout == "bellows":
        thrust = inp["P_b"] * 1e5 * inp["A_eff"] * 1e-4
        spring = (inp.get("k_b") or 0.0) * 1e3 * dL
        o["F_thrust"] = thrust / 1e3
        o["F_spring"] = spring
        o["anchor_force"] = thrust + spring
        o["anchor_moment"] = 0.0
        if inp.get("dL_rated"):
            o["u_bellows"] = dL * 1e3 / inp["dL_rated"]
            o["pass"] = 1.0 if o["u_bellows"] <= 1 else 0.0
        else:
            o["pass"] = 0.0
        return o
    H = inp["H"]
    Sa = inp["Sa"] * 1e6
    Delta = dL if layout == "offset" else dL / 2
    o["Delta"] = Delta * 1e3
    Lreq = math.sqrt(3 * E * OD * Delta / Sa)
    o["L_req"] = Lreq
    o["u_leg"] = Lreq / H
    Fleg = 12 * Er * I * Delta / H ** 3
    Mleg = Fleg * H / 2
    o["F_leg"], o["M_leg"] = Fleg, Mleg
    o["sigma_leg"] = 3 * E * OD * Delta / H ** 2 / 1e6
    o["F_along_run"] = Fleg
    Fan = Fleg
    Mmax = Mleg
    ok = o["u_leg"] <= 1
    if layout == "offset":
        Dleg = al * adT * H
        o["Delta_run"] = Dleg * 1e3
        S = L1 ** 3 + (L2 or 0.0) ** 3
        Frun = 12 * Er * I * Dleg / S
        o["F_run"] = Frun
        o["F_along_leg"] = Frun
        runs = [L1] + ([L2] if L2 else [])
        for i, Li in enumerate(runs):
            di_ = Dleg * Li ** 3 / S
            sig = 3 * E * OD * di_ / Li ** 2
            u = math.sqrt(sig / Sa)
            sfx = "" if i == 0 else "2"
            o["sigma_run" + sfx] = sig / 1e6
            o["u_run" + sfx] = u
            o["L_req_run" + sfx] = Li * u
            o["M_run" + sfx] = Frun * Li / 2
            Mmax = max(Mmax, Frun * Li / 2)
            ok = ok and u <= 1
        Fan = math.hypot(Fleg, Frun)
        Ldev = Ltot + H
        U = math.hypot(Ltot, H)
    else:
        Ldev = L1 + 2 * H
        U = L1
    o["anchor_force"] = Fan
    o["anchor_moment"] = Mmax
    o["L_dev"], o["U_a"] = Ldev, U
    Y = al * adT * U
    o["Y_scr"] = Y * 1e3
    ratio = (OD * 1e3) * (Y * 1e3) / (Ldev - U) ** 2
    o["scr"] = ratio
    if inp.get("K1"):
        o["u_scr"] = ratio / inp["K1"]
        ok = ok and o["u_scr"] <= 1
        o["pass"] = 1.0 if ok else 0.0
    else:
        o["pass"] = 0.0
    return o


def conv(inputs):
    """Convert a KAT input list (kind, symbol, 'value, "unit"') to the model dict."""
    d = {"mode": "temps", "layout": "offset"}
    m = {"E": "E", "alpha": "alpha", "OD": "OD", "t_wall": "t", "L": "L", "L2": "L2", "T_install": "T_install",
         "T_operate": "T_operate", "dT_in": "dT_in", "H": "H", "Sa": "Sa", "K_scr": "K1", "E_hot": "E_hot",
         "P_b": "P_b", "A_eff": "A_eff", "k_b": "k_b", "dL_rated": "dL_rated"}
    for kind, sym, val in inputs:
        if kind == "Q":
            d[m[sym]] = float(val.split(",")[0])
        elif sym == "temp_mode":
            d["mode"] = "change" if val == "ChangeGiven" else "temps"
        elif sym == "layout":
            d["layout"] = {"OffsetLeg": "offset", "UpLoop": "uloop", "AxialBellows": "bellows"}[val]
    return d

KATS = {
 "ka-offset-leg-steel-50m": dict(secondary=False, inputs=[
   ('Q', 'E', '210, "GPa"'),
   ('Q', 'alpha', '12, "um/(m*K)"'),
   ('Q', 'OD', '88.9, "mm"'),
   ('Q', 't_wall', '3.2, "mm"'),
   ('Q', 'L', '50, "m"'),
   ('Choice', 'temp_mode', 'TwoTemperatures'),
   ('Q', 'T_install', '10, "degC"'),
   ('Q', 'T_operate', '80, "degC"'),
   ('Choice', 'layout', 'OffsetLeg'),
   ('Q', 'H', '6, "m"'),
   ('Q', 'Sa', '100, "MPa"'),
   ('Q', 'K_scr', '100, "mm^2/m^2"'),
 ], expected=[
   ('dL', 42, "mm", 1e-9),
   ('dL_signed', 42, "mm", 1e-9),
   ('dT', 70, "K", 1e-9),
   ('sigma_res', 176.4, "MPa", 1e-9),
   ('A_m', 861.5503693204649, "mm^2", 1e-9),
   ('F_res', 151.97748514813, "kN", 1e-9),
   ('L_req', 4.85004536061262, "m", 1e-9),
   ('u_leg', 0.8083408934354367, "", 1e-9),
   ('F_leg', 388.1088087998288, "N", 1e-9),
   ('M_leg', 1164.3264263994865, "N*m", 1e-9),
   ('L_req_run', 1.6801049967189552, "m", 1e-9),
   ('u_run', 0.033602099934379105, "", 1e-9),
   ('F_run', 0.0804782425927325, "N", 1e-9),
   ('F_along_run', 388.1088087998288, "N", 1e-9),
   ('F_along_leg', 0.0804782425927325, "N", 1e-9),
   ('anchor_force', 388.1088171438129, "N", 1e-9),
   ('anchor_moment', 1164.3264263994865, "N*m", 1e-9),
   ('sigma_leg', 65.3415, "MPa", 1e-9),
   ('L_dev', 56, "m", 1e-9),
   ('U_a', 50.35871324805669, "m", 1e-9),
   ('Y_scr', 42.30131912836762, "mm", 1e-9),
   ('scr', 118.16784619539838, "mm^2/m^2", 1e-9),
   ('u_scr', 1.1816784619539837, "", 1e-9),
   ('pass', 0, "", 1e-9),
 ]),
 "ka-u-loop-too-short": dict(secondary=False, inputs=[
   ('Q', 'E', '210, "GPa"'),
   ('Q', 'alpha', '12, "um/(m*K)"'),
   ('Q', 'OD', '88.9, "mm"'),
   ('Q', 't_wall', '3.2, "mm"'),
   ('Q', 'L', '50, "m"'),
   ('Choice', 'temp_mode', 'TwoTemperatures'),
   ('Q', 'T_install', '10, "degC"'),
   ('Q', 'T_operate', '80, "degC"'),
   ('Choice', 'layout', 'UpLoop'),
   ('Q', 'H', '3, "m"'),
   ('Q', 'Sa', '100, "MPa"'),
   ('Q', 'K_scr', '120, "mm^2/m^2"'),
 ], expected=[
   ('dL', 42, "mm", 1e-9),
   ('Delta', 21, "mm", 1e-9),
   ('L_req', 3.4294999635515384, "m", 1e-9),
   ('u_leg', 1.1431666545171795, "", 1e-9),
   ('anchor_force', 1552.4352351993152, "N", 1e-9),
   ('anchor_moment', 2328.652852798973, "N*m", 1e-9),
   ('sigma_leg', 130.683, "MPa", 1e-9),
   ('L_dev', 56, "m", 1e-9),
   ('U_a', 50, "m", 1e-9),
   ('Y_scr', 42, "mm", 1e-9),
   ('scr', 103.71666666666668, "mm^2/m^2", 1e-9),
   ('u_scr', 0.8643055555555558, "", 1e-9),
   ('pass', 0, "", 1e-9),
 ]),
 "ka-cold-copper-loop": dict(secondary=False, inputs=[
   ('Q', 'E', '120, "GPa"'),
   ('Q', 'alpha', '16.6, "um/(m*K)"'),
   ('Q', 'OD', '28, "mm"'),
   ('Q', 't_wall', '1.2, "mm"'),
   ('Q', 'L', '30, "m"'),
   ('Choice', 'temp_mode', 'ChangeGiven'),
   ('Q', 'dT_in', '-40, "K"'),
   ('Choice', 'layout', 'UpLoop'),
   ('Q', 'H', '1.5, "m"'),
   ('Q', 'Sa', '60, "MPa"'),
   ('Q', 'K_scr', '150, "mm^2/m^2"'),
 ], expected=[
   ('dT', -40, "K", 1e-9),
   ('dL', 19.92, "mm", 1e-9),
   ('dL_signed', -19.92, "mm", 1e-9),
   ('sigma_res', 79.68, "MPa", 1e-9),
   ('F_res', 8.0503588208392, "kN", 1e-9),
   ('Delta', 9.96, "mm", 1e-9),
   ('L_req', 1.2935532459083392, "m", 1e-9),
   ('u_leg', 0.8623688306055595, "", 1e-9),
   ('anchor_force', 38.62454824121034, "N", 1e-9),
   ('anchor_moment', 28.968411180907758, "N*m", 1e-9),
   ('scr', 61.97333333333333, "mm^2/m^2", 1e-9),
   ('u_scr', 0.41315555555555555, "", 1e-9),
   ('pass', 1, "", 1e-9),
 ]),
 "ka-z-bend-steel": dict(secondary=False, inputs=[
   ('Q', 'E', '210, "GPa"'),
   ('Q', 'alpha', '12, "um/(m*K)"'),
   ('Q', 'OD', '88.9, "mm"'),
   ('Q', 't_wall', '3.2, "mm"'),
   ('Q', 'L', '30, "m"'),
   ('Q', 'L2', '20, "m"'),
   ('Choice', 'temp_mode', 'TwoTemperatures'),
   ('Q', 'T_install', '10, "degC"'),
   ('Q', 'T_operate', '80, "degC"'),
   ('Choice', 'layout', 'OffsetLeg'),
   ('Q', 'H', '6, "m"'),
   ('Q', 'Sa', '100, "MPa"'),
   ('Q', 'K_scr', '100, "mm^2/m^2"'),
 ], expected=[
   ('L_tot', 50, "m", 1e-9),
   ('dL', 42, "mm", 1e-9),
   ('L_req', 4.85004536061262, "m", 1e-9),
   ('Delta_run', 5.04, "mm", 1e-9),
   ('F_run', 0.28742229497404465, "N", 1e-9),
   ('sigma_run', 0.24195024, "MPa", 1e-9),
   ('u_run', 0.049188437665776706, "", 1e-9),
   ('L_req_run', 1.4756531299733011, "m", 1e-9),
   ('M_run', 4.31133442461067, "N*m", 1e-9),
   ('sigma_run2', 0.16130016, "MPa", 1e-9),
   ('u_run2', 0.04016219117528326, "", 1e-9),
   ('M_run2', 2.8742229497404463, "N*m", 1e-9),
   ('anchor_force', 388.108915228184, "N", 1e-9),
   ('anchor_moment', 1164.3264263994865, "N*m", 1e-9),
   ('L_dev', 56, "m", 1e-9),
   ('U_a', 50.35871324805669, "m", 1e-9),
   ('scr', 118.16784619539838, "mm^2/m^2", 1e-9),
   ('pass', 0, "", 1e-9),
 ]),
 "ka-axial-bellows": dict(secondary=False, inputs=[
   ('Q', 'E', '210, "GPa"'),
   ('Q', 'alpha', '12, "um/(m*K)"'),
   ('Q', 'OD', '88.9, "mm"'),
   ('Q', 't_wall', '3.2, "mm"'),
   ('Q', 'L', '50, "m"'),
   ('Choice', 'temp_mode', 'TwoTemperatures'),
   ('Q', 'T_install', '10, "degC"'),
   ('Q', 'T_operate', '80, "degC"'),
   ('Choice', 'layout', 'AxialBellows'),
   ('Q', 'P_b', '6, "bar"'),
   ('Q', 'A_eff', '80, "cm^2"'),
   ('Q', 'k_b', '120, "N/mm"'),
   ('Q', 'dL_rated', '50, "mm"'),
 ], expected=[
   ('dL', 42, "mm", 1e-9),
   ('F_thrust', 4.8, "kN", 1e-9),
   ('F_spring', 5040, "N", 1e-9),
   ('anchor_force', 9840, "N", 1e-9),
   ('anchor_moment', 0, "N*m", 1e-9),
   ('u_bellows', 0.84, "", 1e-9),
   ('pass', 1, "", 1e-9),
 ]),
}
# Extra probes: name, model inputs, product values observed with tools/verification/probe (run, 2026-10-25).
# Units as the KAT symbols: mm, MPa, kN for F_res and F_thrust, N, N*m, m, mm^2/m^2.
# X11 is a unit-conversion probe (inch, foot, degF, ksi); X5 near-zero, X6 very large, X7 boundary u = 1, X8/X9 bellows, X10 short leg.
EXTRAS = [
    ('X1 L-bend 114.3x4 stainless-like, no K1, 20->150',
     {'E': 200, 'alpha': 17, 'OD': 114.3, 't': 4, 'L': 80, 'mode': 'temps', 'T_install': 20, 'T_operate': 150, 'layout': 'offset', 'H': 10, 'Sa': 150},
     {'dL': 176.8, 'Delta': 176.8, 'L_req': 8.990715211, 'u_leg': 0.8990715211, 'F_leg': 895.5930105, 'M_leg': 4477.965053, 'sigma_leg': 121.24944, 'Delta_run': 22.1, 'F_run': 0.2186506373, 'sigma_run': 0.2368153125, 'u_run': 0.03973372308, 'L_req_run': 3.178697847, 'F_res': 612.64324, 'anchor_force': 895.5930372, 'anchor_moment': 4477.965053, 'Y_scr': 178.1758962, 'scr': 231.5944852, 'pass': 0.0, 'sigma_res': 442.0, 'dL_signed': 176.8}),
    ('X2 L-bend with E_hot 190 and K1 200',
     {'E': 200, 'alpha': 17, 'OD': 114.3, 't': 4, 'E_hot': 190, 'L': 80, 'mode': 'temps', 'T_install': 20, 'T_operate': 150, 'layout': 'offset', 'H': 10, 'Sa': 150, 'K1': 200},
     {'dL': 176.8, 'Delta': 176.8, 'L_req': 8.990715211, 'u_leg': 0.8990715211, 'F_leg': 850.81336, 'M_leg': 4254.0668, 'sigma_leg': 121.24944, 'Delta_run': 22.1, 'F_run': 0.2077181055, 'sigma_run': 0.2368153125, 'u_run': 0.03973372308, 'L_req_run': 3.178697847, 'F_res': 582.011078, 'anchor_force': 850.8133854, 'anchor_moment': 4254.0668, 'Y_scr': 178.1758962, 'scr': 231.5944852, 'u_scr': 1.157972426, 'pass': 0.0, 'sigma_res': 442.0, 'dL_signed': 176.8}),
    ('X3 U-loop cold line T 20 -> -30, plastic-like',
     {'E': 3, 'alpha': 150, 'OD': 110, 't': 6.6, 'L': 40, 'mode': 'temps', 'T_install': 20, 'T_operate': -30, 'layout': 'uloop', 'H': 1.8, 'Sa': 8, 'K1': 50},
     {'dL': 300.0, 'Delta': 150.0, 'L_req': 4.308421985, 'u_leg': 2.393567769, 'F_leg': 2663.836148, 'M_leg': 2397.452533, 'sigma_leg': 45.83333333, 'F_res': 48.23884104, 'anchor_force': 2663.836148, 'anchor_moment': 2397.452533, 'Y_scr': 300.0, 'scr': 2546.296296, 'u_scr': 50.92592593, 'pass': 0.0, 'sigma_res': 22.5, 'dL_signed': -300.0}),
    ('X4 Z-bend 12 m and 40 m, 8 m leg',
     {'E': 210, 'alpha': 12, 'OD': 88.9, 't': 3.2, 'L': 12, 'L2': 40, 'mode': 'temps', 'T_install': 5, 'T_operate': 95, 'layout': 'offset', 'H': 8, 'Sa': 120, 'K1': 250},
     {'dL': 56.16, 'Delta': 56.16, 'L_req': 5.119694913, 'u_leg': 0.6399618641, 'F_leg': 218.9349512, 'M_leg': 875.739805, 'sigma_leg': 49.1461425, 'Delta_run': 8.64, 'F_run': 0.2623742956, 'sigma_run': 0.08834599805, 'u_run': 0.02713331256, 'L_req_run': 0.3255997507, 'u_run2': 0.0495384245, 'F_res': 195.3996238, 'anchor_force': 218.9351085, 'anchor_moment': 875.739805, 'Y_scr': 56.82072861, 'scr': 92.54002182, 'u_scr': 0.3701600873, 'pass': 1.0, 'sigma_res': 226.8, 'dL_signed': 56.16}),
    ('X5 near-zero dT_in 0.01 K, U-loop',
     {'E': 210, 'alpha': 12, 'OD': 88.9, 't': 3.2, 'L': 50, 'mode': 'change', 'dT_in': 0.01, 'layout': 'uloop', 'H': 1, 'Sa': 50},
     {'dL': 0.006, 'Delta': 0.003, 'L_req': 0.05796912972, 'u_leg': 0.05796912972, 'F_leg': 5.987964479, 'M_leg': 2.993982239, 'sigma_leg': 0.168021, 'F_res': 0.02171106931, 'anchor_force': 5.987964479, 'anchor_moment': 2.993982239, 'Y_scr': 0.006, 'scr': 0.13335, 'pass': 0.0, 'sigma_res': 0.0252, 'dL_signed': 0.006}),
    ('X6 very large: 5000 m, dT 400 K, U-loop 40 m legs',
     {'E': 210, 'alpha': 12, 'OD': 88.9, 't': 3.2, 'L': 5000, 'mode': 'change', 'dT_in': 400, 'layout': 'uloop', 'H': 40, 'Sa': 300, 'K1': 100},
     {'dL': 24000.0, 'Delta': 12000.0, 'L_req': 47.33159621, 'u_leg': 1.183289905, 'F_leg': 374.2477799, 'M_leg': 7484.955598, 'sigma_leg': 420.0525, 'F_res': 868.4427723, 'anchor_force': 374.2477799, 'anchor_moment': 7484.955598, 'Y_scr': 24000.0, 'scr': 333.375, 'u_scr': 3.33375, 'pass': 0.0, 'sigma_res': 1008.0, 'dL_signed': 24000.0}),
    ('X7 boundary: H = L_req (u = 1), K1 large',
     {'E': 210, 'alpha': 12, 'OD': 88.9, 't': 3.2, 'L': 50, 'mode': 'temps', 'T_install': 10, 'T_operate': 80, 'layout': 'uloop', 'H': 3.4294999635515384, 'Sa': 100, 'K1': 500},
     {'dL': 42.0, 'Delta': 21.0, 'L_req': 3.429499964, 'u_leg': 1.0, 'F_leg': 1039.165806, 'M_leg': 1781.909547, 'sigma_leg': 100.0, 'F_res': 151.9774851, 'anchor_force': 1039.165806, 'anchor_moment': 1781.909547, 'Y_scr': 42.0, 'scr': 79.36507937, 'u_scr': 0.1587301587, 'pass': 1.0, 'sigma_res': 176.4, 'dL_signed': 42.0}),
    ('X8 bellows no spring no rating, 16 bar 150 cm2',
     {'E': 210, 'alpha': 12, 'OD': 88.9, 't': 3.2, 'L': 100, 'mode': 'temps', 'T_install': 10, 'T_operate': 130, 'layout': 'bellows', 'P_b': 16, 'A_eff': 150},
     {'dL': 144.0, 'F_res': 260.5328317, 'anchor_force': 24000.0, 'anchor_moment': 0.0, 'pass': 0.0, 'F_thrust': 24.0, 'sigma_res': 302.4, 'dL_signed': 144.0}),
    ('X9 bellows over rating 45 mm vs 40 mm',
     {'E': 210, 'alpha': 12, 'OD': 88.9, 't': 3.2, 'L': 50, 'mode': 'temps', 'T_install': 10, 'T_operate': 80, 'layout': 'bellows', 'P_b': 6, 'A_eff': 80, 'k_b': 50, 'dL_rated': 40},
     {'dL': 42.0, 'F_res': 151.9774851, 'anchor_force': 6900.0, 'anchor_moment': 0.0, 'pass': 0.0, 'F_thrust': 4.8, 'F_spring': 2100.0, 'u_bellows': 1.05, 'sigma_res': 176.4, 'dL_signed': 42.0}),
    ('X10 short leg: 0.6 m legs in a 100 mm pipe (warning expected)',
     {'E': 210, 'alpha': 12, 'OD': 114.3, 't': 4, 'L': 20, 'mode': 'temps', 'T_install': 10, 'T_operate': 70, 'layout': 'uloop', 'H': 0.6, 'Sa': 150},
     {'dL': 14.4, 'Delta': 7.2, 'L_req': 1.859148192, 'u_leg': 3.09858032, 'F_leg': 177294.9964, 'M_leg': 53188.49893, 'sigma_leg': 1440.18, 'F_res': 209.5738866, 'anchor_force': 177294.9964, 'anchor_moment': 53188.49893, 'Y_scr': 14.4, 'scr': 1143.0, 'pass': 0.0, 'sigma_res': 151.2, 'dL_signed': 14.4}),
    ('X11 imperial units: 160 ft, 6.625 in OD x 0.280 in, 68->350 degF, 20 ft leg, Sa 20 ksi',
     {'E': 210, 'alpha': 12, 'OD': 168.27499999999998, 't': 7.112, 'L': 48.768, 'mode': 'temps', 'T_install': 20, 'T_operate': 176.66666666666666, 'layout': 'offset', 'H': 6.096, 'Sa': 137.89514586336},
     {'dL': 91.68384, 'Delta': 91.68384, 'L_req': 8.395604707, 'u_leg': 1.377231743, 'F_leg': 11946.78481, 'M_leg': 36413.80011, 'sigma_leg': 261.555, 'Delta_run': 11.46048, 'F_run': 2.916695511, 'sigma_run': 0.5108496094, 'u_run': 0.06086561906, 'L_req_run': 2.96829451, 'F_res': 1421.621908, 'anchor_force': 11946.78517, 'anchor_moment': 36413.80011, 'Y_scr': 92.39734368, 'scr': 475.7975481, 'pass': 0.0, 'sigma_res': 394.8, 'dL_signed': 91.68384}),
    ('X12 L-bend where the run is the weak member (short run 4 m, long leg 15 m)',
     {'E': 210, 'alpha': 12, 'OD': 88.9, 't': 3.2, 'L': 4, 'mode': 'temps', 'T_install': 10, 'T_operate': 90, 'layout': 'offset', 'H': 15, 'Sa': 100, 'K1': 300},
     {'dL': 3.84, 'Delta': 3.84, 'L_req': 1.466515871, 'u_leg': 0.09776772474, 'F_leg': 2.270990973, 'M_leg': 17.03243229, 'sigma_leg': 0.9558528, 'Delta_run': 14.4, 'F_run': 449.0973359, 'sigma_run': 50.4063, 'u_run': 0.7099739432, 'L_req_run': 2.839895773, 'F_res': 173.6885545, 'anchor_force': 449.1030778, 'anchor_moment': 898.1946718, 'Y_scr': 14.90320771, 'scr': 109.6643918, 'u_scr': 0.3655479726, 'pass': 1.0, 'sigma_res': 201.6, 'dL_signed': 3.84}),
]


def rel(a, b):
    return abs(a - b) / max(abs(b), 1e-300) if b != 0 else abs(a - b)


def main():
    fails = 0
    n = 0
    print("%-26s %-12s %-16s %-16s %-9s %-8s %s" % ("case", "symbol", "KAT expected", "independent", "rel.diff", "KAT tol", "result"))
    for name, k in KATS.items():
        o = pe(conv(k["inputs"]))
        for sym, exp, unit, tol in k["expected"]:
            got = o.get(sym)
            if got is None:
                print("%-26s %-12s %-16s %-16s %-9s %-8s %s" % (name, sym, exp, "n/a", "-", tol, "FAIL (no independent value)"))
                fails += 1
                n += 1
                continue
            r = rel(got, exp)
            ok = r <= max(tol, 1e-6)
            n += 1
            fails += 0 if ok else 1
            print("%-26s %-12s %-16.10g %-16.10g %-9.2e %-8.0e %s" % (name, sym, exp, got, r, tol, "PASS" if ok else "FAIL"))
    print("\nExtra checks (product values hard-coded from the probe tool, bs.pipe-expansion 1.2.0)")
    for name, inp, prod in EXTRAS:
        o = pe(inp)
        for sym, pv in prod.items():
            got = o.get(sym)
            r = rel(got, pv) if got is not None else float("inf")
            ok = r <= 1e-6
            n += 1
            fails += 0 if ok else 1
            print("%-26s %-12s %-16.10g %-16.10g %-9.2e %-8s %s" % (name.split()[0], sym, pv, got if got is not None else float("nan"), r, "1e-6", "PASS" if ok else "FAIL"))
    print("\n%d values compared, %d failed" % (n, fails))
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(main())
