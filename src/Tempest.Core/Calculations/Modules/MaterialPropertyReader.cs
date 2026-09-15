using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>Why a material property could not be read for a calculation.</summary>
/// <remarks>Mirrors <see cref="BracketCheckRefusal"/>'s own four reasons, for the same four situations.</remarks>
public enum MaterialPropertyRefusal
{
    /// <summary>The property was read.</summary>
    None,

    /// <summary>No record with the requested Id is registered.</summary>
    MaterialNotFound,

    /// <summary>The record exists but is not <see cref="ReferenceValidationState.Released"/>.</summary>
    MaterialNotReleased,

    /// <summary>The released record does not carry the property.</summary>
    RequiredPropertyMissing,

    /// <summary>The record carries the property in a different dimension from the one the calculation needs.</summary>
    PropertyDimensionWrong,
}

/// <summary>
/// One material property, read from a released record for a calculation —
/// or the reason it could not be.
/// </summary>
/// <param name="Refusal">Why the read failed, or <see cref="MaterialPropertyRefusal.None"/>.</param>
/// <param name="Reason">The refusal, in words a reader can act on; <see langword="null"/> when the read succeeded.</param>
/// <param name="Pin">The exact record and revision the value came from; set whenever the record was found.</param>
/// <param name="Value">The property, in the record's own unit; <see langword="null"/> when refused.</param>
/// <param name="Provenance">Where the record's data came from; set whenever the record was found.</param>
public sealed record MaterialPropertyReading<TDimension>(
    MaterialPropertyRefusal Refusal,
    string? Reason,
    ReferencePin? Pin,
    Quantity<TDimension>? Value,
    ReferenceProvenance? Provenance)
    where TDimension : IDimension
{
    /// <summary>Whether the property was read.</summary>
    public bool Succeeded => Refusal == MaterialPropertyRefusal.None && Value is not null && Pin is not null;
}

/// <summary>
/// Reads a well-known material property from a released record in
/// <see cref="IMaterialCatalog"/>, pinned to the revision it came from —
/// the one way a `WP 21.7A` module's material inputs are meant to be built.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/>
/// must be pure, so no definition can look a material up. The caller
/// resolves the property beforehand and passes it in, with its
/// <see cref="ReferencePin"/>, exactly as <see cref="GovernedBracketCheckService"/>
/// does for the bracket check. That service keeps its reader private;
/// eleven modules need the same four refusals, so the reader lives here,
/// public, once.
/// </para>
/// <para>
/// <b>What it refuses.</b> A record that does not exist, a record nobody
/// has released, a property the record does not carry, and a property
/// carried in the wrong dimension — each a distinct
/// <see cref="MaterialPropertyRefusal"/> with a reason. It never
/// substitutes a typical figure: a number no source stands behind is worse
/// than no number.
/// </para>
/// </remarks>
public static class MaterialPropertyReader
{
    /// <summary>Reads <paramref name="propertyName"/> from the released material <paramref name="materialRecordId"/> as a <typeparamref name="TDimension"/> quantity.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="materials"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="materialRecordId"/> or <paramref name="propertyName"/> is empty or whitespace.</exception>
    public static async Task<MaterialPropertyReading<TDimension>> ReadAsync<TDimension>(
        IMaterialCatalog materials,
        string materialRecordId,
        string propertyName,
        CancellationToken cancellationToken = default)
        where TDimension : IDimension
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentException.ThrowIfNullOrWhiteSpace(materialRecordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var record = await materials.FindAsync(materialRecordId, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return new MaterialPropertyReading<TDimension>(
                MaterialPropertyRefusal.MaterialNotFound,
                $"No material '{materialRecordId}' is registered in {materials.LibraryName}.",
                null, null, null);
        }

        var pin = ReferencePin.For(materials.LibraryName, record);

        if (record.ValidationState != ReferenceValidationState.Released)
        {
            return new MaterialPropertyReading<TDimension>(
                MaterialPropertyRefusal.MaterialNotReleased,
                $"Material '{record.Id}' is {record.ValidationState}, not Released. Engineering work may not rely on "
                + "reference data nobody has verified against its source.",
                pin, null, record.Provenance);
        }

        if (!record.Definition.Properties.TryGetValue(propertyName, out var recorded))
        {
            return new MaterialPropertyReading<TDimension>(
                MaterialPropertyRefusal.RequiredPropertyMissing,
                $"Material '{record.Id}' records no {propertyName}, which this calculation requires. The value is absent "
                + "from the record, so there is nothing to calculate with — and substituting a typical figure would "
                + "produce a number no source stands behind.",
                pin, null, record.Provenance);
        }

        if (recorded.Value is not Quantity<TDimension> typed)
        {
            return new MaterialPropertyReading<TDimension>(
                MaterialPropertyRefusal.PropertyDimensionWrong,
                $"Material '{record.Id}' records {propertyName} as a {recorded.DimensionName}, but this calculation "
                + $"needs a {typeof(TDimension).Name}. The recorded value cannot be used.",
                pin, null, record.Provenance);
        }

        return new MaterialPropertyReading<TDimension>(MaterialPropertyRefusal.None, null, pin, typed, record.Provenance);
    }
}
