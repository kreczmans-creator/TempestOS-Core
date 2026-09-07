using Tempest.Core.ReferenceData;

namespace Tempest.Core.Standards;

/// <summary>
/// Standards-specific data-quality validation — the rules a standard
/// register entry must satisfy to be trustworthy reference data.
/// </summary>
/// <remarks>
/// The surface is <see cref="IReferenceValidationService{TDefinition}"/>,
/// shared with every Group A library; what is standards-specific is the
/// rule set behind it (<see cref="StandardValidationRules"/>).
/// <para>
/// Every rule below asks whether the <em>record</em> is coherent. None
/// asks whether the standard is appropriate, current enough, or applicable
/// to a design — those are contractual and regulatory judgements A2 has no
/// authority to make.
/// </para>
/// </remarks>
public interface IStandardValidationService : IReferenceValidationService<StandardDefinition>
{
}
