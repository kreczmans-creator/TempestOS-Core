using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler reads
/// <see cref="Tempest.Core.Diagnostics.IDiagnosticsProvider"/> and reports
/// a human-readable summary — demonstrating the Command Framework and
/// Diagnostics interacting, exactly as a future Shell command ("show
/// platform status") realistically would.
/// </summary>
/// <remarks>
/// Carries no data — see <see cref="GetDiagnosticsSummaryCommandHandler"/>.
/// </remarks>
public sealed class GetDiagnosticsSummaryCommand : ICommand
{
}
