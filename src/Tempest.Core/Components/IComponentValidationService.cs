using Tempest.Core.ReferenceData;

namespace Tempest.Core.Components;

/// <summary>
/// Component-specific data-quality validation — the rules a mechanical
/// component reference record must satisfy to be trustworthy engineering
/// data.
/// </summary>
/// <remarks>
/// The surface is <see cref="IReferenceValidationService{TDefinition}"/>,
/// shared with every Group A library; what is component-specific is the
/// rule set behind it (<see cref="ComponentValidationRules"/>).
/// <para>
/// Every rule asks whether the record is internally coherent,
/// geometrically possible and properly attributed. None asks whether a
/// spring suits a load, a gear a ratio, or a coupling a drive — that is
/// design, and A5 holds none of it.
/// </para>
/// </remarks>
public interface IComponentValidationService : IReferenceValidationService<ComponentDefinition>
{
}
