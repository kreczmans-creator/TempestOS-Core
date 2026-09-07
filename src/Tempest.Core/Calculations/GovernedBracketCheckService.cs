using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations;

/// <summary>Why a governed bracket check could not be performed.</summary>
public enum BracketCheckRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No material is registered under the requested identity.</summary>
    MaterialNotFound,

    /// <summary>The material exists but has not been released, so engineering work may not rely on it.</summary>
    MaterialNotReleased,

    /// <summary>The material does not record a property the check requires.</summary>
    RequiredPropertyMissing,

    /// <summary>A required property is recorded, but carries the wrong physical dimension.</summary>
    PropertyDimensionWrong,
}

/// <summary>
/// The outcome of asking for a governed bracket check: either a completed
/// calculation, or a refusal that says exactly what was missing.
/// </summary>
/// <remarks>
/// A refusal is a first-class answer here rather than an exception, because
/// "this material has no density" is an ordinary engineering finding a
/// surface should show, not an error condition. Genuinely invalid
/// arithmetic inputs — a negative load, a zero area — still throw, because
/// those are programming errors rather than data gaps.
/// </remarks>
/// <param name="Refusal">Why no calculation was performed, or <see cref="BracketCheckRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Record">The persisted calculation record. <see langword="null"/> when the check was refused.</param>
/// <param name="MaterialPin">The record and revision the properties came from. <see langword="null"/> when the material could not be resolved.</param>
/// <param name="MaterialProvenance">Where the pinned material's own values came from. <see langword="null"/> when the material could not be resolved.</param>
public sealed record GovernedBracketCheck(
    BracketCheckRefusal Refusal,
    string? Reason,
    CalculationRecord<BracketSectionCheckResult>? Record,
    ReferencePin? MaterialPin,
    ReferenceProvenance? MaterialProvenance)
{
    /// <summary>Whether a calculation was actually performed.</summary>
    public bool WasPerformed => Refusal == BracketCheckRefusal.None && Record is not null;

    /// <summary>The result, or <see langword="null"/> where the check was refused.</summary>
    public BracketSectionCheckResult? Result => Record?.Result;
}

/// <summary>What to check, and against which governed material.</summary>
/// <param name="MaterialRecordId">The material record to take allowable stress and density from.</param>
/// <param name="AppliedLoad">The axial load the section carries.</param>
/// <param name="SectionArea">The minimum cross-sectional area resisting it.</param>
/// <param name="MemberLength">The member's length, for the mass estimate.</param>
/// <param name="MassLimit">The heaviest the member may be.</param>
public sealed record GovernedBracketCheckRequest(
    string MaterialRecordId,
    Quantity<Force> AppliedLoad,
    Quantity<Area> SectionArea,
    Quantity<Length> MemberLength,
    Quantity<Mass> MassLimit);

/// <summary>
/// The governed entry point to <see cref="BracketSectionCheckCalculationDefinition"/>:
/// resolves a material through its own catalogue, refuses if the data does
/// not support the check, and otherwise executes and records it.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the seam the platform was missing.</b> Every calculation that
/// already existed took a bare <c>Quantity&lt;Pressure&gt;</c> for a
/// material property, so nothing recorded where the number came from. The
/// engine cannot do that resolution itself —
/// <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/> must stay
/// pure — so something has to stand between the catalogue and the
/// calculation. This is that something, for this one calculation.
/// </para>
/// <para>
/// <b>It is not a framework.</b> There is one service for one calculation,
/// with the property names it needs written into it. A generic
/// "resolve any property for any calculation" layer would have to guess at
/// dimensions, at what to do when a property is missing, and at which
/// lifecycle states each calculation tolerates — three questions that have
/// different right answers per calculation. When a second calculation needs
/// the same shape, the shape can be extracted then, from two real examples
/// rather than one imagined one.
/// </para>
/// <para>
/// <b>Released only, and it says why.</b> A Draft material is refused, not
/// silently used and not quietly downgraded to a warning. That is the whole
/// point of the reference lifecycle, and a calculation is precisely the
/// place where relying on unverified data would do harm.
/// </para>
/// </remarks>
public sealed class GovernedBracketCheckService
{
    private readonly IMaterialCatalog _materials;
    private readonly ICalculationEngine _engine;

    /// <summary>Initialises a new instance of the <see cref="GovernedBracketCheckService"/> class.</summary>
    /// <param name="materials">The Materials Library the check resolves against.</param>
    /// <param name="engine">The calculation engine, which must already have <see cref="BracketSectionCheckCalculationDefinition"/> registered.</param>
    public GovernedBracketCheckService(IMaterialCatalog materials, ICalculationEngine engine)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(engine);

        _materials = materials;
        _engine = engine;
    }

    /// <summary>The material property the allowable stress is taken from.</summary>
    public const string AllowableStressProperty = MaterialPropertyNames.YieldStrength;

    /// <summary>The material property the mass estimate is taken from.</summary>
    public const string DensityProperty = MaterialPropertyNames.Density;

    /// <summary>Resolves the material, then performs and records the check.</summary>
    /// <param name="request">What to check.</param>
    /// <param name="cancellationToken">A token observed throughout.</param>
    /// <returns>The completed check, or a refusal saying what was missing.</returns>
    /// <exception cref="CalculationInputInvalidException">An arithmetic input that must be positive is zero or negative.</exception>
    public async Task<GovernedBracketCheck> CheckAsync(
        GovernedBracketCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MaterialRecordId);

        var record = await _materials.FindAsync(request.MaterialRecordId, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return Refuse(
                BracketCheckRefusal.MaterialNotFound,
                $"No material '{request.MaterialRecordId}' is registered in {_materials.LibraryName}.");
        }

        var pin = ReferencePin.For(_materials.LibraryName, record);

        if (record.ValidationState != ReferenceValidationState.Released)
        {
            return Refuse(
                BracketCheckRefusal.MaterialNotReleased,
                $"Material '{record.Id}' is {record.ValidationState}, not Released. Engineering work may not "
                + "rely on reference data nobody has verified against its source.",
                pin,
                record.Provenance);
        }

        if (!TryReadProperty<Pressure>(record, AllowableStressProperty, out var allowable, out var stressFailure))
            return Refuse(stressFailure!.Value.Refusal, stressFailure.Value.Reason, pin, record.Provenance);

        if (!TryReadProperty<MassDensity>(record, DensityProperty, out var density, out var densityFailure))
            return Refuse(densityFailure!.Value.Refusal, densityFailure.Value.Reason, pin, record.Provenance);

        var executed = await _engine.ExecuteAsync<BracketSectionCheckInput, BracketSectionCheckResult>(
            BracketSectionCheckCalculationDefinition.Id,
            new BracketSectionCheckInput(
                pin,
                allowable,
                density,
                request.AppliedLoad,
                request.SectionArea,
                request.MemberLength,
                request.MassLimit),
            cancellationToken).ConfigureAwait(false);

        return new GovernedBracketCheck(BracketCheckRefusal.None, null, executed, pin, record.Provenance);
    }

    private bool TryReadProperty<TDimension>(
        IReferenceRecord<MaterialDefinition> record,
        string propertyName,
        out Quantity<TDimension> value,
        out (BracketCheckRefusal Refusal, string Reason)? failure)
        where TDimension : IDimension
    {
        value = default;

        if (!record.Definition.Properties.TryGetValue(propertyName, out var recorded))
        {
            failure = (
                BracketCheckRefusal.RequiredPropertyMissing,
                $"Material '{record.Id}' records no {propertyName}, which this check requires. "
                + "The value is absent from the record, so there is nothing to calculate with — and "
                + "substituting a typical figure would produce a number no source stands behind.");
            return false;
        }

        // A recorded value of the wrong dimension is a data defect, not a
        // conversion problem. Reading a density where a pressure belongs
        // would give an answer that is numerically plausible and physically
        // meaningless, so it is refused rather than coerced.
        if (recorded.Value is not Quantity<TDimension> typed)
        {
            failure = (
                BracketCheckRefusal.PropertyDimensionWrong,
                $"Material '{record.Id}' records {propertyName} as a {recorded.DimensionName}, but this "
                + $"check needs a {typeof(TDimension).Name}. The recorded value cannot be used.");
            return false;
        }

        value = typed;
        failure = null;
        return true;
    }

    private static GovernedBracketCheck Refuse(
        BracketCheckRefusal refusal,
        string reason,
        ReferencePin? pin = null,
        ReferenceProvenance? provenance = null) =>
        new(refusal, reason, null, pin, provenance);
}
