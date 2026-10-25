namespace Tempest.Calc.Tests.Calculators;

/// <summary>
/// Test classes that assert a wall-clock budget run alone, never alongside the rest of the suite: a calculator that is well inside its budget on an idle
/// machine can exceed it while hundreds of other tests load every core.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Timing";
}
