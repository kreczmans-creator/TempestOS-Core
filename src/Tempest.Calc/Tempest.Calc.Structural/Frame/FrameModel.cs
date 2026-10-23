namespace Tempest.Calc.Structural.Frame;

/// <summary>A node of a 2D frame (SI): position, restraints and optional spring stiffnesses (N/m, N·m/rad).</summary>
public sealed record FrameNode(int Id, double X, double Y, bool FixX, bool FixY, bool FixR, double Kx = 0, double Ky = 0, double Kr = 0)
{
    public bool IsSupported => FixX || FixY || FixR || Kx > 0 || Ky > 0 || Kr > 0;
}

/// <summary>A Euler–Bernoulli frame element from node I to node J (SI): E, A, I and optional moment releases at either end.</summary>
public sealed record FrameElement(int Id, int I, int J, double E, double A, double Iz, bool ReleaseI, bool ReleaseJ);

public enum FrameLoadKind
{
    /// <summary>Forces and a moment at a node (global axes).</summary>
    Nodal,

    /// <summary>A point force on an element at distance A from end I (global axes).</summary>
    Point,

    /// <summary>A uniformly distributed load W1 over [A, B] of an element.</summary>
    Udl,

    /// <summary>A load varying linearly from W1 at A to W2 at B.</summary>
    Trapezoid,

    /// <summary>A concentrated couple M on an element at distance A from end I (anticlockwise positive).</summary>
    Moment,
}

/// <summary>
/// One load in one load case (SI). Distributed loads act perpendicular to
/// the member (positive towards the member's local +y, the member axis
/// rotated 90° anticlockwise) unless <see cref="Global"/>, in which case
/// they act in global −y (gravity) per unit length of member.
/// </summary>
public sealed record FrameLoad(int Case, FrameLoadKind Kind, int Target, double A, double B, double W1, double W2, double Fx, double Fy, double M, bool Global);

/// <summary>A named combination: Σ factor × load case.</summary>
public sealed record FrameCombination(string Name, IReadOnlyList<(int Case, double Factor)> Terms);

/// <summary>Caps (§3.2 S2): 2,000 nodes, 4,000 elements, 50 load cases.</summary>
public static class FrameLimits
{
    public const int MaxNodes = 2_000;
    public const int MaxElements = 4_000;
    public const int MaxCases = 50;
    public const int MaxCombinations = 50;
    public const int MaxLoads = 20_000;
    public const int MaxLoadsPerMember = 200;
    public const long MaxFactorWork = 1_500_000_000;
    public const int MaxStations = 100;
    public const int MaxBandedEntries = 20_000_000;
}

/// <summary>A validated frame: nodes, elements, loads and combinations, with lookups.</summary>
public sealed class FrameModel
{
    public FrameModel(IReadOnlyList<FrameNode> nodes, IReadOnlyList<FrameElement> elements, IReadOnlyList<FrameLoad> loads, IReadOnlyList<FrameCombination> combinations)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(loads);
        ArgumentNullException.ThrowIfNull(combinations);

        if (nodes.Count < 2)
            throw new FrameModelException("nodes: a frame needs at least two nodes.");

        if (nodes.Count > FrameLimits.MaxNodes)
            throw new FrameModelException($"nodes: at most {FrameLimits.MaxNodes} nodes.");

        if (elements.Count == 0)
            throw new FrameModelException("elements: a frame needs at least one element.");

        if (elements.Count > FrameLimits.MaxElements)
            throw new FrameModelException($"elements: at most {FrameLimits.MaxElements} elements.");

        if (loads.Count > FrameLimits.MaxLoads)
            throw new FrameModelException($"loads: at most {FrameLimits.MaxLoads} load rows.");

        if (nodes.Select(n => n.Id).Distinct().Count() != nodes.Count)
            throw new FrameModelException("nodes: node ids must be unique.");

        if (elements.Select(e => e.Id).Distinct().Count() != elements.Count)
            throw new FrameModelException("elements: element ids must be unique.");

        Nodes = nodes;
        Elements = elements;
        Loads = loads;
        NodeById = nodes.ToDictionary(n => n.Id);
        ElementById = elements.ToDictionary(e => e.Id);

        foreach (var n in nodes)
        {
            if (!double.IsFinite(n.X) || !double.IsFinite(n.Y))
                throw new FrameModelException($"nodes: node {n.Id} has a non-finite coordinate.");

            if (n.Kx < 0 || n.Ky < 0 || n.Kr < 0)
                throw new FrameModelException($"nodes: node {n.Id} has a negative spring stiffness.");
        }

        foreach (var e in elements)
        {
            if (!NodeById.ContainsKey(e.I) || !NodeById.ContainsKey(e.J))
                throw new FrameModelException($"elements: element {e.Id} refers to a node that does not exist.");

            if (e.I == e.J)
                throw new FrameModelException($"elements: element {e.Id} starts and ends at the same node.");

            if (Length(e) <= 1e-9)
                throw new FrameModelException($"elements: element {e.Id} has zero length.");

            if (!(e.E > 0) || !(e.A > 0) || !(e.Iz > 0))
                throw new FrameModelException($"elements: element {e.Id} needs positive E, A and I.");
        }

        var perMember = loads.Where(l => l.Kind != FrameLoadKind.Nodal).GroupBy(l => l.Target).FirstOrDefault(g => g.Count() > FrameLimits.MaxLoadsPerMember);

        if (perMember is not null)
            throw new FrameModelException($"loads: element {perMember.Key} carries {perMember.Count()} load rows; at most {FrameLimits.MaxLoadsPerMember} per member (combine them or split the member).");

        var cases = loads.Select(l => l.Case).Distinct().OrderBy(c => c).ToList();

        if (cases.Count > FrameLimits.MaxCases)
            throw new FrameModelException($"loads: at most {FrameLimits.MaxCases} load cases.");

        foreach (var l in loads)
        {
            foreach (var v in new[] { l.A, l.B, l.W1, l.W2, l.Fx, l.Fy, l.M })
            {
                if (!double.IsFinite(v))
                    throw new FrameModelException($"loads: a load in case {l.Case} has a non-finite value.");
            }

            if (l.Kind == FrameLoadKind.Nodal)
            {
                if (!NodeById.ContainsKey(l.Target))
                    throw new FrameModelException($"loads: nodal load in case {l.Case} targets node {l.Target}, which does not exist.");

                continue;
            }

            if (!ElementById.TryGetValue(l.Target, out var e))
                throw new FrameModelException($"loads: load in case {l.Case} targets element {l.Target}, which does not exist.");

            var len = Length(e);

            if (l.A < -1e-9 || l.A > len + 1e-9 || l.B < -1e-9 || l.B > len + 1e-9)
                throw new FrameModelException($"loads: load in case {l.Case} on element {l.Target} lies outside the member (0 … {len:G4} m).");

            // a = b = 0 means the whole span.
            if (l.Kind is FrameLoadKind.Udl or FrameLoadKind.Trapezoid && l.B <= l.A && !(l.A == 0 && l.B == 0))
                throw new FrameModelException($"loads: distributed load in case {l.Case} on element {l.Target} needs b > a (leave both blank for the whole span).");
        }

        if (combinations.Count > FrameLimits.MaxCombinations)
            throw new FrameModelException($"combinations: at most {FrameLimits.MaxCombinations} combinations.");

        foreach (var c in combinations)
        {
            foreach (var (caseId, factor) in c.Terms)
            {
                if (!cases.Contains(caseId))
                    throw new FrameModelException($"combinations: '{c.Name}' uses load case {caseId}, which has no loads.");

                if (!double.IsFinite(factor))
                    throw new FrameModelException($"combinations: '{c.Name}' has a non-finite factor.");
            }
        }

        Cases = cases;
        Combinations = combinations.Count > 0 ? combinations : cases.Select(c => new FrameCombination($"case {c}", [(c, 1.0)])).ToList();

        if (Combinations.Count == 0)
            throw new FrameModelException("loads: no loads were given.");
    }

    public IReadOnlyList<FrameNode> Nodes { get; }

    public IReadOnlyList<FrameElement> Elements { get; }

    public IReadOnlyList<FrameLoad> Loads { get; }

    public IReadOnlyList<int> Cases { get; }

    public IReadOnlyList<FrameCombination> Combinations { get; }

    public IReadOnlyDictionary<int, FrameNode> NodeById { get; }

    public IReadOnlyDictionary<int, FrameElement> ElementById { get; }

    public double Length(FrameElement e)
    {
        var (dx, dy) = Delta(e);
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public (double Dx, double Dy) Delta(FrameElement e)
    {
        var a = NodeById[e.I];
        var b = NodeById[e.J];
        return (b.X - a.X, b.Y - a.Y);
    }
}

/// <summary>A frame that cannot be analysed as given: a bad reference, a cap, a mechanism.</summary>
public sealed class FrameModelException : Exception
{
    public FrameModelException(string message) : base(message) { }

    public FrameModelException() { }

    public FrameModelException(string message, Exception innerException) : base(message, innerException) { }
}
