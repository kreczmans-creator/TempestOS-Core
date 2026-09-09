using Tempest.Core.ReferenceData;

namespace Tempest.Core.Fasteners;

/// <summary>
/// Fastener-specific data-quality validation — the rules a fastener
/// reference record must satisfy to be trustworthy engineering data.
/// </summary>
/// <remarks>
/// The surface is <see cref="IReferenceValidationService{TDefinition}"/>,
/// shared with every Group A library; what is fastener-specific is the rule
/// set behind it (<see cref="FastenerValidationRules"/>).
/// <para>
/// Every rule asks whether the record is internally coherent, dimensionally
/// possible and properly attributed. None asks whether a fastener suits a
/// joint, whether a torque figure is right for an application, or whether a
/// class is strong enough — those are calculation and judgement, and A3
/// holds neither.
/// </para>
/// </remarks>
public interface IFastenerValidationService : IReferenceValidationService<FastenerDefinition>
{
}
