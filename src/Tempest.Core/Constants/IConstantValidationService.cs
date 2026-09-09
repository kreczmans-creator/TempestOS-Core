using Tempest.Core.ReferenceData;

namespace Tempest.Core.Constants;

/// <summary>
/// Constants-specific data-quality validation — the rules an engineering
/// constant must satisfy to be a value anything may rely on.
/// </summary>
/// <remarks>
/// The surface is <see cref="IReferenceValidationService{TDefinition}"/>,
/// shared with every Group A library; what is constants-specific is the
/// rule set behind it (<see cref="ConstantValidationRules"/>).
/// <para>
/// These rules matter more than most in Group A, because a constant is the
/// one kind of reference data that gets used without being looked at. A
/// bad bearing dimension is noticed when the bearing does not fit; a bad
/// constant propagates silently into every calculation that consumed it.
/// </para>
/// </remarks>
public interface IConstantValidationService : IReferenceValidationService<ConstantDefinition>
{
}
