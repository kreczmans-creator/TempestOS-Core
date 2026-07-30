using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler revises
/// <see cref="EngineeringDataSampleModule"/>'s own sample document through
/// <see cref="Tempest.Core.EngineeringData.IEngineeringDocumentStore"/>.
/// </summary>
/// <remarks>Carries no data — see <see cref="ReviseSampleDocumentCommandHandler"/>.</remarks>
public sealed class ReviseSampleDocumentCommand : ICommand
{
}
