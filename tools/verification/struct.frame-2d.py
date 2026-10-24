#!/usr/bin/env python3
"""Independent re-derivation of struct.frame-2d (Track P, P1).

A small direct-stiffness solver written from first principles in numpy (dense, no banding):
  * 3 freedoms per node (ux, uy, rotation anticlockwise), Euler-Bernoulli 6x6 member stiffness;
  * end moment releases by static condensation, the released rotation is recovered afterwards;
  * equivalent nodal loads by integrating the load against the cubic Hermite shape functions
    (this gives the exact fixed-end forces, so no Macaulay table is needed);
  * member diagrams by direct integration of the end state: V, M (sagging +), N (tension +),
    then EI y'' = M integrated twice on 4001 stations; the far-end deflection is checked against the solved value.
Extra checks (F*, C*, X*, B120, COMB) compare hard-coded product values with this solver; a DIFF there is a finding, not a KAT failure, so the exit status follows the known-answer cases only.
Units: kN, m, kN*m, GPa -> kN/m^2, cm^2 -> m^2, cm^4 -> m^4.  Deflection reported in mm.
Exit 0 when every KAT row passes max(KAT tol, 1e-6).
"""
import math
import sys

import numpy as np

GAUSS_X, GAUSS_W = np.polynomial.legendre.leggauss(10)
NST = 4001


class Frame:
    def __init__(self, E_gpa, nodes, elems, loads):
        """nodes: {id: (x, y, support, kx, ky, kr)}; elems: [(id,i,j,A_cm2,I_cm4,E_gpa|None,rel_i,rel_j)];
        loads: list of dicts with kind, target, a, b, w1, w2, Fx, Fy, M, gravity."""
        self.E = E_gpa * 1e6
        self.nodes, self.elems, self.loads = nodes, elems, loads
        self.ids = sorted(nodes)
        self.idx = {n: k for k, n in enumerate(self.ids)}
        self.solve()

    # ---- geometry
    def geom(self, e):
        _, i, j, A, I, Eg, ri, rj = e
        xi, yi = self.nodes[i][:2]
        xj, yj = self.nodes[j][:2]
        L = math.hypot(xj - xi, yj - yi)
        return L, (xj - xi) / L, (yj - yi) / L

    def klocal(self, e):
        _, i, j, A, I, Eg, ri, rj = e
        E = (Eg * 1e6) if Eg else self.E
        A, I = A * 1e-4, I * 1e-8
        L, c, s = self.geom(e)
        a, b = E * A / L, E * I / L ** 3
        k = np.array([[a, 0, 0, -a, 0, 0],
                      [0, 12 * b, 6 * b * L, 0, -12 * b, 6 * b * L],
                      [0, 6 * b * L, 4 * b * L * L, 0, -6 * b * L, 2 * b * L * L],
                      [-a, 0, 0, a, 0, 0],
                      [0, -12 * b, -6 * b * L, 0, 12 * b, -6 * b * L],
                      [0, 6 * b * L, 2 * b * L * L, 0, -6 * b * L, 4 * b * L * L]])
        return k, E * I

    def rot(self, e):
        L, c, s = self.geom(e)
        R = np.array([[c, s, 0], [-s, c, 0], [0, 0, 1]])
        T = np.zeros((6, 6))
        T[:3, :3] = R
        T[3:, 3:] = R
        return T

    # ---- loads on one member: equivalent nodal vector in local axes plus the load description
    def member_loads(self, e):
        L, c, s = self.geom(e)
        feq = np.zeros(6)
        dist, pts, cpl = [], [], []      # dist: (a,b,wy1,wy2, wx) local; pts: (a, Fx_l, Fy_l); cpl: (a, M)

        def N(x):
            r = x / L
            return np.array([1 - 3 * r * r + 2 * r ** 3, L * (r - 2 * r * r + r ** 3), 3 * r * r - 2 * r ** 3, L * (-r * r + r ** 3)])

        def dN(x):
            r = x / L
            return np.array([(-6 * r + 6 * r * r) / L, 1 - 4 * r + 3 * r * r, (6 * r - 6 * r * r) / L, -2 * r + 3 * r * r])

        for ld in self.loads:
            if ld["target"] != e[0] or ld["kind"] == "nodal":
                continue
            k = ld["kind"]
            if k in ("udl", "trapezoid"):
                a, b = ld["a"], ld["b"]
                if b <= a:                       # a = b = 0 means the whole span
                    a, b = 0.0, L
                w1 = ld["w1"]
                w2 = ld["w2"] if k == "trapezoid" else w1
                if ld.get("gravity"):
                    # w positive downward per metre of member: global (0, -w) -> local components
                    fac = (lambda w: (-w * s, -w * c))   # (axial, transverse) per metre for global -y load w
                    ax1, tr1 = fac(w1)
                    ax2, tr2 = fac(w2)
                else:
                    ax1 = ax2 = 0.0
                    tr1, tr2 = w1, w2
                dist.append((a, b, tr1, tr2, ax1, ax2))
                xs = 0.5 * (b - a) * GAUSS_X + 0.5 * (a + b)
                for x, wt in zip(xs, GAUSS_W):
                    t = (x - a) / (b - a)
                    q = tr1 + (tr2 - tr1) * t
                    p = ax1 + (ax2 - ax1) * t
                    n = N(x)
                    f = 0.5 * (b - a) * wt
                    feq[1] += f * q * n[0]
                    feq[2] += f * q * n[1]
                    feq[4] += f * q * n[2]
                    feq[5] += f * q * n[3]
                    feq[0] += f * p * (1 - x / L)
                    feq[3] += f * p * (x / L)
            elif k == "point":
                a = ld["a"]
                fxl = c * ld["Fx"] + s * ld["Fy"]
                fyl = -s * ld["Fx"] + c * ld["Fy"]
                pts.append((a, fxl, fyl))
                n = N(a)
                feq[1] += fyl * n[0]
                feq[2] += fyl * n[1]
                feq[4] += fyl * n[2]
                feq[5] += fyl * n[3]
                feq[0] += fxl * (1 - a / L)
                feq[3] += fxl * (a / L)
            elif k == "moment":
                a = ld["a"]
                cpl.append((a, ld["M"]))
                d = dN(a)
                feq[1] += ld["M"] * d[0]
                feq[2] += ld["M"] * d[1]
                feq[4] += ld["M"] * d[2]
                feq[5] += ld["M"] * d[3]
        return feq, dist, pts, cpl

    # ---- assembly and solution
    def solve(self):
        nn = len(self.ids)
        K = np.zeros((3 * nn, 3 * nn))
        F = np.zeros(3 * nn)
        self.cache = {}
        for e in self.elems:
            k, EI = self.klocal(e)
            feq, dist, pts, cpl = self.member_loads(e)
            rel = [e[6], e[7]]
            rdofs = [2] if rel[0] else []
            rdofs += [5] if rel[1] else []
            adofs = [d for d in range(6) if d not in rdofs]
            kc, fc = k.copy(), feq.copy()
            if rdofs:
                krr = k[np.ix_(rdofs, rdofs)]
                kar = k[np.ix_(adofs, rdofs)]
                kc = np.zeros((6, 6))
                kc[np.ix_(adofs, adofs)] = k[np.ix_(adofs, adofs)] - kar @ np.linalg.solve(krr, kar.T)
                fc = np.zeros(6)
                fc[adofs] = feq[adofs] - kar @ np.linalg.solve(krr, feq[rdofs])
            T = self.rot(e)
            kg, fg = T.T @ kc @ T, T.T @ fc
            dofs = [3 * self.idx[e[1]] + d for d in range(3)] + [3 * self.idx[e[2]] + d for d in range(3)]
            K[np.ix_(dofs, dofs)] += kg
            F[dofs] += fg
            self.cache[e[0]] = dict(k=k, feq=feq, T=T, rdofs=rdofs, adofs=adofs, dofs=dofs, EI=EI,
                                    dist=dist, pts=pts, cpl=cpl)
        for ld in self.loads:
            if ld["kind"] == "nodal":
                b = 3 * self.idx[ld["target"]]
                F[b] += ld["Fx"]
                F[b + 1] += ld["Fy"]
                F[b + 2] += ld["M"]
        fixed = []
        for n in self.ids:
            x, y, sup, kx, ky, kr = self.nodes[n]
            b = 3 * self.idx[n]
            if sup == "pin":
                fixed += [b, b + 1]
            elif sup == "roller (vertical)":
                fixed += [b + 1]
            elif sup == "roller (horizontal)":
                fixed += [b]
            elif sup == "fixed":
                fixed += [b, b + 1, b + 2]
            elif sup == "spring":
                K[b, b] += kx
                K[b + 1, b + 1] += ky
                K[b + 2, b + 2] += kr
        for d in range(3 * nn):
            if d not in fixed and K[d, d] == 0.0 and not F[d]:
                fixed.append(d)                 # an unconnected or fully released freedom: hold it at zero
        free = [d for d in range(3 * nn) if d not in fixed]
        u = np.zeros(3 * nn)
        u[free] = np.linalg.solve(K[np.ix_(free, free)], F[free])
        self.u, self.fixed = u, fixed
        self.R = (K @ u - F)
        self.Rn = {n: self.R[3 * self.idx[n]:3 * self.idx[n] + 3].copy() for n in self.ids}
        for n in self.ids:                      # a spring pushes back with -k u
            x, y, sup, kx, ky, kr = self.nodes[n]
            if sup == "spring":
                b = 3 * self.idx[n]
                self.Rn[n] = -np.array([kx * u[b], ky * u[b + 1], kr * u[b + 2]])
        # spring supports react with the spring force: R above already includes -F only for fixed dofs; keep fixed only
        self.members = {e[0]: self.member_results(e) for e in self.elems}

    def member_results(self, e):
        c = self.cache[e[0]]
        L, cs, sn = self.geom(e)
        ug = self.u[c["dofs"]]
        ul = c["T"] @ ug
        if c["rdofs"]:
            krr = c["k"][np.ix_(c["rdofs"], c["rdofs"])]
            kra = c["k"][np.ix_(c["rdofs"], c["adofs"])]
            ur = np.linalg.solve(krr, c["feq"][c["rdofs"]] - kra @ ul[c["adofs"]])
            ul = ul.copy()
            ul[c["rdofs"]] = ur
        Q = c["k"] @ ul - c["feq"]          # end actions on the member, local axes
        Fx1, Fy1, M1 = Q[0], Q[1], Q[2]
        marks = [0.0, L] + [d_[0] for d_ in c["dist"]] + [d_[1] for d_ in c["dist"]] + [p_[0] for p_ in c["pts"]] + [q_[0] for q_ in c["cpl"]]
        x = np.unique(np.concatenate([np.linspace(0, L, NST), np.array(marks), np.array(marks) + 1e-9 * L, np.array(marks) - 1e-9 * L]))
        x = x[(x >= 0) & (x <= L)]
        NS = len(x)
        V = np.full(NS, Fy1)
        Mx = -M1 + Fy1 * x
        Nx = np.full(NS, -Fx1)
        for (a, b, tr1, tr2, ax1, ax2) in c["dist"]:
            xe = np.clip(x, a, b)
            span = b - a
            # integral of q from a to xe
            Iq = tr1 * (xe - a) + (tr2 - tr1) * (xe - a) ** 2 / (2 * span)
            V += Iq
            # moment of q about x: integral q(s)(x - s) ds, s from a to xe
            # moment of the load about the section: Gauss quadrature in _moment_quad
            Mx += self._moment_quad(a, b, tr1, tr2, x)
            Nx -= (ax1 * (xe - a) + (ax2 - ax1) * (xe - a) ** 2 / (2 * span))
        for (a, fxl, fyl) in c["pts"]:
            h = (x >= a - 1e-12).astype(float)
            V += fyl * h
            Mx += fyl * (x - a) * h
            Nx -= fxl * h
        for (a, mc) in c["cpl"]:
            Mx -= mc * (x >= a - 1e-12)
        # integrate EI y'' = M
        EI = c["EI"]
        dx = np.diff(x)
        th = np.concatenate([[0], np.cumsum((Mx[1:] + Mx[:-1]) / 2 * dx)]) / EI + ul[2]
        y = np.concatenate([[0], np.cumsum((th[1:] + th[:-1]) / 2 * dx)]) + ul[1]
        return dict(x=x, V=V, M=Mx, N=Nx, y=y, ul=ul, Q=Q, L=L, y_far_solved=ul[4])

    def _moment_quad(self, a, b, tr1, tr2, x):
        """integral over s in [a, min(x,b)] of q(s)(x-s) ds, vectorised by Gauss quadrature."""
        out = np.zeros_like(x)
        hi = np.minimum(x, b)
        ok = hi > a
        for g, w in zip(GAUSS_X, GAUSS_W):
            s_ = 0.5 * (hi - a) * g + 0.5 * (hi + a)
            q = tr1 + (tr2 - tr1) * (s_ - a) / (b - a)
            out += np.where(ok, 0.5 * (hi - a) * w * q * (x - s_), 0.0)
        return out


# ------------------------------------------------------------ helpers for building models
def node(id_, x, y, sup="free", kx=0.0, ky=0.0, kr=0.0):
    return id_, (x, y, sup, kx, ky, kr)


def load(case, kind, target, a=0, b=0, w1=0, w2=0, Fx=0, Fy=0, M=0, gravity=False):
    return dict(case=case, kind=kind, target=target, a=a, b=b, w1=w1, w2=w2, Fx=Fx, Fy=Fy, M=M, gravity=gravity)


def build(E, nodes, elems, loads):
    return Frame(E, dict(nodes), elems, loads)


def summary(fr):
    """Global extremes the product reports (largest over all members, local positions)."""
    out = dict(M_max=-1e99, M_min=1e99, V_max=0.0, N_max=-1e99, N_min=1e99, d_max=0.0, x_Mmax=0.0)
    out["x_Mmax_all"] = []
    for eid, m in fr.members.items():
        k = int(np.argmax(m["M"]))
        if m["M"][k] > out["M_max"]:
            out["M_max"], out["x_Mmax"] = m["M"][k], m["x"][k]
        out["M_min"] = min(out["M_min"], m["M"].min())
        out["x_Mmax_all"].append((m["M"].max(), m["x"][int(np.argmax(m["M"]))]))
        out["V_max"] = max(out["V_max"], np.abs(m["V"]).max())
        out["N_max"] = max(out["N_max"], m["N"].max())
        out["N_min"] = min(out["N_min"], m["N"].min())
        out["d_max"] = max(out["d_max"], np.abs(m["y"]).max() * 1000)
    return out


# ------------------------------------------------------------ reporting
FAIL = [0]


def row(case, sym, kat, ind, tol, note=""):
    floor = max(tol, 1e-6)
    scale = abs(kat) if kat != 0 else 1.0
    rel = abs(ind - kat) / scale
    ok = rel <= floor
    FAIL[0] += 0 if ok else 1
    print(f"{case:<26}{sym:<10}{kat:>14.6g}{ind:>14.6g}{rel:>11.2e}{tol:>10.1e}  {'PASS' if ok else 'FAIL'} {note}")


def kats():
    print(f"{'case':<26}{'symbol':<10}{'KAT':>14}{'indep':>14}{'rel.diff':>11}{'tol':>10}  result")
    # 1. simply supported beam, UDL 10 kN/m down, L = 6, I = 8000 cm4
    fr = build(210, [node(1, 0, 0, "pin"), node(2, 6, 0, "roller (vertical)")], [(1, 1, 2, 100, 8000, None, False, False)],
               [load(1, "udl", 1, 0, 6, -10)])
    s, c = summary(fr), "ka-ss-udl"
    for sym, k, v, t in [("R1_y", 30, fr.Rn[1][1], 1e-9), ("R2_y", 30, fr.Rn[2][1], 1e-9), ("M_max", 45, s["M_max"], 1e-9),
                         ("x_Mmax", 3, s["x_Mmax"], 1e-6), ("V_max", 30, s["V_max"], 1e-9), ("d_max", 10.0446, s["d_max"], 1e-4)]:
        row(c, sym, k, v, t)
    # 2. cantilever tip load
    fr = build(200, [node(1, 0, 0, "fixed"), node(2, 3, 0, "free")], [(1, 1, 2, 50, 2000, None, False, False)],
               [load(1, "nodal", 2, Fy=-20)])
    s, c = summary(fr), "ka-cantilever-point"
    for sym, k, v, t in [("R1_y", 20, fr.Rn[1][1], 1e-9), ("R1_m", 60, fr.Rn[1][2], 1e-9), ("M_min", -60, s["M_min"], 1e-9),
                         ("d_max", 45, s["d_max"], 1e-9)]:
        row(c, sym, k, v, t)
    # 3. propped cantilever, UDL 12 kN/m, L = 8
    fr = build(210, [node(1, 0, 0, "fixed"), node(2, 8, 0, "roller (vertical)")], [(1, 1, 2, 100, 20000, None, False, False)],
               [load(1, "udl", 1, 0, 8, -12)])
    s, c = summary(fr), "ka-propped-cantilever"
    for sym, k, v, t in [("R2_y", 36, fr.Rn[2][1], 1e-9), ("R1_y", 60, fr.Rn[1][1], 1e-9), ("M_min", -96, s["M_min"], 1e-9),
                         ("M_max", 54, s["M_max"], 1e-9), ("x_Mmax", 5, s["x_Mmax"], 1e-6)]:
        row(c, sym, k, v, t)
    # 4. pinned portal, 30 kN lateral at node 2
    fr = build(210, [node(1, 0, 0, "pin"), node(2, 0, 4), node(3, 6, 4), node(4, 6, 0, "pin")],
               [(1, 1, 2, 100, 10000, None, False, False), (2, 2, 3, 1e6, 10000, None, False, False), (3, 3, 4, 100, 10000, None, False, False)],
               [load(1, "nodal", 2, Fx=30)])
    s, c = summary(fr), "ka-portal-sway"
    for sym, k, v, t in [("R1_x", -15, fr.Rn[1][0], 1e-5), ("R4_x", -15, fr.Rn[4][0], 1e-5), ("R1_y", -20, fr.Rn[1][1], 1e-9),
                         ("R4_y", 20, fr.Rn[4][1], 1e-9), ("M_max", 60, s["M_max"], 1e-5),
                         ("N_e1", 20, fr.members[1]["N"][0], 1e-9)]:
        row(c, sym, k, v, t)
    # 5. two-span beam, hinge at the left end of span 2, 40 kN at 2 m on span 2
    fr = build(210, [node(1, 0, 0, "fixed"), node(2, 5, 0, "roller (vertical)"), node(3, 9, 0, "roller (vertical)")],
               [(1, 1, 2, 80, 5000, None, False, False), (2, 2, 3, 80, 5000, None, True, False)],
               [load(1, "point", 2, a=2, Fy=-40)])
    s, c = summary(fr), "ka-two-span-release"
    for sym, k, v, t in [("R3_y", 20, fr.Rn[3][1], 1e-9), ("R2_y", 20, fr.Rn[2][1], 1e-9), ("R1_y", 0, fr.Rn[1][1], 1e-9),
                         ("R1_m", 0, fr.Rn[1][2], 1e-9), ("M_max", 40, s["M_max"], 1e-9)]:
        row(c, sym, k, v, t)
    # self-check: the integrated deflection must reach the solved far-end value
    worst = 0.0
    for fr_ in (fr,):
        for m in fr_.members.values():
            worst = max(worst, abs(m["y"][-1] - m["y_far_solved"]))
    print(f"{'(self-check)':<26}{'y_end':<10} integrated minus solved far-end deflection, worst abs {worst:.2e} m  {'PASS' if worst < 1e-6 else 'FAIL'}")
    FAIL[0] += 0 if worst < 1e-6 else 1


# ------------------------------------------------------------ extra checks against the C# product
# Product values were read from the probe tool (`Probe run struct.frame-2d`) on release/v0.26.0 code.
# kN, m, kN*m, mm.  The same models are solved here with the independent solver above.
# A mismatch prints DIFF and does not change the exit status (KAT cases only).  Finding numbers are shown on the line.
MODELS = {'F1': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0),
                  2: (5, 0, 'roller (vertical)', 0.0, 0.0, 0.0),
                  3: (12, 0, 'roller (vertical)', 0.0, 0.0, 0.0),
                  4: (18, 0, 'roller (vertical)', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 120, 18000, None, False, False), (2, 2, 3, 120, 18000, None, False, False),
                  (3, 3, 4, 120, 18000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'udl',
                   'target': 1,
                   'a': 0,
                   'b': 0,
                   'w1': -18,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'udl',
                   'target': 2,
                   'a': 2,
                   'b': 7,
                   'w1': -25,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'point',
                   'target': 3,
                   'a': 2.5,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': -80,
                   'M': 0,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 0.0,
                    'R1_y': 32.27774327,
                    'R1_m': 0.0,
                    'R2_x': 0.0,
                    'R2_y': 97.87502218,
                    'R2_m': 0.0,
                    'R3_x': 0.0,
                    'R3_y': 147.3542221,
                    'R3_m': 0.0,
                    'R4_x': 0.0,
                    'R4_y': 17.49301242,
                    'R4_m': 0.0,
                    'M_max': 61.22554348,
                    'x_Mmax': 2.5,
                    'M_min': -95.04192547,
                    'V_max': 84.84723455,
                    'N_max': 0.0,
                    'N_min': 0.0,
                    'd_max': 3.994780368}},
 'F2': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0), 2: (6, 3, 'roller (vertical)', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 60, 6000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'udl',
                   'target': 1,
                   'a': 0,
                   'b': 0,
                   'w1': 12,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 0,
                   'gravity': True}],
        'n_div': 20,
        'product': {'R1_x': 0.0,
                    'R1_y': 40.24922359,
                    'R1_m': 7.275957614e-15,
                    'R2_x': 0.0,
                    'R2_y': 40.24922359,
                    'R2_m': 0.0,
                    'M_max': 60.37383539,
                    'x_Mmax': 3.354101966,
                    'M_min': 0.0,
                    'V_max': 36.0,
                    'N_max': 18.0,
                    'N_min': -18.0,
                    'd_max': 22.46050424}},
 'F3': {'E': 200,
        'nodes': {1: (0, 0, 'fixed', 0.0, 0.0, 0.0),
                  2: (0, 4, 'free', 0.0, 0.0, 0.0),
                  3: (7, 4, 'free', 0.0, 0.0, 0.0),
                  4: (7, 0, 'fixed', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 90, 12000, None, False, False), (2, 2, 3, 110, 25000, None, False, False),
                  (3, 3, 4, 90, 12000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'udl',
                   'target': 2,
                   'a': 0,
                   'b': 0,
                   'w1': -20,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'nodal',
                   'target': 2,
                   'a': 0,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 25,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'udl',
                   'target': 1,
                   'a': 1,
                   'b': 3,
                   'w1': 6,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 16.23332711,
                    'R1_y': 64.55407178,
                    'R1_m': -9.982328893,
                    'R4_x': -29.23332711,
                    'R4_y': 75.44592822,
                    'R4_m': 47.86083134,
                    'M_max': 73.22972503,
                    'x_Mmax': 3.227703589,
                    'M_min': -69.0724771,
                    'V_max': 75.44592822,
                    'N_max': -29.23332711,
                    'N_min': -75.44592822,
                    'd_max': 6.5508022}},
 'F4': {'E': 210,
        'nodes': {1: (0, 0, 'fixed', 0.0, 0.0, 0.0),
                  2: (6, 0, 'spring', 0, 25000, 8000),
                  3: (9, 0, 'free', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 80, 9000, None, False, False), (2, 2, 3, 80, 9000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'trapezoid',
                   'target': 1,
                   'a': 1,
                   'b': 5,
                   'w1': -10,
                   'w2': -30,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'moment',
                   'target': 2,
                   'a': 1.5,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 40,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'nodal',
                   'target': 3,
                   'a': 0,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': -15,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'udl',
                   'target': 2,
                   'a': 0,
                   'b': 0,
                   'w1': -8,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 0.0,
                    'R1_y': 38.05963339,
                    'R1_m': 57.88913254,
                    'R2_x': 0.0,
                    'R2_y': 80.94036661,
                    'R2_m': -1.864665556,
                    'M_max': 31.19625701,
                    'x_Mmax': 3.384501494,
                    'M_min': -57.88913254,
                    'V_max': 41.94036661,
                    'N_max': 0.0,
                    'N_min': 0.0,
                    'd_max': 6.824079367}},
 'F6': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0),
                  2: (0, 4, 'free', 0.0, 0.0, 0.0),
                  3: (5, 4, 'free', 0.0, 0.0, 0.0),
                  4: (5, 0, 'pin', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 100, 9000, None, False, False), (2, 2, 3, 100, 9000, None, True, False),
                  (3, 3, 4, 100, 9000, None, False, True)],
        'loads': [{'case': 1,
                   'kind': 'point',
                   'target': 2,
                   'a': 2.5,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': -60,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'nodal',
                   'target': 2,
                   'a': 0,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 10,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 1.818989404e-15,
                    'R1_y': 22.0,
                    'R1_m': -3.637978807e-15,
                    'R4_x': -10.0,
                    'R4_y': 38.0,
                    'R4_m': 0.0,
                    'M_max': 55.0,
                    'x_Mmax': 2.5,
                    'M_min': -40.0,
                    'V_max': 38.0,
                    'N_max': -10.0,
                    'N_min': -38.0,
                    'd_max': 6.092103959}},
 'F7': {'E': 210,
        'nodes': {1: (0, 0, 'fixed', 0.0, 0.0, 0.0), 2: (0, 5, 'free', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 100, 9000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'udl',
                   'target': 1,
                   'a': 0,
                   'b': 0,
                   'w1': 8,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'nodal',
                   'target': 2,
                   'a': 0,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': -100,
                   'M': 0,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 40.0,
                    'R1_y': 100.0,
                    'R1_m': -100.0,
                    'M_max': 100.0,
                    'x_Mmax': 0.0,
                    'M_min': 0.0,
                    'V_max': 40.0,
                    'N_max': -100.0,
                    'N_min': -100.0,
                    'd_max': 33.06878307}},
 'F8': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0),
                  2: (4, 3, 'free', 0.0, 0.0, 0.0),
                  3: (9, 0, 'roller (vertical)', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 60, 5000, 70, False, False), (2, 2, 3, 60, 5000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'point',
                   'target': 1,
                   'a': 2,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 5,
                   'Fy': -20,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'nodal',
                   'target': 2,
                   'a': 0,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': -30,
                   'M': 0,
                   'gravity': False},
                  {'case': 1,
                   'kind': 'moment',
                   'target': 2,
                   'a': 2.5,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': -12,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': -5.0,
                    'R1_y': 31.11111111,
                    'R1_m': -5.456968211e-14,
                    'R3_x': -1.835360308e-12,
                    'R3_y': 18.88888889,
                    'R3_m': 2.091837814e-14,
                    'M_max': 82.44444444,
                    'x_Mmax': 0.0,
                    'M_min': 0.0,
                    'V_max': 27.88888889,
                    'N_max': -6.666666667,
                    'N_min': -14.66666667,
                    'd_max': 175.7758196}},
 'C1': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0), 2: (6, 0, 'roller (vertical)', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 100, 8000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'moment',
                   'target': 1,
                   'a': 3,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 30,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 0.0,
                    'R1_y': 5.0,
                    'R1_m': 0.0,
                    'R2_x': 0.0,
                    'R2_y': -5.0,
                    'R2_m': 0.0,
                    'M_max': 13.5,
                    'x_Mmax': 2.7,
                    'M_min': -15.0,
                    'V_max': 5.0,
                    'N_max': 0.0,
                    'N_min': 0.0,
                    'd_max': 0.5154913118}},
 'C2': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0), 2: (6, 0, 'roller (vertical)', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 100, 8000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'moment',
                   'target': 1,
                   'a': 3,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': -30,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 0.0,
                    'R1_y': -5.0,
                    'R1_m': 0.0,
                    'R2_x': 0.0,
                    'R2_y': 5.0,
                    'R2_m': 0.0,
                    'M_max': 15.0,
                    'x_Mmax': 3.0,
                    'M_min': -13.5,
                    'V_max': 5.0,
                    'N_max': 0.0,
                    'N_min': 0.0,
                    'd_max': 0.5154913118}},
 'C3': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0), 2: (6, 0, 'roller (vertical)', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 100, 8000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'moment',
                   'target': 1,
                   'a': 3.3,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 30,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 0.0,
                    'R1_y': 5.0,
                    'R1_m': 0.0,
                    'R2_x': 0.0,
                    'R2_y': -5.0,
                    'R2_m': 0.0,
                    'M_max': 15.0,
                    'x_Mmax': 3.0,
                    'M_min': -13.5,
                    'V_max': 5.0,
                    'N_max': 0.0,
                    'N_min': 0.0,
                    'd_max': 1.014076757}},
 'C4': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0), 2: (6, 0, 'roller (vertical)', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 100, 8000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'moment',
                   'target': 1,
                   'a': 6,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 30,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 0.0,
                    'R1_y': 5.0,
                    'R1_m': 0.0,
                    'R2_x': 0.0,
                    'R2_y': -5.0,
                    'R2_m': 0.0,
                    'M_max': 28.5,
                    'x_Mmax': 5.7,
                    'M_min': 0.0,
                    'V_max': 5.0,
                    'N_max': 0.0,
                    'N_min': 0.0,
                    'd_max': 4.123930494}},
 'C5': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0), 2: (6, 0, 'roller (vertical)', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 100, 8000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'moment',
                   'target': 1,
                   'a': 3,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 30,
                   'gravity': False}],
        'n_div': 4,
        'product': {'R1_x': 0.0,
                    'R1_y': 5.0,
                    'R1_m': 0.0,
                    'R2_x': 0.0,
                    'R2_y': -5.0,
                    'R2_m': 0.0,
                    'M_max': 7.5,
                    'x_Mmax': 1.5,
                    'M_min': -15.0,
                    'V_max': 5.0,
                    'N_max': 0.0,
                    'N_min': 0.0,
                    'd_max': 0.5154913118}},
 'X1': {'E': 210,
        'nodes': {1: (0, 0, 'pin', 0.0, 0.0, 0.0), 2: (6, 0, 'roller (vertical)', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 1000000.0, 1, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'udl',
                   'target': 1,
                   'a': 0,
                   'b': 0,
                   'w1': -10,
                   'w2': 0,
                   'Fx': 0,
                   'Fy': 0,
                   'M': 0,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': 0.0,
                    'R1_y': 30.0,
                    'R1_m': 3.637978807e-15,
                    'R2_x': 0.0,
                    'R2_y': 30.0,
                    'R2_m': -3.637978807e-15,
                    'M_max': 45.0,
                    'x_Mmax': 3.0,
                    'M_min': 0.0,
                    'V_max': 30.0,
                    'N_max': 0.0,
                    'N_min': 0.0,
                    'd_max': 80357.14286}},
 'X7': {'E': 210,
        'nodes': {1: (0, 0, 'fixed', 0.0, 0.0, 0.0), 2: (6, 0, 'fixed', 0.0, 0.0, 0.0)},
        'elems': [(1, 1, 2, 100, 8000, None, False, False)],
        'loads': [{'case': 1,
                   'kind': 'point',
                   'target': 1,
                   'a': 2,
                   'b': 0,
                   'w1': 0,
                   'w2': 0,
                   'Fx': 100,
                   'Fy': -50,
                   'M': 0,
                   'gravity': False}],
        'n_div': 20,
        'product': {'R1_x': -66.66666667,
                    'R1_y': 37.03703704,
                    'R1_m': 44.44444444,
                    'R2_x': -33.33333333,
                    'R2_y': 12.96296296,
                    'R2_m': -22.22222222,
                    'M_max': 29.62962963,
                    'x_Mmax': 2.0,
                    'M_min': -44.44444444,
                    'V_max': 37.03703704,
                    'N_max': 66.66666667,
                    'N_min': -33.33333333,
                    'd_max': 2.591512796}}}

BIG_PRODUCT = {'M_max': 137.4012351,
 'x_Mmax': 0.2166798708,
 'M_min': -153.7062207,
 'V_max': 87.53312207,
 'N_max': 0.0,
 'N_min': 0.0,
 'd_max': 27.24162521}

COMB_LOADS = [{'case': 1,
  'kind': 'udl',
  'target': 1,
  'a': 0,
  'b': 0,
  'w1': -18,
  'w2': 0,
  'Fx': 0,
  'Fy': 0,
  'M': 0,
  'gravity': False},
 {'case': 1,
  'kind': 'udl',
  'target': 2,
  'a': 2,
  'b': 7,
  'w1': -25,
  'w2': 0,
  'Fx': 0,
  'Fy': 0,
  'M': 0,
  'gravity': False},
 {'case': 2,
  'kind': 'point',
  'target': 3,
  'a': 2.5,
  'b': 0,
  'w1': 0,
  'w2': 0,
  'Fx': 0,
  'Fy': -80,
  'M': 0,
  'gravity': False},
 {'case': 2,
  'kind': 'udl',
  'target': 1,
  'a': 0,
  'b': 0,
  'w1': -6,
  'w2': 0,
  'Fx': 0,
  'Fy': 0,
  'M': 0,
  'gravity': False},
 {'case': 3,
  'kind': 'udl',
  'target': 2,
  'a': 0,
  'b': 0,
  'w1': 12,
  'w2': 0,
  'Fx': 0,
  'Fy': 0,
  'M': 0,
  'gravity': False}]
COMB_TERMS = {'ULS1': [(1, 1.35), (2, 1.5)], 'ULS2': [(1, 1.0), (3, 1.5)], 'SLS': [(1, 1.0), (2, 1.0)]}
COMB_PRODUCT = {'M_max_env': 98.10394022,
 'M_min_env': -131.8218168,
 'V_max_env': 113.5183008,
 'd_max_env': 5.893077701,
 'M_max': 43.95626857,
 'M_min': -32.41656338,
 'V_max': 50.22028571,
 'd_max': 2.805333519}

EXTRA_DIFFS = []
FINDING = {"C1": "P1-struct.frame-2d-01", "C2": "P1-struct.frame-2d-01", "C3": "P1-struct.frame-2d-01",
           "C4": "P1-struct.frame-2d-01", "C5": "P1-struct.frame-2d-01"}


def xrow(probe, sym, prod, ind, tol, note=""):
    scale = max(1.0, abs(ind))
    rel = abs(prod - ind) / scale
    ok = rel <= max(tol, 1e-6)
    if not ok:
        EXTRA_DIFFS.append((probe, sym))
    print(f"{probe:<26}{sym:<10}{prod:>14.8g}{ind:>14.8g}{rel:>11.2e}{tol:>10.1e}  {'PASS' if ok else 'DIFF'} {note}")


def compare_model(tag, fr_, prod, tol=1e-6):
    s = summary(fr_)
    note = FINDING.get(tag, "")
    for key, pv in prod.items():
        if key[0] == "R" and key[1].isdigit():
            nid, comp = int(key[1:].split("_")[0]), "xym".index(key[-1])
            xrow(tag, key, pv, float(fr_.Rn[nid][comp]), tol)
        elif key == "x_Mmax":
            # a position: accept any member that ties for the maximum, to 3 mm
            ties = [x for (mm, x) in s["x_Mmax_all"] if mm >= s["M_max"] - 1e-6 * max(1.0, abs(s["M_max"]))]
            best = min(ties, key=lambda x: abs(x - pv))
            xrow(tag, key, pv, float(best), 3e-3 / max(1.0, abs(best)), note)
        elif key in s:
            xrow(tag, key, pv, float(s[key]), 1e-5 if key == "d_max" else tol, note if key in ("M_max", "M_min") else "")


def extras():
    print("--- extra checks: product (hard-coded) against the independent solver ---")
    for tag, m in MODELS.items():
        fr_ = build(m["E"], m["nodes"], m["elems"], m["loads"])
        compare_model(tag, fr_, m["product"], 1e-6)
    # 120-member continuous beam (summary-only output in the product)
    nodes = {i + 1: (i * 0.5, 0.0, "pin" if i == 0 else ("roller (vertical)" if i % 20 == 0 else "free"), 0, 0, 0) for i in range(121)}
    elems = [(i + 1, i + 1, i + 2, 100, 20000, None, False, False) for i in range(120)]
    loads = [load(1, "udl", i + 1, 0, 0, -10 - (i % 7)) for i in range(120)] + [load(1, "point", 5, a=0.2, Fy=-30)]
    compare_model("B120 120 members", build(210, nodes, elems, loads), BIG_PRODUCT, 1e-6)
    # load combinations and envelope: solve each combination with scaled loads
    base = MODELS["F1"]
    res = {}
    for name, terms in COMB_TERMS.items():
        sc = []
        for cs, f in terms:
            for l in COMB_LOADS:
                if l["case"] == cs:
                    l2 = dict(l)
                    for k in ("w1", "w2", "Fx", "Fy", "M"):
                        l2[k] = l[k] * f
                    sc.append(l2)
        res[name] = summary(build(210, base["nodes"], base["elems"], sc))
    rep = res["ULS2"]
    for k in ("M_max", "M_min", "V_max", "d_max"):
        xrow("COMB report ULS2", k, COMB_PRODUCT[k], float(rep[k]), 1e-6)
    xrow("COMB envelope", "M_max_env", COMB_PRODUCT["M_max_env"], max(r["M_max"] for r in res.values()), 1e-6)
    xrow("COMB envelope", "M_min_env", COMB_PRODUCT["M_min_env"], min(r["M_min"] for r in res.values()), 1e-6)
    xrow("COMB envelope", "V_max_env", COMB_PRODUCT["V_max_env"], max(r["V_max"] for r in res.values()), 1e-6)
    xrow("COMB envelope", "d_max_env", COMB_PRODUCT["d_max_env"], max(r["d_max"] for r in res.values()), 1e-5)
    print(f"extra checks with DIFF: {len(EXTRA_DIFFS)} {EXTRA_DIFFS}")


if __name__ == "__main__":
    kats()
    extras()
    print("RESULT:", "ALL PASS" if FAIL[0] == 0 else f"{FAIL[0]} FAIL")
    sys.exit(0 if FAIL[0] == 0 else 1)
