namespace Tempest.Calc.Tests.Library;

/// <summary>
/// Test classes that change what the whole test process shares, the current directory and environment variables, run alone. While
/// FeatureSourceTests had the current directory in a temporary folder with an appsettings.json that switches lifting on, and five lifting
/// variables set, every Library class running beside it resolved relative paths against that folder and could read those variables.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessStateCollection
{
    public const string Name = "Process state";
}
