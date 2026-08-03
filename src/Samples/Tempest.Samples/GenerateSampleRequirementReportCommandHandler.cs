using Tempest.Core.Commands;
using Tempest.Core.Reporting;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="GenerateSampleRequirementReportCommand"/> by
/// generating <see cref="SampleRequirementReportDefinition"/> through
/// <see cref="IReportingService"/> for <see cref="RequirementsSampleModule.SampleRequirementId"/>.
/// </summary>
public sealed class GenerateSampleRequirementReportCommandHandler : ICommandHandler<GenerateSampleRequirementReportCommand>
{
    private readonly IReportingService _reportingService;
    private readonly RequirementsSampleModule _module;

    /// <summary>Initialises a new instance of the <see cref="GenerateSampleRequirementReportCommandHandler"/> class.</summary>
    public GenerateSampleRequirementReportCommandHandler(IReportingService reportingService, RequirementsSampleModule module)
    {
        ArgumentNullException.ThrowIfNull(reportingService);
        ArgumentNullException.ThrowIfNull(module);

        _reportingService = reportingService;
        _module = module;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(GenerateSampleRequirementReportCommand command, CancellationToken cancellationToken)
    {
        var request = new ReportRequest(new Dictionary<string, string> { ["RequirementId"] = _module.SampleRequirementId!.Value.ToString() });

        var result = await _reportingService.GenerateAsync(
            new SampleRequirementReportDefinition().Id, request, cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success($"Report generated ({result.Content.Length} bytes, {result.ContentType}).");
    }
}
