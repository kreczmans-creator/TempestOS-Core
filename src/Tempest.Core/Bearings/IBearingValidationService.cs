using Tempest.Core.ReferenceData;

namespace Tempest.Core.Bearings;

/// <summary>
/// Bearing-specific data-quality validation — the rules a bearing
/// reference record must satisfy to be trustworthy engineering data.
/// </summary>
/// <remarks>
/// The surface itself is
/// <see cref="IReferenceValidationService{TDefinition}"/>, shared with
/// every Group A library; what is bearing-specific is the rule set behind
/// it (<see cref="BearingValidationRules"/>). This interface exists so a
/// caller can resolve bearing validation by name from the container
/// without naming a generic closed over a domain type.
/// </remarks>
public interface IBearingValidationService : IReferenceValidationService<BearingDefinition>
{
}
