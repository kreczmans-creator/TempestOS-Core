using System.Globalization;
using Tempest.Core.Calculations;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.App.Engineering;

/// <summary>One governed material, as the Engineering Calculation surface should present it.</summary>
/// <param name="RecordId">The record's own identity in the Materials library.</param>
/// <param name="Designation">The engineering designation, e.g. <c>6082</c>.</param>
/// <param name="Name">The record's full name.</param>
/// <param name="ValidationState">Where the record stands in its lifecycle.</param>
/// <param name="RevisionNumber">The revision currently held.</param>
/// <param name="IsUsableForEngineering">Whether engineering work may rely on it — <see langword="true"/> only when Released.</param>
/// <param name="StateExplanation">Why, in a sentence an engineer can read.</param>
/// <param name="Provenance">Where the values came from, and whether anybody has checked them.</param>
/// <param name="ReviewerPrincipalId">Who verified it, where somebody has.</param>
/// <param name="VerificationDate">When, where somebody has.</param>
public sealed record BracketMaterialOption(
    string RecordId,
    string Designation,
    string Name,
    ReferenceValidationState ValidationState,
    int RevisionNumber,
    bool IsUsableForEngineering,
    string StateExplanation,
    string Provenance,
    string? ReviewerPrincipalId,
    DateOnly? VerificationDate)
{
    /// <summary>The one-line label a picker should show.</summary>
    public string Label => $"{Designation} — {Name} ({ValidationState}, rev {RevisionNumber})";

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>
/// What the engineer typed, before it is anything else.
/// </summary>
/// <remarks>
/// Strings, deliberately. The surface collects text and this type is the
/// one place that decides whether the text is an engineering input — so
/// the same wording rejects the same value whether it arrives from a view
/// or from a test, and the view is never the thing holding the rule.
/// </remarks>
/// <param name="MaterialRecordId">The governed material selected.</param>
/// <param name="LoadKilonewtons">Axial load, in kN.</param>
/// <param name="SectionAreaSquareMillimetres">Minimum resisting cross-sectional area, in mm squared.</param>
/// <param name="MemberLengthMillimetres">Member length, in mm, for the mass estimate.</param>
/// <param name="MassLimitGrams">The heaviest the member may be, in g.</param>
public sealed record BracketCalculationInputs(
    string MaterialRecordId,
    string LoadKilonewtons,
    string SectionAreaSquareMillimetres,
    string MemberLengthMillimetres,
    string MassLimitGrams)
{
    /// <summary>Builds the governed request, or explains every reason it cannot.</summary>
    /// <remarks>
    /// Reports <b>all</b> the problems rather than the first: an engineer
    /// correcting four fields one round-trip at a time is being made to do
    /// the form's work.
    /// </remarks>
    /// <param name="request">The governed request, where the input is sound.</param>
    /// <param name="problems">Every reason it is not, where it is not.</param>
    /// <returns><see langword="true"/> where a request was built.</returns>
    public bool TryBuildRequest(out GovernedBracketCheckRequest? request, out IReadOnlyList<string> problems)
    {
        var found = new List<string>();

        if (string.IsNullOrWhiteSpace(MaterialRecordId))
            found.Add("Select a governed material.");

        var load = Positive(LoadKilonewtons, "Load", "kN", found);
        var area = Positive(SectionAreaSquareMillimetres, "Section area", "mm2", found);
        var length = Positive(MemberLengthMillimetres, "Member length", "mm", found);
        var massLimit = Positive(MassLimitGrams, "Mass limit", "g", found);

        if (found.Count > 0)
        {
            request = null;
            problems = found;
            return false;
        }

        request = new GovernedBracketCheckRequest(
            MaterialRecordId,
            new Quantity<Force>(load!.Value, ForceUnits.Kilonewton),
            new Quantity<Area>(area!.Value, AreaUnits.SquareMillimetre),
            new Quantity<Length>(length!.Value, LengthUnits.Millimetre),
            new Quantity<Mass>(massLimit!.Value, MassUnits.Gram));
        problems = [];
        return true;
    }

    private static double? Positive(string text, string field, string unit, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            problems.Add($"{field} is required, in {unit}.");
            return null;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            problems.Add($"{field} must be a number, in {unit}. '{text.Trim()}' is not.");
            return null;
        }

        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            problems.Add($"{field} must be greater than zero, in {unit}.");
            return null;
        }

        return value;
    }
}

/// <summary>What the Engineering Calculation surface should show after a run, or after recovering one.</summary>
/// <param name="Performed">Whether a calculation actually happened.</param>
/// <param name="Problems">Everything wrong with the input, where the surface rejected it before calculating.</param>
/// <param name="RefusalReason">Why the platform refused, where it did — a governance answer, not an error.</param>
/// <param name="CalculationRecordId">The persisted record's own identity.</param>
/// <param name="CalculationId">Which calculation ran.</param>
/// <param name="CalculationRevision">The record's own revision.</param>
/// <param name="ExecutedAt">When it ran.</param>
/// <param name="ExecutedByPrincipalId">Who ran it.</param>
/// <param name="AppliedStress">Applied direct stress, formatted with its unit.</param>
/// <param name="AllowableStress">The material's allowable stress, formatted with its unit.</param>
/// <param name="Density">The material's density, formatted with its unit.</param>
/// <param name="StressMargin">Margin of safety on stress.</param>
/// <param name="EstimatedMass">Estimated member mass, formatted with its unit.</param>
/// <param name="MassLimit">The mass limit it was checked against, formatted with its unit.</param>
/// <param name="StressCriterionMet">Whether the stress criterion was met.</param>
/// <param name="MassCriterionMet">Whether the mass criterion was met.</param>
/// <param name="MeetsCriteria">Whether both were.</param>
/// <param name="OutcomeLabel">The outcome in words. Never "approved" — see <see cref="BracketCheckOutcome"/>.</param>
/// <param name="MaterialRecordId">The material the result stood on.</param>
/// <param name="MaterialLibrary">The library that material came from.</param>
/// <param name="PinnedRevision">The revision the result is pinned to.</param>
/// <param name="CurrentRevision">The revision that record is at now, or <see langword="null"/> if it is no longer held.</param>
/// <param name="MaterialHasMovedOn">Whether the material has been revised since this result was computed.</param>
/// <param name="MaterialStateNow">Where that material stands in its lifecycle now.</param>
/// <param name="Provenance">Where the material's values came from.</param>
/// <param name="ReviewerPrincipalId">Who verified the material.</param>
/// <param name="VerificationDate">When they verified it.</param>
/// <param name="Assumptions">Every assumption the calculation declared.</param>
public sealed record BracketCalculationOutcome(
    bool Performed,
    IReadOnlyList<string> Problems,
    string? RefusalReason,
    Guid CalculationRecordId,
    string CalculationId,
    int CalculationRevision,
    DateTimeOffset ExecutedAt,
    string ExecutedByPrincipalId,
    string AppliedStress,
    string AllowableStress,
    string Density,
    string StressMargin,
    string EstimatedMass,
    string MassLimit,
    bool StressCriterionMet,
    bool MassCriterionMet,
    bool MeetsCriteria,
    string OutcomeLabel,
    string MaterialRecordId,
    string MaterialLibrary,
    int PinnedRevision,
    int? CurrentRevision,
    bool MaterialHasMovedOn,
    string MaterialStateNow,
    string Provenance,
    string? ReviewerPrincipalId,
    DateOnly? VerificationDate,
    IReadOnlyList<string> Assumptions)
{
    /// <summary>The surface rejected the input before asking the platform for anything.</summary>
    /// <param name="problems">Every reason it was rejected.</param>
    public static BracketCalculationOutcome Rejected(IReadOnlyList<string> problems) =>
        Empty() with { Problems = problems };

    /// <summary>The platform refused, and said why.</summary>
    /// <param name="reason">The refusal, in a sentence an engineer can read.</param>
    public static BracketCalculationOutcome Refused(string reason) =>
        Empty() with { RefusalReason = reason };

    private static BracketCalculationOutcome Empty() =>
        new(false, [], null, Guid.Empty, string.Empty, 0, default, string.Empty,
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            false, false, false, string.Empty,
            string.Empty, string.Empty, 0, null, false, string.Empty, string.Empty, null, null, []);
}

/// <summary>The last calculation, as remembered between launches.</summary>
/// <remarks>
/// One settings value in a deliberately dull, greppable format rather than
/// JSON: it holds a record id and four numbers a person typed, and a schema
/// is a liability for something that small. An entry that cannot be parsed
/// is treated as absent, so a value written by a later build can never
/// crash an earlier one.
/// </remarks>
/// <param name="RecordId">The persisted calculation record to recover.</param>
/// <param name="Inputs">The figures the engineer last entered.</param>
internal sealed record RememberedCalculation(Guid RecordId, BracketCalculationInputs Inputs)
{
    private const char Separator = '|';

    /// <summary>Renders this entry as the single string the settings store holds.</summary>
    public string ToStorage() => string.Join(Separator, [
        RecordId.ToString("N"),
        Inputs.MaterialRecordId,
        Inputs.LoadKilonewtons,
        Inputs.SectionAreaSquareMillimetres,
        Inputs.MemberLengthMillimetres,
        Inputs.MassLimitGrams,
    ]);

    /// <summary>Reads an entry back, treating anything unrecognisable as absent.</summary>
    /// <param name="stored">The stored value.</param>
    /// <param name="remembered">The entry, where one could be read.</param>
    /// <returns><see langword="true"/> where an entry was read.</returns>
    public static bool TryParse(string? stored, out RememberedCalculation? remembered)
    {
        remembered = null;

        if (string.IsNullOrWhiteSpace(stored))
            return false;

        var parts = stored.Split(Separator);

        if (parts.Length != 6 || !Guid.TryParseExact(parts[0], "N", out var recordId))
            return false;

        remembered = new RememberedCalculation(
            recordId, new BracketCalculationInputs(parts[1], parts[2], parts[3], parts[4], parts[5]));
        return true;
    }
}
