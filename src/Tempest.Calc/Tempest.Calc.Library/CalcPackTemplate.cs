using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// One calculation of a pack template: a key the bindings use, the template id and the name it is given.
/// </summary>
/// <param name="Key">The key the bindings use.</param>
/// <param name="TemplateId">The calculator's template id.</param>
/// <param name="Name">The name the calculation is given.</param>
/// <param name="Example">
/// Example inputs for the step, used when the caller gives none for it: laid over the sheet's opening inputs, with the inputs a link
/// supplies and those in <paramref name="Omit"/> left out. Every example input is recorded as an assumption to replace
/// (<see cref="CalcInstanceService.ExampleBasis"/>). <see langword="null"/> (the default, and how templates saved from a chain are
/// stored) means the step opens on the sheet's defaults.
/// </param>
/// <param name="Omit">Opening inputs the example leaves out because another input replaces them (a flow, when a heat load gives it).</param>
/// <param name="Hint">Said after the reason when this step blocks the chain (what the user must supply).</param>
/// <param name="TemplateVersion">
/// The template version the step was saved from (a template saved from a chain records each calculation's version); <see langword="null"/> for a
/// template composed in code, which is written for the current version. A binding or an example that names a symbol whose meaning changed after
/// this version (<see cref="CalcOutputPort.MeaningChangedAt"/>, <see cref="CalcInputSpec.MeaningChangedAt"/>) is refused rather than rebound.
/// </param>
public sealed record CalcPackStep(string Key, string TemplateId, string Name, IReadOnlyList<CalcInputValue>? Example = null, IReadOnlyList<string>? Omit = null, string? Hint = null, string? TemplateVersion = null);

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
