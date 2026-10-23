namespace Tempest.Calc.Structural.Frame;

/// <summary>The internal state of one member at one station (SI, local axes): x from end I, axial N (tension +), shear V, bending moment M (sagging +), transverse deflection v and axial displacement u.</summary>
public readonly record struct MemberState(double X, double N, double V, double M, double U, double Vy);

/// <summary>The solved member: end displacements and end forces in local axes, and a function of x for everything in between.</summary>
public sealed class MemberResult
{
    private readonly double[] _u;
    private readonly double[] _q;
    private readonly MemberLoads _loads;
    private readonly double _ea;
    private readonly double _ei;

    internal MemberResult(FrameElement element, double length, double[] localDisplacements, double[] endForces, MemberLoads loads)
    {
        Element = element;
        Length = length;
        _u = localDisplacements;
        _q = endForces;
        _loads = loads;
        _ea = element.E * element.A;
        _ei = element.E * element.Iz;
    }

    public FrameElement Element { get; }

    public double Length { get; }

    /// <summary>Forces the nodes exert on the member, local (F_xi, F_yi, M_i, F_xj, F_yj, M_j).</summary>
    public IReadOnlyList<double> EndForces => _q;

    /// <summary>Local end displacements (u_i, v_i, θ_i, u_j, v_j, θ_j).</summary>
    public IReadOnlyList<double> EndDisplacements => _u;

    public MemberState At(double x)
    {
        var fxi = _q[0];
        var fyi = _q[1];
        var mi = _q[2];
        var n = -fxi - _loads.Axial(x);
        var v = fyi + _loads.Shear(x);
        var m = fyi * x + _loads.Moment(x) - mi;
        var vy = _u[1] + _u[2] * x + (fyi * x * x * x / 6 - mi * x * x / 2 + _loads.Deflection(x)) / _ei;
        var u = _u[0] + (-fxi * x - _loads.AxialDisplacement(x)) / _ea;
        return new MemberState(x, n, v, m, u, vy);
    }

    /// <summary>Slope dv/dx at x.</summary>
    public double Slope(double x) => _u[2] + (_q[1] * x * x / 2 - _q[2] * x + _loads.Slope(x)) / _ei;

    /// <summary>Stations along the member: n uniform intervals plus every load breakpoint, sorted and distinct.</summary>
    public IReadOnlyList<double> Stations(int divisions)
    {
        var set = new SortedSet<double>();

        for (var i = 0; i <= divisions; i++)
            set.Add(Length * i / divisions);

        foreach (var b in _loads.Breakpoints)
        {
            if (b > 0 && b < Length)
                set.Add(b);
        }

        return [.. set];
    }

    /// <summary>The extreme moment and its position: stations plus the points where the shear changes sign (dM/dx = V = 0) found by bisection.</summary>
    public (double Max, double XMax, double Min, double XMin) MomentExtremes(int divisions)
    {
        var stations = Stations(divisions);
        var candidates = new List<double>(stations);

        for (var i = 0; i + 1 < stations.Count; i++)
        {
            var a = stations[i];
            var b = stations[i + 1];
            var va = At(a + 1e-9 * Length).V;
            var vb = At(b - 1e-9 * Length).V;

            if (va * vb < 0)
            {
                for (var k = 0; k < 50; k++)
                {
                    var m = (a + b) / 2;
                    if (At(m).V * va < 0) b = m; else a = m;
                }

                candidates.Add((a + b) / 2);
            }
        }

        return Extremes(candidates, x => At(x).M);
    }

    /// <summary>The extreme transverse deflection and its position (stations plus zero-slope points).</summary>
    public (double Max, double XMax, double Min, double XMin) DeflectionExtremes(int divisions)
    {
        var stations = Stations(divisions);
        var candidates = new List<double>(stations);

        for (var i = 0; i + 1 < stations.Count; i++)
        {
            var a = stations[i];
            var b = stations[i + 1];
            var sa = Slope(a);
            var sb = Slope(b);

            if (sa * sb < 0)
            {
                for (var k = 0; k < 50; k++)
                {
                    var m = (a + b) / 2;
                    if (Slope(m) * sa < 0) b = m; else a = m;
                }

                candidates.Add((a + b) / 2);
            }
        }

        return Extremes(candidates, x => At(x).Vy);
    }

    private static (double Max, double XMax, double Min, double XMin) Extremes(IReadOnlyList<double> xs, Func<double, double> f)
    {
        double max = double.NegativeInfinity, min = double.PositiveInfinity, xMax = 0, xMin = 0;

        foreach (var x in xs)
        {
            var v = f(x);
            if (v > max) { max = v; xMax = x; }
            if (v < min) { min = v; xMin = x; }
        }

        return (max, xMax, min, xMin);
    }
}

/// <summary>Everything one combination produced.</summary>
public sealed record FrameResult(FrameCombination Combination, IReadOnlyDictionary<int, (double Ux, double Uy, double Rz)> Displacements, IReadOnlyDictionary<int, (double Rx, double Ry, double Mz)> Reactions, IReadOnlyList<MemberResult> Members);

/// <summary>
/// Direct-stiffness analysis of a 2D frame: three degrees of freedom per
/// node, Euler–Bernoulli members with optional end releases (static
/// condensation), pin / roller / fixed / spring supports, loads by
/// case and combination. The reduced stiffness matrix is stored in
/// symmetric band form after a reverse Cuthill–McKee ordering and solved
/// by LDLᵀ; a non-positive pivot means a mechanism and is reported as
/// such. Every size is capped by <see cref="FrameLimits"/>.
/// </summary>
public static class FrameSolver
{
    public static IReadOnlyList<FrameResult> Solve(FrameModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var order = ReverseCuthillMcKee(model);
        var nodeIndex = new Dictionary<int, int>();

        for (var i = 0; i < order.Count; i++)
            nodeIndex[order[i]] = i;

        // Element geometry and local stiffness, once.
        var geometry = model.Elements.Select(e =>
        {
            var (dx, dy) = model.Delta(e);
            var l = Math.Sqrt(dx * dx + dy * dy);
            return (Element: e, L: l, C: dx / l, S: dy / l, K: LocalStiffness(e, l));
        }).ToList();

        // Degree-of-freedom map: -1 for restrained.
        var n = model.Nodes.Count;
        var dof = new int[3 * n];
        var free = 0;

        foreach (var node in model.Nodes)
        {
            var i = nodeIndex[node.Id];
            dof[3 * i] = node.FixX ? -1 : free++;
            dof[3 * i + 1] = node.FixY ? -1 : free++;
            dof[3 * i + 2] = node.FixR ? -1 : free++;
        }

        // Bandwidth.
        var half = 0;

        foreach (var g in geometry)
        {
            var a = nodeIndex[g.Element.I];
            var b = nodeIndex[g.Element.J];
            half = Math.Max(half, 3 * Math.Abs(a - b) + 2);
        }

        if ((long)free * (half + 1) > FrameLimits.MaxBandedEntries)
            throw new FrameModelException($"the stiffness matrix would need {(long)free * (half + 1):N0} entries; renumber the nodes so connected nodes are close, or reduce the model.");

        var band = new BandedSymmetric(free, half);

        foreach (var g in geometry)
        {
            var (kc, _) = Condense(g.K, g.Element, null);
            var kg = ToGlobal(kc, g.C, g.S);
            var map = DofMap(g.Element, nodeIndex, dof);

            for (var r = 0; r < 6; r++)
            {
                if (map[r] < 0) continue;

                for (var c = 0; c < 6; c++)
                {
                    if (map[c] < 0 || map[c] < map[r]) continue;
                    band.Add(map[r], map[c], kg[r, c]);
                }
            }
        }

        foreach (var node in model.Nodes)
        {
            var i = nodeIndex[node.Id];
            if (node.Kx > 0 && dof[3 * i] >= 0) band.Add(dof[3 * i], dof[3 * i], node.Kx);
            if (node.Ky > 0 && dof[3 * i + 1] >= 0) band.Add(dof[3 * i + 1], dof[3 * i + 1], node.Ky);
            if (node.Kr > 0 && dof[3 * i + 2] >= 0) band.Add(dof[3 * i + 2], dof[3 * i + 2], node.Kr);
        }

        // Unconnected or fully released rotational freedoms carry no stiffness: pin them to zero so the matrix stays regular.
        var inactive = new bool[free];

        for (var d = 0; d < free; d++)
        {
            if (band.Diagonal(d) == 0)
            {
                inactive[d] = true;
                band.Add(d, d, 1);
            }
        }

        band.Factor();

        var results = new List<FrameResult>();

        foreach (var combination in model.Combinations)
        {
            var memberLoads = geometry.ToDictionary(g => g.Element.Id, g => new MemberLoads(g.L));
            var nodal = new double[3 * n];

            foreach (var (caseId, factor) in combination.Terms)
            {
                foreach (var load in model.Loads.Where(l => l.Case == caseId))
                    Apply(load, factor, model, geometry, nodeIndex, memberLoads, nodal);
            }

            var f = new double[free];
            var equivalent = new Dictionary<int, double[]>();

            foreach (var g in geometry)
            {
                var feq = memberLoads[g.Element.Id].FixedEndForces().Select(v => -v).ToArray();
                var (_, feqc) = Condense(g.K, g.Element, feq);
                equivalent[g.Element.Id] = feq;
                var fg = ToGlobalVector(feqc, g.C, g.S);
                var map = DofMap(g.Element, nodeIndex, dof);

                for (var r = 0; r < 6; r++)
                {
                    if (map[r] >= 0) f[map[r]] += fg[r];
                }
            }

            for (var d = 0; d < 3 * n; d++)
            {
                if (dof[d] >= 0) f[dof[d]] += nodal[d];
            }

            for (var d = 0; d < free; d++)
            {
                if (inactive[d] && Math.Abs(f[d]) > 0)
                    throw new FrameModelException("a load acts on a freedom with no stiffness (a released or unconnected rotation); add a member or remove the release.");
            }

            var u = band.Solve(f);
            var displacements = new Dictionary<int, (double, double, double)>();

            foreach (var node in model.Nodes)
            {
                var i = nodeIndex[node.Id];
                displacements[node.Id] = (Value(u, dof[3 * i]), Value(u, dof[3 * i + 1]), Value(u, dof[3 * i + 2]));
            }

            var members = new List<MemberResult>();
            var nodeForces = new double[3 * n];

            foreach (var g in geometry)
            {
                var map = DofMap(g.Element, nodeIndex, dof);
                var ug = new double[6];

                for (var r = 0; r < 6; r++)
                    ug[r] = Value(u, map[r]);

                var ul = ToLocalVector(ug, g.C, g.S);
                Recover(g.K, g.Element, equivalent[g.Element.Id], ul);
                var q = new double[6];

                for (var r = 0; r < 6; r++)
                {
                    double sum = 0;
                    for (var c = 0; c < 6; c++) sum += g.K[r, c] * ul[c];
                    q[r] = sum - equivalent[g.Element.Id][r];
                }

                var qg = ToGlobalVector(q, g.C, g.S);
                var ni = nodeIndex[g.Element.I];
                var nj = nodeIndex[g.Element.J];

                for (var r = 0; r < 3; r++)
                {
                    nodeForces[3 * ni + r] += qg[r];
                    nodeForces[3 * nj + r] += qg[3 + r];
                }

                members.Add(new MemberResult(g.Element, g.L, ul, q, memberLoads[g.Element.Id]));
            }

            var reactions = new Dictionary<int, (double, double, double)>();

            foreach (var node in model.Nodes)
            {
                if (!node.IsSupported) continue;
                var i = nodeIndex[node.Id];
                reactions[node.Id] = (nodeForces[3 * i] - nodal[3 * i], nodeForces[3 * i + 1] - nodal[3 * i + 1], nodeForces[3 * i + 2] - nodal[3 * i + 2]);
            }

            results.Add(new FrameResult(combination, displacements, reactions, members));
        }

        return results;
    }

    private static double Value(double[] u, int d) => d < 0 ? 0 : u[d];

    private static int[] DofMap(FrameElement e, Dictionary<int, int> nodeIndex, int[] dof)
    {
        var i = nodeIndex[e.I];
        var j = nodeIndex[e.J];
        return [dof[3 * i], dof[3 * i + 1], dof[3 * i + 2], dof[3 * j], dof[3 * j + 1], dof[3 * j + 2]];
    }

    private static void Apply(FrameLoad load, double factor, FrameModel model, List<(FrameElement Element, double L, double C, double S, double[,] K)> geometry, Dictionary<int, int> nodeIndex, Dictionary<int, MemberLoads> memberLoads, double[] nodal)
    {
        if (load.Kind == FrameLoadKind.Nodal)
        {
            var i = nodeIndex[load.Target];
            nodal[3 * i] += factor * load.Fx;
            nodal[3 * i + 1] += factor * load.Fy;
            nodal[3 * i + 2] += factor * load.M;
            return;
        }

        var g = geometry.First(x => x.Element.Id == load.Target);
        var m = memberLoads[load.Target];
        var a = Math.Clamp(load.A, 0, g.L);
        var b = Math.Clamp(load.B, 0, g.L);

        switch (load.Kind)
        {
            case FrameLoadKind.Point:
            {
                // Global force → local components.
                var px = factor * (g.C * load.Fx + g.S * load.Fy);
                var py = factor * (-g.S * load.Fx + g.C * load.Fy);
                if (py != 0) m.AddPoint(a, py);
                if (px != 0) m.AddAxialPoint(a, px);
                break;
            }

            case FrameLoadKind.Moment:
                m.AddCouple(a, factor * load.M);
                break;

            case FrameLoadKind.Udl:
            case FrameLoadKind.Trapezoid:
            {
                var w1 = factor * load.W1;
                var w2 = factor * (load.Kind == FrameLoadKind.Udl ? load.W1 : load.W2);

                if (b <= a)
                {
                    a = 0;
                    b = g.L;
                }

                if (load.Global)
                {
                    // Gravity: W per metre of member in global −y → local y component −W cos α, local x component −W sin α.
                    m.AddTransverse(a, b, -w1 * g.C, -w2 * g.C);
                    if (g.S != 0) m.AddAxial(a, b, -w1 * g.S, -w2 * g.S);
                }
                else
                {
                    m.AddTransverse(a, b, w1, w2);
                }

                break;
            }
        }
    }

    private static double[,] LocalStiffness(FrameElement e, double l)
    {
        var ea = e.E * e.A / l;
        var ei = e.E * e.Iz;
        var c2 = 12 * ei / (l * l * l);
        var c3 = 6 * ei / (l * l);
        var c4 = 4 * ei / l;
        var c5 = 2 * ei / l;
        return new[,]
        {
            { ea, 0, 0, -ea, 0, 0 },
            { 0, c2, c3, 0, -c2, c3 },
            { 0, c3, c4, 0, -c3, c5 },
            { -ea, 0, 0, ea, 0, 0 },
            { 0, -c2, -c3, 0, c2, -c3 },
            { 0, c3, c5, 0, -c3, c4 },
        };
    }

    private static int[] Released(FrameElement e) => (e.ReleaseI, e.ReleaseJ) switch
    {
        (true, true) => [2, 5],
        (true, false) => [2],
        (false, true) => [5],
        _ => [],
    };

    /// <summary>Static condensation of the released rotations: k_cc − k_cr k_rr⁻¹ k_rc, f_c − k_cr k_rr⁻¹ f_r; released rows and columns come back as zeros.</summary>
    private static (double[,] K, double[] F) Condense(double[,] k, FrameElement e, double[]? f)
    {
        var released = Released(e);
        var kc = (double[,])k.Clone();
        var fc = f is null ? new double[6] : (double[])f.Clone();

        if (released.Length == 0)
            return (kc, fc);

        var krr = Sub(k, released, released);
        var inv = Invert(krr);
        var kept = Enumerable.Range(0, 6).Except(released).ToArray();

        foreach (var r in kept)
        {
            foreach (var c in kept)
            {
                double s = 0;
                for (var p = 0; p < released.Length; p++)
                    for (var q = 0; q < released.Length; q++)
                        s += k[r, released[p]] * inv[p, q] * k[released[q], c];
                kc[r, c] -= s;
            }

            if (f is not null)
            {
                double s = 0;
                for (var p = 0; p < released.Length; p++)
                    for (var q = 0; q < released.Length; q++)
                        s += k[r, released[p]] * inv[p, q] * f[released[q]];
                fc[r] -= s;
            }
        }

        foreach (var r in released)
        {
            for (var c = 0; c < 6; c++) { kc[r, c] = 0; kc[c, r] = 0; }
            fc[r] = 0;
        }

        return (kc, fc);
    }

    /// <summary>Fills in the released rotations from q_r = 0: u_r = k_rr⁻¹ (f_r − k_rc u_c).</summary>
    private static void Recover(double[,] k, FrameElement e, double[] f, double[] ul)
    {
        var released = Released(e);

        if (released.Length == 0)
            return;

        var kept = Enumerable.Range(0, 6).Except(released).ToArray();
        var inv = Invert(Sub(k, released, released));
        var rhs = new double[released.Length];

        for (var p = 0; p < released.Length; p++)
        {
            rhs[p] = f[released[p]];
            foreach (var c in kept) rhs[p] -= k[released[p], c] * ul[c];
        }

        for (var p = 0; p < released.Length; p++)
        {
            double s = 0;
            for (var q = 0; q < released.Length; q++) s += inv[p, q] * rhs[q];
            ul[released[p]] = s;
        }
    }

    private static double[,] Sub(double[,] k, int[] rows, int[] cols)
    {
        var m = new double[rows.Length, cols.Length];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < cols.Length; c++)
                m[r, c] = k[rows[r], cols[c]];
        return m;
    }

    private static double[,] Invert(double[,] m)
    {
        var n = m.GetLength(0);

        if (n == 1)
            return new[,] { { 1 / m[0, 0] } };

        var det = m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0];
        return new[,] { { m[1, 1] / det, -m[0, 1] / det }, { -m[1, 0] / det, m[0, 0] / det } };
    }

    private static double[,] ToGlobal(double[,] kl, double c, double s)
    {
        // k_g = Tᵀ k_l T with T = blockdiag(R, R), R = [[c, s, 0], [−s, c, 0], [0, 0, 1]].
        var t = new double[6, 6];
        t[0, 0] = c; t[0, 1] = s; t[1, 0] = -s; t[1, 1] = c; t[2, 2] = 1;
        t[3, 3] = c; t[3, 4] = s; t[4, 3] = -s; t[4, 4] = c; t[5, 5] = 1;
        var kt = new double[6, 6];

        for (var r = 0; r < 6; r++)
            for (var col = 0; col < 6; col++)
            {
                double sum = 0;
                for (var p = 0; p < 6; p++) sum += kl[r, p] * t[p, col];
                kt[r, col] = sum;
            }

        var kg = new double[6, 6];

        for (var r = 0; r < 6; r++)
            for (var col = 0; col < 6; col++)
            {
                double sum = 0;
                for (var p = 0; p < 6; p++) sum += t[p, r] * kt[p, col];
                kg[r, col] = sum;
            }

        return kg;
    }

    private static double[] ToGlobalVector(double[] local, double c, double s) =>
    [
        c * local[0] - s * local[1], s * local[0] + c * local[1], local[2],
        c * local[3] - s * local[4], s * local[3] + c * local[4], local[5],
    ];

    private static double[] ToLocalVector(double[] global, double c, double s) =>
    [
        c * global[0] + s * global[1], -s * global[0] + c * global[1], global[2],
        c * global[3] + s * global[4], -s * global[3] + c * global[4], global[5],
    ];

    /// <summary>Node order that keeps connected nodes close (reverse Cuthill–McKee over the element graph), component by component.</summary>
    private static List<int> ReverseCuthillMcKee(FrameModel model)
    {
        var adj = model.Nodes.ToDictionary(n => n.Id, _ => new List<int>());

        foreach (var e in model.Elements)
        {
            adj[e.I].Add(e.J);
            adj[e.J].Add(e.I);
        }

        var visited = new HashSet<int>();
        var order = new List<int>();

        foreach (var start in model.Nodes.OrderBy(n => adj[n.Id].Count).ThenBy(n => n.Id).Select(n => n.Id))
        {
            if (visited.Contains(start)) continue;
            var queue = new Queue<int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                order.Add(v);

                foreach (var w in adj[v].Distinct().OrderBy(w => adj[w].Count).ThenBy(w => w))
                {
                    if (visited.Add(w)) queue.Enqueue(w);
                }
            }
        }

        order.Reverse();
        return order;
    }

    /// <summary>A symmetric positive-definite matrix stored by upper band, factored in place as LDLᵀ.</summary>
    private sealed class BandedSymmetric
    {
        private readonly int _n;
        private readonly int _half;
        private readonly double[] _a; // _a[i * (_half + 1) + (j - i)] for j ≥ i
        private bool _factored;

        public BandedSymmetric(int n, int halfBandwidth)
        {
            _n = n;
            _half = halfBandwidth;
            _a = new double[Math.Max(1, n * (halfBandwidth + 1))];
        }

        private int Index(int i, int j) => i * (_half + 1) + (j - i);

        public void Add(int i, int j, double v)
        {
            if (j < i) (i, j) = (j, i);
            _a[Index(i, j)] += v;
        }

        public double Diagonal(int i) => _a[Index(i, i)];

        public void Factor()
        {
            var scale = 0.0;
            for (var i = 0; i < _n; i++) scale = Math.Max(scale, Math.Abs(Diagonal(i)));

            for (var j = 0; j < _n; j++)
            {
                var lo = Math.Max(0, j - _half);
                var djj = _a[Index(j, j)];

                for (var k = lo; k < j; k++)
                {
                    var lkj = _a[Index(k, j)];
                    djj -= lkj * lkj * _a[Index(k, k)];
                }

                if (!(djj > 1e-10 * scale))
                    throw new FrameModelException("the structure is a mechanism (a non-positive stiffness pivot): check the supports and the releases.");

                _a[Index(j, j)] = djj;
                var hi = Math.Min(_n - 1, j + _half);

                for (var i = j + 1; i <= hi; i++)
                {
                    var s = _a[Index(j, i)];
                    var lo2 = Math.Max(lo, i - _half);

                    for (var k = lo2; k < j; k++)
                        s -= _a[Index(k, j)] * _a[Index(k, i)] * _a[Index(k, k)];

                    _a[Index(j, i)] = s / djj;
                }
            }

            _factored = true;
        }

        public double[] Solve(double[] b)
        {
            if (!_factored) throw new InvalidOperationException("Factor first.");
            var x = (double[])b.Clone();

            // Forward: L y = b (L stored as rows k → columns i: L[i,k] = _a[Index(k, i)]).
            for (var i = 0; i < _n; i++)
            {
                var lo = Math.Max(0, i - _half);
                for (var k = lo; k < i; k++) x[i] -= _a[Index(k, i)] * x[k];
            }

            for (var i = 0; i < _n; i++) x[i] /= _a[Index(i, i)];

            // Backward: Lᵀ x = z.
            for (var i = _n - 1; i >= 0; i--)
            {
                var hi = Math.Min(_n - 1, i + _half);
                for (var k = i + 1; k <= hi; k++) x[i] -= _a[Index(i, k)] * x[k];
            }

            return x;
        }
    }
}
