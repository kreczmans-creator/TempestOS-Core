using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>One calculation of a pack template: a key the bindings use, the template id and the name it is given.</summary>
public sealed record CalcPackStep(string Key, string TemplateId, string Name);

/// <summary>
/// One link of a pack template: output <paramref name="Output"/> of step <paramref name="FromStep"/> feeds input
/// <paramref name="Input"/> of step <paramref name="ToStep"/>. When <paramref name="Row"/> (zero-based) and
/// <paramref name="Column"/> are both set, <paramref name="Input"/> is a table and the link feeds that cell; the step must be
/// given that row as an input, because instantiating never creates rows.
/// </summary>
public sealed record CalcPackBinding(string FromStep, string Output, string ToStep, string Input, int? Row = null, string? Column = null);

/// <summary>
/// A saved chain of calculations (Addendum A, L1): "Drive train", "Motor circuit", "Wet heating", "Cooling", "Steel support".
/// Instantiated under a project it creates every calculation, binds the links in order and gathers them in one pack,
/// which is checked once and issued as one Evidence bundle.
/// </summary>
public sealed record CalcPackTemplate(string Id, string Title, string Description, IReadOnlyList<CalcPackStep> Steps, IReadOnlyList<CalcPackBinding> Bindings)
{
    /// <summary>The steps a template may hold (a pack holds at most <see cref="CalcPack.MaxInstances"/>).</summary>
    public const int MaxSteps = 20;
}

/// <summary>What instantiating a pack template made: the pack and its calculations by step key.</summary>
public sealed record CalcPackInstantiation(CalcPack Pack, IReadOnlyDictionary<string, CalcInstance> Instances);
