namespace Tempest.Core.Tests;

/// <summary>
/// Test classes that assert a wall-clock budget run alone, after the parallel classes, never beside them: with the other 7,000 Core tests
/// loading every core, PersistenceXeroOutboxOrganisationAndGrowthTests' 1,500 outbox calls ran past their 10 s budget (21 s for the test),
/// though the store-read counts it asserts first were right. The budgets are unchanged.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Core timing";
}
