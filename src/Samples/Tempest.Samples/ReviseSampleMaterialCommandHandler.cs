using Tempest.Core.Commands;
using Tempest.Core.Materials;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="ReviseSampleMaterialCommand"/> by revising
/// <see cref="MaterialsSampleModule.SampleMaterialId"/> through
/// <see cref="IMaterialCatalog"/>.
/// </summary>
public sealed class ReviseSampleMaterialCommandHandler : ICommandHandler<ReviseSampleMaterialCommand>
{
    private readonly IMaterialCatalog _materialCatalog;
    private readonly MaterialsSampleModule _module;

    /// <summary>
    /// Initialises a new instance of the <see cref="ReviseSampleMaterialCommandHandler"/> class.
    /// </summary>
    /// <param name="materialCatalog">The Materials Framework service this handler revises through.</param>
    /// <param name="module">The module instance owning the material this handler revises.</param>
    public ReviseSampleMaterialCommandHandler(IMaterialCatalog materialCatalog, MaterialsSampleModule module)
    {
        ArgumentNullException.ThrowIfNull(materialCatalog);
        ArgumentNullException.ThrowIfNull(module);

        _materialCatalog = materialCatalog;
        _module = module;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ReviseSampleMaterialCommand command, CancellationToken cancellationToken)
    {
        var materialId = _module.RegisteredMaterialId ?? MaterialsSampleModule.SampleMaterialId;

        var revised = await _materialCatalog.ReviseAsync(
            materialId,
            MaterialsSampleModule.BuildSampleDefinition(yieldStrengthMPa: 110.0, referenceLengthMm: 10.0),
            MaterialsSampleModule.SampleProvenance,
            "Revised via command — fictional updated test value.",
            cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success($"Revised material '{materialId}' to revision {revised.RevisionNumber}.");
    }
}
