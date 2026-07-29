using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler generates
/// <see cref="SampleSummaryReportDefinition"/> through
/// <see cref="Tempest.Core.Reporting.IReportingService"/>, demonstrating
/// Identity (permission-gated), Audit (recording), and Notifications
/// (a completion notice) integration together — see
/// <see cref="GenerateSampleReportCommandHandler"/>.
/// </summary>
/// <remarks>
/// Carries no data — the sample report takes no caller-supplied
/// parameters.
/// </remarks>
public sealed class GenerateSampleReportCommand : ICommand
{
}
