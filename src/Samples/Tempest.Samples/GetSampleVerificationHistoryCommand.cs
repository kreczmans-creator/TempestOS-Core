using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler retrieves the verification history
/// for <see cref="VerificationSampleModule.SampleSubjectDocumentId"/>
/// through <see cref="Tempest.Core.Verification.IVerificationService"/>.
/// </summary>
/// <remarks>Carries no data — see <see cref="GetSampleVerificationHistoryCommandHandler"/>.</remarks>
public sealed class GetSampleVerificationHistoryCommand : ICommand
{
}
