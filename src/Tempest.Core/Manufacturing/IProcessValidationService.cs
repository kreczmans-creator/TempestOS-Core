using Tempest.Core.ReferenceData;

namespace Tempest.Core.Manufacturing;

/// <summary>
/// Manufacturing-specific data-quality validation — the rules a process
/// reference record must satisfy to be trustworthy engineering data.
/// </summary>
/// <remarks>
/// The surface is <see cref="IReferenceValidationService{TDefinition}"/>,
/// shared with every Group A library; what is manufacturing-specific is the
/// rule set behind it (<see cref="ProcessValidationRules"/>).
/// <para>
/// Every rule asks whether the record is internally coherent and properly
/// attributed. None asks whether a process is the right one for anything,
/// whether a capability band is achievable in practice, or whether a
/// supplier could hold it.
/// </para>
/// </remarks>
public interface IProcessValidationService : IReferenceValidationService<ProcessDefinition>
{
}
