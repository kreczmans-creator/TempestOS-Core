using Tempest.Core.Bearings;
using Tempest.Core.Fasteners;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>Which governed reference library a calculation input is pinned to.</summary>
public enum ReferenceLibrary
{
    /// <summary>The Materials library (<see cref="IMaterialCatalog"/>).</summary>
    Materials,

    /// <summary>The Fasteners library (<see cref="IFastenerCatalog"/>).</summary>
    Fasteners,

    /// <summary>The Bearings library (<see cref="IBearingCatalog"/>).</summary>
    Bearings,
}

/// <summary>Why a reference property could not be read for a calculation.</summary>
/// <remarks>Mirrors <see cref="BracketCheckRefusal"/>'s own four reasons, for the same four situations, in any of the three libraries.</remarks>
public enum ReferencePropertyRefusal
{
    /// <summary>The property was read.</summary>
    None,

    /// <summary>No record with the requested Id is registered.</summary>
    RecordNotFound,

    /// <summary>The record exists but is not <see cref="ReferenceValidationState.Released"/>.</summary>
    RecordNotReleased,

    /// <summary>The released record does not carry the property.</summary>
    RequiredPropertyMissing,

    /// <summary>The record carries the property in a different dimension from the one the calculation needs.</summary>
    PropertyDimensionWrong,
}

/// <summary>
/// One reference property, read from a released record for a calculation,
/// or the reason it could not be.
/// </summary>
/// <param name="Refusal">Why the read failed, or <see cref="ReferencePropertyRefusal.None"/>.</param>
/// <param name="Reason">The refusal, in words a reader can act on; <see langword="null"/> when the read succeeded.</param>
/// <param name="Pin">The exact record and revision the value came from; set whenever the record was found.</param>
/// <param name="Value">The property, in the record's own unit; <see langword="null"/> when refused.</param>
/// <param name="Provenance">Where the record's data came from; set whenever the record was found.</param>
public sealed record ReferencePropertyReading<TDimension>(
    ReferencePropertyRefusal Refusal,
    string? Reason,
    ReferencePin? Pin,
    Quantity<TDimension>? Value,
    ReferenceProvenance? Provenance)
    where TDimension : IDimension
{
    /// <summary>Whether the property was read.</summary>
    public bool Succeeded => Refusal == ReferencePropertyRefusal.None && Value is not null && Pin is not null;
}

/// <summary>One text or choice fact read from a released record, or the reason it could not be.</summary>
public sealed record ReferenceTextReading(ReferencePropertyRefusal Refusal, string? Reason, ReferencePin? Pin, string? Value)
{
    /// <summary>Whether the fact was read.</summary>
    public bool Succeeded => Refusal == ReferencePropertyRefusal.None && Value is not null && Pin is not null;
}

/// <summary>
/// Reads a well-known material property from a released record in
/// <see cref="IMaterialCatalog"/>, pinned to the revision it came from,
/// the one way a calculation module's material inputs are meant to be built.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/>
/// must be pure, so no definition can look a material up. The caller
/// resolves the property beforehand and passes it in, with its
/// <see cref="ReferencePin"/>, exactly as <see cref="GovernedBracketCheckService"/>
/// does for the bracket check. That service keeps its reader private;
/// the modules need the same four refusals, so the reader lives here,
/// public, once, beside its fastener and bearing siblings
/// (<see cref="FastenerPropertyReader"/>, <see cref="BearingPropertyReader"/>).
/// </para>
/// <para>
/// <b>What it refuses.</b> A record that does not exist, a record nobody
/// has released, a property the record does not carry, and a property
/// carried in the wrong dimension: each a distinct
/// <see cref="ReferencePropertyRefusal"/> with a reason. It never
/// substitutes a typical figure: a number no source stands behind is worse
/// than no number.
/// </para>
/// </remarks>
public static class MaterialPropertyReader
{
    /// <summary>Reads <paramref name="propertyName"/> from the released material <paramref name="materialRecordId"/> as a <typeparamref name="TDimension"/> quantity.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="materials"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="materialRecordId"/> or <paramref name="propertyName"/> is empty or whitespace.</exception>
    public static async Task<ReferencePropertyReading<TDimension>> ReadAsync<TDimension>(
        IMaterialCatalog materials,
        string materialRecordId,
        string propertyName,
        CancellationToken cancellationToken = default)
        where TDimension : IDimension
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentException.ThrowIfNullOrWhiteSpace(materialRecordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var (record, refusal) = await ReferenceRecords.FindReleasedAsync(materials, materialRecordId, cancellationToken).ConfigureAwait(false);
        if (record is null || refusal is not null)
            return refusal!.As<TDimension>();

        var pin = ReferencePin.For(materials.LibraryName, record);

        if (!record.Definition.Properties.TryGetValue(propertyName, out var recorded))
        {
            return new ReferencePropertyReading<TDimension>(
                ReferencePropertyRefusal.RequiredPropertyMissing,
                $"Material '{record.Id}' records no {propertyName}, which this calculation requires. The value is absent "
                + "from the record, so there is nothing to calculate with — and substituting a typical figure would "
                + "produce a number no source stands behind.",
                pin, null, record.Provenance);
        }

        if (recorded.Value is not Quantity<TDimension> typed)
        {
            return new ReferencePropertyReading<TDimension>(
                ReferencePropertyRefusal.PropertyDimensionWrong,
                $"Material '{record.Id}' records {propertyName} as a {recorded.DimensionName}, but this calculation "
                + $"needs a {typeof(TDimension).Name}. The recorded value cannot be used.",
                pin, null, record.Provenance);
        }

        return new ReferencePropertyReading<TDimension>(ReferencePropertyRefusal.None, null, pin, typed, record.Provenance);
    }
}

/// <summary>
/// Reads a mechanical property, or the property class, from a released
/// fastener record in <see cref="IFastenerCatalog"/> (`WP 21.7C`).
/// </summary>
/// <remarks>
/// The shipped fastener seed is geometry only, by its own note: no property
/// class, proof strength or stress area is recorded because no readable
/// source for ISO 898-1's tables was obtainable. A joint calculation must
/// not treat such a record as sufficient, and this reader says so: it
/// refuses with <see cref="ReferencePropertyRefusal.RequiredPropertyMissing"/>
/// rather than guessing a class. A record an engineer adds or revises with
/// its mechanical properties reads straight through.
/// </remarks>
public static class FastenerPropertyReader
{
    /// <summary>The property names this reader knows, each the <see cref="FastenerMechanicalProperties"/> member of that name.</summary>
    public static IReadOnlyList<string> QuantityProperties { get; } =
    [
        nameof(FastenerMechanicalProperties.ProofStrength),
        nameof(FastenerMechanicalProperties.TensileStrength),
        nameof(FastenerMechanicalProperties.YieldStrength),
        nameof(FastenerMechanicalProperties.ProofLoad),
        nameof(FastenerMechanicalProperties.MinimumBreakingLoad),
        nameof(FastenerMechanicalProperties.StressArea),
    ];

    /// <summary>The text facts this reader knows: the property class and the designation.</summary>
    public static IReadOnlyList<string> TextProperties { get; } = [nameof(FastenerMechanicalProperties.PropertyClass), nameof(FastenerDefinition.Designation)];

    /// <summary>Reads the mechanical property <paramref name="propertyName"/> from the released fastener <paramref name="recordId"/>.</summary>
    public static async Task<ReferencePropertyReading<TDimension>> ReadAsync<TDimension>(
        IFastenerCatalog fasteners, string recordId, string propertyName, CancellationToken cancellationToken = default)
        where TDimension : IDimension
    {
        ArgumentNullException.ThrowIfNull(fasteners);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var (record, refusal) = await ReferenceRecords.FindReleasedAsync(fasteners, recordId, cancellationToken).ConfigureAwait(false);
        if (record is null || refusal is not null)
            return refusal!.As<TDimension>();

        var pin = ReferencePin.For(fasteners.LibraryName, record);
        var mechanical = record.Definition.Mechanical;

        object? recorded = propertyName switch
        {
            nameof(FastenerMechanicalProperties.ProofStrength) => mechanical.ProofStrength?.Value,
            nameof(FastenerMechanicalProperties.TensileStrength) => mechanical.TensileStrength?.Value,
            nameof(FastenerMechanicalProperties.YieldStrength) => mechanical.YieldStrength?.Value,
            nameof(FastenerMechanicalProperties.ProofLoad) => mechanical.ProofLoad?.Value,
            nameof(FastenerMechanicalProperties.MinimumBreakingLoad) => mechanical.MinimumBreakingLoad?.Value,
            nameof(FastenerMechanicalProperties.StressArea) => mechanical.StressArea?.Value,
            _ => null,
        };

        return ReferenceRecords.Typed<TDimension>(recorded, "Fastener", record.Id, propertyName, pin, record.Provenance,
            "The shipped fastener seed is geometry only; a record revised with its ISO 898-1 mechanical properties reads through.");
    }

    /// <summary>Reads the property class or the designation of the released fastener <paramref name="recordId"/>.</summary>
    public static async Task<ReferenceTextReading> ReadTextAsync(IFastenerCatalog fasteners, string recordId, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fasteners);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var (record, refusal) = await ReferenceRecords.FindReleasedAsync(fasteners, recordId, cancellationToken).ConfigureAwait(false);
        if (record is null || refusal is not null)
            return new ReferenceTextReading(refusal!.Refusal, refusal.Reason, refusal.Pin, null);

        var pin = ReferencePin.For(fasteners.LibraryName, record);
        var value = propertyName switch
        {
            nameof(FastenerMechanicalProperties.PropertyClass) => record.Definition.Mechanical.PropertyClass is { } propertyClass
                ? $"ISO 898-1 class {propertyClass}, {record.Definition.Designation}"
                : null,
            nameof(FastenerDefinition.Designation) => record.Definition.Designation,
            _ => null,
        };

        return value is null
            ? new ReferenceTextReading(
                ReferencePropertyRefusal.RequiredPropertyMissing,
                $"Fastener '{record.Id}' records no {propertyName}. The shipped fastener seed is geometry only; a record revised with its property class reads through.",
                pin, null)
            : new ReferenceTextReading(ReferencePropertyRefusal.None, null, pin, value);
    }
}

/// <summary>
/// Reads a load rating, the designation or the rolling-element type from a
/// released bearing record in <see cref="IBearingCatalog"/> (`WP 21.7C`).
/// </summary>
public static class BearingPropertyReader
{
    /// <summary>The choice this reader answers for <see cref="RollingBearingType"/>: the family's rolling element.</summary>
    public const string RollingElementProperty = "RollingElement";

    /// <summary>The load ratings this reader knows, each the <see cref="BearingLoadRatings"/> member of that name.</summary>
    public static IReadOnlyList<string> QuantityProperties { get; } =
    [
        nameof(BearingLoadRatings.BasicDynamicRadial),
        nameof(BearingLoadRatings.BasicStaticRadial),
        nameof(BearingLoadRatings.BasicDynamicAxial),
        nameof(BearingLoadRatings.BasicStaticAxial),
        nameof(BearingLoadRatings.FatigueLoadLimit),
    ];

    /// <summary>Reads the load rating <paramref name="propertyName"/> from the released bearing <paramref name="recordId"/>.</summary>
    public static async Task<ReferencePropertyReading<TDimension>> ReadAsync<TDimension>(
        IBearingCatalog bearings, string recordId, string propertyName, CancellationToken cancellationToken = default)
        where TDimension : IDimension
    {
        ArgumentNullException.ThrowIfNull(bearings);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var (record, refusal) = await ReferenceRecords.FindReleasedAsync(bearings, recordId, cancellationToken).ConfigureAwait(false);
        if (record is null || refusal is not null)
            return refusal!.As<TDimension>();

        var pin = ReferencePin.For(bearings.LibraryName, record);
        var ratings = record.Definition.LoadRatings;

        object? recorded = propertyName switch
        {
            nameof(BearingLoadRatings.BasicDynamicRadial) => ratings?.BasicDynamicRadial?.Value,
            nameof(BearingLoadRatings.BasicStaticRadial) => ratings?.BasicStaticRadial?.Value,
            nameof(BearingLoadRatings.BasicDynamicAxial) => ratings?.BasicDynamicAxial?.Value,
            nameof(BearingLoadRatings.BasicStaticAxial) => ratings?.BasicStaticAxial?.Value,
            nameof(BearingLoadRatings.FatigueLoadLimit) => ratings?.FatigueLoadLimit?.Value,
            _ => null,
        };

        return ReferenceRecords.Typed<TDimension>(recorded, "Bearing", record.Id, propertyName, pin, record.Provenance, null);
    }

    /// <summary>Reads the designation, or the rolling element ("Ball" or "Roller") of the released bearing <paramref name="recordId"/>.</summary>
    public static async Task<ReferenceTextReading> ReadTextAsync(IBearingCatalog bearings, string recordId, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bearings);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);

        var (record, refusal) = await ReferenceRecords.FindReleasedAsync(bearings, recordId, cancellationToken).ConfigureAwait(false);
        if (record is null || refusal is not null)
            return new ReferenceTextReading(refusal!.Refusal, refusal.Reason, refusal.Pin, null);

        var pin = ReferencePin.For(bearings.LibraryName, record);
        var definition = record.Definition;
        var value = propertyName switch
        {
            nameof(BearingIdentity.Designation) => definition.Identity.Designation ?? definition.Identity.ManufacturerPartNumber,
            RollingElementProperty => definition.Family switch
            {
                BearingFamily.DeepGrooveBall or BearingFamily.AngularContactBall or BearingFamily.SelfAligningBall or BearingFamily.ThrustBall => nameof(RollingBearingType.Ball),
                BearingFamily.CylindricalRoller or BearingFamily.TaperedRoller or BearingFamily.SphericalRoller or BearingFamily.NeedleRoller or BearingFamily.ThrustRoller => nameof(RollingBearingType.Roller),
                _ => null,
            },
            _ => null,
        };

        return value is null
            ? new ReferenceTextReading(ReferencePropertyRefusal.RequiredPropertyMissing, $"Bearing '{record.Id}' records no {propertyName} this calculation can use (family {definition.Family}).", pin, null)
            : new ReferenceTextReading(ReferencePropertyRefusal.None, null, pin, value);
    }
}

/// <summary>The two steps every reader shares: find a released record, and type a recorded value.</summary>
internal static class ReferenceRecords
{
    public static async Task<(IReferenceRecord<TDefinition>? Record, ReferencePropertyReading<Dimensionless>? Refusal)> FindReleasedAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalogue, string recordId, CancellationToken cancellationToken)
        where TDefinition : class
    {
        var record = await catalogue.FindAsync(recordId, cancellationToken).ConfigureAwait(false);

        if (record is null)
        {
            return (null, new ReferencePropertyReading<Dimensionless>(
                ReferencePropertyRefusal.RecordNotFound,
                $"No record '{recordId}' is registered in {catalogue.LibraryName}.",
                null, null, null));
        }

        if (record.ValidationState != ReferenceValidationState.Released)
        {
            return (null, new ReferencePropertyReading<Dimensionless>(
                ReferencePropertyRefusal.RecordNotReleased,
                $"{catalogue.LibraryName} record '{record.Id}' is {record.ValidationState}, not Released. Engineering work may not "
                + "rely on reference data nobody has verified against its source.",
                ReferencePin.For(catalogue.LibraryName, record), null, record.Provenance));
        }

        return (record, null);
    }

    public static ReferencePropertyReading<TDimension> Typed<TDimension>(
        object? recorded, string library, string recordId, string propertyName, ReferencePin pin, ReferenceProvenance provenance, string? absenceNote)
        where TDimension : IDimension
    {
        if (recorded is null)
        {
            return new ReferencePropertyReading<TDimension>(
                ReferencePropertyRefusal.RequiredPropertyMissing,
                $"{library} '{recordId}' records no {propertyName}, which this calculation requires. {absenceNote ?? "The value is absent from the record, so there is nothing to calculate with."}",
                pin, null, provenance);
        }

        if (recorded is not Quantity<TDimension> typed)
        {
            return new ReferencePropertyReading<TDimension>(
                ReferencePropertyRefusal.PropertyDimensionWrong,
                $"{library} '{recordId}' records {propertyName} as a {recorded.GetType().Name}, but this calculation needs a {typeof(TDimension).Name}.",
                pin, null, provenance);
        }

        return new ReferencePropertyReading<TDimension>(ReferencePropertyRefusal.None, null, pin, typed, provenance);
    }
}

/// <summary>Re-types the shared find step's refusal for the dimension a reader was asked for.</summary>
internal static class ReferencePropertyReadingExtensions
{
    public static ReferencePropertyReading<TDimension> As<TDimension>(this ReferencePropertyReading<Dimensionless> reading)
        where TDimension : IDimension =>
        new(reading.Refusal, reading.Reason, reading.Pin, null, reading.Provenance);
}
