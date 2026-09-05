using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Bearings;

/// <summary>Evaluates a <see cref="BearingQuery"/> against one bearing record.</summary>
/// <remarks>
/// A pure, allocation-light predicate kept out of
/// <see cref="BearingCatalog"/> so that query semantics can be tested
/// directly, without a store — the same separation
/// <c>RequirementStatusTransitions</c> makes for its own table.
/// </remarks>
internal static class BearingQueryEvaluator
{
    public static bool Matches(IBearing bearing, BearingQuery query)
    {
        var definition = bearing.Definition;
        var identity = definition.Identity;

        if (query.Manufacturer is not null
            && !string.Equals(identity.Manufacturer.Trim(), query.Manufacturer.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.PartNumberContains is not null
            && !identity.ManufacturerPartNumber.Contains(query.PartNumberContains, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.DesignationContains is not null
            && (identity.Designation is null
                || !identity.Designation.Contains(query.DesignationContains, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.Series is not null
            && (identity.Series is null
                || !string.Equals(identity.Series.Trim(), query.Series.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;

        if (query.Families.Count > 0 && !query.Families.Contains(definition.Family))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(bearing.ValidationState))
            return false;

        if (!WithinRange(definition.Geometry.Bore, query.BoreMinimum, query.BoreMaximum))
            return false;

        if (!WithinRange(definition.Geometry.OutsideDiameter, query.OutsideDiameterMinimum, query.OutsideDiameterMaximum))
            return false;

        if (!WithinRange(definition.Geometry.Width, query.WidthMinimum, query.WidthMaximum))
            return false;

        if (!AtLeast(definition.LoadRatings?.BasicDynamicRadial?.Value, query.BasicDynamicRadialMinimum))
            return false;

        if (!AtLeast(definition.LoadRatings?.BasicStaticRadial?.Value, query.BasicStaticRadialMinimum))
            return false;

        if (!AtMost(definition.Mass, query.MassMaximum))
            return false;

        if (!MatchesSpeed(definition, query))
            return false;

        if (query.Sealing is not null
            && (definition.Configuration?.Sealing is null || definition.Configuration.Sealing.Type != query.Sealing))
            return false;

        if (query.InternalClearanceClass is not null
            && !EqualsIgnoringCase(definition.Configuration?.InternalClearanceClass, query.InternalClearanceClass))
            return false;

        if (query.PrecisionClass is not null
            && !EqualsIgnoringCase(definition.Configuration?.PrecisionClass, query.PrecisionClass))
            return false;

        if (query.ReferencesMaterialId is not null)
        {
            var referenced = definition.Construction?.ReferencedMaterialIds ?? [];
            if (!referenced.Any(id => string.Equals(id, query.ReferencesMaterialId, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        if (query.ConstructionClass is not null
            && (definition.Construction is null || definition.Construction.Class != query.ConstructionClass))
            return false;

        return true;
    }

    private static bool MatchesSpeed(BearingDefinition definition, BearingQuery query)
    {
        if (query.SpeedMinimum is null)
            return true;

        var threshold = Canonical(query.SpeedMinimum.Value);

        // A bearing that records no speed rating of the requested kind
        // does not match — an unrecorded speed is never read as "fast
        // enough", and never read as zero either.
        var candidates = query.SpeedRatingKind is null
            ? definition.SpeedRatings
            : definition.SpeedRatings.Where(r => r.Kind == query.SpeedRatingKind);

        return candidates.Any(rating => rating.Rating.CanonicalValue >= threshold);
    }

    private static bool WithinRange<TDimension>(Quantity<TDimension>? value, Quantity<TDimension>? minimum, Quantity<TDimension>? maximum)
        where TDimension : IDimension =>
        AtLeast(value, minimum) && AtMost(value, maximum);

    private static bool AtLeast<TDimension>(Quantity<TDimension>? value, Quantity<TDimension>? minimum)
        where TDimension : IDimension
    {
        if (minimum is null)
            return true;

        return value is not null && Canonical(value.Value) >= Canonical(minimum.Value);
    }

    private static bool AtMost<TDimension>(Quantity<TDimension>? value, Quantity<TDimension>? maximum)
        where TDimension : IDimension
    {
        if (maximum is null)
            return true;

        return value is not null && Canonical(value.Value) <= Canonical(maximum.Value);
    }

    /// <summary>The value in its own dimension's base unit, so two quantities recorded in different units compare correctly.</summary>
    private static double Canonical<TDimension>(Quantity<TDimension> quantity)
        where TDimension : IDimension =>
        quantity.Value * quantity.Unit.ToBaseUnitFactor;

    private static bool EqualsIgnoringCase(string? candidate, string expected) =>
        candidate is not null && string.Equals(candidate.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
}
