#!/usr/bin/env python3
"""Independent re-derivation of bs.room-heat-loss (B4), Track P, P1.

Method (written from the standards, not translated from the C#):
  R_total = R_si + sum(d/lambda) + R_g + R_se          (BS EN ISO 6946, homogeneous layers in series)
  U       = 1/R_total, or the user's U override
  Phi_i   = A * U * f * (theta_int - theta_adj)        (BS EN 12831-1 transmission, simplified)
  Phi_tb  = y_tb * sum(A*f over outside-facing) * (theta_int - theta_e)
  Phi_V   = 0.34 * n * V * (theta_int - theta_supply or theta_e)  [W, flow in m3/h]
  rho*cp/3600 = 1.2 * 1005 / 3600 = 0.335 (check of the 0.34 constant)
Run: python3 tools/verification/bs.room-heat-loss.py   (exit 0 = all rows pass)
"""
import sys

RHO_CP_WH = 1.2 * 1005.0 / 3600.0  # 0.335 Wh/(m3 K), compared with the product constant 0.34
K_AIR = 0.34

# The product's known-answer layers changed at 0772fcf8 (invented illustrative conductivities, no manufacturer-style values).
CAVITY_LAYERS = [(102.5, 0.84), (100.0, 0.03), (100.0, 0.2), (13.0, 0.4)]  # d in mm, lambda W/(m K)


def u_value(layers_mm_lambda, rsi=0.13, rse=0.04, rg=0.0):
    r = rsi + rg + rse + sum((d / 1000.0) / lam for d, lam in layers_mm_lambda)
    return r, 1.0 / r


def room(elements, t_int, t_e, y_tb=0.0, n=0.5, V=40.0, t_supply=None, n_inf=None,
         a_floor=None, f_add=0.0, dhdg=False, u_target=None):
    """elements: list of dict(area, layers|None, u_user|None, t_adj|None, f, rsi, rse, rg, u_max)."""
    out = {}
    dT = t_int - t_e
    phi_sum = 0.0
    a_tb = 0.0
    hf = 0.0  # sum A*U*f
    ha = 0.0  # sum A*U
    a_all = 0.0
    for i, e in enumerate(elements, 1):
        outside = e.get("t_adj") is None or e["t_adj"] <= t_e
        # The product uses the entered adjacent temperature even when it is below theta_e (see report P1-bs.room-heat-loss-02).
        t_adj = t_e if e.get("t_adj") is None else e["t_adj"]
        # Heat-flow direction (product 1.1.0): blank = horizontal 0.13, upward 0.10, downward 0.17 (BS EN ISO 6946 conventional values).
        rsi = e.get("rsi", {"upward": 0.10, "downward": 0.17}.get(e.get("flow"), 0.13))
        rse = e.get("rse", 0.04 if outside else rsi)
        rg = e.get("rg", 0.0)
        if e.get("u_user") is not None:
            u = e["u_user"]
        else:
            r, u = u_value(e["layers"], rsi, rse, rg)
            out[f"R_{i}"] = r
        f = e.get("f", 1.0)
        phi = e["area"] * u * f * (t_int - t_adj)
        out[f"U_{i}"] = u
        out[f"Phi_{i}"] = phi
        # The global target applies to built-up elements facing outside only (product 1.1.0, B-A-04).
        tgt = e.get("u_max", u_target if outside else None)
        if tgt is not None and e.get("u_user") is None:
            out[f"u_{i}"] = u / tgt
        phi_sum += phi
        hf += e["area"] * u * f
        ha += e["area"] * u
        a_all += e["area"]
        if outside:
            a_tb += e["area"] * f
    phi_tb = y_tb * a_tb * dT
    out["A_tb"] = a_tb
    out["U_m"] = hf / a_all            # what the product labels "area-weighted mean U" (f included)
    out["U_m_area"] = ha / a_all       # true area-weighted mean U (f not included)
    out["Phi_tb"] = phi_tb
    out["Phi_T"] = phi_sum + phi_tb
    if t_supply is None:
        qv = n * V
        phi_v = K_AIR * qv * dT
    else:
        qv = n * V
        phi_v = K_AIR * qv * (t_int - t_supply)
        if n_inf:
            qinf = n_inf * V
            phi_v += K_AIR * qinf * dT
            out["q_inf"] = qinf
    out["q_v"] = qv
    out["Phi_V"] = phi_v
    phi_hl = out["Phi_T"] + phi_v
    if dhdg:
        phi_hl *= (1 + f_add / 100.0)
    out["Phi_HL"] = phi_hl
    out["HLC"] = phi_hl / dT
    if a_floor:
        out["Phi_A"] = phi_hl / a_floor
    return out


def cavity_room(**kw):
    els = [dict(area=20, layers=CAVITY_LAYERS), dict(area=3, u_user=1.4)]
    return room(els, 21, -3, n=0.5, V=40, u_target=0.3, **kw)


def mvhr_room(**kw):
    els = [dict(area=15, layers=CAVITY_LAYERS),
           dict(area=12, layers=[(12.5, 0.22), (12.5, 0.22)], t_adj=18, rg=0.18),
           dict(area=10, u_user=0.25, f=0.6)]
    return room(els, 21, -3, y_tb=0.05, n=0.6, V=50, t_supply=16, **kw)


# (case, symbol, KAT expected, KAT tol)
KATS = [
    ("ka-cavity-wall", "R_1", 4.157857, 1e-6), ("ka-cavity-wall", "U_1", 0.2405085, 1e-6),
    ("ka-cavity-wall", "u_1", 0.8016950, 1e-6), ("ka-cavity-wall", "Phi_1", 115.44408, 1e-6),
    ("ka-cavity-wall", "Phi_2", 100.8, 1e-9), ("ka-cavity-wall", "Phi_T", 216.2441, 1e-6),
    ("ka-cavity-wall", "q_v", 20, 1e-9), ("ka-cavity-wall", "Phi_V", 163.2, 1e-9),
    ("ka-cavity-wall", "Phi_HL", 379.4441, 1e-6), ("ka-cavity-wall", "HLC", 15.81017, 1e-6),
    ("ka-mvhr-partition", "U_2", 1.806240, 1e-6), ("ka-mvhr-partition", "Phi_1", 86.58306, 1e-6),
    ("ka-mvhr-partition", "Phi_2", 65.02463, 1e-6), ("ka-mvhr-partition", "Phi_3", 36, 1e-9),
    ("ka-mvhr-partition", "A_tb", 21, 1e-9), ("ka-mvhr-partition", "Phi_tb", 25.2, 1e-9),
    ("ka-mvhr-partition", "Phi_T", 212.8077, 1e-6), ("ka-mvhr-partition", "Phi_V", 51, 1e-9),
    ("ka-mvhr-partition", "Phi_HL", 263.8077, 1e-6), ("ka-mvhr-partition", "HLC", 10.99199, 1e-6),
    ("ka-dhdg-allowance", "Phi_T", 216.2441, 1e-6), ("ka-dhdg-allowance", "Phi_V", 163.2, 1e-9),
    ("ka-dhdg-allowance", "Phi_HL", 417.3885, 1e-6), ("ka-dhdg-allowance", "HLC", 17.39119, 1e-6),
    ("ka-single-layer-target", "U_1", 4.142012, 1e-6), ("ka-single-layer-target", "u_1", 0.9204471, 1e-6),
    ("ka-single-layer-target", "Phi_1", 994.0828, 1e-6), ("ka-single-layer-target", "Phi_V", 4.08, 1e-9),
    ("ka-mvhr-infiltration", "Phi_V", 91.8, 1e-9), ("ka-mvhr-infiltration", "Phi_T", 212.8077, 1e-6),
    ("ka-mvhr-infiltration", "Phi_HL", 304.6077, 1e-6), ("ka-mvhr-infiltration", "HLC", 12.69199, 1e-6),
    ("ka-roof-upward", "R_1", 2.14, 1e-9), ("ka-roof-upward", "U_1", 0.4672897, 1e-6),
    ("ka-roof-upward", "u_1", 0.9345794, 1e-6), ("ka-roof-upward", "Phi_1", 112.1495, 1e-6),
    ("ka-roof-upward", "U_2", 2.173913, 1e-6),
]

CASES = {
    "ka-cavity-wall": lambda: cavity_room(),
    "ka-mvhr-partition": lambda: mvhr_room(),
    "ka-dhdg-allowance": lambda: cavity_room(dhdg=True, f_add=10),
    "ka-single-layer-target": lambda: room([dict(area=10, layers=[(100, 1.4)])], 21, -3, n=0.1, V=5, u_target=4.5),
    "ka-roof-upward": lambda: room([dict(area=10, layers=[(100, 0.05)], flow="upward"),
                                    dict(area=12, layers=[(100, 0.5)], t_adj=18)], 21, -3, n=0.1, V=5, u_target=0.5),
    "ka-mvhr-infiltration": lambda: mvhr_room(n_inf=0.1),
}


BASE = dict(t_int=21, t_e=-3, n=0.5, V=40)
W = lambda **k: dict(area=10, layers=[(100, 0.5)], **k)
P1_LAYERS_WALL = [(102.5, 0.77), (90, 0.022), (100, 0.15), (13, 0.57)]
P1_LAYERS_ROOF = [(25, 1.0), (200, 0.037), (12.5, 0.21)]
EXTRAS = [
    # (label, function returning dict, symbol, product value observed with the probe tool)
    ("p1-typical", lambda: room([dict(area=18, layers=P1_LAYERS_WALL), dict(area=20, layers=P1_LAYERS_ROOF, rsi=0.10),
        dict(area=20, u_user=0.22, f=0.7), dict(area=4, u_user=1.4)], 21, -4, y_tb=0.08, n=0.7, V=55, a_floor=20), "U_1", 0.196714875),
    ("p1-typical", None, "U_2", 0.1776221266),
    ("p1-typical", None, "Phi_tb", 112.0), ("p1-typical", None, "Phi_T", 506.3327571),
    ("p1-typical", None, "Phi_V", 327.25), ("p1-typical", None, "Phi_HL", 833.5827571),
    ("p1-typical", None, "HLC", 33.34331028), ("p1-typical", None, "Phi_A", 41.67913785),
    ("p1-typical", None, "U_m", 0.2544082304),
    ("p2-max-boundary", lambda: room([dict(area=500, layers=[(500, 5)])], 16, -15, y_tb=0.3, n=3, V=1000), "Phi_T", 62057.40741),
    ("p2-max-boundary", None, "Phi_V", 31620.0), ("p2-max-boundary", None, "Phi_HL", 93677.40741), ("p2-max-boundary", None, "HLC", 3021.851852),
    ("p3-near-zero", lambda: room([dict(area=0.1, layers=[(500, 0.01)])], 16, 5, n=0.1, V=5, t_supply=16), "Phi_T", 0.02192545346),
    ("p3-near-zero", None, "Phi_V", 0.0), ("p3-near-zero", None, "HLC", 0.001993223042),
    ("p4-gain-adj33", lambda: room([dict(area=30, layers=[(100, 0.5)], t_adj=33)], 21, 5, n=0.1, V=5), "Phi_T", -782.6086957),
    ("p4-gain-adj33", None, "Phi_HL", -779.8886957), ("p4-gain-adj33", None, "HLC", -48.74304348),
    ("p5-adj-below-te", lambda: room([W(t_adj=-8)], 21, -3, y_tb=0.1, n=0.5, V=40), "Phi_T", 807.7837838),
    ("p5-adj-below-te", None, "Phi_tb", 24.0),
    ("p5b-adj-equals-te", lambda: room([W(t_adj=-3)], 21, -3, y_tb=0.1, n=0.5, V=40), "Phi_T", 672.6486486),
    ("p6-mvhr-cold-supply", lambda: room([W()], 21, -3, n=0.5, V=40, t_supply=-8, n_inf=0.2), "Phi_V", 262.48),
    ("p6-mvhr-cold-supply", None, "Phi_HL", 911.1286486),
    ("p7-shared-build-up", lambda: room([W(), dict(area=8, layers=[(100, 0.5)])], 21, -3, n=0.5, V=40), "Phi_T", 1167.567568),
    ("p7-shared-build-up", None, "Phi_HL", 1330.767568),
    ("p9-dhdg-20pct", lambda: room([W()], 21, -3, n=0.5, V=40, dhdg=True, f_add=20), "Phi_HL", 974.2183784),
    ("p9b-en-ignores-f_add", lambda: room([W()], 21, -3, n=0.5, V=40, dhdg=False, f_add=10), "Phi_HL", 811.8486486),
    ("p10-ninf-natural-ignored", lambda: room([W()], 21, -3, n=0.5, V=40, n_inf=None), "Phi_HL", 811.8486486),
    ("p11-rsi-0.17", lambda: room([W(rsi=0.17)], 21, -3, n=0.5, V=40), "U_1", 2.43902439),
    ("p11-rsi-0.17", None, "Phi_HL", 748.5658537),
    ("p12-partition-18C", lambda: room([W(t_adj=18)], 21, -3, n=0.5, V=40), "U_1", 2.173913043),
    ("p12-partition-18C", None, "Phi_HL", 228.4173913),
    ("p13-rg-0.18", lambda: room([W(rg=0.18, rsi=0.0, rse=0.0)], 21, -3, n=0.5, V=40), "U_1", 2.631578947),
    # u1: product inputs were 69.8 degF, 270.15 K, 0.1 m and 40000 L; they convert to 21, -3, 100 mm, 40 m3.
    ("u1-unit-conversion", lambda: room([W()], 21, -3, n=0.5, V=40), "Phi_HL", 811.8486486),
    ("u3-targets", lambda: room([dict(area=10, layers=[(100, 0.5)], u_max=2.0), dict(area=10, layers=[(100, 0.04)], u_max=0.3),
        dict(area=5, u_user=0.2, u_max=0.15)], 21, -3, n=0.5, V=40, u_target=0.35), "u_2", 1.248439451),
    ("u3-targets", None, "u_1", 1.351351351), ("u3-targets", None, "U_m", 1.270893815),
]
_last = {}
def _resolve():
    out = []
    for label, fn, sym, prod in EXTRAS:
        if fn is not None:
            _last[label] = fn()
        out.append((label, _last[label], sym, prod))
    return out

# Informational: true area-weighted mean U for the typical probe (product label "area-weighted" includes f).
INFO_NOTE = "p1-typical: product U_m = 0.2544 includes f; true area-weighted mean U (no f) is shown here."


def row(case, sym, exp, ind, tol):
    tol = max(tol, 1e-6)
    rel = abs(ind - exp) / abs(exp) if exp != 0 else abs(ind - exp)
    ok = rel <= tol
    print(f"{case:28s} {sym:8s} KAT={exp:<14.7g} indep={ind:<14.7g} rel={rel:9.2e} tol={tol:8.1e} {'PASS' if ok else 'FAIL'}")
    return ok


def main():
    print(f"Check of the air constant: rho*cp/3600 = {RHO_CP_WH:.4f} Wh/(m3 K); product and spec use {K_AIR}")
    allok = True
    cache = {k: f() for k, f in CASES.items()}
    print("--- Known-answer cases ---")
    for case, sym, exp, tol in KATS:
        allok &= row(case, sym, exp, cache[case][sym], tol)
    print("--- Extra checks (product values observed with the probe tool) ---")
    for label, res, sym, prod in _resolve():
        allok &= row(label, sym, prod, res[sym], 1e-6)
    p1 = _last["p1-typical"]
    print(INFO_NOTE, f"U_m(no f) = {p1['U_m_area']:.6f}, U_m(with f) = {p1['U_m']:.6f}")
    print(f"Air constant: rho*cp/3600 = {RHO_CP_WH:.4f}; 0.34 is {100*(0.34/RHO_CP_WH-1):.1f} % higher (conservative)")
    print("ALL PASS" if allok else "SOME FAIL")
    return 0 if allok else 1


if __name__ == "__main__":
    sys.exit(main())
