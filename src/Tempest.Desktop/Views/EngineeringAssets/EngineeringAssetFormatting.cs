using Tempest.Core.EngineeringAssets;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Desktop.Views.EngineeringAssets;

/// <summary>
/// One piece of engineering evidence, flattened out of whichever
/// calculation pack, template or verification artefact cited it — the
/// Engineering Assets area's own "Engineering evidence" list (`WP 21.2B`,
/// scope item 1). Nothing here is a second store: every row is read fresh
/// from the owning record's own <see cref="AssetGovernanceFacts.Evidence"/>
/// (or, for a verification artefact, its own dedicated
/// <c>Verification.VerificationArtefact.Evidence</c> as well), never
/// cached and never written back.
/// </summary>
/// <param name="Kind">What sort of evidence it is.</param>
/// <param name="Description">What it shows.</param>
/// <param name="IsLocatable">Whether it names a document, a record pin, or a reference — something somebody could actually go and find.</param>
/// <param name="IsIndependent">Whether it is independent of the person asserting the claim.</param>
/// <param name="CitedByKind">The owning record's own kind — "Calculation pack", "Template" or "Verification artefact".</param>
/// <param name="CitedByRecordId">The owning record's own catalogue identity — what <c>Open</c> navigates to.</param>
/// <param name="CitedByReference">The owning record's own engineering reference.</param>
public sealed record EngineeringEvidenceRow(
    EngineeringEvidenceKind Kind,
    string Description,
    bool IsLocatable,
    bool IsIndependent,
    string CitedByKind,
    string CitedByRecordId,
    string CitedByReference)
{
    /// <summary>The single line a row shows.</summary>
    public string Label =>
        $"{Kind} — {Description}  ·  {(IsLocatable ? "locatable" : "NOT LOCATABLE")}  ·  "
        + $"{(IsIndependent ? "independent" : "internal")}  ·  cited by {CitedByKind} {CitedByReference}";
}

/// <summary>
/// Renders the governance facts, applicability and validation every
/// engineering asset carries (`AssetGovernanceFacts`, `AssetApplicability`)
/// as plain text — the same "the view renders, nothing decides" discipline
/// every other Desktop read surface in this codebase follows. Shared by
/// the calculation pack, template and verification artefact detail panes
/// so the three record kinds a Work Package brief calls "the same
/// governance facts" actually render identically.
/// </summary>
internal static class EngineeringAssetFormatting
{
    /// <summary>Where and when an asset applies, in one line — "Unrestricted" where <see cref="AssetApplicability.IsRestricted"/> is <see langword="false"/>.</summary>
    public static string DescribeApplicability(AssetApplicability applicability)
    {
        if (!applicability.IsRestricted)
            return "Unrestricted — applies to every discipline, project and subject.";

        var parts = new List<string>();

        if (applicability.Disciplines.Count > 0)
            parts.Add($"disciplines: {string.Join(", ", applicability.Disciplines)}");

        if (applicability.ProjectIdentifiers.Count > 0)
            parts.Add($"projects: {string.Join(", ", applicability.ProjectIdentifiers)}");

        if (applicability.SubjectKinds.Count > 0)
            parts.Add($"subjects: {string.Join(", ", applicability.SubjectKinds)}");

        if (applicability.Validity is { } validity)
            parts.Add($"effective {validity.From:yyyy-MM-dd} to {(validity.To is { } end ? end.ToString("yyyy-MM-dd") : "open")}");

        if (applicability.Conditions.Count > 0)
            parts.Add($"conditions: {string.Join("; ", applicability.Conditions)}");

        return string.Join("  ·  ", parts);
    }

    /// <summary>Who owns, authored, reviewed and approved an asset, in a few short lines.</summary>
    public static IReadOnlyList<string> DescribeGovernance(AssetGovernanceFacts governance)
    {
        var lines = new List<string>
        {
            $"Owner: {governance.Ownership?.OwnerPrincipalId ?? "not named"}",
            $"Author: {governance.Authorship?.AuthoredByPrincipalId ?? "not named"}",
            governance.Reviews.Count == 0
                ? "Reviews: none"
                : $"Reviews: {governance.Reviews.Count}, latest {governance.LatestReview!.Outcome} by {governance.LatestReview.ReviewedByPrincipalId}",
            $"Approval: {(governance.IsApproved ? $"approved by {governance.Approval!.PrincipalId} on {governance.Approval.GrantedOn:yyyy-MM-dd}" : "not approved")}",
            $"Classification: {governance.Classification}",
            $"Evidence: {governance.Evidence.Count} item(s)",
        };

        return lines;
    }

    /// <summary>A validation result as plain lines — every error and warning named by its own rule code, so "a failing validation names its rule" is literally true on screen.</summary>
    public static IReadOnlyList<string> DescribeValidation(IValidationResult result)
    {
        var lines = new List<string> { result.IsValid ? "Valid — no errors." : $"Invalid — {result.Errors.Count} error(s)." };

        foreach (var error in result.Errors)
            lines.Add($"ERROR [{error.Code}] {error.Message}");

        foreach (var warning in result.Warnings)
            lines.Add($"Warning [{warning.Code}] {warning.Message}");

        if (result.Errors.Count == 0 && result.Warnings.Count == 0)
            lines.Add("No warnings either.");

        return lines;
    }

    /// <summary>Every evidence item a calculation pack, template or verification artefact's own governance facts carry, as rows citing the owning record.</summary>
    public static IEnumerable<EngineeringEvidenceRow> RowsFor(
        string citedByKind, string citedByRecordId, string citedByReference, AssetGovernanceFacts governance) =>
        governance.Evidence.Select(e => new EngineeringEvidenceRow(
            e.Kind, e.Description, e.IsLocatable, e.IsIndependent, citedByKind, citedByRecordId, citedByReference));
}
