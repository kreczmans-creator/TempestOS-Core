using Tempest.Core.Commands;
using Tempest.Core.Diagnostics;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="GetDiagnosticsSummaryCommand"/> by reading
/// <see cref="IDiagnosticsProvider"/> and reporting a one-line summary of
/// the Host's own current lifecycle state.
/// </summary>
/// <remarks>
/// Depends on <see cref="IDiagnosticsProvider"/> directly, as an ordinary,
/// explicit peer dependency of this command's own application logic —
/// the Command Framework itself never depends on Diagnostics, and
/// Diagnostics never depends on the Command Framework. Always succeeds:
/// <see cref="IDiagnosticsProvider"/> has no failure mode of its own to
/// propagate (see <c>Diagnostics Architecture.md</c>'s own Failure
/// Model).
/// </remarks>
public sealed class GetDiagnosticsSummaryCommandHandler : ICommandHandler<GetDiagnosticsSummaryCommand>
{
    private readonly IDiagnosticsProvider _diagnosticsProvider;

    /// <summary>
    /// Initialises a new instance of the <see cref="GetDiagnosticsSummaryCommandHandler"/> class.
    /// </summary>
    /// <param name="diagnosticsProvider">The Diagnostics service this handler reads from.</param>
    public GetDiagnosticsSummaryCommandHandler(IDiagnosticsProvider diagnosticsProvider)
    {
        ArgumentNullException.ThrowIfNull(diagnosticsProvider);

        _diagnosticsProvider = diagnosticsProvider;
    }

    /// <inheritdoc />
    public Task<CommandResult> HandleAsync(GetDiagnosticsSummaryCommand command, CancellationToken cancellationToken)
    {
        var summary =
            $"Host: {_diagnosticsProvider.HostState}. " +
            $"Modules: {_diagnosticsProvider.Modules.Count} tracked " +
            $"({_diagnosticsProvider.Modules.Count(m => m.State == Core.Modules.ModuleState.Running)} running). " +
            $"Hosted services: {_diagnosticsProvider.HostedServices.Count} tracked.";

        return Task.FromResult(CommandResult.Success(summary));
    }
}
