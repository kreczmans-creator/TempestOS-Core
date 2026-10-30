namespace Tempest.Desktop.Tests;

/// <summary>
/// Test classes that assert a wall-clock budget run alone, after the parallel classes, never beside them: a render well inside its budget on a
/// quiet machine took 6 to 12.5 s against a 5 s budget while the rest of a Desktop shard loaded every core (Red_PdfRendererAttacks,
/// Hostile_Pack_Renders_In_Budget), and 1 s on its own. The budgets are unchanged.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Desktop timing";
}
