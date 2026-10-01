namespace Tempest.Core.Calculations.Modules.Diagrams;

/// <summary>A point on a diagram's own drawing sheet: <see cref="CalculationDiagramSpec.Width"/> by <see cref="CalculationDiagramSpec.Height"/> units, y downward. Not to scale.</summary>
/// <param name="X">Across, from the left edge.</param>
/// <param name="Y">Down, from the top edge.</param>
public readonly record struct DiagramPoint(double X, double Y);

/// <summary>How a member is held where a support is drawn.</summary>
public enum DiagramSupportKind
{
    /// <summary>A pin: a triangle on hatched ground. Restrains translation, free to rotate.</summary>
    Pinned,

    /// <summary>A roller: a triangle on two wheels. Restrains translation normal to the ground only.</summary>
    Roller,

    /// <summary>A built-in end: a hatched wall. Restrains translation and rotation.</summary>
    Fixed,
}

/// <summary>A direction on the sheet: which way a load points, or which side of a support its ground is on.</summary>
public enum DiagramDirection
{
    /// <summary>Towards the left edge.</summary>
    Left,

    /// <summary>Towards the right edge.</summary>
    Right,

    /// <summary>Towards the top edge.</summary>
    Up,

    /// <summary>Towards the bottom edge.</summary>
    Down,
}

/// <summary>
/// One shape of a reference diagram. A shape that names an
/// <see cref="InputName"/> is that input's shape: it is labelled with the
/// input's value as typed, and highlighted while the input has focus.
/// </summary>
/// <param name="Id">The shape's own id, unique within its variant.</param>
/// <param name="InputName">The input the shape stands for (the descriptor's <see cref="CalculationInputDescriptor.Name"/>), or <see langword="null"/> for context drawn for orientation only.</param>
/// <param name="Symbol">The symbol its label leads with (<c>L</c>, <c>W</c>, <c>ΔT</c>); a shape bound to no input shows it as plain text. <see langword="null"/> for no label (a bound shape without one is still highlighted with its input).</param>
public abstract record DiagramElement(string Id, string? InputName, string? Symbol)
{
    /// <summary>Where the label is centred: the spec's own position where given, otherwise the shape's natural place for one.</summary>
    public abstract DiagramPoint LabelAnchor { get; }
}

/// <summary>A structural member drawn as a thick line: a beam, a column, a bar, a shaft.</summary>
public sealed record DiagramMember(string Id, DiagramPoint From, DiagramPoint To, double Thickness = 8, string? InputName = null, string? Symbol = null, DiagramPoint? LabelAt = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => LabelAt ?? new((From.X + To.X) / 2, Math.Min(From.Y, To.Y) - Thickness - 10);
}

/// <summary>A plate or a section drawn as a filled rectangle.</summary>
public sealed record DiagramPlate(string Id, double X, double Y, double Width, double Height, string? InputName = null, string? Symbol = null, DiagramPoint? LabelAt = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => LabelAt ?? new(X + Width / 2, Y + Height / 2);
}

/// <summary>A circle: a hole (unfilled), a bolt or shaft section, a vessel shell.</summary>
public sealed record DiagramCircle(string Id, DiagramPoint Centre, double Radius, bool Filled, string? InputName = null, string? Symbol = null, DiagramPoint? LabelAt = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => LabelAt ?? Centre;
}

/// <summary>A support at a point of a member, its ground on the <paramref name="Ground"/> side.</summary>
public sealed record DiagramSupport(string Id, DiagramPoint At, DiagramSupportKind Kind, DiagramDirection Ground, string? InputName = null, string? Symbol = null, DiagramPoint? LabelAt = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => LabelAt ?? new(At.X, At.Y + 34);
}

/// <summary>A point load: an arrow of <paramref name="Length"/> whose head touches <paramref name="Tip"/>, pointing <paramref name="Direction"/>.</summary>
public sealed record DiagramPointLoad(string Id, DiagramPoint Tip, DiagramDirection Direction, double Length = 44, string? InputName = null, string? Symbol = null, DiagramPoint? LabelAt = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <summary>The arrow's tail.</summary>
    public DiagramPoint Tail => Direction switch
    {
        DiagramDirection.Down => new(Tip.X, Tip.Y - Length),
        DiagramDirection.Up => new(Tip.X, Tip.Y + Length),
        DiagramDirection.Left => new(Tip.X + Length, Tip.Y),
        _ => new(Tip.X - Length, Tip.Y),
    };

    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => LabelAt ?? Direction switch
    {
        DiagramDirection.Down => new(Tail.X, Tail.Y - 10),
        DiagramDirection.Up => new(Tail.X, Tail.Y + 10),
        _ => new(Tail.X, Tail.Y - 12),
    };
}

/// <summary>A uniformly distributed load along <paramref name="FromX"/> to <paramref name="ToX"/>, arrows pointing down onto <paramref name="SurfaceY"/>.</summary>
public sealed record DiagramDistributedLoad(string Id, double FromX, double ToX, double SurfaceY, double Height = 30, string? InputName = null, string? Symbol = null, DiagramPoint? LabelAt = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => LabelAt ?? new((FromX + ToX) / 2, SurfaceY - Height - 10);
}

/// <summary>A moment or a torque: a three-quarter arc arrow about <paramref name="Centre"/>.</summary>
public sealed record DiagramMoment(string Id, DiagramPoint Centre, double Radius = 18, bool Clockwise = true, string? InputName = null, string? Symbol = null, DiagramPoint? LabelAt = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => LabelAt ?? new(Centre.X, Centre.Y - Radius - 12);
}

/// <summary>A spring (a zig-zag) from <paramref name="From"/> to <paramref name="To"/>: an elastic restraint.</summary>
public sealed record DiagramSpring(string Id, DiagramPoint From, DiagramPoint To, string? InputName = null, string? Symbol = null, DiagramPoint? LabelAt = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => LabelAt ?? new((From.X + To.X) / 2, Math.Min(From.Y, To.Y) - 18);
}

/// <summary>
/// A dimension: a line with an arrowhead at each end, parallel to
/// <paramref name="From"/>–<paramref name="To"/> and offset from it by
/// <paramref name="Offset"/> (positive is below a horizontal dimension,
/// right of a vertical one), with extension lines back to the measured points.
/// </summary>
public sealed record DiagramDimension(string Id, DiagramPoint From, DiagramPoint To, double Offset, string? InputName = null, string? Symbol = null, DiagramPoint? LabelAt = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <summary>Whether the dimension runs up and down the sheet rather than across.</summary>
    public bool IsVertical => Math.Abs(To.X - From.X) < Math.Abs(To.Y - From.Y);

    /// <summary>The dimension line's own start, after the offset.</summary>
    public DiagramPoint LineFrom => IsVertical ? new(From.X + Offset, From.Y) : new(From.X, From.Y + Offset);

    /// <summary>The dimension line's own end, after the offset.</summary>
    public DiagramPoint LineTo => IsVertical ? new(To.X + Offset, To.Y) : new(To.X, To.Y + Offset);

    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => LabelAt ?? (IsVertical
        ? new(LineFrom.X + 8, (LineFrom.Y + LineTo.Y) / 2)
        : new((LineFrom.X + LineTo.X) / 2, LineFrom.Y + 12));
}

/// <summary>A label alone: an input that has no shape of its own (a modulus, a section property, a temperature change), or a plain note when bound to no input.</summary>
public sealed record DiagramLabel(string Id, DiagramPoint At, string? InputName = null, string? Symbol = null)
    : DiagramElement(Id, InputName, Symbol)
{
    /// <inheritdoc />
    public override DiagramPoint LabelAnchor => At;
}

/// <summary>
/// One drawing of a calculation: the variant drawn when every input named
/// in <paramref name="When"/> holds the value given there (a choice's
/// member name, a number exactly as typed, or <c>true</c> / <c>false</c>
/// for a yes-or-no input).
/// </summary>
/// <param name="Caption">What the variant shows, in words: "Simply supported, point load at mid-span".</param>
/// <param name="When">The input values that select this variant; empty for the variant drawn otherwise.</param>
/// <param name="Elements">The shapes, in drawing order.</param>
public sealed record DiagramVariant(string Caption, IReadOnlyDictionary<string, string> When, IReadOnlyList<DiagramElement> Elements);

/// <summary>
/// The reference diagram of one calculation: its variants, the first whose
/// <see cref="DiagramVariant.When"/> holds being the one drawn. Inputs
/// only: no result, no deflected shape; never to scale.
/// </summary>
/// <param name="CalculationId">The calculation it illustrates.</param>
/// <param name="Variants">Its variants, most specific first; the last should match always.</param>
public sealed record CalculationDiagramSpec(string CalculationId, IReadOnlyList<DiagramVariant> Variants)
{
    /// <summary>The drawing sheet's width, in sheet units.</summary>
    public const double Width = 400;

    /// <summary>The drawing sheet's height, in sheet units.</summary>
    public const double Height = 240;

    /// <summary>Every input name any shape of any variant is bound to, or any variant is selected by.</summary>
    public IEnumerable<string> BoundInputNames =>
        Variants.SelectMany(v => v.Elements.Select(e => e.InputName).OfType<string>().Concat(v.When.Keys)).Distinct(StringComparer.Ordinal);
}
