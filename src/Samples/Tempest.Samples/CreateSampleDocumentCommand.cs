using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler creates a new document through
/// <see cref="Tempest.Core.EngineeringData.IEngineeringDocumentStore"/>.
/// </summary>
/// <remarks>Carries no data — see <see cref="CreateSampleDocumentCommandHandler"/>.</remarks>
public sealed class CreateSampleDocumentCommand : ICommand
{
}
