using Tempest.Core.Commands;
using Tempest.Core.Materials;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="RegisterSampleMaterialCommand"/> by registering a new,
/// uniquely-identified fictional material through <see cref="IMaterialCatalog"/>.
/// </summary>
public sealed class RegisterSampleMaterialCommandHandler : ICommandHandler<RegisterSampleMaterialCommand>
{
    private readonly IMaterialCatalog _materialCatalog;

    /// <summary>
    /// Initialises a new instance of the <see cref="RegisterSampleMaterialCommandHandler"/> class.
    /// </summary>
    /// <param name="materialCatalog">The Materials Framework service this handler registers through.</param>
    public RegisterSampleMaterialCommandHandler(IMaterialCatalog materialCatalog)
    {
        ArgumentNullException.ThrowIfNull(materialCatalog);

        _materialCatalog = materialCatalog;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(RegisterSampleMaterialCommand command, CancellationToken cancellationToken)
    {
        var materialId = $"sample.materials-command-{Guid.NewGuid():N}";

        var definition = MaterialsSampleModule.BuildSampleDefinition(yieldStrengthMPa: 100.0, referenceLengthMm: 10.0) with
        {
            Name = "Fictional Command-Registered Material",
        };

        var material = await _materialCatalog.RegisterAsync(
            materialId,
            definition,
            MaterialsSampleModule.SampleProvenance,
            cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success($"Registered material '{material.Id}'.");
    }
}
