using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler revises this module's own sample
/// material through <see cref="Tempest.Core.Materials.IMaterialCatalog"/>.
/// </summary>
/// <remarks>Carries no data — see <see cref="ReviseSampleMaterialCommandHandler"/>.</remarks>
public sealed class ReviseSampleMaterialCommand : ICommand
{
}
