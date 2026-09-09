using Tempest.Core.ReferenceData;

namespace Tempest.Core.Materials;

/// <summary>
/// Materials-specific data-quality validation — the rules a material
/// reference record must satisfy to be trustworthy engineering data.
/// </summary>
/// <remarks>
/// The surface is <see cref="IReferenceValidationService{TDefinition}"/>,
/// shared with every Group A library; what is materials-specific is the
/// rule set behind it (<see cref="MaterialValidationRules"/>).
/// </remarks>
public interface IMaterialValidationService : IReferenceValidationService<MaterialDefinition>
{
}
