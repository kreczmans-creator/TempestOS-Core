namespace Tempest.Calc.Tests.Calculators;

/// <summary>
/// Test classes that assert a wall-clock budget run alone, never alongside the rest of the suite: a calculator that is well inside its budget on an idle
/// machine can exceed it while hundreds of other tests load every core. They also carry the trait <c>Category=Timing</c>, and CI's coverage run
/// leaves them out and runs them again without the coverage collector: its instrumentation counts every branch the code takes, which made the
/// tolerance stack at its hard limits take 8.9 s instead of 2 s here (about 20 s against the 15 s budget on the Release calc leg).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Timing";

    /// <summary>The trait value CI filters on to run these tests without the coverage collector.</summary>
    public const string Category = "Timing";
}
