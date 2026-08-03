using Tempest.Core.Commands;
using Tempest.Core.Identity;
using Tempest.Core.Verification;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="GetSampleVerificationHistoryCommand"/> by retrieving
/// the verification history for
/// <see cref="VerificationSampleModule.SampleSubjectDocumentId"/> through
/// <see cref="IVerificationService"/>.
/// </summary>
/// <remarks>
/// Reports the denial explicitly, as an ordinary
/// <see cref="CommandResult.Failure(string)"/>, when the current
/// principal does not hold <see cref="VerificationService.ReadPermission"/> —
/// this handler's own choice, mirroring
/// <see cref="Tempest.Samples.QuerySampleAuditRecordsCommandHandler"/>'s
/// own convention.
/// </remarks>
public sealed class GetSampleVerificationHistoryCommandHandler : ICommandHandler<GetSampleVerificationHistoryCommand>
{
    private readonly IVerificationService _verificationService;
    private readonly VerificationSampleModule _module;

    /// <summary>
    /// Initialises a new instance of the <see cref="GetSampleVerificationHistoryCommandHandler"/> class.
    /// </summary>
    /// <param name="verificationService">The Verification Framework service this handler queries through.</param>
    /// <param name="module">The module instance owning the subject document this handler queries.</param>
    public GetSampleVerificationHistoryCommandHandler(IVerificationService verificationService, VerificationSampleModule module)
    {
        ArgumentNullException.ThrowIfNull(verificationService);
        ArgumentNullException.ThrowIfNull(module);

        _verificationService = verificationService;
        _module = module;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(GetSampleVerificationHistoryCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var history = await _verificationService.GetVerificationHistoryAsync(
                _module.SampleSubjectDocumentId!.Value, cancellationToken)
                .ConfigureAwait(false);

            return CommandResult.Success($"Found {history.Count} verification record(s).");
        }
        catch (PermissionDeniedException)
        {
            return CommandResult.Failure("Denied: current principal does not hold the verification-read permission.");
        }
    }
}
