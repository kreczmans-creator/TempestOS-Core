using System.Text;
using Tempest.Core.Reporting;
using Tempest.Core.Requirements;

namespace Tempest.Samples;

/// <summary>
/// Renders <see cref="SampleRequirementReportDefinition"/> as a plain-text
/// summary of the sample requirement identified by
/// <see cref="ReportRequest.Parameters"/>'s own <c>"RequirementId"</c>
/// entry — gathered through <see cref="IRequirementsService"/> directly,
/// never through report-owned state, mirroring every existing renderer's
/// own "gather data, then render" separation.
/// </summary>
public sealed class SampleRequirementReportRenderer : IReportRenderer<SampleRequirementReportDefinition>
{
    private readonly IRequirementsService _requirementsService;

    /// <summary>Initialises a new instance of the <see cref="SampleRequirementReportRenderer"/> class.</summary>
    public SampleRequirementReportRenderer(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    /// <inheritdoc />
    public async Task<ReportResult> RenderAsync(SampleRequirementReportDefinition definition, ReportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        var requirementId = Guid.Parse(request.Parameters["RequirementId"]);
        var requirement = await _requirementsService.FindAsync(requirementId, cancellationToken).ConfigureAwait(false);

        var text = new StringBuilder()
            .AppendLine($"Requirement: {requirement?.Identifier}")
            .AppendLine($"Statement: {requirement?.Statement}")
            .AppendLine($"Category: {requirement?.Category}")
            .AppendLine($"Status: {requirement?.Status}")
            .ToString();

        return new ReportResult("text/plain", Encoding.UTF8.GetBytes(text));
    }
}
