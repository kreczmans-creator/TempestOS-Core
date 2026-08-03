using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler registers a new, fictional material
/// through <see cref="Tempest.Core.Materials.IMaterialCatalog"/>.
/// </summary>
/// <remarks>Carries no data — see <see cref="RegisterSampleMaterialCommandHandler"/>.</remarks>
public sealed class RegisterSampleMaterialCommand : ICommand
{
}
