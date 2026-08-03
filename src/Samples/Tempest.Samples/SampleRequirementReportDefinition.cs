using Tempest.Core.Reporting;

namespace Tempest.Samples;

/// <summary>A minimal report definition summarising <see cref="RequirementsSampleModule"/>'s own sample requirement — demonstrating Reporting integration for the Requirements Engine.</summary>
public sealed class SampleRequirementReportDefinition : IReportDefinition
{
    /// <inheritdoc />
    public string Id => "sample.requirement-summary";

    /// <inheritdoc />
    public string Name => "Sample Requirement Summary";
}
