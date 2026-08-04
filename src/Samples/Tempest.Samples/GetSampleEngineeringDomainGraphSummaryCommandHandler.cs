using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="GetSampleEngineeringDomainGraphSummaryCommand"/> by traversing outward from
/// <see cref="EngineeringDomainSampleModule.SampleAssemblyId"/> over the Composition category, demonstrating
/// <see cref="IDependencyTraversal"/> against a real, multi-object graph.
/// </summary>
public sealed class GetSampleEngineeringDomainGraphSummaryCommandHandler : ICommandHandler<GetSampleEngineeringDomainGraphSummaryCommand>
{
    private readonly IDependencyTraversal _dependencyTraversal;
    private readonly EngineeringDomainSampleModule _sampleModule;

    public GetSampleEngineeringDomainGraphSummaryCommandHandler(IDependencyTraversal dependencyTraversal, EngineeringDomainSampleModule sampleModule)
    {
        ArgumentNullException.ThrowIfNull(dependencyTraversal);
        ArgumentNullException.ThrowIfNull(sampleModule);

        _dependencyTraversal = dependencyTraversal;
        _sampleModule = sampleModule;
    }

    public async Task<CommandResult> HandleAsync(GetSampleEngineeringDomainGraphSummaryCommand command, CancellationToken cancellationToken)
    {
        if (_sampleModule.SampleAssemblyId is not { } assemblyId)
            return CommandResult.Failure("The sample module has not finished initialising yet.");

        var composedChildren = await _dependencyTraversal.TraverseAsync(
            assemblyId, RelationshipCategory.Composition, maxDepth: 2, cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success(
            $"Sample graph contains {_sampleModule.AllSampleObjectIds.Count} objects total; " +
            $"{composedChildren.Count} found by composition traversal (depth 2) from the sample Assembly.");
    }
}
