using Tempest.Core.Reporting;

namespace Tempest.Samples;

/// <summary>
/// A reference report definition — a simple, domain-neutral summary
/// report demonstrating the Reporting Framework's own registration and
/// generation pipeline.
/// </summary>
/// <remarks>
/// Carries no rendering logic of its own — see
/// <see cref="SampleSummaryReportRenderer"/>.
/// </remarks>
public sealed class SampleSummaryReportDefinition : IReportDefinition
{
    /// <summary>This report definition's own registered Id.</summary>
    public const string ReportId = "tempest.samples.reporting.summary";

    /// <inheritdoc />
    public string Id => ReportId;

    /// <inheritdoc />
    public string Name => "Sample Summary Report";
}
